using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StreetFoodNarrator.App.Converters;
using StreetFoodNarrator.App.Core.Models;
using StreetFoodNarrator.App.Core.Services;
using StreetFoodNarrator.App.Helpers;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Text.Json;

namespace StreetFoodNarrator.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    public sealed class TourListItem
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int EstimatedDurationMinutes { get; set; }
        public int PoiCount { get; set; }
        public string CoverImageUrl { get; set; } = "welcome_streetfood.png";
        public bool IsActive { get; set; } = true;
    }

    public enum AppMode
    {
        Explore,
        Real,
        Virtual,
        Detail
    }

    public enum ExploreState
    {
        Far,
        Near,
        InZone
    }

    public enum MapViewState
    {
        NearOverview,
        InZoneActive,
        InZoneMinimized
    }

    public sealed class ExplorePoiCard
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = "welcome_streetfood.png";
        public string DistanceText { get; set; } = "—";
        public string SuggestionInfoText { get; set; } = "— • 4.5★";
        public string PriceTierText { get; set; } = "$";
        public string AreaText { get; set; } = string.Empty;
        public string ShortDescription { get; set; } = string.Empty;
        public double DistanceMeters { get; set; }
        public string StatusText { get; set; } = string.Empty;
        public string MetaText { get; set; } = string.Empty;
        public string TagText { get; set; } = string.Empty;
        public string QuoteText { get; set; } = string.Empty;
        public double Rating { get; set; }
    }

    private readonly IGeofenceService _geofence;
    private readonly ILocationService _location;
    private readonly IZoneRepository _repository;
    private readonly ILocalDatabaseService _db;
    private readonly IReviewService _reviewService;
    private readonly SimulatedLocationService? _simulator;
    private readonly SimulatedLocationService _fallbackSimulator;
    private readonly List<ExplorePoiCard> _exploreDemoCards = new();
    private bool _isUsingFallback = false;

    public MainViewModel(
        IGeofenceService geofence,
        ILocationService location,
        IZoneRepository repository,
        ILocalDatabaseService db,
        IReviewService reviewService)
    {
        _geofence = geofence;
        _repository = repository;
        _location = location;
        _db = db;
        _reviewService = reviewService;
        _simulator = location as SimulatedLocationService;
        _fallbackSimulator = new SimulatedLocationService();
        IsReviewOnline = HasInternetAccess();
        IsNetworkOffline = !HasInternetAccess();
        IsOfflineHintDismissed = OfflineBannerSessionState.IsDismissed;
        Connectivity.Current.ConnectivityChanged += (_, _) =>
            MainThread.BeginInvokeOnMainThread(() =>
            {
                var hasInternet = HasInternetAccess();
                IsReviewOnline = hasInternet;
                IsNetworkOffline = !hasInternet;
            });

        // Default to AppConfig location until GPS/sim updates arrive
        if (CurrentLat == 0 && CurrentLon == 0)
        {
            CurrentLat = StreetFoodNarrator.App.AppConfig.DefaultLatitude;
            CurrentLon = StreetFoodNarrator.App.AppConfig.DefaultLongitude;
            CoordDisplay = $"{CurrentLat:F6}, {CurrentLon:F6}";
        }

        InitializeExploreDemoData();
        RefreshExploreExperience();

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

                RefreshExploreExperience();
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

                // Mark as visited and reset audio UI for the new zone.
                if (zone != null) VisitedPOIIds.Add(zone.Id);
                IsAudioPlaying = false;
                IsNarrating = false;
                if (zone != null)
                {
                    // Default audio duration if API does not provide one
                    int duration = 45; 
                    AudioDuration = $"{duration / 60}:{duration % 60:D2}";
                    AudioTimeElapsed = "0:00";
                    AudioProgress = 0.0;
                }
                else
                {
                    AudioDuration = "0:00";
                    AudioTimeElapsed = "0:00";
                    AudioProgress = 0.0;
                }

                RefreshExploreExperience();
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
                // If demo lock was turned on earlier, real/sim GPS updates should unlock it.
                if (IsExploreStateDemoLocked)
                    IsExploreStateDemoLocked = false;

                HasLocationFix = true;
                CurrentLat = loc.Latitude;
                CurrentLon = loc.Longitude;
                UpdateTrackingProximityState(loc.Latitude, loc.Longitude);
                RefreshExploreExperience();
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
                if (false && !_isUsingFallback && _simulator == null)
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

    private ILocationService GetActiveLocationService()
        => _isUsingFallback ? _fallbackSimulator : _location;

    private void UpdateTrackingProximityState(double lat, double lon)
    {
        var nearestSpot = AllPOIs
            .Where(p => p.ZoneType == "Spot")
            .OrderBy(p => HaversineDistance(lat, lon, p.Latitude, p.Longitude))
            .FirstOrDefault();

        if (nearestSpot == null)
        {
            GetActiveLocationService().SetTrackingState(TrackingProximityState.Far);
            return;
        }

        var nearestDistance = HaversineDistance(lat, lon, nearestSpot.Latitude, nearestSpot.Longitude);
        var inZoneRadius = Math.Max(nearestSpot.Radius, AppConfig.TrackingInsideMeters);
        var nextState = nearestDistance <= inZoneRadius
            ? TrackingProximityState.Inside
            : nearestDistance <= AppConfig.TrackingNearMeters
                ? TrackingProximityState.Near
                : TrackingProximityState.Far;

        GetActiveLocationService().SetTrackingState(nextState);
    }

    public void ReduceTrackingForBackground()
    {
        if (!IsTracking)
            return;

        GetActiveLocationService().SetTrackingState(TrackingProximityState.Far);
    }

    public void RestoreTrackingFromBackground()
    {
        if (!IsTracking)
            return;

        if (HasLocationFix && (CurrentLat != 0 || CurrentLon != 0))
            UpdateTrackingProximityState(CurrentLat, CurrentLon);
        else
            GetActiveLocationService().SetTrackingState(TrackingProximityState.Near);
    }

    // ── Observable Properties ─────────────────────────────────
    [ObservableProperty] private POI? primaryZone;
    [ObservableProperty] private string primaryZoneName = "—";
    [ObservableProperty] private string primaryZoneType = "";
    [ObservableProperty] private string primaryZoneDesc = "Hãy bắt đầu hành trình!";

    partial void OnPrimaryZoneChanged(POI? value)
    {
        // ✅ FIX 5: Don't auto-load menu/reviews on every swipe
        // Menu/Reviews only load when user explicitly switches to those tabs
        // Use LoadMenuForCurrentPOIAsync() and LoadReviewsForCurrentPOIAsync() instead
    }
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
    [ObservableProperty] private MapViewState viewState = MapViewState.NearOverview;
    [ObservableProperty] private int? playingPoiId = null;
    [ObservableProperty] private bool isAudioPaused = false;
    private bool _userManuallyExitedInZone;
    [ObservableProperty] private bool isSimulated = AppConfig.UseSimulatedGPS;
    [ObservableProperty] private bool hasLocationFix = false;
    [ObservableProperty] private AppMode currentAppMode = AppMode.Explore;
    [ObservableProperty] private ExploreState currentExploreState = ExploreState.Far;
    [ObservableProperty] private bool isExploreStateDemoLocked = false;
    [ObservableProperty] private bool isLegacyMapVisible = false;
    [ObservableProperty] private string exploreStateBadge = "LOCATION: REMOTE";
    [ObservableProperty] private string exploreHeadline = "Bạn đang ở xa khu ẩm thực";
    [ObservableProperty] private string exploreSubtitle = "Khám phá trước hành trình";
    [ObservableProperty] private string explorePrimaryActionText = "🎬 Xem thử tour";
    [ObservableProperty] private string exploreSecondaryActionText = "🗺️ Xem bản đồ";
    [ObservableProperty] private bool hasSecondaryExploreAction = true;
    [ObservableProperty] private string exploreHeroImage = "welcome_streetfood.png";
    [ObservableProperty] private ExplorePoiCard? featuredExplorePoi;
    [ObservableProperty] private ObservableCollection<ExplorePoiCard> nearbyExplorePois = new();
    [ObservableProperty] private bool isExploreAudioAvailable = false;
    [ObservableProperty] private bool isExploreAudioPlaying = false;
    [ObservableProperty] private double exploreAudioProgressValue = 0.32;
    [ObservableProperty] private string exploreAudioProgressText = "01:12";
    [ObservableProperty] private string exploreAudioQuote = "\"Hương vị phố xưa đang chờ bạn khám phá...\"";
    
    partial void OnCurrentAppModeChanged(AppMode value)
    {
        OnPropertyChanged(nameof(IsExploreMode));
        OnPropertyChanged(nameof(IsRealMode));
        OnPropertyChanged(nameof(IsVirtualMode));
        OnPropertyChanged(nameof(IsNotVirtualMode));
        OnPropertyChanged(nameof(IsDetailMode));
        OnPropertyChanged(nameof(IsRealOrVirtualMode));
        OnPropertyChanged(nameof(IsBottomNavVisible));
        OnPropertyChanged(nameof(IsExploreStateScreenVisible));
        OnPropertyChanged(nameof(IsLegacyExperienceVisible));
        OnPropertyChanged(nameof(IsMapModeVisible));
        OnPropertyChanged(nameof(IsFarHeroVisible));
    }

    partial void OnCurrentExploreStateChanged(ExploreState value)
    {
        if (value == ExploreState.InZone && !_userManuallyExitedInZone && ViewState == MapViewState.NearOverview)
            ViewState = MapViewState.InZoneActive;
        else if (value != ExploreState.InZone && ViewState != MapViewState.NearOverview)
        {
            _userManuallyExitedInZone = false;
            ViewState = MapViewState.NearOverview;
        }

        OnPropertyChanged(nameof(IsFarExploreState));
        OnPropertyChanged(nameof(IsNearExploreState));
        OnPropertyChanged(nameof(IsInZoneExploreState));
        OnPropertyChanged(nameof(IsExploreStateScreenVisible));
        OnPropertyChanged(nameof(IsFarHeroVisible));
    }

    partial void OnIsLegacyMapVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(IsExploreStateScreenVisible));
        OnPropertyChanged(nameof(IsLegacyExperienceVisible));
        OnPropertyChanged(nameof(IsMapModeVisible));
        OnPropertyChanged(nameof(IsFarHeroVisible));
    }

    partial void OnViewStateChanged(MapViewState value)
    {
        OnPropertyChanged(nameof(IsInZoneExploreState));
        OnPropertyChanged(nameof(IsInZoneMinimizedExploreState));
        OnPropertyChanged(nameof(IsInZoneDashboardVisible));
        OnPropertyChanged(nameof(MapHeaderBackIcon));
        OnPropertyChanged(nameof(IsNearExploreState));
    }

    public bool IsExploreMode => CurrentAppMode == AppMode.Explore;
    public bool IsRealMode => CurrentAppMode == AppMode.Real;
    public bool IsVirtualMode => CurrentAppMode == AppMode.Virtual;
    public bool IsNotVirtualMode => CurrentAppMode != AppMode.Virtual;
    public bool IsDetailMode => CurrentAppMode == AppMode.Detail;
    public bool IsRealOrVirtualMode => IsRealMode || IsVirtualMode;
    public bool IsBottomNavVisible => IsExploreMode || IsRealMode;
    public bool IsExploreStateScreenVisible => CurrentAppMode == AppMode.Explore && !IsLegacyMapVisible;
    public bool IsLegacyExperienceVisible => IsLegacyMapVisible || IsRealMode || IsVirtualMode || IsDetailMode;
    public bool IsFarExploreState => CurrentExploreState == ExploreState.Far;
    public bool IsNearExploreState => CurrentExploreState == ExploreState.Near;
    // MainPage does not have a dedicated minimized InZone layout.
    // Keep InZone content visible there to avoid blank screen after returning from ExploreMapPage.
    public bool IsInZoneExploreState => CurrentExploreState == ExploreState.InZone;
    public bool IsInZoneMinimizedExploreState => ViewState == MapViewState.InZoneMinimized;
    public bool IsInZoneDashboardVisible =>
        CurrentExploreState == ExploreState.InZone &&
        ViewState != MapViewState.InZoneMinimized;
    public string MapHeaderBackIcon => ViewState == MapViewState.InZoneActive ? "\U000F0140" : "\U000F004D";
    public bool IsFarHeroVisible => IsExploreStateScreenVisible && IsFarExploreState;
    /// <summary>True when the Explore screen is in Legacy map mode (user tapped "Xem bản đồ").</summary>
    public bool IsMapModeVisible => CurrentAppMode == AppMode.Explore && IsLegacyMapVisible;

    // Proximity banner in VirtualMode — true when user enters the food area geofence
    public bool IsVirtualProximityBannerVisible =>
        IsVirtualMode && (CurrentExploreState == ExploreState.Near || CurrentExploreState == ExploreState.InZone);

    partial void OnCurrentLatChanged(double value)
        => OnPropertyChanged(nameof(CurrentPoiDistanceText));

    partial void OnCurrentLonChanged(double value)
        => OnPropertyChanged(nameof(CurrentPoiDistanceText));

    /// <summary>Fires when user taps "Chuyển sang Real Mode" in the proximity banner.</summary>
    public event EventHandler? SwitchToRealModeRequested;

    [RelayCommand]
    public void SwitchToRealModeFromVirtual() => SwitchToRealModeRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void MinimizeInZone()
    {
        if (CurrentExploreState == ExploreState.InZone)
        {
            _userManuallyExitedInZone = true;
            ViewState = MapViewState.InZoneMinimized;
        }
    }

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
    [ObservableProperty] private ObservableCollection<TourListItem> allTours = new();
    [ObservableProperty] private ObservableCollection<TourListItem> filteredTours = new();
    [ObservableProperty] private string searchQuery = "";
    [ObservableProperty] private string tourSearchQuery = "";
    [ObservableProperty] private ObservableCollection<POI> searchSuggestions = new();
    [ObservableProperty] private bool hasSearchText = false;
    [ObservableProperty] private bool isToursLoading = false;
    [ObservableProperty] private bool isTourDataStale = false;
    [ObservableProperty] private ObservableCollection<POI> virtualTourPOIs = new();
    [ObservableProperty] private bool isVirtualTourPopupShown = false;
    [ObservableProperty] private POI? selectedPinPOI;
    [ObservableProperty] private bool isPinPopupVisible = false;

    partial void OnSelectedPinPOIChanged(POI? value)
    {
        OnPropertyChanged(nameof(CurrentPOI));
        OnPropertyChanged(nameof(IsMapPoiCardVisible));
        OnPropertyChanged(nameof(CurrentPoiDistanceText));
    }

    /// <summary>Alias for SelectedPinPOI — used by TabMapView POI card bindings.</summary>
    public POI? CurrentPOI
    {
        get => SelectedPinPOI;
        set => SelectedPinPOI = value;
    }

    public bool IsMapPoiCardVisible => SelectedPinPOI != null;
    public string CurrentPoiDistanceText
    {
        get
        {
            if (SelectedPinPOI == null)
                return "0.2 km";

            var hasLocation = CurrentLat != 0 && CurrentLon != 0;
            var fromLat = hasLocation ? CurrentLat : AppConfig.DefaultLatitude;
            var fromLon = hasLocation ? CurrentLon : AppConfig.DefaultLongitude;
            var distanceMeters = HaversineDistance(fromLat, fromLon, SelectedPinPOI.Latitude, SelectedPinPOI.Longitude);

            return distanceMeters < 1000
                ? $"{distanceMeters:F0} m"
                : $"{distanceMeters / 1000:F1} km";
        }
    }

    // ── Categories & Filters ──────────────────────────────────
    [ObservableProperty] private ObservableCollection<string> categories = new();
    [ObservableProperty] private string selectedCategory = "Tất cả";
    private readonly object _applyFilterLock = new();
    private CancellationTokenSource? _applyFilterCts;
    private readonly object _tourSyncLock = new();
    private bool _isTourSyncInFlight;
    private readonly SemaphoreSlim _liveSyncGate = new(1, 1);
    private bool _isLiveSyncInFlight;
    private const string ToursCacheJsonKey = "tours_cache_json_v1";
    private const string LastTourSyncTimeKey = "LastTourSyncTime";
    private const string LastPoiSyncTimeKey = "LastSyncTime";
    private static readonly JsonSerializerOptions TourJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    partial void OnSelectedCategoryChanged(string value)
    {
        _ = ApplyFilterAsync();
    }

    partial void OnTourSearchQueryChanged(string value)
    {
        ApplyTourFilterCore();
    }

    /// <summary>
    /// Builds the map filter categories list from loaded POI data.
    /// Called once after POIs are loaded. "Tất cả" is always first.
    /// Fires CategoriesUpdated so UI can refresh chip highlights.
    /// </summary>
    public event EventHandler? CategoriesUpdated;

    public void BuildMapCategories()
    {
        var cats = AllPOIs
            .Where(p => p.ZoneType == "Spot" && !string.IsNullOrWhiteSpace(p.Type))
            .Select(p => p.Type!)
            .Distinct()
            .OrderBy(c => c)
            .ToList();

        var nextCategories = new List<string> { "Tất cả" };
        nextCategories.AddRange(cats);

        void apply()
        {
            Categories = new ObservableCollection<string>(nextCategories);

            // Reset to "Tất cả" if current selection is no longer valid
            if (!nextCategories.Contains(SelectedCategory))
                SelectedCategory = "Tất cả";

            // Notify UI that categories are ready so chip highlights can be refreshed
            CategoriesUpdated?.Invoke(this, EventArgs.Empty);
        }

        if (MainThread.IsMainThread) apply();
        else MainThread.BeginInvokeOnMainThread(apply);
    }

    // ── Navigation routing ───────────────────────────────────────────────
    [ObservableProperty] private POI? navigationTarget;
    [ObservableProperty] private bool isVirtualNavigation = false;
    [ObservableProperty] private bool isVirtualTourActive = false;
    [ObservableProperty] private string virtualTourStatus = "";
    [ObservableProperty] private bool autoStartRequestedTour = false;
    [ObservableProperty] private string virtualTourCurrentTab = "INFO";

    // ── Journal / Virtual Food Tour Properties ─────────────────────────────
    [ObservableProperty] private string journalZoneLabel = "VĨNH KHÁNH";
    [ObservableProperty] private string journalTourTitle = "Khám phá Vĩnh Khánh";
    [ObservableProperty] private int journalCurrentCount = 0;
    [ObservableProperty] private int journalTotalCount = 0;
    [ObservableProperty] private double journalProgressFraction = 0;

    // Currently Playing
    [ObservableProperty] private string journalCurrentlyPlayingName = "Phở Hà Nội";
    [ObservableProperty] private string journalCurrentlyPlayingDesc = "Khám phá bí mật nước dùng trong 12 giờ của gian hàng 40 năm tuổi này.";
    [ObservableProperty] private string journalCurrentlyPlayingImage = "welcome_streetfood.png";
    [ObservableProperty] private POI? journalCurrentlyPlayingPoi;

    // Queue: remaining stops after the currently playing one
    [ObservableProperty] private ObservableCollection<POI> journalQueueItems = new();

    private const string JournalMilestonePopupDateKey = "journal_milestone_popup_last_date";
    private const int MaxVirtualQueueItems = 6;
    private readonly HashSet<int> _journalCompletedPoiIds = new();
    private void ReplaceJournalQueueItems(IEnumerable<POI> queueItems)
    {
        var snapshot = queueItems.ToList();

        void apply()
        {
            // Replace whole collection instead of Clear/Add to avoid ObservableCollection re-entrancy crashes.
            JournalQueueItems = new ObservableCollection<POI>(snapshot);
        }

        if (MainThread.IsMainThread) apply();
        else MainThread.BeginInvokeOnMainThread(apply);
    }

    /// <summary>Populates Journal properties from the current zone and tour state.</summary>
    public void RefreshJournalState()
    {
        // Update queue duration converter with current user location
        JournalQueueDurationConverter.UpdateUserLocation(CurrentLat, CurrentLon);

        var spots = AllPOIs.Where(p => p.ZoneType == "Spot").OrderBy(p => p.Id).ToList();
        if (spots.Count == 0)
        {
            JournalTotalCount = 0;
            JournalCurrentCount = 0;
            JournalProgressFraction = 0;
            ReplaceJournalQueueItems(Array.Empty<POI>());
            _journalCompletedPoiIds.Clear();
            return;
        }

        JournalTotalCount = spots.Count;
        var activeSpotIds = spots.Select(p => p.Id).ToHashSet();
        _journalCompletedPoiIds.RemoveWhere(id => !activeSpotIds.Contains(id));
        JournalCurrentCount = Math.Min(_journalCompletedPoiIds.Count, JournalTotalCount);
        JournalProgressFraction = JournalTotalCount > 0
            ? (double)JournalCurrentCount / JournalTotalCount
            : 0;

        // Determine currently playing index (PrimaryZone if set, otherwise first spot)
        int currentIdx = 0;
        if (PrimaryZone != null)
        {
            var found = spots.FindIndex(p => p.Id == PrimaryZone.Id);
            if (found >= 0) currentIdx = found;
        }

        // Currently playing POI
        var current = spots[currentIdx];
        JournalCurrentlyPlayingName = current.Name_Vi ?? current.Name_En ?? "—";
        JournalCurrentlyPlayingDesc = current.Description_Vi ?? current.FunFact ?? "";
        JournalCurrentlyPlayingImage = current.DisplayImageUrl;
        JournalCurrentlyPlayingPoi = current;

        // Queue: show all remaining POIs in order (wrap-around)
        var queueItems = new List<POI>();
        var queueCount = Math.Min(MaxVirtualQueueItems, Math.Max(0, spots.Count - 1));
        for (var i = 1; i <= queueCount; i++)
        {
            var idx = (currentIdx + i) % spots.Count;
            queueItems.Add(spots[idx]);
        }

        ReplaceJournalQueueItems(queueItems);
    }

    /// <summary>
    /// Refreshes only the journal queue (JournalQueueItems) without touching the
    /// currently-playing card. Used when user taps a queue item — the audio switches
    /// but the hero card should NOT update until the audio actually starts playing.
    /// </summary>
    public void RefreshJournalQueueOnly()
    {
        var spots = AllPOIs.Where(p => p.ZoneType == "Spot").OrderBy(p => p.Id).ToList();
        var queueItems = new List<POI>();
        if (spots.Count == 0)
        {
            ReplaceJournalQueueItems(Array.Empty<POI>());
            return;
        }

        int currentIdx = 0;
        if (PrimaryZone != null)
        {
            var found = spots.FindIndex(p => p.Id == PrimaryZone.Id);
            if (found >= 0) currentIdx = found;
        }

        var queueCount = Math.Min(MaxVirtualQueueItems, Math.Max(0, spots.Count - 1));
        for (var i = 1; i <= queueCount; i++)
        {
            var idx = (currentIdx + i) % spots.Count;
            queueItems.Add(spots[idx]);
        }

        ReplaceJournalQueueItems(queueItems);
    }

    public bool MarkJournalPoiCompleted(POI? poi)
    {
        if (poi == null || poi.ZoneType != "Spot")
            return false;

        var added = _journalCompletedPoiIds.Add(poi.Id);
        if (!added)
            return false;

        JournalCurrentCount = Math.Min(_journalCompletedPoiIds.Count, JournalTotalCount);
        JournalProgressFraction = JournalTotalCount > 0
            ? (double)JournalCurrentCount / JournalTotalCount
            : 0;

        return true;
    }

    public bool HasCompletedJournalCycle()
        => JournalTotalCount > 0 && _journalCompletedPoiIds.Count >= JournalTotalCount;

    public void ResetJournalCycleProgress()
    {
        _journalCompletedPoiIds.Clear();
        JournalCurrentCount = 0;
        JournalProgressFraction = 0;
    }

    public bool ShouldShowJournalMilestonePopup()
    {
        if (JournalCurrentCount < 4)
            return false;

        var notInsideZone = !IsInsideAnyZone && CurrentExploreState != ExploreState.InZone;
        if (!notInsideZone)
            return false;

        var today = DateTime.Now.ToString("yyyy-MM-dd");
        var lastShownDate = Preferences.Get(JournalMilestonePopupDateKey, string.Empty);
        return !string.Equals(today, lastShownDate, StringComparison.Ordinal);
    }

    public void MarkJournalMilestonePopupShownToday()
    {
        Preferences.Set(JournalMilestonePopupDateKey, DateTime.Now.ToString("yyyy-MM-dd"));
    }

    [RelayCommand]
    private async Task SwitchVirtualTab(string tab)
    {
        VirtualTourCurrentTab = tab;
        if (tab == "REVIEWS" && PrimaryZone != null)
        {
            // ✅ FIX 5: Use cache-based load
            await LoadReviewsForCurrentPOIAsync();
        }
        else if (tab == "MENU" && PrimaryZone != null)
        {
            // ✅ FIX 5: Use cache-based load
            await LoadMenuForCurrentPOIAsync();
        }
    }

    [ObservableProperty] private ObservableCollection<MenuItemDto> virtualMenuItems = new();
    [ObservableProperty] private ObservableCollection<Review> poiReviews = new();

    // Menu & Reviews caches for virtual tour (FIX 5: swipe optimization)
    private Dictionary<int, List<MenuItemDto>> _menuCache = new();
    private Dictionary<int, List<Review>> _reviewCache = new();
    [ObservableProperty] private ObservableCollection<Review> visiblePoiReviews = new();
    [ObservableProperty] private int userRating = 0;
    [ObservableProperty] private string userComment = "";
    [ObservableProperty] private bool isSubmittingReview = false;
    [ObservableProperty] private bool isReviewOnline = true;
    [ObservableProperty] private bool isNetworkOffline = false;
    [ObservableProperty] private bool isOfflineHintDismissed = false;
    [ObservableProperty] private string reviewSubmitMessage = "";
    [ObservableProperty] private bool hasMoreReviews = false;
    [ObservableProperty] private string loadMoreReviewsText = "Xem thêm";

    private int _visibleReviewCount = 5;

    private static bool HasInternetAccess()
    {
        var access = Connectivity.Current.NetworkAccess;
        return access == NetworkAccess.Internet || access == NetworkAccess.ConstrainedInternet;
    }

    public bool ShowOfflineBanner => IsNetworkOffline && !OfflineBannerSessionState.IsDismissed;

    partial void OnIsNetworkOfflineChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowOfflineBanner));
    }

    partial void OnIsOfflineHintDismissedChanged(bool value)
        => OnPropertyChanged(nameof(ShowOfflineBanner));

    [RelayCommand]
    private void DismissOfflineBanner()
    {
        IsOfflineHintDismissed = true;
        OfflineBannerSessionState.IsDismissed = true;
        OnPropertyChanged(nameof(ShowOfflineBanner));
    }

    public void RefreshOfflineBannerSession()
    {
        IsOfflineHintDismissed = OfflineBannerSessionState.IsDismissed;
        OnPropertyChanged(nameof(ShowOfflineBanner));
    }

    private void InitializeExploreDemoData()
    {
        if (_exploreDemoCards.Count > 0) return;

        _exploreDemoCards.Add(new ExplorePoiCard
        {
            Id = 9001,
            Name = "Phở Hà Nội",
            ImageUrl = "welcome_streetfood.png",
            DistanceMeters = 300,
            DistanceText = "300m",
            SuggestionInfoText = "300M • 4.9★",
            PriceTierText = "$$",
            AreaText = "Quận 4",
            ShortDescription = "Nước dùng ngọt thanh, thơm quế hồi, chuẩn vị phố cổ.",
            StatusText = "Đang mở cửa",
            MetaText = "4.9 (2K+) • Phở gia truyền",
            TagText = "Món ngon gần nhất",
            QuoteText = "\"Nước dùng ngọt thanh, thơm quế hồi giữa lòng phố cổ.\"",
            Rating = 4.9
        });

        _exploreDemoCards.Add(new ExplorePoiCard
        {
            Id = 9002,
            Name = "Bún bò Nam Bộ",
            ImageUrl = "welcome_streetfood.png",
            DistanceMeters = 120,
            DistanceText = "120m",
            SuggestionInfoText = "120M • 4.8★",
            PriceTierText = "$",
            AreaText = "Quận 4",
            ShortDescription = "Bún bò trộn vị đậm đà, lạc rang giòn và rau thơm tươi.",
            StatusText = "4.8 ★",
            MetaText = "Bò xào • Rau thơm • Nước mắm chua ngọt",
            TagText = "Gần bạn còn",
            QuoteText = "\"Một tô bún đậm vị, đủ mềm, đủ thơm và rất Hà Nội.\"",
            Rating = 4.8
        });

        _exploreDemoCards.Add(new ExplorePoiCard
        {
            Id = 9003,
            Name = "Ốc Vũ Hải Phòng",
            ImageUrl = "welcome_streetfood.png",
            DistanceMeters = 350,
            DistanceText = "350m",
            SuggestionInfoText = "350M • 4.5★",
            PriceTierText = "$$",
            AreaText = "Quận 4",
            ShortDescription = "Ốc cay, nước chấm đậm vị, ăn là nhớ.",
            StatusText = "4.5 ★",
            MetaText = "Ốc cay • Nước chấm đậm vị",
            TagText = "Gần bạn còn",
            QuoteText = "\"Một điểm dừng thú vị cho ai mê vị cay và hải sản.\"",
            Rating = 4.5
        });
    }

    private void RefreshExploreExperience()
    {
        var resolvedState = IsExploreStateDemoLocked ? CurrentExploreState : ResolveExploreState();
        var candidateCards = IsExploreStateDemoLocked
            ? _exploreDemoCards.ToList()
            : BuildLiveExploreCards(resolvedState);

        if (candidateCards.Count == 0)
        {
            candidateCards = _exploreDemoCards.ToList();
        }

        var featured = candidateCards.FirstOrDefault();
        var nearby = BuildNearbyExploreSuggestions(resolvedState, featured, candidateCards);

        CurrentExploreState = resolvedState;
        ApplyExploreStateContent(resolvedState, featured, nearby);
    }

    public void RefreshExploreState()
    {
        RefreshExploreExperience();
    }

    private ExploreState ResolveExploreState()
    {
        if (HasLocationFix)
        {
            var nearestSpot = AllPOIs
                .Where(p => p.ZoneType == "Spot")
                .OrderBy(p => HaversineDistance(CurrentLat, CurrentLon, p.Latitude, p.Longitude))
                .FirstOrDefault();

            if (nearestSpot != null)
            {
                var nearestDistance = HaversineDistance(CurrentLat, CurrentLon, nearestSpot.Latitude, nearestSpot.Longitude);
                var inZoneRadius = Math.Max(nearestSpot.Radius, AppConfig.InZoneRadiusMeters);

                if (nearestDistance <= inZoneRadius)
                {
                    return ExploreState.InZone;
                }

                if (nearestDistance <= AppConfig.NearRadiusMeters)
                {
                    return ExploreState.Near;
                }

                // Guard against stale/misaligned POI coordinates:
                // fallback to food-street center distance before declaring Far.
                var centerDistance = HaversineDistance(
                    CurrentLat, CurrentLon,
                    AppConfig.DefaultLatitude, AppConfig.DefaultLongitude);
                if (centerDistance <= AppConfig.FallbackInZoneMeters)
                    return ExploreState.InZone;
                if (centerDistance <= AppConfig.FallbackNearMeters)
                    return ExploreState.Near;

                return ExploreState.Far;
            }

            // If spots are not available yet, estimate by configured food-street center.
            var fallbackCenterDistance = HaversineDistance(
                CurrentLat, CurrentLon,
                AppConfig.DefaultLatitude, AppConfig.DefaultLongitude);
            if (fallbackCenterDistance <= AppConfig.FallbackInZoneMeters)
                return ExploreState.InZone;
            if (fallbackCenterDistance <= AppConfig.FallbackNearMeters)
                return ExploreState.Near;
        }

        if (PrimaryZone != null || IsInsideAnyZone)
        {
            return ExploreState.InZone;
        }

        if (IsApproaching || ActiveZones.Any())
        {
            return ExploreState.Near;
        }

        return ExploreState.Far;
    }

    private List<ExplorePoiCard> BuildLiveExploreCards(ExploreState resolvedState)
    {
        var source = AllPOIs
            .Where(p => p.ZoneType == "Spot")
            .Select(p => CreateExploreCardFromPoi(p))
            .Where(card => card != null)
            .Cast<ExplorePoiCard>()
            .OrderBy(card => card.DistanceMeters)
            .ToList();

        if (resolvedState == ExploreState.InZone && PrimaryZone != null)
        {
            var primaryCard = CreateExploreCardFromPoi(PrimaryZone);
            if (primaryCard != null)
            {
                source.RemoveAll(card => card.Id == primaryCard.Id);
                source.Insert(0, primaryCard);
            }
        }

        return source;
    }

    private List<ExplorePoiCard> BuildNearbyExploreSuggestions(
        ExploreState state,
        ExplorePoiCard? featured,
        IReadOnlyCollection<ExplorePoiCard> candidateCards)
    {
        if (state == ExploreState.Near && !IsExploreStateDemoLocked && featured != null)
        {
            var featuredPoi = AllPOIs.FirstOrDefault(p => p.ZoneType == "Spot" && p.Id == featured.Id);
            if (featuredPoi != null)
            {
                var nearbyFromFeatured = AllPOIs
                    .Where(p => p.ZoneType == "Spot" && p.Id != featuredPoi.Id)
                    .Select(p => new
                    {
                        Poi = p,
                        DistanceFromFeatured = HaversineDistance(
                            featuredPoi.Latitude,
                            featuredPoi.Longitude,
                            p.Latitude,
                            p.Longitude)
                    })
                    .OrderBy(x => x.DistanceFromFeatured)
                    .Take(8)
                    .Select(x => CreateExploreCardFromPoi(x.Poi, x.DistanceFromFeatured))
                    .Where(card => card != null)
                    .Cast<ExplorePoiCard>()
                    .Take(2)
                    .ToList();

                if (nearbyFromFeatured.Count > 0)
                {
                    return nearbyFromFeatured;
                }
            }
        }

        return candidateCards
            .Where(card => featured == null || card.Id != featured.Id)
            .Take(2)
            .ToList();
    }

    private ExplorePoiCard? CreateExploreCardFromPoi(POI? poi, double? distanceOverrideMeters = null)
    {
        if (poi == null) return null;

        var distanceMeters = distanceOverrideMeters ?? (poi.DistanceFromUser > 0
            ? poi.DistanceFromUser
            : HaversineDistance(CurrentLat, CurrentLon, poi.Latitude, poi.Longitude));
        var priceTier = BuildPriceTierText(poi.AveragePrice);
        var ratingText = BuildRatingBadgeText(poi.Rating ?? 4.7);
        var hours = poi.OpeningHoursText ?? poi.EstimatedHours;
        var metaParts = new List<string>();

        if (poi.Rating.HasValue)
        {
            metaParts.Add($"{poi.Rating.Value:F1} ★");
        }

        if (!string.IsNullOrWhiteSpace(hours))
        {
            metaParts.Add(hours);
        }
        else if (poi.AveragePrice.HasValue)
        {
            metaParts.Add($"{poi.AveragePrice.Value:N0}đ");
        }

        if (!string.IsNullOrWhiteSpace(poi.SignatureDish))
        {
            metaParts.Add(poi.SignatureDish);
        }

        return new ExplorePoiCard
        {
            Id = poi.Id,
            Name = poi.Name_Vi ?? "Địa điểm ẩm thực",
            ImageUrl = poi.DisplayImageUrl,
            DistanceMeters = distanceMeters,
            DistanceText = FormatDistance(distanceMeters),
            SuggestionInfoText = $"{FormatSuggestionDistance(distanceMeters)} • {ratingText}",
            PriceTierText = priceTier,
            AreaText = ExtractAreaText(poi.Address),
            ShortDescription = BuildExploreDescriptionSnippet(poi),
            StatusText = !string.IsNullOrWhiteSpace(hours) ? "Đang mở cửa" : "Sẵn sàng khám phá",
            MetaText = string.Join(" • ", metaParts.Where(part => !string.IsNullOrWhiteSpace(part))),
            TagText = poi.Category ?? poi.Type ?? "Ẩm thực đường phố",
            QuoteText = BuildExploreQuote(poi),
            Rating = poi.Rating ?? 4.7
        };
    }

    private static string BuildExploreQuote(POI poi)
    {
        var raw = poi.Description_Vi ?? poi.FunFact ?? poi.SignatureDish ?? "Một hương vị đáng thử trên hành trình của bạn.";
        var trimmed = raw.Length > 88 ? $"{raw[..88].Trim()}..." : raw.Trim();
        return $"\"{trimmed}\"";
    }

    private static string BuildExploreDescriptionSnippet(POI poi)
    {
        var raw = poi.Description_Vi ?? poi.FunFact ?? poi.SignatureDish ?? "Một quán nổi bật đáng để khám phá.";
        var normalized = raw.Replace("\r", " ").Replace("\n", " ").Trim();
        return normalized.Length > 120 ? $"{normalized[..120].Trim()}..." : normalized;
    }

    private static string BuildPriceTierText(decimal? averagePrice)
    {
        if (!averagePrice.HasValue || averagePrice.Value <= 0) return "$";
        if (averagePrice.Value <= 50000) return "$";
        if (averagePrice.Value <= 120000) return "$$";
        return "$$$";
    }

    private static string BuildRatingBadgeText(double rating)
    {
        if (rating <= 0)
            rating = 4.5;

        return $"{rating:F1}★";
    }

    private static string FormatSuggestionDistance(double meters)
    {
        if (meters <= 0) return "—";
        if (meters < 1000) return $"{meters:F0}M";
        return $"{meters / 1000:F1}KM";
    }

    private static string ExtractAreaText(string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
            return "Khu ẩm thực";

        var parts = address.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
            return parts[1];

        return address.Trim();
    }

    private void ApplyExploreStateContent(
        ExploreState state,
        ExplorePoiCard? featured,
        IReadOnlyCollection<ExplorePoiCard> nearby)
    {
        switch (state)
        {
            case ExploreState.Far:
                ExploreStateBadge = "LOCATION: REMOTE";
                ExploreHeadline = "Bạn đang ở xa khu ẩm thực";
                ExploreSubtitle = "Khám phá trước hành trình";
                ExplorePrimaryActionText = "🎬 Xem thử tour";
                ExploreSecondaryActionText = "🗺️ Xem bản đồ";
                HasSecondaryExploreAction = true;
                IsExploreAudioAvailable = false;
                IsExploreAudioPlaying = false;
                break;

            case ExploreState.Near:
                ExploreStateBadge = "GPS ACTIVE • NEAR MODE";
                ExploreHeadline = "Bạn đang đến gần khu ẩm thực";
                ExploreSubtitle = "Khám phá hương vị bản địa ngay tại vị trí của bạn.";
                ExplorePrimaryActionText = "Bắt đầu khám phá";
                ExploreSecondaryActionText = "🗺️ Xem bản đồ";
                HasSecondaryExploreAction = false;
                IsExploreAudioAvailable = false;
                IsExploreAudioPlaying = false;
                break;

            default:
                ExploreStateBadge = "GPS ACTIVE • IN-ZONE";
                ExploreHeadline = "Ứng dụng đã nhận diện khu ẩm thực";
                ExploreSubtitle = "Cùng bạn đồng hành trong chuyến đi thưởng thức và tìm hiểu ẩm thực đặc trưng vùng Vĩnh Khánh .";
                ExplorePrimaryActionText = "➡️ Chỉ đường";
                ExploreSecondaryActionText = "🗺️ Xem bản đồ";
                HasSecondaryExploreAction = true;
                IsExploreAudioAvailable = true;
                IsExploreAudioPlaying = IsAudioPlaying || IsNarrating;
                break;
        }

        FeaturedExplorePoi = featured;
        ExploreHeroImage = featured?.ImageUrl ?? "welcome_streetfood.png";
        ExploreAudioQuote = featured?.QuoteText ?? "\"Hương vị phố xưa đang chờ bạn khám phá...\"";
        ExploreAudioProgressValue = AudioProgress > 0 ? AudioProgress : 0.32;
        ExploreAudioProgressText = AudioDuration != "0:00" ? AudioDuration : "01:12";

        // Replace collection instance to avoid ObservableCollection re-entrancy
        // when location/geofence updates arrive during UI CollectionChanged handling.
        NearbyExplorePois = new ObservableCollection<ExplorePoiCard>(nearby);
    }

    private static string FormatDistance(double meters)
    {
        if (meters <= 0) return "—";
        return meters < 1000 ? $"{meters:F0}m" : $"{meters / 1000:F1}km";
    }

    private async Task LoadPoiReviewsAsync(int poiId)
    {
        var serverReviews = await _reviewService.GetReviewsAsync(poiId);
        var localReviews = await _db.GetReviewsByPoiAsync(poiId);

        var reviews = new List<Review>(serverReviews);
        foreach (var local in localReviews)
        {
            var duplicate = serverReviews.Any(s =>
                string.Equals((s.UserName ?? string.Empty).Trim(), (local.UserName ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase) &&
                string.Equals((s.Comment ?? string.Empty).Trim(), (local.Comment ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase) &&
                s.Rating == local.Rating);

            if (!duplicate)
            {
                reviews.Add(local);
            }
        }

        reviews = reviews.OrderByDescending(r => r.CreatedAt).ToList();

        PoiReviews = new ObservableCollection<Review>(reviews);

        _visibleReviewCount = 5;
        RebuildVisibleReviews();
    }

    private async Task LoadVirtualMenuItemsAsync(POI poi)
    {
        List<MenuItemDto> menuItems = new();

        try
        {
            var netAccess = Connectivity.Current.NetworkAccess;
            var isOnline = netAccess is NetworkAccess.Internet or NetworkAccess.ConstrainedInternet;

            if (isOnline)
            {
                var url = $"{AppConfig.GetResolvedApiBaseUrl()}api/MenuItems?poiId={poi.Id}&page=1&pageSize=50";
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
                var response = await client.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<MenuItemResponse>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (result?.Data != null && result.Data.Any())
                    {
                        menuItems = result.Data;
                        await _db.SaveMenuItemsAsync(menuItems);
                    }
                }
            }
        }
        catch
        {
            // Ignore API errors and fallback to local cache below.
        }

        if (!menuItems.Any())
        {
            menuItems = await _db.GetMenuItemsByPoiAsync(poi.Id);
        }

        if (!menuItems.Any())
        {
            menuItems = BuildFallbackMenuItems(poi);
        }

        VirtualMenuItems = new ObservableCollection<MenuItemDto>(
            menuItems.OrderByDescending(m => m.IsSignatureDish).ThenBy(m => m.Name_Vi));
    }

    private static List<MenuItemDto> BuildFallbackMenuItems(POI poi)
    {
        var list = new List<MenuItemDto>();
        var fallbackNames = poi.DisplaySignatureDishes;
        var baseId = poi.Id * 1000;

        for (var i = 0; i < fallbackNames.Count; i++)
        {
            list.Add(new MenuItemDto
            {
                MenuItemId = baseId + i + 1,
                POI_ID = poi.Id,
                Name_Vi = fallbackNames[i],
                Description_Vi = poi.Description_Vi ?? "Món ăn địa phương",
                ImageUrl = poi.ImageUrl ?? string.Empty,
                Price = poi.AveragePrice ?? 0,
                IsSignatureDish = true
            });
        }

        return list;
    }

    private void RebuildVisibleReviews()
    {
        VisiblePoiReviews = new ObservableCollection<Review>(PoiReviews.Take(_visibleReviewCount));

        HasMoreReviews = PoiReviews.Count > _visibleReviewCount;
        var remaining = Math.Max(0, PoiReviews.Count - _visibleReviewCount);
        LoadMoreReviewsText = remaining > 0 ? $"Xem thêm ({remaining})" : "Xem thêm";
    }

    [RelayCommand]
    private void LoadMoreReviews()
    {
        _visibleReviewCount += 5;
        RebuildVisibleReviews();
    }

    // ✅ FIX 5: Cache-based load methods for Menu & Reviews
    // Use these instead of auto-loading in OnPrimaryZoneChanged

    /// <summary>
    /// Load menu for current POI (with caching). Call when user switches to MENU tab.
    /// </summary>
    public async Task LoadMenuForCurrentPOIAsync()
    {
        if (PrimaryZone == null) return;

        // ✅ Check cache first
        if (_menuCache.TryGetValue(PrimaryZone.Id, out var cachedMenu))
        {
            VirtualMenuItems = new ObservableCollection<MenuItemDto>(cachedMenu);
            return;
        }

        // ✅ Load from repository (will be cached)
        var originalMethod = PrimaryZone;
        await LoadVirtualMenuItemsAsync(PrimaryZone);

        // ✅ Cache the loaded items (only if still on same POI)
        if (PrimaryZone == originalMethod)
        {
            _menuCache[PrimaryZone.Id] = VirtualMenuItems.ToList();
        }
    }

    /// <summary>
    /// Load reviews for current POI (with caching). Call when user switches to REVIEWS tab.
    /// </summary>
    public async Task LoadReviewsForCurrentPOIAsync()
    {
        if (PrimaryZone == null) return;

        // ✅ Check cache first
        if (_reviewCache.TryGetValue(PrimaryZone.Id, out var cachedReviews))
        {
            PoiReviews = new ObservableCollection<Review>(cachedReviews);
            RebuildVisibleReviews();
            return;
        }

        // ✅ Load from repository (will be cached)
        var originalPoiId = PrimaryZone.Id;
        await LoadPoiReviewsAsync(PrimaryZone.Id);

        // ✅ Cache the loaded items (only if still on same POI)
        if (PrimaryZone != null && PrimaryZone.Id == originalPoiId)
        {
            _reviewCache[originalPoiId] = PoiReviews.ToList();
        }
    }

    [RelayCommand]
    private async Task SubmitReview()
    {
        if (PrimaryZone == null || UserRating < 1 || IsSubmittingReview) return;

        IsReviewOnline = HasInternetAccess();
        if (!IsReviewOnline)
        {
            ReviewSubmitMessage = "Phải có mạng mới gửi được đánh giá.";
            LatestStatus = "Không có mạng: chưa thể gửi đánh giá.";
            return;
        }

        IsSubmittingReview = true;
        try
        {
            // Keep UX responsive: always show a short sending state.
            await Task.Delay(300);

            // Format username: Platform + Model (matching history.html style)
            string userName = $"{DeviceInfo.Current.Platform} {DeviceInfo.Current.Model}";
            string submittedComment = UserComment;
            int submittedRating = UserRating;
            ReviewSubmitMessage = string.Empty;

            var newReview = await _reviewService.SubmitReviewAsync(PrimaryZone.Id, userName, submittedRating, submittedComment);
            var reviewToShow = newReview ?? new Review
            {
                POI_ID = PrimaryZone.Id,
                UserName = userName,
                Rating = submittedRating,
                Comment = submittedComment,
                CreatedAt = DateTime.UtcNow
            };

            await _db.SaveReviewAsync(reviewToShow);

            PoiReviews.Insert(0, reviewToShow);
            RebuildVisibleReviews();

            // Clear form fields right after successful local update.
            UserRating = 0;
            UserComment = "";
            ReviewSubmitMessage = "Gửi đánh giá thành công.";

            // Background sync to align with server ordering/content.
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(800);
                    await MainThread.InvokeOnMainThreadAsync(async () => await LoadPoiReviewsAsync(PrimaryZone.Id));
                }
                catch
                {
                    // Keep optimistic local state if background refresh fails.
                }
            });
        }
        finally
        {
            IsSubmittingReview = false;
        }
    }

    [RelayCommand]
    private void SetUserRating(string rating)
    {
        ReviewSubmitMessage = string.Empty;
        if (int.TryParse(rating, out int r))
            UserRating = r;
    }

    public List<POI>? RequestedTourStops { get; set; }

    // Visited/saved sets (non-observable — used for map pin coloring)
    public HashSet<int> VisitedPOIIds { get; } = new();
    public HashSet<int> SavedPOIIds   { get; } = new();

    /// <summary>Auto-starts GPS tracking on page load. Safe to call multiple times.</summary>
    public async Task StartTrackingAsync()
    {
        if (IsTracking) return;
        var activeLoc = GetActiveLocationService();
        IsTracking = true;
        if (HasLocationFix && (CurrentLat != 0 || CurrentLon != 0))
            UpdateTrackingProximityState(CurrentLat, CurrentLon);
        else
            activeLoc.SetTrackingState(TrackingProximityState.Near);
        LatestStatus = "📡 GPS đang chạy...";
        await activeLoc.StartAsync();
    }

    // ── Commands ──────────────────────────────────────────────
    [RelayCommand]
    private async Task ToggleTrackingAsync()
    {
        var activeLoc = _isUsingFallback ? _fallbackSimulator : _location;

        if (!IsTracking)
        {
            if (HasLocationFix && (CurrentLat != 0 || CurrentLon != 0))
                UpdateTrackingProximityState(CurrentLat, CurrentLon);
            else
                activeLoc.SetTrackingState(TrackingProximityState.Near);
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
        RefreshExploreExperience();
    }

    [RelayCommand]
    private void ToggleAudio()
    {
        IsAudioPlaying = !IsAudioPlaying;
        RefreshExploreExperience();
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

    [RelayCommand]
    private async Task StartTourFromPrimaryAsync()
    {
        if (PrimaryZone == null) return;
        
        // Ensure tracking is active
        if (!IsTracking)
        {
            await ToggleTrackingCommand.ExecuteAsync(null);
        }
        
        // Switch to REAL MODE
        CurrentAppMode = AppMode.Real;
    }

    /// <summary>Select a map filter category chip.</summary>
    [RelayCommand]
    private void SelectCategory(string category)
    {
        SelectedCategory = category ?? "Tất cả";
    }

    // ── Browse / Map tab ─────────────────────────────────────────────────

    /// <summary>
    /// Load POIs using cache-first pattern. UI shows cached data immediately
    /// while API sync happens in background (non-blocking).
    /// </summary>
    /// <param name="forceSyncNow">If true, sync API immediately (blocking). Use false for fast UI load.</param>
    public async Task LoadAllPoisAsync(bool forceSyncNow = false)
    {
        Console.WriteLine("[MainViewModel] 🔄 LoadAllPoisAsync started...");

        // 1. ✅ Load local SQLite data IMMEDIATELY (cache-first)
        await _repository.LoadLocalAsync();

        var zones = _repository.GetAllActiveZones();
        if (zones.Count == 0)
        {
            Console.WriteLine("[MainViewModel] Cache is empty, forcing immediate sync/fallback...");
            await _repository.SyncFromMongoAsync();
            await _repository.LoadLocalAsync();
            zones = _repository.GetAllActiveZones();
        }

        AllPOIs = new ObservableCollection<POI>(zones);

        // 2. ✅ Populate VirtualTourPOIs (Spots only for FIX 5)
        VirtualTourPOIs = new ObservableCollection<POI>(AllPOIs.Where(p => p.ZoneType == "Spot"));

        // 3. ✅ Build map filter categories from loaded POI data
        BuildMapCategories();
        _ = ApplyFilterAsync();
        _ = LoadToursAsync(forceSyncNow);
        RefreshDataSourceState();
        await LoadSavedPOIsAsync();
        RefreshExploreExperience();

        Console.WriteLine($"[MainViewModel] ✅ Cache-first load done! AllPOIs={AllPOIs.Count}, VirtualTourPOIs={VirtualTourPOIs.Count}");

        // 3. ✅ Sync in background (non-blocking) unless forceSyncNow=true
        if (forceSyncNow || ShouldSyncNow())
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await _repository.SyncFromMongoAsync();

                    // After sync, reload UI on main thread
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        await _repository.LoadLocalAsync();
                        var updatedZones = _repository.GetAllActiveZones();
                        AllPOIs = new ObservableCollection<POI>(updatedZones);
                        VirtualTourPOIs = new ObservableCollection<POI>(AllPOIs.Where(p => p.ZoneType == "Spot"));

                        BuildMapCategories();
                        _ = ApplyFilterAsync();
                        RefreshDataSourceState();
                    });

                    // Update last sync time
                    if (_repository.CurrentDataSource == DataSourceKind.LiveApi)
                        Preferences.Set("LastSyncTime", DateTime.Now.ToString("O"));
                    Console.WriteLine("[MainViewModel] ✅ Background sync completed");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ Background sync failed: {ex.Message}");
                    // Don't crash app, just log error
                }
            });
        }
    }

    /// <summary>
    /// Check if we should sync from API.
    /// Only sync if > 5 minutes since last sync, or no previous sync recorded.
    /// </summary>
    private bool ShouldSyncNow()
    {
        if (AllPOIs.Count == 0 || _repository.CurrentDataSource == DataSourceKind.Unknown)
            return true;

        var lastSyncStr = Preferences.Get("LastSyncTime", "");
        if (string.IsNullOrEmpty(lastSyncStr))
            return true;

        if (DateTime.TryParse(lastSyncStr, out var lastSync))
        {
            return (DateTime.Now - lastSync).TotalSeconds > 20;
        }
        return true;
    }

    private bool ShouldSyncToursNow()
    {
        if (AllTours.Count == 0)
            return true;

        var lastSyncStr = Preferences.Get(LastTourSyncTimeKey, "");
        if (string.IsNullOrWhiteSpace(lastSyncStr))
            return true;

        if (DateTime.TryParse(lastSyncStr, out var lastSync))
            return (DateTime.Now - lastSync).TotalSeconds > 20;

        return true;
    }

    public async Task LiveSyncIfDueAsync()
    {
        if (_isLiveSyncInFlight)
            return;

        if (!HasInternetAccess())
            return;

        if (!ShouldSyncNow() && !ShouldSyncToursNow())
            return;

        await _liveSyncGate.WaitAsync();
        try
        {
            if (_isLiveSyncInFlight)
                return;
            _isLiveSyncInFlight = true;

            await _repository.SyncFromMongoAsync();
            await _repository.LoadLocalAsync();
            var updatedZones = _repository.GetAllActiveZones();

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                AllPOIs = new ObservableCollection<POI>(updatedZones);
                VirtualTourPOIs = new ObservableCollection<POI>(AllPOIs.Where(p => p.ZoneType == "Spot"));
                BuildMapCategories();
                _ = ApplyFilterAsync();
                RefreshExploreExperience();
                RefreshDataSourceState();
            });

            Preferences.Set(LastPoiSyncTimeKey, DateTime.Now.ToString("O"));
            await LoadToursAsync(forceSyncNow: true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] LiveSyncIfDueAsync error: {ex.Message}");
        }
        finally
        {
            _isLiveSyncInFlight = false;
            _liveSyncGate.Release();
        }
    }

    public async Task LoadToursAsync(bool forceSyncNow = false)
    {
        try
        {
            IsToursLoading = true;

            // 1) cache-first for instant UI
            var cachedTours = ReadToursFromCache();
            if (cachedTours.Count > 0)
            {
                AllTours = new ObservableCollection<TourListItem>(cachedTours);
                ApplyTourFilterCore();
            }

            if (!AppConfig.UseBackendApi || !HasInternetAccess())
            {
                IsTourDataStale = true;
                return;
            }

            if (!forceSyncNow && !ShouldSyncToursNow())
            {
                IsTourDataStale = false;
                return;
            }

            lock (_tourSyncLock)
            {
                if (_isTourSyncInFlight)
                    return;
                _isTourSyncInFlight = true;
            }

            try
            {
                var liveTours = await FetchToursFromApiAsync();
                if (liveTours.Count > 0)
                {
                    AllTours = new ObservableCollection<TourListItem>(liveTours);
                    ApplyTourFilterCore();
                    WriteToursToCache(liveTours);
                }

                Preferences.Set(LastTourSyncTimeKey, DateTime.Now.ToString("O"));
                IsTourDataStale = false;
            }
            finally
            {
                lock (_tourSyncLock)
                {
                    _isTourSyncInFlight = false;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] LoadToursAsync error: {ex.Message}");
            IsTourDataStale = true;
        }
        finally
        {
            IsToursLoading = false;
        }
    }

    [RelayCommand]
    private async Task RefreshToursAsync()
    {
        await LoadToursAsync(forceSyncNow: true);
    }

    private void ApplyTourFilterCore()
    {
        var q = (TourSearchQuery ?? string.Empty).Trim().ToLowerInvariant();

        var results = string.IsNullOrEmpty(q)
            ? AllTours.ToList()
            : AllTours.Where(t =>
                    t.Name.ToLowerInvariant().Contains(q) ||
                    t.Description.ToLowerInvariant().Contains(q))
                .ToList();

        FilteredTours = new ObservableCollection<TourListItem>(results);
    }

    private List<TourListItem> ReadToursFromCache()
    {
        var json = Preferences.Get(ToursCacheJsonKey, string.Empty);
        if (string.IsNullOrWhiteSpace(json))
            return new List<TourListItem>();

        try
        {
            return JsonSerializer.Deserialize<List<TourListItem>>(json, TourJsonOptions) ?? new List<TourListItem>();
        }
        catch
        {
            return new List<TourListItem>();
        }
    }

    private void WriteToursToCache(List<TourListItem> tours)
    {
        try
        {
            var json = JsonSerializer.Serialize(tours, TourJsonOptions);
            Preferences.Set(ToursCacheJsonKey, json);
        }
        catch
        {
            // Ignore cache write errors to keep UI smooth.
        }
    }

    private async Task<List<TourListItem>> FetchToursFromApiAsync()
    {
        var baseUrl = AppConfig.GetResolvedApiBaseUrl();
        using var http = new HttpClient
        {
            BaseAddress = new Uri(baseUrl),
            Timeout = TimeSpan.FromSeconds(AppConfig.NetworkTimeoutSeconds)
        };

        var response = await http.GetAsync("api/Tours?page=1&pageSize=100&isActive=true");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var payload = JsonSerializer.Deserialize<TourListResponseDto>(json, TourJsonOptions);
        var data = payload?.Data ?? new List<TourDto>();

        return data
            .Where(t => t.IsActive)
            .Select(t => new TourListItem
            {
                Id = t.Id ?? string.Empty,
                Name = string.IsNullOrWhiteSpace(t.TourName) ? "Tour ẩm thực" : t.TourName,
                Description = t.Description ?? string.Empty,
                EstimatedDurationMinutes = Math.Max(1, t.EstimatedDurationMinutes),
                PoiCount = t.Pois?.Count ?? 0,
                CoverImageUrl = "welcome_streetfood.png",
                IsActive = t.IsActive
            })
            .ToList();
    }

    private sealed class TourListResponseDto
    {
        public List<TourDto> Data { get; set; } = new();
    }

    private sealed class TourDto
    {
        public string? Id { get; set; }
        public string TourName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int EstimatedDurationMinutes { get; set; }
        public bool IsActive { get; set; }
        public List<TourPoiDto>? Pois { get; set; }
    }

    private sealed class TourPoiDto
    {
        public string Id { get; set; } = string.Empty;
    }

    /// <summary>Called automatically when SearchQuery changes.</summary>
    partial void OnSearchQueryChanged(string value)
    {
        HasSearchText = !string.IsNullOrWhiteSpace(value);
        _ = ApplyFilterAsync();
    }

    private void ApplyFilterCore()
    {
        var q = (SearchQuery ?? "").Trim().ToLower();
        var cat = SelectedCategory ?? "Tất cả";
        var byCategory = cat == "Tất cả"
            ? AllPOIs.Where(p => p.ZoneType == "Spot")
            : AllPOIs.Where(p => p.ZoneType == "Spot" && p.Type == cat);

        var results = byCategory.Where(p =>
            string.IsNullOrEmpty(q) ||
            (p.Name_Vi?.ToLower().Contains(q) == true) ||
            (p.SignatureDish?.ToLower().Contains(q) == true) ||
            (p.SignatureDishesJson?.ToLower().Contains(q) == true) ||
            (p.Description_Vi?.ToLower().Contains(q) == true) ||
            (p.Type?.ToLower().Contains(q) == true)).ToList();

        var suggestions = HasSearchText ? results.Take(6).ToList() : new List<POI>();

        MainThread.BeginInvokeOnMainThread(() =>
        {
            FilteredPOIs = new ObservableCollection<POI>(results);
            SearchSuggestions = new ObservableCollection<POI>(suggestions);
        });
    }

    public void ApplyFilter()
    {
        ApplyFilterCore();
    }

    private Task ApplyFilterAsync()
    {
        CancellationToken token;
        lock (_applyFilterLock)
        {
            _applyFilterCts?.Cancel();
            _applyFilterCts = new CancellationTokenSource();
            token = _applyFilterCts.Token;
        }

        return Task.Run(async () =>
        {
            await Task.Delay(80, token);
            token.ThrowIfCancellationRequested();

            var q = (SearchQuery ?? "").Trim().ToLower();
            var cat = SelectedCategory ?? "Tất cả";
            var byCategory = cat == "Tất cả"
                ? AllPOIs.Where(p => p.ZoneType == "Spot")
                : AllPOIs.Where(p => p.ZoneType == "Spot" && p.Type == cat);

            var results = byCategory.Where(p =>
                string.IsNullOrEmpty(q) ||
                (p.Name_Vi?.ToLower().Contains(q) == true) ||
                (p.SignatureDish?.ToLower().Contains(q) == true) ||
                (p.SignatureDishesJson?.ToLower().Contains(q) == true) ||
                (p.Description_Vi?.ToLower().Contains(q) == true) ||
                (p.Type?.ToLower().Contains(q) == true)).ToList();

            var suggestions = HasSearchText ? results.Take(6).ToList() : new List<POI>();

            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (token.IsCancellationRequested)
                    return;

                FilteredPOIs = new ObservableCollection<POI>(results);
                SearchSuggestions = new ObservableCollection<POI>(suggestions);
            });
        }, token);
    }

    public async Task LoadSavedPOIsAsync()
    {
        var liked = await _db.GetLikedPOIsAsync();
        SavedPOIIds.Clear();
        SavedPOIs = new ObservableCollection<POI>(liked);
        foreach (var poi in liked)
        {
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
        ApplyFilterCore();
    }

    [RelayCommand]
    private void ShowPinPopup(POI poi)
    {
        if (poi != null)
            VisitedPOIIds.Add(poi.Id);

        SelectedPinPOI = poi;
        IsPinPopupVisible = poi != null;
    }

    [RelayCommand]
    private void ClosePinPopup()
    {
        IsPinPopupVisible = false;
        SelectedPinPOI = null;
    }

    [RelayCommand]
    private void SetExploreStateDemo(string state)
    {
        if (!Enum.TryParse<ExploreState>(state, true, out var parsedState))
        {
            return;
        }

        IsExploreStateDemoLocked = true;
        CurrentExploreState = parsedState;
        ApplyExploreStateContent(parsedState, _exploreDemoCards.FirstOrDefault(), _exploreDemoCards.Skip(1).Take(2).ToList());
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
            AllPOIs = new ObservableCollection<POI>(_repository.GetAllActiveZones());
            VirtualTourPOIs = new ObservableCollection<POI>(AllPOIs.Where(p => p.ZoneType == "Spot"));
            _ = ApplyFilterAsync();
            
            RefreshDataSourceState();
            RefreshExploreExperience();
            
            Console.WriteLine($"[MainViewModel] ✓ ResetDatabaseAsync completed! {AllPOIs.Count} POIs loaded, Source: {_repository.CurrentDataSource}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MainViewModel] ❌ ResetDatabaseAsync error: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] ResetDatabaseAsync error: {ex}");
        }
    }
}
