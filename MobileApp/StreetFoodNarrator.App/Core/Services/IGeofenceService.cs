namespace StreetFoodNarrator.App.Core.Services.Implementations;

using StreetFoodNarrator.App.Core.Models;
using StreetFoodNarrator.App.Core.Services;

/// <summary>
/// Core geofencing logic - the brain of the system.
/// Implements smart overlapping zones algorithm.
/// </summary>
public class GeofenceService : IGeofenceService
{
    // ── Constants ─────────────────────────────────────────────────
    private const double GPS_DEBOUNCE_METERS = 5.0;
    private const int GPS_DEBOUNCE_MS = 3000;
    private const double EARTH_RADIUS_M = 6_371_000.0;

    // ── Dependencies ──────────────────────────────────────────────
    private readonly IZoneRepository _repository;
    private readonly IAudioService _audio;
    private readonly UserSession _session;
    private readonly ILocalDatabaseService _db;

    // ── State ─────────────────────────────────────────────────────
    private Microsoft.Maui.Devices.Sensors.Location? _lastLocation;
    private List<POI> _currentZones = new();
    private POI? _primaryZone = null;
    private bool _localLoaded = false;
    private bool _isMonitoringEnabled = true;

    // ── Events ────────────────────────────────────────────────────
    public event Action<List<POI>>? OnActiveZonesChanged;
    public event Action<POI?>? OnPrimaryZoneChanged;
    public event Action<string>? OnStatusMessage;

    public GeofenceService(IZoneRepository repository, IAudioService audio, UserSession session, ILocalDatabaseService db)
    {
        _repository = repository;
        _audio = audio;
        _session = session;
        _db = db;
    }

    // ─────────────────────────────────────────────────────────────
    // MAIN ENTRY POINT
    // ─────────────────────────────────────────────────────────────
    public async Task OnLocationChangedAsync(Microsoft.Maui.Devices.Sensors.Location newLocation)
    {
        if (!_isMonitoringEnabled)
        {
            _lastLocation = newLocation;
            return;
        }

        if (!_localLoaded)
        {
            await _repository.LoadLocalAsync();
            _localLoaded = true;
        }

        // STEP 1: Debounce — ignore micro-movements
        if (!ShouldProcess(newLocation))
            return;

        _lastLocation = newLocation;

        // STEP 2: Get all candidate zones
        var candidates = _repository.GetAllActiveZones();

        // STEP 3: Filter — only keep zones where user is actually inside
        foreach (var z in candidates)
            z.DistanceFromUser = HaversineDistance(
                newLocation.Latitude, newLocation.Longitude,
                z.Latitude, z.Longitude);

        var insideZones = candidates
            .Where(z => z.DistanceFromUser <= z.Radius)
            .ToList();

        // STEP 4: Compute diffs
        var enteredZones = insideZones
            .Where(z => !_currentZones.Any(c => c.Id == z.Id))
            .ToList();
        var exitedZones = _currentZones
            .Where(z => !insideZones.Any(i => i.Id == z.Id))
            .ToList();

        _currentZones = insideZones;
        OnActiveZonesChanged?.Invoke(_currentZones);

        // STEP 5: Handle exits first
        foreach (var zone in exitedZones)
            await HandleExitAsync(zone);

        // STEP 6: Handle entries
        foreach (var zone in enteredZones)
            LogEntry(zone);

        // STEP 7: Select & update primary zone
        await UpdatePrimaryZoneAsync(insideZones);
    }

    public async Task SetMonitoringEnabledAsync(bool enabled)
    {
        if (_isMonitoringEnabled == enabled)
            return;

        _isMonitoringEnabled = enabled;

        if (!enabled)
        {
            await ClearMonitoringStateAsync("GPS đang ở xa POI, tạm tắt geofence để tiết kiệm pin.");
            return;
        }

        OnStatusMessage?.Invoke("Đã bật geofence watch mode cho POI gần nhất.");
    }

