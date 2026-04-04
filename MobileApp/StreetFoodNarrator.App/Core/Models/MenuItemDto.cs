using System.Text.Json.Serialization;
using SQLite;

namespace StreetFoodNarrator.App.Core.Models;

public class MenuItemDto
{
    [PrimaryKey]
    [JsonPropertyName("menuItemId")]
    public int MenuItemId { get; set; }
    
    [JsonPropertyName("poI_ID")]
    public int POI_ID { get; set; }
    
    [JsonPropertyName("name_Vi")]
    public string Name_Vi { get; set; } = string.Empty;
    
    [JsonPropertyName("name_En")]
    public string Name_En { get; set; }
    
    [JsonPropertyName("name_Zh")]
    public string Name_Zh { get; set; }
    
    [JsonPropertyName("description_Vi")]
    public string Description_Vi { get; set; }
    
    [JsonPropertyName("description_En")]
    public string Description_En { get; set; }
    
    [JsonPropertyName("description_Zh")]
    public string Description_Zh { get; set; }
    
    [JsonPropertyName("imageUrl")]
    public string ImageUrl { get; set; }
    
    [JsonPropertyName("price")]
    public decimal Price { get; set; }
    
    [JsonPropertyName("priceUnit")]
    public string PriceUnit { get; set; } = "VND";
    
    [JsonPropertyName("isSignatureDish")]
    public bool IsSignatureDish { get; set; }
    
    public string DisplayImageUrl
    {
        get
        {
            if (string.IsNullOrWhiteSpace(ImageUrl)) return "welcome_streetfood.jpg";
            if (ImageUrl.StartsWith("http://") || ImageUrl.StartsWith("https://"))
                return ImageUrl;
            var baseUrl = AppConfig.GetResolvedApiBaseUrl().TrimEnd('/');
            return $"{baseUrl}/{ImageUrl.TrimStart('/')}";
        }
    }
}

public class MenuItemResponse
{
    [JsonPropertyName("data")]
    public List<MenuItemDto> Data { get; set; } = new();
}

