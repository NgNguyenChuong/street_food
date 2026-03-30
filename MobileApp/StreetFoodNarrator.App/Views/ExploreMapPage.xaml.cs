using System.Collections.Specialized;
using System.ComponentModel;
using BruTile.Predefined;
using BruTile.Web;
using Mapsui;
using Mapsui.Layers;
using Mapsui.Nts;
using Mapsui.Projections;
using Mapsui.Styles;
using Mapsui.Tiling;
using Mapsui.Tiling.Layers;
using Microsoft.Extensions.DependencyInjection;
using NetTopologySuite.Geometries;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using Microsoft.Maui.Dispatching;
using StreetFoodNarrator.App.Core.Models;
using StreetFoodNarrator.App.Core.Services;
using StreetFoodNarrator.App.ViewModels;
using MapsColor = Mapsui.Styles.Color;
using MapsBrush = Mapsui.Styles.Brush;
using MauiColor = Microsoft.Maui.Graphics.Color;
using MauiImage = Microsoft.Maui.Controls.Image;

namespace StreetFoodNarrator.App.Views;

public partial class ExploreMapPage : ContentPage
{
    private readonly MainViewModel _vm;
    private readonly ITTSService _tts;
    private readonly LanguageService _lang;
    private readonly bool _isNearFocusMode;
    private readonly List<POI> _nearFocusNearbyPois = new();
    private MemoryLayer? _pinsLayer;
    private MemoryLayer? _userPinLayer;
    private MemoryLayer? _routeLayer;
    private MemoryLayer? _zoneRingLayer;
    private bool _isMapInitialized;
    private bool _isFirstLocation = true;
    private bool _eventsHooked;
    private bool _isClosing;
    private int? _previewPoiId;
    private bool _isPreviewPaused;
    private readonly HttpClient _routeHttpClient = new() { Timeout = TimeSpan.FromSeconds(6) };
    private bool _isNearRouteActive;
    private int? _nearRouteTargetPoiId;
    private double _nearRouteInitialDistanceMeters;
    private double _nearRouteDistanceMeters;
    private double _nearRouteDurationSeconds;
    private Coordinate[]? _nearRouteCoordinates;
    private bool _isNearSheetCollapsed;
    private MainViewModel.ExploreState _lastNearFocusState = MainViewModel.ExploreState.Far;
    private IDispatcherTimer? _previewUiTimer;
    private bool _syncPrimaryZoneOnNextAppearing;
    private bool _isOpeningPoiDetail;
    private int? _lastAutoNarratedPoiId;
    private int? _lastInZoneFocusedPoiId;
    private int? _activeSpotZonePoiId;
    private DateTime _lastSpotZoneSwitchAtUtc = DateTime.MinValue;
    private const int DefaultMapZoomLevel = (int)AppConfig.DefaultZoom + 1;
    private const int NearFocusZoomLevel = 18;
    private const int InZoneFocusZoomLevel = 19;
    private const double NearFocusMidpointMeters = 800;
    private const double SpotZoneHysteresisMeters = 8;
    private static readonly TimeSpan SpotZoneSwitchCooldown = TimeSpan.FromSeconds(8);
    private const string IconHeart = "\U000F02D1";
    private const string IconPlay = "\U000F040A";
    private const string IconPause = "\U000F03E4";

    public ExploreMapPage()
        : this(null, false)
    {
    }

    public ExploreMapPage(MainViewModel? viewModel, bool nearFocusMode = false)
    {
        InitializeComponent();

        _isNearFocusMode = nearFocusMode;
        _vm = viewModel ?? ResolveRequiredService<MainViewModel>();
        _tts = ResolveRequiredService<ITTSService>();
        _lang = ResolveRequiredService<LanguageService>();
        BindingContext = _vm;
        TabMapComponent.BindingContext = _vm;
    }

    public bool IsNearFocusMode => _isNearFocusMode;

