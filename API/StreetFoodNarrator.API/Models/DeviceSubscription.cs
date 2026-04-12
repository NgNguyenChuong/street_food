using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace StreetFoodNarrator.API.Models;

[BsonIgnoreExtraElements]
public class DeviceSubscription
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("Subscription_ID")]
    public int Subscription_ID { get; set; }

    [Required, MaxLength(120)]
    public string DeviceId { get; set; } = string.Empty;

    [MaxLength(30)]
    public string PlanCode { get; set; } = "TourExplore";

    [MaxLength(32)]
    public string Status { get; set; } = "active";

    [MaxLength(80)]
    public string InvoiceNumber { get; set; } = string.Empty;

    [MaxLength(32)]
    public string RecoveryCode { get; set; } = string.Empty;

    public DateTime InvoiceCreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime StartsAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; } = DateTime.UtcNow.AddMonths(1);
    public DateTime ConfirmedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? RestoredAtUtc { get; set; }

    [MaxLength(120)]
    public string? RestoredFromDeviceId { get; set; }

    [MaxLength(160)]
    public string? TransferContent { get; set; }

    [MaxLength(40)]
    public string? Platform { get; set; }

    [MaxLength(120)]
    public string? Model { get; set; }

    [MaxLength(40)]
    public string? OsVersion { get; set; }

    [MaxLength(30)]
    public string? AppVersion { get; set; }
}