using Microsoft.Maui.Storage;
using System.Net;

namespace StreetFoodNarrator.App.Core.Utils;

public sealed class QrDeepLinkPayload
{
    public string RawUrl { get; init; } = string.Empty;
    public int? PoiId { get; init; }
    public string TourId { get; init; } = string.Empty;
    public bool OpenMainPageOnly { get; init; }
    public string ApiBaseUrl { get; init; } = string.Empty;
    public DateTimeOffset? ExpiresAtUtc { get; init; }

    public bool IsExpired(DateTimeOffset nowUtc)
        => ExpiresAtUtc.HasValue && nowUtc > ExpiresAtUtc.Value;
}

public static class QrDeepLinkManager
{
    private const string PendingDeepLinkKey = "pending_qr_deeplink_v1";

    public static event EventHandler? PendingDeepLinkChanged;

    public static void SavePending(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return;

        Preferences.Set(PendingDeepLinkKey, url);
        PendingDeepLinkChanged?.Invoke(null, EventArgs.Empty);
    }

    public static string? ConsumePending()
    {
        var value = Preferences.Get(PendingDeepLinkKey, string.Empty);
        if (string.IsNullOrWhiteSpace(value))
            return null;

        Preferences.Remove(PendingDeepLinkKey);
        return value;
    }

    public static bool TryParse(string rawUrl, out QrDeepLinkPayload payload, out string errorMessage)
    {
        payload = new QrDeepLinkPayload { RawUrl = rawUrl };
        errorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(rawUrl))
        {
            errorMessage = "Mã QR không hợp lệ.";
            return false;
        }

        rawUrl = NormalizeIntentUriIfNeeded(rawUrl.Trim());
        rawUrl = NormalizeLooseQrPayloadIfNeeded(rawUrl);

        if (!Uri.TryCreate(rawUrl.Trim(), UriKind.Absolute, out var uri))
        {
            errorMessage = "Mã QR không đúng định dạng liên kết.";
            return false;
        }

        var scheme = uri.Scheme.ToLowerInvariant();
        var host = uri.Host.ToLowerInvariant();
        var path = uri.AbsolutePath.Trim();

        var isCustomQrLink = scheme == "streetfood";
        var isWebQrLink = (scheme == "https" || scheme == "http") &&
                  IsSupportedQrHost(host) &&
                  (path.StartsWith("/qr", StringComparison.OrdinalIgnoreCase) ||
                   path.StartsWith("/open", StringComparison.OrdinalIgnoreCase) ||
                   path.StartsWith("/deeplink", StringComparison.OrdinalIgnoreCase));

        if (!isCustomQrLink && !isWebQrLink)
        {
            errorMessage = "Liên kết QR không thuộc ứng dụng Street Food Narrator.";
            return false;
        }

        var query = ParseQuery(uri.Query);
        var poiId = TryParsePoiId(query);
        var tourId = TryParseTourId(query);
        var openMainPageOnly = TryParseMainPageFlag(query);
        var apiBaseUrl = TryParseApiBaseUrl(query);
        var expiresAt = TryParseExpiry(query);

        if (!poiId.HasValue && string.IsNullOrWhiteSpace(tourId) && !openMainPageOnly)
        {
            TryParsePathPayload(uri, out var pathPoiId, out var pathTourId, out var pathOpenMain);
            poiId = pathPoiId;
            tourId = pathTourId;
            openMainPageOnly = pathOpenMain;
        }

        if (!poiId.HasValue && string.IsNullOrWhiteSpace(tourId) && !openMainPageOnly)
        {
            errorMessage = "Mã QR thiếu thông tin điểm đến.";
            return false;
        }

        payload = new QrDeepLinkPayload
        {
            RawUrl = rawUrl,
            PoiId = poiId,
            TourId = tourId ?? string.Empty,
            OpenMainPageOnly = openMainPageOnly,
            ApiBaseUrl = apiBaseUrl ?? string.Empty,
            ExpiresAtUtc = expiresAt
        };

