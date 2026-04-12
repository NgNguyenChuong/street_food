using SQLite;
using System.Text.RegularExpressions;
using System.Globalization;
using Microsoft.Maui.Networking;
using Microsoft.Maui.Storage;
using StreetFoodNarrator.App.Core.Services.Implementations;

namespace StreetFoodNarrator.App.Core.Models;

/// <summary>
/// Point of Interest - Điểm thuyết minh (Enhanced with multi-language support)
/// </summary>
[Table("POIs")]
public class POI
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    /// <summary>
    /// Backend Mongo ObjectId string for diagnostics/reconciliation.
    /// </summary>
    public string? ServerObjectId { get; set; }
    
    /// <summary>
    /// Vĩ độ (Latitude)
    /// </summary>
    [Indexed]
    public double Latitude { get; set; }
    
    /// <summary>
    /// Kinh độ (Longitude)
    /// </summary>
    [Indexed]
    public double Longitude { get; set; }
    
    /// <summary>
    /// Bán kính kích hoạt (mét)
    /// </summary>
    public int Radius { get; set; } = 50;
    
    /// <summary>
    /// Loại vùng: Area (Khu vực lớn), District (Khu trung), Spot (Điểm cụ thể)
    /// </summary>
    [Indexed]
    public string ZoneType { get; set; } = "Spot";
    
    /// <summary>
    /// Cấp độ vùng (computed): Area=1, District=2, Spot=3
    /// </summary>
    public int ZoneLevel { get; set; } = 3;
    
    /// <summary>
    /// Độ ưu tiên (1-10) - Dùng khi nhiều vùng chồng lấn
    /// </summary>
    public int Priority { get; set; } = 5;
    
    /// <summary>
    /// Thời gian chờ (phút) trước khi trigger lại
    /// </summary>
    public int CooldownMinutes { get; set; } = 30;
    
    /// <summary>
    /// ID của vùng cha (nested zones)
    /// </summary>
    public int? ParentZoneId { get; set; }
    
    /// <summary>
    /// Số lần phát tối đa trong 1 session
    /// </summary>
    public int MaxPlaysPerSession { get; set; } = 1;
    
    // ============================================
    // MULTI-LANGUAGE NAME FIELDS - CHỈ 3 NGÔN NGỮ
    // ============================================
    
    public string Name_Vi { get; set; } = string.Empty;
    public string Name_En { get; set; } = string.Empty;
    public string? Name_Zh { get; set; }
    
    // ============================================
    // MULTI-LANGUAGE DESCRIPTION FIELDS (for TTS)
    // ============================================
    
    public string? Description_Vi { get; set; }
    public string? Description_En { get; set; }
    public string? Description_Zh { get; set; }
    
    // ============================================
    // MULTI-LANGUAGE AUDIO URLs
    // ============================================
    
    public string? AudioUrl_Vi { get; set; }
    public string? AudioUrl_En { get; set; }
    public string? AudioUrl_Zh { get; set; }

    public string? Script_Vi { get; set; }
    public string? Script_En { get; set; }
    public string? Script_Zh { get; set; }
    
    // ============================================
    // MEDIA & METADATA
    // ============================================
    
    public string? ImageUrl { get; set; }

    private static readonly string[] BundledFallbackImages =
    {
        "poi_mock_1.jpg",
        "poi_mock_2.jpg",
        "poi_mock_3.jpg",
        "welcome_streetfood.jpg"
    };

    [Ignore]
    public string DisplayImageUrl
    {
        get
        {
            if (string.IsNullOrWhiteSpace(ImageUrl))
                return ResolveBundledFallbackImage();

            var value = ImageUrl.Trim().Replace('\\', '/');
            var isOnline = Connectivity.Current.NetworkAccess == NetworkAccess.Internet ||
                           Connectivity.Current.NetworkAccess == NetworkAccess.ConstrainedInternet;

            if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                var cachedPath = PoiImageCacheService.GetCachedPathIfExists(AppConfig.GetResolvedApiBaseUrl(), value);
                if (!string.IsNullOrWhiteSpace(cachedPath))
                    return cachedPath;

                return isOnline ? value : ResolveBundledFallbackImage();
            }

            if (value.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                value.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                value.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                value.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
            {
                var hasPathSeparator = value.Contains('/') || value.Contains("uploads", StringComparison.OrdinalIgnoreCase);
                if (!hasPathSeparator)
                    return value;
            }

            if (!isOnline)
            {
                var cachedPath = PoiImageCacheService.GetCachedPathIfExists(AppConfig.GetResolvedApiBaseUrl(), value);
                return !string.IsNullOrWhiteSpace(cachedPath)
                    ? cachedPath
                    : ResolveBundledFallbackImage();
            }

            var baseUrl = AppConfig.GetResolvedApiBaseUrl().TrimEnd('/');
            var absoluteUrl = PoiImageCacheService.NormalizeToAbsoluteUrl(baseUrl, value)
                ?? $"{baseUrl}/{value.TrimStart('/')}";

            var onlineCachedPath = PoiImageCacheService.GetCachedPathIfExists(baseUrl, absoluteUrl);
            return !string.IsNullOrWhiteSpace(onlineCachedPath)
                ? onlineCachedPath
                : absoluteUrl;
        }
    }

    private string ResolveBundledFallbackImage()
    {
        var index = Math.Abs(Id) % BundledFallbackImages.Length;
        return BundledFallbackImages[index];
    }
    
    /// <summary>
    /// Món ăn đặc trưng
    /// </summary>
    public string? SignatureDish { get; set; }
    
    /// <summary>
    /// Điều thú vị về địa điểm
    /// </summary>
    public string? FunFact { get; set; }
    
    /// <summary>
    /// Giờ hoạt động (ví dụ: "15:00-23:00")
    /// </summary>
    public string? EstimatedHours { get; set; }
    
    // ============================================
    // RESTAURANT INFO (for Detail Page)
    // ============================================
    
    /// <summary>
    /// Địa chỉ cụ thể
    /// </summary>
    public string? Address { get; set; }

    /// <summary>
    /// External map url/deeplink from backend.
    /// </summary>
    public string? MapUrl { get; set; }
    
    /// <summary>
    /// Số điện thoại liên hệ
    /// </summary>
    public string? PhoneNumber { get; set; }
    
    /// <summary>
    /// Giá trung bình (VND)
    /// </summary>
    public decimal? AveragePrice { get; set; }
    
    /// <summary>
    /// Đánh giá (0-5 sao)
    /// </summary>
    public double? Rating { get; set; }

    /// <summary>
    /// Số lượng đánh giá
    /// </summary>
    public int NumReviews { get; set; } = 0;

    /// <summary>
    /// Mức giá chuẩn hóa (1-5).
    /// </summary>
    public int? PriceLevel { get; set; }

    /// <summary>
    /// Tổng số lượt nghe đã chốt từ backend analytics.
    /// </summary>
    public long PlayCount { get; set; }

    /// <summary>
    /// Thời lượng nghe trung bình (giây) từ backend analytics.
    /// </summary>
    public double MeanPlay { get; set; }
    
    /// <summary>
    /// Danh mục (ẨM THỰC ĐƯỜNG PHỐ, etc.)
    /// </summary>
    public string? Category { get; set; }
    
    /// <summary>
    /// Giờ mở cửa chi tiết (text format)
    /// </summary>
    public string? OpeningHoursText { get; set; }

    /// <summary>
    /// Opening hours compact display for UI.
    /// Example:
    /// - "T2-T6: 10:00-22:00; T7-CN: 09:00-23:00"
    /// - "Hàng ngày: 17:00-23:00"
    /// - "24/7"
    /// </summary>
    [Ignore]
    public string DisplayOpeningHoursText
    {
        get
        {
            var raw = string.IsNullOrWhiteSpace(OpeningHoursText)
                ? (string.IsNullOrWhiteSpace(EstimatedHours) ? string.Empty : EstimatedHours!)
                : OpeningHoursText!;

            if (string.IsNullOrWhiteSpace(raw))
                return "Đang cập nhật";

            return CompactOpeningHours(raw);
        }
    }

    [Ignore]
    public bool? IsOpenNow
    {
        get
        {
            if (!TryGetOpeningHoursSource(out var raw))
                return null;

            if (TryResolveOpenStatus(raw, DateTime.Now, out var isOpen))
                return isOpen;

            return null;
        }
    }

    [Ignore]
    public string DisplayOpenStatusText
    {
        get
        {
            var lang = Preferences.Get("app_language", "vi");
            return IsOpenNow switch
            {
                true => lang switch
                {
                    "en" => "Open now",
                    "zh" => "营业中",
                    _ => "Đang mở cửa"
                },
                false => lang switch
                {
                    "en" => "Closed now",
                    "zh" => "已打烊",
                    _ => "Đang đóng cửa"
                },
                _ => lang switch
                {
                    "en" => "Hours updating",
                    "zh" => "营业时间更新中",
                    _ => "Giờ mở cửa cập nhật"
                }
            };
        }
    }

    [Ignore]
    public string DisplayOpenStatusTextColor => IsOpenNow switch
    {
        true => "#4BE277",
        false => "#F87171",
        _ => "#FBBF24"
    };

    [Ignore]
    public string DisplayOpenStatusBackgroundColor => IsOpenNow switch
    {
        true => "#224BE277",
        false => "#22F87171",
        _ => "#22FBBF24"
    };

    [Ignore]
    public string DisplayOpenStatusStrokeColor => IsOpenNow switch
    {
        true => "#404BE277",
        false => "#40F87171",
        _ => "#40FBBF24"
    };

    private static string CompactOpeningHours(string input)
    {
        var text = input.Trim();

        // Normalize separators and spaces
        text = text.Replace("–", "-").Replace("—", "-");
        text = Regex.Replace(text, @"\s+", " ");

        // Common always-open patterns
        var lower = text.ToLowerInvariant();
        if (lower.Contains("24/7") || lower.Contains("24h") || lower.Contains("24 giờ") || lower.Contains("00:00-00:00"))
            return "24/7";

        // If already short enough, keep it
        if (text.Length <= 42)
            return text;

        // Convert "Hàng ngày: HH:mm-HH:mm ..." => "Hàng ngày: HH:mm-HH:mm"
        var dailyMatch = Regex.Match(text, @"(?i)(hàng ngày|mỗi ngày)\s*:\s*(\d{1,2}:\d{2})\s*-\s*(\d{1,2}:\d{2})");
        if (dailyMatch.Success)
            return $"Hàng ngày: {dailyMatch.Groups[2].Value}-{dailyMatch.Groups[3].Value}";

        // Pattern: "Mon-Sun HH:mm-HH:mm" or "Thứ 2 - Chủ Nhật HH:mm-HH:mm"
        var timeRange = Regex.Match(text, @"(\d{1,2}:\d{2})\s*-\s*(\d{1,2}:\d{2})");
        if (timeRange.Success)
            return $"Hàng ngày: {timeRange.Groups[1].Value}-{timeRange.Groups[2].Value}";

        // Fallback with max length to avoid layout break
        return text.Length > 48 ? text.Substring(0, 45) + "..." : text;
    }

    private bool TryGetOpeningHoursSource(out string raw)
    {
        raw = !string.IsNullOrWhiteSpace(OpeningHoursText)
            ? OpeningHoursText!.Trim()
            : (EstimatedHours?.Trim() ?? string.Empty);

        return !string.IsNullOrWhiteSpace(raw);
    }

    private static bool TryResolveOpenStatus(string raw, DateTime nowLocal, out bool isOpen)
    {
        isOpen = false;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        var text = raw.Trim().Replace("–", "-").Replace("—", "-");
        var lower = text.ToLowerInvariant();

        if (lower.Contains("24/7") || lower.Contains("24h") || lower.Contains("24 giờ") || lower.Contains("00:00-00:00"))
        {
            isOpen = true;
            return true;
        }

        if (TryExtractDaySpecificRange(text, nowLocal.DayOfWeek, out var start, out var end) ||
            TryExtractFirstRange(text, out start, out end))
        {
            isOpen = IsWithinRange(nowLocal.TimeOfDay, start, end);
            return true;
        }

        return false;
    }

    private static bool TryExtractDaySpecificRange(string text, DayOfWeek dayOfWeek, out TimeSpan start, out TimeSpan end)
    {
        start = default;
        end = default;

        var aliases = dayOfWeek switch
        {
            DayOfWeek.Monday => new[] { "thứ 2", "thu 2", "t2", "monday", "mon" },
            DayOfWeek.Tuesday => new[] { "thứ 3", "thu 3", "t3", "tuesday", "tue" },
            DayOfWeek.Wednesday => new[] { "thứ 4", "thu 4", "t4", "wednesday", "wed" },
            DayOfWeek.Thursday => new[] { "thứ 5", "thu 5", "t5", "thứ năm", "thu nam", "thursday" },
            DayOfWeek.Friday => new[] { "thứ 6", "thu 6", "t6", "friday", "fri" },
            DayOfWeek.Saturday => new[] { "thứ 7", "thu 7", "t7", "saturday", "sat" },
            _ => new[] { "chủ nhật", "chu nhat", "cn", "sunday", "sun" }
        };

        foreach (var alias in aliases)
        {
            var pattern = $@"(?i){Regex.Escape(alias)}[^0-9]*(\d{{1,2}}:\d{{2}})\s*-\s*(\d{{1,2}}:\d{{2}})";
            var match = Regex.Match(text, pattern);
            if (!match.Success)
                continue;

            if (TryParseTime(match.Groups[1].Value, out start) && TryParseTime(match.Groups[2].Value, out end))
                return true;
        }

        return false;
    }

    private static bool TryExtractFirstRange(string text, out TimeSpan start, out TimeSpan end)
    {
        start = default;
        end = default;

        var match = Regex.Match(text, @"(\d{1,2}:\d{2})\s*-\s*(\d{1,2}:\d{2})");
        if (!match.Success)
            return false;

        return TryParseTime(match.Groups[1].Value, out start) && TryParseTime(match.Groups[2].Value, out end);
    }

    private static bool TryParseTime(string value, out TimeSpan time)
    {
        return TimeSpan.TryParseExact(value, @"h\:mm", CultureInfo.InvariantCulture, out time)
            || TimeSpan.TryParseExact(value, @"hh\:mm", CultureInfo.InvariantCulture, out time)
            || TimeSpan.TryParse(value, out time);
    }

    private static bool IsWithinRange(TimeSpan now, TimeSpan start, TimeSpan end)
    {
        if (start == end)
            return true;

        if (end > start)
            return now >= start && now <= end;

        // Overnight range, e.g. 17:00-02:00
        return now >= start || now <= end;
    }

    /// <summary>
    /// Danh sách món ăn đặc trưng (JSON serialized as comma-separated string)
    /// </summary>
    public string? SignatureDishesJson { get; set; }
    
    /// <summary>
    /// Helper: Parse SignatureDishesJson → List
    /// </summary>
    [Ignore]
    public List<string> SignatureDishes
    {
        get
        {
            if (string.IsNullOrEmpty(SignatureDishesJson)) return new List<string>();
            return new List<string>(SignatureDishesJson.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }
        set
        {
            SignatureDishesJson = value != null ? string.Join(",", value) : null;
        }
    }
    
    [Ignore]
    public List<string> DisplaySignatureDishes
    {
        get
        {
            var list = SignatureDishes;
            if (list.Count > 0) return list;
            if (!string.IsNullOrWhiteSpace(SignatureDish)) return new List<string> { SignatureDish };
            return new List<string> { "Menu đang cập nhật..." };
        }
    }

    /// <summary>
    /// Comma-separated signature dishes for display in card labels
    /// </summary>
    [Ignore]
    public string DisplaySignatureDishText
    {
        get
        {
            var list = DisplaySignatureDishes;
            return string.Join(" • ", list.Take(3));
        }
    }
    
    // ============================================
    // COMPUTED PROPERTIES (not stored in DB)
    // ============================================
    
    /// <summary>
    /// Khoảng cách từ user đến POI (meters) - calculated at runtime
    /// </summary>
    [Ignore]
    public double DistanceFromUser { get; set; }
    
    // ============================================
    // LEGACY FIELDS (for backward compatibility)
    // ============================================
    
    /// <summary>
    /// Loại POI (Restaurant, Stall, LandMark, etc.)
    /// </summary>
    public string Type { get; set; } = string.Empty;
    
    [Indexed]
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Soft-delete trạng thái từ backend.
    /// </summary>
    public bool IsDeleted { get; set; } = false;

    /// <summary>
    /// Số lượng audio được backend tổng hợp cho POI.
    /// </summary>
    public int AudioCount { get; set; }

    /// <summary>
    /// Vendor ID sở hữu POI.
    /// </summary>
    public int? VendorId { get; set; }

    /// <summary>
    /// Trạng thái duyệt nội dung từ backend.
    /// </summary>
    public string? ReviewStatus { get; set; }

    /// <summary>
    /// Pending change set serialized JSON.
    /// </summary>
    public string? PendingChangesJson { get; set; }

    /// <summary>
    /// User đã tim/lưu quán này (local only, không sync với server)
    /// </summary>
    public bool IsLikedByUser { get; set; } = false;
    
    // ============================================
    // TIMESTAMPS
    // ============================================
    
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public DateTime? DeletedAt { get; set; }
    
    // ============================================
    // NAVIGATION PROPERTIES
    // ============================================
    
    /// <summary>
    /// Danh sách audio liên quan (legacy)
    /// </summary>
    [Ignore]
    public ICollection<AudioContent> AudioContents { get; set; } = new List<AudioContent>();
    
    // ============================================
    // HELPER METHODS
    // ============================================
    
    /// <summary>
    /// Lấy tên theo ngôn ngữ - CHỈ 3 NGÔN NGỮ
    /// </summary>
    public string GetName(string language = "vi")
    {
        return language.ToLower() switch
        {
            "vi" => Name_Vi,
            "en" => Name_En,
            "zh" => Name_Zh ?? Name_En ?? Name_Vi,
            _ => Name_Vi
        };
    }

    /// <summary>
    /// Lấy tên hiển thị theo ngôn ngữ hiện tại, không trộn ngôn ngữ chéo.
    /// </summary>
    public string GetDisplayName(string language = "vi")
    {
        return language.ToLowerInvariant() switch
        {
            "en" => string.IsNullOrWhiteSpace(Name_En) ? string.Empty : Name_En,
            "zh" => string.IsNullOrWhiteSpace(Name_Zh) ? string.Empty : Name_Zh,
            _ => string.IsNullOrWhiteSpace(Name_Vi) ? string.Empty : Name_Vi
        };
    }
    
    /// <summary>
    /// Lấy mô tả theo ngôn ngữ - CHỈ 3 NGÔN NGỮ
    /// </summary>
    public string? GetDescription(string language = "vi")
    {
        return language.ToLower() switch
        {
            "vi" => Description_Vi,
            "en" => Description_En,
            "zh" => Description_Zh ?? Description_En ?? Description_Vi,
            _ => Description_Vi
        };
    }

    /// <summary>
    /// Lấy mô tả hiển thị theo ngôn ngữ hiện tại, không trộn ngôn ngữ chéo.
    /// </summary>
    public string GetDisplayDescription(string language = "vi")
    {
        return language.ToLowerInvariant() switch
        {
            "en" => Description_En?.Trim() ?? string.Empty,
            "zh" => Description_Zh?.Trim() ?? string.Empty,
            _ => Description_Vi?.Trim() ?? string.Empty
        };
    }
    
    /// <summary>
    /// Lấy audio URL theo ngôn ngữ
    /// </summary>
    public string? GetAudioUrl(string language = "vi")
    {
        return language.ToLower() switch
        {
            "vi" => AudioUrl_Vi,
            "en" => AudioUrl_En,
            "zh" => AudioUrl_Zh ?? AudioUrl_En ?? AudioUrl_Vi,
            _ => AudioUrl_Vi
        };
    }

    [Ignore]
    public string DisplayName => GetDisplayName(Preferences.Get("app_language", "vi"));

    [Ignore]
    public string DisplayDescription => GetDisplayDescription(Preferences.Get("app_language", "vi"));
}

