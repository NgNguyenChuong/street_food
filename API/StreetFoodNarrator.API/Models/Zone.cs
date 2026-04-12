using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace StreetFoodNarrator.API.Models;

[BsonIgnoreExtraElements]
public class Zone
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("ZoneId")]
    public int ZoneId { get; set; }

    [Required, MaxLength(200)]
    public string Name_Vi { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Name_En { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    // GeoJSON Polygon
    // Format: { type: "Polygon", coordinates: [[[lng, lat], [lng, lat], ...]] }
    [BsonElement("Boundary")]
    public GeoJsonPolygon? Boundary { get; set; }

    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; } = false;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}

public class GeoJsonPolygon
{
    [BsonElement("type")]
    public string Type { get; set; } = "Polygon";

    [BsonElement("coordinates")]
    public double[][][] Coordinates { get; set; } = Array.Empty<double[][]>();
}
