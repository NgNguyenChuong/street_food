// Mapsui – OSM map, offline-capable (replaces Microsoft.Maui.Maps / Google Maps)
using Mapsui;
using Mapsui.Layers;
using Mapsui.Projections;
using Mapsui.Styles;
using Mapsui.Tiling;
using Mapsui.UI.Maui;
// Disambiguate Mapsui.Styles.Color / Brush from MAUI types
using MapsColor = Mapsui.Styles.Color;
using MapsBrush = Mapsui.Styles.Brush;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using StreetFoodNarrator.App.Core.Models;
using StreetFoodNarrator.App.Core.Services;
using StreetFoodNarrator.App.ViewModels;
using System.Threading;

namespace StreetFoodNarrator.App.Views;

public partial class MainPage : ContentPage
{
    private readonly MainViewModel _vm;
    private readonly ITTSService _tts;
    private readonly LanguageService _lang;

    // ── Map state (Mapsui) ──────────────────────────────────────────────────
    private MemoryLayer? _pinsLayer;
    private bool _isPlaying;
    private bool _isMuted;
    private int _speedIndex = 0;
    private readonly double[] _speeds = { 1.0, 1.25, 1.5, 0.75 };
    private readonly string[] _speedLabels = { "1×", "1.25×", "1.5×", "0.75×" };
    private double _sheetOriginalY;
    private CancellationTokenSource? _waveCts;

    public MainPage(MainViewModel viewModel, ITTSService? ttsService = null, LanguageService? languageService = null)
    {
        InitializeComponent();
        BindingContext = _vm = viewModel;
        _tts = ttsService ?? MauiProgram.Services.GetRequiredService<ITTSService>();
        _lang = languageService ?? MauiProgram.Services.GetRequiredService<LanguageService>();

        _vm.PropertyChanged += OnViewModelPropertyChanged;
        // UpdateZonePins() is triggered via PropertyChanged on ActiveZoneCount
        // (fires once after all zones are added — avoids N+1 CollectionChanged rebuilds)
        Loaded += OnPageLoaded;
    }

    private void OnPageLoaded(object? sender, EventArgs e)
    {
        try
        {
            InitializeMap();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainPage] InitializeMap error: {ex}");
        }

        _ = _vm.LoadAllPoisAsync().ContinueWith(t =>
        {
            if (t.Exception != null)
                System.Diagnostics.Debug.WriteLine($"[MainPage] LoadAllPoisAsync error: {t.Exception}");
        }, TaskContinuationOptions.OnlyOnFaulted);

        _ = _vm.StartTrackingAsync().ContinueWith(t =>
        {
            if (t.Exception != null)
                System.Diagnostics.Debug.WriteLine($"[MainPage] StartTrackingAsync error: {t.Exception}");
        }, TaskContinuationOptions.OnlyOnFaulted);

