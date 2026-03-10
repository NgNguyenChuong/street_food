namespace StreetFoodNarrator.App.Core.Services;

using StreetFoodNarrator.App.Core.Models;

// ── Geofence Service ───────────────────────────────────────────────
public interface IGeofenceService
{
    Task OnLocationChangedAsync(Microsoft.Maui.Devices.Sensors.Location newLocation);
    
    event Action<List<POI>>? OnActiveZonesChanged;
    event Action<POI?>? OnPrimaryZoneChanged;
    event Action<string>? OnStatusMessage;
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
    Task PlayAsync(int zoneId, string audioSource, int durationSeconds);
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
    bool IsRunning { get; }

    event Action<Microsoft.Maui.Devices.Sensors.Location>? OnLocationUpdated;
}
