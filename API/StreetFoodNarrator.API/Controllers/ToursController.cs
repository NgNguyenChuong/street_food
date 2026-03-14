using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using StreetFoodNarrator.API.Data;
using StreetFoodNarrator.API.Models;

namespace StreetFoodNarrator.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ToursController : ControllerBase
{
    private readonly MongoDbContext _db;
    private readonly MongoSequenceService _sequence;

    public ToursController(MongoDbContext db, MongoSequenceService sequence)
    {
        _db = db;
        _sequence = sequence;
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
            filter = Builders<Tour>.Filter.Regex(t => t.TourName, 
                new MongoDB.Bson.BsonRegularExpression(search, "i"));
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
                Description = t.Description,
                EstimatedDurationMinutes = t.EstimatedDurationMinutes,
                IsActive = t.IsActive,
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

        var tour = new Tour
        {
            Tour_ID = tourId,
            TourName = request.TourName,
            Description = request.Description,
            EstimatedDurationMinutes = request.EstimatedDurationMinutes,
            IsActive = request.IsActive ?? true,
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

        var update = Builders<Tour>.Update
            .Set(t => t.TourName, request.TourName)
            .Set(t => t.Description, request.Description)
            .Set(t => t.EstimatedDurationMinutes, request.EstimatedDurationMinutes)
            .Set(t => t.IsActive, request.IsActive)
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
    public string? Description { get; set; }
    public int EstimatedDurationMinutes { get; set; }
    public bool IsActive { get; set; }
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
    public string? Description { get; set; }
    public int EstimatedDurationMinutes { get; set; }
    public bool? IsActive { get; set; }
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
    public string? Description { get; set; }
    public int EstimatedDurationMinutes { get; set; }
    public bool IsActive { get; set; }
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
