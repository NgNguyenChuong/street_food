using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace StreetFoodNarrator.API.Models;

[BsonIgnoreExtraElements]
public class UserSettings
{
    [BsonId]
    public ObjectId Id { get; set; }

    public string DeviceId { get; set; } = string.Empty;

    public string? DefaultLanguage { get; set; }

    public TTSSettings TTS { get; set; } = new();
    public LocationSettings Location { get; set; } = new();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

public class TTSSettings
{
    public string Voice { get; set; } = "vi-VN-HoaiMyNeural";  // Default Vietnamese female
    public double Speed { get; set; } = 1.0;  // 0.5 to 2.0
    public double Pitch { get; set; } = 1.0;  // 0.5 to 2.0
    public int Volume { get; set; } = 80;  // 0 to 100
    public bool AutoPlay { get; set; } = false;
}

public class LocationSettings
{
    public int SensitivityRadius { get; set; } = 40;  // in meters, 0 to 100
}
