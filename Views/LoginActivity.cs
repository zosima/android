using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Akavache;
using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;
using Microsoft.WindowsAzure.MobileServices;
using ReactiveUI;
using ReactiveUI.Android;
using Android.Accounts;
using Java.Util.Concurrent;
using Windows.Data.Json;
using System.Reactive.Subjects;
using System.Net.Http;
using System.Text;

namespace Zosima
{
    [Activity(Label = "Zosima", MainLauncher = true)]
    public class LoginActivity : ReactiveActivity<LoginViewModel>
    {
        readonly Subject<Tuple<int, Result, Intent>> activityResult = new Subject<Tuple<int, Result, Intent>>();

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);

            ViewModel = new LoginViewModel(loginWithDeviceAccount);
            ViewModel.Login.Execute(null);

            this.WhenAnyValue(x => x.ViewModel.CurrentClient)
                .Where(x => x != null)
                .SelectMany(async client => {
                    // Set up Push Notifications
                    var regId = await BlobCache.LocalMachine.GetOrFetchObject<string>("push_registration_id", () => Observable.Return(""));

                    if (String.IsNullOrWhiteSpace(regId)) {
                        var regPushIntent = new Intent("com.google.android.c2dm.intent.REGISTER");

                        regPushIntent.SetPackage("com.google.android.gsf");
                        regPushIntent.PutExtra("app", PendingIntent.GetBroadcast(this, 0, new Intent(), 0));
                        regPushIntent.PutExtra("sender", GcmInformation.SenderId);
                        StartService(regPushIntent);
                    }

                    return Unit.Default;
                })
                .Subscribe(_ => this.Log().Info("Set up push notifications"));

            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.Login);
        }

        protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
        {
            base.OnActivityResult(requestCode, resultCode, data);
            activityResult.OnNext(Tuple.Create(requestCode, resultCode, data));
        }

        async Task<MobileServiceUser> loginWithDeviceAccount(MobileServiceClient client)
        {
            var acMgr = AccountManager.Get(this);
            var p = "oauth2:https://www.googleapis.com/auth/userinfo.profile";

            var chooser = AccountManager.NewChooseAccountIntent(null, null, new[] { "com.google" }, false, null, null, null, null);
            StartActivityForResult(chooser, 0);
            var acctIntent = await activityResult.Take(1);

            var acct = acMgr.GetAccountsByType("com.google").FirstOrDefault(x => x.Name == 
                acctIntent.Item3.GetStringExtra(AccountManager.KeyAccountName));

            var bundle = ((Bundle)(await acMgr.GetAuthToken(acct, p, null, this, null, null).GetResultAsync(30, TimeUnit.Seconds)));
            var keys = bundle.KeySet().ToArray();
            var token = bundle.GetString(AccountManager.KeyAuthtoken);

            var hc = new HttpClient();
            var tokenContent = new JsonObject();    
            tokenContent.SetNamedValue("access_token", JsonValue.CreateStringValue(token));
            var stringContent = tokenContent.Stringify();

            if (String.IsNullOrWhiteSpace(token)) {
                throw new Exception("Couldn't get OAuth token from device");
            }

            var rq = new HttpRequestMessage(HttpMethod.Post, MobileSiteInfo.SiteUrl + "login/google");
            rq.Content = new StringContent(tokenContent.Stringify(), Encoding.UTF8, "application/json");
            rq.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            rq.Headers.Add("X-ZUMO-APPLICATION", MobileSiteInfo.SiteToken);

            // XXX: This will always return 401 with, 
            // "Error: The POST Google login request must contain both code and id_token in the body of the request."
            var resp = await hc.SendAsync(rq);
            var respContent = await resp.Content.ReadAsStringAsync();

            /*
            var hc = new HttpClient();
            var profileJson = await hc.GetStringAsync(String.Format("https://www.googleapis.com/oauth2/v1/userinfo?alt=json&access_token={0}",
                Uri.EscapeUriString(token)));

            var id = "Google:" + JsonValue.Parse(profileJson).GetObject().GetNamedString("id");

            var root = new JsonObject();
            var user = new JsonObject();
            user.SetNamedValue("userId", JsonValue.CreateStringValue(id));
            root.SetNamedValue("user", user);
            root.SetNamedValue("authenticationToken", JsonValue.CreateStringValue(token));
*/

            return await client.LoginAsync(this, MobileServiceAuthenticationProvider.Google, null);
        }
    }
}
