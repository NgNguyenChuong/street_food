using MongoDB.Bson.Serialization.Attributes;

namespace StreetFoodNarrator.API.Models;

/// <summary>
/// GeoJSON Point representation for MongoDB 2dsphere indexing
/// Follows GeoJSON standard: https://datatracker.ietf.org/doc/html/rfc7946
/// </summary>
[BsonIgnoreExtraElements]
public class GeoJsonLocation
{
    /// <summary>
    /// GeoJSON type - always "Point" for POI locations
    /// </summary>
    [BsonElement("type")]
    public string Type { get; set; } = "Point";
    
    /// <summary>
    /// Coordinates array: [Longitude, Latitude]
    /// ⚠️ WARNING: GeoJSON uses [lon, lat] order (opposite of common usage)
    /// </summary>
    [BsonElement("coordinates")]
    public double[] Coordinates { get; set; } = new double[2];
    
    /// <summary>
    /// Helper property to get/set Longitude (index 0)
    /// </summary>
    [BsonIgnore]
    public double Longitude
    {
        get => Coordinates[0];
        set => Coordinates[0] = value;
    }
    
    /// <summary>
    /// Helper property to get/set Latitude (index 1)
    /// </summary>
    [BsonIgnore]
    public double Latitude
    {
        get => Coordinates[1];
        set => Coordinates[1] = value;
    }
    
    /// <summary>
    /// Create GeoJsonLocation from lat/lon
    /// </summary>
    public static GeoJsonLocation FromLatLon(double latitude, double longitude)
    {
        return new GeoJsonLocation
        {
            Coordinates = new[] { longitude, latitude }
        };
    }
    
    /// <summary>
    /// Convert to human-readable string
    /// </summary>
    public override string ToString()
    {
        return $"Point(Lat: {Latitude}, Lon: {Longitude})";
    }
}
