using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using StreetFoodNarrator.API.Data;
using StreetFoodNarrator.API.Models;

namespace StreetFoodNarrator.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class POIsController : ControllerBase
{
    private readonly MongoDbContext _db;
    private readonly MongoSequenceService _sequence;

    public POIsController(MongoDbContext db, MongoSequenceService sequence)
    {
        _db = db;
        _sequence = sequence;
    }

    /// <summary>
    /// Get all POIs with pagination and filters
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<POIListResponse>> GetPOIs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] string? category = null)
    {
        var filter = Builders<POI>.Filter.Eq(p => p.DeletedAt, null);

        // Search filter
        if (!string.IsNullOrEmpty(search))
        {
            var nameFilter = Builders<POI>.Filter.Regex(p => p.Name_Vi, new MongoDB.Bson.BsonRegularExpression(search, "i"));
            var addressFilter = Builders<POI>.Filter.Regex(p => p.Address, new MongoDB.Bson.BsonRegularExpression(search, "i"));
            filter &= Builders<POI>.Filter.Or(nameFilter, addressFilter);
        }

        // Active filter
        if (isActive.HasValue)
        {
            filter &= Builders<POI>.Filter.Eq(p => p.IsActive, isActive.Value);
        }

        // Category filter
        if (!string.IsNullOrWhiteSpace(category))
        {
            var categoryFilter = Builders<POI>.Filter.Regex(p => p.Category, new MongoDB.Bson.BsonRegularExpression(category, "i"));
            filter &= categoryFilter;
        }

        var total = await _db.POIs.CountDocumentsAsync(filter);
        var pois = await _db.POIs
            .Find(filter)
            .SortByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync();

        var poiDtos = await BuildPoiDtosAsync(pois);

        return Ok(new POIListResponse
        {
            Data = poiDtos,
            Total = (int)total,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(total / (double)pageSize)
        });
    }

    /// <summary>
    /// Sync POIs for offline-first clients.
    /// </summary>
    [HttpGet("sync")]
    public async Task<ActionResult<POISyncResponse>> SyncPOIs([FromQuery] long sinceVersion = 0)
    {
        var filter = Builders<POI>.Filter.Eq(p => p.DeletedAt, null);

        var latest = await _db.POIs
            .Find(filter)
            .SortByDescending(p => p.UpdatedAt)
            .Limit(1)
            .FirstOrDefaultAsync();

        var serverVersion = latest?.UpdatedAt?.Ticks ?? 0;
        if (serverVersion <= sinceVersion)
        {
            return Ok(new POISyncResponse
            {
                DataVersion = serverVersion,
                Data = new List<POIDto>()
            });
        }

        var pois = await _db.POIs
            .Find(filter)
            .SortByDescending(p => p.UpdatedAt)
            .ToListAsync();

        var poiDtos = await BuildPoiDtosAsync(pois);

        return Ok(new POISyncResponse
        {
            DataVersion = serverVersion,
            Data = poiDtos
        });
    }

    private async Task<List<POIDto>> BuildPoiDtosAsync(List<POI> pois)
    {
        var poiIds = pois.Select(p => p.POI_ID).ToList();
        var audioCounts = await _db.AudioContents
            .Aggregate()
            .Match(a => poiIds.Contains(a.POI_ID))
            .Group(a => a.POI_ID, g => new { POI_ID = g.Key, Count = g.Count() })
            .ToListAsync();

        var audioCountMap = audioCounts.ToDictionary(x => x.POI_ID, x => x.Count);
        return pois.Select(p => new POIDto
        {
            POI_ID = p.POI_ID,
            Name_Vi = p.Name_Vi,
            Name_En = p.Name_En,
            Name_Zh = p.Name_Zh,
            Description_Vi = p.Description_Vi,
            Description_En = p.Description_En,
            Description_Zh = p.Description_Zh,
            Address = p.Address ?? string.Empty,
            Latitude = (decimal)(p.Location?.Latitude ?? 0),
            Longitude = (decimal)(p.Location?.Longitude ?? 0),
            IsActive = p.IsActive,
            CreatedAt = p.CreatedAt,
            AudioCount = audioCountMap.TryGetValue(p.POI_ID, out var count) ? (int)count : 0,
            VendorId = p.VendorId,
            Category = p.Category,
            SignatureDish = p.SignatureDishes?.FirstOrDefault(),
            OpeningHoursText = p.OpeningHoursText,
            PhoneNumber = p.PhoneNumber,
            AveragePrice = p.AveragePrice,
            Rating = p.Rating,
            PriceLevel = p.PriceLevel,
            ImageUrl = p.ImageUrl,
            FunFact = p.FunFact,
            AudioUrl_Vi = p.AudioUrl_Vi,
            AudioUrl_En = p.AudioUrl_En,
            AudioUrl_Zh = p.AudioUrl_Zh,
            ZoneType = p.ZoneType,
            ZoneLevel = p.ZoneLevel,
            Priority = p.Priority,
            TriggerRadius = p.TriggerRadius,
            CooldownMinutes = p.CooldownMinutes,
            ParentZoneId = p.ParentZoneId,
            MaxPlaysPerSession = p.MaxPlaysPerSession
        }).ToList();
    }

    /// <summary>
    /// Get POI by ID with full details
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<POI>> GetPOI(int id)
    {
        var poi = await _db.POIs.Find(p => p.POI_ID == id && p.DeletedAt == null).FirstOrDefaultAsync();

        if (poi == null)
        {
            return NotFound(new { message = "POI not found" });
        }

        var audios = await _db.AudioContents.Find(a => a.POI_ID == id).ToListAsync();
        poi.AudioContents = audios;

        return Ok(poi);
    }

    /// <summary>
    /// Create a new POI
    /// </summary>
    [Authorize(Roles = "Admin,Vendor")]
    [HttpPost]
    public async Task<ActionResult<POI>> CreatePOI([FromBody] CreatePOIModel model)
    {
        var nextId = await _sequence.GetNextAsync("poi_id");
        var zoneType = string.IsNullOrWhiteSpace(model.ZoneType) ? "Spot" : model.ZoneType;
        var zoneLevel = model.ZoneLevel ?? (zoneType == "Area" ? 1 : zoneType == "District" ? 2 : 3);
        var cooldown = model.CooldownMinutes ?? (zoneType == "Spot" ? 0 : 30);
        var triggerRadius = model.TriggerRadius ?? 50;
        var priority = model.Priority ?? 5;
        var maxPlays = model.MaxPlaysPerSession ?? 1;
        var poi = new POI
        {
            POI_ID = nextId,
            Name_Vi = model.Name_Vi,
            Name_En = model.Name_En,
            Name_Zh = model.Name_Zh,
            Description_Vi = model.Description_Vi ?? string.Empty,
            Description_En = model.Description_En,
            Description_Zh = model.Description_Zh,
            Address = model.Address,
            Category = model.Category,
            SignatureDishes = BuildSignatureDishes(model.SignatureDish, model.SignatureDishes),
            Specialties = model.Specialties,
            History = model.History,
            Story = model.Story,
            OpeningHours = model.OpeningHours,
            OpeningHoursText = model.OpeningHoursText,
            PhoneNumber = model.PhoneNumber,
            AveragePrice = model.AveragePrice,
            PriceLevel = model.PriceLevel,
            Rating = model.Rating,
            Tags = model.Tags,
            ImageUrl = model.ImageUrl,
            ImageUrls = model.ImageUrls,
            FunFact = model.FunFact,
            AudioUrl_Vi = model.AudioUrl_Vi,
            AudioUrl_En = model.AudioUrl_En,
            AudioUrl_Zh = model.AudioUrl_Zh,
            ZoneType = zoneType,
            ZoneLevel = zoneLevel,
            Priority = priority,
            TriggerRadius = triggerRadius,
            CooldownMinutes = cooldown,
            ParentZoneId = model.ParentZoneId,
            MaxPlaysPerSession = maxPlays,
            IsActive = model.IsActive,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Location = GeoJsonLocation.FromLatLon((double)model.Latitude, (double)model.Longitude)
        };

        await _db.POIs.InsertOneAsync(poi);

        return CreatedAtAction(nameof(GetPOI), new { id = poi.POI_ID }, poi);
    }

    /// <summary>
    /// Update existing POI
    /// </summary>
    [Authorize(Roles = "Admin,Vendor")]
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdatePOI(int id, [FromBody] UpdatePOIModel model)
    {
        var poi = await _db.POIs.Find(p => p.POI_ID == id && p.DeletedAt == null).FirstOrDefaultAsync();

        if (poi == null)
        {
            return NotFound(new { message = "POI not found" });
        }

        var zoneType = string.IsNullOrWhiteSpace(model.ZoneType) ? poi.ZoneType : model.ZoneType;
        var zoneLevel = model.ZoneLevel ?? (zoneType == "Area" ? 1 : zoneType == "District" ? 2 : 3);
        var cooldown = model.CooldownMinutes ?? (zoneType == "Spot" ? 0 : 30);
        var triggerRadius = model.TriggerRadius ?? poi.TriggerRadius;
        var priority = model.Priority ?? poi.Priority;
        var maxPlays = model.MaxPlaysPerSession ?? poi.MaxPlaysPerSession;
        var latitude = model.Latitude.HasValue ? (double)model.Latitude.Value : poi.Location.Latitude;
        var longitude = model.Longitude.HasValue ? (double)model.Longitude.Value : poi.Location.Longitude;

        var update = Builders<POI>.Update
            .Set(p => p.Name_Vi, model.Name_Vi ?? poi.Name_Vi)
            .Set(p => p.Name_En, model.Name_En ?? poi.Name_En)
            .Set(p => p.Name_Zh, model.Name_Zh ?? poi.Name_Zh)
            .Set(p => p.Description_Vi, model.Description_Vi ?? poi.Description_Vi)
            .Set(p => p.Description_En, model.Description_En ?? poi.Description_En)
            .Set(p => p.Description_Zh, model.Description_Zh ?? poi.Description_Zh)
            .Set(p => p.Address, model.Address ?? poi.Address)
            .Set(p => p.Location, GeoJsonLocation.FromLatLon(latitude, longitude))
            .Set(p => p.Category, model.Category ?? poi.Category)
            .Set(p => p.SignatureDishes, BuildSignatureDishes(model.SignatureDish, model.SignatureDishes) ?? poi.SignatureDishes)
            .Set(p => p.Specialties, model.Specialties ?? poi.Specialties)
            .Set(p => p.History, model.History ?? poi.History)
            .Set(p => p.Story, model.Story ?? poi.Story)
            .Set(p => p.OpeningHours, model.OpeningHours ?? poi.OpeningHours)
            .Set(p => p.OpeningHoursText, model.OpeningHoursText ?? poi.OpeningHoursText)
            .Set(p => p.PhoneNumber, model.PhoneNumber ?? poi.PhoneNumber)
            .Set(p => p.AveragePrice, model.AveragePrice ?? poi.AveragePrice)
            .Set(p => p.PriceLevel, model.PriceLevel ?? poi.PriceLevel)
            .Set(p => p.Rating, model.Rating ?? poi.Rating)
            .Set(p => p.Tags, model.Tags ?? poi.Tags)
            .Set(p => p.ImageUrl, model.ImageUrl ?? poi.ImageUrl)
            .Set(p => p.ImageUrls, model.ImageUrls ?? poi.ImageUrls)
            .Set(p => p.FunFact, model.FunFact ?? poi.FunFact)
            .Set(p => p.AudioUrl_Vi, model.AudioUrl_Vi ?? poi.AudioUrl_Vi)
            .Set(p => p.AudioUrl_En, model.AudioUrl_En ?? poi.AudioUrl_En)
            .Set(p => p.AudioUrl_Zh, model.AudioUrl_Zh ?? poi.AudioUrl_Zh)
            .Set(p => p.ZoneType, zoneType)
            .Set(p => p.ZoneLevel, zoneLevel)
            .Set(p => p.Priority, priority)
            .Set(p => p.TriggerRadius, triggerRadius)
            .Set(p => p.CooldownMinutes, cooldown)
            .Set(p => p.ParentZoneId, model.ParentZoneId ?? poi.ParentZoneId)
            .Set(p => p.MaxPlaysPerSession, maxPlays)
            .Set(p => p.IsActive, model.IsActive ?? poi.IsActive)
            .Set(p => p.UpdatedAt, DateTime.UtcNow);

        await _db.POIs.UpdateOneAsync(p => p.POI_ID == id, update);

        return Ok(poi);
    }

    /// <summary>
    /// Delete POI (soft delete)
    /// </summary>
    [Authorize(Roles = "Admin")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeletePOI(int id)
    {
        var poi = await _db.POIs.Find(p => p.POI_ID == id && p.DeletedAt == null).FirstOrDefaultAsync();

        if (poi == null)
        {
            return NotFound(new { message = "POI not found" });
        }

        var update = Builders<POI>.Update.Set(p => p.DeletedAt, DateTime.UtcNow);
        await _db.POIs.UpdateOneAsync(p => p.POI_ID == id, update);

        return Ok(new { message = "POI deleted successfully" });
    }

    /// <summary>
    /// Get POI statistics
    /// </summary>
    [Authorize(Roles = "Admin")]
    [HttpGet("stats")]
    public async Task<ActionResult<object>> GetStats()
    {
        var activeFilter = Builders<POI>.Filter.Eq(p => p.IsActive, true) & Builders<POI>.Filter.Eq(p => p.DeletedAt, null);
        var inactiveFilter = Builders<POI>.Filter.Eq(p => p.IsActive, false) & Builders<POI>.Filter.Eq(p => p.DeletedAt, null);
        var totalFilter = Builders<POI>.Filter.Eq(p => p.DeletedAt, null);

        var totalPOIs = await _db.POIs.CountDocumentsAsync(totalFilter);
        var activePOIs = await _db.POIs.CountDocumentsAsync(activeFilter);
        var inactivePOIs = await _db.POIs.CountDocumentsAsync(inactiveFilter);
        var totalAudios = await _db.AudioContents.CountDocumentsAsync(Builders<AudioContent>.Filter.Empty);

        var poiIdsWithViAudio = await _db.AudioContents
            .DistinctAsync(a => a.POI_ID, a => a.Language == "vi-VN");
        var poiIdSet = await poiIdsWithViAudio.ToListAsync();
        var poisWithoutAudio = await _db.POIs.CountDocumentsAsync(
            Builders<POI>.Filter.Eq(p => p.DeletedAt, null) &
            Builders<POI>.Filter.Eq(p => p.IsActive, true) &
            Builders<POI>.Filter.Nin(p => p.POI_ID, poiIdSet) &
            Builders<POI>.Filter.Ne(p => p.Description_Vi, null) &
            Builders<POI>.Filter.Ne(p => p.Description_Vi, string.Empty));

        var stats = new
        {
            totalPOIs,
            activePOIs,
            inactivePOIs,
            totalAudios,
            poisWithoutAudio,
            pendingPOIs = 0
        };

        return Ok(stats);
    }

    /// <summary>
    /// Merges a single SignatureDish string into a SignatureDishes list.
    /// If both are provided, prepends single to the list (if not already present).
    /// </summary>
    private static List<string>? BuildSignatureDishes(string? single, List<string>? list)
    {
        if (list != null && list.Count > 0)
        {
            if (!string.IsNullOrWhiteSpace(single) && !list.Contains(single))
                list.Insert(0, single);
            return list;
        }
        if (!string.IsNullOrWhiteSpace(single))
            return new List<string> { single };
        return null;
    }
}

