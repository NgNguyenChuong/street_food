using System;
using System.Collections.Generic;

namespace StreetFoodNarrator.API.Services;

public static class TtsVoiceCatalog
{
    private static readonly Dictionary<string, string> DefaultVoices = new(StringComparer.OrdinalIgnoreCase)
    {
        ["vi"] = "vi-VN-HoaiMyNeural",
        ["en"] = "en-US-JennyNeural",
        ["zh"] = "zh-CN-XiaoxiaoNeural"
    };

    private static readonly Dictionary<string, HashSet<string>> AllowedVoices = new(StringComparer.OrdinalIgnoreCase)
    {
        ["vi"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "vi-VN-HoaiMyNeural" },
        ["en"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "en-US-JennyNeural" },
        ["zh"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "zh-CN-XiaoxiaoNeural" }
    };

    public static string NormalizeLanguage(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return "vi";
        }

        var lang = language.Trim().ToLowerInvariant();
        if (lang.StartsWith("vi")) return "vi";
        if (lang.StartsWith("en")) return "en";
        if (lang.StartsWith("zh")) return "zh";
        return "vi";
    }

    public static string GetDefaultVoice(string? language)
    {
        var normalized = NormalizeLanguage(language);
        return DefaultVoices.TryGetValue(normalized, out var voice)
            ? voice
            : DefaultVoices["vi"];
    }

    public static bool IsAllowedVoice(string? voice, string? language)
    {
        if (string.IsNullOrWhiteSpace(voice))
        {
            return false;
        }

        var normalized = NormalizeLanguage(language);
        return AllowedVoices.TryGetValue(normalized, out var allowed) && allowed.Contains(voice);
    }

    public static string GetAllowedVoice(string? voice, string? language)
    {
        return IsAllowedVoice(voice, language) ? voice!.Trim() : GetDefaultVoice(language);
    }

    public static IEnumerable<(string Language, string Voice, string Name, string Gender)> GetFemaleVoices()
    {
        yield return ("vi-VN", "vi-VN-HoaiMyNeural", "Hoai My (Female)", "Female");
        yield return ("en-US", "en-US-JennyNeural", "Jenny (Female)", "Female");
        yield return ("zh-CN", "zh-CN-XiaoxiaoNeural", "Xiaoxiao (Female)", "Female");
    }
}
