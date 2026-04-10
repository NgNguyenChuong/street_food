using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using StreetFoodNarrator.API.Data;
using StreetFoodNarrator.API.Models;
using System.Security.Claims;

namespace StreetFoodNarrator.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class PaymentsController : ControllerBase
{
    private const decimal PremiumOneYearPriceVnd = 1990000m;

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

        if (!string.Equals(vendor.VerificationStatus, "approved", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Vendor phải được duyệt trước khi đăng ký gói premium." });
        }

        var submissionId = await _sequence.GetNextAsync("submission_id");
        var now = DateTime.UtcNow;
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
            Status = SubmissionStatuses.Pending,
            RequestedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _db.ServiceSubmissions.InsertOneAsync(submission);

        return Ok(new
        {
            message = "Đã tạo yêu cầu thanh toán giả lập. Chờ admin duyệt để kích hoạt gói premium.",
            submission = ToDto(submission, vendor.BusinessName)
        });
    }

    [HttpGet("me")]
    [Authorize(Roles = "Vendor")]
    public async Task<ActionResult<object>> GetMySubmissions()
    {
        var vendor = await ResolveCurrentVendorAsync();
        if (vendor == null)
            return Forbid();

        var rows = await _db.ServiceSubmissions
            .Find(x => x.VendorId == vendor.VendorId)
            .SortByDescending(x => x.RequestedAt)
            .Limit(50)
            .ToListAsync();

        var data = rows.Select(x => ToDto(x, vendor.BusinessName)).ToList();
        return Ok(new { data, total = data.Count });
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
    public async Task<ActionResult<object>> GetAllSubmissions([FromQuery] string? status = null, [FromQuery] string? search = null)
    {
        var filter = Builders<ServiceSubmission>.Filter.Empty;
        if (!string.IsNullOrWhiteSpace(status))
            filter &= Builders<ServiceSubmission>.Filter.Eq(x => x.Status, status.Trim().ToLowerInvariant());

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
}

public class SimulatePremiumPaymentRequest
{
    public string? PaymentMethod { get; set; }
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
