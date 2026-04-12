namespace StreetFoodNarrator.App.Core.Models;

/// <summary>
/// Vị trí người dùng
/// </summary>
public class UserLocation
{
    public double Latitude { get; set; }
    
    public double Longitude { get; set; }
    
    /// <summary>
    /// Độ chính xác (mét)
    /// </summary>
    public double Accuracy { get; set; }
    
    /// <summary>
    /// Độ cao (mét)
    /// </summary>
    public double? Altitude { get; set; }
    
    /// <summary>
    /// Hướng di chuyển (độ)
    /// </summary>
    public double? Heading { get; set; }
    
    /// <summary>
    /// Tốc độ (m/s)
    /// </summary>
    public double? Speed { get; set; }
    
    public DateTime Timestamp { get; set; } = DateTime.Now;
}
