using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using StreetFoodNarrator.API.Data;
using StreetFoodNarrator.API.Models;
using System.Globalization;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace StreetFoodNarrator.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class NotificationsController : ControllerBase
{
    private readonly MongoDbContext _db;

    public NotificationsController(MongoDbContext db)
    {
        _db = db;
    }

    [Authorize(Roles = "Admin,Vendor")]
    [HttpGet("sidebar")]
    public async Task<ActionResult<SidebarNotificationListResponse>> GetSidebarNotifications([FromQuery] int limit = 20)
    {
        var safeLimit = Math.Clamp(limit, 1, 100);
        var collection = _db.Database.GetCollection<BsonDocument>("app_notifications");
        var filter = await BuildAudienceFilterAsync();

        var docs = await collection
            .Find(filter)
            .Sort(Builders<BsonDocument>.Sort.Descending("CreatedAt").Descending("NotificationId"))
            .Limit(safeLimit)
            .ToListAsync();

        var items = docs
            .Select(MapNotification)
            .Where(n => !string.IsNullOrWhiteSpace(n.Title) || !string.IsNullOrWhiteSpace(n.Message))
            .ToList();

        return Ok(new SidebarNotificationListResponse
        {
            Data = items,
            Total = items.Count,
            PageSize = safeLimit
        });
    }

    private async Task<FilterDefinition<BsonDocument>> BuildAudienceFilterAsync()
    {
        var f = Builders<BsonDocument>.Filter;

        var roleFilter = User.IsInRole("Admin")
            ? BuildRoleFilter("admin")
            : BuildRoleFilter("vendor");

        if (!User.IsInRole("Vendor") || User.IsInRole("Admin"))
        {
            return roleFilter;
        }

        var vendorId = await ResolveVendorIdAsync();
        if (vendorId.HasValue)
        {
            var vendorFilter = f.Or(
                f.Eq("AudienceVendorId", vendorId.Value),
                f.Eq("AudienceVendorId", BsonNull.Value),
                f.Exists("AudienceVendorId", false)
            );

            return f.And(roleFilter, vendorFilter);
        }

        var noVendorTargetFilter = f.Or(
            f.Eq("AudienceVendorId", BsonNull.Value),
            f.Exists("AudienceVendorId", false)
        );

        return f.And(roleFilter, noVendorTargetFilter);
    }

    private static FilterDefinition<BsonDocument> BuildRoleFilter(string role)
    {
        var f = Builders<BsonDocument>.Filter;
        var roleRegex = new BsonRegularExpression($"^{Regex.Escape(role)}$", "i");
        var allRegex = new BsonRegularExpression("^all$", "i");

        return f.Or(
            f.Regex("AudienceRole", roleRegex),
            f.Regex("AudienceRole", allRegex),
            f.Eq("AudienceRole", BsonNull.Value),
            f.Exists("AudienceRole", false)
        );
    }

    private async Task<int?> ResolveVendorIdAsync()
    {
        foreach (var claim in User.Claims)
        {
            if (!string.Equals(claim.Type, "vendorId", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(claim.Type, "vendor_id", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (int.TryParse(claim.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var fromClaim) && fromClaim > 0)
            {
                return fromClaim;
            }
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        var vendor = await _db.VendorProfiles
            .Find(v => v.UserId == userId && !v.IsDeleted)
            .FirstOrDefaultAsync();

        return vendor?.VendorId;
    }

    private static SidebarNotificationDto MapNotification(BsonDocument doc)
    {
        var kind = GetString(doc, "Kind", "kind") ?? "info";
        var icon = GetString(doc, "Icon", "icon");

        return new SidebarNotificationDto
        {
            NotificationId = GetInt(doc, "NotificationId", "notificationId", "notification_id"),
            Kind = kind,
            Icon = !string.IsNullOrWhiteSpace(icon) ? icon : DefaultIconFor(kind),
            Title = GetString(doc, "Title", "title") ?? "Thông báo",
            Message = GetString(doc, "Message", "message") ?? string.Empty,
            Href = GetString(doc, "Href", "href"),
            Category = GetString(doc, "Category", "category"),
            Status = GetString(doc, "Status", "status", "ReviewStatus", "reviewStatus"),
            CreatedAt = GetDate(doc, "CreatedAt", "createdAt", "created_at"),
            ResponseMessage = GetString(
                doc,
                "ResponseMessage",
                "AdminResponse",
                "Response",
                "Feedback",
                "FeedbackMessage",
                "ReplyMessage",
                "RejectReason",
                "RejectedReason",
                "ModerationNote"
            ),
            ProcessedAt = GetDate(
                doc,
                "ProcessedAt",
                "processedAt",
                "HandledAt",
                "ReviewedAt",
                "ResolvedAt",
                "ResponseAt",
                "UpdatedAt",
                "RejectedAt",
                "ApprovedAt"
            ),
            ProcessedBy = GetString(doc, "ProcessedBy", "HandledBy", "ReviewedBy", "UpdatedBy", "AdminName", "ReviewerName")
        };
    }

    private static string DefaultIconFor(string kind)
    {
        return kind.Trim().ToLowerInvariant() switch
        {
            "warn" => "fa-circle-exclamation",
            "warning" => "fa-triangle-exclamation",
            "error" => "fa-circle-xmark",
            "success" => "fa-circle-check",
            _ => "fa-circle-info"
        };
    }

    private static string? GetString(BsonDocument doc, params string[] names)
    {
        foreach (var name in names)
        {
            if (!doc.TryGetValue(name, out var value) || value.IsBsonNull)
            {
                continue;
            }

            if (value.BsonType == BsonType.String)
            {
                var text = value.AsString?.Trim();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
                continue;
            }

            var asText = (value.ToString() ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(asText))
            {
                return asText;
            }
        }

        return null;
    }

    private static int? GetInt(BsonDocument doc, params string[] names)
    {
        foreach (var name in names)
        {
            if (!doc.TryGetValue(name, out var value) || value.IsBsonNull)
            {
                continue;
            }

            if (value.IsInt32) return value.AsInt32;
            if (value.IsInt64 && value.AsInt64 <= int.MaxValue && value.AsInt64 >= int.MinValue) return (int)value.AsInt64;
            if (value.IsDouble && value.AsDouble <= int.MaxValue && value.AsDouble >= int.MinValue) return (int)Math.Round(value.AsDouble);

            if (int.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static DateTime? GetDate(BsonDocument doc, params string[] names)
    {
        foreach (var name in names)
        {
            if (!doc.TryGetValue(name, out var value) || value.IsBsonNull)
            {
                continue;
            }

            if (value.BsonType == BsonType.DateTime)
            {
                return value.ToUniversalTime();
            }

            if (value.IsInt64)
            {
                try
                {
                    return DateTimeOffset.FromUnixTimeMilliseconds(value.AsInt64).UtcDateTime;
                }
                catch
                {
                    // ignore parse issue
                }
            }

            if (DateTime.TryParse(value.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
            {
                return parsed.Kind == DateTimeKind.Unspecified
                    ? DateTime.SpecifyKind(parsed, DateTimeKind.Utc)
                    : parsed.ToUniversalTime();
            }
        }

        return null;
    }
}

public class SidebarNotificationListResponse
{
    public List<SidebarNotificationDto> Data { get; set; } = new();
    public int Total { get; set; }
    public int PageSize { get; set; }
}

public class SidebarNotificationDto
{
    public int? NotificationId { get; set; }
    public string Kind { get; set; } = "info";
    public string Icon { get; set; } = "fa-circle-info";
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Href { get; set; }
    public string? Category { get; set; }
    public string? Status { get; set; }
    public DateTime? CreatedAt { get; set; }
    public string? ResponseMessage { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string? ProcessedBy { get; set; }
}
