using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace StreetFoodNarrator.API.Models;

[BsonIgnoreExtraElements]
public class AppNotification
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("NotificationId")]
    public long NotificationId { get; set; }

    [MaxLength(20)]
    public string AudienceRole { get; set; } = NotificationAudienceRoles.Admin;

    public int? AudienceVendorId { get; set; }

    [MaxLength(30)]
    public string Kind { get; set; } = "info";

    [MaxLength(80)]
    public string Icon { get; set; } = "fa-circle-info";

    [MaxLength(160)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(700)]
    public string Message { get; set; } = string.Empty;

    [MaxLength(220)]
    public string? Href { get; set; }

    [MaxLength(40)]
    public string Category { get; set; } = "general";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class AppNotificationDto
{
    public long NotificationId { get; set; }
    public string AudienceRole { get; set; } = NotificationAudienceRoles.Admin;
    public int? AudienceVendorId { get; set; }
    public string Kind { get; set; } = "info";
    public string Icon { get; set; } = "fa-circle-info";
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Href { get; set; }
    public string Category { get; set; } = "general";
    public DateTime CreatedAt { get; set; }
}

public class AppNotificationListResponse
{
    public List<AppNotificationDto> Data { get; set; } = new();
    public int Total { get; set; }
}

public static class NotificationAudienceRoles
{
    public const string Admin = "admin";
    public const string Vendor = "vendor";
    public const string Both = "both";
}

public static class NotificationGroups
{
    public const string AdminGroup = "notifications:role:admin";
    public const string VendorRoleGroup = "notifications:role:vendor";

    public static string VendorGroup(int vendorId) => $"notifications:vendor:{vendorId}";
}
