namespace StreetFoodNarrator.App.Core.Models;

/// <summary>
/// Represents an active geofence zone that user is currently within
/// </summary>
public class ActiveGeofence
{
    /// <summary>
    /// The POI representing this geofence
    /// </summary>
    public POI POI { get; set; } = null!;
    
    /// <summary>
    /// Distance from user to POI center (in meters)
    /// </summary>
    public double Distance { get; set; }
    
    /// <summary>
    /// Calculated priority score for zone selection
    /// Higher = more relevant to show to user
    /// </summary>
    public double PriorityScore { get; set; }
    
    /// <summary>
    /// When this zone was first entered
    /// </summary>
    public DateTime EnteredAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Is this zone currently active (playing audio)?
    /// </summary>
    public bool IsActive { get; set; }
}
