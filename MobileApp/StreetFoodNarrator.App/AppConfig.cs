namespace StreetFoodNarrator.App;

using Microsoft.Maui.Devices;

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
    //10.761921196165435, 106.70190931342479  
    // 10.7628 106.7028
    //10.76146206331572, 106.70031339896585
    public const double DefaultLatitude  =  10.7628;  // Cổng chính — 11 Đường Vĩnh Khánh, P.8, Q.4
    public const double DefaultLongitude = 106.7028;
    public const double DefaultZoom      = 24;

    // ── Backend API ───────────────────────────────────────────────
        // ── Emulator (Android AVD): http://10.0.2.2:5004/
        //    (10.0.2.2 là địa chỉ host PC khi chạy trên máy ảo Android)
      //  public static string EmulatorApiBaseUrl { get; set; } = "http://10.0.2.2:5004/";
      public static string EmulatorApiBaseUrl { get; set; } = "https://vexed-unsaturated-jovanni.ngrok-free.dev/";

        // ── Máy thật (Real Device): dùng IP LAN của PC chạy API
      //  public static string DefaultRealDeviceApiUrl { get; set; } = "http://192.168.100.9:5004/";
    public static string DefaultRealDeviceApiUrl { get; set; } = "https://vexed-unsaturated-jovanni.ngrok-free.dev/";
    public const int NetworkTimeoutSeconds = 30;
    public static bool UseBackendApi { get; set; } = true;
    public const string DataVersionKey = "pois_data_version";
    public const string LanguagePrefKey = "app_language";
    public const string MainPagePrewarmReadyKey = "mainpage_prewarm_ready";
    public const string MainPagePrewarmAtUtcKey = "mainpage_prewarm_at_utc";
    public const string LocationSourceModePrefKey = "settings_location_source_mode";
    public const string LocationSourceReal = "real";
    public const string LocationSourceSimulated = "simulated";
    public const string GpsTestModeEnabledPrefKey = "settings_gps_test_mode_enabled";
    public const string GpsTestEnsurePoiApiPath = "api/POIs/test-mode/ensure-nearby";
    public const string GpsTestCleanupPoiApiPath = "api/POIs/test-mode/cleanup";
    public const string GpsTestApiKey = "streetfood-gps-test-mode-2026";
    public const string AutoOpenExploreMapOnNextMainPageKey = "auto_open_exploremap_on_next_mainpage";
    public const string ShowExploreSimulationControlsPrefKey = "settings_show_explore_simulation_controls";
    public const bool DefaultShowExploreSimulationControls = false;
    public const double GpsTestLatitude = 10.842597772316791;
    public const double GpsTestLongitude = 106.60874204402752;
    public const string GpsTestAddress = "Toa do test GPS thuc te - 10.842597772316791, 106.60874204402752";
    public const string OfflineRouterDbAssetName = "routing/vinhkhanh_q4.routerdb";
    public const string OfflineRouterDbFileName = "vinhkhanh_q4.routerdb";

    // ── GPS Settings ──────────────────────────────────────────────
    public const double DebounceMeters = 5.0;
    public const int    DebounceMs     = 3000;
    public const double InZoneRadiusMeters = 120.0;
    public const double NearRadiusMeters = 400.0;
    public const double TrackingInsideMeters = 60.0;
    public const double TrackingNearMeters = 250.0;
    public const double FallbackInZoneMeters = 120.0;
    public const double FallbackNearMeters = 400.0;
    public const double SpotZoneMinMeters = 15.0;
    public const double SpotZoneMaxMeters = 40.0;
    public const double SpotZoneGpsErrorBufferMeters = 8.0;
    public const double SpotZoneConfidenceThreshold = 0.7;
    public const int SpotZoneActivationDelayMs = 1500;
    public const int SpotZoneSwitchCooldownMs = 700;
    public const int SpotZoneApproachToastCooldownMs = 2500;
    public const double SpotZoneDeactivationBuffer = 1.3;
    public const double SpotZoneSwitchLeadMeters = 6.0;
    public const double SpotZoneSwitchHysteresisMeters = 3.0;

    // ── Feature flags ──────────────────────────────────────────────
    public const bool DefaultUseSimulatedGps = false;
    public const bool UseSimulatedGPS = DefaultUseSimulatedGps;

    private const string CUSTOM_API_URL_KEY = "CustomApiUrl";

    /// <summary>
    /// Get the resolved API Base URL.
    /// Priority: Custom URL (from settings) > Default Real Device URL > Emulator URL
    /// </summary>
    public static string GetResolvedApiBaseUrl()
    {
        // ✅ Priority 1: Custom URL set by user
        var customUrl = Preferences.Get(CUSTOM_API_URL_KEY, "");
        if (!string.IsNullOrEmpty(customUrl))
            return customUrl;

        // ✅ Priority 2: Emulator (Android AVD)
        if (DeviceInfo.Platform == DevicePlatform.Android && DeviceInfo.DeviceType == DeviceType.Virtual)
            return EmulatorApiBaseUrl;

        // ✅ Priority 3: Real device default
        return DefaultRealDeviceApiUrl;
    }

    /// <summary>
    /// Set a custom API URL for real device connection.
    /// Use this when default IP doesn't work or you need to change server.
    /// </summary>
    /// <param name="url">Full URL including trailing slash, e.g. "http://192.168.1.100:5004/"</param>
    public static void SetCustomApiUrl(string url)
    {
        if (!string.IsNullOrEmpty(url))
        {
            // Ensure trailing slash
            url = url.TrimEnd('/') + "/";
            Preferences.Set(CUSTOM_API_URL_KEY, url);
            Console.WriteLine($"[AppConfig] Custom API URL set: {url}");
        }
    }

    /// <summary>
    /// Clear custom API URL and revert to default.
    /// </summary>
    public static void ClearCustomApiUrl()
    {
        Preferences.Remove(CUSTOM_API_URL_KEY);
        Console.WriteLine("[AppConfig] Custom API URL cleared, using defaults");
    }

    /// <summary>
    /// Check if a custom API URL is currently configured.
    /// </summary>
    public static bool HasCustomApiUrl()
    {
        return !string.IsNullOrEmpty(Preferences.Get(CUSTOM_API_URL_KEY, ""));
    }

    /// <summary>
    /// Builds an absolute API URL from the current base URL and a relative path.
    /// </summary>
    public static string BuildApiUrl(string relativePath)
    {
        var baseUrl = GetResolvedApiBaseUrl().TrimEnd('/');
        var path = string.IsNullOrWhiteSpace(relativePath)
            ? string.Empty
            : relativePath.TrimStart('/');

        return string.IsNullOrEmpty(path)
            ? baseUrl
            : $"{baseUrl}/{path}";
    }
}
