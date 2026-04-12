using System.Globalization;
using StreetFoodNarrator.App.Resources.Strings;

namespace StreetFoodNarrator.App.Core.Services;

/// <summary>
/// Service quản lý ngôn ngữ ứng dụng
/// Hỗ trợ 3 ngôn ngữ chính: Tiếng Việt, English, 中文
/// </summary>
public class LanguageService
{
    private const string LanguagePreferenceKey = "app_language";

    public static event EventHandler<string>? LanguageChanged;

    /// <summary>
    /// Danh sách ngôn ngữ được hỗ trợ
    /// </summary>
    public static readonly List<AppLanguage> SupportedLanguages = new()
    {
        new("vi", "Tiếng Việt", "🇻🇳"),
        new("en", "English",    "🇬🇧"),
        new("zh", "中文",        "🇨🇳"),
    };

    /// <summary>
    /// Ngôn ngữ hiện tại (mặc định: Tiếng Việt)
    /// </summary>
    public string CurrentLanguage => Preferences.Get(LanguagePreferenceKey, "vi");

    /// <summary>
    /// Apply ngôn ngữ vào ứng dụng
    /// </summary>
    public void ApplyLanguage(string languageCode)
    {
        if (!IsSupported(languageCode))
        {
            languageCode = "vi"; // fallback to Vietnamese
        }

        // Lưu preference
        Preferences.Set(LanguagePreferenceKey, languageCode);

        // Apply culture
        var culture = languageCode switch
        {
            "en" => new CultureInfo("en-US"),
            "zh" => new CultureInfo("zh-CN"),
            _ => new CultureInfo("vi-VN")
        };

        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;

        // Update AppStrings
        AppStrings.SetCulture(languageCode);

        LanguageChanged?.Invoke(this, languageCode);
    }

    /// <summary>
    /// Khôi phục ngôn ngữ đã lưu (gọi khi app khởi động)
    /// </summary>
    public void RestoreSavedLanguage()
    {
        ApplyLanguage(CurrentLanguage);
    }

    /// <summary>
    /// Kiểm tra ngôn ngữ có được hỗ trợ không
    /// </summary>
    public bool IsSupported(string languageCode)
    {
        return SupportedLanguages.Any(l => l.Code == languageCode);
    }

    /// <summary>
    /// Lấy thông tin ngôn ngữ hiện tại
    /// </summary>
    public AppLanguage GetCurrentLanguageInfo()
    {
        return SupportedLanguages.FirstOrDefault(l => l.Code == CurrentLanguage)
            ?? SupportedLanguages[0]; // fallback to Vietnamese
    }
}

/// <summary>
/// Model cho ngôn ngữ
/// </summary>
/// <param name="Code">Mã ngôn ngữ (vi, en, zh)</param>
/// <param name="DisplayName">Tên hiển thị</param>
/// <param name="Flag">Emoji cờ</param>
public record AppLanguage(string Code, string DisplayName, string Flag);