// DTOs
public class POIDto
{
    public int POI_ID { get; set; }
    public string Name_Vi { get; set; } = null!;
    public string? Name_En { get; set; }
    public string? Name_Ja { get; set; }
    public string? Name_Fr { get; set; }
    public string? Name_Ko { get; set; }
    public string? Name_Zh { get; set; }
    public string? Description_Vi { get; set; }
    public string? Description_En { get; set; }
    public string? Description_Ja { get; set; }
    public string? Description_Fr { get; set; }
    public string? Description_Ko { get; set; }
    public string? Description_Zh { get; set; }
    public string Address { get; set; } = null!;
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public int AudioCount { get; set; }
    public int? VendorId { get; set; }
    public string? Category { get; set; }
    public string? SignatureDish { get; set; }
    public string? OpeningHoursText { get; set; }
    public string? PhoneNumber { get; set; }
    public decimal? AveragePrice { get; set; }
    public double? Rating { get; set; }
    public int? PriceLevel { get; set; }
    public string? ImageUrl { get; set; }
    public string? FunFact { get; set; }
    public string? AudioUrl_Vi { get; set; }
    public string? AudioUrl_En { get; set; }
    public string? AudioUrl_Ja { get; set; }
    public string? AudioUrl_Fr { get; set; }
    public string? AudioUrl_Ko { get; set; }
    public string? AudioUrl_Zh { get; set; }
    public string? ZoneType { get; set; }
    public int ZoneLevel { get; set; }
    public int Priority { get; set; }
    public int TriggerRadius { get; set; }
    public int CooldownMinutes { get; set; }
    public int? ParentZoneId { get; set; }
    public int MaxPlaysPerSession { get; set; }
}

