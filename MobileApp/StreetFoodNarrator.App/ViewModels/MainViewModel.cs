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
            });

        _geofence.OnPrimaryZoneChanged += zone =>
            MainThread.BeginInvokeOnMainThread(() =>
            {
                PrimaryZone = zone;
                PrimaryZoneName = zone?.Name_Vi ?? "—";
                PrimaryZoneType = zone?.ZoneType ?? "";
                PrimaryZoneDesc = zone?.Description_Vi ?? "Chưa vào khu vực nào.";
                PrimaryZoneEmoji = zone?.ZoneType switch
                {
                    "Area" => "🗺️",
                    "District" => "🏘️",
                    "Spot" => "📍",
                    _ => "📡"
                };

                // Simulate audio playing
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
            await _geofence.OnLocationChangedAsync(loc);
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

    // Cooldown display
    [ObservableProperty] private bool isCooldownActive = false;
    [ObservableProperty] private string cooldownMessage = "";

    // Settings drawer
    [ObservableProperty] private bool isSettingsOpen = false;

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
}
