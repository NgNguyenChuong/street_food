using StreetFoodNarrator.App.Core.Models;
using StreetFoodNarrator.App.Core.Services;
using StreetFoodNarrator.App.Resources.Strings;
using StreetFoodNarrator.App.ViewModels;
using System.Globalization;
using System.Text;

namespace StreetFoodNarrator.App.Views;

public partial class TourDetailPopupPage : ContentPage
{
    private readonly MainViewModel _vm;
    private readonly MainViewModel.TourListItem _tour;

    public TourDetailPopupPage(MainViewModel viewModel, MainViewModel.TourListItem tour)
    {
        InitializeComponent();
        _vm = viewModel;
        _tour = tour;
        BindingContext = BuildVm();
        ApplyLocalizedTexts();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        LanguageService.LanguageChanged -= OnLanguageChanged;
        LanguageService.LanguageChanged += OnLanguageChanged;
    }

    private void OnUnloaded(object? sender, EventArgs e)
    {
        LanguageService.LanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(object? sender, string languageCode)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            BindingContext = BuildVm();
            ApplyLocalizedTexts();
        });
    }

    private void ApplyLocalizedTexts()
    {
        TourExperienceBadgeLabel.Text = AppStrings.Get("PopupTour_Badge");
        DurationTitleLabel.Text = AppStrings.Get("PopupTour_Duration_Estimate");
        TourTypeTitleLabel.Text = AppStrings.Get("PopupTour_Type");
        ThemeTitleLabel.Text = AppStrings.Get("PopupTour_Theme");
        ItineraryTitleLabel.Text = AppStrings.Get("PopupTour_Itinerary");
        SaveTourButton.Text = AppStrings.Get("PopupTour_Save");
        StartNowButton.Text = AppStrings.Get("PopupTour_StartNow");
    }

    private PopupVm BuildVm()
    {
        var tourPois = ResolveTourStops(_tour, _vm.GetTourPoiCatalog(includeTemporarilyClosed: true));
        var stops = tourPois
            .Select((poi, index) => new TourStopItem
            {
                Title = $"{index + 1}. {(string.IsNullOrWhiteSpace(poi.DisplayName) ? AppStrings.Format("PopupTour_Stop_DefaultTitleFormat", index + 1) : poi.DisplayName)}",
                Subtitle = ResolvePoiSubtitle(poi),
                IsTemporarilyClosed = !poi.IsActive,
                ClosedStatusText = "Quán tạm đóng cửa"
            })
            .ToList();

        if (stops.Count == 0)
        {
            stops = Enumerable.Range(1, Math.Max(1, _tour.PoiCount))
                .Select(i => new TourStopItem
                {
                    Title = AppStrings.Format("PopupTour_Stop_DefaultTitleFormat", i),
                    Subtitle = AppStrings.Get("PopupTour_Stop_DefaultSubtitle")
                })
                .ToList();
        }

        var duration = Math.Max(1, _tour.EstimatedDurationMinutes);
        return new PopupVm
        {
            Name = string.IsNullOrWhiteSpace(_tour.Name) ? AppStrings.Get("PopupTour_DefaultName") : _tour.Name,
            CoverImageUrl = ResolveCoverImage(_tour.CoverImageUrl, tourPois),
            DescriptionQuote = string.IsNullOrWhiteSpace(_tour.Description)
                ? AppStrings.Get("PopupTour_DefaultDescription")
                : $"\"{_tour.Description.Trim()}\"",
            DurationText = duration >= 60
                ? $"{duration / 60}h{duration % 60:D2}"
                : $"{duration}p",
            TourTypeText = ResolveTourType(_tour, tourPois),
            ThemeText = ResolveThemeText(tourPois),
            Stops = stops
        };
    }

    private static string ResolveCoverImage(string? coverImageUrl, IReadOnlyList<POI> tourPois)
    {
        if (!string.IsNullOrWhiteSpace(coverImageUrl))
            return coverImageUrl;

        var firstPoiImage = tourPois.FirstOrDefault()?.DisplayImageUrl;
        return string.IsNullOrWhiteSpace(firstPoiImage) ? "welcome_streetfood.jpg" : firstPoiImage;
    }

    private static string ResolvePoiSubtitle(POI poi)
    {
        if (!string.IsNullOrWhiteSpace(poi.FunFact))
            return poi.FunFact.Trim();

        if (!string.IsNullOrWhiteSpace(poi.DisplayDescription))
            return Shorten(poi.DisplayDescription, 95);

        if (!string.IsNullOrWhiteSpace(poi.SignatureDish))
            return AppStrings.Format("PopupTour_SignatureDish_Format", poi.SignatureDish.Trim());

        return AppStrings.Get("PopupTour_DefaultPoiExperience");
    }

    private static string Shorten(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var text = value.Trim().Replace("\r", " ").Replace("\n", " ");
        if (text.Length <= maxLength)
            return text;

        return text[..maxLength].TrimEnd() + "...";
    }

    private static string ResolveTourType(MainViewModel.TourListItem tour, IReadOnlyList<POI> tourPois)
    {
        if (tour.PoiIds.Count > 0 && tourPois.Count > 0)
            return AppStrings.Get("PopupTour_Type_Curated");

        if (tourPois.Count >= 6)
            return AppStrings.Get("PopupTour_Type_Long");

        return AppStrings.Get("PopupTour_Type_Flexible");
    }

    private static string ResolveThemeText(IReadOnlyList<POI> tourPois)
    {
        if (tourPois == null || tourPois.Count == 0)
            return AppStrings.Get("PopupTour_Theme_Default");

        var normalized = tourPois
            .Select(p => !string.IsNullOrWhiteSpace(p.Category)
                ? p.Category!.Trim()
                : (p.Type ?? string.Empty).Trim())
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToList();

        if (normalized.Count == 0)
            return AppStrings.Get("PopupTour_Theme_Default");

        return normalized
            .GroupBy(v => v, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key)
            .Select(g => g.Key)
            .FirstOrDefault() ?? AppStrings.Get("PopupTour_Theme_Default");
    }

    private static List<POI> ResolveTourStops(MainViewModel.TourListItem tour, IEnumerable<POI> allPois)
    {
        var spots = allPois.Where(p => p.ZoneType == "Spot").ToList();
        if (spots.Count == 0)
            return new List<POI>();

        if (tour.PoiIds.Count > 0)
        {
            var byId = spots.ToDictionary(p => p.Id);
            var ordered = new List<POI>();
            foreach (var poiId in tour.PoiIds)
            {
                if (byId.TryGetValue(poiId, out var poi))
                    ordered.Add(poi);
            }

            if (ordered.Count > 0)
                return ordered;
        }

        if (tour.PoiNames.Count > 0)
        {
            var orderedByNames = new List<POI>();
            var seen = new HashSet<int>();
            foreach (var poiName in tour.PoiNames)
            {
                var normalizedName = NormalizeTourPoiName(poiName);
                if (string.IsNullOrWhiteSpace(normalizedName))
                    continue;

                var poi = spots.FirstOrDefault(p =>
                    NormalizeTourPoiName(p.Name_Vi) == normalizedName ||
                    NormalizeTourPoiName(p.Name_En) == normalizedName ||
                    NormalizeTourPoiName(p.Name_Zh) == normalizedName);

                if (poi != null && seen.Add(poi.Id))
                    orderedByNames.Add(poi);
            }

            if (orderedByNames.Count > 0)
                return orderedByNames;
        }

        return spots.Take(Math.Clamp(tour.PoiCount > 0 ? tour.PoiCount : 4, 1, Math.Min(8, spots.Count))).ToList();
    }

    private static string NormalizeTourPoiName(string? rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
            return string.Empty;

        var normalized = rawName.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            var unicode = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (unicode == UnicodeCategory.NonSpacingMark)
                continue;

            if (char.IsLetterOrDigit(ch) || char.IsWhiteSpace(ch))
                builder.Append(ch);
        }

        return string.Join(' ', builder
            .ToString()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private async void OnCloseTapped(object sender, TappedEventArgs e)
    {
        await Navigation.PopModalAsync(false);
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        _vm.ToggleSaveTourCommand.Execute(_tour);
        await Navigation.PopModalAsync(false);
    }

    private async void OnStartNowClicked(object sender, EventArgs e)
    {
        await _vm.StartTourNowCommand.ExecuteAsync(_tour);
        await Navigation.PopModalAsync(false);
    }

    private sealed class PopupVm
    {
        public string Name { get; set; } = string.Empty;
        public string CoverImageUrl { get; set; } = "welcome_streetfood.jpg";
        public string DescriptionQuote { get; set; } = string.Empty;
        public string DurationText { get; set; } = string.Empty;
        public string TourTypeText { get; set; } = string.Empty;
        public string ThemeText { get; set; } = string.Empty;
        public List<TourStopItem> Stops { get; set; } = new();
    }

    private sealed class TourStopItem
    {
        public string Title { get; set; } = string.Empty;
        public string Subtitle { get; set; } = string.Empty;
        public bool IsTemporarilyClosed { get; set; }
        public string ClosedStatusText { get; set; } = string.Empty;
    }
}

