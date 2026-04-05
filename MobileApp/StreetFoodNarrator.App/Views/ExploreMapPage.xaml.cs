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
using System.Text.Json;
using StreetFoodNarrator.App.Core.Models;
using StreetFoodNarrator.App.Core.Services;
using StreetFoodNarrator.App.Helpers;
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
    private readonly IAudioCacheService? _audioCache;
    private readonly IOfflineRoutingService? _offlineRouting;
    private readonly bool _isNearFocusMode;
    private readonly List<POI> _nearFocusNearbyPois = new();
    private readonly HttpClient _routeHttpClient = new() { Timeout = TimeSpan.FromSeconds(6) };
    private MemoryLayer? _routeLayer;
    private MemoryLayer? _pinsLayer;
    private MemoryLayer? _userPinLayer;
    private MemoryLayer? _spotZoneLayer;
    private bool _isMapInitialized;
    private bool _isFirstLocation = true;
    private bool _eventsHooked;
    private bool _isClosing;
    private bool _isDetailPageOpen;
    private int? _pendingPoiIdWhileDetailOpen;
    private int? _activeSpotZonePoiId;
    private int? _candidateSpotZonePoiId;
    private DateTime _candidateSpotSeenUtc = DateTime.MinValue;
    private int? _lastApproachingToastPoiId;
    private DateTime _lastApproachingToastUtc = DateTime.MinValue;
    private int? _approachingToastPoiId;
    private DateTime _lastAutoPoiSwitchUtc = DateTime.MinValue;
    private CancellationTokenSource? _simulateThreeStopsCts;
    private IDispatcherTimer? _nearFocusAudioTimer;
    private readonly SemaphoreSlim _previewAudioLock = new(1, 1);
    private bool _isNearRouteActive;
    private int? _nearRouteTargetPoiId;
    private DateTime _lastRouteRedraw = DateTime.MinValue;
    private int _routeDrawRequestVersion;
    private DateTime _lastLocationEvalUtc = DateTime.MinValue;
    private bool _locationEvalQueued;
    private DateTime _lastZonePinsUpdateUtc = DateTime.MinValue;
    private bool _zonePinsUpdateQueued;
    private DateTime _lastNearOverlayUpdateUtc = DateTime.MinValue;
    private string? _lastNearOverlayImageUrl;
    private DateTime _lastNearEntryNoticeUtc = DateTime.MinValue;
    private bool _isNearEntryNoticeShowing;
    private double _lastUserPinLat = double.NaN;
    private double _lastUserPinLon = double.NaN;
    private DateTime _lastUserPinRefreshUtc = DateTime.MinValue;
    private bool _isInZoneEvalRunning;
    private bool _pendingInZoneEval;
    private bool _pendingInZoneEvalForceSwitch;

    private bool _isNearSheetCollapsed;
    private bool _isSimulationToolsExpanded;
    private bool _useInZoneSheetAfterRoute;
    private readonly HashSet<int> _tourSimulationVisitedPoiIds = new();
    private string _tourSimulationScopeKey = string.Empty;
    private readonly HashSet<int> _zoneProbeVisitedPoiIds = new();
    private string _zoneProbeScopeKey = string.Empty;
    private MainViewModel.ExploreState _lastNearFocusState = MainViewModel.ExploreState.Far;
    private MainViewModel.ExploreState _previousExploreState = MainViewModel.ExploreState.Far;
    private CancellationTokenSource? _inZoneToastCts;
    private CancellationTokenSource? _approachingToastCts;
    private bool _isHandlingFarTransition;
    private const int DefaultMapZoomLevel = (int)AppConfig.DefaultZoom + 1;
    private const int NearFocusZoomLevel = 18;
    private const int InZoneFocusZoomLevel = 19;
    private const double NearFocusMidpointMeters = 800;
    private const double NearRouteArrivalMeters = 35;
    private const int LocationEvalMinIntervalMs = 700;
    private const int ZonePinsMinIntervalMs = 320;
    private const int NearOverlayMinIntervalMs = 320;
    private const int UserPinRefreshMinIntervalMs = 300;
    private const double UserPinMinMoveMeters = 2.5;
    private const double SpotZoneTemporaryExpandFactor = 1.15;
    private const double SimulationPathStepMeters = 7.5;
    private const int SimulationStepDelayMinMs = 240;
    private const int SimulationStepDelayMaxMs = 520;
    private const int NearEntryNoticeCooldownMs = 10000;
    private const string IconHeart = "\U000F02D1";
    private const string IconPlay = "\U000F040A";
    private const string IconPause = "\U000F03E4";

    private sealed class SpotZoneEntry
    {
        public POI Poi { get; init; } = null!;
        public double Distance { get; init; }
        public double Radius { get; init; }
        public double Confidence { get; init; }
        public bool IsInside => Distance <= Radius;
        public bool IsReliable => Radius > 0 && (Distance / Radius) <= AppConfig.SpotZoneConfidenceThreshold;
    }

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
        _audioCache = ResolveOptionalService<IAudioCacheService>();
        _offlineRouting = ResolveOptionalService<IOfflineRoutingService>();
        BindingContext = _vm;
        TabMapComponent.BindingContext = _vm;
    }

    public bool IsNearFocusMode => _isNearFocusMode;

    private string Ui(string vi, string en, string? zh = null)
        => _lang.CurrentLanguage switch
        {
            "en" => en,
            "zh" => zh ?? en,
            _ => vi
        };

    public void Prewarm()
    {
        try
        {
            EnsureMapInitialized();
            UpdateZonePins();
            UpdateUserPin();
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
            SetNearRoutingCandidate(focusPoi);

            if (_vm.CurrentExploreState == MainViewModel.ExploreState.Near)
            {
                _useInZoneSheetAfterRoute = false;
                _isNearRouteActive = true;
                _nearRouteTargetPoiId = focusPoi.Id;
                _routeDrawRequestVersion++;
                _lastRouteRedraw = DateTime.MinValue;
                _ = DrawNearFocusRouteAsync();
            }
            else if (_vm.CurrentExploreState == MainViewModel.ExploreState.InZone)
            {
                _useInZoneSheetAfterRoute = true;
                _isNearRouteActive = false;
                _nearRouteTargetPoiId = null;
                _routeDrawRequestVersion++;
                ClearNearFocusRoute();
            }
        }

        CenterMapForCurrentState();
        UpdateNearFocusOverlayState();
    }

    private POI? ResolveNearFocusPoi()
    {
        var preferred = _vm.SelectedPinPOI
            ?? _vm.PrimaryZone;

        // In InZone, always prioritize the live active spot instead of the initial navigation target.
        if (preferred == null)
            preferred = _vm.NavigationTarget;
        if (preferred != null)
            return preferred;

        var featuredId = _vm.FeaturedExplorePoi?.Id;
        if (featuredId.HasValue)
            return _vm.AllPOIs.FirstOrDefault(p => p.ZoneType == "Spot" && p.Id == featuredId.Value);

        return _vm.AllPOIs.FirstOrDefault(p => p.ZoneType == "Spot");
    }

    private void SetNearRoutingCandidate(POI poi)
    {
        _vm.NavigationTarget = poi;
        _vm.SelectedPinPOI = poi;
        _vm.VisitedPOIIds.Add(poi.Id);

        if (_isNearRouteActive)
        {
            _nearRouteTargetPoiId = poi.Id;
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.RefreshOfflineBannerSession();

        HookEvents();
        if (_vm.Categories.Count == 0)
            _vm.BuildMapCategories();
        EnsureMapInitialized();
        if (_isNearFocusMode)
        {
            TabMapComponent.IsVisible = false;
            NearFocusOverlay.IsVisible = true;
            InitializeNearFocusMode();
            UpdateSimulationToolsVisibility();
        }
        else
        {
            NearFocusOverlay.IsVisible = false;
            TabMapComponent.IsVisible = true;
            _isSimulationToolsExpanded = false;
            UpdateSimulationToolsVisibility();
            CenterMapForCurrentState();
        }

        UpdateZonePins();
        UpdateUserPin();
        UpdateExploreFarStateAdvanceButton();
        _ = EvaluateInZoneSpotPlaybackAsync(forceSwitch: true);
        StartNearFocusAudioTimer();
        if (!_isNearFocusMode)
        {
            TabMapComponent.InitializeChips(_vm.SelectedCategory);
            TabMapComponent.RefreshLikeIcon();
        }
        UpdatePreviewAudioUiState();
        _previousExploreState = _vm.CurrentExploreState;
        _ = _vm.StartTrackingAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _inZoneToastCts?.Cancel();
        _approachingToastCts?.Cancel();
        _simulateThreeStopsCts?.Cancel();
        StopNearFocusAudioTimer();
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

        MapView.Map.Layers.Clear();
        MapView.Map.Widgets.Clear();
        MapView.Map.BackColor = new MapsColor(15, 28, 22, 255);

        var cacheDb = Path.Combine(FileSystem.AppDataDirectory, "map_cache", "tiles.db");
        var isOnline = Connectivity.Current.NetworkAccess == NetworkAccess.Internet ||
                       Connectivity.Current.NetworkAccess == NetworkAccess.ConstrainedInternet;
        var hasTileCache = HasUsableTileCache(cacheDb);
        var fallbackAdded = false;

        // Always add a lightweight offline base when there is no tile cache.
        // This prevents a fully blank map when network is flaky or blocked.
        if (!hasTileCache)
        {
            MapView.Map.Layers.Add(CreateOfflineFallbackLayer());
            fallbackAdded = true;
        }

        if (isOnline || hasTileCache)
        {
            try
            {
                var tileCache = new StreetFoodNarrator.App.Services.SimpleTileCache(cacheDb);
                var tileSource = new HttpTileSource(
                    new GlobalSphericalMercator(),
                    "https://a.basemaps.cartocdn.com/rastertiles/voyager/{z}/{x}/{y}.png",
                    name: "Carto",
                    persistentCache: tileCache);

                MapView.Map.Layers.Add(new TileLayer(tileSource) { Name = "BaseMap" });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ExploreMapPage] Base tile init failed: {ex.Message}");
                isOnline = false;
            }
        }

        if (!fallbackAdded && !isOnline)
        {
            MapView.Map.Layers.Add(CreateOfflineFallbackLayer());
            fallbackAdded = true;
        }

        if (OfflineMapHint != null && OfflineMapHintLabel != null)
        {
            var showOfflineHint = !isOnline && !hasTileCache;
            OfflineMapHint.IsVisible = showOfflineHint;
            OfflineMapHintLabel.Text = Ui(
                "Đang offline: hiển thị bản đồ tối giản. Khi có mạng, nền chi tiết sẽ tự tải.",
                "Offline mode: showing simplified map. Detailed basemap will load automatically when online.",
                "离线模式：显示简化地图。联网后将自动加载详细底图。");
        }

        var darkOverlay = new MemoryLayer("DarkOverlay")
        {
            Style = new VectorStyle
            {
                Fill = new MapsBrush(new MapsColor(8, 22, 12, fallbackAdded ? 68 : 98)),
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
        _spotZoneLayer = new MemoryLayer("SpotZone");
        _userPinLayer = new MemoryLayer("UserPin");
        _pinsLayer = new MemoryLayer("Pins");
        MapView.Map.Layers.Add(_routeLayer);
        MapView.Map.Layers.Add(_spotZoneLayer);
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

        var now = DateTime.UtcNow;
        var elapsedMs = (now - _lastZonePinsUpdateUtc).TotalMilliseconds;
        if (elapsedMs < ZonePinsMinIntervalMs)
        {
            if (_zonePinsUpdateQueued)
                return;

            _zonePinsUpdateQueued = true;
            var delayMs = Math.Max(120, ZonePinsMinIntervalMs - (int)Math.Max(0, elapsedMs));

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(delayMs).ConfigureAwait(false);
                    if (_isClosing)
                        return;

                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        if (_isClosing)
                            return;

                        _lastZonePinsUpdateUtc = DateTime.UtcNow;
                        UpdateZonePinsCore();
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ExploreMapPage] UpdateZonePins throttle: {ex.Message}");
                }
                finally
                {
                    _zonePinsUpdateQueued = false;
                }
            });
            return;
        }

        _lastZonePinsUpdateUtc = now;
        MainThread.BeginInvokeOnMainThread(UpdateZonePinsCore);
    }

    private void UpdateZonePinsCore()
    {
        if (_pinsLayer == null)
            return;

        var nearbyIds = _vm.ActiveZones.Select(z => z.Id).ToHashSet();
        var visiblePoiIds = _vm.FilteredPOIs.Select(p => p.Id).ToHashSet();
        var isInZoneState = _isNearFocusMode &&
                            (_vm.CurrentExploreState == MainViewModel.ExploreState.InZone || _useInZoneSheetAfterRoute);
        HashSet<int>? inZoneTourPoiIds = null;
        if (isInZoneState && _vm.HasActiveTourOverride)
        {
            var tourIds = GetNavigablePois().Select(p => p.Id).ToList();
            if (tourIds.Count > 0)
                inZoneTourPoiIds = tourIds.ToHashSet();
        }
        var features = new List<IFeature>();

        foreach (var poi in _vm.AllPOIs)
        {
            try
            {
                if (poi.ZoneType == "Area" || poi.ZoneType == "District")
                    continue;

                if (inZoneTourPoiIds != null && !inZoneTourPoiIds.Contains(poi.Id))
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

                var labelText = poi.GetDisplayName(_lang.CurrentLanguage);
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
        MapView?.RefreshGraphics();
    }

    private void UpdateUserPin()
    {
        if (!_isMapInitialized || _userPinLayer == null || MapView?.Map == null)
            return;

        if (_vm.CurrentLat == 0)
            return;

        if (!ShouldRefreshUserPin())
            return;

        var (ux, uy) = SphericalMercator.FromLonLat(_vm.CurrentLon, _vm.CurrentLat);
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
        _lastUserPinLat = _vm.CurrentLat;
        _lastUserPinLon = _vm.CurrentLon;
        _lastUserPinRefreshUtc = DateTime.UtcNow;

        if (_isFirstLocation)
        {
            CenterMapForCurrentState();
            _isFirstLocation = false;
        }

        MapView.RefreshGraphics();
    }

    private bool ShouldRefreshUserPin()
    {
        if (_isFirstLocation)
            return true;

        if (double.IsNaN(_lastUserPinLat) || double.IsNaN(_lastUserPinLon))
            return true;

        var elapsedMs = (DateTime.UtcNow - _lastUserPinRefreshUtc).TotalMilliseconds;
        var movedMeters = HaversineDistance(_lastUserPinLat, _lastUserPinLon, _vm.CurrentLat, _vm.CurrentLon);
        return movedMeters >= UserPinMinMoveMeters || elapsedMs >= UserPinRefreshMinIntervalMs;
    }

    private void QueueLocationDrivenInZoneEvaluation()
    {
        var now = DateTime.UtcNow;
        var elapsedMs = (now - _lastLocationEvalUtc).TotalMilliseconds;
        if (elapsedMs >= LocationEvalMinIntervalMs)
        {
            _lastLocationEvalUtc = now;
            _ = EvaluateInZoneSpotPlaybackAsync();
            return;
        }

        if (_locationEvalQueued)
            return;

        _locationEvalQueued = true;
        var delayMs = Math.Max(120, LocationEvalMinIntervalMs - (int)Math.Max(0, elapsedMs));

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(delayMs).ConfigureAwait(false);
                if (_isClosing)
                    return;

                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    if (_isClosing)
                        return;

                    _lastLocationEvalUtc = DateTime.UtcNow;
                    await EvaluateInZoneSpotPlaybackAsync();
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ExploreMapPage] QueueLocationDrivenInZoneEvaluation: {ex.Message}");
            }
            finally
            {
                _locationEvalQueued = false;
            }
        });
    }

    private void UpdateNearFocusOverlayThrottled()
    {
        if (!_isNearFocusMode)
            return;

        var now = DateTime.UtcNow;
        if ((now - _lastNearOverlayUpdateUtc).TotalMilliseconds < NearOverlayMinIntervalMs)
            return;

        _lastNearOverlayUpdateUtc = now;
        UpdateNearFocusOverlayState();
    }

    private double GetSpotActivationRadius(POI poi)
    {
        var rawRadius = poi.Radius > 0 ? poi.Radius : 15;
        var expandedRadius = rawRadius * SpotZoneTemporaryExpandFactor;
        return Math.Clamp(expandedRadius, AppConfig.SpotZoneMinMeters, AppConfig.SpotZoneMaxMeters);
    }

    private void UpdateSpotZoneOverlay(POI? poi)
    {
        if (_spotZoneLayer == null)
            return;

        // Hide zone-circle overlay (future heatmap will replace it).
        _spotZoneLayer.Features = Array.Empty<IFeature>();
        _spotZoneLayer.DataHasChanged();
        MapView?.RefreshGraphics();
    }

    private static GeometryFeature BuildCircleFeature(double centerLat, double centerLon, double radiusMeters)
    {
        const int segments = 48;
        var coordinates = new List<Coordinate>(segments + 1);
        var latRadians = centerLat * Math.PI / 180.0;

        for (var i = 0; i <= segments; i++)
        {
            var angle = (2 * Math.PI * i) / segments;
            var deltaLat = (radiusMeters / 111_320.0) * Math.Cos(angle);
            var deltaLon = (radiusMeters / (111_320.0 * Math.Cos(latRadians))) * Math.Sin(angle);
            var lon = centerLon + deltaLon;
            var lat = centerLat + deltaLat;
            var (x, y) = SphericalMercator.FromLonLat(lon, lat);
            coordinates.Add(new Coordinate(x, y));
        }

        var ring = new LinearRing(coordinates.ToArray());
        return new GeometryFeature
        {
            Geometry = new Polygon(ring)
        };
    }

    private static GeometryFeature CreateRectanglePolygon(double lonMin, double latMin, double lonMax, double latMax)
    {
        var (x1, y1) = SphericalMercator.FromLonLat(lonMin, latMin);
        var (x2, y2) = SphericalMercator.FromLonLat(lonMax, latMin);
        var (x3, y3) = SphericalMercator.FromLonLat(lonMax, latMax);
        var (x4, y4) = SphericalMercator.FromLonLat(lonMin, latMax);

        var ring = new LinearRing(new[]
        {
            new Coordinate(x1, y1),
            new Coordinate(x2, y2),
            new Coordinate(x3, y3),
            new Coordinate(x4, y4),
            new Coordinate(x1, y1)
        });

        return new GeometryFeature
        {
            Geometry = new Polygon(ring)
        };
    }

    private static bool HasUsableTileCache(string cacheDbPath)
    {
        try
        {
            var info = new FileInfo(cacheDbPath);
            return info.Exists && info.Length > 12 * 1024;
        }
        catch
        {
            return false;
        }
    }

    private static MemoryLayer CreateOfflineFallbackLayer()
    {
        var layer = new MemoryLayer("OfflineFallbackGrid");
        var features = new List<IFeature>();

        var latMin = AppConfig.DefaultLatitude - 0.01;
        var latMax = AppConfig.DefaultLatitude + 0.01;
        var lonMin = AppConfig.DefaultLongitude - 0.01;
        var lonMax = AppConfig.DefaultLongitude + 0.01;

        var districtShell = CreateRectanglePolygon(lonMin, latMin, lonMax, latMax);
        districtShell.Styles = new[]
        {
            new VectorStyle
            {
                Fill = new Mapsui.Styles.Brush(new MapsColor(34, 76, 58, 255)),
                Outline = new Pen(new MapsColor(172, 212, 188, 220), 2.2f)
            }
        };
        features.Add(districtShell);

        void AddRoadHint((double lon, double lat) a, (double lon, double lat) b, float width, MapsColor color)
        {
            var (x1, y1) = SphericalMercator.FromLonLat(a.lon, a.lat);
            var (x2, y2) = SphericalMercator.FromLonLat(b.lon, b.lat);
            features.Add(new GeometryFeature
            {
                Geometry = new LineString(new[] { new Coordinate(x1, y1), new Coordinate(x2, y2) }),
                Styles = new[] { new VectorStyle { Line = new Pen(color, width) } }
            });
        }

        AddRoadHint((106.6964, 10.7630), (106.7094, 10.7576), 3.2f, new MapsColor(230, 205, 168, 245));
        AddRoadHint((106.6976, 10.7566), (106.7090, 10.7605), 2.6f, new MapsColor(220, 190, 152, 230));
        AddRoadHint((106.6991, 10.7594), (106.7056, 10.7549), 2.2f, new MapsColor(208, 180, 146, 220));

        var step = 0.0015;
        for (var lat = latMin; lat <= latMax; lat += step)
        {
            var (x1, y1) = SphericalMercator.FromLonLat(lonMin, lat);
            var (x2, y2) = SphericalMercator.FromLonLat(lonMax, lat);
            features.Add(new GeometryFeature
            {
                Geometry = new LineString(new[] { new Coordinate(x1, y1), new Coordinate(x2, y2) }),
                Styles = new[] { new VectorStyle { Line = new Pen(new MapsColor(156, 184, 164, 64), 1f) } }
            });
        }

        for (var lon = lonMin; lon <= lonMax; lon += step)
        {
            var (x1, y1) = SphericalMercator.FromLonLat(lon, latMin);
            var (x2, y2) = SphericalMercator.FromLonLat(lon, latMax);
            features.Add(new GeometryFeature
            {
                Geometry = new LineString(new[] { new Coordinate(x1, y1), new Coordinate(x2, y2) }),
                Styles = new[] { new VectorStyle { Line = new Pen(new MapsColor(156, 184, 164, 64), 1f) } }
            });
        }

        var (cx, cy) = SphericalMercator.FromLonLat(AppConfig.DefaultLongitude, AppConfig.DefaultLatitude);
        features.Add(new GeometryFeature
        {
            Geometry = new NetTopologySuite.Geometries.Point(cx, cy),
            Styles = new[]
            {
                new SymbolStyle
                {
                    SymbolScale = 0.7,
                    Fill = new Mapsui.Styles.Brush(new MapsColor(255, 239, 213, 255)),
                    Outline = new Pen(new MapsColor(109, 84, 55, 255), 2.4f)
                }
            }
        });

        layer.Features = features;
        return layer;
    }

    private async Task DrawNearFocusRouteAsync()
    {
        var routeTargetPoiId = _nearRouteTargetPoiId;
        var drawVersion = ++_routeDrawRequestVersion;

        if (!_isNearFocusMode || !_isNearRouteActive || !routeTargetPoiId.HasValue)
        {
            ClearNearFocusRoute();
            return;
        }

        if (!_isMapInitialized || MapView?.Map == null || _routeLayer == null)
            return;

        var target = _vm.AllPOIs.FirstOrDefault(p => p.Id == routeTargetPoiId.Value);
        if (target == null)
        {
            ClearNearFocusRoute();
            return;
        }

        var hasCurrentLocation = _vm.CurrentLat != 0 && _vm.CurrentLon != 0;
        var startLon = hasCurrentLocation ? _vm.CurrentLon : AppConfig.DefaultLongitude;
        var startLat = hasCurrentLocation ? _vm.CurrentLat : AppConfig.DefaultLatitude;
        var endLon = target.Longitude;
        var endLat = target.Latitude;

        var (startPx, startPy) = SphericalMercator.FromLonLat(startLon, startLat);
        var (endPx, endPy) = SphericalMercator.FromLonLat(endLon, endLat);

        Coordinate[] routeCoords;
        var isOnline = Connectivity.Current.NetworkAccess == NetworkAccess.Internet ||
                       Connectivity.Current.NetworkAccess == NetworkAccess.ConstrainedInternet;
        var offlineCoords = await FetchOfflineRouteAsync(startLat, startLon, endLat, endLon);
        if (offlineCoords is { Length: >= 2 })
        {
            routeCoords = offlineCoords;
        }
        else if (isOnline && hasCurrentLocation)
        {
            var osrmCoords = await FetchOsrmRouteAsync(startLon, startLat, endLon, endLat);
            routeCoords = osrmCoords ?? new[]
            {
                new Coordinate(startPx, startPy),
                new Coordinate(endPx, endPy)
            };
        }
        else
        {
            routeCoords = new[]
            {
                new Coordinate(startPx, startPy),
                new Coordinate(endPx, endPy)
            };
        }

        if (!_isNearRouteActive ||
            !_nearRouteTargetPoiId.HasValue ||
            _nearRouteTargetPoiId.Value != routeTargetPoiId.Value ||
            drawVersion != _routeDrawRequestVersion)
        {
            ClearNearFocusRoute();
            return;
        }

        var feature = new GeometryFeature(new LineString(routeCoords));
        feature.Styles.Add(new VectorStyle
        {
            Line = new Pen(new MapsColor(75, 226, 119), 4f)
            {
                PenStyle = PenStyle.Solid
            }
        });

        _routeLayer.Features = new[] { feature };
        _routeLayer.DataHasChanged();
        MapView.RefreshGraphics();
    }

    private void ClearNearFocusRoute()
    {
        if (_routeLayer == null)
            return;

        _routeLayer.Features = Array.Empty<IFeature>();
        _routeLayer.DataHasChanged();
        MapView?.RefreshGraphics();
    }

    private async Task<Coordinate[]?> FetchOfflineRouteAsync(
        double srcLat,
        double srcLon,
        double dstLat,
        double dstLon)
    {
        if (_offlineRouting == null)
            return null;

        try
        {
            var result = await _offlineRouting.TryBuildWalkingRouteAsync(
                new GeoCoordinate(srcLat, srcLon),
                new GeoCoordinate(dstLat, dstLon),
                CancellationToken.None);

            if (result?.Path is null || result.Path.Count < 2)
                return null;

            var coords = result.Path
                .Select(point =>
                {
                    var (x, y) = SphericalMercator.FromLonLat(point.Longitude, point.Latitude);
                    return new Coordinate(x, y);
                })
                .ToArray();

            return coords.Length >= 2 ? coords : null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ExploreMapPage] Offline route fallback: {ex.Message}");
            return null;
        }
    }

    private async Task<Coordinate[]?> FetchOsrmRouteAsync(
        double srcLon,
        double srcLat,
        double dstLon,
        double dstLat)
    {
        try
        {
            var ic = System.Globalization.CultureInfo.InvariantCulture;
            var url = $"https://router.project-osrm.org/route/v1/walking/" +
                      $"{srcLon.ToString(ic)},{srcLat.ToString(ic)};" +
                      $"{dstLon.ToString(ic)},{dstLat.ToString(ic)}" +
                      "?geometries=geojson&overview=full";

            var json = await _routeHttpClient.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);

            var routes = doc.RootElement.GetProperty("routes");
            if (routes.GetArrayLength() == 0)
                return null;

            var coords = routes[0]
                .GetProperty("geometry")
                .GetProperty("coordinates")
                .EnumerateArray()
                .Select(c =>
                {
                    if (c.ValueKind != JsonValueKind.Array || c.GetArrayLength() < 2)
                        return new Coordinate(0, 0);

                    var (x, y) = SphericalMercator.FromLonLat(c[0].GetDouble(), c[1].GetDouble());
                    return new Coordinate(x, y);
                })
                .Where(coord => coord.X != 0 || coord.Y != 0)
                .ToArray();

            return coords.Length >= 2 ? coords : null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ExploreMapPage] OSRM fallback (straight line): {ex.Message}");
            return null;
        }
    }

    private async Task<IReadOnlyList<GeoCoordinate>> BuildSimulationWalkingPathAsync(
        double startLat,
        double startLon,
        double destinationLat,
        double destinationLon,
        CancellationToken token,
        double stopDistanceFromDestinationMeters = 0,
        double stepMeters = SimulationPathStepMeters)
    {
        IReadOnlyList<GeoCoordinate>? path = null;

        if (_offlineRouting != null)
        {
            try
            {
                var result = await _offlineRouting.TryBuildWalkingRouteAsync(
                    new GeoCoordinate(startLat, startLon),
                    new GeoCoordinate(destinationLat, destinationLon),
                    token);

                if (result?.Path is { Count: >= 2 })
                    path = result.Path;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ExploreMapPage] BuildSimulationWalkingPathAsync offline fallback: {ex.Message}");
            }
        }

        if (path == null)
        {
            path = await FetchOsrmRouteGeoAsync(startLat, startLon, destinationLat, destinationLon, token);
        }

        path ??= BuildDirectGeoFallbackPath(startLat, startLon, destinationLat, destinationLon);

        if (stopDistanceFromDestinationMeters > 0)
            path = TrimPathToStopDistance(path, stopDistanceFromDestinationMeters);

        var effectiveStepMeters = stepMeters > 0 ? stepMeters : SimulationPathStepMeters;
        return ReducePathForSimulation(path, effectiveStepMeters);
    }

    private async Task<IReadOnlyList<GeoCoordinate>?> FetchOsrmRouteGeoAsync(
        double srcLat,
        double srcLon,
        double dstLat,
        double dstLon,
        CancellationToken token)
    {
        try
        {
            var ic = System.Globalization.CultureInfo.InvariantCulture;
            var url = $"https://router.project-osrm.org/route/v1/walking/" +
                      $"{srcLon.ToString(ic)},{srcLat.ToString(ic)};" +
                      $"{dstLon.ToString(ic)},{dstLat.ToString(ic)}" +
                      "?geometries=geojson&overview=full";

            using var response = await _routeHttpClient.GetAsync(url, token);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(token);

            using var doc = JsonDocument.Parse(json);
            var routes = doc.RootElement.GetProperty("routes");
            if (routes.GetArrayLength() == 0)
                return null;

            var points = routes[0]
                .GetProperty("geometry")
                .GetProperty("coordinates")
                .EnumerateArray()
                .Select(c =>
                {
                    if (c.ValueKind != JsonValueKind.Array || c.GetArrayLength() < 2)
                        return new GeoCoordinate(0, 0);

                    var lon = c[0].GetDouble();
                    var lat = c[1].GetDouble();
                    return new GeoCoordinate(lat, lon);
                })
                .Where(p => p.Latitude != 0 || p.Longitude != 0)
                .ToList();

            return points.Count >= 2 ? points : null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ExploreMapPage] FetchOsrmRouteGeoAsync fallback: {ex.Message}");
            return null;
        }
    }

    private static IReadOnlyList<GeoCoordinate> BuildDirectGeoFallbackPath(
        double startLat,
        double startLon,
        double destinationLat,
        double destinationLon)
    {
        const int fallbackSteps = 16;
        var points = new List<GeoCoordinate>(fallbackSteps + 1);
        for (var i = 0; i <= fallbackSteps; i++)
        {
            var t = i / (double)fallbackSteps;
            points.Add(new GeoCoordinate(
                Lerp(startLat, destinationLat, t),
                Lerp(startLon, destinationLon, t)));
        }

        return points;
    }

    private static IReadOnlyList<GeoCoordinate> TrimPathToStopDistance(
        IReadOnlyList<GeoCoordinate> path,
        double stopDistanceFromDestinationMeters)
    {
        if (path.Count < 2 || stopDistanceFromDestinationMeters <= 0)
            return path;

        var totalDistance = 0d;
        for (var i = 1; i < path.Count; i++)
        {
            totalDistance += HaversineDistance(
                path[i - 1].Latitude,
                path[i - 1].Longitude,
                path[i].Latitude,
                path[i].Longitude);
        }

        if (totalDistance <= stopDistanceFromDestinationMeters)
            return path;

        var keepDistance = totalDistance - stopDistanceFromDestinationMeters;
        var kept = new List<GeoCoordinate> { path[0] };
        var walked = 0d;

        for (var i = 1; i < path.Count; i++)
        {
            var prev = path[i - 1];
            var curr = path[i];
            var segment = HaversineDistance(prev.Latitude, prev.Longitude, curr.Latitude, curr.Longitude);
            if (segment <= 0.01)
                continue;

            if (walked + segment < keepDistance)
            {
                kept.Add(curr);
                walked += segment;
                continue;
            }

            var ratio = Math.Clamp((keepDistance - walked) / segment, 0.0, 1.0);
            kept.Add(new GeoCoordinate(
                Lerp(prev.Latitude, curr.Latitude, ratio),
                Lerp(prev.Longitude, curr.Longitude, ratio)));
            break;
        }

        return kept.Count >= 2 ? kept : path;
    }

    private static IReadOnlyList<GeoCoordinate> ReducePathForSimulation(
        IReadOnlyList<GeoCoordinate> path,
        double stepMeters)
    {
        if (path.Count < 2 || stepMeters <= 0)
            return path;

        var reduced = new List<GeoCoordinate> { path[0] };
        var lastKept = path[0];

        for (var i = 1; i < path.Count; i++)
        {
            var current = path[i];
            var segment = HaversineDistance(lastKept.Latitude, lastKept.Longitude, current.Latitude, current.Longitude);
            if (segment < stepMeters && i < path.Count - 1)
                continue;

            reduced.Add(current);
            lastKept = current;
        }

        if (reduced[^1].Latitude != path[^1].Latitude || reduced[^1].Longitude != path[^1].Longitude)
            reduced.Add(path[^1]);

        return reduced;
    }

    private async Task WalkSimulationPathAsync(
        IReadOnlyList<GeoCoordinate> path,
        CancellationToken token,
        bool evaluateInZoneDuringWalk,
        double delayScale = 1.0,
        double microPauseChance = 0.08)
    {
        if (path.Count == 0)
            return;

        var normalizedDelayScale = Math.Clamp(delayScale, 0.35, 1.5);
        var normalizedPauseChance = Math.Clamp(microPauseChance, 0.0, 0.4);
        var stepDelayMin = Math.Max(80, (int)Math.Round(SimulationStepDelayMinMs * normalizedDelayScale));
        var stepDelayMax = Math.Max(stepDelayMin + 20, (int)Math.Round(SimulationStepDelayMaxMs * normalizedDelayScale));
        var microPauseMin = Math.Max(180, (int)Math.Round(650 * normalizedDelayScale));
        var microPauseMax = Math.Max(microPauseMin + 60, (int)Math.Round(1400 * normalizedDelayScale));

        for (var i = 0; i < path.Count; i++)
        {
            token.ThrowIfCancellationRequested();
            var point = path[i];
            SetVirtualLocation(point.Latitude, point.Longitude);
            UpdateUserPin();

            if (evaluateInZoneDuringWalk && (i % 3 == 0 || i == path.Count - 1))
                await EvaluateInZoneSpotPlaybackAsync(forceSwitch: false);

            if (i == path.Count - 1)
                continue;

            if (Random.Shared.NextDouble() < normalizedPauseChance)
            {
                // Random micro-pauses make movement look less robotic.
                await Task.Delay(Random.Shared.Next(microPauseMin, microPauseMax + 1), token);
            }

            await Task.Delay(Random.Shared.Next(stepDelayMin, stepDelayMax + 1), token);
        }
    }

    private static int GetHumanStopDwellDurationMs()
    {
        // Mostly short waits, occasionally a longer stop (queue/photo/chat).
        if (Random.Shared.NextDouble() < 0.25)
            return Random.Shared.Next(8500, 15000);

        return Random.Shared.Next(3200, 7600);
    }

    private async Task SwitchToInZoneSpotAsync(POI poi, bool forceAudioRestart)
    {
        var switchingToDifferentPoi = _vm.PlayingPoiId.HasValue && _vm.PlayingPoiId.Value != poi.Id;

        _vm.NavigationTarget = poi;
        _vm.SelectedPinPOI = poi;
        _vm.PrimaryZone = poi;
        _vm.PrimaryZoneName = poi.GetDisplayName(_lang.CurrentLanguage);
        _vm.PrimaryZoneType = poi.ZoneType ?? poi.Category ?? string.Empty;
        _vm.PrimaryZoneDesc = poi.GetDisplayDescription(_lang.CurrentLanguage);
        _vm.PrimaryZoneAddress = poi.Address ?? Ui("Đang cập nhật", "Updating", "更新中");
        _vm.PrimaryZoneRating = (poi.Rating ?? 4.5).ToString("F1");
        _vm.VisitedPOIIds.Add(poi.Id);
        UpdateZonePins();

        CenterMapOnPoi(poi, InZoneFocusZoomLevel);

        if (switchingToDifferentPoi && (_tts.IsPlaying() || _vm.IsAudioPaused))
            await StopPreviewAudioAsync();

        var isSamePlaying = _vm.PlayingPoiId == poi.Id && (_tts.IsPlaying() || _vm.IsAudioPaused);
        if (!isSamePlaying || forceAudioRestart || switchingToDifferentPoi)
            await PlayPreviewAudioAsync(poi, allowToggleCurrent: false, showErrorAlert: false);
    }

    private async Task EvaluateInZoneSpotPlaybackAsync(bool forceSwitch = false)
    {
        if (_isInZoneEvalRunning)
        {
            _pendingInZoneEval = true;
            _pendingInZoneEvalForceSwitch |= forceSwitch;
            return;
        }

        _isInZoneEvalRunning = true;
        try
        {
            if (_vm.CurrentExploreState == MainViewModel.ExploreState.InZone && _isNearRouteActive)
            {
                _isNearRouteActive = false;
                _nearRouteTargetPoiId = null;
                _routeDrawRequestVersion++;
                ClearNearFocusRoute();
            }

            if (_isNearRouteActive && _nearRouteTargetPoiId.HasValue)
            {
                var routePoi = _vm.AllPOIs.FirstOrDefault(p => p.Id == _nearRouteTargetPoiId.Value);
                if (routePoi != null)
                {
                    _vm.NavigationTarget = routePoi;
                    _vm.SelectedPinPOI = routePoi;
                    EvaluateNearRouteCompletion(routePoi);
                    UpdateNearFocusOverlayState();
                    return;
                }
            }

            if (!_isNearFocusMode || _vm.CurrentExploreState != MainViewModel.ExploreState.InZone)
            {
                if (_activeSpotZonePoiId.HasValue && (_tts.IsPlaying() || _vm.IsAudioPaused))
                    await StopPreviewAudioAsync();
                _activeSpotZonePoiId = null;
                _candidateSpotZonePoiId = null;
                UpdateSpotZoneOverlay(null);
                HideInZoneToastIfVisible();
                return;
            }

            if (_vm.CurrentLat == 0 && _vm.CurrentLon == 0)
                return;

            var spotEntries = BuildSpotZoneEntries();

            if (spotEntries.Count == 0)
                return;

            var candidate = spotEntries
                .Where(s => s.IsInside)
                .OrderByDescending(s => s.Poi.Priority)
                .ThenByDescending(s => s.Confidence)
                .ThenBy(s => s.Distance)
                .ThenBy(s => s.Poi.Id)
                .FirstOrDefault();

            var currentEntry = _activeSpotZonePoiId.HasValue
                ? spotEntries.FirstOrDefault(s => s.Poi.Id == _activeSpotZonePoiId.Value)
                : null;

            if (currentEntry != null &&
                currentEntry.Distance <= currentEntry.Radius)
            {
                // Keep current spot immediately only when there is no competing candidate.
                // If a different candidate exists, continue into debounce path so we can switch
                // after stability delay instead of getting "stuck" on the old spot.
                var hasCompetingCandidate = candidate != null && candidate.Poi.Id != currentEntry.Poi.Id;
                if (!hasCompetingCandidate)
                {
                    UpdateSpotZoneOverlay(currentEntry.Poi);
                    if (currentEntry.Distance <= currentEntry.Radius * 0.9)
                        HideApproachingToastForPoi(currentEntry.Poi.Id);
                    if (_vm.SelectedPinPOI?.Id != currentEntry.Poi.Id && !_isDetailPageOpen)
                        await SwitchToInZoneSpotAsync(currentEntry.Poi, forceAudioRestart: _vm.PlayingPoiId != currentEntry.Poi.Id);
                    return;
                }
            }

            if (candidate == null)
            {
                if (_activeSpotZonePoiId.HasValue && (_tts.IsPlaying() || _vm.IsAudioPaused))
                    await StopPreviewAudioAsync();
                _activeSpotZonePoiId = null;
                _candidateSpotZonePoiId = null;
                UpdateSpotZoneOverlay(null);
                HideInZoneToastIfVisible();
                return;
            }

            if (_activeSpotZonePoiId.HasValue && _activeSpotZonePoiId.Value != candidate.Poi.Id)
                ShowApproachingNextPoiToastIfNeeded(candidate.Poi, candidate.Distance);

            if (!forceSwitch)
            {
                if (_candidateSpotZonePoiId != candidate.Poi.Id)
                {
                    _candidateSpotZonePoiId = candidate.Poi.Id;
                    _candidateSpotSeenUtc = DateTime.UtcNow;
                    if (_activeSpotZonePoiId.HasValue && _activeSpotZonePoiId.Value != candidate.Poi.Id)
                        ShowApproachingNextPoiToastIfNeeded(candidate.Poi, candidate.Distance);
                    return;
                }

                if ((DateTime.UtcNow - _candidateSpotSeenUtc).TotalMilliseconds < AppConfig.SpotZoneActivationDelayMs)
                    return;
            }

            _candidateSpotZonePoiId = null;
            _activeSpotZonePoiId = candidate.Poi.Id;
            UpdateSpotZoneOverlay(candidate.Poi);
            HideApproachingToastForPoi(candidate.Poi.Id);

            if (_isDetailPageOpen)
            {
                _pendingPoiIdWhileDetailOpen = candidate.Poi.Id;
                return;
            }

            var tooSoon = !forceSwitch &&
                          (DateTime.UtcNow - _lastAutoPoiSwitchUtc).TotalMilliseconds < AppConfig.SpotZoneSwitchCooldownMs;
            if (tooSoon && _vm.SelectedPinPOI?.Id != candidate.Poi.Id)
                return;

            await SwitchToInZoneSpotAsync(candidate.Poi, forceAudioRestart: _vm.PlayingPoiId != candidate.Poi.Id);
            _lastAutoPoiSwitchUtc = DateTime.UtcNow;
        }
        finally
        {
            _isInZoneEvalRunning = false;

            if (_pendingInZoneEval)
            {
                var replayForceSwitch = _pendingInZoneEvalForceSwitch;
                _pendingInZoneEval = false;
                _pendingInZoneEvalForceSwitch = false;
                _ = EvaluateInZoneSpotPlaybackAsync(replayForceSwitch);
            }
        }
    }

    private List<SpotZoneEntry> BuildSpotZoneEntries()
    {
        var spots = _vm.AllPOIs.Where(p => p.ZoneType == "Spot").ToList();
        if (spots.Count == 0)
            return new List<SpotZoneEntry>();

        return spots.Select(p =>
        {
            var distance = HaversineDistance(_vm.CurrentLat, _vm.CurrentLon, p.Latitude, p.Longitude);
            var radius = GetDynamicSpotActivationRadius(p, spots);
            var confidence = radius > 0
                ? Math.Max(0.0, 1.0 - (distance / radius))
                : 0.0;

            return new SpotZoneEntry
            {
                Poi = p,
                Distance = distance,
                Radius = radius,
                Confidence = confidence
            };
        }).ToList();
    }

    private double GetDynamicSpotActivationRadius(POI targetPoi, IReadOnlyCollection<POI> allSpots)
    {
        const double radiusScale = 0.72;
        var minRadius = Math.Max(12d, AppConfig.SpotZoneMinMeters * 0.8 * SpotZoneTemporaryExpandFactor);
        var maxRadius = Math.Max(18d, AppConfig.SpotZoneMaxMeters * 0.72 * SpotZoneTemporaryExpandFactor);

        var nearestDistance = allSpots
            .Where(p => p.Id != targetPoi.Id)
            .Select(p => HaversineDistance(targetPoi.Latitude, targetPoi.Longitude, p.Latitude, p.Longitude))
            .DefaultIfEmpty(double.MaxValue)
            .Min();

        if (double.IsInfinity(nearestDistance) || nearestDistance == double.MaxValue)
            return Math.Clamp(GetSpotActivationRadius(targetPoi) * radiusScale, minRadius, maxRadius);

        var dynamicRadius = ((nearestDistance / 2.0) - AppConfig.SpotZoneGpsErrorBufferMeters) * radiusScale * SpotZoneTemporaryExpandFactor;
        return Math.Clamp(dynamicRadius, minRadius, maxRadius);
    }

    private void ShowApproachingNextPoiToastIfNeeded(POI poi, double distanceMeters)
    {
        var tooSoon = _lastApproachingToastPoiId == poi.Id &&
                      (DateTime.UtcNow - _lastApproachingToastUtc).TotalMilliseconds < AppConfig.SpotZoneApproachToastCooldownMs;
        if (tooSoon)
            return;

        _lastApproachingToastPoiId = poi.Id;
        _lastApproachingToastUtc = DateTime.UtcNow;

        var poiName = poi.GetDisplayName(_lang.CurrentLanguage);
        if (string.IsNullOrWhiteSpace(poiName))
            poiName = Ui("điểm tiếp theo", "next stop", "下一站");
        var distText = distanceMeters < 1000 ? $"{distanceMeters:F0}m" : $"{distanceMeters / 1000:F1}km";
        _ = ShowApproachingToastCompactAsync(poi.Id, $"→ {poiName} {distText}");
    }

    private async Task ShowApproachingToastCompactAsync(int poiId, string message)
    {
        if (InZoneToast == null || InZoneToastLabel == null)
            return;

        _approachingToastCts?.Cancel();
        _approachingToastCts = new CancellationTokenSource();
        var token = _approachingToastCts.Token;

        _approachingToastPoiId = poiId;
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            InZoneToastLabel.Text = message;
            InZoneToast.IsVisible = true;
            InZoneToast.Opacity = 1;
        });

        try
        {
            await Task.Delay(2600, token);
            if (token.IsCancellationRequested)
                return;

            // Auto-hide compact toast if user has not reached this candidate yet.
            if (_approachingToastPoiId == poiId)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    InZoneToast.Opacity = 0;
                    InZoneToast.IsVisible = false;
                });
                _approachingToastPoiId = null;
            }
        }
        catch (TaskCanceledException)
        {
            // Ignore
        }
    }

    private void HideApproachingToastForPoi(int poiId)
    {
        if (_approachingToastPoiId == poiId)
            HideInZoneToastIfVisible();
    }

    private void OnInZoneToastCloseTapped(object? sender, TappedEventArgs e)
    {
        HideInZoneToastIfVisible();
    }

    private void HideInZoneToastIfVisible()
    {
        _inZoneToastCts?.Cancel();
        _approachingToastCts?.Cancel();
        _approachingToastPoiId = null;
        if (InZoneToast == null)
            return;

        InZoneToast.Opacity = 0;
        InZoneToast.IsVisible = false;
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
                if (_vm.CurrentExploreState == MainViewModel.ExploreState.Near ||
                    _vm.CurrentExploreState == MainViewModel.ExploreState.InZone)
                    return;

                SetNearRoutingCandidate(nearest);
                UpdateNearFocusOverlayState();
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
        await HandleBackOrMinimizeAsync();
    }

    private async void OnSavedRequested(object? sender, EventArgs e)
    {
        await NavigateToShellRouteAsync("//SavedPage");
    }

    private async void OnProfileRequested(object? sender, EventArgs e)
    {
        await NavigateToShellRouteAsync("//SettingsPage");
    }

    private async void OnNearFocusBackTapped(object? sender, EventArgs e)
        => await HandleBackOrMinimizeAsync();

    private async Task HandleBackOrMinimizeAsync()
    {
        var shouldMinimizeInZone = _isNearFocusMode &&
                                   _vm.CurrentExploreState == MainViewModel.ExploreState.InZone &&
                                   _vm.ViewState == MainViewModel.MapViewState.InZoneActive;

        if (shouldMinimizeInZone)
        {
            _vm.MinimizeInZoneCommand.Execute(null);
            await ClosePageAsync(preservePreviewAudio: true);
            return;
        }

        await ClosePageAsync();
    }

    private void OnNearFocusSettingsTapped(object? sender, EventArgs e)
        => OnSettingsRequested(sender, e);

    private async void OnNearFocusSavedTapped(object? sender, EventArgs e)
    {
        await NavigateToShellRouteAsync("//SavedPage");
    }

    private async void OnNearFocusProfileTapped(object? sender, EventArgs e)
    {
        await NavigateToShellRouteAsync("//SettingsPage");
    }

    private async void OnNearFocusSeeMoreTapped(object? sender, EventArgs e)
    {
        var poi = ResolveNearFocusPoi();
        if (poi == null)
            return;

        await OpenPoiDetailAsync(poi);
    }

    private async void OnNearFocusPlayPauseTapped(object? sender, EventArgs e)
    {
        var poi = ResolveNearFocusPoi();
        if (poi == null)
            return;

        await PlayPreviewAudioAsync(poi, allowToggleCurrent: true, showErrorAlert: true);
        UpdateNearFocusOverlayState();
    }

    private async void OnNearFocusCollapsedStartTapped(object? sender, EventArgs e)
    {
        var poi = ResolveNearFocusPoi();
        if (poi == null)
            return;

        OnStartFromHereRequested(sender, poi);
        await Task.CompletedTask;
    }

    private async void OnNearFocusCollapsedDetailTapped(object? sender, EventArgs e)
    {
        var poi = ResolveNearFocusPoi();
        if (poi == null)
            return;

        await OpenPoiDetailAsync(poi);
    }

    private void OnNearFocusCollapsedStartClicked(object? sender, EventArgs e)
        => OnNearFocusCollapsedStartTapped(sender, e);

    private void OnNearFocusCollapsedDetailClicked(object? sender, EventArgs e)
        => OnNearFocusCollapsedDetailTapped(sender, e);

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
        if (index < 0 || index >= _nearFocusNearbyPois.Count)
            return;

        var poi = _nearFocusNearbyPois[index];
        SetNearRoutingCandidate(poi);
        CenterMapForNearFocus();
        UpdateNearFocusOverlayState();
        UpdateZonePins();
    }

    private void OnNearFocusSheetToggleTapped(object? sender, EventArgs e)
    {
        var isFocusState = _vm.CurrentExploreState == MainViewModel.ExploreState.Near ||
                           _vm.CurrentExploreState == MainViewModel.ExploreState.InZone;
        if (!_isNearFocusMode || !isFocusState)
            return;

        _isNearSheetCollapsed = !_isNearSheetCollapsed;
        UpdateNearFocusSheetState();
        UpdateNearRouteActionVisibility();
    }

    private void UpdateNearFocusSheetState()
    {
        var isFocusState = _isNearFocusMode &&
                           (_vm.CurrentExploreState == MainViewModel.ExploreState.Near ||
                            _vm.CurrentExploreState == MainViewModel.ExploreState.InZone);

        if (_vm.CurrentExploreState == MainViewModel.ExploreState.InZone &&
            _lastNearFocusState != MainViewModel.ExploreState.InZone)
        {
            _isNearSheetCollapsed = false;
        }

        NearFocusExpandedContent.IsVisible = isFocusState && !_isNearSheetCollapsed;
        NearFocusCollapsedContent.IsVisible = !isFocusState || _isNearSheetCollapsed;
        NearFocusSheetToggleHandle.IsVisible = isFocusState;
        NearFocusSheetToggleLabel.Text = _isNearSheetCollapsed
            ? Ui("Mở rộng", "Expand", "展开")
            : Ui("Thu gọn", "Collapse", "收起");
        _lastNearFocusState = _vm.CurrentExploreState;
    }

    private void UpdateNearRouteActionVisibility()
    {
        var isInZoneState = _isNearFocusMode &&
                            (_vm.CurrentExploreState == MainViewModel.ExploreState.InZone || _useInZoneSheetAfterRoute);
        var isNearState = _isNearFocusMode &&
                          _vm.CurrentExploreState == MainViewModel.ExploreState.Near &&
                          !isInZoneState;
        var isFarState = _isNearFocusMode &&
                         _vm.CurrentExploreState == MainViewModel.ExploreState.Far;
        var shouldShowRouteActions = isNearState;
        var isCollapsed = _isNearSheetCollapsed;

        NearFocusCollapsedInZoneActions.IsVisible = isInZoneState && !shouldShowRouteActions;
        NearFocusCollapsedProgressSection.IsVisible = isInZoneState;
        NearRouteCollapsedActions.IsVisible = shouldShowRouteActions && isCollapsed;
        NearFocusCollapsedFarActions.IsVisible = isFarState;

        NearFocusRouteStatusLabel.IsVisible = isInZoneState;
        NearFocusRouteMetaLabel.IsVisible = isInZoneState;

        NearRouteContinueButton.IsVisible = shouldShowRouteActions;
        NearRouteCancelButton.IsVisible = shouldShowRouteActions;

        NearRouteCancelButton.Text = Ui("⏭  Bỏ qua quán", "⏭  Skip stop", "⏭  跳过当前店铺");
        NearRouteCollapsedCancelButton.Text = Ui("Bỏ qua", "Skip", "跳过");
        NearFocusCollapsedFarDetailButton.Text = Ui("Xem chi tiết", "Details", "详情");
        NearFocusCollapsedFarStartButton.Text = Ui("🚀  Bắt đầu từ đây", "🚀  Start here", "🚀 从这里开始");

        var canStopTour = (isNearState || isInZoneState) && _vm.HasActiveTourOverride;
        InZoneStopTourButton.IsVisible = canStopTour;
        NearFocusCollapsedStopTourButton.IsVisible = canStopTour;

        var continueText = _isNearRouteActive
            ? Ui("⏹  Hủy chỉ đường", "⏹  Cancel route", "⏹  取消导航")
            : Ui("➤  Chỉ đường", "➤  Navigate", "➤  开始导航");
        NearRouteContinueButton.Text = continueText;
        NearRouteCollapsedContinueButton.Text = continueText;
    }

    private async Task ClosePageAsync(bool preservePreviewAudio = false, bool animated = true)
    {
        if (_isClosing)
            return;

        _isClosing = true;

        try
        {
            if (!preservePreviewAudio)
                await StopPreviewAudioAsync();
            _vm.SelectedPinPOI = null;
            TabMapComponent.HideSuggestions();
            if (Navigation.ModalStack.Any())
                await Navigation.PopModalAsync(animated);
            else
                await Navigation.PopAsync(animated);
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
            await NavigateToShellRouteAsync("//SettingsPage");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ExploreMapPage] OnSettingsRequested: {ex}");
        }
    }

    private async Task NavigateToShellRouteAsync(string shellRoute)
    {
        await ClosePageAsync(animated: false);

        if (Shell.Current == null)
            return;

        await Shell.Current.GoToAsync(shellRoute, false);
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

    private void OnNearFocusCenterUserTapped(object? sender, TappedEventArgs e)
    {
        EnsureMapInitialized();
        CenterMapForCurrentState();
    }

    private void OnNearFocusSimulateToggleTapped(object? sender, TappedEventArgs e)
    {
        _isSimulationToolsExpanded = !_isSimulationToolsExpanded;
        UpdateSimulationToolsVisibility();
    }

    private void UpdateSimulationToolsVisibility()
    {
        var shouldShowPanel = _isNearFocusMode && _isSimulationToolsExpanded;
        if (NearFocusSimulateOptionsPanel != null)
            NearFocusSimulateOptionsPanel.IsVisible = shouldShowPanel;

        if (NearFocusSimulateToggleIcon != null)
            NearFocusSimulateToggleIcon.Text = shouldShowPanel ? "✕" : "🧪";
    }

    private async void OnNearFocusSimulateThreeStopsTapped(object? sender, TappedEventArgs e)
    {
        _isSimulationToolsExpanded = false;
        UpdateSimulationToolsVisibility();
        await RunSimulateThreeStopsAsync();
    }

    private async void OnNearFocusSimulateInZoneTapped(object? sender, TappedEventArgs e)
    {
        _isSimulationToolsExpanded = false;
        UpdateSimulationToolsVisibility();
        await RunSimulateInZoneAsync();
    }

    private async void OnExploreFarStateAdvanceTapped(object? sender, TappedEventArgs e)
    {
        if (_isNearFocusMode)
            return;

        await RunSimulateNearFromFarAsync();
    }

    private async void OnNearFocusSimulateFarTapped(object? sender, TappedEventArgs e)
    {
        _isSimulationToolsExpanded = false;
        UpdateSimulationToolsVisibility();
        await RunSimulateFarAsync();
    }

    private void OnVirtualStateAdvanceTapped(object? sender, TappedEventArgs e)
    {
        if (!_isNearFocusMode)
            return;

        var nextState = _vm.CurrentExploreState switch
        {
            MainViewModel.ExploreState.Far => MainViewModel.ExploreState.Near,
            MainViewModel.ExploreState.Near => MainViewModel.ExploreState.InZone,
            _ => MainViewModel.ExploreState.InZone
        };

        _vm.SetExploreStateDemoCommand.Execute(nextState.ToString());
        _useInZoneSheetAfterRoute = false;
        UpdateNearFocusOverlayState();
        CenterMapForCurrentState();
    }

    private async Task RunSimulateThreeStopsAsync()
    {
        try
        {
            _simulateThreeStopsCts?.Cancel();
            _simulateThreeStopsCts = new CancellationTokenSource();
            var token = _simulateThreeStopsCts.Token;

            var startLat = _vm.CurrentLat != 0 ? _vm.CurrentLat : AppConfig.DefaultLatitude;
            var startLon = _vm.CurrentLon != 0 ? _vm.CurrentLon : AppConfig.DefaultLongitude;

            var navigableSpots = GetNavigablePois();
            var spots = ResolveTopSimulationStops(navigableSpots, startLat, startLon, out var usingSelectedTour);

            if (spots.Count == 0)
            {
                await ShowInZoneToastAsync(Ui("Chưa có dữ liệu quán để giả lập.", "No venue data available for simulation.", "没有可用于模拟的店铺数据。"));
                return;
            }

            await ShowApproachingToastCompactAsync(
                spots[0].Id,
                usingSelectedTour
                    ? Ui(
                        $"→ Theo tour đã chọn: {spots.Count} quán",
                        $"→ Follow selected tour: {spots.Count} venues",
                        $"→ 按已选路线：{spots.Count} 个店铺")
                    : Ui(
                        $"→ Không có tour: giả lập ngẫu nhiên {spots.Count} quán",
                        $"→ No active tour: random {spots.Count}-venue simulation",
                        $"→ 未选择路线：随机模拟 {spots.Count} 个店铺"));

            foreach (var spot in spots)
            {
                token.ThrowIfCancellationRequested();
                var spotName = spot.GetDisplayName(_lang.CurrentLanguage);
                if (string.IsNullOrWhiteSpace(spotName))
                    spotName = Ui("quán tiếp theo", "next venue", "下一家");
                await ShowApproachingToastCompactAsync(
                    spot.Id,
                    Ui($"→ Tiến tới {spotName}", $"→ Moving to {spotName}", $"→ 前往 {spotName}"));

                var currentLat = _vm.CurrentLat != 0 ? _vm.CurrentLat : AppConfig.DefaultLatitude;
                var currentLon = _vm.CurrentLon != 0 ? _vm.CurrentLon : AppConfig.DefaultLongitude;
                var spotsForRadius = _vm.AllPOIs.Where(p => p.ZoneType == "Spot").ToList();
                var zoneRadius = GetDynamicSpotActivationRadius(spot, spotsForRadius);
                var reliableArrivalDistance = Math.Clamp(zoneRadius * 0.18, 1.2, 3.2);

                var walkingPath = await BuildSimulationWalkingPathAsync(
                    currentLat,
                    currentLon,
                    spot.Latitude,
                    spot.Longitude,
                    token,
                    stopDistanceFromDestinationMeters: reliableArrivalDistance);

                await WalkSimulationPathAsync(walkingPath, token, evaluateInZoneDuringWalk: true);
                await MoveCloserToPoiForReliableZoneAsync(spot, reliableArrivalDistance, token);

                var arrivedLat = _vm.CurrentLat != 0 ? _vm.CurrentLat : walkingPath[^1].Latitude;
                var arrivedLon = _vm.CurrentLon != 0 ? _vm.CurrentLon : walkingPath[^1].Longitude;
                SetVirtualLocation(arrivedLat, arrivedLon);
                UpdateUserPin();
                CenterMapOnPoi(spot, InZoneFocusZoomLevel);

                var dwellDurationMs = usingSelectedTour
                    ? Random.Shared.Next(3600, 6201)
                    : Random.Shared.Next(5000, 7001);
                var holdUntil = DateTime.UtcNow.AddMilliseconds(dwellDurationMs);
                while (DateTime.UtcNow < holdUntil)
                {
                    token.ThrowIfCancellationRequested();
                    await EvaluateInZoneSpotPlaybackAsync(forceSwitch: false);
                    await Task.Delay(Random.Shared.Next(260, 520), token);
                }

                if (usingSelectedTour)
                    _tourSimulationVisitedPoiIds.Add(spot.Id);

                // One final pass to ensure bottom sheet/audio reflect the new spot.
                await EvaluateInZoneSpotPlaybackAsync(forceSwitch: false);
            }

            HideInZoneToastIfVisible();
        }
        catch (TaskCanceledException)
        {
            // Ignore
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ExploreMapPage] RunSimulateThreeStopsAsync error: {ex}");
            await DisplayAlertAsync(
                Ui("Lỗi", "Error", "错误"),
                Ui("Không thể chạy giả lập 3 quán lúc này.", "Unable to run 3-stop simulation right now.", "当前无法运行三站模拟。"),
                "OK");
        }
    }

    private async Task RunSimulateFarAsync()
    {
        try
        {
            _simulateThreeStopsCts?.Cancel();
            _simulateThreeStopsCts = new CancellationTokenSource();
            var token = _simulateThreeStopsCts.Token;

            var farLat = AppConfig.DefaultLatitude + 0.015;
            var farLon = AppConfig.DefaultLongitude + 0.015;
            await ShowApproachingToastCompactAsync(
                -1,
                Ui("→ Giả lập vị trí ra xa khu ẩm thực", "→ Simulate moving far from the food area", "→ 模拟移动到美食区外"));

            const int steps = 10;
            var startLat = _vm.CurrentLat != 0 ? _vm.CurrentLat : AppConfig.DefaultLatitude;
            var startLon = _vm.CurrentLon != 0 ? _vm.CurrentLon : AppConfig.DefaultLongitude;
            for (var i = 1; i <= steps; i++)
            {
                token.ThrowIfCancellationRequested();
                var t = i / (double)steps;
                SetVirtualLocation(Lerp(startLat, farLat, t), Lerp(startLon, farLon, t));
                UpdateUserPin();
                await Task.Delay(150, token);
            }

            _isNearRouteActive = false;
            _nearRouteTargetPoiId = null;
            _routeDrawRequestVersion++;
            ClearNearFocusRoute();
            await EvaluateInZoneSpotPlaybackAsync(forceSwitch: true);
            UpdateNearFocusOverlayState();
            CenterMapForCurrentState();
            await ShowApproachingToastCompactAsync(
                -1,
                Ui("Đã chuyển sang trạng thái ở xa", "Switched to far-away state", "已切换到远离区域状态"));
        }
        catch (TaskCanceledException)
        {
            // Ignore
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ExploreMapPage] RunSimulateFarAsync error: {ex}");
        }
    }

    private async Task RunSimulateInZoneAsync()
    {
        try
        {
            _simulateThreeStopsCts?.Cancel();
            _simulateThreeStopsCts = new CancellationTokenSource();
            var token = _simulateThreeStopsCts.Token;

            var startLat = _vm.CurrentLat != 0 ? _vm.CurrentLat : AppConfig.DefaultLatitude;
            var startLon = _vm.CurrentLon != 0 ? _vm.CurrentLon : AppConfig.DefaultLongitude;
            var navigableSpots = GetNavigablePois();
            var targetSpot = ResolveNextZoneProbeTarget(navigableSpots, startLat, startLon);

            if (targetSpot == null)
            {
                await ShowApproachingToastCompactAsync(
                    -1,
                    Ui("→ Chưa có dữ liệu quán để giả lập vào vùng quán", "→ No venue data to simulate in-venue state", "→ 没有可用于模拟进入店铺范围的数据"));
                return;
            }

            var spotsForRadius = _vm.AllPOIs.Where(p => p.ZoneType == "Spot").ToList();
            var inZoneRadius = GetDynamicSpotActivationRadius(targetSpot, spotsForRadius);
            var insideDistance = Math.Clamp(inZoneRadius * 0.18, 1.2, 3.2);

            await ShowApproachingToastCompactAsync(
                targetSpot.Id,
                Ui(
                    $"→ Test zone quán: {Fallback(targetSpot.GetDisplayName("vi"), "khu ẩm thực")}",
                    $"→ Zone test venue: {Fallback(targetSpot.GetDisplayName("en"), "food area")}",
                    $"→ 测试店铺范围：{Fallback(targetSpot.GetDisplayName("zh"), "美食区")}"));

            var inZonePath = await BuildSimulationWalkingPathAsync(
                startLat,
                startLon,
                targetSpot.Latitude,
                targetSpot.Longitude,
                token,
                stopDistanceFromDestinationMeters: insideDistance);

            await WalkSimulationPathAsync(inZonePath, token, evaluateInZoneDuringWalk: true);
            await MoveCloserToPoiForReliableZoneAsync(targetSpot, insideDistance, token);

            var arrivedLat = _vm.CurrentLat != 0 ? _vm.CurrentLat : inZonePath[^1].Latitude;
            var arrivedLon = _vm.CurrentLon != 0 ? _vm.CurrentLon : inZonePath[^1].Longitude;
            SetVirtualLocation(arrivedLat, arrivedLon);
            UpdateUserPin();
            UpdateZonePins();

            _isNearRouteActive = false;
            _nearRouteTargetPoiId = null;
            _routeDrawRequestVersion++;
            _useInZoneSheetAfterRoute = true;
            _vm.NavigationTarget = targetSpot;
            _vm.SelectedPinPOI = targetSpot;
            ClearNearFocusRoute();
            _zoneProbeVisitedPoiIds.Add(targetSpot.Id);

            CenterMapOnPoi(targetSpot, InZoneFocusZoomLevel);
            await EvaluateInZoneSpotPlaybackAsync(forceSwitch: true);
            UpdateNearFocusOverlayState();
            await ShowInZoneToastAsync(
                Ui(
                    $"Bạn đã vào vùng {Fallback(targetSpot.GetDisplayName("vi"), "khu ẩm thực")}",
                    $"You entered {Fallback(targetSpot.GetDisplayName("en"), "food area")} zone",
                    $"你已进入 {Fallback(targetSpot.GetDisplayName("zh"), "美食区")} 区域"));
        }
        catch (TaskCanceledException)
        {
            // Ignore.
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ExploreMapPage] RunSimulateInZoneAsync error: {ex}");
            await DisplayAlertAsync(
                Ui("Lỗi", "Error", "错误"),
                Ui("Không thể chạy giả lập vào vùng quán lúc này.", "Unable to run in-venue simulation right now.", "当前无法运行进入店铺范围模拟。"),
                "OK");
        }
    }

    private async Task RunSimulateNearFromFarAsync()
    {
        try
        {
            _simulateThreeStopsCts?.Cancel();
            _simulateThreeStopsCts = new CancellationTokenSource();
            var token = _simulateThreeStopsCts.Token;

            if (_vm.IsExploreStateDemoLocked)
                _vm.IsExploreStateDemoLocked = false;

            if (_vm.CurrentExploreState != MainViewModel.ExploreState.Far)
            {
                await ShowApproachingToastCompactAsync(
                    -1,
                    Ui("→ Bạn đã ở gần hoặc trong vùng quán", "→ You are already nearby or inside a venue zone", "→ 你已在附近或店铺范围内"));
                UpdateExploreFarStateAdvanceButton();
                return;
            }

            var startLat = _vm.CurrentLat != 0 ? _vm.CurrentLat : AppConfig.DefaultLatitude;
            var startLon = _vm.CurrentLon != 0 ? _vm.CurrentLon : AppConfig.DefaultLongitude;
            var targetSpot = ResolveSimulationPrimaryTarget(startLat, startLon);

            if (targetSpot == null)
            {
                await ShowApproachingToastCompactAsync(
                    -1,
                    Ui("→ Chưa có dữ liệu quán để giả lập ở gần", "→ No venue data to simulate nearby state", "→ 没有可用于模拟附近状态的店铺数据"));
                return;
            }

            var inZoneRadius = Math.Max(targetSpot.Radius, AppConfig.InZoneRadiusMeters);
            var nearTargetDistance = Math.Clamp(
                inZoneRadius + 45d,
                inZoneRadius + 20d,
                AppConfig.NearRadiusMeters - 20d);

            await ShowApproachingToastCompactAsync(
                targetSpot.Id,
                Ui(
                    $"→ Giả lập di chuyển gần {Fallback(targetSpot.GetDisplayName("vi"), "khu ẩm thực")}",
                    $"→ Simulate moving near {Fallback(targetSpot.GetDisplayName("en"), "food area")}",
                    $"→ 模拟移动到 {Fallback(targetSpot.GetDisplayName("zh"), "美食区")} 附近"));

            var nearPoint = GetPointTowardsTargetByDistance(
                startLat,
                startLon,
                targetSpot.Latitude,
                targetSpot.Longitude,
                nearTargetDistance);

            // Far -> Near is a quick state-jump simulation, not a full route-walk.
            const int quickSteps = 7;
            for (var i = 1; i <= quickSteps; i++)
            {
                token.ThrowIfCancellationRequested();
                var t = i / (double)quickSteps;
                SetVirtualLocation(
                    Lerp(startLat, nearPoint.Lat, t),
                    Lerp(startLon, nearPoint.Lon, t));
                UpdateUserPin();

                if (i % 2 == 0 || i == quickSteps)
                    await EvaluateInZoneSpotPlaybackAsync(forceSwitch: false);

                await Task.Delay(95, token);
            }

            SetVirtualLocation(nearPoint.Lat, nearPoint.Lon);
            _vm.RefreshExploreState();
            UpdateUserPin();
            UpdateZonePins();
            CenterMapForCurrentState();
            UpdateExploreFarStateAdvanceButton();
        }
        catch (TaskCanceledException)
        {
            // Ignore.
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ExploreMapPage] RunSimulateNearFromFarAsync error: {ex}");
        }
    }

    private POI? ResolveSimulationPrimaryTarget(double startLat, double startLon)
    {
        var navigableSpots = GetNavigablePois();
        if (navigableSpots.Count == 0)
            return null;

        if (_nearRouteTargetPoiId.HasValue)
        {
            var routeSpot = navigableSpots.FirstOrDefault(p => p.Id == _nearRouteTargetPoiId.Value);
            if (routeSpot != null)
                return routeSpot;
        }

        if (_vm.NavigationTarget != null)
        {
            var navSpot = navigableSpots.FirstOrDefault(p => p.Id == _vm.NavigationTarget.Id);
            if (navSpot != null)
                return navSpot;
        }

        if (_vm.HasActiveTourOverride)
            return navigableSpots[0];

        return navigableSpots
            .OrderBy(p => HaversineDistance(startLat, startLon, p.Latitude, p.Longitude))
            .FirstOrDefault();
    }

    private List<POI> ResolveTopSimulationStops(
        IReadOnlyList<POI> navigableSpots,
        double startLat,
        double startLon,
        out bool usingSelectedTour)
    {
        usingSelectedTour = _vm.HasActiveTourOverride;
        if (navigableSpots.Count == 0)
            return new List<POI>();

        if (!usingSelectedTour)
            return ResolveRandomThreeStops(navigableSpots, startLat, startLon);

        var scopeKey = ResolveSimulationScopeKey("tour");
        if (!string.Equals(scopeKey, _tourSimulationScopeKey, StringComparison.OrdinalIgnoreCase))
        {
            _tourSimulationScopeKey = scopeKey;
            _tourSimulationVisitedPoiIds.Clear();
        }

        var remainingTourStops = navigableSpots
            .Where(p => !_tourSimulationVisitedPoiIds.Contains(p.Id))
            .ToList();

        if (remainingTourStops.Count == 0)
        {
            _tourSimulationVisitedPoiIds.Clear();
            remainingTourStops = navigableSpots.ToList();
        }

        return BuildNearestFirstOrder(remainingTourStops, startLat, startLon);
    }

    private POI? ResolveNextZoneProbeTarget(
        IReadOnlyList<POI> navigableSpots,
        double startLat,
        double startLon)
    {
        if (navigableSpots.Count == 0)
            return null;

        var scopeKey = ResolveSimulationScopeKey("zone");
        if (!string.Equals(scopeKey, _zoneProbeScopeKey, StringComparison.OrdinalIgnoreCase))
        {
            _zoneProbeScopeKey = scopeKey;
            _zoneProbeVisitedPoiIds.Clear();
        }

        var next = navigableSpots
            .Where(p => !_zoneProbeVisitedPoiIds.Contains(p.Id))
            .OrderBy(p => HaversineDistance(startLat, startLon, p.Latitude, p.Longitude))
            .FirstOrDefault();

        if (next != null)
            return next;

        _zoneProbeVisitedPoiIds.Clear();
        return navigableSpots
            .OrderBy(p => HaversineDistance(startLat, startLon, p.Latitude, p.Longitude))
            .FirstOrDefault();
    }

    private string ResolveSimulationScopeKey(string mode)
    {
        if (!_vm.HasActiveTourOverride)
            return $"{mode}:default";

        var tourKey = !string.IsNullOrWhiteSpace(_vm.ActiveTourId)
            ? _vm.ActiveTourId.Trim()
            : _vm.ActiveTourName.Trim();

        if (string.IsNullOrWhiteSpace(tourKey))
            tourKey = "active-tour";

        return $"{mode}:{tourKey}";
    }

    private static List<POI> ResolveRandomThreeStops(
        IReadOnlyList<POI> navigableSpots,
        double startLat,
        double startLon)
    {
        var targetCount = Math.Min(3, navigableSpots.Count);
        if (targetCount <= 0)
            return new List<POI>();

        var nearestPool = navigableSpots
            .OrderBy(p => HaversineDistance(startLat, startLon, p.Latitude, p.Longitude))
            .Take(Math.Min(10, navigableSpots.Count))
            .ToList();

        if (nearestPool.Count < targetCount)
            nearestPool = navigableSpots.ToList();

        return nearestPool
            .OrderBy(_ => Random.Shared.Next())
            .Take(targetCount)
            .ToList();
    }

    private static List<POI> BuildNearestFirstOrder(
        IReadOnlyList<POI> candidates,
        double startLat,
        double startLon)
    {
        if (candidates.Count <= 1)
            return candidates.ToList();

        var remaining = candidates.ToList();
        var ordered = new List<POI>(remaining.Count);
        var cursorLat = startLat;
        var cursorLon = startLon;

        while (remaining.Count > 0)
        {
            var next = remaining
                .OrderBy(p => HaversineDistance(cursorLat, cursorLon, p.Latitude, p.Longitude))
                .First();

            ordered.Add(next);
            remaining.Remove(next);
            cursorLat = next.Latitude;
            cursorLon = next.Longitude;
        }

        return ordered;
    }

    private void SetVirtualLocation(double lat, double lon)
    {
        if (_vm.IsExploreStateDemoLocked)
            _vm.IsExploreStateDemoLocked = false;

        _vm.HasLocationFix = true;
        _vm.CurrentLat = lat;
        _vm.CurrentLon = lon;
        _vm.CoordDisplay = $"{lat:F6}, {lon:F6}";
        _vm.RefreshExploreState();
    }

    private static double Lerp(double from, double to, double t)
        => from + ((to - from) * t);

    private static (double Lat, double Lon) GetPointTowardsTargetByDistance(
        double startLat, double startLon,
        double targetLat, double targetLon,
        double stopDistanceMeters)
    {
        var total = HaversineDistance(startLat, startLon, targetLat, targetLon);
        if (total <= stopDistanceMeters || total <= 0.001)
            return (targetLat, targetLon);

        var ratio = (total - stopDistanceMeters) / total;
        ratio = Math.Clamp(ratio, 0.0, 1.0);
        var lat = Lerp(startLat, targetLat, ratio);
        var lon = Lerp(startLon, targetLon, ratio);
        return (lat, lon);
    }

    private async Task MoveCloserToPoiForReliableZoneAsync(
        POI poi,
        double preferredStopDistanceMeters,
        CancellationToken token)
    {
        var startLat = _vm.CurrentLat != 0 ? _vm.CurrentLat : AppConfig.DefaultLatitude;
        var startLon = _vm.CurrentLon != 0 ? _vm.CurrentLon : AppConfig.DefaultLongitude;
        var currentDistance = HaversineDistance(startLat, startLon, poi.Latitude, poi.Longitude);
        if (currentDistance <= preferredStopDistanceMeters + 0.6)
            return;

        var targetPoint = GetPointTowardsTargetByDistance(
            startLat,
            startLon,
            poi.Latitude,
            poi.Longitude,
            preferredStopDistanceMeters);

        const int finalSteps = 4;
        for (var i = 1; i <= finalSteps; i++)
        {
            token.ThrowIfCancellationRequested();
            var t = i / (double)finalSteps;
            SetVirtualLocation(
                Lerp(startLat, targetPoint.Lat, t),
                Lerp(startLon, targetPoint.Lon, t));
            UpdateUserPin();
            await EvaluateInZoneSpotPlaybackAsync(forceSwitch: false);
            await Task.Delay(75, token);
        }
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
            _vm.PlayingPoiId = null;
            _vm.IsAudioPaused = false;
            UpdatePreviewAudioUiState();
        }
    }

    private async void OnViewDetailRequested(object? sender, POI poi)
    {
        await OpenPoiDetailAsync(poi);
    }

    private async Task OpenPoiDetailAsync(POI poi)
    {
        if (poi == null)
            return;

        _vm.SelectedPinPOI = poi;
        _vm.PrimaryZone = poi;
        _vm.PrimaryZoneName = poi.GetDisplayName(_lang.CurrentLanguage);
        _vm.PrimaryZoneDesc = poi.GetDisplayDescription(_lang.CurrentLanguage);
        _vm.PrimaryZoneAddress = poi.Address ?? Ui("Đang cập nhật", "Updating", "更新中");
        _vm.PrimaryZoneRating = (poi.Rating ?? 4.5).ToString("F1");

        _isDetailPageOpen = true;
        try
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                var nav = Shell.Current?.Navigation ?? Navigation;
                if (nav != null)
                    await nav.PushModalAsync(new POIDetailPage(poi, keepCurrentAudio: true), false);
            });
        }
        finally
        {
            _isDetailPageOpen = false;
            if (_pendingPoiIdWhileDetailOpen.HasValue)
            {
                var pendingPoi = _vm.AllPOIs.FirstOrDefault(p => p.Id == _pendingPoiIdWhileDetailOpen.Value);
                _pendingPoiIdWhileDetailOpen = null;
                if (pendingPoi != null)
                    await SwitchToInZoneSpotAsync(pendingPoi, forceAudioRestart: true);
            }
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
            _vm.PlayingPoiId = null;
            _vm.IsAudioPaused = false;
            UpdatePreviewAudioUiState();
            await DisplayAlertAsync(
                Ui("Lỗi", "Error", "错误"),
                Ui($"Không thể phát nghe thử: {ex.Message}", $"Unable to play preview: {ex.Message}", $"无法播放试听：{ex.Message}"),
                "OK");
        }
    }

    private async void OnStartFromHereRequested(object? sender, POI poi)
    {
        if (poi == null)
            return;

        try
        {
            var currentState = _vm.CurrentExploreState;
            var poiName = poi.GetDisplayName(_lang.CurrentLanguage);
            if (string.IsNullOrWhiteSpace(poiName))
                poiName = Ui("địa điểm này", "this place", "这个地点");

            if (currentState == MainViewModel.ExploreState.Far)
            {
                await CustomAlert.ShowAsync(
                    Ui("Bạn đang ở xa", "You are far away", "你距离较远"),
                    Ui(
                        $"Hiện bạn vẫn đang ở xa khu ẩm thực, nên chưa thể bắt đầu trải nghiệm thật tại {poiName}. Bạn vẫn có thể xem chi tiết hoặc nghe thử trước.",
                        $"You are still far from the food area, so real experience cannot start at {poiName} yet. You can still view details or preview audio.",
                        $"你目前离美食区较远，暂时无法从 {poiName} 开始真实体验。你仍可查看详情或先试听。"),
                    Ui("Đã hiểu", "Got it", "知道了"),
                    AlertType.Info);
                return;
            }

            var shouldStart = await CustomAlert.ShowConfirmAsync(
                Ui("Bắt đầu trải nghiệm thật?", "Start real experience?", "开始真实体验？"),
                Ui(
                    $"Bạn muốn bắt đầu hành trình thực tế từ {poiName} chứ?",
                    $"Do you want to start the real route from {poiName}?",
                    $"你想从 {poiName} 开始真实路线吗？"),
                Ui("Bắt đầu", "Start", "开始"),
                Ui("Ở lại bản đồ", "Stay on map", "留在地图"),
                AlertType.Warning);

            if (!shouldStart)
                return;

            await StopPreviewAudioAsync();

            _vm.VisitedPOIIds.Add(poi.Id);
            _vm.SelectedPinPOI = poi;
            _vm.PrimaryZone = poi;
            _vm.PrimaryZoneName = poi.GetDisplayName(_lang.CurrentLanguage);
            _vm.PrimaryZoneType = poi.ZoneType ?? poi.Category ?? string.Empty;
            _vm.PrimaryZoneDesc = poi.GetDisplayDescription(_lang.CurrentLanguage);
            _vm.PrimaryZoneAddress = poi.Address ?? "Đang cập nhật";
            _vm.PrimaryZoneRating = (poi.Rating ?? 4.5).ToString("F1");
            _vm.CurrentAppMode = MainViewModel.AppMode.Explore;
            _vm.IsLegacyMapVisible = false;
            _vm.RefreshExploreState();

            if (!_isNearFocusMode &&
                (_vm.CurrentExploreState == MainViewModel.ExploreState.Near ||
                 _vm.CurrentExploreState == MainViewModel.ExploreState.InZone))
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    var nav = Shell.Current?.Navigation ?? Navigation;
                    if (nav == null)
                        return;

                    var currentPage = this;
                    await nav.PushAsync(new ExploreMapPage(_vm, nearFocusMode: true), false);
                    if (nav.NavigationStack.Contains(currentPage))
                        nav.RemovePage(currentPage);
                });
                return;
            }

            if (_vm.CurrentExploreState == MainViewModel.ExploreState.Near)
            {
                _useInZoneSheetAfterRoute = false;
                _isNearRouteActive = true;
                _nearRouteTargetPoiId = poi.Id;
                _routeDrawRequestVersion++;
                _lastRouteRedraw = DateTime.MinValue;
                _ = DrawNearFocusRouteAsync();
            }
            else if (_vm.CurrentExploreState == MainViewModel.ExploreState.InZone)
            {
                _useInZoneSheetAfterRoute = true;
                _isNearRouteActive = false;
                _nearRouteTargetPoiId = null;
                _routeDrawRequestVersion++;
                ClearNearFocusRoute();
            }

            UpdateZonePins();
            await EvaluateInZoneSpotPlaybackAsync(forceSwitch: true);
            UpdateNearFocusOverlayState();
            CenterMapForCurrentState();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ExploreMapPage] OnStartFromHereRequested: {ex}");
            await DisplayAlertAsync(
                Ui("Lỗi", "Error", "错误"),
                Ui($"Không thể bắt đầu từ quán này: {ex.Message}", $"Unable to start from this venue: {ex.Message}", $"无法从该店铺开始：{ex.Message}"),
                "OK");
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
            await DisplayAlertAsync(
                Ui("Lỗi", "Error", "错误"),
                Ui("Không thể cập nhật trạng thái yêu thích lúc này.", "Unable to update favorite status right now.", "当前无法更新收藏状态。"),
                "OK");
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
        _vm.SearchQuery = poi.GetDisplayName(_lang.CurrentLanguage);
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

    private void CenterMapOnPoi(POI poi, int? zoomLevel = null)
    {
        if (MapView?.Map == null)
            return;

        var (px, py) = SphericalMercator.FromLonLat(poi.Longitude, poi.Latitude);
        MapView.Map.Navigator.CenterOn(new MPoint(px, py));

        if (zoomLevel.HasValue)
        {
            var resolutions = MapView.Map.Navigator.Resolutions?.ToList();
            if (resolutions != null && resolutions.Count > 0)
            {
                var safeZoom = Math.Clamp(zoomLevel.Value, 0, resolutions.Count - 1);
                MapView.Map.Navigator.ZoomToLevel(safeZoom);
                return;
            }
        }

        ZoomToDefaultLevel();
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

    private void UpdateNearFocusOverlayState()
    {
        if (!_isNearFocusMode)
            return;

        UpdateNearFocusSheetState();
        UpdateNearRouteActionVisibility();
        UpdateVirtualStateAdvanceButton();

        var poi = ResolveNearFocusPoi();
        if (poi == null)
            return;

        var isInZoneState = _vm.CurrentExploreState == MainViewModel.ExploreState.InZone || _useInZoneSheetAfterRoute;
        var isNearState = _vm.CurrentExploreState == MainViewModel.ExploreState.Near && !isInZoneState;
        var isInsidePoiZone = IsUserInsidePoiActivationZone(poi);
        if (NearFocusBottomSheet != null)
            NearFocusBottomSheet.IsVisible = !isInZoneState || isInsidePoiZone || _isNearRouteActive;

        if (_vm.CurrentExploreState == MainViewModel.ExploreState.InZone && !isInsidePoiZone)
            return;

        NearFocusLikeIcon.Text = IconHeart;
        NearFocusLikeIcon.TextColor = poi.IsLikedByUser
            ? MauiColor.FromArgb("#EF4444")
            : MauiColor.FromArgb("#D8E6DF");

        var isCurrentPreview = _vm.PlayingPoiId == poi.Id && (_tts.IsPlaying() || _vm.IsAudioPaused);
        NearFocusPlayPauseIcon.Text = isCurrentPreview && _tts.IsPlaying() ? IconPause : IconPlay;
        NearFocusPlayPauseIcon.TextColor = MauiColor.FromArgb("#063014");
        NearFocusCollapsedPlayPauseIcon.Text = isCurrentPreview && _tts.IsPlaying() ? IconPause : IconPlay;
        NearFocusCollapsedPlayPauseIcon.TextColor = MauiColor.FromArgb("#063014");

        var currentDistance = ComputeDistanceFromCurrentToPoi(poi);
        var durationSeconds = EstimateWalkingSeconds(currentDistance);

        var routeMetaText = FormatDistance(currentDistance).ToUpperInvariant();

        var collapsedName = poi.GetDisplayName(_lang.CurrentLanguage);
        if (string.IsNullOrWhiteSpace(collapsedName))
            collapsedName = Ui("Địa điểm", "Place", "地点");
        if (!string.Equals(NearFocusCollapsedNameLabel.Text, collapsedName, StringComparison.Ordinal))
            NearFocusCollapsedNameLabel.Text = collapsedName;

        var collapsedImageUrl = poi.DisplayImageUrl;
        if (!string.Equals(_lastNearOverlayImageUrl, collapsedImageUrl, StringComparison.Ordinal))
        {
            NearFocusCollapsedThumb.Source = collapsedImageUrl;
            _lastNearOverlayImageUrl = collapsedImageUrl;
        }

        if (isNearState)
        {
            NearFocusDescriptionLabel.IsVisible = false;
            NearFocusTagsRow.IsVisible = false;
            NearFocusLikeButton.IsVisible = false;
            NearFocusPlayPauseButton.IsVisible = false;
            NearFocusProgressSection.IsVisible = false;
            NearNearbySection.IsVisible = false;
            NearFocusRouteStatusLabel.IsVisible = false;
            NearFocusRouteMetaLabel.IsVisible = false;
            NearFocusCollapsedMetaLabel.IsVisible = true;
            NearFocusCollapsedMetaLabel.Text = _lang.CurrentLanguage switch
            {
                "en" => $"{routeMetaText} • ~{Math.Max(1, (int)Math.Ceiling(durationSeconds / 60))} min",
                "zh" => $"{routeMetaText} • 约{Math.Max(1, (int)Math.Ceiling(durationSeconds / 60))}分钟",
                _ => $"{routeMetaText} • ~{Math.Max(1, (int)Math.Ceiling(durationSeconds / 60))} phút"
            };
        }
        else if (isInZoneState)
        {
            NearFocusDescriptionLabel.IsVisible = true;
            NearFocusTagsRow.IsVisible = true;
            NearFocusLikeButton.IsVisible = true;
            NearFocusPlayPauseButton.IsVisible = true;
            NearFocusProgressSection.IsVisible = true;
            NearNearbySection.IsVisible = false;
            NearFocusRouteStatusLabel.Text = Ui("ĐANG THUYẾT MINH", "NOW NARRATING", "正在讲解");
            NearFocusRouteMetaLabel.Text = Ui("XEM THÊM →", "SEE MORE →", "查看更多 →");
            NearFocusRouteMetaLabel.IsVisible = true;
            NearFocusCollapsedMetaLabel.IsVisible = false;

            var tags = BuildNearFocusTags(poi);
            NearFocusTag1Label.Text = tags[0];
            NearFocusTag2Label.Text = tags[1];
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

        NearFocusCollapsedHeartIcon.Text = IconHeart;
        NearFocusCollapsedHeartIcon.TextColor = poi.IsLikedByUser
            ? MauiColor.FromArgb("#EF4444")
            : MauiColor.FromArgb("#D8E6DF");
        NearFocusCollapsedHeartIcon.IsVisible = !isNearState;

        if (isInZoneState)
        {
            var poiAudioDuration = Math.Max(0, _tts.GetDuration());
            var poiAudioCurrent = Math.Max(0, _tts.GetCurrentPosition());
            if (poiAudioDuration > 0 && poiAudioCurrent > poiAudioDuration)
                poiAudioCurrent = poiAudioDuration;

            NearFocusProgressBar.Progress = poiAudioDuration > 0
                ? Math.Clamp(poiAudioCurrent / poiAudioDuration, 0, 1)
                : 0;
            NearFocusElapsedLabel.Text = FormatAudioTime(poiAudioCurrent);
            NearFocusDurationLabel.Text = FormatAudioTime(poiAudioDuration > 0 ? poiAudioDuration : 0);
        }
        else
        {
            NearFocusProgressBar.Progress = 0;
            NearFocusElapsedLabel.Text = FormatDistance(currentDistance);
            NearFocusDurationLabel.Text = _lang.CurrentLanguage switch
            {
                "en" => $"~{Math.Max(1, (int)Math.Ceiling(durationSeconds / 60))} min",
                "zh" => $"约{Math.Max(1, (int)Math.Ceiling(durationSeconds / 60))}分钟",
                _ => $"~{Math.Max(1, (int)Math.Ceiling(durationSeconds / 60))} phút"
            };
        }

        // Compact in-zone card shows audio progress/time.
        var audioDuration = Math.Max(0, _tts.GetDuration());
        var audioCurrent = Math.Max(0, _tts.GetCurrentPosition());
        if (audioDuration > 0 && audioCurrent > audioDuration)
            audioCurrent = audioDuration;

        NearFocusCollapsedProgressBar.Progress = audioDuration > 0
            ? Math.Clamp(audioCurrent / audioDuration, 0, 1)
            : 0;
        NearFocusCollapsedElapsedLabel.Text = FormatAudioTime(audioCurrent);
        NearFocusCollapsedDurationLabel.Text = FormatAudioTime(audioDuration > 0 ? audioDuration : 0);

        if (isInZoneState && !_tts.IsPlaying() && !_vm.IsAudioPaused)
        {
            // Keep compact card stable even if audio not started yet.
            NearFocusCollapsedProgressBar.Progress = 0;
            NearFocusCollapsedElapsedLabel.Text = "00:00";
        }
    }

    private void UpdateNearFocusAudioProgressUiOnly()
    {
        if (!_isNearFocusMode)
            return;

        var poi = ResolveNearFocusPoi();
        if (poi == null)
            return;

        var isInZoneState = _vm.CurrentExploreState == MainViewModel.ExploreState.InZone || _useInZoneSheetAfterRoute;
        if (!isInZoneState)
            return;

        if (_vm.CurrentExploreState == MainViewModel.ExploreState.InZone && !IsUserInsidePoiActivationZone(poi))
            return;

        var poiAudioDuration = Math.Max(0, _tts.GetDuration());
        var poiAudioCurrent = Math.Max(0, _tts.GetCurrentPosition());
        if (poiAudioDuration > 0 && poiAudioCurrent > poiAudioDuration)
            poiAudioCurrent = poiAudioDuration;

        NearFocusProgressBar.Progress = poiAudioDuration > 0
            ? Math.Clamp(poiAudioCurrent / poiAudioDuration, 0, 1)
            : 0;
        NearFocusElapsedLabel.Text = FormatAudioTime(poiAudioCurrent);
        NearFocusDurationLabel.Text = FormatAudioTime(poiAudioDuration > 0 ? poiAudioDuration : 0);

        NearFocusCollapsedProgressBar.Progress = poiAudioDuration > 0
            ? Math.Clamp(poiAudioCurrent / poiAudioDuration, 0, 1)
            : 0;
        NearFocusCollapsedElapsedLabel.Text = FormatAudioTime(poiAudioCurrent);
        NearFocusCollapsedDurationLabel.Text = FormatAudioTime(poiAudioDuration > 0 ? poiAudioDuration : 0);
    }

    private void UpdateVirtualStateAdvanceButton()
    {
        if (VirtualStateAdvanceButton == null || VirtualStateAdvanceLabel == null)
            return;

        var isFarState = _vm.CurrentExploreState == MainViewModel.ExploreState.Far;
        var isNearState = _vm.CurrentExploreState == MainViewModel.ExploreState.Near;
        VirtualStateAdvanceButton.IsVisible = _isNearFocusMode && (isFarState || isNearState);
        VirtualStateAdvanceLabel.Text = isFarState
            ? Ui("Gần", "Approach", "靠近")
            : Ui("Vào", "Enter", "进入");
    }

    private void UpdateExploreFarStateAdvanceButton()
    {
        if (ExploreFarStateAdvanceButton == null || ExploreFarStateAdvanceLabel == null)
            return;

        var isFarState = _vm.CurrentExploreState == MainViewModel.ExploreState.Far;
        ExploreFarStateAdvanceButton.IsVisible = !_isNearFocusMode && isFarState;
        ExploreFarStateAdvanceLabel.Text = Ui("Gần", "Approach", "靠近");
    }

    private bool IsUserInsidePoiActivationZone(POI poi)
    {
        if (_vm.CurrentLat == 0 && _vm.CurrentLon == 0)
            return false;

        var spots = _vm.AllPOIs.Where(p => p.ZoneType == "Spot").ToList();
        if (spots.Count == 0)
            return false;

        var distance = HaversineDistance(_vm.CurrentLat, _vm.CurrentLon, poi.Latitude, poi.Longitude);
        var radius = GetDynamicSpotActivationRadius(poi, spots);
        return distance <= radius;
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
        nameLabel.Text = string.IsNullOrWhiteSpace(poi.DisplayName)
            ? "Địa điểm gần"
            : poi.DisplayName;

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
        if (_vm.HasActiveTourOverride)
        {
            var activeTourSpots = _vm.GetRuntimeSpotPool()
                .Where(p => p.ZoneType == "Spot")
                .ToList();

            if (activeTourSpots.Count > 0)
                return activeTourSpots;
        }

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
            QueueLocationDrivenInZoneEvaluation();
            UpdateNearFocusOverlayThrottled();

            if (_isNearRouteActive && _nearRouteTargetPoiId.HasValue)
            {
                var routePoi = _vm.AllPOIs.FirstOrDefault(p => p.Id == _nearRouteTargetPoiId.Value);
                if (routePoi != null)
                {
                    EvaluateNearRouteCompletion(routePoi);
                    var now = DateTime.UtcNow;
                    if ((now - _lastRouteRedraw).TotalMilliseconds >= 1500)
                    {
                        _lastRouteRedraw = now;
                        _ = DrawNearFocusRouteAsync();
                    }
                }
            }
        }
        else if (e.PropertyName == nameof(MainViewModel.ActiveZoneCount) ||
                 e.PropertyName == nameof(MainViewModel.SelectedPinPOI))
        {
            UpdateZonePins();
            TabMapComponent.RefreshLikeIcon();
            if (_isNearFocusMode)
            {
                UpdateNearFocusOverlayState();
                if (_isNearRouteActive)
                    _ = DrawNearFocusRouteAsync();
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
            UpdateExploreFarStateAdvanceButton();
            var previousState = _previousExploreState;
            var currentState = _vm.CurrentExploreState;

            if (_isNearFocusMode)
            {
                if (currentState == MainViewModel.ExploreState.Near &&
                    previousState != MainViewModel.ExploreState.Near &&
                    !_isNearRouteActive)
                {
                    var nearPoi = ResolveNearFocusPoi();
                    if (nearPoi != null)
                    {
                        _useInZoneSheetAfterRoute = false;
                        _isNearRouteActive = true;
                        _nearRouteTargetPoiId = nearPoi.Id;
                        _routeDrawRequestVersion++;
                        _vm.NavigationTarget = nearPoi;
                        _vm.SelectedPinPOI = nearPoi;
                        _vm.VisitedPOIIds.Add(nearPoi.Id);
                        _lastRouteRedraw = DateTime.MinValue;
                        _ = DrawNearFocusRouteAsync();
                    }
                }

                if (currentState == MainViewModel.ExploreState.Far)
                    _useInZoneSheetAfterRoute = false;
                UpdateNearFocusOverlayState();
                UpdateZonePins();
                _ = EvaluateInZoneSpotPlaybackAsync(forceSwitch: true);
                _ = HandleNearOrInZoneToFarTransitionAsync(previousState, currentState);
            }

            TryShowExploreStateTransition(previousState, currentState);
        }
    }

    private void TryShowExploreStateTransition(
        MainViewModel.ExploreState previousState,
        MainViewModel.ExploreState currentState)
    {
        if (currentState == MainViewModel.ExploreState.Near &&
            previousState == MainViewModel.ExploreState.Far)
        {
            if (_isNearFocusMode)
            {
                _ = ShowInZoneToastAsync(
                    Ui(
                        "Bạn đã vào vùng gần khu ẩm thực",
                        "You entered the nearby food area",
                        "你已进入美食区附近"));
            }
            else
            {
                _ = ShowNearEntryAlertAsync();
            }
        }

        _previousExploreState = currentState;
    }

    private async Task ShowNearEntryAlertAsync()
    {
        if (_isNearEntryNoticeShowing)
            return;

        if ((DateTime.UtcNow - _lastNearEntryNoticeUtc).TotalMilliseconds < NearEntryNoticeCooldownMs)
            return;

        try
        {
            _isNearEntryNoticeShowing = true;
            _lastNearEntryNoticeUtc = DateTime.UtcNow;
            await MainThread.InvokeOnMainThreadAsync(() =>
                CustomAlert.ShowAsync(
                    Ui("Bạn đã vào vùng gần", "You entered nearby zone", "你已进入附近区域"),
                    Ui(
                        "Bạn đang ở trạng thái Near. Có thể bắt đầu dẫn đường tới quán.",
                        "You are now in Near state. You can start navigation to a venue.",
                        "你当前处于 Near 状态，可开始导航到店铺。"),
                    Ui("Đã hiểu", "Got it", "知道了"),
                    AlertType.Info));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ExploreMapPage] ShowNearEntryAlertAsync: {ex.Message}");
        }
        finally
        {
            _isNearEntryNoticeShowing = false;
        }
    }

    private async Task HandleNearOrInZoneToFarTransitionAsync(
        MainViewModel.ExploreState previousState,
        MainViewModel.ExploreState currentState)
    {
        if (_isHandlingFarTransition)
            return;

        if (!_isNearFocusMode || !IsVisible)
            return;

        if (currentState != MainViewModel.ExploreState.Far)
            return;

        if (previousState != MainViewModel.ExploreState.Near &&
            previousState != MainViewModel.ExploreState.InZone)
            return;

        try
        {
            _isHandlingFarTransition = true;
            await MainThread.InvokeOnMainThreadAsync(() =>
                CustomAlert.ShowAsync(
                    Ui("Bạn đã ra xa khu ẩm thực", "You moved away from the food area", "你已离开美食区"),
                    Ui("Ứng dụng chuyển sang chế độ Xem ảo để tiếp tục trải nghiệm.", "The app switched to Virtual View mode so you can continue the experience.", "应用已切换到虚拟浏览模式以继续体验。"),
                    Ui("Đã hiểu", "Got it", "知道了"),
                    AlertType.Info));

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                _vm.CurrentAppMode = MainViewModel.AppMode.Virtual;
                _vm.IsLegacyMapVisible = false;
                await ClosePageAsync(preservePreviewAudio: true);
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ExploreMapPage] HandleNearOrInZoneToFarTransitionAsync: {ex}");
        }
        finally
        {
            _isHandlingFarTransition = false;
        }
    }

    private async Task ShowInZoneToastAsync(string message)
    {
        if (InZoneToast == null || InZoneToastLabel == null)
            return;

        _inZoneToastCts?.Cancel();
        _inZoneToastCts = new CancellationTokenSource();
        var token = _inZoneToastCts.Token;
        _approachingToastPoiId = null;

        try
        {
            InZoneToastLabel.Text = message;
            InZoneToast.Opacity = 0;
            InZoneToast.IsVisible = true;
            await InZoneToast.FadeToAsync(1, 180);
            await Task.Delay(1400, token);
            if (token.IsCancellationRequested)
                return;

            await InZoneToast.FadeToAsync(0, 220);
            InZoneToast.IsVisible = false;
        }
        catch (TaskCanceledException)
        {
            // Ignore cancellation
        }
        finally
        {
            if (!token.IsCancellationRequested && InZoneToast.Opacity <= 0.01)
                InZoneToast.IsVisible = false;
        }
    }

    private void OnPreviewPlaybackEnded()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _vm.PlayingPoiId = null;
            _vm.IsAudioPaused = false;
            UpdatePreviewAudioUiState();
        });
    }

    private void UpdatePreviewAudioUiState()
    {
        TabMapComponent.SetPreviewAudioState(_vm.PlayingPoiId, _tts.IsPlaying(), _vm.IsAudioPaused);
        UpdateNearFocusOverlayState();
    }

    private async Task SwitchPreviewToPoiIfNeededAsync(POI poi)
    {
        try
        {
            if (_vm.PlayingPoiId == null || !_tts.IsPlaying())
                return;

            if (_vm.PlayingPoiId == poi.Id)
            {
                UpdatePreviewAudioUiState();
                return;
            }

            await PlayPreviewAudioAsync(poi, allowToggleCurrent: false, showErrorAlert: false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ExploreMapPage] SwitchPreviewToPoiIfNeededAsync: {ex}");
            _vm.PlayingPoiId = null;
            _vm.IsAudioPaused = false;
            UpdatePreviewAudioUiState();
        }
    }

    private async Task<bool> PlayPreviewAudioAsync(POI poi, bool allowToggleCurrent, bool showErrorAlert)
    {
        await _previewAudioLock.WaitAsync();
        try
        {
            var switchingToDifferentPoi = _vm.PlayingPoiId.HasValue && _vm.PlayingPoiId.Value != poi.Id;
            if (switchingToDifferentPoi && (_tts.IsPlaying() || _vm.IsAudioPaused))
                await StopPreviewAudioAsync();

            if (_vm.PlayingPoiId == poi.Id)
            {
                if (_tts.IsPlaying())
                {
                    if (allowToggleCurrent)
                    {
                        _tts.Pause();
                        _vm.IsAudioPaused = true;
                    }
                    else
                    {
                        _vm.IsAudioPaused = false;
                    }

                    UpdatePreviewAudioUiState();
                    return true;
                }

                if (_vm.IsAudioPaused)
                {
                    _tts.Resume();
                    if (_tts.IsPlaying())
                    {
                        _vm.IsAudioPaused = false;
                        UpdatePreviewAudioUiState();
                        return true;
                    }
                }
            }

            await StopPreviewAudioAsync();

            if (_isNearFocusMode &&
                _vm.CurrentExploreState == MainViewModel.ExploreState.InZone &&
                !await CanPlayPoiAudioWithoutTtsFallbackAsync(poi))
            {
                if (showErrorAlert)
                    await DisplayAlertAsync(
                        Ui("Âm thanh", "Audio", "音频"),
                        Ui("Điểm này chưa có audio xuất bản để phát.", "This place has no published audio to play.", "该地点暂无可播放的已发布音频。"),
                        "OK");
                return false;
            }

            var ok = await _tts.SpeakAsync(
                ResolvePreviewNarrationText(poi),
                ResolvePreviewLanguageCode(),
                poiId: poi.Id);

            _vm.PlayingPoiId = ok ? poi.Id : null;
            _vm.IsAudioPaused = false;
            UpdatePreviewAudioUiState();

            if (!ok && showErrorAlert)
                await DisplayAlertAsync(
                    Ui("Âm thanh", "Audio", "音频"),
                    Ui("Không thể phát nghe thử lúc này.", "Unable to play preview right now.", "当前无法播放试听。"),
                    "OK");

            return ok;
        }
        finally
        {
            _previewAudioLock.Release();
        }
    }

    private async Task<bool> CanPlayPoiAudioWithoutTtsFallbackAsync(POI poi)
    {
        if (_audioCache == null)
            return false;

        var language = ResolvePreviewLanguageCode();
        if (_audioCache.IsCached(poi.Id, language))
            return true;

        if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
            return false;

        var url = await _audioCache.GetAudioUrlAsync(poi.Id, language);
        return !string.IsNullOrWhiteSpace(url);
    }

    private void StartNearFocusAudioTimer()
    {
        if (_nearFocusAudioTimer != null || Dispatcher == null)
            return;

        _nearFocusAudioTimer = Dispatcher.CreateTimer();
        _nearFocusAudioTimer.Interval = TimeSpan.FromMilliseconds(500);
        _nearFocusAudioTimer.Tick += (_, _) =>
        {
            if (!_isNearFocusMode || !IsVisible)
                return;

            if (_vm.CurrentExploreState == MainViewModel.ExploreState.InZone &&
                (_tts.IsPlaying() || _vm.IsAudioPaused))
            {
                UpdateNearFocusAudioProgressUiOnly();
            }
        };
        _nearFocusAudioTimer.Start();
    }

    private void StopNearFocusAudioTimer()
    {
        if (_nearFocusAudioTimer == null)
            return;

        _nearFocusAudioTimer.Stop();
        _nearFocusAudioTimer = null;
    }

    private string ResolvePreviewLanguageCode()
        => _lang.CurrentLanguage switch
        {
            "en" => "en-US",
            "zh" => "zh-CN",
            _ => "vi-VN"
        };

    private string ResolvePreviewNarrationText(POI poi)
    {
        var text = poi.GetDisplayDescription(_lang.CurrentLanguage);
        if (string.IsNullOrWhiteSpace(text))
            text = poi.GetDisplayName(_lang.CurrentLanguage);
        if (string.IsNullOrWhiteSpace(text))
            text = Ui("Chào mừng đến với điểm tham quan.", "Welcome to this point of interest.", "欢迎来到该景点。");
        return text;
    }

    private void OnNearRouteContinueClicked(object? sender, EventArgs e)
    {
        if (_isNearRouteActive)
        {
            CancelNearRouteTracking(keepNearState: true);
            UpdateNearFocusOverlayState();
            UpdateZonePins();
            return;
        }

        var poi = ResolveNearFocusPoi();
        if (poi == null)
            return;

        StartNearRouteToPoi(poi);
        UpdateNearFocusOverlayState();
        CenterMapForNearFocus();
    }

    private void OnNearRouteCancelClicked(object? sender, EventArgs e)
    {
        var currentPoi = ResolveNearFocusPoi();
        var nextPoi = ResolveNextPoiAfterSkip(currentPoi?.Id);
        if (nextPoi == null)
        {
            _ = ShowInZoneToastAsync(
                Ui(
                    "Không còn quán nào khác trong tour để bỏ qua.",
                    "No more stops left in this tour to skip to.",
                    "当前行程中没有可跳过到的下一站。"));
            return;
        }

        StartNearRouteToPoi(nextPoi);
        UpdateNearFocusOverlayState();
        UpdateZonePins();
        _ = ShowApproachingToastCompactAsync(
            nextPoi.Id,
            Ui(
                $"→ Đã bỏ qua, chuyển sang {Fallback(nextPoi.GetDisplayName("vi"), "quán kế tiếp")}",
                $"→ Skipped. Heading to {Fallback(nextPoi.GetDisplayName("en"), "next stop")}",
                $"→ 已跳过，前往 {Fallback(nextPoi.GetDisplayName("zh"), "下一站")}"));
    }

    private void StartNearRouteToPoi(POI poi)
    {
        _useInZoneSheetAfterRoute = false;
        _isNearRouteActive = true;
        _nearRouteTargetPoiId = poi.Id;
        _routeDrawRequestVersion++;
        _vm.NavigationTarget = poi;
        _vm.SelectedPinPOI = poi;
        _vm.VisitedPOIIds.Add(poi.Id);
        _lastRouteRedraw = DateTime.MinValue;
        _ = DrawNearFocusRouteAsync();
    }

    private POI? ResolveNextPoiAfterSkip(int? currentPoiId)
    {
        var candidates = GetNavigablePois();
        if (candidates.Count <= 1)
            return null;

        var currentId = currentPoiId ?? _nearRouteTargetPoiId;
        if (!currentId.HasValue)
            return candidates[0];

        var currentIndex = candidates.FindIndex(p => p.Id == currentId.Value);
        if (currentIndex < 0)
            return candidates[0];

        for (var i = 1; i < candidates.Count; i++)
        {
            var next = candidates[(currentIndex + i) % candidates.Count];
            if (next.Id != currentId.Value)
                return next;
        }

        return null;
    }

    private void CancelNearRouteTracking(bool keepNearState = false)
    {
        var cancelledPoiId = _nearRouteTargetPoiId;
        _isNearRouteActive = false;
        _nearRouteTargetPoiId = null;
        _routeDrawRequestVersion++;
        _useInZoneSheetAfterRoute = !keepNearState;

        if (cancelledPoiId.HasValue && _vm.NavigationTarget?.Id == cancelledPoiId.Value)
            _vm.NavigationTarget = null;

        ClearNearFocusRoute();
    }

    private async void OnInZoneStopTourClicked(object? sender, EventArgs e)
    {
        await StopActiveTourOverrideAsync();
    }

    private async void OnInZoneStopTourTapped(object? sender, TappedEventArgs e)
    {
        await StopActiveTourOverrideAsync();
    }

    private async Task StopActiveTourOverrideAsync()
    {
        try
        {
            if (!_vm.HasActiveTourOverride)
            {
                await ShowInZoneToastAsync(
                    Ui("Không có tour đang chạy để dừng.", "No active tour to stop.", "当前没有正在进行的路线可停止。"));
                return;
            }

            var activeTourName = string.IsNullOrWhiteSpace(_vm.ActiveTourName)
                ? Ui("tour hiện tại", "current tour", "当前路线")
                : _vm.ActiveTourName;

            var shouldStop = await DisplayAlertAsync(
                Ui("Dừng tour hiện tại?", "Stop current tour?", "停止当前路线？"),
                Ui(
                    $"Bạn muốn dừng \"{activeTourName}\" và quay về tuyến tự động?",
                    $"Do you want to stop \"{activeTourName}\" and return to auto route?",
                    $"你要停止“{activeTourName}”并返回自动路线吗？"),
                Ui("Dừng tour", "Stop", "停止"),
                Ui("Hủy", "Cancel", "取消"));

            if (!shouldStop)
                return;

            _isNearRouteActive = false;
            _nearRouteTargetPoiId = null;
            _routeDrawRequestVersion++;
            _useInZoneSheetAfterRoute = false;
            _vm.NavigationTarget = null;
            _vm.RequestedTourStops = null;
            _vm.ClearTourOverride();

            ClearNearFocusRoute();
            UpdateZonePins();
            await EvaluateInZoneSpotPlaybackAsync(forceSwitch: true);
            UpdateNearFocusOverlayState();
            CenterMapForCurrentState();

            await ShowInZoneToastAsync(
                Ui("Đã dừng tour hiện tại.", "Current tour stopped.", "当前路线已停止。"));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ExploreMapPage] StopActiveTourOverrideAsync error: {ex}");
            await DisplayAlertAsync(
                Ui("Lỗi", "Error", "错误"),
                Ui("Không thể dừng tour lúc này.", "Unable to stop the tour right now.", "当前无法停止路线。"),
                "OK");
        }
    }

    private void EvaluateNearRouteCompletion(POI routePoi)
    {
        var distance = ComputeDistanceFromCurrentToPoi(routePoi);
        if (distance > NearRouteArrivalMeters)
            return;

        _isNearRouteActive = false;
        _nearRouteTargetPoiId = null;
        _routeDrawRequestVersion++;
        _useInZoneSheetAfterRoute = true;
        _vm.NavigationTarget = routePoi;
        _vm.SelectedPinPOI = routePoi;
        ClearNearFocusRoute();
        UpdateNearFocusOverlayState();
        _ = ShowInZoneToastAsync(
            Ui(
                $"Bạn đã đến {Fallback(routePoi.GetDisplayName("vi"), "điểm đến")}",
                $"You have arrived at {Fallback(routePoi.GetDisplayName("en"), "destination")}",
                $"你已到达 {Fallback(routePoi.GetDisplayName("zh"), "目的地")}"));
    }

    private static string Fallback(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value;

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

    private static T? ResolveOptionalService<T>() where T : class
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
                return provider.GetService<T>();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        return null;
    }
}


