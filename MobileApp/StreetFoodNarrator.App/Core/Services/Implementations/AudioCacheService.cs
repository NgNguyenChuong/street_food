using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StreetFoodNarrator.App.Core.Services.Implementations;

/// <summary>
/// Tải trước và lưu file audio MP3 offline cho từng POI.
/// File được lưu tại: AppDataDirectory/audio_cache/{poiId}_{lang}.mp3
/// </summary>
public class AudioCacheService : IAudioCacheService
{
    private static readonly TimeSpan CacheValidationInterval = TimeSpan.FromMinutes(2);

    private readonly HttpClient _http;
    private readonly string _cacheDir;
    private readonly string _metaFilePath;

    // Bộ nhớ trong — tránh gọi File.Exists() liên tục trên UI thread
    private readonly HashSet<string> _cachedKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _cachedSourceUrls = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTime> _lastValidationUtc = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly object _validationSync = new();
    private readonly HashSet<string> _validationInFlight = new(StringComparer.OrdinalIgnoreCase);

    public AudioCacheService(HttpClient httpClient)
    {
        _http    = httpClient;
        _cacheDir = Path.Combine(FileSystem.AppDataDirectory, "audio_cache");
        _metaFilePath = Path.Combine(_cacheDir, "audio_cache_index.json");
        Directory.CreateDirectory(_cacheDir);

        // Lập chỉ mục file đã tồn tại lúc khởi động
        foreach (var file in Directory.EnumerateFiles(_cacheDir, "*.mp3"))
            _cachedKeys.Add(Path.GetFileNameWithoutExtension(file));

        LoadCacheIndex();
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
        var hasCachedFile = File.Exists(path);
        var networkAccess = Connectivity.Current.NetworkAccess;

        if (hasCachedFile)
        {
            if ((networkAccess == NetworkAccess.Internet || networkAccess == NetworkAccess.ConstrainedInternet) &&
                ShouldValidateNow(key))
            {
                var refreshedNow = await TryRefreshCachedAudioNowAsync(
                    poiId,
                    language,
                    key,
                    networkAccess,
                    ct);

                if (!refreshedNow)
                    QueueBackgroundValidation(poiId, language, key, networkAccess);
            }

            return File.OpenRead(path);
        }

        // Không có mạng thì không thể tải về mới.
        if (networkAccess != NetworkAccess.Internet &&
            networkAccess != NetworkAccess.ConstrainedInternet)
        {
            Debug.WriteLine($"[AudioCache] Offline: bỏ qua download POI {poiId} ({language})");
            return null;
        }

        var audioUrl = await GetAudioUrlAsync(poiId, language, ct);
        if (string.IsNullOrWhiteSpace(audioUrl))
            return null;

        var requestTimeout = networkAccess == NetworkAccess.ConstrainedInternet
            ? TimeSpan.FromSeconds(6)
            : TimeSpan.FromSeconds(12);

        var tc = CancellationTokenSource.CreateLinkedTokenSource(ct);
        tc.CancelAfter(requestTimeout);

        try
        {
            await DownloadAndSaveAsync(audioUrl, key, tc.Token);
            return File.Exists(path) ? File.OpenRead(path) : null;
        }
        catch (OperationCanceledException) when (tc.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            Debug.WriteLine($"[AudioCache] Timeout tải audio ({requestTimeout.TotalSeconds:0.#}s), chuyển sang fallback TTS.");
            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AudioCache] Lỗi khi tải audio: {ex.Message}");
            return null;
        }
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
            _cachedSourceUrls.Clear();
            _lastValidationUtc.Clear();
            if (File.Exists(_metaFilePath))
                File.Delete(_metaFilePath);
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
            var url = $"{GetBaseUrl()}/api/audio?status=published&pageSize=500";
            var result = await _http.GetFromJsonAsync<AudioListResult>(url, ct);
            if (result?.Data == null) return 0;

            int newCount = 0;
            var indexTouched = false;
            foreach (var audio in result.Data)
            {
                if (string.IsNullOrWhiteSpace(audio.AudioUrl)) continue;

                var lang = NormalizeLanguage(audio.Language);
                var latestUrl = NormalizeAudioUrl(audio.AudioUrl);

                // ⚠️ File cache được lưu theo poiId_lang.mp3, không phải AudioContentId_lang.mp3
                // Nên phải dùng PoiId để build cache key cho đúng.
                if (audio.PoiId <= 0) continue;

                var key = CacheKey(audio.PoiId, lang);
                var path = CachePath(key);
                var hasFile = _cachedKeys.Contains(key) || File.Exists(path);
                if (!hasFile)
                {
                    newCount++;
                    continue;
                }

                _cachedKeys.Add(key);
                if (string.IsNullOrWhiteSpace(GetCachedSourceUrl(key)))
                {
                    // Legacy cache with no source metadata -> do not count as update yet.
                    // The first playback/preload validation will force-refresh safely.
                    continue;
                }

                if (!string.Equals(GetCachedSourceUrl(key), latestUrl, StringComparison.OrdinalIgnoreCase))
                {
                    newCount++;
                    continue;
                }

                _lastValidationUtc[key] = DateTime.UtcNow;
                indexTouched = true;
            }

