namespace StreetFoodNarrator.App.Core.Utils;

using Microsoft.Maui.Storage;

public static class VipAudioPreviewGate
{
    public const int FreePoiAudioQuota = 2;

    private const string VipExpiresAtUtcKey = "vip_expires_at_utc_v1";
    private const string ConsumedPoiIdsKeyPrefix = "free_audio_preview_poi_ids_daily_v2_";

    public static bool IsVipActive(bool isVipFlag = false)
    {
        if (isVipFlag)
            return true;

        var raw = Preferences.Get(VipExpiresAtUtcKey, string.Empty);
        if (!DateTime.TryParse(raw, out var expiresAtUtc))
            return false;

        return expiresAtUtc.ToUniversalTime() > DateTime.UtcNow;
    }

    public static bool TryGrantAccessForPoi(int poiId, bool isVipFlag, out int remainingFreePoiSlots)
    {
        if (poiId <= 0)
        {
            remainingFreePoiSlots = 0;
            return true;
        }

        if (IsVipActive(isVipFlag))
        {
            remainingFreePoiSlots = int.MaxValue;
            return true;
        }

        var consumed = LoadConsumedPoiIds();
        if (consumed.Contains(poiId))
        {
            remainingFreePoiSlots = Math.Max(0, FreePoiAudioQuota - consumed.Count);
            return true;
        }

        if (consumed.Count >= FreePoiAudioQuota)
        {
            remainingFreePoiSlots = 0;
            return false;
        }

        consumed.Add(poiId);
        SaveConsumedPoiIds(consumed);
        remainingFreePoiSlots = Math.Max(0, FreePoiAudioQuota - consumed.Count);
        return true;
    }

    private static HashSet<int> LoadConsumedPoiIds()
    {
        var raw = Preferences.Get(BuildConsumedPoiIdsKey(), string.Empty);
        var ids = new HashSet<int>();

        if (string.IsNullOrWhiteSpace(raw))
            return ids;

        var parts = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var part in parts)
        {
            if (int.TryParse(part, out var id) && id > 0)
                ids.Add(id);
        }

        return ids;
    }

    private static void SaveConsumedPoiIds(HashSet<int> ids)
    {
        var ordered = ids
            .Where(id => id > 0)
            .OrderBy(id => id)
            .Select(id => id.ToString());

        Preferences.Set(BuildConsumedPoiIdsKey(), string.Join(',', ordered));
    }

    private static string BuildConsumedPoiIdsKey()
    {
        // Daily key keeps free quota fair for demo sessions (resets every local day).
        var localDate = DateTime.Now.ToString("yyyyMMdd");
        return $"{ConsumedPoiIdsKeyPrefix}{localDate}";
    }
}