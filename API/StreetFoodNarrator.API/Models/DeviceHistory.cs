using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace StreetFoodNarrator.API.Models;

[BsonIgnoreExtraElements]
public class DeviceHistory
{
    [BsonId]
    public ObjectId Id { get; set; }

    public string DeviceId { get; set; } = string.Empty;
    public string? UserId { get; set; }

    public string EventType { get; set; } = string.Empty; // e.g. "audio_played"

    public int? POI_ID { get; set; }
    public int? TourId { get; set; }
    public int? AudioContentId { get; set; }
    public string? Language { get; set; }

    // Detailed event metrics
    public DeviceHistoryEventData? EventData { get; set; }

    [BsonElement("Location")]
    public GeoJsonLocation? Location { get; set; }

    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}

public class DeviceHistoryEventData
{
    public int? Duration { get; set; }
    public double? CompletionRate { get; set; }
    public bool? WasSkipped { get; set; }
    public int? SkippedAt { get; set; }
    public string? TriggerType { get; set; } // geo | manual
}
