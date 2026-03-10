using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using StreetFoodNarrator.API.Data;
using StreetFoodNarrator.API.Models;

namespace StreetFoodNarrator.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AnalyticsController : ControllerBase
{
    private readonly MongoDbContext _db;
    private readonly MongoSequenceService _sequence;

    public AnalyticsController(MongoDbContext db, MongoSequenceService sequence)
    {
        _db = db;
        _sequence = sequence;
    }

    /// <summary>
    /// Get narration logs (history)
    /// </summary>
    [HttpGet("narration-logs")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<NarrationLogListResponse>> GetNarrationLogs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] int? poiId = null,
        [FromQuery] string? userId = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        var filter = Builders<NarrationLog>.Filter.Empty;

        if (poiId.HasValue)
        {
            filter &= Builders<NarrationLog>.Filter.Eq(l => l.POI_ID, poiId.Value);
        }

        if (!string.IsNullOrEmpty(userId))
        {
            filter &= Builders<NarrationLog>.Filter.Eq(l => l.UserId, userId);
        }

        if (fromDate.HasValue)
        {
            filter &= Builders<NarrationLog>.Filter.Gte(l => l.TriggeredAt, fromDate.Value);
        }

        if (toDate.HasValue)
        {
            filter &= Builders<NarrationLog>.Filter.Lte(l => l.TriggeredAt, toDate.Value);
        }

        var total = await _db.NarrationLogs.CountDocumentsAsync(filter);
        var skip = (page - 1) * pageSize;

        var logs = await _db.NarrationLogs
            .Find(filter)
            .Sort(Builders<NarrationLog>.Sort.Descending(l => l.TriggeredAt))
            .Skip(skip)
            .Limit(pageSize)
            .ToListAsync();

        // Get POI names
        var poiIds = logs.Select(l => l.POI_ID).Distinct().ToList();
        var pois = await _db.POIs
            .Find(p => poiIds.Contains(p.POI_ID))
            .ToListAsync();
        var poiDict = pois.ToDictionary(p => p.POI_ID, p => p.Name_Vi ?? p.Name_En);

        var dtos = logs.Select(l => new NarrationLogDto
        {
            Id = l.Id.ToString(),
            Log_ID = l.Log_ID,
            POI_ID = l.POI_ID,
            POIName = poiDict.ContainsKey(l.POI_ID) ? poiDict[l.POI_ID] : "Unknown",
            UserId = l.UserId,
            TriggeredAt = l.TriggeredAt,
            TriggerType = l.TriggerType,
            WasPlayed = l.WasPlayed
        }).ToList();

