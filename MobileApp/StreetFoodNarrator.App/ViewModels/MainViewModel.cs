using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StreetFoodNarrator.App.Core.Models;
using StreetFoodNarrator.App.Core.Services;
using System.Collections.ObjectModel;

namespace StreetFoodNarrator.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IGeofenceService _geofence;
    private readonly ILocationService _location;
    private readonly IZoneRepository _repository;
    private readonly ILocalDatabaseService _db;
    private readonly SimulatedLocationService? _simulator;
    private readonly SimulatedLocationService _fallbackSimulator;
    private bool _isUsingFallback = false;

    public MainViewModel(
        IGeofenceService geofence,
        ILocationService location,
        IZoneRepository repository,
        ILocalDatabaseService db)
    {
        _geofence = geofence;
        _repository = repository;
        _location = location;
        _db = db;
        _simulator = location as SimulatedLocationService;
        _fallbackSimulator = new SimulatedLocationService();

        // Default to AppConfig location until GPS/sim updates arrive
        if (CurrentLat == 0 && CurrentLon == 0)
        {
            CurrentLat = StreetFoodNarrator.App.AppConfig.DefaultLatitude;
            CurrentLon = StreetFoodNarrator.App.AppConfig.DefaultLongitude;
            CoordDisplay = $"{CurrentLat:F6}, {CurrentLon:F6}";
        }

        // Wire events from geofence service
        _geofence.OnActiveZonesChanged += zones =>
            MainThread.BeginInvokeOnMainThread(() =>
            {
                ActiveZones.Clear();
                foreach (var z in zones) ActiveZones.Add(z);
                ActiveZoneCount = zones.Count;
                IsInsideAnyZone = zones.Any();
                IsApproaching = zones.Any();
                var first = zones.FirstOrDefault();
                ApproachingZoneName = first?.Name_Vi ?? ApproachingZoneName;
                ApproachingDistance = first != null ? $"{first.DistanceFromUser:F0}" : ApproachingDistance;

                // Sync distances back into AllPOIs for status-based pin colors
                foreach (var poi in AllPOIs)
                {
                    var match = zones.FirstOrDefault(z => z.Id == poi.Id);
                    if (match != null) poi.DistanceFromUser = match.DistanceFromUser;
                }
            });

        _geofence.OnPrimaryZoneChanged += zone =>
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (zone != null)
                {
                    PrimaryZone = zone;
                    PrimaryZoneName = zone.Name_Vi ?? "—";
                    PrimaryZoneType = zone.ZoneType ?? "";
                    PrimaryZoneDesc = zone.Description_Vi ?? "Không có mô tả.";
                    PrimaryZoneAddress = zone.Address ?? "Địa chỉ đang cập nhật";
                    PrimaryZoneEmoji = zone.ZoneType switch
                    {
                        "Area" => "🗺️",
                        "District" => "🏘️",
                        "Spot" => "📍",
                        _ => "📡"
                    };
                    PrimaryZoneRating = (zone.Rating ?? 4.5).ToString("F1");

                    // Tính current spot index (sắp xếp theo Id để thứ tự nhất quán)
                    var spots = AllPOIs.Where(p => p.ZoneType == "Spot").OrderBy(p => p.Id).ToList();
                    int idx = spots.FindIndex(p => p.Id == zone.Id);
                    if (idx >= 0)
                    {
                        CurrentStopIndex = idx + 1;
                        CurrentStopBadge = $"{idx + 1}/{spots.Count}";

                        // Distance from user to current spot
                        var curDist = HaversineDistance(CurrentLat, CurrentLon, zone.Latitude, zone.Longitude);
                        CurrentDistText = curDist < 1000 ? $"{curDist:F0}m" : $"{curDist / 1000:F1}km";
                        CurrentWalkTimeText = $"~{Math.Max(1, (int)Math.Ceiling(curDist / 80.0))} phút";

                        // Info chips from zone data
                        ChipCategory = $"🍽️ {zone.Category ?? zone.Type ?? "Ẩm thực"}";
                        CategoryName = zone.Category ?? zone.Type ?? "Ẩm thực";
                        var hours = zone.EstimatedHours ?? zone.OpeningHoursText;
                        HasOpenHours = !string.IsNullOrEmpty(hours);
                        ChipOpenHours = HasOpenHours ? $"🕐 {hours}" : "";
                        HasPrice = zone.AveragePrice.HasValue;
                        ChipPrice = HasPrice ? $"💰 {zone.AveragePrice:N0}đ" : "";

                        // Next stops with real distances
                        if (idx + 1 < spots.Count)
                        {
                            var next1 = spots[idx + 1];
                            NextStop1Index = idx + 2;
                            NextStop1Name = next1.Name_Vi ?? "Điểm tiếp theo";
                            var d1 = HaversineDistance(CurrentLat, CurrentLon, next1.Latitude, next1.Longitude);
                            NextStop1Dist = $"~{d1:F0}m • {Math.Max(1, (int)Math.Ceiling(d1 / 80.0))} phút";
                        }
                        if (idx + 2 < spots.Count)
                        {
                            var next2 = spots[idx + 2];
                            NextStop2Index = idx + 3;
                            NextStop2Name = next2.Name_Vi ?? "Điểm kế";
                            var d2 = HaversineDistance(CurrentLat, CurrentLon, next2.Latitude, next2.Longitude);
                            NextStop2Dist = $"~{d2:F0}m • {Math.Max(1, (int)Math.Ceiling(d2 / 80.0))} phút";
                        }
                    }
                }
                else
                {
                    PrimaryZone = null;
                    PrimaryZoneName = "—";
                    PrimaryZoneType = "";
                    PrimaryZoneDesc = "Hãy bắt đầu hành trình!";
                    PrimaryZoneAddress = "Đang cập nhật";
                    PrimaryZoneEmoji = "🗺️";
                    PrimaryZoneRating = "—";
                    CurrentStopBadge = "—";
                    CurrentStopIndex = 0;
                    NextStop1Index = 0;
                    NextStop2Index = 0;
                    CurrentDistText = "—";
                    CurrentWalkTimeText = "—";
                    ChipCategory = "🍽️ Ẩm thực";
                    CategoryName = "Ẩm thực";
                    HasOpenHours = false;
                    ChipOpenHours = "";
                    HasPrice = false;
                    ChipPrice = "";
                    NextStop1Name = "—";
                    NextStop2Name = "—";
                }

                // Mark as visited + simulate audio playing
                if (zone != null) VisitedPOIIds.Add(zone.Id);
                IsAudioPlaying = zone != null;
                IsNarrating = zone != null;
                if (zone != null)
                {
                    // Default audio duration if API does not provide one
                    int duration = 45; 
                    AudioDuration = $"{duration / 60}:{duration % 60:D2}";
                    AudioTimeElapsed = "0:00";
                    AudioProgress = 0.0;
                }
            });

        _geofence.OnStatusMessage += msg =>
            MainThread.BeginInvokeOnMainThread(() =>
            {
                StatusLog.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {msg}");
                if (StatusLog.Count > 50) StatusLog.RemoveAt(StatusLog.Count - 1);
                LatestStatus = msg;
            });

        Action<Microsoft.Maui.Devices.Sensors.Location> onLocationUpdateSync = loc =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                CurrentLat = loc.Latitude;
                CurrentLon = loc.Longitude;
                CoordDisplay = $"{loc.Latitude:F6}, {loc.Longitude:F6}";
                var actSim = _isUsingFallback ? _fallbackSimulator : _simulator;
                if (actSim != null)
                    SimStepLabel = $"Bước {actSim.CurrentStep}/{actSim.TotalSteps}: {actSim.CurrentLabel}";
            });
        };

        Func<Microsoft.Maui.Devices.Sensors.Location, Task> onLocationUpdateAsync = async loc =>
        {
            try
            {
                // Kiểm tra xem có cần Fallback sang Xem Ảo không (chỉ check nếu chưa dùng simulated nào)
                if (!_isUsingFallback && _simulator == null)
                {
                    var gate = new Microsoft.Maui.Devices.Sensors.Location(AppConfig.DefaultLatitude, AppConfig.DefaultLongitude);
                    var dist = Microsoft.Maui.Devices.Sensors.Location.CalculateDistance(loc, gate, DistanceUnits.Kilometers);
                    
                    if (dist > 1.0)
                    {
                        _isUsingFallback = true;
                        MainThread.BeginInvokeOnMainThread(async () =>
                        {
                            await _location.StopAsync();
                            IsSimulated = true;
                            LatestStatus = "Đã bật chế độ Xem Ảo (Tour Giả lập) vì khoảng cách > 1km";
                            await DisplayAlertAsync("Chuyển sang Xem Ảo", "Bạn đang ở ngoài khu vực Phố ẩm thực Vĩnh Khánh. Tính năng Tour đã được tự động chuyển sang chế độ Xem Ảo.", "OK");
                            await _fallbackSimulator.StartAsync();
                        });
                        return; // Bỏ qua tọa độ thực tế này
                    }
                }

                await _geofence.OnLocationChangedAsync(loc);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Geofence] Unhandled error: {ex.Message}");
            }
        };

        // Gắn event cho cả location thường và fallback simulator
        _location.OnLocationUpdated += onLocationUpdateSync;
        _location.OnLocationUpdated += async loc => await onLocationUpdateAsync(loc);

        _fallbackSimulator.OnLocationUpdated += onLocationUpdateSync;
        _fallbackSimulator.OnLocationUpdated += async loc => await onLocationUpdateAsync(loc);
    }

    private Task DisplayAlertAsync(string title, string msg, string cancel)
    {
        if (Application.Current?.MainPage != null)
            return Application.Current.MainPage.DisplayAlert(title, msg, cancel);
        return Task.CompletedTask;
    }

    /// <summary>Tính khoảng cách (mét) giữa 2 toạ độ theo công thức Haversine.</summary>
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

    // ── Observable Properties ─────────────────────────────────
    [ObservableProperty] private POI? primaryZone;
    [ObservableProperty] private string primaryZoneName = "—";
    [ObservableProperty] private string primaryZoneType = "";
    [ObservableProperty] private string primaryZoneDesc = "Hãy bắt đầu hành trình!";
    [ObservableProperty] private string primaryZoneEmoji = "🗺️";
    [ObservableProperty] private ObservableCollection<POI> activeZones = new();
    [ObservableProperty] private int activeZoneCount = 0;
    [ObservableProperty] private bool isInsideAnyZone = false;
    [ObservableProperty] private double currentLat;
    [ObservableProperty] private double currentLon;
    [ObservableProperty] private string coordDisplay = "Đang lấy vị trí...";
    [ObservableProperty] private string latestStatus = "Sẵn sàng.";
    [ObservableProperty] private string simStepLabel = "";
    [ObservableProperty] private bool isTracking = false;
    [ObservableProperty] private bool isSimulated = AppConfig.UseSimulatedGPS;
    [ObservableProperty] private ObservableCollection<string> statusLog = new();
    [ObservableProperty] private string sessionSummary = "—";

    // Audio playback UI
    [ObservableProperty] private bool isAudioPlaying = false;
    [ObservableProperty] private bool isNarrating = false;  // Controls mini player vs info cards in Tour tab
    [ObservableProperty] private double audioProgress = 0.0;
    [ObservableProperty] private string audioTimeElapsed = "0:00";
    [ObservableProperty] private string audioDuration = "0:00";
    [ObservableProperty] private string tourName = "Phố ẩm thực Vĩnh Khánh";
    [ObservableProperty] private bool isApproaching = false;
    [ObservableProperty] private string approachingZoneName = "";
    [ObservableProperty] private string approachingDistance = "0";
    [ObservableProperty] private string currentStopBadge = "—";
    [ObservableProperty] private string primaryZoneAddress = "Đang cập nhật";
    [ObservableProperty] private string primaryZoneRating = "4.8";
    [ObservableProperty] private string nextStop1Name = "Điểm kế 1";
    [ObservableProperty] private string nextStop1Dist = "~100m • 2 phút";
    [ObservableProperty] private string nextStop2Name = "Điểm kế 2";
    [ObservableProperty] private string nextStop2Dist = "~200m • 4 phút";
    [ObservableProperty] private bool isTourUiVisible = false;

    // Stop index numbers (1-based) for bottom sheet list
    [ObservableProperty] private int currentStopIndex = 0;
    [ObservableProperty] private int nextStop1Index = 0;
    [ObservableProperty] private int nextStop2Index = 0;

    // Info cards (collapsed sheet)
    [ObservableProperty] private string currentDistText = "—";
    [ObservableProperty] private string currentWalkTimeText = "—";

    // Info chips (expanded sheet)
    [ObservableProperty] private string chipCategory = "🍽️ Ẩm thực";
    [ObservableProperty] private string categoryName = "Ẩm thực";   // collapsed card (no emoji prefix)
    [ObservableProperty] private string chipOpenHours = "";
    [ObservableProperty] private bool hasOpenHours = false;
    [ObservableProperty] private string chipPrice = "";
    [ObservableProperty] private bool hasPrice = false;

    [ObservableProperty] private bool isCooldownActive = false;
    [ObservableProperty] private string cooldownMessage = "";

    // Settings drawer
    [ObservableProperty] private bool isSettingsOpen = false;

    // Trạng thái nguồn dữ liệu (cho offline badge)
    [ObservableProperty] private bool isOffline = false;
    [ObservableProperty] private string dataSourceLabel = "";

    // ── New: Browse & Map tabs ─────────────────────────────────
    [ObservableProperty] private ObservableCollection<POI> allPOIs = new();
    [ObservableProperty] private ObservableCollection<POI> filteredPOIs = new();
    [ObservableProperty] private ObservableCollection<POI> savedPOIs = new();
    [ObservableProperty] private string searchQuery = "";
    [ObservableProperty] private POI? selectedPinPOI;
    [ObservableProperty] private bool isPinPopupVisible = false;

    // ── Navigation routing ───────────────────────────────────────────────
    [ObservableProperty] private POI? navigationTarget;
    [ObservableProperty] private bool isVirtualNavigation = false;
    [ObservableProperty] private bool isVirtualTourActive = false;
    [ObservableProperty] private string virtualTourStatus = "";
    [ObservableProperty] private bool autoStartRequestedTour = false;

    public List<POI>? RequestedTourStops { get; set; }

    // Visited/saved sets (non-observable — used for map pin coloring)
    public HashSet<int> VisitedPOIIds { get; } = new();
    public HashSet<int> SavedPOIIds   { get; } = new();

    /// <summary>Auto-starts GPS tracking on page load. Safe to call multiple times.</summary>
    public async Task StartTrackingAsync()
    {
        if (IsTracking) return;
        IsTracking = true;
        LatestStatus = "📡 GPS đang chạy...";
        await _location.StartAsync();
    }

    // ── Commands ──────────────────────────────────────────────
    [RelayCommand]
    private async Task ToggleTrackingAsync()
    {
        var activeLoc = _isUsingFallback ? _fallbackSimulator : _location;

        if (!IsTracking)
        {
            await activeLoc.StartAsync();
            IsTracking = true;
            LatestStatus = _isUsingFallback ? "👣 Xem Ảo đang chạy..." : "📡 GPS đang chạy...";
        }
        else
        {
            await activeLoc.StopAsync();
            IsTracking = false;
            LatestStatus = "⏹ Đã dừng theo dõi.";
        }
    }

    [RelayCommand]
    private async Task SyncMongoAsync()
    {
        LatestStatus = "🔄 Đang sync dữ liệu...";
        await _repository.SyncFromMongoAsync();
        await _repository.LoadLocalAsync();
        RefreshDataSourceState();
        LatestStatus = $"✅ Sync hoàn tất — {_repository.GetAllActiveZones().Count} zones";
    }

    private void RefreshDataSourceState()
    {
        var src = _repository.CurrentDataSource;
        IsOffline = src != DataSourceKind.Unknown && src != DataSourceKind.LiveApi;
        DataSourceLabel = src switch
        {
            DataSourceKind.LiveApi      => "",
            DataSourceKind.SqliteCache  => "Offline — dữ liệu cache",
            DataSourceKind.BundledJson  => "Offline — dữ liệu bundled",
            DataSourceKind.MockFallback => "Offline — dữ liệu mẫu",
            _                           => ""
        };
    }

    [RelayCommand]
    private void StepSimulator()
    {
        var activeSim = _isUsingFallback ? _fallbackSimulator : _simulator;
        activeSim?.StepForward();
    }

    [RelayCommand]
    private async Task ResetSessionAsync()
    {
        _simulator?.Reset();
        _fallbackSimulator.Reset();
        if (_isUsingFallback) await _fallbackSimulator.StopAsync();
        else await _location.StopAsync();
        
        IsTracking = false;
        StatusLog.Clear();
        ActiveZones.Clear();
        ActiveZoneCount = 0;
        PrimaryZone = null;
        PrimaryZoneName = "—";
        PrimaryZoneDesc = "Phiên đã reset. Nhấn Start để bắt đầu lại.";
        LatestStatus = "🔄 Đã reset phiên.";
        IsAudioPlaying = false;
        IsSettingsOpen = false;
    }

    [RelayCommand]
    private void ToggleAudio()
    {
        IsAudioPlaying = !IsAudioPlaying;
    }

    [RelayCommand]
    private void CenterMap()
    {
        LatestStatus = "🗺 Đã căn giữa bản đồ";
    }

    [RelayCommand]
    private void ShowDetails()
    {
        LatestStatus = "📋 Chi tiết đang được phát triển";
    }

    [RelayCommand]
    private void ShowSettings()
    {
        IsSettingsOpen = true;
    }

    [RelayCommand]
    private void CloseSettings()
    {
        IsSettingsOpen = false;
    }

    // Placeholder commands for UI bindings (prev/next/bookmark/select)
    [RelayCommand]
    private void PrevStop()
    {
        // TODO: implement previous stop navigation
    }

    [RelayCommand]
    private void NextStop()
    {
        // TODO: implement next stop navigation
    }

    [RelayCommand]
    private void Bookmark()
    {
        // TODO: implement bookmark logic
    }

    [RelayCommand]
    private void SelectStop(int stopIndex)
    {
        // TODO: implement direct stop selection
    }

    // ── Browse / Map tab ─────────────────────────────────────────────────

    /// <summary>Load all active POIs into AllPOIs + FilteredPOIs.</summary>
    public async Task LoadAllPoisAsync()
    {
        Console.WriteLine("[MainViewModel] 🔄 LoadAllPoisAsync started...");
        
        // Đọc SQLite trước để UI hiện nhanh
        await _repository.LoadLocalAsync();

        // Luôn sync từ API để cập nhật dữ liệu và trạng thái kết nối.
        // SyncFromMongoAsync tự kiểm tra version → không tải thừa nếu dữ liệu chưa đổi.
        Console.WriteLine("[MainViewModel] Calling SyncFromMongoAsync to refresh data + connection state...");
        await _repository.SyncFromMongoAsync();

        var zones = _repository.GetAllActiveZones();
        Console.WriteLine($"[MainViewModel] Got {zones.Count} zones from repository");
        
        AllPOIs.Clear();
        foreach (var z in zones) AllPOIs.Add(z);
        Console.WriteLine($"[MainViewModel] Added {AllPOIs.Count} POIs to AllPOIs collection");
        
        ApplyFilter();

        // Cập nhật badge offline theo nguồn dữ liệu
        RefreshDataSourceState();

        // Load liked POIs từ SQLite để hiển thị SavedPOIs
        await LoadSavedPOIsAsync();

        Console.WriteLine($"[MainViewModel] ✓ LoadAllPoisAsync completed! Final AllPOIs.Count={AllPOIs.Count}");
    }

    /// <summary>Called automatically when SearchQuery changes.</summary>
    partial void OnSearchQueryChanged(string value) => ApplyFilter();

    public void ApplyFilter()
    {
        var q = (SearchQuery ?? "").Trim().ToLower();
        var results = AllPOIs.Where(p =>
            p.ZoneType == "Spot" &&
            (string.IsNullOrEmpty(q) ||
             (p.Name_Vi?.ToLower().Contains(q) == true) ||
             (p.SignatureDish?.ToLower().Contains(q) == true) ||
             (p.SignatureDishesJson?.ToLower().Contains(q) == true) ||
             (p.Description_Vi?.ToLower().Contains(q) == true) ||
             (p.Type?.ToLower().Contains(q) == true)));
        FilteredPOIs.Clear();
        foreach (var p in results) FilteredPOIs.Add(p);
    }

    public async Task LoadSavedPOIsAsync()
    {
        var liked = await _db.GetLikedPOIsAsync();
        SavedPOIs.Clear();
        SavedPOIIds.Clear();
        foreach (var poi in liked)
        {
            SavedPOIs.Add(poi);
            SavedPOIIds.Add(poi.Id);
        }
    }

    [RelayCommand]
    private async Task ToggleSavePOIAsync(POI poi)
    {
        if (poi == null) return;
        poi.IsLikedByUser = !poi.IsLikedByUser;
        await _db.SavePOIAsync(poi);

        if (poi.IsLikedByUser)
        {
            if (!SavedPOIs.Any(p => p.Id == poi.Id))
                SavedPOIs.Add(poi);
            SavedPOIIds.Add(poi.Id);
        }
        else
        {
            var item = SavedPOIs.FirstOrDefault(p => p.Id == poi.Id);
            if (item != null) SavedPOIs.Remove(item);
            SavedPOIIds.Remove(poi.Id);
        }

        // Rebuild FilteredPOIs so heart colors reflect the new liked state
        MainThread.BeginInvokeOnMainThread(ApplyFilter);
    }

    [RelayCommand]
    private void ShowPinPopup(POI poi)
    {
        SelectedPinPOI = poi;
        IsPinPopupVisible = poi != null;
    }

    [RelayCommand]
    private void ClosePinPopup()
    {
        IsPinPopupVisible = false;
        SelectedPinPOI = null;
    }

    /// <summary>
    /// 🔄 DEBUG: Reset database và fetch lại từ API (hoặc mock nếu offline).
    /// Sử dụng khi dữ liệu bị corrupt.
    /// </summary>
    public async Task ResetDatabaseAsync()
    {
        try
        {
            Console.WriteLine("[MainViewModel] 🔄 ResetDatabaseAsync started...");
            
            // Reset database và sync từ API
            await _repository.ResetAndSyncAsync();
            
            // Reload toàn bộ POIs vào UI
            AllPOIs.Clear();
            foreach (var poi in _repository.GetAllActiveZones())
            {
                AllPOIs.Add(poi);
            }
            
            RefreshDataSourceState();
            
            Console.WriteLine($"[MainViewModel] ✓ ResetDatabaseAsync completed! {AllPOIs.Count} POIs loaded, Source: {_repository.CurrentDataSource}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MainViewModel] ❌ ResetDatabaseAsync error: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] ResetDatabaseAsync error: {ex}");
        }
    }
}
