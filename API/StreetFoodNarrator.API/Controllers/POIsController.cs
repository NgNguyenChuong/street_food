using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using StreetFoodNarrator.API.Data;
using StreetFoodNarrator.API.Models;
using System.Security.Claims;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Text;

namespace StreetFoodNarrator.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class POIsController : ControllerBase
{
    private readonly MongoDbContext _db;
    private readonly MongoSequenceService _sequence;
    private readonly IWebHostEnvironment _env;
    private readonly HttpClient _httpClient;
    private readonly ILogger<POIsController> _logger;
    private const string GpsTestApiKey = "streetfood-gps-test-mode-2026";
    private const string GpsTestCategory = "gps-test";
    private const string GpsTestReviewedBy = "gps-test-mode";
    private const int GpsTestTriggerRadiusMeters = 50;
    private static readonly GpsTestPoiSeed[] DefaultGpsTestPoiSeeds =
    [
        new("POI Test GPS Thuc Te 1", "Real GPS Test POI 1", "GPS shi di ce shi dian 1", 10.842078975289178, 106.60899362124417, 10),
        new("POI Test GPS Thuc Te 2", "Real GPS Test POI 2", "GPS shi di ce shi dian 2", 10.842098731748207, 106.60896276355663, 9),
        new("POI Test GPS Thuc Te 3", "Real GPS Test POI 3", "GPS shi di ce shi dian 3", 10.84207831569258, 106.60906602860645, 8)
    ];

    public POIsController(MongoDbContext db, MongoSequenceService sequence, IWebHostEnvironment env, ILogger<POIsController> logger)
    {
        _db = db;
        _sequence = sequence;
        _env = env;
        _httpClient = new HttpClient();
        _logger = logger;
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
        [FromQuery] string? category = null,
        [FromQuery] string? reviewStatus = null)
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

        if (!string.IsNullOrWhiteSpace(reviewStatus))
        {
            filter &= Builders<POI>.Filter.Eq(p => p.ReviewStatus, reviewStatus.ToLowerInvariant());
        }

