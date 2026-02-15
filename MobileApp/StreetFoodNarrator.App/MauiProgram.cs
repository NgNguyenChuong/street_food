using Microsoft.Extensions.Logging;
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
            .UseMauiMaps()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // ── Register Services ──────────────────────────────────────
        builder.Services.AddSingleton<UserSession>();
        builder.Services.AddSingleton<ILocalDatabaseService, LocalDatabaseService>();
        builder.Services.AddSingleton<IZoneRepository, ZoneRepository>();
        builder.Services.AddSingleton<IAudioService, AudioService>();
        builder.Services.AddSingleton<IGeofenceService, GeofenceService>();

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
        return app;
    }
}
