using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using StreetFoodNarrator.API.Data;
using StreetFoodNarrator.API.Models;
using System.Security.Claims;

namespace StreetFoodNarrator.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize(Roles = "Admin,Vendor")]
public class SettingsController : ControllerBase
{
    private readonly MongoDbContext _db;

    public SettingsController(MongoDbContext db)
    {
        _db = db;
    }

    [HttpGet("me")]
    public async Task<ActionResult<UserSettings>> GetMySettings([FromQuery] string? role = null)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var resolvedRole = ResolveRole(role);
        if (resolvedRole == null)
        {
            return Forbid();
        }

        var settings = await _db.UserSettings
            .Find(x => x.UserId == userId && x.Role == resolvedRole)
            .FirstOrDefaultAsync();

        if (settings == null)
        {
            settings = CreateDefaults(userId, resolvedRole);
            await _db.UserSettings.InsertOneAsync(settings);
        }

        return Ok(settings);
    }

    [HttpPut("me")]
    public async Task<ActionResult<UserSettings>> UpdateMySettings([FromBody] SettingsUpdateDto dto, [FromQuery] string? role = null)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var resolvedRole = ResolveRole(role);
        if (resolvedRole == null)
        {
            return Forbid();
        }

        var settings = await _db.UserSettings
            .Find(x => x.UserId == userId && x.Role == resolvedRole)
            .FirstOrDefaultAsync();

        if (settings == null)
        {
            settings = CreateDefaults(userId, resolvedRole);
        }

        ApplyUpdate(settings, dto, resolvedRole);
        settings.UpdatedAt = DateTime.UtcNow;

        await _db.UserSettings.ReplaceOneAsync(
            x => x.UserId == userId && x.Role == resolvedRole,
            settings,
            new ReplaceOptions { IsUpsert = true }
        );

        return Ok(settings);
    }

    private string? ResolveRole(string? role)
    {
        if (!string.IsNullOrWhiteSpace(role))
        {
            if (role.Equals("admin", StringComparison.OrdinalIgnoreCase) && User.IsInRole("Admin"))
            {
                return "Admin";
            }
            if (role.Equals("vendor", StringComparison.OrdinalIgnoreCase) && User.IsInRole("Vendor"))
            {
                return "Vendor";
            }
        }

        if (User.IsInRole("Admin")) return "Admin";
        if (User.IsInRole("Vendor")) return "Vendor";
        return null;
    }

    private static UserSettings CreateDefaults(string userId, string role)
    {
        return new UserSettings
        {
            UserId = userId,
            Role = role,
            AppName = role == "Admin" ? "Street Food Narrator" : null,
            ContactEmail = role == "Admin" ? "admin@streetfood.com" : null,
            DefaultGeofenceRadius = role == "Admin" ? 50 : null,
            DefaultLanguage = "vi",
            Notifications = new NotificationSettings
            {
                Email = true,
                Push = true,
                WeeklyReport = false
            },
            Security = new SecuritySettings
            {
                TwoFactorEnabled = false
            },
            TTS = new TTSSettings
            {
                Voice = "vi-VN-HoaiMyNeural",
                Speed = 1.0,
                Pitch = 1.0,
                Volume = 80,
                AutoPlay = false
            },
            Location = new LocationSettings
            {
                SensitivityRadius = 40
            }
        };
    }

    private static void ApplyUpdate(UserSettings settings, SettingsUpdateDto dto, string role)
    {
        if (role == "Admin")
        {
            settings.AppName = dto.AppName ?? settings.AppName;
            settings.ContactEmail = dto.ContactEmail ?? settings.ContactEmail;
            settings.DefaultGeofenceRadius = dto.DefaultGeofenceRadius ?? settings.DefaultGeofenceRadius;
        }

        settings.DefaultLanguage = dto.DefaultLanguage ?? settings.DefaultLanguage;

        if (dto.Notifications != null)
        {
            settings.Notifications.Email = dto.Notifications.Email ?? settings.Notifications.Email;
            settings.Notifications.Push = dto.Notifications.Push ?? settings.Notifications.Push;
            settings.Notifications.WeeklyReport = dto.Notifications.WeeklyReport ?? settings.Notifications.WeeklyReport;
        }

        if (dto.Security != null)
        {
            settings.Security.TwoFactorEnabled = dto.Security.TwoFactorEnabled ?? settings.Security.TwoFactorEnabled;
        }

        if (dto.TTS != null)
        {
            settings.TTS.Voice = dto.TTS.Voice ?? settings.TTS.Voice;
            settings.TTS.Speed = dto.TTS.Speed ?? settings.TTS.Speed;
            settings.TTS.Pitch = dto.TTS.Pitch ?? settings.TTS.Pitch;
            settings.TTS.Volume = dto.TTS.Volume ?? settings.TTS.Volume;
            settings.TTS.AutoPlay = dto.TTS.AutoPlay ?? settings.TTS.AutoPlay;
        }

        if (dto.Location != null)
        {
            settings.Location.SensitivityRadius = dto.Location.SensitivityRadius ?? settings.Location.SensitivityRadius;
        }
    }
}

public class SettingsUpdateDto
{
    public string? AppName { get; set; }
    public string? ContactEmail { get; set; }
    public int? DefaultGeofenceRadius { get; set; }
    public string? DefaultLanguage { get; set; }
    public NotificationSettingsUpdateDto? Notifications { get; set; }
    public SecuritySettingsUpdateDto? Security { get; set; }
    public TTSSettingsUpdateDto? TTS { get; set; }
    public LocationSettingsUpdateDto? Location { get; set; }
}

public class NotificationSettingsUpdateDto
{
    public bool? Email { get; set; }
    public bool? Push { get; set; }
    public bool? WeeklyReport { get; set; }
}

public class SecuritySettingsUpdateDto
{
    public bool? TwoFactorEnabled { get; set; }
}

public class TTSSettingsUpdateDto
{
    public string? Voice { get; set; }
    public double? Speed { get; set; }
    public double? Pitch { get; set; }
    public int? Volume { get; set; }
    public bool? AutoPlay { get; set; }
}

public class LocationSettingsUpdateDto
{
    public int? SensitivityRadius { get; set; }
}
