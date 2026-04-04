using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Graphics;
using AColor = Android.Graphics.Color;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using AndroidX.Core.View;
using StreetFoodNarrator.App.Core.Utils;

namespace StreetFoodNarrator.App
{
    [Activity(
        Theme = "@style/Maui.SplashTheme",
        MainLauncher = true,
        LaunchMode = LaunchMode.SingleTask,
        ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    [IntentFilter(
        new[] { Intent.ActionView },
        Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
        DataScheme = "streetfood",
        DataHost = "qr")]
    [IntentFilter(
        new[] { Intent.ActionView },
        Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
        DataScheme = "streetfood")]
    [IntentFilter(
        new[] { Intent.ActionView },
        Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
        AutoVerify = true,
        DataScheme = "https",
        DataHost = "streetfoodnarrator.app",
        DataPathPrefix = "/qr")]
    [IntentFilter(
        new[] { Intent.ActionView },
        Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
        AutoVerify = true,
        DataScheme = "https",
        DataHost = "streetfoodnarrator.app",
        DataPathPrefix = "/open")]
    [IntentFilter(
        new[] { Intent.ActionView },
        Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
        AutoVerify = true,
        DataScheme = "https",
        DataHost = "streetfoodnarrator.app",
        DataPathPrefix = "/deeplink")]
    [IntentFilter(
        new[] { Intent.ActionView },
        Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
        DataScheme = "http",
        DataHost = "streetfoodnarrator.app",
        DataPathPrefix = "/qr")]
    [IntentFilter(
        new[] { Intent.ActionView },
        Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
        DataScheme = "http",
        DataHost = "streetfoodnarrator.app",
        DataPathPrefix = "/open")]
    [IntentFilter(
        new[] { Intent.ActionView },
        Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
        DataScheme = "http",
        DataHost = "streetfoodnarrator.app",
        DataPathPrefix = "/deeplink")]
    [IntentFilter(
        new[] { Intent.ActionView },
        Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
        AutoVerify = true,
        DataScheme = "https",
        DataHost = "www.streetfoodnarrator.app",
        DataPathPrefix = "/qr")]
    [IntentFilter(
        new[] { Intent.ActionView },
        Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
        AutoVerify = true,
        DataScheme = "https",
        DataHost = "www.streetfoodnarrator.app",
        DataPathPrefix = "/open")]
    [IntentFilter(
        new[] { Intent.ActionView },
        Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
        AutoVerify = true,
        DataScheme = "https",
        DataHost = "www.streetfoodnarrator.app",
        DataPathPrefix = "/deeplink")]
    [IntentFilter(
        new[] { Intent.ActionView },
        Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
        DataScheme = "http",
        DataHost = "localhost",
        DataPathPrefix = "/qr")]
    [IntentFilter(
        new[] { Intent.ActionView },
        Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
        DataScheme = "http",
        DataHost = "localhost",
        DataPathPrefix = "/open")]
    [IntentFilter(
        new[] { Intent.ActionView },
        Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
        DataScheme = "http",
        DataHost = "localhost",
        DataPathPrefix = "/deeplink")]
    [IntentFilter(
        new[] { Intent.ActionView },
        Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
        DataScheme = "http",
        DataHost = "10.0.2.2",
        DataPathPrefix = "/qr")]
    [IntentFilter(
        new[] { Intent.ActionView },
        Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
        DataScheme = "http",
        DataHost = "10.0.2.2",
        DataPathPrefix = "/open")]
    [IntentFilter(
        new[] { Intent.ActionView },
        Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
        DataScheme = "http",
        DataHost = "10.0.2.2",
        DataPathPrefix = "/deeplink")]
    [IntentFilter(
        new[] { Intent.ActionView },
        Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
        DataScheme = "http",
        DataHost = "127.0.0.1",
        DataPathPrefix = "/qr")]
    [IntentFilter(
        new[] { Intent.ActionView },
        Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
        DataScheme = "http",
        DataHost = "127.0.0.1",
        DataPathPrefix = "/open")]
    [IntentFilter(
        new[] { Intent.ActionView },
        Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
        DataScheme = "http",
        DataHost = "127.0.0.1",
        DataPathPrefix = "/deeplink")]
    [IntentFilter(
        new[] { Intent.ActionView },
        Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
        DataScheme = "http",
        DataHost = "192.168.100.9",
        DataPathPrefix = "/qr")]
    [IntentFilter(
        new[] { Intent.ActionView },
        Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
        DataScheme = "http",
        DataHost = "192.168.100.9",
        DataPathPrefix = "/open")]
    [IntentFilter(
        new[] { Intent.ActionView },
        Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
        DataScheme = "http",
        DataHost = "192.168.100.9",
        DataPathPrefix = "/deeplink")]
    [IntentFilter(
        new[] { Intent.ActionSend },
        Categories = new[] { Intent.CategoryDefault },
        DataMimeType = "text/plain")]
    public class MainActivity : MauiAppCompatActivity
    {
        private const int RequestNotificationPermission = 2001;

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            TryCaptureQrDeepLink(Intent);

            // Edge-to-edge so the map can render under the status bar.
            WindowCompat.SetDecorFitsSystemWindows(Window!, false);
#pragma warning disable CA1416, CA1422 // SetStatusBarColor obsoleted API 35+; safe on our min-SDK
            Window!.SetStatusBarColor(AColor.ParseColor("#0A1612"));
#pragma warning restore CA1416, CA1422

            var insetsController = WindowCompat.GetInsetsController(Window!, Window!.DecorView);
            if (insetsController != null)
                insetsController.AppearanceLightStatusBars = false;

            // Android 13+ (API 33): must request POST_NOTIFICATIONS at runtime
            if (OperatingSystem.IsAndroidVersionAtLeast(33))
            {
                var granted = ContextCompat.CheckSelfPermission(
                    this, global::Android.Manifest.Permission.PostNotifications);
                if (granted != global::Android.Content.PM.Permission.Granted)
                    ActivityCompat.RequestPermissions(
                        this,
                        new[] { global::Android.Manifest.Permission.PostNotifications },
                        RequestNotificationPermission);
            }
        }

        protected override void OnNewIntent(Intent? intent)
        {
            base.OnNewIntent(intent);
            TryCaptureQrDeepLink(intent);
        }

        private static void TryCaptureQrDeepLink(Intent? intent)
        {
            try
            {
                if (intent == null)
                    return;

                var isView = intent.Action == Intent.ActionView;
                var isSend = intent.Action == Intent.ActionSend;
                if (!isView && !isSend)
                    return;

                var rawUrl = intent.DataString;
                if (string.IsNullOrWhiteSpace(rawUrl) && intent.HasExtra(Intent.ExtraText))
                    rawUrl = intent.GetStringExtra(Intent.ExtraText);

                if (string.IsNullOrWhiteSpace(rawUrl) && intent.HasExtra("SCAN_RESULT"))
                    rawUrl = intent.GetStringExtra("SCAN_RESULT");

                if (string.IsNullOrWhiteSpace(rawUrl))
                    return;

                if (!QrDeepLinkManager.TryParse(rawUrl, out _, out _))
                    return;

                QrDeepLinkManager.SavePending(rawUrl);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainActivity] QR deep link capture error: {ex.Message}");
            }
        }
    }
}
