using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace StreetFoodNarrator.API.Models;

[BsonIgnoreExtraElements]
public class ServiceSubmission
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("SubmissionId")]
    public int SubmissionId { get; set; }

    [BsonElement("vendor_id")]
    public int VendorId { get; set; }

    [BsonElement("vendor_user_id")]
    public string VendorUserId { get; set; } = string.Empty;

    [BsonElement("plan_code")]
    public string PlanCode { get; set; } = "premium_1y";

    [BsonElement("plan_name")]
    public string PlanName { get; set; } = "Premium 1 năm";

    [BsonElement("duration_months")]
    public int DurationMonths { get; set; } = 12;

    [BsonElement("amount_vnd")]
    public decimal AmountVnd { get; set; }

    [BsonElement("currency")]
    public string Currency { get; set; } = "VND";

    [BsonElement("payment_method")]
    public string PaymentMethod { get; set; } = "mock_bank_transfer";

    [BsonElement("transaction_ref")]
    public string TransactionRef { get; set; } = string.Empty;

    [BsonElement("status")]
    public string Status { get; set; } = SubmissionStatuses.Pending;

    [BsonElement("requested_at")]
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("paid_at")]
    public DateTime? PaidAt { get; set; }

    [BsonElement("expires_at")]
    public DateTime? ExpiresAt { get; set; }

    [BsonElement("reviewed_at")]
    public DateTime? ReviewedAt { get; set; }

    [BsonElement("reviewed_by")]
    public string? ReviewedBy { get; set; }

    [BsonElement("review_note")]
    public string? ReviewNote { get; set; }

    [BsonElement("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("updated_at")]
    public DateTime? UpdatedAt { get; set; }
}

public static class SubmissionStatuses
{
    public const string Pending = "pending";
    public const string Approved = "approved";
    public const string Rejected = "rejected";
}
