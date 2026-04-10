using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using StreetFoodNarrator.API.Data;
using StreetFoodNarrator.API.Models;
using System.Text.RegularExpressions;

namespace StreetFoodNarrator.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ToursController : ControllerBase
{
    private readonly MongoDbContext _db;
    private readonly MongoSequenceService _sequence;
    private readonly IWebHostEnvironment _env;

    public ToursController(MongoDbContext db, MongoSequenceService sequence, IWebHostEnvironment env)
    {
        _db = db;
        _sequence = sequence;
        _env = env;
    }

    /// <summary>
    /// Get all tours with pagination
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<TourListResponse>> GetTours(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] bool? isActive = null)
    {
        var filter = Builders<Tour>.Filter.Empty;

        if (!string.IsNullOrEmpty(search))
        {
            var rx = new MongoDB.Bson.BsonRegularExpression(search, "i");
            filter = Builders<Tour>.Filter.Or(
                Builders<Tour>.Filter.Regex(t => t.TourName, rx),
                Builders<Tour>.Filter.Regex(t => t.TourName_En, rx),
                Builders<Tour>.Filter.Regex(t => t.TourName_Zh, rx),
                Builders<Tour>.Filter.Regex(t => t.Description, rx),
                Builders<Tour>.Filter.Regex(t => t.Description_En, rx),
                Builders<Tour>.Filter.Regex(t => t.Description_Zh, rx));
        }

        if (isActive.HasValue)
        {
            filter &= Builders<Tour>.Filter.Eq(t => t.IsActive, isActive.Value);
        }

        var total = await _db.Tours.CountDocumentsAsync(filter);
        var skip = (page - 1) * pageSize;

        var tours = await _db.Tours
            .Find(filter)
            .Sort(Builders<Tour>.Sort.Descending(t => t.CreatedAt))
            .Skip(skip)
            .Limit(pageSize)
            .ToListAsync();

        // Collect all POI ObjectIds referenced across the returned tours
        var allPoiIdStrings = tours.SelectMany(t => t.PoiIds ?? new()).Distinct().ToList();
        var poiMap = new Dictionary<string, POI>();
        if (allPoiIdStrings.Count > 0)
        {
            var objIds = allPoiIdStrings
                .Where(s => MongoDB.Bson.ObjectId.TryParse(s, out _))
                .Select(s => MongoDB.Bson.ObjectId.Parse(s))
                .ToList();
            if (objIds.Count > 0)
            {
                var pois = await _db.POIs
                    .Find(Builders<POI>.Filter.In(p => p.Id, objIds))
                    .ToListAsync();
                poiMap = pois.ToDictionary(p => p.Id.ToString());
            }
        }

        var tourDtos = tours.Select(t =>
        {
            var orderedPois = (t.PoiIds ?? new())
                .Select(pid => poiMap.TryGetValue(pid, out var p) ? new TourPoiDto
                {
                    Id       = p.Id.ToString(),
                    Name     = p.Name_Vi,
                    Category = p.Category,
                    Address  = p.Address,
                    IsActive = p.IsActive,
                    Latitude  = p.Location?.Latitude,
                    Longitude = p.Location?.Longitude
                } : null)
                .Where(p => p != null)
                .ToList();
            return new TourDto
            {
                Id = t.Id.ToString(),
                Tour_ID = t.Tour_ID,
                TourName = t.TourName,
                TourName_En = t.TourName_En,
                TourName_Zh = t.TourName_Zh,
                Description = t.Description,
                Description_En = t.Description_En,
                Description_Zh = t.Description_Zh,
                ImageUrl = NormalizeImageUrlForResponse(t.ImageUrl),
                EstimatedDurationMinutes = t.EstimatedDurationMinutes,
                IsActive = t.IsActive,
                IsFreeTour = t.IsFreeTour,
                CreatedAt = t.CreatedAt,
                Themes = t.Themes ?? new(),
                TimeSlots = t.TimeSlots ?? new(),
                RouteType = t.RouteType ?? "ordered",
                Pois = orderedPois!,
                RouteGeometry = t.RouteGeometry
            };
        }).ToList();

        return Ok(new TourListResponse
        {
            Data = tourDtos,
            Total = (int)total,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling((double)total / pageSize)
        });
    }

    /// <summary>
    /// Get tour by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<Tour>> GetTour(string id)
    {
        if (!MongoDB.Bson.ObjectId.TryParse(id, out var objectId))
            return BadRequest("Invalid tour ID format");

        var tour = await _db.Tours.Find(t => t.Id == objectId).FirstOrDefaultAsync();
        if (tour == null) return NotFound();

        return Ok(tour);
    }

    /// <summary>
    /// Create new tour (Admin only)
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<Tour>> CreateTour([FromBody] CreateTourRequest request)
    {
        var tourId = await _sequence.GetNextAsync("Tour_ID");
        var isFreeTour = request.IsFreeTour ?? false;
        var isActive = request.IsActive ?? true;

        if (isFreeTour && isActive)
        {
            var activeFreeTour = await FindOtherActiveFreeTourAsync();
            if (activeFreeTour != null)
                return Conflict(CreateFreeTourConflictPayload(activeFreeTour));
        }

        var tour = new Tour
        {
            Tour_ID = tourId,
            TourName = request.TourName,
            TourName_En = request.TourName_En,
            TourName_Zh = request.TourName_Zh,
            Description = request.Description,
            Description_En = request.Description_En,
            Description_Zh = request.Description_Zh,
            ImageUrl = NormalizeImageUrlForStorage(request.ImageUrl),
            EstimatedDurationMinutes = request.EstimatedDurationMinutes,
            IsActive = isActive,
            IsFreeTour = isFreeTour,
            Themes = request.Themes ?? new(),
            TimeSlots = request.TimeSlots ?? new(),
            RouteType = request.RouteType ?? "ordered",
            PoiIds = request.PoiIds ?? new(),
            RouteGeometry = request.RouteGeometry,
            CreatedAt = DateTime.UtcNow
        };

        await _db.Tours.InsertOneAsync(tour);

        return CreatedAtAction(nameof(GetTour), new { id = tour.Id.ToString() }, tour);
    }

    /// <summary>
    /// Update tour (Admin only)
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateTour(string id, [FromBody] UpdateTourRequest request)
    {
        if (!MongoDB.Bson.ObjectId.TryParse(id, out var objectId))
            return BadRequest("Invalid tour ID format");

        var currentTour = await _db.Tours.Find(t => t.Id == objectId).FirstOrDefaultAsync();
        if (currentTour == null)
            return NotFound();

        var normalizedIsActive = request.IsActive ?? currentTour.IsActive;
        var normalizedIsFreeTour = request.IsFreeTour ?? currentTour.IsFreeTour;

        if (normalizedIsFreeTour && normalizedIsActive)
        {
            var activeFreeTour = await FindOtherActiveFreeTourAsync(objectId);
            if (activeFreeTour != null)
                return Conflict(CreateFreeTourConflictPayload(activeFreeTour));
        }

        var update = Builders<Tour>.Update
            .Set(t => t.TourName, request.TourName)
            .Set(t => t.TourName_En, request.TourName_En)
            .Set(t => t.TourName_Zh, request.TourName_Zh)
            .Set(t => t.Description, request.Description)
            .Set(t => t.Description_En, request.Description_En)
            .Set(t => t.Description_Zh, request.Description_Zh)
            .Set(t => t.ImageUrl, NormalizeImageUrlForStorage(request.ImageUrl))
            .Set(t => t.EstimatedDurationMinutes, request.EstimatedDurationMinutes)
            .Set(t => t.IsActive, normalizedIsActive)
            .Set(t => t.IsFreeTour, normalizedIsFreeTour)
            .Set(t => t.Themes, request.Themes ?? new())
            .Set(t => t.TimeSlots, request.TimeSlots ?? new())
            .Set(t => t.RouteType, request.RouteType ?? "ordered")
            .Set(t => t.PoiIds, request.PoiIds ?? new())
            .Set(t => t.RouteGeometry, request.RouteGeometry)
            .Set(t => t.UpdatedAt, DateTime.UtcNow);

        var result = await _db.Tours.UpdateOneAsync(t => t.Id == objectId, update);
        if (result.MatchedCount == 0) return NotFound();

        return NoContent();
    }

    private async Task<Tour?> FindOtherActiveFreeTourAsync(MongoDB.Bson.ObjectId? excludeTourId = null)
    {
        var filter = Builders<Tour>.Filter.And(
            Builders<Tour>.Filter.Eq(t => t.IsFreeTour, true),
            Builders<Tour>.Filter.Eq(t => t.IsActive, true));

        if (excludeTourId.HasValue)
        {
            filter &= Builders<Tour>.Filter.Ne(t => t.Id, excludeTourId.Value);
        }

        return await _db.Tours
            .Find(filter)
            .SortByDescending(t => t.UpdatedAt)
            .ThenByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync();
    }

    private static object CreateFreeTourConflictPayload(Tour activeFreeTour)
        => new
        {
            message = $"Đã có tour free \"{activeFreeTour.TourName}\" đang bật. Vui lòng ẩn tour đó trước khi kích hoạt free tour này.",
            existingFreeTourId = activeFreeTour.Id.ToString(),
            existingFreeTourName = activeFreeTour.TourName
        };

    /// <summary>
    /// Upload image for a Tour
    /// </summary>
    [HttpPost("upload-image")]
    [Authorize(Roles = "Admin")]
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

        if (file.Length > 5 * 1024 * 1024)
        {
            return BadRequest(new { message = "File size exceeds 5MB limit" });
        }

        var uploadsPath = Path.Combine(_env.ContentRootPath, "Uploads", "tour-images");
        Directory.CreateDirectory(uploadsPath);

        var fileName = $"{Guid.NewGuid()}{extension}";
        var filePath = Path.Combine(uploadsPath, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var imageUrl = $"/uploads/tour-images/{fileName}";
        var absoluteImageUrl = $"{Request.Scheme}://{Request.Host}{imageUrl}";
        return Ok(new { imageUrl, absoluteImageUrl });
    }

    private static string? NormalizeImageUrlForStorage(string? rawUrl)
    {
        if (string.IsNullOrWhiteSpace(rawUrl))
            return null;

        var value = rawUrl.Trim();

        if (Uri.TryCreate(value, UriKind.Absolute, out _))
            return value;

        value = value.Replace('\\', '/');

        var uploadsMatch = Regex.Match(value, @"(?:^|/)(?:wwwroot/)?uploads/(.+)$", RegexOptions.IgnoreCase);
        if (uploadsMatch.Success)
            return "/uploads/" + uploadsMatch.Groups[1].Value.TrimStart('/');

        if (!value.StartsWith('/'))
            value = "/" + value;

        return value;
    }

    private static string? NormalizeImageUrlForResponse(string? rawUrl)
    {
        if (string.IsNullOrWhiteSpace(rawUrl))
            return null;

        var value = rawUrl.Trim();
        if (Uri.TryCreate(value, UriKind.Absolute, out _))
            return value;

        value = value.Replace('\\', '/');

        var uploadsMatch = Regex.Match(value, @"(?:^|/)(?:wwwroot/)?uploads/(.+)$", RegexOptions.IgnoreCase);
        if (uploadsMatch.Success)
            return "/uploads/" + uploadsMatch.Groups[1].Value.TrimStart('/');

        if (!value.StartsWith('/'))
            value = "/" + value;

        return value;
    }

    /// <summary>
    /// Delete tour (Admin only)
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteTour(string id)
    {
        if (!MongoDB.Bson.ObjectId.TryParse(id, out var objectId))
            return BadRequest("Invalid tour ID format");

        var result = await _db.Tours.DeleteOneAsync(t => t.Id == objectId);
        if (result.DeletedCount == 0) return NotFound();

        return NoContent();
    }

    /// <summary>
    /// Get tour statistics (Admin only)
    /// </summary>
    [HttpGet("stats")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<TourStatsResponse>> GetStats()
    {
        var total  = await _db.Tours.CountDocumentsAsync(Builders<Tour>.Filter.Empty);
        var active = await _db.Tours.CountDocumentsAsync(t => t.IsActive);
        var draft  = await _db.Tours.CountDocumentsAsync(t => !t.IsActive);
        var routed = await _db.Tours.CountDocumentsAsync(
            Builders<Tour>.Filter.And(
                Builders<Tour>.Filter.Exists("RouteGeometry"),
                Builders<Tour>.Filter.Ne<GeoJsonLineString?>("RouteGeometry", null)
            ));

        return Ok(new TourStatsResponse
        {
            Total  = (int)total,
            Active = (int)active,
            Draft  = (int)draft,
            Routed = (int)routed
        });
    }
}

