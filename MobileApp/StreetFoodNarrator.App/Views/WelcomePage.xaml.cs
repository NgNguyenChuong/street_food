using Microsoft.Maui.Graphics;
using Microsoft.Maui.Networking;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Storage;
using StreetFoodNarrator.App;
using StreetFoodNarrator.App.Core.Services;
using StreetFoodNarrator.App.Resources.Strings;
using StreetFoodNarrator.App.ViewModels;
using StreetFoodNarrator.App.Core.Models;
namespace StreetFoodNarrator.App.Views;

public partial class WelcomePage : ContentPage
{
    private const string LangVi = "vi";
    private const string LangEn = "en";
    private const string LangZh = "zh";
    private const string PREF_FULL_OFFLINE = "has_full_offline";
    private const string PREF_DONT_SHOW_INFO = "dont_show_offline_info";

    private readonly IZoneRepository _repository;
    private readonly IAudioCacheService? _audioCache;
    private bool _flowStarted = false;
    private string _currentLang = LangVi;
    private StatusKind _statusKind = StatusKind.None;
    private string? _errorDetails;
    private int _newAudioCount = 0;

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
        _repository  = MauiProgram.Services.GetRequiredService<IZoneRepository>();
        _audioCache  = MauiProgram.Services.GetService<IAudioCacheService>();
        var lang = Preferences.Get(AppConfig.LanguagePrefKey, LangVi);
        ApplyLanguage(lang);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        ApplyLanguage(Preferences.Get(AppConfig.LanguagePrefKey, LangVi));

        if (_flowStarted) return;
        _flowStarted = true;

