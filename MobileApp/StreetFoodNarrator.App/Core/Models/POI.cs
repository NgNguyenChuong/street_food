using SQLite;

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
    /// Danh mục (ẨM THỰC ĐƯỜNG PHỐ, etc.)
    /// </summary>
    public string? Category { get; set; }
    
    /// <summary>
    /// Giờ mở cửa chi tiết (text format)
    /// </summary>
    public string? OpeningHoursText { get; set; }
    
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
}
