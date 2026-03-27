using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace StreetFoodNarrator.API.Models;

[BsonIgnoreExtraElements]
public class Review
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("POI_ID")]
    public int POI_ID { get; set; }

    [MaxLength(100)]
    public string UserName { get; set; } = "Khách";

    public int Rating { get; set; }

    [MaxLength(250)]
    public string? Comment { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class ReviewDto
{
    public int POI_ID { get; set; }
    
    [MaxLength(100)]
    public string UserName { get; set; } = "Khách";
    
    [Range(1, 5)]
    public int Rating { get; set; }
    
    [MaxLength(250)]
    public string? Comment { get; set; }
}
