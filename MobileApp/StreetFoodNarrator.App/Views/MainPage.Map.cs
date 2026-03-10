// ─────────────────────────────────────────────────────────────────────────────
// MainPage.Map.cs  –  Tab Bản đồ:
//   • InitializeMap()   – khởi tạo tile OSM + layer pins
//   • UpdateZonePins()  – vẽ lại tất cả pins theo trạng thái
//   • UpdateUserPin()   – cập nhật vị trí người dùng + auto-center
//   • Zoom & Header handlers (back, center, settings)
// ─────────────────────────────────────────────────────────────────────────────

using Mapsui;
using Mapsui.Layers;
using Mapsui.Projections;
using Mapsui.Styles;
using Mapsui.Tiling;
using Mapsui.Tiling.Layers;
using BruTile.Predefined;
using MapsColor = Mapsui.Styles.Color;
using MapsBrush = Mapsui.Styles.Brush;
using StreetFoodNarrator.App.Core.Services;
using NetTopologySuite.Geometries;
using Mapsui.Nts;

namespace StreetFoodNarrator.App.Views;

public partial class MainPage
{
    // ── Map state ──────────────────────────────────────────────────────────────
    private MemoryLayer? _pinsLayer;
    private const int DefaultMapZoomLevel = (int)AppConfig.DefaultZoom + 1;

    private void ZoomToDefaultLevel()
    {
        if (MapView?.Map?.Navigator == null) return;

        var resolutions = MapView.Map.Navigator.Resolutions;
        if (resolutions == null) return;

        var maxLevel = Math.Max(0, resolutions.Count() - 1);
        var level = Math.Clamp(DefaultMapZoomLevel, 0, maxLevel);
        MapView.Map.Navigator.ZoomToLevel(level);
    }

    // ─── Khởi tạo bản đồ ────────────────────────────────────────────────────

    private void InitializeMap()
    {
        Console.WriteLine("[Map] InitializeMap starting...");
        
        if (MapView?.Map == null)
        {
            Console.WriteLine("[Map] ❌ MapView or MapView.Map is null!");
            return;
        }

        // Tile layer OSM – dùng SQLite cache để hỗ trợ offline
        try
        {
            var cacheDir  = Path.Combine(FileSystem.AppDataDirectory, "tile_cache");
            Directory.CreateDirectory(cacheDir);
            var cacheDb   = Path.Combine(cacheDir, "osm.db");
            var tileCache = new SqliteTileCache(cacheDb);

            var tileSource = KnownTileSources.Create(
                KnownTileSource.OpenStreetMap,
                persistentCache: tileCache);
            var osmLayer = new TileLayer(tileSource) { Name = "OSM" };
            MapView.Map.Layers.Add(osmLayer);
            Console.WriteLine("[Map] ✓ OSM tile layer added");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Map] ⚠️ OSM tile error (offline?): {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[Map] OSM tile error (offline?): {ex.Message}");
            // Tiếp tục không có tile nền — pins vẫn hoạt động
        }

        // ── Dark overlay layer - ADD NGAY SAU OSM để làm tối nền ──
        var darkOverlay = new MemoryLayer("DarkOverlay")
        {
            Style = new VectorStyle
            {
                Fill = new MapsBrush(new MapsColor(8, 22, 12, 180)), // Màu tối xanh lá 70% opacity
                Outline = null
            }
        };
        Console.WriteLine("[Map] ✓ Dark overlay layer created");

        // Tạo một polygon phủ toàn bộ thế giới (Web Mercator bounds)
        var worldExtent = new[]
        {
            new MPoint(-20037508.34, -20037508.34), // Bottom-left
            new MPoint(20037508.34, -20037508.34),  // Bottom-right
            new MPoint(20037508.34, 20037508.34),   // Top-right
            new MPoint(-20037508.34, 20037508.34),  // Top-left
            new MPoint(-20037508.34, -20037508.34)  // Close polygon
        };
        var overlayPolygon = new NetTopologySuite.Geometries.Polygon(
            new NetTopologySuite.Geometries.LinearRing(
                worldExtent.Select(p => new NetTopologySuite.Geometries.Coordinate(p.X, p.Y)).ToArray()));
        darkOverlay.Features = new[] { new GeometryFeature { Geometry = overlayPolygon } };
        MapView.Map.Layers.Add(darkOverlay);
        Console.WriteLine("[Map] ✓ Dark overlay added (between OSM and Pins)");

        // Layer pins cho user + POI markers - ADD CUỐI CÙNG để ở trên cùng
        _pinsLayer = new MemoryLayer("Pins");
        MapView.Map.Layers.Add(_pinsLayer);
        Console.WriteLine($"[Map] ✓ Pins layer added on top. Total layers: {MapView.Map.Layers.Count}");

        // Ẩn debug widgets mặc định (toạ độ, zoom level…)
        MapView.Map.Widgets.Clear();

        // Để MAUI không nuốt scroll/touch event trước khi đến MapControl
        MapView.InputTransparent         = false;
        MapView.CascadeInputTransparent  = false;

        // Tắt rotation (mobile không cần xoay bản đồ)
        MapView.Map.Navigator.RotationLock = true;

        // Bật fling/momentum khi pan
        MapView.UseFling = true;

        // Tự động căn giữa Vĩnh Khánh khi mở lần đầu với zoom sát hơn
        Dispatcher.Dispatch(async () =>
        {
            await Task.Delay(150);
            var (cx, cy) = SphericalMercator.FromLonLat(
                AppConfig.DefaultLongitude, AppConfig.DefaultLatitude);
            MapView?.Map?.Navigator.CenterOn(new MPoint(cx, cy));
            ZoomToDefaultLevel(); // Zoom nhà/địa điểm rõ ràng (house-level)
        });

        // Lắng nghe sự kiện tap trên bản đồ để hiển thị pin popup
        MapView.Map.Info += OnMapInfoTapped;
        
        Console.WriteLine($"[Map] ✓ InitializeMap completed! Map ready at ({AppConfig.DefaultLatitude}, {AppConfig.DefaultLongitude})");
    }

