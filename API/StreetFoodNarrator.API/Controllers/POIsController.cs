using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using StreetFoodNarrator.API.Data;
using StreetFoodNarrator.API.Models;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace StreetFoodNarrator.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class POIsController : ControllerBase
{
    private readonly MongoDbContext _db;
    private readonly MongoSequenceService _sequence;
    private readonly IWebHostEnvironment _env;
    private readonly HttpClient _httpClient;

    public POIsController(MongoDbContext db, MongoSequenceService sequence, IWebHostEnvironment env)
    {
        _db = db;
        _sequence = sequence;
        _env = env;
        _httpClient = new HttpClient();
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
            // Public: only approved + active
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
    /// </summary>
    [HttpGet("sync")]
    public async Task<ActionResult<POISyncResponse>> SyncPOIs([FromQuery] long sinceVersion = 0)
    {
        var filter = Builders<POI>.Filter.Eq(p => p.DeletedAt, null) &
                     Builders<POI>.Filter.Eq(p => p.ReviewStatus, "approved") &
                     Builders<POI>.Filter.Eq(p => p.IsActive, true);

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
            Id = p.Id.ToString(),
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

        // Public access: only approved + active
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
    [Authorize(Roles = "Admin,Vendor")]
    [HttpPost]
    public async Task<ActionResult<POI>> CreatePOI([FromBody] CreatePOIModel model)
    {
        // Vendor must only create POIs under their own VendorId.
        int? vendorId = null;
        var isVendor = User.IsInRole("Vendor");
        if (User.IsInRole("Vendor"))
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Forbid();
            }

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
        else if (User.IsInRole("Admin"))
        {
            if (!model.VendorId.HasValue)
            {
                return BadRequest(new { message = "VendorId is required when admin creates a POI" });
            }

            var vendorProfile = await _db.VendorProfiles
                .Find(v => v.VendorId == model.VendorId.Value)
                .FirstOrDefaultAsync();

            if (vendorProfile == null)
            {
                return BadRequest(new { message = "VendorId does not exist" });
            }

            vendorId = model.VendorId.Value;
        }

        var nextId = await _sequence.GetNextAsync("poi_id");
        var zoneType = string.IsNullOrWhiteSpace(model.ZoneType) ? "Spot" : model.ZoneType;
        var zoneLevel = model.ZoneLevel ?? (zoneType == "Area" ? 1 : zoneType == "District" ? 2 : 3);
        var cooldown = model.CooldownMinutes ?? (zoneType == "Spot" ? 0 : 30);
        var triggerRadius = model.TriggerRadius ?? 50;
        var priority = model.Priority ?? 5;
        var maxPlays = model.MaxPlaysPerSession ?? 1;
        var reviewStatus = "pending";
        if (!isVendor && !string.IsNullOrWhiteSpace(model.ReviewStatus))
        {
            reviewStatus = model.ReviewStatus.ToLowerInvariant();
        }
        else if (!isVendor)
        {
            reviewStatus = "approved";
        }

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
            IsActive = model.IsActive,
            VendorId = vendorId,
            ReviewStatus = reviewStatus,
            ReviewNote = null,
            ReviewedAt = reviewStatus == "approved" ? DateTime.UtcNow : null,
            ReviewedBy = reviewStatus == "approved" ? "admin" : null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Location = GeoJsonLocation.FromLatLon((double)model.Latitude, (double)model.Longitude)
        };

        if (isVendor || reviewStatus != "approved")
        {
            poi.IsActive = false;
        }

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

        if (User.IsInRole("Admin") && model.VendorId.HasValue)
        {
            var vendorExists = await _db.VendorProfiles
                .Find(v => v.VendorId == model.VendorId.Value)
                .AnyAsync();

            if (!vendorExists)
            {
                return BadRequest(new { message = "VendorId does not exist" });
            }

            effectiveVendorId = model.VendorId.Value;
        }

        if (!effectiveVendorId.HasValue && !User.IsInRole("Admin"))
        {
            return BadRequest(new { message = "POI must belong to a vendor" });
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
            .Set(p => p.Script_Vi, model.Script_Vi ?? poi.Script_Vi)
            .Set(p => p.Script_En, model.Script_En ?? poi.Script_En)
            .Set(p => p.Script_Zh, model.Script_Zh ?? poi.Script_Zh)
            .Set(p => p.ZoneType, zoneType)
            .Set(p => p.ZoneLevel, zoneLevel)
            .Set(p => p.Priority, priority)
            .Set(p => p.TriggerRadius, triggerRadius)
            .Set(p => p.CooldownMinutes, cooldown)
            .Set(p => p.ParentZoneId, model.ParentZoneId ?? poi.ParentZoneId)
            .Set(p => p.MaxPlaysPerSession, maxPlays)
            .Set(p => p.IsActive, model.IsActive ?? poi.IsActive)
            .Set(p => p.UpdatedAt, DateTime.UtcNow);

        if (effectiveVendorId.HasValue)
        {
            update = update.Set(p => p.VendorId, effectiveVendorId.Value);
        }

        if (User.IsInRole("Vendor"))
        {
            update = update
                .Set(p => p.ReviewStatus, "pending")
                .Set(p => p.ReviewNote, null)
                .Set(p => p.ReviewedAt, null)
                .Set(p => p.ReviewedBy, null)
                .Set(p => p.IsActive, false);
        }
        else if (User.IsInRole("Admin") && !string.IsNullOrWhiteSpace(model.ReviewStatus))
        {
            var status = model.ReviewStatus.ToLowerInvariant();

            if (status == "approved")
            {
                if (!effectiveVendorId.HasValue)
                {
                    return BadRequest(new { message = "Cannot approve POI without VendorId" });
                }

                var vendor = await _db.VendorProfiles
                    .Find(v => v.VendorId == effectiveVendorId.Value)
                    .FirstOrDefaultAsync();

                if (vendor == null)
                {
                    return BadRequest(new { message = "Cannot approve POI because VendorId does not exist" });
                }

                if (!string.Equals(vendor.VerificationStatus, "approved", StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(new { message = "Cannot approve POI while vendor is not approved" });
                }
            }

            update = update
                .Set(p => p.ReviewStatus, status)
                .Set(p => p.ReviewNote, model.ReviewNote)
                .Set(p => p.ReviewedAt, DateTime.UtcNow)
                .Set(p => p.ReviewedBy, "admin");

            if (status != "approved")
                update = update.Set(p => p.IsActive, false);
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
            .Set(p => p.IsActive, status == "approved");

        await _db.POIs.UpdateOneAsync(p => p.POI_ID == id && p.DeletedAt == null, update);

        return Ok(new { message = $"POI review updated to {status}" });
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
    /// Delete POI (soft delete) - Admin or owning Vendor only
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
                return StatusCode(403, new { message = "Bạn không có quyền xóa POI này." });
            }
        }

        var blockingTours = new List<Tour>();
        var poiObjectId = poi.Id.ToString();

        var toursByList = await _db.Tours.Find(t =>
                t.DeletedAt == null &&
                t.PoiIds.Contains(poiObjectId))
            .ToListAsync();
        if (toursByList.Count > 0) blockingTours.AddRange(toursByList);

        var links = await _db.POITours.Find(pt => pt.POI_ID == id).ToListAsync();
        if (links.Count > 0)
        {
            var tourIds = links.Select(l => l.Tour_ID).Distinct().ToList();
            var toursByJoin = await _db.Tours.Find(t => tourIds.Contains(t.Tour_ID) && t.DeletedAt == null).ToListAsync();
            if (toursByJoin.Count > 0) blockingTours.AddRange(toursByJoin);
        }

        if (blockingTours.Count > 0)
        {
            var names = blockingTours
                .Select(t => t.TourName)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct()
                .Take(3)
                .ToList();

            var msg = names.Count > 0
                ? $"POI đang được dùng trong tour: {string.Join(", ", names)}. Vui lòng gỡ khỏi tour trước khi xóa."
                : "POI đang được dùng trong tour. Vui lòng gỡ khỏi tour trước khi xóa.";

            return BadRequest(new { message = msg, tours = names });
        }

        var update = Builders<POI>.Update
            .Set(p => p.IsDeleted, true)
            .Set(p => p.DeletedAt, DateTime.UtcNow)
            .Set(p => p.IsActive, false);
        await _db.POIs.UpdateOneAsync(p => p.POI_ID == id && p.DeletedAt == null, update);

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
    [Authorize(Roles = "Admin,Vendor")]
    [HttpPost("resolve-map-link")]
    public async Task<ActionResult> ResolveMapLink([FromBody] ResolveMapLinkRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Url))
            return BadRequest(new { message = "URL is required" });

        var url = request.Url.Trim();

        try
        {
            // Resolve short links (maps.app.goo.gl or goo.gl)
            if (url.Contains("maps.app.goo.gl") || url.Contains("goo.gl/maps"))
            {
                var response = await _httpClient.GetAsync(url);
                url = response.RequestMessage?.RequestUri?.ToString() ?? url;
            }

            // Extract lat/lng via regex
            // Pattern 1: @10.760741,106.703301
            var match = Regex.Match(url, @"@(-?\d+\.\d+),(-?\d+\.\d+)");
            if (match.Success)
            {
                return Ok(new
                {
                    latitude = double.Parse(match.Groups[1].Value),
                    longitude = double.Parse(match.Groups[2].Value)
                });
            }

            // Pattern 2: ?q=10.760741,106.703301 or &q=...
            var qMatch = Regex.Match(url, @"[?&]q=(-?\d+\.\d+),(-?\d+\.\d+)");
            if (qMatch.Success)
            {
                return Ok(new
                {
                    latitude = double.Parse(qMatch.Groups[1].Value),
                    longitude = double.Parse(qMatch.Groups[2].Value)
                });
            }

            // Pattern 3: search/10.760741,106.703301
            var sMatch = Regex.Match(url, @"search/(-?\d+\.\d+),(-?\d+\.\d+)");
            if (sMatch.Success)
            {
                return Ok(new
                {
                    latitude = double.Parse(sMatch.Groups[1].Value),
                    longitude = double.Parse(sMatch.Groups[2].Value)
                });
            }

            return BadRequest(new { message = "Could not extract coordinates from this Google Maps link" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error resolving map link", error = ex.Message });
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
}

// DTOs
public class ResolveMapLinkRequest
{
    public string Url { get; set; } = string.Empty;
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
    public string? AudioUrl_Ja { get; set; }
    public string? AudioUrl_Fr { get; set; }
    public string? AudioUrl_Ko { get; set; }
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
    public string? Script_Vi { get; set; }
    public string? Script_En { get; set; }
    public string? Script_Zh { get; set; }
    public string? ZoneType { get; set; }
    public int? ZoneLevel { get; set; }
    public int? Priority { get; set; }
    public int? CooldownMinutes { get; set; }
    public int? ParentZoneId { get; set; }
    public int? MaxPlaysPerSession { get; set; }
    public int? VendorId { get; set; }
    public string? ReviewStatus { get; set; }
    public string? ReviewNote { get; set; }
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
    public string? Script_Vi { get; set; }
    public string? Script_En { get; set; }
    public string? Script_Zh { get; set; }
    public string? ZoneType { get; set; }
    public int? ZoneLevel { get; set; }
    public int? Priority { get; set; }
    public int? CooldownMinutes { get; set; }
    public int? ParentZoneId { get; set; }
    public int? MaxPlaysPerSession { get; set; }
    public int? VendorId { get; set; }
    public string? ReviewStatus { get; set; }
    public string? ReviewNote { get; set; }
    public bool? IsActive { get; set; }
}
