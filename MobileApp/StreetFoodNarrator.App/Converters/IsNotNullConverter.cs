using System.Globalization;

namespace StreetFoodNarrator.App.Converters;

/// <summary>
/// Converter để check object có null không (dùng cho IsVisible binding)
/// </summary>
public class IsNotNullConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value != null;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
