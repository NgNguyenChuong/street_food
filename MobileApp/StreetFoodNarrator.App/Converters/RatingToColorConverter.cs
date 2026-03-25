using System.Globalization;

namespace StreetFoodNarrator.App.Converters;

public class RatingToColorConverter : IValueConverter
{
    public Color ActiveColor { get; set; } = Color.FromArgb("#4BE277");
    public Color InactiveColor { get; set; } = Color.FromArgb("#2B3733");

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int rating && parameter is string paramStr && int.TryParse(paramStr, out int starIndex))
        {
            return rating >= starIndex ? ActiveColor : InactiveColor;
        }
        return InactiveColor;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
