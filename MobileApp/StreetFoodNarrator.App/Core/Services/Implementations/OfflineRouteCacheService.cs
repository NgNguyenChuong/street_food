using System.Text.Json;
using StreetFoodNarrator.App.Core.Services;

namespace StreetFoodNarrator.App.Core.Services.Implementations;

/// <summary>
/// Stores previously resolved walking routes (lat/lon polyline) for offline reuse.
/// </summary>
public static class OfflineRouteCacheService
{
    private const int MaxEntriesPerPoi = 6;
    private const int MaxEntriesTotal = 120;
    private static readonly SemaphoreSlim _gate = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };

    public static async Task SaveRouteAsync(
        int poiId,
        double startLat,
        double startLon,
        IReadOnlyList<GeoCoordinate> path,
        CancellationToken cancellationToken = default)
    {
        if (poiId <= 0 || path == null || path.Count < 4)
            return;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var doc = await ReadDocumentAsync(cancellationToken);
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            var entriesForPoi = doc.Entries.Where(x => x.PoiId == poiId).ToList();
            foreach (var existing in entriesForPoi)
            {
                var sameStart = HaversineDistanceMeters(startLat, startLon, existing.StartLat, existing.StartLon) <= 80;
                if (!sameStart)
                    continue;

                existing.StartLat = startLat;
                existing.StartLon = startLon;
                existing.UpdatedAtUnix = now;
                existing.Points = path.Select(p => new RoutePointDto
                {
                    Latitude = p.Latitude,
                    Longitude = p.Longitude
                }).ToList();

                await WriteDocumentAsync(doc, cancellationToken);
                return;
            }

            doc.Entries.Add(new RouteCacheEntry
            {
                PoiId = poiId,
                StartLat = startLat,
                StartLon = startLon,
                UpdatedAtUnix = now,
                Points = path.Select(p => new RoutePointDto
                {
                    Latitude = p.Latitude,
                    Longitude = p.Longitude
                }).ToList()
            });

            TrimCache(doc);
            await WriteDocumentAsync(doc, cancellationToken);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RouteCache] SaveRouteAsync error: {ex.Message}");
        }
        finally
        {
            _gate.Release();
        }
    }

    public static async Task<IReadOnlyList<GeoCoordinate>?> TryGetRouteAsync(
        int poiId,
        double startLat,
        double startLon,
        double maxStartDistanceMeters = 750,
        CancellationToken cancellationToken = default)
    {
        if (poiId <= 0)
            return null;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var doc = await ReadDocumentAsync(cancellationToken);
            var candidate = doc.Entries
                .Where(x => x.PoiId == poiId && x.Points.Count >= 2)
                .Select(x => new
                {
                    Entry = x,
                    DistanceMeters = HaversineDistanceMeters(startLat, startLon, x.StartLat, x.StartLon)
                })
                .Where(x => x.DistanceMeters <= maxStartDistanceMeters)
                .OrderBy(x => x.DistanceMeters)
                .ThenByDescending(x => x.Entry.UpdatedAtUnix)
                .FirstOrDefault();

            if (candidate == null)
                return null;

            return candidate.Entry.Points
                .Select(p => new GeoCoordinate(p.Latitude, p.Longitude))
                .ToList();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RouteCache] TryGetRouteAsync error: {ex.Message}");
            return null;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static void TrimCache(RouteCacheDocument doc)
    {
        if (doc.Entries.Count == 0)
            return;

        var grouped = doc.Entries
            .GroupBy(x => x.PoiId)
            .ToList();

        foreach (var group in grouped)
        {
            var keep = group
                .OrderByDescending(x => x.UpdatedAtUnix)
                .Take(MaxEntriesPerPoi)
                .ToHashSet();

            doc.Entries.RemoveAll(x => x.PoiId == group.Key && !keep.Contains(x));
        }

        if (doc.Entries.Count <= MaxEntriesTotal)
            return;

        doc.Entries = doc.Entries
            .OrderByDescending(x => x.UpdatedAtUnix)
            .Take(MaxEntriesTotal)
            .ToList();
    }

    private static async Task<RouteCacheDocument> ReadDocumentAsync(CancellationToken cancellationToken)
    {
        var path = GetCacheFilePath();
        if (!File.Exists(path))
            return new RouteCacheDocument();

        await using var stream = File.OpenRead(path);
        var doc = await JsonSerializer.DeserializeAsync<RouteCacheDocument>(stream, JsonOptions, cancellationToken);
        return doc ?? new RouteCacheDocument();
    }

    private static async Task WriteDocumentAsync(RouteCacheDocument document, CancellationToken cancellationToken)
    {
        var path = GetCacheFilePath();
        var tempPath = path + ".tmp";

        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, document, JsonOptions, cancellationToken);
        }

        if (File.Exists(path))
            File.Delete(path);

        File.Move(tempPath, path);
    }

    private static string GetCacheFilePath()
    {
        var dir = Path.Combine(FileSystem.AppDataDirectory, "route_cache");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "walking_routes.json");
    }

    private static double HaversineDistanceMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadius = 6_371_000;
        var dLat = (lat2 - lat1) * Math.PI / 180.0;
        var dLon = (lon2 - lon1) * Math.PI / 180.0;
        var a = Math.Sin(dLat / 2.0) * Math.Sin(dLat / 2.0)
              + Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0)
              * Math.Sin(dLon / 2.0) * Math.Sin(dLon / 2.0);
        return earthRadius * 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));
    }

    private sealed class RouteCacheDocument
    {
        public List<RouteCacheEntry> Entries { get; set; } = new();
    }

    private sealed class RouteCacheEntry
    {
        public int PoiId { get; set; }
        public double StartLat { get; set; }
        public double StartLon { get; set; }
        public long UpdatedAtUnix { get; set; }
        public List<RoutePointDto> Points { get; set; } = new();
    }

    private sealed class RoutePointDto
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }
}
