using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using StreetFoodNarrator.API.Data;
using StreetFoodNarrator.API.Models;

namespace StreetFoodNarrator.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize(Roles = "Admin")]
public class UsersController : ControllerBase
{
    private static readonly HashSet<string> ManageableRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Admin",
        "Vendor"
    };

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly MongoDbContext _db;

    public UsersController(
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        MongoDbContext db)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<UserListResponse>> GetUsers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? role = null,
        [FromQuery] bool includeDeleted = false)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var users = _userManager.Users.ToList();

        if (!includeDeleted)
        {
            users = users.Where(u => !u.IsDeleted).ToList();
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim().ToLowerInvariant();
            users = users.Where(u =>
                    (u.Email ?? string.Empty).ToLowerInvariant().Contains(keyword) ||
                    (u.FullName ?? string.Empty).ToLowerInvariant().Contains(keyword) ||
                    (u.PhoneNumber ?? string.Empty).ToLowerInvariant().Contains(keyword))
                .ToList();
        }

        var roleFilter = NormalizeRole(role);
        var items = new List<UserDto>(users.Count);

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            if (roleFilter != null && !roles.Any(r => string.Equals(r, roleFilter, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var vendorProfile = await _db.VendorProfiles
                .Find(v => v.UserId == user.Id.ToString())
                .FirstOrDefaultAsync();

            items.Add(new UserDto
            {
                Id = user.Id.ToString(),
                Email = user.Email ?? string.Empty,
                FullName = user.FullName,
                PhoneNumber = user.PhoneNumber,
                Roles = roles.ToList(),
                VendorId = vendorProfile?.VendorId,
                IsDeleted = user.IsDeleted,
                IsLocked = user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow,
                CreatedAt = user.CreatedAt,
                LastLoginAt = user.LastLoginAt
            });
        }

        var total = items.Count;
        var data = items
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Ok(new UserListResponse
        {
            Data = data,
            Total = total,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(total / (double)pageSize)
        });
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<UserDto>> GetUserById(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null || user.IsDeleted)
        {
            return NotFound(new { message = "User not found" });
        }

        var roles = await _userManager.GetRolesAsync(user);
        var vendorProfile = await _db.VendorProfiles
            .Find(v => v.UserId == user.Id.ToString())
            .FirstOrDefaultAsync();

        return Ok(new UserDto
        {
            Id = user.Id.ToString(),
            Email = user.Email ?? string.Empty,
            FullName = user.FullName,
            PhoneNumber = user.PhoneNumber,
            Roles = roles.ToList(),
            VendorId = vendorProfile?.VendorId,
            IsDeleted = user.IsDeleted,
            IsLocked = user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow,
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt
        });
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { message = "Email and password are required" });
        }

        var normalizedRole = NormalizeRole(request.Role);
        if (normalizedRole == null)
        {
            return BadRequest(new { message = "Role must be Admin or Vendor" });
        }

        if (!await _roleManager.RoleExistsAsync(normalizedRole))
        {
            return BadRequest(new { message = $"Role '{normalizedRole}' does not exist" });
        }

        var existing = await _userManager.FindByEmailAsync(request.Email.Trim());
        if (existing != null && !existing.IsDeleted)
        {
            return Conflict(new { message = "Email already registered" });
        }

        var user = new ApplicationUser
        {
            UserName = request.Email.Trim(),
            Email = request.Email.Trim(),
            FullName = request.FullName,
            PhoneNumber = request.PhoneNumber
        };

        var createResult = await _userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            return BadRequest(new { errors = createResult.Errors.Select(e => e.Description) });
        }

        await _userManager.AddToRoleAsync(user, normalizedRole);

        return Ok(new
        {
            message = "User created successfully",
            id = user.Id,
            role = normalizedRole
        });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateUser(string id, [FromBody] UpdateUserRequest request)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null || user.IsDeleted)
        {
            return NotFound(new { message = "User not found" });
        }

        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            var email = request.Email.Trim();
            var emailOwner = await _userManager.FindByEmailAsync(email);
            if (emailOwner != null && emailOwner.Id != user.Id)
            {
                return Conflict(new { message = "Email already in use" });
            }

            user.Email = email;
            user.UserName = email;
        }

        if (request.FullName != null)
        {
            user.FullName = request.FullName.Trim();
        }

        if (request.PhoneNumber != null)
        {
            user.PhoneNumber = request.PhoneNumber.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.Role))
        {
            var normalizedRole = NormalizeRole(request.Role);
            if (normalizedRole == null)
            {
                return BadRequest(new { message = "Role must be Admin or Vendor" });
            }

            var currentRoles = await _userManager.GetRolesAsync(user);
            foreach (var currentRole in currentRoles.Where(r => ManageableRoles.Contains(r)))
            {
                await _userManager.RemoveFromRoleAsync(user, currentRole);
            }

            if (!currentRoles.Any(r => string.Equals(r, normalizedRole, StringComparison.OrdinalIgnoreCase)))
            {
                await _userManager.AddToRoleAsync(user, normalizedRole);
            }
        }

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });
        }

        return Ok(new { message = "User updated successfully" });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null || user.IsDeleted)
        {
            return NotFound(new { message = "User not found" });
        }

        user.IsDeleted = true;
        user.DeletedAt = DateTime.UtcNow;
        user.LockoutEnabled = true;
        user.LockoutEnd = DateTimeOffset.MaxValue;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });
        }

        return Ok(new { message = "User deleted" });
    }

    [HttpPost("{id}/lock")]
    public async Task<IActionResult> LockUser(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null || user.IsDeleted)
        {
            return NotFound(new { message = "User not found" });
        }

        user.LockoutEnabled = true;
        user.LockoutEnd = DateTimeOffset.MaxValue;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });
        }

        return Ok(new { message = "User locked" });
    }

    [HttpPost("{id}/unlock")]
    public async Task<IActionResult> UnlockUser(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null || user.IsDeleted)
        {
            return NotFound(new { message = "User not found" });
        }

        user.LockoutEnd = null;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });
        }

        return Ok(new { message = "User unlocked" });
    }

    private static string? NormalizeRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role)) return null;
        var trimmed = role.Trim();
        return ManageableRoles.FirstOrDefault(r => string.Equals(r, trimmed, StringComparison.OrdinalIgnoreCase));
    }
}

public class UserDto
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public string? PhoneNumber { get; set; }
    public List<string> Roles { get; set; } = new();
    public int? VendorId { get; set; }
    public bool IsDeleted { get; set; }
    public bool IsLocked { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
}

public class UserListResponse
{
    public List<UserDto> Data { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

public class CreateUserRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public string? PhoneNumber { get; set; }
    public string Role { get; set; } = "Vendor";
}

public class UpdateUserRequest
{
    public string? Email { get; set; }
    public string? FullName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Role { get; set; }
}
