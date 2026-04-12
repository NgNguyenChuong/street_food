namespace StreetFoodNarrator.App.Core.Models;

/// <summary>
/// Nội dung âm thanh thuyết minh
/// </summary>
public class AudioContent
{
    public int Id { get; set; }
    
    public int POIId { get; set; }
    
    /// <summary>
    /// Mã ngôn ngữ (vi, en, ja, ko)
    /// </summary>
    public string Language { get; set; } = "vi";
    
    /// <summary>
    /// Đường dẫn file audio (local hoặc URL)
    /// </summary>
    public string? FilePath { get; set; }
    
    /// <summary>
    /// Script cho TTS nếu không có file audio
    /// </summary>
    public string? TTSScript { get; set; }
    
    /// <summary>
    /// Loại audio (Prerecorded, TTS)
    /// </summary>
    public AudioType Type { get; set; } = AudioType.TTS;
    
    /// <summary>
    /// Thời lượng (giây)
    /// </summary>
    public int Duration { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    
    /// <summary>
    /// POI liên quan
    /// </summary>
    public POI? POI { get; set; }
}

public enum AudioType
{
    TTS,
    Prerecorded
}
