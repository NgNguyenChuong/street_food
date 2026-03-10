using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace StreetFoodNarrator.API.Models;

[BsonIgnoreExtraElements]
public class VendorProfile
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("VendorId")]
    public int VendorId { get; set; }
    
    public string UserId { get; set; } = string.Empty;
    [BsonIgnore]
    public ApplicationUser User { get; set; } = null!;
    
    [MaxLength(200)]
    public string? ContactName { get; set; }

    [MaxLength(200)]
    public string? ContactEmail { get; set; }

    [MaxLength(200)]
    public string BusinessName { get; set; } = string.Empty;
    
    [MaxLength(500)]
    public string? BusinessDescription { get; set; }
    
    [Phone]
    public string? ContactPhone { get; set; }
    
    [MaxLength(500)]
    public string? Address { get; set; }
    
    public bool IsVerified { get; set; } = false;
    [MaxLength(20)]
    public string VerificationStatus { get; set; } = "pending"; // approved | pending | rejected
    public double? Rating { get; set; }
    public long ViewCount { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    
    // Navigation
    [BsonIgnore]
    public ICollection<POI> POIs { get; set; } = new List<POI>();
}
