using Microsoft.Maui.Graphics;
using Microsoft.Maui.Networking;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Storage;
using Microsoft.Extensions.DependencyInjection;
using StreetFoodNarrator.App;
using StreetFoodNarrator.App.Core.Services;
using StreetFoodNarrator.App.Core.Services.Implementations;
using StreetFoodNarrator.App.Helpers;
using StreetFoodNarrator.App.Resources.Strings;
using StreetFoodNarrator.App.ViewModels;
using StreetFoodNarrator.App.Core.Models;
using System.Net;
using System.Text.RegularExpressions;
namespace StreetFoodNarrator.App.Views;

public partial class WelcomePage : ContentPage
{
    private const string AutoOpenInZoneOnNextMainPageKey = "auto_open_inzone_on_next_mainpage";
    private const string LangVi = "vi";
    private const string LangEn = "en";
    private const string LangZh = "zh";
    private const string PREF_FULL_OFFLINE = "has_full_offline";
    private const string PREF_DONT_SHOW_INFO = "dont_show_offline_info_v2";
    private static readonly Regex ProgressPairRegex = new(@"(\d+)\s*/\s*(\d+)", RegexOptions.Compiled);

    private readonly IZoneRepository _repository;
    private readonly IAudioCacheService? _audioCache;
    private readonly DataSyncService _dataSyncService;
    private readonly LanguageService _languageService;
    private bool _flowStarted = false;
    private bool _canStartTour = false;
    private string _currentLang = LangVi;
    private StatusKind _statusKind = StatusKind.None;
    private string? _errorDetails;
    private int _newAudioCount = 0;
    private bool _hasSystemUpdate = false;
    private bool _isNavigatingToMap = false;
    private bool _hasShownOfflineFirstLaunchNotice;

    private enum StatusKind
    {
        None,
        DownloadingMetadata,
        DownloadingOffline,
        Ready,
        NeedInternet,
        Error
    }

    public WelcomePage()
    {
        InitializeComponent();
        _repository      = MauiProgram.Services.GetRequiredService<IZoneRepository>();
        _audioCache      = MauiProgram.Services.GetService<IAudioCacheService>();
        _dataSyncService = MauiProgram.Services.GetRequiredService<DataSyncService>();
        _languageService = MauiProgram.Services.GetRequiredService<LanguageService>();
        var lang = Preferences.Get(AppConfig.LanguagePrefKey, LangVi);
        ApplyLanguage(lang);
    }

    private static AlertType ResolveAlertType(string title)
    {
        var normalized = (title ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized.Contains("lỗi") || normalized.Contains("error") || normalized.Contains("không thể"))
            return AlertType.Error;
        if (normalized.Contains("cần quyền") || normalized.Contains("warning") || normalized.Contains("cảnh báo"))
            return AlertType.Warning;
        return AlertType.Info;
    }

    private new Task DisplayAlertAsync(string title, string message, string cancel)
        => CustomAlert.ShowAsync(title, message, cancel, ResolveAlertType(title));

    private new Task<bool> DisplayAlertAsync(string title, string message, string accept, string cancel)
        => CustomAlert.ShowConfirmAsync(title, message, accept, cancel, ResolveAlertType(title));

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        ApplyLanguage(Preferences.Get(AppConfig.LanguagePrefKey, LangVi));
        UpdateDataSourceLabel();

        _isNavigatingToMap = false;

        // Leave Start button disabled until we know if there is data
        StartButton.IsEnabled = false;

        if (_flowStarted) return;
        _flowStarted = true;

