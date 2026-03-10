namespace StreetFoodNarrator.App.Core.Services;

/// <summary>
/// Service tải trước và lưu trữ file audio MP3 offline cho từng POI.
/// Ưu tiên phát file đã cache; chỉ gọi backend khi chưa có.
/// </summary>
public interface IAudioCacheService
{
    /// <summary>
    /// Tải trước toàn bộ file audio (status=published) cho danh sách POI ID.
    /// Chạy background sau khi app khởi động xong.
    /// </summary>
    /// <param name="poiIds">Danh sách ID các POI cần tải.</param>
    /// <param name="progress">Callback tiến trình (done, total).</param>
    /// <param name="ct">Token huỷ.</param>
    Task PreloadAllAsync(
        IEnumerable<int> poiIds,
        IProgress<(int done, int total)>? progress = null,
        CancellationToken ct = default);

    /// <summary>
    /// Trả về Stream đọc file đã cache, hoặc <c>null</c> nếu chưa có.
    /// Caller chịu trách nhiệm Dispose stream.
    /// </summary>
    Task<Stream?> GetCachedStreamAsync(int poiId, string language);

    /// <summary>
    /// Kiểm tra nhanh xem file đã được cache chưa (không cần async).
    /// </summary>
    bool IsCached(int poiId, string language);

    /// <summary>Tổng dung lượng cache hiện tại (bytes).</summary>
    long GetCacheSizeBytes();

    /// <summary>Xoá toàn bộ file audio đã cache.</summary>
    Task ClearAsync();
    /// <summary>
    /// Kiểm tra số lượng audio mới (status=published) chưa được cache.
    /// Yêu cầu có internet. Trả về 0 nếu offline hoặc không có gì mới.
    /// </summary>
    Task<int> CheckForUpdatesAsync(CancellationToken ct = default);}
