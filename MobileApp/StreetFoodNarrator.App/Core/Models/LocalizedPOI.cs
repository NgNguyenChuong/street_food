namespace StreetFoodNarrator.App.Core.Models;

/// <summary>
/// Localized POI - POI với fields được localized cho ngôn ngữ hiện tại
/// Inspired by FlavorQuest LocalizedPOI
/// </summary>
public class LocalizedPOI
{
    public int Id { get; set; }
    
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    
    public int Radius { get; set; }
    public int Priority { get; set; }
    
    /// <summary>
    /// Tên đã được localized
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Mô tả đã được localized
    /// </summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Audio URL đã được localized
    /// </summary>
    public string? AudioUrl { get; set; }
    
    public string? ImageUrl { get; set; }
    public string? SignatureDish { get; set; }
    public string? FunFact { get; set; }
    public string? EstimatedHours { get; set; }
    
    public string Type { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    
    /// <summary>
    /// Ngôn ngữ hiện tại
    /// </summary>
    public string CurrentLanguage { get; set; } = "vi";
    
    /// <summary>
    /// Convert từ POI thông thường
    /// </summary>
    public static LocalizedPOI FromPOI(POI poi, string language = "vi")
    {
        return new LocalizedPOI
        {
            Id = poi.Id,
            Latitude = poi.Latitude,
            Longitude = poi.Longitude,
            Radius = poi.Radius,
            Priority = poi.Priority,
            Name = poi.GetName(language),
            Description = poi.GetDescription(language) ?? string.Empty,
            AudioUrl = poi.GetAudioUrl(language),
            ImageUrl = poi.ImageUrl,
            SignatureDish = poi.SignatureDish,
            FunFact = poi.FunFact,
            EstimatedHours = poi.EstimatedHours,
            Type = poi.Type,
            IsActive = poi.IsActive,
            CurrentLanguage = language
        };
    }
}

/// <summary>
/// POI with distance from current location
/// </summary>
public class POIWithDistance : LocalizedPOI
{
    /// <summary>
    /// Khoảng cách từ vị trí hiện tại (mét)
    /// </summary>
    public double Distance { get; set; }
    
    /// <summary>
    /// Có trong phạm vi kích hoạt không
    /// </summary>
    public bool IsInRange => Distance <= Radius;
    
    /// <summary>
    /// Khoảng cách hiển thị (ví dụ: "50m", "1.2km")
    /// </summary>
    public string DistanceFormatted
    {
        get
        {
            if (Distance < 1000)
                return $"{Distance:F0}m";
            else
                return $"{Distance / 1000:F1}km";
        }
    }
}