        try { SetActiveTab(0); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[MainPage] SetActiveTab error: {ex}"); }
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.CurrentLat) ||
            e.PropertyName == nameof(MainViewModel.CurrentLon))
        {
            UpdateUserPin();
        }
        else if (e.PropertyName == nameof(MainViewModel.ActiveZoneCount))
        {
            // Fires once after ActiveZones is fully populated (not N+1 times via CollectionChanged)
            UpdateZonePins();
        }
        else if (e.PropertyName == nameof(MainViewModel.IsApproaching))
        {
            // Keep approaching banner in sync when on Tour tab
            if (_activeTabIndex == 1)
                ApproachingBanner.IsVisible = _vm.IsApproaching;
        }
    }

    // ── Map ───────────────────────────────────────────────────────────────

    private void InitializeMap()
    {
        if (MapView?.Map == null) return;

        // OSM tile layer – shows blank tiles when offline (no crash)
        try
        {
            var osmLayer = OpenStreetMap.CreateTileLayer("StreetFoodNarrator/1.0");
            MapView.Map.Layers.Add(osmLayer);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainPage] OSM tile layer error (offline?): {ex.Message}");
            // Continue without tile background — pins still work
        }

        // Memory layer for user pin + POI markers
        _pinsLayer = new MemoryLayer("Pins");
        MapView.Map.Layers.Add(_pinsLayer);

        // Tắt tất cả debug widgets mặc định của Mapsui
        // (thông tin tọa độ, zoom level, v.v. hiện ở góc trên)
        MapView.Map.Widgets.Clear();

        // ── Gesture & input configuration ──────────────────────────────────
        // Đảm bảo MAUI không nuốt mouse/scroll events trước khi
        // chúng đến native Mapsui MapControl (sửa lỗi scroll wheel không zoom)
        MapView.InputTransparent = false;
        MapView.CascadeInputTransparent = false;

        // Tắt rotation (giao diện di động không cần xoay bản đồ)
        MapView.Map.Navigator.RotationLock = true;

        // Bật fling/momentum khi pan
        MapView.UseFling = true;

        // Centre on Vinh Khanh on first load (resolution ≈1.2 ≈ zoom 17)
        // Defer so the MapControl has been sized before navigating
        Dispatcher.Dispatch(async () =>
        {
            await Task.Delay(150);
            var (cx, cy) = SphericalMercator.FromLonLat(
                AppConfig.DefaultLongitude, AppConfig.DefaultLatitude);
            var center = new MPoint(cx, cy);
            MapView?.Map?.Navigator.CenterOn(center);
            MapView?.Map?.Navigator.ZoomTo(1.2);
        });

        // Hook map tap event for pin popup
        MapView.Map.Info += OnMapInfoTapped;
    }

    private void UpdateZonePins()
    {
        if (_pinsLayer == null || MapView?.Map == null) return;

        var features = new List<IFeature>();
        var nearbyIds = _vm.ActiveZones.Select(z => z.Id).ToHashSet();

        // User location – white ring with green fill
        if (_vm.CurrentLat != 0)
        {
            var (ux, uy) = SphericalMercator.FromLonLat(_vm.CurrentLon, _vm.CurrentLat);
            var f = new PointFeature(new MPoint(ux, uy));
            f.Styles.Add(new SymbolStyle
            {
                SymbolScale = 0.6,
                Fill = new MapsBrush(new MapsColor(34, 197, 94)),
                Outline = new Pen(MapsColor.White, 3)
            });
            features.Add(f);
        }

        // All Spot POIs with status-based colors
        // Priority: ⭐ Saved(gold) > 🟣 Visited(purple) > 🔵 Nearby(blue) > 🟢 Default(green)
        foreach (var poi in _vm.AllPOIs)
        {
            if (poi.ZoneType == "Area" || poi.ZoneType == "District") continue;
            var (px, py) = SphericalMercator.FromLonLat(poi.Longitude, poi.Latitude);
            var f = new PointFeature(new MPoint(px, py));
            f["POI_ID"] = poi.Id;

            MapsColor fillColor;
            if (_vm.SavedPOIIds.Contains(poi.Id))
                fillColor = new MapsColor(251, 191, 36);  // ⭐ Gold = saved
            else if (_vm.VisitedPOIIds.Contains(poi.Id))
                fillColor = new MapsColor(147, 51, 234);  // 🟣 Purple = visited
            else if (nearbyIds.Contains(poi.Id))
                fillColor = new MapsColor(59, 130, 246);  // 🔵 Blue = nearby
            else
                fillColor = new MapsColor(34, 197, 94);   // 🟢 Green = unvisited

            f.Styles.Add(new SymbolStyle
            {
                SymbolScale = 0.5,
                Fill = new MapsBrush(fillColor),
                Outline = new Pen(MapsColor.White, 1.5f)
            });
            features.Add(f);
        }

        _pinsLayer.Features = features;
        _pinsLayer.DataHasChanged();
        MapView.RefreshGraphics();
    }

    private void UpdateUserPin()
    {
        UpdateZonePins();
        if (_vm.CurrentLat == 0 || MapView?.Map == null) return;
        var (ux, uy) = SphericalMercator.FromLonLat(_vm.CurrentLon, _vm.CurrentLat);
        MapView.Map.Navigator.CenterOn(new MPoint(ux, uy));
    }

    // ─── Header ────────────────────────────────────────────────────────────

    private async void OnBackClicked(object sender, EventArgs e)
    {
        try { await Navigation.PopAsync(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[MainPage] OnBackClicked error: {ex}"); }
    }

    private void OnCenterMapClicked(object sender, EventArgs e)
    {
        if (_vm.CurrentLat == 0 || MapView?.Map == null) return;
        var (cx, cy) = SphericalMercator.FromLonLat(_vm.CurrentLon, _vm.CurrentLat);
        MapView.Map.Navigator.CenterOn(new MPoint(cx, cy));
    }

    private async void OnSettingsClicked(object sender, EventArgs e)
    {
        try { await Navigation.PushAsync(new SettingsPage()); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[MainPage] OnSettingsClicked error: {ex}"); }
    }

    // ─── Zoom ──────────────────────────────────────────────────────────────

    private void OnZoomInClicked(object sender, EventArgs e)
        => MapView?.Map?.Navigator.ZoomIn(300);

    private void OnZoomOutClicked(object sender, EventArgs e)
        => MapView?.Map?.Navigator.ZoomOut(300);

    // ─── Bottom sheet pan ──────────────────────────────────────────────────

    private void OnBottomSheetPan(object sender, PanUpdatedEventArgs e)
    {
        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _sheetOriginalY = BottomSheet.TranslationY;
                break;

            case GestureStatus.Running:
                // Cho phép kéo lên tối đa 100px, xuống tối đa 80px
                var newY = _sheetOriginalY + e.TotalY;
                BottomSheet.TranslationY = Math.Clamp(newY, -100, 80);
                break;

            case GestureStatus.Completed:
                // Snap: nếu kéo lên quá giữa → giữ mở rộng, ngược lại về vị trí mặc định
                var snapTarget = BottomSheet.TranslationY < 0 ? -90 : 0;
                BottomSheet.TranslateTo(0, snapTarget, 220, Easing.CubicOut);
                break;
        }
    }

    // ─── Audio ─────────────────────────────────────────────────────────────

    private async void OnPlayPauseTapped(object sender, EventArgs e)
    {
        try
        {
        if (_isPlaying)
        {
            await _tts.StopAsync();
            _isPlaying = false;
            UpdatePlayIcon();
            StopWaveAnimation();
            return;
        }

        var zone = _vm.PrimaryZone;
        if (zone == null) return;

        string lang = _lang.CurrentLanguage switch
        {
            "en" => "en-US",
            "zh" => "zh-CN",
            _    => "vi-VN"
        };

        var text = _lang.CurrentLanguage switch
        {
            "en" => zone.Description_En ?? zone.Name_En ?? zone.Name_Vi,
            "zh" => zone.Description_Zh ?? zone.Name_Zh ?? zone.Name_En ?? zone.Name_Vi,
            _    => zone.Description_Vi ?? zone.Name_Vi ?? zone.Name_En
        } ?? "Chào mừng đến với điểm tham quan.";

        var ok = await _tts.SpeakAsync(text, lang);
        _isPlaying = ok;
        UpdatePlayIcon();
        if (ok) StartWaveAnimation();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainPage] OnPlayPauseTapped error: {ex}");
            _isPlaying = false;
            UpdatePlayIcon();
        }
    }

    private void UpdatePlayIcon()
    {
        PlayPauseIcon.Text = _isPlaying ? "❚❚" : "▶";
    }

    private void OnSeekBarDragCompleted(object sender, EventArgs e)
    {
        // Seek chưa được hỗ trợ với API streaming hiện tại
    }

    private void OnSpeedClicked(object sender, EventArgs e)
    {
        _speedIndex = (_speedIndex + 1) % _speeds.Length;
        SpeedLabel.Text = _speedLabels[_speedIndex];
        // Tốc độ phát chưa áp dụng vào stream; chỉ cập nhật UI
    }

    private void OnMuteClicked(object sender, EventArgs e)
    {
        _isMuted = !_isMuted;
        MuteIcon.Text = _isMuted ? "🔇" : "🔊";
        // Chưa nối vào player TTS hiện tại
    }

    // ─── Wave animation ────────────────────────────────────────────────────

    private void StartWaveAnimation()
    {
        _waveCts?.Cancel();
        _waveCts = new CancellationTokenSource();
        var token = _waveCts.Token;

        _ = Task.Run(async () =>
        {
            var waves = new[] { Wave1, Wave2, Wave3, Wave4, Wave5, Wave6, Wave7, Wave8 };
            var baseHeights = new double[] { 8, 14, 22, 18, 26, 14, 20, 10 };
            var rand = new Random();

            while (!token.IsCancellationRequested)
            {
                // Use ScaleY (GPU render transform) instead of HeightRequest to avoid
                // triggering layout/measure passes on the main thread every frame.
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    for (int i = 0; i < waves.Length; i++)
                    {
                        double target = baseHeights[i] + rand.NextDouble() * 8 - 4;
                        waves[i].AnchorY = 1.0; // scale from bottom
                        waves[i].ScaleY  = Math.Clamp(target / baseHeights[i], 0.15, 1.8);
                    }
                });
                await Task.Delay(400, token); // 2.5 fps — was 160 ms (6 fps)
            }
        }, token);
    }

    private void StopWaveAnimation()
    {
        _waveCts?.Cancel();
        var waves = new[] { Wave1, Wave2, Wave3, Wave4, Wave5, Wave6, Wave7, Wave8 };
        for (int i = 0; i < waves.Length; i++)
            waves[i].ScaleY = 1.0; // reset to original height
    }

    // ─── Description expand ────────────────────────────────────────────────

    private bool _isDescExpanded;

    private void OnExpandDescriptionClicked(object sender, EventArgs e)
    {
        _isDescExpanded = !_isDescExpanded;
        DescriptionLabel.MaxLines        = _isDescExpanded ? int.MaxValue : 2;
        DescriptionLabel.LineBreakMode   = _isDescExpanded ? LineBreakMode.WordWrap : LineBreakMode.TailTruncation;
        ExpandDescLabel.Text             = _isDescExpanded ? "THU GỌN" : "XEM THÊM";
    }

    // ─── Share ─────────────────────────────────────────────────────────────

    private async void OnShareClicked(object sender, EventArgs e)
    {
        try
        {
            var text = $"Tôi đang ở {_vm.PrimaryZoneName} trong tour ẩm thực!";
            await Share.Default.RequestAsync(new ShareTextRequest
            {
                Title = "Chia sẻ điểm đến",
                Text  = text,
                Uri   = "https://streetfoodnarrator.app"
            });
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[MainPage] OnShareClicked error: {ex}"); }
    }

    // ─── Bottom nav ────────────────────────────────────────────────────────
    // Tab 0=Bản đồ  1=Hành trình  2=Menu  3=Đã lưu  4=Cài đặt

    private int _activeTabIndex = 0;
    private double _navItemWidth = 0;
    private bool _navWidthMeasured = false;

    private void OnNavMapClicked(object sender, EventArgs e)      => SetActiveTab(0);
    private void OnNavTourClicked(object sender, EventArgs e)     => SetActiveTab(1);
    private void OnNavMenuClicked(object sender, EventArgs e)     => SetActiveTab(2);
    private void OnNavSavedClicked(object sender, EventArgs e)    => SetActiveTab(3);
    private async void OnNavSettingsClicked(object sender, EventArgs e)
    {
        try { await Navigation.PushAsync(new SettingsPage()); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[MainPage] OnNavSettingsClicked error: {ex}"); }
    }

    private void SetActiveTab(int tabIndex)
    {
        _activeTabIndex = tabIndex;

        // ── Content visibility ──────────────────────────────────────────
        // Tab 0: map controls visible, tour UI hidden, overlays hidden
        MapTopBar.IsVisible        = (tabIndex == 0);
        MapLegend.IsVisible        = (tabIndex == 0);
        MapRightControls.IsVisible = (tabIndex == 0);
        PinPopupCard.IsVisible     = false; // always dismiss on tab switch

        HeaderBar.IsVisible        = (tabIndex == 1);
        ApproachingBanner.IsVisible= (tabIndex == 1) && _vm.IsApproaching;
        BottomSheet.IsVisible      = (tabIndex == 1);

        MenuOverlay.IsVisible      = (tabIndex == 2);
        SavedOverlay.IsVisible     = (tabIndex == 3);

        // ── Nav indicator colors ─────────────────────────────────────────
        var active   = Microsoft.Maui.Graphics.Color.FromArgb("#F97316");
        var inactive = Microsoft.Maui.Graphics.Color.FromArgb("#6B7280");

        Label[] glyphs = [ NavMapGlyph, NavTourGlyph, NavMenuGlyph, NavSavedGlyph, NavSettingsGlyph ];
        Label[] labels = [ NavMapLabel, NavTourLabel, NavMenuLabel, NavSavedLabel, NavSettingsLabel ];

        for (int i = 0; i < glyphs.Length; i++)
        {
            bool isActive = (i == tabIndex);
            glyphs[i].TextColor      = isActive ? active   : inactive;
            labels[i].TextColor      = isActive ? active   : inactive;
            labels[i].FontAttributes = isActive ? FontAttributes.Bold : FontAttributes.None;
        }

        SlideIndicatorToTab(tabIndex);
    }

    private void SlideIndicatorToTab(int tabIndex)
    {
        double navWidth = BottomNavBar.Width > 0
            ? BottomNavBar.Width
            : DeviceDisplay.MainDisplayInfo.Width / DeviceDisplay.MainDisplayInfo.Density;
        _navItemWidth = (navWidth - 32) / 5.0; // 5 tabs
        NavIndicator.TranslateTo(tabIndex * _navItemWidth, 0, 220, Easing.CubicOut);
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (width > 0 && !_navWidthMeasured)
        {
            _navWidthMeasured = true;
            _navItemWidth     = (width - 32) / 5.0;
            NavIndicator.TranslationX = _activeTabIndex * _navItemWidth;
        }
    }

    // ─── Map pin popup ──────────────────────────────────────────────────────

    private void OnMapInfoTapped(object? sender, Mapsui.MapInfoEventArgs e)
    {
        if (_activeTabIndex != 0) return;
        var worldPos = e.WorldPosition;
        if (worldPos == null) return;

        // Find nearest Spot POI to the tapped world position
        POI? nearest = null;
        double minDist = 600; // world-unit threshold (~20m at zoom 17)
        foreach (var poi in _vm.AllPOIs)
        {
            if (poi.ZoneType == "Area" || poi.ZoneType == "District") continue;
            var (px, py) = SphericalMercator.FromLonLat(poi.Longitude, poi.Latitude);
            double dist = Math.Sqrt(Math.Pow(worldPos.X - px, 2) + Math.Pow(worldPos.Y - py, 2));
            if (dist < minDist) { minDist = dist; nearest = poi; }
        }

        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (nearest != null)
            {
                _vm.SelectedPinPOI   = nearest;
                _vm.IsPinPopupVisible = true;
                PinPopupCard.IsVisible = true;
            }
            else
            {
                PinPopupCard.IsVisible = false;
            }
        });
    }

    private void OnPinPlayAudio(object sender, EventArgs e)
    {
        if (_vm.SelectedPinPOI == null) return;
        // Trigger audio play via TTS (same flow as tour auto-play)
        _ = _tts.SpeakAsync(
            _vm.SelectedPinPOI.Description_Vi ?? _vm.SelectedPinPOI.Name_Vi ?? "Điểm thăm quan",
            "vi-VN");
    }

    private async void OnPinNavigate(object sender, EventArgs e)
    {
        try
        {
            var poi = _vm.SelectedPinPOI;
            if (poi == null) return;
            var uri = $"https://www.google.com/maps/dir/?api=1&destination={poi.Latitude},{poi.Longitude}";
            await Launcher.Default.OpenAsync(new Uri(uri));
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[MainPage] OnPinNavigate error: {ex}"); }
    }

    private void OnPinSave(object sender, EventArgs e)
    {
        if (_vm.SelectedPinPOI != null)
            _vm.ToggleSavePOICommand.Execute(_vm.SelectedPinPOI);
        UpdateZonePins();
    }

    // ─── Menu filter chips ─────────────────────────────────────────────────

    private void ApplyFilterChipStyle(Border? active)
    {
        var chips = new[] { FilterChipAll, FilterChipPho, FilterChipBanhMi, FilterChipCom, FilterChipChe };
        foreach (var c in chips)
        {
            bool isActive = c == active;
            c.BackgroundColor = Microsoft.Maui.Graphics.Color.FromArgb(isActive ? "#F97316" : "#0E1E17");
            c.Stroke          = new SolidColorBrush(Microsoft.Maui.Graphics.Color.FromArgb(isActive ? "#F97316" : "#1C3024"));
        }
    }

    private void OnFilterAll(object sender, EventArgs e)
    {
        _vm.SearchQuery = "";
        _vm.ApplyFilter();
        ApplyFilterChipStyle(FilterChipAll);
    }

    private void OnFilterPho(object sender, EventArgs e)
    {
        _vm.SearchQuery = "phở";
        _vm.ApplyFilter();
        ApplyFilterChipStyle(FilterChipPho);
    }

    private void OnFilterBanhMi(object sender, EventArgs e)
    {
        _vm.SearchQuery = "bánh mì";
        _vm.ApplyFilter();
        ApplyFilterChipStyle(FilterChipBanhMi);
    }

    private void OnFilterCom(object sender, EventArgs e)
    {
        _vm.SearchQuery = "cơm";
        _vm.ApplyFilter();
        ApplyFilterChipStyle(FilterChipCom);
    }

    private void OnFilterChe(object sender, EventArgs e)
    {
        _vm.SearchQuery = "chè";
        _vm.ApplyFilter();
        ApplyFilterChipStyle(FilterChipChe);
    }


    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _waveCts?.Cancel();
        _ = _tts.StopAsync();
    }
}