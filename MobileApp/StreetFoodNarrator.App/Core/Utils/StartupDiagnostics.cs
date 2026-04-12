using Microsoft.Maui.Storage;

namespace StreetFoodNarrator.App.Core.Utils;

public static class StartupDiagnostics
{
    private static readonly object FileLock = new();

    public static void Append(Exception ex, string stage)
    {
        if (ex == null)
            return;

        var content = $"[{DateTime.Now:O}] Stage={stage}\n{ex}\n\n";
        AppendRaw(content);
    }

    public static void AppendMessage(string stage, string message)
    {
        var content = $"[{DateTime.Now:O}] Stage={stage}\n{message}\n\n";
        AppendRaw(content);
    }

    private static void AppendRaw(string content)
    {
        try
        {
            lock (FileLock)
            {
                var logPath = GetLogPath();
                File.AppendAllText(logPath, content);
                System.Diagnostics.Debug.WriteLine($"[StartupDiagnostics] Logged to {logPath}");

#if ANDROID
                var externalLogPath = GetAndroidExternalLogPath();
                if (!string.IsNullOrWhiteSpace(externalLogPath)
                    && !string.Equals(externalLogPath, logPath, StringComparison.OrdinalIgnoreCase))
                {
                    File.AppendAllText(externalLogPath, content);
                    System.Diagnostics.Debug.WriteLine($"[StartupDiagnostics] Logged external copy to {externalLogPath}");
                }
#endif
            }
        }
        catch
        {
            // Avoid recursive failures in diagnostics path.
        }
    }

    private static string GetLogPath()
    {
        try
        {
            var appData = FileSystem.AppDataDirectory;
            if (!string.IsNullOrWhiteSpace(appData))
                return Path.Combine(appData, "startup_crash_log.txt");
        }
        catch
        {
            // Fall through to platform-local path.
        }

        var localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localData))
            localData = AppContext.BaseDirectory;

        return Path.Combine(localData, "startup_crash_log.txt");
    }

#if ANDROID
    private static string? GetAndroidExternalLogPath()
    {
        try
        {
            var context = Android.App.Application.Context;
            var externalDir = context?.GetExternalFilesDir(null)?.AbsolutePath;
            if (string.IsNullOrWhiteSpace(externalDir))
                return null;

            return Path.Combine(externalDir, "startup_crash_log.txt");
        }
        catch
        {
            return null;
        }
    }
#endif
}