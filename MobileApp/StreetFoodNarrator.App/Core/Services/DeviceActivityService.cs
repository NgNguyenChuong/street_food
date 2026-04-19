using System.Text;
using System.Text.Json;

namespace StreetFoodNarrator.App.Core.Services;

/// <summary>
/// Sends periodic heartbeat to backend to track online/offline status.
/// Started when app resumes, stopped when app goes to background.
/// </summary>
public class DeviceActivityService
{
    private readonly HttpClient _httpClient;
    private System.Threading.Timer? _heartbeatTimer;
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(60);
    private bool _isRunning;

    public DeviceActivityService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// Start sending heartbeats (call on app resume / startup).
    /// </summary>
    public void Start()
    {
        if (_isRunning) return;
        _isRunning = true;

        // Send immediately, then every 60s
        _ = SendHeartbeatAsync();
        _heartbeatTimer = new System.Threading.Timer(
            _ => _ = SendHeartbeatAsync(),
            null,
            HeartbeatInterval,
            HeartbeatInterval);
    }

    /// <summary>
    /// Stop heartbeats and send disconnect (call on app pause / sleep).
    /// </summary>
    public void Stop()
    {
        if (!_isRunning) return;
        _isRunning = false;

        _heartbeatTimer?.Dispose();
        _heartbeatTimer = null;

        _ = SendDisconnectAsync();
    }

    private static string GetDeviceId() => AppConfig.GetOrCreateDeviceId();

    private string GetPlatform()
    {
#if ANDROID
        return "Android";
#elif IOS
        return "iOS";
#else
        return "Unknown";
#endif
    }

    private async Task SendHeartbeatAsync()
    {
        try
        {
            var url = AppConfig.BuildApiUrl("api/device-activity/heartbeat");
            var payload = new
            {
                deviceId = GetDeviceId(),
                platform = GetPlatform(),
                userRole = "tourist",
                clientType = GetPlatform().ToLowerInvariant()
            };
            var content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");
            await _httpClient.PostAsync(url, content);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DeviceActivity] Heartbeat failed: {ex.Message}");
        }
    }

    private async Task SendDisconnectAsync()
    {
        try
        {
            var url = AppConfig.BuildApiUrl("api/device-activity/disconnect");
            var payload = new { deviceId = GetDeviceId() };
            var content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");
            await _httpClient.PostAsync(url, content);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DeviceActivity] Disconnect failed: {ex.Message}");
        }
    }
}
