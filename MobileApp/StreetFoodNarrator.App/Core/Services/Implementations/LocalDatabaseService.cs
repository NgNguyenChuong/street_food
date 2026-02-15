using StreetFoodNarrator.App.Core.Models;
using StreetFoodNarrator.App.Core.Services;
using System.Text.Json;
using Microsoft.Maui.Storage;
using System.IO;
using System.Threading;

namespace StreetFoodNarrator.App.Core.Services.Implementations;

/// <summary>
/// STUB: Local Database Service implementation cho testing
/// TODO: Implement với SQLite-net-pcl thực tế
/// </summary>
public class LocalDatabaseService : ILocalDatabaseService
{
    private static readonly JsonSerializerOptions StorageJsonOptions = new()
    {
        WriteIndented = false
    };

    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly string _poiFilePath;
    private readonly string _historyFilePath;

    // In-memory cache
    private List<POI> _poisCache = new();
    private List<ZoneHistory> _historyCache = new();
    private bool _isInitialized = false;

    public LocalDatabaseService()
    {
        var dataDir = FileSystem.AppDataDirectory;
        _poiFilePath = Path.Combine(dataDir, "pois.json");
        _historyFilePath = Path.Combine(dataDir, "zone_history.json");
    }
    
    public Task InitializeAsync()
    {
        return InitializeInternalAsync();
    }

    private async Task InitializeInternalAsync()
    {
        if (_isInitialized)
            return;

        await _lock.WaitAsync();
        try
        {
            if (_isInitialized)
                return;

            _poisCache = await ReadPoisAsync();
            _historyCache = await ReadHistoryAsync();

            _isInitialized = true;
            System.Diagnostics.Debug.WriteLine($"LocalDatabaseService: Initialized ({_poisCache.Count} POIs)");
        }
        finally
        {
            _lock.Release();
        }
    }
    
    public async Task<List<POI>> GetAllActivePOIsAsync()
    {
        await InitializeInternalAsync();
        var activePOIs = _poisCache.Where(p => p.IsActive).ToList();
        return activePOIs;
    }
    
    public Task SavePOIAsync(POI poi)
    {
        return SavePoiInternalAsync(poi);
    }
    
    public Task SavePOIsAsync(List<POI> pois)
    {
        return SavePoisInternalAsync(pois);
    }
    
    public Task DeleteAllPOIsAsync()
    {
        return DeleteAllPoisInternalAsync();
    }
    
    public async Task<List<ZoneHistory>> GetZoneHistoriesForSessionAsync(string sessionId)
    {
        await InitializeInternalAsync();
        var histories = _historyCache
            .Where(h => h.SessionId == sessionId)
            .ToList();

        return histories;
    }
    
    public Task SaveZoneHistoryAsync(ZoneHistory history)
    {
        return SaveHistoryInternalAsync(history);
    }
    
    public Task ClearZoneHistoryAsync()
    {
        return ClearHistoryInternalAsync();
    }
    
    public Task<int> InsertAsync<T>(T entity) where T : class, new()
    {
        if (entity is POI poi)
            return InsertPoiAsync(poi);
        if (entity is ZoneHistory history)
            return InsertHistoryAsync(history);

        return Task.FromResult(0);
    }
    
    public Task<int> InsertAllAsync<T>(IEnumerable<T> entities) where T : class, new()
    {
        return InsertAllInternalAsync(entities);
    }
    
    public Task<int> DeleteAllAsync<T>() where T : class, new()
    {
        if (typeof(T) == typeof(POI))
            return DeleteAllPoisTypedAsync();
        if (typeof(T) == typeof(ZoneHistory))
            return DeleteAllHistoryTypedAsync();

        return Task.FromResult(0);
    }

    private async Task<List<POI>> ReadPoisAsync()
    {
        if (!File.Exists(_poiFilePath))
            return new List<POI>();

        var json = await File.ReadAllTextAsync(_poiFilePath);
        return JsonSerializer.Deserialize<List<POI>>(json, StorageJsonOptions) ?? new List<POI>();
    }

