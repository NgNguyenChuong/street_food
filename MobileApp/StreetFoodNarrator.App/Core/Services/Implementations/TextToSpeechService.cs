using System.Net.Http.Json;
using System.Text.Json;
using Plugin.Maui.Audio;
using StreetFoodNarrator.App.Core.Models;
#if ANDROID
using Android.App;
using Android.OS;
using AndroidLocale = Java.Util.Locale;
using AndroidTextToSpeech = Android.Speech.Tts.TextToSpeech;
using AndroidVoice = Android.Speech.Tts.Voice;
using AndroidUtteranceProgressListener = Android.Speech.Tts.UtteranceProgressListener;
#endif

namespace StreetFoodNarrator.App.Core.Services.Implementations;

/// <summary>
/// Text-to-Speech service uses backend audio first, then native fallback.
/// Priority: cached offline audio -> published/streamed audio -> TTS API -> native TTS.
/// </summary>
public class TextToSpeechService : ITTSService
{
    // For POIs, prefer curated uploaded audio. If it does not exist, allow server TTS generation
    // before falling back to native TTS so remote narration still works over ngrok.
    private const bool EnablePoiServerTtsFallback = true;

    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly IAudioManager _audioManager;
    private readonly IAudioCacheService? _audioCache;
    private IAudioPlayer? _currentPlayer;
    private double _lastKnownDuration;

    private CancellationTokenSource? _nativeTtsCts;
    private bool _manualStop;
    private Task? _nativeSpeakTask;

    // Native TTS progress simulation
    private bool _isNativeTts;
    private double _nativeTtsDuration;
    private DateTime _nativeTtsStartTime;

    public event Action? OnPlaybackEnded;

    public bool IsAvailable => true;

    public TextToSpeechService(HttpClient httpClient, IAudioCacheService? audioCache = null)
    {
        _httpClient = httpClient;
        _audioCache = audioCache;
        _baseUrl = AppConfig.GetResolvedApiBaseUrl().TrimEnd('/');
        _audioManager = AudioManager.Current;
    }

    public double GetDuration()
    {
        if (_currentPlayer != null)
        {
            var duration = _currentPlayer.Duration;
            if (duration > 0)
            {
                _lastKnownDuration = duration;
                return duration;
            }

            if (_lastKnownDuration > 0)
                return _lastKnownDuration;
        }

        return _isNativeTts ? _nativeTtsDuration : 0;
    }

    public double GetCurrentPosition()
    {
        if (_currentPlayer != null)
            return _currentPlayer.CurrentPosition;

        if (_isNativeTts && _nativeTtsDuration > 0)
            return Math.Min((DateTime.Now - _nativeTtsStartTime).TotalSeconds, _nativeTtsDuration);

        return 0;
    }

    public void SetVolume(double volume)
    {
        if (_currentPlayer != null)
            _currentPlayer.Volume = Math.Clamp(volume, 0.0, 1.0);
    }

    public void SetSpeed(double speed)
    {
        try
        {
            if (_currentPlayer != null)
                _currentPlayer.Speed = Math.Max(0.1, speed);
        }
        catch
        {
        }
    }

    public void Seek(double positionSeconds)
    {
        try
        {
            _currentPlayer?.Seek(positionSeconds);
        }
        catch
        {
        }
    }

    public void Pause()
    {
        try
        {
            if (_currentPlayer != null && _currentPlayer.IsPlaying)
            {
                _currentPlayer.Pause();
                return;
            }

            // Native TTS fallback does not support true pause/resume well.
            // Treat pause as stop so UI and POI switching stay responsive.
            if (_isNativeTts)
            {
                _manualStop = true;
                _nativeTtsCts?.Cancel();
                _nativeTtsCts = null;
                _isNativeTts = false;
                _manualStop = false;
            }
        }
        catch
        {
        }
    }

    public void Resume()
    {
        try
        {
            if (_currentPlayer != null && !_currentPlayer.IsPlaying)
            {
                _currentPlayer.Play();
            }
        }
        catch
        {
        }
    }

    public bool IsPlaying()
    {
        try
        {
            return (_currentPlayer?.IsPlaying ?? false) || _isNativeTts;
        }
        catch
        {
            return false;
        }
    }

