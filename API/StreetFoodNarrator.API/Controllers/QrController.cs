using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using StreetFoodNarrator.API.Data;
using StreetFoodNarrator.API.Models;
using System.Globalization;
using System.Net;

namespace StreetFoodNarrator.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize(Roles = "Admin")]
public class QrController : ControllerBase
{
    private const string CampaignId = "vinh-khanh-ben-xe-gate";
    private const int ProductionCycleDays = 5;
    private const int TestCycleMinutes = 3;

    private static readonly DateTimeOffset CycleAnchorUtc = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly MongoDbContext _db;
    private readonly string? _publicBaseUrl;

    public QrController(MongoDbContext db, IConfiguration configuration)
    {
        _db = db;
        _publicBaseUrl = ResolvePublicBaseUrl(configuration);
    }

    [HttpGet("admin/status")]
    public async Task<ActionResult<object>> GetAdminStatus()
    {
        var state = await GetOrCreateStateAsync();
        var status = BuildStatus(state, DateTimeOffset.UtcNow);
        return Ok(status);
    }

    [HttpPost("admin/test-rotate")]
    public async Task<ActionResult<object>> StartThreeMinuteTestRotation()
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var testExpiresAtUtc = nowUtc.AddMinutes(TestCycleMinutes);
        var testCode = $"test-{nowUtc.ToUnixTimeSeconds()}";

        var update = Builders<QrCampaignState>.Update
            .Set(x => x.TestCode, testCode)
            .Set(x => x.TestExpiresAtUtc, testExpiresAtUtc.UtcDateTime)
            .Set(x => x.UpdatedAtUtc, nowUtc.UtcDateTime);

        await _db.QrCampaignStates.UpdateOneAsync(
            x => x.Id == CampaignId,
            update,
            new UpdateOptions { IsUpsert = true });

