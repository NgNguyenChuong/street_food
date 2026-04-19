namespace StreetFoodNarrator.App.Core.Services;

using Plugin.Maui.Audio;
using StreetFoodNarrator.App.Core.Models;
using System.Diagnostics;
using System.Net.Http.Json;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;
#if ANDROID
using Android.Content;
#endif

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

#if ANDROID
    private global::Android.Media.AudioManager? _androidAudioManager;
    private AudioFocusChangeListener? _audioFocusListener;
    private readonly HashSet<int> _focusPausedZoneIds = new();
    private bool _hasAudioFocus;
#endif

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

    #if ANDROID
        InitializeAudioFocus();
    #endif
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

    private static string GetOrCreateAnonymousDeviceId() => AppConfig.GetOrCreateDeviceId();

#if ANDROID
    private sealed class AudioFocusChangeListener : Java.Lang.Object, global::Android.Media.AudioManager.IOnAudioFocusChangeListener
    {
        private readonly AudioService _owner;

        public AudioFocusChangeListener(AudioService owner)
        {
            _owner = owner;
        }

        public void OnAudioFocusChange(global::Android.Media.AudioFocus focusChange)
        {
            _owner.HandleAudioFocusChange(focusChange);
        }
    }

    private void InitializeAudioFocus()
    {
        try
        {
            _androidAudioManager = global::Android.App.Application.Context.GetSystemService(Context.AudioService) as global::Android.Media.AudioManager;
            if (_androidAudioManager == null)
                return;

            _audioFocusListener = new AudioFocusChangeListener(this);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Audio] Audio focus init failed: {ex.Message}");
        }
    }

    private bool TryRequestAudioFocus()
    {
        if (_hasAudioFocus)
            return true;

        if (_androidAudioManager == null || _audioFocusListener == null)
            return true;

        try
        {
            var result = _androidAudioManager.RequestAudioFocus(
                _audioFocusListener,
                global::Android.Media.Stream.Music,
                global::Android.Media.AudioFocus.Gain);

            _hasAudioFocus = result == global::Android.Media.AudioFocusRequest.Granted;
            return _hasAudioFocus;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Audio] RequestAudioFocus failed: {ex.Message}");
            return true;
        }
    }

    private void AbandonAudioFocus()
    {
        if (!_hasAudioFocus || _androidAudioManager == null || _audioFocusListener == null)
            return;

        try
        {
            _androidAudioManager.AbandonAudioFocus(_audioFocusListener);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Audio] AbandonAudioFocus failed: {ex.Message}");
        }
        finally
        {
            _hasAudioFocus = false;
        }
    }

    private void AbandonAudioFocusIfIdle()
    {
        var hasAnyPlaying = _players.Values.Any(player => player.IsPlaying);
        if (!hasAnyPlaying)
            AbandonAudioFocus();
    }

    private void HandleAudioFocusChange(global::Android.Media.AudioFocus focusChange)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            switch (focusChange)
            {
                case global::Android.Media.AudioFocus.Gain:
                    ResumeAfterAudioFocusGain();
                    break;

                case global::Android.Media.AudioFocus.LossTransient:
                case global::Android.Media.AudioFocus.LossTransientCanDuck:
                    PauseForTransientAudioFocusLoss();
                    break;

                case global::Android.Media.AudioFocus.Loss:
                    _ = Task.Run(HandlePermanentAudioFocusLossAsync);
                    break;
            }
        });
    }

    private void PauseForTransientAudioFocusLoss()
    {
        foreach (var kv in _players.ToList())
        {
            var zoneId = kv.Key;
            var player = kv.Value;
            if (!player.IsPlaying)
                continue;

            player.Pause();
            _positions[zoneId] = player.CurrentPosition;
            CaptureZoneProgress(zoneId, player.CurrentPosition);
            _focusPausedZoneIds.Add(zoneId);
        }
    }

    private void ResumeAfterAudioFocusGain()
    {
        if (_focusPausedZoneIds.Count == 0)
            return;

        foreach (var zoneId in _focusPausedZoneIds.ToList())
        {
            if (_players.TryGetValue(zoneId, out var player) && !player.IsPlaying)
            {
                try
                {
                    player.Play();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Audio] Resume after focus gain failed for zone {zoneId}: {ex.Message}");
                }
            }
        }

        _focusPausedZoneIds.Clear();
    }

    private async Task HandlePermanentAudioFocusLossAsync()
    {
        _focusPausedZoneIds.Clear();
        await StopAllAsync();
        AbandonAudioFocus();
    }
#endif

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

    #if ANDROID
        _focusPausedZoneIds.Remove(zoneId);
        AbandonAudioFocusIfIdle();
    #endif
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

#if ANDROID
        if (!TryRequestAudioFocus())
        {
            Debug.WriteLine($"[Audio] Audio focus denied for zone {zoneId}");
            return;
        }
        _focusPausedZoneIds.Remove(zoneId);
#endif

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

#if ANDROID
                    _focusPausedZoneIds.Remove(zoneId);
                    AbandonAudioFocusIfIdle();
#endif
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

#if ANDROID
        AbandonAudioFocusIfIdle();
#endif

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

#if ANDROID
            _focusPausedZoneIds.Remove(zoneId);
            AbandonAudioFocusIfIdle();
#endif
        }
        await Task.CompletedTask;
    }

    public async Task ResumeFromAsync(int zoneId, double positionSeconds)
    {
        if (_players.TryGetValue(zoneId, out var p))
        {
#if ANDROID
            if (!TryRequestAudioFocus())
            {
                Debug.WriteLine($"[Audio] Audio focus denied while resuming zone {zoneId}");
                return;
            }
            _focusPausedZoneIds.Remove(zoneId);
#endif

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

#if ANDROID
        _focusPausedZoneIds.Clear();
        AbandonAudioFocus();
#endif
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
