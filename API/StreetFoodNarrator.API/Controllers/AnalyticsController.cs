using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using StreetFoodNarrator.API.Data;
using StreetFoodNarrator.API.Models;
using System.ComponentModel.DataAnnotations;

namespace StreetFoodNarrator.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AnalyticsController : ControllerBase
{
    private readonly MongoDbContext _db;
    private readonly MongoSequenceService _sequence;

    // Keep this boundary aligned with mobile map scope: AppConfig default center +/- 0.01.
    private const decimal VinhKhanhLatitudeMin = 10.7528m;
    private const decimal VinhKhanhLatitudeMax = 10.7728m;
    private const decimal VinhKhanhLongitudeMin = 106.6928m;
    private const decimal VinhKhanhLongitudeMax = 106.7128m;

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
            POIName = (poiDict.TryGetValue(l.POI_ID, out var poiName) ? poiName : null) ?? "Unknown",
            UserId = l.UserId,
            SessionId = l.SessionId,
            DeviceId = l.DeviceId,
            TriggeredAt = l.TriggeredAt,
            TriggerType = l.TriggerType,
            ActionType = l.ActionType,
            DwellSeconds = l.DwellSeconds,
            UserLatitude = l.UserLatitude,
            UserLongitude = l.UserLongitude,
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
        var safeLimit = Math.Clamp(limit, 1, 100);

        // Primary source: persisted POI-level listen stats (PlayCount/MeanPlay).
        var poiStats = await _db.POIs
            .Find(p => p.DeletedAt == null && p.PlayCount > 0)
            .Project(p => new
            {
                p.POI_ID,
                p.Name_Vi,
                p.Name_En,
                p.PlayCount,
                p.MeanPlay
            })
            .ToListAsync();

        if (poiStats.Count > 0)
        {
            var ranked = poiStats
                .Select(p => new TopPOIDto
                {
                    POI_ID = p.POI_ID,
                    POIName = p.Name_Vi ?? p.Name_En ?? "Unknown",
                    ViewCount = (int)Math.Min(int.MaxValue, Math.Max(0, p.PlayCount)),
                    PlayCount = Math.Max(0, p.PlayCount),
                    MeanPlay = Math.Max(0, p.MeanPlay),
                    TotalListenSeconds = Math.Max(0, p.PlayCount) * Math.Max(0, p.MeanPlay)
                })
                .OrderByDescending(x => x.TotalListenSeconds)
                .ThenByDescending(x => x.PlayCount)
                .ThenBy(x => x.POIName)
                .Take(safeLimit)
                .ToList();

            return Ok(ranked);
        }

        // Backward-compatible fallback for legacy datasets where POI stats are still empty.
        var playedLogs = await _db.NarrationLogs
            .Find(l => l.WasPlayed)
            .ToListAsync();

        var topPoiIds = playedLogs
            .GroupBy(l => l.POI_ID)
            .OrderByDescending(g => g.Count())
            .Take(safeLimit)
            .Select(g => new { POI_ID = g.Key, ViewCount = g.Count() })
            .ToList();

        var poiIds = topPoiIds.Select(x => x.POI_ID).ToList();
        var pois = await _db.POIs
            .Find(p => poiIds.Contains(p.POI_ID))
            .ToListAsync();

        var fallback = topPoiIds.Select(x =>
        {
            var poi = pois.FirstOrDefault(p => p.POI_ID == x.POI_ID);
            return new TopPOIDto
            {
                POI_ID = x.POI_ID,
                POIName = poi?.Name_Vi ?? poi?.Name_En ?? "Unknown",
                ViewCount = x.ViewCount,
                PlayCount = x.ViewCount,
                MeanPlay = 0,
                TotalListenSeconds = 0
            };
        }).ToList();

