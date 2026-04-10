using Microsoft.Maui.Networking;
using Microsoft.Maui.Storage;
using Plugin.Maui.Audio;
using StreetFoodNarrator.App.Core.Models;
using StreetFoodNarrator.App.Core.Services;
using StreetFoodNarrator.App.Helpers;
using StreetFoodNarrator.App.Resources.Strings;
using StreetFoodNarrator.App.ViewModels;
using System.Collections.ObjectModel;
using System.Net.Http.Json;
using System.Text.Json;

namespace StreetFoodNarrator.App.Views;

public partial class SettingsPage : ContentPage
{
    private readonly LanguageService _languageService;
    private readonly ITTSService _ttsService;
    private readonly MainViewModel? _mainViewModel;

    private readonly ObservableCollection<VoiceItemViewModel> _voices = new();
    private readonly ObservableCollection<VoiceItemViewModel> _allVoices = new();

    private UserSettings? _settings;
    private bool _isVoicePickerExpanded;
    private bool _isLoadingGpsTestModeSwitch;
    private bool _isApplyingControls;
    private bool _hasPendingChanges;
    private bool _initialGpsTestModeEnabled;
    private bool _initialSimulationToolsVisible;
    private string _pendingLanguageCode = "vi";
    private string _pendingLocationSourceMode = AppConfig.LocationSourceReal;

    private const string PrefSettings = "UserSettings";
    private const string PrefHapticFeedback = "settings_haptic_feedback";
    private const string PrefKeepScreenOn = "settings_keep_screen_on";
    private const string PrefLargeText = "settings_large_text";
    private const string HasOnboardedPreferenceKey = "has_onboarded";
    private const string AutoOpenInZoneOnNextMainPageKey = "auto_open_inzone_on_next_mainpage";

    private static readonly Dictionary<string, string> DemoTexts = new()
    {
        { "vi", "Chào mừng bạn đến với ứng dụng." },
        { "en", "Welcome to the app." },
        { "zh", "欢迎使用应用。" }
    };

    private static readonly Dictionary<string, string> VoiceDemoFiles = new()
    {
        { "vi-VN-HoaiMyNeural", "vi-VN-HoaiMyNeural_welcome.mp3" },
        { "en-US-JennyNeural", "en-US-JennyNeural_welcome.mp3" },
        { "zh-CN-XiaoxiaoNeural", "zh-CN-XiaoxiaoNeural_welcome.mp3" }
    };

    private static readonly IReadOnlyList<GpsTestPoiPointMobileRequest> FixedGpsTestPoiPoints =
    [
        new() { Latitude = 10.842078975289178, Longitude = 106.60899362124417, Priority = 10 },
        new() { Latitude = 10.842098731748207, Longitude = 106.60896276355663, Priority = 9 },
        new() { Latitude = 10.84207831569258, Longitude = 106.60906602860645, Priority = 8 }
    ];

    private readonly List<LocationSourceOption> _locationSourceOptions = new();
    private static readonly JsonSerializerOptions MobileJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private IAudioPlayer? _demoPlayer;

