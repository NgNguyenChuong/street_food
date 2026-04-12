using Android.App;
using Android.Runtime;
using StreetFoodNarrator.App.Core.Utils;

namespace StreetFoodNarrator.App
{
    [Application]
    public class MainApplication : MauiApplication
    {
        private static bool _androidUnhandledHooked;

        public MainApplication(IntPtr handle, JniHandleOwnership ownership)
            : base(handle, ownership)
        {
        }

        public override void OnCreate()
        {
            base.OnCreate();

            if (_androidUnhandledHooked)
                return;

            AndroidEnvironment.UnhandledExceptionRaiser += OnAndroidUnhandledException;
            _androidUnhandledHooked = true;
            StartupDiagnostics.AppendMessage("MainApplication.OnCreate", "Android unhandled exception hook registered");
        }

        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

        private static void OnAndroidUnhandledException(object? sender, RaiseThrowableEventArgs e)
        {
            try
            {
                if (e.Exception != null)
                    StartupDiagnostics.Append(e.Exception, "AndroidEnvironment.UnhandledExceptionRaiser");
            }
            catch
            {
                // Diagnostics path must never throw.
            }
        }
    }
}