    public bool CanPauseResume()
    {
        try
        {
            return _currentPlayer != null;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> SpeakNativeFallbackAsync(
        string text,
        string languageCode,
        CancellationToken cancellationToken = default)
    {
        await StopAsync();
        return await SpeakWithResilientNativeFallbackAsync(text, languageCode, cancellationToken);
    }

    public async Task<bool> SpeakAsync(
        string text,
        string languageCode,
        string? voiceName = null,
        int? poiId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            text = EnsureNarrationText(text, languageCode);
            var preferredVoice = ResolvePreferredVoice(voiceName, languageCode);

            await StopAsync();

            var playbackMode = GetPlaybackMode();
            var networkAccess = Connectivity.Current.NetworkAccess;
            var isOnline = networkAccess == NetworkAccess.Internet ||
                           networkAccess == NetworkAccess.ConstrainedInternet;

            if (poiId.HasValue && _audioCache != null)
            {
                var cachedStream = await _audioCache.GetCachedStreamAsync(poiId.Value, languageCode);
                if (cachedStream != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[TTS] Using cached offline audio for POI {poiId}");
                    _currentPlayer = CreateAndWirePlayer(cachedStream);
                    _currentPlayer.Play();
                    StartDurationWarmup(_currentPlayer);
                    return true;
                }

                if (playbackMode == AudioPlaybackModes.Download)
                {
                    if (!isOnline)
                    {
                        System.Diagnostics.Debug.WriteLine("[TTS] Download mode but offline, skip download and use native fallback.");
                    }

                    var ensuredStream = await _audioCache.GetOrDownloadCachedStreamAsync(
                        poiId.Value,
                        languageCode,
                        cancellationToken);
                    if (ensuredStream != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[TTS] Downloaded and cached POI {poiId} audio locally.");
                        _currentPlayer = CreateAndWirePlayer(ensuredStream);
                        _currentPlayer.Play();
                        StartDurationWarmup(_currentPlayer);
                        return true;
                    }
                }

                if (playbackMode != AudioPlaybackModes.Download && isOnline)
                {
                    var audioUrl = await _audioCache.GetAudioUrlAsync(poiId.Value, languageCode, cancellationToken);
                    if (!string.IsNullOrEmpty(audioUrl))
                    {
                        System.Diagnostics.Debug.WriteLine($"[TTS] Streaming published audio for POI {poiId}: {audioUrl}");
                        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                        timeoutCts.CancelAfter(TimeSpan.FromSeconds(10));

                        var audioBytes = await _httpClient.GetByteArrayAsync(audioUrl, timeoutCts.Token);
                        var audioStream = new MemoryStream(audioBytes);
                        _currentPlayer = CreateAndWirePlayer(audioStream);
                        _currentPlayer.Play();
                        StartDurationWarmup(_currentPlayer);
                        return true;
                    }
                }

                System.Diagnostics.Debug.WriteLine($"[TTS] No published audio for POI {poiId}, falling back to TTS.");

                if (!EnablePoiServerTtsFallback)
                    return await SpeakWithResilientNativeFallbackAsync(text, languageCode, cancellationToken);
            }

            if (!isOnline)
            {
                System.Diagnostics.Debug.WriteLine("[TTS] Offline and no cache available, use native TTS.");
                return await SpeakWithResilientNativeFallbackAsync(text, languageCode, cancellationToken);
            }

            var request = new
            {
                text,
                language = languageCode,
                voice = ResolveServerVoice(preferredVoice, languageCode)
            };

            using var timeoutCts2 = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var ttsTimeout = poiId.HasValue ? TimeSpan.FromSeconds(2.2) : TimeSpan.FromSeconds(6);
            timeoutCts2.CancelAfter(ttsTimeout);

            var response = await _httpClient.PostAsJsonAsync(
                $"{_baseUrl}/api/tts/generate",
                request,
                timeoutCts2.Token);

            if (!response.IsSuccessStatusCode)
            {
                var errBody = await response.Content.ReadAsStringAsync(cancellationToken);
                System.Diagnostics.Debug.WriteLine($"[TTS] API error: {response.StatusCode}. Body={errBody}");
                return await SpeakWithResilientNativeFallbackAsync(text, languageCode, cancellationToken);
            }

            var result = await response.Content.ReadFromJsonAsync<TtsGenerateResponse>(
                cancellationToken: timeoutCts2.Token);
            if (result == null || string.IsNullOrEmpty(result.FilePath))
                return await SpeakWithResilientNativeFallbackAsync(text, languageCode, cancellationToken);

            var audioUrl2 = $"{_baseUrl}{result.FilePath}";
            System.Diagnostics.Debug.WriteLine($"[TTS] Generated TTS audio: {audioUrl2}");

            var audioBytes2 = await _httpClient.GetByteArrayAsync(audioUrl2, timeoutCts2.Token);
            var audioStream2 = new MemoryStream(audioBytes2);
            _currentPlayer = CreateAndWirePlayer(audioStream2);
            _currentPlayer.Play();
            StartDurationWarmup(_currentPlayer);
            return true;
        }
        catch (System.OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine("[TTS] API timeout, falling back to native TTS.");
            return await SpeakWithResilientNativeFallbackAsync(text, languageCode, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TTS] Network error, falling back to native TTS: {ex.Message}");
            return await SpeakWithResilientNativeFallbackAsync(text, languageCode, cancellationToken);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TTS] Error: {ex.Message}");
            return await SpeakWithResilientNativeFallbackAsync(text, languageCode, cancellationToken);
        }
    }

