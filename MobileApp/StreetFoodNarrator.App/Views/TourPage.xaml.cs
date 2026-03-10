// ─────────────────────────────────────────────────────────────────────────────
// TourPage.xaml.cs  –  Tab Tour (Hành trình)
// ─────────────────────────────────────────────────────────────────────────────

using Mapsui;
using Mapsui.Layers;
using Mapsui.Projections;
using Mapsui.Styles;
using Mapsui.Tiling;
using Mapsui.Tiling.Layers;
using BruTile.Predefined;
using Mapsui.Nts;
using NetTopologySuite.Geometries;
using MapsColor = Mapsui.Styles.Color;
using MapsBrush = Mapsui.Styles.Brush;
using StreetFoodNarrator.App.ViewModels;
using StreetFoodNarrator.App.Core.Services;

namespace StreetFoodNarrator.App.Views;

public partial class TourPage : ContentPage
{
    private readonly MainViewModel _vm;
    private MemoryLayer? _tourPinsLayer;
    private bool _mapInitialized = false;
    private const int DefaultMapZoomLevel = (int)AppConfig.DefaultZoom + 1;

    private void ZoomToDefaultLevel()
    {
        if (TourMapView?.Map?.Navigator == null) return;

        var resolutions = TourMapView.Map.Navigator.Resolutions;
        if (resolutions == null) return;

        var maxLevel = Math.Max(0, resolutions.Count() - 1);
        var level = Math.Clamp(DefaultMapZoomLevel, 0, maxLevel);
        TourMapView.Map.Navigator.ZoomToLevel(level);
    }

    public TourPage()
    {
        InitializeComponent();
        _vm = MauiProgram.Services.GetRequiredService<MainViewModel>();
        BindingContext = _vm;
        WireComponentEvents();
        Loaded += OnPageLoaded;
        Console.WriteLine("[TourPage] Initialized");
    }

    private void WireComponentEvents()
    {
        TourContent.CenterMapRequested += OnCenterMapClicked;
        TourContent.ZoomInRequested += OnZoomInClicked;
        TourContent.ZoomOutRequested += OnZoomOutClicked;
    }

    private void OnCenterMapClicked(object? sender, EventArgs e)
    {
        if (TourMapView?.Map == null) return;

        if (_vm.CurrentLat != 0)
        {
            var (ux, uy) = SphericalMercator.FromLonLat(_vm.CurrentLon, _vm.CurrentLat);
            TourMapView.Map.Navigator.CenterOn(new MPoint(ux, uy));
        }
        else
        {
            var (cx, cy) = SphericalMercator.FromLonLat(
                AppConfig.DefaultLongitude, AppConfig.DefaultLatitude);
            TourMapView.Map.Navigator.CenterOn(new MPoint(cx, cy));
        }
    }

    private void OnZoomInClicked(object? sender, EventArgs e)
        => TourMapView?.Map?.Navigator.ZoomIn(300);

    private void OnZoomOutClicked(object? sender, EventArgs e)
        => TourMapView?.Map?.Navigator.ZoomOut(300);