            if (indexTouched)
                PersistCacheIndex();

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
            var url = $"{GetBaseUrl()}/api/audio/poi/{poiId}/{normalizedLang}";
            var audio = await _http.GetFromJsonAsync<AudioDto>(url, ct);

            if (audio?.AudioUrl == null)
                return null;

            // Convert relative path to absolute URL
            var audioUrl = audio.AudioUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? audio.AudioUrl
                : $"{GetBaseUrl()}{audio.AudioUrl}";

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
                var url = $"{GetBaseUrl()}/api/audio/poi/{poiId}/{lang}";
                var audio = await _http.GetFromJsonAsync<AudioDto>(url, ct);
                
                if (audio == null || string.IsNullOrWhiteSpace(audio.AudioUrl))
                    continue;

                var normalizedLang = NormalizeLanguage(lang);
                var key = CacheKey(poiId, normalizedLang);
                var latestUrl = NormalizeAudioUrl(audio.AudioUrl);
                var path = CachePath(key);
                
                // Download if file missing or source URL has changed.
                if (!_cachedKeys.Contains(key) || !File.Exists(path) ||
                    !string.Equals(GetCachedSourceUrl(key), latestUrl, StringComparison.OrdinalIgnoreCase))
                {
                    await DownloadAndSaveAsync(latestUrl, key, ct);
                    Debug.WriteLine($"[AudioCache] ✅ Đồng bộ POI {poiId} ({normalizedLang})");
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
        var fileUrl = NormalizeAudioUrl(audioUrl);

        using var response = await _http.GetAsync(fileUrl, ct);
        response.EnsureSuccessStatusCode();

        if (response.Content.Headers.ContentType?.MediaType?.Contains("text/html") == true)
        {
            throw new Exception("Ngrok html returned instead of media file. Add ngrok-skip-browser-warning properly.");
        }

        var bytes = await response.Content.ReadAsByteArrayAsync(ct);
        var path = CachePath(key);

        await _lock.WaitAsync(ct);
        try
        {
            await File.WriteAllBytesAsync(path, bytes, ct);
            _cachedKeys.Add(key);
            _cachedSourceUrls[key] = fileUrl;
            _lastValidationUtc[key] = DateTime.UtcNow;
            PersistCacheIndex();
            Debug.WriteLine($"[AudioCache] ✓ Đã lưu {key}.mp3 ({bytes.Length / 1024} KB)");
        }
        finally { _lock.Release(); }
    }

