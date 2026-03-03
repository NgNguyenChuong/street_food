using SQLite;

namespace StreetFoodNarrator.App.Core.Models;

/// <summary>
/// Records when a zone was triggered (for cooldown management)
/// </summary>
[Table("ZoneHistory")]
public class ZoneHistory
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    
    /// <summary>
    /// POI ID that was triggered
    /// </summary>
    [Indexed]
    public int POI_ID { get; set; }
    
    /// <summary>
    /// When was this zone first played in current session
    /// </summary>
    public DateTime FirstPlayedAt { get; set; }
    
    /// <summary>
    /// Last time this zone was triggered
    /// </summary>
    [Indexed]
    public DateTime LastTriggeredAt { get; set; }
    
    /// <summary>
    /// How many times has this zone been triggered in current session
    /// </summary>
    public int PlayCount { get; set; }
    
    /// <summary>
    /// Language used for audio
    /// </summary>
    public string Language { get; set; } = "vi";
    
    /// <summary>
    /// Session ID to track per-session plays
    /// </summary>
    [Indexed]
    public string SessionId { get; set; } = string.Empty;
    
    /// <summary>
    /// Is this zone currently in cooldown period?
    /// </summary>
    public bool IsInCooldown(int cooldownMinutes)
    {
        var elapsed = DateTime.UtcNow - LastTriggeredAt;
        return elapsed.TotalMinutes < cooldownMinutes;
    }
}
