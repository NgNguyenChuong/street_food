namespace StreetFoodNarrator.App.Core.Utils;

/// <summary>
/// Tính toán khoảng cách giữa 2 tọa độ GPS
/// </summary>
public static class DistanceCalculator
{
    private const double EARTH_RADIUS_KM = 6371.0;
    
    /// <summary>
    /// Tính khoảng cách Haversine (km)
    /// </summary>
    public static double CalculateDistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        var dLat = DegreesToRadians(lat2 - lat1);
        var dLon = DegreesToRadians(lon2 - lon1);
        
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        
        return EARTH_RADIUS_KM * c;
    }
    
    /// <summary>
    /// Tính khoảng cách (mét)
    /// </summary>
    public static double CalculateDistanceMeters(double lat1, double lon1, double lat2, double lon2)
    {
        return CalculateDistanceKm(lat1, lon1, lat2, lon2) * 1000;
    }
    
    private static double DegreesToRadians(double degrees)
    {
        return degrees * Math.PI / 180.0;
    }
}