    // ─── Pins ─────────────────────────────────────────────────────────────────

    private void UpdateZonePins()
    {
        if (_pinsLayer == null || MapView?.Map == null)
        {
            Console.WriteLine("[Map] ❌ UpdateZonePins called but _pinsLayer or MapView.Map is null");
            return;
        }

        var features  = new List<IFeature>();
        var nearbyIds = _vm.ActiveZones.Select(z => z.Id).ToHashSet();

        Console.WriteLine($"[Map] UpdateZonePins: AllPOIs.Count={_vm.AllPOIs.Count}, ActiveZones.Count={_vm.ActiveZones.Count}");
        Console.WriteLine($"[Map] Map center: ({AppConfig.DefaultLatitude}, {AppConfig.DefaultLongitude})");

        // Vị trí người dùng – vòng trắng, nhân xanh lá
        if (_vm.CurrentLat != 0)
        {
            var (ux, uy) = SphericalMercator.FromLonLat(_vm.CurrentLon, _vm.CurrentLat);
            Console.WriteLine($"[Map] User position: ({_vm.CurrentLat}, {_vm.CurrentLon}) -> Mercator ({ux:F2}, {uy:F2})");
            var f = new PointFeature(new MPoint(ux, uy));
            f.Styles.Add(new SymbolStyle
            {
                SymbolScale = 0.6,
                Fill        = new MapsBrush(new MapsColor(34, 197, 94)),
                Outline     = new Pen(MapsColor.White, 3)
            });
            features.Add(f);
        }

        // Tất cả POI loại Spot – màu theo trạng thái
        // Ưu tiên: ⭐ Đã lưu (vàng) > 🟣 Đã ghé (tím) > 🔵 Đang gần (xanh dương) > � Chưa ghé (cam)
        var poiCount = 0;
        foreach (var poi in _vm.AllPOIs)
        {
            if (poi.ZoneType == "Area" || poi.ZoneType == "District") continue;
            
            poiCount++;
            var (px, py) = SphericalMercator.FromLonLat(poi.Longitude, poi.Latitude);
            
            // Log first 3 POIs để debug tọa độ
            if (poiCount <= 3)
            {
                Console.WriteLine($"[Map] POI #{poiCount}: '{poi.Name_Vi}' at ({poi.Latitude}, {poi.Longitude}) -> Mercator ({px:F2}, {py:F2})");
            }
            
            var f = new PointFeature(new MPoint(px, py));
            f["POI_ID"] = poi.Id;

            MapsColor fillColor;
            if (_vm.SavedPOIIds.Contains(poi.Id))
                fillColor = new MapsColor(251, 191, 36);  // ⭐ Vàng = đã lưu
            else if (_vm.VisitedPOIIds.Contains(poi.Id))
                fillColor = new MapsColor(147, 51, 234);  // 🟣 Tím  = đã ghé
            else if (nearbyIds.Contains(poi.Id))
                fillColor = new MapsColor(59, 130, 246);  // 🔵 Xanh dương = đang gần
            else
                fillColor = new MapsColor(249, 115, 22);  // 🟠 Cam  = chưa ghé

            // ── Glow ring ngoài (nhỏ, mờ) ──
            f.Styles.Add(new SymbolStyle
            {
                SymbolScale = 1.0,
                Fill        = new MapsBrush(new MapsColor(fillColor.R, fillColor.G, fillColor.B, 100)),
                Outline     = null,
                SymbolType  = SymbolType.Ellipse
            });
            // ── Dot chính (dấu chấm nhỏ, viền trắng) ──
            f.Styles.Add(new SymbolStyle
            {
                SymbolScale = 0.5,
                Fill        = new MapsBrush(fillColor),
                Outline     = new Pen(MapsColor.White, 2.5f),
                SymbolType  = SymbolType.Ellipse
            });

            // ── Label dưới dot (tên quán ngắn gọn) ──
            var labelText = poi.Name_Vi ?? poi.Name_En ?? "";
            if (labelText.Length > 15) labelText = labelText[..15];
            f.Styles.Add(new LabelStyle
            {
                Text               = labelText,
                ForeColor          = MapsColor.White,
                BackColor          = new MapsBrush(new MapsColor(10, 16, 12, 220)),
                Font               = new Mapsui.Styles.Font { FontFamily = "sans-serif", Size = 8, Bold = false },
                Offset             = new Offset(0, 18),
                HorizontalAlignment = LabelStyle.HorizontalAlignmentEnum.Center,
                VerticalAlignment  = LabelStyle.VerticalAlignmentEnum.Top,
                MaxWidth           = 80,
                WordWrap           = LabelStyle.LineBreakMode.NoWrap
            });
    
        // Tính khoảng cách từ map center đến POI đầu tiên
        var firstSpot = _vm.AllPOIs.FirstOrDefault(p => p.ZoneType != "Area" && p.ZoneType != "District");
        if (firstSpot != null)
        {
            var latDiff = Math.Abs(firstSpot.Latitude - AppConfig.DefaultLatitude);
            var lonDiff = Math.Abs(firstSpot.Longitude - AppConfig.DefaultLongitude);
            var distanceKm = Math.Sqrt(latDiff * latDiff + lonDiff * lonDiff) * 111; // rough km conversion
            Console.WriteLine($"[Map] First POI distance from center: ~{distanceKm:F2} km (latDiff={latDiff:F6}, lonDiff={lonDiff:F6})");
        }
    
            features.Add(f);
        }

        _pinsLayer.Features = features;
        _pinsLayer.DataHasChanged();
        MapView.RefreshGraphics();
        
        var spotCount = _vm.AllPOIs.Count(p => p.ZoneType != "Area" && p.ZoneType != "District");
        Console.WriteLine($"[Map] ✓ UpdateZonePins completed: Drew {features.Count} features (1 user + {spotCount} POI pins)");
    }

