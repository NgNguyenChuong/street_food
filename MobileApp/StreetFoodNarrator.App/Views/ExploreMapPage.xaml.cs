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

    private bool _isNearSheetCollapsed;
    private MainViewModel.ExploreState _lastNearFocusState = MainViewModel.ExploreState.Far;
    private MainViewModel.ExploreState _previousExploreState = MainViewModel.ExploreState.Far;
    private CancellationTokenSource? _inZoneToastCts;
    private CancellationTokenSource? _approachingToastCts;
    private const int DefaultMapZoomLevel = (int)AppConfig.DefaultZoom + 1;
    private const int NearFocusZoomLevel = 18;
    private const int InZoneFocusZoomLevel = 19;
    private const double NearFocusMidpointMeters = 800;
    private const double NearRouteArrivalMeters = 25;
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

        HookEvents();
        if (_vm.Categories.Count == 0)
            _vm.BuildMapCategories();
        EnsureMapInitialized();
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
        var cacheDb = Path.Combine(FileSystem.AppDataDirectory, "map_cache", "tiles.db");
        var isOffline = Connectivity.Current.NetworkAccess != NetworkAccess.Internet &&
                        Connectivity.Current.NetworkAccess != NetworkAccess.ConstrainedInternet;

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
            isOffline = true;
        }

        var offlineNoCache = isOffline && !HasUsableTileCache(cacheDb);
        if (offlineNoCache)
            MapView.Map.Layers.Add(CreateOfflineFallbackLayer());

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
            MapView.RefreshGraphics();
        });
    }

    private void UpdateUserPin()
    {
        if (!_isMapInitialized || _userPinLayer == null || MapView?.Map == null)
            return;

        if (_vm.CurrentLat == 0)
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

        if (_isFirstLocation)
        {
            CenterMapForCurrentState();
            _isFirstLocation = false;
        }

        MapView.RefreshGraphics();
    }

    private double GetSpotActivationRadius(POI poi)
    {
        var rawRadius = poi.Radius > 0 ? poi.Radius : 15;
        return Math.Clamp(rawRadius, AppConfig.SpotZoneMinMeters, AppConfig.SpotZoneMaxMeters);
    }

    private void UpdateSpotZoneOverlay(POI? poi)
    {
        if (_spotZoneLayer == null)
            return;

        if (poi == null)
        {
            _spotZoneLayer.Features = Array.Empty<IFeature>();
            _spotZoneLayer.DataHasChanged();
            MapView?.RefreshGraphics();
            return;
        }

        var radiusMeters = GetSpotActivationRadius(poi);
        var feature = BuildCircleFeature(poi.Latitude, poi.Longitude, radiusMeters);
        feature.Styles.Add(new VectorStyle
        {
            Fill = new MapsBrush(new MapsColor(74, 222, 128, 38)),
            Outline = new Pen(new MapsColor(74, 222, 128, 220), 2f)
        });

        _spotZoneLayer.Features = new[] { feature };
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

        var step = 0.0015;
        for (var lat = latMin; lat <= latMax; lat += step)
        {
            var (x1, y1) = SphericalMercator.FromLonLat(lonMin, lat);
            var (x2, y2) = SphericalMercator.FromLonLat(lonMax, lat);
            features.Add(new GeometryFeature
            {
                Geometry = new LineString(new[] { new Coordinate(x1, y1), new Coordinate(x2, y2) }),
                Styles = new[] { new VectorStyle { Line = new Pen(new MapsColor(120, 138, 128, 90), 1f) } }
            });
        }

        for (var lon = lonMin; lon <= lonMax; lon += step)
        {
            var (x1, y1) = SphericalMercator.FromLonLat(lon, latMin);
            var (x2, y2) = SphericalMercator.FromLonLat(lon, latMax);
            features.Add(new GeometryFeature
            {
                Geometry = new LineString(new[] { new Coordinate(x1, y1), new Coordinate(x2, y2) }),
                Styles = new[] { new VectorStyle { Line = new Pen(new MapsColor(120, 138, 128, 90), 1f) } }
            });
        }

        layer.Features = features;
        return layer;
    }

    private async Task DrawNearFocusRouteAsync()
    {
        if (!_isNearFocusMode || !_isNearRouteActive || !_nearRouteTargetPoiId.HasValue)
        {
            ClearNearFocusRoute();
            return;
        }

        if (!_isMapInitialized || MapView?.Map == null || _routeLayer == null)
            return;

        var target = _vm.AllPOIs.FirstOrDefault(p => p.Id == _nearRouteTargetPoiId.Value);
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

    private async Task SwitchToInZoneSpotAsync(POI poi, bool forceAudioRestart)
    {
        var switchingToDifferentPoi = _vm.PlayingPoiId.HasValue && _vm.PlayingPoiId.Value != poi.Id;

        _vm.NavigationTarget = poi;
        _vm.SelectedPinPOI = poi;
        _vm.PrimaryZone = poi;
        _vm.PrimaryZoneName = poi.Name_Vi ?? poi.Name_En ?? "—";
        _vm.PrimaryZoneType = poi.ZoneType ?? poi.Category ?? string.Empty;
        _vm.PrimaryZoneDesc = poi.Description_Vi ?? poi.Description_En ?? string.Empty;
        _vm.PrimaryZoneAddress = poi.Address ?? "Đang cập nhật";
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
            currentEntry.Distance <= (currentEntry.Radius * AppConfig.SpotZoneDeactivationBuffer))
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
        var nearestDistance = allSpots
            .Where(p => p.Id != targetPoi.Id)
            .Select(p => HaversineDistance(targetPoi.Latitude, targetPoi.Longitude, p.Latitude, p.Longitude))
            .DefaultIfEmpty(double.MaxValue)
            .Min();

        if (double.IsInfinity(nearestDistance) || nearestDistance == double.MaxValue)
            return Math.Clamp(GetSpotActivationRadius(targetPoi), AppConfig.SpotZoneMinMeters, AppConfig.SpotZoneMaxMeters);

        var dynamicRadius = (nearestDistance / 2.0) - AppConfig.SpotZoneGpsErrorBufferMeters;
        return Math.Clamp(dynamicRadius, AppConfig.SpotZoneMinMeters, AppConfig.SpotZoneMaxMeters);
    }

    private void ShowApproachingNextPoiToastIfNeeded(POI poi, double distanceMeters)
    {
        var tooSoon = _lastApproachingToastPoiId == poi.Id &&
                      (DateTime.UtcNow - _lastApproachingToastUtc).TotalMilliseconds < AppConfig.SpotZoneApproachToastCooldownMs;
        if (tooSoon)
            return;

        _lastApproachingToastPoiId = poi.Id;
        _lastApproachingToastUtc = DateTime.UtcNow;

        var poiName = poi.Name_Vi ?? poi.Name_En ?? "điểm tiếp theo";
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
        var poi = ResolveNearFocusPoi();
        if (poi == null)
            return;

        OnViewDetailRequested(sender, poi);
        await Task.CompletedTask;
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
        NearFocusSheetToggleLabel.Text = _isNearSheetCollapsed ? "Mở rộng" : "Thu gọn";
        _lastNearFocusState = _vm.CurrentExploreState;
    }

    private void UpdateNearRouteActionVisibility()
    {
        var isNearState = _isNearFocusMode &&
                          _vm.CurrentExploreState == MainViewModel.ExploreState.Near;
        var isInZoneState = _isNearFocusMode &&
                            _vm.CurrentExploreState == MainViewModel.ExploreState.InZone;
        var shouldShowRouteActions = isNearState || (isInZoneState && _isNearRouteActive);
        var isCollapsed = _isNearSheetCollapsed;

        NearFocusCollapsedInZoneActions.IsVisible = isInZoneState && !shouldShowRouteActions;
        NearFocusCollapsedProgressSection.IsVisible = isInZoneState;
        NearRouteCollapsedActions.IsVisible = shouldShowRouteActions && isCollapsed;

        NearFocusRouteStatusLabel.IsVisible = isNearState;
        NearFocusRouteMetaLabel.IsVisible = false;

        NearRouteContinueButton.IsVisible = shouldShowRouteActions;
        NearRouteCancelButton.IsVisible = shouldShowRouteActions;

        var continueText = _isNearRouteActive ? "Tiếp tục chỉ đường" : "Bắt đầu chỉ đường";
        NearRouteContinueButton.Text = continueText;
        NearRouteCollapsedContinueButton.Text = continueText;
    }

    private async Task ClosePageAsync(bool preservePreviewAudio = false)
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
                await Navigation.PopModalAsync();
            else
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

    private void OnNearFocusCenterUserTapped(object? sender, TappedEventArgs e)
    {
        EnsureMapInitialized();
        CenterMapForCurrentState();
    }

    private async void OnNearFocusSimulateThreeStopsTapped(object? sender, TappedEventArgs e)
    {
        await RunSimulateThreeStopsAsync();
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

            var spots = _vm.AllPOIs
                .Where(p => p.ZoneType == "Spot")
                .OrderBy(p => HaversineDistance(startLat, startLon, p.Latitude, p.Longitude))
                .Take(3)
                .ToList();

            if (spots.Count == 0)
            {
                await ShowInZoneToastAsync("Chưa có dữ liệu quán để giả lập.");
                return;
            }

            await ShowApproachingToastCompactAsync(spots[0].Id, $"→ Bắt đầu giả lập {spots.Count} quán");

            foreach (var spot in spots)
            {
                token.ThrowIfCancellationRequested();
                var spotName = spot.Name_Vi ?? spot.Name_En ?? "quán tiếp theo";
                await ShowApproachingToastCompactAsync(spot.Id, $"→ Tiến tới {spotName}");

                var currentLat = _vm.CurrentLat != 0 ? _vm.CurrentLat : AppConfig.DefaultLatitude;
                var currentLon = _vm.CurrentLon != 0 ? _vm.CurrentLon : AppConfig.DefaultLongitude;
                var spotsForRadius = _vm.AllPOIs.Where(p => p.ZoneType == "Spot").ToList();
                var zoneRadius = GetDynamicSpotActivationRadius(spot, spotsForRadius);

                var edgePoint = GetPointTowardsTargetByDistance(
                    currentLat, currentLon,
                    spot.Latitude, spot.Longitude,
                    Math.Max(2.5, zoneRadius * 0.65));

                const int steps = 12;
                for (var i = 1; i <= steps; i++)
                {
                    token.ThrowIfCancellationRequested();

                    var t = i / (double)steps;
                    var nextLat = Lerp(currentLat, edgePoint.Lat, t);
                    var nextLon = Lerp(currentLon, edgePoint.Lon, t);
                    SetVirtualLocation(nextLat, nextLon);
                    UpdateUserPin();

                    await EvaluateInZoneSpotPlaybackAsync(forceSwitch: false);
                    await Task.Delay(280, token);
                }

                SetVirtualLocation(edgePoint.Lat, edgePoint.Lon);
                UpdateUserPin();
                CenterMapOnPoi(spot, InZoneFocusZoomLevel);

                // Hold at this spot for 4s so debounce can confirm and switch UI/audio reliably.
                var holdUntil = DateTime.UtcNow.AddMilliseconds(4000);
                while (DateTime.UtcNow < holdUntil)
                {
                    token.ThrowIfCancellationRequested();
                    await EvaluateInZoneSpotPlaybackAsync(forceSwitch: false);
                    await Task.Delay(250, token);
                }

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
            await DisplayAlertAsync("Lỗi", "Không thể chạy giả lập 3 quán lúc này.", "OK");
        }
    }

    private void SetVirtualLocation(double lat, double lon)
    {
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
        if (poi == null)
            return;

        _vm.SelectedPinPOI = poi;
        _vm.PrimaryZone = poi;
        _vm.PrimaryZoneName = poi.Name_Vi ?? poi.Name_En ?? "—";
        _vm.PrimaryZoneDesc = poi.Description_Vi ?? poi.Description_En ?? "";
        _vm.PrimaryZoneAddress = poi.Address ?? "Đang cập nhật";
        _vm.PrimaryZoneRating = (poi.Rating ?? 4.5).ToString("F1");

        _isDetailPageOpen = true;
        try
        {
            await Shell.Current.Navigation.PushModalAsync(new POIDetailPage(poi));
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

        var poi = ResolveNearFocusPoi();
        if (poi == null)
            return;

        var isNearState = _vm.CurrentExploreState == MainViewModel.ExploreState.Near;
        var isInZoneState = _vm.CurrentExploreState == MainViewModel.ExploreState.InZone;
        var isInsidePoiZone = IsUserInsidePoiActivationZone(poi);
        if (NearFocusBottomSheet != null)
            NearFocusBottomSheet.IsVisible = !isInZoneState || isInsidePoiZone || _isNearRouteActive;

        if (isInZoneState && !isInsidePoiZone)
            return;

        NearFocusLikeIcon.Text = IconHeart;
        NearFocusLikeIcon.TextColor = poi.IsLikedByUser
            ? MauiColor.FromArgb("#EF4444")
            : MauiColor.FromArgb("#D8E6DF");

        var isCurrentPreview = _vm.PlayingPoiId == poi.Id && (_tts.IsPlaying() || _vm.IsAudioPaused);
        NearFocusPlayPauseIcon.Text = isCurrentPreview && _tts.IsPlaying() ? IconPause : IconPlay;
        NearFocusPlayPauseIcon.TextColor = MauiColor.FromArgb("#063014");

        var currentDistance = ComputeDistanceFromCurrentToPoi(poi);
        var durationSeconds = EstimateWalkingSeconds(currentDistance);

        var routeMetaText = FormatDistance(currentDistance).ToUpperInvariant();

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

        NearFocusCollapsedNameLabel.Text = poi.Name_Vi ?? poi.Name_En ?? "Địa điểm";
        NearFocusCollapsedHeartIcon.Text = IconHeart;
        NearFocusCollapsedHeartIcon.TextColor = poi.IsLikedByUser
            ? MauiColor.FromArgb("#EF4444")
            : MauiColor.FromArgb("#D8E6DF");

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
            NearFocusDurationLabel.Text = $"~{Math.Max(1, (int)Math.Ceiling(durationSeconds / 60))} phút";
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
            _ = EvaluateInZoneSpotPlaybackAsync();
            if (_isNearFocusMode)
            {
                UpdateNearFocusOverlayState();
            }

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
            if (_isNearFocusMode)
            {
                UpdateNearFocusOverlayState();
                TryShowInZoneToastTransition();
                _ = EvaluateInZoneSpotPlaybackAsync(forceSwitch: true);
            }
        }
    }

    private void TryShowInZoneToastTransition()
    {
        // UX requirement: do not show transition toast between Near <-> InZone.
        // Keep only destination arrival toast when route tracking completes.
        _previousExploreState = _vm.CurrentExploreState;
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
                    await DisplayAlertAsync("Âm thanh", "Điểm này chưa có audio xuất bản để phát.", "OK");
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
                await DisplayAlertAsync("Âm thanh", "Không thể phát nghe thử lúc này.", "OK");

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
        _nearFocusAudioTimer.Interval = TimeSpan.FromMilliseconds(250);
        _nearFocusAudioTimer.Tick += (_, _) =>
        {
            if (!_isNearFocusMode || !IsVisible)
                return;

            if (_vm.CurrentExploreState == MainViewModel.ExploreState.InZone &&
                (_tts.IsPlaying() || _vm.IsAudioPaused))
            {
                UpdateNearFocusOverlayState();
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
        => _lang.CurrentLanguage switch
        {
            "en" => poi.Description_En ?? poi.Name_En ?? poi.Name_Vi,
            "zh" => poi.Description_Zh ?? poi.Name_Zh ?? poi.Name_En ?? poi.Name_Vi,
            _ => poi.Description_Vi ?? poi.Name_Vi ?? poi.Name_En
        } ?? "Chào mừng đến với điểm tham quan.";

    private void OnNearRouteContinueClicked(object? sender, EventArgs e)
    {
        var poi = ResolveNearFocusPoi();
        if (poi == null)
            return;

        _isNearRouteActive = true;
        _nearRouteTargetPoiId = poi.Id;
        _vm.NavigationTarget = poi;
        _vm.SelectedPinPOI = poi;
        _vm.VisitedPOIIds.Add(poi.Id);
        _lastRouteRedraw = DateTime.MinValue;
        UpdateNearFocusOverlayState();
        CenterMapForNearFocus();
        _ = DrawNearFocusRouteAsync();
    }

    private void OnNearRouteCancelClicked(object? sender, EventArgs e)
    {
        var cancelledPoiId = _nearRouteTargetPoiId;
        _isNearRouteActive = false;
        _nearRouteTargetPoiId = null;

        if (cancelledPoiId.HasValue && _vm.NavigationTarget?.Id == cancelledPoiId.Value)
            _vm.NavigationTarget = null;

        ClearNearFocusRoute();
        UpdateNearFocusOverlayState();
    }

    private void EvaluateNearRouteCompletion(POI routePoi)
    {
        var distance = ComputeDistanceFromCurrentToPoi(routePoi);
        if (distance > NearRouteArrivalMeters)
            return;

        _isNearRouteActive = false;
        _nearRouteTargetPoiId = null;
        _vm.NavigationTarget = routePoi;
        ClearNearFocusRoute();
        UpdateNearFocusOverlayState();
        _ = ShowInZoneToastAsync($"Bạn đã đến {routePoi.Name_Vi ?? routePoi.Name_En ?? "điểm đến"}");
    }

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


