using System;
using System.Reactive;
using System.Reactive.Linq;
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

namespace Zosima
{
    [Activity(Label = "Zosima", MainLauncher = true)]
    public class LoginActivity : ReactiveActivity<LoginViewModel>
    {
        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);

            ViewModel = new LoginViewModel(x => x.LoginAsync(this, MobileServiceAuthenticationProvider.Google));
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
    }
}


