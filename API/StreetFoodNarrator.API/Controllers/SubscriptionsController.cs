using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using StreetFoodNarrator.API.Data;
using StreetFoodNarrator.API.Models;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;

namespace StreetFoodNarrator.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class SubscriptionsController : ControllerBase
{
    private const string PlanCode = "TourExplore";
    private const string SubscriptionSequenceName = "Device_Subscription_ID";

    private readonly MongoDbContext _db;
    private readonly MongoSequenceService _sequence;

    public SubscriptionsController(MongoDbContext db, MongoSequenceService sequence)
    {
        _db = db;
        _sequence = sequence;
    }

    [HttpGet("admin/subscriptions")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<object>> GetAdminSubscriptions(
        [FromQuery] string? status = null,
        [FromQuery] string? platform = null,
        [FromQuery] string? search = null)
    {
        var rows = await _db.DeviceSubscriptions
            .Find(Builders<DeviceSubscription>.Filter.Empty)
            .SortByDescending(x => x.InvoiceCreatedAtUtc)
            .Limit(300)
            .ToListAsync();

        if (!string.IsNullOrWhiteSpace(platform))
        {
            var normalizedPlatform = platform.Trim().ToLowerInvariant();
            rows = rows
                .Where(x => (x.Platform ?? string.Empty).ToLowerInvariant() == normalizedPlatform)
                .ToList();
        }

        var data = rows.Select(ToAdminDto).ToList();

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalizedStatus = status.Trim().ToLowerInvariant();
            data = data
                .Where(x => x.Status == normalizedStatus)
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim().ToLowerInvariant();
            data = data
                .Where(x =>
                    (x.DeviceId ?? string.Empty).ToLowerInvariant().Contains(keyword)
                    || (x.InvoiceNumber ?? string.Empty).ToLowerInvariant().Contains(keyword)
                    || (x.RecoveryCode ?? string.Empty).ToLowerInvariant().Contains(keyword)
                    || (x.TransferContent ?? string.Empty).ToLowerInvariant().Contains(keyword)
                    || (x.Platform ?? string.Empty).ToLowerInvariant().Contains(keyword)
                    || (x.Model ?? string.Empty).ToLowerInvariant().Contains(keyword)
                    || x.SubscriptionId.ToString().Contains(keyword))
                .ToList();
        }

        return Ok(new { data, total = data.Count });
    }

    /// <summary>
    /// Confirm client-side payment action and create invoice/subscription log per device.
    /// A successful confirmation grants VIP for 1 month from invoice creation time.
    /// </summary>
    [HttpPost("confirm-device-payment")]
    public async Task<ActionResult<SubscriptionStatusResponse>> ConfirmDevicePayment([FromBody] ConfirmDevicePaymentRequest request)
    {
        var deviceId = request.DeviceId?.Trim();
        if (string.IsNullOrWhiteSpace(deviceId))
            return BadRequest(new { message = "DeviceId is required." });

        var now = DateTime.UtcNow;

        var latest = await _db.DeviceSubscriptions
            .Find(s => s.DeviceId == deviceId)
            .SortByDescending(s => s.ExpiresAtUtc)
            .FirstOrDefaultAsync();

        var startsAt = latest != null && latest.ExpiresAtUtc > now
            ? latest.ExpiresAtUtc
            : now;

        var expiresAt = startsAt.AddMonths(1);
        var sequenceValue = await _sequence.GetNextAsync(SubscriptionSequenceName);
        var invoiceCreatedAt = now;
        var recoveryCode = GenerateRecoveryCode();

        var subscription = new DeviceSubscription
        {
            Subscription_ID = sequenceValue,
            DeviceId = deviceId,
            PlanCode = PlanCode,
            Status = "active",
            InvoiceNumber = $"INV-{invoiceCreatedAt:yyyyMMdd}-{sequenceValue:D6}",
            RecoveryCode = recoveryCode,
            InvoiceCreatedAtUtc = invoiceCreatedAt,
            StartsAtUtc = startsAt,
            ExpiresAtUtc = expiresAt,
            ConfirmedAtUtc = now,
            TransferContent = request.TransferContent?.Trim(),
            Platform = request.Platform?.Trim(),
            Model = request.Model?.Trim(),
            OsVersion = request.OsVersion?.Trim(),
            AppVersion = request.AppVersion?.Trim()
        };

        await _db.DeviceSubscriptions.InsertOneAsync(subscription);

        await UpsertDeviceHeartbeatAsync(deviceId, request, now);

        return Ok(BuildStatusResponse(
            isVip: true,
            deviceId: deviceId,
            invoiceNumber: subscription.InvoiceNumber,
            invoiceCreatedAtUtc: subscription.InvoiceCreatedAtUtc,
            startsAtUtc: subscription.StartsAtUtc,
            expiresAtUtc: subscription.ExpiresAtUtc,
            recoveryCode: subscription.RecoveryCode));
    }

