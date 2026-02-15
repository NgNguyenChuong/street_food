using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace StreetFoodNarrator.API.Models;

[BsonIgnoreExtraElements]
public class Tour
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("Tour_ID")]
    public int Tour_ID { get; set; }
    
    [Required, MaxLength(200)]
    public string TourName { get; set; } = string.Empty;
    
    [MaxLength(1000)]
    public string? Description { get; set; }
    
    public int EstimatedDurationMinutes { get; set; }
    public bool IsActive { get; set; } = true;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    
    [BsonIgnore]
    public ICollection<POI_Tour> POI_Tours { get; set; } = new List<POI_Tour>();
}