public class POIListResponse
{
    public List<POIDto> Data { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

public class POISyncResponse
{
    public long DataVersion { get; set; }
    public List<POIDto> Data { get; set; } = new();
}

public class CreatePOIModel
{
    public string Name_Vi { get; set; } = null!;
    public string? Name_En { get; set; }
    public string? Name_Ja { get; set; }
    public string? Name_Fr { get; set; }
    public string? Name_Ko { get; set; }
    public string? Name_Zh { get; set; }
    public string? Description_Vi { get; set; }
    public string? Description_En { get; set; }
    public string? Description_Ja { get; set; }
    public string? Description_Fr { get; set; }
    public string? Description_Ko { get; set; }
    public string? Description_Zh { get; set; }
    public string Address { get; set; } = null!;
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public string? Category { get; set; }
    public string? SignatureDish { get; set; }
    public List<string>? SignatureDishes { get; set; }
    public List<string>? Specialties { get; set; }
    public string? History { get; set; }
    public string? Story { get; set; }
    public List<string>? OpeningHours { get; set; }
    public string? OpeningHoursText { get; set; }
    public string? PhoneNumber { get; set; }
    public decimal? AveragePrice { get; set; }
    public int? PriceLevel { get; set; }
    public double? Rating { get; set; }
    public List<string>? Tags { get; set; }
    public string? ImageUrl { get; set; }
    public List<string>? ImageUrls { get; set; }
    public int? TriggerRadius { get; set; }
    public string? FunFact { get; set; }
    public string? AudioUrl_Vi { get; set; }
    public string? AudioUrl_En { get; set; }
    public string? AudioUrl_Ja { get; set; }
    public string? AudioUrl_Fr { get; set; }
    public string? AudioUrl_Ko { get; set; }
    public string? AudioUrl_Zh { get; set; }
    public string? ZoneType { get; set; }
    public int? ZoneLevel { get; set; }
    public int? Priority { get; set; }
    public int? CooldownMinutes { get; set; }
    public int? ParentZoneId { get; set; }
    public int? MaxPlaysPerSession { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UpdatePOIModel
{
    public string? Name_Vi { get; set; }
    public string? Name_En { get; set; }
    public string? Name_Ja { get; set; }
    public string? Name_Fr { get; set; }
    public string? Name_Ko { get; set; }
    public string? Name_Zh { get; set; }
    public string? Description_Vi { get; set; }
    public string? Description_En { get; set; }
    public string? Description_Ja { get; set; }
    public string? Description_Fr { get; set; }
    public string? Description_Ko { get; set; }
    public string? Description_Zh { get; set; }
    public string? Address { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string? Category { get; set; }
    public string? SignatureDish { get; set; }
    public List<string>? SignatureDishes { get; set; }
    public List<string>? Specialties { get; set; }
    public string? History { get; set; }
    public string? Story { get; set; }
    public List<string>? OpeningHours { get; set; }
    public string? OpeningHoursText { get; set; }
    public string? PhoneNumber { get; set; }
    public decimal? AveragePrice { get; set; }
    public int? PriceLevel { get; set; }
    public double? Rating { get; set; }
    public List<string>? Tags { get; set; }
    public string? ImageUrl { get; set; }
    public List<string>? ImageUrls { get; set; }
    public int? TriggerRadius { get; set; }
    public string? FunFact { get; set; }
    public string? AudioUrl_Vi { get; set; }
    public string? AudioUrl_En { get; set; }
    public string? AudioUrl_Ja { get; set; }
    public string? AudioUrl_Fr { get; set; }
    public string? AudioUrl_Ko { get; set; }
    public string? AudioUrl_Zh { get; set; }
    public string? ZoneType { get; set; }
    public int? ZoneLevel { get; set; }
    public int? Priority { get; set; }
    public int? CooldownMinutes { get; set; }
    public int? ParentZoneId { get; set; }
    public int? MaxPlaysPerSession { get; set; }
    public bool? IsActive { get; set; }
}
