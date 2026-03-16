using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace StreetFoodNarrator.API.Models;

[BsonIgnoreExtraElements]
public class MenuItem
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("MenuItemId")]
    public int MenuItemId { get; set; }

    public int POI_ID { get; set; }
    
    [BsonIgnore]
    public POI? POI { get; set; }

    public int VendorId { get; set; }

    // Multi-language Names
    [Required, MaxLength(200)]
    public string Name_Vi { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Name_En { get; set; }

    [MaxLength(200)]
    public string? Name_Zh { get; set; }

    // Multi-language Descriptions
    [MaxLength(1000)]
    public string? Description_Vi { get; set; }

    [MaxLength(1000)]
    public string? Description_En { get; set; }

    [MaxLength(1000)]
    public string? Description_Zh { get; set; }

    // Images
    [MaxLength(500)]
    public string? ImageUrl { get; set; }

    public List<string>? ImageUrls { get; set; }

    // Price
    public decimal Price { get; set; }

    [MaxLength(20)]
    public string PriceUnit { get; set; } = "VND";

    [MaxLength(200)]
    public string? PriceNote { get; set; }

    // Categorization
    [MaxLength(100)]
    public string? Category { get; set; }

    public List<string>? Tags { get; set; }

    public bool IsSignatureDish { get; set; } = false;
    public bool IsAvailable { get; set; } = true;
    public int SortOrder { get; set; } = 0;

    // Soft delete
    public bool IsDeleted { get; set; } = false;

    // Timestamps
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}
