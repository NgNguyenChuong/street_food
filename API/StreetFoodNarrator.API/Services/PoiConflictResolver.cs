using StreetFoodNarrator.API.Models;

namespace StreetFoodNarrator.API.Services;

/// <summary>
/// Giải quyết xung đột khi nhiều POI nằm gần nhau (overlap).
///
/// Quy tắc ưu tiên (cao → thấp):
///   1. POI của vendor có premium đang hoạt động > POI của vendor KHÔNG có premium.
///   2. Nếu cả hai đều premium (hoặc đều không): POI có Priority field cao hơn thắng.
///   3. Nếu Priority bằng nhau: POI gần user hơn thắng (khoảng cách nhỏ hơn).
///   4. Nếu khoảng cách bằng nhau (hiếm): POI có TriggerRadius nhỏ hơn thắng
///      (vì Spot cụ thể hơn Area).
///   5. Tiebreaker cuối: POI_ID nhỏ hơn (tạo trước) thắng để đảm bảo stable sort.
///
/// Edge cases:
///   - POI không có vendor (admin tạo): coi như premium = false, nhưng giữ priority field.
///   - Vendor premium vừa hết hạn trong ngày: so sánh theo PremiumExpiresAt ≤ UtcNow → không còn premium.
///   - Hai vendor cùng premium, cùng Priority, cùng khoảng cách:
///       → Tiebreak bằng TriggerRadius (nhỏ hơn = cụ thể hơn) rồi POI_ID.
///   - POI bị xóa mềm (DeletedAt != null): loại khỏi danh sách trước khi resolve.
///   - Vùng Area lớn chứa nhiều Spot: Area luôn thua Spot bất kể premium
///       vì ZoneType="Spot" mặc định được gán Priority cao hơn Area/District.
/// </summary>
public static class PoiConflictResolver
{
    /// <summary>
    /// Chọn POI ưu tiên nhất trong danh sách các POI đang overlap tại vị trí user.
    /// Trả về null nếu danh sách rỗng.
    /// </summary>
    /// <param name="overlappingPois">Danh sách POI user đang đứng trong vùng trigger.</param>
    /// <param name="vendorPremiumMap">Map VendorId → (isPremium, premiumExpiresAt). Admin POI (VendorId=null) không có trong map.</param>
    /// <param name="distanceMap">Map POI_ID → khoảng cách thực tế từ user (metres).</param>
    public static POI? ResolvePrimary(
        IEnumerable<POI> overlappingPois,
        IReadOnlyDictionary<int, (bool IsPremium, DateTime? ExpiresAt)> vendorPremiumMap,
        IReadOnlyDictionary<int, double> distanceMap)
    {
        var candidates = overlappingPois
            .Where(p => p.DeletedAt == null && p.IsActive)
            .ToList();

        if (candidates.Count == 0)
            return null;

        if (candidates.Count == 1)
            return candidates[0];

        var now = DateTime.UtcNow;

        return candidates
            .OrderByDescending(p => IsPremiumActive(p, vendorPremiumMap, now) ? 1 : 0) // 1. premium first
            .ThenByDescending(p => p.Priority)                                           // 2. higher priority
            .ThenBy(p => distanceMap.TryGetValue(p.POI_ID, out var d) ? d : double.MaxValue) // 3. closer
            .ThenBy(p => p.TriggerRadius)                                                // 4. tighter radius
            .ThenBy(p => p.POI_ID)                                                       // 5. stable tiebreak
            .First();
    }

    /// <summary>
    /// Kiểm tra xem POI có thuộc vendor đang active premium không.
    /// POI không có VendorId (admin) → không tính là premium.
    /// </summary>
    private static bool IsPremiumActive(
        POI poi,
        IReadOnlyDictionary<int, (bool IsPremium, DateTime? ExpiresAt)> vendorPremiumMap,
        DateTime nowUtc)
    {
        if (poi.VendorId == null)
            return false;

        if (!vendorPremiumMap.TryGetValue(poi.VendorId.Value, out var info))
            return false;

        if (!info.IsPremium)
            return false;

        // Nếu có ngày hết hạn thì kiểm tra; nếu null thì coi là còn hạn
        if (info.ExpiresAt.HasValue && info.ExpiresAt.Value <= nowUtc)
            return false;

        return true;
    }

    /// <summary>
    /// Build vendorPremiumMap từ danh sách VendorProfile.
    /// Gọi hàm này khi cần resolve và đã có danh sách vendor từ DB.
    /// </summary>
    public static Dictionary<int, (bool IsPremium, DateTime? ExpiresAt)> BuildVendorPremiumMap(
        IEnumerable<VendorProfile> vendors)
    {
        var now = DateTime.UtcNow;
        return vendors.ToDictionary(
            v => v.VendorId,
            v =>
            {
                var isPremium = string.Equals(v.ServicePlan, "premium", StringComparison.OrdinalIgnoreCase)
                                && (v.PremiumExpiresAt == null || v.PremiumExpiresAt.Value > now);
                return (IsPremium: isPremium, ExpiresAt: v.PremiumExpiresAt);
            });
    }
}
