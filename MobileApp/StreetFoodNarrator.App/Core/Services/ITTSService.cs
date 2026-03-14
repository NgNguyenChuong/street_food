using StreetFoodNarrator.App.Core.Models;

namespace StreetFoodNarrator.App.Core.Services;

/// <summary>
/// Interface for Text-to-Speech service
/// </summary>
public interface ITTSService
{
    /// <summary>
    /// Phát text với ngôn ngữ và voice được chỉ định.
    /// Nếu <paramref name="poiId"/> được truyền và file đã cache sẵ ưu tiên phát offline.
    /// </summary>
    /// <param name="poiId">ID POI — dùng để tìm file audio đã tải trước. Để null nếu không biết.</param>
    Task<bool> SpeakAsync(string text, string languageCode, string? voiceName = null,
        int? poiId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Test trực tiếp native TTS fallback (bỏ qua API/server).
    /// Dùng để kiểm tra nhanh trên thiết bị khi nghi ngờ lỗi giọng hoặc mạng.
    /// </summary>
    Task<bool> SpeakNativeFallbackAsync(string text, string languageCode, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Lấy danh sách giọng nói cho ngôn ngữ
    /// </summary>
    Task<List<VoiceInfo>> GetVoicesAsync(string? languageCode = null);
    
    /// <summary>
    /// Dừng phát
    /// </summary>
    Task StopAsync();
    
    /// <summary>
    /// Kiểm tra TTS có sẵn không
    /// </summary>
    bool IsAvailable { get; }
}