    private async Task<List<ZoneHistory>> ReadHistoryAsync()
    {
        if (!File.Exists(_historyFilePath))
            return new List<ZoneHistory>();

        var json = await File.ReadAllTextAsync(_historyFilePath);
        return JsonSerializer.Deserialize<List<ZoneHistory>>(json, StorageJsonOptions) ?? new List<ZoneHistory>();
    }

    private async Task WritePoisAsync()
    {
        var json = JsonSerializer.Serialize(_poisCache, StorageJsonOptions);
        await File.WriteAllTextAsync(_poiFilePath, json);
    }

    private async Task WriteHistoryAsync()
    {
        var json = JsonSerializer.Serialize(_historyCache, StorageJsonOptions);
        await File.WriteAllTextAsync(_historyFilePath, json);
    }

    private async Task SavePoiInternalAsync(POI poi)
    {
        await InitializeInternalAsync();
        await _lock.WaitAsync();
        try
        {
            var existing = _poisCache.FirstOrDefault(p => p.Id == poi.Id);
            if (existing != null)
                _poisCache.Remove(existing);

            _poisCache.Add(poi);
            await WritePoisAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task SavePoisInternalAsync(List<POI> pois)
    {
        await InitializeInternalAsync();
        await _lock.WaitAsync();
        try
        {
            foreach (var poi in pois)
            {
                var existing = _poisCache.FirstOrDefault(p => p.Id == poi.Id);
                if (existing != null)
                    _poisCache.Remove(existing);

                _poisCache.Add(poi);
            }

            await WritePoisAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task DeleteAllPoisInternalAsync()
    {
        await InitializeInternalAsync();
        await _lock.WaitAsync();
        try
        {
            _poisCache.Clear();
            await WritePoisAsync();
            System.Diagnostics.Debug.WriteLine("LocalDatabaseService: All POIs cleared");
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task SaveHistoryInternalAsync(ZoneHistory history)
    {
        await InitializeInternalAsync();
        await _lock.WaitAsync();
        try
        {
            var existing = _historyCache.FirstOrDefault(h =>
                h.POI_ID == history.POI_ID &&
                h.SessionId == history.SessionId);

            if (existing != null)
                _historyCache.Remove(existing);

            _historyCache.Add(history);
            await WriteHistoryAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task ClearHistoryInternalAsync()
    {
        await InitializeInternalAsync();
        await _lock.WaitAsync();
        try
        {
            _historyCache.Clear();
            await WriteHistoryAsync();
            System.Diagnostics.Debug.WriteLine("LocalDatabaseService: Zone history cleared");
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<int> InsertPoiAsync(POI poi)
    {
        await SavePoiInternalAsync(poi);
        return 1;
    }

    private async Task<int> InsertHistoryAsync(ZoneHistory history)
    {
        await SaveHistoryInternalAsync(history);
        return 1;
    }

    private async Task<int> InsertAllInternalAsync<T>(IEnumerable<T> entities) where T : class, new()
    {
        if (entities is IEnumerable<POI> poiList)
        {
            var list = poiList.ToList();
            await SavePoisInternalAsync(list);
            return list.Count;
        }

        if (entities is IEnumerable<ZoneHistory> historyList)
        {
            await InitializeInternalAsync();
            await _lock.WaitAsync();
            try
            {
                foreach (var history in historyList)
                {
                    var existing = _historyCache.FirstOrDefault(h =>
                        h.POI_ID == history.POI_ID &&
                        h.SessionId == history.SessionId);

                    if (existing != null)
                        _historyCache.Remove(existing);

                    _historyCache.Add(history);
                }

                await WriteHistoryAsync();
                return _historyCache.Count;
            }
            finally
            {
                _lock.Release();
            }
        }

        return 0;
    }

    private async Task<int> DeleteAllPoisTypedAsync()
    {
        await DeleteAllPoisInternalAsync();
        return 0;
    }

    private async Task<int> DeleteAllHistoryTypedAsync()
    {
        await ClearHistoryInternalAsync();
        return 0;
    }
}
