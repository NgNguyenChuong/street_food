namespace StreetFoodNarrator.App.Core.Services;

using Plugin.Maui.Audio;
using StreetFoodNarrator.App.Core.Models;
using System.Diagnostics;

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
    private readonly Dictionary<int, IAudioPlayer> _players  = new();
    private readonly Dictionary<int, double>       _positions = new();
    private readonly Dictionary<int, double>       _volumes   = new();

    public event Action<int>?         OnPlaybackCompleted;
    public event Action<int, double>? OnPositionChanged;

    public AudioService(IAudioCacheService audioCache, ITTSService tts, LanguageService langService)
    {
        _audioManager = AudioManager.Current;
        _audioCache = audioCache;
        _tts = tts;
        _langService = langService;
    }

    // ── Public API ───────────────────────────────────────────────────────────

    public async Task PlayAsync(int zoneId, string audioSource, int durationSeconds, string fallbackText = "")
    {
        await StopAsync(zoneId);

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
        if (_players.TryGetValue(zoneId, out var p))
        {
            p.Stop();
            p.Dispose();
            _players.Remove(zoneId);
        }
        _positions[zoneId] = 0;
        Debug.WriteLine($"[Audio] ⏹ Stopped zone {zoneId}");
        await Task.CompletedTask;
    }

    public async Task StopAllAsync()
    {
        foreach (var id in _players.Keys.ToList())
            await StopAsync(id);
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
