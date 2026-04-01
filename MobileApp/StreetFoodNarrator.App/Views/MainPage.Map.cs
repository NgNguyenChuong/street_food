// -----------------------------------------------------------------------------
// MainPage.Map.cs  -  Tab Ban do:
//   - InitializeMap()   - khoi tao tile OSM + layer pins
//   - UpdateZonePins()  - ve lai tat ca pins theo trang thai
//   - UpdateUserPin()   - cap nhat vi tri nguoi dung + auto-center
//   - Zoom & Header handlers (back, center, settings)
// -----------------------------------------------------------------------------

using Mapsui;
using Mapsui.Layers;
using Mapsui.Projections;
using Mapsui.Styles;
using Mapsui.Tiling;
using Mapsui.Tiling.Layers;
using BruTile.Predefined;
using BruTile.Web;
using System.Net.Http;
using MapsColor = Mapsui.Styles.Color;
using MapsBrush = Mapsui.Styles.Brush;
using StreetFoodNarrator.App;
using StreetFoodNarrator.App.Core.Services;
using NetTopologySuite.Geometries;
using Mapsui.Nts;
using System.Text.Json;
using StreetFoodNarrator.App.Helpers;

namespace StreetFoodNarrator.App.Views;

public partial class MainPage
{
    private MemoryLayer? _pinsLayer;
    private MemoryLayer? _routeLayer;
    private MemoryLayer? _userPinLayer;  // ✅ Separate user pin layer (FIX 4)
    private bool _isFirstLocation = true;
    private DateTime _lastRouteRedraw = DateTime.MinValue;
    private const int ROUTE_REDRAW_INTERVAL_MS = 3000;  // ✅ Debounce route redraw (FIX 4)
    private const int DefaultMapZoomLevel = (int)AppConfig.DefaultZoom + 1;
    private readonly HttpClient _routeHttpClient = new() { Timeout = TimeSpan.FromSeconds(6) };

    private void ZoomToDefaultLevel()
    {
        if (MapView?.Map?.Navigator == null) return;

        var resolutions = MapView.Map.Navigator.Resolutions;
        if (resolutions == null) return;

        var maxLevel = Math.Max(0, resolutions.Count() - 1);
        var level = Math.Clamp(DefaultMapZoomLevel, 0, maxLevel);
        MapView.Map.Navigator.ZoomToLevel(level);
    }

    private void InitializeMap()
    {
        try
        {
            Console.WriteLine("[Map] InitializeMap starting...");

            if (MapView?.Map == null)
            {
                Console.WriteLine("[Map] MapView or MapView.Map is null!");
                return;
            }

            MapView.Map.Layers.Clear();
            MapView.Map.Widgets.Clear();

            try
            {
                var cacheDb = Path.Combine(FileSystem.AppDataDirectory, "map_cache", "tiles.db");
                var tileCache = new StreetFoodNarrator.App.Services.SimpleTileCache(cacheDb);

                var tileSource = new HttpTileSource(
                    new GlobalSphericalMercator(),
                    "https://a.basemaps.cartocdn.com/rastertiles/voyager/{z}/{x}/{y}.png",
                    name: "Carto",
                    persistentCache: tileCache
                );
                var baseLayer = new TileLayer(tileSource) { Name = "BaseMap" };
                MapView.Map.Layers.Add(baseLayer);
                Console.WriteLine("[Map] OSM tile layer added");

                var offlineNoCache = !HasUsableTileCache(cacheDb) &&
                                     Connectivity.Current.NetworkAccess != NetworkAccess.Internet &&
                                     Connectivity.Current.NetworkAccess != NetworkAccess.ConstrainedInternet;
                if (offlineNoCache)
                {
                    MapView.Map.Layers.Add(CreateOfflineFallbackLayer());
                    Console.WriteLine("[Map] Offline fallback layer enabled (no tile cache)");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Map] OSM tile error (offline?): {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[Map] OSM tile error (offline?): {ex.Message}");
                MapView.Map.Layers.Add(CreateOfflineFallbackLayer());
                Console.WriteLine("[Map] Fallback layer enabled after tile error");
            }

            try
            {
                var darkOverlay = new MemoryLayer("DarkOverlay")
                {
                    Style = new VectorStyle
                    {
                        // Keep map contrast, but avoid covering base map too heavily on slow tile loads.
                        Fill = new MapsBrush(new MapsColor(8, 22, 12, 55)),
                        Outline = null
                    }
                };
                Console.WriteLine("[Map] Dark overlay layer created");

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
                Console.WriteLine("[Map] Dark overlay added (between OSM and Pins)");

                _routeLayer = new MemoryLayer("RouteLayer");
                MapView.Map.Layers.Add(_routeLayer);

                _userPinLayer = new MemoryLayer("UserPin");  // ✅ Separate user pin layer (FIX 4)
                MapView.Map.Layers.Add(_userPinLayer);

                _pinsLayer = new MemoryLayer("Pins");
                MapView.Map.Layers.Add(_pinsLayer);
                Console.WriteLine($"[Map] Pins layer added on top. Total layers: {MapView.Map.Layers.Count}");

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
                Console.WriteLine($"[Map] Centered on default location ({AppConfig.DefaultLatitude}, {AppConfig.DefaultLongitude})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Map] Layer initialization error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[Map] Layer initialization error: {ex.Message}");
                // Don't rethrow — still mark initialized if critical layers failed
                _isMapInitialized = true;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Map] InitializeMap FATAL error: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[Map] InitializeMap FATAL error: {ex}");
            try
            {
                var logPath = Path.Combine(FileSystem.AppDataDirectory, "crash_log.txt");
                File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [Map.InitializeMap] {ex.Message}\n{ex.StackTrace}\n\n");
            }
            catch { /* safe */ }
        }
    }

    private void UpdateZonePins()
    {
        if (!_isMapInitialized || MapView?.IsVisible != true)
            return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (_pinsLayer == null || MapView?.Map == null)
            {
                Console.WriteLine("[Map] UpdateZonePins called but _pinsLayer or MapView.Map is null");
                return;
            }

            var features = new List<IFeature>();
            var nearbyIds = _vm.ActiveZones.Select(z => z.Id).ToHashSet();

            Console.WriteLine($"[Map] UpdateZonePins: AllPOIs.Count={_vm.AllPOIs.Count}, ActiveZones.Count={_vm.ActiveZones.Count}");

            // ✅ NOTE: User pin is now in separate _userPinLayer (FIX 4) - not drawn here

            var visiblePoiIds = _vm.FilteredPOIs.Select(p => p.Id).ToHashSet();

            foreach (var poi in _vm.AllPOIs)
            {
                try
                {
                    if (poi.ZoneType == "Area" || poi.ZoneType == "District") continue;
                    if (visiblePoiIds.Count > 0 && !visiblePoiIds.Contains(poi.Id)) continue;
                    if (double.IsNaN(poi.Latitude) || double.IsNaN(poi.Longitude)) continue;

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
                    if (labelText.Length > 15) labelText = labelText[..15];
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
                    System.Diagnostics.Debug.WriteLine($"[Map] Skip invalid POI #{poi.Id}: {ex.Message}");
                }
            }

            _pinsLayer.Features = features;
            _pinsLayer.DataHasChanged();
            MapView.RefreshGraphics();

            var spotCount = _vm.AllPOIs.Count(p => p.ZoneType != "Area" && p.ZoneType != "District");
            Console.WriteLine($"[Map] UpdateZonePins completed: Drew {features.Count} POI pins (user pin is separate layer)");
        });
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

