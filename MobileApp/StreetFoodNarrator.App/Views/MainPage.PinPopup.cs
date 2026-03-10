// ─────────────────────────────────────────────────────────────────────────────
// MainPage.PinPopup.cs  –  Popup khi nhấn vào pin trên bản đồ:
//   • OnMapInfoTapped – tìm POI gần nhất tọa độ tap, hiện PinPopupCard
//   • OnPinPlayAudio  – phát audio của POI (offline cache ưu tiên)
//   • OnPinNavigate   – mở Google Maps dẫn đường
//   • OnPinSave       – lưu / bỏ lưu POI
// ─────────────────────────────────────────────────────────────────────────────

using Mapsui;
using Mapsui.Projections;

namespace StreetFoodNarrator.App.Views;

public partial class MainPage
{
    // ─── Map tap → tìm POI gần nhất ──────────────────────────────────────────

    private void OnMapInfoTapped(object? sender, Mapsui.MapInfoEventArgs e)
    {
        var worldPos = e.WorldPosition;
        if (worldPos == null) return;

        // Tìm Spot POI gần nhất trong ngưỡng 600 đơn vị bản đồ (~20m ở zoom 17)
        Core.Models.POI? nearest = null;
        double minDist = 600;

        foreach (var poi in _vm.AllPOIs)
        {
            if (poi.ZoneType == "Area" || poi.ZoneType == "District") continue;
            var (px, py) = SphericalMercator.FromLonLat(poi.Longitude, poi.Latitude);
            double dist  = Math.Sqrt(Math.Pow(worldPos.X - px, 2) + Math.Pow(worldPos.Y - py, 2));
            if (dist < minDist) { minDist = dist; nearest = poi; }
        }

        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (nearest != null)
            {
                _vm.SelectedPinPOI    = nearest;
                _vm.IsPinPopupVisible = true;
                PinPopupComponent.IsVisible = true;
            }
            else
            {
                PinPopupComponent.IsVisible = false;
            }
        });
    }

    // ─── Pin action buttons ───────────────────────────────────────────────────

    private void OnPinPlayAudio(object? sender, EventArgs e)
    {
        if (_vm.SelectedPinPOI == null) return;
        // Ưu tiên: cache MP3 offline → TTS API → native MAUI TTS
        _ = _tts.SpeakAsync(
            _vm.SelectedPinPOI.Description_Vi ?? _vm.SelectedPinPOI.Name_Vi ?? "Điểm thăm quan",
            "vi-VN",
            poiId: _vm.SelectedPinPOI.Id);
    }

    private async void OnPinNavigate(object? sender, EventArgs e)
    {
        try
        {
            var poi = _vm.SelectedPinPOI;
            if (poi == null) return;
            var uri = $"https://www.google.com/maps/dir/?api=1&destination={poi.Latitude},{poi.Longitude}";
            await Launcher.Default.OpenAsync(new Uri(uri));
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PinPopup] OnPinNavigate: {ex}"); }
    }

    private void OnPinSave(object? sender, EventArgs e)
    {
        if (_vm.SelectedPinPOI != null)
            _vm.ToggleSavePOICommand.Execute(_vm.SelectedPinPOI);
        UpdateZonePins(); // cập nhật màu pin ngay sau khi lưu/bỏ lưu
    }

    private async void OnPinViewDetails(object? sender, EventArgs e)
    {
        Console.WriteLine("[MainPage] OnPinViewDetails - Handler called!");
        
        var poi = _vm.SelectedPinPOI;
        if (poi == null)
        {
            Console.WriteLine("[MainPage] OnPinViewDetails - SelectedPinPOI is NULL!");
            return;
        }

        Console.WriteLine($"[MainPage] OnPinViewDetails - POI: {poi.Name_Vi}");

        // Ẩn popup
        _vm.IsPinPopupVisible = false;
        PinPopupComponent.IsVisible = false;

        try
        {
            // Điều hướng đến trang chi tiết POI
            Console.WriteLine("[MainPage] OnPinViewDetails - Creating POIDetailPage...");
            var detailPage = new POIDetailPage(poi);
            
            Console.WriteLine($"[MainPage] OnPinViewDetails - Navigation exists: {Navigation != null}");
            Console.WriteLine("[MainPage] OnPinViewDetails - Calling PushAsync...");
            
            await Navigation.PushAsync(detailPage);
            
            Console.WriteLine("[MainPage] OnPinViewDetails - Navigation SUCCESS!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MainPage] OnPinViewDetails - ERROR: {ex.Message}");
            Console.WriteLine($"[MainPage] OnPinViewDetails - Stack trace: {ex.StackTrace}");
        }
    }
}
