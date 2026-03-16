using System.Globalization;

namespace StreetFoodNarrator.App.Converters;

/// <summary>
/// Converts a bool to a Color.
/// ConverterParameter format: "TrueHex:FalseHex"  e.g. "#EF4444:#FFFFFF"
/// Defaults: true=Red, false=White
/// </summary>
public sealed class BoolToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isTrue = value is bool b && b;
        if (parameter is string param)
        {
            var idx = param.IndexOf(':');
            if (idx > 0)
            {
                var colorStr = isTrue ? param[..idx] : param[(idx + 1)..];
                return Color.FromArgb(colorStr);
            }
        }
        return isTrue ? Color.FromArgb("#EF4444") : Color.FromArgb("#FFFFFF");
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
