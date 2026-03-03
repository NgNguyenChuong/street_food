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
        
        // HttpClient for API calls – 10s timeout prevents long freezes when offline
        builder.Services.AddSingleton(new HttpClient { Timeout = TimeSpan.FromSeconds(10) });
        
        // Text-to-Speech with Edge-TTS API
        builder.Services.AddSingleton<ITTSService, TextToSpeechService>();

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
        builder.Services.AddTransient<MainViewModel>();
        builder.Services.AddTransient<MainPage>();

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
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            System.Diagnostics.Debug.WriteLine($"[CRASH] UnobservedTask: {args.Exception}");
            args.SetObserved(); // Prevent process termination for fire-and-forget task faults
        };

        return app;
    }
}
