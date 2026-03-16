using System.Globalization;

namespace StreetFoodNarrator.App.Converters;

/// <summary>
/// Inverts boolean values for binding (true -> false, false -> true)
/// </summary>
public class InvertedBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }
        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }
        return false;
    }
}

/// <summary>Converts ZoneType string to a display Color.</summary>
public class ZoneTypeToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var str = value?.ToString() ?? "";

        // If converting a bool (IsTracking)
        if (value is bool b)
            return b ? Color.FromArgb("#22C55E") : Color.FromArgb("#64748B");

        return str switch
        {
            "Area"     => Color.FromArgb("#22C55E"),
            "District" => Color.FromArgb("#10B981"),
            "Spot"     => Color.FromArgb("#84CC16"),
            _          => Color.FromArgb("#64748B")
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>Converts IsTracking bool to GPS icon emoji.</summary>
public class BoolToTrackingLabelConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? "📍" : "📡";

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>Converts Gender string to emoji icon.</summary>
public class GenderToEmojiConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var gender = value?.ToString() ?? "";
        return gender switch
        {
            "Female" => "👩",
            "Male" => "👨",
            _ => "🎤"
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
