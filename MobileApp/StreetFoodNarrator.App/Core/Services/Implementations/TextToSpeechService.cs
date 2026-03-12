using System.Net.Http.Json;
using StreetFoodNarrator.App.Core.Models;
using Plugin.Maui.Audio;

namespace StreetFoodNarrator.App.Core.Services.Implementations;

/// <summary>
/// Text-to-Speech service sử dụng Edge-TTS từ backend API.
/// Thứ tự ưu tiên: file cache offline → TTS API → native MAUI TTS.
/// </summary>
public class TextToSpeechService : ITTSService
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly IAudioManager _audioManager;
    private readonly IAudioCacheService? _audioCache;
    private IAudioPlayer? _currentPlayer;

    private CancellationTokenSource? _nativeTtsCts;

    public TextToSpeechService(HttpClient httpClient, IAudioCacheService? audioCache = null)
    {
        _httpClient  = httpClient;
        _audioCache  = audioCache;
        _baseUrl     = AppConfig.ApiBaseUrl.TrimEnd('/');
        _audioManager = AudioManager.Current;
    }

    public bool IsAvailable => true;

    /// <summary>
    /// Phát text với ngôn ngữ và voice được chỉ định.
    /// Thứ tự ưu tiên:
    /// 1. File cache offline (nếu có poiId)
    /// 2. Stream audio từ server (nếu có audio published)
    /// 3. TTS API (generate mới)
    /// 4. Native TTS (fallback)
    /// </summary>
    public async Task<bool> SpeakAsync(string text, string languageCode, string? voiceName = null,
        int? poiId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            // Dừng bất kỳ lượt phát hiện tại
            await StopAsync();

            // Ưu tiên 1: file audio đã tải trước (offline)
            if (poiId.HasValue && _audioCache != null)
            {
                var cachedStream = await _audioCache.GetCachedStreamAsync(poiId.Value, languageCode);
                if (cachedStream != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[TTS] 💾 Phát file cache offline: POI {poiId}");
                    _currentPlayer = _audioManager.CreatePlayer(cachedStream);
                    _currentPlayer.Play();
                    return true;
                }

                // Ưu tiên 2: Stream audio từ server (online, không cần tải về)
                var audioUrl = await _audioCache.GetAudioUrlAsync(poiId.Value, languageCode, cancellationToken);
                if (!string.IsNullOrEmpty(audioUrl))
                {
                    System.Diagnostics.Debug.WriteLine($"[TTS] 🌐 Stream audio online: POI {poiId} → {audioUrl}");
                    
                    using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    timeoutCts.CancelAfter(TimeSpan.FromSeconds(10)); // Timeout cho việc tải audio

                    var audioBytes = await _httpClient.GetByteArrayAsync(audioUrl, timeoutCts.Token);
                    var audioStream = new MemoryStream(audioBytes);
                    _currentPlayer = _audioManager.CreatePlayer(audioStream);
                    _currentPlayer.Play();
                    return true;
                }

                System.Diagnostics.Debug.WriteLine($"[TTS] ℹ️ POI {poiId} không có audio published → fallback TTS");
            }
            
            // Ưu tiên 3: TTS API (generate mới)
            // Build request
            var request = new
            {
                text = text,
                language = languageCode,
                voice = voiceName
            };

            // Use a short timeout so offline fallback kicks in quickly
            using var timeoutCts2 = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts2.CancelAfter(TimeSpan.FromSeconds(6));

            // Call API to generate audio
            var response = await _httpClient.PostAsJsonAsync(
                $"{_baseUrl}/api/tts/generate", request, timeoutCts2.Token);

            if (!response.IsSuccessStatusCode)
            {
                var errBody = await response.Content.ReadAsStringAsync(cancellationToken);
                System.Diagnostics.Debug.WriteLine($"[TTS] API error: {response.StatusCode} — falling back to native TTS");
                return await NativeSpeakAsync(text, languageCode, cancellationToken);
            }

            var result = await response.Content.ReadFromJsonAsync<TtsGenerateResponse>(
                cancellationToken: timeoutCts2.Token);
            if (result == null || string.IsNullOrEmpty(result.FilePath))
                return await NativeSpeakAsync(text, languageCode, cancellationToken);

            // Play audio file
            var audioUrl2 = $"{_baseUrl}{result.FilePath}";
            System.Diagnostics.Debug.WriteLine($"[TTS] 🤖 TTS API generated: {audioUrl2}");
            
            var audioBytes2 = await _httpClient.GetByteArrayAsync(audioUrl2, timeoutCts2.Token);
            var audioStream2 = new MemoryStream(audioBytes2);
            _currentPlayer = _audioManager.CreatePlayer(audioStream2);
            _currentPlayer.Play();
            
            return true;
        }
        catch (OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine("[TTS] API timeout — falling back to native TTS");
            return await NativeSpeakAsync(text, languageCode, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TTS] Network error — falling back to native TTS: {ex.Message}");
            return await NativeSpeakAsync(text, languageCode, cancellationToken);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TTS] Error: {ex.Message}");
            return await NativeSpeakAsync(text, languageCode, cancellationToken);
        }
    }

    /// <summary>
    /// Fallback: MAUI native TTS — Android TextToSpeech / iOS AVSpeechSynthesizer.
    /// Works fully offline.
    /// </summary>
    private async Task<bool> NativeSpeakAsync(
        string text, string languageCode, CancellationToken cancellationToken = default)
    {
        try
        {
            // Link with caller's token so StopAsync() also cancels it
            _nativeTtsCts?.Cancel();
            _nativeTtsCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var cts = _nativeTtsCts;

            // Best-effort locale match; null = device default
            Locale? locale = null;
            try
            {
                var available = await TextToSpeech.Default.GetLocalesAsync();
                locale = available.FirstOrDefault(l =>
                    l.Language.StartsWith(
                        languageCode.Split('-')[0],
                        StringComparison.OrdinalIgnoreCase));
            }
            catch { /* ignore — null locale falls back to device default */ }

            var settings = new SpeechOptions { Volume = 1.0f, Pitch = 1.0f, Locale = locale };
            await TextToSpeech.Default.SpeakAsync(text, settings, cts.Token);
            System.Diagnostics.Debug.WriteLine("[TTS] Native TTS playback complete.");
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TTS] Native TTS error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Lấy danh sách voices cho ngôn ngữ
    /// </summary>
    public async Task<List<VoiceInfo>> GetVoicesAsync(string? languageCode = null)
    {
        try
        {
            var url = string.IsNullOrWhiteSpace(languageCode)
                ? $"{_baseUrl}/api/tts/voices"
                : $"{_baseUrl}/api/tts/voices?language={MapToApiLanguageCode(languageCode)}";
            var response = await _httpClient.GetFromJsonAsync<List<VoiceInfo>>(url);
            return response ?? new List<VoiceInfo>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TTS] Get voices error: {ex.Message}");
            return new List<VoiceInfo>();
        }
    }

    /// <summary>
    /// Dừng phát
    /// </summary>
    public async Task StopAsync()
    {
        try
        {
            // Stop Plugin.Maui.Audio player if active
            if (_currentPlayer != null)
            {
                _currentPlayer.Stop();
                _currentPlayer.Dispose();
                _currentPlayer = null;
            }
            // Cancel native TTS if a speak is in progress
            _nativeTtsCts?.Cancel();
            _nativeTtsCts = null;
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TTS] Stop error: {ex.Message}");
        }
    }

    private string MapToApiLanguageCode(string languageCode)
    {
        var normalized = languageCode.Trim().ToLowerInvariant();
        if (normalized.StartsWith("vi")) return "vi-VN";
        if (normalized.StartsWith("en")) return "en-US";
        if (normalized.StartsWith("zh")) return "zh-CN";
        return "vi-VN";
    }

    private class TtsGenerateResponse
    {
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string Language { get; set; } = string.Empty;
        public string Voice { get; set; } = string.Empty;
    }
}