        return Ok(new NarrationLogListResponse
        {
            Data = dtos,
            Total = (int)total,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling((double)total / pageSize)
        });
    }

    /// <summary>
    /// Get device tracking list
    /// </summary>
    [HttpGet("devices")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<DeviceListResponse>> GetDevices(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? platform = null,
        [FromQuery] string? search = null)
    {
        var filter = Builders<DeviceInfo>.Filter.Empty;

        if (!string.IsNullOrEmpty(platform))
        {
            filter &= Builders<DeviceInfo>.Filter.Eq(d => d.Platform, platform);
        }

        if (!string.IsNullOrEmpty(search))
        {
            var deviceIdFilter = Builders<DeviceInfo>.Filter.Regex(d => d.DeviceId, 
                new MongoDB.Bson.BsonRegularExpression(search, "i"));
            var modelFilter = Builders<DeviceInfo>.Filter.Regex(d => d.Model, 
                new MongoDB.Bson.BsonRegularExpression(search, "i"));
            filter &= Builders<DeviceInfo>.Filter.Or(deviceIdFilter, modelFilter);
        }

        var total = await _db.Devices.CountDocumentsAsync(filter);
        var skip = (page - 1) * pageSize;

        var devices = await _db.Devices
            .Find(filter)
            .Sort(Builders<DeviceInfo>.Sort.Descending(d => d.LastSeen))
            .Skip(skip)
            .Limit(pageSize)
            .ToListAsync();

        var dtos = devices.Select(d => new DeviceDto
        {
            Id = d.Id.ToString(),
            Device_ID = d.Device_ID,
            DeviceId = d.DeviceId,
            Platform = d.Platform,
            Model = d.Model,
            OsVersion = d.OsVersion,
            AppVersion = d.AppVersion,
            FirstSeen = d.FirstSeen,
            LastSeen = d.LastSeen,
            TotalSessions = d.TotalSessions,
            TotalPOIsViewed = d.TotalPOIsViewed,
            TotalAudioPlayed = d.TotalAudioPlayed,
            PreferredLanguage = d.PreferredLanguage
        }).ToList();

        return Ok(new DeviceListResponse
        {
            Data = dtos,
            Total = (int)total,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling((double)total / pageSize)
        });
    }

    /// <summary>
    /// Register or update device info
    /// </summary>
    [HttpPost("devices/register")]
    public async Task<ActionResult<DeviceInfo>> RegisterDevice([FromBody] RegisterDeviceRequest request)
    {
        var existing = await _db.Devices.Find(d => d.DeviceId == request.DeviceId).FirstOrDefaultAsync();

        if (existing != null)
        {
            // Update existing device
            var update = Builders<DeviceInfo>.Update
                .Set(d => d.LastSeen, DateTime.UtcNow)
                .Set(d => d.Platform, request.Platform)
                .Set(d => d.Model, request.Model)
                .Set(d => d.OsVersion, request.OsVersion)
                .Set(d => d.AppVersion, request.AppVersion)
                .Inc(d => d.TotalSessions, 1);

            if (!string.IsNullOrEmpty(request.PreferredLanguage))
            {
                update = update.Set(d => d.PreferredLanguage, request.PreferredLanguage);
            }

            await _db.Devices.UpdateOneAsync(d => d.DeviceId == request.DeviceId, update);
            
            var updated = await _db.Devices.Find(d => d.DeviceId == request.DeviceId).FirstOrDefaultAsync();
            return Ok(updated);
        }
        else
        {
            // Create new device
            var deviceId = await _sequence.GetNextAsync("Device_ID");

            var device = new DeviceInfo
            {
                Device_ID = deviceId,
                DeviceId = request.DeviceId,
                Platform = request.Platform,
                Model = request.Model,
                OsVersion = request.OsVersion,
                AppVersion = request.AppVersion,
                PreferredLanguage = request.PreferredLanguage,
                FirstSeen = DateTime.UtcNow,
                LastSeen = DateTime.UtcNow,
                TotalSessions = 1
            };

            await _db.Devices.InsertOneAsync(device);
            return CreatedAtAction(nameof(GetDevices), new { id = device.Id.ToString() }, device);
        }
    }

    /// <summary>
    /// Get analytics overview
    /// </summary>
    [HttpGet("overview")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<AnalyticsOverviewResponse>> GetOverview()
    {
        var totalDevices = await _db.Devices.CountDocumentsAsync(Builders<DeviceInfo>.Filter.Empty);
        var activeDevices = await _db.Devices.CountDocumentsAsync(
            d => d.LastSeen >= DateTime.UtcNow.AddDays(-7));

        var totalLogs = await _db.NarrationLogs.CountDocumentsAsync(Builders<NarrationLog>.Filter.Empty);
        var recentLogs = await _db.NarrationLogs.CountDocumentsAsync(
            l => l.TriggeredAt >= DateTime.UtcNow.AddDays(-7));

        var playedLogs = await _db.NarrationLogs.CountDocumentsAsync(l => l.WasPlayed);

        return Ok(new AnalyticsOverviewResponse
        {
            TotalDevices = (int)totalDevices,
            ActiveDevicesLast7Days = (int)activeDevices,
            TotalNarrations = (int)totalLogs,
            RecentNarrationsLast7Days = (int)recentLogs,
            TotalAudioPlayed = (int)playedLogs
        });
    }

    /// <summary>
    /// Get top POIs by views
    /// </summary>
    [HttpGet("top-pois")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<List<TopPOIDto>>> GetTopPOIs([FromQuery] int limit = 10)
    {
        var logs = await _db.NarrationLogs
            .Find(Builders<NarrationLog>.Filter.Empty)
            .ToListAsync();

        var topPoiIds = logs
            .GroupBy(l => l.POI_ID)
            .OrderByDescending(g => g.Count())
            .Take(limit)
            .Select(g => new { POI_ID = g.Key, ViewCount = g.Count() })
            .ToList();

        var poiIds = topPoiIds.Select(x => x.POI_ID).ToList();
        var pois = await _db.POIs
            .Find(p => poiIds.Contains(p.POI_ID))
            .ToListAsync();

        var result = topPoiIds.Select(x =>
        {
            var poi = pois.FirstOrDefault(p => p.POI_ID == x.POI_ID);
            return new TopPOIDto
            {
                POI_ID = x.POI_ID,
                POIName = poi?.Name_Vi ?? poi?.Name_En ?? "Unknown",
                ViewCount = x.ViewCount
            };
        }).ToList();

        return Ok(result);
    }
}

// DTOs
public class NarrationLogDto
{
    public string Id { get; set; } = string.Empty;
    public int Log_ID { get; set; }
    public int POI_ID { get; set; }
    public string POIName { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public DateTime TriggeredAt { get; set; }
    public string TriggerType { get; set; } = string.Empty;
    public bool WasPlayed { get; set; }
}

public class NarrationLogListResponse
{
    public List<NarrationLogDto> Data { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

public class DeviceDto
{
    public string Id { get; set; } = string.Empty;
    public int Device_ID { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string? Model { get; set; }
    public string? OsVersion { get; set; }
    public string? AppVersion { get; set; }
    public DateTime FirstSeen { get; set; }
    public DateTime LastSeen { get; set; }
    public int TotalSessions { get; set; }
    public int TotalPOIsViewed { get; set; }
    public int TotalAudioPlayed { get; set; }
    public string? PreferredLanguage { get; set; }
}

public class DeviceListResponse
{
    public List<DeviceDto> Data { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

public class RegisterDeviceRequest
{
    public string DeviceId { get; set; } = string.Empty;
    public string Platform { get; set; } = "Unknown";
    public string? Model { get; set; }
    public string? OsVersion { get; set; }
    public string? AppVersion { get; set; }
    public string? PreferredLanguage { get; set; }
}

public class AnalyticsOverviewResponse
{
    public int TotalDevices { get; set; }
    public int ActiveDevicesLast7Days { get; set; }
    public int TotalNarrations { get; set; }
    public int RecentNarrationsLast7Days { get; set; }
    public int TotalAudioPlayed { get; set; }
}

public class TopPOIDto
{
    public int POI_ID { get; set; }
    public string POIName { get; set; } = string.Empty;
    public int ViewCount { get; set; }
}