    /// <summary>
    /// Restore VIP to current device by recovery code (demo-friendly replacement for email OTP).
    /// </summary>
    [HttpPost("restore-device-vip")]
    public async Task<ActionResult<SubscriptionStatusResponse>> RestoreDeviceVip([FromBody] RestoreDeviceVipRequest request)
    {
        var deviceId = request.DeviceId?.Trim();
        if (string.IsNullOrWhiteSpace(deviceId))
            return BadRequest(new { message = "DeviceId is required." });

        var recoveryCode = NormalizeRecoveryCode(request.RecoveryCode);
        if (string.IsNullOrWhiteSpace(recoveryCode))
            return BadRequest(new { message = "RecoveryCode is required." });

        var source = await _db.DeviceSubscriptions
            .Find(s => s.RecoveryCode == recoveryCode)
            .SortByDescending(s => s.ExpiresAtUtc)
            .FirstOrDefaultAsync();

        if (source == null)
            return NotFound(new { message = "Recovery code not found." });

        var now = DateTime.UtcNow;
        var isStillVip = string.Equals(source.Status, "active", StringComparison.OrdinalIgnoreCase)
            && source.ExpiresAtUtc > now;

        if (!isStillVip)
        {
            if (string.Equals(source.Status, "active", StringComparison.OrdinalIgnoreCase))
            {
                var expireUpdate = Builders<DeviceSubscription>.Update.Set(s => s.Status, "expired");
                await _db.DeviceSubscriptions.UpdateOneAsync(s => s.Id == source.Id, expireUpdate);
            }

            return Ok(BuildStatusResponse(
                isVip: false,
                deviceId: deviceId,
                invoiceNumber: source.InvoiceNumber,
                invoiceCreatedAtUtc: source.InvoiceCreatedAtUtc,
                startsAtUtc: source.StartsAtUtc,
                expiresAtUtc: source.ExpiresAtUtc,
                recoveryCode: source.RecoveryCode));
        }

        if (!string.Equals(source.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase))
        {
            var sequenceValue = await _sequence.GetNextAsync(SubscriptionSequenceName);
            var restored = new DeviceSubscription
            {
                Subscription_ID = sequenceValue,
                DeviceId = deviceId,
                PlanCode = source.PlanCode,
                Status = "active",
                InvoiceNumber = source.InvoiceNumber,
                RecoveryCode = source.RecoveryCode,
                InvoiceCreatedAtUtc = source.InvoiceCreatedAtUtc,
                StartsAtUtc = now,
                ExpiresAtUtc = source.ExpiresAtUtc,
                ConfirmedAtUtc = now,
                RestoredAtUtc = now,
                RestoredFromDeviceId = source.DeviceId,
                TransferContent = source.TransferContent,
                Platform = request.Platform?.Trim(),
                Model = request.Model?.Trim(),
                OsVersion = request.OsVersion?.Trim(),
                AppVersion = request.AppVersion?.Trim()
            };

            await _db.DeviceSubscriptions.InsertOneAsync(restored);
        }

        var heartbeatRequest = new ConfirmDevicePaymentRequest
        {
            DeviceId = deviceId,
            Platform = request.Platform,
            Model = request.Model,
            OsVersion = request.OsVersion,
            AppVersion = request.AppVersion
        };

        await UpsertDeviceHeartbeatAsync(deviceId, heartbeatRequest, now);

        return Ok(BuildStatusResponse(
            isVip: true,
            deviceId: deviceId,
            invoiceNumber: source.InvoiceNumber,
            invoiceCreatedAtUtc: source.InvoiceCreatedAtUtc,
            startsAtUtc: source.StartsAtUtc,
            expiresAtUtc: source.ExpiresAtUtc,
            recoveryCode: source.RecoveryCode));
    }

