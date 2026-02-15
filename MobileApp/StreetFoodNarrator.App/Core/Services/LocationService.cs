namespace StreetFoodNarrator.App.Core.Services;

/// <summary>
/// Wraps MAUI Geolocation API for continuous GPS tracking.
/// Also provides a simulated mode for emulator testing.
/// </summary>
public class LocationService : ILocationService
{
    private CancellationTokenSource? _cts;
    private bool _isRunning = false;

    public bool IsRunning => _isRunning;
    public event Action<Microsoft.Maui.Devices.Sensors.Location>? OnLocationUpdated;

    public async Task StartAsync()
    {
        if (_isRunning) return;

        var status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
        if (status != PermissionStatus.Granted)
        {
            System.Diagnostics.Debug.WriteLine("[GPS] Permission denied.");
            return;
        }

        _isRunning = true;
        _cts = new CancellationTokenSource();

        _ = Task.Run(async () =>
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    var loc = await Geolocation.GetLocationAsync(new GeolocationRequest
                    {
                        DesiredAccuracy = GeolocationAccuracy.Best,
                        Timeout = TimeSpan.FromSeconds(10)
                    }, _cts.Token);

                    if (loc != null)
                        OnLocationUpdated?.Invoke(loc);
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

                await Task.Delay(2500, _cts.Token);
            }

            _isRunning = false;
        });

        await Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        _cts?.Cancel();
        _isRunning = false;
        await Task.CompletedTask;
    }
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

    // Simulated path: Vinh Khanh area (lon=106.692x, lat=10.762x)
    private static readonly (double lat, double lon, string label)[] _path = new[]
    {
        (10.7600, 106.6900, "Ngoài khu vực"),
        (10.7610, 106.6910, "Đang tiến đến..."),
        (10.7620, 106.6920, "Gần Vĩnh Khánh"),
        (10.7626, 106.6927, "Vào khu Vĩnh Khánh!"),        // Area
        (10.7627, 106.6928, "Đang khám phá..."),
        (10.7628, 106.6929, "Đến gần Bánh Mì Ba Lẹ"),
        (10.7628, 106.6929, "Vào Bánh Mì Ba Lẹ!"),        // Spot 1
        (10.7630, 106.6932, "Rời Bánh Mì, vẫn ở Vĩnh Khánh"),
        (10.7632, 106.6935, "Đến Gỏi Cuốn Tươi Ngon"),
        (10.7632, 106.6935, "Vào Gỏi Cuốn!"),            // Spot 2
        (10.7640, 106.6945, "Rời Vĩnh Khánh..."),
        (10.7650, 106.6960, "Ngoài khu vực — im lặng"),
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

        _ = Task.Run(async () =>
        {
            while (_stepIndex < _path.Length && !_cts.Token.IsCancellationRequested)
            {
                var (lat, lon, _) = _path[_stepIndex];
                var loc = new Microsoft.Maui.Devices.Sensors.Location(lat, lon)
                {
                    Timestamp = DateTimeOffset.UtcNow,
                    Accuracy = 5.0
                };
                OnLocationUpdated?.Invoke(loc);
                _stepIndex++;
                await Task.Delay(3500, _cts.Token); // Step every 3.5s
            }
            IsRunning = false;
        }, _cts.Token);

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

    public async Task StopAsync()
    {
        _cts?.Cancel();
        IsRunning = false;
        await Task.CompletedTask;
    }
}
