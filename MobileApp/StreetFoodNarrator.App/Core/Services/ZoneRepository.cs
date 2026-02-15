namespace StreetFoodNarrator.App.Core.Services;

using StreetFoodNarrator.App.Core.Models;
using StreetFoodNarrator.App.Core.Services;
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
    private bool _hasData = false;
    private bool _isLoaded = false;

    public bool IsSeeded => _hasData;

    public ZoneRepository(ILocalDatabaseService localDb)
    {
        _localDb = localDb;
        if (!AppConfig.UseBackendApi)
            SeedMockData();
    }

    public List<POI> GetAllActiveZones()
    {
        return _zones.Where(z => z.IsActive).ToList();
    }

    public POI? GetById(int id)
        => _zones.FirstOrDefault(z => z.Id == id);

    public async Task LoadLocalAsync()
    {
        await _localDb.InitializeAsync();
        _zones = await _localDb.GetAllActivePOIsAsync();
        _hasData = _zones.Count > 0;
        _isLoaded = true;
    }

    public async Task SyncFromMongoAsync()
    {
        if (!AppConfig.UseBackendApi)
        {
            if (!_hasData) SeedMockData();
            return;
        }

        try
        {
            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
                return;

            var currentVersion = Preferences.Get(AppConfig.DataVersionKey, 0L);
            using var http = new HttpClient { BaseAddress = new Uri(AppConfig.ApiBaseUrl) };
            var response = await http.GetAsync($"api/POIs/sync?sinceVersion={currentVersion}");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<PoiSyncResponse>(json, ApiJsonOptions);
            if (data == null)
                return;

            if (data.DataVersion <= currentVersion || data.Data.Count == 0)
            {
                if (!_isLoaded)
                    await LoadLocalAsync();
                return;
            }

            var items = data.Data ?? new List<PoiDto>();

            var mapped = items.Select(MapToPoi).ToList();
            await _localDb.DeleteAllPOIsAsync();
            await _localDb.SavePOIsAsync(mapped);
            Preferences.Set(AppConfig.DataVersionKey, data.DataVersion);

            _zones = mapped.Where(z => z.IsActive).ToList();
            _hasData = _zones.Count > 0;
            _isLoaded = true;
            System.Diagnostics.Debug.WriteLine($"[Repository] Synced {_zones.Count} zones from API");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Repository] Sync failed: {ex.Message}");
            if (!_hasData && !AppConfig.UseBackendApi)
                SeedMockData();
        }
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

        return new POI
        {
            Id = dto.POI_ID,
            Name_Vi = dto.Name_Vi ?? string.Empty,
            Name_En = dto.Name_En ?? string.Empty,
            Name_Ja = dto.Name_Ja,
            Name_Fr = dto.Name_Fr,
            Name_Ko = dto.Name_Ko,
            Name_Zh = dto.Name_Zh,
            Description_Vi = dto.Description_Vi,
            Description_En = dto.Description_En,
            Description_Ja = dto.Description_Ja,
            Description_Fr = dto.Description_Fr,
            Description_Ko = dto.Description_Ko,
            Description_Zh = dto.Description_Zh,
            Latitude = (double)dto.Latitude,
            Longitude = (double)dto.Longitude,
            Radius = dto.TriggerRadius > 0 ? dto.TriggerRadius : 50,
            ZoneType = zoneType,
            ZoneLevel = zoneLevel,
            Priority = dto.Priority != 0 ? dto.Priority : 5,
            CooldownMinutes = cooldown,
            ParentZoneId = dto.ParentZoneId,
            MaxPlaysPerSession = dto.MaxPlaysPerSession != 0 ? dto.MaxPlaysPerSession : 1,
            AudioUrl_Vi = dto.AudioUrl_Vi,
            AudioUrl_En = dto.AudioUrl_En,
            AudioUrl_Ja = dto.AudioUrl_Ja,
            AudioUrl_Fr = dto.AudioUrl_Fr,
            AudioUrl_Ko = dto.AudioUrl_Ko,
            AudioUrl_Zh = dto.AudioUrl_Zh,
            ImageUrl = dto.ImageUrl,
            SignatureDish = dto.SignatureDish,
            FunFact = dto.FunFact,
            EstimatedHours = dto.OpeningHoursText,
            IsActive = dto.IsActive
        };
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
                Latitude = 10.7626,
                Longitude = 106.6927,
                Radius = 200,
                ZoneType = "Area",
                ZoneLevel = 1,
                Priority = 1,
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
                Latitude = 10.7628,
                Longitude = 106.6929,
                Radius = 15,
                ZoneType = "Spot",
                ZoneLevel = 3,
                Priority = 10,
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
                Latitude = 10.7632,
                Longitude = 106.6935,
                Radius = 12,
                ZoneType = "Spot",
                ZoneLevel = 3,
                Priority = 10,
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
                Latitude = 10.7625,
                Longitude = 106.6925,
                Radius = 10,
                ZoneType = "Spot",
                ZoneLevel = 3,
                Priority = 10,
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
    }
}