    public void Prewarm()
    {
        try
        {
            EnsureMapInitialized();
            UpdateZonePins();
            UpdateUserPin();
            UpdateNearFocusRoute();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ExploreMapPage] Prewarm error: {ex.Message}");
        }
    }

    private void InitializeNearFocusMode()
    {
        _isNearSheetCollapsed = false;

        var focusPoi = ResolveNearFocusPoi();
        if (focusPoi != null)
        {
            SetNearRoutingCandidate(focusPoi, clearActiveRoute: true);
        }

        CenterMapForCurrentState();
        UpdateNearFocusOverlayState();
        UpdateNearFocusRoute();
    }

    private POI? ResolveNearFocusPoi()
    {
        var preferred = _vm.NavigationTarget
            ?? _vm.SelectedPinPOI
            ?? _vm.PrimaryZone;
        if (preferred != null)
            return preferred;

        var featuredId = _vm.FeaturedExplorePoi?.Id;
        if (featuredId.HasValue)
            return _vm.AllPOIs.FirstOrDefault(p => p.ZoneType == "Spot" && p.Id == featuredId.Value);

        return _vm.AllPOIs.FirstOrDefault(p => p.ZoneType == "Spot");
    }

    private void SetNearRoutingCandidate(POI poi, bool clearActiveRoute)
    {
        _vm.NavigationTarget = poi;
        _vm.SelectedPinPOI = poi;
        _vm.VisitedPOIIds.Add(poi.Id);

        if (clearActiveRoute)
        {
            _isNearRouteActive = false;
            _nearRouteTargetPoiId = null;
            _nearRouteInitialDistanceMeters = 0;
            _nearRouteDistanceMeters = 0;
            _nearRouteDurationSeconds = 0;
            _nearRouteCoordinates = null;
        }
    }

    private void SyncNearFocusPoiFromPrimaryZone()
    {
        if (!_isNearFocusMode)
            return;

        if (ResolveOverlayExploreState() != MainViewModel.ExploreState.InZone)
            return;

        var primary = _vm.PrimaryZone;
        if (primary == null || primary.ZoneType != "Spot")
            return;

        SetNearRoutingCandidate(primary, clearActiveRoute: true);
    }

    private void EnsurePreviewUiTimer()
    {
        if (_previewUiTimer != null || Dispatcher == null)
            return;

        _previewUiTimer = Dispatcher.CreateTimer();
        _previewUiTimer.Interval = TimeSpan.FromMilliseconds(350);
        _previewUiTimer.IsRepeating = true;
        _previewUiTimer.Tick += (_, _) =>
        {
            if (!_isNearFocusMode || !IsVisible)
                return;

            if (_tts.IsPlaying() || _isPreviewPaused)
                UpdateNearFocusOverlayState();
        };
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        HookEvents();
        EnsurePreviewUiTimer();
        _previewUiTimer?.Start();
        if (_vm.Categories.Count == 0)
            _vm.BuildMapCategories();
        EnsureMapInitialized();

        if (_isNearFocusMode &&
            (_syncPrimaryZoneOnNextAppearing || ResolveOverlayExploreState() == MainViewModel.ExploreState.InZone))
        {
            _syncPrimaryZoneOnNextAppearing = false;
            SyncNearFocusPoiFromPrimaryZone();
        }

        if (_isNearFocusMode)
        {
            TabMapComponent.IsVisible = false;
            NearFocusOverlay.IsVisible = true;
            InitializeNearFocusMode();
        }
        else
        {
            NearFocusOverlay.IsVisible = false;
            TabMapComponent.IsVisible = true;
            CenterMapForCurrentState();
        }

        UpdateZonePins();
        UpdateUserPin();
        if (!_isNearFocusMode)
        {
            TabMapComponent.InitializeChips(_vm.SelectedCategory);
            TabMapComponent.RefreshLikeIcon();
        }
        UpdatePreviewAudioUiState();
        if (_isNearFocusMode)
            _ = EnsureInZoneNarrationAndFocusAsync();
        _ = _vm.StartTrackingAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _previewUiTimer?.Stop();
        UnhookEvents();
    }

    private void HookEvents()
    {
        if (_eventsHooked)
            return;

        _vm.PropertyChanged += OnViewModelPropertyChanged;
        _tts.OnPlaybackEnded += OnPreviewPlaybackEnded;

        TabMapComponent.BackRequested += OnBackRequested;
        TabMapComponent.ExploreNavRequested += OnBackRequested;
        TabMapComponent.SavedNavRequested += OnSavedRequested;
        TabMapComponent.ProfileNavRequested += OnProfileRequested;
        TabMapComponent.SettingsRequested += OnSettingsRequested;
        TabMapComponent.ZoomInRequested += OnZoomInRequested;
        TabMapComponent.ZoomOutRequested += OnZoomOutRequested;
        TabMapComponent.CenterMapRequested += OnCenterMapRequested;
        TabMapComponent.ViewDetailRequested += OnViewDetailRequested;
        TabMapComponent.PreviewAudioRequested += OnPreviewAudioRequested;
        TabMapComponent.StartFromHereRequested += OnStartFromHereRequested;
        TabMapComponent.PrevPoiRequested += OnPrevPoiRequested;
        TabMapComponent.NextPoiRequested += OnNextPoiRequested;
        TabMapComponent.LikeRequested += OnLikeRequested;
        TabMapComponent.CategorySelected += OnCategorySelected;
        TabMapComponent.SearchTextChanged += OnSearchTextChanged;
        TabMapComponent.ClearSearchRequested += OnClearSearchRequested;
        TabMapComponent.SuggestionSelected += OnSuggestionSelected;

        _eventsHooked = true;
    }

    private void UnhookEvents()
    {
        if (!_eventsHooked)
            return;

        _vm.PropertyChanged -= OnViewModelPropertyChanged;
        _tts.OnPlaybackEnded -= OnPreviewPlaybackEnded;

        TabMapComponent.BackRequested -= OnBackRequested;
        TabMapComponent.ExploreNavRequested -= OnBackRequested;
        TabMapComponent.SavedNavRequested -= OnSavedRequested;
        TabMapComponent.ProfileNavRequested -= OnProfileRequested;
        TabMapComponent.SettingsRequested -= OnSettingsRequested;
        TabMapComponent.ZoomInRequested -= OnZoomInRequested;
        TabMapComponent.ZoomOutRequested -= OnZoomOutRequested;
        TabMapComponent.CenterMapRequested -= OnCenterMapRequested;
        TabMapComponent.ViewDetailRequested -= OnViewDetailRequested;
        TabMapComponent.PreviewAudioRequested -= OnPreviewAudioRequested;
        TabMapComponent.StartFromHereRequested -= OnStartFromHereRequested;
        TabMapComponent.PrevPoiRequested -= OnPrevPoiRequested;
        TabMapComponent.NextPoiRequested -= OnNextPoiRequested;
        TabMapComponent.LikeRequested -= OnLikeRequested;
        TabMapComponent.CategorySelected -= OnCategorySelected;
        TabMapComponent.SearchTextChanged -= OnSearchTextChanged;
        TabMapComponent.ClearSearchRequested -= OnClearSearchRequested;
        TabMapComponent.SuggestionSelected -= OnSuggestionSelected;

        if (MapView != null)
            MapView.Info -= OnMapInfoTapped;

        _eventsHooked = false;
    }

    private void EnsureMapInitialized()
    {
        if (_isMapInitialized)
            return;

        if (MapView?.Map == null)
            return;

        var cacheDb = Path.Combine(FileSystem.AppDataDirectory, "map_cache", "tiles.db");
        var tileCache = new StreetFoodNarrator.App.Services.SimpleTileCache(cacheDb);
        var tileSource = new HttpTileSource(
            new GlobalSphericalMercator(),
            "https://a.basemaps.cartocdn.com/rastertiles/voyager/{z}/{x}/{y}.png",
            name: "Carto",
            persistentCache: tileCache);

        MapView.Map.Layers.Clear();
        MapView.Map.Widgets.Clear();
        MapView.Map.Layers.Add(new TileLayer(tileSource) { Name = "BaseMap" });

        var darkOverlay = new MemoryLayer("DarkOverlay")
        {
            Style = new VectorStyle
            {
                Fill = new MapsBrush(new MapsColor(8, 22, 12, 98)),
                Outline = null
            }
        };

        var worldExtent = new[]
        {
            new MPoint(-20037508.34, -20037508.34),
            new MPoint(20037508.34, -20037508.34),
            new MPoint(20037508.34, 20037508.34),
            new MPoint(-20037508.34, 20037508.34),
            new MPoint(-20037508.34, -20037508.34)
        };
        var overlayPolygon = new Polygon(
            new LinearRing(worldExtent.Select(p => new Coordinate(p.X, p.Y)).ToArray()));
        darkOverlay.Features = new[] { new GeometryFeature { Geometry = overlayPolygon } };
        MapView.Map.Layers.Add(darkOverlay);

        _routeLayer = new MemoryLayer("RouteLayer");
        MapView.Map.Layers.Add(_routeLayer);
        _zoneRingLayer = new MemoryLayer("ZoneRingLayer");
        MapView.Map.Layers.Add(_zoneRingLayer);
        _userPinLayer = new MemoryLayer("UserPin");
        _pinsLayer = new MemoryLayer("Pins");
        MapView.Map.Layers.Add(_userPinLayer);
        MapView.Map.Layers.Add(_pinsLayer);

        MapView.InputTransparent = false;
        MapView.CascadeInputTransparent = false;
        MapView.Info -= OnMapInfoTapped;
        MapView.Info += OnMapInfoTapped;
        MapView.Map.Navigator.RotationLock = true;
        MapView.UseFling = true;

        var (cx, cy) = SphericalMercator.FromLonLat(AppConfig.DefaultLongitude, AppConfig.DefaultLatitude);
        MapView.Map.Navigator.CenterOn(new MPoint(cx, cy));
        ZoomToDefaultLevel();
        _isMapInitialized = true;
    }

    private void ZoomToDefaultLevel()
    {
        if (MapView?.Map?.Navigator == null)
            return;

        var resolutions = MapView.Map.Navigator.Resolutions;
        if (resolutions == null)
            return;

        var maxLevel = Math.Max(0, resolutions.Count() - 1);
        var level = Math.Clamp(DefaultMapZoomLevel, 0, maxLevel);
        MapView.Map.Navigator.ZoomToLevel(level);
    }

    private void UpdateZonePins()
    {
        if (!_isMapInitialized || _pinsLayer == null || MapView?.Map == null)
            return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (_pinsLayer == null)
                return;

            var nearbyIds = _vm.ActiveZones.Select(z => z.Id).ToHashSet();
            var visiblePoiIds = _vm.FilteredPOIs.Select(p => p.Id).ToHashSet();
            var features = new List<IFeature>();

            foreach (var poi in _vm.AllPOIs)
            {
                try
                {
                    if (poi.ZoneType == "Area" || poi.ZoneType == "District")
                        continue;

                    if (visiblePoiIds.Count > 0 && !visiblePoiIds.Contains(poi.Id))
                        continue;

                    if (double.IsNaN(poi.Latitude) || double.IsNaN(poi.Longitude))
                        continue;

                    var (px, py) = SphericalMercator.FromLonLat(poi.Longitude, poi.Latitude);
                    var poiFeature = new PointFeature(new MPoint(px, py));
                    poiFeature["POI_ID"] = poi.Id;

                    MapsColor fillColor;
                    if (_vm.SavedPOIIds.Contains(poi.Id))
                        fillColor = new MapsColor(251, 191, 36);
                    else if (_vm.VisitedPOIIds.Contains(poi.Id))
                        fillColor = new MapsColor(147, 51, 234);
                    else if (nearbyIds.Contains(poi.Id))
                        fillColor = new MapsColor(59, 130, 246);
                    else
                        fillColor = new MapsColor(249, 115, 22);

                    poiFeature.Styles.Add(new SymbolStyle
                    {
                        SymbolScale = 0.5,
                        Fill = new MapsBrush(fillColor),
                        Outline = new Pen(MapsColor.White, 2.5f),
                        SymbolType = SymbolType.Ellipse
                    });

                    var labelText = poi.Name_Vi ?? poi.Name_En ?? string.Empty;
                    if (labelText.Length > 15)
                        labelText = labelText[..15];

                    poiFeature.Styles.Add(new LabelStyle
                    {
                        Text = labelText,
                        ForeColor = MapsColor.White,
                        BackColor = new MapsBrush(new MapsColor(10, 16, 12, 220)),
                        Font = new Mapsui.Styles.Font { FontFamily = "sans-serif", Size = 8 },
                        Offset = new Offset(0, 18),
                        HorizontalAlignment = LabelStyle.HorizontalAlignmentEnum.Center,
                        VerticalAlignment = LabelStyle.VerticalAlignmentEnum.Top
                    });

                    features.Add(poiFeature);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ExploreMapPage] Skip invalid POI #{poi.Id}: {ex.Message}");
                }
            }

            _pinsLayer.Features = features;
            _pinsLayer.DataHasChanged();
            UpdateInZoneZoneRing();
            MapView.RefreshGraphics();
        });
    }

    private void UpdateInZoneZoneRing()
    {
        if (!_isMapInitialized || _zoneRingLayer == null || MapView?.Map == null)
            return;

        var inZoneState = ResolveOverlayExploreState() == MainViewModel.ExploreState.InZone;
        var zonePoi = ResolveCurrentSpotZonePoi();
        if (!inZoneState || zonePoi == null || !IsUserInsidePoiZone(zonePoi))
        {
            _zoneRingLayer.Features = Array.Empty<IFeature>();
            _zoneRingLayer.DataHasChanged();
            _lastInZoneFocusedPoiId = null;
            _lastAutoNarratedPoiId = null;
            _activeSpotZonePoiId = null;
            return;
        }

        var radiusMeters = GetPoiAudioZoneRadiusMeters(zonePoi);
        var ringCoords = CreateZoneRingCoordinates(zonePoi.Latitude, zonePoi.Longitude, radiusMeters);
        if (ringCoords.Length < 4)
        {
            _zoneRingLayer.Features = Array.Empty<IFeature>();
            _zoneRingLayer.DataHasChanged();
            return;
        }

        var ring = new GeometryFeature(new Polygon(new LinearRing(ringCoords)));
        ring.Styles.Add(new VectorStyle
        {
            Fill = new MapsBrush(new MapsColor(52, 211, 153, 42)),
            Outline = new Pen(new MapsColor(52, 211, 153, 190), 2.5f)
        });

        _zoneRingLayer.Features = new IFeature[] { ring };
        _zoneRingLayer.DataHasChanged();
    }

    private void UpdateUserPin()
    {
        if (!_isMapInitialized || _userPinLayer == null || MapView?.Map == null)
            return;

        var hasUserFix = _vm.CurrentLat != 0 || _vm.CurrentLon != 0;
        var pinLat = hasUserFix ? _vm.CurrentLat : AppConfig.DefaultLatitude;
        var pinLon = hasUserFix ? _vm.CurrentLon : AppConfig.DefaultLongitude;
        var (ux, uy) = SphericalMercator.FromLonLat(pinLon, pinLat);
        var userFeature = new PointFeature(new MPoint(ux, uy));
        userFeature.Styles.Add(new SymbolStyle
        {
            SymbolScale = 0.6,
            Fill = new MapsBrush(new MapsColor(34, 197, 94)),
            Outline = new Pen(MapsColor.White, 3),
            SymbolType = SymbolType.Ellipse
        });

        _userPinLayer.Features = new[] { userFeature };
        _userPinLayer.DataHasChanged();

        if (_isFirstLocation)
        {
            CenterMapForCurrentState();
            _isFirstLocation = false;
        }

        MapView.RefreshGraphics();
    }

    private void OnMapInfoTapped(object? sender, Mapsui.MapInfoEventArgs e)
    {
        var worldPos = e.WorldPosition;
        if (worldPos == null)
            return;

        POI? nearest = null;
        double minDist = 600;

        foreach (var poi in _vm.AllPOIs)
        {
            if (poi.ZoneType == "Area" || poi.ZoneType == "District")
                continue;

            var (px, py) = SphericalMercator.FromLonLat(poi.Longitude, poi.Latitude);
            var dist = Math.Sqrt(Math.Pow(worldPos.X - px, 2) + Math.Pow(worldPos.Y - py, 2));
            if (dist < minDist)
            {
                minDist = dist;
                nearest = poi;
            }
        }

        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (nearest == null)
                return;

            if (_isNearFocusMode)
            {
                // In InZone map, pin selection is locked to live geofence flow.
                if (ResolveOverlayExploreState() == MainViewModel.ExploreState.InZone)
                    return;

                SetNearRoutingCandidate(nearest, clearActiveRoute: true);
                UpdateNearFocusOverlayState();
                UpdateNearFocusRoute();
                UpdateZonePins();
                return;
            }

            _vm.VisitedPOIIds.Add(nearest.Id);
            _vm.SelectedPinPOI = nearest;
            UpdateZonePins();
            _ = SwitchPreviewToPoiIfNeededAsync(nearest);
        });
    }

    private async void OnBackRequested(object? sender, EventArgs e)
    {
        await ClosePageAsync();
    }

    private async void OnSavedRequested(object? sender, EventArgs e)
    {
        await ClosePageAsync();
        if (Shell.Current != null)
            await Shell.Current.GoToAsync("//SavedPage");
    }

    private async void OnProfileRequested(object? sender, EventArgs e)
    {
        await ClosePageAsync();
        if (Shell.Current != null)
            await Shell.Current.GoToAsync("//ProfilePage");
    }

    private async void OnNearFocusBackTapped(object? sender, EventArgs e)
        => await ClosePageAsync();

    private void OnNearFocusSettingsTapped(object? sender, EventArgs e)
        => OnSettingsRequested(sender, e);

    private async void OnNearFocusSavedTapped(object? sender, EventArgs e)
    {
        await ClosePageAsync();
        if (Shell.Current != null)
            await Shell.Current.GoToAsync("//SavedPage");
    }

    private async void OnNearFocusProfileTapped(object? sender, EventArgs e)
    {
        await ClosePageAsync();
        if (Shell.Current != null)
            await Shell.Current.GoToAsync("//ProfilePage");
    }

    private async void OnNearFocusSeeMoreTapped(object? sender, EventArgs e)
    {
        try
        {
            var poi = ResolveNearFocusPoi();
            if (poi == null)
                return;

            await OpenPoiDetailAsync(poi);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ExploreMapPage] OnNearFocusSeeMoreTapped: {ex}");
            await DisplayAlertAsync("Lỗi", "Không thể mở trang chi tiết quán lúc này.", "OK");
        }
    }

    private async void OnNearFocusPlayPauseTapped(object? sender, EventArgs e)
    {
        var poi = ResolveNearFocusPoi();
        if (poi == null)
            return;

        await PlayPreviewAudioAsync(poi, allowToggleCurrent: true, showErrorAlert: true);
        UpdateNearFocusOverlayState();
    }

    private async void OnNearFocusLikeTapped(object? sender, EventArgs e)
    {
        var poi = ResolveNearFocusPoi();
        if (poi == null)
            return;

        await _vm.ToggleSavePOICommand.ExecuteAsync(poi);
        _vm.SelectedPinPOI = _vm.AllPOIs.FirstOrDefault(p => p.Id == poi.Id) ?? poi;
        UpdateNearFocusOverlayState();
        UpdateZonePins();
    }

    private void OnNearNearbyCard1Tapped(object? sender, EventArgs e)
        => SelectNearNearbyPoi(0);

    private void OnNearNearbyCard2Tapped(object? sender, EventArgs e)
        => SelectNearNearbyPoi(1);

    private void SelectNearNearbyPoi(int index)
    {
        if (ResolveOverlayExploreState() != MainViewModel.ExploreState.Near)
            return;

        if (index < 0 || index >= _nearFocusNearbyPois.Count)
            return;

        var poi = _nearFocusNearbyPois[index];
        SetNearRoutingCandidate(poi, clearActiveRoute: true);
        CenterMapForNearFocus();
        UpdateNearFocusRoute();
        UpdateNearFocusOverlayState();
        UpdateZonePins();
    }

    private void OnNearFocusStartRouteTapped(object? sender, EventArgs e)
    {
        if (_vm.CurrentExploreState != MainViewModel.ExploreState.Near)
            return;

        var poi = ResolveNearFocusPoi();
        if (poi == null)
            return;

        SetNearRoutingCandidate(poi, clearActiveRoute: false);
        _isNearRouteActive = true;
        _nearRouteTargetPoiId = poi.Id;
        var currentDistance = ComputeDistanceFromCurrentToPoi(poi);
        _nearRouteInitialDistanceMeters = currentDistance > 0 ? currentDistance : _nearRouteInitialDistanceMeters;
        _nearRouteCoordinates = null;
        UpdateNearFocusRoute();
        UpdateNearFocusOverlayState();
    }

    private void OnNearFocusSkipRouteTapped(object? sender, EventArgs e)
    {
        _isNearRouteActive = false;
        _nearRouteTargetPoiId = null;
        _nearRouteInitialDistanceMeters = 0;
        _nearRouteDistanceMeters = 0;
        _nearRouteDurationSeconds = 0;
        _nearRouteCoordinates = null;
        UpdateNearFocusRoute();
        UpdateNearFocusOverlayState();
    }

    private void OnNearFocusSheetToggleTapped(object? sender, EventArgs e)
    {
        var overlayState = ResolveOverlayExploreState();
        var isFocusState = overlayState == MainViewModel.ExploreState.Near ||
                           overlayState == MainViewModel.ExploreState.InZone;
        if (!_isNearFocusMode || !isFocusState)
            return;

        _isNearSheetCollapsed = !_isNearSheetCollapsed;
        UpdateNearFocusSheetState();
        UpdateNearRouteActionVisibility();
    }

    private void UpdateNearFocusSheetState()
    {
        var overlayState = ResolveOverlayExploreState();
        var isFocusState = _isNearFocusMode &&
                           (overlayState == MainViewModel.ExploreState.Near ||
                            overlayState == MainViewModel.ExploreState.InZone);

        if (overlayState == MainViewModel.ExploreState.InZone &&
            _lastNearFocusState != MainViewModel.ExploreState.InZone)
        {
            _isNearSheetCollapsed = false;
        }

        NearFocusExpandedContent.IsVisible = isFocusState && !_isNearSheetCollapsed;
        NearFocusCollapsedContent.IsVisible = !isFocusState || _isNearSheetCollapsed;
        NearFocusSheetToggleHandle.IsVisible = isFocusState;
        NearFocusSheetToggleLabel.Text = _isNearSheetCollapsed ? "Mở rộng" : "Thu gọn";
        _lastNearFocusState = overlayState;
    }

    private void UpdateNearRouteActionVisibility()
    {
        var overlayState = ResolveOverlayExploreState();
        var isNearState = _isNearFocusMode &&
                          overlayState == MainViewModel.ExploreState.Near;
        var isInZoneState = _isNearFocusMode &&
                            overlayState == MainViewModel.ExploreState.InZone;

        NearFocusStartRouteButton.IsVisible = isNearState && !_isNearSheetCollapsed;
        NearFocusSkipRouteButton.IsVisible = isNearState && !_isNearSheetCollapsed;
        NearFocusRouteActionsSection.IsVisible = isNearState && !_isNearSheetCollapsed;
        NearFocusCollapsedNearActions.IsVisible = isNearState && _isNearSheetCollapsed;
        NearFocusCollapsedStartRouteButton.IsVisible = isNearState && _isNearSheetCollapsed;
        NearFocusCollapsedSkipRouteButton.IsVisible = isNearState && _isNearSheetCollapsed;

        NearFocusCollapsedInZoneActions.IsVisible = isInZoneState;
        NearFocusCollapsedProgressSection.IsVisible = isInZoneState;

        NearFocusRouteStatusLabel.IsVisible = isNearState || isInZoneState;
        NearFocusRouteMetaLabel.IsVisible = false;

        if (!isNearState && _isNearRouteActive)
        {
            _isNearRouteActive = false;
            _nearRouteTargetPoiId = null;
            _nearRouteInitialDistanceMeters = 0;
            _nearRouteDistanceMeters = 0;
            _nearRouteDurationSeconds = 0;
            _nearRouteCoordinates = null;
            UpdateNearFocusRoute();
        }
    }

    private MainViewModel.ExploreState ResolveOverlayExploreState()
    {
        if (_vm.CurrentExploreState == MainViewModel.ExploreState.InZone ||
            _vm.IsInsideAnyZone ||
            _vm.PrimaryZone != null)
        {
            return MainViewModel.ExploreState.InZone;
        }

        if (_vm.CurrentExploreState == MainViewModel.ExploreState.Near)
            return MainViewModel.ExploreState.Near;

        return _vm.CurrentExploreState;
    }

    private async Task ClosePageAsync()
    {
        if (_isClosing)
            return;

        _isClosing = true;

        try
        {
            await StopPreviewAudioAsync();
            _vm.SelectedPinPOI = null;
            TabMapComponent.HideSuggestions();

            // InZone back action should exit the flow to Welcome page.
            if (_isNearFocusMode && ResolveOverlayExploreState() == MainViewModel.ExploreState.InZone)
            {
                if (Shell.Current != null)
                {
                    await Shell.Current.GoToAsync("//WelcomePage");
                    return;
                }
            }

            await Navigation.PopAsync();
        }
        finally
        {
            _isClosing = false;
        }
    }

    private async void OnSettingsRequested(object? sender, EventArgs e)
    {
        try
        {
            if (Shell.Current is AppShell shell)
            {
                await Shell.Current.Navigation.PushModalAsync(shell.GetCachedSettingsPage());
                return;
            }

            var settingsPage = ResolveRequiredService<SettingsPage>();
            await Navigation.PushModalAsync(settingsPage);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ExploreMapPage] OnSettingsRequested: {ex}");
        }
    }

    private void OnZoomInRequested(object? sender, EventArgs e)
        => MapView?.Map?.Navigator.ZoomIn(300);

    private void OnZoomOutRequested(object? sender, EventArgs e)
        => MapView?.Map?.Navigator.ZoomOut(300);

    private void OnCenterMapRequested(object? sender, EventArgs e)
    {
        EnsureMapInitialized();
        CenterMapForCurrentState();
    }

    private void OnNearFocusCenterUserTapped(object? sender, EventArgs e)
    {
        EnsureMapInitialized();
        CenterMapOnUserLocation();
    }

    private async Task StopPreviewAudioAsync()
    {
        try
        {
            await _tts.StopAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ExploreMapPage] StopPreviewAudioAsync: {ex.Message}");
        }
        finally
        {
            _previewPoiId = null;
            _isPreviewPaused = false;
            UpdatePreviewAudioUiState();
        }
    }

    private async void OnViewDetailRequested(object? sender, POI poi)
    {
        if (poi == null)
            return;

        await OpenPoiDetailAsync(poi);
    }

    private async Task OpenPoiDetailAsync(POI poi)
    {
        if (poi == null || _isOpeningPoiDetail)
            return;

        _isOpeningPoiDetail = true;
        try
        {
            // Keep current narration alive when opening POI detail from map.
            _vm.SelectedPinPOI = poi;
            _vm.PrimaryZone = poi;
            _vm.PrimaryZoneName = poi.Name_Vi ?? poi.Name_En ?? "—";
            _vm.PrimaryZoneDesc = poi.Description_Vi ?? poi.Description_En ?? "";
            _vm.PrimaryZoneAddress = poi.Address ?? "Đang cập nhật";
            _vm.PrimaryZoneRating = (poi.Rating ?? 4.5).ToString("F1");
            _syncPrimaryZoneOnNextAppearing = true;

            if (Shell.Current?.Navigation != null)
                await Shell.Current.Navigation.PushModalAsync(new POIDetailPage(poi, keepCurrentAudio: true));
            else
                await Navigation.PushModalAsync(new POIDetailPage(poi, keepCurrentAudio: true));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ExploreMapPage] OpenPoiDetailAsync: {ex}");
            await DisplayAlertAsync("Lỗi", "Không thể mở trang chi tiết quán lúc này.", "OK");
        }
        finally
        {
            _isOpeningPoiDetail = false;
        }
    }

    private async void OnPreviewAudioRequested(object? sender, POI poi)
    {
        if (poi == null)
            return;

        try
        {
            await PlayPreviewAudioAsync(poi, allowToggleCurrent: true, showErrorAlert: true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ExploreMapPage] OnPreviewAudioRequested: {ex}");
            _previewPoiId = null;
            _isPreviewPaused = false;
            UpdatePreviewAudioUiState();
            await DisplayAlertAsync("Lỗi", $"Không thể phát nghe thử: {ex.Message}", "OK");
        }
    }

    private async void OnStartFromHereRequested(object? sender, POI poi)
    {
        if (poi == null)
            return;

        try
        {
            var currentState = _vm.CurrentExploreState;
            var poiName = poi.Name_Vi ?? poi.Name_En ?? "địa điểm này";

            if (currentState == MainViewModel.ExploreState.Far)
            {
                await DisplayAlertAsync(
                    "Bạn đang ở xa",
                    $"Hiện bạn vẫn đang ở xa khu ẩm thực, nên chưa thể bắt đầu trải nghiệm thật tại {poiName}. Bạn vẫn có thể xem chi tiết hoặc nghe thử trước.",
                    "Đã hiểu");
                return;
            }

            var shouldStart = await DisplayAlertAsync(
                "Bắt đầu trải nghiệm thật?",
                $"Bạn muốn bắt đầu hành trình thực tế từ {poiName} chứ?",
                "Bắt đầu",
                "Ở lại bản đồ");

            if (!shouldStart)
                return;

            await StopPreviewAudioAsync();

            _vm.VisitedPOIIds.Add(poi.Id);
            _vm.SelectedPinPOI = poi;
            _vm.PrimaryZone = poi;
            _vm.PrimaryZoneName = poi.Name_Vi ?? poi.Name_En ?? "—";
            _vm.PrimaryZoneType = poi.ZoneType ?? poi.Category ?? string.Empty;
            _vm.PrimaryZoneDesc = poi.Description_Vi ?? poi.Description_En ?? string.Empty;
            _vm.PrimaryZoneAddress = poi.Address ?? "Đang cập nhật";
            _vm.PrimaryZoneRating = (poi.Rating ?? 4.5).ToString("F1");
            await _vm.StartTourFromPrimaryCommand.ExecuteAsync(null);
            UpdateZonePins();

            if (!_isClosing)
                await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ExploreMapPage] OnStartFromHereRequested: {ex}");
            await DisplayAlertAsync("Lỗi", $"Không thể bắt đầu từ quán này: {ex.Message}", "OK");
        }
    }

    private void OnPrevPoiRequested(object? sender, POI poi)
    {
        var spots = GetNavigablePois();
        if (spots.Count == 0)
            return;

        var idx = spots.FindIndex(p => p.Id == poi.Id);
        var prevIdx = (idx - 1 + spots.Count) % spots.Count;
        var prevPoi = spots[prevIdx];
        _vm.SelectedPinPOI = prevPoi;
        CenterMapOnPoi(prevPoi);
        UpdateZonePins();
        _ = SwitchPreviewToPoiIfNeededAsync(prevPoi);
    }

    private void OnNextPoiRequested(object? sender, POI poi)
    {
        var spots = GetNavigablePois();
        if (spots.Count == 0)
            return;

        var idx = spots.FindIndex(p => p.Id == poi.Id);
        var nextIdx = (idx + 1) % spots.Count;
        var nextPoi = spots[nextIdx];
        _vm.SelectedPinPOI = nextPoi;
        CenterMapOnPoi(nextPoi);
        UpdateZonePins();
        _ = SwitchPreviewToPoiIfNeededAsync(nextPoi);
    }

    private async void OnLikeRequested(object? sender, POI poi)
    {
        if (poi == null)
            return;

        try
        {
            await _vm.ToggleSavePOICommand.ExecuteAsync(poi);
            _vm.SelectedPinPOI = _vm.AllPOIs.FirstOrDefault(p => p.Id == poi.Id) ?? poi;
            UpdateZonePins();
            TabMapComponent.RefreshLikeIcon();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ExploreMapPage] OnLikeRequested: {ex}");
            await DisplayAlertAsync("Lỗi", "Không thể cập nhật trạng thái yêu thích lúc này.", "OK");
        }
    }

    private void OnCategorySelected(object? sender, string category)
    {
        _vm.SelectCategoryCommand.Execute(category);
        TabMapComponent.HighlightSelectedChip(category);

        if (_vm.SelectedPinPOI != null && !_vm.FilteredPOIs.Any(p => p.Id == _vm.SelectedPinPOI.Id))
            _vm.SelectedPinPOI = null;

        UpdateZonePins();
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        _vm.SearchQuery = e.NewTextValue ?? string.Empty;
        _ = UpdateSuggestionsDropdownAsync();

        if (_vm.SelectedPinPOI != null && !_vm.FilteredPOIs.Any(p => p.Id == _vm.SelectedPinPOI.Id))
            _vm.SelectedPinPOI = null;

        UpdateZonePins();
    }

    private void OnClearSearchRequested(object? sender, EventArgs e)
    {
        _vm.SearchQuery = string.Empty;
        TabMapComponent.HideSuggestions();
        UpdateZonePins();
    }

    private void OnSuggestionSelected(object? sender, POI poi)
    {
        _vm.SearchQuery = poi.Name_Vi ?? poi.Name_En ?? string.Empty;
        TabMapComponent.HideSuggestions();
        CenterMapOnPoi(poi);
        _vm.VisitedPOIIds.Add(poi.Id);
        _vm.SelectedPinPOI = poi;
        UpdateZonePins();
        _ = SwitchPreviewToPoiIfNeededAsync(poi);
    }

    private async Task UpdateSuggestionsDropdownAsync()
    {
        await Task.Delay(140);
        MainThread.BeginInvokeOnMainThread(() => TabMapComponent.ShowSuggestions(_vm.SearchSuggestions));
    }

    private void CenterMapOnPoi(POI poi)
    {
        if (MapView?.Map == null)
            return;

        var (px, py) = SphericalMercator.FromLonLat(poi.Longitude, poi.Latitude);
        MapView.Map.Navigator.CenterOn(new MPoint(px, py));
        ZoomToDefaultLevel();
    }

    private void CenterMapOnUserLocation()
    {
        if (MapView?.Map == null)
            return;

        var hasUserFix = _vm.CurrentLat != 0 || _vm.CurrentLon != 0;
        var lat = hasUserFix ? _vm.CurrentLat : AppConfig.DefaultLatitude;
        var lon = hasUserFix ? _vm.CurrentLon : AppConfig.DefaultLongitude;
        var (ux, uy) = SphericalMercator.FromLonLat(lon, lat);
        MapView.Map.Navigator.CenterOn(new MPoint(ux, uy));
        MapView.Map.Navigator.ZoomToLevel(Math.Max(17, NearFocusZoomLevel));
    }

    private void CenterMapForCurrentState()
    {
        if (MapView?.Map == null)
            return;

        if (_isNearFocusMode)
        {
            CenterMapForNearFocus();
            return;
        }

        var shouldCenterOnFoodZone =
            _vm.CurrentExploreState == MainViewModel.ExploreState.Far ||
            _vm.CurrentLat == 0 ||
            _vm.CurrentLon == 0;

        if (shouldCenterOnFoodZone)
        {
            var (zoneX, zoneY) = SphericalMercator.FromLonLat(AppConfig.DefaultLongitude, AppConfig.DefaultLatitude);
            MapView.Map.Navigator.CenterOn(new MPoint(zoneX, zoneY));
            ZoomToDefaultLevel();
            return;
        }

        var (ux, uy) = SphericalMercator.FromLonLat(_vm.CurrentLon, _vm.CurrentLat);
        MapView.Map.Navigator.CenterOn(new MPoint(ux, uy));
        ZoomToDefaultLevel();
    }

    private void CenterMapForNearFocus()
    {
        if (MapView?.Map == null)
            return;

        var target = ResolveNearFocusPoi();
        if (target == null)
        {
            var (zoneX, zoneY) = SphericalMercator.FromLonLat(AppConfig.DefaultLongitude, AppConfig.DefaultLatitude);
            MapView.Map.Navigator.CenterOn(new MPoint(zoneX, zoneY));
            MapView.Map.Navigator.ZoomToLevel(NearFocusZoomLevel);
            return;
        }

        var hasCurrentLocation = _vm.CurrentLat != 0 && _vm.CurrentLon != 0;
        var startLon = hasCurrentLocation ? _vm.CurrentLon : AppConfig.DefaultLongitude;
        var startLat = hasCurrentLocation ? _vm.CurrentLat : AppConfig.DefaultLatitude;

        var (startPx, startPy) = SphericalMercator.FromLonLat(startLon, startLat);
        var (endPx, endPy) = SphericalMercator.FromLonLat(target.Longitude, target.Latitude);
        var distanceMeters = HaversineDistance(startLat, startLon, target.Latitude, target.Longitude);

        if (distanceMeters <= NearFocusMidpointMeters)
        {
            var midPx = (startPx + endPx) / 2.0;
            var midPy = (startPy + endPy) / 2.0;
            MapView.Map.Navigator.CenterOn(new MPoint(midPx, midPy));
            MapView.Map.Navigator.ZoomToLevel(Math.Max(16, NearFocusZoomLevel - 1));
            return;
        }

        MapView.Map.Navigator.CenterOn(new MPoint(endPx, endPy));
        MapView.Map.Navigator.ZoomToLevel(NearFocusZoomLevel);
    }

    private void UpdateNearFocusRoute()
        => _ = UpdateNearFocusRouteAsync();

    private async Task UpdateNearFocusRouteAsync()
    {
        if (_routeLayer == null || MapView?.Map == null)
            return;

        var target = ResolveNearFocusPoi();
        if (target == null || !_isNearRouteActive || _nearRouteTargetPoiId != target.Id)
        {
            _routeLayer.Features = Array.Empty<IFeature>();
            _routeLayer.DataHasChanged();
            MapView.RefreshGraphics();
            return;
        }

        var hasCurrentLocation = _vm.CurrentLat != 0 && _vm.CurrentLon != 0;
        var startLon = hasCurrentLocation ? _vm.CurrentLon : AppConfig.DefaultLongitude;
        var startLat = hasCurrentLocation ? _vm.CurrentLat : AppConfig.DefaultLatitude;

        var coords = _nearRouteCoordinates;
        if (coords == null || coords.Length < 2)
        {
            var routeResult = await FetchNearRouteAsync(startLon, startLat, target.Longitude, target.Latitude);
            coords = routeResult?.Coordinates;
            if (coords != null && coords.Length >= 2)
            {
                _nearRouteCoordinates = coords;
                _nearRouteDistanceMeters = routeResult?.DistanceMeters ?? ComputeDistanceFromCurrentToPoi(target);
                _nearRouteDurationSeconds = routeResult?.DurationSeconds ?? EstimateWalkingSeconds(_nearRouteDistanceMeters);
            }
        }

        if (coords == null || coords.Length < 2)
        {
            var (startPx, startPy) = SphericalMercator.FromLonLat(startLon, startLat);
            var (endPx, endPy) = SphericalMercator.FromLonLat(target.Longitude, target.Latitude);
            coords = new[]
            {
                new Coordinate(startPx, startPy),
                new Coordinate(endPx, endPy)
            };

            _nearRouteDistanceMeters = ComputeDistanceFromCurrentToPoi(target);
            _nearRouteDurationSeconds = EstimateWalkingSeconds(_nearRouteDistanceMeters);
        }
        else
        {
            // Keep route path stable (cached) and continuously refresh ETA by current distance.
            _nearRouteDistanceMeters = ComputeDistanceFromCurrentToPoi(target);
            _nearRouteDurationSeconds = EstimateWalkingSeconds(_nearRouteDistanceMeters);
        }

        if (_nearRouteInitialDistanceMeters <= 0)
            _nearRouteInitialDistanceMeters = Math.Max(1, _nearRouteDistanceMeters);

        var feature = new GeometryFeature(new LineString(coords));
        feature.Styles.Add(new VectorStyle
        {
            Line = new Pen(new MapsColor(75, 226, 119), 4)
            {
                PenStyle = PenStyle.Dash
            }
        });

        _routeLayer.Features = new IFeature[] { feature };
        _routeLayer.DataHasChanged();
        MapView.RefreshGraphics();
    }

    private sealed class NearRouteResult
    {
        public required Coordinate[] Coordinates { get; init; }
        public double DistanceMeters { get; init; }
        public double DurationSeconds { get; init; }
    }

    private async Task<NearRouteResult?> FetchNearRouteAsync(double srcLon, double srcLat, double dstLon, double dstLat)
    {
        try
        {
            var isOnline = Connectivity.Current.NetworkAccess == NetworkAccess.Internet ||
                           Connectivity.Current.NetworkAccess == NetworkAccess.ConstrainedInternet;
            if (!isOnline)
                return null;

            var url = string.Create(
                CultureInfo.InvariantCulture,
                $"https://router.project-osrm.org/route/v1/walking/{srcLon},{srcLat};{dstLon},{dstLat}?geometries=geojson&overview=full");

            var json = await _routeHttpClient.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);
            var routes = doc.RootElement.GetProperty("routes");
            if (routes.GetArrayLength() == 0)
                return null;
            var route = routes[0];
            var geometry = route.GetProperty("geometry").GetProperty("coordinates");
            var coords = geometry.EnumerateArray()
                .Select(c =>
                {
                    if (c.ValueKind != JsonValueKind.Array || c.GetArrayLength() < 2)
                        return new Coordinate(0, 0);
                    var (x, y) = SphericalMercator.FromLonLat(c[0].GetDouble(), c[1].GetDouble());
                    return new Coordinate(x, y);
                })
                .Where(coord => coord.X != 0 || coord.Y != 0)
                .ToArray();

            if (coords.Length < 2)
                return null;

            return new NearRouteResult
            {
                Coordinates = coords,
                DistanceMeters = route.TryGetProperty("distance", out var distanceEl) ? distanceEl.GetDouble() : 0,
                DurationSeconds = route.TryGetProperty("duration", out var durationEl) ? durationEl.GetDouble() : 0
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ExploreMapPage] OSRM fallback route: {ex.Message}");
            return null;
        }
    }

    private void UpdateNearFocusOverlayState()
    {
        if (!_isNearFocusMode)
            return;

        UpdateNearFocusSheetState();
        UpdateNearRouteActionVisibility();

        var poi = ResolveNearFocusPoi();
        if (poi == null)
            return;

        var overlayState = ResolveOverlayExploreState();
        var isNearState = overlayState == MainViewModel.ExploreState.Near;
        var isInZoneState = overlayState == MainViewModel.ExploreState.InZone;

        NearFocusLikeIcon.Text = IconHeart;
        NearFocusLikeIcon.TextColor = poi.IsLikedByUser
            ? MauiColor.FromArgb("#EF4444")
            : MauiColor.FromArgb("#D8E6DF");

        var isCurrentPreview = _previewPoiId == poi.Id && (_tts.IsPlaying() || _isPreviewPaused);
        NearFocusPlayPauseIcon.Text = isCurrentPreview && _tts.IsPlaying() ? IconPause : IconPlay;
        NearFocusPlayPauseIcon.TextColor = MauiColor.FromArgb("#063014");
        NearFocusCollapsedPlayPauseIcon.Text = isCurrentPreview && _tts.IsPlaying() ? IconPause : IconPlay;
        NearFocusCollapsedPlayPauseIcon.TextColor = MauiColor.FromArgb("#063014");

        var currentDistance = ComputeDistanceFromCurrentToPoi(poi);
        var durationSeconds = _nearRouteDurationSeconds > 0
            ? _nearRouteDurationSeconds
            : EstimateWalkingSeconds(currentDistance);

        var routeMetaText = $"{FormatDistanceForRoute(currentDistance)}";

        NearFocusCollapsedThumb.Source = poi.DisplayImageUrl;
        NearFocusCollapsedNameLabel.Text = poi.Name_Vi ?? poi.Name_En ?? "Địa điểm";

        if (isNearState)
        {
            NearFocusDescriptionLabel.IsVisible = false;
            NearFocusTagsRow.IsVisible = false;
            NearFocusLikeButton.IsVisible = false;
            NearFocusPlayPauseButton.IsVisible = false;
            NearFocusProgressSection.IsVisible = false;
            NearNearbySection.IsVisible = false;
            NearFocusRouteStatusLabel.Text = routeMetaText;
            NearFocusRouteMetaLabel.IsVisible = false;
            NearFocusCollapsedMetaLabel.IsVisible = true;
            NearFocusCollapsedMetaLabel.Text = routeMetaText;
        }
        else if (isInZoneState)
        {
            NearFocusDescriptionLabel.IsVisible = true;
            NearFocusTagsRow.IsVisible = true;
            NearFocusLikeButton.IsVisible = true;
            NearFocusPlayPauseButton.IsVisible = true;
            NearFocusProgressSection.IsVisible = true;
            NearNearbySection.IsVisible = false;
            NearFocusRouteStatusLabel.Text = "ĐANG THUYẾT MINH";
            NearFocusRouteMetaLabel.Text = "XEM THÊM ->";
            NearFocusRouteMetaLabel.IsVisible = true;
            NearFocusCollapsedMetaLabel.IsVisible = false;

            var tags = BuildNearFocusTags(poi);
            NearFocusTag1Label.Text = tags.ElementAtOrDefault(0) ?? "#MONNGON";
            NearFocusTag2Label.Text = tags.ElementAtOrDefault(1) ?? "#KHAMPHA";
        }
        else
        {
            NearFocusDescriptionLabel.IsVisible = false;
            NearFocusTagsRow.IsVisible = false;
            NearFocusLikeButton.IsVisible = false;
            NearFocusPlayPauseButton.IsVisible = false;
            NearFocusProgressSection.IsVisible = false;
            NearNearbySection.IsVisible = false;
            NearFocusRouteStatusLabel.Text = routeMetaText;
            NearFocusRouteMetaLabel.IsVisible = false;
            NearFocusCollapsedMetaLabel.IsVisible = true;
            NearFocusCollapsedMetaLabel.Text = routeMetaText;
        }

        NearFocusCollapsedNameLabel.Text = poi.Name_Vi ?? poi.Name_En ?? "Địa điểm";
        NearFocusCollapsedHeartIcon.Text = IconHeart;
        NearFocusCollapsedHeartIcon.TextColor = poi.IsLikedByUser
            ? MauiColor.FromArgb("#EF4444")
            : MauiColor.FromArgb("#D8E6DF");

        if (isInZoneState)
        {
            var audioDuration = Math.Max(0, _tts.GetDuration());
            var audioCurrent = Math.Max(0, _tts.GetCurrentPosition());
            if (audioDuration > 0 && audioCurrent > audioDuration)
                audioCurrent = audioDuration;

            NearFocusProgressBar.Progress = audioDuration > 0
                ? Math.Clamp(audioCurrent / audioDuration, 0, 1)
                : 0;
            NearFocusElapsedLabel.Text = FormatAudioTime(audioCurrent);
            NearFocusDurationLabel.Text = FormatAudioTime(audioDuration > 0 ? audioDuration : 0);
        }
        else if (_isNearRouteActive && _nearRouteTargetPoiId == poi.Id && _nearRouteInitialDistanceMeters > 0)
        {
            var progress = 1 - (currentDistance / _nearRouteInitialDistanceMeters);
            NearFocusProgressBar.Progress = Math.Clamp(progress, 0, 1);
            NearFocusElapsedLabel.Text = FormatDistance(currentDistance);
            NearFocusDurationLabel.Text = $"~{Math.Max(1, (int)Math.Ceiling(durationSeconds / 60))} phút";
        }
        else
        {
            NearFocusProgressBar.Progress = 0;
            NearFocusElapsedLabel.Text = FormatDistance(currentDistance);
            NearFocusDurationLabel.Text = $"~{Math.Max(1, (int)Math.Ceiling(durationSeconds / 60))} phút";
        }

        // Compact in-zone card shows audio progress/time.
        var compactAudioDuration = Math.Max(0, _tts.GetDuration());
        var compactAudioCurrent = Math.Max(0, _tts.GetCurrentPosition());
        if (compactAudioDuration > 0 && compactAudioCurrent > compactAudioDuration)
            compactAudioCurrent = compactAudioDuration;

        NearFocusCollapsedProgressBar.Progress = compactAudioDuration > 0
            ? Math.Clamp(compactAudioCurrent / compactAudioDuration, 0, 1)
            : 0;
        NearFocusCollapsedElapsedLabel.Text = FormatAudioTime(compactAudioCurrent);
        NearFocusCollapsedDurationLabel.Text = FormatAudioTime(compactAudioDuration > 0 ? compactAudioDuration : 0);

        if (isInZoneState && !_tts.IsPlaying() && !_isPreviewPaused)
        {
            // Keep compact card stable even if audio not started yet.
            NearFocusCollapsedProgressBar.Progress = 0;
            NearFocusCollapsedElapsedLabel.Text = "00:00";
        }
    }

    private void UpdateNearFocusNearbySection(POI currentPoi)
    {
        _nearFocusNearbyPois.Clear();
        _nearFocusNearbyPois.AddRange(BuildNearFocusNearbyPois(currentPoi, 2));

        NearNearbySection.IsVisible = _nearFocusNearbyPois.Count > 0;

        ApplyNearNearbyCard(
            NearNearbyCard1,
            NearNearbyImage1,
            NearNearbyName1,
            NearNearbyMeta1,
            _nearFocusNearbyPois.ElementAtOrDefault(0),
            currentPoi);

        ApplyNearNearbyCard(
            NearNearbyCard2,
            NearNearbyImage2,
            NearNearbyName2,
            NearNearbyMeta2,
            _nearFocusNearbyPois.ElementAtOrDefault(1),
            currentPoi);
    }

    private List<POI> BuildNearFocusNearbyPois(POI currentPoi, int maxCount)
    {
        return _vm.AllPOIs
            .Where(p => p.ZoneType == "Spot" && p.Id != currentPoi.Id)
            .OrderBy(p => HaversineDistance(currentPoi.Latitude, currentPoi.Longitude, p.Latitude, p.Longitude))
            .Take(maxCount)
            .ToList();
    }

    private static void ApplyNearNearbyCard(
        Border card,
        MauiImage image,
        Label nameLabel,
        Label metaLabel,
        POI? poi,
        POI currentPoi)
    {
        if (poi == null)
        {
            card.IsVisible = false;
            return;
        }

        card.IsVisible = true;
        image.Source = poi.DisplayImageUrl;
        nameLabel.Text = poi.Name_Vi ?? poi.Name_En ?? "Địa điểm gần";

        var distanceMeters = HaversineDistance(currentPoi.Latitude, currentPoi.Longitude, poi.Latitude, poi.Longitude);
        var distanceText = distanceMeters < 1000 ? $"{distanceMeters:F0}m" : $"{distanceMeters / 1000:F1}km";
        var rating = poi.Rating ?? 4.5;
        metaLabel.Text = $"{distanceText} • {rating:F1}★";
    }

    private static double HaversineDistance(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371000;
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLon = (lon2 - lon1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180)
              * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static Coordinate[] CreateZoneRingCoordinates(double centerLat, double centerLon, double radiusMeters, int segments = 48)
    {
        if (radiusMeters <= 0)
            return Array.Empty<Coordinate>();

        const double EarthRadiusMeters = 6378137.0;
        var centerLatRad = centerLat * Math.PI / 180.0;
        var centerLonRad = centerLon * Math.PI / 180.0;
        var angularDistance = radiusMeters / EarthRadiusMeters;
        var coords = new List<Coordinate>(segments + 1);

        for (var i = 0; i <= segments; i++)
        {
            var bearing = 2.0 * Math.PI * i / segments;
            var sinLat = Math.Sin(centerLatRad) * Math.Cos(angularDistance) +
                         Math.Cos(centerLatRad) * Math.Sin(angularDistance) * Math.Cos(bearing);
            var latRad = Math.Asin(sinLat);
            var lonRad = centerLonRad + Math.Atan2(
                Math.Sin(bearing) * Math.Sin(angularDistance) * Math.Cos(centerLatRad),
                Math.Cos(angularDistance) - Math.Sin(centerLatRad) * Math.Sin(latRad));

            var lat = latRad * 180.0 / Math.PI;
            var lon = lonRad * 180.0 / Math.PI;
            var (x, y) = SphericalMercator.FromLonLat(lon, lat);
            coords.Add(new Coordinate(x, y));
        }

        return coords.ToArray();
    }

    private bool IsUserInsidePoiZone(POI poi)
    {
        var distanceMeters = ComputeDistanceFromCurrentToPoi(poi);
        var zoneRadius = GetPoiAudioZoneRadiusMeters(poi);
        return distanceMeters <= zoneRadius;
    }

    private async Task EnsureInZoneNarrationAndFocusAsync()
    {
        if (!_isNearFocusMode || !IsVisible || _isOpeningPoiDetail)
            return;

        if (ResolveOverlayExploreState() != MainViewModel.ExploreState.InZone)
            return;

        var poi = ResolveCurrentSpotZonePoi();
        if (poi == null)
            return;

        if (!IsUserInsidePoiZone(poi))
            return;

        if (_vm.SelectedPinPOI?.Id != poi.Id)
            _vm.SelectedPinPOI = poi;

        if (_lastInZoneFocusedPoiId != poi.Id)
        {
            var (px, py) = SphericalMercator.FromLonLat(poi.Longitude, poi.Latitude);
            MapView?.Map?.Navigator.CenterOn(new MPoint(px, py));
            MapView?.Map?.Navigator.ZoomToLevel(InZoneFocusZoomLevel);
            _lastInZoneFocusedPoiId = poi.Id;
        }

        if (_lastAutoNarratedPoiId == poi.Id && (_tts.IsPlaying() || _isPreviewPaused))
            return;

        var played = await PlayPreviewAudioAsync(poi, allowToggleCurrent: false, showErrorAlert: false);
        if (played)
        {
            _lastAutoNarratedPoiId = poi.Id;
            _previewPoiId = poi.Id;
            _isPreviewPaused = false;
            UpdatePreviewAudioUiState();
        }
    }

    private POI? ResolveCurrentSpotZonePoi()
    {
        var spots = _vm.AllPOIs
            .Where(p => p.ZoneType == "Spot")
            .ToList();
        if (spots.Count == 0)
        {
            _activeSpotZonePoiId = null;
            return null;
        }

        var candidates = spots
            .Select(p => new
            {
                Poi = p,
                Distance = ComputeDistanceFromCurrentToPoi(p),
                Radius = GetPoiAudioZoneRadiusMeters(p)
            })
            .ToList();

        var activeCandidate = _activeSpotZonePoiId.HasValue
            ? candidates.FirstOrDefault(x => x.Poi.Id == _activeSpotZonePoiId.Value)
            : null;

        if (activeCandidate != null && activeCandidate.Distance <= activeCandidate.Radius + SpotZoneHysteresisMeters)
            return activeCandidate.Poi;

        var insideSpots = candidates
            .Where(x => x.Distance <= x.Radius)
            .OrderBy(x => x.Poi.Priority)
            .ThenBy(x => x.Distance)
            .ThenBy(x => x.Radius)
            .ThenBy(x => x.Poi.Id)
            .ToList();

        if (insideSpots.Count > 0)
        {
            var best = insideSpots.First();

            if (activeCandidate != null)
            {
                var samePriority = best.Poi.Priority == activeCandidate.Poi.Priority;
                var distanceDelta = best.Distance - activeCandidate.Distance;
                var withinSwitchCooldown = DateTime.UtcNow - _lastSpotZoneSwitchAtUtc < SpotZoneSwitchCooldown;

                if (samePriority && distanceDelta <= SpotZoneHysteresisMeters)
                    best = activeCandidate;
                else if (withinSwitchCooldown && activeCandidate.Distance <= activeCandidate.Radius + (SpotZoneHysteresisMeters * 2))
                    best = activeCandidate;
            }

            if (_activeSpotZonePoiId != best.Poi.Id)
            {
                _activeSpotZonePoiId = best.Poi.Id;
                _lastSpotZoneSwitchAtUtc = DateTime.UtcNow;
            }

            return best.Poi;
        }

        if (activeCandidate != null && activeCandidate.Distance <= activeCandidate.Radius + (SpotZoneHysteresisMeters * 2))
            return activeCandidate.Poi;

        _activeSpotZonePoiId = null;

        var selectedSpot = _vm.SelectedPinPOI?.ZoneType == "Spot"
            ? _vm.SelectedPinPOI
            : null;
        if (selectedSpot != null)
            return selectedSpot;

        var primarySpot = _vm.PrimaryZone?.ZoneType == "Spot"
            ? _vm.PrimaryZone
            : null;
        if (primarySpot != null)
            return primarySpot;

        return candidates
            .OrderBy(x => x.Distance)
            .Select(x => x.Poi)
            .FirstOrDefault();
    }

    private static double GetPoiAudioZoneRadiusMeters(POI poi)
    {
        var raw = poi.Radius > 0 ? poi.Radius : AppConfig.TrackingInsideMeters;
        // Keep trigger/ring to small per-POI zone, not the larger Explore InZone radius.
        return Math.Clamp(raw, 20, 55);
    }

    private double ComputeDistanceFromCurrentToPoi(POI poi)
    {
        var hasCurrentLocation = _vm.CurrentLat != 0 || _vm.CurrentLon != 0;
        var fromLat = hasCurrentLocation ? _vm.CurrentLat : AppConfig.DefaultLatitude;
        var fromLon = hasCurrentLocation ? _vm.CurrentLon : AppConfig.DefaultLongitude;
        return HaversineDistance(fromLat, fromLon, poi.Latitude, poi.Longitude);
    }

    private static double EstimateWalkingSeconds(double distanceMeters)
    {
        const double averageWalkingSpeedMetersPerSecond = 1.3;
        return distanceMeters <= 0 ? 60 : distanceMeters / averageWalkingSpeedMetersPerSecond;
    }

    private static string FormatDistance(double meters)
    {
        if (meters <= 0)
            return "—";

        return meters < 1000
            ? $"{meters:F0}m"
            : $"{meters / 1000:F1}km";
    }

    private static string FormatDistanceForRoute(double meters)
    {
        if (meters <= 0)
            return "—";

        return meters < 1000
            ? $"{meters:F0}M"
            : $"{meters / 1000:F1}KM";
    }

    private static IReadOnlyList<string> BuildNearFocusTags(POI poi)
    {
        var values = new List<string>();
        if (!string.IsNullOrWhiteSpace(poi.Category))
            values.Add(poi.Category!);
        if (!string.IsNullOrWhiteSpace(poi.Type))
            values.Add(poi.Type);
        if (!string.IsNullOrWhiteSpace(poi.SignatureDish))
            values.Add(poi.SignatureDish!);

        var hashtags = values
            .Select(ToHashTag)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .Take(2)
            .ToList();

        while (hashtags.Count < 2)
            hashtags.Add(hashtags.Count == 0 ? "#MONNGON" : "#KHAMPHA");

        return hashtags;
    }

    private static string ToHashTag(string raw)
    {
        var cleaned = new string(raw.Where(char.IsLetterOrDigit).ToArray());
        if (string.IsNullOrWhiteSpace(cleaned))
            return string.Empty;

        if (cleaned.Length > 12)
            cleaned = cleaned[..12];

        return $"#{cleaned.ToUpperInvariant()}";
    }

    private static string FormatAudioTime(double seconds)
    {
        var total = Math.Max(0, (int)Math.Round(seconds));
        var min = total / 60;
        var sec = total % 60;
        return $"{min:00}:{sec:00}";
    }

    private List<POI> GetNavigablePois()
    {
        var filteredSpots = _vm.FilteredPOIs.Where(p => p.ZoneType == "Spot").ToList();
        if (filteredSpots.Count > 0)
            return filteredSpots;

        return _vm.AllPOIs.Where(p => p.ZoneType == "Spot").ToList();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.CurrentLat) ||
            e.PropertyName == nameof(MainViewModel.CurrentLon))
        {
            UpdateUserPin();
            UpdateZonePins();
            if (_isNearFocusMode)
            {
                UpdateNearFocusRoute();
                UpdateNearFocusOverlayState();
                _ = EnsureInZoneNarrationAndFocusAsync();
            }
        }
        else if (e.PropertyName == nameof(MainViewModel.ActiveZoneCount) ||
                 e.PropertyName == nameof(MainViewModel.SelectedPinPOI))
        {
            UpdateZonePins();
            TabMapComponent.RefreshLikeIcon();
            if (_isNearFocusMode)
            {
                UpdateNearFocusRoute();
                UpdateNearFocusOverlayState();
            }
            UpdatePreviewAudioUiState();
        }
        else if (e.PropertyName == nameof(MainViewModel.AllPOIs) ||
                 e.PropertyName == nameof(MainViewModel.FilteredPOIs) ||
                 e.PropertyName == nameof(MainViewModel.SavedPOIs))
        {
            UpdateZonePins();
            TabMapComponent.RefreshLikeIcon();
            if (_isNearFocusMode)
                UpdateNearFocusOverlayState();
        }
        else if (e.PropertyName == nameof(MainViewModel.Categories))
        {
            TabMapComponent.InitializeChips(_vm.SelectedCategory);
        }
        else if (e.PropertyName == nameof(MainViewModel.CurrentExploreState))
        {
            if (_isNearFocusMode)
            {
                UpdateNearFocusRoute();
                UpdateNearFocusOverlayState();
                UpdateZonePins();
                _ = EnsureInZoneNarrationAndFocusAsync();
            }
        }
        else if (e.PropertyName == nameof(MainViewModel.PrimaryZone))
        {
            if (_isNearFocusMode && IsVisible && !_isOpeningPoiDetail)
            {
                SyncNearFocusPoiFromPrimaryZone();
                UpdateNearFocusRoute();
                UpdateNearFocusOverlayState();
                UpdateZonePins();
                _ = EnsureInZoneNarrationAndFocusAsync();
            }
            else
            {
                _syncPrimaryZoneOnNextAppearing = true;
            }
        }
    }

    private void OnPreviewPlaybackEnded()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _previewPoiId = null;
            _isPreviewPaused = false;
            UpdatePreviewAudioUiState();
        });
    }

    private void UpdatePreviewAudioUiState()
    {
        TabMapComponent.SetPreviewAudioState(_previewPoiId, _tts.IsPlaying(), _isPreviewPaused);
        UpdateNearFocusOverlayState();
    }

    private async Task SwitchPreviewToPoiIfNeededAsync(POI poi)
    {
        try
        {
            if (_previewPoiId == null || !_tts.IsPlaying())
                return;

            if (_previewPoiId == poi.Id)
            {
                UpdatePreviewAudioUiState();
                return;
            }

            await PlayPreviewAudioAsync(poi, allowToggleCurrent: false, showErrorAlert: false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ExploreMapPage] SwitchPreviewToPoiIfNeededAsync: {ex}");
            _previewPoiId = null;
            _isPreviewPaused = false;
            UpdatePreviewAudioUiState();
        }
    }

    private async Task<bool> PlayPreviewAudioAsync(POI poi, bool allowToggleCurrent, bool showErrorAlert)
    {
        if (_previewPoiId == poi.Id)
        {
            if (_tts.IsPlaying())
            {
                if (allowToggleCurrent)
                {
                    _tts.Pause();
                    _isPreviewPaused = true;
                }
                else
                {
                    _isPreviewPaused = false;
                }

                UpdatePreviewAudioUiState();
                return true;
            }

            if (_isPreviewPaused)
            {
                _tts.Resume();
                if (_tts.IsPlaying())
                {
                    _isPreviewPaused = false;
                    UpdatePreviewAudioUiState();
                    return true;
                }
            }
        }

        await StopPreviewAudioAsync();

        var ok = await _tts.SpeakAsync(
            ResolvePreviewNarrationText(poi),
            ResolvePreviewLanguageCode(),
            poiId: poi.Id);

        _previewPoiId = ok ? poi.Id : null;
        _isPreviewPaused = false;
        UpdatePreviewAudioUiState();

        if (!ok && showErrorAlert)
            await DisplayAlertAsync("Âm thanh", "Không thể phát nghe thử lúc này.", "OK");

        return ok;
    }

    private string ResolvePreviewLanguageCode()
        => _lang.CurrentLanguage switch
        {
            "en" => "en-US",
            "zh" => "zh-CN",
            _ => "vi-VN"
        };

    private string ResolvePreviewNarrationText(POI poi)
        => _lang.CurrentLanguage switch
        {
            "en" => poi.Description_En ?? poi.Name_En ?? poi.Name_Vi,
            "zh" => poi.Description_Zh ?? poi.Name_Zh ?? poi.Name_En ?? poi.Name_Vi,
            _ => poi.Description_Vi ?? poi.Name_Vi ?? poi.Name_En
        } ?? "Chào mừng đến với điểm tham quan.";

    private static T ResolveRequiredService<T>() where T : notnull
    {
        var providers = new IServiceProvider?[]
        {
            Application.Current?.Handler?.MauiContext?.Services,
            Application.Current?.Windows.FirstOrDefault()?.Page?.Handler?.MauiContext?.Services,
            MauiProgram.Services
        };

        foreach (var provider in providers)
        {
            if (provider == null)
                continue;

            try
            {
                return provider.GetRequiredService<T>();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        throw new InvalidOperationException($"Unable to resolve service {typeof(T).Name}.");
    }
}
