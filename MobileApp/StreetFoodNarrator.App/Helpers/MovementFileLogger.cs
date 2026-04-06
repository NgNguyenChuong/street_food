namespace StreetFoodNarrator.App.Helpers;

using System.Globalization;
using System.Text;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Storage;

/// <summary>
/// Writes a lightweight movement trace that can be pulled with adb for field debugging.
/// </summary>
public static class MovementFileLogger
{
    private const string LogFileName = "movement_trace.log";
    private const long MaxLogBytes = 1_500_000;
    private const int KeepTailLines = 1600;
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly TimeSpan MinLocationInterval = TimeSpan.FromMilliseconds(900);
    private static DateTime _lastLocationLoggedUtc = DateTime.MinValue;

    public static string GetLogFilePath()
        => Path.Combine(FileSystem.AppDataDirectory, LogFileName);

    public static Task LogEventAsync(string source, string message)
        => AppendLineAsync($"{DateTime.UtcNow:O}|{source}|{message}");

    public static Task LogLocationAsync(string source, Location location, string? note = null)
    {
        var now = DateTime.UtcNow;
        if ((now - _lastLocationLoggedUtc) < MinLocationInterval)
            return Task.CompletedTask;

        _lastLocationLoggedUtc = now;

        var lat = location.Latitude.ToString("F7", CultureInfo.InvariantCulture);
        var lon = location.Longitude.ToString("F7", CultureInfo.InvariantCulture);
        var acc = location.Accuracy?.ToString("F1", CultureInfo.InvariantCulture) ?? "na";
        var speed = location.Speed?.ToString("F2", CultureInfo.InvariantCulture) ?? "na";
        var heading = location.Course?.ToString("F1", CultureInfo.InvariantCulture) ?? "na";
        var altitude = location.Altitude?.ToString("F1", CultureInfo.InvariantCulture) ?? "na";
        var fixUtc = location.Timestamp.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

        var line = $"{now:O}|{source}|lat={lat}|lon={lon}|acc={acc}|spd={speed}|head={heading}|alt={altitude}|fix={fixUtc}";
        if (!string.IsNullOrWhiteSpace(note))
            line = $"{line}|{note}";

        return AppendLineAsync(line);
    }

    private static async Task AppendLineAsync(string line)
    {
        try
        {
            await Gate.WaitAsync();

            var path = GetLogFilePath();
            var folder = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(folder))
                Directory.CreateDirectory(folder);

            await RotateIfNeededAsync(path);
            await File.AppendAllTextAsync(path, line + Environment.NewLine, Encoding.UTF8);
        }
        catch
        {
            // Never break runtime flow because of diagnostic logging.
        }
        finally
        {
            Gate.Release();
        }
    }

    private static async Task RotateIfNeededAsync(string path)
    {
        if (!File.Exists(path))
            return;

        var info = new FileInfo(path);
        if (info.Length < MaxLogBytes)
            return;

        var lines = await File.ReadAllLinesAsync(path, Encoding.UTF8);
        var tail = lines.Length <= KeepTailLines
            ? lines
            : lines.Skip(lines.Length - KeepTailLines).ToArray();

        var marker = $"{DateTime.UtcNow:O}|system|log-truncated|maxBytes={MaxLogBytes}";
        var rewritten = new string[tail.Length + 1];
        rewritten[0] = marker;
        Array.Copy(tail, 0, rewritten, 1, tail.Length);
        await File.WriteAllLinesAsync(path, rewritten, Encoding.UTF8);
    }
}