    public async Task<List<VoiceInfo>> GetVoicesAsync(string? languageCode = null)
    {
        var voices = new List<VoiceInfo>();

        try
        {
            var url = string.IsNullOrWhiteSpace(languageCode)
                ? $"{_baseUrl}/api/tts/voices"
                : $"{_baseUrl}/api/tts/voices?language={MapToApiLanguageCode(languageCode)}";
            var response = await _httpClient.GetFromJsonAsync<List<VoiceInfo>>(url);
            if (response != null)
                voices.AddRange(response);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TTS] Get voices error: {ex.Message}");
        }

#if ANDROID
        try
        {
            var localVoices = await GetAndroidOfflineVoicesAsync(languageCode);
            foreach (var localVoice in localVoices)
            {
                if (!voices.Any(v => string.Equals(v.Voice, localVoice.Voice, StringComparison.OrdinalIgnoreCase)))
                    voices.Add(localVoice);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TTS] Get Android offline voices error: {ex.Message}");
        }
#endif

        return voices;
    }

    public async Task StopAsync()
    {
        try
        {
            _manualStop = true;

            if (_currentPlayer != null)
            {
                _currentPlayer.Stop();
                _currentPlayer.Dispose();
                _currentPlayer = null;
            }

            _nativeTtsCts?.Cancel();
            _nativeTtsCts = null;
            _isNativeTts = false;
            _lastKnownDuration = 0;
            _manualStop = false;
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TTS] Stop error: {ex.Message}");
        }
    }

    private IAudioPlayer CreateAndWirePlayer(Stream stream)
    {
        var player = _audioManager.CreatePlayer(stream);
        player.PlaybackEnded += (_, _) =>
        {
            var duration = player.Duration;
            if (duration > 0)
                _lastKnownDuration = duration;

            if (!_manualStop)
                OnPlaybackEnded?.Invoke();
        };
        return player;
    }

    private void StartDurationWarmup(IAudioPlayer player)
    {
        _lastKnownDuration = 0;

        _ = Task.Run(async () =>
        {
            var delaysMs = new[] { 100, 150, 250 };
            foreach (var delayMs in delaysMs)
            {
                await Task.Delay(delayMs).ConfigureAwait(false);

                if (!ReferenceEquals(_currentPlayer, player))
                    return;

                try
                {
                    var duration = player.Duration;
                    if (duration > 0)
                    {
                        _lastKnownDuration = duration;
                        return;
                    }
                }
                catch
                {
                    return;
                }
            }
        });
    }

    private static string GetPlaybackMode()
    {
        try
        {
            var json = Preferences.Get("UserSettings", string.Empty);
            if (string.IsNullOrWhiteSpace(json))
                return AudioPlaybackModes.Auto;

            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("TTS", out var ttsObj))
                return AudioPlaybackModes.Auto;

            if (!ttsObj.TryGetProperty("AudioPlaybackMode", out var modeProp))
                return AudioPlaybackModes.Auto;

            var mode = modeProp.GetString()?.Trim().ToLowerInvariant();
            return mode switch
            {
                AudioPlaybackModes.Stream => AudioPlaybackModes.Stream,
                AudioPlaybackModes.Download => AudioPlaybackModes.Download,
                _ => AudioPlaybackModes.Auto
            };
        }
        catch
        {
            return AudioPlaybackModes.Auto;
        }
    }

    /// <summary>
    /// Phát text với voice package đã tải về (offline).
    /// Sử dụng MAUI TTS với voice đã chọn.
    /// </summary>
    public async Task<bool> SpeakWithVoicePackageAsync(
        string text,
        string languageCode,
        string voiceName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await StopAsync();

            _isNativeTts = true;
            _nativeTtsDuration = Math.Max(2.0, text.Length / 12.0);
            _nativeTtsStartTime = DateTime.Now;
            _lastKnownDuration = 0;

            // Try MAUI TTS with the specified voice
            Microsoft.Maui.Media.Locale? locale = null;
            try
            {
                var available = await Microsoft.Maui.Media.TextToSpeech.Default.GetLocalesAsync();
                // Find locale matching the language
                var langPrefix = languageCode.Split('-')[0].ToLowerInvariant();
                locale = available.FirstOrDefault(l =>
                    l.Language.StartsWith(langPrefix, StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
            }

            var settings = new SpeechOptions
            {
                Volume = 1.0f,
                Pitch = 1.0f,
                Locale = locale
            };

            await Microsoft.Maui.Media.TextToSpeech.Default.SpeakAsync(text, settings, cancellationToken);
            System.Diagnostics.Debug.WriteLine($"[TTS] Voice package playback complete: {voiceName}");

            if (!_manualStop)
                OnPlaybackEnded?.Invoke();

            return true;
        }
        catch (System.OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TTS] Voice package TTS error: {ex.Message}");
            // Fallback to generic native TTS
            return await SpeakWithResilientNativeFallbackAsync(text, languageCode, cancellationToken);
        }
        finally
        {
            _isNativeTts = false;
        }
    }

    private async Task<bool> NativeSpeakAsync(
        string text,
        string languageCode,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _nativeTtsCts?.Cancel();
            _nativeTtsCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var cts = _nativeTtsCts;

            _isNativeTts = true;
            _nativeTtsDuration = Math.Max(2.0, text.Length / 12.0);
            _nativeTtsStartTime = DateTime.Now;
            _lastKnownDuration = 0;

#if ANDROID
            var preferredVoice = ResolvePreferredVoice(null, languageCode);
            var androidOfflineOk = await SpeakWithAndroidOfflineVoiceAsync(
                text,
                languageCode,
                preferredVoice,
                cts.Token);
            if (androidOfflineOk)
            {
                if (!_manualStop)
                    OnPlaybackEnded?.Invoke();
                return true;
            }
#endif

            Microsoft.Maui.Media.Locale? locale = null;
            try
            {
                var available = await Microsoft.Maui.Media.TextToSpeech.Default.GetLocalesAsync();
                locale = available.FirstOrDefault(l =>
                    l.Language.StartsWith(
                        languageCode.Split('-')[0],
                        StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
            }

            var settings = new SpeechOptions
            {
                Volume = 1.0f,
                Pitch = 1.0f,
                Locale = locale
            };

            // Run native TTS in background so UI controls are not blocked while speaking.
            _nativeSpeakTask = RunNativeSpeakAsync(text, settings, cts);
            return true;
        }
        catch (System.OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TTS] Native TTS error: {ex.Message}");
            return false;
        }
    }

    private async Task RunNativeSpeakAsync(string text, SpeechOptions settings, CancellationTokenSource cts)
    {
        try
        {
            await Microsoft.Maui.Media.TextToSpeech.Default.SpeakAsync(text, settings, cts.Token);
            System.Diagnostics.Debug.WriteLine("[TTS] Native MAUI fallback playback complete.");

            if (!_manualStop && !cts.IsCancellationRequested)
                OnPlaybackEnded?.Invoke();
        }
        catch (System.OperationCanceledException)
        {
            // Expected when user stops or switches POI.
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TTS] Native background playback error: {ex.Message}");
        }
        finally
        {
            if (ReferenceEquals(_nativeTtsCts, cts))
                _nativeTtsCts = null;

            _isNativeTts = false;
        }
    }

    private async Task<bool> SpeakWithResilientNativeFallbackAsync(
        string text,
        string languageCode,
        CancellationToken cancellationToken = default)
    {
        var normalizedText = EnsureNarrationText(text, languageCode);
        if (await NativeSpeakAsync(normalizedText, languageCode, cancellationToken))
            return true;

        try
        {
            _isNativeTts = true;
            _nativeTtsDuration = Math.Max(2.0, normalizedText.Length / 12.0);
            _nativeTtsStartTime = DateTime.Now;
            _lastKnownDuration = 0;

            var fallbackSettings = new SpeechOptions
            {
                Volume = 1.0f,
                Pitch = 1.0f
            };

            await Microsoft.Maui.Media.TextToSpeech.Default.SpeakAsync(
                normalizedText,
                fallbackSettings,
                cancellationToken);

            if (!_manualStop)
                OnPlaybackEnded?.Invoke();

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TTS] Resilient native fallback failed: {ex.Message}");
            return false;
        }
        finally
        {
            _isNativeTts = false;
        }
    }

    private static string EnsureNarrationText(string? text, string languageCode)
    {
        var cleaned = (text ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(cleaned))
            return cleaned;

        var lang = (languageCode ?? string.Empty).Trim().ToLowerInvariant();
        if (lang.StartsWith("en"))
            return "Welcome to this destination.";
        if (lang.StartsWith("zh"))
            return "欢迎来到这个地点。";
        return "Chao mung ban den diem nay.";
    }

    private string MapToApiLanguageCode(string languageCode)
    {
        var normalized = languageCode.Trim().ToLowerInvariant();
        if (normalized.StartsWith("vi"))
            return "vi-VN";
        if (normalized.StartsWith("en"))
            return "en-US";
        if (normalized.StartsWith("zh"))
            return "zh-CN";
        return "vi-VN";
    }

    private string ResolvePreferredVoice(string? explicitVoiceName, string languageCode)
    {
        if (!string.IsNullOrWhiteSpace(explicitVoiceName))
            return explicitVoiceName.Trim();

        try
        {
            var json = Preferences.Get("UserSettings", string.Empty);
            if (!string.IsNullOrWhiteSpace(json))
            {
                var settings = JsonSerializer.Deserialize<UserSettings>(json);
                var configuredVoice = settings?.TTS?.Voice?.Trim();
                if (!string.IsNullOrWhiteSpace(configuredVoice))
                    return configuredVoice;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TTS] Resolve preferred voice error: {ex.Message}");
        }

        return GetDefaultServerVoice(languageCode);
    }

    private string? ResolveServerVoice(string? preferredVoice, string languageCode)
    {
        if (!string.IsNullOrWhiteSpace(preferredVoice) &&
            preferredVoice.Contains("Neural", StringComparison.OrdinalIgnoreCase) &&
            preferredVoice.StartsWith(MapToApiLanguageCode(languageCode), StringComparison.OrdinalIgnoreCase))
        {
            return preferredVoice;
        }

        return GetDefaultServerVoice(languageCode);
    }

    private string GetDefaultServerVoice(string languageCode)
    {
        return MapToApiLanguageCode(languageCode) switch
        {
            "en-US" => "en-US-JennyNeural",
            "zh-CN" => "zh-CN-XiaoxiaoNeural",
            _ => "vi-VN-HoaiMyNeural"
        };
    }

#if ANDROID
    private async Task<List<VoiceInfo>> GetAndroidOfflineVoicesAsync(string? languageCode)
    {
        // Temporarily disabled - Android TTS binding has SDK compatibility issues
        // App will use MAUI TTS fallback instead
        await Task.CompletedTask;
        return new List<VoiceInfo>();
    }

    private async Task<bool> SpeakWithAndroidOfflineVoiceAsync(
        string text,
        string languageCode,
        string? preferredVoice,
        CancellationToken cancellationToken)
    {
        // Temporarily disabled - use MAUI TTS fallback instead
        await Task.CompletedTask;
        return false;
    }

    // Temporarily disabled - Android TTS binding has SDK compatibility issues
    // Use MAUI TTS fallback (Microsoft.Maui.Media.TextToSpeech) instead
    // private static class AndroidOfflineTtsBridge
#endif

    private class TtsGenerateResponse
    {
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string Language { get; set; } = string.Empty;
        public string Voice { get; set; } = string.Empty;
    }
}
