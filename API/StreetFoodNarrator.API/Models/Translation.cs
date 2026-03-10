using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace StreetFoodNarrator.API.Models;

/// <summary>
/// Translation strings for i18n support
/// </summary>
[BsonIgnoreExtraElements]
public class Translation
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("Translation_ID")]
    public int Translation_ID { get; set; }
    
    /// <summary>
    /// Translation key (e.g., "welcome", "poi_nearby")
    /// </summary>
    [Required, MaxLength(100)]
    public string Key { get; set; } = string.Empty;
    
    /// <summary>
    /// Category for grouping (e.g., "general", "poi", "audio", "error")
    /// </summary>
    [MaxLength(50)]
    public string Category { get; set; } = "general";
    
    /// <summary>
    /// Vietnamese translation
    /// </summary>
    [MaxLength(500)]
    public string? Vietnamese { get; set; }
    
    /// <summary>
    /// English translation
    /// </summary>
    [MaxLength(500)]
    public string? English { get; set; }
    
    /// <summary>
    /// Chinese translation (optional)
    /// </summary>
    [MaxLength(500)]
    public string? Chinese { get; set; }
    
    /// <summary>
    /// Translation status (draft, done, need_review)
    /// </summary>
    [MaxLength(20)]
    public string Status { get; set; } = "draft";
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