    /// <summary>
    /// Test-only endpoint: simulate VIP expiry as invoice-created-time + 3 minutes.
    /// This helps test short expiry flows without using the current expiry as a base.
    /// </summary>
    [HttpPost("simulate-renew-3-minutes")]
    public async Task<ActionResult<SubscriptionStatusResponse>> SimulateRenewThreeMinutes([FromBody] SimulateRenewRequest request)
    {
        var deviceId = request.DeviceId?.Trim();
        if (string.IsNullOrWhiteSpace(deviceId))
            return BadRequest(new { message = "DeviceId is required." });

        var latest = await _db.DeviceSubscriptions
            .Find(s => s.DeviceId == deviceId)
            .SortByDescending(s => s.ExpiresAtUtc)
            .FirstOrDefaultAsync();

        if (latest == null)
            return NotFound(new { message = "No subscription found for this device." });

        var now = DateTime.UtcNow;
        var simulatedExpiresAt = latest.InvoiceCreatedAtUtc.AddMinutes(3);
        var simulatedIsVip = simulatedExpiresAt > now;
        var nextStatus = simulatedIsVip ? "active" : "expired";

        var update = Builders<DeviceSubscription>.Update
            .Set(s => s.ExpiresAtUtc, simulatedExpiresAt)
            .Set(s => s.Status, nextStatus)
            .Set(s => s.ConfirmedAtUtc, now)
            .Set(s => s.Platform, request.Platform ?? latest.Platform)
            .Set(s => s.Model, request.Model ?? latest.Model)
            .Set(s => s.OsVersion, request.OsVersion ?? latest.OsVersion)
            .Set(s => s.AppVersion, request.AppVersion ?? latest.AppVersion);

        await _db.DeviceSubscriptions.UpdateOneAsync(s => s.Id == latest.Id, update);

        var heartbeatRequest = new ConfirmDevicePaymentRequest
        {
            DeviceId = deviceId,
            Platform = request.Platform,
            Model = request.Model,
            OsVersion = request.OsVersion,
            AppVersion = request.AppVersion
        };

        await UpsertDeviceHeartbeatAsync(deviceId, heartbeatRequest, now);

        return Ok(BuildStatusResponse(
            isVip: simulatedIsVip,
            deviceId: deviceId,
            invoiceNumber: latest.InvoiceNumber,
            invoiceCreatedAtUtc: latest.InvoiceCreatedAtUtc,
            startsAtUtc: latest.StartsAtUtc,
            expiresAtUtc: simulatedExpiresAt,
            recoveryCode: latest.RecoveryCode));
    }

