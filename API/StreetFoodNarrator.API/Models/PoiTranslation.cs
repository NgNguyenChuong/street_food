using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace StreetFoodNarrator.API.Models;

[BsonIgnoreExtraElements]
public class PoiTranslation
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("TranslationId")]
    public int TranslationId { get; set; }

    public int POI_ID { get; set; }

    [Required, MaxLength(10)]
    public string Language { get; set; } = string.Empty;

    public int Version { get; set; } = 1;

    // Translated Contents
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [MaxLength(1000)]
    public string? FunFact { get; set; }

    [MaxLength(2000)]
    public string? Story { get; set; }

    [MaxLength(2000)]
    public string? History { get; set; }

    [MaxLength(500)]
    public string? SignatureDish { get; set; }

    public List<string>? Specialties { get; set; }

    [MaxLength(500)]
    public string? OpeningHoursText { get; set; }

    [MaxLength(500)]
    public string? Address { get; set; }

    // Workflow parameters
    [MaxLength(20)]
    public string Status { get; set; } = "Draft"; // Draft | Pending | Approved | Rejected

    public string? TranslatedBy { get; set; }
    public string? TranslatedByRole { get; set; }

    public string? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }

    [MaxLength(500)]
    public string? RejectedReason { get; set; }

    [MaxLength(1000)]
    public string? TranslatorNote { get; set; }

    public bool IsAutoTranslated { get; set; } = false;
    
    [MaxLength(100)]
    public string? AutoTranslateEngine { get; set; }

    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}
