using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using StreetFoodNarrator.API.Data;
using StreetFoodNarrator.API.Models;

namespace StreetFoodNarrator.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class SettingsController : ControllerBase
{
    private readonly MongoDbContext _db;

    public SettingsController(MongoDbContext db)
    {
        _db = db;
    }

    [HttpGet("{deviceId}")]
    public async Task<ActionResult<UserSettings>> GetDeviceSettings(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId)) return BadRequest("DeviceId is required");

        var settings = await _db.UserSettings
            .Find(x => x.DeviceId == deviceId)
            .FirstOrDefaultAsync();

        if (settings == null)
        {
            settings = CreateDefaults(deviceId);
            await _db.UserSettings.InsertOneAsync(settings);
        }

        return Ok(settings);
    }

    [HttpPut("{deviceId}")]
    public async Task<ActionResult<UserSettings>> UpdateDeviceSettings(string deviceId, [FromBody] SettingsUpdateDto dto)
    {
        if (string.IsNullOrWhiteSpace(deviceId)) return BadRequest("DeviceId is required");

        var settings = await _db.UserSettings
            .Find(x => x.DeviceId == deviceId)
            .FirstOrDefaultAsync();

        if (settings == null)
        {
            settings = CreateDefaults(deviceId);
        }

        ApplyUpdate(settings, dto);
        settings.UpdatedAt = DateTime.UtcNow;

        await _db.UserSettings.ReplaceOneAsync(
            x => x.DeviceId == deviceId,
            settings,
            new ReplaceOptions { IsUpsert = true }
        );

        return Ok(settings);
    }

    private static UserSettings CreateDefaults(string deviceId)
    {
        return new UserSettings
        {
            DeviceId = deviceId,
            DefaultLanguage = "vi",
            Location = new LocationSettings { SensitivityRadius = 40 },
            TTS = new TTSSettings
            {
                Voice = "vi-VN-HoaiMyNeural",
                Speed = 1.0,
                Pitch = 1.0,
                Volume = 80,
                AutoPlay = false
            }
        };
    }

    private static void ApplyUpdate(UserSettings settings, SettingsUpdateDto dto)
    {
        settings.DefaultLanguage = dto.DefaultLanguage ?? settings.DefaultLanguage;

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
    public string? DefaultLanguage { get; set; }
    public TTSSettingsUpdateDto? TTS { get; set; }
    public LocationSettingsUpdateDto? Location { get; set; }
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
