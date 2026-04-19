using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using StreetFoodNarrator.API.Data;
using StreetFoodNarrator.API.Models;
using System.Globalization;
using System.Security.Claims;

namespace StreetFoodNarrator.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class PaymentsController : ControllerBase
{
    private const decimal PremiumOneYearPriceVnd = 1990000m;
    private const string AppNotificationSequenceName = "app_notification_id";
    private const string AppNotificationCollectionName = "app_notifications";

    private readonly MongoDbContext _db;
    private readonly MongoSequenceService _sequence;

    public PaymentsController(MongoDbContext db, MongoSequenceService sequence)
    {
        _db = db;
        _sequence = sequence;
    }

    [HttpPost("simulate-premium")]
    [Authorize(Roles = "Vendor")]
    public async Task<ActionResult<object>> SimulatePremiumPayment([FromBody] SimulatePremiumPaymentRequest? request)
    {
        var vendor = await ResolveCurrentVendorAsync();
        if (vendor == null)
            return Forbid();

        var activePremium = await GetActivePremiumAsync(vendor);
        if (activePremium.IsActive)
        {
            return Conflict(new
            {
                message = "Bạn đã có gói premium đang hoạt động, không thể đăng ký thêm.",
                expiresAt = activePremium.ExpiresAt
            });
        }

        if (!string.Equals(vendor.VerificationStatus, "approved", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Vendor phải được duyệt trước khi đăng ký gói premium." });
        }

        var submissionId = await _sequence.GetNextAsync("submission_id");
        var now = DateTime.UtcNow;
        var expiresAt = now.AddYears(1);
        var transactionRef = $"MOCK-{DateTime.UtcNow:yyyyMMddHHmmss}-{submissionId}";

        var submission = new ServiceSubmission
        {
            SubmissionId = submissionId,
            VendorId = vendor.VendorId,
            VendorUserId = vendor.UserId,
            PlanCode = "premium_1y",
            PlanName = "Premium 1 năm",
            DurationMonths = 12,
            AmountVnd = PremiumOneYearPriceVnd,
            Currency = "VND",
            PaymentMethod = string.IsNullOrWhiteSpace(request?.PaymentMethod)
                ? "mock_bank_transfer"
                : request.PaymentMethod!.Trim(),
            TransactionRef = transactionRef,
            Status = SubmissionStatuses.Approved,
            RequestedAt = now,
            PaidAt = now,
            ExpiresAt = expiresAt,
            ReviewedAt = now,
            ReviewedBy = "system-auto",
            ReviewNote = "Auto approved on successful payment simulation.",
            CreatedAt = now,
            UpdatedAt = now
        };

        await _db.ServiceSubmissions.InsertOneAsync(submission);

        var vendorUpdate = Builders<VendorProfile>.Update
            .Set(v => v.ServicePlan, "premium")
            .Set(v => v.PremiumExpiresAt, expiresAt)
            .Set(v => v.UpdatedAt, now);
        await _db.VendorProfiles.UpdateOneAsync(v => v.VendorId == vendor.VendorId, vendorUpdate);

        return Ok(new
        {
            message = "Thanh toán thành công. Gói premium đã được kích hoạt ngay.",
            submission = ToDto(submission, vendor.BusinessName)
        });
    }

    [HttpGet("me")]
    [Authorize(Roles = "Vendor")]
    public async Task<ActionResult<object>> GetMySubmissions(
        [FromQuery] string? status = null,
        [FromQuery] string? search = null,
        [FromQuery] string? fromDate = null,
        [FromQuery] string? toDate = null)
    {
        var vendor = await ResolveCurrentVendorAsync();
        if (vendor == null)
            return Forbid();

        var filter = Builders<ServiceSubmission>.Filter.Eq(x => x.VendorId, vendor.VendorId);

        if (!string.IsNullOrWhiteSpace(status))
            filter &= Builders<ServiceSubmission>.Filter.Eq(x => x.Status, status.Trim().ToLowerInvariant());

        if (TryParseDateBoundary(fromDate, endOfDay: false, out var fromUtc))
            filter &= Builders<ServiceSubmission>.Filter.Gte(x => x.RequestedAt, fromUtc);

        if (TryParseDateBoundary(toDate, endOfDay: true, out var toUtc))
            filter &= Builders<ServiceSubmission>.Filter.Lte(x => x.RequestedAt, toUtc);

        var rows = await _db.ServiceSubmissions
            .Find(filter)
            .SortByDescending(x => x.RequestedAt)
            .Limit(50)
            .ToListAsync();

        var data = rows.Select(x => ToDto(x, vendor.BusinessName)).ToList();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim().ToLowerInvariant();
            data = data.Where(x =>
                    (x.BusinessName ?? string.Empty).ToLowerInvariant().Contains(keyword) ||
                    (x.TransactionRef ?? string.Empty).ToLowerInvariant().Contains(keyword) ||
                    (x.PlanName ?? string.Empty).ToLowerInvariant().Contains(keyword) ||
                    (x.PaymentMethod ?? string.Empty).ToLowerInvariant().Contains(keyword) ||
                    (x.ReviewNote ?? string.Empty).ToLowerInvariant().Contains(keyword) ||
                    x.SubmissionId.ToString().Contains(keyword))
                .ToList();
        }

        return Ok(new { data, total = data.Count });
    }

