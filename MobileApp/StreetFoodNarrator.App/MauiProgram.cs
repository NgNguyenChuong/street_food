using Microsoft.Extensions.Logging;
using SkiaSharp.Views.Maui.Controls.Hosting;
using StreetFoodNarrator.App.Core.Models;
using StreetFoodNarrator.App.Core.Services;
using StreetFoodNarrator.App.Core.Services.Implementations;
using StreetFoodNarrator.App.Views;
using StreetFoodNarrator.App.ViewModels;

namespace StreetFoodNarrator.App;

public static class MauiProgram
{
    public static IServiceProvider Services { get; private set; } = null!;
    
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseSkiaSharp()          // registers SKGLView handler required by Mapsui
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                fonts.AddFont("MaterialDesignIcons.ttf", "MDI");
            });

        // ── Register Services ──────────────────────────────────────
        builder.Services.AddSingleton<LanguageService>();
        builder.Services.AddSingleton<UserSession>();
        builder.Services.AddSingleton<ILocalDatabaseService, LocalDatabaseService>();
        builder.Services.AddSingleton<IZoneRepository, ZoneRepository>();
        builder.Services.AddSingleton<IAudioService, AudioService>();
        builder.Services.AddSingleton<IGeofenceService, GeofenceService>();
        builder.Services.AddSingleton<IReviewService, ReviewService>();
        
        // HttpClient for API calls – keep generous timeout to avoid false negatives on emulator/mobile networks
        builder.Services.AddSingleton(new HttpClient { Timeout = TimeSpan.FromSeconds(AppConfig.NetworkTimeoutSeconds) });

        // Audio cache: tải trước file MP3 offline cho từng POI
        builder.Services.AddSingleton<IAudioCacheService, AudioCacheService>();

        // Offline data sync: size calculation, download, update check
        builder.Services.AddSingleton<DataSyncService>();

        // Text-to-Speech with Edge-TTS API (+ offline cache fallback)
        builder.Services.AddSingleton<ITTSService, TextToSpeechService>();

        // Voice packages for offline TTS (3 languages: vi, en, zh)
        builder.Services.AddSingleton<IVoicePackageService, VoicePackageService>();

        // Virtual tour completion prompt system
        builder.Services.AddSingleton<ITourEngagementService, TourEngagementService>();
        builder.Services.AddSingleton<IPopupService, PopupService>();
        builder.Services.AddSingleton<IVirtualTourViewModel, VirtualTourViewModel>();
        builder.Services.AddSingleton<IOfflineRoutingService, OfflineRoutingService>();

        // Location: use Simulated GPS by default (great for emulator)
        if (AppConfig.UseSimulatedGPS)
        {
            var simService = new SimulatedLocationService();
            builder.Services.AddSingleton<ILocationService>(simService);
            builder.Services.AddSingleton(simService);
        }
        else
        {
            builder.Services.AddSingleton<ILocationService, LocationService>();
        }

        // ── Register Pages & ViewModels ────────────────────────────
        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<ExploreMapPage>();
        builder.Services.AddTransient<SettingsPage>();

#if DEBUG
        builder.Logging.AddDebug();
        builder.Logging.SetMinimumLevel(LogLevel.Debug);
#endif

        var app = builder.Build();
        Services = app.Services;

        // ── Global exception handlers (catch remaining unhandled crashes) ──
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            var ex = args.ExceptionObject as Exception;
            System.Diagnostics.Debug.WriteLine($"[CRASH] UnhandledException: {ex}");
            try
            {
                var logPath = Path.Combine(FileSystem.AppDataDirectory, "crash_log.txt");
                var crashLog = $"[{DateTime.Now}] CRASH Unhandled: {ex?.Message}\n{ex?.StackTrace}\n\n";
                File.AppendAllText(logPath, crashLog);
                System.Diagnostics.Debug.WriteLine($"[CRASH LOGGED TO {logPath}]");
            }
            catch { /* safe */ }
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            System.Diagnostics.Debug.WriteLine($"[CRASH] UnobservedTask: {args.Exception}");
            try
            {
                var logPath = Path.Combine(FileSystem.AppDataDirectory, "crash_log.txt");
                var crashLog = $"[{DateTime.Now}] CRASH Task: {args.Exception?.Message}\n{args.Exception?.StackTrace}\n\n";
                File.AppendAllText(logPath, crashLog);
            }
            catch { /* safe */ }
            args.SetObserved(); // Prevent process termination for fire-and-forget task faults
        };

        return app;
    }
}
