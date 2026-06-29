using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace StreetFoodNarrator.API.Models;

/// <summary>
/// Device tracking for anonymous users
/// </summary>
[BsonIgnoreExtraElements]
public class DeviceInfo
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("Device_ID")]
    public int Device_ID { get; set; }
    
    /// <summary>
    /// Unique device identifier (UUID from app)
    /// </summary>
    [Required, MaxLength(100)]
    public string DeviceId { get; set; } = string.Empty;
    
    /// <summary>
    /// Platform (iOS, Android)
    /// </summary>
    [MaxLength(20)]
    public string Platform { get; set; } = "Unknown";
    
    /// <summary>
    /// Device model (e.g., "iPhone 13", "Samsung Galaxy S21")
    /// </summary>
    [MaxLength(100)]
    public string? Model { get; set; }
    
    /// <summary>
    /// OS version (e.g., "16.0", "Android 12")
    /// </summary>
    [MaxLength(50)]
    public string? OsVersion { get; set; }
    
    /// <summary>
    /// App version
    /// </summary>
    [MaxLength(20)]
    public string? AppVersion { get; set; }
    
    /// <summary>
    /// First time seen
    /// </summary>
    public DateTime FirstSeen { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Last activity timestamp
    /// </summary>
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Total app sessions
    /// </summary>
    public int TotalSessions { get; set; } = 0;
    
    /// <summary>
    /// Total POIs viewed
    /// </summary>
    public int TotalPOIsViewed { get; set; } = 0;
    
    /// <summary>
    /// Total audio played
    /// </summary>
    public int TotalAudioPlayed { get; set; } = 0;
    
    /// <summary>
    /// User's preferred language
    /// </summary>
    [MaxLength(10)]
    public string? PreferredLanguage { get; set; }

    /// <summary>
    /// Whether the device is currently online (heartbeat within threshold)
    /// </summary>
    public bool IsOnline { get; set; } = false;

    /// <summary>
    /// Last heartbeat timestamp from the device
    /// </summary>
    public DateTime? LastHeartbeatAt { get; set; }

    /// <summary>
    /// User role: "tourist" (web/mobile app user) or "vendor" (vendor dashboard user)
    /// </summary>
    [MaxLength(20)]
    public string UserRole { get; set; } = "tourist";

    /// <summary>
    /// Client type: "web", "android", "ios", "dashboard"
    /// </summary>
    [MaxLength(20)]
    public string ClientType { get; set; } = "web";
}
