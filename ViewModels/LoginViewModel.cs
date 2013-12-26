using System;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using ReactiveUI;
using Akavache;
using Microsoft.WindowsAzure.MobileServices;
using System.Threading.Tasks;
using Windows.Data.Json;

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
                var client = new MobileServiceClient(MobileSiteInfo.SiteUrl, MobileSiteInfo.SiteToken);
                var user = default(MobileServiceUser);

                while (user == null) {
                    user = await requestInteractiveLogin(client).ToObservable()
                        .LoggedCatch(this, Observable.Return(default(MobileServiceUser)), "Couldn't log in interactively");
                }

                return client;
            });

            newClient.ToProperty(this, x => x.CurrentClient, out currentClient);
        }
    }
}

