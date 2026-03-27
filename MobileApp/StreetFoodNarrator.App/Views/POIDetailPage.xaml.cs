using StreetFoodNarrator.App.Core.Models;
using StreetFoodNarrator.App.Core.Services;
using Mapsui.Layers;
using Microsoft.Maui.Networking;
using Mapsui.Projections;
using Mapsui.Styles;
using Mapsui.Tiling;
using Mapsui.Tiling.Layers;
using BruTile.Predefined;
using BruTile.Web;
using MapsColor = Mapsui.Styles.Color;
using MapsBrush = Mapsui.Styles.Brush;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Text.Json;
using NetTopologySuite.Geometries;
using Mapsui.Nts;
using MPoint = Mapsui.MPoint;

namespace StreetFoodNarrator.App.Views;

public partial class POIDetailPage : ContentPage
{
    private readonly POI _poi;
    private readonly ITTSService _tts;
    private readonly LanguageService _lang;
    private bool _isPlaying = false;
    private bool _mapInitialized = false;
    
    public ObservableCollection<MenuItemDto> MenuItems { get; } = new();

    public POIDetailPage(POI poi)
    {
        InitializeComponent();
        _poi = poi;
        _tts = MauiProgram.Services.GetRequiredService<ITTSService>();
        _lang = MauiProgram.Services.GetRequiredService<LanguageService>();
        BindingContext = poi;
        
        // Initialize map after the page is loaded
        Loaded += (s, e) => InitializeMap();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        // ✅ Load menu in background (non-blocking) so page shows immediately
        _ = LoadMenuItemsAsync();
    }