// DTOs
public class TourDto
{
    public string Id { get; set; } = string.Empty;
    public int Tour_ID { get; set; }
    public string TourName { get; set; } = string.Empty;
    public string? TourName_En { get; set; }
    public string? TourName_Zh { get; set; }
    public string? Description { get; set; }
    public string? Description_En { get; set; }
    public string? Description_Zh { get; set; }
    public string? ImageUrl { get; set; }
    public int EstimatedDurationMinutes { get; set; }
    public bool IsActive { get; set; }
    public bool IsFreeTour { get; set; }
    public DateTime CreatedAt { get; set; }
    // v2 fields
    public List<string> Themes { get; set; } = new();
    public List<string> TimeSlots { get; set; } = new();
    public string RouteType { get; set; } = "ordered";
    public List<TourPoiDto> Pois { get; set; } = new();
    public GeoJsonLineString? RouteGeometry { get; set; }
}

public class TourPoiDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}

public class TourListResponse
{
    public List<TourDto> Data { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

public class CreateTourRequest
{
    public string TourName { get; set; } = string.Empty;
    public string? TourName_En { get; set; }
    public string? TourName_Zh { get; set; }
    public string? Description { get; set; }
    public string? Description_En { get; set; }
    public string? Description_Zh { get; set; }
    public string? ImageUrl { get; set; }
    public int EstimatedDurationMinutes { get; set; }
    public bool? IsActive { get; set; }
    public bool? IsFreeTour { get; set; }
    // v2 fields
    public List<string>? Themes { get; set; }
    public List<string>? TimeSlots { get; set; }
    public string? RouteType { get; set; }
    public List<string>? PoiIds { get; set; }
    public GeoJsonLineString? RouteGeometry { get; set; }
}

public class UpdateTourRequest
{
    public string TourName { get; set; } = string.Empty;
    public string? TourName_En { get; set; }
    public string? TourName_Zh { get; set; }
    public string? Description { get; set; }
    public string? Description_En { get; set; }
    public string? Description_Zh { get; set; }
    public string? ImageUrl { get; set; }
    public int EstimatedDurationMinutes { get; set; }
    public bool? IsActive { get; set; }
    public bool? IsFreeTour { get; set; }
    // v2 fields
    public List<string>? Themes { get; set; }
    public List<string>? TimeSlots { get; set; }
    public string? RouteType { get; set; }
    public List<string>? PoiIds { get; set; }
    public GeoJsonLineString? RouteGeometry { get; set; }
}

public class TourStatsResponse
{
    public int Total { get; set; }
    public int Active { get; set; }
    public int Draft { get; set; }
    public int Routed { get; set; }
}
