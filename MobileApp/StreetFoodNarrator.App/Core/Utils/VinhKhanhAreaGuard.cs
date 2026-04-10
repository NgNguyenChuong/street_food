namespace StreetFoodNarrator.App.Core.Utils;

using StreetFoodNarrator.App.Core.Models;

/// <summary>
/// Shared geo boundary guard for analytics logging in the Vinh Khanh area.
/// Keeps backend and mobile behavior aligned to the same rectangular scope.
/// </summary>
public static class VinhKhanhAreaGuard
{
    // Same boundary used by map fallback grid: default center +/- 0.01.
    public const double LatitudeMin = AppConfig.DefaultLatitude - 0.01;
    public const double LatitudeMax = AppConfig.DefaultLatitude + 0.01;
    public const double LongitudeMin = AppConfig.DefaultLongitude - 0.01;
    public const double LongitudeMax = AppConfig.DefaultLongitude + 0.01;

    public static bool IsInside(double latitude, double longitude)
    {
        if (!double.IsFinite(latitude) || !double.IsFinite(longitude))
            return false;

        return latitude >= LatitudeMin &&
               latitude <= LatitudeMax &&
               longitude >= LongitudeMin &&
               longitude <= LongitudeMax;
    }

    public static bool IsInside(POI poi)
    {
        return poi != null && IsInside(poi.Latitude, poi.Longitude);
    }
}
