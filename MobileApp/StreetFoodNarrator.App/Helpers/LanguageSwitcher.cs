namespace StreetFoodNarrator.App.Helpers;

using StreetFoodNarrator.App.Core.Services;

/// <summary>
/// Shared helper for compact header language switch buttons.
/// </summary>
public static class LanguageSwitcher
{
    private static readonly string[] LanguageOrder = ["vi", "en", "zh"];

    public static string GetNextLanguageCode(string? currentLanguage)
    {
        var normalized = (currentLanguage ?? "vi").Trim().ToLowerInvariant();
        var idx = Array.IndexOf(LanguageOrder, normalized);
        if (idx < 0)
            return LanguageOrder[0];

        return LanguageOrder[(idx + 1) % LanguageOrder.Length];
    }

    public static string GetHeaderLabel(string? languageCode)
        => (languageCode ?? "vi").Trim().ToLowerInvariant() switch
        {
            "en" => "EN",
            "zh" => "中",
            _ => "VI"
        };

    public static string CycleLanguage(LanguageService languageService)
    {
        var next = GetNextLanguageCode(languageService.CurrentLanguage);
        languageService.ApplyLanguage(next);
        return next;
    }

    public static async Task<string> ShowLanguagePickerAsync(Page page, LanguageService languageService)
    {
        var current = (languageService.CurrentLanguage ?? "vi").Trim().ToLowerInvariant();
        var items = LanguageService.SupportedLanguages
            .Select(lang => new
            {
                lang.Code,
                Option = $"{(lang.Code == current ? "* " : string.Empty)}{GetHeaderLabel(lang.Code)} - {lang.DisplayName}"
            })
            .ToList();

        var selected = await page.DisplayActionSheet(
            "Chon ngon ngu",
            "Huy",
            null,
            items.Select(i => i.Option).ToArray());

        if (string.IsNullOrWhiteSpace(selected) || selected == "Huy")
            return current;

        var chosen = items.FirstOrDefault(i => i.Option == selected)?.Code ?? current;
        if (!string.Equals(chosen, current, StringComparison.OrdinalIgnoreCase))
            languageService.ApplyLanguage(chosen);

        return chosen;
    }
}