    private async Task LoadMenuItemsAsync()
    {
        try 
        {
            var db = MauiProgram.Services.GetRequiredService<ILocalDatabaseService>();
            var netAccess = Connectivity.Current.NetworkAccess;
            var isOnline  = netAccess is NetworkAccess.Internet or NetworkAccess.ConstrainedInternet;
            
            if (isOnline)
            {
                var url = $"{AppConfig.GetResolvedApiBaseUrl()}api/MenuItems?poiId={_poi.Id}&page=1&pageSize=50";
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                var response = await client.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<MenuItemResponse>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (result?.Data != null)
                    {
                        // Save to offline DB
                        if (result.Data.Any())
                        {
                            await db.SaveMenuItemsAsync(result.Data);
                        }
                        
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            MenuItems.Clear();
                            foreach(var item in result.Data) MenuItems.Add(item);
                            UpdateDishIndicator();
                        });
                        return;
                    }
                }
            }
            
            // Offline fallback or API failed
            var localItems = await db.GetMenuItemsByPoiAsync(_poi.Id);
            if (localItems != null && localItems.Any())
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    MenuItems.Clear();
                    foreach(var item in localItems) MenuItems.Add(item);
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[POIDetail] Load menu error: {ex}");
        }
    }

    private void InitializeMap()
    {
        // ✅ Prevent re-initialization
        if (_mapInitialized || MapView?.Map == null) return;
        _mapInitialized = true;

        try
        {
            // Add OSM tile layer
            var cacheDb = Path.Combine(FileSystem.AppDataDirectory, "map_cache", "tiles.db");
            var tileCache = new StreetFoodNarrator.App.Services.SimpleTileCache(cacheDb);

            var tileSource = new HttpTileSource(
                new GlobalSphericalMercator(),
                "https://a.basemaps.cartocdn.com/rastertiles/voyager/{z}/{x}/{y}.png",
                name: "Carto",
                persistentCache: tileCache
            );
            var baseLayer = new TileLayer(tileSource) { Name = "BaseMap", Opacity = 0.9 };
            MapView.Map.BackColor = new MapsColor(26, 26, 26); // Dark background
            MapView.Map.Layers.Add(baseLayer);

            // Add marker for POI location
            var poiLocation = SphericalMercator.FromLonLat(_poi.Longitude, _poi.Latitude);
            var feature = new PointFeature(new MPoint(poiLocation.x, poiLocation.y));
            feature.Styles = new IStyle[]
            {
                new SymbolStyle
                {
                    Fill = new MapsBrush(new MapsColor(34, 197, 94)), // Green color
                    Outline = new Pen(new MapsColor(255, 255, 255), 2),
                    SymbolScale = 0.8,
                    SymbolType = SymbolType.Ellipse
                },
                new LabelStyle
                {
                    Text = _poi.Name_Vi,
                    BackColor = new MapsBrush(new MapsColor(0, 0, 0, 180)), // Semi-transparent black background
                    ForeColor = new MapsColor(255, 255, 255),
                    Halo = new Pen(new MapsColor(26, 26, 26), 2), // #1A1A1A outline
                    Offset = new Offset(0, 16)
                }
            };
            
            var markerLayer = new MemoryLayer("POIMarker")
            {
                Features = new[] { feature }
            };
            MapView.Map.Layers.Add(markerLayer);

            // Center map on POI location and zoom closer
            MapView.Map.Navigator.CenterOn(poiLocation.x, poiLocation.y);
            MapView.Map.Navigator.ZoomTo(18);

            MapView.Map.Widgets.Clear();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[POIDetail] Map init error: {ex}");
        }
    }

    private async void OnBackTapped(object sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
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

    private void OnDishPrev(object sender, EventArgs e)
    {
        if (DishCarousel.Position > 0) DishCarousel.Position--;
        UpdateDishIndicator();
    }

    private void OnDishNext(object sender, EventArgs e)
    {
        if (DishCarousel.Position < MenuItems.Count - 1) DishCarousel.Position++;
        UpdateDishIndicator();
    }

    private void UpdateDishIndicator()
    {
        if (MenuItems.Count == 0) { DishIndicatorLabel.Text = ""; return; }
        DishIndicatorLabel.Text = $"{DishCarousel.Position + 1} / {MenuItems.Count}";
    }

    private async void OnListenAudioTapped(object sender, EventArgs e)
    {
        try
        {
            if (_isPlaying)
            {
                await _tts.StopAsync();
                _isPlaying = false;
                return;
            }

            string lang = _lang.CurrentLanguage switch
            {
                "en" => "en-US",
                "zh" => "zh-CN",
                _    => "vi-VN"
            };

            var text = _lang.CurrentLanguage switch
            {
                "en" => _poi.Description_En ?? _poi.Name_En ?? _poi.Name_Vi,
                "zh" => _poi.Description_Zh ?? _poi.Name_Zh ?? _poi.Name_En ?? _poi.Name_Vi,
                _    => _poi.Description_Vi ?? _poi.Name_Vi ?? _poi.Name_En
            } ?? "Chao mung den voi diem tham quan.";

            var ok = await _tts.SpeakAsync(text, lang, poiId: _poi.Id);
            _isPlaying = ok;
            
            if (ok)
            {
                await DisplayAlertAsync("🎵 Audio", "Đang phát audio hướng dẫn...", "OK");
            }
            else
            {
                await DisplayAlertAsync("⚠️ Lỗi", "Không thể phát audio. Vui lòng thử lại.", "OK");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[POIDetail] OnListenAudioTapped: {ex}");
            await DisplayAlertAsync("❌ Lỗi", $"Không thể phát audio: {ex.Message}", "OK");
            _isPlaying = false;
        }
    }

    private async void OnNavigateTapped(object sender, EventArgs e)
    {
        try
        {
            var vm = MauiProgram.Services.GetService<StreetFoodNarrator.App.ViewModels.MainViewModel>();
            if (vm == null) return;

            // Tính khoảng cách từ vị trí hiện tại đến POI
            var currentLoc = new Microsoft.Maui.Devices.Sensors.Location(vm.CurrentLat, vm.CurrentLon);
            var destLoc = new Microsoft.Maui.Devices.Sensors.Location(_poi.Latitude, _poi.Longitude);
            var distKm = Microsoft.Maui.Devices.Sensors.Location.CalculateDistance(currentLoc, destLoc, DistanceUnits.Kilometers);

            if (distKm <= 1.0)
            {
                vm.IsVirtualNavigation = false;
            }
            else
            {
                await DisplayAlert("Chế độ Xem Ảo", "Bạn đang ở cách quán hơn 1km. Bản đồ sẽ chuyển sang tương tác Xem Ảo.", "Đã Hiểu");
                vm.IsVirtualNavigation = true;
            }

            // Set target after mode is finalized so map renders with the correct routing style/viewport.
            vm.NavigationTarget = _poi;

            // Đóng modal chi tiết rồi chuyển sang tab Bản đồ để vẽ tuyến đường.
            await Navigation.PopModalAsync();
            if (Shell.Current != null)
                await Shell.Current.GoToAsync("//MapPage");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[POIDetail] Navigate error: {ex}");
            await DisplayAlert("Lỗi", "Không thể gọi tính năng chỉ đường lúc này.", "OK");
        }
    }
}
