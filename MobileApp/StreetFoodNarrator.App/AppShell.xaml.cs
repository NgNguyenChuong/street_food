using Microsoft.Extensions.DependencyInjection;
using StreetFoodNarrator.App.Core.Services;
using StreetFoodNarrator.App.Helpers;
using StreetFoodNarrator.App.Resources.Strings;
using StreetFoodNarrator.App.ViewModels;
using StreetFoodNarrator.App.Views;
using System.Collections.Generic;

namespace StreetFoodNarrator.App;

public partial class AppShell : Shell
{
    private MainPage? _cachedMainPage;
    private SavedPage? _cachedSavedPage;
    private SettingsPage? _cachedSettingsTabPage;
    private readonly MainViewModel _mainViewModel;
    private readonly LanguageService _languageService;
    private readonly DeviceActivityService? _deviceActivityService;
    private IDispatcherTimer? _vipStatusMonitorTimer;
    private bool _isVipStatusRefreshInFlight;
    private bool _isVipExpiryAlertVisible;
    private static readonly TimeSpan VipStatusMonitorInterval = TimeSpan.FromSeconds(20);
    public static string PreviousNonSettingsRoute { get; private set; } = string.Empty;
    public static string LastNonSettingsRoute { get; private set; } = "//MapPage";

    public AppShell(bool isOnboarding = false)
    {
        InitializeComponent();
        _mainViewModel = ResolveActiveServices().GetRequiredService<MainViewModel>();
        _languageService = ResolveActiveServices().GetRequiredService<LanguageService>();
        try { _deviceActivityService = ResolveActiveServices().GetRequiredService<DeviceActivityService>(); }
        catch { _deviceActivityService = null; }
        _mainViewModel.VipSubscriptionExpired += OnVipSubscriptionExpired;
        LanguageService.LanguageChanged += OnLanguageChanged;

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
        Loaded += OnShellLoaded;
        Unloaded += OnShellUnloaded;
        ApplyLocalizedTabTitles();
    }

    private void OnShellLoaded(object? sender, EventArgs e)
    {
        StartVipStatusMonitorTimer();
        _ = RefreshVipStatusSafelyAsync("OnShellLoaded");
        _deviceActivityService?.Start();
    }

    private void OnShellUnloaded(object? sender, EventArgs e)
    {
        StopVipStatusMonitorTimer();
        _deviceActivityService?.Stop();
    }

    private void StartVipStatusMonitorTimer()
    {
        if (_vipStatusMonitorTimer != null || Dispatcher == null)
            return;

        _vipStatusMonitorTimer = Dispatcher.CreateTimer();
        _vipStatusMonitorTimer.Interval = VipStatusMonitorInterval;
        _vipStatusMonitorTimer.Tick += OnVipStatusMonitorTick;
        _vipStatusMonitorTimer.Start();
    }

    private void StopVipStatusMonitorTimer()
    {
        if (_vipStatusMonitorTimer == null)
            return;

        _vipStatusMonitorTimer.Tick -= OnVipStatusMonitorTick;
        _vipStatusMonitorTimer.Stop();
        _vipStatusMonitorTimer = null;
    }

    private async void OnVipStatusMonitorTick(object? sender, EventArgs e)
    {
        if (_isVipStatusRefreshInFlight)
            return;

        _isVipStatusRefreshInFlight = true;
        try
        {
            await _mainViewModel.RefreshVipSubscriptionStatusAsync(force: false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AppShell] VIP status monitor error: {ex.Message}");
        }
        finally
        {
            _isVipStatusRefreshInFlight = false;
        }
    }

    private async Task RefreshVipStatusSafelyAsync(string stage)
    {
        try
        {
            await _mainViewModel.RefreshVipSubscriptionStatusAsync(force: false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AppShell] {stage} error: {ex.Message}");
            App.AppendStartupErrorToLog(ex, $"AppShell.{stage}");
        }
    }

    private async void OnVipSubscriptionExpired(object? sender, EventArgs e)
    {
        if (_isVipExpiryAlertVisible)
            return;

        _isVipExpiryAlertVisible = true;

        // Force tour list to re-lock VIP-gated tours immediately on expiry.
        try { await _mainViewModel.LoadToursAsync(forceSyncNow: false); } catch { }

        try
        {
            var expiresText = _mainViewModel.VipExpiresAtUtc?.ToLocalTime().ToString("dd/MM/yyyy HH:mm")
                ?? UiText("không rõ", "unknown", "未知");

            await CustomAlert.ShowAsync(
                UiText("VIP đã hết hạn", "VIP expired", "VIP 已过期"),
                UiText(
                    $"Gói VIP của bạn đã hết hạn lúc {expiresText}. Vui lòng gia hạn để tiếp tục sử dụng đầy đủ tính năng.",
                    $"Your VIP plan expired at {expiresText}. Please renew to continue using all premium features.",
                    $"您的 VIP 套餐已于 {expiresText} 到期。请续费以继续使用全部高级功能。"),
                UiText("Đã hiểu", "OK", "知道了"),
                AlertType.Warning);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AppShell] VIP expiry alert error: {ex.Message}");
        }
        finally
        {
            _isVipExpiryAlertVisible = false;
        }
    }

    private string UiText(string vi, string en, string zh)
        => _languageService.CurrentLanguage switch
        {
            "en" => en,
            "zh" => zh,
            _ => vi
        };

    private void OnLanguageChanged(object? sender, string languageCode)
    {
        MainThread.BeginInvokeOnMainThread(ApplyLocalizedTabTitles);
    }

    private void ApplyLocalizedTabTitles()
    {
        MapShellContent.Title = AppStrings.Get("Nav_Map");
        SavedShellContent.Title = AppStrings.Get("Nav_Library");
        SettingsShellContent.Title = AppStrings.Get("Nav_Settings");
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
