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

namespace StreetFoodNarrator.App.Views;

public partial class POIDetailPage : ContentPage
{
    private readonly POI _poi;
    private readonly POIDetailViewModel _vm;
    private bool _mapInitialized = false;

    // Cache track width ONCE when layout is ready — avoid repeated Width reads
    private double _cachedTrackWidth = -1;
    private bool _progressLayoutReady = false;

    public POIDetailPage(POI poi, bool keepCurrentAudio = false)
    {
        InitializeComponent();

        _poi = poi;

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
        // Cache track width once layout is ready — delay 50ms so frame has settled
        _ = Task.Run(async () =>
        {
            await Task.Delay(50);
            MainThread.BeginInvokeOnMainThread(CacheTrackWidth);
        });

        // Initialize map once page is loaded (not lazily on tab click)
        InitializeMap();
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
        // Refresh like icon and localized content when page re-appears
        UpdateLikeIcon();
        _vm.RefreshLocalizedContent();
        UpdateNavigateCtaVisibility();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _vm.PropertyChanged -= Vm_PropertyChanged;
        _vm.Cleanup();
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
    private void OnTabMapClicked(object? sender, EventArgs e)
    {
        SwitchTab(2);
        if (!_mapInitialized)
            InitializeMap();
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
        TabMapBtn.BackgroundColor   = idx == 2 ? MauiColor.FromArgb("#22c55e") : MauiColor.FromArgb("#16221e");
        TabMapBtn.TextColor          = idx == 2 ? MauiColor.FromArgb("#004b1e") : MauiColor.FromArgb("#bccbb9");

        // Content visibility
        TabInfoContent.IsVisible = idx == 0;
        TabMenuContent.IsVisible = idx == 1;
        TabMapContent.IsVisible  = idx == 2;

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
                await mainVm.LoadSavedPOIsAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[POIDetail] Like toggle error: {ex}");
        }
    }

    private void UpdateLikeIcon()
    {
        LikeIcon.Text = "\uF02D1";
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
        await Navigation.PopModalAsync();
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
            await DisplayAlertAsync("Lỗi", "Không thể gọi điện.", "OK");
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
                await DisplayAlertAsync(
                    "Bạn đang ở xa",
                    "Khu ẩm thực nên chỉ có thể thực hiện chức năng xem ảo.",
                    "Đã hiểu");
                return;
            }

            var currentLoc = new Microsoft.Maui.Devices.Sensors.Location(vm.CurrentLat, vm.CurrentLon);
            var destLoc    = new Microsoft.Maui.Devices.Sensors.Location(_poi.Latitude, _poi.Longitude);
            var distKm = Microsoft.Maui.Devices.Sensors.Location.CalculateDistance(
                currentLoc, destLoc, Microsoft.Maui.Devices.Sensors.DistanceUnits.Kilometers);

            if (distKm > 1.0)
            {
                await DisplayAlertAsync("Chế độ Xem Ảo",
                    "Bạn đang ở cách quán hơn 1km. Bản đồ sẽ chuyển sang tương tác Xem Ảo.", "Đã Hiểu");
                vm.IsVirtualNavigation = true;
            }
            else
            {
                vm.IsVirtualNavigation = false;
            }

            vm.NavigationTarget = _poi;
            await Navigation.PopModalAsync();

            if (Shell.Current != null)
                await Shell.Current.GoToAsync("//MapPage");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[POIDetail] Navigate error: {ex}");
            await DisplayAlertAsync("Lỗi", "Không thể chỉ đường lúc này.", "OK");
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
            await Navigation.PopModalAsync();
            if (Shell.Current != null)
                await Shell.Current.GoToAsync("//MapPage");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[POIDetail] OpenFullMap error: {ex}");
        }
    }

    // ── Map ───────────────────────────────────────────────────────
    private void InitializeMap()
    {
        if (_mapInitialized || MiniMapView?.Map == null) return;
        _mapInitialized = true;

        try
        {
            var cacheDb = Path.Combine(FileSystem.AppDataDirectory, "map_cache", "tiles.db");
            Directory.CreateDirectory(Path.GetDirectoryName(cacheDb)!);
            var tileCache = new Services.SimpleTileCache(cacheDb);

            var tileSource = new HttpTileSource(
                new GlobalSphericalMercator(),
                "https://a.basemaps.cartocdn.com/rastertiles/voyager/{z}/{x}/{y}.png",
                name: "Carto",
                persistentCache: tileCache
            );

            var baseLayer = new TileLayer(tileSource) { Name = "BaseMap", Opacity = 0.9 };
            MiniMapView.Map.BackColor = new MapsColor(5, 16, 13);
            MiniMapView.Map.Layers.Add(baseLayer);

            // POI marker
            var poiLoc = SphericalMercator.FromLonLat(_poi.Longitude, _poi.Latitude);
            var feature = new PointFeature(new MPoint(poiLoc.x, poiLoc.y));
            feature.Styles = new IStyle[]
            {
                new SymbolStyle
                {
                    Fill = new MapsBrush(new MapsColor(34, 197, 94)),
                    Outline = new Pen(new MapsColor(255, 255, 255), 2),
                    SymbolScale = 0.9,
                    SymbolType = SymbolType.Ellipse
                },
                new LabelStyle
                {
                    Text = _poi.Name_Vi,
                    BackColor = new MapsBrush(new MapsColor(0, 0, 0, 180)),
                    ForeColor = new MapsColor(255, 255, 255),
                    Halo = new Pen(new MapsColor(5, 16, 13), 2),
                    Offset = new Offset(0, 18)
                }
            };

            var markerLayer = new MemoryLayer("POIMarker")
            {
                Features = new[] { feature }
            };
            MiniMapView.Map.Layers.Add(markerLayer);

            MiniMapView.Map.Navigator.CenterOn(poiLoc.x, poiLoc.y);
            MiniMapView.Map.Navigator.ZoomTo(18.5);
            MiniMapView.Map.Widgets.Clear();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[POIDetail] Map init error: {ex}");
        }
    }
}
