namespace StreetFoodNarrator.App.Core.Utils;

/// <summary>
/// Các hằng số của ứng dụng
/// </summary>
public static class Constants
{
    // GPS & Geofencing
    public const double DEFAULT_GEOFENCE_RADIUS = 50.0; // meters
    public const int GPS_UPDATE_INTERVAL = 10; // seconds
    public const int COOLDOWN_MINUTES = 5; // phút
    
    // Database
    public const string DATABASE_NAME = "streetfood.db3";
    public const int DATABASE_VERSION = 1;
    
    // Languages
    public static readonly string[] SUPPORTED_LANGUAGES = { "vi", "en", "ja", "ko", "zh" };
    public const string DEFAULT_LANGUAGE = "vi";
    
    // API (nếu có backend)
    public const string API_BASE_URL = "https://api.streetfoodnarrator.com";
    public const int API_TIMEOUT_SECONDS = 30;
    
    // Audio
    public const int MAX_AUDIO_DURATION = 300; // seconds (5 phút)
    public const string AUDIO_CACHE_FOLDER = "AudioCache";
    
    // Map
    public const double DEFAULT_MAP_ZOOM = 15.0;
    public const double VINH_KHANH_LATITUDE = 10.762622;
    public const double VINH_KHANH_LONGITUDE = 106.660172;
    
    // Permissions
    public const string PERMISSION_LOCATION = "Location";
    public const string PERMISSION_CAMERA = "Camera";
    
    // QR Code
    public const int QR_CODE_EXPIRY_DAYS = 5;
    public const string QR_CODE_PREFIX = "SFOOD_";
}
