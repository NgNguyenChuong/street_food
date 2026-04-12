using BruTile;
using Microsoft.Maui.Networking;
using Microsoft.Maui.Storage;
using StreetFoodNarrator.App.Core.Models;
using StreetFoodNarrator.App.Core.Services;
using System.Text.Json;
using System.Globalization;

namespace StreetFoodNarrator.App.Core.Services.Implementations;

/// <summary>
/// Manages offline data: size calculation, download, update check, and mock data.
/// Metadata is stored in Preferences (no extra model files needed).
/// </summary>
public class DataSyncService
{
    private const int EssentialAudioPoiCount = 4;
    private const int EssentialMapMinZoom = 16;
    private const int EssentialMapMaxZoom = 17;
    private const int EssentialMapMaxTiles = 24;
    private const int OfflineMapMinZoom = 18;
    private const int OfflineMapMaxZoom = 20;
    private const double OfflineMapPaddingDegrees = 0.0004;
    private const double AverageTileSizeKb = 18.0;
    private static readonly Uri CartoMapTileBaseUri = new("https://a.basemaps.cartocdn.com/rastertiles/voyager/");

    // ─── Preference Keys ──────────────────────────────────────────────────────
    public const string PrefHasFullOffline = "has_full_offline";
    public const string PrefOfflinePOICount = "offline_poi_count";
    public const string PrefOfflineVersion  = "offline_version";
    public const string PrefOfflineLastSync = "offline_last_sync";
    public const string PrefOfflineSizeMb   = "offline_size_mb";
    public const string PrefSkippedVersion  = "skipped_update_version";

    private readonly IZoneRepository _repository;
    private readonly ILocalDatabaseService _db;
    private readonly IAudioCacheService _audioCache;
    private readonly HttpClient _http;
    private readonly SemaphoreSlim _backgroundOfflineLock = new(1, 1);
    private readonly object _backgroundTaskSync = new();
    private Task? _backgroundOfflineCompletionTask;

    public DataSyncService(
        IZoneRepository repository,
        ILocalDatabaseService db,
        IAudioCacheService audioCache,
        HttpClient http)
    {
        _repository  = repository;
        _db          = db;
        _audioCache  = audioCache;
        _http        = http;
    }

    // ─── Status Helpers ───────────────────────────────────────────────────────

    public bool HasOfflineData()
        => Preferences.Get(PrefHasFullOffline, false);

    public int GetOfflinePOICount()
        => Preferences.Get(PrefOfflinePOICount, 0);

    public string GetLastSyncTime()
        => Preferences.Get(PrefOfflineLastSync, "Chưa đồng bộ");

    public double GetOfflineSizeMb()
        => Preferences.Get(PrefOfflineSizeMb, 0.0);

    public string GetLocalVersion()
        => Preferences.Get(PrefOfflineVersion, "0.0.0");

    // ─── Size Calculation ─────────────────────────────────────────────────────

    /// <summary>
    /// Estimates download size. Uses HEAD requests where possible,
    /// falls back to per-file-type averages.
    /// Returns (total MB, poi count, audio count, image count, breakdown text).
    /// </summary>
    public async Task<(double TotalMb, int POICount, int AudioCount, int ImageCount, string BreakdownText)>
        CalculateDownloadSizeAsync()
    {
        var pois = _repository.GetAllActiveZones();
        int poiCount = pois.Count;

        // POI JSON data: ~4 KB per POI
        double poiDataMb = poiCount * 4.0 / 1024.0;

        // Audio: HEAD request → fallback 2 MB/file
        double audioMb = 0;
        int audioCount = 0;
        foreach (var poi in pois)
        {
            foreach (var url in new[] { poi.AudioUrl_Vi, poi.AudioUrl_En, poi.AudioUrl_Zh })
            {
                if (!string.IsNullOrEmpty(url))
                {
                    audioMb += await GetRemoteFileSizeMbAsync(url, 2.0);
                    audioCount++;
                }
            }
        }

        // Images: HEAD request → fallback 150 KB/image
        double imageMb = 0;
        int imageCount = 0;
        foreach (var poi in pois)
        {
            if (!string.IsNullOrEmpty(poi.ImageUrl))
            {
                imageMb += await GetRemoteFileSizeMbAsync(poi.ImageUrl, 0.15);
                imageCount++;
            }
        }

        // Map tiles estimate for Vĩnh Khánh area
        double mapMb = EstimateMapTilesMb(pois);

        double totalMb = poiDataMb + audioMb + imageMb + mapMb;

        var breakdown =
            $"• {poiCount} quán ẩm thực: {poiDataMb:F1} MB\n" +
            $"• {audioCount} audio thuyết minh: {audioMb:F1} MB\n" +
            $"• {imageCount} hình ảnh: {imageMb:F1} MB\n" +
            $"• Bản đồ khu vực: {mapMb:F1} MB";

        return (totalMb, poiCount, audioCount, imageCount, breakdown);
    }

