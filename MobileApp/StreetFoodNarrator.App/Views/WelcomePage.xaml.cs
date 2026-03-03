using Microsoft.Maui.Graphics;
using Microsoft.Maui.Networking;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Storage;
using StreetFoodNarrator.App;
using StreetFoodNarrator.App.Core.Services;
using StreetFoodNarrator.App.Resources.Strings;
using StreetFoodNarrator.App.ViewModels;

namespace StreetFoodNarrator.App.Views;

public partial class WelcomePage : ContentPage
{
    private const string LangVi = "vi";
    private const string LangEn = "en";
    private const string LangZh = "zh";
    private const string PREF_FULL_OFFLINE = "has_full_offline";
    private const string PREF_DONT_SHOW_INFO = "dont_show_offline_info";

    private readonly IZoneRepository _repository;
    private bool _flowStarted = false;
    private string _currentLang = LangVi;
    private StatusKind _statusKind = StatusKind.None;
    private string? _errorDetails;

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
        _repository = MauiProgram.Services.GetRequiredService<IZoneRepository>();
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

        var stack = new VerticalStackLayout { Spacing = 16 };

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
        var vm = MauiProgram.Services.GetRequiredService<MainViewModel>();
        var page = new MainPage(vm);
        await Navigation.PushAsync(page);
    }

    private async void OnManualBrowseClicked(object sender, EventArgs e)
    {
        await DisplayAlert(AppStrings.Alert_ManualBrowse_Title, AppStrings.Alert_ManualBrowse_Message, AppStrings.Common_OK);
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
    
    private static bool IsOnline() => 
        Connectivity.Current.NetworkAccess == NetworkAccess.Internet;

    private void EnableStartButton()
    {
        StartButton.IsEnabled = true;
        StartButton.Opacity = 1.0;
    }
}
