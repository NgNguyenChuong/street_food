using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using StreetFoodNarrator.API.Data;
using StreetFoodNarrator.API.Models;
using System.Security.Claims;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace StreetFoodNarrator.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class VendorsController : ControllerBase
{
    private readonly MongoDbContext _db;
    private readonly MongoSequenceService _sequence;
    private readonly UserManager<ApplicationUser> _userManager;

    private static readonly Regex PhoneRegex = new(@"^\+84(3|5|7|8|9)\d{8}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex PersonNameRegex = new(@"^[\p{L}][\p{L}\s'.-]{1,199}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex BusinessNameRegex = new(@"^(?=.{2,200}$)[\p{L}\p{N}\s&().,'""/+\-]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public VendorsController(MongoDbContext db, MongoSequenceService sequence, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _sequence = sequence;
        _userManager = userManager;
    }

    [Authorize(Roles = "Admin,Vendor")]
    [HttpGet("me")]
    public async Task<ActionResult<VendorProfile>> GetMyVendor()
    {
        if (!User.IsInRole("Vendor") && !User.IsInRole("Admin"))
        {
            return Forbid();
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var email = User.FindFirstValue(ClaimTypes.Email);
        var vendor = await _db.VendorProfiles.Find(v => v.UserId == userId).FirstOrDefaultAsync();
        if (vendor == null && !string.IsNullOrWhiteSpace(email))
        {
            vendor = await _db.VendorProfiles.Find(v => v.ContactEmail == email).FirstOrDefaultAsync();
            if (vendor != null && string.IsNullOrWhiteSpace(vendor.UserId))
            {
                var update = Builders<VendorProfile>.Update
                    .Set(v => v.UserId, userId)
                    .Set(v => v.UpdatedAt, DateTime.UtcNow);
                await _db.VendorProfiles.UpdateOneAsync(v => v.VendorId == vendor.VendorId, update);
            }
        }
        if (vendor == null)
        {
            if (!User.IsInRole("Vendor"))
            {
                return NotFound(new { message = "Vendor profile not found" });
            }

            var vendorId = await _sequence.GetNextAsync("vendor_id");
            var name = User.FindFirstValue(ClaimTypes.Name);
            var fallbackName = !string.IsNullOrWhiteSpace(name)
                ? name
                : (!string.IsNullOrWhiteSpace(email) ? email.Split('@')[0] : "Vendor");

            vendor = new VendorProfile
            {
                VendorId = vendorId,
                UserId = userId,
                ContactName = name,
                ContactEmail = email,
                BusinessName = fallbackName
            };

            await _db.VendorProfiles.InsertOneAsync(vendor);
        }

        return Ok(vendor);
    }

    [Authorize(Roles = "Admin,Vendor")]
    [HttpPut("me")]
    public async Task<ActionResult<VendorProfile>> UpdateMyVendor([FromBody] UpdateMyVendorRequest request)
    {
        if (!User.IsInRole("Vendor") && !User.IsInRole("Admin"))
        {
            return Forbid();
        }

        if (request == null)
        {
            return BadRequest(new { message = "Thiếu dữ liệu cập nhật." });
        }

        var hasChanges = request.BusinessName != null
            || request.BusinessDescription != null
            || request.ContactName != null
            || request.ContactEmail != null
            || request.ContactPhone != null
            || request.Address != null
            || request.FullName != null
            || request.PhoneNumber != null;

        if (!hasChanges)
        {
            return BadRequest(new { message = "Không có dữ liệu để cập nhật." });
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var email = User.FindFirstValue(ClaimTypes.Email);
        var name = User.FindFirstValue(ClaimTypes.Name);

        var businessName = request.BusinessName?.Trim();
        if (request.BusinessName != null && string.IsNullOrWhiteSpace(businessName))
        {
            return BadRequest(new { message = "Tên cửa hàng không được để trống." });
        }

        var fullName = NormalizeNullable(request.FullName);
        var businessDescription = NormalizeNullable(request.BusinessDescription);
        var contactName = NormalizeNullable(request.ContactName);
        var contactEmail = NormalizeNullable(request.ContactEmail)?.ToLowerInvariant();
        var address = NormalizeNullable(request.Address);

        if (!string.IsNullOrWhiteSpace(fullName) && fullName.Length > 200)
        {
            return BadRequest(new { message = "Họ và tên tối đa 200 ký tự." });
        }

        if (!string.IsNullOrWhiteSpace(businessName) && businessName.Length > 200)
        {
            return BadRequest(new { message = "Tên cửa hàng tối đa 200 ký tự." });
        }

        if (!string.IsNullOrWhiteSpace(contactName) && contactName.Length > 200)
        {
            return BadRequest(new { message = "Tên người liên hệ tối đa 200 ký tự." });
        }

        if (!string.IsNullOrWhiteSpace(contactEmail) && contactEmail.Length > 200)
        {
            return BadRequest(new { message = "Email liên hệ tối đa 200 ký tự." });
        }

        if (!string.IsNullOrWhiteSpace(address) && address.Length > 500)
        {
            return BadRequest(new { message = "Địa chỉ tối đa 500 ký tự." });
        }

        if (!string.IsNullOrWhiteSpace(businessDescription) && businessDescription.Length > 500)
        {
            return BadRequest(new { message = "Mô tả cửa hàng tối đa 500 ký tự." });
        }

        if (!string.IsNullOrWhiteSpace(fullName) && !PersonNameRegex.IsMatch(fullName))
        {
            return BadRequest(new { message = "Họ và tên chứa ký tự không hợp lệ." });
        }

        if (!string.IsNullOrWhiteSpace(contactName) && !PersonNameRegex.IsMatch(contactName))
        {
            return BadRequest(new { message = "Tên người liên hệ chứa ký tự không hợp lệ." });
        }

        if (!string.IsNullOrWhiteSpace(businessName) && !BusinessNameRegex.IsMatch(businessName))
        {
            return BadRequest(new { message = "Tên cửa hàng chứa ký tự không hợp lệ." });
        }

        if (HasInvalidControlChars(fullName)
            || HasInvalidControlChars(businessName)
            || HasInvalidControlChars(contactName)
            || HasInvalidControlChars(contactEmail)
            || HasInvalidControlChars(address)
            || HasInvalidControlChars(request.PhoneNumber)
            || HasInvalidControlChars(request.ContactPhone)
            || HasInvalidControlChars(businessDescription, allowLineBreaks: true))
        {
            return BadRequest(new { message = "Dữ liệu chứa ký tự điều khiển không hợp lệ." });
        }

        if (!string.IsNullOrWhiteSpace(contactEmail) && !new EmailAddressAttribute().IsValid(contactEmail))
        {
            return BadRequest(new { message = "Email liên hệ không đúng định dạng." });
        }

        var normalizedContactPhone = NormalizePhoneNumber(request.ContactPhone);
        if (request.ContactPhone != null && !string.IsNullOrWhiteSpace(request.ContactPhone) && normalizedContactPhone == null)
        {
            return BadRequest(new { message = "Số điện thoại liên hệ không đúng định dạng Việt Nam." });
        }

        var normalizedAccountPhone = NormalizePhoneNumber(request.PhoneNumber);
        if (request.PhoneNumber != null && !string.IsNullOrWhiteSpace(request.PhoneNumber) && normalizedAccountPhone == null)
        {
            return BadRequest(new { message = "Số điện thoại tài khoản không đúng định dạng Việt Nam." });
        }

        var vendor = await _db.VendorProfiles.Find(v => v.UserId == userId).FirstOrDefaultAsync();
        if (vendor == null && !string.IsNullOrWhiteSpace(email))
        {
            vendor = await _db.VendorProfiles.Find(v => v.ContactEmail == email).FirstOrDefaultAsync();
            if (vendor != null && string.IsNullOrWhiteSpace(vendor.UserId))
            {
                var bindUpdate = Builders<VendorProfile>.Update
                    .Set(v => v.UserId, userId)
                    .Set(v => v.UpdatedAt, DateTime.UtcNow);
                await _db.VendorProfiles.UpdateOneAsync(v => v.VendorId == vendor.VendorId, bindUpdate);
            }
        }

        if (vendor == null)
        {
            if (!User.IsInRole("Vendor"))
            {
                return NotFound(new { message = "Vendor profile not found" });
            }

            var vendorId = await _sequence.GetNextAsync("vendor_id");
            var fallbackName = !string.IsNullOrWhiteSpace(name)
                ? name
                : (!string.IsNullOrWhiteSpace(email) ? email.Split('@')[0] : "Vendor");

            vendor = new VendorProfile
            {
                VendorId = vendorId,
                UserId = userId,
                ContactName = name,
                ContactEmail = email,
                BusinessName = fallbackName
            };

            await _db.VendorProfiles.InsertOneAsync(vendor);
        }

        var updates = new List<UpdateDefinition<VendorProfile>>
        {
            Builders<VendorProfile>.Update.Set(v => v.UpdatedAt, DateTime.UtcNow)
        };

        if (request.BusinessName != null)
            updates.Add(Builders<VendorProfile>.Update.Set(v => v.BusinessName, businessName!));
        if (request.BusinessDescription != null)
            updates.Add(Builders<VendorProfile>.Update.Set(v => v.BusinessDescription, businessDescription));
        if (request.ContactName != null)
            updates.Add(Builders<VendorProfile>.Update.Set(v => v.ContactName, contactName));
        if (request.ContactEmail != null)
            updates.Add(Builders<VendorProfile>.Update.Set(v => v.ContactEmail, contactEmail));
        if (request.ContactPhone != null)
            updates.Add(Builders<VendorProfile>.Update.Set(v => v.ContactPhone, normalizedContactPhone));
        if (request.Address != null)
            updates.Add(Builders<VendorProfile>.Update.Set(v => v.Address, address));

        var combinedUpdate = Builders<VendorProfile>.Update.Combine(updates);
        await _db.VendorProfiles.UpdateOneAsync(v => v.VendorId == vendor.VendorId, combinedUpdate);

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return NotFound(new { message = "User not found" });
        }

        var userChanged = false;
        if (request.FullName != null)
        {
            user.FullName = fullName;
            userChanged = true;
        }

        if (request.PhoneNumber != null)
        {
            user.PhoneNumber = normalizedAccountPhone;
            userChanged = true;
        }

        if (userChanged)
        {
            var updateUserResult = await _userManager.UpdateAsync(user);
            if (!updateUserResult.Succeeded)
            {
                return BadRequest(new
                {
                    message = "Không thể cập nhật thông tin tài khoản.",
                    errors = updateUserResult.Errors.Select(e => e.Description)
                });
            }
        }

        var updatedVendor = await _db.VendorProfiles.Find(v => v.VendorId == vendor.VendorId).FirstOrDefaultAsync();
        return Ok(updatedVendor);
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<ActionResult<VendorListResponse>> GetVendors(
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] string? sort = null)
    {
        var filter = Builders<VendorProfile>.Filter.Empty;

        if (!string.IsNullOrWhiteSpace(search))
        {
            var regex = new MongoDB.Bson.BsonRegularExpression(search, "i");
            var filters = new List<FilterDefinition<VendorProfile>>
            {
                Builders<VendorProfile>.Filter.Regex(v => v.BusinessName, regex),
                Builders<VendorProfile>.Filter.Regex(v => v.ContactName, regex),
                Builders<VendorProfile>.Filter.Regex(v => v.ContactEmail, regex),
                Builders<VendorProfile>.Filter.Regex(v => v.ContactPhone, regex),
                Builders<VendorProfile>.Filter.Regex(v => v.Address, regex)
            };
            filter &= Builders<VendorProfile>.Filter.Or(filters);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            filter &= Builders<VendorProfile>.Filter.Eq(v => v.VerificationStatus, status.ToLowerInvariant());
        }

        var vendors = await _db.VendorProfiles.Find(filter).ToListAsync();
        var vendorIds = vendors.Select(v => v.VendorId).ToList();

        var poiCounts = await _db.POIs
            .Aggregate()
            .Match(p => vendorIds.Contains(p.VendorId ?? 0) && p.DeletedAt == null)
            .Group(p => p.VendorId, g => new { VendorId = g.Key, Count = g.Count() })
            .ToListAsync();

        var poiCountMap = poiCounts
            .Where(x => x.VendorId.HasValue)
            .ToDictionary(x => x.VendorId!.Value, x => x.Count);

        var result = vendors.Select(v => new VendorDto
        {
            VendorId = v.VendorId,
            BusinessName = v.BusinessName,
            ContactName = v.ContactName,
            ContactEmail = v.ContactEmail,
            ContactPhone = v.ContactPhone,
            Address = v.Address,
            VerificationStatus = v.VerificationStatus,
            IsVerified = v.IsVerified,
            CreatedAt = v.CreatedAt,
            PoiCount = poiCountMap.TryGetValue(v.VendorId, out var count) ? count : 0,
            ViewCount = v.ViewCount,
            Rating = v.Rating
        }).ToList();

        result = sort switch
        {
            "oldest" => result.OrderBy(v => v.CreatedAt).ToList(),
            "name-asc" => result.OrderBy(v => v.BusinessName).ToList(),
            "pois-desc" => result.OrderByDescending(v => v.PoiCount).ToList(),
            _ => result.OrderByDescending(v => v.CreatedAt).ToList()
        };

        return Ok(new VendorListResponse
        {
            Data = result,
            Total = result.Count
        });
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("stats")]
    public async Task<ActionResult<VendorStatsResponse>> GetVendorStats()
    {
        var total = await _db.VendorProfiles.CountDocumentsAsync(Builders<VendorProfile>.Filter.Empty);
        var approved = await _db.VendorProfiles.CountDocumentsAsync(v => v.VerificationStatus == "approved");
        var pending = await _db.VendorProfiles.CountDocumentsAsync(v => v.VerificationStatus == "pending");
        var rejected = await _db.VendorProfiles.CountDocumentsAsync(v => v.VerificationStatus == "rejected");

        return Ok(new VendorStatsResponse
        {
            Total = total,
            Approved = approved,
            Pending = pending,
            Rejected = rejected
        });
    }

    /// <summary>
    /// Get vendor detail by VendorId (Admin only)
    /// </summary>
    [Authorize(Roles = "Admin")]
    [HttpGet("{vendorId:int}")]
    public async Task<ActionResult<VendorDetailDto>> GetVendor(int vendorId)
    {
        var vendor = await _db.VendorProfiles
            .Find(v => v.VendorId == vendorId)
            .FirstOrDefaultAsync();

        if (vendor == null)
            return NotFound(new { message = "Vendor not found" });

        var pois = await _db.POIs
            .Find(p => p.VendorId == vendorId && p.DeletedAt == null)
            .ToListAsync();

        return Ok(new VendorDetailDto
        {
            VendorId = vendor.VendorId,
            BusinessName = vendor.BusinessName,
            BusinessDescription = vendor.BusinessDescription,
            ContactName = vendor.ContactName,
            ContactEmail = vendor.ContactEmail,
            ContactPhone = vendor.ContactPhone,
            Address = vendor.Address,
            VerificationStatus = vendor.VerificationStatus,
            IsVerified = vendor.IsVerified,
            Rating = vendor.Rating,
            ViewCount = vendor.ViewCount,
            CreatedAt = vendor.CreatedAt,
            UpdatedAt = vendor.UpdatedAt,
            POIs = pois.Select(p => new VendorPoiDto
            {
                POI_ID = p.POI_ID,
                Name = p.Name_Vi,
                Address = p.Address,
                Category = p.Category,
                IsActive = p.IsActive
            }).ToList()
        });
    }

    /// <summary>
    /// Update vendor verification status (Admin only)
    /// </summary>
    [Authorize(Roles = "Admin")]
    [HttpPut("{vendorId:int}/status")]
    public async Task<IActionResult> UpdateVendorStatus(int vendorId, [FromBody] UpdateVendorStatusRequest request)
    {
        var allowedStatuses = new[] { "pending", "approved", "rejected" };
        var status = request.Status?.ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(status) || !allowedStatuses.Contains(status))
            return BadRequest(new { message = "Invalid status. Allowed: pending, approved, rejected" });

        var filter = Builders<VendorProfile>.Filter.Eq(v => v.VendorId, vendorId);
        var update = Builders<VendorProfile>.Update
            .Set(v => v.VerificationStatus, status)
            .Set(v => v.IsVerified, status == "approved")
            .Set(v => v.UpdatedAt, DateTime.UtcNow);

        var result = await _db.VendorProfiles.UpdateOneAsync(filter, update);
        if (result.MatchedCount == 0)
            return NotFound(new { message = "Vendor not found" });

        return Ok(new { message = $"Vendor status updated to {status}" });
    }

    /// <summary>
    /// Update vendor profile (Admin only)
    /// </summary>
    [Authorize(Roles = "Admin")]
    [HttpPut("{vendorId:int}")]
    public async Task<IActionResult> UpdateVendor(int vendorId, [FromBody] UpdateVendorRequest request)
    {
        var filter = Builders<VendorProfile>.Filter.Eq(v => v.VendorId, vendorId);
        var updates = new List<UpdateDefinition<VendorProfile>>
        {
            Builders<VendorProfile>.Update.Set(v => v.UpdatedAt, DateTime.UtcNow)
        };

        if (request.BusinessName != null)
            updates.Add(Builders<VendorProfile>.Update.Set(v => v.BusinessName, request.BusinessName));
        if (request.BusinessDescription != null)
            updates.Add(Builders<VendorProfile>.Update.Set(v => v.BusinessDescription, request.BusinessDescription));
        if (request.ContactName != null)
            updates.Add(Builders<VendorProfile>.Update.Set(v => v.ContactName, request.ContactName));
        if (request.ContactEmail != null)
            updates.Add(Builders<VendorProfile>.Update.Set(v => v.ContactEmail, request.ContactEmail));
        if (request.ContactPhone != null)
            updates.Add(Builders<VendorProfile>.Update.Set(v => v.ContactPhone, request.ContactPhone));
        if (request.Address != null)
            updates.Add(Builders<VendorProfile>.Update.Set(v => v.Address, request.Address));

        var combinedUpdate = Builders<VendorProfile>.Update.Combine(updates);
        var result = await _db.VendorProfiles.UpdateOneAsync(filter, combinedUpdate);

        if (result.MatchedCount == 0)
            return NotFound(new { message = "Vendor not found" });

        return Ok(new { message = "Vendor updated successfully" });
    }

    /// <summary>
    /// Create a new vendor (Admin creates user + vendor profile)
    /// </summary>
    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<IActionResult> CreateVendor([FromBody] CreateVendorRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.BusinessName))
            return BadRequest(new { message = "Email and BusinessName are required" });

        // Check if email already exists
        var existingUser = await _userManager.FindByEmailAsync(request.Email);
        if (existingUser != null)
            return Conflict(new { message = "Email already registered" });

        // Create user account
        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FullName = request.ContactName ?? request.BusinessName,
            PhoneNumber = request.ContactPhone
        };

        var password = request.Password ?? GenerateDefaultPassword();
        var createResult = await _userManager.CreateAsync(user, password);
        if (!createResult.Succeeded)
            return BadRequest(new { errors = createResult.Errors.Select(e => e.Description) });

        await _userManager.AddToRoleAsync(user, "Vendor");

        // Create vendor profile
        var vendorId = await _sequence.GetNextAsync("vendor_id");
        var initialStatus = request.Status?.ToLowerInvariant() == "approved" ? "approved" : "pending";

        var vendor = new VendorProfile
        {
            VendorId = vendorId,
            UserId = user.Id.ToString(),
            ContactName = request.ContactName,
            ContactEmail = request.Email,
            BusinessName = request.BusinessName,
            BusinessDescription = request.BusinessDescription,
            ContactPhone = request.ContactPhone,
            Address = request.Address,
            VerificationStatus = initialStatus,
            IsVerified = initialStatus == "approved"
        };

        await _db.VendorProfiles.InsertOneAsync(vendor);

        return Ok(new
        {
            message = "Vendor created successfully",
            vendorId = vendor.VendorId,
            temporaryPassword = password
        });
    }

    /// <summary>
    /// Soft-delete a vendor (Admin only)
    /// </summary>
    [Authorize(Roles = "Admin")]
    [HttpDelete("{vendorId:int}")]
    public async Task<IActionResult> DeleteVendor(int vendorId)
    {
        var filter = Builders<VendorProfile>.Filter.Eq(v => v.VendorId, vendorId);
        var update = Builders<VendorProfile>.Update
            .Set(v => v.IsDeleted, true)
            .Set(v => v.DeletedAt, DateTime.UtcNow);

        var result = await _db.VendorProfiles.UpdateOneAsync(filter, update);
        if (result.MatchedCount == 0)
            return NotFound(new { message = "Vendor not found" });

        return Ok(new { message = "Vendor deleted" });
    }

    private static string GenerateDefaultPassword()
    {
        // Generate a reasonably secure default password
        var random = new Random();
        const string upper = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        const string lower = "abcdefghijklmnopqrstuvwxyz";
        const string digits = "0123456789";
        const string special = "!@#$%&*";

        var password = new char[12];
        password[0] = upper[random.Next(upper.Length)];
        password[1] = lower[random.Next(lower.Length)];
        password[2] = digits[random.Next(digits.Length)];
        password[3] = special[random.Next(special.Length)];

        const string all = upper + lower + digits + special;
        for (int i = 4; i < 12; i++)
            password[i] = all[random.Next(all.Length)];

        // Shuffle
        for (int i = password.Length - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (password[i], password[j]) = (password[j], password[i]);
        }

        return new string(password);
    }

    private static string? NormalizeNullable(string? value)
    {
        if (value == null)
            return null;

        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }

    private static string? NormalizePhoneNumber(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var compact = Regex.Replace(raw.Trim(), @"[\s\.\-]", string.Empty);
        if (compact.StartsWith("84", StringComparison.Ordinal))
            compact = $"+{compact}";
        else if (compact.StartsWith("0", StringComparison.Ordinal))
            compact = $"+84{compact[1..]}";

        if (!PhoneRegex.IsMatch(compact))
            return null;

        return compact;
    }

    private static bool HasInvalidControlChars(string? value, bool allowLineBreaks = false)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        foreach (var ch in value)
        {
            if (char.IsControl(ch))
            {
                if (allowLineBreaks && (ch == '\n' || ch == '\r' || ch == '\t'))
                    continue;

                return true;
            }
        }

        return false;
    }
}

