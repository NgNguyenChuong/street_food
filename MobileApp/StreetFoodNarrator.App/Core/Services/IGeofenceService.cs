namespace StreetFoodNarrator.App.Core.Services.Implementations;

using StreetFoodNarrator.App.Core.Models;
using StreetFoodNarrator.App.Core.Services;
using StreetFoodNarrator.App.Core.Utils;
using System.Net.Http.Json;

/// <summary>
/// Core geofencing logic - the brain of the system.
/// Implements smart overlapping zones algorithm.
/// </summary>
public class GeofenceService : IGeofenceService
{
    // ── Constants ─────────────────────────────────────────────────
    private const double GPS_DEBOUNCE_METERS_DEFAULT = 4.0;
    private const double GPS_DEBOUNCE_METERS_INSIDE = 1.8;
    private const int GPS_DEBOUNCE_MS_DEFAULT = 2200;
    private const int GPS_DEBOUNCE_MS_INSIDE = 900;
    private const double MAX_ACCEPTABLE_ACCURACY_INSIDE_METERS = 28.0;
    private const double MAX_ACCEPTABLE_ACCURACY_NEAR_METERS = 50.0;
    private const double EARTH_RADIUS_M = 6_371_000.0;
    private const int MOVEMENT_UPLOAD_INTERVAL_SECONDS = 3;
    private const double MOVEMENT_UPLOAD_DISTANCE_METERS = 5.0;
    private const int SPOT_MAX_COOLDOWN_MINUTES = 4;
    private const int LIVE_STATUS_INTERVAL_SECONDS = 2;

    // ── Dependencies ──────────────────────────────────────────────
    private readonly IZoneRepository _repository;
    private readonly IAudioService _audio;
    private readonly UserSession _session;
    private readonly ILocalDatabaseService _db;
    private readonly HttpClient _httpClient;

    // ── State ─────────────────────────────────────────────────────
    private Microsoft.Maui.Devices.Sensors.Location? _lastLocation;
    private List<POI> _currentZones = new();
    private POI? _primaryZone = null;
    private bool _localLoaded = false;
    private bool _isMonitoringEnabled = true;
    private readonly Dictionary<int, DateTime> _zoneEnteredAtUtc = new();
    private DateTime _lastMovementUploadAtUtc = DateTime.MinValue;
    private Microsoft.Maui.Devices.Sensors.Location? _lastUploadedMovementLocation;
    private DateTime _lastLiveStatusAtUtc = DateTime.MinValue;

    // ── Events ────────────────────────────────────────────────────
    public event Action<List<POI>>? OnActiveZonesChanged;
    public event Action<POI?>? OnPrimaryZoneChanged;
    public event Action<string>? OnStatusMessage;

    public GeofenceService(IZoneRepository repository, IAudioService audio, UserSession session, ILocalDatabaseService db, HttpClient httpClient)
    {
        _repository = repository;
        _audio = audio;
        _session = session;
        _db = db;
        _httpClient = httpClient;
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
            // Repository is usually loaded by MainViewModel during startup.
            // Avoid duplicate SQLite load on first GPS callback.
            if (!_repository.IsSeeded)
                await _repository.LoadLocalAsync();

            // Self-heal: if local cache is stale/corrupt (e.g. only one POI), force one sync pass.
            if (AppConfig.UseBackendApi && _repository.GetAllActiveZones().Count <= 1)
            {
                await _repository.SyncFromMongoAsync();
                await _repository.LoadLocalAsync();
            }

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
            .Where(z =>
            {
                var effectiveRadius = GetEffectiveDetectionRadiusMeters(z, newLocation);
                return z.DistanceFromUser <= effectiveRadius;
            })
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
        EmitLiveTrackingStatus(candidates, insideZones);

        await UploadMovementPingAsync(newLocation, insideZones);

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
            await ClearMonitoringStateAsync("GPS đang ở xa quán, tạm tắt geofence để tiết kiệm pin.");
            return;
        }

        OnStatusMessage?.Invoke("Đã bật geofence watch mode cho quán gần nhất.");
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

