using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace StreetFoodNarrator.API.Models;

[BsonIgnoreExtraElements]
public class NarrationLog
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("Log_ID")]
    public int Log_ID { get; set; }
    
    public int POI_ID { get; set; }
    [BsonIgnore]
    public POI POI { get; set; } = null!;
    
    [MaxLength(100)]
    public string? UserId { get; set; }
    
    public DateTime TriggeredAt { get; set; } = DateTime.UtcNow;
    
    [MaxLength(20)]
    public string TriggerType { get; set; } = "Auto"; // Auto | Manual
    
    public decimal? UserLatitude { get; set; }
    public decimal? UserLongitude { get; set; }
    
    public bool WasPlayed { get; set; } = false;
}
