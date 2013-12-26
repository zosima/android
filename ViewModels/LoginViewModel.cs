using System;
using System.Reactive;
using System.Reactive.Linq;
using ReactiveUI;
using Akavache;
using Microsoft.WindowsAzure.MobileServices;
using System.Threading.Tasks;

namespace Zosima
{
    public class LoginViewModel : ReactiveObject
    {
        ObservableAsPropertyHelper<MobileServiceClient> currentClient;
        public MobileServiceClient CurrentClient {
            get { return currentClient.Value; }
        } 

        public ReactiveCommand Login { get; set; }

        public LoginViewModel(Func<MobileServiceClient, Task<MobileServiceUser>> requestInteractiveLogin)
        {
            Login = new ReactiveCommand();

            var newClient = Login.RegisterAsyncTask<MobileServiceClient>(async _ => {
                var loginInfo = default(LoginInfo);
                var client = new MobileServiceClient(MobileSiteInfo.SiteUrl, MobileSiteInfo.SiteToken);

            retry:
                loginInfo = await BlobCache.Secure.GetLoginAsync().Catch(Observable.Return(default(LoginInfo)));

                if (loginInfo == null) {
                    // NB: If this dies we're going to let it escape out to 
                    // ThrownExceptions.
                    var user = await requestInteractiveLogin(client);

                    await BlobCache.Secure.SaveLogin("user", user.MobileServiceAuthenticationToken);
                    return client;
                }

                try {
                    await client.LoginAsync(loginInfo.Password);
                    return client;
                } catch (Exception ex) {
                    this.Log().WarnException("Failed to log in using saved info", ex);
                }

                await BlobCache.Secure.EraseLogin();
                goto retry;
            });

            newClient.ToProperty(this, x => x.CurrentClient, out currentClient);
        }
    }
}

