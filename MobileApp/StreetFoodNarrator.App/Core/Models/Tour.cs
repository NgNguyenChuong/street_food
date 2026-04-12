namespace StreetFoodNarrator.App.Core.Models;

/// <summary>
/// Tour tham quan
/// </summary>
public class Tour
{
    public int Id { get; set; }
    
    public string Name { get; set; } = string.Empty;
    
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Thời lượng ước tính (phút)
    /// </summary>
    public int Duration { get; set; }
    
    public bool IsActive { get; set; } = true;
    
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    
    /// <summary>
    /// Danh sách POI trong tour
    /// </summary>
    public ICollection<POI_Tour> POI_Tours { get; set; } = new List<POI_Tour>();
}

/// <summary>
/// Bảng trung gian POI và Tour
/// </summary>
public class POI_Tour
{
    public int POIId { get; set; }
    public POI? POI { get; set; }
    
    public int TourId { get; set; }
    public Tour? Tour { get; set; }
    
    /// <summary>
    /// Thứ tự trong tour
    /// </summary>
    public int Order { get; set; }
}
