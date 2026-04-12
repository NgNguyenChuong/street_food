using BruTile;
using BruTile.Cache;
using SQLite;
using System;
using System.IO;

namespace StreetFoodNarrator.App.Services;

/// <summary>
/// SQLite-based persistent cache for map tiles
/// Enables offline map viewing for previously loaded areas
/// </summary>
public class SimpleTileCache : IPersistentCache<byte[]>
{
    private readonly SQLite.SQLiteConnection _db;

    /// <summary>
    /// Initialize tile cache with SQLite database
    /// </summary>
    /// <param name="dbPath">Full path to SQLite database file</param>
    public SimpleTileCache(string dbPath)
    {
        // Ensure directory exists
        var dir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        // Open or create SQLite database
        _db = new SQLite.SQLiteConnection(dbPath);
        _db.CreateTable<CachedTile>();
        
        System.Diagnostics.Debug.WriteLine($"✅ Tile cache initialized: {dbPath}");
    }

    /// <summary>
    /// Add tile to cache
    /// </summary>
    public void Add(TileIndex index, byte[] tile)
    {
        try
        {
            var entry = new CachedTile
            {
                Key = GetKey(index),
                Data = tile,
                CachedAt = DateTime.UtcNow
            };
            
            _db.InsertOrReplace(entry);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"⚠️ Cache add error: {ex.Message}");
        }
    }

    /// <summary>
    /// Find tile in cache
    /// Returns null if not found or expired (>30 days old)
    /// </summary>
    public byte[]? Find(TileIndex index)
    {
        try
        {
            var key = GetKey(index);
            var entry = _db.Table<CachedTile>()
                .FirstOrDefault(t => t.Key == key);
            
            // Return tile if exists and not expired (30 days)
            if (entry != null && (DateTime.UtcNow - entry.CachedAt).TotalDays < 30)
            {
                return entry.Data;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"⚠️ Cache find error: {ex.Message}");
        }
        
        return null;
    }

    /// <summary>
    /// Remove tile from cache (currently not implemented)
    /// </summary>
    public void Remove(TileIndex index)
    {
        try
        {
            var key = GetKey(index);
            _db.Execute("DELETE FROM CachedTile WHERE Key = ?", key);
        }
        catch { }
    }

    /// <summary>
    /// Generate unique key for tile
    /// </summary>
    private static string GetKey(TileIndex index)
    {
        return $"{index.Level}_{index.Col}_{index.Row}";
    }

    /// <summary>
    /// SQLite table for cached tiles
    /// </summary>
    [SQLite.Table("CachedTile")]
    private class CachedTile
    {
        [SQLite.PrimaryKey]
        public string Key { get; set; } = "";
        
        public byte[] Data { get; set; } = Array.Empty<byte>();
        
        public DateTime CachedAt { get; set; }
    }
}