            if (ShouldPlayZoneNarration(bestZone))
                await PlayZoneAsync(bestZone);
            else
                OnStatusMessage?.Invoke(BuildCooldownBlockedMessage(bestZone));
        }
        // ── SCENARIO B: Entered a brand-new zone ─────────────────────
        else
        {
            if (previousZone != null)
                await _audio.StopAsync(previousZone.Id);

            if (ShouldPlayZoneNarration(bestZone))
                await PlayZoneAsync(bestZone);
            else
                OnStatusMessage?.Invoke(BuildCooldownBlockedMessage(bestZone));
        }
    }

    private async Task HandleExitAsync(POI exited)
    {
        OnStatusMessage?.Invoke($"🚶 Rời {exited.Name_Vi}");

        if (_zoneEnteredAtUtc.TryGetValue(exited.Id, out var enteredAtUtc))
        {
            var dwellSeconds = Math.Max(0, (int)(DateTime.UtcNow - enteredAtUtc).TotalSeconds);
            _zoneEnteredAtUtc.Remove(exited.Id);
            await UploadMobileLogAsync(exited, _lastLocation, "ExitZone", "GeofenceExit", false, dwellSeconds);
        }

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
        var fallbackText = !string.IsNullOrWhiteSpace(zone.Description_Vi) 
            ? zone.Description_Vi 
            : (zone.Name_Vi ?? "");
        await _audio.PlayAsync(zone.Id, zone.AudioUrl_Vi ?? "", 45, fallbackText); // Mock 45s duration

        await UploadMobileLogAsync(zone, _lastLocation, "NarrationPlayed", "GeofenceEnter", true, null);

        _session.MarkPlayedThisSession(zone.Id);
        _session.SetCooldown(zone.Id, GetEffectiveCooldownMinutes(zone));

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
                    Language = ResolveCurrentLanguageCode(),
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

    private bool ShouldPlayZoneNarration(POI zone)
    {
        var effectiveCooldown = GetEffectiveCooldownMinutes(zone);

        // Cooldown=0 keeps one-play-per-session behavior to avoid spam loops.
        if (effectiveCooldown <= 0)
            return !_session.IsPlayedThisSession(zone.Id);

        return !_session.IsOnCooldown(zone.Id);
    }

    private string BuildCooldownBlockedMessage(POI zone)
    {
        var effectiveCooldown = GetEffectiveCooldownMinutes(zone);
        if (effectiveCooldown <= 0)
            return $"✅ {zone.Name_Vi} — đã nghe trong phiên này.";

        var remaining = _session.GetCooldownRemaining(zone.Id);
        return remaining.HasValue
            ? $"⏳ {zone.Name_Vi} — còn {remaining.Value.Minutes}p {remaining.Value.Seconds}s cooldown"
            : $"⏳ {zone.Name_Vi} — đang trong cooldown.";
    }

    private static int GetEffectiveCooldownMinutes(POI zone)
    {
        var rawCooldown = Math.Max(0, zone.CooldownMinutes);
        if (string.Equals(zone.ZoneType, "Spot", StringComparison.OrdinalIgnoreCase) && rawCooldown > 0)
            return Math.Min(rawCooldown, SPOT_MAX_COOLDOWN_MINUTES);

        return rawCooldown;
    }

    private void EmitLiveTrackingStatus(List<POI> candidates, List<POI> insideZones)
    {
        var now = DateTime.UtcNow;
        if ((now - _lastLiveStatusAtUtc).TotalSeconds < LIVE_STATUS_INTERVAL_SECONDS)
            return;

        _lastLiveStatusAtUtc = now;

        if (insideZones.Count > 0)
        {
            var nearestInside = insideZones
                .OrderBy(z => z.DistanceFromUser)
                .FirstOrDefault();

            if (nearestInside != null)
            {
                OnStatusMessage?.Invoke(
                    $"Đang trong vùng {nearestInside.Name_Vi} ({nearestInside.DistanceFromUser:F0}m)");
                return;
            }
        }

        var nearest = candidates
            .OrderBy(z => z.DistanceFromUser)
            .FirstOrDefault();

        if (nearest != null)
        {
            OnStatusMessage?.Invoke(
                $"Đang theo dõi gần {nearest.Name_Vi} ({nearest.DistanceFromUser:F0}m)");
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

        _zoneEnteredAtUtc.Clear();

        if (!string.IsNullOrWhiteSpace(statusMessage))
            OnStatusMessage?.Invoke(statusMessage);
    }

    private void LogEntry(POI zone)
    {
        _zoneEnteredAtUtc[zone.Id] = DateTime.UtcNow;
        OnStatusMessage?.Invoke($"✅ Vào vùng: {zone.Name_Vi} ({zone.DistanceFromUser:F0}m)");

        // Record zone entry even when narration may be blocked by cooldown.
        _ = UploadMobileLogAsync(zone, _lastLocation, "GeofenceEnter", "GeofenceEnter", false, null);
    }

    private async Task UploadMovementPingAsync(Microsoft.Maui.Devices.Sensors.Location location, List<POI> insideZones)
    {
        if (!VinhKhanhAreaGuard.IsInside(location.Latitude, location.Longitude))
            return;

        var now = DateTime.UtcNow;

        POI? nearestPoi;
        if (insideZones.Any())
        {
            nearestPoi = insideZones.OrderBy(z => z.DistanceFromUser).FirstOrDefault();
        }
        else
        {
            // If GPS jitter misses strict in-zone match, still record near-spot movement.
            var activeSpots = _repository.GetAllActiveZones()
                .Where(z => string.Equals(z.ZoneType, "Spot", StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var z in activeSpots)
            {
                z.DistanceFromUser = HaversineDistance(
                    location.Latitude,
                    location.Longitude,
                    z.Latitude,
                    z.Longitude);
            }

            nearestPoi = activeSpots
                .OrderBy(z => z.DistanceFromUser)
                .FirstOrDefault();

            // Skip noisy pings when user is too far from all POIs.
            if (nearestPoi == null || nearestPoi.DistanceFromUser > AppConfig.TrackingNearMeters)
                return;
        }

        if (nearestPoi == null)
            return;

        var secondsSinceLast = (now - _lastMovementUploadAtUtc).TotalSeconds;
        var movedEnough = _lastUploadedMovementLocation == null ||
            HaversineDistance(
                _lastUploadedMovementLocation.Latitude,
                _lastUploadedMovementLocation.Longitude,
                location.Latitude,
                location.Longitude) >= MOVEMENT_UPLOAD_DISTANCE_METERS;

        if (secondsSinceLast < MOVEMENT_UPLOAD_INTERVAL_SECONDS && !movedEnough)
            return;

        await UploadMobileLogAsync(nearestPoi, location, "LocationPing", "Proximity", false, null);
        _lastMovementUploadAtUtc = now;
        _lastUploadedMovementLocation = location;
    }

    private async Task UploadMobileLogAsync(
        POI poi,
        Microsoft.Maui.Devices.Sensors.Location? location,
        string actionType,
        string triggerType,
        bool wasPlayed,
        int? dwellSeconds)
    {
        try
        {
            var effectiveLatitude = location?.Latitude ?? poi.Latitude;
            var effectiveLongitude = location?.Longitude ?? poi.Longitude;

            if (!VinhKhanhAreaGuard.IsInside(effectiveLatitude, effectiveLongitude))
                return;

            var baseUrl = AppConfig.GetResolvedApiBaseUrl()?.TrimEnd('/');
            if (string.IsNullOrWhiteSpace(baseUrl))
                return;

            var payload = new MobileNarrationLogPayload
            {
                POI_ID = poi.Id,
                UserId = _session.SessionId,
                SessionId = _session.SessionId,
                DeviceId = GetOrCreateAnonymousDeviceId(),
                Platform = Microsoft.Maui.Devices.DeviceInfo.Platform.ToString(),
                Model = Microsoft.Maui.Devices.DeviceInfo.Model,
                OsVersion = Microsoft.Maui.Devices.DeviceInfo.VersionString,
                AppVersion = Microsoft.Maui.ApplicationModel.AppInfo.Current.VersionString,
                Language = ResolveCurrentLanguageCode(),
                TriggerType = triggerType,
                ActionType = actionType,
                TriggeredAt = DateTime.UtcNow,
                UserLatitude = Convert.ToDecimal(effectiveLatitude),
                UserLongitude = Convert.ToDecimal(effectiveLongitude),
                DwellSeconds = dwellSeconds,
                WasPlayed = wasPlayed
            };

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(6));
            var response = await _httpClient.PostAsJsonAsync($"{baseUrl}/api/Analytics/narration-logs/mobile", payload, cts.Token);
            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine(
                    $"[Geofence] Upload analytics log rejected: {(int)response.StatusCode} {response.ReasonPhrase}; body={responseBody}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Geofence] Upload analytics log failed: {ex.Message}");
        }
    }

    private static double GetEffectiveDetectionRadiusMeters(POI zone, Microsoft.Maui.Devices.Sensors.Location location)
    {
        var baseRadius = string.Equals(zone.ZoneType, "Spot", StringComparison.OrdinalIgnoreCase)
            ? AppConfig.NormalizeSpotRadiusMeters(zone.Radius)
            : (zone.Radius > 0 ? zone.Radius : 50);

        var accuracy = Math.Max(0, location.Accuracy ?? 0);
        var dynamicAccuracyBuffer = Math.Min(12, accuracy * 0.35);
        var spotBuffer = string.Equals(zone.ZoneType, "Spot", StringComparison.OrdinalIgnoreCase)
            ? AppConfig.SpotZoneGpsErrorBufferMeters
            : 0;

        return baseRadius + spotBuffer + dynamicAccuracyBuffer;
    }

    private static string GetOrCreateAnonymousDeviceId()
    {
        const string key = "analytics_anonymous_device_id";
        var current = Microsoft.Maui.Storage.Preferences.Get(key, string.Empty);
        if (!string.IsNullOrWhiteSpace(current))
            return current;

        var created = $"m-{Guid.NewGuid():N}";
        Microsoft.Maui.Storage.Preferences.Set(key, created);
        return created;
    }

    private static string ResolveCurrentLanguageCode()
    {
        var raw = Microsoft.Maui.Storage.Preferences.Get(AppConfig.LanguagePrefKey, "vi");
        if (string.IsNullOrWhiteSpace(raw))
            return "vi";

        var value = raw.Trim().ToLowerInvariant();
        if (value.StartsWith("vi")) return "vi";
        if (value.StartsWith("en")) return "en";
        if (value.StartsWith("zh") || value.StartsWith("cn")) return "zh";
        if (value.StartsWith("ja")) return "ja";
        if (value.StartsWith("ko")) return "ko";
        if (value.StartsWith("fr")) return "fr";
        return value;
    }

    private bool ShouldProcess(Microsoft.Maui.Devices.Sensors.Location newLoc)
    {
        if (!IsAccuracyAcceptable(newLoc))
            return false;

        if (_lastLocation == null)
            return true;

        var isInsideAnyZone = _currentZones.Count > 0;
        var minDebounceMs = isInsideAnyZone ? GPS_DEBOUNCE_MS_INSIDE : GPS_DEBOUNCE_MS_DEFAULT;
        var minDebounceMeters = isInsideAnyZone ? GPS_DEBOUNCE_METERS_INSIDE : GPS_DEBOUNCE_METERS_DEFAULT;

        var timeSinceLastMs = (DateTime.UtcNow - _lastLocation.Timestamp.UtcDateTime).TotalMilliseconds;
        if (timeSinceLastMs < minDebounceMs)
            return false;

        if (newLoc.Speed.HasValue && newLoc.Speed.Value > 1.3)
            return true;

        var dist = HaversineDistance(
            _lastLocation.Latitude, _lastLocation.Longitude,
            newLoc.Latitude, newLoc.Longitude);

        return dist >= minDebounceMeters;
    }

    private bool IsAccuracyAcceptable(Microsoft.Maui.Devices.Sensors.Location location)
    {
        if (!location.Accuracy.HasValue || location.Accuracy.Value <= 0)
            return true;

        var maxAllowedAccuracy = _currentZones.Count > 0
            ? MAX_ACCEPTABLE_ACCURACY_INSIDE_METERS
            : MAX_ACCEPTABLE_ACCURACY_NEAR_METERS;

        return location.Accuracy.Value <= maxAllowedAccuracy;
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

    private sealed class MobileNarrationLogPayload
    {
        public int POI_ID { get; set; }
        public string? UserId { get; set; }
        public string? SessionId { get; set; }
        public string? DeviceId { get; set; }
        public string? Platform { get; set; }
        public string? Model { get; set; }
        public string? OsVersion { get; set; }
        public string? AppVersion { get; set; }
        public string? Language { get; set; }
        public string? TriggerType { get; set; }
        public string? ActionType { get; set; }
        public DateTime TriggeredAt { get; set; }
        public decimal? UserLatitude { get; set; }
        public decimal? UserLongitude { get; set; }
        public int? DwellSeconds { get; set; }
        public bool WasPlayed { get; set; }
    }
}
