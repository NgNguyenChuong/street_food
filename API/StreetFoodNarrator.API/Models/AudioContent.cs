using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace StreetFoodNarrator.API.Models;

[BsonIgnoreExtraElements]
public class AudioContent
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("AudioContent_ID")]
    public int AudioContent_ID { get; set; }
    
    public int POI_ID { get; set; }
    [BsonIgnore]
    public POI POI { get; set; } = null!;
    
    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;
    
    [MaxLength(1000)]
    public string? Description { get; set; }
    
    [MaxLength(500)]
    public string? AudioUrl { get; set; }

    [Required, MaxLength(10)]
    public string Language { get; set; } = "vi";

    // Audio file metadata
    [MaxLength(10)]
    public string? Format { get; set; } = "mp3";
    public int? Bitrate { get; set; }

    [MaxLength(20)]
    public string Status { get; set; } = AudioStatuses.Draft;

    public int? VendorId { get; set; }

    [MaxLength(64)]
    public string? CreatedByUserId { get; set; }

    [MaxLength(20)]
    public string? CreatedByRole { get; set; }

    [MaxLength(64)]
    public string? ApprovedByUserId { get; set; }

    public DateTime? ApprovedAt { get; set; }

    [MaxLength(500)]
    public string? RejectedReason { get; set; }

    // TTS metadata
    [MaxLength(100)]
    public string? TTSVoice { get; set; }
    [MaxLength(100)]
    public string? TTSProvider { get; set; }
    public double? TTSSpeed { get; set; }
    public double? TTSPitch { get; set; }
    public TTSConfig? TTSConfig { get; set; }

    // Template metadata (bulk TTS)
    [MaxLength(50)]
    public string? TemplateId { get; set; }
    [MaxLength(200)]
    public string? TemplateName { get; set; }
    public Dictionary<string, string>? TemplateVariables { get; set; }

    // Analytics
    public long PlayCount { get; set; } = 0;
    public long DownloadCount { get; set; } = 0;
    public double? CompletionRate { get; set; }
    public int? AverageListenTime { get; set; }
    public double? SkipRate { get; set; }

    // Versioning / publish timestamps
    public int Version { get; set; } = 1;
    public int? PreviousVersionId { get; set; }
    public DateTime? PublishedAt { get; set; }
    
    public int? Duration { get; set; } // seconds
    
    public long? FileSize { get; set; } // bytes
    
    [MaxLength(2000)]
    public string? TTSText { get; set; }
    
    public bool IsActive { get; set; } = true;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
}

public class TTSConfig
{
    public string? Voice { get; set; }
    public double? Speed { get; set; }
    public double? Pitch { get; set; }
    public double? Volume { get; set; }
    public string? Provider { get; set; }
}

public static class AudioStatuses
{
    public const string Draft = "draft";
    public const string Pending = "pending";
    public const string Approved = "approved";
    public const string Rejected = "rejected";
    public const string Published = "published";
}