    /// <summary>
    /// Returns current VIP status for a device. If expired, status is reset automatically.
    /// </summary>
    [HttpGet("status")]
    public async Task<ActionResult<SubscriptionStatusResponse>> GetStatus([FromQuery] string deviceId)
    {
        var normalizedDeviceId = deviceId?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedDeviceId))
            return BadRequest(new { message = "DeviceId is required." });

        var latest = await _db.DeviceSubscriptions
            .Find(s => s.DeviceId == normalizedDeviceId)
            .SortByDescending(s => s.ExpiresAtUtc)
            .FirstOrDefaultAsync();

        if (latest == null)
        {
            return Ok(BuildStatusResponse(
                isVip: false,
                deviceId: normalizedDeviceId,
                invoiceNumber: null,
                invoiceCreatedAtUtc: null,
                startsAtUtc: null,
                expiresAtUtc: null,
                recoveryCode: null));
        }

        var now = DateTime.UtcNow;
        var isVip = string.Equals(latest.Status, "active", StringComparison.OrdinalIgnoreCase)
            && latest.ExpiresAtUtc > now;

        if (!isVip && string.Equals(latest.Status, "active", StringComparison.OrdinalIgnoreCase))
        {
            var update = Builders<DeviceSubscription>.Update.Set(s => s.Status, "expired");
            await _db.DeviceSubscriptions.UpdateOneAsync(s => s.Id == latest.Id, update);
            latest.Status = "expired";
        }

        return Ok(BuildStatusResponse(
            isVip: isVip,
            deviceId: normalizedDeviceId,
            invoiceNumber: latest.InvoiceNumber,
            invoiceCreatedAtUtc: latest.InvoiceCreatedAtUtc,
            startsAtUtc: latest.StartsAtUtc,
            expiresAtUtc: latest.ExpiresAtUtc,
            recoveryCode: latest.RecoveryCode));
    }

    private async Task UpsertDeviceHeartbeatAsync(string deviceId, ConfirmDevicePaymentRequest request, DateTime now)
    {
        var existing = await _db.Devices.Find(d => d.DeviceId == deviceId).FirstOrDefaultAsync();
        if (existing == null)
        {
            var deviceSequence = await _sequence.GetNextAsync("Device_ID");
            var newDevice = new DeviceInfo
            {
                Device_ID = deviceSequence,
                DeviceId = deviceId,
                Platform = string.IsNullOrWhiteSpace(request.Platform) ? "Unknown" : request.Platform.Trim(),
                Model = request.Model?.Trim(),
                OsVersion = request.OsVersion?.Trim(),
                AppVersion = request.AppVersion?.Trim(),
                FirstSeen = now,
                LastSeen = now,
                TotalSessions = 1
            };

            await _db.Devices.InsertOneAsync(newDevice);
            return;
        }

        var update = Builders<DeviceInfo>.Update
            .Set(d => d.LastSeen, now)
            .Set(d => d.Platform, request.Platform ?? existing.Platform)
            .Set(d => d.Model, request.Model ?? existing.Model)
            .Set(d => d.OsVersion, request.OsVersion ?? existing.OsVersion)
            .Set(d => d.AppVersion, request.AppVersion ?? existing.AppVersion);

        await _db.Devices.UpdateOneAsync(d => d.DeviceId == deviceId, update);
    }

    private static SubscriptionStatusResponse BuildStatusResponse(
        bool isVip,
        string deviceId,
        string? invoiceNumber,
        DateTime? invoiceCreatedAtUtc,
        DateTime? startsAtUtc,
        DateTime? expiresAtUtc,
        string? recoveryCode)
    {
        var now = DateTime.UtcNow;
        int remainingDays = 0;
        if (isVip && expiresAtUtc.HasValue)
        {
            remainingDays = Math.Max(0, (int)Math.Ceiling((expiresAtUtc.Value - now).TotalDays));
        }

        return new SubscriptionStatusResponse
        {
            DeviceId = deviceId,
            IsVip = isVip,
            PlanCode = PlanCode,
            InvoiceNumber = invoiceNumber,
            RecoveryCode = recoveryCode,
            InvoiceCreatedAtUtc = invoiceCreatedAtUtc,
            StartsAtUtc = startsAtUtc,
            ExpiresAtUtc = expiresAtUtc,
            RemainingDays = remainingDays
        };
    }

    private static string NormalizeRecoveryCode(string? rawCode)
    {
        if (string.IsNullOrWhiteSpace(rawCode))
            return string.Empty;

        return rawCode.Trim().ToUpperInvariant();
    }

    private static string GenerateRecoveryCode()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        Span<byte> bytes = stackalloc byte[10];
        RandomNumberGenerator.Fill(bytes);

        Span<char> token = stackalloc char[10];
        for (var i = 0; i < token.Length; i++)
            token[i] = alphabet[bytes[i] % alphabet.Length];

        var tokenText = new string(token);
        return $"VIP-{tokenText[..5]}-{tokenText[5..]}";
    }

    private static AdminDeviceSubscriptionDto ToAdminDto(DeviceSubscription row)
    {
        var now = DateTime.UtcNow;
        var rawStatus = string.IsNullOrWhiteSpace(row.Status)
            ? "unknown"
            : row.Status.Trim().ToLowerInvariant();
        var isVipNow = rawStatus == "active" && row.ExpiresAtUtc > now;
        var status = rawStatus == "active" && !isVipNow
            ? "expired"
            : rawStatus;
        var remainingDays = isVipNow
            ? Math.Max(0, (int)Math.Ceiling((row.ExpiresAtUtc - now).TotalDays))
            : 0;

        return new AdminDeviceSubscriptionDto
        {
            SubscriptionId = row.Subscription_ID,
            DeviceId = row.DeviceId,
            PlanCode = row.PlanCode,
            Status = status,
            InvoiceNumber = row.InvoiceNumber,
            RecoveryCode = row.RecoveryCode,
            InvoiceCreatedAtUtc = row.InvoiceCreatedAtUtc,
            StartsAtUtc = row.StartsAtUtc,
            ExpiresAtUtc = row.ExpiresAtUtc,
            ConfirmedAtUtc = row.ConfirmedAtUtc,
            RestoredAtUtc = row.RestoredAtUtc,
            RestoredFromDeviceId = row.RestoredFromDeviceId,
            TransferContent = row.TransferContent,
            Platform = row.Platform,
            Model = row.Model,
            OsVersion = row.OsVersion,
            AppVersion = row.AppVersion,
            IsVipNow = isVipNow,
            RemainingDays = remainingDays
        };
    }
}