    public SettingsPage()
    {
        InitializeComponent();
        _languageService = new LanguageService();
        _ttsService = MauiProgram.Services.GetRequiredService<ITTSService>();
        _mainViewModel = MauiProgram.Services.GetService<MainViewModel>();

        RefreshLocationSourceOptions();

        LoadSettingsAndControls();
        ReloadUIStrings();
        _ = RefreshVipStatusUiAsync(forceSync: false);
        _ = LoadVoicesAsync();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        LanguageService.LanguageChanged -= OnLanguageChanged;
        LanguageService.LanguageChanged += OnLanguageChanged;
        _ = RefreshVipStatusUiAsync(forceSync: false);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        LanguageService.LanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(object? sender, string languageCode)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _pendingLanguageCode = languageCode;
            ReloadUIStrings();
            RefreshLocationSourceOptions();
            FilterVoicesByLanguage(_pendingLanguageCode);
            VoiceListView.ItemsSource = _voices;
            UpdateSelectedVoiceDisplay();
        });
    }

    private string Localize(string vi, string en, string zh)
        => _pendingLanguageCode switch
        {
            "en" => en,
            "zh" => zh,
            _ => vi
        };

    private async Task RefreshVipStatusUiAsync(bool forceSync)
    {
        if (_mainViewModel != null)
        {
            try
            {
                await _mainViewModel.RefreshVipSubscriptionStatusAsync(force: forceSync);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Settings] RefreshVipStatusUiAsync: {ex.Message}");
            }
        }

        ApplyVipStatusUi();
    }

    private void ApplyVipStatusUi()
    {
        if (_mainViewModel == null)
        {
            HeaderVipBadge.IsVisible = false;
            VipStatusValueLabel.Text = Localize(
                "Bạn chưa kích hoạt VIP",
                "VIP is not active",
                "VIP 未激活");
            VipStatusSubtitleLabel.Text = Localize(
                "Đăng ký VIP để mở thêm tour và tính năng cao cấp",
                "Subscribe to VIP to unlock more tours and premium features",
                "订阅 VIP 以解锁更多路线与高级功能");
            return;
        }

        var isVip = _mainViewModel.IsVipUser;
        HeaderVipBadge.IsVisible = isVip;

        if (isVip)
        {
            var expiresAtUtc = _mainViewModel.VipExpiresAtUtc;
            var daysRemaining = expiresAtUtc.HasValue
                ? Math.Max(0, (int)Math.Ceiling((expiresAtUtc.Value - DateTime.UtcNow).TotalDays))
                : 0;

            HeaderVipBadgeLabel.Text = daysRemaining > 0 ? $"VIP • {daysRemaining}d" : "VIP";
            VipStatusValueLabel.Text = Localize(
                $"VIP còn {daysRemaining} ngày",
                $"VIP has {daysRemaining} day(s) left",
                $"VIP 剩余 {daysRemaining} 天");
            VipStatusSubtitleLabel.Text = expiresAtUtc.HasValue
                ? Localize(
                    $"Hiệu lực đến: {expiresAtUtc.Value.ToLocalTime():dd/MM/yyyy HH:mm}",
                    $"Valid until: {expiresAtUtc.Value.ToLocalTime():dd/MM/yyyy HH:mm}",
                    $"有效期至：{expiresAtUtc.Value.ToLocalTime():dd/MM/yyyy HH:mm}")
                : Localize(
                    "Đang đồng bộ thời hạn VIP...",
                    "Syncing VIP validity...",
                    "正在同步 VIP 有效期...");
            return;
        }

        HeaderVipBadgeLabel.Text = "VIP";
        VipStatusValueLabel.Text = Localize(
            "Bạn chưa kích hoạt VIP",
            "VIP is not active",
            "VIP 未激活");
        VipStatusSubtitleLabel.Text = Localize(
            "Đăng ký VIP để mở thêm tour và tính năng cao cấp",
            "Subscribe to VIP to unlock more tours and premium features",
            "订阅 VIP 以解锁更多路线与高级功能");
    }

    private string BuildDataSourceFooterText()
    {
        var resolvedBaseUrl = AppConfig.GetResolvedApiBaseUrl().TrimEnd('/');
        var locationSourceLabel = string.Equals(_pendingLocationSourceMode, AppConfig.LocationSourceSimulated, StringComparison.OrdinalIgnoreCase)
            ? Localize("GPS gia lap", "Simulated GPS", "mo ni GPS")
            : Localize("GPS that", "Real GPS", "zhen shi GPS");

        return Localize(
            $"Nguon du lieu: API ({resolvedBaseUrl}) | {locationSourceLabel}",
            $"Data source: API ({resolvedBaseUrl}) | {locationSourceLabel}",
            $"shu ju lai yuan: API ({resolvedBaseUrl}) | {locationSourceLabel}");
    }

    private void LoadSettingsAndControls()
    {
        _isApplyingControls = true;
        _settings = LoadSettingsFromPreferences();
        _pendingLanguageCode = _languageService.CurrentLanguage;
        _pendingLocationSourceMode = Preferences.Get(AppConfig.LocationSourceModePrefKey, AppConfig.LocationSourceReal);
        if (!string.Equals(_pendingLocationSourceMode, AppConfig.LocationSourceSimulated, StringComparison.OrdinalIgnoreCase))
            _pendingLocationSourceMode = AppConfig.LocationSourceReal;

        RefreshLocationSourceOptions();
        RefreshLanguageSelectionLabel();
        RefreshLocationSourceSelectionLabel();
        ApplySettingsToControls();
        ApplyUserPreferencesToControls();
        SetupGpsTestModeControls();
        SetupSimulationToolsControls();
        _hasPendingChanges = false;
        _isApplyingControls = false;
    }

    private void SetupGpsTestModeControls()
    {
        _isLoadingGpsTestModeSwitch = true;
        _initialGpsTestModeEnabled = Preferences.Get(AppConfig.GpsTestModeEnabledPrefKey, false);
        GpsTestModeSwitch.IsToggled = _initialGpsTestModeEnabled;
        _isLoadingGpsTestModeSwitch = false;
    }

    private void SetupSimulationToolsControls()
    {
        if (SimulationToolsSwitch == null)
            return;

        _initialSimulationToolsVisible = Preferences.Get(
            AppConfig.ShowExploreSimulationControlsPrefKey,
            AppConfig.DefaultShowExploreSimulationControls);
        SimulationToolsSwitch.IsToggled = _initialSimulationToolsVisible;
    }

    private UserSettings LoadSettingsFromPreferences()
    {
        var cachedJson = Preferences.Get(PrefSettings, string.Empty);
        if (!string.IsNullOrWhiteSpace(cachedJson))
        {
            try
            {
                var cached = JsonSerializer.Deserialize<UserSettings>(cachedJson);
                if (cached != null)
                    return cached;
            }
            catch
            {
                // Ignore invalid cache and fall back to defaults.
            }
        }

        return new UserSettings
        {
            TTS = new TTSSettings
            {
                Voice = "vi-VN-HoaiMyNeural",
                Volume = 80,
                AutoPlay = true,
                AudioPlaybackMode = AudioPlaybackModes.Auto
            },
            Location = new LocationSettings
            {
                SensitivityRadius = 40
            }
        };
    }

    private static UserSettings CreateDefaultSettings()
    {
        return new UserSettings
        {
            TTS = new TTSSettings
            {
                Voice = "vi-VN-HoaiMyNeural",
                Volume = 80,
                AutoPlay = true,
                AudioPlaybackMode = AudioPlaybackModes.Auto
            },
            Location = new LocationSettings
            {
                SensitivityRadius = 40
            }
        };
    }

    private void RefreshLanguageSelectionLabel()
    {
        if (LanguageValueLabel == null)
            return;

        LanguageValueLabel.Text = _pendingLanguageCode switch
        {
            "en" => "EN - English",
            "zh" => "中 - 中文",
            _ => "VI - Tiếng Việt"
        };
    }

    private void RefreshLocationSourceOptions()
    {
        _locationSourceOptions.Clear();
        _locationSourceOptions.Add(new LocationSourceOption(
            AppConfig.LocationSourceReal,
            Localize("GPS thật (mặc định)", "Real GPS (default)", "真实 GPS（默认）")));
        _locationSourceOptions.Add(new LocationSourceOption(
            AppConfig.LocationSourceSimulated,
            Localize("GPS giả lập", "Simulated GPS", "模拟 GPS")));
    }

    private void RefreshLocationSourceSelectionLabel()
    {
        if (LocationSourceValueLabel == null)
            return;

        var selected = _locationSourceOptions.FirstOrDefault(x =>
            string.Equals(x.Mode, _pendingLocationSourceMode, StringComparison.OrdinalIgnoreCase));

        LocationSourceValueLabel.Text = selected?.Label
            ?? _locationSourceOptions.FirstOrDefault()?.Label
            ?? Localize("GPS thật", "Real GPS", "真实 GPS");
    }

    private void ApplySettingsToControls()
    {
        if (_settings == null)
            return;

        VolumeSlider.Value = _settings.TTS.Volume;
        VolumeValueLabel.Text = $"{_settings.TTS.Volume}%";

        AutoPlaySwitch.IsToggled = _settings.TTS.AutoPlay;
    }

    private void ApplyUserPreferencesToControls()
    {
        HapticFeedbackSwitch.IsToggled = Preferences.Get(PrefHapticFeedback, true);
        KeepScreenOnSwitch.IsToggled = Preferences.Get(PrefKeepScreenOn, false);
        LargeTextSwitch.IsToggled = Preferences.Get(PrefLargeText, false);

        DeviceDisplay.Current.KeepScreenOn = KeepScreenOnSwitch.IsToggled;
    }

    private async Task LoadVoicesAsync()
    {
        try
        {
            var voices = await _ttsService.GetVoicesAsync();
            if (voices.Count == 0)
            {
                voices =
                [
                    new VoiceInfo { Language = "vi-VN", Voice = "vi-VN-HoaiMyNeural", Name = "Hoài My (Nữ)", Gender = "Female" },
                    new VoiceInfo { Language = "en-US", Voice = "en-US-JennyNeural", Name = "Jenny (Female)", Gender = "Female" },
                    new VoiceInfo { Language = "zh-CN", Voice = "zh-CN-XiaoxiaoNeural", Name = "Xiaoxiao (女)", Gender = "Female" }
                ];
            }

            _allVoices.Clear();
            foreach (var voice in voices)
            {
                _allVoices.Add(new VoiceItemViewModel
                {
                    Voice = voice.Voice,
                    Name = voice.Name,
                    Language = voice.Language,
                    Gender = voice.Gender,
                    IsSelected = voice.Voice == _settings?.TTS.Voice
                });
            }

            if (_settings != null && !_allVoices.Any(v => v.IsSelected) && _allVoices.Count > 0)
            {
                _allVoices[0].IsSelected = true;
                _settings.TTS.Voice = _allVoices[0].Voice;
                _hasPendingChanges = true;
            }

            FilterVoicesByLanguage(_languageService.CurrentLanguage);
            VoiceListView.ItemsSource = _voices;
            UpdateSelectedVoiceDisplay();
        }
        catch (Exception ex)
        {
            await CustomAlert.ShowAsync(
                Localize("Lỗi", "Error", "错误"),
                Localize("Không thể tải danh sách giọng nói", "Unable to load voice list", "无法加载语音列表") + $": {ex.Message}",
                "OK",
                AlertType.Error);
        }
    }

    private void FilterVoicesByLanguage(string langCode)
    {
        var prefix = langCode switch
        {
            "en" => "en-US",
            "zh" => "zh-CN",
            _ => "vi-VN"
        };

        _voices.Clear();
        foreach (var voice in _allVoices.Where(v => v.Language.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            _voices.Add(voice);
    }

    private async Task SaveSettingsAsync()
    {
        if (_settings == null)
            return;

        try
        {
            var json = JsonSerializer.Serialize(_settings);
            Preferences.Set(PrefSettings, json);
        }
        catch (Exception ex)
        {
            await CustomAlert.ShowAsync(
                Localize("Lỗi", "Error", "错误"),
                Localize("Không thể lưu cài đặt", "Unable to save settings", "无法保存设置") + $": {ex.Message}",
                "OK",
                AlertType.Error);
        }
    }

    private async Task SaveAllPreferencesFromControlsAsync()
    {
        if (_settings == null)
            return;

        var requestedGpsTestModeEnabled = GpsTestModeSwitch.IsToggled;
        var requestedSimulationToolsVisible = SimulationToolsSwitch?.IsToggled == true;
        var gpsTestModeChanged = requestedGpsTestModeEnabled != _initialGpsTestModeEnabled;

        _settings.TTS.AutoPlay = AutoPlaySwitch.IsToggled;
        _settings.TTS.Volume = (int)Math.Round(VolumeSlider.Value);

        _languageService.ApplyLanguage(_pendingLanguageCode);

        if (GpsTestModeSwitch.IsToggled)
        {
            _pendingLocationSourceMode = AppConfig.LocationSourceReal;
            RefreshLocationSourceSelectionLabel();
        }

        Preferences.Set(AppConfig.LocationSourceModePrefKey, _pendingLocationSourceMode);
        Preferences.Set(AppConfig.GpsTestModeEnabledPrefKey, requestedGpsTestModeEnabled);
        if (!requestedGpsTestModeEnabled)
            Preferences.Set(AppConfig.AutoOpenExploreMapOnNextMainPageKey, false);
        Preferences.Set(AppConfig.ShowExploreSimulationControlsPrefKey, requestedSimulationToolsVisible);
        Preferences.Set(PrefHapticFeedback, HapticFeedbackSwitch.IsToggled);
        Preferences.Set(PrefKeepScreenOn, KeepScreenOnSwitch.IsToggled);
        Preferences.Set(PrefLargeText, LargeTextSwitch.IsToggled);
        DeviceDisplay.Current.KeepScreenOn = KeepScreenOnSwitch.IsToggled;

        if (_mainViewModel != null)
        {
            await _mainViewModel.ApplyLocationSourceModeAsync(
                string.Equals(_pendingLocationSourceMode, AppConfig.LocationSourceSimulated, StringComparison.OrdinalIgnoreCase));
        }

        if (gpsTestModeChanged)
        {
            if (requestedGpsTestModeEnabled)
                await EnableGpsTestModeAsync();
            else
                await DisableGpsTestModeAsync();
        }

        await SaveSettingsAsync();

        _initialGpsTestModeEnabled = requestedGpsTestModeEnabled;
        _initialSimulationToolsVisible = requestedSimulationToolsVisible;
        _hasPendingChanges = false;
    }

    private async void OnHeaderLanguageClicked(object sender, EventArgs e)
    {
        await ApplyLanguageSelectionAsync();
    }

    private async void OnLanguageSelectionTapped(object sender, EventArgs e)
    {
        await ApplyLanguageSelectionAsync();
    }

    private async Task ApplyLanguageSelectionAsync()
    {
        if (_isApplyingControls)
            return;

        var selected = await LanguageSwitcher.ShowLanguagePickerAsync(this, _languageService);
        if (string.Equals(selected, _pendingLanguageCode, StringComparison.OrdinalIgnoreCase))
            return;

        _pendingLanguageCode = selected;
        SwitchVoiceForLanguage(_pendingLanguageCode);
        _hasPendingChanges = true;
        RefreshLocationSourceOptions();
        ReloadUIStrings();
    }

    private async void OnLocationSourceSelectionTapped(object sender, EventArgs e)
    {
        if (_isApplyingControls)
            return;

        var currentIndex = _locationSourceOptions.FindIndex(x =>
            string.Equals(x.Mode, _pendingLocationSourceMode, StringComparison.OrdinalIgnoreCase));
        if (currentIndex < 0)
            currentIndex = 0;

        var selectedIndex = await CustomAlert.ShowSelectionAsync(
            title: Localize("Chọn nguồn vị trí", "Choose location source", "选择定位来源"),
            options: _locationSourceOptions.Select(x => x.Label).ToList(),
            selectedIndex: currentIndex,
            cancelText: Localize("Hủy", "Cancel", "取消"),
            hostPage: this);

        if (!selectedIndex.HasValue || selectedIndex.Value < 0 || selectedIndex.Value >= _locationSourceOptions.Count)
            return;

        var selectedMode = _locationSourceOptions[selectedIndex.Value].Mode;

        if (GpsTestModeSwitch.IsToggled &&
            string.Equals(selectedMode, AppConfig.LocationSourceSimulated, StringComparison.OrdinalIgnoreCase))
        {
            await CustomAlert.ShowAsync(
                Localize("GPS test mode đang bật", "GPS test mode is enabled", "GPS ce shi mo shi yi qi yong"),
                Localize(
                    "Hãy tắt GPS test mode nếu bạn muốn dùng GPS giả lập.",
                    "Turn off GPS test mode if you want to use simulated GPS.",
                    "ru xu shi yong mo ni GPS, qing xian guan bi GPS ce shi mo shi."),
                "OK",
                AlertType.Info);
            return;
        }

        if (string.Equals(selectedMode, _pendingLocationSourceMode, StringComparison.OrdinalIgnoreCase))
            return;

        _pendingLocationSourceMode = selectedMode;
        RefreshLocationSourceSelectionLabel();
        _hasPendingChanges = true;
        FooterPolicyLabel.Text = BuildDataSourceFooterText();
    }

    private async void OnGpsTestModeToggled(object sender, ToggledEventArgs e)
    {
        if (_isApplyingControls || _isLoadingGpsTestModeSwitch)
            return;

        if (e.Value)
        {
            _pendingLocationSourceMode = AppConfig.LocationSourceReal;
            RefreshLocationSourceSelectionLabel();
            FooterPolicyLabel.Text = BuildDataSourceFooterText();
        }

        _hasPendingChanges = true;
    }

    private void OnSimulationToolsToggled(object sender, ToggledEventArgs e)
    {
        if (_isApplyingControls)
            return;

        _hasPendingChanges = true;
    }

    private async Task EnableGpsTestModeAsync()
    {
        await MovementFileLogger.LogEventAsync("gps-test", "enable-requested-via-toggle");

        var poiIds = await EnsureGpsTestPoisAsync();

        await MovementFileLogger.LogEventAsync(
            "gps-test",
            $"ensure-pois-success;count={poiIds.Count};ids={string.Join(',', poiIds)}");

        await SyncGpsTestPoisToMainViewModelAsync();
        Preferences.Set(AppConfig.AutoOpenExploreMapOnNextMainPageKey, true);
    }

    private async Task DisableGpsTestModeAsync()
    {
        await MovementFileLogger.LogEventAsync("gps-test", "disable-requested-via-toggle");

        var deletedCount = await CleanupGpsTestPoisAsync();
        await MovementFileLogger.LogEventAsync("gps-test", $"cleanup-success;deleted={deletedCount}");

        await SyncGpsTestPoisToMainViewModelAsync();
        Preferences.Set(AppConfig.AutoOpenExploreMapOnNextMainPageKey, false);
    }

    private async Task SyncGpsTestPoisToMainViewModelAsync()
    {
        if (_mainViewModel == null)
            return;

        await _mainViewModel.LoadAllPoisAsync(forceSyncNow: true);
        _mainViewModel.RefreshExploreState();
    }

    private async Task<IReadOnlyList<int>> EnsureGpsTestPoisAsync()
    {
        var baseUrl = AppConfig.GetResolvedApiBaseUrl().TrimEnd('/');
        var url = $"{baseUrl}/{AppConfig.GpsTestEnsurePoiApiPath}";

        var points = FixedGpsTestPoiPoints
            .Select(p => new GpsTestPoiPointMobileRequest
            {
                Latitude = p.Latitude,
                Longitude = p.Longitude,
                Priority = p.Priority
            })
            .ToList();

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(AppConfig.NetworkTimeoutSeconds) };
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(new EnsureGpsTestPoiMobileRequest
            {
                Address = "GPS test fixed coordinates (3 POIs)",
                Points = points
            })
        };
        request.Headers.Add("X-Gps-Test-Key", AppConfig.GpsTestApiKey);

        using var response = await http.SendAsync(request);
        var payload = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Cannot ensure GPS test POIs ({(int)response.StatusCode}): {payload}");

        return ParseGpsTestPoiIds(payload);
    }

    private async Task<int> CleanupGpsTestPoisAsync()
    {
        var baseUrl = AppConfig.GetResolvedApiBaseUrl().TrimEnd('/');
        var url = $"{baseUrl}/{AppConfig.GpsTestCleanupPoiApiPath}";

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(AppConfig.NetworkTimeoutSeconds) };
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("X-Gps-Test-Key", AppConfig.GpsTestApiKey);

        using var response = await http.SendAsync(request);
        var payload = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Cannot cleanup GPS test POIs ({(int)response.StatusCode}): {payload}");

        try
        {
            using var doc = JsonDocument.Parse(payload);
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("deletedCount", out var deletedCountValue) &&
                deletedCountValue.ValueKind == JsonValueKind.Number &&
                deletedCountValue.TryGetInt32(out var parsed))
                return parsed;
        }
        catch
        {
            // Ignore parse errors for best-effort log details.
        }

        return 0;
    }

    private static IReadOnlyList<int> ParseGpsTestPoiIds(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return Array.Empty<int>();

        try
        {
            using var doc = JsonDocument.Parse(payload);
            var ids = new HashSet<int>();

            void AddPoiIdIfPresent(JsonElement element)
            {
                if (element.ValueKind != JsonValueKind.Object)
                    return;

                foreach (var prop in element.EnumerateObject())
                {
                    if (!string.Equals(prop.Name, "POI_ID", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (prop.Value.ValueKind == JsonValueKind.Number && prop.Value.TryGetInt32(out var parsed) && parsed > 0)
                        ids.Add(parsed);
                }
            }

            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in doc.RootElement.EnumerateArray())
                    AddPoiIdIfPresent(item);
            }
            else if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                AddPoiIdIfPresent(doc.RootElement);

                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    if (!string.Equals(prop.Name, "data", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (prop.Value.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in prop.Value.EnumerateArray())
                            AddPoiIdIfPresent(item);
                    }
                    else
                    {
                        AddPoiIdIfPresent(prop.Value);
                    }
                }
            }

            return ids.ToList();
        }
        catch (JsonException)
        {
            return Array.Empty<int>();
        }
    }

    private void SwitchVoiceForLanguage(string lang)
    {
        if (_settings == null || _allVoices.Count == 0)
            return;

        FilterVoicesByLanguage(lang);

        var prefix = lang switch
        {
            "en" => "en-US",
            "zh" => "zh-CN",
            _ => "vi-VN"
        };

        var matchingVoice = _allVoices.FirstOrDefault(v => v.Language.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        if (matchingVoice == null)
            return;

        foreach (var voice in _allVoices)
            voice.IsSelected = voice.Voice == matchingVoice.Voice;
        foreach (var voice in _voices)
            voice.IsSelected = voice.Voice == matchingVoice.Voice;

        _settings.TTS.Voice = matchingVoice.Voice;

        VoiceListView.ItemsSource = _voices;
        UpdateSelectedVoiceDisplay();
        _hasPendingChanges = true;
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        try
        {
            var shouldOpenExploreMap = Preferences.Get(AppConfig.AutoOpenExploreMapOnNextMainPageKey, false);
            if (shouldOpenExploreMap && Shell.Current != null)
            {
                await Shell.Current.GoToAsync("//MapPage", false);
                return;
            }

            if (Navigation?.ModalStack?.Count > 0)
            {
                await Navigation.PopModalAsync(false);
                return;
            }

            var shellNav = Shell.Current?.Navigation;
            if (shellNav != null)
            {
                var modalStack = shellNav.ModalStack;
                if (modalStack.Count > 0)
                {
                    await shellNav.PopModalAsync(false);
                    return;
                }
            }

            if (Navigation?.NavigationStack?.Count > 1)
            {
                await Navigation.PopAsync(false);
                return;
            }

            // Try parent route first; this works for common Shell navigation paths.
            if (Shell.Current != null)
            {
                var shell = Shell.Current;
                var routeBeforeBack = shell.CurrentState?.Location?.OriginalString ?? string.Empty;
                var isOnSettingsRoute = routeBeforeBack.Contains("SettingsPage", StringComparison.OrdinalIgnoreCase);

                try
                {
                    await shell.GoToAsync("..");

                    // Shell root/tab routes can treat ".." as a no-op (no exception thrown).
                    // If still on SettingsPage, continue to explicit fallback route handling.
                    var routeAfterBack = shell.CurrentState?.Location?.OriginalString ?? string.Empty;
                    var stillOnSettingsRoute = routeAfterBack.Contains("SettingsPage", StringComparison.OrdinalIgnoreCase);
                    if (!stillOnSettingsRoute || !isOnSettingsRoute)
                        return;
                }
                catch
                {
                    // Continue to route fallback below.
                }

                var previousRoute = AppShell.LastNonSettingsRoute;
                if (!string.IsNullOrWhiteSpace(previousRoute) &&
                    !previousRoute.Contains("SettingsPage", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        await shell.GoToAsync(previousRoute, animate: false);
                        return;
                    }
                    catch
                    {
                        // Continue to direct map fallback below.
                    }
                }

                // Root/tab-hosted settings page fallback.
                try
                {
                    await shell.GoToAsync("//MapPage", animate: false);
                    return;
                }
                catch
                {
                    if (TrySelectShellContent(shell, "MapPage"))
                        return;
                }
            }

            var nav = Navigation;
            if (nav?.NavigationStack.Count > 0)
                await nav.PopToRootAsync(false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Settings] OnBackClicked: {ex}");
        }
    }

    private static bool TrySelectShellContent(Shell shell, string targetRoute)
    {
        foreach (var shellItem in shell.Items)
        {
            foreach (var section in shellItem.Items)
            {
                foreach (var content in section.Items)
                {
                    if (!string.Equals(content.Route, targetRoute, StringComparison.OrdinalIgnoreCase))
                        continue;

                    shell.CurrentItem = shellItem;
                    shellItem.CurrentItem = section;
                    section.CurrentItem = content;
                    return true;
                }
            }
        }

        return false;
    }

    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        try
        {
            var confirmed = await CustomAlert.ShowConfirmAsync(
                Localize("Đăng xuất", "Log out", "退出登录"),
                Localize("Bạn có muốn đăng xuất và thoát ứng dụng không?", "Do you want to log out and quit the app?", "你要退出登录并关闭应用吗？"),
                Localize("Có", "Yes", "是"),
                Localize("Không", "No", "否"),
                AlertType.Warning);

            if (!confirmed)
                return;

            Preferences.Remove(HasOnboardedPreferenceKey);
            Preferences.Remove("hasSeenOnboarding"); // legacy key
            Preferences.Remove(AutoOpenInZoneOnNextMainPageKey);
            Preferences.Set(AppConfig.AutoOpenExploreMapOnNextMainPageKey, false);
            QuitApplication();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Settings] OnLogoutClicked: {ex}");
        }
    }

    private async void OnResetDefaultsClicked(object sender, EventArgs e)
    {
        try
        {
            var confirmed = await CustomAlert.ShowConfirmAsync(
                Localize("Khôi phục mặc định", "Restore defaults", "恢复默认设置"),
                Localize(
                    "Bạn có muốn đưa toàn bộ cài đặt về mặc định ban đầu của ứng dụng không?",
                    "Do you want to restore all settings to the app defaults?",
                    "你要将所有设置恢复为应用默认值吗？"),
                Localize("Khôi phục", "Restore", "恢复"),
                Localize("Không", "No", "否"),
                AlertType.Warning);

            if (!confirmed)
                return;

            _isApplyingControls = true;

            _settings = CreateDefaultSettings();
            _pendingLanguageCode = "vi";
            _pendingLocationSourceMode = AppConfig.LocationSourceReal;

            Preferences.Set(AppConfig.LocationSourceModePrefKey, AppConfig.LocationSourceReal);
            Preferences.Set(AppConfig.GpsTestModeEnabledPrefKey, false);
            Preferences.Set(AppConfig.AutoOpenExploreMapOnNextMainPageKey, false);
            Preferences.Set(AppConfig.ShowExploreSimulationControlsPrefKey, AppConfig.DefaultShowExploreSimulationControls);
            Preferences.Set(PrefHapticFeedback, true);
            Preferences.Set(PrefKeepScreenOn, false);
            Preferences.Set(PrefLargeText, false);
            Preferences.Set(PrefSettings, JsonSerializer.Serialize(_settings));

            DeviceDisplay.Current.KeepScreenOn = false;

            _languageService.ApplyLanguage(_pendingLanguageCode);

            RefreshLocationSourceOptions();
            RefreshLanguageSelectionLabel();
            RefreshLocationSourceSelectionLabel();
            ApplySettingsToControls();
            ApplyUserPreferencesToControls();
            SetupGpsTestModeControls();
            SetupSimulationToolsControls();

            foreach (var voice in _allVoices)
                voice.IsSelected = voice.Voice == _settings.TTS.Voice;

            FilterVoicesByLanguage(_pendingLanguageCode);
            VoiceListView.ItemsSource = _voices;
            UpdateSelectedVoiceDisplay();

            if (_mainViewModel != null)
            {
                await _mainViewModel.ApplyLocationSourceModeAsync(
                    useSimulatedGps: false);
            }

            _hasPendingChanges = false;
            ReloadUIStrings();

            await CustomAlert.ShowAsync(
                Localize("Đã khôi phục", "Restored", "已恢复"),
                Localize(
                    "Tất cả cài đặt đã được đưa về mặc định.",
                    "All settings have been restored to defaults.",
                    "所有设置已恢复为默认值。"),
                "OK",
                AlertType.Success);
        }
        catch (Exception ex)
        {
            await CustomAlert.ShowAsync(
                Localize("Lỗi", "Error", "错误"),
                $"{Localize("Không thể khôi phục cài đặt mặc định", "Unable to restore defaults", "无法恢复默认设置")}: {ex.Message}",
                "OK",
                AlertType.Error);
        }
        finally
        {
            _isApplyingControls = false;
        }
    }

    private static void QuitApplication()
    {
        try
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                Application.Current?.Quit();
            });
        }
        catch
        {
            Environment.Exit(0);
        }
    }

    private void ReloadUIStrings()
    {
        TitleLabel.Text = AppStrings.Settings_Title;
        HeaderLanguageButton.Text = LanguageSwitcher.GetHeaderLabel(_pendingLanguageCode);
        GeneralSectionLabel.Text = AppStrings.Settings_General;
        TourSectionLabel.Text = AppStrings.Settings_TourExperience;
        VipStatusTitleLabel.Text = Localize("Gói VIP", "VIP plan", "VIP 套餐");
        LanguageTitleLabel.Text = Localize("Ngôn ngữ", "Language", "语言");
        TtsTitleLabel.Text = AppStrings.Settings_Tts;
        AutoPlayTitleLabel.Text = AppStrings.Settings_AutoPlay;
        AutoPlaySubtitleLabel.Text = AppStrings.Settings_AutoPlayDesc;
        VolumeTitleLabel.Text = AppStrings.Settings_VolumeShort;
        HapticTitleLabel.Text = Localize("Rung phản hồi", "Haptic feedback", "触觉反馈");
        HapticSubtitleLabel.Text = Localize("Rung nhẹ khi thao tác chính", "Light vibration for key actions", "关键操作时轻微振动");
        KeepScreenOnTitleLabel.Text = Localize("Giữ màn hình sáng", "Keep screen on", "保持屏幕常亮");
        KeepScreenOnSubtitleLabel.Text = Localize("Không tắt màn hình khi đang dùng app", "Prevent screen sleep while using app", "使用应用时不自动熄屏");
        LargeTextTitleLabel.Text = Localize("Văn bản lớn", "Large text", "大号文字");
        LargeTextSubtitleLabel.Text = Localize("Ưu tiên cỡ chữ lớn hơn cho dễ đọc", "Prefer larger text for readability", "优先使用更大字号便于阅读");
        LogoutTitleLabel.Text = Localize("Đăng xuất", "Log out", "退出登录");
        LogoutSubtitleLabel.Text = Localize("Thoát khỏi phiên hiện tại và đóng ứng dụng", "Exit current session and close app", "退出当前会话并关闭应用");
        LogoutButton.Text = Localize("Đăng xuất", "Log out", "退出登录");
        FooterPolicyLabel.Text = BuildDataSourceFooterText();
        FooterVersionLabel.Text = $"{Localize("Phiên bản", "Version", "版本")} 1.0.0";
        LocationSourceTitleLabel.Text = Localize("Nguồn vị trí", "Location source", "位置来源");
        LocationSourceSubtitleLabel.Text = Localize(
            "Chọn GPS thật hoặc GPS giả lập khi test",
            "Choose real GPS or simulated GPS for testing",
            "测试时可选择真实 GPS 或模拟 GPS");
        GpsTestModeTitleLabel.Text = Localize("GPS test mode", "GPS test mode", "GPS ce shi mo shi");
        GpsTestModeSubtitleLabel.Text = Localize(
            "Bật để đánh dấu GPS test mode (cần bấm Lưu thay đổi để áp dụng)",
            "Turn on to mark GPS test mode (tap Save changes to apply)",
            "kai qi hou jin biao ji GPS ce shi mo shi（xu dian ji bao cun hou sheng xiao）");
        GpsTestCoordinateLabel.Text = Localize(
            "Khi lưu: tạo đúng 3 điểm GPS test với priority 10, 9, 8; khi tắt và lưu sẽ dọn các điểm test này.",
            "When saved: creates exactly 3 GPS test points with priorities 10, 9, 8; disabling and saving will remove them.",
            "bao cun hou: jing que chuang jian 3 ge ce shi dian（10,9,8）；guan bi bing bao cun hui qing li ce shi dian.");
        SimulationToolsTitleLabel.Text = Localize(
            "Hien nut gia lap ExploreMap",
            "Show ExploreMap simulation buttons",
            "xian shi ExploreMap mo ni gong neng an niu");
        SimulationToolsSubtitleLabel.Text = Localize(
            "Bat de hien bo nut mo phong test nhanh tren ExploreMap",
            "Enable to show quick simulation controls on ExploreMap",
            "kai qi hou zai ExploreMap xian shi kuai su mo ni ce shi an niu");
        RefreshLanguageSelectionLabel();
        RefreshLocationSourceSelectionLabel();
        DefaultBackButton.Text = Localize("Khôi phục mặc định", "Restore defaults", "恢复默认设置");
        SaveSettingsButton.Text = Localize("Lưu thay đổi", "Save changes", "保存更改");
        ApplyVipStatusUi();
    }

    private async void OnSaveChangesClicked(object sender, EventArgs e)
    {
        try
        {
            if (!_hasPendingChanges)
            {
                await CustomAlert.ShowAsync(
                    Localize("Không có thay đổi", "No changes", "没有变更"),
                    Localize("Bạn chưa thay đổi cài đặt nào.", "You haven't changed any settings yet.", "你还没有更改任何设置。"),
                    "OK",
                    AlertType.Info);
                return;
            }

            await SaveAllPreferencesFromControlsAsync();
            await CustomAlert.ShowAsync(
                Localize("Đã lưu", "Saved", "已保存"),
                Localize("Cài đặt đã được lưu thành công.", "Your settings were saved successfully.", "设置已成功保存。"),
                "OK",
                AlertType.Success);
        }
        catch (Exception ex)
        {
            await CustomAlert.ShowAsync(
                Localize("Lỗi", "Error", "错误"),
                $"{Localize("Không thể lưu cài đặt", "Unable to save settings", "无法保存设置")}: {ex.Message}",
                "OK",
                AlertType.Error);
        }
    }

    private void OnTtsClicked(object sender, EventArgs e)
    {
        _isVoicePickerExpanded = !_isVoicePickerExpanded;
        VoicePickerContainer.IsVisible = _isVoicePickerExpanded;
        TtsExpandIcon.Text = _isVoicePickerExpanded ? "▲" : "▼";
    }

    private void OnVoiceSelected(object sender, EventArgs e)
    {
        if (sender is not Grid grid || grid.BindingContext is not VoiceItemViewModel voice || _settings == null)
            return;

        foreach (var v in _allVoices)
            v.IsSelected = v.Voice == voice.Voice;
        foreach (var v in _voices)
            v.IsSelected = v.Voice == voice.Voice;

        _settings.TTS.Voice = voice.Voice;
        _hasPendingChanges = true;

        UpdateSelectedVoiceDisplay();
    }

    private void UpdateSelectedVoiceDisplay()
    {
        var selected = _allVoices.FirstOrDefault(v => v.IsSelected);
        if (selected == null)
            return;

        SelectedVoiceName.Text = selected.Name;
        SelectedVoiceLanguage.Text = selected.Language;
        TtsSubtitleLabel.Text = selected.Name;
    }

    private void OnAutoPlayToggled(object sender, ToggledEventArgs e)
    {
        if (_isApplyingControls || _settings == null)
            return;

        _settings.TTS.AutoPlay = e.Value;
        _hasPendingChanges = true;
    }

    private void OnVolumeChanged(object sender, ValueChangedEventArgs e)
    {
        if (_isApplyingControls || _settings == null)
            return;

        var value = (int)e.NewValue;
        VolumeValueLabel.Text = $"{value}%";
        _settings.TTS.Volume = value;
        _hasPendingChanges = true;
    }

    private void OnHapticFeedbackToggled(object sender, ToggledEventArgs e)
    {
        if (_isApplyingControls)
            return;

        _hasPendingChanges = true;
        if (e.Value)
        {
            try { HapticFeedback.Default.Perform(HapticFeedbackType.Click); } catch { }
        }
    }

    private void OnKeepScreenOnToggled(object sender, ToggledEventArgs e)
    {
        if (_isApplyingControls)
            return;

        _hasPendingChanges = true;
        DeviceDisplay.Current.KeepScreenOn = e.Value;
    }

    private void OnLargeTextToggled(object sender, ToggledEventArgs e)
    {
        if (_isApplyingControls)
            return;

        _hasPendingChanges = true;
        var msg = e.Value
            ? Localize(
                "Đã bật văn bản lớn. Một số màn hình sẽ áp dụng sau khi mở lại.",
                "Large text is enabled. Some screens will update after reopening.",
                "已启用大号文字。部分页面需重新打开后生效。")
            : Localize(
                "Đã tắt văn bản lớn.",
                "Large text is disabled.",
                "已关闭大号文字。");
        _ = MainThread.InvokeOnMainThreadAsync(() =>
            CustomAlert.ShowAsync(Localize("Hiển thị", "Display", "显示"), msg, "OK", AlertType.Info));
    }

    private async void OnTestVoiceClicked(object sender, EventArgs e)
    {
        if (_settings == null)
            return;

        TestVoiceButton.IsEnabled = false;
        TestVoiceButton.Text = "...";

        try
        {
            var voiceName = _settings.TTS.Voice;
            if (string.IsNullOrWhiteSpace(voiceName))
            {
                voiceName = _languageService.CurrentLanguage switch
                {
                    "en" => "en-US-JennyNeural",
                    "zh" => "zh-CN-XiaoxiaoNeural",
                    _ => "vi-VN-HoaiMyNeural"
                };
            }

            if (!VoiceDemoFiles.TryGetValue(voiceName, out var demoFile))
            {
                demoFile = _languageService.CurrentLanguage switch
                {
                    "en" => "en-US-JennyNeural_welcome.mp3",
                    "zh" => "zh-CN-XiaoxiaoNeural_welcome.mp3",
                    _ => "vi-VN-HoaiMyNeural_welcome.mp3"
                };
            }

            _demoPlayer?.Stop();
            _demoPlayer?.Dispose();
            _demoPlayer = null;

            var stream = await FileSystem.Current.OpenAppPackageFileAsync(demoFile);
            _demoPlayer = AudioManager.Current.CreatePlayer(stream);
            _demoPlayer.Play();
        }
        catch (Exception ex)
        {
            await CustomAlert.ShowAsync(
                Localize("Lỗi", "Error", "错误"),
                Localize("Không thể phát thử giọng đọc", "Unable to play voice preview", "无法播放语音试听") + $": {ex.Message}",
                "OK",
                AlertType.Error);
        }
        finally
        {
            TestVoiceButton.IsEnabled = true;
            TestVoiceButton.Text = "▶";
        }
    }

    private async void OnTestFallbackTtsClicked(object sender, EventArgs e)
    {
        TestFallbackButton.IsEnabled = false;
        TestFallbackButton.Text = "...";

        try
        {
            var lang = _languageService.CurrentLanguage switch
            {
                "en" => "en-US",
                "zh" => "zh-CN",
                _ => "vi-VN"
            };

            var text = DemoTexts.TryGetValue(_languageService.CurrentLanguage, out var demo)
                ? demo
                : DemoTexts["vi"];

            var ok = await _ttsService.SpeakNativeFallbackAsync(text, lang);
            if (!ok)
            {
                await CustomAlert.ShowAsync(
                    Localize("Lỗi", "Error", "错误"),
                    Localize("Native fallback TTS không phát được trên thiết bị này.", "Native fallback TTS is unavailable on this device.", "此设备无法使用原生回退 TTS。"),
                    "OK",
                    AlertType.Error);
            }
        }
        catch (Exception ex)
        {
            await CustomAlert.ShowAsync(
                Localize("Lỗi", "Error", "错误"),
                Localize("Test fallback TTS thất bại", "Fallback TTS test failed", "回退 TTS 测试失败") + $": {ex.Message}",
                "OK",
                AlertType.Error);
        }
        finally
        {
            TestFallbackButton.IsEnabled = true;
            TestFallbackButton.Text = "N";
        }
    }
}

public class VoiceItemViewModel : System.ComponentModel.INotifyPropertyChanged
{
    public string Voice { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
}

public class LocationSourceOption
{
    public string Mode { get; }
    public string Label { get; }

    public LocationSourceOption(string mode, string label)
    {
        Mode = mode;
        Label = label;
    }
}

public sealed class EnsureGpsTestPoiMobileRequest
{
    public string Address { get; set; } = string.Empty;
    public List<GpsTestPoiPointMobileRequest> Points { get; set; } = new();
}

public sealed class GpsTestPoiPointMobileRequest
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int Priority { get; set; }
}

public sealed class GpsTestPoiMobileDto
{
    public int POI_ID { get; set; }
}
