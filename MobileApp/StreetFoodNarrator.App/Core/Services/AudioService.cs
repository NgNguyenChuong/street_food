namespace StreetFoodNarrator.App.Core.Services;

using StreetFoodNarrator.App.Core.Models;
using System.Diagnostics;

/// <summary>
/// Mock audio service for demo/testing.
/// Simulates playback with debug logging instead of real audio.
/// </summary>
public class AudioService : IAudioService
{
    private readonly Dictionary<int, bool> _playingStates = new();
    private readonly Dictionary<int, double> _positions = new();
    private readonly Dictionary<int, double> _volumes = new();
    private readonly Dictionary<int, CancellationTokenSource> _timers = new();

    public event Action<int>? OnPlaybackCompleted;
    public event Action<int, double>? OnPositionChanged;

    public async Task PlayAsync(int zoneId, string audioSource, int durationSeconds)
    {
        await StopAsync(zoneId); // Stop any existing playback

        Debug.WriteLine($"[Audio] ▶ Playing zone {zoneId}: {audioSource} ({durationSeconds}s)");
        _playingStates[zoneId] = true;
        _volumes[zoneId] = 1.0;
        _positions[zoneId] = 0;

        // Simulate playback with a timer
        var cts = new CancellationTokenSource();
        _timers[zoneId] = cts;

        _ = Task.Run(async () =>
        {
            for (int i = 0; i < durationSeconds; i++)
            {
                if (cts.Token.IsCancellationRequested) break;
                await Task.Delay(1000, cts.Token);
                _positions[zoneId] = i + 1;
                OnPositionChanged?.Invoke(zoneId, i + 1);
            }

            _playingStates[zoneId] = false;
            OnPlaybackCompleted?.Invoke(zoneId);
            Debug.WriteLine($"[Audio] ⏹ Completed zone {zoneId}");
        }, cts.Token);

        await Task.CompletedTask;
    }

    public async Task PauseAsync(int zoneId)
    {
        if (_timers.ContainsKey(zoneId))
        {
            _timers[zoneId].Cancel();
            _playingStates[zoneId] = false;
            Debug.WriteLine($"[Audio] ⏸ Paused zone {zoneId} at {_positions[zoneId]:F0}s");
        }
        await Task.CompletedTask;
    }

    public async Task ResumeFromAsync(int zoneId, double positionSeconds)
    {
        Debug.WriteLine($"[Audio] ▶ Resume zone {zoneId} from {positionSeconds:F0}s");
        // In real implementation, seek to position and play
        _playingStates[zoneId] = true;
        await Task.CompletedTask;
    }

    public async Task StopAsync(int zoneId)
    {
        if (_timers.ContainsKey(zoneId))
        {
            _timers[zoneId].Cancel();
            _timers.Remove(zoneId);
        }
        _playingStates[zoneId] = false;
        _positions[zoneId] = 0;
        Debug.WriteLine($"[Audio] ⏹ Stopped zone {zoneId}");
        await Task.CompletedTask;
    }

    public async Task StopAllAsync()
    {
        foreach (var zoneId in _timers.Keys.ToList())
        {
            await StopAsync(zoneId);
        }
    }

    public async Task SetVolumeAsync(int zoneId, double volume)
    {
        _volumes[zoneId] = volume;
        Debug.WriteLine($"[Audio] 🔊 Volume zone {zoneId} → {volume * 100:F0}%");
        await Task.CompletedTask;
    }

    public double GetCurrentPosition(int zoneId)
        => _positions.ContainsKey(zoneId) ? _positions[zoneId] : 0;

    public bool IsPlaying(int zoneId)
        => _playingStates.ContainsKey(zoneId) && _playingStates[zoneId];
}