public class VendorDto
{
    public int VendorId { get; set; }
    public string BusinessName { get; set; } = string.Empty;
    public string? ContactName { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? Address { get; set; }
    public string VerificationStatus { get; set; } = "pending";
    public bool IsVerified { get; set; }
    public DateTime CreatedAt { get; set; }
    public int PoiCount { get; set; }
    public long ViewCount { get; set; }
    public double? Rating { get; set; }
}

public class VendorListResponse
{
    public List<VendorDto> Data { get; set; } = new();
    public int Total { get; set; }
}

public class VendorStatsResponse
{
    public long Total { get; set; }
    public long Approved { get; set; }
    public long Pending { get; set; }
    public long Rejected { get; set; }
}

public class VendorDetailDto
{
    public int VendorId { get; set; }
    public string BusinessName { get; set; } = string.Empty;
    public string? BusinessDescription { get; set; }
    public string? ContactName { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? Address { get; set; }
    public string VerificationStatus { get; set; } = "pending";
    public bool IsVerified { get; set; }
    public double? Rating { get; set; }
    public long ViewCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<VendorPoiDto> POIs { get; set; } = new();
}

public class VendorPoiDto
{
    public int POI_ID { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? Category { get; set; }
    public bool IsActive { get; set; }
}

public class UpdateVendorStatusRequest
{
    public string Status { get; set; } = string.Empty;
}

public class UpdateMyVendorRequest
{
    public string? FullName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? BusinessName { get; set; }
    public string? BusinessDescription { get; set; }
    public string? ContactName { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? Address { get; set; }
}

public class UpdateVendorRequest
{
    public string? BusinessName { get; set; }
    public string? BusinessDescription { get; set; }
    public string? ContactName { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? Address { get; set; }
}

public class CreateVendorRequest
{
    public string Email { get; set; } = string.Empty;
    public string? Password { get; set; }
    public string BusinessName { get; set; } = string.Empty;
    public string? BusinessDescription { get; set; }
    public string? ContactName { get; set; }
    public string? ContactPhone { get; set; }
    public string? Address { get; set; }
    public string? Status { get; set; }
}
