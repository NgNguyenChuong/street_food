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

#if ANDROID
        // Start Foreground Service so Android does not kill GPS in background
        StartAndroidForegroundService();
#endif

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

                await Task.Delay(7500, _cts.Token); // 7.5s — GPS accuracy budget
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

    public async Task StopAsync()
    {
        _cts?.Cancel();
        IsRunning = false;
        await Task.CompletedTask;
    }
}
