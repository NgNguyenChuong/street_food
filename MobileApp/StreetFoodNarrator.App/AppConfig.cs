namespace StreetFoodNarrator.App;

/// <summary>
/// App-wide configuration.
/// Replace placeholder values before building for production.
/// </summary>
public static class AppConfig
{
    public const string PLACEHOLDER = "YOUR_VALUE_HERE";

    // ── MongoDB Atlas ─────────────────────────────────────────────
    /// <summary>
    /// MongoDB Atlas connection string.
    /// Format: mongodb+srv://user:pass@cluster.mongodb.net/
    /// Leave as PLACEHOLDER to use offline mock data.
    /// </summary>
    public static string MongoConnectionString { get; set; } =
        PLACEHOLDER; // e.g. "mongodb+srv://user:pass@cluster0.xxxxx.mongodb.net/"

    public const string DatabaseName   = "street_food_narrator";
    public const string CollectionName = "pois";

    // ── Maptiler ──────────────────────────────────────────────────
    /// <summary>
    /// Free Maptiler API key — get at: https://cloud.maptiler.com/account/keys/
    /// Free tier: 100,000 map tiles/month
    /// </summary>
    public static string MaptilerApiKey { get; set; } =
        PLACEHOLDER; // e.g. "aBcD1234xYzW..."

    // ── Default Map Center (Cổng chào Phố ẩm thực Vĩnh Khánh, Q.4) ──────
    public const double DefaultLatitude  = 10.762094471587867;  // Cổng chính — 11 Đường Vĩnh Khánh, P.8, Q.4
    public const double DefaultLongitude = 106.70189053795724;
    public const double DefaultZoom      = 24;
    // Backend API
    // Emulator: http://10.0.2.2:5004  |  Real device: http://<YOUR_PC_IP>:5004
    public static string ApiBaseUrl { get; set; } = "http://10.0.2.2:5004/";
    public static bool UseBackendApi { get; set; } = true;
    public const string DataVersionKey = "pois_data_version";
    public const string LanguagePrefKey = "app_language";

    // ── GPS Settings ──────────────────────────────────────────────
    public const double DebounceMeters = 5.0;
    public const int    DebounceMs     = 3000;

    // ── Feature flags ─────────────────────────────────────────────
    public const bool UseSimulatedGPS = true;  // Set false for real device GPS
}
