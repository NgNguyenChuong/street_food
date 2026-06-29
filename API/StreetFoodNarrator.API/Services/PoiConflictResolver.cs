using StreetFoodNarrator.API.Models;

namespace StreetFoodNarrator.API.Services;

/// <summary>
/// Giải quyết xung đột khi nhiều POI nằm gần nhau (overlap).
///
/// Quy tắc ưu tiên (cao → thấp):
///   1. NumLikes cao hơn (quán được nhiều user yêu thích hơn) thắng.
///   2. Nếu NumLikes bằng nhau: POI có Priority field cao hơn thắng.
///   3. Nếu Priority bằng nhau: POI gần user hơn thắng (khoảng cách nhỏ hơn).
///   4. Nếu khoảng cách bằng nhau (hiếm): POI có TriggerRadius nhỏ hơn thắng
///      (vì Spot cụ thể hơn Area).
///   5. Tiebreaker cuối: POI_ID nhỏ hơn (tạo trước) thắng để đảm bảo stable sort.
///
/// Edge cases:
///   - POI bị xóa mềm (DeletedAt != null): loại khỏi danh sách trước khi resolve.
///   - Vùng Area lớn chứa nhiều Spot: Area luôn thua Spot bất kể NumLikes
///       vì ZoneType="Spot" mặc định được gán Priority cao hơn Area/District.
/// </summary>
public static class PoiConflictResolver
{
    /// <summary>
    /// Chọn POI ưu tiên nhất trong danh sách các POI đang overlap tại vị trí user.
    /// Trả về null nếu danh sách rỗng.
    /// </summary>
    /// <param name="overlappingPois">Danh sách POI user đang đứng trong vùng trigger.</param>
    /// <param name="distanceMap">Map POI_ID → khoảng cách thực tế từ user (metres).</param>
    public static POI? ResolvePrimary(
        IEnumerable<POI> overlappingPois,
        IReadOnlyDictionary<int, double> distanceMap)
    {
        var candidates = overlappingPois
            .Where(p => p.DeletedAt == null && p.IsActive)
            .ToList();

        if (candidates.Count == 0)
            return null;

        if (candidates.Count == 1)
            return candidates[0];

        return candidates
            .OrderByDescending(p => p.NumLikes)                                          // 1. most liked first
            .ThenBy(p => distanceMap.TryGetValue(p.POI_ID, out var d) ? d : double.MaxValue) // 2. closer
            .ThenBy(p => p.TriggerRadius)                                                // 3. tighter radius
            .ThenBy(p => p.POI_ID)                                                       // 4. stable tiebreak
            .First();
    }
}
