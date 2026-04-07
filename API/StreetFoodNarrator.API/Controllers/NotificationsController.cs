using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using StreetFoodNarrator.API.Data;
using StreetFoodNarrator.API.Models;
using StreetFoodNarrator.API.Services;
using System.Security.Claims;

namespace StreetFoodNarrator.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize(Roles = "Admin,Vendor")]
public class NotificationsController : ControllerBase
{
    private readonly MongoDbContext _db;

    public NotificationsController(MongoDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<AppNotificationListResponse>> GetMyNotifications(
        [FromQuery] int take = 30,
        [FromQuery] string? role = null,
        [FromQuery] long? afterId = null)
    {
        take = Math.Clamp(take, 1, 100);
        var audience = ResolveAudience(role);
        if (audience == null)
        {
            return Forbid();
        }

        var filter = Builders<AppNotification>.Filter.Empty;
        if (afterId.HasValue)
        {
            filter &= Builders<AppNotification>.Filter.Gt(n => n.NotificationId, afterId.Value);
        }

        if (audience == NotificationAudienceRoles.Admin)
        {
            filter &= Builders<AppNotification>.Filter.In(
                n => n.AudienceRole,
                new[] { NotificationAudienceRoles.Admin, NotificationAudienceRoles.Both });
        }
        else
        {
            var vendorId = await ResolveVendorIdAsync();
            if (!vendorId.HasValue)
            {
                return Ok(new AppNotificationListResponse { Data = new List<AppNotificationDto>(), Total = 0 });
            }

            var roleFilter = Builders<AppNotification>.Filter.In(
                n => n.AudienceRole,
                new[] { NotificationAudienceRoles.Vendor, NotificationAudienceRoles.Both });
            var vendorFilter = Builders<AppNotification>.Filter.Or(
                Builders<AppNotification>.Filter.Eq(n => n.AudienceVendorId, null),
                Builders<AppNotification>.Filter.Eq(n => n.AudienceVendorId, vendorId.Value));

            filter &= roleFilter & vendorFilter;
        }

        var rows = await _db.Notifications
            .Find(filter)
            .SortByDescending(n => n.NotificationId)
            .Limit(take)
            .ToListAsync();

        var dto = rows.Select(NotificationService.ToDto).ToList();
        return Ok(new AppNotificationListResponse
        {
            Data = dto,
            Total = dto.Count
        });
    }

    private string? ResolveAudience(string? role)
    {
        var requested = (role ?? string.Empty).Trim().ToLowerInvariant();

        if (requested == NotificationAudienceRoles.Admin && User.IsInRole("Admin"))
        {
            return NotificationAudienceRoles.Admin;
        }

        if (requested == NotificationAudienceRoles.Vendor && User.IsInRole("Vendor"))
        {
            return NotificationAudienceRoles.Vendor;
        }

        if (User.IsInRole("Admin") && !User.IsInRole("Vendor"))
        {
            return NotificationAudienceRoles.Admin;
        }

        if (User.IsInRole("Vendor"))
        {
            return NotificationAudienceRoles.Vendor;
        }

        if (User.IsInRole("Admin"))
        {
            return NotificationAudienceRoles.Admin;
        }

        return null;
    }

    private async Task<int?> ResolveVendorIdAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrWhiteSpace(userId))
        {
            var byUserId = await _db.VendorProfiles.Find(v => v.UserId == userId).FirstOrDefaultAsync();
            if (byUserId != null) return byUserId.VendorId;
        }

        var email = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name;
        if (!string.IsNullOrWhiteSpace(email))
        {
            var byEmail = await _db.VendorProfiles.Find(v => v.ContactEmail == email).FirstOrDefaultAsync();
            if (byEmail != null) return byEmail.VendorId;
        }

        return null;
    }
}
