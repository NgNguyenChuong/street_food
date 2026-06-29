using System.Collections.Concurrent;

namespace StreetFoodNarrator.API.Services;

/// <summary>
/// Quản lý hàng đợi phát thuyết minh khi nhiều user cùng vào một POI.
///
/// Tại sao cần queue?
///   - TTS audio được cache sẵn trên server, nên phát trên client là độc lập (mỗi điện thoại tự phát).
///   - Không cần queue cho audio playback.
///   - Tuy nhiên, nếu nhiều user CÙNG LÚC trigger TTS generation cho cùng POI + ngôn ngữ
///     thì server sẽ tạo N bản giống nhau → lãng phí CPU/API.
///
/// Giải pháp:
///   - Dùng SemaphoreSlim(1,1) theo key "poiId:language" để dedup TTS generation.
///   - Kết quả (audio URL) được cache sau lần đầu; các request sau lấy từ cache.
///   - Hàng đợi chỉ block ở bước GENERATION, không block bước PLAYBACK.
///

public sealed class NarrationQueueService
{
    // Số request TTS generation đồng thời tối đa — bảo vệ TTS API rate limit.
    private const int MaxConcurrentGenerations = 3;

    // Giữ tối đa N slot chờ per POI để tránh memory leak khi bị flood.
    private const int MaxQueueDepthPerPoi = 20;

    // Thời gian một generation task được phép chạy trước khi timeout.
    private static readonly TimeSpan GenerationTimeout = TimeSpan.FromSeconds(30);

    // Global semaphore: giới hạn concurrent TTS calls đến provider bên ngoài.
    private readonly SemaphoreSlim _globalSlot = new(MaxConcurrentGenerations, MaxConcurrentGenerations);

    // Per-POI-Language lock: đảm bảo chỉ 1 lần generate cho mỗi (poiId, language).
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _perPoiLocks = new();

    // Simple in-memory cache: key = "poiId:language" → audioUrl.
    // Trong production nên dùng IMemoryCache / Redis với TTL.
    private readonly ConcurrentDictionary<string, string> _audioUrlCache = new();

    // Số user đang chờ theo từng POI (dùng cho monitoring / dashboard).
    private readonly ConcurrentDictionary<string, int> _waitingCounts = new();

    /// <summary>
    /// Lấy audio URL cho POI, generate nếu chưa có cache.
    /// Nhiều caller cùng key sẽ đợi lần generate đầu tiên hoàn thành.
    /// </summary>
    /// <param name="poiId">ID của POI.</param>
    /// <param name="language">Ngôn ngữ: "vi-VN", "en-US", "zh-CN".</param>
    /// <param name="generateAudioAsync">Factory tạo audio nếu cache miss. Trả về URL trên server.</param>
    /// <param name="cancellationToken">CancellationToken từ HTTP request.</param>
    /// <returns>Audio URL đã được serve, hoặc null nếu generate thất bại / queue đầy.</returns>
    public async Task<string?> GetOrGenerateAudioAsync(
        int poiId,
        string language,
        Func<Task<string?>> generateAudioAsync,
        CancellationToken cancellationToken = default)
    {
        var key = $"{poiId}:{language}";

        // Cache hit — trả về ngay, không lock.
        if (_audioUrlCache.TryGetValue(key, out var cached))
            return cached;

        // Lấy per-POI semaphore, tạo mới nếu chưa có.
        var perPoiLock = _perPoiLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));

        // Kiểm tra queue depth để tránh OOM khi bị flood.
        var waiting = _waitingCounts.AddOrUpdate(key, 1, (_, old) => old + 1);
        if (waiting > MaxQueueDepthPerPoi)
        {
            _waitingCounts.AddOrUpdate(key, 0, (_, old) => Math.Max(0, old - 1));
            return null; // Từ chối: quá nhiều người đang chờ.
        }

        try
        {
            // Đợi lượt — timeout để tránh bị block mãi.
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(GenerationTimeout);

            var entered = await perPoiLock.WaitAsync(GenerationTimeout, cts.Token);
            if (!entered)
                return null;

            try
            {
                // Double-check sau khi vào lock: có thể một request trước vừa điền cache.
                if (_audioUrlCache.TryGetValue(key, out var doubleChecked))
                    return doubleChecked;

                // Lấy global slot trước khi gọi TTS provider.
                await _globalSlot.WaitAsync(GenerationTimeout, cts.Token);
                try
                {
                    var audioUrl = await generateAudioAsync();
                    if (!string.IsNullOrWhiteSpace(audioUrl))
                        _audioUrlCache[key] = audioUrl;
                    return audioUrl;
                }
                finally
                {
                    _globalSlot.Release();
                }
            }
            finally
            {
                perPoiLock.Release();
            }
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        finally
        {
            _waitingCounts.AddOrUpdate(key, 0, (_, old) => Math.Max(0, old - 1));
        }
    }

    /// <summary>
    /// Xóa cache của một POI (dùng khi admin cập nhật script/audio).
    /// </summary>
    public void InvalidateCache(int poiId, string? language = null)
    {
        if (language != null)
        {
            _audioUrlCache.TryRemove($"{poiId}:{language}", out _);
            return;
        }

        var prefix = $"{poiId}:";
        foreach (var key in _audioUrlCache.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)))
            _audioUrlCache.TryRemove(key, out _);
    }

    /// <summary>
    /// Trả về số request đang chờ cho từng POI (dùng cho admin monitoring).
    /// </summary>
    public IReadOnlyDictionary<string, int> GetWaitingCounts()
        => _waitingCounts;
}
