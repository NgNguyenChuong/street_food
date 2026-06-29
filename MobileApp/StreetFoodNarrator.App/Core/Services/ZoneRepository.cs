namespace StreetFoodNarrator.App.Core.Services;

using StreetFoodNarrator.App.Core.Models;
using StreetFoodNarrator.App.Core.Services;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using Microsoft.Maui.Networking;
using Microsoft.Maui.Storage;

/// <summary>
/// Repository for POI zones.
/// Provides mock data for offline/demo mode, with future MongoDB sync capability.
/// </summary>
public class ZoneRepository : IZoneRepository
{
    private List<POI> _zones = new();
    private static readonly JsonSerializerOptions ApiJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
    private readonly ILocalDatabaseService _localDb;
    private readonly HttpClient _httpClient;
    private bool _hasData = false;
    private bool _isLoaded = false;

    public bool IsSeeded => _hasData;
    public DataSourceKind CurrentDataSource { get; private set; } = DataSourceKind.Unknown;

    public ZoneRepository(ILocalDatabaseService localDb, HttpClient httpClient)
    {
        _localDb = localDb;
        _httpClient = httpClient;
        // Load bundled default_pois.json so the app works fully offline from first launch
        if (!AppConfig.UseBackendApi)
            _ = SeedFromBundledJsonAsync();
    }

    public List<POI> GetAllActiveZones()
    {
        return _zones.Where(z => z.IsActive).ToList();
    }

    public POI? GetById(int id)
        => _zones.FirstOrDefault(z => z.Id == id);

    /// <summary>
    /// Reset database và fetch lại từ API (hoặc mock nếu API không khả dụng).
    /// Sử dụng khi dữ liệu bị corrupt (ví dụ: tất cả POI có tọa độ 0,0).
    /// </summary>
    public async Task ResetAndSyncAsync()
    {
        Console.WriteLine("[ZoneRepository] 🔄 Resetting database and syncing from API...");
        
        // Xóa toàn bộ SQLite database
        await _localDb.DeleteAllPOIsAsync();
        Console.WriteLine("[ZoneRepository] ✓ Deleted all POIs from SQLite");
        
        // Reset data version để force fetch từ API
        Preferences.Remove(AppConfig.DataVersionKey);
        Console.WriteLine("[ZoneRepository] ✓ Reset data version");
        
        // Sync từ API (hoặc fallback mock nếu offline/error)
        await SyncFromMongoAsync();
        
        Console.WriteLine($"[ZoneRepository] ✓ Reset complete! Source: {CurrentDataSource}, POIs: {_zones.Count}");
    }

    public async Task LoadLocalAsync()
    {
        Console.WriteLine("[ZoneRepository] LoadLocalAsync starting...");
        await _localDb.InitializeAsync();
        _zones = await _localDb.GetAllActivePOIsAsync();
        _hasData = _zones.Count > 0;
        _isLoaded = true;
        if (_hasData && CurrentDataSource == DataSourceKind.Unknown)
            CurrentDataSource = DataSourceKind.SqliteCache;
        Console.WriteLine($"[ZoneRepository] ✓ LoadLocalAsync: Loaded {_zones.Count} POIs from SQLite");
        if (_zones.Count > 0)
        {
            Console.WriteLine($"[ZoneRepository] Sample POIs: {string.Join(", ", _zones.Take(3).Select(p => p.Name_Vi ?? p.Name_En))}");
        }
        else
        {
            Console.WriteLine("[ZoneRepository] ⚠️ SQLite is empty!");
        }
    }