    private void UpdateUserPin()
    {
        UpdateZonePins();
        if (_vm.CurrentLat == 0 || MapView?.Map == null) return;
        var (ux, uy) = SphericalMercator.FromLonLat(_vm.CurrentLon, _vm.CurrentLat);
        MapView.Map.Navigator.CenterOn(new MPoint(ux, uy));
    }

    // ─── Header buttons ───────────────────────────────────────────────────────

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        try { await Navigation.PopAsync(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Map] OnBackClicked: {ex}"); }
    }

    private void OnCenterMapClicked(object? sender, EventArgs e)
    {
        if (MapView?.Map == null) return;
        var (cx, cy) = SphericalMercator.FromLonLat(AppConfig.DefaultLongitude, AppConfig.DefaultLatitude);
        MapView.Map.Navigator.CenterOn(new MPoint(cx, cy));
    }

    private async void OnSettingsClicked(object? sender, EventArgs e)
    {
        try { await Navigation.PushAsync(new SettingsPage()); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Map] OnSettingsClicked: {ex}"); }
    }

    // ─── Zoom ─────────────────────────────────────────────────────────────────

    private void OnZoomInClicked(object? sender, EventArgs e)
        => MapView?.Map?.Navigator.ZoomIn(300);

    private void OnZoomOutClicked(object? sender, EventArgs e)
        => MapView?.Map?.Navigator.ZoomOut(300);

    // ─── DEBUG: Reset Database ────────────────────────────────────────────────

    private async void OnResetDatabaseClicked(object? sender, EventArgs e)
    {
        try
        {
            Console.WriteLine("[Map] 🔄 Reset database button clicked!");
            
            // Hiển thị confirm dialog
            bool confirm = await DisplayAlertAsync(
                "Reset Database",
                "Xóa toàn bộ dữ liệu và seed lại mock POIs với tọa độ đúng?",
                "Reset",
                "Hủy");
            
            if (!confirm)
            {
                Console.WriteLine("[Map] User cancelled reset");
                return;
            }
            
            // Gọi ViewModel để reset
            await _vm.ResetDatabaseAsync();
            
            // Vẽ lại pins với tọa độ mới
            UpdateZonePins();
            
            await DisplayAlertAsync("✓ Hoàn tất", "Database đã được reset với tọa độ đúng!", "OK");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Map] ❌ OnResetDatabaseClicked error: {ex.Message}");
            await DisplayAlertAsync("Lỗi", $"Không thể reset database: {ex.Message}", "OK");
        }
    }
}
