namespace StreetFoodNarrator.App.Core.Services;

using Plugin.Maui.Audio;
using StreetFoodNarrator.App.Core.Models;
using System.Diagnostics;
using System.Net.Http.Json;
using Microsoft.Maui.Storage;

/// <summary>
/// AudioService backed by Plugin.Maui.Audio.
/// Plays local bundled audio files from Resources/Raw/ (no internet required).
/// Falls back to silent mock when a file is not yet bundled.
/// </summary>
public class AudioService : IAudioService
{
    private readonly IAudioManager _audioManager;
    private readonly IAudioCacheService _audioCache;
    private readonly ITTSService _tts;
    private readonly LanguageService _langService;
    private readonly HttpClient _httpClient;
    private readonly UserSession _session;
    private readonly string _apiBaseUrl;
    private readonly Dictionary<int, IAudioPlayer> _players  = new();
    private readonly Dictionary<int, double>       _positions = new();
    private readonly Dictionary<int, double>       _volumes   = new();
    private readonly Dictionary<int, PoiListenSession> _listenSessions = new();

    public event Action<int>?         OnPlaybackCompleted;
    public event Action<int, double>? OnPositionChanged;

    public AudioService(
        IAudioCacheService audioCache,
        ITTSService tts,
        LanguageService langService,
        HttpClient httpClient,
        UserSession session)
    {
        _audioManager = AudioManager.Current;
        _audioCache = audioCache;
        _tts = tts;
        _langService = langService;
        _httpClient = httpClient;
        _session = session;
        _apiBaseUrl = AppConfig.GetResolvedApiBaseUrl().TrimEnd('/');
    }

    private sealed class PoiListenSession
    {
        public string PlaybackSessionId { get; set; } = Guid.NewGuid().ToString("N");
        public double MaxPositionSeconds { get; set; }
    }

    private void EnsurePoiListenSession(int zoneId)
    {
        if (zoneId <= 0)
            return;

        if (_listenSessions.ContainsKey(zoneId))
            return;

        _listenSessions[zoneId] = new PoiListenSession();
    }

    private void CaptureZoneProgress(int zoneId, double? overridePosition = null)
    {
        if (!_listenSessions.TryGetValue(zoneId, out var session))
            return;

        var current = overridePosition ?? GetCurrentPosition(zoneId);
        if (double.IsNaN(current) || double.IsInfinity(current))
            return;

        if (current > session.MaxPositionSeconds)
            session.MaxPositionSeconds = current;
    }

