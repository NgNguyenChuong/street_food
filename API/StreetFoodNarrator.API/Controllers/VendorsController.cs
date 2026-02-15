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
public class VendorsController : ControllerBase
{
    private readonly MongoDbContext _db;
    private readonly MongoSequenceService _sequence;

    public VendorsController(MongoDbContext db, MongoSequenceService sequence)
    {
        _db = db;
        _sequence = sequence;
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

        var vendor = await _db.VendorProfiles.Find(v => v.UserId == userId).FirstOrDefaultAsync();
        if (vendor == null)
        {
            if (!User.IsInRole("Vendor"))
            {
                return NotFound(new { message = "Vendor profile not found" });
            }

            var vendorId = await _sequence.GetNextAsync("vendor_id");
            var email = User.FindFirstValue(ClaimTypes.Email);
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
