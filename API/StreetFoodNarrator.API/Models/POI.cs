using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace StreetFoodNarrator.API.Models;

[BsonIgnoreExtraElements]
public class POI
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("POI_ID")]
    public int POI_ID { get; set; }

    // Multi-language Names - CHỈ 3 NGÔN NGỮ CHÍNH
    [Required, MaxLength(200)]
    public string Name_Vi { get; set; } = string.Empty;
    
    [MaxLength(200)]
    public string? Name_En { get; set; }
    
    [MaxLength(200)]
    public string? Name_Zh { get; set; }

    // Multi-language Descriptions
    [Required]
    public string Description_Vi { get; set; } = string.Empty;
    
    public string? Description_En { get; set; }
    public string? Description_Zh { get; set; }

    // ============================================
    // GEOSPATIAL DATA (GeoJSON Standard)
    // ============================================
    
    /// <summary>
    /// GeoJSON location 
    /// Format: { type: "Point", coordinates: [longitude, latitude] }
    /// Access: Location.Latitude / Location.Longitude (helper props on GeoJsonLocation)
    /// </summary>
    [BsonElement("location")]
    public GeoJsonLocation Location { get; set; } = new();

    [MaxLength(500)]
    public string? Address { get; set; }

    // Audio URLs for each language - CHỈ 3 NGÔN NGỮ
    [MaxLength(500)]
    public string? AudioUrl_Vi { get; set; }
    
    [MaxLength(500)]
    public string? AudioUrl_En { get; set; }
    
    [MaxLength(500)]
    public string? AudioUrl_Zh { get; set; }

    // Audio Scripts for each language (TTS Source)
    [MaxLength(4000)]
    public string? Script_Vi { get; set; }
    
    [MaxLength(4000)]
    public string? Script_En { get; set; }
    
    [MaxLength(4000)]
    public string? Script_Zh { get; set; }

    // Additional fields
    /// <summary>List of signature dishes. Use SignatureDishes[0] as the primary dish.</summary>
    public List<string>? SignatureDishes { get; set; }

    public List<string>? Specialties { get; set; }
    
    [MaxLength(500)]
    public string? FunFact { get; set; }

    [MaxLength(1000)]
    public string? History { get; set; }

    [MaxLength(1000)]
    public string? Story { get; set; }

    public decimal? EstimatedHours { get; set; }

    public List<string>? OpeningHours { get; set; }

    [MaxLength(500)]
    public string? OpeningHoursText { get; set; }

    [MaxLength(50)]
    public string? PhoneNumber { get; set; }

    public decimal? AveragePrice { get; set; }

    public int? PriceLevel { get; set; }

    public double? Rating { get; set; }
    
    public int NumReviews { get; set; } = 0;

    public List<string>? Tags { get; set; }

    [MaxLength(100)]
    public string? Category { get; set; }
    
    [MaxLength(500)]
    public string? ImageUrl { get; set; }

    public List<string>? ImageUrls { get; set; }

    // ============================================
    // GEOFENCING & ZONE MANAGEMENT
    // ============================================
    
    /// <summary>
    /// Zone Type: Area (Khu vực lớn), District (Khu trung), Spot (Điểm cụ thể)
    /// </summary>
    [MaxLength(20)]
    public string ZoneType { get; set; } = "Spot"; // "Area" | "District" | "Spot"
    
    /// <summary>
    /// Zone Level: 1 (Area), 2 (District), 3 (Spot)
    /// Computed from ZoneType for easier sorting
    /// </summary>
    public int ZoneLevel { get; set; } = 3;
    
    /// <summary>
    /// Priority for geofence selection (1-10, higher = more important)
    /// Used when multiple zones overlap
    /// </summary>
    public int Priority { get; set; } = 5;
    
    /// <summary>
    /// Trigger radius in meters (how close user needs to be)
    /// </summary>
    public int TriggerRadius { get; set; } = 50; // meters
    
    /// <summary>
    /// Cooldown period in minutes before allowing re-trigger
    /// Area: 30min, District: 15min, Spot: 0 (once per session)
    /// </summary>
    public int CooldownMinutes { get; set; } = 30;
    
    /// <summary>
    /// Parent zone ID for nested zones (Spot → District → Area)
    /// </summary>
    public int? ParentZoneId { get; set; }
    
    /// <summary>
    /// Maximum plays per session/day to prevent spam
    /// </summary>
    public int MaxPlaysPerSession { get; set; } = 1;
    
    public bool IsActive { get; set; } = true;

    // Review status for admin workflow
    [MaxLength(20)]
    public string ReviewStatus { get; set; } = "pending"; // approved | pending | rejected
    [MaxLength(500)]
    public string? ReviewNote { get; set; }
    public DateTime? ReviewedAt { get; set; }
    [MaxLength(100)]
    public string? ReviewedBy { get; set; }

    /// <summary>
    /// Snapshot of major fields changed by vendor, pending admin review.
    /// Key = field label (e.g. "T\u00ean qu\u00e1n"), Value = old/new string pair.
    /// Cleared when admin approves or rejects via ReviewPOI.
    /// </summary>
    public Dictionary<string, PendingFieldChange>? PendingChanges { get; set; }

    // Vendor relationship
    public int? VendorId { get; set; }
    [BsonIgnore]
    public VendorProfile? VendorProfile { get; set; }

    // Timestamps
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; } // Soft delete

    // Navigation properties
    [BsonIgnore]
    public ICollection<AudioContent> AudioContents { get; set; } = new List<AudioContent>();
    [BsonIgnore]
    public ICollection<POI_Tour> POI_Tours { get; set; } = new List<POI_Tour>();
    [BsonIgnore]
    public ICollection<NarrationLog> NarrationLogs { get; set; } = new List<NarrationLog>();

    // Helper methods
    public string GetName(string language = "vi")
    {
        return language.ToLower() switch
        {
            "en" => Name_En ?? Name_Vi,
            "zh" => Name_Zh ?? Name_En ?? Name_Vi,
            _ => Name_Vi
        };
    }

    public string GetDescription(string language = "vi")
    {
        return language.ToLower() switch
        {
            "en" => Description_En ?? Description_Vi,
            "zh" => Description_Zh ?? Description_En ?? Description_Vi,
            _ => Description_Vi
        };
    }

    public string? GetAudioUrl(string language = "vi")
    {
        return language.ToLower() switch
        {
            "en" => AudioUrl_En ?? AudioUrl_Vi,
            "zh" => AudioUrl_Zh ?? AudioUrl_En ?? AudioUrl_Vi,
            _ => AudioUrl_Vi
        };
    }
}

/// <summary>
/// Represents a single field change tracked for admin review.
/// </summary>
public class PendingFieldChange
{
    /// <summary>Previous value before vendor edit.</summary>
    public string? Old { get; set; }
    /// <summary>New value submitted by vendor.</summary>
    public string? New { get; set; }
}
