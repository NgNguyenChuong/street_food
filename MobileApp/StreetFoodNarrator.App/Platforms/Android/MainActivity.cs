using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Graphics;
using AColor = Android.Graphics.Color;
using AndroidX.Core.View;

namespace StreetFoodNarrator.App
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            // Edge-to-edge so the map can render under the status bar.
            WindowCompat.SetDecorFitsSystemWindows(Window, false);
            Window.SetStatusBarColor(AColor.Transparent);
        }
    }
}