    [HttpPost("simulate-premium-expiring")]
    [Authorize(Roles = "Vendor")]
    public async Task<ActionResult<object>> SimulatePremiumExpiring([FromBody] SimulatePremiumExpiringRequest? request)
    {
        var vendor = await ResolveCurrentVendorAsync();
        if (vendor == null)
            return Forbid();

        var activePremium = await GetActivePremiumAsync(vendor);
        if (activePremium.IsActive)
        {
            return Conflict(new
            {
                message = "Bạn đã có gói premium đang hoạt động, không thể đăng ký thêm.",
                expiresAt = activePremium.ExpiresAt
            });
        }

        if (!string.Equals(vendor.VerificationStatus, "approved", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Vendor phải được duyệt trước khi đăng ký gói premium." });
        }

        var remainingMinutes = Math.Clamp(request?.RemainingMinutes ?? 3, 1, 30);
        var now = DateTime.UtcNow;
        var paidAt = now.AddYears(-1).AddMinutes(remainingMinutes);
        var expiresAt = paidAt.AddYears(1);

        var submissionId = await _sequence.GetNextAsync("submission_id");
        var transactionRef = $"MOCK-EXP-{DateTime.UtcNow:yyyyMMddHHmmss}-{submissionId}";

        var submission = new ServiceSubmission
        {
            SubmissionId = submissionId,
            VendorId = vendor.VendorId,
            VendorUserId = vendor.UserId,
            PlanCode = "premium_1y",
            PlanName = "Premium 1 năm (test sắp hết hạn)",
            DurationMonths = 12,
            AmountVnd = PremiumOneYearPriceVnd,
            Currency = "VND",
            PaymentMethod = "mock_expiring_test",
            TransactionRef = transactionRef,
            Status = SubmissionStatuses.Approved,
            RequestedAt = paidAt,
            PaidAt = paidAt,
            ExpiresAt = expiresAt,
            ReviewedAt = paidAt,
            ReviewedBy = "system-test",
            ReviewNote = $"Backdated test subscription with {remainingMinutes} minute(s) remaining.",
            CreatedAt = now,
            UpdatedAt = now
        };

        await _db.ServiceSubmissions.InsertOneAsync(submission);

        var vendorUpdate = Builders<VendorProfile>.Update
            .Set(v => v.ServicePlan, "premium")
            .Set(v => v.PremiumExpiresAt, expiresAt)
            .Set(v => v.UpdatedAt, now);
        await _db.VendorProfiles.UpdateOneAsync(v => v.VendorId == vendor.VendorId, vendorUpdate);

        return Ok(new
        {
            message = $"Đã tạo gói premium giả lập còn {remainingMinutes} phút để kiểm tra tự động hết hạn.",
            remainingMinutes,
            paidAt,
            expiresAt,
            submission = ToDto(submission, vendor.BusinessName)
        });
    }

    [HttpGet("me/premium-status")]
    [Authorize(Roles = "Vendor")]
    public async Task<ActionResult<object>> GetMyPremiumStatus()
    {
        var vendor = await ResolveCurrentVendorAsync();
        if (vendor == null)
            return Forbid();

        var now = DateTime.UtcNow;

        var activeByProfile = string.Equals(vendor.ServicePlan, "premium", StringComparison.OrdinalIgnoreCase)
            && vendor.PremiumExpiresAt.HasValue
            && vendor.PremiumExpiresAt.Value > now;

        if (activeByProfile)
        {
            return Ok(new
            {
                isPremiumActive = true,
                plan = "premium",
                expiresAt = vendor.PremiumExpiresAt,
                source = "profile"
            });
        }

        var activeSubmission = await _db.ServiceSubmissions
            .Find(x => x.VendorId == vendor.VendorId
                && x.Status == SubmissionStatuses.Approved
                && x.ExpiresAt.HasValue
                && x.ExpiresAt > now)
            .SortByDescending(x => x.ExpiresAt)
            .FirstOrDefaultAsync();

        return Ok(new
        {
            isPremiumActive = activeSubmission != null,
            plan = activeSubmission?.PlanCode,
            expiresAt = activeSubmission?.ExpiresAt,
            source = activeSubmission != null ? "submission" : "none"
        });
    }

    [HttpGet("admin/submissions")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<object>> GetAllSubmissions(
        [FromQuery] string? status = null,
        [FromQuery] string? search = null,
        [FromQuery] string? fromDate = null,
        [FromQuery] string? toDate = null)
    {
        var filter = Builders<ServiceSubmission>.Filter.Empty;
        if (!string.IsNullOrWhiteSpace(status))
            filter &= Builders<ServiceSubmission>.Filter.Eq(x => x.Status, status.Trim().ToLowerInvariant());

        if (TryParseDateBoundary(fromDate, endOfDay: false, out var fromUtc))
            filter &= Builders<ServiceSubmission>.Filter.Gte(x => x.RequestedAt, fromUtc);

        if (TryParseDateBoundary(toDate, endOfDay: true, out var toUtc))
            filter &= Builders<ServiceSubmission>.Filter.Lte(x => x.RequestedAt, toUtc);

        var rows = await _db.ServiceSubmissions
            .Find(filter)
            .SortByDescending(x => x.RequestedAt)
            .Limit(200)
            .ToListAsync();

        var vendorMap = await BuildVendorMapAsync(rows.Select(x => x.VendorId).Distinct().ToList());

        var data = rows
            .Select(x => ToDto(x, vendorMap.TryGetValue(x.VendorId, out var name) ? name : null))
            .ToList();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim().ToLowerInvariant();
            data = data.Where(x =>
                    (x.BusinessName ?? string.Empty).ToLowerInvariant().Contains(keyword) ||
                    (x.TransactionRef ?? string.Empty).ToLowerInvariant().Contains(keyword) ||
                    x.VendorId.ToString().Contains(keyword) ||
                    x.SubmissionId.ToString().Contains(keyword))
                .ToList();
        }

        return Ok(new { data, total = data.Count });
    }

    [HttpPost("admin/submissions/{submissionId:int}/review")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<object>> ReviewSubmission(int submissionId, [FromBody] ReviewSubmissionRequest request)
    {
        var status = request.Status?.Trim().ToLowerInvariant();
        if (status != SubmissionStatuses.Approved && status != SubmissionStatuses.Rejected)
            return BadRequest(new { message = "Status không hợp lệ. Chỉ chấp nhận approved hoặc rejected." });

        var submission = await _db.ServiceSubmissions
            .Find(x => x.SubmissionId == submissionId)
            .FirstOrDefaultAsync();

        if (submission == null)
            return NotFound(new { message = "Không tìm thấy submission." });

        if (submission.Status != SubmissionStatuses.Pending)
            return BadRequest(new { message = "Submission này đã được xử lý trước đó." });

        var now = DateTime.UtcNow;
        DateTime? expiresAt = null;
        if (status == SubmissionStatuses.Approved)
        {
            expiresAt = now.AddYears(1);
        }

        var update = Builders<ServiceSubmission>.Update
            .Set(x => x.Status, status)
            .Set(x => x.ReviewNote, request.Note)
            .Set(x => x.ReviewedAt, now)
            .Set(x => x.ReviewedBy, User.Identity?.Name ?? "admin")
            .Set(x => x.UpdatedAt, now)
            .Set(x => x.PaidAt, status == SubmissionStatuses.Approved ? now : null)
            .Set(x => x.ExpiresAt, expiresAt);

        await _db.ServiceSubmissions.UpdateOneAsync(x => x.SubmissionId == submissionId, update);

        if (status == SubmissionStatuses.Approved)
        {
            var vendorUpdate = Builders<VendorProfile>.Update
                .Set(v => v.ServicePlan, "premium")
                .Set(v => v.PremiumExpiresAt, expiresAt)
                .Set(v => v.UpdatedAt, now);
            await _db.VendorProfiles.UpdateOneAsync(v => v.VendorId == submission.VendorId, vendorUpdate);
        }

        await CreateVendorNotificationForPremiumReviewAsync(submission, status, request.Note);

        return Ok(new
        {
            message = status == SubmissionStatuses.Approved
                ? "Đã duyệt thanh toán và kích hoạt gói premium 1 năm."
                : "Đã từ chối yêu cầu thanh toán.",
            submissionId,
            status,
            expiresAt
        });
    }

    private async Task<VendorProfile?> ResolveCurrentVendorAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name;

        VendorProfile? vendor = null;
        if (!string.IsNullOrWhiteSpace(userId))
        {
            vendor = await _db.VendorProfiles.Find(v => v.UserId == userId).FirstOrDefaultAsync();
        }

        if (vendor == null && !string.IsNullOrWhiteSpace(email))
        {
            vendor = await _db.VendorProfiles.Find(v => v.ContactEmail == email).FirstOrDefaultAsync();
            if (vendor != null && !string.IsNullOrWhiteSpace(userId) && string.IsNullOrWhiteSpace(vendor.UserId))
            {
                var userBindUpdate = Builders<VendorProfile>.Update
                    .Set(v => v.UserId, userId)
                    .Set(v => v.UpdatedAt, DateTime.UtcNow);
                await _db.VendorProfiles.UpdateOneAsync(v => v.VendorId == vendor.VendorId, userBindUpdate);
                vendor.UserId = userId;
            }
        }

        return vendor;
    }

    private async Task<Dictionary<int, string>> BuildVendorMapAsync(List<int> vendorIds)
    {
        if (vendorIds.Count == 0)
            return new Dictionary<int, string>();

        var vendors = await _db.VendorProfiles
            .Find(v => vendorIds.Contains(v.VendorId))
            .ToListAsync();

        return vendors.ToDictionary(v => v.VendorId, v => v.BusinessName);
    }

    private static bool TryParseDateBoundary(string? rawValue, bool endOfDay, out DateTime boundaryUtc)
    {
        boundaryUtc = default;

        if (string.IsNullOrWhiteSpace(rawValue))
            return false;

        if (!DateTime.TryParseExact(rawValue.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
            return false;

        var boundary = endOfDay
            ? parsedDate.Date.AddDays(1).AddTicks(-1)
            : parsedDate.Date;

        boundaryUtc = DateTime.SpecifyKind(boundary, DateTimeKind.Utc);
        return true;
    }

    private async Task<(bool IsActive, DateTime? ExpiresAt)> GetActivePremiumAsync(VendorProfile vendor)
    {
        var now = DateTime.UtcNow;
        var activeByProfile = string.Equals(vendor.ServicePlan, "premium", StringComparison.OrdinalIgnoreCase)
            && vendor.PremiumExpiresAt.HasValue
            && vendor.PremiumExpiresAt.Value > now;

        if (activeByProfile)
        {
            return (true, vendor.PremiumExpiresAt);
        }

        var activeSubmission = await _db.ServiceSubmissions
            .Find(x => x.VendorId == vendor.VendorId
                && x.Status == SubmissionStatuses.Approved
                && x.ExpiresAt.HasValue
                && x.ExpiresAt > now)
            .SortByDescending(x => x.ExpiresAt)
            .FirstOrDefaultAsync();

        return (activeSubmission != null, activeSubmission?.ExpiresAt);
    }

    private static SubmissionDto ToDto(ServiceSubmission row, string? businessName)
    {
        return new SubmissionDto
        {
            SubmissionId = row.SubmissionId,
            VendorId = row.VendorId,
            BusinessName = businessName,
            PlanCode = row.PlanCode,
            PlanName = row.PlanName,
            DurationMonths = row.DurationMonths,
            AmountVnd = row.AmountVnd,
            Currency = row.Currency,
            PaymentMethod = row.PaymentMethod,
            TransactionRef = row.TransactionRef,
            Status = row.Status,
            RequestedAt = row.RequestedAt,
            PaidAt = row.PaidAt,
            ExpiresAt = row.ExpiresAt,
            ReviewedAt = row.ReviewedAt,
            ReviewedBy = row.ReviewedBy,
            ReviewNote = row.ReviewNote
        };
    }

    private async Task CreateAdminNotificationForPremiumSubmitAsync(ServiceSubmission submission, VendorProfile vendor)
    {
        var vendorName = string.IsNullOrWhiteSpace(vendor.BusinessName)
            ? $"Vendor #{vendor.VendorId}"
            : vendor.BusinessName;

        var notification = new BsonDocument
        {
            { "AudienceRole", "admin" },
            { "Kind", "info" },
            { "Title", "Có yêu cầu Premium mới" },
            { "Message", $"{vendorName} vừa gửi yêu cầu đăng ký gói Premium 1 năm." },
            { "Href", "payment-management?status=pending" },
            { "Category", "premium-submit" },
            { "Status", SubmissionStatuses.Pending },
            { "CreatedAt", DateTime.UtcNow }
        };

        await InsertAppNotificationSafeAsync(notification);
    }

    private async Task CreateVendorNotificationForPremiumReviewAsync(ServiceSubmission submission, string status, string? note)
    {
        var isApproved = string.Equals(status, SubmissionStatuses.Approved, StringComparison.OrdinalIgnoreCase);
        var processedBy = User.Identity?.Name ?? User.FindFirstValue(ClaimTypes.Email) ?? "admin";
        var message = isApproved
            ? "Yêu cầu Premium của bạn đã được duyệt và gói đã được kích hoạt."
            : "Yêu cầu Premium của bạn đã bị từ chối.";

        var notification = new BsonDocument
        {
            { "AudienceRole", "vendor" },
            { "AudienceVendorId", submission.VendorId },
            { "Kind", isApproved ? "success" : "warning" },
            { "Title", isApproved ? "Premium đã được duyệt" : "Premium bị từ chối" },
            { "Message", message },
            { "Href", "payment-management" },
            { "Category", "premium-review" },
            { "Status", status },
            { "CreatedAt", DateTime.UtcNow },
            { "ProcessedAt", DateTime.UtcNow },
            { "ProcessedBy", processedBy }
        };

        if (!string.IsNullOrWhiteSpace(note))
        {
            notification.Add("ResponseMessage", note.Trim());
        }

        await InsertAppNotificationSafeAsync(notification);
    }

    private async Task InsertAppNotificationSafeAsync(BsonDocument notification)
    {
        try
        {
            notification["NotificationId"] = await _sequence.GetNextAsync(AppNotificationSequenceName);
            var collection = _db.Database.GetCollection<BsonDocument>(AppNotificationCollectionName);
            await collection.InsertOneAsync(notification);
        }
        catch
        {
            // Do not block payment workflow if notification write fails.
        }
    }
}

public class SimulatePremiumPaymentRequest
{
    public string? PaymentMethod { get; set; }
}

public class SimulatePremiumExpiringRequest
{
    public int? RemainingMinutes { get; set; } = 3;
}

public class ReviewSubmissionRequest
{
    public string Status { get; set; } = string.Empty;
    public string? Note { get; set; }
}

public class SubmissionDto
{
    public int SubmissionId { get; set; }
    public int VendorId { get; set; }
    public string? BusinessName { get; set; }
    public string PlanCode { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;
    public int DurationMonths { get; set; }
    public decimal AmountVnd { get; set; }
    public string Currency { get; set; } = "VND";
    public string PaymentMethod { get; set; } = string.Empty;
    public string TransactionRef { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewedBy { get; set; }
    public string? ReviewNote { get; set; }
}
