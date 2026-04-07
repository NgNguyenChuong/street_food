using Microsoft.AspNetCore.SignalR;
using StreetFoodNarrator.API.Data;
using StreetFoodNarrator.API.Hubs;
using StreetFoodNarrator.API.Models;

namespace StreetFoodNarrator.API.Services;

public class NotificationService
{
    private readonly MongoDbContext _db;
    private readonly MongoSequenceService _sequence;
    private readonly IHubContext<NotificationsHub> _hub;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        MongoDbContext db,
        MongoSequenceService sequence,
        IHubContext<NotificationsHub> hub,
        ILogger<NotificationService> logger)
    {
        _db = db;
        _sequence = sequence;
        _hub = hub;
        _logger = logger;
    }

    public async Task PublishToAdminsAsync(
        string title,
        string message,
        string? href = null,
        string kind = "info",
        string icon = "fa-circle-info",
        string category = "general")
    {
        await PublishAsync(new NotificationPublishRequest
        {
            AudienceRole = NotificationAudienceRoles.Admin,
            Title = title,
            Message = message,
            Href = href,
            Kind = kind,
            Icon = icon,
            Category = category
        });
    }

    public async Task PublishToVendorAsync(
        int vendorId,
        string title,
        string message,
        string? href = null,
        string kind = "info",
        string icon = "fa-circle-info",
        string category = "general")
    {
        await PublishAsync(new NotificationPublishRequest
        {
            AudienceRole = NotificationAudienceRoles.Vendor,
            AudienceVendorId = vendorId,
            Title = title,
            Message = message,
            Href = href,
            Kind = kind,
            Icon = icon,
            Category = category
        });
    }

    public async Task PublishToAllVendorsAsync(
        string title,
        string message,
        string? href = null,
        string kind = "info",
        string icon = "fa-circle-info",
        string category = "general")
    {
        await PublishAsync(new NotificationPublishRequest
        {
            AudienceRole = NotificationAudienceRoles.Vendor,
            AudienceVendorId = null,
            Title = title,
            Message = message,
            Href = href,
            Kind = kind,
            Icon = icon,
            Category = category
        });
    }

    public async Task PublishAsync(NotificationPublishRequest request)
    {
        try
        {
            var notificationId = await _sequence.GetNextAsync("notification_id");
            var entity = new AppNotification
            {
                NotificationId = notificationId,
                AudienceRole = NormalizeAudienceRole(request.AudienceRole),
                AudienceVendorId = request.AudienceVendorId,
                Kind = NormalizeKind(request.Kind),
                Icon = string.IsNullOrWhiteSpace(request.Icon) ? "fa-circle-info" : request.Icon.Trim(),
                Title = string.IsNullOrWhiteSpace(request.Title) ? "Thông báo" : request.Title.Trim(),
                Message = string.IsNullOrWhiteSpace(request.Message) ? "Có cập nhật mới." : request.Message.Trim(),
                Href = string.IsNullOrWhiteSpace(request.Href) ? null : request.Href.Trim(),
                Category = string.IsNullOrWhiteSpace(request.Category) ? "general" : request.Category.Trim().ToLowerInvariant(),
                CreatedAt = DateTime.UtcNow
            };

            await _db.Notifications.InsertOneAsync(entity);
            var dto = ToDto(entity);
            await BroadcastAsync(dto);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Publish notification failed");
        }
    }

    private async Task BroadcastAsync(AppNotificationDto dto)
    {
        switch (dto.AudienceRole)
        {
            case NotificationAudienceRoles.Admin:
                await _hub.Clients.Group(NotificationGroups.AdminGroup).SendAsync("notification", dto);
                break;
            case NotificationAudienceRoles.Vendor:
                if (dto.AudienceVendorId.HasValue)
                {
                    await _hub.Clients.Group(NotificationGroups.VendorGroup(dto.AudienceVendorId.Value))
                        .SendAsync("notification", dto);
                }
                else
                {
                    await _hub.Clients.Group(NotificationGroups.VendorRoleGroup).SendAsync("notification", dto);
                }
                break;
            default:
                await _hub.Clients.Group(NotificationGroups.AdminGroup).SendAsync("notification", dto);
                await _hub.Clients.Group(NotificationGroups.VendorRoleGroup).SendAsync("notification", dto);
                break;
        }
    }

    private static string NormalizeAudienceRole(string? role)
    {
        var normalized = (role ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized == NotificationAudienceRoles.Admin) return NotificationAudienceRoles.Admin;
        if (normalized == NotificationAudienceRoles.Vendor) return NotificationAudienceRoles.Vendor;
        if (normalized == NotificationAudienceRoles.Both) return NotificationAudienceRoles.Both;
        return NotificationAudienceRoles.Admin;
    }

    private static string NormalizeKind(string? kind)
    {
        var normalized = (kind ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "success" => "success",
            "warn" => "warn",
            "warning" => "warn",
            "error" => "warn",
            _ => "info"
        };
    }

    public static AppNotificationDto ToDto(AppNotification entity)
    {
        return new AppNotificationDto
        {
            NotificationId = entity.NotificationId,
            AudienceRole = entity.AudienceRole,
            AudienceVendorId = entity.AudienceVendorId,
            Kind = entity.Kind,
            Icon = entity.Icon,
            Title = entity.Title,
            Message = entity.Message,
            Href = entity.Href,
            Category = entity.Category,
            CreatedAt = entity.CreatedAt
        };
    }
}

public class NotificationPublishRequest
{
    public string AudienceRole { get; set; } = NotificationAudienceRoles.Admin;
    public int? AudienceVendorId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Href { get; set; }
    public string Kind { get; set; } = "info";
    public string Icon { get; set; } = "fa-circle-info";
    public string Category { get; set; } = "general";
}