        // Vendor scoping: authenticated vendor only sees their own POIs
        if (User.Identity?.IsAuthenticated == true && User.IsInRole("Vendor"))
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var vendor = await _db.VendorProfiles.Find(v => v.UserId == userId).FirstOrDefaultAsync();
            if (vendor == null)
            {
                var email = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name;
                if (!string.IsNullOrWhiteSpace(email))
                {
                    vendor = await _db.VendorProfiles.Find(v => v.ContactEmail == email).FirstOrDefaultAsync();
                    if (vendor != null && string.IsNullOrWhiteSpace(vendor.UserId))
                    {
                        var update = Builders<VendorProfile>.Update
                            .Set(v => v.UserId, userId)
                            .Set(v => v.UpdatedAt, DateTime.UtcNow);
                        await _db.VendorProfiles.UpdateOneAsync(v => v.VendorId == vendor.VendorId, update);
                    }
                }
            }
            if (vendor != null)
            {
                filter &= Builders<POI>.Filter.Eq(p => p.VendorId, vendor.VendorId);
            }
        }
        else if (User.Identity?.IsAuthenticated != true)
        {
            // Public users can only see approved and active POIs.
            filter &= Builders<POI>.Filter.Eq(p => p.ReviewStatus, "approved") &
                      Builders<POI>.Filter.Eq(p => p.IsActive, true);
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
    /// Uses POI_ID as version because it always increments (no race condition vs UpdatedAt).
    /// </summary>
    [HttpGet("sync")]
    public async Task<ActionResult<POISyncResponse>> SyncPOIs([FromQuery] long sinceVersion = 0)
    {
        // App clients only receive approved+active POIs.
        var visibleFilter = Builders<POI>.Filter.Eq(p => p.DeletedAt, null) &
                            Builders<POI>.Filter.Eq(p => p.ReviewStatus, "approved") &
                            Builders<POI>.Filter.Eq(p => p.IsActive, true);

        // Version must consider all POI moderation/update changes (not only currently visible rows),
        // otherwise approved/rejected transitions may not trigger sync on clients.
        var versionProjection = Builders<POI>.Projection.Expression(p => new { p.CreatedAt, p.UpdatedAt, p.DeletedAt });
        var versionRows = await _db.POIs.Find(Builders<POI>.Filter.Empty).Project(versionProjection).ToListAsync();

        long serverVersion = 0;
        foreach (var p in versionRows)
        {
            long ticks = Math.Max(
                Math.Max(p.UpdatedAt?.Ticks ?? 0, p.CreatedAt.Ticks),
                p.DeletedAt?.Ticks ?? 0);
            if (ticks > serverVersion)
            {
                serverVersion = ticks;
            }
        }

        if (serverVersion <= sinceVersion)
        {
            return Ok(new POISyncResponse
            {
                DataVersion = serverVersion,
                Data = new List<POIDto>()
            });
        }

        var pois = await _db.POIs
            .Find(visibleFilter)
            .ToListAsync();

        var poiDtos = await BuildPoiDtosAsync(pois);

        return Ok(new POISyncResponse
        {
            DataVersion = serverVersion,
            Data = poiDtos
        });
    }

    /// <summary>
    /// Return the current global data version as a string for mobile app update checks
    /// </summary>
    [HttpGet("/api/data/version")]
    public async Task<IActionResult> GetDataVersion()
    {
        var projection = Builders<POI>.Projection.Expression(p => new { p.CreatedAt, p.UpdatedAt, p.DeletedAt });
        var pois = await _db.POIs.Find(Builders<POI>.Filter.Empty).Project(projection).ToListAsync();

        long serverVersion = 0;
        foreach (var p in pois)
        {
            long ticks = Math.Max(
                Math.Max(p.UpdatedAt?.Ticks ?? 0, p.CreatedAt.Ticks),
                p.DeletedAt?.Ticks ?? 0);
            if (ticks > serverVersion) serverVersion = ticks;
        }

        return Ok(new { version = serverVersion.ToString() });
    }

    /// <summary>
    /// Ensures configured GPS-test POIs (default: 3 fixed points) exist and are active.
    /// </summary>
    [HttpPost("test-mode/ensure-nearby")]
    public async Task<ActionResult<List<POIDto>>> EnsureNearbyGpsTestPois(
        [FromBody] EnsureGpsTestPoiRequest? request,
        [FromHeader(Name = "X-Gps-Test-Key")] string? testKey = null)
    {
        if (!string.Equals(testKey, GpsTestApiKey, StringComparison.Ordinal))
            return Unauthorized(new { message = "Invalid GPS test key" });

        var seeds = BuildGpsTestPoiSeeds(request);
        var baseAddress = string.IsNullOrWhiteSpace(request?.Address)
            ? "GPS test fixed coordinates"
            : request!.Address!.Trim();

        var now = DateTime.UtcNow;
        var affectedIds = new List<int>();

        var expectedNames = seeds.Select(s => s.NameVi).ToList();
        if (expectedNames.Count > 0)
        {
            var staleFilter = Builders<POI>.Filter.Eq(p => p.Category, GpsTestCategory) &
                              Builders<POI>.Filter.Nin(p => p.Name_Vi, expectedNames);
            _ = await _db.POIs.DeleteManyAsync(staleFilter);
        }

        for (var i = 0; i < seeds.Count; i++)
        {
            var seed = seeds[i];
            var poiLat = seed.Latitude;
            var poiLon = seed.Longitude;
            var poiAddress = $"{baseAddress} (point {i + 1}: {poiLat:F6}, {poiLon:F6})";

            var testPoiFilter = Builders<POI>.Filter.Eq(p => p.Category, GpsTestCategory) &
                                Builders<POI>.Filter.Eq(p => p.Name_Vi, seed.NameVi);

            var existing = await _db.POIs
                .Find(testPoiFilter)
                .SortByDescending(p => p.UpdatedAt)
                .FirstOrDefaultAsync();

            if (existing == null)
            {
                var nextId = await _sequence.GetNextAsync("poi_id");
                var created = new POI
                {
                    POI_ID = nextId,
                    Name_Vi = seed.NameVi,
                    Name_En = seed.NameEn,
                    Name_Zh = seed.NameZh,
                    Description_Vi = $"Diem mau {i + 1} de test geofence GPS ngoai thuc te.",
                    Description_En = $"Sample point {i + 1} for real-world GPS geofence testing.",
                    Description_Zh = $"yong yu shi di GPS geofence ce shi de shi li dian {i + 1}.",
                    Address = poiAddress,
                    Category = GpsTestCategory,
                    SignatureDishes = new List<string> { "GPS Test" },
                    FunFact = "Auto-generated by GPS test mode.",
                    Script_Vi = $"Ban da vao POI test GPS thuc te so {i + 1}.",
                    Script_En = $"You have entered real GPS test POI #{i + 1}.",
                    Script_Zh = $"nin yi jin ru GPS shi ce dian {i + 1}.",
                    ZoneType = "Spot",
                    ZoneLevel = 3,
                    Priority = seed.Priority,
                    TriggerRadius = GpsTestTriggerRadiusMeters,
                    CooldownMinutes = 0,
                    MaxPlaysPerSession = 99,
                    IsActive = true,
                    ReviewStatus = "approved",
                    ReviewNote = "Auto-generated for GPS test mode",
                    ReviewedAt = now,
                    ReviewedBy = GpsTestReviewedBy,
                    CreatedAt = now,
                    UpdatedAt = now,
                    IsDeleted = false,
                    DeletedAt = null,
                    Location = GeoJsonLocation.FromLatLon(poiLat, poiLon)
                };

                await _db.POIs.InsertOneAsync(created);
                affectedIds.Add(created.POI_ID);
                continue;
            }

            var update = Builders<POI>.Update
                .Set(p => p.Name_Vi, seed.NameVi)
                .Set(p => p.Name_En, seed.NameEn)
                .Set(p => p.Name_Zh, seed.NameZh)
                .Set(p => p.Description_Vi, $"Diem mau {i + 1} de test geofence GPS ngoai thuc te.")
                .Set(p => p.Description_En, $"Sample point {i + 1} for real-world GPS geofence testing.")
                .Set(p => p.Description_Zh, $"yong yu shi di GPS geofence ce shi de shi li dian {i + 1}.")
                .Set(p => p.Address, poiAddress)
                .Set(p => p.Category, GpsTestCategory)
                .Set(p => p.SignatureDishes, new List<string> { "GPS Test" })
                .Set(p => p.FunFact, "Auto-generated by GPS test mode.")
                .Set(p => p.Script_Vi, $"Ban da vao POI test GPS thuc te so {i + 1}.")
                .Set(p => p.Script_En, $"You have entered real GPS test POI #{i + 1}.")
                .Set(p => p.Script_Zh, $"nin yi jin ru GPS shi ce dian {i + 1}.")
                .Set(p => p.ZoneType, "Spot")
                .Set(p => p.ZoneLevel, 3)
                .Set(p => p.Priority, seed.Priority)
                .Set(p => p.TriggerRadius, GpsTestTriggerRadiusMeters)
                .Set(p => p.CooldownMinutes, 0)
                .Set(p => p.MaxPlaysPerSession, 99)
                .Set(p => p.IsActive, true)
                .Set(p => p.ReviewStatus, "approved")
                .Set(p => p.ReviewNote, "Auto-generated for GPS test mode")
                .Set(p => p.ReviewedAt, now)
                .Set(p => p.ReviewedBy, GpsTestReviewedBy)
                .Set(p => p.IsDeleted, false)
                .Set(p => p.DeletedAt, null)
                .Set(p => p.Location, GeoJsonLocation.FromLatLon(poiLat, poiLon))
                .Set(p => p.UpdatedAt, now);

            await _db.POIs.UpdateOneAsync(p => p.POI_ID == existing.POI_ID, update);
            affectedIds.Add(existing.POI_ID);
        }

        if (affectedIds.Count == 0)
            return Ok(new List<POIDto>());

        var affectedPois = await _db.POIs
            .Find(p => affectedIds.Contains(p.POI_ID))
            .ToListAsync();

        var dtos = await BuildPoiDtosAsync(affectedPois);
        return Ok(dtos.OrderBy(x => x.POI_ID).ToList());
    }

    /// <summary>
    /// Hard-delete all GPS test POIs created for field testing.
    /// </summary>
    [HttpPost("test-mode/cleanup")]
    public async Task<ActionResult<object>> CleanupGpsTestPois(
        [FromHeader(Name = "X-Gps-Test-Key")] string? testKey = null)
    {
        if (!string.Equals(testKey, GpsTestApiKey, StringComparison.Ordinal))
            return Unauthorized(new { message = "Invalid GPS test key" });

        var filter = Builders<POI>.Filter.Eq(p => p.Category, GpsTestCategory);
        var result = await _db.POIs.DeleteManyAsync(filter);

        // Hard-delete removes rows, so explicitly touch one visible POI to bump sync version.
        var touchFilter = Builders<POI>.Filter.Ne(p => p.Category, GpsTestCategory) &
                          Builders<POI>.Filter.Eq(p => p.DeletedAt, null);
        var touchUpdate = Builders<POI>.Update.Set(p => p.UpdatedAt, DateTime.UtcNow);
        _ = await _db.POIs.UpdateOneAsync(touchFilter, touchUpdate);

        return Ok(new { deletedCount = result.DeletedCount });
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
            Id = p.Id.ToString(),
            POI_ID = p.POI_ID,
            Name_Vi = p.Name_Vi,
            Name_En = p.Name_En,
            Name_Zh = p.Name_Zh,
            Description_Vi = p.Description_Vi,
            Description_En = p.Description_En,
            Description_Zh = p.Description_Zh,
            Address = p.Address ?? string.Empty,
            MapUrl = p.MapUrl,
            Latitude = (decimal)(p.Location?.Latitude ?? 0),
            Longitude = (decimal)(p.Location?.Longitude ?? 0),
            IsActive = p.IsActive,
            CreatedAt = p.CreatedAt,
            AudioCount = audioCountMap.TryGetValue(p.POI_ID, out var count) ? (int)count : 0,
            VendorId = p.VendorId,
            ReviewStatus = p.ReviewStatus,
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
            Script_Vi = p.Script_Vi,
            Script_En = p.Script_En,
            Script_Zh = p.Script_Zh,
            ZoneType = p.ZoneType,
            ZoneLevel = p.ZoneLevel,
            Priority = p.Priority,
            TriggerRadius = p.TriggerRadius,
            CooldownMinutes = p.CooldownMinutes,
            ParentZoneId = p.ParentZoneId,
            MaxPlaysPerSession = p.MaxPlaysPerSession,
            PendingChanges = p.PendingChanges
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

        // Public access: only approved + active content.
        if (User.Identity?.IsAuthenticated != true &&
            (poi.ReviewStatus != "approved" || !poi.IsActive))
        {
            return NotFound(new { message = "POI not found" });
        }

        // Attach audio contents with least-privilege:
        // - Admin: all audio
        // - Vendor: only if owns this POI
        // - Anonymous: published + active + not deleted only
        var canSeeAll = User.Identity?.IsAuthenticated == true && User.IsInRole("Admin");
        var isVendor = User.Identity?.IsAuthenticated == true && User.IsInRole("Vendor");

        if (canSeeAll)
        {
            var audios = await _db.AudioContents.Find(a => a.POI_ID == id).ToListAsync();
            poi.AudioContents = audios;
        }
        else if (isVendor)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var vendor = !string.IsNullOrWhiteSpace(userId)
                ? await _db.VendorProfiles.Find(v => v.UserId == userId).FirstOrDefaultAsync()
                : null;

            if (vendor != null && poi.VendorId == vendor.VendorId)
            {
                var audios = await _db.AudioContents.Find(a => a.POI_ID == id).ToListAsync();
                poi.AudioContents = audios;
            }
            else
            {
                return Forbid();
            }
        }
        else
        {
            var publishedFilter = Builders<AudioContent>.Filter.And(
                Builders<AudioContent>.Filter.Eq(a => a.POI_ID, id),
                Builders<AudioContent>.Filter.Eq(a => a.Status, AudioStatuses.Published),
                Builders<AudioContent>.Filter.Eq(a => a.IsActive, true),
                Builders<AudioContent>.Filter.Eq(a => a.IsDeleted, false)
            );
            var audios = await _db.AudioContents.Find(publishedFilter).ToListAsync();
            poi.AudioContents = audios;
        }

        return Ok(poi);
    }

    /// <summary>
    /// Create a new POI
    /// </summary>
    [Authorize(Roles = "Vendor")]
    [HttpPost]
    public async Task<ActionResult<POI>> CreatePOI([FromBody] CreatePOIModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Name_Vi))
            return BadRequest(new { message = "Name_Vi is required" });

        if (string.IsNullOrWhiteSpace(model.Description_Vi))
            return BadRequest(new { message = "Description_Vi is required" });

        if (string.IsNullOrWhiteSpace(model.Address))
            return BadRequest(new { message = "Address is required" });

        var normalizedMapUrl = NormalizeMapUrl(model.MapUrl);
        if (!string.IsNullOrWhiteSpace(model.MapUrl) && normalizedMapUrl == null)
            return BadRequest(new { message = "MapUrl is invalid. Please provide a valid http/https URL." });

        // Vendor must only create POIs under their own VendorId.
        int? vendorId = null;
        string? actorUserId = null;
        if (User.IsInRole("Vendor"))
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Forbid();
            }

            actorUserId = userId;

            var vendor = await _db.VendorProfiles.Find(v => v.UserId == userId).FirstOrDefaultAsync();
            if (vendor == null)
            {
                return Forbid();
            }

            if (!string.Equals(vendor.VerificationStatus, "approved", StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }

            vendorId = vendor.VendorId;
        }

        if (!vendorId.HasValue)
            return Forbid();

        var idempotencyKey = Request.Headers["Idempotency-Key"].FirstOrDefault()?.Trim();
        if (!string.IsNullOrWhiteSpace(idempotencyKey) && !string.IsNullOrWhiteSpace(actorUserId))
        {
            var existingSubmission = await _db.SubmissionIdempotencies
                .Find(x => x.IdempotencyKey == idempotencyKey && x.ActorUserId == actorUserId && x.TargetType == "poi_create")
                .FirstOrDefaultAsync();

            if (existingSubmission?.ResponsePoiId is int existingPoiId)
            {
                var existingPoi = await _db.POIs
                    .Find(p => p.POI_ID == existingPoiId && p.DeletedAt == null)
                    .FirstOrDefaultAsync();

                if (existingPoi != null)
                    return Ok(existingPoi);
            }
        }

        var duplicate = await FindDuplicatePoiAsync(vendorId.Value, model);
        if (duplicate != null)
        {
            return Conflict(new
            {
                code = "DUPLICATE_POI_SUSPECTED",
                message = "A similar POI already exists for this vendor",
                existingPoiId = duplicate.POI_ID
            });
        }

        var nextId = await _sequence.GetNextAsync("poi_id");
        var zoneType = string.IsNullOrWhiteSpace(model.ZoneType) ? "Spot" : model.ZoneType;
        var zoneLevel = model.ZoneLevel ?? (zoneType == "Area" ? 1 : zoneType == "District" ? 2 : 3);
        var cooldown = model.CooldownMinutes ?? (zoneType == "Spot" ? 0 : 30);
        var triggerRadius = model.TriggerRadius ?? 50;
        var priority = model.Priority ?? 5;
        var maxPlays = model.MaxPlaysPerSession ?? 1;
        var reviewStatus = "pending";

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
            MapUrl = normalizedMapUrl,
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
            Script_Vi = model.Script_Vi,
            Script_En = model.Script_En,
            Script_Zh = model.Script_Zh,
            ZoneType = zoneType,
            ZoneLevel = zoneLevel,
            Priority = priority,
            TriggerRadius = triggerRadius,
            CooldownMinutes = cooldown,
            ParentZoneId = model.ParentZoneId,
            MaxPlaysPerSession = maxPlays,
            IsActive = false,
            VendorId = vendorId,
            ReviewStatus = reviewStatus,
            ReviewNote = null,
            ReviewedAt = null,
            ReviewedBy = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Location = GeoJsonLocation.FromLatLon((double)model.Latitude, (double)model.Longitude)
        };

        await _db.POIs.InsertOneAsync(poi);
        _logger.LogInformation("CreatePOI saved MapUrl for POI_ID={PoiId}: {MapUrl}", poi.POI_ID, poi.MapUrl ?? "<null>");

        if (!string.IsNullOrWhiteSpace(idempotencyKey) && !string.IsNullOrWhiteSpace(actorUserId))
        {
            var idempotencyRecord = new SubmissionIdempotency
            {
                IdempotencyKey = idempotencyKey,
                ActorUserId = actorUserId,
                TargetType = "poi_create",
                TargetHash = ComputePoiSubmissionHash(vendorId.Value, model),
                ResponsePoiId = poi.POI_ID,
                CreatedAt = DateTime.UtcNow
            };

            try
            {
                await _db.SubmissionIdempotencies.InsertOneAsync(idempotencyRecord);
            }
            catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
            {
                // Another concurrent request with the same idempotency key has already been persisted.
            }
        }

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

        // Vendor must only update their own POIs
        if (User.IsInRole("Vendor"))
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Forbid();
            }

            var vendor = await _db.VendorProfiles.Find(v => v.UserId == userId).FirstOrDefaultAsync();
            if (vendor == null || poi.VendorId != vendor.VendorId)
            {
                return Forbid();
            }

            if (!string.Equals(vendor.VerificationStatus, "approved", StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }
        }

        int? effectiveVendorId = poi.VendorId;
        var isAdmin = User.IsInRole("Admin");

        if (!effectiveVendorId.HasValue)
        {
            return BadRequest(new { message = "POI must belong to a vendor" });
        }

        var zoneType = isAdmin
            ? (string.IsNullOrWhiteSpace(model.ZoneType) ? poi.ZoneType : model.ZoneType)
            : poi.ZoneType;
        var zoneLevel = isAdmin
            ? (model.ZoneLevel ?? (zoneType == "Area" ? 1 : zoneType == "District" ? 2 : 3))
            : poi.ZoneLevel;
        var cooldown = isAdmin
            ? (model.CooldownMinutes ?? (zoneType == "Spot" ? 0 : 30))
            : poi.CooldownMinutes;
        var triggerRadius = isAdmin ? (model.TriggerRadius ?? poi.TriggerRadius) : poi.TriggerRadius;
        var priority = isAdmin ? (model.Priority ?? poi.Priority) : poi.Priority;
        var maxPlays = isAdmin ? (model.MaxPlaysPerSession ?? poi.MaxPlaysPerSession) : poi.MaxPlaysPerSession;
        var latitude = model.Latitude.HasValue ? (double)model.Latitude.Value : poi.Location.Latitude;
        var longitude = model.Longitude.HasValue ? (double)model.Longitude.Value : poi.Location.Longitude;

        var imageUrl = !string.IsNullOrWhiteSpace(model.ImageUrl) ? model.ImageUrl : poi.ImageUrl;
        var imageUrls = isAdmin
            ? (model.ImageUrls ?? poi.ImageUrls)
            : (!string.IsNullOrWhiteSpace(model.ImageUrl)
                ? new List<string> { model.ImageUrl }
                : poi.ImageUrls);
        var parentZoneId = isAdmin ? (model.ParentZoneId ?? poi.ParentZoneId) : poi.ParentZoneId;
        var audioUrlVi = isAdmin ? (model.AudioUrl_Vi ?? poi.AudioUrl_Vi) : poi.AudioUrl_Vi;
        var audioUrlEn = isAdmin ? (model.AudioUrl_En ?? poi.AudioUrl_En) : poi.AudioUrl_En;
        var audioUrlZh = isAdmin ? (model.AudioUrl_Zh ?? poi.AudioUrl_Zh) : poi.AudioUrl_Zh;
        var scriptVi = isAdmin ? (model.Script_Vi ?? poi.Script_Vi) : poi.Script_Vi;
        var scriptEn = isAdmin ? (model.Script_En ?? poi.Script_En) : poi.Script_En;
        var scriptZh = isAdmin ? (model.Script_Zh ?? poi.Script_Zh) : poi.Script_Zh;
        var normalizedMapUrl = model.MapUrl == null ? null : NormalizeMapUrl(model.MapUrl);

        if (model.MapUrl != null && normalizedMapUrl == null && !string.IsNullOrWhiteSpace(model.MapUrl))
            return BadRequest(new { message = "MapUrl is invalid. Please provide a valid http/https URL." });

        var finalMapUrl = model.MapUrl == null
            ? poi.MapUrl
            : normalizedMapUrl;

        var update = Builders<POI>.Update
            .Set(p => p.Name_Vi, model.Name_Vi ?? poi.Name_Vi)
            .Set(p => p.Name_En, model.Name_En ?? poi.Name_En)
            .Set(p => p.Name_Zh, model.Name_Zh ?? poi.Name_Zh)
            .Set(p => p.Description_Vi, model.Description_Vi ?? poi.Description_Vi)
            .Set(p => p.Description_En, model.Description_En ?? poi.Description_En)
            .Set(p => p.Description_Zh, model.Description_Zh ?? poi.Description_Zh)
            .Set(p => p.Address, model.Address ?? poi.Address)
            .Set(p => p.MapUrl, finalMapUrl)
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
            .Set(p => p.ImageUrl, imageUrl)
            .Set(p => p.ImageUrls, imageUrls)
            .Set(p => p.FunFact, model.FunFact ?? poi.FunFact)
            .Set(p => p.AudioUrl_Vi, audioUrlVi)
            .Set(p => p.AudioUrl_En, audioUrlEn)
            .Set(p => p.AudioUrl_Zh, audioUrlZh)
            .Set(p => p.Script_Vi, scriptVi)
            .Set(p => p.Script_En, scriptEn)
            .Set(p => p.Script_Zh, scriptZh)
            .Set(p => p.ZoneType, zoneType)
            .Set(p => p.ZoneLevel, zoneLevel)
            .Set(p => p.Priority, priority)
            .Set(p => p.TriggerRadius, triggerRadius)
            .Set(p => p.CooldownMinutes, cooldown)
            .Set(p => p.ParentZoneId, parentZoneId)
            .Set(p => p.MaxPlaysPerSession, maxPlays)
            .Set(p => p.UpdatedAt, DateTime.UtcNow);

        if (effectiveVendorId.HasValue)
        {
            update = update.Set(p => p.VendorId, effectiveVendorId.Value);
        }

        if (isAdmin)
        {
            var targetIsActive = model.IsActive ?? poi.IsActive;
            update = update.Set(p => p.IsActive, targetIsActive);

            // Admin keeps moderation control; turning on implies approved state.
            if (targetIsActive)
            {
                update = update
                    .Set(p => p.ReviewStatus, "approved")
                    .Set(p => p.ReviewNote, null)
                    .Set(p => p.ReviewedAt, DateTime.UtcNow)
                    .Set(p => p.ReviewedBy, User.Identity?.Name ?? "admin");
            }
        }
        else
        {
            var targetIsActive = model.IsActive ?? poi.IsActive;
            var isApproved = string.Equals(poi.ReviewStatus, "approved", StringComparison.OrdinalIgnoreCase);

            // Vendor edits are applied immediately, but while POI is waiting approval
            // vendor is not allowed to change the active state.
            if (!isApproved)
            {
                update = update.Set(p => p.IsActive, false);
            }
            else
            {
                update = update.Set(p => p.IsActive, targetIsActive);
            }

            // Clear legacy pending diff artifacts when vendor saves using direct-apply flow.
            update = update
                .Set(p => p.PendingChanges, null)
                .Set(p => p.ReviewNote, null);
        }

        await _db.POIs.UpdateOneAsync(p => p.POI_ID == id, update);

        return Ok(poi);
    }

    /// <summary>
    /// Admin review POI (approve/reject)
    /// </summary>
    [Authorize(Roles = "Admin")]
    [HttpPost("{id}/review")]
    public async Task<IActionResult> ReviewPOI(int id, [FromBody] ReviewPOIRequest request)
    {
        var status = request.Status?.ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(status) || (status != "approved" && status != "rejected"))
            return BadRequest(new { message = "Invalid status. Allowed: approved, rejected" });

        var poi = await _db.POIs
            .Find(p => p.POI_ID == id && p.DeletedAt == null)
            .FirstOrDefaultAsync();

        if (poi == null)
            return NotFound(new { message = "POI not found" });

        if (status == "approved")
        {
            if (!poi.VendorId.HasValue)
                return BadRequest(new { message = "Cannot approve POI without VendorId" });

            var vendor = await _db.VendorProfiles
                .Find(v => v.VendorId == poi.VendorId.Value)
                .FirstOrDefaultAsync();

            if (vendor == null)
                return BadRequest(new { message = "Cannot approve POI because VendorId does not exist" });

            if (!string.Equals(vendor.VerificationStatus, "approved", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { message = "Cannot approve POI while vendor is not approved" });
        }

        var update = Builders<POI>.Update
            .Set(p => p.ReviewStatus, status)
            .Set(p => p.ReviewNote, request.Note)
            .Set(p => p.ReviewedAt, DateTime.UtcNow)
            .Set(p => p.ReviewedBy, User.Identity?.Name ?? "admin")
            .Set(p => p.IsActive, status == "approved")
            .Set(p => p.PendingChanges, null)
            .Set(p => p.UpdatedAt, DateTime.UtcNow);

        // When rejected: REVERT the actual field data back to original values stored in PendingChanges
        if (status == "rejected" && poi.PendingChanges != null)
        {
            foreach (var kv in poi.PendingChanges)
            {
                var oldVal = kv.Value?.Old;
                if (oldVal == null) continue;

                switch (kv.Key)
                {
                    case "T\u00ean qu\u00e1n":
                        update = update.Set(p => p.Name_Vi, oldVal);
                        break;

                    case "\u0110\u1ecba ch\u1ec9":
                        update = update.Set(p => p.Address, oldVal);
                        break;

                    case "V\u1ecb tr\u00ed GPS":
                        // OldVal format: "lat, lon"
                        var parts = oldVal.Split(',');
                        if (parts.Length == 2 &&
                            double.TryParse(parts[0].Trim(), System.Globalization.NumberStyles.Any,
                                System.Globalization.CultureInfo.InvariantCulture, out var oldLat) &&
                            double.TryParse(parts[1].Trim(), System.Globalization.NumberStyles.Any,
                                System.Globalization.CultureInfo.InvariantCulture, out var oldLon))
                        {
                            update = update.Set(p => p.Location, GeoJsonLocation.FromLatLon(oldLat, oldLon));
                        }
                        break;
                }
            }
        }

        await _db.POIs.UpdateOneAsync(p => p.POI_ID == id && p.DeletedAt == null, update);

        var resultMsg = status == "rejected"
            ? "Đã từ chối và khôi phục lại dữ liệu gốc cho POI."
            : "POI đã được duyệt thành công.";
        return Ok(new { message = resultMsg });
    }

    /// <summary>
    /// Check integrity of Vendor-POI relation.
    /// </summary>
    [Authorize(Roles = "Admin")]
    [HttpGet("vendor-integrity")]
    public async Task<IActionResult> GetVendorPoiIntegrity()
    {
        var poiFilter = Builders<POI>.Filter.Eq(p => p.DeletedAt, null);
        var pois = await _db.POIs.Find(poiFilter).ToListAsync();
        var vendors = await _db.VendorProfiles.Find(Builders<VendorProfile>.Filter.Empty).ToListAsync();

        var vendorIdSet = vendors.Select(v => v.VendorId).ToHashSet();
        var assignedPois = pois.Where(p => p.VendorId.HasValue).ToList();
        var unassignedPois = pois.Where(p => !p.VendorId.HasValue).Select(p => p.POI_ID).ToList();
        var orphanPoiIds = assignedPois
            .Where(p => !vendorIdSet.Contains(p.VendorId!.Value))
            .Select(p => p.POI_ID)
            .ToList();

        var poiVendorSet = assignedPois
            .Where(p => p.VendorId.HasValue)
            .Select(p => p.VendorId!.Value)
            .ToHashSet();

        var vendorsWithoutPoi = vendors
            .Where(v => !poiVendorSet.Contains(v.VendorId))
            .Select(v => new { v.VendorId, v.BusinessName, v.ContactName })
            .ToList();

        return Ok(new
        {
            totalVendors = vendors.Count,
            totalPois = pois.Count,
            assignedPois = assignedPois.Count,
            unassignedPoiCount = unassignedPois.Count,
            orphanPoiCount = orphanPoiIds.Count,
            vendorsWithoutPoiCount = vendorsWithoutPoi.Count,
            unassignedPoiIds = unassignedPois,
            orphanPoiIds,
            vendorsWithoutPoi
        });
    }

    /// <summary>
    /// Deactivate POI - Admin or owning Vendor only
    /// </summary>
    [Authorize(Roles = "Admin,Vendor")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeletePOI(int id)
    {
        var poi = await _db.POIs.Find(p => p.POI_ID == id && p.DeletedAt == null).FirstOrDefaultAsync();

        if (poi == null)
        {
            return NotFound(new { message = "POI not found" });
        }

        var isAdmin = User.IsInRole("Admin");
        if (!isAdmin)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var vendor = await _db.VendorProfiles.Find(v => v.UserId == userId).FirstOrDefaultAsync();
            if (vendor == null)
            {
                var email = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name;
                if (!string.IsNullOrWhiteSpace(email))
                {
                    vendor = await _db.VendorProfiles.Find(v => v.ContactEmail == email).FirstOrDefaultAsync();
                }
            }
            if (vendor == null || poi.VendorId != vendor.VendorId)
            {
                return StatusCode(403, new { message = "Bạn không có quyền ngưng hoạt động POI này." });
            }
        }

        var update = Builders<POI>.Update
            .Set(p => p.IsActive, false)
            .Set(p => p.UpdatedAt, DateTime.UtcNow);
        await _db.POIs.UpdateOneAsync(p => p.POI_ID == id && p.DeletedAt == null, update);

        return Ok(new { message = "POI deactivated successfully" });
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
        var pendingFilter = Builders<POI>.Filter.Eq(p => p.ReviewStatus, "pending") & Builders<POI>.Filter.Eq(p => p.DeletedAt, null);

        var totalPOIs = await _db.POIs.CountDocumentsAsync(totalFilter);
        var activePOIs = await _db.POIs.CountDocumentsAsync(activeFilter);
        var inactivePOIs = await _db.POIs.CountDocumentsAsync(inactiveFilter);
        var pendingPOIs = await _db.POIs.CountDocumentsAsync(pendingFilter);
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
            pendingPOIs
        };

        return Ok(stats);
    }

    /// <summary>
    /// Resolve Google Maps link to coordinates
    /// </summary>
    [Authorize(Roles = "Vendor")]
    [HttpPost("resolve-map-link")]
    public async Task<ActionResult> ResolveMapLink([FromBody] ResolveMapLinkRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Url))
            return BadRequest(new { message = "URL is required" });

        var url = request.Url.Trim();
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return BadRequest(new { message = "Invalid URL format" });

        if (!IsGoogleMapsHost(uri.Host))
            return BadRequest(new { message = "URL must be a Google Maps link" });

        try
        {
            // Resolve short links (maps.app.goo.gl or goo.gl)
            if (url.Contains("maps.app.goo.gl") || url.Contains("goo.gl/maps"))
            {
                var response = await _httpClient.GetAsync(url);
                url = response.RequestMessage?.RequestUri?.ToString() ?? url;
            }

            if (TryExtractCoordinates(url, out var latitude, out var longitude))
                return Ok(new { latitude, longitude });

            return BadRequest(new { message = "Could not extract coordinates from this Google Maps link" });
        }
        catch
        {
            return BadRequest(new { message = "Could not resolve this Google Maps link" });
        }
    }

    /// <summary>
    /// Upload image for a POI
    /// </summary>
    [Authorize(Roles = "Admin,Vendor")]
    [HttpPost("upload-image")]
    public async Task<ActionResult> UploadImage(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "No file uploaded" });
        }

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        
        if (!allowedExtensions.Contains(extension))
        {
            return BadRequest(new { message = "Only JPG, PNG and WEBP images are allowed" });
        }

        if (file.Length > 5 * 1024 * 1024) // 5MB limit
        {
            return BadRequest(new { message = "File size exceeds 5MB limit" });
        }

        var uploadsPath = Path.Combine(_env.WebRootPath, "uploads", "images");
        Directory.CreateDirectory(uploadsPath);

        var fileName = $"{Guid.NewGuid()}{extension}";
        var filePath = Path.Combine(uploadsPath, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var imageUrl = $"/uploads/images/{fileName}";
        return Ok(new { imageUrl });
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

    private static bool IsGoogleMapsHost(string host)
    {
        var normalized = host.Trim().ToLowerInvariant();
        return normalized == "maps.google.com"
            || normalized.EndsWith(".google.com")
            || normalized == "goo.gl"
            || normalized.EndsWith(".goo.gl");
    }

    private static bool TryExtractCoordinates(string url, out double latitude, out double longitude)
    {
        latitude = 0;
        longitude = 0;

        var patterns = new[]
        {
            @"@(-?\d+\.\d+),(-?\d+\.\d+)",
            @"[?&]q=(-?\d+\.\d+),(-?\d+\.\d+)",
            @"search/(-?\d+\.\d+),(-?\d+\.\d+)"
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(url, pattern);
            if (!match.Success)
            {
                continue;
            }

            if (!double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out latitude))
            {
                continue;
            }

            if (!double.TryParse(match.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out longitude))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private async Task<POI?> FindDuplicatePoiAsync(int vendorId, CreatePOIModel model)
    {
        var recentPois = await _db.POIs
            .Find(p => p.VendorId == vendorId && p.DeletedAt == null)
            .SortByDescending(p => p.CreatedAt)
            .Limit(100)
            .ToListAsync();

        var normalizedIncomingName = NormalizeForComparison(model.Name_Vi);
        var normalizedIncomingAddress = NormalizeForComparison(model.Address);
        var hasCoordinates = model.Latitude != 0 || model.Longitude != 0;

        foreach (var candidate in recentPois)
        {
            if (!string.Equals(normalizedIncomingName, NormalizeForComparison(candidate.Name_Vi), StringComparison.Ordinal))
                continue;

            var addressMatched = !string.IsNullOrWhiteSpace(normalizedIncomingAddress)
                && string.Equals(normalizedIncomingAddress, NormalizeForComparison(candidate.Address), StringComparison.Ordinal);

            var distanceMatched = false;
            if (hasCoordinates && candidate.Location != null)
            {
                var distanceMeters = CalculateDistanceMeters(
                    (double)model.Latitude,
                    (double)model.Longitude,
                    candidate.Location.Latitude,
                    candidate.Location.Longitude);
                distanceMatched = distanceMeters <= 30;
            }

            if (addressMatched || distanceMatched)
                return candidate;
        }

        return null;
    }

    private static string NormalizeForComparison(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var lowered = input.Trim().ToLowerInvariant();
        lowered = Regex.Replace(lowered, @"\s+", " ");
        return lowered;
    }

    private static string ComputePoiSubmissionHash(int vendorId, CreatePOIModel model)
    {
        var payload = string.Join("|", new[]
        {
            vendorId.ToString(CultureInfo.InvariantCulture),
            NormalizeForComparison(model.Name_Vi),
            NormalizeForComparison(model.Address),
            model.Latitude.ToString("F6", CultureInfo.InvariantCulture),
            model.Longitude.ToString("F6", CultureInfo.InvariantCulture),
            NormalizeForComparison(model.Description_Vi)
        });

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes);
    }

    /// <summary>
    /// Determines if a vendor update is a "major" change requiring admin review.
    /// Major = tên quán, địa chỉ, hoặc vị trí GPS thay đổi.
    /// Minor = mô tả, audio, menu, giờ mở cửa, SĐT, giá, ảnh → auto-approve.
    /// </summary>
    private static bool IsMajorChange(POI existing, UpdatePOIModel model)
    {
        // Tên quán thay đổi
        if (model.Name_Vi != null &&
            !string.Equals(model.Name_Vi.Trim(), existing.Name_Vi?.Trim(), StringComparison.OrdinalIgnoreCase))
            return true;

        // Địa chỉ thay đổi
        if (model.Address != null &&
            !string.Equals(model.Address.Trim(), existing.Address?.Trim(), StringComparison.OrdinalIgnoreCase))
            return true;

        // Vị trí GPS thay đổi (thay đổi hơn ~10 mét)
        if (model.Latitude.HasValue || model.Longitude.HasValue)
        {
            var newLat = model.Latitude.HasValue ? (double)model.Latitude.Value : existing.Location.Latitude;
            var newLon = model.Longitude.HasValue ? (double)model.Longitude.Value : existing.Location.Longitude;
            var distanceMeters = CalculateDistanceMeters(
                existing.Location.Latitude, existing.Location.Longitude,
                newLat, newLon);
            if (distanceMeters > 10)
                return true;
        }

        return false;
    }

    private static double CalculateDistanceMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadius = 6371000;

        var dLat = DegreesToRadians(lat2 - lat1);
        var dLon = DegreesToRadians(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return earthRadius * c;
    }

    private static List<GpsTestPoiSeed> BuildGpsTestPoiSeeds(EnsureGpsTestPoiRequest? request)
    {
        if (request?.Points == null || request.Points.Count == 0)
            return DefaultGpsTestPoiSeeds.ToList();

        var seeds = new List<GpsTestPoiSeed>();

        for (var i = 0; i < request.Points.Count; i++)
        {
            var point = request.Points[i];
            if (point.Latitude is < -90 or > 90 || point.Longitude is < -180 or > 180)
                continue;

            var index = seeds.Count + 1;
            var priority = Math.Clamp(point.Priority ?? (10 - i), 1, 10);

            seeds.Add(new GpsTestPoiSeed(
                NameVi: $"POI Test GPS Thuc Te {index}",
                NameEn: $"Real GPS Test POI {index}",
                NameZh: $"GPS shi di ce shi dian {index}",
                Latitude: point.Latitude,
                Longitude: point.Longitude,
                Priority: priority));
        }

        return seeds.Count > 0
            ? seeds
            : DefaultGpsTestPoiSeeds.ToList();
    }

    private sealed record GpsTestPoiSeed(
        string NameVi,
        string NameEn,
        string NameZh,
        double Latitude,
        double Longitude,
        int Priority);

    private static double DegreesToRadians(double degrees) => degrees * (Math.PI / 180);

    private static string? NormalizeMapUrl(string? rawUrl)
    {
        if (string.IsNullOrWhiteSpace(rawUrl))
            return null;

        var trimmed = rawUrl.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
            return null;

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return null;

        return uri.ToString();
    }
}

