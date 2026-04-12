using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using StreetFoodNarrator.API.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;

namespace StreetFoodNarrator.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IConfiguration _configuration;

    private static readonly Regex EmailRegex = new(@"^[^\s@]+@[^\s@]+\.[^\s@]{2,}$", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex PasswordRegex = new(@"^(?=.*[A-Z]).{8,}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex PhoneRegex = new(@"^\+84(3|5|7|8|9)\d{8}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public AuthController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, IConfiguration configuration)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _configuration = configuration;
    }

    /// <summary>
    /// Register a new user
    /// </summary>
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterModel model)
    {
        if (model == null)
            return BadRequest(new { message = "Dữ liệu đăng ký không hợp lệ." });

        var fullName = (model.FullName ?? string.Empty).Trim();
        var email = (model.Email ?? string.Empty).Trim().ToLowerInvariant();
        var password = model.Password ?? string.Empty;
        var normalizedPhone = NormalizePhoneNumber(model.PhoneNumber);

        if (string.IsNullOrWhiteSpace(fullName) || fullName.Length < 2)
            return BadRequest(new { message = "Họ và tên phải có ít nhất 2 ký tự." });

        if (!EmailRegex.IsMatch(email))
            return BadRequest(new { message = "Email không đúng định dạng." });

        if (!PasswordRegex.IsMatch(password))
            return BadRequest(new { message = "Mật khẩu phải có ít nhất 8 ký tự và chứa tối thiểu 1 ký tự in hoa." });

        if (!string.IsNullOrWhiteSpace(model.PhoneNumber) && normalizedPhone == null)
            return BadRequest(new { message = "Số điện thoại không đúng định dạng Việt Nam." });

        var existingByEmail = await _userManager.FindByEmailAsync(email);
        if (existingByEmail != null)
            return Conflict(new { message = "Email này đã được đăng ký. Vui lòng dùng email khác." });

        if (!string.IsNullOrWhiteSpace(normalizedPhone))
        {
            var existingByPhone = _userManager.Users
                .AsEnumerable()
                .FirstOrDefault(u => !string.IsNullOrWhiteSpace(u.PhoneNumber)
                    && string.Equals(NormalizePhoneNumber(u.PhoneNumber), normalizedPhone, StringComparison.OrdinalIgnoreCase));

            if (existingByPhone != null)
                return Conflict(new { message = "Số điện thoại này đã được đăng ký. Vui lòng dùng số khác." });
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = fullName,
            PhoneNumber = normalizedPhone
        };

        var result = await _userManager.CreateAsync(user, password);

        if (!result.Succeeded)
        {
            return BadRequest(new { errors = MapIdentityErrors(result.Errors) });
        }

        // Assign default role
        await _userManager.AddToRoleAsync(user, "Vendor");

        return Ok(new { message = "Đăng ký tài khoản thành công.", userId = user.Id });
    }

    /// <summary>
    /// Login and get JWT token
    /// </summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginModel model)
    {
        if (model == null)
            return BadRequest(new { message = "Dữ liệu đăng nhập không hợp lệ." });

        var email = (model.Email ?? string.Empty).Trim().ToLowerInvariant();
        var password = model.Password ?? string.Empty;

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return BadRequest(new { message = "Vui lòng nhập đầy đủ email và mật khẩu." });

        if (!EmailRegex.IsMatch(email))
            return BadRequest(new { message = "Email không đúng định dạng." });

        var user = await _userManager.FindByEmailAsync(email);
        
        if (user == null || user.IsDeleted)
        {
            return Unauthorized(new { message = "Email hoặc mật khẩu không đúng." });
        }

        if (await _userManager.IsLockedOutAsync(user))
        {
            return Unauthorized(new { message = "Tài khoản đang bị khóa tạm thời. Vui lòng thử lại sau." });
        }

        var result = await _signInManager.CheckPasswordSignInAsync(user, password, false);

        if (result.IsLockedOut)
        {
            return Unauthorized(new { message = "Tài khoản đang bị khóa tạm thời. Vui lòng thử lại sau." });
        }

        if (result.IsNotAllowed)
        {
            return Unauthorized(new { message = "Tài khoản hiện chưa được phép đăng nhập." });
        }

        if (!result.Succeeded)
        {
            return Unauthorized(new { message = "Email hoặc mật khẩu không đúng." });
        }

        user.LastLoginAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        var token = await GenerateJwtToken(user);
        var roles = await _userManager.GetRolesAsync(user);

        return Ok(new
        {
            token,
            expiresIn = _configuration.GetSection("JwtSettings")["ExpiresInHours"],
            user = new
            {
                id = user.Id,
                email = user.Email,
                fullName = user.FullName,
                roles
            }
        });
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

    private static List<string> MapIdentityErrors(IEnumerable<IdentityError> errors)
    {
        var mapped = new List<string>();

        foreach (var error in errors)
        {
            switch (error.Code)
            {
                case "DuplicateUserName":
                case "DuplicateEmail":
                    mapped.Add("Email này đã được đăng ký. Vui lòng dùng email khác.");
                    break;
                case "InvalidEmail":
                    mapped.Add("Email không đúng định dạng.");
                    break;
                case "PasswordTooShort":
                case "PasswordRequiresUpper":
                    mapped.Add("Mật khẩu phải có ít nhất 8 ký tự và chứa tối thiểu 1 ký tự in hoa.");
                    break;
                default:
                    mapped.Add(error.Description);
                    break;
            }
        }

        return mapped.Distinct().ToList();
    }

    /// <summary>
    /// Get current user profile
    /// </summary>
    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentUser()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = await _userManager.FindByIdAsync(userId!);

        if (user == null)
        {
            return NotFound(new { message = "User not found" });
        }

        var roles = await _userManager.GetRolesAsync(user);

        return Ok(new
        {
            id = user.Id,
            email = user.Email,
            fullName = user.FullName,
            phoneNumber = user.PhoneNumber,
            roles
        });
    }

    /// <summary>
    /// Refresh JWT token
    /// </summary>
    [Authorize]
    [HttpPost("refresh")]
    public async Task<IActionResult> RefreshToken()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = await _userManager.FindByIdAsync(userId!);

        if (user == null)
        {
            return Unauthorized();
        }

        var token = await GenerateJwtToken(user);

        return Ok(new
        {
            token,
            expiresIn = _configuration.GetSection("JwtSettings")["ExpiresInHours"]
        });
    }

    private async Task<string> GenerateJwtToken(ApplicationUser user)
    {
        var jwtSettings = _configuration.GetSection("JwtSettings");
        var secretKey = jwtSettings["SecretKey"]!;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var roles = await _userManager.GetRolesAsync(user);
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email!),
            new Claim(ClaimTypes.Name, user.FullName ?? user.UserName!),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(Convert.ToDouble(jwtSettings["ExpiresInHours"])),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

// DTOs
public class RegisterModel
{
    public string Email { get; set; } = null!;
    public string Password { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string? PhoneNumber { get; set; }
}

public class LoginModel
{
    public string Email { get; set; } = null!;
    public string Password { get; set; } = null!;
}
