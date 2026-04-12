// Android-only file — API-level guards are all explicit runtime version checks.
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;

namespace StreetFoodNarrator.App.Platforms.Android.Services;

#pragma warning disable CA1416 // Runtime guards cover all API-level differences
#pragma warning disable CS0618 // StopForeground(bool) only used on API 21-23 path

/// <summary>
/// Android Foreground Service — giữ GPS tracking hoạt động khi app chạy nền.
/// Hiển thị persistent notification để Android không kill process.
/// </summary>
[Service(ForegroundServiceType = ForegroundService.TypeLocation)]
public class GpsForegroundService : Service
{
    public const string ChannelId     = "gps_tracking_channel";
    public const string ActionStart   = "ACTION_START_GPS";
    public const string ActionStop    = "ACTION_STOP_GPS";
    private const int   NotificationId = 1001;

    private static bool _isRunning;
    public static bool IsRunning => _isRunning;

    public override IBinder? OnBind(Intent? intent) => null;

    public override void OnCreate()
    {
        base.OnCreate();
        CreateNotificationChannel();
    }

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        if (intent?.Action == ActionStop)
        {
            // Use analyzer-friendly OperatingSystem.IsAndroidVersionAtLeast() guards
            if (OperatingSystem.IsAndroidVersionAtLeast(24))
                StopForeground(StopForegroundFlags.Remove);
            else
                StopForeground(removeNotification: true); // API 21-23 only
            StopSelf();
            _isRunning = false;
            return StartCommandResult.NotSticky;
        }

        // Build and show persistent notification
        var notification = BuildNotification("📍 Đang theo dõi vị trí", "Phố ẩm thực Vĩnh Khánh đang hoạt động");

        if (OperatingSystem.IsAndroidVersionAtLeast(29))
            StartForeground(NotificationId, notification, ForegroundService.TypeLocation);
        else
            StartForeground(NotificationId, notification);

        _isRunning = true;
        return StartCommandResult.Sticky; // Sticky = restart if killed
    }

    public override void OnDestroy()
    {
        _isRunning = false;
        base.OnDestroy();
    }

    // ── Helpers ───────────────────────────────────────────────────

    private Notification BuildNotification(string title, string content)
    {
        // PendingIntentFlags.Immutable requires API 23+
        var piFlags = OperatingSystem.IsAndroidVersionAtLeast(23)
            ? PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable
            : PendingIntentFlags.UpdateCurrent;

        var pendingIntent = PendingIntent.GetActivity(
            this, 0,
            new Intent(this, typeof(MainActivity)),
            piFlags)!;

        var stopIntent = new Intent(this, typeof(GpsForegroundService));
        stopIntent.SetAction(ActionStop);
        var stopPending = PendingIntent.GetService(this, 0, stopIntent, piFlags)!;

        return new NotificationCompat.Builder(this, ChannelId)
            .SetContentTitle(title)!
            .SetContentText(content)!
            .SetSmallIcon(Resource.Mipmap.appicon)!
            .SetContentIntent(pendingIntent)!
            .SetOngoing(true)!
            .SetPriority(NotificationCompat.PriorityLow)!
            .AddAction(0, "Dừng", stopPending)!
            .Build()!;
    }

    private void CreateNotificationChannel()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26)) return;

        var channel = new NotificationChannel(
            ChannelId,
            "GPS Tracking",
            NotificationImportance.Low);
        channel.Description         = "Theo dõi vị trí khi dùng Saigon Guide";
        channel.LockscreenVisibility = NotificationVisibility.Public;

        var manager = (NotificationManager?)GetSystemService(NotificationService);
        manager?.CreateNotificationChannel(channel);
    }  // EnsureChannel
}  // GpsForegroundService

#pragma warning restore CS0618
#pragma warning restore CA1416
