namespace StreetFoodNarrator.App.Core.Models;

/// <summary>
/// Tracks user session state: played zones, cooldowns, audio positions.
/// Resets when app restarts or user manually resets session.
/// </summary>
public class UserSession
{
    private readonly HashSet<int> _playedThisSession = new();
    private readonly Dictionary<int, DateTime> _cooldowns = new();
    private readonly Dictionary<int, double> _audioPositions = new();

    public int ZonesTriggeredCount => _playedThisSession.Count;
    public string SessionInfo =>
        $"Khu vực đã nghe: {ZonesTriggeredCount} | Cooldowns: {_cooldowns.Count}";

    // ── Play tracking ─────────────────────────────────────────────
    public void MarkPlayedThisSession(int zoneId)
        => _playedThisSession.Add(zoneId);

    public bool IsPlayedThisSession(int zoneId)
        => _playedThisSession.Contains(zoneId);

    // ── Cooldown tracking ─────────────────────────────────────────
    public void SetCooldown(int zoneId, int cooldownMinutes)
    {
        if (cooldownMinutes > 0)
            _cooldowns[zoneId] = DateTime.UtcNow.AddMinutes(cooldownMinutes);
    }

    public bool IsOnCooldown(int zoneId)
    {
        if (!_cooldowns.ContainsKey(zoneId))
            return false;

        if (DateTime.UtcNow < _cooldowns[zoneId])
            return true;

        // Cooldown expired — remove it
        _cooldowns.Remove(zoneId);
        return false;
    }

    public TimeSpan? GetCooldownRemaining(int zoneId)
    {
        if (!_cooldowns.ContainsKey(zoneId))
            return null;

        var remaining = _cooldowns[zoneId] - DateTime.UtcNow;
        return remaining.TotalSeconds > 0 ? remaining : null;
    }

    // ── Audio position tracking (for resume) ──────────────────────
    public void SaveAudioPosition(int zoneId, double positionSeconds)
        => _audioPositions[zoneId] = positionSeconds;

    public double GetAudioPosition(int zoneId)
        => _audioPositions.ContainsKey(zoneId) ? _audioPositions[zoneId] : 0;

    // ── Reset ─────────────────────────────────────────────────────
    public void Reset()
    {
        _playedThisSession.Clear();
        _cooldowns.Clear();
        _audioPositions.Clear();
    }
}
