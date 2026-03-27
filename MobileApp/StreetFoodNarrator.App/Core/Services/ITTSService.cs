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
    /// Phát text với voice package đã tải về (offline).
    /// Nếu voice package chưa có, fallback về native TTS.
    /// </summary>
    Task<bool> SpeakWithVoicePackageAsync(string text, string languageCode, string voiceName,
        CancellationToken cancellationToken = default);
    
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

    /// <summary>Lấy thời lượng audio hiện tại (giây). 0 nếu chưa phát.</summary>
    double GetDuration();

    /// <summary>Lấy vị trí phát hiện tại (giây). 0 nếu chưa phát.</summary>
    double GetCurrentPosition();

    /// <summary>Đặt âm lượng phát (0.0–1.0).</summary>
    void SetVolume(double volume);

    /// <summary>Đặt tốc độ phát (ví dụ: 0.75, 1.0, 1.25, 1.5).</summary>
    void SetSpeed(double speed);

    /// <summary>Seek đến vị trí cụ thể (giây).</summary>
    void Seek(double positionSeconds);

    /// <summary>Tạm dừng audio đang phát.</summary>
    void Pause();

    /// <summary>Tiếp tục phát audio đang tạm dừng.</summary>
    void Resume();

    /// <summary>Kiểm tra audio có đang phát không.</summary>
    bool IsPlaying();

    /// <summary>Ẩn khi audio phát xong. Không fire khi gọi StopAsync.</summary>
    event Action? OnPlaybackEnded;
}