// DTOs
public class ResolveMapLinkRequest
{
    public string Url { get; set; } = string.Empty;
}

public class EnsureGpsTestPoiRequest
{
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? Address { get; set; }
    public List<GpsTestPoiPointRequest>? Points { get; set; }
}

public class GpsTestPoiPointRequest
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int? Priority { get; set; }
}

public class ReviewPOIRequest
{
    public string? Status { get; set; }
    public string? Note { get; set; }
}

public class POIDto
{
    public string? Id { get; set; }
    public int POI_ID { get; set; }
    public string Name_Vi { get; set; } = null!;
    public string? Name_En { get; set; }
    public string? Name_Zh { get; set; }
    public string? Description_Vi { get; set; }
    public string? Description_En { get; set; }
    public string? Description_Zh { get; set; }
    public string Address { get; set; } = null!;
    public string? MapUrl { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public int AudioCount { get; set; }
    public int? VendorId { get; set; }
    public string? ReviewStatus { get; set; }
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
    public string? AudioUrl_Zh { get; set; }
    public string? Script_Vi { get; set; }
    public string? Script_En { get; set; }
    public string? Script_Zh { get; set; }
    public string? ZoneType { get; set; }
    public int ZoneLevel { get; set; }
    public int Priority { get; set; }
    public int TriggerRadius { get; set; }
    public int CooldownMinutes { get; set; }
    public int? ParentZoneId { get; set; }
    public int MaxPlaysPerSession { get; set; }
    public Dictionary<string, PendingFieldChange>? PendingChanges { get; set; }
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
    public string? Name_Zh { get; set; }
    public string? Description_Vi { get; set; }
    public string? Description_En { get; set; }
    public string? Description_Zh { get; set; }
    public string Address { get; set; } = null!;
    public string? MapUrl { get; set; }
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
    public string? AudioUrl_Zh { get; set; }
    public string? Script_Vi { get; set; }
    public string? Script_En { get; set; }
    public string? Script_Zh { get; set; }
    public string? ZoneType { get; set; }
    public int? ZoneLevel { get; set; }
    public int? Priority { get; set; }
    public int? CooldownMinutes { get; set; }
    public int? ParentZoneId { get; set; }
    public int? MaxPlaysPerSession { get; set; }
}

public class UpdatePOIModel
{
    public string? Name_Vi { get; set; }
    public string? Name_En { get; set; }
    public string? Name_Zh { get; set; }
    public string? Description_Vi { get; set; }
    public string? Description_En { get; set; }
    public string? Description_Zh { get; set; }
    public string? Address { get; set; }
    public string? MapUrl { get; set; }
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
    public string? AudioUrl_Zh { get; set; }
    public string? Script_Vi { get; set; }
    public string? Script_En { get; set; }
    public string? Script_Zh { get; set; }
    public string? ZoneType { get; set; }
    public int? ZoneLevel { get; set; }
    public int? Priority { get; set; }
    public int? CooldownMinutes { get; set; }
    public int? ParentZoneId { get; set; }
    public int? MaxPlaysPerSession { get; set; }
    public bool? IsActive { get; set; }
}
