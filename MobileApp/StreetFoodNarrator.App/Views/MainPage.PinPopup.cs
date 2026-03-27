// ─────────────────────────────────────────────────────────────────────────────
// MainPage.PinPopup.cs  –  Popup khi nhấn vào pin trên bản đồ:
//   • OnMapInfoTapped – tìm POI gần nhất tọa độ tap, hiện PinPopupCard
//   • OnPinPlayAudio  – phát audio của POI (offline cache ưu tiên)
//   • OnPinNavigate   – mở Google Maps dẫn đường
//   • OnPinSave       – lưu / bỏ lưu POI
// ─────────────────────────────────────────────────────────────────────────────

using Mapsui;
using Mapsui.Projections;
using StreetFoodNarrator.App.ViewModels;

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
                _vm.SelectedPinPOI = nearest;
                _vm.IsPinPopupVisible = false;
                PinPopupComponent.IsVisible = false;
            }
            else
            {
                _vm.SelectedPinPOI = null;
                _vm.IsPinPopupVisible = false;
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
        var poi = _vm.SelectedPinPOI;
        if (poi == null) return;
        _vm.ToggleSavePOICommand.Execute(poi);
        // POI không có INPC → reassign để BoolToColorConverter re-evaluate ngay
        _vm.SelectedPinPOI = null;
        _vm.SelectedPinPOI = poi;
        UpdateZonePins();
    }

    private void OnPinViewDetails(object? sender, EventArgs e)
    {
        Console.WriteLine("[MainPage] OnPinViewDetails - Opening Detail Mode...");
        
        var poi = _vm.SelectedPinPOI;
        if (poi == null)
        {
            Console.WriteLine("[MainPage] OnPinViewDetails - SelectedPinPOI is NULL!");
            return;
        }

        // Set the primary zone to the selected POI
        _vm.PrimaryZone = poi;
        // Switch to detail mode
        _vm.CurrentAppMode = MainViewModel.AppMode.Detail;
        
        // Cập nhật thủ công các trường vì Binding PrimaryZoneName có khi trễ
        _vm.PrimaryZoneName = poi.Name_Vi ?? poi.Name_En ?? "—";
        _vm.PrimaryZoneDesc = poi.Description_Vi ?? poi.Description_En ?? "Không có mô tả";
        _vm.PrimaryZoneAddress= poi.Address ?? "Địa chỉ đang cập nhật";
        _vm.PrimaryZoneRating = (poi.Rating ?? 4.5).ToString("F1");

        // Hide popup
        _vm.IsPinPopupVisible = false;
        PinPopupComponent.IsVisible = false;
    }

    // ── POI Card navigation (TabMapView redesign) ───────────────────────────

    private async void OnMapViewDetail(object? sender, Core.Models.POI poi)
    {
        if (poi == null) return;
        _vm.SelectedPinPOI = poi;
        _vm.PrimaryZone = poi;
        _vm.PrimaryZoneName = poi.Name_Vi ?? poi.Name_En ?? "—";
        _vm.PrimaryZoneDesc = poi.Description_Vi ?? poi.Description_En ?? "";
        _vm.PrimaryZoneAddress = poi.Address ?? "Đang cập nhật";
        _vm.PrimaryZoneRating = (poi.Rating ?? 4.5).ToString("F1");
        _vm.CurrentAppMode = MainViewModel.AppMode.Detail;

        // Stop audio and navigate to POIDetailPage
        await StopNarrationAsync(resetProgress: true, clearResumeState: true);
        await Shell.Current.Navigation.PushModalAsync(new POIDetailPage(poi));
    }

    private void OnMapPrevPoi(object? sender, Core.Models.POI poi)
    {
        var spots = _vm.FilteredPOIs.Where(p => p.ZoneType == "Spot").ToList();
        if (spots.Count == 0)
            spots = _vm.AllPOIs.Where(p => p.ZoneType == "Spot").ToList();
        if (spots.Count == 0) return;
        var idx = spots.FindIndex(p => p.Id == poi.Id);
        var prevIdx = (idx - 1 + spots.Count) % spots.Count;
        var prev = spots[prevIdx];
        _vm.SelectedPinPOI = prev;
        CenterMapOnPOI(prev);
    }

    private void OnMapNextPoi(object? sender, Core.Models.POI poi)
    {
        var spots = _vm.FilteredPOIs.Where(p => p.ZoneType == "Spot").ToList();
        if (spots.Count == 0)
            spots = _vm.AllPOIs.Where(p => p.ZoneType == "Spot").ToList();
        if (spots.Count == 0) return;
        var idx = spots.FindIndex(p => p.Id == poi.Id);
        var nextIdx = (idx + 1) % spots.Count;
        var next = spots[nextIdx];
        _vm.SelectedPinPOI = next;
        CenterMapOnPOI(next);
    }
}
