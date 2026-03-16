using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Graphics;
using AColor = Android.Graphics.Color;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using AndroidX.Core.View;

namespace StreetFoodNarrator.App
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        private const int RequestNotificationPermission = 2001;

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

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
    }
}
