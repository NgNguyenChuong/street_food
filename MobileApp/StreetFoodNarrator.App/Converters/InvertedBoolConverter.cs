using System.Globalization;
using Color = Microsoft.Maui.Graphics.Color;

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

/// <summary>
/// Converts Journal queue POI to a walking time string (e.g. "8 phút").
/// Parameter: "prevLat,prevLon" — comma-separated lat/lon of the previous stop.
/// </summary>
public class JournalQueueDurationConverter : IValueConverter
{
    // Shared user location for calculating queue item distances
    private static double _userLat;
    private static double _userLon;

    public static void UpdateUserLocation(double lat, double lon)
    {
        _userLat = lat;
        _userLon = lon;
    }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not StreetFoodNarrator.App.Core.Models.POI poi)
            return "—";

        // Calculate distance from user to this POI (Haversine)
        var dist = HaversineDistance(_userLat, _userLon, poi.Latitude, poi.Longitude);
        var walkingMin = Math.Max(1, (int)Math.Ceiling(dist / 80.0)); // 80m/min walking speed
        return $"{walkingMin} phút";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();

    private static double HaversineDistance(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371000; // metres
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLon = (lon2 - lon1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180)
              * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
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
