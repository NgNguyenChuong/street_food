using System.Security.Cryptography;

namespace StreetFoodNarrator.App.Core.Services.Implementations;

/// <summary>
/// Caches remote POI images under AppDataDirectory for offline display.
/// </summary>
public static class PoiImageCacheService
{
    private static readonly SemaphoreSlim _gate = new(1, 1);

    public static string? GetCachedPathIfExists(string baseUrl, string? imageUrl)
    {
        var absoluteUrl = NormalizeToAbsoluteUrl(baseUrl, imageUrl);
        if (string.IsNullOrWhiteSpace(absoluteUrl))
            return null;

        var canonicalPath = BuildCachePath(absoluteUrl);
        if (File.Exists(canonicalPath))
            return canonicalPath;

        // Backward compatibility: old versions hashed the full URL (including query).
        var legacyPath = BuildCachePathLegacy(absoluteUrl);
        return File.Exists(legacyPath) ? legacyPath : null;
    }

    public static async Task<bool> EnsureCachedAsync(
        HttpClient httpClient,
        string baseUrl,
        string? imageUrl,
        CancellationToken cancellationToken = default)
    {
        var absoluteUrl = NormalizeToAbsoluteUrl(baseUrl, imageUrl);
        if (string.IsNullOrWhiteSpace(absoluteUrl))
            return false;

        var cachePath = BuildCachePath(absoluteUrl);
        if (File.Exists(cachePath))
            return true;

        var legacyPath = BuildCachePathLegacy(absoluteUrl);
        if (File.Exists(legacyPath))
            return true;

        var hasInternet = Connectivity.Current.NetworkAccess == NetworkAccess.Internet ||
                          Connectivity.Current.NetworkAccess == NetworkAccess.ConstrainedInternet;
        if (!hasInternet)
            return false;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (File.Exists(cachePath) || File.Exists(legacyPath))
                return true;

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(12));

            using var response = await httpClient.GetAsync(absoluteUrl, timeoutCts.Token);
            if (!response.IsSuccessStatusCode)
                return false;

            var bytes = await response.Content.ReadAsByteArrayAsync(timeoutCts.Token);
            if (bytes.Length == 0)
                return false;

            await File.WriteAllBytesAsync(cachePath, bytes, timeoutCts.Token);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ImageCache] EnsureCachedAsync failed: {ex.Message}");
            return false;
        }
        finally
        {
            _gate.Release();
        }
    }

    public static long GetCacheSizeBytes()
    {
        try
        {
            var dir = GetCacheDirectory();
            if (!Directory.Exists(dir))
                return 0;

            return Directory.EnumerateFiles(dir)
                .Sum(path => new FileInfo(path).Length);
        }
        catch
        {
            return 0;
        }
    }

    public static string? NormalizeToAbsoluteUrl(string baseUrl, string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
            return null;

        var trimmed = imageUrl.Trim().Replace('\\', '/');

        if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return trimmed;

        if (trimmed.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
            trimmed.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
            trimmed.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
            trimmed.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
        {
            var hasPathSeparator = trimmed.Contains('/') || trimmed.Contains("uploads", StringComparison.OrdinalIgnoreCase);
            if (!hasPathSeparator)
                return null;
        }

        var normalizedBase = (baseUrl ?? string.Empty).TrimEnd('/');
        if (string.IsNullOrWhiteSpace(normalizedBase))
            return null;

        return $"{normalizedBase}/{trimmed.TrimStart('/')}";
    }

    private static string BuildCachePath(string absoluteUrl)
    {
        var canonicalUrl = CanonicalizeCacheUrl(absoluteUrl);
        return BuildCachePathCore(canonicalUrl, absoluteUrl);
    }

    private static string BuildCachePathLegacy(string absoluteUrl)
        => BuildCachePathCore(absoluteUrl, absoluteUrl);

    private static string BuildCachePathCore(string hashSource, string extSourceUrl)
    {
        var ext = ".img";
        try
        {
            var uri = new Uri(extSourceUrl);
            var uriExt = Path.GetExtension(uri.AbsolutePath);
            if (!string.IsNullOrWhiteSpace(uriExt) && uriExt.Length <= 8)
                ext = uriExt.ToLowerInvariant();
        }
        catch
        {
            // Keep default extension when URL parsing fails.
        }

        using var sha1 = SHA1.Create();
        var hashBytes = sha1.ComputeHash(System.Text.Encoding.UTF8.GetBytes(hashSource));
        var hash = Convert.ToHexString(hashBytes).ToLowerInvariant();

        return Path.Combine(GetCacheDirectory(), $"{hash}{ext}");
    }

    private static string CanonicalizeCacheUrl(string absoluteUrl)
    {
        try
        {
            var uri = new Uri(absoluteUrl);
            var builder = new UriBuilder(uri)
            {
                Query = string.Empty,
                Fragment = string.Empty
            };
            return builder.Uri.AbsoluteUri;
        }
        catch
        {
            var noFragment = absoluteUrl.Split('#')[0];
            var queryIndex = noFragment.IndexOf('?');
            return queryIndex >= 0 ? noFragment[..queryIndex] : noFragment;
        }
    }

    private static string GetCacheDirectory()
    {
        var dir = Path.Combine(FileSystem.AppDataDirectory, "image_cache");
        Directory.CreateDirectory(dir);
        return dir;
    }
}