public class ConfirmDevicePaymentRequest
{
    [Required, MaxLength(120)]
    public string DeviceId { get; set; } = string.Empty;

    [MaxLength(160)]
    public string? TransferContent { get; set; }

    [MaxLength(40)]
    public string? Platform { get; set; }

    [MaxLength(120)]
    public string? Model { get; set; }

    [MaxLength(40)]
    public string? OsVersion { get; set; }

    [MaxLength(30)]
    public string? AppVersion { get; set; }
}

public class SubscriptionStatusResponse
{
    public string DeviceId { get; set; } = string.Empty;
    public bool IsVip { get; set; }
    public string PlanCode { get; set; } = "TourExplore";
    public string? InvoiceNumber { get; set; }
    public string? RecoveryCode { get; set; }
    public DateTime? InvoiceCreatedAtUtc { get; set; }
    public DateTime? StartsAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public int RemainingDays { get; set; }
}

public class RestoreDeviceVipRequest
{
    [Required, MaxLength(120)]
    public string DeviceId { get; set; } = string.Empty;

    [Required, MaxLength(32)]
    public string RecoveryCode { get; set; } = string.Empty;

    [MaxLength(40)]
    public string? Platform { get; set; }

    [MaxLength(120)]
    public string? Model { get; set; }

    [MaxLength(40)]
    public string? OsVersion { get; set; }

    [MaxLength(30)]
    public string? AppVersion { get; set; }
}

public class SimulateRenewRequest
{
    [Required, MaxLength(120)]
    public string DeviceId { get; set; } = string.Empty;

    [MaxLength(40)]
    public string? Platform { get; set; }

    [MaxLength(120)]
    public string? Model { get; set; }

    [MaxLength(40)]
    public string? OsVersion { get; set; }

    [MaxLength(30)]
    public string? AppVersion { get; set; }
}

public class AdminDeviceSubscriptionDto
{
    public int SubscriptionId { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public string PlanCode { get; set; } = "TourExplore";
    public string Status { get; set; } = "unknown";
    public string InvoiceNumber { get; set; } = string.Empty;
    public string RecoveryCode { get; set; } = string.Empty;
    public DateTime InvoiceCreatedAtUtc { get; set; }
    public DateTime StartsAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime ConfirmedAtUtc { get; set; }
    public DateTime? RestoredAtUtc { get; set; }
    public string? RestoredFromDeviceId { get; set; }
    public string? TransferContent { get; set; }
    public string? Platform { get; set; }
    public string? Model { get; set; }
    public string? OsVersion { get; set; }
    public string? AppVersion { get; set; }
    public bool IsVipNow { get; set; }
    public int RemainingDays { get; set; }
}