        return Ok(fallback);
    }

    /// <summary>
    /// Finalize listened progress for a POI and update running average metrics.
    /// mean_new = (n * mean_old + x_new) / (n + 1)
    /// </summary>
    [HttpPost("poi-listen-progress")]
    [AllowAnonymous]
    public async Task<ActionResult> UpdatePoiListenProgress([FromBody] PoiListenProgressRequest request)
    {
        if (request.POI_ID <= 0)
            return BadRequest(new { message = "POI_ID must be greater than 0." });

        var listenSeconds = Math.Round(Math.Max(0, request.ListenSeconds), 2);
        if (listenSeconds <= 0)
            return Ok(new { success = true, skipped = true, reason = "listen_seconds_non_positive" });

        var playbackSessionId = request.PlaybackSessionId?.Trim();
        if (!string.IsNullOrWhiteSpace(playbackSessionId))
        {
            var actorUserId =
                !string.IsNullOrWhiteSpace(request.SessionId) ? request.SessionId!.Trim() :
                !string.IsNullOrWhiteSpace(request.DeviceId) ? request.DeviceId!.Trim() :
                "anonymous";

            var idempotencyKey = $"poi-listen-progress:{playbackSessionId}";
            var idempotency = new SubmissionIdempotency
            {
                IdempotencyKey = idempotencyKey,
                ActorUserId = actorUserId,
                TargetType = "poi_listen_progress",
                TargetHash = $"{request.POI_ID}:{listenSeconds:0.00}",
                ResponsePoiId = request.POI_ID,
                CreatedAt = DateTime.UtcNow
            };

            try
            {
                await _db.SubmissionIdempotencies.InsertOneAsync(idempotency);
            }
            catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
            {
                return Ok(new
                {
                    success = true,
                    duplicate = true,
                    poiId = request.POI_ID,
                    listenedSeconds = listenSeconds
                });
            }
        }

        var poi = await _db.POIs
            .Find(p => p.POI_ID == request.POI_ID && p.DeletedAt == null)
            .FirstOrDefaultAsync();

        if (poi == null)
            return NotFound(new { message = "POI not found" });

        var oldCount = Math.Max(0, poi.PlayCount);
        var oldMean = Math.Max(0, poi.MeanPlay);
        var newCount = oldCount + 1;
        var newMean = ((oldCount * oldMean) + listenSeconds) / newCount;

        var update = Builders<POI>.Update
            .Set(p => p.PlayCount, newCount)
            .Set(p => p.MeanPlay, newMean)
            .Set(p => p.UpdatedAt, DateTime.UtcNow);

        await _db.POIs.UpdateOneAsync(p => p.POI_ID == request.POI_ID && p.DeletedAt == null, update);

        return Ok(new
        {
            success = true,
            poiId = request.POI_ID,
            playCount = newCount,
            meanPlay = Math.Round(newMean, 2),
            totalListenSeconds = Math.Round(newCount * newMean, 2),
            listenedSeconds = listenSeconds,
            source = string.IsNullOrWhiteSpace(request.Source) ? "unknown" : request.Source
        });
    }

    /// <summary>
    /// Receive anonymous movement/narration logs from mobile app.
    /// </summary>
    [HttpPost("narration-logs/mobile")]
    [AllowAnonymous]
    public async Task<ActionResult> CreateMobileNarrationLog([FromBody] MobileNarrationLogRequest request)
    {
        if (request.POI_ID <= 0)
            return BadRequest(new { message = "POI_ID must be greater than 0." });

        POI? poiForCoordinateFallback = null;
        if (!request.UserLatitude.HasValue || !request.UserLongitude.HasValue)
        {
            poiForCoordinateFallback = await _db.POIs
                .Find(p => p.POI_ID == request.POI_ID && p.DeletedAt == null)
                .FirstOrDefaultAsync();
        }

        if (!TryResolveCoordinatesForAreaCheck(request, poiForCoordinateFallback, out var effectiveLatitude, out var effectiveLongitude) ||
            !IsInsideVinhKhanhArea(effectiveLatitude, effectiveLongitude))
        {
            return Ok(new
            {
                success = true,
                skipped = true,
                reason = "outside_vinh_khanh_area"
            });
        }

        var triggeredAt = request.TriggeredAt?.ToUniversalTime() ?? DateTime.UtcNow;
        var safeTriggerType = string.IsNullOrWhiteSpace(request.TriggerType)
            ? "LocationPing"
            : request.TriggerType.Trim();
        var safeActionType = string.IsNullOrWhiteSpace(request.ActionType)
            ? "LocationPing"
            : request.ActionType.Trim();

        var log = new NarrationLog
        {
            Log_ID = await _sequence.GetNextAsync("Log_ID"),
            POI_ID = request.POI_ID,
            UserId = string.IsNullOrWhiteSpace(request.UserId) ? request.SessionId : request.UserId,
            SessionId = request.SessionId,
            DeviceId = request.DeviceId,
            TriggeredAt = triggeredAt,
            TriggerType = safeTriggerType,
            ActionType = safeActionType,
            DwellSeconds = request.DwellSeconds,
            UserLatitude = request.UserLatitude ?? effectiveLatitude,
            UserLongitude = request.UserLongitude ?? effectiveLongitude,
            WasPlayed = request.WasPlayed
        };

        await _db.NarrationLogs.InsertOneAsync(log);

        if (!string.IsNullOrWhiteSpace(request.DeviceId))
        {
            var existing = await _db.Devices.Find(d => d.DeviceId == request.DeviceId).FirstOrDefaultAsync();
            if (existing == null)
            {
                var device = new DeviceInfo
                {
                    Device_ID = await _sequence.GetNextAsync("Device_ID"),
                    DeviceId = request.DeviceId,
                    Platform = string.IsNullOrWhiteSpace(request.Platform) ? "Unknown" : request.Platform,
                    Model = request.Model,
                    OsVersion = request.OsVersion,
                    AppVersion = request.AppVersion,
                    PreferredLanguage = request.Language,
                    FirstSeen = DateTime.UtcNow,
                    LastSeen = DateTime.UtcNow,
                    TotalSessions = 1,
                    TotalPOIsViewed = 1,
                    TotalAudioPlayed = request.WasPlayed ? 1 : 0
                };
                await _db.Devices.InsertOneAsync(device);
            }
            else
            {
                var update = Builders<DeviceInfo>.Update
                    .Set(d => d.LastSeen, DateTime.UtcNow)
                    .Set(d => d.Platform, string.IsNullOrWhiteSpace(request.Platform) ? existing.Platform : request.Platform)
                    .Set(d => d.Model, request.Model ?? existing.Model)
                    .Set(d => d.OsVersion, request.OsVersion ?? existing.OsVersion)
                    .Set(d => d.AppVersion, request.AppVersion ?? existing.AppVersion)
                    .Set(d => d.PreferredLanguage, request.Language ?? existing.PreferredLanguage)
                    .Inc(d => d.TotalPOIsViewed, 1);

                if (request.WasPlayed)
                    update = update.Inc(d => d.TotalAudioPlayed, 1);

                await _db.Devices.UpdateOneAsync(d => d.DeviceId == request.DeviceId, update);
            }
        }

        return Ok(new { success = true, logId = log.Log_ID });
    }

    private static bool IsInsideVinhKhanhArea(decimal latitude, decimal longitude)
    {
        return latitude >= VinhKhanhLatitudeMin &&
               latitude <= VinhKhanhLatitudeMax &&
               longitude >= VinhKhanhLongitudeMin &&
               longitude <= VinhKhanhLongitudeMax;
    }

    private static bool TryResolveCoordinatesForAreaCheck(
        MobileNarrationLogRequest request,
        POI? fallbackPoi,
        out decimal latitude,
        out decimal longitude)
    {
        if (request.UserLatitude.HasValue && request.UserLongitude.HasValue)
        {
            latitude = request.UserLatitude.Value;
            longitude = request.UserLongitude.Value;
            return true;
        }

        if (fallbackPoi?.Location?.Coordinates?.Length >= 2)
        {
            latitude = (decimal)fallbackPoi.Location.Latitude;
            longitude = (decimal)fallbackPoi.Location.Longitude;
            return true;
        }

        latitude = 0;
        longitude = 0;
        return false;
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
    public string? SessionId { get; set; }
    public string? DeviceId { get; set; }
    public DateTime TriggeredAt { get; set; }
    public string TriggerType { get; set; } = string.Empty;
    public string? ActionType { get; set; }
    public int? DwellSeconds { get; set; }
    public decimal? UserLatitude { get; set; }
    public decimal? UserLongitude { get; set; }
    public bool WasPlayed { get; set; }
}