    private void OnPageLoaded(object? sender, EventArgs e)
    {
        // Khởi tạo bản đồ chỉ một lần khi page load
        if (!_mapInitialized)
        {
            _mapInitialized = true;
            try { InitializeTourMap(); }
            catch (Exception ex) { Console.WriteLine($"[TourPage] InitializeTourMap error: {ex.Message}"); }
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Console.WriteLine("[TourPage] OnAppearing");
        TourContent.IsVisible = true;

        // Tải POIs nếu chưa có và cập nhật pins
        if (_vm.AllPOIs.Count == 0)
        {
            _ = _vm.LoadAllPoisAsync().ContinueWith(_ =>
                MainThread.BeginInvokeOnMainThread(UpdateTourPins));
        }
        else
        {
            UpdateTourPins();
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        Console.WriteLine("[TourPage] OnDisappearing");
    }

    // ─── Khởi tạo bản đồ Tour ─────────────────────────────────────────────────

    private void InitializeTourMap()
    {
        if (TourMapView?.Map == null) return;

        // Tile layer OSM với SQLite cache offline
        try
        {
            var cacheDir  = Path.Combine(FileSystem.AppDataDirectory, "tile_cache");
            Directory.CreateDirectory(cacheDir);
            var cacheDb   = Path.Combine(cacheDir, "osm_tour.db");
            var tileCache = new SqliteTileCache(cacheDb);
            var tileSource = KnownTileSources.Create(
                KnownTileSource.OpenStreetMap,
                persistentCache: tileCache);
            TourMapView.Map.Layers.Add(new TileLayer(tileSource) { Name = "OSM" });
            Console.WriteLine("[TourPage] ✓ OSM tile layer added");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TourPage] ⚠️ OSM offline: {ex.Message}");
        }

        // Dark overlay để hòa hợp theme tối
        var darkOverlay = new MemoryLayer("DarkOverlay");
        var worldCoords = new[]
        {
            new Coordinate(-20037508.34, -20037508.34),
            new Coordinate(20037508.34, -20037508.34),
            new Coordinate(20037508.34, 20037508.34),
            new Coordinate(-20037508.34, 20037508.34),
            new Coordinate(-20037508.34, -20037508.34)
        };
        var worldPoly = new Polygon(new LinearRing(worldCoords));
        darkOverlay.Features = new[] { new GeometryFeature { Geometry = worldPoly } };
        darkOverlay.Style = new VectorStyle
        {
            Fill    = new MapsBrush(new MapsColor(8, 22, 12, 180)),
            Outline = null
        };
        TourMapView.Map.Layers.Add(darkOverlay);

        // Layer pins
        _tourPinsLayer = new MemoryLayer("TourPins");
        TourMapView.Map.Layers.Add(_tourPinsLayer);

        // Cấu hình bản đồ
        TourMapView.Map.Widgets.Clear();
        TourMapView.Map.Navigator.RotationLock = true;
        TourMapView.UseFling = true;
        TourMapView.InputTransparent = false;
        TourMapView.CascadeInputTransparent = false;

        // Căn giữa Vĩnh Khánh
        Dispatcher.Dispatch(async () =>
        {
            await Task.Delay(150);
            var (cx, cy) = SphericalMercator.FromLonLat(
                AppConfig.DefaultLongitude, AppConfig.DefaultLatitude);
            TourMapView?.Map?.Navigator.CenterOn(new MPoint(cx, cy));
            ZoomToDefaultLevel();
        });

        Console.WriteLine("[TourPage] ✓ InitializeTourMap completed");
    }

    // ─── Cập nhật pins bản đồ Tour ───────────────────────────────────────────

    private void UpdateTourPins()
    {
        if (_tourPinsLayer == null || TourMapView?.Map == null) return;

        var features = new List<IFeature>();
        var activePOIId = _vm.PrimaryZone?.Id ?? -1;

        // Vị trí người dùng – chấm xanh lá
        if (_vm.CurrentLat != 0)
        {
            var (ux, uy) = SphericalMercator.FromLonLat(_vm.CurrentLon, _vm.CurrentLat);
            var userFeature = new PointFeature(new MPoint(ux, uy));
            userFeature.Styles.Add(new SymbolStyle
            {
                SymbolScale = 0.6,
                Fill        = new MapsBrush(new MapsColor(34, 197, 94)),
                Outline     = new Pen(MapsColor.White, 3)
            });
            features.Add(userFeature);
        }

        // Tất cả POI Spot – highlight active POI bằng màu vàng cam nổi bật
        foreach (var poi in _vm.AllPOIs)
        {
            if (poi.ZoneType == "Area" || poi.ZoneType == "District") continue;

            var (px, py) = SphericalMercator.FromLonLat(poi.Longitude, poi.Latitude);
            var f = new PointFeature(new MPoint(px, py));
            f["POI_ID"] = poi.Id;

            // Active POI: vàng lớn + pulse ring | Còn lại: cam nhỏ
            bool isActive = poi.Id == activePOIId;
            var fillColor = isActive
                ? new MapsColor(251, 191, 36)   // Vàng – active
                : new MapsColor(249, 115, 22);  // Cam  – chưa ghé

            // Glow ring
            f.Styles.Add(new SymbolStyle
            {
                SymbolScale = isActive ? 1.4 : 1.0,
                Fill        = new MapsBrush(new MapsColor(fillColor.R, fillColor.G, fillColor.B, isActive ? 130 : 80)),
                Outline     = null,
                SymbolType  = SymbolType.Ellipse
            });
            // Dot chính
            f.Styles.Add(new SymbolStyle
            {
                SymbolScale = isActive ? 0.7 : 0.45,
                Fill        = new MapsBrush(fillColor),
                Outline     = new Pen(MapsColor.White, isActive ? 3f : 2f),
                SymbolType  = SymbolType.Ellipse
            });
            // Label tên
            var label = poi.Name_Vi ?? poi.Name_En ?? "";
            if (label.Length > 15) label = label[..15];
            f.Styles.Add(new LabelStyle
            {
                Text              = label,
                ForeColor         = MapsColor.White,
                BackColor         = new MapsBrush(new MapsColor(10, 16, 12, 210)),
                Font              = new Mapsui.Styles.Font { FontFamily = "sans-serif", Size = isActive ? 9 : 8, Bold = isActive },
                Offset            = new Offset(0, isActive ? 22 : 18),
                HorizontalAlignment = LabelStyle.HorizontalAlignmentEnum.Center,
                VerticalAlignment = LabelStyle.VerticalAlignmentEnum.Top,
                MaxWidth          = 90,
                WordWrap          = LabelStyle.LineBreakMode.NoWrap
            });

            features.Add(f);
        }

        _tourPinsLayer.Features = features;
        _tourPinsLayer.DataHasChanged();
        TourMapView.RefreshGraphics();
        Console.WriteLine($"[TourPage] ✓ UpdateTourPins: {features.Count} features");
    }
}
