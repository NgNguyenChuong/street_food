using AspNetCore.Identity.MongoDbCore.Models;

namespace StreetFoodNarrator.API.Models;

public class ApplicationUser : MongoIdentityUser<Guid>
{
    public string? FullName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    
    // Navigation property for vendor
    public VendorProfile? VendorProfile { get; set; }

    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
}
