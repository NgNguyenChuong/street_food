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

    private ActionResult TranslationManagementDisabled()
    {
        return StatusCode(StatusCodes.Status410Gone, new
        {
            message = "Translation management has been removed from this system."
        });
    }

    /// <summary>
    /// Get all translations with filtering
    /// </summary>
    [HttpGet]
    public ActionResult<TranslationListResponse> GetTranslations(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? search = null,
        [FromQuery] string? category = null,
        [FromQuery] string? status = null)
    {
        return TranslationManagementDisabled();
    }

    /// <summary>
    /// Get translation by ID
    /// </summary>
    [HttpGet("{id}")]
    public ActionResult<Translation> GetTranslation(string id)
    {
        return TranslationManagementDisabled();
    }

    /// <summary>
    /// Create new translation (Admin only)
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public ActionResult<Translation> CreateTranslation([FromBody] CreateTranslationRequest request)
    {
        return TranslationManagementDisabled();
    }

    /// <summary>
    /// Update translation (Admin only)
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public IActionResult UpdateTranslation(string id, [FromBody] UpdateTranslationRequest request)
    {
        return TranslationManagementDisabled();
    }

    /// <summary>
    /// Delete translation (Admin only)
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public IActionResult DeleteTranslation(string id)
    {
        return TranslationManagementDisabled();
    }

    /// <summary>
    /// Get translation statistics (Admin only)
    /// </summary>
    [HttpGet("stats")]
    [Authorize(Roles = "Admin")]
    public ActionResult<TranslationStatsResponse> GetStats()
    {
        return TranslationManagementDisabled();
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
