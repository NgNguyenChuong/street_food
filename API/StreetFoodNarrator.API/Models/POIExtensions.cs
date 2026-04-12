namespace StreetFoodNarrator.API.Models;

/// <summary>
/// Extension methods để lấy nội dung POI theo ngôn ngữ
/// </summary>
public static class POIExtensions
{
    /// <summary>
    /// Lấy tên POI theo ngôn ngữ
    /// </summary>
    public static string GetName(this POI poi, string languageCode)
    {
        return languageCode switch
        {
            "vi" => poi.Name_Vi,
            "en" => poi.Name_En ?? poi.Name_Vi,
            "zh" => poi.Name_Zh ?? poi.Name_En ?? poi.Name_Vi,
            _ => poi.Name_Vi // Default fallback
        };
    }
    
    /// <summary>
    /// Lấy mô tả POI theo ngôn ngữ
    /// </summary>
    public static string GetDescription(this POI poi, string languageCode)
    {
        return languageCode switch
        {
            "vi" => poi.Description_Vi,
            "en" => poi.Description_En ?? poi.Description_Vi,
            "zh" => poi.Description_Zh ?? poi.Description_En ?? poi.Description_Vi,
            _ => poi.Description_Vi
        };
    }
    
    /// <summary>
    /// Lấy URL audio theo ngôn ngữ
    /// </summary>
    public static string? GetAudioUrl(this POI poi, string languageCode)
    {
        return languageCode switch
        {
            "vi" => poi.AudioUrl_Vi,
            "en" => poi.AudioUrl_En ?? poi.AudioUrl_Vi,
            "zh" => poi.AudioUrl_Zh ?? poi.AudioUrl_En ?? poi.AudioUrl_Vi,
            _ => poi.AudioUrl_Vi
        };
    }
}

/// <summary>
/// DTO để trả về POI với nội dung đã localized
/// </summary>
public class LocalizedPOIResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? AudioUrl { get; set; }
    public string? ImageUrl { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int Radius { get; set; }
    public string ZoneType { get; set; } = "Spot";
    public int ZoneLevel { get; set; }
    public int Priority { get; set; }
    public string Language { get; set; } = "vi";
    
    public static LocalizedPOIResponse FromPOI(POI poi, string languageCode)
    {
        return new LocalizedPOIResponse
        {
            Id = poi.POI_ID,
            Name = poi.GetName(languageCode),
            Description = poi.GetDescription(languageCode),
            AudioUrl = poi.GetAudioUrl(languageCode),
            ImageUrl = poi.ImageUrl,
            Latitude = poi.Location?.Latitude ?? 0,
            Longitude = poi.Location?.Longitude ?? 0,
            Radius = poi.TriggerRadius,
            ZoneType = poi.ZoneType ?? "Spot",
            ZoneLevel = poi.ZoneLevel,
            Priority = poi.Priority,
            Language = languageCode
        };
    }
}
