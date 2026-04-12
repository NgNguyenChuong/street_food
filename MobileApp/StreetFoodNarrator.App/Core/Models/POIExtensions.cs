using StreetFoodNarrator.App.Core.Services;

namespace StreetFoodNarrator.App.Core.Models;

/// <summary>
/// Extension methods để lấy nội dung POI theo ngôn ngữ hiện tại
/// </summary>
public static class POIExtensions
{
    /// <summary>
    /// Lấy tên POI theo ngôn ngữ hiện tại
    /// </summary>
    public static string GetName(this POI poi, string languageCode)
    {
        return languageCode switch
        {
            "vi" => poi.Name_Vi,
            "en" => poi.Name_En ?? poi.Name_Vi, // Fallback to Vietnamese
            "zh" => poi.Name_Zh ?? poi.Name_En ?? poi.Name_Vi,
            _ => poi.Name_Vi
        };
    }
    
    /// <summary>
    /// Lấy mô tả POI theo ngôn ngữ hiện tại
    /// </summary>
    public static string GetDescription(this POI poi, string languageCode)
    {
        return languageCode switch
        {
            "vi" => poi.Description_Vi ?? "",
            "en" => poi.Description_En ?? poi.Description_Vi ?? "",
            "zh" => poi.Description_Zh ?? poi.Description_En ?? poi.Description_Vi ?? "",
            _ => poi.Description_Vi ?? ""
        };
    }
    
    /// <summary>
    /// Lấy URL audio theo ngôn ngữ hiện tại
    /// </summary>
    public static string? GetAudioUrl(this POI poi, string languageCode)
    {
        return languageCode switch
        {
            "vi" => poi.AudioUrl_Vi,
            "en" => poi.AudioUrl_En ?? poi.AudioUrl_Vi, // Fallback
            "zh" => poi.AudioUrl_Zh ?? poi.AudioUrl_En ?? poi.AudioUrl_Vi,
            _ => poi.AudioUrl_Vi
        };
    }
    
    /// <summary>
    /// Lấy tên POI theo LanguageService hiện tại
    /// </summary>
    public static string GetLocalizedName(this POI poi, LanguageService languageService)
    {
        return poi.GetName(languageService.CurrentLanguage);
    }
    
    /// <summary>
    /// Lấy mô tả POI theo LanguageService hiện tại
    /// </summary>
    public static string GetLocalizedDescription(this POI poi, LanguageService languageService)
    {
        return poi.GetDescription(languageService.CurrentLanguage) ?? string.Empty;
    }
    
    /// <summary>
    /// Lấy URL audio theo LanguageService hiện tại
    /// </summary>
    public static string? GetLocalizedAudioUrl(this POI poi, LanguageService languageService)
    {
        return poi.GetAudioUrl(languageService.CurrentLanguage);
    }
}
