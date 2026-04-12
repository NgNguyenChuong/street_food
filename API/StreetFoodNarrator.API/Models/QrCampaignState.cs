using MongoDB.Bson.Serialization.Attributes;

namespace StreetFoodNarrator.API.Models;

[BsonIgnoreExtraElements]
public class QrCampaignState
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement("test_code")]
    public string? TestCode { get; set; }

    [BsonElement("test_expires_at_utc")]
    public DateTime? TestExpiresAtUtc { get; set; }

    [BsonElement("updated_at_utc")]
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}