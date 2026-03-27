using System.Globalization;

namespace StreetFoodNarrator.App.Converters;

/// <summary>
/// Converts downloaded status to button color:
/// Downloaded (true) = Green (#22C55E) - can use voice
/// Not downloaded (false) = Orange (#F97316) - needs download
/// </summary>
public class DownloadedToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isDownloaded)
        {
            return isDownloaded
                ? Color.FromArgb("#22C55E")  // Green - already downloaded
                : Color.FromArgb("#F97316"); // Orange - needs download
        }
        return Color.FromArgb("#64748B"); // Default gray
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
