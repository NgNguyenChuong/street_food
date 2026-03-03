using StreetFoodNarrator.App.Core.Models;
using StreetFoodNarrator.App.Core.Services;
using System.Text.Json;
using Microsoft.Maui.Storage;
using System.IO;
using System.Threading;
using SQLite;

namespace StreetFoodNarrator.App.Core.Services.Implementations;

/// <summary>
/// SQLite-based Local Database Service implementation
/// Migrates from JSON files on first run if needed
/// </summary>
public class LocalDatabaseService : ILocalDatabaseService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };

    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly string _dbPath;
    private readonly string _legacyPoiPath;
    private readonly string _legacyHistoryPath;
    
    private SQLiteAsyncConnection? _database;
    private bool _isInitialized = false;

    public LocalDatabaseService()
    {
        var dataDir = FileSystem.AppDataDirectory;
        _dbPath = Path.Combine(dataDir, "streetfood.db3");
        _legacyPoiPath = Path.Combine(dataDir, "pois.json");
        _legacyHistoryPath = Path.Combine(dataDir, "zone_history.json");
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

            // Initialize SQLite connection
            _database = new SQLiteAsyncConnection(_dbPath, 
                SQLiteOpenFlags.Create | 
                SQLiteOpenFlags.ReadWrite | 
                SQLiteOpenFlags.FullMutex);

            // Create tables
            await _database.CreateTableAsync<POI>();
            await _database.CreateTableAsync<ZoneHistory>();

            // Migrate from JSON if exists
            await MigrateFromJsonIfNeededAsync();

            _isInitialized = true;
            var count = await _database.Table<POI>().CountAsync();
            System.Diagnostics.Debug.WriteLine($"LocalDatabaseService: SQLite initialized ({count} POIs in DB)");
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task MigrateFromJsonIfNeededAsync()
    {
        if (_database == null)
            return;

        // Check if DB is empty and JSON files exist
        var poiCount = await _database.Table<POI>().CountAsync();
        if (poiCount > 0)
            return; // Already has data

        // Try to migrate POIs from JSON
        if (File.Exists(_legacyPoiPath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(_legacyPoiPath);
                var pois = JsonSerializer.Deserialize<List<POI>>(json, JsonOptions);
                
                if (pois != null && pois.Count > 0)
                {
                    await _database.InsertAllAsync(pois);
                    System.Diagnostics.Debug.WriteLine($"Migrated {pois.Count} POIs from JSON to SQLite");
                    
                    // Backup and delete old JSON file
                    File.Move(_legacyPoiPath, _legacyPoiPath + ".migrated");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to migrate POIs from JSON: {ex.Message}");
            }
        }

        // Try to migrate ZoneHistory from JSON
        if (File.Exists(_legacyHistoryPath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(_legacyHistoryPath);
                var histories = JsonSerializer.Deserialize<List<ZoneHistory>>(json, JsonOptions);
                
                if (histories != null && histories.Count > 0)
                {
                    await _database.InsertAllAsync(histories);
                    System.Diagnostics.Debug.WriteLine($"Migrated {histories.Count} histories from JSON to SQLite");
                    
                    // Backup and delete old JSON file
                    File.Move(_legacyHistoryPath, _legacyHistoryPath + ".migrated");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to migrate histories from JSON: {ex.Message}");
            }
        }
    }
    
    public async Task<List<POI>> GetAllActivePOIsAsync()
    {
        await InitializeInternalAsync();
        
        if (_database == null)
            return new List<POI>();

        return await _database.Table<POI>()
            .Where(p => p.IsActive)
            .ToListAsync();
    }
    
    public async Task SavePOIAsync(POI poi)
    {
        await InitializeInternalAsync();
        
        if (_database == null)
            return;

        await _lock.WaitAsync();
        try
        {
            poi.UpdatedAt = DateTime.Now;
            
            if (poi.Id > 0)
            {
                await _database.UpdateAsync(poi);
            }
            else
            {
                poi.CreatedAt = DateTime.Now;
                await _database.InsertAsync(poi);
            }
        }
        finally
        {
            _lock.Release();
        }
    }
    
    public async Task SavePOIsAsync(List<POI> pois)
    {
        await InitializeInternalAsync();
        
        if (_database == null)
            return;

        await _lock.WaitAsync();
        try
        {
            foreach (var poi in pois)
            {
                poi.UpdatedAt = DateTime.Now;
                
                if (poi.Id > 0)
                {
                    await _database.InsertOrReplaceAsync(poi);
                }
                else
                {
                    poi.CreatedAt = DateTime.Now;
                    await _database.InsertAsync(poi);
                }
            }
        }
        finally
        {
            _lock.Release();
        }
    }
    
    public async Task DeleteAllPOIsAsync()
    {
        await InitializeInternalAsync();
        
        if (_database == null)
            return;

        await _lock.WaitAsync();
        try
        {
            await _database.DeleteAllAsync<POI>();
            System.Diagnostics.Debug.WriteLine("LocalDatabaseService: All POIs deleted");
        }
        finally
        {
            _lock.Release();
        }
    }
    
    public async Task<List<ZoneHistory>> GetZoneHistoriesForSessionAsync(string sessionId)
    {
        await InitializeInternalAsync();
        
        if (_database == null)
            return new List<ZoneHistory>();

        return await _database.Table<ZoneHistory>()
            .Where(h => h.SessionId == sessionId)
            .ToListAsync();
    }
    
    public async Task SaveZoneHistoryAsync(ZoneHistory history)
    {
        await InitializeInternalAsync();
        
        if (_database == null)
            return;

        await _lock.WaitAsync();
        try
        {
            if (history.Id > 0)
            {
                await _database.UpdateAsync(history);
            }
            else
            {
                await _database.InsertAsync(history);
            }
        }
        finally
        {
            _lock.Release();
        }
    }
    
    public async Task ClearZoneHistoryAsync()
    {
        await InitializeInternalAsync();
        
        if (_database == null)
            return;

        await _lock.WaitAsync();
        try
        {
            await _database.DeleteAllAsync<ZoneHistory>();
            System.Diagnostics.Debug.WriteLine("LocalDatabaseService: Zone history cleared");
        }
        finally
        {
            _lock.Release();
        }
    }
    
    public async Task<int> InsertAsync<T>(T entity) where T : class, new()
    {
        await InitializeInternalAsync();
        
        if (_database == null)
            return 0;

        return await _database.InsertAsync(entity);
    }
    
    public async Task<int> InsertAllAsync<T>(IEnumerable<T> entities) where T : class, new()
    {
        await InitializeInternalAsync();
        
        if (_database == null)
            return 0;

        await _lock.WaitAsync();
        try
        {
            return await _database.InsertAllAsync(entities);
        }
        finally
        {
            _lock.Release();
        }
    }
    
    public async Task<int> DeleteAllAsync<T>() where T : class, new()
    {
        await InitializeInternalAsync();
        
        if (_database == null)
            return 0;

        return await _database.DeleteAllAsync<T>();
    }
}
