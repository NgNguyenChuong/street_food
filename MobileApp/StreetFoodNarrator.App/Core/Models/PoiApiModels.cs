namespace StreetFoodNarrator.App.Core.Models;

public class PoiListResponse
{
    public List<PoiDto> Data { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

public class PoiSyncResponse
{
    public long DataVersion { get; set; }
    public List<PoiDto> Data { get; set; } = new();
}

public class PoiDto
{
    public string? Id { get; set; }
    public int POI_ID { get; set; }

    public string? Name_Vi { get; set; }
    public string? Name_En { get; set; }
    public string? Name_Ja { get; set; }
    public string? Name_Fr { get; set; }
    public string? Name_Ko { get; set; }
    public string? Name_Zh { get; set; }

    public string? Description_Vi { get; set; }
    public string? Description_En { get; set; }
    public string? Description_Ja { get; set; }
    public string? Description_Fr { get; set; }
    public string? Description_Ko { get; set; }
    public string? Description_Zh { get; set; }

    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }

    public string? Address { get; set; }
    public string? MapUrl { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public int AudioCount { get; set; }
    public int? VendorId { get; set; }
    public string? ReviewStatus { get; set; }
    public string? Category { get; set; }
    public string? SignatureDish { get; set; }
    public List<string>? SignatureDishes { get; set; }
    public string? OpeningHoursText { get; set; }
    public string? PhoneNumber { get; set; }
    public decimal? AveragePrice { get; set; }
    public double? Rating { get; set; }
    public int NumReviews { get; set; }
    public long PlayCount { get; set; }
    public double MeanPlay { get; set; }
    public long NumLikes { get; set; }
    public int? PriceLevel { get; set; }
    public string? ImageUrl { get; set; }
    public string? FunFact { get; set; }

    public string? AudioUrl_Vi { get; set; }
    public string? AudioUrl_En { get; set; }
    public string? AudioUrl_Ja { get; set; }
    public string? AudioUrl_Fr { get; set; }
    public string? AudioUrl_Ko { get; set; }
    public string? AudioUrl_Zh { get; set; }
    public string? Script_Vi { get; set; }
    public string? Script_En { get; set; }
    public string? Script_Zh { get; set; }

    public string? ZoneType { get; set; }
    public int ZoneLevel { get; set; }
    public int Priority { get; set; }
    public int TriggerRadius { get; set; }
    public int CooldownMinutes { get; set; }
    public int? ParentZoneId { get; set; }
    public int MaxPlaysPerSession { get; set; }

    public bool IsActive { get; set; }
    public Dictionary<string, PendingFieldChangeDto>? PendingChanges { get; set; }
}

public class PendingFieldChangeDto
{
    public string? Old { get; set; }
    public string? New { get; set; }
}
