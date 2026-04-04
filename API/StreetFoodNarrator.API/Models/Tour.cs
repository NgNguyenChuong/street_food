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

    [MaxLength(1000)]
    public string? ImageUrl { get; set; }
    
    public int EstimatedDurationMinutes { get; set; }
    public bool IsActive { get; set; } = true;

    // ── New fields (tour v2) ──────────────────────────────────────

    /// <summary>Food themes, e.g. ["oc","lau","nuong"]</summary>
    public List<string> Themes { get; set; } = new();

    /// <summary>Time slots, e.g. ["sang","khuya"]</summary>
    public List<string> TimeSlots { get; set; } = new();

    /// <summary>Route style: "ordered" | "free" | "theme"</summary>
    [MaxLength(20)]
    public string RouteType { get; set; } = "ordered";

    /// <summary>
    /// Ordered list of POI ObjectId strings defining the tour sequence.
    /// Replaces the separate POI_Tour join collection for this use-case.
    /// </summary>
    public List<string> PoiIds { get; set; } = new();

    /// <summary>
    /// Pre-computed walking/driving route as GeoJSON LineString.
    /// Computed once on web admin via OSRM, stored here so mobile app
    /// can render it offline without calling OSRM at runtime.
    /// </summary>
    public GeoJsonLineString? RouteGeometry { get; set; }

    // ─────────────────────────────────────────────────────────────
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    
    [BsonIgnore]
    public ICollection<POI_Tour> POI_Tours { get; set; } = new List<POI_Tour>();
}

/// <summary>
/// GeoJSON LineString — stores a pre-computed route path from OSRM.
/// Coordinates: array of [longitude, latitude] pairs (GeoJSON order).
/// </summary>
[BsonIgnoreExtraElements]
public class GeoJsonLineString
{
    [BsonElement("type")]
    public string Type { get; set; } = "LineString";

    /// <summary>Each element is [longitude, latitude].</summary>
    [BsonElement("coordinates")]
    public List<List<double>> Coordinates { get; set; } = new();
}
