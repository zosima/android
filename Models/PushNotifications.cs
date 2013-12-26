using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Akavache;
using ReactiveUI;
using Microsoft.WindowsAzure.MobileServices;

namespace Zosima
{
    public class DeviceRegistration
    {
        public string Token { get; set; }
    }

    [Service]
    public class PushIntentService : IntentService, IEnableLogger
    {
        static readonly object gate = 42;
        static PowerManager.WakeLock wakeLock;  // NB: This is either null, or acquired. Never non-null but unacquired.
        public const string NotificationsDisabledKey = "__didntwork_brah__";

        public static void RunIntentInService(Context context, Intent intent)
        {
            lock (gate) {
                if (wakeLock == null) {
                    wakeLock = PowerManager.FromContext(context).NewWakeLock(WakeLockFlags.Partial, "Zosima Push Notifications");
                }

                wakeLock.Acquire();
            }

            intent.SetClass(context, typeof(PushIntentService));
            context.StartService(intent);
        }

        public static void DropWakeLock()
        {
            lock (gate) {
                if (wakeLock == null) return;
                wakeLock.Release();
                wakeLock = null;
            }
        }

        protected override async void OnHandleIntent(Intent intent)
        {
            try {
                var ctx = ApplicationContext;

                if (intent.Action.Contains("REGISTRATION")) {
                    if (intent.HasExtra("error") && intent.GetStringExtra("error") == "INVALID_PARAMETERS") {
                        this.Log().Warn("This device doesn't support push notifications, won't try again");
                        await BlobCache.LocalMachine.InsertObject("push_registration_id", NotificationsDisabledKey).Finally(() => DropWakeLock());
                        return;
                    }

                    if (!intent.HasExtra("registration_id")) {
                        this.Log().Error("Failed to register for push notifications: " + intent.GetStringExtra("error"));
                        DropWakeLock();
                        return;
                    }

                    var id = intent.GetStringExtra("registration_id");

                    var mobileServiceApi = RxApp.DependencyResolver.GetService<MobileServiceClient>();
                    var devRegTable = mobileServiceApi.GetTable<DeviceRegistration>();
                    await devRegTable.InsertAsync(new DeviceRegistration() { Token = id, });

                    await BlobCache.LocalMachine.InsertObject("push_registration_id", id).Finally(() => DropWakeLock());

                    this.Log().Info("Registered for pushes: {0}", id);
                }

                if (intent.Action.Contains("RECEIVE")) {
                    this.Log().Info("Got a push notification!");
                    foreach(string v in intent.Extras.KeySet()) {
                        this.Log().Info(v);
                    }
                }
            } catch(Exception ex) {
                this.Log().WarnException("Failed to save push info", ex);
                DropWakeLock();
            }
        }
    }

    [BroadcastReceiver(Permission= "com.google.android.c2dm.permission.SEND")]
    [IntentFilter(new string[] { "com.google.android.c2dm.intent.RECEIVE" }, Categories = new string[] {"@PACKAGE_NAME@" })]
    [IntentFilter(new string[] { "com.google.android.c2dm.intent.REGISTRATION" }, Categories = new string[] {"@PACKAGE_NAME@" })]
    [IntentFilter(new string[] { "com.google.android.gcm.intent.RETRY" }, Categories = new string[] { "@PACKAGE_NAME@"})]
    public class GCMBroadcastReceiver : BroadcastReceiver
    {
        const string TAG = "PushHandlerBroadcastReceiver";
        public override void OnReceive(Context context, Intent intent)
        {
            PushIntentService.RunIntentInService(context, intent);
            SetResult(Result.Ok, null, null);
        }
    }

    [BroadcastReceiver]
    [IntentFilter(new[] { Android.Content.Intent.ActionBootCompleted })]
    public class MyGCMBootReceiver : BroadcastReceiver
    {
        public override void OnReceive(Context context, Intent intent)
        {
            PushIntentService.RunIntentInService(context, intent);
            SetResult(Result.Ok, null, null);
        }
    }
}