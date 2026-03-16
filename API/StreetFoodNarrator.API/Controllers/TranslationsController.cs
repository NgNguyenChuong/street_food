using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using StreetFoodNarrator.API.Data;
using StreetFoodNarrator.API.Models;

namespace StreetFoodNarrator.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class TranslationsController : ControllerBase
{
    private readonly MongoDbContext _db;
    private readonly MongoSequenceService _sequence;

    public TranslationsController(MongoDbContext db, MongoSequenceService sequence)
    {
        _db = db;
        _sequence = sequence;
    }

    /// <summary>
    /// Get all translations with filtering
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<TranslationListResponse>> GetTranslations(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? search = null,
        [FromQuery] string? category = null,
        [FromQuery] string? status = null)
    {
        var filter = Builders<Translation>.Filter.Empty;

        if (!string.IsNullOrEmpty(search))
        {
            var keyFilter = Builders<Translation>.Filter.Regex(t => t.Key, 
                new MongoDB.Bson.BsonRegularExpression(search, "i"));
            var viFilter = Builders<Translation>.Filter.Regex(t => t.Vietnamese, 
                new MongoDB.Bson.BsonRegularExpression(search, "i"));
            var enFilter = Builders<Translation>.Filter.Regex(t => t.English, 
                new MongoDB.Bson.BsonRegularExpression(search, "i"));
            filter = Builders<Translation>.Filter.Or(keyFilter, viFilter, enFilter);
        }

        if (!string.IsNullOrEmpty(category))
        {
            filter &= Builders<Translation>.Filter.Eq(t => t.Category, category);
        }

        if (!string.IsNullOrEmpty(status))
        {
            filter &= Builders<Translation>.Filter.Eq(t => t.Status, status);
        }

        var total = await _db.Translations.CountDocumentsAsync(filter);
        var skip = (page - 1) * pageSize;

        var translations = await _db.Translations
            .Find(filter)
            .Sort(Builders<Translation>.Sort.Ascending(t => t.Key))
            .Skip(skip)
            .Limit(pageSize)
            .ToListAsync();

        var dtos = translations.Select(t => new TranslationDto
        {
            Id = t.Id.ToString(),
            Translation_ID = t.Translation_ID,
            Key = t.Key,
            Category = t.Category,
            Vietnamese = t.Vietnamese,
            English = t.English,
            Chinese = t.Chinese,
            Status = t.Status,
            CreatedAt = t.CreatedAt
        }).ToList();

        return Ok(new TranslationListResponse
        {
            Data = dtos,
            Total = (int)total,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling((double)total / pageSize)
        });
    }

    /// <summary>
    /// Get translation by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<Translation>> GetTranslation(string id)
    {
        if (!MongoDB.Bson.ObjectId.TryParse(id, out var objectId))
            return BadRequest("Invalid translation ID format");

        var translation = await _db.Translations.Find(t => t.Id == objectId).FirstOrDefaultAsync();
        if (translation == null) return NotFound();

        return Ok(translation);
    }

    /// <summary>
    /// Create new translation (Admin only)
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<Translation>> CreateTranslation([FromBody] CreateTranslationRequest request)
    {
        // Check if key already exists
        var existing = await _db.Translations.Find(t => t.Key == request.Key).FirstOrDefaultAsync();
        if (existing != null)
            return BadRequest($"Translation with key '{request.Key}' already exists");

        var translationId = await _sequence.GetNextAsync("Translation_ID");

        var translation = new Translation
        {
            Translation_ID = translationId,
            Key = request.Key,
            Category = request.Category ?? "general",
            Vietnamese = request.Vietnamese,
            English = request.English,
            Chinese = request.Chinese,
            Status = request.Status ?? "draft",
            CreatedAt = DateTime.UtcNow
        };

        await _db.Translations.InsertOneAsync(translation);
        return CreatedAtAction(nameof(GetTranslation), new { id = translation.Id.ToString() }, translation);
    }

    /// <summary>
    /// Update translation (Admin only)
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateTranslation(string id, [FromBody] UpdateTranslationRequest request)
    {
        if (!MongoDB.Bson.ObjectId.TryParse(id, out var objectId))
            return BadRequest("Invalid translation ID format");

        var update = Builders<Translation>.Update
            .Set(t => t.Vietnamese, request.Vietnamese)
            .Set(t => t.English, request.English)
            .Set(t => t.Chinese, request.Chinese)
            .Set(t => t.Status, request.Status)
            .Set(t => t.UpdatedAt, DateTime.UtcNow);

        var result = await _db.Translations.UpdateOneAsync(t => t.Id == objectId, update);
        if (result.MatchedCount == 0) return NotFound();

        return NoContent();
    }

    /// <summary>
    /// Delete translation (Admin only)
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteTranslation(string id)
    {
        if (!MongoDB.Bson.ObjectId.TryParse(id, out var objectId))
            return BadRequest("Invalid translation ID format");

        var result = await _db.Translations.DeleteOneAsync(t => t.Id == objectId);
        if (result.DeletedCount == 0) return NotFound();

        return NoContent();
    }

    /// <summary>
    /// Get translation statistics (Admin only)
    /// </summary>
    [HttpGet("stats")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<TranslationStatsResponse>> GetStats()
    {
        var total = await _db.Translations.CountDocumentsAsync(Builders<Translation>.Filter.Empty);
        var done = await _db.Translations.CountDocumentsAsync(t => t.Status == "done");
        var draft = await _db.Translations.CountDocumentsAsync(t => t.Status == "draft");
        var needReview = await _db.Translations.CountDocumentsAsync(t => t.Status == "need_review");

        return Ok(new TranslationStatsResponse
        {
            Total = (int)total,
            Done = (int)done,
            Draft = (int)draft,
            NeedReview = (int)needReview
        });
    }

    /// <summary>
    /// Export translations as JSON (for app consumption)
    /// </summary>
    [HttpGet("export")]
    public async Task<ActionResult> ExportTranslations([FromQuery] string? language = "vi")
    {
        var translations = await _db.Translations
            .Find(t => t.Status == "done")
            .ToListAsync();

        var result = new Dictionary<string, string>();
        foreach (var t in translations)
        {
            var value = language?.ToLower() switch
            {
                "vi" or "vi-vn" => t.Vietnamese,
                "en" or "en-us" => t.English,
                "zh" or "zh-cn" => t.Chinese,
                _ => t.Vietnamese
            };
            
            if (!string.IsNullOrEmpty(value))
                result[t.Key] = value;
        }

        return Ok(result);
    }
}

// DTOs
public class TranslationDto
{
    public string Id { get; set; } = string.Empty;
    public int Translation_ID { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Vietnamese { get; set; }
    public string? English { get; set; }
    public string? Chinese { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class TranslationListResponse
{
    public List<TranslationDto> Data { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

public class CreateTranslationRequest
{
    public string Key { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? Vietnamese { get; set; }
    public string? English { get; set; }
    public string? Chinese { get; set; }
    public string? Status { get; set; }
}

public class UpdateTranslationRequest
{
    public string? Vietnamese { get; set; }
    public string? English { get; set; }
    public string? Chinese { get; set; }
    public string Status { get; set; } = "draft";
}

public class TranslationStatsResponse
{
    public int Total { get; set; }
    public int Done { get; set; }
    public int Draft { get; set; }
    public int NeedReview { get; set; }
}
