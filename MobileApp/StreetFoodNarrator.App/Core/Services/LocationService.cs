#if ANDROID
using Android.Content;
using StreetFoodNarrator.App.Platforms.Android.Services;
#endif

namespace StreetFoodNarrator.App.Core.Services;

/// <summary>
/// Wraps MAUI Geolocation API for continuous GPS tracking.
/// On Android: starts a Foreground Service so tracking continues in background.
/// Also provides a simulated mode for emulator testing.
/// </summary>
public class LocationService : ILocationService
{
    private CancellationTokenSource? _cts;
    private bool _isRunning = false;
    private TrackingProximityState _trackingState = TrackingProximityState.Near;
    private Microsoft.Maui.Devices.Sensors.Location? _lastLocation;
    private DateTimeOffset _lastEmittedAt = DateTimeOffset.MinValue;
    private const double SignificantMovementMeters = 2.5;
    private const int StationaryDelayInsideMs = 3_000;
    private const int StationaryDelayNearMs = 7_000;
    private const int StationaryDelayFarMs = 25_000;

    public bool IsRunning => _isRunning;
    public event Action<Microsoft.Maui.Devices.Sensors.Location>? OnLocationUpdated;

    public async Task StartAsync()
    {
        if (_isRunning) return;

        var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
        if (status != PermissionStatus.Granted)
        {
            status = await MainThread.InvokeOnMainThreadAsync(
                () => Permissions.RequestAsync<Permissions.LocationWhenInUse>());
        }

        if (status != PermissionStatus.Granted)
        {
            System.Diagnostics.Debug.WriteLine("[GPS] Permission denied.");
            return;
        }

#if ANDROID
        // Request background location permission to keep geofence tracking active while app is backgrounded.
        var alwaysStatus = await Permissions.CheckStatusAsync<Permissions.LocationAlways>();
        if (alwaysStatus != PermissionStatus.Granted)
        {
            alwaysStatus = await MainThread.InvokeOnMainThreadAsync(
                () => Permissions.RequestAsync<Permissions.LocationAlways>());
        }

        if (alwaysStatus != PermissionStatus.Granted)
        {
            System.Diagnostics.Debug.WriteLine("[GPS] Background location permission not granted; tracking will run in foreground-only mode.");
        }

        // Start Foreground Service so Android does not kill GPS in background
        StartAndroidForegroundService();
#endif

        _isRunning = true;
        _cts = new CancellationTokenSource();

        _ = Task.Run(async () =>
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                var delayMs = GetDelayMs(_trackingState);

                try
                {
                    var loc = await Geolocation.GetLocationAsync(new GeolocationRequest
                    {
                        DesiredAccuracy = GetAccuracy(_trackingState),
                        Timeout = TimeSpan.FromSeconds(10)
                    }, _cts.Token);

                    if (loc != null)
                    {
                        var hasAcceptableAccuracy = IsAccuracyAcceptable(loc);
                        var stabilizedLoc = ApplyLocationStabilization(loc);
                        var significantMovement = hasAcceptableAccuracy && IsSignificantMovement(stabilizedLoc);

                        if (!hasAcceptableAccuracy)
                        {
                            delayMs = Math.Max(delayMs, GetNoisyFixDelayMs(_trackingState));

                            if (ShouldEmitHeartbeat(_trackingState) && _lastLocation != null)
                            {
                                _lastEmittedAt = DateTimeOffset.UtcNow;
                                OnLocationUpdated?.Invoke(_lastLocation);
                            }
                        }
                        else
                        {
                            if (!significantMovement)
                            {
                                // Standing still: reduce jitter and keep a modest heartbeat for UI freshness.
                                delayMs = Math.Max(delayMs, GetStationaryDelayMs(_trackingState));
                            }

                            if (significantMovement || ShouldEmitHeartbeat(_trackingState))
                            {
                                _lastLocation = stabilizedLoc;
                                _lastEmittedAt = DateTimeOffset.UtcNow;
                                OnLocationUpdated?.Invoke(stabilizedLoc);
                            }
                        }
                    }
                }
                catch (FeatureNotEnabledException)
                {
                    System.Diagnostics.Debug.WriteLine("[GPS] Location services disabled.");
                }
                catch (PermissionException)
                {
                    System.Diagnostics.Debug.WriteLine("[GPS] Permission revoked.");
                    break;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[GPS] Error: {ex.Message}");
                }

                await Task.Delay(delayMs, _cts.Token);
            }

            _isRunning = false;
        });

        await Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        _cts?.Cancel();
        _isRunning = false;
#if ANDROID
        StopAndroidForegroundService();