    public async Task SyncFromMongoAsync()
    {
        if (!AppConfig.UseBackendApi)
        {
            if (!_hasData) await SeedFromBundledJsonAsync();
            else if (CurrentDataSource == DataSourceKind.Unknown) CurrentDataSource = DataSourceKind.BundledJson;
            return;
        }

        try
        {
            var netAccess = Connectivity.Current.NetworkAccess;
            var hasNetworkForApi = netAccess is NetworkAccess.Internet or NetworkAccess.ConstrainedInternet;
            if (!hasNetworkForApi && netAccess == NetworkAccess.Local)
                hasNetworkForApi = IsLikelyLocalApiHost(AppConfig.GetResolvedApiBaseUrl());

            if (!hasNetworkForApi)
            {
                // Offline: fall back to SQLite cache or bundled JSON so the app always works
                if (!_isLoaded) await LoadLocalAsync();
                if (!_hasData) await SeedFromBundledJsonAsync();
                else if (CurrentDataSource == DataSourceKind.Unknown) CurrentDataSource = DataSourceKind.SqliteCache;
                return;
            }

            var currentVersion = Preferences.Get(AppConfig.DataVersionKey, 0L);
            var response = await _httpClient.GetAsync(AppConfig.BuildApiUrl($"api/POIs/sync?sinceVersion={currentVersion}"));
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<PoiSyncResponse>(json, ApiJsonOptions);
            if (data == null)
            {
                // API response invalid — fall back to local data
                if (!_isLoaded) await LoadLocalAsync();
                if (_zones.Count == 0)
                {
                    await SeedFromBundledJsonAsync();
                    // SeedFromBundledJsonAsync sets _zones but keep SQLite as source of truth
                    await LoadLocalAsync();
                }
                return;
            }

            if (data.DataVersion <= currentVersion || data.Data.Count == 0)
            {
                // No new data from server – keep using what's in SQLite.
                // But we DID reach the API, so mark as LiveApi (online).
                if (!_isLoaded)
                    await LoadLocalAsync();

                // Recovery guard: if local cache is empty or suspiciously tiny,
                // force one full sync even when version appears unchanged.
                if (_zones.Count <= 1 && currentVersion > 0)
                {
                    var fullResponse = await _httpClient.GetAsync(AppConfig.BuildApiUrl("api/POIs/sync?sinceVersion=0"));
                    fullResponse.EnsureSuccessStatusCode();

                    var fullJson = await fullResponse.Content.ReadAsStringAsync();
                    var fullData = JsonSerializer.Deserialize<PoiSyncResponse>(fullJson, ApiJsonOptions);
                    if (fullData?.Data != null && fullData.Data.Count > 0)
                    {
                        var fullMapped = fullData.Data.Select(MapToPoi).ToList();
                        await _localDb.SavePOIsAsync(fullMapped);

                        _zones = fullMapped.Where(z => z.IsActive).ToList();
                        _hasData = _zones.Count > 0;
                        _isLoaded = true;
                        CurrentDataSource = DataSourceKind.LiveApi;
                        Preferences.Set(AppConfig.DataVersionKey, fullData.DataVersion);
                        Console.WriteLine($"[ZoneRepository] ✓ Full sync recovery: {fullMapped.Count} POIs ({_zones.Count} active)");
                        return;
                    }
                }

                if (_zones.Count == 0)
                {
                    await SeedFromBundledJsonAsync();
                    return;
                }

                CurrentDataSource = DataSourceKind.LiveApi;
                return;
            }

            var items = data.Data ?? new List<PoiDto>();
            var mapped = items.Select(MapToPoi).ToList();
            var incomingIds = new HashSet<int>(mapped.Select(p => p.Id));

            Console.WriteLine($"[ZoneRepository] API returned {mapped.Count} POIs, upserting into SQLite...");

            // Upsert all approved+active POIs from server.
            await _localDb.SavePOIsAsync(mapped);

            // Reconcile removals: if a POI is no longer in approved+active sync payload,
            // mark it inactive locally so app hides it.
            var existingPois = await _localDb.GetAllActivePOIsAsync();
            var toDeactivate = existingPois
                .Where(p => !incomingIds.Contains(p.Id))
                .ToList();
            if (toDeactivate.Count > 0)
            {
                foreach (var poi in toDeactivate)
                {
                    poi.IsActive = false;
                    await _localDb.SavePOIAsync(poi);
                }
                Console.WriteLine($"[ZoneRepository] Deactivated {toDeactivate.Count} POIs removed from approved sync set");
            }

            // Commit the new data version only AFTER new payload is persisted.
            Preferences.Set(AppConfig.DataVersionKey, data.DataVersion);

            _zones = mapped.Where(z => z.IsActive).ToList();
            _hasData = _zones.Count > 0;
            _isLoaded = true;
            CurrentDataSource = DataSourceKind.LiveApi;
            Console.WriteLine($"[ZoneRepository] ✓ Upserted {mapped.Count} POIs ({_zones.Count} active). Source: LiveApi");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ZoneRepository] ❌ SyncFromMongo FAILED: {ex.GetType().Name}: {ex.Message}");
            Console.WriteLine($"[ZoneRepository] NetworkAccess={Connectivity.Current.NetworkAccess}, ApiUrl={AppConfig.GetResolvedApiBaseUrl()}");
            System.Diagnostics.Debug.WriteLine($"[Repository] Sync failed: {ex.Message}");
            // On error, keep whatever we have in SQLite; do NOT clear it
            if (!_isLoaded) await LoadLocalAsync();
            if (_zones.Count == 0) await SeedFromBundledJsonAsync();
            else if (CurrentDataSource == DataSourceKind.Unknown) CurrentDataSource = DataSourceKind.SqliteCache;
        }
    }

    private static bool IsLikelyLocalApiHost(string? baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            return false;

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
            return false;

        var host = uri.Host;
        if (string.IsNullOrWhiteSpace(host))
            return false;

        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            host.Equals("10.0.2.2", StringComparison.OrdinalIgnoreCase) ||
            host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase))
            return true;

        if (!IPAddress.TryParse(host, out var ip))
            return false;

        var bytes = ip.GetAddressBytes();
        if (bytes.Length == 4)
        {
            if (bytes[0] == 10) return true;
            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return true;
            if (bytes[0] == 192 && bytes[1] == 168) return true;
            if (bytes[0] == 127) return true;
        }

        return IPAddress.IsLoopback(ip);
    }


    private static POI MapToPoi(PoiDto dto)
    {
        var zoneType = string.IsNullOrWhiteSpace(dto.ZoneType) ? "Spot" : dto.ZoneType;
        var zoneLevel = dto.ZoneLevel != 0
            ? dto.ZoneLevel
            : zoneType switch
            {
                "Area" => 1,
                "District" => 2,
                _ => 3
            };
        var cooldown = dto.CooldownMinutes != 0
            ? dto.CooldownMinutes
            : zoneType == "Spot" ? 0 : 30;
        var radius = string.Equals(zoneType, "Spot", StringComparison.OrdinalIgnoreCase)
            ? (int)Math.Round(AppConfig.NormalizeSpotRadiusMeters(dto.TriggerRadius))
            : (dto.TriggerRadius > 0 ? dto.TriggerRadius : 50);

        return new POI
        {
            Id = dto.POI_ID,
            ServerObjectId = dto.Id,
            Name_Vi = dto.Name_Vi ?? string.Empty,
            Name_En = dto.Name_En ?? string.Empty,
            Name_Zh = dto.Name_Zh,
            Description_Vi = dto.Description_Vi,
            Description_En = dto.Description_En,
            Description_Zh = dto.Description_Zh,
            Latitude = (double)dto.Latitude,
            Longitude = (double)dto.Longitude,
            MapUrl = dto.MapUrl,
            Radius = radius,
            ZoneType = zoneType,
            ZoneLevel = zoneLevel,
            Priority = dto.Priority != 0 ? dto.Priority : 5,
            CooldownMinutes = cooldown,
            ParentZoneId = dto.ParentZoneId,
            MaxPlaysPerSession = dto.MaxPlaysPerSession != 0 ? dto.MaxPlaysPerSession : 1,
            AudioCount = dto.AudioCount,
            VendorId = dto.VendorId,
            ReviewStatus = dto.ReviewStatus,
            AudioUrl_Vi = dto.AudioUrl_Vi,
            AudioUrl_En = dto.AudioUrl_En,
            AudioUrl_Zh = dto.AudioUrl_Zh,
            Script_Vi = dto.Script_Vi,
            Script_En = dto.Script_En,
            Script_Zh = dto.Script_Zh,
            ImageUrl = dto.ImageUrl,
            SignatureDish = dto.SignatureDish,
            FunFact = dto.FunFact,
            EstimatedHours = dto.OpeningHoursText,
            // New detail fields
            Address = dto.Address,
            Category = dto.Category,
            PhoneNumber = dto.PhoneNumber,
            AveragePrice = dto.AveragePrice,
            Rating = dto.Rating,
            NumReviews = dto.NumReviews,
            PriceLevel = dto.PriceLevel,
            PlayCount = dto.PlayCount,
            MeanPlay = dto.MeanPlay,
            NumLikes = dto.NumLikes,
            OpeningHoursText = dto.OpeningHoursText,
            SignatureDishesJson = dto.SignatureDishes != null && dto.SignatureDishes.Count > 0
                ? string.Join(",", dto.SignatureDishes)
                : dto.SignatureDish,
            PendingChangesJson = dto.PendingChanges != null
                ? JsonSerializer.Serialize(dto.PendingChanges)
                : null,
            IsDeleted = dto.IsDeleted,
            IsActive = dto.IsActive && !dto.IsDeleted,
            CreatedAt = dto.CreatedAt == default ? DateTime.Now : dto.CreatedAt,
            DeletedAt = dto.DeletedAt
        };
    }

    /// <summary>
    /// Loads POIs from the bundled Resources/Raw/default_pois.json asset.
    /// Falls back to hardcoded mock data if the file cannot be read.
    /// </summary>
    private async Task SeedFromBundledJsonAsync()
    {
        try
        {
            using var stream = await FileSystem.OpenAppPackageFileAsync("default_pois.json");
            using var reader = new System.IO.StreamReader(stream);
            var json = await reader.ReadToEndAsync();
            var dtos = JsonSerializer.Deserialize<List<PoiDto>>(json, ApiJsonOptions);
            if (dtos != null && dtos.Count > 0)
            {
                var allPois = dtos.Select(MapToPoi).ToList();
                _zones    = allPois.Where(z => z.IsActive).ToList();
                _hasData  = true;
                _isLoaded = true;
                CurrentDataSource = DataSourceKind.BundledJson;

                // Persist to SQLite so subsequent LoadLocalAsync() calls find the data
                await _localDb.SavePOIsAsync(allPois);

                System.Diagnostics.Debug.WriteLine(
                    $"[Repository] Loaded {_zones.Count} POIs from bundled JSON and saved to SQLite");
                return;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[Repository] Bundled JSON load error: {ex.Message} — falling back to mock data");
        }

        // Fallback if the JSON asset is missing during development
        SeedMockData();
    }

    private void SeedMockData()
    {
        _zones = new List<POI>
        {
            // ── LEVEL 1: Khu Vực Vĩnh Khánh ──────────────────────────
            new POI
            {
                Id = 1000,
                Name_Vi = "Khu Phố Ẩm Thực Vĩnh Khánh",
                Name_En = "Vinh Khanh Food Street",
                Description_Vi = "Khu ẩm thực nổi tiếng của Sài Gòn với hơn 30 quán ăn đặc sản đường phố.",
                Description_En = "Famous street food area in Saigon with over 30 specialty vendors.",
                Latitude  = StreetFoodNarrator.App.AppConfig.DefaultLatitude,
                Longitude = StreetFoodNarrator.App.AppConfig.DefaultLongitude,
                Radius = 200, // Area keeps large radius for ambient awareness
                ZoneType = "Area",
                ZoneLevel = 1,
                Priority = 2, // LOW: Area is general, lower than specific Spots
                CooldownMinutes = 30,
                MaxPlaysPerSession = 1,
                ParentZoneId = null,
                AudioUrl_Vi = "audio_vinhkhanh_vi.mp3",
                AudioUrl_En = "audio_vinhkhanh_en.mp3",
                ImageUrl = "https://example.com/vinhkhanh.jpg",
                SignatureDish = "Đa dạng món ăn đường phố",
                IsActive = true
            },

            // ── LEVEL 2: Điểm Cụ Thể (Spots) ─────────────────────────
            new POI
            {
                Id = 1011,
                Name_Vi = "Bánh Mì Ba Lẹ",
                Name_En = "Banh Mi Ba Le",
                Description_Vi = "Quán bánh mì nổi tiếng với nhân thịt đặc biệt và pate tự làm.",
                Description_En = "Famous banh mi shop with special meat filling and homemade pate.",
                Latitude  = StreetFoodNarrator.App.AppConfig.DefaultLatitude + 0.0004,
                Longitude = StreetFoodNarrator.App.AppConfig.DefaultLongitude - 0.0004,
                Radius = 25,
                ZoneType = "Spot",
                ZoneLevel = 3,
                Priority = 8, // HIGH: specific spot, high importance
                CooldownMinutes = 0,
                MaxPlaysPerSession = 1,
                ParentZoneId = 1000,
                AudioUrl_Vi = "audio_banhmi_vi.mp3",
                AudioUrl_En = "audio_banhmi_en.mp3",
                ImageUrl = "https://example.com/banhmi.jpg",
                SignatureDish = "Bánh mì đặc biệt",

                IsActive = true
            },

            new POI
            {
                Id = 1012,
                Name_Vi = "Gỏi Cuốn Tươi Ngon",
                Name_En = "Fresh Spring Rolls",
                Description_Vi = "Gỏi cuốn tôm thịt tươi ngon với nước mắm chua ngọt đặc biệt.",
                Description_En = "Fresh spring rolls with pork and shrimp, served with special fish sauce.",
                Latitude  = StreetFoodNarrator.App.AppConfig.DefaultLatitude - 0.0003,
                Longitude = StreetFoodNarrator.App.AppConfig.DefaultLongitude + 0.0005,
                Radius = 25,
                ZoneType = "Spot",
                ZoneLevel = 3,
                Priority = 8, // HIGH: specific spot, high importance
                CooldownMinutes = 0,
                MaxPlaysPerSession = 1,
                ParentZoneId = 1000,
                AudioUrl_Vi = "audio_goicuon_vi.mp3",
                AudioUrl_En = "audio_goicuon_en.mp3",
                ImageUrl = "https://example.com/goicuon.jpg",
                SignatureDish = "Gỏi cuốn tôm thịt",

                IsActive = true
            },

            new POI
            {
                Id = 1013,
                Name_Vi = "Phở Hòa",
                Name_En = "Pho Hoa",
                Description_Vi = "Phở bò truyền thống với nước dùng hầm 12 tiếng.",
                Description_En = "Traditional beef pho with 12-hour slow-cooked broth.",
                Latitude  = StreetFoodNarrator.App.AppConfig.DefaultLatitude + 0.0006,
                Longitude = StreetFoodNarrator.App.AppConfig.DefaultLongitude + 0.0002,
                Radius = 25,
                ZoneType = "Spot",
                ZoneLevel = 3,
                Priority = 7, // MEDIUM-HIGH: specific spot, slightly lower than Banh Mi
                CooldownMinutes = 0,
                MaxPlaysPerSession = 1,
                ParentZoneId = 1000,
                AudioUrl_Vi = "audio_pho_vi.mp3",
                AudioUrl_En = "audio_pho_en.mp3",
                ImageUrl = "https://example.com/pho.jpg",
                SignatureDish = "Phở bò tái",

                IsActive = true
            }
        };

        _hasData = true;
        _isLoaded = true;
        CurrentDataSource = DataSourceKind.MockFallback;
    }
}
