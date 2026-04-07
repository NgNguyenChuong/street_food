using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using MongoDB.Driver;
using StreetFoodNarrator.API.Data;
using StreetFoodNarrator.API.Models;
using System.Security.Claims;

namespace StreetFoodNarrator.API.Hubs;

[Authorize(Roles = "Admin,Vendor")]
public class NotificationsHub : Hub
{
    private readonly MongoDbContext _db;

    public NotificationsHub(MongoDbContext db)
    {
        _db = db;
    }

    public override async Task OnConnectedAsync()
    {
        var user = Context.User;
        if (user == null)
        {
            await base.OnConnectedAsync();
            return;
        }

        if (user.IsInRole("Admin"))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, NotificationGroups.AdminGroup);
        }

        if (user.IsInRole("Vendor"))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, NotificationGroups.VendorRoleGroup);

            var vendorId = await ResolveVendorIdAsync(user);
            if (vendorId.HasValue)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, NotificationGroups.VendorGroup(vendorId.Value));
            }
        }

        await base.OnConnectedAsync();
    }

    private async Task<int?> ResolveVendorIdAsync(ClaimsPrincipal user)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrWhiteSpace(userId))
        {
            var byUserId = await _db.VendorProfiles.Find(v => v.UserId == userId).FirstOrDefaultAsync();
            if (byUserId != null) return byUserId.VendorId;
        }

        var email = user.FindFirstValue(ClaimTypes.Email) ?? user.Identity?.Name;
        if (!string.IsNullOrWhiteSpace(email))
        {
            var byEmail = await _db.VendorProfiles.Find(v => v.ContactEmail == email).FirstOrDefaultAsync();
            if (byEmail != null) return byEmail.VendorId;
        }

        return null;
    }
}