        var state = await GetOrCreateStateAsync();
        var status = BuildStatus(state, nowUtc);
        return Ok(status);
    }

    private async Task<QrCampaignState> GetOrCreateStateAsync()
    {
        var state = await _db.QrCampaignStates
            .Find(x => x.Id == CampaignId)
            .FirstOrDefaultAsync();

        if (state != null)
            return state;

        state = new QrCampaignState
        {
            Id = CampaignId,
            UpdatedAtUtc = DateTime.UtcNow
        };

        await _db.QrCampaignStates.ReplaceOneAsync(
            x => x.Id == CampaignId,
            state,
            new ReplaceOptions { IsUpsert = true });

        return state;
    }

    private object BuildStatus(QrCampaignState state, DateTimeOffset nowUtc)
    {
        var testExpiresAtUtc = state.TestExpiresAtUtc.HasValue
            ? new DateTimeOffset(DateTime.SpecifyKind(state.TestExpiresAtUtc.Value, DateTimeKind.Utc))
            : (DateTimeOffset?)null;

        var hasActiveTest = testExpiresAtUtc.HasValue && testExpiresAtUtc.Value > nowUtc && !string.IsNullOrWhiteSpace(state.TestCode);

        var mode = hasActiveTest ? "test" : "normal";
        var expiresAtUtc = hasActiveTest
            ? testExpiresAtUtc!.Value
            : GetCurrentCycleEnd(nowUtc);

        var cycleCode = hasActiveTest
            ? state.TestCode!
            : GetProductionCycleCode(nowUtc);

        var remaining = expiresAtUtc - nowUtc;
        if (remaining < TimeSpan.Zero)
            remaining = TimeSpan.Zero;

        var remainingSeconds = (int)Math.Max(0, Math.Floor(remaining.TotalSeconds));
        var remainingDays = mode == "normal"
            ? Math.Max(0, (int)Math.Ceiling(remaining.TotalDays))
            : 0;

        var expUnix = expiresAtUtc.ToUnixTimeSeconds();
        var modeQuery = mode == "test" ? "&mode=test" : string.Empty;
        var apiBaseForMobile = BuildAbsoluteUrl("/");
        var apiQuery = string.IsNullOrWhiteSpace(apiBaseForMobile)
            ? string.Empty
            : $"&api={Uri.EscapeDataString(apiBaseForMobile)}";
        var qrRelativeUrl = $"/qr/main?entry=vinh-khanh-ben-xe&cycle={Uri.EscapeDataString(cycleCode)}&exp={expUnix}{modeQuery}{apiQuery}";
        var qrAbsoluteUrl = BuildAbsoluteUrl(qrRelativeUrl);

        return new
        {
            campaign = CampaignId,
            mode,
            cycleCode,
            expiresAtUtc = expiresAtUtc.UtcDateTime,
            remainingSeconds,
            remainingDays,
            remainingHuman = FormatRemaining(remaining, mode),
            qrRelativeUrl,
            qrAbsoluteUrl,
            scanBehavior = new
            {
                openAppIfInstalled = true,
                fallbackToDownloadPageIfMissing = true,
                showExpiredWhenOutdated = true
            }
        };
    }

    private string BuildAbsoluteUrl(string pathAndQuery)
    {
        if (!string.IsNullOrWhiteSpace(_publicBaseUrl))
            return $"{_publicBaseUrl}{pathAndQuery}";

        var request = HttpContext.Request;

        // If dashboard is opened on localhost but request came through a proxy/tunnel
        // (e.g. ngrok), prefer forwarded host so scanned QR works on phones.
        if (IsLoopbackHost(request.Host.Host))
        {
            var forwardedHost = request.Headers["X-Forwarded-Host"].ToString();
            var forwardedProto = request.Headers["X-Forwarded-Proto"].ToString();
            if (!string.IsNullOrWhiteSpace(forwardedHost))
            {
                var firstForwardedHost = forwardedHost.Split(',')[0].Trim();
                var firstForwardedProto = string.IsNullOrWhiteSpace(forwardedProto)
                    ? request.Scheme
                    : forwardedProto.Split(',')[0].Trim();

                if (!string.IsNullOrWhiteSpace(firstForwardedHost))
                    return $"{firstForwardedProto}://{firstForwardedHost}{pathAndQuery}";
            }
        }

        return $"{request.Scheme}://{request.Host}{pathAndQuery}";
    }

    private static string? ResolvePublicBaseUrl(IConfiguration configuration)
    {
        var configured = configuration["QrCampaign:PublicBaseUrl"]
            ?? configuration["AppDownload:PublicBaseUrl"];

        if (string.IsNullOrWhiteSpace(configured))
            return null;

        var value = configured.Trim().TrimEnd('/');
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
            return null;

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return value;
    }

    private static bool IsLoopbackHost(string host)
    {
        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
            return true;

        if (IPAddress.TryParse(host, out var ip))
            return IPAddress.IsLoopback(ip);

        return false;
    }

    private static DateTimeOffset GetCurrentCycleEnd(DateTimeOffset nowUtc)
    {
        var cycle = TimeSpan.FromDays(ProductionCycleDays);
        var elapsed = nowUtc - CycleAnchorUtc;
        var index = Math.Floor(elapsed.TotalSeconds / cycle.TotalSeconds);
        if (index < 0)
            index = 0;

        var cycleStart = CycleAnchorUtc.AddSeconds(index * cycle.TotalSeconds);
        return cycleStart.Add(cycle);
    }

    private static string GetProductionCycleCode(DateTimeOffset nowUtc)
    {
        var cycle = TimeSpan.FromDays(ProductionCycleDays);
        var elapsed = nowUtc - CycleAnchorUtc;
        var index = Math.Floor(elapsed.TotalSeconds / cycle.TotalSeconds);
        if (index < 0)
            index = 0;

        return $"prod-{index.ToString(CultureInfo.InvariantCulture)}";
    }

    private static string FormatRemaining(TimeSpan remaining, string mode)
    {
        if (mode == "test")
        {
            var minutes = Math.Max(0, (int)Math.Ceiling(remaining.TotalMinutes));
            var seconds = Math.Max(0, (int)Math.Ceiling(remaining.TotalSeconds));

            if (minutes >= 1)
                return $"{minutes} phut";

            return $"{seconds} giay";
        }

        var days = Math.Max(0, (int)Math.Ceiling(remaining.TotalDays));
        return $"{days} ngay";
    }
}
