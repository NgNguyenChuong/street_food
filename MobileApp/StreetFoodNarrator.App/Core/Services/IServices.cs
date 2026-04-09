namespace StreetFoodNarrator.App.Core.Services;

using StreetFoodNarrator.App.Core.Models;

// ── Geofence Service ───────────────────────────────────────────────
public interface IGeofenceService
{
    Task OnLocationChangedAsync(Microsoft.Maui.Devices.Sensors.Location newLocation);
    Task SetMonitoringEnabledAsync(bool enabled);
    
    event Action<List<POI>>? OnActiveZonesChanged;
    event Action<POI?>? OnPrimaryZoneChanged;
    event Action<string>? OnStatusMessage;
}

public enum TrackingProximityState
{
    Far,
    Near,
    Inside
}

// ── Data-source tracking ──────────────────────────────────────────
public enum DataSourceKind
{
    Unknown,
    LiveApi,
    SqliteCache,
    BundledJson,
    MockFallback
}

// ── Zone Repository ────────────────────────────────────────────────
public interface IZoneRepository
{
    List<POI> GetAllActiveZones();
    POI? GetById(int id);
    Task LoadLocalAsync();
    Task SyncFromMongoAsync();
    Task ResetAndSyncAsync(); // 🔄 DEBUG: Reset database và sync từ API
    bool IsSeeded { get; }
    DataSourceKind CurrentDataSource { get; }
}

// ── Local Database Service ───────────────────────────────────────────────
public interface ILocalDatabaseService
{
    Task InitializeAsync();
    Task<List<POI>> GetAllActivePOIsAsync();
    Task SavePOIAsync(POI poi);
    Task SavePOIsAsync(List<POI> pois);
    Task DeleteAllPOIsAsync();
    Task<List<POI>> GetLikedPOIsAsync();
    Task<List<MenuItemDto>> GetMenuItemsByPoiAsync(int poiId);
    Task SaveMenuItemsAsync(List<MenuItemDto> menuItems);
    Task<List<Review>> GetReviewsByPoiAsync(int poiId);
    Task SaveReviewAsync(Review review);
    Task<List<ZoneHistory>> GetAllZoneHistoriesAsync();
    Task<ZoneHistory?> GetZoneHistoryAsync(string sessionId, int poiId);
    Task<List<ZoneHistory>> GetZoneHistoriesForSessionAsync(string sessionId);
    Task SaveZoneHistoryAsync(ZoneHistory history);
    Task ClearZoneHistoryAsync();
    Task<int> InsertAsync<T>(T entity) where T : class, new();
    Task<int> InsertAllAsync<T>(IEnumerable<T> entities) where T : class, new();
    Task<int> DeleteAllAsync<T>() where T : class, new();
}

// ── Audio Service ──────────────────────────────────────────────────
public interface IAudioService
{
    Task PlayAsync(int zoneId, string audioSource, int durationSeconds, string fallbackText = "");
    Task PauseAsync(int zoneId);
    Task ResumeFromAsync(int zoneId, double positionSeconds);
    Task StopAsync(int zoneId);
    Task StopAllAsync();
    Task SetVolumeAsync(int zoneId, double volume);
    double GetCurrentPosition(int zoneId);
    bool IsPlaying(int zoneId);

    event Action<int>? OnPlaybackCompleted;
    event Action<int, double>? OnPositionChanged;
}

// ── Location Service ───────────────────────────────────────────────
public interface ILocationService
{
    Task StartAsync();
    Task StopAsync();
    void SetTrackingState(TrackingProximityState state);
    bool IsRunning { get; }

    event Action<Microsoft.Maui.Devices.Sensors.Location>? OnLocationUpdated;
}

// ── Virtual tour engagement ──────────────────────────────────────────
public interface ITourEngagementService
{
    void TrackPOIViewed(int? poiId = null);
    void StartSession();
    void EndSession();
    bool ShouldShowCompletionPrompt(double distanceMeters);
    int GetViewedPoiCount();
    TimeSpan GetTotalVirtualTime();
}

public interface IPopupService
{
    Task<bool> ShowCompletionPopup();
    void SetLastShownTime(DateTime? utcNow = null);
    DateTime? GetLastShownTime();
    bool IsSuppressedToday();
    Task ShowCompletionPopupAsync(Func<bool, Task> onAccepted);
}

public static class VirtualTourPromptPreferenceKeys
{
    public const string ViewedPoiCount = "virtual_tour.prompt.viewed_poi_count";
    public const string ViewedPoiIds = "virtual_tour.prompt.viewed_poi_ids";
    public const string TotalVirtualSeconds = "virtual_tour.prompt.total_virtual_seconds";
    public const string SessionStartUtc = "virtual_tour.prompt.session_start_utc";
    public const string LastPromptShownUtc = "virtual_tour.prompt.last_shown_utc";
    public const string SuppressUntilDate = "virtual_tour.prompt.suppress_until_date";
}

public static class VirtualTourPromptPolicy
{
    public const int MinViewedPois = 3;
    public const int MinVirtualSeconds = 120;
    public const double MinDistanceMeters = 1000;
    public static readonly TimeSpan Cooldown = TimeSpan.FromHours(24);
}

public enum VirtualPromptTrigger
{
    Idle,
    Exit
}

public enum VirtualPromptDecision
{
    NotShown,
    Dismissed,
    StartedRealTour
}

public interface IVirtualTourViewModel
{
    void ConfigureContext(Func<double> distanceProviderMeters, Func<Task> switchToRealModeAsync, Action switchToExploreFar);
    void StartVirtualTourSession();
    Task<VirtualPromptDecision> EndVirtualTourSessionAsync(bool evaluatePromptOnExit);
    void OnPoiViewed(int poiId);
    void RegisterInteraction();
    void OnAppBackgrounded();
    void OnAppResumed();
}
