using System;
using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;
using Akavache;
using System.Reactive;
using System.Reactive.Linq;
using ReactiveUI.Android;
using Microsoft.WindowsAzure.MobileServices;

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

            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.Login);
        }
    }
}


