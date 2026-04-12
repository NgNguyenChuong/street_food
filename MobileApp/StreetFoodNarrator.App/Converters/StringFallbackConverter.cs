using System.Globalization;

namespace StreetFoodNarrator.App.Converters;

/// <summary>
/// Returns a fallback string when the input is null/empty/whitespace.
/// Use ConverterParameter to provide a custom fallback.
/// </summary>
public sealed class StringFallbackConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string s && !string.IsNullOrWhiteSpace(s))
            return s;

        if (value == null)
            return parameter?.ToString() ?? "Đang cập nhật";

        var text = value.ToString();
        return string.IsNullOrWhiteSpace(text)
            ? (parameter?.ToString() ?? "Đang cập nhật")
            : text!;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
