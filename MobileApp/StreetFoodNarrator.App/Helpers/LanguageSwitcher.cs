namespace StreetFoodNarrator.App.Helpers;

using System;
using System.Linq;
using Microsoft.Maui.Controls;
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

        var currentIndex = LanguageService.SupportedLanguages.FindIndex(l => l.Code == current);
        if (currentIndex < 0)
            currentIndex = 0;

        var options = LanguageService.SupportedLanguages
            .Select((lang, idx) =>
                $"{(idx == currentIndex ? "* " : string.Empty)}{GetHeaderLabel(lang.Code)} - {lang.DisplayName}")
            .ToList();

        var selectedIndex = await CustomAlert.ShowSelectionAsync(
            title: GetPickerTitle(current),
            options: options,
            selectedIndex: currentIndex,
            cancelText: GetCancelText(current),
            hostPage: page);

        if (!selectedIndex.HasValue)
            return current;

        var chosen = LanguageService.SupportedLanguages[selectedIndex.Value].Code;
        if (!string.Equals(chosen, current, StringComparison.OrdinalIgnoreCase))
            languageService.ApplyLanguage(chosen);

        return chosen;
    }

    private static string GetPickerTitle(string currentLanguageCode)
        => currentLanguageCode switch
        {
            "en" => "Choose language",
            "zh" => "选择语言",
            _ => "Chọn ngôn ngữ"
        };

    private static string GetCancelText(string currentLanguageCode)
        => currentLanguageCode switch
        {
            "en" => "Cancel",
            "zh" => "取消",
            _ => "Hủy"
        };
}
