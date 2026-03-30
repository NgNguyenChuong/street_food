using Microsoft.Extensions.DependencyInjection;
using StreetFoodNarrator.App.Views;
using System.Collections.Generic;

namespace StreetFoodNarrator.App;

public partial class AppShell : Shell
{
    private MainPage? _cachedMainPage;

    public AppShell(bool isOnboarding = false)
    {
        InitializeComponent();

        if (isOnboarding)
        {
            WelcomeShellContent.ContentTemplate = new DataTemplate(() => new WelcomePage());
        }

        // Cache MainPage once so navigation is instant (no DI resolution each time)
        // DEFENSIVE: If DI fails, create MainPage without injected dependencies
        MapShellContent.ContentTemplate = new DataTemplate(() =>
        {
            if (_cachedMainPage != null)
                return _cachedMainPage;

            try
            {
                System.Diagnostics.Debug.WriteLine("[AppShell] Attempting to resolve MainPage via DI...");
                _cachedMainPage = ResolveActiveServices().GetRequiredService<MainPage>();
                System.Diagnostics.Debug.WriteLine("[AppShell] ✓ Successfully resolved MainPage via DI");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AppShell] ✗ Failed to resolve MainPage via DI ({ex.GetType().Name}): {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[AppShell] Creating MainPage without DI services (will use parameterless constructor)...");
                try
                {
                    // Fallback: Try creating MainPage without DI - it will self-resolve services
                    _cachedMainPage = new MainPage();
                    System.Diagnostics.Debug.WriteLine("[AppShell] ✓ MainPage created with self-resolution");
                }
                catch (Exception fallbackEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[AppShell] ✗ MainPage creation failed even with fallback: {fallbackEx.Message}");
                    throw new InvalidOperationException("Failed to create MainPage - both DI and fallback failed.", fallbackEx);
                }
            }

            return _cachedMainPage;
        });

        Routing.RegisterRoute("POIDetailPage", typeof(POIDetailPage));

        // SettingsPage route for non-modal shell navigation paths
        Routing.RegisterRoute("SettingsPage", typeof(SettingsPage));
    }

    /// <summary>
    /// Returns a fresh SettingsPage instance for modal navigation.
    /// Reusing one modal page instance can cause blank-screen navigation issues.
    /// </summary>
    public SettingsPage GetCachedSettingsPage()
    {
        return ResolveActiveServices().GetRequiredService<SettingsPage>();
    }

    private static IServiceProvider ResolveActiveServices()
    {
        var providers = new IServiceProvider?[]
        {
            Application.Current?.Handler?.MauiContext?.Services,
            Application.Current?.Windows.FirstOrDefault()?.Page?.Handler?.MauiContext?.Services,
            MauiProgram.Services
        };

        List<Exception> errors = new();
        foreach (var provider in providers)
        {
            if (provider == null) continue;
            try
            {
                System.Diagnostics.Debug.WriteLine($"[ServiceProvider] Checking provider availability...");
                _ = provider.GetService<IServiceProvider>();
                System.Diagnostics.Debug.WriteLine($"[ServiceProvider] ✓ Provider is available");
                return provider;
            }
            catch (ObjectDisposedException ode)
            {
                // Try next available provider.
                System.Diagnostics.Debug.WriteLine($"[ServiceProvider] Provider stale: {ode.Message}");
                errors.Add(ode);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ServiceProvider] Provider check failed: {ex.GetType().Name}: {ex.Message}");
                errors.Add(ex);
            }
        }

        System.Diagnostics.Debug.WriteLine($"[ServiceProvider] ✗ No active service provider found after checking {errors.Count + 1} provider(s)");
        throw new InvalidOperationException("No active service provider is available.", errors.FirstOrDefault());
    }
}
