using StreetFoodNarrator.App.Core.Models;
using StreetFoodNarrator.App.Core.Services;
using Mapsui;
using Mapsui.Layers;
using Mapsui.Projections;
using Mapsui.Styles;
using Mapsui.Tiling;
using Mapsui.Tiling.Layers;
using BruTile.Predefined;
using MapsColor = Mapsui.Styles.Color;
using MapsBrush = Mapsui.Styles.Brush;
using NetTopologySuite.Geometries;
using Mapsui.Nts;
using MPoint = Mapsui.MPoint;

namespace StreetFoodNarrator.App.Views;

public partial class POIDetailPage : ContentPage
{
    private readonly POI _poi;

    public POIDetailPage(POI poi)
    {
        InitializeComponent();
        _poi = poi;
        BindingContext = poi;
        
        // Initialize map after the page is loaded
        Loaded += (s, e) => InitializeMap();
    }

    private void InitializeMap()
    {
        if (MapView?.Map == null) return;

        try
        {
            // Add OSM tile layer
            var cacheDir = Path.Combine(FileSystem.AppDataDirectory, "tile_cache");
            Directory.CreateDirectory(cacheDir);
            var cacheDb = Path.Combine(cacheDir, "osm.db");
            var tileCache = new SqliteTileCache(cacheDb);

            var tileSource = KnownTileSources.Create(
                KnownTileSource.OpenStreetMap,
                persistentCache: tileCache);
            var osmLayer = new TileLayer(tileSource) { Name = "OSM" };
            MapView.Map.Layers.Add(osmLayer);

            // Add marker for POI location
            var poiLocation = SphericalMercator.FromLonLat(_poi.Longitude, _poi.Latitude);
            var feature = new PointFeature(new MPoint(poiLocation.x, poiLocation.y));
            feature.Styles = new[]
            {
                new SymbolStyle
                {
                    Fill = new MapsBrush(new MapsColor(34, 197, 94)), // Green color
                    Outline = new Pen(new MapsColor(255, 255, 255), 2),
                    SymbolScale = 0.8,
                    SymbolType = SymbolType.Ellipse
                }
            };
            
            var markerLayer = new MemoryLayer("POIMarker")
            {
                Features = new[] { feature }
            };
            MapView.Map.Layers.Add(markerLayer);

            // Center map on POI location with zoom level 16
            MapView.Map.Navigator.CenterOn(poiLocation.x, poiLocation.y);
            MapView.Map.Navigator.ZoomTo(16);

            MapView.Map.Widgets.Clear();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[POIDetail] Map init error: {ex}");
        }
    }

    private async void OnBackTapped(object sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }

    private async void OnShareTapped(object sender, EventArgs e)
    {
        try
        {
            await Share.Default.RequestAsync(new ShareTextRequest
            {
                Title = _poi.Name_Vi,
                Text = $"Xem thông tin quán {_poi.Name_Vi} tại địa chỉ: {_poi.Address}"
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[POIDetail] Share error: {ex}");
        }
    }

    private async void OnPhoneTapped(object sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(_poi.PhoneNumber)) return;
        
        try
        {
            PhoneDialer.Default.Open(_poi.PhoneNumber);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[POIDetail] Phone dialer error: {ex}");
        }
    }

    private async void OnViewMenuTapped(object sender, EventArgs e)
    {
        await DisplayAlertAsync("Menu", "Tính năng xem menu đầy đủ sẽ được cập nhật sớm", "OK");
    }

    private async void OnListenAudioTapped(object sender, EventArgs e)
    {
        await DisplayAlertAsync("Audio", "Đang phát audio hướng dẫn...", "OK");
        // TODO: Implement audio playback
    }

    private async void OnNavigateTapped(object sender, EventArgs e)
    {
        try
        {
            var uri = $"https://www.google.com/maps/dir/?api=1&destination={_poi.Latitude},{_poi.Longitude}";
            await Launcher.Default.OpenAsync(new Uri(uri));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[POIDetail] Navigate error: {ex}");
        }
    }
}