        // Run all heavy operations in background - UI is already responsive!
        _ = Task.Run(async () =>
        {
            try
            {
                await RunSimpleFlowAsync();
                MainThread.BeginInvokeOnMainThread(() => UpdateDataSourceLabel());

                // Background update check — only show popup if user already has offline data
                if (_dataSyncService.HasOfflineData() && IsOnline())
                    await CheckForUpdatesInBackgroundAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WelcomePage] OnAppearing error: {ex}");
                MainThread.BeginInvokeOnMainThread(() => ShowErrorState(ex.Message));
            }
        });
    }
    // AUTO LOAD: Tự động tải metadata ở background 
   
    private async Task RunSimpleFlowAsync()
    {
        var didInitialSync = false;

        // Step 1: load from SQLite cache
        await _repository.LoadLocalAsync();

        if (!_repository.IsSeeded)
        {
            // Step 2: no SQLite data — seed now
            // Online  → downloads from API and saves to SQLite
            // Offline → loads bundled default_pois.json and saves to SQLite
            if (IsOnline())
            {
                MainThread.BeginInvokeOnMainThread(() => ShowDownloadingMetadataState());
            }

            await _repository.SyncFromMongoAsync();
            didInitialSync = true;

            // Always reload from SQLite after sync — ensures _zones matches persisted data
            // regardless of which path SyncFromMongoAsync took (API success / null response / offline)
            await _repository.LoadLocalAsync();

            if (_repository.CurrentDataSource == DataSourceKind.LiveApi)
            {
                // Avoid immediate duplicate sync when MainPage boots right after Welcome.
                Preferences.Set("LastSyncTime", DateTime.UtcNow.ToString("o"));
            }
        }

        if (_repository.IsSeeded)
        {
            MainThread.BeginInvokeOnMainThread(() => 
            {
                ShowReadyState();
                UpdateDataSourceLabel();
            });

            // First-time user might need offline data, but we only ask them when they click Start.
            MainThread.BeginInvokeOnMainThread(() => EnableStartButton());

            // Keep offline assets (menu/audio/map/route) in sync even on first-launch online flow.
            if (IsOnline())
                _ = _dataSyncService.EnsureDeferredOfflineCompletionAsync();

            if (!didInitialSync)
                _ = RunBackgroundSyncAsync();
            // Kiểm tra audio mới ngay sau khi UI sẵn sàng
            CheckAudioUpdatesInBackground();
            return;
        }

        // Truly no data even after bundled seed — very unlikely
        MainThread.BeginInvokeOnMainThread(() => 
        {
            ShowErrorState("Không thể tải dữ liệu địa điểm.");
            EnableStartButton();
        });
    }

    private async Task RunBackgroundSyncAsync()
    {
        try
        {
            if (!IsOnline()) return;
            // ✅ Removed 2 second delay - sync immediately!
            await _repository.SyncFromMongoAsync();
            await _repository.LoadLocalAsync();

            // ✅ Record sync time so MainPage won't re-sync unnecessarily
            if (_repository.CurrentDataSource == DataSourceKind.LiveApi)
                Preferences.Set("LastSyncTime", DateTime.Now.ToString("O"));

            _ = _dataSyncService.EnsureDeferredOfflineCompletionAsync();

            MainThread.BeginInvokeOnMainThread(UpdateDataSourceLabel);
        }
        catch { /* Silent */ }
    }

    // ── Cảnh báo audio mới (ở background) ────────────────────────

    private void CheckAudioUpdatesInBackground()
    {
        if (!IsOnline() || _audioCache == null) return;

        _ = Task.Run(async () =>
        {
            try
            {
                // ✅ Removed 2.5 second delay - check immediately!
                var count = await _audioCache.CheckForUpdatesAsync();

                MainThread.BeginInvokeOnMainThread(() => SetUpdateBadge(count));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WelcomePage] CheckAudioUpdates lỗi: {ex.Message}");
            }
        });
    }

    private void OnBellClicked(object sender, EventArgs e)
        => OnUpdateClicked(sender, e);

    private async void OnUpdateClicked(object sender, EventArgs e)
    {
        // Case 1: Không có mạng → thông báo không tải được
        if (!IsOnline())
        {
            await CustomAlert.ShowAsync(
                AppStrings.Alert_NoNetwork_Title,
                AppStrings.Alert_NoNetwork_Message,
                AppStrings.Common_OK,
                AlertType.Warning);
            return;
        }

        // Case 2: Có mạng → kiểm tra xem đã tải chưa
        // Nếu đã tải rồi (PREF_FULL_OFFLINE) thì check version server
        var alreadyDownloaded = Preferences.Get(PREF_FULL_OFFLINE, false);

        if (alreadyDownloaded)
        {
            // Check server version to see if there's new data
            var (hasUpdate, _) = await _dataSyncService.CheckForUpdatesAsync();

            if (!hasUpdate)
            {
                // Đã tải và server không có gì mới → thông báo đã tải rồi
                await CustomAlert.ShowAsync(
                    "Đã tải rồi",
                    "Dữ liệu offline của bạn đã là phiên bản mới nhất.",
                    "OK",
                    AlertType.Success);
                return;
            }

            // Có bản cập nhật mới → hỏi có muốn đồng bộ không
            var shouldSync = await CustomAlert.ShowConfirmAsync(
                "Có bản cập nhật mới",
                "Server có dữ liệu mới. Đồng bộ ngay để cập nhật?",
                "Đồng bộ ngay",
                AppStrings.Common_Cancel,
                AlertType.Info);
            if (!shouldSync) return;
        }
        else
        {
            // Chưa từng tải offline → hỏi có muốn tải không
            var shouldSync = await CustomAlert.ShowConfirmAsync(
                "Cập nhật dữ liệu",
                "Đồng bộ dữ liệu mới nhất từ web quản lý: quán, menu và audio.",
                "Đồng bộ ngay",
                AppStrings.Common_Cancel,
                AlertType.Info);
            if (!shouldSync) return;
        }

        UpdateButton.IsEnabled = false;
        try
        {
            await DownloadUpdatesAsync();
        }
        finally
        {
            UpdateButton.IsEnabled = true;
        }
    }

    private async Task<string> ShowAudioUpdateDialogAsync(int count)
    {
        var tcs     = new TaskCompletionSource<string>();
        var overlay = new Grid { BackgroundColor = Color.FromArgb("#80000000") };

        var dialog = new Border
        {
            BackgroundColor  = Color.FromArgb("#111E18"),
            Stroke           = Color.FromArgb("#1E3D2A"),
            StrokeThickness  = 1,
            Padding          = new Thickness(24),
            Margin           = new Thickness(32),
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions   = LayoutOptions.Center,
            MaximumWidthRequest = 340,
            StrokeShape      = new RoundRectangle { CornerRadius = new CornerRadius(20) }
        };

        var stack = new VerticalStackLayout { Spacing = 14 };

        // Icon + Tiêu đề
        var titleRow = new HorizontalStackLayout { Spacing = 10 };
        titleRow.Add(new Label
        {
            Text = "🪧", FontSize = 22, FontAttributes = FontAttributes.Bold,
            // TextColor = Color.FromArgb("#F97316"),
            VerticalOptions = LayoutOptions.Center
        });
        titleRow.Add(new Label
        {
            Text = "Có audio mới!",
            FontSize = 20, FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White, VerticalOptions = LayoutOptions.Center
        });
        stack.Add(titleRow);

        stack.Add(new Label
        {
            Text = $"Admin vừa cập nhật {count} file audio thuyết minh mới.\n"
                 + "Tải về ngay để sử dụng offline khi không có mạng.",
            FontSize = 14,
            TextColor = Color.FromArgb("#94A3B8"),
            LineBreakMode = LineBreakMode.WordWrap
        });

        var btnDownload = new Button
        {
            Text = "Tải xuống ngay",
            BackgroundColor = Color.FromArgb("#22C55E"),
            TextColor = Colors.White,
            FontAttributes = FontAttributes.Bold,
            CornerRadius = 12, HeightRequest = 50
        };
        btnDownload.Clicked += (s, e) => { tcs.TrySetResult("download"); overlay.IsVisible = false; };
        stack.Add(btnDownload);

        var btnLater = new Button
        {
            Text = "Để sau",
            BackgroundColor = Colors.Transparent,
            TextColor = Color.FromArgb("#94A3B8"),
            BorderColor = Color.FromArgb("#1E3D2A"),
            BorderWidth = 1,
            CornerRadius = 12, HeightRequest = 44
        };
        btnLater.Clicked += (s, e) => { tcs.TrySetResult("later"); overlay.IsVisible = false; };
        stack.Add(btnLater);

        dialog.Content = stack;
        overlay.Children.Add(dialog);
        var mainGrid = (Grid)this.Content;
        mainGrid.Children.Add(overlay);

        var result = await tcs.Task;
        mainGrid.Children.Remove(overlay);
        return result;
    }

    private async Task ShowNoUpdateDialogAsync()
    {
        var tcs     = new TaskCompletionSource<bool>();
        var overlay = new Grid { BackgroundColor = Color.FromArgb("#80000000") };

        var dialog = new Border
        {
            BackgroundColor  = Color.FromArgb("#111E18"),
            Stroke           = Color.FromArgb("#1E3D2A"),
            StrokeThickness  = 1,
            Padding          = new Thickness(22),
            Margin           = new Thickness(32),
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions   = LayoutOptions.Center,
            MaximumWidthRequest = 320,
            StrokeShape      = new RoundRectangle { CornerRadius = new CornerRadius(20) }
        };

        var stack = new VerticalStackLayout { Spacing = 12 };

        var titleRow = new HorizontalStackLayout { Spacing = 10 };
        titleRow.Add(new Label
        {
            Text = "🪧", FontSize = 20, FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#F97316"),
            VerticalOptions = LayoutOptions.Center
        });
        titleRow.Add(new Label
        {
            Text = "Không có cập nhật",
            FontSize = 18, FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White, VerticalOptions = LayoutOptions.Center
        });
        stack.Add(titleRow);

        stack.Add(new Label
        {
            Text = "Không có audio mới trên server.",
            FontSize = 13,
            TextColor = Color.FromArgb("#94A3B8"),
            LineBreakMode = LineBreakMode.WordWrap
        });

        var btnOk = new Button
        {
            Text = "OK",
            BackgroundColor = Color.FromArgb("#22C55E"),
            TextColor = Colors.White,
            FontAttributes = FontAttributes.Bold,
            CornerRadius = 12, HeightRequest = 46
        };
        btnOk.Clicked += (s, e) => { tcs.TrySetResult(true); overlay.IsVisible = false; };
        stack.Add(btnOk);

        dialog.Content = stack;
        overlay.Children.Add(dialog);
        var mainGrid = (Grid)this.Content;
        mainGrid.Children.Add(overlay);

        await tcs.Task;
        mainGrid.Children.Remove(overlay);
    }

    private async Task DownloadUpdatesAsync()
    {
        var isWifiModern = Connectivity.Current.ConnectionProfiles.Contains(ConnectionProfile.WiFi);
        if (!isWifiModern)
        {
            var continueSync = await CustomAlert.ShowConfirmAsync(
                AppStrings.Alert_UsingCellular_Title,
                AppStrings.Alert_UsingCellular_Message,
                AppStrings.Common_Continue,
                AppStrings.Common_Cancel,
                AlertType.Warning);
            if (!continueSync) return;
        }

        UpdateButton.IsEnabled = false;

        try
        {
            await _dataSyncService.DownloadAllDataAsync(msg =>
                System.Diagnostics.Debug.WriteLine($"[WelcomePage Sync] {msg}"));

            SetUpdateBadge(0);
            UpdateDataSourceLabel();
            await CustomAlert.ShowAsync(
                "Đã cập nhật",
                "Dữ liệu offline đã được cập nhật phiên bản mới nhất.",
                "OK",
                AlertType.Success);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WelcomePage] DownloadUpdates lỗi: {ex.Message}");
            await CustomAlert.ShowAsync(
                "Lỗi cập nhật",
                $"Không thể tải dữ liệu offline.\nChi tiết: {ex.Message}",
                "OK",
                AlertType.Error);
        }
        finally
        {
            UpdateButton.IsEnabled = true;
        }
    }

    // START BUTTON:
    private async void OnStartTourClicked(object sender, EventArgs e)
    {
        if (_isNavigatingToMap)
            return;

        _isNavigatingToMap = true;
        StartButton.IsEnabled = false;

        try
        {
            var alreadyHasOffline = Preferences.Get(PREF_FULL_OFFLINE, false) && _dataSyncService.HasOfflineData();
            var dontShowInfo     = Preferences.Get(PREF_DONT_SHOW_INFO, false);
            if (!alreadyHasOffline && !dontShowInfo && IsOnline())
            {
                var result = await ShowCustomInfoDialog();
                
                if (result == "close")
                {
                    _isNavigatingToMap = false;
                    StartButton.IsEnabled = true;
                    return;
                }

                // Popup handlers already execute "download" and "skip" actions directly.
                // Return here to avoid double download / double navigation races.
                if (result == "download" || result == "skip")
                    return;
            }

            if (!alreadyHasOffline && !IsOnline() && !_hasShownOfflineFirstLaunchNotice)
            {
                _hasShownOfflineFirstLaunchNotice = true;
                await CustomAlert.ShowAsync(
                    "Bạn đang không có mạng",
                    "Ứng dụng sẽ dùng dữ liệu hệ thống đã đóng gói sẵn trên máy để bạn vẫn vào được bản đồ và điểm quán cơ bản. Khi có mạng, vào Cài đặt > Tải dữ liệu offline để tải đầy đủ audio, ảnh và dữ liệu mới nhất.",
                    "Đã hiểu",
                    AlertType.Info);
            }

            // Normal path - no popup needed
            StartLoadingOverlay.IsVisible = true;

            // Request permissions FIRST on the UI thread before navigating to avoid ANR deadlocks
            var hasPermission = await EnsureLocationPermissionFlowAsync();
            if (!hasPermission)
                return;

            // Then navigate
            await NavigateToMapAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WelcomePage] OnStartTourClicked error: {ex}");
            await DisplayAlertAsync("Lỗi", $"Không thể mở bản đồ: {ex.Message}", "OK");
        }
        finally
        {
            _isNavigatingToMap = false;
            StartButton.IsEnabled = true;
            StartLoadingOverlay.IsVisible = false;
        }
    }

    private async Task<bool> EnsureLocationPermissionFlowAsync()
    {
        try
        {
            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (status == PermissionStatus.Granted)
                return true;

            status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            if (status == PermissionStatus.Granted)
                return true;

            var openSettings = await DisplayAlertAsync(
                "Cần quyền vị trí",
                "App cần vị trí để tự động nhận biết bạn đang ở gần quán nào. Bạn vẫn có thể tiếp tục mà không bật vị trí.",
                "Bật lại",
                "Tiếp tục không dùng vị trí");

            if (openSettings)
            {
                AppInfo.ShowSettingsUI();
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WelcomePage] EnsureLocationPermissionFlowAsync error: {ex.Message}");
            return true;
        }
    }

    // ── Background update check ──────────────────────────────────────────────
    private const double AUTO_SYNC_SIZE_THRESHOLD_MB = 5.0;  // Ngưỡng tự sync ngầm

    private async Task CheckForUpdatesInBackgroundAsync()
    {
        try
        {
            // ✅ Removed 2 second delay - check immediately!

            // Check if server has newer version
            var (hasUpdate, newVersion) = await _dataSyncService.CheckForUpdatesAsync();
            if (!hasUpdate) return;

            // ✅ Calculate download size to decide auto-sync or prompt
            (double totalMb, int poiCount, int audioCount, int imageCount, string breakdown) sizeInfo;
            try
            {
                sizeInfo = await _dataSyncService.CalculateDownloadSizeAsync();
            }
            catch
            {
                sizeInfo = (9.8, 13, 39, 52, "• Dữ liệu quán: 0.1 MB\n• Audio 3 ngôn ngữ: 6.5 MB\n• Hình ảnh: 2.7 MB\n• Bản đồ: 0.5 MB");
            }

            MainThread.BeginInvokeOnMainThread(() =>
            {
                _hasSystemUpdate = true;
                UpdateBadgeVisibility();

                // ✅ Size < 5MB: Auto sync in background + badge notification
                if (sizeInfo.totalMb < AUTO_SYNC_SIZE_THRESHOLD_MB)
                {
                    Console.WriteLine($"[WelcomePage] 📦 Update size small ({sizeInfo.totalMb:F1} MB < {AUTO_SYNC_SIZE_THRESHOLD_MB} MB) → Auto sync in background");
                    _ = Task.Run(RunSilentBackgroundUpdateAsync);
                }
                // ✅ Size >= 5MB: Show popup asking user
                else
                {
                    // Suppress intrusive sync popup: keep badge only, user updates manually.
                    Console.WriteLine($"[WelcomePage] 📦 Update size large ({sizeInfo.totalMb:F1} MB >= {AUTO_SYNC_SIZE_THRESHOLD_MB} MB) → badge only");
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WelcomePage] UpdateCheck lỗi: {ex.Message}");
        }
    }

    /// <summary>
    /// ✅ Auto sync in background when update size is small (&lt; 5MB)
    /// Shows badge + optional toast, no popup blocking UI
    /// </summary>
    private async Task RunSilentBackgroundUpdateAsync()
    {
        try
        {
            await _dataSyncService.DownloadAllDataAsync(status =>
            {
                System.Diagnostics.Debug.WriteLine($"[SilentUpdate] {status}");
            });

            // Update completed
            _hasSystemUpdate = false;
            MainThread.BeginInvokeOnMainThread(() => UpdateBadgeVisibility());
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WelcomePage] SilentBackgroundUpdate failed: {ex.Message}");
        }
    }

    private async Task ShowUpdateAvailablePopupAsync(
        string newVersion,
        (double TotalMb, int POICount, int AudioCount, int ImageCount, string BreakdownText) sizeInfo)
    {
        var tcs     = new TaskCompletionSource<string>();
        var overlay = new Grid { BackgroundColor = Color.FromArgb("#80000000") };

        var dialog = new Border
        {
            BackgroundColor  = Color.FromArgb("#111E18"),
            Stroke           = Color.FromArgb("#22C55E"),
            StrokeThickness  = 1.5,
            Padding          = new Thickness(24),
            Margin           = new Thickness(28),
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions   = LayoutOptions.Center,
            MaximumWidthRequest = 340,
            StrokeShape      = new RoundRectangle { CornerRadius = new CornerRadius(20) }
        };

        var stack = new VerticalStackLayout { Spacing = 14 };

        var titleRow = new HorizontalStackLayout { Spacing = 10 };
        titleRow.Add(new Label { Text = "🎉", FontSize = 22, VerticalOptions = LayoutOptions.Center });
        titleRow.Add(new Label
        {
            Text = "Có bản cập nhật mới!",
            FontSize = 18, FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White, VerticalOptions = LayoutOptions.Center
        });
        stack.Add(titleRow);

        // ✅ Show size info in popup (for large updates >= 5MB)
        stack.Add(new Label
        {
            Text = $"Phiên bản {newVersion} đã sẵn sàng.\nDung lượng: {sizeInfo.TotalMb:F1} MB\n\nCập nhật để có thêm quán mới, audio cải tiến và hình ảnh mới!",
            FontSize = 14, TextColor = Color.FromArgb("#94A3B8"),
            LineBreakMode = LineBreakMode.WordWrap
        });

        var btnUpdate = new Button
        {
            Text = "⬇️  Cập nhật ngay",
            BackgroundColor = Color.FromArgb("#22C55E"),
            TextColor = Colors.White, FontAttributes = FontAttributes.Bold,
            CornerRadius = 12, HeightRequest = 50
        };
        btnUpdate.Clicked += (s, e) => { tcs.TrySetResult("update"); overlay.IsVisible = false; };
        stack.Add(btnUpdate);

        var btnLater = new Button
        {
            Text = "Để sau",
            BackgroundColor = Colors.Transparent,
            TextColor = Color.FromArgb("#64748B"),
            BorderColor = Color.FromArgb("#1E3D2A"), BorderWidth = 1,
            CornerRadius = 12, HeightRequest = 44
        };
        btnLater.Clicked += (s, e) =>
        {
            _dataSyncService.SkipVersion(newVersion);
            _hasSystemUpdate = false;
            UpdateBadgeVisibility();
            tcs.TrySetResult("later");
            overlay.IsVisible = false;
        };
        stack.Add(btnLater);

        dialog.Content = stack;
        overlay.Children.Add(dialog);
        var mainGrid = (Grid)this.Content;
        mainGrid.Children.Add(overlay);

        var result = await tcs.Task;
        mainGrid.Children.Remove(overlay);

        if (result == "update")
        {
            await RunDownloadWithOverlayAsync(isUpdate: true);
        }
    }

    // ── First-launch offline popup ───────────────────────────────────────────

    private Task ShowFirstLaunchOfflinePopupAsync()
    {
        // ✅ FIX: Show popup IMMEDIATELY with estimated sizes — no slow HEAD requests!
        // Real size will be shown in download progress overlay
        var estimatedMb = 9.8;
        var estimatedBreakdown =
            "• Dữ liệu quán: 0.1 MB\n" +
            "• Audio 3 ngôn ngữ: 6.5 MB\n" +
            "• Hình ảnh: 2.7 MB\n" +
            "• Bản đồ: 0.5 MB";

        var tcs     = new TaskCompletionSource<string>();
        var dontShowAgain = false;
        var overlay = new Grid { BackgroundColor = Color.FromArgb("#80000000") };

        var dialog = new Border
        {
            BackgroundColor  = Color.FromArgb("#111E18"),
            Stroke           = Color.FromArgb("#22C55E"),
            StrokeThickness  = 1.5,
            Padding          = new Thickness(24),
            Margin           = new Thickness(28),
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions   = LayoutOptions.Center,
            MaximumWidthRequest = 360,
            StrokeShape      = new RoundRectangle { CornerRadius = new CornerRadius(20) }
        };

        var stack = new VerticalStackLayout { Spacing = 14 };

        var headerGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            }
        };

        var titleLabel = new Label
        {
            Text = "📦 Tải dữ liệu offline",
            FontSize = 20, FontAttributes = FontAttributes.Bold, TextColor = Colors.White,
            VerticalOptions = LayoutOptions.Center
        };
        Grid.SetColumn(titleLabel, 0);
        headerGrid.Add(titleLabel);

        var closeButton = new Button
        {
            Text = "✕",
            FontSize = 18, FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#94A3B8"),
            BackgroundColor = Colors.Transparent,
            Padding = new Thickness(10, 0),
            VerticalOptions = LayoutOptions.Center
        };
        closeButton.Clicked += (s, e) =>
        {
            tcs.TrySetResult("close");
            overlay.IsVisible = false;
        };
        Grid.SetColumn(closeButton, 1);
        headerGrid.Add(closeButton);

        stack.Add(headerGrid);

        stack.Add(new Label
        {
            Text = $"Tải về để sử dụng không cần mạng:\n\n{estimatedBreakdown}\n\n📊 Tổng ước tính: ~{estimatedMb:F1} MB\n⏱️ ~{(estimatedMb / 1.5):F0} giây (WiFi)",
            FontSize = 13, TextColor = Color.FromArgb("#94A3B8"),
            LineBreakMode = LineBreakMode.WordWrap
        });

        // Checkbox: Don't show again
        var checkGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star }
            },
            ColumnSpacing = 10, Margin = new Thickness(0, 4, 0, 0)
        };
        var checkbox = new CheckBox { Color = Color.FromArgb("#22C55E"), VerticalOptions = LayoutOptions.Center };
        checkbox.CheckedChanged += (s, e) => dontShowAgain = e.Value;
        checkGrid.Add(checkbox);
        Grid.SetColumn(checkbox, 0);
        var checkLabel = new Label
        {
            Text = AppStrings.Info_DontShowAgain, FontSize = 12,
            TextColor = Color.FromArgb("#64748B"), VerticalOptions = LayoutOptions.Center
        };
        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => checkbox.IsChecked = !checkbox.IsChecked;
        checkLabel.GestureRecognizers.Add(tap);
        checkGrid.Add(checkLabel);
        Grid.SetColumn(checkLabel, 1);
        stack.Add(checkGrid);

        var btnDownload = new Button
        {
            Text = "⬇️  Tải ngay",
            BackgroundColor = Color.FromArgb("#22C55E"),
            TextColor = Colors.White, FontAttributes = FontAttributes.Bold,
            CornerRadius = 12, HeightRequest = 50
        };
        btnDownload.Clicked += async (s, e) =>
        {
            if (dontShowAgain) Preferences.Set(PREF_DONT_SHOW_INFO, true);
            tcs.TrySetResult("download");
            overlay.IsVisible = false;
            // ✅ Popup closes instantly; download runs in background with its own progress overlay
            await RunDownloadWithOverlayAsync(isUpdate: false);
        };
        stack.Add(btnDownload);

        var btnSkip = new Button
        {
            Text = "⏭️  Dùng ngay",
            BackgroundColor = Colors.Transparent,
            TextColor = Color.FromArgb("#64748B"),
            BorderColor = Color.FromArgb("#1E3D2A"), BorderWidth = 1,
            CornerRadius = 12, HeightRequest = 44
        };
        btnSkip.Clicked += async (s, e) =>
        {
            if (dontShowAgain) Preferences.Set(PREF_DONT_SHOW_INFO, true);
            tcs.TrySetResult("skip");
            overlay.IsVisible = false;
            await NavigateToMapAsync();
        };
        stack.Add(btnSkip);

        dialog.Content = stack;
        overlay.Children.Add(dialog);
        var mainGrid = (Grid)this.Content;
        mainGrid.Children.Add(overlay);

        return tcs.Task;
    }

    /// <summary>Runs full download with a spinner overlay and success popup.</summary>
    private async Task RunDownloadWithOverlayAsync(bool isUpdate)
    {
        if (!isUpdate && !await EnsureOnlineBeforeOfflineDownloadAsync())
            return;

        // Show spinner overlay
        var spinnerOverlay = new Grid
        {
            BackgroundColor = Color.FromArgb("#CC000000")
        };
        var spinnerBox = new Border
        {
            BackgroundColor  = Color.FromArgb("#111E18"),
            Stroke           = Color.FromArgb("#22C55E"),
            StrokeThickness  = 1,
            Padding          = new Thickness(32),
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions   = LayoutOptions.Center,
            StrokeShape      = new RoundRectangle { CornerRadius = new CornerRadius(20) }
        };

        var spinnerStack = new VerticalStackLayout { Spacing = 16, HorizontalOptions = LayoutOptions.Center };
        spinnerStack.Add(new ActivityIndicator { IsRunning = true, Color = Color.FromArgb("#22C55E"), HeightRequest = 48, WidthRequest = 48, HorizontalOptions = LayoutOptions.Center });
        var progressBar = new ProgressBar
        {
            Progress = 0,
            ProgressColor = Color.FromArgb("#22C55E"),
            BackgroundColor = Color.FromArgb("#1E3D2A"),
            HeightRequest = 8,
            WidthRequest = 220
        };
        spinnerStack.Add(progressBar);
        var percentLbl = new Label
        {
            Text = "0%",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White,
            HorizontalOptions = LayoutOptions.Center
        };
        spinnerStack.Add(percentLbl);
        var statusLbl = new Label
        {
            Text = "Đang chuẩn bị...",
            FontSize = 14, TextColor = Color.FromArgb("#94A3B8"),
            HorizontalOptions = LayoutOptions.Center
        };
        spinnerStack.Add(statusLbl);
        spinnerBox.Content = spinnerStack;
        spinnerOverlay.Children.Add(spinnerBox);

        var mainGrid = (Grid)this.Content;
        mainGrid.Children.Add(spinnerOverlay);

        try
        {
            if (isUpdate)
            {
                await _dataSyncService.DownloadAllDataAsync(msg =>
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        var progress = GetDownloadOverlayProgress(msg);
                        statusLbl.Text = SimplifyDownloadStatusMessage(msg);
                        percentLbl.Text = $"{(int)Math.Round(progress * 100)}%";
                        await progressBar.ProgressTo(progress, 120, Easing.Linear);
                    }));

                progressBar.Progress = 1;
                percentLbl.Text = "100%";
            }
            else
            {
                await _dataSyncService.PrimeEssentialOfflineDataAsync(_currentLang, msg =>
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        var progress = GetDownloadOverlayProgress(msg);
                        statusLbl.Text = SimplifyDownloadStatusMessage(msg);
                        percentLbl.Text = $"{(int)Math.Round(progress * 100)}%";
                        await progressBar.ProgressTo(progress, 120, Easing.Linear);
                    }));

                progressBar.Progress = 1;
                percentLbl.Text = "100%";
            }

            mainGrid.Children.Remove(spinnerOverlay);
            UpdateDataSourceLabel();
            EnableStartButton();

            if (isUpdate)
            {
                Preferences.Set(PREF_FULL_OFFLINE, true);
                await DisplayAlertAsync(
                    "✅ Hoàn tất!",
                    "Dữ liệu đã được cập nhật phiên bản mới nhất. Tận hưởng tour nhé!",
                    "OK");
            }
            else
            {
                _ = _dataSyncService.EnsureDeferredOfflineCompletionAsync();
                await NavigateToMapAsync();
                return;
            }
        }
        catch (Exception ex)
        {
            mainGrid.Children.Remove(spinnerOverlay);
            EnableStartButton();
            await DisplayAlertAsync("Lỗi tải dữ liệu", $"Không thể tải: {ex.Message}", "OK");
        }
    }

    /// <summary>
    /// Hiện info lần đầu - KHÔNG AGGRESSIVE, có checkbox
    /// </summary>
    private static string SimplifyDownloadStatusMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return "Dang chuan bi...";

        var clean = ProgressPairRegex.Replace(message, string.Empty).Trim();
        clean = clean.Replace("  ", " ");

        if (clean.Contains("audio", StringComparison.OrdinalIgnoreCase))
            return "Dang tai audio thuyet minh...";
        if (clean.Contains("khu vuc chinh", StringComparison.OrdinalIgnoreCase))
            return "Dang tai ban do can thiet...";
        if (clean.Contains("ban do offline", StringComparison.OrdinalIgnoreCase))
            return "Dang tai ban do offline...";
        if (clean.Contains("hinh anh", StringComparison.OrdinalIgnoreCase) ||
            clean.Contains("anh", StringComparison.OrdinalIgnoreCase))
            return "Dang tai hinh anh dia diem...";
        if (clean.Contains("chi duong", StringComparison.OrdinalIgnoreCase))
            return "Dang chuan bi chi duong offline...";
        if (clean.Contains("thuc don", StringComparison.OrdinalIgnoreCase))
            return "Dang tai thuc don...";
        if (clean.Contains("danh sach quan", StringComparison.OrdinalIgnoreCase))
            return "Dang tai danh sach quan...";

        return clean;
    }

    private static double GetDownloadOverlayProgress(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return 0;

        if (message.Contains("danh sach quan", StringComparison.OrdinalIgnoreCase))
            return 0.10;

        if (message.Contains("thuc don", StringComparison.OrdinalIgnoreCase))
            return 0.25;

        if (message.Contains("audio", StringComparison.OrdinalIgnoreCase))
            return 0.25 + GetFractionFromMessage(message) * 0.45;

        if (message.Contains("ban do offline", StringComparison.OrdinalIgnoreCase))
            return 0.70 + GetFractionFromMessage(message) * 0.30;

        return 0.05;
    }

    private static double GetFractionFromMessage(string message)
    {
        var match = ProgressPairRegex.Match(message);
        if (!match.Success)
            return 0;

        if (!int.TryParse(match.Groups[1].Value, out var done))
            return 0;
        if (!int.TryParse(match.Groups[2].Value, out var total))
            return 0;
        if (total <= 0)
            return 0;

        return Math.Clamp((double)done / total, 0, 1);
    }


    /// <summary>
    /// Custom dialog với checkbox (vì DisplayAlert không có checkbox)
    /// </summary>
    private async Task<string> ShowCustomInfoDialog()
    {
        var tcs = new TaskCompletionSource<string>();
        var dontShowAgain = false;

        var overlay = new Grid
        {
            BackgroundColor = Color.FromArgb("#80000000")
        };

        var dialog = new Border
        {
            BackgroundColor = Color.FromArgb("#14251F"),
            Stroke = Color.FromArgb("#1E3D2A"),
            StrokeThickness = 1,
            Padding = new Thickness(24),
            Margin = new Thickness(32),
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            MaximumWidthRequest = 340,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(20) }
        };

        var stack = new VerticalStackLayout { Spacing = 14 };

        // Icon + Title
        stack.Add(new Label
        {
            Text = AppStrings.Info_Offline_Title,
            FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White
        });

        // Message
        stack.Add(new Label
        {
            Text = AppStrings.Info_Offline_Message,
            FontSize = 14,
            TextColor = Color.FromArgb("#94A3B8"),
            LineBreakMode = LineBreakMode.WordWrap
        });

        // Checkbox: "Không hiển thị lại"
        var checkboxGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star }
            },
            ColumnSpacing = 10,
            Margin = new Thickness(0, 8, 0, 0)
        };

        var checkbox = new CheckBox
        {
            Color = Color.FromArgb("#22C55E"),
            VerticalOptions = LayoutOptions.Center
        };
        checkbox.CheckedChanged += (s, e) => dontShowAgain = e.Value;

        checkboxGrid.Add(checkbox);
        Grid.SetColumn(checkbox, 0);

        var checkboxLabel = new Label
        {
            Text = AppStrings.Info_DontShowAgain,
            FontSize = 13,
            TextColor = Color.FromArgb("#94A3B8"),
            VerticalOptions = LayoutOptions.Center
        };
        checkboxGrid.Add(checkboxLabel);
        Grid.SetColumn(checkboxLabel, 1);

        var checkboxTap = new TapGestureRecognizer();
        checkboxTap.Tapped += (s, e) => checkbox.IsChecked = !checkbox.IsChecked;
        checkboxLabel.GestureRecognizers.Add(checkboxTap);

        stack.Add(checkboxGrid);

        // Buttons
        var btnDownload = new Button
        {
            Text = AppStrings.Button_DownloadOfflineNow,
            BackgroundColor = Color.FromArgb("#22C55E"),
            TextColor = Colors.White,
            FontAttributes = FontAttributes.Bold,
            CornerRadius = 12,
            HeightRequest = 48
        };
        btnDownload.Clicked += (s, e) =>
        {
            if (dontShowAgain)
                Preferences.Set(PREF_DONT_SHOW_INFO, true);
            tcs.SetResult("download");
            overlay.IsVisible = false;
        };
        stack.Add(btnDownload);

        var btnContinue = new Button
        {
            Text = AppStrings.Button_ContinueOnline,
            BackgroundColor = Colors.Transparent,
            TextColor = Color.FromArgb("#94A3B8"),
            BorderColor = Color.FromArgb("#1E3D2A"),
            BorderWidth = 1,
            CornerRadius = 12,
            HeightRequest = 44
        };
        btnContinue.Clicked += (s, e) =>
        {
            if (dontShowAgain)
                Preferences.Set(PREF_DONT_SHOW_INFO, true);
            tcs.SetResult("continue");
            overlay.IsVisible = false;
        };
        stack.Add(btnContinue);

        // Close button (small, top right)
        var closeBtn = new Button
        {
            Text = "✕",
            FontSize = 18,
            BackgroundColor = Colors.Transparent,
            TextColor = Color.FromArgb("#64748B"),
            WidthRequest = 32,
            HeightRequest = 32,
            CornerRadius = 16,
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.Start,
            Margin = new Thickness(0, -10, -10, 0)
        };
        closeBtn.Clicked += (s, e) =>
        {
            tcs.SetResult("close");
            overlay.IsVisible = false;
        };

        dialog.Content = stack;
        overlay.Children.Add(dialog);
        overlay.Children.Add(closeBtn);

        // Add to page
        var mainGrid = (Grid)this.Content;
        mainGrid.Children.Add(overlay);

        var result = await tcs.Task;
        mainGrid.Children.Remove(overlay);

        return result;
    }

    // DOWNLOAD OFFLINE
    private async Task DownloadOfflineFromSettings()
    {
        // Check network
        if (!await EnsureOnlineBeforeOfflineDownloadAsync())
            return;

        // Check WiFi vs 4G
        var isWifi = Connectivity.Current.ConnectionProfiles
            .Contains(ConnectionProfile.WiFi);

        if (!isWifi)
        {
            var confirm = await CustomAlert.ShowConfirmAsync(
                AppStrings.Alert_UsingCellular_Title,
                AppStrings.Alert_UsingCellular_Message,
                AppStrings.Common_Continue,
                AppStrings.Common_Cancel,
                AlertType.Warning);

            if (!confirm) return;
        }

        // Start download
        ShowDownloadingOfflineState();
        _canStartTour = false;
        UpdatePrimaryActionState();

        try
        {
            // ✅ No artificial delays — real work only
            await _repository.SyncFromMongoAsync();
            await _repository.LoadLocalAsync();

            if (_repository.IsSeeded)
            {
                ShowReadyState();
                EnableStartButton();
                Preferences.Set(PREF_FULL_OFFLINE, true);

                await CustomAlert.ShowAsync(
                    AppStrings.Alert_DownloadDone_Title,
                    AppStrings.Alert_DownloadDone_Message,
                    AppStrings.Common_OK,
                    AlertType.Success);

                await NavigateToMapAsync();
            }
            else
            {
                ShowErrorState();
                EnableStartButton();
            }
        }
        catch (Exception ex)
        {
            ShowErrorState(ex.Message);
            EnableStartButton();
        }
    }

    private async Task<bool> EnsureOnlineBeforeOfflineDownloadAsync()
    {
        if (IsOnline())
            return true;

        await CustomAlert.ShowAsync(
            "Không có mạng để tải gói offline",
            "Hiện tại thiết bị đang offline, nên chưa thể tải thêm dữ liệu. Ứng dụng sẽ tiếp tục dùng dữ liệu hệ thống có sẵn trên máy. Khi có mạng, hãy vào lại Cài đặt và bấm Tải dữ liệu offline.",
            AppStrings.Common_OK,
            AlertType.Warning);
        return false;
    }

    private Label FindDetailLabel()
    {
        // Helper to find the detail label in status view for progress updates
        if (StatusContainer.Content is Border border &&
            border.Content is Grid grid)
        {
            foreach (var child in grid.Children)
            {
                if (child is VerticalStackLayout stack && stack.Children.Count > 1)
                {
                    return stack.Children[1] as Label;
                }
            }
        }
        return null;
    }

    private async Task UpdateProgress(Label label, string text)
    {
        if (label != null)
        {
            label.Text = text;
            await Task.Delay(50); // Give UI time to update
        }
    }

    // ══════════════════════════════════════════════════════════════
    // STATUS VIEWS
    // ══════════════════════════════════════════════════════════════
    
    private void ShowDownloadingMetadataState()
    {
        _statusKind = StatusKind.DownloadingMetadata;
        StatusContainer.Content = CreateDownloadingView(
            AppStrings.Status_Downloading_Metadata_Title,
            AppStrings.Status_Downloading_Metadata_Detail);
    }

    private void ShowDownloadingOfflineState()
    {
        _statusKind = StatusKind.DownloadingOffline;
        StatusContainer.Content = CreateDownloadingView(
            AppStrings.Status_Downloading_Offline_Title,
            AppStrings.Status_Downloading_Offline_Detail);
    }

    private void ShowReadyState()
    {
        _statusKind = StatusKind.Ready;
        // BỎ HOÀN TOÀN - không hiện gì cả
        StatusContainer.Content = null;
        UpdateDataSourceLabel();
    }

    private void ShowNeedInternetState()
    {
        _statusKind = StatusKind.NeedInternet;
        StatusContainer.Content = CreateNeedInternetView();
    }

    private void ShowErrorState(string? details = null)
    {
        _statusKind = StatusKind.Error;
        _errorDetails = details;
        StatusContainer.Content = CreateErrorView(details);
    }

    private void RefreshStatusView()
    {
        switch (_statusKind)
        {
            case StatusKind.DownloadingMetadata:
                ShowDownloadingMetadataState();
                break;
            case StatusKind.DownloadingOffline:
                ShowDownloadingOfflineState();
                break;
            case StatusKind.Ready:
                ShowReadyState();
                break;
            case StatusKind.NeedInternet:
                ShowNeedInternetState();
                break;
            case StatusKind.Error:
                ShowErrorState(_errorDetails);
                break;
            default:
                break;
        }

        UpdatePrimaryActionState();
    }

    private View CreateDownloadingView(string title, string detail)
    {
        var border = new Border
        {
            BackgroundColor = Color.FromArgb("#0F1F17"),
            Stroke = Color.FromArgb("#22C55E30"),
            StrokeThickness = 1,
            Padding = new Thickness(20, 16),
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(16) }
        };

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star }
            },
            ColumnSpacing = 14
        };

        var iconBorder = new Border
        {
            BackgroundColor = Color.FromArgb("#22C55E20"),
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(14) },
            WidthRequest = 56,
            HeightRequest = 56,
            Content = new ActivityIndicator
            {
                IsRunning = true,
                Color = Color.FromArgb("#22C55E"),
                WidthRequest = 32,
                HeightRequest = 32,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            }
        };
        Grid.SetColumn(iconBorder, 0);
        grid.Add(iconBorder);

        var textStack = new VerticalStackLayout
        {
            Spacing = 6,
            VerticalOptions = LayoutOptions.Center
        };
        textStack.Add(new Label
        {
            Text = title,
            FontSize = 15,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#E2E8F0")
        });
        textStack.Add(new Label
        {
            Text = detail,
            FontSize = 13,
            TextColor = Color.FromArgb("#64748B")
        });
        Grid.SetColumn(textStack, 1);
        grid.Add(textStack);

        border.Content = grid;
        border.Opacity = 0;
        _ = border.FadeToAsync(1, 300);

        return border;
    }

    private View CreateReadyView()
    {
        var border = new Border
        {
            BackgroundColor = Color.FromArgb("#0F1F17"),
            Stroke = Color.FromArgb("#22C55E"),
            StrokeThickness = 2,
            Padding = new Thickness(20, 16),
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(16) },
            Shadow = new Shadow
            {
                Brush = Color.FromArgb("#22C55E"),
                Opacity = 0.2f,
                Radius = 12,
                Offset = new Point(0, 4)
            }
        };

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star }
            },
            ColumnSpacing = 14
        };

        var iconBorder = new Border
        {
            BackgroundColor = Color.FromArgb("#22C55E"),
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(14) },
            WidthRequest = 56,
            HeightRequest = 56,
            Content = new Label
            {
                Text = "✓",
                FontSize = 32,
                FontAttributes = FontAttributes.Bold,
                TextColor = Colors.White,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            }
        };
        Grid.SetColumn(iconBorder, 0);
        grid.Add(iconBorder);

        var textStack = new VerticalStackLayout { Spacing = 6, VerticalOptions = LayoutOptions.Center };
        textStack.Add(new Label
        {
            Text = AppStrings.Status_Ready_Title,
            FontSize = 15,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#22C55E")
        });
        textStack.Add(new Label
        {
            Text = AppStrings.Status_Ready_Detail,
            FontSize = 13,
            TextColor = Color.FromArgb("#64748B")
        });
        Grid.SetColumn(textStack, 1);
        grid.Add(textStack);

        border.Content = grid;
        border.Opacity = 0;
        _ = border.FadeToAsync(1, 300);

        return border;
    }

    private View CreateNeedInternetView()
    {
        var mainStack = new VerticalStackLayout { Spacing = 12 };

        var border = new Border
        {
            BackgroundColor = Color.FromArgb("#0F1F17"),
            Stroke = Color.FromArgb("#F59E0B"),
            StrokeThickness = 2,
            Padding = new Thickness(20, 16),
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(16) }
        };

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star }
            },
            ColumnSpacing = 14
        };

        var iconBorder = new Border
        {
            BackgroundColor = Color.FromArgb("#F59E0B20"),
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(14) },
            WidthRequest = 56,
            HeightRequest = 56,
            Content = new Label
            {
                Text = "📵",
                FontSize = 28,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            }
        };
        Grid.SetColumn(iconBorder, 0);
        grid.Add(iconBorder);

        var textStack = new VerticalStackLayout { Spacing = 6, VerticalOptions = LayoutOptions.Center };
        textStack.Add(new Label
        {
            Text = AppStrings.Status_NeedInternet_Title,
            FontSize = 15,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#F59E0B")
        });
        textStack.Add(new Label
        {
            Text = AppStrings.Status_NeedInternet_Detail,
            FontSize = 13,
            TextColor = Color.FromArgb("#64748B")
        });
        Grid.SetColumn(textStack, 1);
        grid.Add(textStack);

        border.Content = grid;
        mainStack.Add(border);

        var retryButton = new Button
        {
            Text = AppStrings.Button_TryAgain,
            BackgroundColor = Color.FromArgb("#F59E0B"),
            TextColor = Colors.White,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            HeightRequest = 44,
            CornerRadius = 12
        };
        retryButton.Clicked += async (s, e) => await RunSimpleFlowAsync();
        mainStack.Add(retryButton);

        return mainStack;
    }

    private View CreateErrorView(string? details = null)
    {
        var mainStack = new VerticalStackLayout { Spacing = 12 };

        var border = new Border
        {
            BackgroundColor = Color.FromArgb("#0F1F17"),
            Stroke = Color.FromArgb("#EF4444"),
            StrokeThickness = 2,
            Padding = new Thickness(20, 16),
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(16) }
        };

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star }
            },
            ColumnSpacing = 14
        };

        var iconBorder = new Border
        {
            BackgroundColor = Color.FromArgb("#EF444420"),
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(14) },
            WidthRequest = 56,
            HeightRequest = 56,
            Content = new Label
            {
                Text = "⚠️",
                FontSize = 28,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            }
        };
        Grid.SetColumn(iconBorder, 0);
        grid.Add(iconBorder);

        var textStack = new VerticalStackLayout { Spacing = 6, VerticalOptions = LayoutOptions.Center };
        textStack.Add(new Label
        {
            Text = AppStrings.Status_Error_Title,
            FontSize = 15,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#EF4444")
        });
        textStack.Add(new Label
        {
            Text = string.IsNullOrEmpty(details)
                ? AppStrings.Status_Error_Detail
                : details,
            FontSize = 13,
            TextColor = Color.FromArgb("#64748B")
        });
        Grid.SetColumn(textStack, 1);
        grid.Add(textStack);

        border.Content = grid;
        mainStack.Add(border);

        var retryButton = new Button
        {
            Text = AppStrings.Button_TryAgain,
            BackgroundColor = Color.FromArgb("#EF4444"),
            TextColor = Colors.White,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            HeightRequest = 44,
            CornerRadius = 12
        };
        retryButton.Clicked += async (s, e) => await RunSimpleFlowAsync();
        mainStack.Add(retryButton);

        return mainStack;
    }

    // NAVIGATION & SETTINGS
    
    private async Task NavigateToMapAsync()
    {
        // Do not block navigation on prewarm. Let MainPage loading handle data prep.
        StartMainPagePrewarmInBackground();

        // Avoid forcing an immediate in-zone map auto-open on first launch.
        // This was causing heavy startup contention and first-run ANR/crash loops.
        Preferences.Set(AutoOpenInZoneOnNextMainPageKey, false);
        
        App.CompleteOnboarding(); // Sets has_onboarded = true

        // Do not mutate Shell item visibility at runtime here.
        // Toggling tab visibility during first navigation can race Shell fragment lifecycle.
        await Shell.Current.GoToAsync("//MapPage", false);
    }

    private void StartMainPagePrewarmInBackground()
    {
        _ = MainThread.InvokeOnMainThreadAsync(async () =>
        {
            await PrewarmMainPageDataAsync();
        });
    }

    private async Task PrewarmMainPageDataAsync()
    {
        try
        {
            var vm = MauiProgram.Services.GetRequiredService<MainViewModel>();

            await vm.LoadAllPoisAsync(forceSyncNow: false);
            vm.RefreshExploreState();

            var hasData = vm.AllPOIs.Count > 0;
            Preferences.Set(AppConfig.MainPagePrewarmReadyKey, hasData);
            if (hasData)
                Preferences.Set(AppConfig.MainPagePrewarmAtUtcKey, DateTime.UtcNow.ToString("O"));
            else
                Preferences.Remove(AppConfig.MainPagePrewarmAtUtcKey);
        }
        catch (Exception ex)
        {
            Preferences.Set(AppConfig.MainPagePrewarmReadyKey, false);
            Preferences.Remove(AppConfig.MainPagePrewarmAtUtcKey);
            System.Diagnostics.Debug.WriteLine($"[WelcomePage] PrewarmMainPageDataAsync error: {ex.Message}");
        }
    }

    private async void OnSettingsClicked(object sender, EventArgs e)
    {
        // Use cached SettingsPage + Shell navigation for instant response
        if (Shell.Current is AppShell shell)
        {
            await Shell.Current.Navigation.PushModalAsync(shell.GetCachedSettingsPage(), false);
        }
        else
        {
            await Navigation.PushModalAsync(new SettingsPage(), false);
        }
    }

    private async void OnHeaderLanguageClicked(object sender, EventArgs e)
    {
        var selected = await LanguageSwitcher.ShowLanguagePickerAsync(this, _languageService);
        ApplyLanguage(selected);
    }

    // LANGUAGE
    
    private void OnLanguageClicked(object sender, EventArgs e)
    {
        if (sender is not Button button) return;
        var lang = button.CommandParameter?.ToString() ?? LangVi;
        Preferences.Set(AppConfig.LanguagePrefKey, lang);
        ApplyLanguage(lang);
    }

    private void ApplyLanguage(string lang)
    {
        _currentLang = lang;
        AppStrings.SetCulture(lang);
        HeaderLanguageButton.Text = LanguageSwitcher.GetHeaderLabel(lang);
        TitleLabel.Text = AppStrings.Welcome_AppTitle;
        SubtitleLabel.Text = AppStrings.Welcome_Subtitle;
        StartButton.Text = AppStrings.Welcome_StartTour;
        FeatureAutoTitleLabel.Text = AppStrings.Welcome_Feature_Auto_Title;
        FeatureAutoSubtitleLabel.Text = AppStrings.Welcome_Feature_Auto_Desc;
        FeatureOfflineTitleLabel.Text = AppStrings.Welcome_Feature_Offline_Title;
        FeatureOfflineSubtitleLabel.Text = AppStrings.Welcome_Feature_Offline_Desc;
        FeatureLocalTitleLabel.Text = AppStrings.Welcome_Feature_Local_Title;
        FeatureLocalSubtitleLabel.Text = AppStrings.Welcome_Feature_Local_Desc;

        SetLangState(LangViButton, lang == LangVi);
        SetLangState(LangEnButton, lang == LangEn);
        SetLangState(LangZhButton, lang == LangZh);

        RefreshStatusView();
        UpdatePrimaryActionState();
    }

    private static void SetLangState(Button button, bool isActive)
    {
        button.BackgroundColor = isActive ? Color.FromArgb("#22C55E") : Colors.Transparent;
        button.TextColor = isActive ? Colors.White : Color.FromArgb("#64748B");
    }

    // ══════════════════════════════════════════════════════════════
    // HELPERS
    // ══════════════════════════════════════════════════════════════
    
    private void SetUpdateBadge(int count)
    {
        _newAudioCount = Math.Max(0, count);
        UpdateBadgeVisibility();
    }

    private void UpdateBadgeVisibility()
    {
        if (UpdateBadge == null) return;
        UpdateBadge.IsVisible = _newAudioCount > 0 || _hasSystemUpdate;
    }

    private void UpdateDataSourceLabel()
    {
        if (DataSourceLabel == null) return;

        var sourceText = _repository.CurrentDataSource switch
        {
            DataSourceKind.LiveApi => Ui("Nguồn dữ liệu: Server", "Data source: Server", "数据来源：服务器"),
            DataSourceKind.SqliteCache => Ui("Nguồn dữ liệu: Cache", "Data source: Cache", "数据来源：缓存"),
            DataSourceKind.BundledJson => Ui("Nguồn dữ liệu: Bundled", "Data source: Bundled", "数据来源：内置"),
            DataSourceKind.MockFallback => Ui("Nguồn dữ liệu: Mock", "Data source: Mock", "数据来源：模拟"),
            _ => Ui("Nguồn dữ liệu: Đang kiểm tra", "Data source: Checking", "数据来源：检查中")
        };

        var lastSync = Preferences.Get("LastSyncTime", Ui("Chưa đồng bộ", "Not synced yet", "尚未同步"));
        if (DateTime.TryParse(lastSync, out var parsed))
        {
            lastSync = parsed.ToString("dd/MM HH:mm");
            if (_repository.CurrentDataSource == DataSourceKind.Unknown)
                sourceText = Ui("Nguồn dữ liệu: Cache", "Data source: Cache", "数据来源：缓存");
        }

        DataSourceLabel.Text = $"{sourceText} | {Ui("Lần cuối", "Last sync", "最近同步")}: {lastSync}";
    }

    private string Ui(string vi, string en, string zh)
        => _currentLang switch
        {
            LangEn => en,
            LangZh => zh,
            _ => vi
        };

    private static bool IsOnline()
    {
        var access = Connectivity.Current.NetworkAccess;
        if (access is NetworkAccess.Internet or NetworkAccess.ConstrainedInternet)
            return true;

        if (access == NetworkAccess.Local && AppConfig.UseBackendApi)
            return IsLikelyLocalApiHost(AppConfig.GetResolvedApiBaseUrl());

        return false;
    }

    private static bool IsLikelyLocalApiHost(string? baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            return false;

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
            return false;

        var host = uri.Host;
        if (string.IsNullOrWhiteSpace(host))
            return false;

        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            host.Equals("10.0.2.2", StringComparison.OrdinalIgnoreCase) ||
            host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase))
            return true;

        if (!IPAddress.TryParse(host, out var ip))
            return false;

        var bytes = ip.GetAddressBytes();
        if (bytes.Length == 4)
        {
            if (bytes[0] == 10) return true;
            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return true;
            if (bytes[0] == 192 && bytes[1] == 168) return true;
            if (bytes[0] == 127) return true;
        }

        return IPAddress.IsLoopback(ip);
    }

    private void EnableStartButton()
    {
        _canStartTour = true;
        UpdatePrimaryActionState();
    }

    private void UpdatePrimaryActionState()
    {
        if (StartButton == null)
            return;

        StartButton.Text = AppStrings.Welcome_StartTour;
        StartButton.IsEnabled = _canStartTour;
        StartButton.Opacity = _canStartTour ? 1.0 : 0.55;
    }
}