#endif
        await Task.CompletedTask;
    }

    public void SetTrackingState(TrackingProximityState state)
    {
        _trackingState = state;
    }

    private bool IsSignificantMovement(Microsoft.Maui.Devices.Sensors.Location location)
    {
        if (_lastLocation == null)
            return true;

        if (location.Speed.HasValue && location.Speed.Value > 1.4)
            return true;

        var movedMeters = Microsoft.Maui.Devices.Sensors.Location.CalculateDistance(
            _lastLocation,
            location,
            Microsoft.Maui.Devices.Sensors.DistanceUnits.Kilometers) * 1000.0;

        var accuracy = Math.Max(0, location.Accuracy ?? 0);
        var threshold = _trackingState switch
        {
            TrackingProximityState.Inside => Math.Max(2.5, accuracy * 0.45),
            TrackingProximityState.Near => Math.Max(SignificantMovementMeters, accuracy * 0.6),
            _ => Math.Max(6.0, accuracy * 0.75)
        };

        return movedMeters >= threshold;
    }

    private Microsoft.Maui.Devices.Sensors.Location ApplyLocationStabilization(Microsoft.Maui.Devices.Sensors.Location location)
    {
        if (_lastLocation == null || _trackingState != TrackingProximityState.Inside)
            return location;

        var movedMeters = Microsoft.Maui.Devices.Sensors.Location.CalculateDistance(
            _lastLocation,
            location,
            Microsoft.Maui.Devices.Sensors.DistanceUnits.Kilometers) * 1000.0;

        var accuracy = Math.Max(0, location.Accuracy ?? 0);
        var snapThreshold = Math.Max(1.4, accuracy * 0.22);

        // While user stands still inside zone, keep pin stable instead of following tiny GPS jitter.
        if (movedMeters <= snapThreshold && (!location.Speed.HasValue || location.Speed.Value < 0.7))
        {
            var snapped = CloneLocation(_lastLocation.Latitude, _lastLocation.Longitude, location);
            snapped.Accuracy = location.Accuracy.HasValue && _lastLocation.Accuracy.HasValue
                ? Math.Min(location.Accuracy.Value, _lastLocation.Accuracy.Value)
                : (location.Accuracy ?? _lastLocation.Accuracy);
            return snapped;
        }

        // For small drifts, blend old/new coordinate to keep movement smooth on map.
        if (movedMeters < 7.5 && (!location.Speed.HasValue || location.Speed.Value < 1.2))
        {
            var lat = (_lastLocation.Latitude * 0.68) + (location.Latitude * 0.32);
            var lon = (_lastLocation.Longitude * 0.68) + (location.Longitude * 0.32);
            return CloneLocation(lat, lon, location);
        }

        return location;
    }

    private static Microsoft.Maui.Devices.Sensors.Location CloneLocation(
        double latitude,
        double longitude,
        Microsoft.Maui.Devices.Sensors.Location source)
    {
        return new Microsoft.Maui.Devices.Sensors.Location(latitude, longitude)
        {
            Accuracy = source.Accuracy,
            Altitude = source.Altitude,
            Course = source.Course,
            Speed = source.Speed,
            Timestamp = source.Timestamp
        };
    }

    private bool IsAccuracyAcceptable(Microsoft.Maui.Devices.Sensors.Location location)
    {
        var accuracy = location.Accuracy;
        if (!accuracy.HasValue || accuracy.Value <= 0)
            return true;

        var maxAllowed = _trackingState switch
        {
            TrackingProximityState.Inside => 28.0,
            TrackingProximityState.Near => 45.0,
            _ => 85.0
        };

        return accuracy.Value <= maxAllowed;
    }

    private bool ShouldEmitHeartbeat(TrackingProximityState state)
    {
        var heartbeatMs = state switch
        {
            TrackingProximityState.Inside => 3_500,
            TrackingProximityState.Near => 8_000,
            _ => 20_000
        };

        return (DateTimeOffset.UtcNow - _lastEmittedAt).TotalMilliseconds >= heartbeatMs;
    }

    private static int GetStationaryDelayMs(TrackingProximityState state) => state switch
    {
        TrackingProximityState.Inside => StationaryDelayInsideMs,
        TrackingProximityState.Near => StationaryDelayNearMs,
        _ => StationaryDelayFarMs
    };

    private static int GetNoisyFixDelayMs(TrackingProximityState state) => state switch
    {
        TrackingProximityState.Inside => 4_000,
        TrackingProximityState.Near => 9_000,
        _ => 30_000
    };