        return true;
    }

    private static int? TryParsePoiId(IReadOnlyDictionary<string, string> query)
    {
        var value = GetQueryValue(query, "poiId")
            ?? GetQueryValue(query, "poi")
            ?? GetQueryValue(query, "poi_id")
            ?? GetQueryValue(query, "stopId")
            ?? GetQueryValue(query, "stop_id");
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return int.TryParse(value, out var id) && id > 0
            ? id
            : null;
    }

    private static string? TryParseTourId(IReadOnlyDictionary<string, string> query)
        => GetQueryValue(query, "tourId")
           ?? GetQueryValue(query, "tour")
           ?? GetQueryValue(query, "tour_id")
           ?? GetQueryValue(query, "routeId")
           ?? GetQueryValue(query, "route_id");

    private static bool TryParseMainPageFlag(IReadOnlyDictionary<string, string> query)
    {
        var value = GetQueryValue(query, "screen")
            ?? GetQueryValue(query, "page")
            ?? GetQueryValue(query, "target")
            ?? GetQueryValue(query, "destination")
            ?? GetQueryValue(query, "action");
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var normalized = value.Trim().ToLowerInvariant();
        return normalized is "main" or "mainpage" or "home" or "explore";
    }

    private static string? TryParseApiBaseUrl(IReadOnlyDictionary<string, string> query)
    {
        var value = GetQueryValue(query, "api")
            ?? GetQueryValue(query, "apiUrl")
            ?? GetQueryValue(query, "apiBase")
            ?? GetQueryValue(query, "baseUrl");
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri))
            return null;

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return $"{uri.Scheme}://{uri.Authority}/";
    }

    private static void TryParsePathPayload(Uri uri, out int? poiId, out string? tourId, out bool openMainPageOnly)
    {
        poiId = null;
        tourId = null;
        openMainPageOnly = false;

        var segments = uri.AbsolutePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0)
            return;

        var index = 0;
        if (segments[0].Equals("qr", StringComparison.OrdinalIgnoreCase) ||
            segments[0].Equals("open", StringComparison.OrdinalIgnoreCase) ||
            segments[0].Equals("deeplink", StringComparison.OrdinalIgnoreCase))
        {
            index = 1;
        }

        if (index >= segments.Length)
            return;

        if (segments[index].Equals("main", StringComparison.OrdinalIgnoreCase) ||
            segments[index].Equals("home", StringComparison.OrdinalIgnoreCase) ||
            segments[index].Equals("explore", StringComparison.OrdinalIgnoreCase))
        {
            openMainPageOnly = true;
            return;
        }

        if (segments[index].Equals("poi", StringComparison.OrdinalIgnoreCase) &&
            index + 1 < segments.Length &&
            int.TryParse(segments[index + 1], out var poiFromPath) &&
            poiFromPath > 0)
        {
            poiId = poiFromPath;
            return;
        }

        if (segments[index].Equals("tour", StringComparison.OrdinalIgnoreCase) &&
            index + 1 < segments.Length &&
            !string.IsNullOrWhiteSpace(segments[index + 1]))
        {
            tourId = segments[index + 1].Trim();
            return;
        }

        if (int.TryParse(segments[index], out var directPoiId) && directPoiId > 0)
        {
            poiId = directPoiId;
            return;
        }

        if (!string.IsNullOrWhiteSpace(segments[index]))
            tourId = segments[index].Trim();
    }

    private static DateTimeOffset? TryParseExpiry(IReadOnlyDictionary<string, string> query)
    {
        var value = GetQueryValue(query, "exp")
            ?? GetQueryValue(query, "expires")
            ?? GetQueryValue(query, "expiry")
            ?? GetQueryValue(query, "expiresAt")
            ?? GetQueryValue(query, "expires_at");
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (long.TryParse(value, out var epochSeconds) && epochSeconds > 0)
            return DateTimeOffset.FromUnixTimeSeconds(epochSeconds);

        if (DateTimeOffset.TryParse(value, out var parsed))
            return parsed.ToUniversalTime();

        return null;
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(query))
            return dict;

        var trimmed = query[0] == '?' ? query[1..] : query;
        var pairs = trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries);
        foreach (var pair in pairs)
        {
            var idx = pair.IndexOf('=');
            if (idx < 0)
            {
                dict[Uri.UnescapeDataString(pair)] = string.Empty;
                continue;
            }

            var key = Uri.UnescapeDataString(pair[..idx]);
            var value = Uri.UnescapeDataString(pair[(idx + 1)..]);
            dict[key] = value;
        }

        return dict;
    }

    private static string? GetQueryValue(IReadOnlyDictionary<string, string> query, string key)
        => query.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;

    private static string NormalizeIntentUriIfNeeded(string rawUrl)
    {
        if (!rawUrl.StartsWith("intent://", StringComparison.OrdinalIgnoreCase))
            return rawUrl;

        try
        {
            var withoutPrefix = rawUrl["intent://".Length..];
            var fragmentIndex = withoutPrefix.IndexOf("#Intent;", StringComparison.OrdinalIgnoreCase);
            var targetPart = fragmentIndex >= 0 ? withoutPrefix[..fragmentIndex] : withoutPrefix;
            var intentPart = fragmentIndex >= 0 ? withoutPrefix[(fragmentIndex + 1)..] : string.Empty;

            var scheme = "streetfood";
            if (!string.IsNullOrWhiteSpace(intentPart))
            {
                var tokens = intentPart.Split(';', StringSplitOptions.RemoveEmptyEntries);
                foreach (var token in tokens)
                {
                    if (token.StartsWith("scheme=", StringComparison.OrdinalIgnoreCase))
                    {
                        scheme = token["scheme=".Length..];
                        break;
                    }
                }
            }

            return $"{scheme}://{targetPart}";
        }
        catch
        {
            return rawUrl;
        }
    }

    private static string NormalizeLooseQrPayloadIfNeeded(string rawUrl)
    {
        if (string.IsNullOrWhiteSpace(rawUrl))
            return rawUrl;

        var value = rawUrl.Trim();
        if (value.Contains("://", StringComparison.Ordinal))
            return value;

        if (value.StartsWith("www.streetfoodnarrator.app", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("streetfoodnarrator.app", StringComparison.OrdinalIgnoreCase))
        {
            return $"https://{value}";
        }

        if (value.Contains("=", StringComparison.Ordinal) &&
            (value.StartsWith("poiId=", StringComparison.OrdinalIgnoreCase) ||
             value.Contains("&poiId=", StringComparison.OrdinalIgnoreCase) ||
             value.StartsWith("poi=", StringComparison.OrdinalIgnoreCase) ||
             value.Contains("&poi=", StringComparison.OrdinalIgnoreCase) ||
             value.StartsWith("tourId=", StringComparison.OrdinalIgnoreCase) ||
             value.Contains("&tourId=", StringComparison.OrdinalIgnoreCase) ||
             value.StartsWith("tour=", StringComparison.OrdinalIgnoreCase) ||
             value.Contains("&tour=", StringComparison.OrdinalIgnoreCase) ||
             value.StartsWith("screen=main", StringComparison.OrdinalIgnoreCase) ||
             value.Contains("&screen=main", StringComparison.OrdinalIgnoreCase) ||
             value.StartsWith("page=main", StringComparison.OrdinalIgnoreCase) ||
             value.Contains("&page=main", StringComparison.OrdinalIgnoreCase) ||
             value.StartsWith("target=main", StringComparison.OrdinalIgnoreCase) ||
             value.Contains("&target=main", StringComparison.OrdinalIgnoreCase)))
        {
            return $"streetfood://qr?{value.TrimStart('?')}";
        }

        return value;
    }

    private static bool IsSupportedQrHost(string host)
    {
        if (string.Equals(host, "streetfoodnarrator.app", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(host, "www.streetfoodnarrator.app", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(host, "10.0.2.2", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (host.EndsWith(".ngrok-free.app", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".ngrok-free.dev", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IPAddress.TryParse(host, out _);
    }
}
