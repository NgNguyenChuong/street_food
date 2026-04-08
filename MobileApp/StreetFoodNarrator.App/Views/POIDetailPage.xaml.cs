using StreetFoodNarrator.App.Core.Models;
using StreetFoodNarrator.App.Core.Services;
using StreetFoodNarrator.App.ViewModels;
using Mapsui.Layers;
using Mapsui.Projections;
using Mapsui.Styles;
using Mapsui.Tiling;
using Mapsui.Tiling.Layers;
using BruTile.Predefined;
using BruTile.Web;
using MapsColor = Mapsui.Styles.Color;
using MapsBrush = Mapsui.Styles.Brush;
using MPoint = Mapsui.MPoint;
using MauiColor = Microsoft.Maui.Graphics.Color;
using StreetFoodNarrator.App.Helpers;
using StreetFoodNarrator.App.Resources.Strings;

namespace StreetFoodNarrator.App.Views;

public partial class POIDetailPage : ContentPage
{
    private readonly POI _poi;
    private readonly POIDetailViewModel _vm;
    private readonly LanguageService _langService;

    // Cache track width ONCE when layout is ready — avoid repeated Width reads
    private double _cachedTrackWidth = -1;
    private bool _progressLayoutReady = false;

    public POIDetailPage(POI poi, bool keepCurrentAudio = false)
    {
        InitializeComponent();

        _poi = poi;
        _langService = MauiProgram.Services.GetRequiredService<LanguageService>();

        // ViewModel handles: audio state, menu items, tab index
        _vm = new POIDetailViewModel(poi, OnAudioSeek, keepCurrentAudio);
        _vm.PropertyChanged += Vm_PropertyChanged;

        BindingContext = _vm;

        // Update like icon state on init
        UpdateLikeIcon();

        // Wire menu empty/visible state
        _vm.MenuItems.CollectionChanged += (s, e) =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                MenuEmptyState.IsVisible = _vm.MenuItems.Count == 0;
                MenuCollectionView.IsVisible = _vm.MenuItems.Count > 0;
            });
        };

        // Init audio progress layout when it first gets a valid size
        Loaded += OnPageLoaded;
    }

    private void OnPageLoaded(object? sender, EventArgs e)
    {
        ApplyLocalizedStaticTexts();

        // Cache track width once layout is ready — delay 50ms so frame has settled
        _ = Task.Run(async () =>
        {
            await Task.Delay(50);
            MainThread.BeginInvokeOnMainThread(CacheTrackWidth);
        });

        // Maps initialization removed
        _ = _vm.LoadMenuItemsAsync();
    }

    private void CacheTrackWidth()
    {
        if (_cachedTrackWidth > 0) return;
        if (AudioProgressTrack?.Width > 0)
        {
            _cachedTrackWidth = AudioProgressTrack.Width;
            _progressLayoutReady = true;
            // Apply any pending progress
            if (_vm.AudioProgress > 0)
                ApplyProgressFill(_vm.AudioProgress);
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        LanguageService.LanguageChanged -= OnLanguageChanged;
        LanguageService.LanguageChanged += OnLanguageChanged;
        // Refresh like icon and localized content when page re-appears
        UpdateLikeIcon();
        _vm.RefreshLocalizedContent();
        ApplyLocalizedStaticTexts();
        UpdateNavigateCtaVisibility();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        LanguageService.LanguageChanged -= OnLanguageChanged;
        _vm.PropertyChanged -= Vm_PropertyChanged;
        _vm.Cleanup();
    }

    private void OnLanguageChanged(object? sender, string languageCode)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _vm.RefreshLocalizedContent();
            ApplyLocalizedStaticTexts();
            RefreshMenuBindings();

            RefreshMenuBindings();
        });
    }

    private string Ui(string vi, string en, string zh)
        => _langService.CurrentLanguage switch
        {
            "en" => en,
            "zh" => zh,
            _ => vi
        };

    private void ApplyLocalizedStaticTexts()
    {
        HeaderLanguageLabel.Text = LanguageSwitcher.GetHeaderLabel(_langService.CurrentLanguage);

        CategoryBadgeLabel.Text = string.IsNullOrWhiteSpace(_poi.Category)
            ? Ui("ẨM THỰC ĐƯỜNG PHỐ", "STREET FOOD", "街头美食")
            : _poi.Category;

        RatingTitleLabel.Text = Ui("Đánh giá", "Rating", "评分");

        SignatureValueLabel.Text = string.IsNullOrWhiteSpace(_poi.SignatureDish)
            ? Ui("Đặc sản", "Signature", "招牌")
            : _poi.SignatureDish;
        SignatureTitleLabel.Text = Ui("Top món", "Top dish", "招牌菜");

        OpeningHoursValueLabel.Text = string.IsNullOrWhiteSpace(_poi.DisplayOpeningHoursText)
            ? Ui("Đang cập nhật", "Updating", "更新中")
            : _poi.DisplayOpeningHoursText;
        OpeningHoursTitleLabel.Text = Ui("Giờ mở cửa", "Opening hours", "营业时间");
        OpeningStatusBadgeLabel.Text = _poi.DisplayOpenStatusText;
        OpeningStatusBadgeLabel.TextColor = MauiColor.FromArgb(_poi.DisplayOpenStatusTextColor);
        OpeningStatusBadge.BackgroundColor = MauiColor.FromArgb(_poi.DisplayOpenStatusBackgroundColor);
        OpeningStatusBadge.Stroke = MauiColor.FromArgb(_poi.DisplayOpenStatusStrokeColor);

        TabInfoBtn.Text = Ui("Thông tin", "Info", "信息");
        TabMenuBtn.Text = Ui("Menu", "Menu", "菜单");
        TabFunFactBtn.Text = Ui("Mẹo hay", "Tips", "温馨提示");

        StoryTitleLabel.Text = Ui("Câu chuyện", "Story", "故事");
        AudioGuideTitleLabel.Text = Ui("Hướng dẫn âm thanh", "Audio guide", "语音导览");

        CardTopDishTitleLabel.Text = Ui("Top món", "Top dish", "招牌菜");
        CardTopDishValueLabel.Text = string.IsNullOrWhiteSpace(_poi.SignatureDish)
            ? Ui("Menu đặc trưng", "Signature menu", "特色菜单")
            : _poi.SignatureDish;
        CardSpaceTitleLabel.Text = Ui("Không gian", "Ambience", "环境");
        CardSpaceValueLabel.Text = string.IsNullOrWhiteSpace(_poi.Category)
            ? Ui("Ẩm thực đường phố", "Street food", "街头美食")
            : _poi.Category;

        MenuSectionTitleLabel.Text = Ui("Danh sách món", "Menu list", "菜单列表");
        MenuEmptyTitleLabel.Text = Ui("Menu đang cập nhật", "Menu is being updated", "菜单更新中");

        FunFactSectionTitleLabel.Text = Ui("Khám phá thêm", "Explore more", "探索更多");
        NavigateCtaLabel.Text = Ui("Chỉ đường ngay", "Navigate now", "立即导航");
    }

    private void RefreshMenuBindings()
    {
        if (MenuCollectionView == null)
            return;

        var source = MenuCollectionView.ItemsSource;
        MenuCollectionView.ItemsSource = null;
        MenuCollectionView.ItemsSource = source;
    }

    // ── ViewModel property changed ────────────────────────────────
    // Only updates UI elements that actually changed — no thrashing
    private void Vm_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(POIDetailViewModel.IsAudioPlaying):
                MainThread.BeginInvokeOnMainThread(() =>
                    PlayPauseBtn.Text = _vm.IsAudioPlaying ? "\U000F03E4" : "\U000F040A");
                    
                break;

            case nameof(POIDetailViewModel.AudioProgress):
                MainThread.BeginInvokeOnMainThread(() =>
                    ApplyProgressFill(_vm.AudioProgress));
                break;
        }
    }

    /// <summary>
    /// Update progress fill width. Reads track width ONCE and caches it.
    /// Guard: skips if width not yet cached or ≤ 0.
    /// </summary>
    private void ApplyProgressFill(double progress)
    {
        if (!_progressLayoutReady || _cachedTrackWidth <= 0) return;

        var fillWidth = _cachedTrackWidth * Math.Clamp(progress, 0, 1);
        AudioProgressFill.WidthRequest = fillWidth;
    }

    // ── Tab navigation ───────────────────────────────────────────
    private void OnTabInfoClicked(object? sender, EventArgs e) => SwitchTab(0);
    private void OnTabMenuClicked(object? sender, EventArgs e) => SwitchTab(1);
    private void OnTabFunFactClicked(object? sender, EventArgs e)
    {
        SwitchTab(2);
    }

    private void OnHeaderLanguageTapped(object? sender, EventArgs e)
    {
        LanguageSwitcher.CycleLanguage(_langService);
    }

    private void SwitchTab(int idx)
    {
        if (_vm.SelectedTabIndex == idx) return;
        _vm.SelectedTabIndex = idx;

        // Button styles — set color directly, no layout pass
        TabInfoBtn.BackgroundColor  = idx == 0 ? MauiColor.FromArgb("#22c55e") : MauiColor.FromArgb("#16221e");
        TabInfoBtn.TextColor        = idx == 0 ? MauiColor.FromArgb("#004b1e") : MauiColor.FromArgb("#bccbb9");
        TabMenuBtn.BackgroundColor  = idx == 1 ? MauiColor.FromArgb("#22c55e") : MauiColor.FromArgb("#16221e");
        TabMenuBtn.TextColor        = idx == 1 ? MauiColor.FromArgb("#004b1e") : MauiColor.FromArgb("#bccbb9");
        TabFunFactBtn.BackgroundColor   = idx == 2 ? MauiColor.FromArgb("#22c55e") : MauiColor.FromArgb("#16221e");
        TabFunFactBtn.TextColor          = idx == 2 ? MauiColor.FromArgb("#004b1e") : MauiColor.FromArgb("#bccbb9");

        // Content visibility
        TabInfoContent.IsVisible = idx == 0;
        TabMenuContent.IsVisible = idx == 1;
        TabFunFactContent.IsVisible  = idx == 2;

        // Scroll to top only when leaving info tab
        if (idx != 0)
            MainScroll.ScrollToAsync(0, 0, false);

        if (idx == 1)
            _ = _vm.LoadMenuItemsAsync();
    }

    // ── Audio ─────────────────────────────────────────────────────
    private async void OnPlayPauseClicked(object? sender, EventArgs e)
    {
        await _vm.TogglePlayPauseCommand.ExecuteAsync(null);
    }

    private void OnAudioSeek(double normalizedValue)
    {
        _vm.OnSeek(normalizedValue);
    }

    // ── Like ─────────────────────────────────────────────────────
    private async void OnLikeTapped(object? sender, EventArgs e)
    {
        try
        {
            var db = MauiProgram.Services.GetRequiredService<ILocalDatabaseService>();
            _poi.IsLikedByUser = !_poi.IsLikedByUser;
            await db.SavePOIAsync(_poi);
            UpdateLikeIcon();

            var mainVm = MauiProgram.Services.GetService<ViewModels.MainViewModel>();
            if (mainVm != null)
                await mainVm.LoadSavedPOIsAsync(forceReload: true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[POIDetail] Like toggle error: {ex}");
        }
    }

    private void UpdateLikeIcon()
    {
        LikeIcon.Text = "\U000F02D1";
        LikeIcon.TextColor = _poi.IsLikedByUser
            ? MauiColor.FromArgb("#EF4444")
            : MauiColor.FromArgb("#FFFFFF");

        if (LikeButtonBorder != null)
        {
            LikeButtonBorder.BackgroundColor = _poi.IsLikedByUser
                ? MauiColor.FromArgb("#EF444418")
                : MauiColor.FromArgb("#66000000");
            LikeButtonBorder.Stroke = new SolidColorBrush(_poi.IsLikedByUser
                ? MauiColor.FromArgb("#EF444430")
                : MauiColor.FromArgb("#22000000"));
        }
    }

    // ── Navigation ─────────────────────────────────────────────────
    private async void OnBackTapped(object? sender, EventArgs e)
    {
        await Navigation.PopModalAsync(false);
    }

    private async void OnPhoneTapped(object? sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(_poi.PhoneNumber)) return;
        try
        {
            PhoneDialer.Default.Open(_poi.PhoneNumber);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[POIDetail] Phone error: {ex}");
            await CustomAlert.ShowAsync(AppStrings.Get("Common_Error"), AppStrings.Get("PoiDetail_Call_Error"), AppStrings.Get("Common_OK"), AlertType.Error);
        }
    }

    private async void OnNavigateTapped(object? sender, EventArgs e)
    {
        try
        {
            var vm = MauiProgram.Services.GetService<ViewModels.MainViewModel>();
            if (vm == null) return;

            // InZone mode already uses real-time geofence narration flow, so hide/disable route CTA.
            if (vm.CurrentExploreState == MainViewModel.ExploreState.InZone)
                return;

            if (vm.CurrentExploreState == MainViewModel.ExploreState.Far)
            {
                await CustomAlert.ShowAsync(
                    AppStrings.Get("PoiDetail_Far_Title"),
                    AppStrings.Get("PoiDetail_Far_Message"),
                    AppStrings.Get("Main_OfflineBasic_Ack"), AlertType.Warning);
                return;
            }

            var currentLoc = new Microsoft.Maui.Devices.Sensors.Location(vm.CurrentLat, vm.CurrentLon);
            var destLoc    = new Microsoft.Maui.Devices.Sensors.Location(_poi.Latitude, _poi.Longitude);
            var distKm = Microsoft.Maui.Devices.Sensors.Location.CalculateDistance(
                currentLoc, destLoc, Microsoft.Maui.Devices.Sensors.DistanceUnits.Kilometers);

            if (distKm > 1.0)
            {
                await CustomAlert.ShowAsync(Ui("Chế độ Xem Ảo", "Virtual mode", "虚拟模式"),
                    AppStrings.Get("PoiDetail_Virtual_Message"), AppStrings.Get("Main_OfflineBasic_Ack"), AlertType.Info);
                vm.IsVirtualNavigation = true;
            }
            else
            {
                vm.IsVirtualNavigation = false;
            }

            vm.NavigationTarget = _poi;
            await Navigation.PopModalAsync(false);

            if (Shell.Current != null)
                await Shell.Current.GoToAsync("//MapPage", false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[POIDetail] Navigate error: {ex}");
            await CustomAlert.ShowAsync(AppStrings.Get("Common_Error"), AppStrings.Get("PoiDetail_Navigate_Error"), AppStrings.Get("Common_OK"), AlertType.Error);
        }
    }

    private void UpdateNavigateCtaVisibility()
    {
        try
        {
            var vm = MauiProgram.Services.GetService<ViewModels.MainViewModel>();
            var isInZone = vm?.CurrentExploreState == MainViewModel.ExploreState.InZone;
            NavigateCtaBorder.IsVisible = !isInZone;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[POIDetail] UpdateNavigateCtaVisibility: {ex}");
            NavigateCtaBorder.IsVisible = true;
        }
    }

    private async void OnOpenFullMapClicked(object? sender, EventArgs e)
    {
        try
        {
            var vm = MauiProgram.Services.GetService<ViewModels.MainViewModel>();
            if (vm != null)
            {
                vm.NavigationTarget = _poi;
                vm.IsVirtualNavigation = true;
            }
            await Navigation.PopModalAsync(false);
            if (Shell.Current != null)
                await Shell.Current.GoToAsync("//MapPage", false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[POIDetail] OpenFullMap error: {ex}");
        }
    }

    // Map specific methods removed
}