    private async Task<double> GetRemoteFileSizeMbAsync(string url, double fallbackMb)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Head, url);
            using var resp = await _http.SendAsync(req).WaitAsync(TimeSpan.FromSeconds(2));
            if (resp.IsSuccessStatusCode && resp.Content.Headers.ContentLength.HasValue)
                return resp.Content.Headers.ContentLength.Value / 1024.0 / 1024.0;
        }
        catch { /* fall through to estimate */ }

        // Fallback by extension
        var ext = System.IO.Path.GetExtension(url).ToLowerInvariant();
        return ext switch
        {
            ".mp3" => 2.0,
            ".jpg" or ".jpeg" => 0.15,
            ".png" => 0.20,
            _ => fallbackMb
        };
    }

    private static double EstimateMapTilesMb(IReadOnlyCollection<POI> pois)
    {
        // Estimate tiles for ~1.5 km radius around Vĩnh Khánh at zoom 14-18
        // Average ~18 KB/tile with ~150 tiles across all zoom levels
        return EstimateMapTilesForPois(pois).Count * AverageTileSizeKb / 1024.0;
    }

    // ─── Update Check ─────────────────────────────────────────────────────────

    /// <summary>
    /// Checks if the server has a version newer than the locally stored one.
    /// Returns (hasUpdate, newVersion) or (false, "") if offline/error.
    /// </summary>
    public async Task<(bool HasUpdate, string NewVersion)> CheckForUpdatesAsync()
    {
        try
        {
            var isOnline = Connectivity.Current.NetworkAccess is NetworkAccess.Internet
                                                              or NetworkAccess.ConstrainedInternet;
            if (!isOnline) return (false, "");

            var url = AppConfig.BuildApiUrl("api/data/version");
            var response = await _http.GetAsync(url).WaitAsync(TimeSpan.FromSeconds(3));

            if (!response.IsSuccessStatusCode) return (false, "");

            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);
            var serverVersion = doc.RootElement.GetProperty("version").GetString() ?? "";

            var localVersion  = GetLocalVersion();
            var skipped       = Preferences.Get(PrefSkippedVersion, "");

            // Has update if server > local, and user hasn't already skipped this version
            bool hasUpdate = !string.IsNullOrEmpty(serverVersion)
                          && serverVersion != localVersion
                          && serverVersion != skipped;

            return (hasUpdate, serverVersion);
        }
        catch
        {
            return (false, "");
        }
    }

    /// <summary>
    /// Marks a version as "skipped" so the update popup won't show again for it.
    /// </summary>
    public void SkipVersion(string version)
        => Preferences.Set(PrefSkippedVersion, version);

    // ─── Download All Data ────────────────────────────────────────────────────

    /// <summary>
    /// Downloads all offline data (POIs, menus, audio) and saves metadata to Preferences.
    /// Calls statusCallback with progress messages.
    /// </summary>
    public async Task DownloadAllDataAsync(Action<string>? statusCallback = null)
    {
        await _backgroundOfflineLock.WaitAsync();
        try
        {
            await DownloadAllDataCoreAsync(statusCallback);
        }
        finally
        {
            _backgroundOfflineLock.Release();
        }
    }

    public async Task PrimeEssentialOfflineDataAsync(
        string preferredLanguage,
        Action<string>? statusCallback = null)
    {
        statusCallback?.Invoke("Đang chuẩn bị dữ liệu cần thiết...");
        await _repository.SyncFromMongoAsync();
        await _repository.LoadLocalAsync();

        var pois = _repository.GetAllActiveZones();
        if (pois.Count == 0)
            return;

        var priorityPoiIds = GetPriorityPoiIds(pois, EssentialAudioPoiCount);

        statusCallback?.Invoke("Đang tải audio ưu tiên...");
        await PreloadPriorityAudioAsync(priorityPoiIds, preferredLanguage, statusCallback);

        statusCallback?.Invoke("Đang tải ảnh ưu tiên...");
        await PreloadPoiImagesAsync(
            pois,
            priorityPoiIds,
            progress => statusCallback?.Invoke($"Đang tải ảnh {progress.done}/{progress.total}..."));

        statusCallback?.Invoke("Đang tải bản đồ khu vực chính...");
        await PreloadOfflineMapTilesAsync(
            pois,
            EssentialMapMinZoom,
            EssentialMapMaxZoom,
            EssentialMapMaxTiles,
            progress => statusCallback?.Invoke($"Đang tải bản đồ offline {progress.done}/{progress.total}..."));

        statusCallback?.Invoke("Đang chuẩn bị chỉ đường ưu tiên...");
        await PreloadOfflineRouteSnapshotsAsync(
            pois,
            priorityPoiIds,
            progress => statusCallback?.Invoke($"Đang chuẩn bị chỉ đường {progress.done}/{progress.total}..."));
    }

    public Task EnsureDeferredOfflineCompletionAsync()
    {
        lock (_backgroundTaskSync)
        {
            if (_backgroundOfflineCompletionTask is { IsCompleted: false })
                return _backgroundOfflineCompletionTask;

            _backgroundOfflineCompletionTask = Task.Run(async () =>
            {
                await _backgroundOfflineLock.WaitAsync();
                try
                {
                    await DownloadAllDataCoreAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[DataSync] Background offline completion failed: {ex.Message}");
                }
                finally
                {
                    _backgroundOfflineLock.Release();
                }
            });

            return _backgroundOfflineCompletionTask;
        }
    }

    private async Task DownloadAllDataCoreAsync(Action<string>? statusCallback = null)
    {
        statusCallback?.Invoke("Đang tải danh sách quán...");
        await _repository.SyncFromMongoAsync();
        await _repository.LoadLocalAsync();

        var pois = _repository.GetAllActiveZones();
        var poiIds = pois.Select(p => p.Id).ToList();

        statusCallback?.Invoke("Đang tải thực đơn...");
        await SyncMenusAsync(poiIds);

        statusCallback?.Invoke("Đang tải audio thuyết minh...");
        var audioProgress = new Progress<(int done, int total)>(p =>
            statusCallback?.Invoke($"Đang tải audio {p.done}/{p.total}..."));
        await _audioCache.PreloadAllAsync(poiIds, audioProgress);

        statusCallback?.Invoke("Đang tải hình ảnh địa điểm...");
        await PreloadPoiImagesAsync(
            pois,
            null,
            progress => statusCallback?.Invoke($"Đang tải ảnh {progress.done}/{progress.total}..."));

        statusCallback?.Invoke("Đang tải bản đồ offline khu ẩm thực...");
        var mapTileCount = await PreloadOfflineMapTilesAsync(
            pois,
            OfflineMapMinZoom,
            OfflineMapMaxZoom,
            maxTiles: null,
            progress => statusCallback?.Invoke($"Đang tải bản đồ offline {progress.done}/{progress.total}..."));

        statusCallback?.Invoke("Đang chuẩn bị chỉ đường offline...");
        var routeCount = await PreloadOfflineRouteSnapshotsAsync(
            pois,
            null,
            progress => statusCallback?.Invoke($"Đang chuẩn bị chỉ đường {progress.done}/{progress.total}..."));

        var now = DateTime.Now.ToString("dd/MM HH:mm");
        var sizeMb = (_audioCache.GetCacheSizeBytes() + GetOfflineMapCacheSizeBytes() + PoiImageCacheService.GetCacheSizeBytes()) / 1024.0 / 1024.0;
        var serverVer = await GetServerVersionAsync();

        Preferences.Set(PrefHasFullOffline, true);
        Preferences.Set(PrefOfflinePOICount, pois.Count);
        Preferences.Set(PrefOfflineLastSync, now);
        Preferences.Set(PrefOfflineSizeMb, sizeMb);
        Preferences.Set(PrefOfflineVersion, serverVer);
        Preferences.Set("LastSyncTime", now);
        Preferences.Remove(PrefSkippedVersion);
        System.Diagnostics.Debug.WriteLine($"[OfflineMap] Cached {mapTileCount} tiles for offline map use.");
        System.Diagnostics.Debug.WriteLine($"[OfflineRoute] Cached {routeCount} route snapshots for offline reuse.");
    }

    // ─── Clear Offline Data ───────────────────────────────────────────────────

    public async Task ClearOfflineDataAsync()
    {
        await _audioCache.ClearAsync();
        ClearOfflineMapCache();
        Preferences.Set(PrefHasFullOffline,  false);
        Preferences.Set(PrefOfflinePOICount, 0);
        Preferences.Set(PrefOfflineSizeMb,   0.0);
        Preferences.Remove(PrefOfflineVersion);
        Preferences.Remove(PrefOfflineLastSync);
    }

    // ─── Mock Data (13 POIs) ─────────────────────────────────────────────────

    /// <summary>
    /// Returns 13 hardcoded "Phố Vĩnh Khánh" POIs for offline-without-download fallback.
    /// </summary>
    public static List<POI> GetMockPOIs() => new()
    {
        new POI { Id=1,  Name_Vi="Ốc Vũ",             Name_En="Oc Vu Snails",        Latitude=10.7614, Longitude=106.6991, Radius=80, ZoneType="Spot", IsActive=true, Address="37 Vĩnh Khánh, Q.4",        Category="Hải sản", SignatureDish="Ốc hương rang muối ớt",     Rating=4.5 },
        new POI { Id=2,  Name_Vi="Bò Né Thanh Xuân",  Name_En="Bo Ne Thanh Xuan",    Latitude=10.7622, Longitude=106.6985, Radius=80, ZoneType="Spot", IsActive=true, Address="54 Vĩnh Khánh, Q.4",        Category="Bò né",   SignatureDish="Bò né trứng ốp la",         Rating=4.3 },
        new POI { Id=3,  Name_Vi="Cháo Ếch Singapore",Name_En="Frog Porridge",        Latitude=10.7630, Longitude=106.6979, Radius=80, ZoneType="Spot", IsActive=true, Address="12 Vĩnh Khánh, Q.4",        Category="Cháo",    SignatureDish="Cháo ếch nấu Singapore",    Rating=4.2 },
        new POI { Id=4,  Name_Vi="Tôm Cà Ri Phát",    Name_En="Phat Curry Shrimp",   Latitude=10.7637, Longitude=106.6974, Radius=80, ZoneType="Spot", IsActive=true, Address="7 Vĩnh Khánh, Q.4",         Category="Hải sản", SignatureDish="Tôm cà ri kiểu Thái",       Rating=4.4 },
        new POI { Id=5,  Name_Vi="Bún Riêu Cua Lan",  Name_En="Bun Rieu Cua Lan",    Latitude=10.7644, Longitude=106.6968, Radius=80, ZoneType="Spot", IsActive=true, Address="29 Vĩnh Khánh, Q.4",        Category="Bún",     SignatureDish="Bún riêu cua đồng",         Rating=4.6 },
        new POI { Id=6,  Name_Vi="Ốc Nữ Hoàng",       Name_En="Nu Hoang Snails",     Latitude=10.7651, Longitude=106.6962, Radius=80, ZoneType="Spot", IsActive=true, Address="41 Vĩnh Khánh, Q.4",        Category="Hải sản", SignatureDish="Sò huyết xào tỏi",          Rating=4.1 },
        new POI { Id=7,  Name_Vi="Hải Sản Thành Đạt", Name_En="Thanh Dat Seafood",   Latitude=10.7658, Longitude=106.6956, Radius=80, ZoneType="Spot", IsActive=true, Address="63 Vĩnh Khánh, Q.4",        Category="Hải sản", SignatureDish="Tôm hùm nướng muối ớt",     Rating=4.7 },
        new POI { Id=8,  Name_Vi="Cơm Tấm Bà Tư",     Name_En="Ba Tu Broken Rice",   Latitude=10.7665, Longitude=106.6950, Radius=80, ZoneType="Spot", IsActive=true, Address="8 Vĩnh Khánh, Q.4",         Category="Cơm tấm", SignatureDish="Cơm tấm sườn bì chả",       Rating=4.5 },
        new POI { Id=9,  Name_Vi="Lẩu Thái Thảo Ly",  Name_En="Thao Ly Thai Hotpot", Latitude=10.7671, Longitude=106.6944, Radius=80, ZoneType="Spot", IsActive=true, Address="19 Vĩnh Khánh, Q.4",        Category="Lẩu",     SignatureDish="Lẩu Thái chua cay hải sản", Rating=4.3 },
        new POI { Id=10, Name_Vi="Bún Bò Huế Hương",   Name_En="Huong Bun Bo Hue",    Latitude=10.7678, Longitude=106.6938, Radius=80, ZoneType="Spot", IsActive=true, Address="88 Vĩnh Khánh, Q.4",        Category="Bún",     SignatureDish="Bún bò Huế giò heo",        Rating=4.4 },
        new POI { Id=11, Name_Vi="Gỏi Cuốn Kim Anh",   Name_En="Kim Anh Fresh Rolls", Latitude=10.7684, Longitude=106.6932, Radius=80, ZoneType="Spot", IsActive=true, Address="33 Vĩnh Khánh, Q.4",        Category="Gỏi cuốn",SignatureDish="Gỏi cuốn tôm thịt chấm mắm", Rating=4.2 },
        new POI { Id=12, Name_Vi="Mì Xào Hải Sản Hùng",Name_En="Hung Seafood Noodle", Latitude=10.7691, Longitude=106.6926, Radius=80, ZoneType="Spot", IsActive=true, Address="72 Vĩnh Khánh, Q.4",        Category="Mì",      SignatureDish="Mì xào giòn hải sản",       Rating=4.0 },
        new POI { Id=13, Name_Vi="Che Ben Duong",       Name_En="Street Side Dessert", Latitude=10.7697, Longitude=106.6920, Radius=80, ZoneType="Spot", IsActive=true, Address="Góc Vĩnh Khánh & Khánh Hội",Category="Chè",     SignatureDish="Chè bưởi thạch",             Rating=4.1 },
    };

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private async Task<string> GetServerVersionAsync()
    {
        try
        {
            var url  = AppConfig.BuildApiUrl("api/data/version");
            var json = await _http.GetStringAsync(url).WaitAsync(TimeSpan.FromSeconds(5));
            var doc  = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("version").GetString() ?? "1.0.0";
        }
        catch
        {
            return "1.0.0";
        }
    }

    private async Task SyncMenusAsync(IEnumerable<int> poiIds)
    {
        foreach (var id in poiIds)
        {
            try
            {
                var menuUrl = AppConfig.BuildApiUrl($"api/MenuItems?poiId={id}&page=1&pageSize=50");
                var resp = await _http.GetAsync(menuUrl);
                if (!resp.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine($"[DataSync] Menu sync failed for POI {id}: {(int)resp.StatusCode}");
                    continue;
                }

                var content = await resp.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<MenuItemResponse>(
                    content,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (result?.Data?.Any() == true)
                    await _db.SaveMenuItemsAsync(result.Data);
            }
            catch (Exception ex)
            {
                // Ignore per-POI failures so other data can still finish.
                System.Diagnostics.Debug.WriteLine($"[DataSync] Menu sync exception for POI {id}: {ex.Message}");
            }
        }
    }

    private async Task PreloadPriorityAudioAsync(
        IReadOnlyCollection<int> poiIds,
        string preferredLanguage,
        Action<string>? statusCallback = null)
    {
        var ids = poiIds.Distinct().ToList();
        if (ids.Count == 0)
            return;

        var done = 0;
        foreach (var poiId in ids)
        {
            try
            {
                var stream = await _audioCache.GetOrDownloadCachedStreamAsync(poiId, preferredLanguage);
                if (stream != null)
                    await stream.DisposeAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DataSync] Priority audio preload failed for POI {poiId}: {ex.Message}");
            }

            done++;
            statusCallback?.Invoke($"Đang tải audio {done}/{ids.Count}...");
        }
    }

    private static List<int> GetPriorityPoiIds(IReadOnlyCollection<POI> pois, int maxCount)
    {
        return pois
            .Where(p => p.IsActive && p.ZoneType != "Area" && p.ZoneType != "District")
            .OrderBy(p => DistanceToDefaultLocationSquared(p))
            .Take(maxCount)
            .Select(p => p.Id)
            .ToList();
    }

    private static double DistanceToDefaultLocationSquared(POI poi)
    {
        var latDiff = poi.Latitude - AppConfig.DefaultLatitude;
        var lonDiff = poi.Longitude - AppConfig.DefaultLongitude;
        return (latDiff * latDiff) + (lonDiff * lonDiff);
    }

    private async Task<int> PreloadOfflineMapTilesAsync(
        IReadOnlyCollection<POI> pois,
        int minZoom,
        int maxZoom,
        int? maxTiles,
        Action<(int done, int total)>? progressCallback = null)
    {
        var tiles = EstimateMapTilesForPois(pois, minZoom, maxZoom, maxTiles);
        if (tiles.Count == 0)
            return 0;

        var cache = new StreetFoodNarrator.App.Services.SimpleTileCache(GetOfflineMapCacheDbPath());
        var done = 0;

        foreach (var tile in tiles)
        {
            if (cache.Find(tile) != null)
            {
                done++;
                progressCallback?.Invoke((done, tiles.Count));
                continue;
            }

            try
            {
                using var response = await _http.GetAsync(BuildTileUri(tile)).WaitAsync(TimeSpan.FromSeconds(10));
                if (response.IsSuccessStatusCode)
                {
                    var data = await response.Content.ReadAsByteArrayAsync();
                    cache.Add(tile, data);
                }
            }
            catch
            {
                // Ignore individual tile failures so the rest of the offline bundle still completes.
            }

            done++;
            progressCallback?.Invoke((done, tiles.Count));
        }

        return tiles.Count;
    }

    private static List<TileIndex> EstimateMapTilesForPois(
        IReadOnlyCollection<POI> pois,
        int minZoom = OfflineMapMinZoom,
        int maxZoom = OfflineMapMaxZoom,
        int? maxTiles = null)
    {
        var spotPois = pois
            .Where(p => p.IsActive && p.ZoneType != "Area" && p.ZoneType != "District")
            .ToList();

        if (spotPois.Count == 0)
            return [];

        var minLat = spotPois.Min(p => p.Latitude) - OfflineMapPaddingDegrees;
        var maxLat = spotPois.Max(p => p.Latitude) + OfflineMapPaddingDegrees;
        var minLon = spotPois.Min(p => p.Longitude) - OfflineMapPaddingDegrees;
        var maxLon = spotPois.Max(p => p.Longitude) + OfflineMapPaddingDegrees;

        var tiles = new List<TileIndex>();
        for (var zoom = minZoom; zoom <= maxZoom; zoom++)
        {
            var minX = LonToTileX(minLon, zoom);
            var maxX = LonToTileX(maxLon, zoom);
            var minY = LatToTileY(maxLat, zoom);
            var maxY = LatToTileY(minLat, zoom);

            for (var x = minX; x <= maxX; x++)
            {
                for (var y = minY; y <= maxY; y++)
                    tiles.Add(new TileIndex(x, y, zoom));
            }
        }

        if (maxTiles.HasValue && tiles.Count > maxTiles.Value)
            return tiles.Take(maxTiles.Value).ToList();

        return tiles;
    }

    private static int LonToTileX(double lon, int zoom)
    {
        var n = Math.Pow(2, zoom);
        return (int)Math.Floor((lon + 180.0) / 360.0 * n);
    }

    private static int LatToTileY(double lat, int zoom)
    {
        var clampedLat = Math.Clamp(lat, -85.05112878, 85.05112878);
        var latRad = clampedLat * Math.PI / 180.0;
        var n = Math.Pow(2, zoom);
        var value = (1.0 - Math.Log(Math.Tan(latRad) + (1.0 / Math.Cos(latRad))) / Math.PI) / 2.0 * n;
        return (int)Math.Floor(value);
    }

    private static Uri BuildTileUri(TileIndex tile)
        => new(CartoMapTileBaseUri, $"{tile.Level}/{tile.Col}/{tile.Row}.png");

    private async Task PreloadPoiImagesAsync(
        IReadOnlyCollection<POI> pois,
        IReadOnlyCollection<int>? onlyPoiIds,
        Action<(int done, int total)>? progressCallback = null)
    {
        var baseUrl = AppConfig.GetResolvedApiBaseUrl().TrimEnd('/');
        var selected = pois
            .Where(p => p.IsActive)
            .Where(p => onlyPoiIds == null || onlyPoiIds.Contains(p.Id))
            .Where(p => !string.IsNullOrWhiteSpace(p.ImageUrl))
            .ToList();

        if (selected.Count == 0)
            return;

        var done = 0;
        foreach (var poi in selected)
        {
            try
            {
                await PoiImageCacheService.EnsureCachedAsync(_http, baseUrl, poi.ImageUrl);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DataSync] Image preload failed for POI {poi.Id}: {ex.Message}");
            }

            done++;
            progressCallback?.Invoke((done, selected.Count));
        }
    }

    private async Task<int> PreloadOfflineRouteSnapshotsAsync(
        IReadOnlyCollection<POI> pois,
        IReadOnlyCollection<int>? onlyPoiIds,
        Action<(int done, int total)>? progressCallback = null)
    {
        var hasInternet = Connectivity.Current.NetworkAccess == NetworkAccess.Internet ||
                          Connectivity.Current.NetworkAccess == NetworkAccess.ConstrainedInternet;
        if (!hasInternet)
            return 0;

        var candidates = pois
            .Where(p => p.IsActive && p.ZoneType != "Area" && p.ZoneType != "District")
            .Where(p => onlyPoiIds == null || onlyPoiIds.Contains(p.Id))
            .OrderBy(DistanceToDefaultLocationSquared)
            .Take(18)
            .ToList();

        if (candidates.Count == 0)
            return 0;

        var done = 0;
        var cachedCount = 0;
        foreach (var poi in candidates)
        {
            try
            {
                var path = await FetchOsrmRouteGeoAsync(
                    AppConfig.DefaultLatitude,
                    AppConfig.DefaultLongitude,
                    poi.Latitude,
                    poi.Longitude);

                if (path is { Count: >= 4 })
                {
                    await OfflineRouteCacheService.SaveRouteAsync(
                        poi.Id,
                        AppConfig.DefaultLatitude,
                        AppConfig.DefaultLongitude,
                        path);
                    cachedCount++;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DataSync] Route preload failed for POI {poi.Id}: {ex.Message}");
            }

            done++;
            progressCallback?.Invoke((done, candidates.Count));
        }

        return cachedCount;
    }

    private async Task<IReadOnlyList<GeoCoordinate>?> FetchOsrmRouteGeoAsync(
        double srcLat,
        double srcLon,
        double dstLat,
        double dstLon)
    {
        try
        {
            var ic = CultureInfo.InvariantCulture;
            var url = $"https://router.project-osrm.org/route/v1/walking/" +
                      $"{srcLon.ToString(ic)},{srcLat.ToString(ic)};" +
                      $"{dstLon.ToString(ic)},{dstLat.ToString(ic)}" +
                      "?geometries=geojson&overview=full";

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            using var response = await _http.GetAsync(url, timeoutCts.Token);
            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync(timeoutCts.Token);
            using var doc = JsonDocument.Parse(json);
            var routes = doc.RootElement.GetProperty("routes");
            if (routes.GetArrayLength() == 0)
                return null;

            var points = routes[0]
                .GetProperty("geometry")
                .GetProperty("coordinates")
                .EnumerateArray()
                .Where(c => c.ValueKind == JsonValueKind.Array && c.GetArrayLength() >= 2)
                .Select(c => new GeoCoordinate(c[1].GetDouble(), c[0].GetDouble()))
                .ToList();

            return points.Count >= 2 ? points : null;
        }
        catch
        {
            return null;
        }
    }

    private static string GetOfflineMapCacheDbPath()
    {
        var cacheDir = Path.Combine(FileSystem.AppDataDirectory, "map_cache");
        Directory.CreateDirectory(cacheDir);
        return Path.Combine(cacheDir, "tiles.db");
    }

    private static long GetOfflineMapCacheSizeBytes()
    {
        var dbPath = GetOfflineMapCacheDbPath();
        return File.Exists(dbPath) ? new FileInfo(dbPath).Length : 0L;
    }

    private static void ClearOfflineMapCache()
    {
        var dbPath = GetOfflineMapCacheDbPath();
        if (File.Exists(dbPath))
            File.Delete(dbPath);
    }
}
