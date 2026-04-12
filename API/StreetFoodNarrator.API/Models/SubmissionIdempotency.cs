using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace StreetFoodNarrator.API.Models;

public class SubmissionIdempotency
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("idempotency_key")]
    public string IdempotencyKey { get; set; } = string.Empty;

    [BsonElement("actor_user_id")]
    public string ActorUserId { get; set; } = string.Empty;

    [BsonElement("target_type")]
    public string TargetType { get; set; } = string.Empty;

    [BsonElement("target_hash")]
    public string TargetHash { get; set; } = string.Empty;

    [BsonElement("response_poi_id")]
    public int? ResponsePoiId { get; set; }

    [BsonElement("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}