namespace StreetFoodNarrator.App.Core.Models;

public class UserSettings
{
    public string UserId { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string? DefaultLanguage { get; set; }
    public NotificationSettings Notifications { get; set; } = new();
    public SecuritySettings Security { get; set; } = new();
    public TTSSettings TTS { get; set; } = new();
    public LocationSettings Location { get; set; } = new();
}

public class NotificationSettings
{
    public bool Email { get; set; } = true;
    public bool Push { get; set; } = true;
    public bool WeeklyReport { get; set; } = false;
}

public class SecuritySettings
{
    public bool TwoFactorEnabled { get; set; } = false;
}

public class TTSSettings
{
    public string Voice { get; set; } = "vi-VN-HoaiMyNeural";
    public double Speed { get; set; } = 1.0;
    public double Pitch { get; set; } = 1.0;
    public int Volume { get; set; } = 80;
    public bool AutoPlay { get; set; } = false;
    public string AudioPlaybackMode { get; set; } = AudioPlaybackModes.Auto;
}

public static class AudioPlaybackModes
{
    public const string Auto = "auto";
    public const string Stream = "stream";
    public const string Download = "download";
}

public class LocationSettings
{
    public int SensitivityRadius { get; set; } = 40;
}

public class SettingsUpdateDto
{
    public string? DefaultLanguage { get; set; }
    public NotificationSettingsUpdateDto? Notifications { get; set; }
    public SecuritySettingsUpdateDto? Security { get; set; }
    public TTSSettingsUpdateDto? TTS { get; set; }
    public LocationSettingsUpdateDto? Location { get; set; }
}

public class NotificationSettingsUpdateDto
{
    public bool? Email { get; set; }
    public bool? Push { get; set; }
    public bool? WeeklyReport { get; set; }
}

public class SecuritySettingsUpdateDto
{
    public bool? TwoFactorEnabled { get; set; }
}

public class TTSSettingsUpdateDto
{
    public string? Voice { get; set; }
    public double? Speed { get; set; }
    public double? Pitch { get; set; }
    public int? Volume { get; set; }
    public bool? AutoPlay { get; set; }
    public string? AudioPlaybackMode { get; set; }
}

public class LocationSettingsUpdateDto
{
    public int? SensitivityRadius { get; set; }
}

public class VoiceInfo
{
    public string Language { get; set; } = string.Empty;
    public string Voice { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
}