    private async Task UpdatePrimaryZoneAsync(List<POI> insideZones)
    {
        if (!insideZones.Any())
        {
            if (_primaryZone != null)
            {
                await _audio.StopAllAsync();
                _primaryZone = null;
                OnPrimaryZoneChanged?.Invoke(null);
                OnStatusMessage?.Invoke("⏹ Rời tất cả khu vực — im lặng.");
            }
            return;
        }

        // Stable sort for overlapping zones: highest priority first, then nearest distance, then tighter radius.
        // Convention: Priority 1-10, HIGHER = more important (Spot > District > Area).
        var sorted = insideZones
            .OrderByDescending(z => z.Priority)
            .ThenBy(z => z.DistanceFromUser)
            .ThenBy(z => z.Radius)
            .ThenBy(z => z.Id)
            .ToList();
        var bestZone = sorted.First();

        // Hysteresis: when priorities are equal and user is on overlap boundary, keep current zone briefly.
        if (_primaryZone != null)
        {
            var current = insideZones.FirstOrDefault(z => z.Id == _primaryZone.Id);
            if (current != null)
            {
                var samePriority = current.Priority == bestZone.Priority;
                var distanceDelta = bestZone.DistanceFromUser - current.DistanceFromUser;
                if (samePriority && distanceDelta <= 8.0)
                    bestZone = current;
            }
        }
        var previousId = _primaryZone?.Id;

        if (bestZone.Id == previousId)
            return; // No change

        var previousZone = _primaryZone;
        _primaryZone = bestZone;
        OnPrimaryZoneChanged?.Invoke(_primaryZone);

        // ── SCENARIO A: Entered a more-specific Spot while Area plays ─
        if (previousZone != null
            && bestZone.ZoneType == "Spot"
            && previousZone.ZoneType == "Area"
            && (bestZone.ParentZoneId == previousZone.Id || bestZone.Radius < previousZone.Radius))
        {
            OnStatusMessage?.Invoke($"🎯 Vào {bestZone.Name_Vi} — thu nhỏ âm lượng {previousZone.Name_Vi}");

            // Save Area's current position for later resume
            _session.SaveAudioPosition(previousZone.Id, _audio.GetCurrentPosition(previousZone.Id));

            // Duck the Area audio
            await _audio.SetVolumeAsync(previousZone.Id, 0.2);

            // Play Spot if not already played this session
            if (!_session.IsPlayedThisSession(bestZone.Id) && !_session.IsOnCooldown(bestZone.Id))
                await PlayZoneAsync(bestZone);
            else
                OnStatusMessage?.Invoke($"✅ {bestZone.Name_Vi} — đã nghe trong phiên này.");
        }
        // ── SCENARIO B: Entered a brand-new zone ─────────────────────
        else
        {
            if (previousZone != null)
                await _audio.StopAsync(previousZone.Id);

            if (!_session.IsPlayedThisSession(bestZone.Id) && !_session.IsOnCooldown(bestZone.Id))
                await PlayZoneAsync(bestZone);
            else
            {
                var remaining = _session.GetCooldownRemaining(bestZone.Id);
                var msg = remaining.HasValue
                    ? $"⏳ {bestZone.Name_Vi} — còn {remaining.Value.Minutes}p {remaining.Value.Seconds}s cooldown"
                    : $"✅ {bestZone.Name_Vi} — đã nghe trong phiên này.";
                OnStatusMessage?.Invoke(msg);
            }
        }
    }

    private async Task HandleExitAsync(POI exited)
    {
        OnStatusMessage?.Invoke($"🚶 Rời {exited.Name_Vi}");

        if (exited.ZoneType != "Spot") return;

        // Find parent Area still in active zones
        var parent = _currentZones.FirstOrDefault(z =>
            z.Id == exited.ParentZoneId || (z.ZoneType == "Area" && z.Radius > exited.Radius));

        if (parent == null) return;

        // Restore parent volume
        await _audio.SetVolumeAsync(parent.Id, 1.0);

        var saved = _session.GetAudioPosition(parent.Id);
        if (saved > 0)
        {
            await _audio.ResumeFromAsync(parent.Id, saved);
            OnStatusMessage?.Invoke($"▶ Tiếp tục {parent.Name_Vi} từ {saved:F0}s");
        }
    }

    private async Task PlayZoneAsync(POI zone)
    {
        OnStatusMessage?.Invoke($"▶️ Phát: {zone.Name_Vi}");
        await _audio.PlayAsync(zone.Id, zone.AudioUrl_Vi ?? "", 45); // Mock 45s duration

        _session.MarkPlayedThisSession(zone.Id);
        _session.SetCooldown(zone.Id, zone.CooldownMinutes);

        try
        {
            var now = DateTime.UtcNow;
            var sessionId = _session.SessionId;
            var history = await _db.GetZoneHistoryAsync(sessionId, zone.Id);
            if (history == null)
            {
                history = new ZoneHistory
                {
                    POI_ID = zone.Id,
                    FirstPlayedAt = now,
                    LastTriggeredAt = now,
                    PlayCount = 1,
                    Language = "vi",
                    SessionId = sessionId
                };
            }
            else
            {
                history.LastTriggeredAt = now;
                history.PlayCount += 1;
            }

            await _db.SaveZoneHistoryAsync(history);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Geofence] Save history error: {ex.Message}");
        }
    }

    private async Task ClearMonitoringStateAsync(string? statusMessage = null)
    {
        if (_primaryZone != null)
        {
            await _audio.StopAllAsync();
            _primaryZone = null;
            OnPrimaryZoneChanged?.Invoke(null);
        }

        if (_currentZones.Count > 0)
        {
            _currentZones = new List<POI>();
            OnActiveZonesChanged?.Invoke(_currentZones);
        }

        if (!string.IsNullOrWhiteSpace(statusMessage))
            OnStatusMessage?.Invoke(statusMessage);
    }

    private void LogEntry(POI zone)
    {
        OnStatusMessage?.Invoke($"✅ Vào vùng: {zone.Name_Vi} ({zone.DistanceFromUser:F0}m)");
    }

    private bool ShouldProcess(Microsoft.Maui.Devices.Sensors.Location newLoc)
    {
        if (_lastLocation == null)
            return true;

        var timeSinceLastMs = (DateTime.UtcNow - _lastLocation.Timestamp.UtcDateTime).TotalMilliseconds;
        if (timeSinceLastMs < GPS_DEBOUNCE_MS)
            return false;

        var dist = HaversineDistance(
            _lastLocation.Latitude, _lastLocation.Longitude,
            newLoc.Latitude, newLoc.Longitude);

        return dist >= GPS_DEBOUNCE_METERS;
    }

    private double HaversineDistance(double lat1, double lon1, double lat2, double lon2)
    {
        var dLat = (lat2 - lat1) * Math.PI / 180.0;
        var dLon = (lon2 - lon1) * Math.PI / 180.0;

        lat1 = lat1 * Math.PI / 180.0;
        lat2 = lat2 * Math.PI / 180.0;

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2) * Math.Cos(lat1) * Math.Cos(lat2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return EARTH_RADIUS_M * c;
    }
}
