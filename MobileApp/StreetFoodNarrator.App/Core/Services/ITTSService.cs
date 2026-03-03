using StreetFoodNarrator.App.Core.Models;

namespace StreetFoodNarrator.App.Core.Services;

/// <summary>
/// Interface for Text-to-Speech service
/// </summary>
public interface ITTSService
{
    /// <summary>
    /// Phát demo text với giọng đọc được chọn
    /// </summary>
    Task<bool> SpeakAsync(string text, string languageCode, string? voiceName = null, CancellationToken cancellationToken = default);
    
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