        try
        {
            await RunSimpleFlowAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WelcomePage] OnAppearing error: {ex}");
            ShowErrorState(ex.Message);
            EnableStartButton();
        }
    }
    // AUTO LOAD: Tự động tải metadata ở background 
   
    private async Task RunSimpleFlowAsync()
    {
        // Step 1: load from SQLite cache
        await _repository.LoadLocalAsync();

        if (!_repository.IsSeeded)
        {
            // Step 2: no SQLite data — seed now
            // Online  → downloads from API and saves to SQLite
            // Offline → loads bundled default_pois.json and saves to SQLite
            if (IsOnline())
                ShowDownloadingMetadataState();

            await _repository.SyncFromMongoAsync();

            // After SyncFromMongoAsync the in-memory zones are set;
            // reload from SQLite so IsSeeded reflects reality on next launch
            if (_repository.IsSeeded)
                await _repository.LoadLocalAsync(); // sync in-memory → SQLite state
        }

        if (_repository.IsSeeded)
        {
            ShowReadyState();
            EnableStartButton();
            _ = RunBackgroundSyncAsync();
            // Kiểm tra audio mới ngay sau khi UI sẵn sàng
            CheckAudioUpdatesInBackground();
            return;
        }

        // Truly no data even after bundled seed — very unlikely
        ShowErrorState("Không thể tải dữ liệu địa điểm.");
        EnableStartButton();
    }

    private async Task RunBackgroundSyncAsync()
    {
        try
        {
            if (!IsOnline()) return;
            await Task.Delay(2000);
            await _repository.SyncFromMongoAsync();
            await _repository.LoadLocalAsync();
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
                await Task.Delay(2500); // Đợi UI ổn định trước
                var count = await _audioCache.CheckForUpdatesAsync();

                MainThread.BeginInvokeOnMainThread(() => SetUpdateBadge(count));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WelcomePage] CheckAudioUpdates lỗi: {ex.Message}");
            }
        });
    }

    private async void OnUpdateClicked(object sender, EventArgs e)
    {
        if (_audioCache == null)
        {
            await DisplayAlert("Thông báo", "Tính năng cập nhật audio chưa sẵn sàng.", "OK");
            return;
        }

        if (!IsOnline())
        {
            await DisplayAlert(
                AppStrings.Alert_NoNetwork_Title,
                AppStrings.Alert_NoNetwork_Message,
                AppStrings.Common_OK);
            return;
        }

        UpdateButton.IsEnabled = false;
        try
        {
            var count = await _audioCache.CheckForUpdatesAsync();
            SetUpdateBadge(count);

            if (count <= 0)
            {
                await ShowNoUpdateDialogAsync();
                return;
            }

            var result = await ShowAudioUpdateDialogAsync(count);
            if (result == "download")
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
        if (_audioCache == null) return;

        // Cảnh báo khi dùng mạng di động
        var isWifi = Connectivity.Current.ConnectionProfiles.Contains(ConnectionProfile.WiFi);
        if (!isWifi)
        {
            var confirm = await DisplayAlert(
                AppStrings.Alert_UsingCellular_Title,
                AppStrings.Alert_UsingCellular_Message,
                AppStrings.Common_Continue, AppStrings.Common_Cancel);
            if (!confirm) return;
        }

        // Khóa nút, ẩn dot badge trong lúc đồng bộ
        UpdateButton.IsEnabled = false;

        try
        {
            // 1. Sync POIs
            System.Diagnostics.Debug.WriteLine("[Sync] Bước 1: Sync POIs...");
            await _repository.SyncFromMongoAsync();
            await _repository.LoadLocalAsync();
            
            var poiIds = _repository.GetAllActiveZones().Select(p => p.Id).ToList();
            
            // 2. Sync Menu Items
            System.Diagnostics.Debug.WriteLine("[Sync] Bước 2: Sync Menu...");
            var db = MauiProgram.Services.GetRequiredService<ILocalDatabaseService>();
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            
            foreach (var id in poiIds)
            {
                try {
                    var menuUrl = $"{AppConfig.ApiBaseUrl}api/MenuItems?poiId={id}&page=1&pageSize=50";
                    var response = await client.GetAsync(menuUrl);
                    if (response.IsSuccessStatusCode) {
                        var content = await response.Content.ReadAsStringAsync();
                        var result = System.Text.Json.JsonSerializer.Deserialize<MenuItemResponse>(content, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        if (result?.Data != null && result.Data.Any()) {
                            await db.SaveMenuItemsAsync(result.Data);
                        }
                    }
                } catch { /* ignore menu error for individual POI */ }
            }

            // 3. Sync Audio
            System.Diagnostics.Debug.WriteLine("[Sync] Bước 3: Sync Audio...");
            var progress = new Progress<(int done, int total)>(p =>
                System.Diagnostics.Debug.WriteLine($"[Sync] Audio {p.done}/{p.total}"));

            await _audioCache.PreloadAllAsync(poiIds, progress);

            // Tải xong → ẩn chuông
            SetUpdateBadge(0);
            Preferences.Set("LastSyncTime", DateTime.Now.ToString("dd/MM HH:mm"));
            await DisplayAlert("✅ Hoàn tất",
                "Đồng bộ hoàn tất! Dữ liệu quán, menu và audio đã sẵn sàng dùng offline.",
                "OK");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WelcomePage] DownloadUpdates lỗi: {ex.Message}");
            UpdateButton.IsEnabled = true;
        }
    }

    // START BUTTON:
    private async void OnStartTourClicked(object sender, EventArgs e)
    {
        try
        {
            var hasFullOffline = Preferences.Get(PREF_FULL_OFFLINE, false);
            var dontShowInfo = Preferences.Get(PREF_DONT_SHOW_INFO, false);

            // CASE 1: Đã có dữ liệu (offline hoặc online) → vào thẳng
            if (_repository.IsSeeded)
            {
                // Nếu đã từng dùng full-offline flag, vào thẳng không hỏi
                if (hasFullOffline || !IsOnline())
                {
                    await NavigateToMapAsync();
                    return;
                }
            }

            // CASE 2: Không có mạng, không có dữ liệu → cảnh báo rõ ràng
            if (!IsOnline())
            {
                await DisplayAlert(
                    AppStrings.Alert_NeedInternet_Title,
                    AppStrings.Alert_NeedInternet_Message,
                    AppStrings.Common_OK);
                return;
            }

            // CASE 3: Lần đầu, có mạng → hiện info với checkbox
            if (!dontShowInfo && !hasFullOffline)
            {
                await ShowFirstTimeInfoAsync();
                return;
            }

            // CASE 4: User đã chọn "Không hỏi lại" → vào thẳng
            await NavigateToMapAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WelcomePage] OnStartTourClicked error: {ex}");
            await DisplayAlert("Lỗi", $"Không thể mở bản đồ: {ex.Message}", "OK");
        }
    }

    /// <summary>
    /// Hiện info lần đầu - KHÔNG AGGRESSIVE, có checkbox
    /// </summary>
    private async Task ShowFirstTimeInfoAsync()
    {
        // CUSTOM DIALOG với checkbox "Không hiển thị lại"
        var result = await ShowCustomInfoDialog();

        if (result == "download")
        {
            // User chọn "Tải offline ngay"
            await DownloadOfflineFromSettings();
        }
        else if (result == "continue")
        {
            // User chọn "Tiếp tục Online"
            await NavigateToMapAsync();
        }
        // else: User đóng dialog → không làm gì
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
        if (!IsOnline())
        {
            await DisplayAlert(
                AppStrings.Alert_NoNetwork_Title,
                AppStrings.Alert_NoNetwork_Message,
                AppStrings.Common_OK);
            return;
        }

        // Check WiFi vs 4G
        var isWifi = Connectivity.Current.ConnectionProfiles
            .Contains(ConnectionProfile.WiFi);

        if (!isWifi)
        {
            var confirm = await DisplayAlert(
                AppStrings.Alert_UsingCellular_Title,
                AppStrings.Alert_UsingCellular_Message,
                AppStrings.Common_Continue, AppStrings.Common_Cancel);

            if (!confirm) return;
        }

        // Start download
        ShowDownloadingOfflineState();
        StartButton.IsEnabled = false;

        try
        {
            // Simulate progress steps
            var detailLabel = FindDetailLabel();
            if (detailLabel != null)
            {
                await UpdateProgress(detailLabel, AppStrings.Progress_ConnectingMongo);
                await Task.Delay(500);

                await UpdateProgress(detailLabel, AppStrings.Progress_DownloadingList);
                await _repository.SyncFromMongoAsync();
                await Task.Delay(300);

                await UpdateProgress(detailLabel, AppStrings.Progress_DownloadingAudio);
                await Task.Delay(800);

                await UpdateProgress(detailLabel, AppStrings.Progress_SavingOffline);
                await _repository.LoadLocalAsync();
                await Task.Delay(300);

                await UpdateProgress(detailLabel, AppStrings.Progress_Done);
            }
            else
            {
                await _repository.SyncFromMongoAsync();
                await _repository.LoadLocalAsync();
            }

            if (_repository.IsSeeded)
            {
                // BỎ card "Sẵn sàng offline"
                ShowReadyState();
                EnableStartButton();
                Preferences.Set(PREF_FULL_OFFLINE, true);

                // Hiện Toast notification
                await DisplayAlert(
                    AppStrings.Alert_DownloadDone_Title,
                    AppStrings.Alert_DownloadDone_Message,
                    AppStrings.Common_OK);

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
        border.FadeTo(1, 300);

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
        border.FadeTo(1, 300);

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
        // Replace root with AppShell (Main app with tabs)
        if (Application.Current?.Windows.Count > 0)
        {
            Application.Current.Windows[0].Page = new AppShell();
        }
    }

    private async void OnSettingsClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new SettingsPage());
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
        UpdateBadge.IsVisible = _newAudioCount > 0;
        // Dot chỉ hiện/ẩn, không cần text số nữa
    }

    private static bool IsOnline()
    {
        var access = Connectivity.Current.NetworkAccess;
        return access == NetworkAccess.Internet || access == NetworkAccess.ConstrainedInternet;
    }

    private void EnableStartButton()
    {
        StartButton.IsEnabled = true;
        StartButton.Opacity = 1.0;
    }
}
