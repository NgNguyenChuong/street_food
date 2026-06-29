using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using StreetFoodNarrator.API.Data;
using StreetFoodNarrator.API.Models;

namespace StreetFoodNarrator.API.Controllers;

[Route("api/device-activity")]
[ApiController]
public class DeviceActivityController : ControllerBase
{
    private readonly MongoDbContext _db;
    private readonly MongoSequenceService _sequence;

    /// <summary>
    /// Devices with no heartbeat for this duration are considered offline.
    /// 5 minutes gives comfortable buffer over the 60s heartbeat interval.
    /// </summary>
    private static readonly TimeSpan OnlineThreshold = TimeSpan.FromMinutes(5);

    public DeviceActivityController(MongoDbContext db, MongoSequenceService sequence)
    {
        _db = db;
        _sequence = sequence;
    }

    // ── Heartbeat (tourist / vendor) ──────────────────────────────────
    /// <summary>
    /// POST /api/device-activity/heartbeat
    /// Called periodically (every 60s) by web app, mobile app, and vendor dashboard.
    /// Upserts device record and marks it online.
    /// </summary>
    [HttpPost("heartbeat")]
    [AllowAnonymous]
    public async Task<IActionResult> Heartbeat([FromBody] HeartbeatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DeviceId))
            return BadRequest(new { message = "DeviceId is required." });

        var deviceId = request.DeviceId.Trim();
        var now = DateTime.UtcNow;
        var userRole = string.IsNullOrWhiteSpace(request.UserRole) ? "tourist" : request.UserRole.Trim().ToLowerInvariant();
        var clientType = string.IsNullOrWhiteSpace(request.ClientType) ? "web" : request.ClientType.Trim().ToLowerInvariant();

        var filter = Builders<DeviceInfo>.Filter.Eq(d => d.DeviceId, deviceId);

        var update = Builders<DeviceInfo>.Update
            .Set(d => d.IsOnline, true)
            .Set(d => d.LastHeartbeatAt, now)
            .Set(d => d.LastSeen, now)
            .Set(d => d.UserRole, userRole)
            .Set(d => d.ClientType, clientType);

        if (!string.IsNullOrWhiteSpace(request.Platform))
            update = update.Set(d => d.Platform, request.Platform);

        var result = await _db.Devices.UpdateOneAsync(filter, update);

        if (result.MatchedCount == 0)
        {
            // Device not registered yet — create it
            var seqId = await _sequence.GetNextAsync("Device_ID");
            var device = new DeviceInfo
            {
                Device_ID = seqId,
                DeviceId = deviceId,
                Platform = request.Platform ?? "Unknown",
                IsOnline = true,
                LastHeartbeatAt = now,
                LastSeen = now,
                FirstSeen = now,
                TotalSessions = 1,
                UserRole = userRole,
                ClientType = clientType
            };
            await _db.Devices.InsertOneAsync(device);
        }

        return Ok(new { success = true });
    }

    // ── Disconnect ────────────────────────────────────────────────────
    /// <summary>
    /// POST /api/device-activity/disconnect
    /// Called when user closes the app / navigates away (beforeunload, app pause).
    /// </summary>
    [HttpPost("disconnect")]
    [AllowAnonymous]
    public async Task<IActionResult> Disconnect([FromBody] DisconnectRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DeviceId))
            return BadRequest(new { message = "DeviceId is required." });

        var filter = Builders<DeviceInfo>.Filter.Eq(d => d.DeviceId, request.DeviceId.Trim());
        var update = Builders<DeviceInfo>.Update
            .Set(d => d.IsOnline, false)
            .Set(d => d.LastSeen, DateTime.UtcNow);

        await _db.Devices.UpdateOneAsync(filter, update);
        return Ok(new { success = true });
    }

    // ── Admin: online devices summary ─────────────────────────────────
    /// <summary>
    /// GET /api/device-activity/online-summary
    /// Returns counts of online devices grouped by role and client type.
    /// </summary>
    [HttpGet("online-summary")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> OnlineSummary()
    {
        var threshold = DateTime.UtcNow.Subtract(OnlineThreshold);
        // Exclude only admin accounts; vendor dashboard sessions are included
        var adminRegexSummary = new MongoDB.Bson.BsonRegularExpression("^admin$", "i");
        var excludeAdmin = Builders<DeviceInfo>.Filter.Not(Builders<DeviceInfo>.Filter.Regex(d => d.UserRole, adminRegexSummary));
        var onlineFilter = Builders<DeviceInfo>.Filter.Eq(d => d.IsOnline, true)
            & Builders<DeviceInfo>.Filter.Gte(d => d.LastHeartbeatAt, threshold)
            & excludeAdmin;

        var onlineDevices = await _db.Devices.Find(onlineFilter).ToListAsync();

        var totalOnline = onlineDevices.Count;
        var touristOnline = onlineDevices.Count(d => d.UserRole == "tourist");
        var vendorOnline = onlineDevices.Count(d => d.UserRole == "vendor");
        var webOnline = onlineDevices.Count(d => d.ClientType == "web");
        var androidOnline = onlineDevices.Count(d => d.ClientType == "android");
        var iosOnline = onlineDevices.Count(d => d.ClientType == "ios");
        var dashOnline = onlineDevices.Count(d => d.ClientType == "dashboard");

        return Ok(new
        {
            totalOnline,
            touristOnline,
            vendorOnline,
            byClientType = new { web = webOnline, android = androidOnline, ios = iosOnline, dashboard = dashOnline }
        });
    }

    // ── Admin: online devices list ────────────────────────────────────
    /// <summary>
    /// GET /api/device-activity/online
    /// Returns list of currently online devices with details.
    /// </summary>
    [HttpGet("online")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetOnlineDevices(
        [FromQuery] string? role = null,
        [FromQuery] string? clientType = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var threshold = DateTime.UtcNow.Subtract(OnlineThreshold);
        var filter = Builders<DeviceInfo>.Filter.Eq(d => d.IsOnline, true)
            & Builders<DeviceInfo>.Filter.Gte(d => d.LastHeartbeatAt, threshold);

        if (!string.IsNullOrWhiteSpace(role))
            filter &= Builders<DeviceInfo>.Filter.Eq(d => d.UserRole, role.Trim().ToLowerInvariant());

        if (!string.IsNullOrWhiteSpace(clientType))
            filter &= Builders<DeviceInfo>.Filter.Eq(d => d.ClientType, clientType.Trim().ToLowerInvariant());

        var total = await _db.Devices.CountDocumentsAsync(filter);
        var skip = (page - 1) * pageSize;

        var devices = await _db.Devices
            .Find(filter)
            .Sort(Builders<DeviceInfo>.Sort.Descending(d => d.LastHeartbeatAt))
            .Skip(skip)
            .Limit(pageSize)
            .ToListAsync();

        var data = devices.Select(d => new
        {
            d.DeviceId,
            d.Device_ID,
            d.Platform,
            d.Model,
            d.UserRole,
            d.ClientType,
            d.PreferredLanguage,
            d.LastHeartbeatAt,
            d.LastSeen,
            d.FirstSeen,
            d.TotalSessions
        }).ToList();

        return Ok(new { data, total = (int)total, page, pageSize });
    }

    // ── Admin: all devices with online status ─────────────────────────
    /// <summary>
    /// GET /api/device-activity/all
    /// Returns all devices with their online/offline status.
    /// </summary>
    [HttpGet("all")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAllDevices(
        [FromQuery] string? role = null,
        [FromQuery] string? status = null,
        [FromQuery] string? search = null,
        [FromQuery] string? dateFrom = null,
        [FromQuery] string? dateTo = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var threshold = DateTime.UtcNow.Subtract(OnlineThreshold);
        // Exclude only admin role; vendor dashboard sessions are intentionally included
        var adminRegex = new MongoDB.Bson.BsonRegularExpression("^admin$", "i");
        var filter = Builders<DeviceInfo>.Filter.Not(Builders<DeviceInfo>.Filter.Regex(d => d.UserRole, adminRegex));

        if (!string.IsNullOrWhiteSpace(role))
            filter &= Builders<DeviceInfo>.Filter.Eq(d => d.UserRole, role.Trim().ToLowerInvariant());

        if (!string.IsNullOrWhiteSpace(search))
        {
            var regex = new MongoDB.Bson.BsonRegularExpression(search.Trim(), "i");
            filter &= Builders<DeviceInfo>.Filter.Or(
                Builders<DeviceInfo>.Filter.Regex(d => d.DeviceId, regex),
                Builders<DeviceInfo>.Filter.Regex(d => d.Model, regex),
                Builders<DeviceInfo>.Filter.Regex(d => d.Platform, regex));
        }

        if (!string.IsNullOrWhiteSpace(dateFrom) && DateTime.TryParse(dateFrom, out var dtFrom))
            filter &= Builders<DeviceInfo>.Filter.Gte(d => d.FirstSeen, dtFrom.ToUniversalTime());

        if (!string.IsNullOrWhiteSpace(dateTo) && DateTime.TryParse(dateTo, out var dtTo))
            filter &= Builders<DeviceInfo>.Filter.Lte(d => d.FirstSeen, dtTo.ToUniversalTime().AddDays(1));

        // Post-filter for status — need to compute isOnline from heartbeat
        // We'll do it after fetch since MongoDB can't evaluate runtime threshold easily
        var total = await _db.Devices.CountDocumentsAsync(filter);

        var allDevices = await _db.Devices
            .Find(filter)
            .Sort(Builders<DeviceInfo>.Sort.Descending(d => d.LastSeen))
            .ToListAsync();

        // Compute real online status
        foreach (var d in allDevices)
        {
            d.IsOnline = d.IsOnline && d.LastHeartbeatAt.HasValue && d.LastHeartbeatAt.Value >= threshold;
        }

        if (status == "online")
            allDevices = allDevices.Where(d => d.IsOnline).ToList();
        else if (status == "offline")
            allDevices = allDevices.Where(d => !d.IsOnline).ToList();

        var filteredTotal = allDevices.Count;
        var paged = allDevices.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        // Lookup VIP subscriptions for paged devices
        var pagedDeviceIds = paged.Select(d => d.DeviceId).ToList();
        var nowUtc = DateTime.UtcNow;
        var vipFilter = Builders<DeviceSubscription>.Filter.In(s => s.DeviceId, pagedDeviceIds)
            & Builders<DeviceSubscription>.Filter.Eq(s => s.Status, "active")
            & Builders<DeviceSubscription>.Filter.Gt(s => s.ExpiresAtUtc, nowUtc);
        var vipSubs = await _db.DeviceSubscriptions.Find(vipFilter).ToListAsync();
        var vipDeviceIds = vipSubs.Select(s => s.DeviceId).ToHashSet();

        var data = paged.Select(d => new
        {
            d.DeviceId,
            d.Device_ID,
            d.Platform,
            d.Model,
            d.UserRole,
            d.ClientType,
            d.IsOnline,
            d.PreferredLanguage,
            d.LastHeartbeatAt,
            d.LastSeen,
            d.FirstSeen,
            d.TotalSessions,
            d.TotalPOIsViewed,
            d.TotalAudioPlayed,
            IsVip = vipDeviceIds.Contains(d.DeviceId)
        }).ToList();

        return Ok(new { data, total = filteredTotal, page, pageSize });
    }
}

// ── Request DTOs ──────────────────────────────────────────────────────
public class HeartbeatRequest
{
    public string DeviceId { get; set; } = string.Empty;
    public string? Platform { get; set; }
    public string? UserRole { get; set; }   // "tourist" | "vendor"
    public string? ClientType { get; set; } // "web" | "android" | "ios" | "dashboard"
}

public class DisconnectRequest
{
    public string DeviceId { get; set; } = string.Empty;
}