    private async Task FinalizeZoneProgressAsync(int zoneId, string source)
    {
        if (!_listenSessions.TryGetValue(zoneId, out var session))
            return;

        CaptureZoneProgress(zoneId);
        var listenedSeconds = Math.Round(Math.Max(0, session.MaxPositionSeconds), 2);
        var playbackSessionId = session.PlaybackSessionId;
        _listenSessions.Remove(zoneId);

        if (listenedSeconds <= 0 || string.IsNullOrWhiteSpace(_apiBaseUrl))
            return;

        try
        {
            var payload = new PoiListenProgressPayload
            {
                POI_ID = zoneId,
                ListenSeconds = listenedSeconds,
                SessionId = _session.SessionId,
                DeviceId = GetOrCreateAnonymousDeviceId(),
                PlaybackSessionId = playbackSessionId,
                Source = source
            };

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(6));
            await _httpClient.PostAsJsonAsync(
                $"{_apiBaseUrl}/api/Analytics/poi-listen-progress",
                payload,
                cts.Token);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Audio] Finalize listen progress failed for zone {zoneId}: {ex.Message}");
        }
    }

    private static string GetOrCreateAnonymousDeviceId()
    {
        const string key = "analytics_anonymous_device_id";
        var current = Preferences.Get(key, string.Empty);
        if (!string.IsNullOrWhiteSpace(current))
            return current;

        var created = $"m-{Guid.NewGuid():N}";
        Preferences.Set(key, created);
        return created;
    }

    private async Task StopAsyncInternal(int zoneId, bool finalizeProgress)
    {
        CaptureZoneProgress(zoneId);
        if (finalizeProgress)
            await FinalizeZoneProgressAsync(zoneId, "geofence-audio");

        if (_players.TryGetValue(zoneId, out var p))
        {
            p.Stop();
            p.Dispose();
            _players.Remove(zoneId);
        }

        _positions[zoneId] = 0;
        Debug.WriteLine($"[Audio] ⏹ Stopped zone {zoneId}");
    }

    private async Task FinalizeSessionsForSwitchAsync(int nextZoneId)
    {
        // Finalize only when user switches to another POI.
        var switchFromIds = _listenSessions.Keys
            .Where(id => id != nextZoneId)
            .ToList();

        foreach (var id in switchFromIds)
        {
            await FinalizeZoneProgressAsync(id, "geofence-audio-switch");
        }
    }

    private sealed class PoiListenProgressPayload
    {
        public int POI_ID { get; set; }
        public double ListenSeconds { get; set; }
        public string? SessionId { get; set; }
        public string? DeviceId { get; set; }
        public string? PlaybackSessionId { get; set; }
        public string? Source { get; set; }
    }

    // ── Public API ───────────────────────────────────────────────────────────

    public async Task PlayAsync(int zoneId, string audioSource, int durationSeconds, string fallbackText = "")
    {
        await FinalizeSessionsForSwitchAsync(zoneId);
        await StopAsyncInternal(zoneId, finalizeProgress: false);
        EnsurePoiListenSession(zoneId);

        Stream? stream = null;

        // 1. Try to load from AudioCacheService (downloads via HttpClient with ngrok bypass)
        try
        {
            // Always try to load the current localized audio
            stream = await _audioCache.GetOrDownloadCachedStreamAsync(zoneId, _langService.CurrentLanguage);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Audio] Cache error for zone {zoneId}: {ex.Message}");
        }

        // 2. Fallback to local bundle
        if (stream == null)
        {
            var fileName = string.IsNullOrWhiteSpace(audioSource)
                ? null
                : Path.GetFileName(audioSource);

            if (fileName != null)
            {
                try
                {
                    stream = await FileSystem.OpenAppPackageFileAsync(fileName);
                }
                catch { }
            }
        }

        if (stream != null)
        {
            try
            {
                var player = _audioManager.CreatePlayer(stream);

                _players[zoneId]   = player;
                _volumes[zoneId]   = 1.0;
                _positions[zoneId] = 0;
                player.Volume      = 1.0;

                player.PlaybackEnded += (_, _) =>
                {
                    _positions[zoneId] = durationSeconds;
                    CaptureZoneProgress(zoneId, durationSeconds);
                    _ = Task.Run(async () =>
                    {
                        await FinalizeZoneProgressAsync(zoneId, "geofence-audio-complete");
                    });
                    OnPlaybackCompleted?.Invoke(zoneId);
                    Debug.WriteLine($"[Audio] ⏹ Completed zone {zoneId}");
                };

                player.Play();
                Debug.WriteLine($"[Audio] ▶ Playing stream for zone {zoneId}");
                return;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Audio] Player error for stream: {ex.Message}");
            }
        }

        // Silent mock fallback or TTS fallback
        if (!string.IsNullOrWhiteSpace(fallbackText))
        {
            Debug.WriteLine($"[Audio] 🔇 Falling back to TTS for zone {zoneId}");
            _positions[zoneId] = 0;
            _ = Task.Run(async () =>
            {
                await _tts.SpeakAsync(fallbackText, "vi", poiId: zoneId);
                OnPlaybackCompleted?.Invoke(zoneId);
            });
        }
        else
        {
            Debug.WriteLine($"[Audio] 🔇 Mock zone {zoneId} — stream not available and no text");
            _positions[zoneId] = 0;
            _ = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(durationSeconds));
                OnPlaybackCompleted?.Invoke(zoneId);
            });
        }
    }

    public async Task PauseAsync(int zoneId)
    {
        if (_players.TryGetValue(zoneId, out var p) && p.IsPlaying)
        {
            p.Pause();
            _positions[zoneId] = p.CurrentPosition;
            CaptureZoneProgress(zoneId, p.CurrentPosition);
            Debug.WriteLine($"[Audio] ⏸ Paused zone {zoneId} at {_positions[zoneId]:F0}s");
        }
        await Task.CompletedTask;
    }

    public async Task ResumeFromAsync(int zoneId, double positionSeconds)
    {
        if (_players.TryGetValue(zoneId, out var p))
        {
            p.Seek(positionSeconds);
            p.Play();
            Debug.WriteLine($"[Audio] ▶ Resume zone {zoneId} from {positionSeconds:F0}s");
        }
        await Task.CompletedTask;
    }

    public async Task StopAsync(int zoneId)
    {
        // User stop/back should not finalize; finalize occurs only on POI switch.
        await StopAsyncInternal(zoneId, finalizeProgress: false);
    }

    public async Task StopAllAsync()
    {
        var ids = _players.Keys
            .Concat(_listenSessions.Keys)
            .Distinct()
            .ToList();

        foreach (var id in ids)
            await StopAsyncInternal(id, finalizeProgress: false);
    }

    public async Task SetVolumeAsync(int zoneId, double volume)
    {
        _volumes[zoneId] = volume;
        if (_players.TryGetValue(zoneId, out var p))
            p.Volume = Math.Clamp(volume, 0.0, 1.0);
        Debug.WriteLine($"[Audio] 🔊 Volume zone {zoneId} → {volume * 100:F0}%");
        await Task.CompletedTask;
    }

    public double GetCurrentPosition(int zoneId)
        => _players.TryGetValue(zoneId, out var p) ? p.CurrentPosition
           : _positions.GetValueOrDefault(zoneId, 0);

    public bool IsPlaying(int zoneId)
        => _players.TryGetValue(zoneId, out var p) && p.IsPlaying;
}