#if ANDROID
    private static void StartAndroidForegroundService()
    {
        var context = global::Android.App.Application.Context;
        var intent  = new Intent(context, typeof(GpsForegroundService));
        intent.SetAction(GpsForegroundService.ActionStart);
        if (global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.O)
            context.StartForegroundService(intent);
        else
            context.StartService(intent);
    }

    private static void StopAndroidForegroundService()
    {
        var context = global::Android.App.Application.Context;
        var intent  = new Intent(context, typeof(GpsForegroundService));
        intent.SetAction(GpsForegroundService.ActionStop);
        context.StartService(intent);
    }
#endif

    private static int GetDelayMs(TrackingProximityState state) => state switch
    {
        TrackingProximityState.Far => 30_000,
        TrackingProximityState.Inside => 2_000,
        _ => 5_000
    };

    private static GeolocationAccuracy GetAccuracy(TrackingProximityState state) => state switch
    {
        TrackingProximityState.Far => GeolocationAccuracy.Medium,
        TrackingProximityState.Inside => GeolocationAccuracy.Best,
        _ => GeolocationAccuracy.Best
    };
}

/// <summary>
/// Simulated location service for emulator testing.
/// Walks through predefined GPS waypoints around Vinh Khanh area.
/// </summary>
public class SimulatedLocationService : ILocationService
{
    private CancellationTokenSource? _cts;
    public bool IsRunning { get; private set; }
    public event Action<Microsoft.Maui.Devices.Sensors.Location>? OnLocationUpdated;

    // Simulated path: đi dọc Phố ẩm thực Vĩnh Khánh từ cổng chính vào trong
    // Tọa độ thực: Cổng chính 10.7619153, 106.701912
    private static readonly (double lat, double lon, string label)[] _path = new[]
    {
        (StreetFoodNarrator.App.AppConfig.DefaultLatitude, StreetFoodNarrator.App.AppConfig.DefaultLongitude, "Ngoài khu vực"),
        (10.7610, 106.7005, "Đang tiến đến Vĩnh Khánh..."),
        (10.7615, 106.7012, "Gần cổng chào Vĩnh Khánh"),
        (10.7619, 106.7019, "Vào cổng Phố ẩm thực Vĩnh Khánh!"),  // Cổng chính
        (10.7622, 106.7022, "Đang khám phá phố ẩm thực..."),
        (10.7625, 106.7025, "Đến gần Bánh Mì Ba Lẹ"),
        (10.7628, 106.7028, "Vào Bánh Mì Ba Lẹ!"),               // Spot 1
        (10.7630, 106.7032, "Rời Bánh Mì, vẫn ở Vĩnh Khánh"),
        (10.7633, 106.7036, "Đến Gỏi Cuốn Tươi Ngon"),
        (10.7635, 106.7038, "Vào Gỏi Cuốn!"),                    // Spot 2
        (10.7645, 106.7050, "Rời Vĩnh Khánh..."),
        (10.7660, 106.7065, "Ngoài khu vực — im lặng"),
    };

    private int _stepIndex = 0;
    public int CurrentStep => _stepIndex;
    public int TotalSteps => _path.Length;
    public string CurrentLabel => _stepIndex < _path.Length ? _path[_stepIndex].label : "—";

    public async Task StartAsync()
    {
        IsRunning = true;
        _cts = new CancellationTokenSource();
        _stepIndex = 0;

        // Chỉ phát vị trí ban đầu (step 0) — KHÔNG tự động đi tiếp.
        // Dùng nút "Step" (StepForward) để di chuyển thủ công.
        var (lat, lon, _) = _path[_stepIndex];
        var loc = new Microsoft.Maui.Devices.Sensors.Location(lat, lon)
        {
            Timestamp = DateTimeOffset.UtcNow,
            Accuracy = 5.0
        };
        OnLocationUpdated?.Invoke(loc);
        _stepIndex++;

        await Task.CompletedTask;
    }

    public void StepForward()
    {
        if (_stepIndex < _path.Length)
        {
            var (lat, lon, _) = _path[_stepIndex];
            var loc = new Microsoft.Maui.Devices.Sensors.Location(lat, lon)
            {
                Timestamp = DateTimeOffset.UtcNow,
                Accuracy = 5.0
            };
            OnLocationUpdated?.Invoke(loc);
            _stepIndex++;
        }
    }

    public void Reset() => _stepIndex = 0;

    public void SetTrackingState(TrackingProximityState state)
    {
        // Simulator advances manually; tracking interval changes do not apply here.
    }

    public async Task StopAsync()
    {
        _cts?.Cancel();
        IsRunning = false;
        await Task.CompletedTask;
    }
}
