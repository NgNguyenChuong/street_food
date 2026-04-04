using Microsoft.Extensions.DependencyInjection;
using StreetFoodNarrator.App.Views;
using System.Collections.Generic;

namespace StreetFoodNarrator.App;

public partial class AppShell : Shell
{
    private MainPage? _cachedMainPage;
    private SavedPage? _cachedSavedPage;
    private SettingsPage? _cachedSettingsTabPage;
    public static string PreviousNonSettingsRoute { get; private set; } = string.Empty;
    public static string LastNonSettingsRoute { get; private set; } = "//MapPage";

    public AppShell(bool isOnboarding = false)
    {
        InitializeComponent();

        if (isOnboarding)
        {
            WelcomeShellContent.ContentTemplate = new DataTemplate(() => new WelcomePage());
        }
        else
        {
            WelcomeShellContent.IsVisible = false;
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

        SavedShellContent.ContentTemplate = new DataTemplate(() =>
        {
            _cachedSavedPage ??= new SavedPage();
            return _cachedSavedPage;
        });

        SettingsShellContent.ContentTemplate = new DataTemplate(() =>
        {
            _cachedSettingsTabPage ??= new SettingsPage();
            return _cachedSettingsTabPage;
        });

        Routing.RegisterRoute("POIDetailPage", typeof(POIDetailPage));

        // SettingsPage route for non-modal shell navigation paths
        Routing.RegisterRoute("SettingsPage", typeof(SettingsPage));

        Navigated += OnShellNavigated;
    }

    private void OnShellNavigated(object? sender, ShellNavigatedEventArgs e)
    {
        try
        {
            var location = CurrentState?.Location?.OriginalString ?? string.Empty;
            if (string.IsNullOrWhiteSpace(location))
                return;

            if (!location.Contains("SettingsPage", StringComparison.OrdinalIgnoreCase))
            {
                PreviousNonSettingsRoute = LastNonSettingsRoute;
                LastNonSettingsRoute = location;
            }
        }
        catch
        {
            // Ignore route tracking errors; back logic has fallback routes.
        }
    }

    /// <summary>
    /// Returns a fresh SettingsPage instance for modal navigation.
    /// Reusing one modal page instance can cause blank-screen navigation issues.
    /// </summary>
    public SettingsPage GetCachedSettingsPage()
    {
        return ResolveActiveServices().GetRequiredService<SettingsPage>();
    }

    // Tối ưu hóa DI tránh vòng lặp Exception chậm chạp
    private static IServiceProvider ResolveActiveServices()
    {
        return MauiProgram.Services;
    }
}
