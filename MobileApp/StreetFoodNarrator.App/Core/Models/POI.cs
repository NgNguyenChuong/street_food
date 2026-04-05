using SQLite;
using System.Text.RegularExpressions;
using Microsoft.Maui.Networking;
using Microsoft.Maui.Storage;

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
                return ResolveBundledFallbackImage();

            var baseUrl = AppConfig.GetResolvedApiBaseUrl().TrimEnd('/');
            return $"{baseUrl}/{value.TrimStart('/')}";
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