    public async Task DrawNavigationRouteAsync()
    {
        if (!_isMapInitialized || MapView?.IsVisible != true)
            return;

        if (_routeLayer == null || MapView?.Map == null) return;

        var features = new List<IFeature>();
        var target = _vm.NavigationTarget;

        if (target != null)
        {
            var hasCurrentLocation = _vm.CurrentLat != 0 && _vm.CurrentLon != 0;
            var startLon = hasCurrentLocation ? _vm.CurrentLon : AppConfig.DefaultLongitude;
            var startLat = hasCurrentLocation ? _vm.CurrentLat : AppConfig.DefaultLatitude;

            var (startPx, startPy) = SphericalMercator.FromLonLat(startLon, startLat);
            var (endPx, endPy) = SphericalMercator.FromLonLat(target.Longitude, target.Latitude);

            Coordinate[] routeCoords;
            var isOnline = Connectivity.Current.NetworkAccess == NetworkAccess.Internet ||
                           Connectivity.Current.NetworkAccess == NetworkAccess.ConstrainedInternet;
            var offlineCoords = await FetchOfflineRouteAsync(
                startLat, startLon, target.Latitude, target.Longitude);

            if (offlineCoords is { Length: >= 2 })
            {
                routeCoords = offlineCoords;
            }
            else if (isOnline && !_vm.IsVirtualNavigation && hasCurrentLocation)
            {
                var osrmCoords = await FetchOsrmRouteAsync(
                    startLon, startLat, target.Longitude, target.Latitude);
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

            var lineString = new LineString(routeCoords);
            var feature = new GeometryFeature(lineString);
            feature.Styles.Add(new VectorStyle
            {
                Line = new Pen(new MapsColor(59, 130, 246), 4)
                {
                    PenStyle = _vm.IsVirtualNavigation ? PenStyle.Dash : PenStyle.Solid
                }
            });
            features.Add(feature);

            if (_vm.IsVirtualNavigation)
            {
                MapView.Map.Navigator.CenterOn(new MPoint(endPx, endPy));
                ZoomToDefaultLevel();
            }
            else
            {
                double midPx = (startPx + endPx) / 2.0;
                double midPy = (startPy + endPy) / 2.0;
                double maxDiff = Math.Max(Math.Abs(endPx - startPx), Math.Abs(endPy - startPy));

                if (maxDiff <= 800)
                {
                    MapView.Map.Navigator.CenterOn(new MPoint(endPx, endPy));
                    MapView.Map.Navigator.ZoomToLevel(19);
                }
                else
                {
                    int zoomLevel = DefaultMapZoomLevel;
                    if (maxDiff > 20000) zoomLevel = 10;
                    else if (maxDiff > 10000) zoomLevel = 12;
                    else if (maxDiff > 3000) zoomLevel = 13;
                    else if (maxDiff > 1000) zoomLevel = 14;
                    else if (maxDiff > 500) zoomLevel = 16;
                    else zoomLevel = 17;
                    MapView.Map.Navigator.CenterOn(new MPoint(midPx, midPy));
                    MapView.Map.Navigator.ZoomToLevel(zoomLevel);
                }
            }
        }

        _routeLayer.Features = features;
        _routeLayer.DataHasChanged();
        MapView.RefreshGraphics();
    }

    private async Task<Coordinate[]?> FetchOfflineRouteAsync(
        double srcLat, double srcLon, double dstLat, double dstLon)
    {
        if (_offlineRouting == null || _vm.IsVirtualNavigation)
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

            Console.WriteLine($"[Map] Offline route: {coords.Length} points, source={result.Source}");
            return coords.Length >= 2 ? coords : null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Map] Offline route fallback: {ex.Message}");
            return null;
        }
    }

    private async Task<Coordinate[]?> FetchOsrmRouteAsync(
        double srcLon, double srcLat, double dstLon, double dstLat)
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

            Console.WriteLine($"[Map] OSRM route: {coords.Length} diem theo duong di");
            return coords.Length >= 2 ? coords : null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Map] OSRM fallback (duong thang): {ex.Message}");
            return null;
        }
    }

    private void UpdateUserPin()
    {
        if (!_isMapInitialized || _userPinLayer == null || MapView?.Map == null || MapView?.IsVisible != true)
            return;

        try
        {
            if (_vm.CurrentLat == 0) return;

            var (ux, uy) = SphericalMercator.FromLonLat(_vm.CurrentLon, _vm.CurrentLat);

            // ✅ Only update user pin feature (not all POIs) - lightweight!
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

            // ✅ Only center on first location (not every GPS update)
            if (_isFirstLocation)
            {
                MapView.Map.Navigator.CenterOn(new MPoint(ux, uy));
                _isFirstLocation = false;
            }

            // ✅ Lightweight refresh - only user pin layer changed
            MapView.RefreshGraphics();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"UpdateUserPin error: {ex.Message}");
        }
    }

    private void OnBackClicked(object? sender, EventArgs e)
    {
        if (_vm.IsLegacyMapVisible)
        {
            ShowExploreStateMode();
            return;
        }

        _ = NavigateBackToWelcomeAsync();
    }

    private async Task NavigateBackToWelcomeAsync()
    {
        try
        {
            if (Shell.Current != null)
            {
                await Shell.Current.GoToAsync("//WelcomePage");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Map] NavigateBackToWelcomeAsync: {ex}");
        }
    }

    private void OnCenterMapClicked(object? sender, EventArgs e)
    {
        EnsureMapInitialized();
        if (MapView?.Map == null) return;
        var (cx, cy) = SphericalMercator.FromLonLat(AppConfig.DefaultLongitude, AppConfig.DefaultLatitude);
        MapView.Map.Navigator.CenterOn(new MPoint(cx, cy));
    }

    private async void OnSettingsClicked(object? sender, EventArgs e)
    {
        try
        {
            if (Shell.Current is AppShell shell)
            {
                await Shell.Current.Navigation.PushModalAsync(shell.GetCachedSettingsPage());
                return;
            }

            await Navigation.PushModalAsync(new SettingsPage());
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Map] OnSettingsClicked: {ex}");
        }
    }

    private void OnZoomInClicked(object? sender, EventArgs e)
        => MapView?.Map?.Navigator.ZoomIn(300);

    private void OnZoomOutClicked(object? sender, EventArgs e)
        => MapView?.Map?.Navigator.ZoomOut(300);

    private async void OnResetDatabaseClicked(object? sender, EventArgs e)
    {
        try
        {
            Console.WriteLine("[Map] Reset database button clicked!");

            bool confirm = await CustomAlert.ShowConfirmAsync(
                "Reset Database",
                "Xoa toan bo du lieu va seed lai mock POIs voi toa do dung?",
                "Reset",
                "Huy", AlertType.Warning);

            if (!confirm)
            {
                Console.WriteLine("[Map] User cancelled reset");
                return;
            }

            await _vm.ResetDatabaseAsync();
            UpdateZonePins();

            await CustomAlert.ShowAsync("Hoan tat", "Database da duoc reset voi toa do dung!", "OK", AlertType.Success);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Map] OnResetDatabaseClicked error: {ex.Message}");
            await CustomAlert.ShowAsync("Loi", $"Khong the reset database: {ex.Message}", "OK", AlertType.Error);
        }
    }
}
