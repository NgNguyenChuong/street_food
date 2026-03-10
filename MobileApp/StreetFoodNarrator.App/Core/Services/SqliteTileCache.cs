using BruTile.Cache;
using SQLite;

namespace StreetFoodNarrator.App.Core.Services;

/// <summary>
/// Persistent SQLite tile cache for Mapsui/BruTile.
/// Implements IPersistentCache&lt;byte[]&gt; so OSM tiles are stored on-device
/// and served offline after the first visit.
/// Max size: ~150 MB (~5000 tiles at ~30 KB each).
/// </summary>
public sealed class SqliteTileCache : IPersistentCache<byte[]>
{
    // ── SQLite model ──────────────────────────────────────────────
    [Table("tiles")]
    private sealed class TileRow
    {
        [PrimaryKey]
        public string Key     { get; set; } = string.Empty;
        public byte[] Data    { get; set; } = Array.Empty<byte>();
        public long   Fetched { get; set; } // Unix timestamp
    }

    private const int MaxTiles     = 5_000;
    private const int PruneKeep    = 4_000; // how many to keep after pruning
    private readonly SQLiteConnection _db;
    private readonly object _lock = new();

    public SqliteTileCache(string dbPath)
    {
        _db = new SQLiteConnection(dbPath,
            SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.FullMutex);
        _db.CreateTable<TileRow>();
    }

    // ── IPersistentCache<byte[]> ──────────────────────────────────

    public byte[]? Find(BruTile.TileIndex index)
    {
        lock (_lock)
        {
            var row = _db.Find<TileRow>(TileKey(index));
            return row?.Data;
        }
    }

    public void Add(BruTile.TileIndex index, byte[] tile)
    {
        if (tile == null || tile.Length == 0) return;
        lock (_lock)
        {
            _db.InsertOrReplace(new TileRow
            {
                Key     = TileKey(index),
                Data    = tile,
                Fetched = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            });
            PruneIfNeeded();
        }
    }

    public void Remove(BruTile.TileIndex index)
    {
        lock (_lock)
            _db.Delete<TileRow>(TileKey(index));
    }

    // ── Helpers ──────────────────────────────────────────────────

    private static string TileKey(BruTile.TileIndex t) =>
        $"{t.Level}/{t.Col}/{t.Row}";

    private void PruneIfNeeded()
    {
        var count = _db.ExecuteScalar<int>("SELECT COUNT(*) FROM tiles");
        if (count <= MaxTiles) return;

        // Delete oldest tiles beyond PruneKeep
        var oldest = _db.Query<TileRow>(
            "SELECT Key FROM tiles ORDER BY Fetched ASC LIMIT ?", count - PruneKeep);
        foreach (var row in oldest)
            _db.Delete<TileRow>(row.Key);
    }
}
