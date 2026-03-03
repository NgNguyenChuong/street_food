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
    private readonly SimulatedLocationService? _simulator;

    public MainViewModel(
        IGeofenceService geofence,
        ILocationService location,
        IZoneRepository repository)
    {
        _geofence = geofence;
        _repository = repository;
        _location = location;
        _simulator = location as SimulatedLocationService;

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
                PrimaryZone = zone;
                PrimaryZoneName = zone?.Name_Vi ?? "—";
                PrimaryZoneType = zone?.ZoneType ?? "";
                PrimaryZoneDesc = zone?.Description_Vi ?? "Chưa vào khu vực nào.";
                PrimaryZoneAddress = zone?.SignatureDish ?? "Địa chỉ đang cập nhật";
                PrimaryZoneEmoji = zone?.ZoneType switch
                {
                    "Area" => "🗺️",
                    "District" => "🏘️",
                    "Spot" => "📍",
                    _ => "📡"
                };

                // Mark as visited + simulate audio playing
                if (zone != null) VisitedPOIIds.Add(zone.Id);
                IsAudioPlaying = zone != null;
                if (zone != null)
                {
                    int duration = 45; // Mock duration
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

        _location.OnLocationUpdated += loc =>
            MainThread.BeginInvokeOnMainThread(() =>
            {
                CurrentLat = loc.Latitude;
                CurrentLon = loc.Longitude;
                CoordDisplay = $"{loc.Latitude:F6}, {loc.Longitude:F6}";
                if (_simulator != null)
                    SimStepLabel = $"Bước {_simulator.CurrentStep}/{_simulator.TotalSteps}: {_simulator.CurrentLabel}";
            });

        _location.OnLocationUpdated += async loc =>
        {
            try
            {
                await _geofence.OnLocationChangedAsync(loc);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Geofence] Unhandled error: {ex.Message}");
            }
        };
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
    [ObservableProperty] private double audioProgress = 0.0;
    [ObservableProperty] private string audioTimeElapsed = "0:00";
    [ObservableProperty] private string audioDuration = "0:00";
    [ObservableProperty] private string tourName = "Phố ẩm thực Vĩnh Khánh";
    [ObservableProperty] private bool isApproaching = true;
    [ObservableProperty] private string approachingZoneName = "Quán Ốc X - Sắp phát";
    [ObservableProperty] private string approachingDistance = "50";
    [ObservableProperty] private string currentStopBadge = "ĐIỂM 3 / 8";
    [ObservableProperty] private string primaryZoneAddress = "Đang cập nhật";
    [ObservableProperty] private string primaryZoneRating = "4.8";
    [ObservableProperty] private string nextStop1Name = "Điểm kế 1";
    [ObservableProperty] private string nextStop1Dist = "~100m • 2 phút";
    [ObservableProperty] private string nextStop2Name = "Điểm kế 2";
    [ObservableProperty] private string nextStop2Dist = "~200m • 4 phút";
    [ObservableProperty] private bool isTourUiVisible = false;

    [ObservableProperty] private bool isCooldownActive = false;
    [ObservableProperty] private string cooldownMessage = "";

    // Settings drawer
    [ObservableProperty] private bool isSettingsOpen = false;

    // ── New: Browse & Map tabs ─────────────────────────────────
    [ObservableProperty] private ObservableCollection<POI> allPOIs = new();
    [ObservableProperty] private ObservableCollection<POI> filteredPOIs = new();
    [ObservableProperty] private ObservableCollection<POI> savedPOIs = new();
    [ObservableProperty] private string searchQuery = "";
    [ObservableProperty] private POI? selectedPinPOI;
    [ObservableProperty] private bool isPinPopupVisible = false;

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
        if (!IsTracking)
        {
            await _location.StartAsync();
            IsTracking = true;
            LatestStatus = "📡 GPS đang chạy...";
        }
        else
        {
            await _location.StopAsync();
            IsTracking = false;
            LatestStatus = "⏹ GPS đã dừng.";
        }
    }

    [RelayCommand]
    private async Task SyncMongoAsync()
    {
        LatestStatus = "🔄 Đang sync dữ liệu...";
        await _repository.SyncFromMongoAsync();
        await _repository.LoadLocalAsync();
        LatestStatus = $"✅ Sync hoàn tất — {_repository.GetAllActiveZones().Count} zones";
    }

    [RelayCommand]
    private void StepSimulator()
    {
        _simulator?.StepForward();
    }

    [RelayCommand]
    private async Task ResetSessionAsync()
    {
        _simulator?.Reset();
        await _location.StopAsync();
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
        // Always load from SQLite first so we have real synced data
        await _repository.LoadLocalAsync();
        var zones = _repository.GetAllActiveZones();
        AllPOIs.Clear();
        foreach (var z in zones) AllPOIs.Add(z);
        ApplyFilter();
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
             (p.Type?.ToLower().Contains(q) == true)));
        FilteredPOIs.Clear();
        foreach (var p in results) FilteredPOIs.Add(p);
    }

    [RelayCommand]
    private void ToggleSavePOI(POI poi)
    {
        if (poi == null) return;
        if (SavedPOIIds.Contains(poi.Id))
        {
            SavedPOIIds.Remove(poi.Id);
            var item = SavedPOIs.FirstOrDefault(p => p.Id == poi.Id);
            if (item != null) SavedPOIs.Remove(item);
        }
        else
        {
            SavedPOIIds.Add(poi.Id);
            SavedPOIs.Add(poi);
        }
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
}