    private async Task<string?> TryGetPoiAudioUrlAsync(int poiId, string language, CancellationToken ct)
    {
        try
        {
            var url = $"{GetBaseUrl()}/api/POIs/{poiId}";
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
                : $"{GetBaseUrl()}{selected}";
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

    private bool ShouldValidateNow(string key)
    {
        if (!_lastValidationUtc.TryGetValue(key, out var lastChecked))
            return true;

        return (DateTime.UtcNow - lastChecked) >= CacheValidationInterval;
    }

    private void MarkValidated(string key)
    {
        _lastValidationUtc[key] = DateTime.UtcNow;
    }

    private string? GetCachedSourceUrl(string key)
        => _cachedSourceUrls.TryGetValue(key, out var value) ? value : null;

    private string NormalizeAudioUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return string.Empty;

        var trimmed = url.Trim();
        if (trimmed.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            return trimmed;

        var baseUrl = GetBaseUrl();
        return trimmed.StartsWith("/")
            ? $"{baseUrl}{trimmed}"
            : $"{baseUrl}/{trimmed}";
    }

    private static string GetBaseUrl()
        => AppConfig.GetResolvedApiBaseUrl().TrimEnd('/');

    private void LoadCacheIndex()
    {
        try
        {
            if (!File.Exists(_metaFilePath))
                return;

            var json = File.ReadAllText(_metaFilePath);
            var doc = JsonSerializer.Deserialize<AudioCacheIndexDocument>(json);
            if (doc?.Entries == null)
                return;

            foreach (var entry in doc.Entries)
            {
                if (string.IsNullOrWhiteSpace(entry.Key))
                    continue;

                if (!string.IsNullOrWhiteSpace(entry.SourceUrl))
                    _cachedSourceUrls[entry.Key] = entry.SourceUrl;

                if (entry.LastCheckedUtc.HasValue)
                    _lastValidationUtc[entry.Key] = entry.LastCheckedUtc.Value;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AudioCache] Load cache index failed: {ex.Message}");
        }
    }

    private void PersistCacheIndex()
    {
        try
        {
            var entries = _cachedKeys
                .Select(key => new AudioCacheIndexEntry
                {
                    Key = key,
                    SourceUrl = _cachedSourceUrls.TryGetValue(key, out var source) ? source : null,
                    LastCheckedUtc = _lastValidationUtc.TryGetValue(key, out var checkedUtc) ? checkedUtc : null
                })
                .ToList();

            var json = JsonSerializer.Serialize(
                new AudioCacheIndexDocument { Entries = entries },
                new JsonSerializerOptions { WriteIndented = false });
            File.WriteAllText(_metaFilePath, json);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AudioCache] Persist cache index failed: {ex.Message}");
        }
    }

    private static string CacheKey(int poiId, string language)
        => $"{poiId}_{NormalizeLanguage(language)}";

    private void QueueBackgroundValidation(int poiId, string language, string key, NetworkAccess networkAccess)
    {
        lock (_validationSync)
        {
            if (!_validationInFlight.Add(key))
                return;
        }

        _ = Task.Run(async () =>
        {
            var timeout = networkAccess == NetworkAccess.ConstrainedInternet
                ? TimeSpan.FromSeconds(3)
                : TimeSpan.FromSeconds(5);

            using var cts = new CancellationTokenSource(timeout);
            try
            {
                var latestAudioUrl = await GetAudioUrlAsync(poiId, language, cts.Token);
                if (string.IsNullOrWhiteSpace(latestAudioUrl))
                {
                    MarkValidated(key);
                    return;
                }

                var normalizedLatestUrl = NormalizeAudioUrl(latestAudioUrl);
                if (string.Equals(GetCachedSourceUrl(key), normalizedLatestUrl, StringComparison.OrdinalIgnoreCase))
                {
                    MarkValidated(key);
                    return;
                }

                await DownloadAndSaveAsync(normalizedLatestUrl, key, cts.Token);
                MarkValidated(key);
                Debug.WriteLine($"[AudioCache] Updated cached audio in background: {key}");
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine($"[AudioCache] Background validation timeout for {key}.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AudioCache] Background validation failed for {key}: {ex.Message}");
            }
            finally
            {
                lock (_validationSync)
                    _validationInFlight.Remove(key);
            }
        });
    }

    private async Task<bool> TryRefreshCachedAudioNowAsync(
        int poiId,
        string language,
        string key,
        NetworkAccess networkAccess,
        CancellationToken externalToken)
    {
        var timeout = networkAccess == NetworkAccess.ConstrainedInternet
            ? TimeSpan.FromSeconds(2.5)
            : TimeSpan.FromSeconds(4);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);
        cts.CancelAfter(timeout);

        try
        {
            var latestAudioUrl = await GetAudioUrlAsync(poiId, language, cts.Token);
            if (string.IsNullOrWhiteSpace(latestAudioUrl))
            {
                MarkValidated(key);
                PersistCacheIndex();
                return false;
            }

            var normalizedLatestUrl = NormalizeAudioUrl(latestAudioUrl);
            if (string.Equals(GetCachedSourceUrl(key), normalizedLatestUrl, StringComparison.OrdinalIgnoreCase))
            {
                MarkValidated(key);
                PersistCacheIndex();
                return false;
            }

            await DownloadAndSaveAsync(normalizedLatestUrl, key, cts.Token);
            MarkValidated(key);
            PersistCacheIndex();
            Debug.WriteLine($"[AudioCache] Refreshed cached audio immediately: {key}");
            return true;
        }
        catch (OperationCanceledException)
        {
            Debug.WriteLine($"[AudioCache] Immediate refresh timeout for {key}.");
            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AudioCache] Immediate refresh failed for {key}: {ex.Message}");
            return false;
        }
    }

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

    private sealed class AudioCacheIndexDocument
    {
        [JsonPropertyName("entries")]
        public List<AudioCacheIndexEntry> Entries { get; set; } = new();
    }

    private sealed class AudioCacheIndexEntry
    {
        [JsonPropertyName("key")]
        public string Key { get; set; } = string.Empty;

        [JsonPropertyName("sourceUrl")]
        public string? SourceUrl { get; set; }

        [JsonPropertyName("lastCheckedUtc")]
        public DateTime? LastCheckedUtc { get; set; }
    }
}
