using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace StreetFoodNarrator.App.Core.Services.Implementations;

/// <summary>
/// Tải trước và lưu file audio MP3 offline cho từng POI.
/// File được lưu tại: AppDataDirectory/audio_cache/{poiId}_{lang}.mp3
/// </summary>
public class AudioCacheService : IAudioCacheService
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private readonly string _cacheDir;

    // Bộ nhớ trong — tránh gọi File.Exists() liên tục trên UI thread
    private readonly HashSet<string> _cachedKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _lock = new(1, 1);

    public AudioCacheService(HttpClient httpClient)
    {
        _http    = httpClient;
        _baseUrl = AppConfig.GetResolvedApiBaseUrl().TrimEnd('/');
        _cacheDir = Path.Combine(FileSystem.AppDataDirectory, "audio_cache");
        Directory.CreateDirectory(_cacheDir);

        // Lập chỉ mục file đã tồn tại lúc khởi động
        foreach (var file in Directory.EnumerateFiles(_cacheDir, "*.mp3"))
            _cachedKeys.Add(Path.GetFileNameWithoutExtension(file));
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task PreloadAllAsync(
        IEnumerable<int> poiIds,
        IProgress<(int done, int total)>? progress = null,
        CancellationToken ct = default)
    {
        var ids   = poiIds.Distinct().ToList();
        int done  = 0;
        int timeoutFailures = 0;

        foreach (var poiId in ids)
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                await DownloadAudioForPoiAsync(poiId, ct);
                timeoutFailures = 0;
            }
            catch (TaskCanceledException ex)
            {
                timeoutFailures++;
                Debug.WriteLine($"[AudioCache] Timeout preload POI {poiId}: {ex.Message}");
                if (timeoutFailures >= 3)
                {
                    Debug.WriteLine("[AudioCache] Quá nhiều timeout liên tiếp, dừng preload để tránh treo UX.");
                    break;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AudioCache] Bỏ qua POI {poiId}: {ex.Message}");
            }
            progress?.Report((++done, ids.Count));
        }

        Debug.WriteLine($"[AudioCache] Preload xong: {done}/{ids.Count} POI, cache={GetCacheSizeBytes() / 1024} KB");
    }

    /// <inheritdoc/>
    public Task<Stream?> GetCachedStreamAsync(int poiId, string language)
    {
        var key  = CacheKey(poiId, language);
        var path = CachePath(key);
        if (File.Exists(path))
            return Task.FromResult<Stream?>(File.OpenRead(path));
        return Task.FromResult<Stream?>(null);
    }

    /// <inheritdoc/>
    public async Task<Stream?> GetOrDownloadCachedStreamAsync(int poiId, string language, CancellationToken ct = default)
    {
        var key = CacheKey(poiId, language);
        var path = CachePath(key);

        if (File.Exists(path))
            return File.OpenRead(path);

        // Không có mạng thì không thể tải về mới.
        if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
        {
            Debug.WriteLine($"[AudioCache] Offline: bỏ qua download POI {poiId} ({language})");
            return null;
        }

        var audioUrl = await GetAudioUrlAsync(poiId, language, ct);
        if (string.IsNullOrWhiteSpace(audioUrl))
            return null;

        await DownloadAndSaveAsync(audioUrl, key, ct);

        return File.Exists(path) ? File.OpenRead(path) : null;
    }

    /// <inheritdoc/>
    public bool IsCached(int poiId, string language)
        => _cachedKeys.Contains(CacheKey(poiId, language));

    /// <inheritdoc/>
    public long GetCacheSizeBytes()
    {
        try
        {
            return Directory.EnumerateFiles(_cacheDir, "*.mp3")
                            .Sum(f => new FileInfo(f).Length);
        }
        catch { return 0; }
    }

    /// <inheritdoc/>
    public async Task ClearAsync()
    {
        await _lock.WaitAsync();
        try
        {
            foreach (var file in Directory.EnumerateFiles(_cacheDir, "*.mp3"))
                File.Delete(file);
            _cachedKeys.Clear();
            Debug.WriteLine("[AudioCache] Đã xoá toàn bộ cache audio.");
        }
        finally { _lock.Release(); }
    }

    /// <inheritdoc/>
    public async Task<int> CheckForUpdatesAsync(CancellationToken ct = default)
    {
        try
        {
            // Lấy tất cả audio đã published
            var url = $"{_baseUrl}/api/audio?status=published&pageSize=500";
            var result = await _http.GetFromJsonAsync<AudioListResult>(url, ct);
            if (result?.Data == null) return 0;

            int newCount = 0;
            foreach (var audio in result.Data)
            {
                if (string.IsNullOrWhiteSpace(audio.AudioUrl)) continue;

                var lang = NormalizeLanguage(audio.Language);

                // ⚠️ File cache được lưu theo poiId_lang.mp3, không phải AudioContentId_lang.mp3
                // Nên phải dùng PoiId để build cache key cho đúng.
                if (audio.PoiId <= 0) continue;

                var key = CacheKey(audio.PoiId, lang);
                if (!_cachedKeys.Contains(key))
                {
                    // Kiểm tra thêm bằng File.Exists để đảm bảo
                    var path = CachePath(key);
                    if (!File.Exists(path))
                        newCount++;
                    else
                        _cachedKeys.Add(key); // Cập nhật lại in-memory set nếu file đã có
                }
            }

            Debug.WriteLine($"[AudioCache] Kiểm tra cập nhật: {newCount} audio mới chưa tải.");
            return newCount;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AudioCache] CheckForUpdates lỗi: {ex.Message}");
            return 0;
        }
    }

    /// <inheritdoc/>
    public async Task<string?> GetAudioUrlAsync(int poiId, string language, CancellationToken ct = default)
    {
        var normalizedLang = language.ToLowerInvariant() switch
        {
            "vi" or "vi-vn" => "vi-VN",
            "en" or "en-us" => "en-US",
            "zh" or "zh-cn" => "zh-CN",
            _ => language
        };

        try
        {
            // Call endpoint mới để lấy chính xác 1 audio
            var url = $"{_baseUrl}/api/audio/poi/{poiId}/{normalizedLang}";
            var audio = await _http.GetFromJsonAsync<AudioDto>(url, ct);

            if (audio?.AudioUrl == null)
                return null;

            // Convert relative path to absolute URL
            var audioUrl = audio.AudioUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? audio.AudioUrl
                : $"{_baseUrl}{audio.AudioUrl}";

            Debug.WriteLine($"[AudioCache] 🌐 URL online: POI {poiId} ({normalizedLang}) → {audioUrl}");
            return audioUrl;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            Debug.WriteLine($"[AudioCache] POI {poiId} không có audio published {normalizedLang}, thử fallback từ POI.AudioUrl_*");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AudioCache] Lỗi lấy URL audio published: {ex.Message}");
        }

        // Fallback: lấy URL audio trực tiếp từ POI (AudioUrl_Vi/En/Zh)
        var poiAudioUrl = await TryGetPoiAudioUrlAsync(poiId, normalizedLang, ct);
        if (!string.IsNullOrWhiteSpace(poiAudioUrl))
        {
            Debug.WriteLine($"[AudioCache] 🌐 URL fallback từ POI {poiId} ({normalizedLang}) → {poiAudioUrl}");
            return poiAudioUrl;
        }

        return null;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task DownloadAudioForPoiAsync(int poiId, CancellationToken ct)
    {
        // Thử tải audio cho các ngôn ngữ chính (vi, en, zh)
        var languages = new[] { "vi-VN", "en-US", "zh-CN" };
        
        foreach (var lang in languages)
        {
            try
            {
                // Dùng endpoint mới để lấy chính xác 1 audio cho POI + Language
                var url = $"{_baseUrl}/api/audio/poi/{poiId}/{lang}";
                var audio = await _http.GetFromJsonAsync<AudioDto>(url, ct);
                
                if (audio == null || string.IsNullOrWhiteSpace(audio.AudioUrl))
                    continue;

                var normalizedLang = NormalizeLanguage(lang);
                var key = CacheKey(poiId, normalizedLang);
                
                // Download nếu chưa có trong cache
                if (!_cachedKeys.Contains(key))
                {
                    await DownloadAndSaveAsync(audio.AudioUrl, key, ct);
                    Debug.WriteLine($"[AudioCache] ✅ Tải về POI {poiId} ({normalizedLang})");
                }
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // Bình thường - POI này không có audio cho ngôn ngữ này
                Debug.WriteLine($"[AudioCache] POI {poiId} không có audio {lang}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AudioCache] Lỗi tải POI {poiId} ({lang}): {ex.Message}");
            }
        }
    }

    private async Task DownloadAndSaveAsync(string audioUrl, string key, CancellationToken ct)
    {
        var fileUrl = audioUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? audioUrl
            : $"{_baseUrl}{audioUrl}";

        var bytes = await _http.GetByteArrayAsync(fileUrl, ct);
        var path = CachePath(key);

        await _lock.WaitAsync(ct);
        try
        {
            await File.WriteAllBytesAsync(path, bytes, ct);
            _cachedKeys.Add(key);
            Debug.WriteLine($"[AudioCache] ✓ Đã lưu {key}.mp3 ({bytes.Length / 1024} KB)");
        }
        finally { _lock.Release(); }
    }

    private async Task<string?> TryGetPoiAudioUrlAsync(int poiId, string language, CancellationToken ct)
    {
        try
        {
            var url = $"{_baseUrl}/api/POIs/{poiId}";
            using var response = await _http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
                return null;

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await System.Text.Json.JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var root = doc.RootElement;

            string? vi = GetJsonString(root, "audioUrl_Vi", "audioUrlVi", "AudioUrl_Vi", "AudioUrlVi");
            string? en = GetJsonString(root, "audioUrl_En", "audioUrlEn", "AudioUrl_En", "AudioUrlEn");
            string? zh = GetJsonString(root, "audioUrl_Zh", "audioUrlZh", "AudioUrl_Zh", "AudioUrlZh");

            var selected = language.ToLowerInvariant() switch
            {
                "en-us" => en ?? vi,
                "zh-cn" => zh ?? en ?? vi,
                _ => vi
            };

            if (string.IsNullOrWhiteSpace(selected))
                return null;

            return selected.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? selected
                : $"{_baseUrl}{selected}";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AudioCache] Fallback POI audio lỗi: {ex.Message}");
            return null;
        }
    }

    private static string? GetJsonString(System.Text.Json.JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (root.TryGetProperty(name, out var value) && value.ValueKind == System.Text.Json.JsonValueKind.String)
                return value.GetString();
        }

        return null;
    }

    // ── Utilities ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Chuẩn hoá language code về dạng 2 ký tự: "vi-VN" → "vi", "en-US" → "en"
    /// </summary>
    private static string NormalizeLanguage(string? language)
        => (language ?? "vi").Split('-', '_')[0].ToLowerInvariant();

    private static string CacheKey(int poiId, string language)
        => $"{poiId}_{NormalizeLanguage(language)}";

    private string CachePath(string key)
        => Path.Combine(_cacheDir, $"{key}.mp3");

    // ── Minimal DTOs để deserialise response từ /api/audio ───────────────────

    private sealed class AudioDto
    {
        [JsonPropertyName("audioUrl")]
        public string? AudioUrl { get; set; }

        [JsonPropertyName("language")]
        public string Language { get; set; } = "vi";
    }

    private sealed class AudioListResult
    {
        [JsonPropertyName("data")]
        public List<AudioRecord> Data { get; set; } = new();
    }

    private sealed class AudioRecord
    {
        [JsonPropertyName("audioContent_ID")]
        public int AudioContentId { get; set; }

        [JsonPropertyName("poI_ID")]
        public int PoiId { get; set; }

        [JsonPropertyName("audioUrl")]
        public string? AudioUrl { get; set; }

        [JsonPropertyName("language")]
        public string Language { get; set; } = "vi";

        [JsonPropertyName("status")]
        public string Status { get; set; } = "";
    }
}