public class MobileNarrationLogRequest
{
    [Required]
    public int POI_ID { get; set; }

    [MaxLength(100)]
    public string? UserId { get; set; }

    [MaxLength(80)]
    public string? SessionId { get; set; }

    [MaxLength(120)]
    public string? DeviceId { get; set; }

    [MaxLength(30)]
    public string? Platform { get; set; }

    [MaxLength(100)]
    public string? Model { get; set; }

    [MaxLength(40)]
    public string? OsVersion { get; set; }

    [MaxLength(30)]
    public string? AppVersion { get; set; }

    [MaxLength(10)]
    public string? Language { get; set; }

    [MaxLength(40)]
    public string? TriggerType { get; set; }

    [MaxLength(40)]
    public string? ActionType { get; set; }

    public DateTime? TriggeredAt { get; set; }
    public decimal? UserLatitude { get; set; }
    public decimal? UserLongitude { get; set; }
    public int? DwellSeconds { get; set; }
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
    public long PlayCount { get; set; }
    public double MeanPlay { get; set; }
    public double TotalListenSeconds { get; set; }
}

public class PoiListenProgressRequest
{
    [Required]
    public int POI_ID { get; set; }

    [Range(0, 60 * 60 * 6)]
    public double ListenSeconds { get; set; }

    [MaxLength(120)]
    public string? SessionId { get; set; }

    [MaxLength(120)]
    public string? DeviceId { get; set; }

    [MaxLength(120)]
    public string? PlaybackSessionId { get; set; }

    [MaxLength(40)]
    public string? Source { get; set; }
}
