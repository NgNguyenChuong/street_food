namespace StreetFoodNarrator.App.Core.Utils;

/// <summary>
/// Supported language codes
/// Based on FlavorQuest multi-language support
/// </summary>
public static class Languages
{
    public const string Vietnamese = "vi";
    public const string English = "en";
    public const string Japanese = "ja";
    public const string French = "fr";
    public const string Korean = "ko";
    public const string Chinese = "zh";
    
    /// <summary>
    /// All supported languages
    /// </summary>
    public static readonly string[] All = { Vietnamese, English, Japanese, French, Korean, Chinese };
    
    /// <summary>
    /// Get language display name
    /// </summary>
    public static string GetDisplayName(string languageCode)
    {
        return languageCode.ToLower() switch
        {
            Vietnamese => "Tiếng Việt",
            English => "English",
            Japanese => "日本語",
            French => "Français",
            Korean => "한국어",
            Chinese => "中文",
            _ => languageCode
        };
    }
    
    /// <summary>
    /// Get language flag emoji
    /// </summary>
    public static string GetFlag(string languageCode)
    {
        return languageCode.ToLower() switch
        {
            Vietnamese => "🇻🇳",
            English => "🇬🇧",
            Japanese => "🇯🇵",
            French => "🇫🇷",
            Korean => "🇰🇷",
            Chinese => "🇨🇳",
            _ => "🌐"
        };
    }
    
    /// <summary>
    /// Get TTS language code (for text-to-speech)
    /// </summary>
    public static string GetTTSCode(string languageCode)
    {
        return languageCode.ToLower() switch
        {
            Vietnamese => "vi-VN",
            English => "en-US",
            Japanese => "ja-JP",
            French => "fr-FR",
            Korean => "ko-KR",
            Chinese => "zh-CN",
            _ => "vi-VN"
        };
    }
}
