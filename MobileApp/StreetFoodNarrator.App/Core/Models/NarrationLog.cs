namespace StreetFoodNarrator.App.Core.Models;

/// <summary>
/// Log lịch sử thuyết minh
/// </summary>
public class NarrationLog
{
    public int Id { get; set; }
    
    public string? UserId { get; set; }
    
    public int POIId { get; set; }
    
    public DateTime Timestamp { get; set; } = DateTime.Now;
    
    /// <summary>
    /// Thời lượng nghe (giây)
    /// </summary>
    public int Duration { get; set; }
    
    public string Language { get; set; } = "vi";
    
    /// <summary>
    /// Vị trí khi phát thuyết minh
    /// </summary>
    public double? LocationLatitude { get; set; }
    public double? LocationLongitude { get; set; }
    
    /// <summary>
    /// Kiểu kích hoạt (Geofence, QRCode, Manual)
    /// </summary>
    public TriggerType TriggerType { get; set; }
    
    public POI? POI { get; set; }
}

public enum TriggerType
{
    Geofence,
    QRCode,
    Manual
}
