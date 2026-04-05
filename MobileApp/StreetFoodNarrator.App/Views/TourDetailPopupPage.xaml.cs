using StreetFoodNarrator.App.Core.Models;
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
    }

    private PopupVm BuildVm()
    {
        var tourPois = ResolveTourStops(_tour, _vm.AllPOIs);
        var stops = tourPois
            .Select((poi, index) => new TourStopItem
            {
                Title = $"{index + 1}. {(string.IsNullOrWhiteSpace(poi.DisplayName) ? $"Điểm dừng {index + 1}" : poi.DisplayName)}",
                Subtitle = ResolvePoiSubtitle(poi)
            })
            .ToList();

        if (stops.Count == 0)
        {
            stops = Enumerable.Range(1, Math.Max(1, _tour.PoiCount))
                .Select(i => new TourStopItem
                {
                    Title = $"Điểm dừng {i}",
                    Subtitle = "Thông tin điểm dừng sẽ được cập nhật khi đồng bộ dữ liệu."
                })
                .ToList();
        }

        var duration = Math.Max(1, _tour.EstimatedDurationMinutes);
        return new PopupVm
        {
            Name = string.IsNullOrWhiteSpace(_tour.Name) ? "Tour ẩm thực" : _tour.Name,
            CoverImageUrl = ResolveCoverImage(_tour.CoverImageUrl, tourPois),
            DescriptionQuote = string.IsNullOrWhiteSpace(_tour.Description)
                ? "Khám phá ẩm thực theo nhịp sống đường phố, với các điểm dừng được chọn sẵn cho bạn."
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
            return $"Món nổi bật: {poi.SignatureDish.Trim()}";

        return "Điểm dừng ẩm thực với nhiều trải nghiệm đặc trưng.";
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
            return "Chọn sẵn";

        if (tourPois.Count >= 6)
            return "Khám phá dài";

        return "Linh hoạt";
    }

    private static string ResolveThemeText(IReadOnlyList<POI> tourPois)
    {
        if (tourPois == null || tourPois.Count == 0)
            return "Ẩm thực";

        var normalized = tourPois
            .Select(p => !string.IsNullOrWhiteSpace(p.Category)
                ? p.Category!.Trim()
                : (p.Type ?? string.Empty).Trim())
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToList();

        if (normalized.Count == 0)
            return "Ẩm thực";

        return normalized
            .GroupBy(v => v, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key)
            .Select(g => g.Key)
            .FirstOrDefault() ?? "Ẩm thực";
    }

    private static List<POI> ResolveTourStops(MainViewModel.TourListItem tour, IEnumerable<POI> allPois)
    {
        var activeSpots = allPois.Where(p => p.ZoneType == "Spot" && p.IsActive).ToList();
        if (activeSpots.Count == 0)
            return new List<POI>();

        if (tour.PoiIds.Count > 0)
        {
            var byId = activeSpots.ToDictionary(p => p.Id);
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

                var poi = activeSpots.FirstOrDefault(p =>
                    NormalizeTourPoiName(p.Name_Vi) == normalizedName ||
                    NormalizeTourPoiName(p.Name_En) == normalizedName ||
                    NormalizeTourPoiName(p.Name_Zh) == normalizedName);

                if (poi != null && seen.Add(poi.Id))
                    orderedByNames.Add(poi);
            }

            if (orderedByNames.Count > 0)
                return orderedByNames;
        }

        return activeSpots.Take(Math.Clamp(tour.PoiCount > 0 ? tour.PoiCount : 4, 1, Math.Min(8, activeSpots.Count))).ToList();
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
    }
}

