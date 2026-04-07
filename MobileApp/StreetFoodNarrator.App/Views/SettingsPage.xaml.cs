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
    private bool _isLoadingPlaybackMode;
    private bool _isLoadingLanguagePicker;
    private bool _isLoadingLocationSourcePicker;
    private bool _isLoadingGpsTestModeSwitch;
    private bool _isApplyingControls;
    private bool _hasPendingChanges;
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

    private readonly List<AudioPlaybackOption> _audioPlaybackOptions = new();
    private readonly List<LocationSourceOption> _locationSourceOptions = new();

    private IAudioPlayer? _demoPlayer;

    public SettingsPage()
    {
        InitializeComponent();
        _languageService = new LanguageService();
        _ttsService = MauiProgram.Services.GetRequiredService<ITTSService>();
        _mainViewModel = MauiProgram.Services.GetService<MainViewModel>();

        RefreshPlaybackModeOptions();
        RefreshLocationSourceOptions();

        LoadSettingsAndControls();
        ReloadUIStrings();
        _ = LoadVoicesAsync();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        LanguageService.LanguageChanged -= OnLanguageChanged;
        LanguageService.LanguageChanged += OnLanguageChanged;
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
            SetupLanguagePicker();
            ReloadUIStrings();
            RefreshPlaybackModeOptions();
            RefreshLocationSourceOptions();
        });
    }

    private string Localize(string vi, string en, string zh)
        => _pendingLanguageCode switch
        {
            "en" => en,
            "zh" => zh,
            _ => vi
        };

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

    private void RefreshPlaybackModeOptions()
    {
        var selectedMode = _settings?.TTS.AudioPlaybackMode?.Trim().ToLowerInvariant() ?? AudioPlaybackModes.Auto;

        _audioPlaybackOptions.Clear();
        _audioPlaybackOptions.Add(new AudioPlaybackOption(
            AudioPlaybackModes.Auto,
            Localize("Tự động (khuyên dùng)", "Auto (recommended)", "自动（推荐）")));
        _audioPlaybackOptions.Add(new AudioPlaybackOption(
            AudioPlaybackModes.Stream,
            Localize("Phát trực tuyến khi có mạng", "Stream when online", "联网时在线播放")));

        _isLoadingPlaybackMode = true;
        AudioPlaybackModePicker.ItemsSource = _audioPlaybackOptions.Select(o => o.Label).ToList();
        var selectedIndex = _audioPlaybackOptions.FindIndex(o => o.Mode == selectedMode);
        AudioPlaybackModePicker.SelectedIndex = selectedIndex >= 0 ? selectedIndex : 0;
        _isLoadingPlaybackMode = false;
    }

    private void LoadSettingsAndControls()
    {
        _isApplyingControls = true;
        _settings = LoadSettingsFromPreferences();
        _pendingLanguageCode = _languageService.CurrentLanguage;
        _pendingLocationSourceMode = Preferences.Get(AppConfig.LocationSourceModePrefKey, AppConfig.LocationSourceReal);
        if (!string.Equals(_pendingLocationSourceMode, AppConfig.LocationSourceSimulated, StringComparison.OrdinalIgnoreCase))
            _pendingLocationSourceMode = AppConfig.LocationSourceReal;

        SetupLanguagePicker();
        SetupLocationSourcePicker();
        ApplySettingsToControls();
        ApplyUserPreferencesToControls();
        SetupGpsTestModeControls();
        _hasPendingChanges = false;
        _isApplyingControls = false;
    }

    private void SetupGpsTestModeControls()
    {
        _isLoadingGpsTestModeSwitch = true;
        GpsTestModeSwitch.IsToggled = Preferences.Get(AppConfig.GpsTestModeEnabledPrefKey, false);
        _isLoadingGpsTestModeSwitch = false;
        UpdateGpsTestModeActionButtonText();
    }

    private void UpdateGpsTestModeActionButtonText()
    {
        if (GpsTestModeActionButton == null)
            return;

        GpsTestModeActionButton.Text = GpsTestModeSwitch?.IsToggled == true
            ? Localize("Tao/Cap nhat POI test + dong bo", "Create/update test POI + sync", "chuang jian huo geng xin ce shi dian + tong bu")
            : Localize("Bat GPS test mode", "Enable GPS test mode", "kai qi GPS ce shi mo shi");
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

    private void SetupLanguagePicker()
    {
        _isLoadingLanguagePicker = true;
        LanguagePicker.ItemsSource = new List<string>
        {
            "Tiếng Việt",
            "English",
            "中文"
        };

        LanguagePicker.SelectedIndex = _pendingLanguageCode switch
        {
            "en" => 1,
            "zh" => 2,
            _ => 0
        };
        _isLoadingLanguagePicker = false;
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

    private void SetupLocationSourcePicker()
    {
        RefreshLocationSourceOptions();
        _isLoadingLocationSourcePicker = true;
        LocationSourcePicker.ItemsSource = _locationSourceOptions.Select(x => x.Label).ToList();

        var selectedIndex = _locationSourceOptions.FindIndex(x =>
            string.Equals(x.Mode, _pendingLocationSourceMode, StringComparison.OrdinalIgnoreCase));
        LocationSourcePicker.SelectedIndex = selectedIndex >= 0 ? selectedIndex : 0;
        _isLoadingLocationSourcePicker = false;
    }

    private void ApplySettingsToControls()
    {
        if (_settings == null)
            return;

        VolumeSlider.Value = _settings.TTS.Volume;
        VolumeValueLabel.Text = $"{_settings.TTS.Volume}%";

        SensitivitySlider.Value = _settings.Location.SensitivityRadius;
        SensitivityValueLabel.Text = $"{_settings.Location.SensitivityRadius}m";

        AutoPlaySwitch.IsToggled = _settings.TTS.AutoPlay;

        var mode = string.IsNullOrWhiteSpace(_settings.TTS.AudioPlaybackMode)
            ? AudioPlaybackModes.Auto
            : _settings.TTS.AudioPlaybackMode.Trim().ToLowerInvariant();

        var idx = _audioPlaybackOptions.FindIndex(x => x.Mode == mode);
        if (idx < 0) idx = 0;

        _isLoadingPlaybackMode = true;
        AudioPlaybackModePicker.SelectedIndex = idx;
        _isLoadingPlaybackMode = false;
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

        _settings.TTS.AutoPlay = AutoPlaySwitch.IsToggled;
        _settings.TTS.Volume = (int)Math.Round(VolumeSlider.Value);
        _settings.Location.SensitivityRadius = (int)Math.Round(SensitivitySlider.Value);

        var modeIndex = AudioPlaybackModePicker.SelectedIndex;
        if (modeIndex >= 0 && modeIndex < _audioPlaybackOptions.Count)
            _settings.TTS.AudioPlaybackMode = _audioPlaybackOptions[modeIndex].Mode;

        if (LanguagePicker.SelectedIndex >= 0)
        {
            _pendingLanguageCode = LanguagePicker.SelectedIndex switch
            {
                1 => "en",
                2 => "zh",
                _ => "vi"
            };
        }

        _languageService.ApplyLanguage(_pendingLanguageCode);
        var sourceIndex = LocationSourcePicker.SelectedIndex;
        if (sourceIndex >= 0 && sourceIndex < _locationSourceOptions.Count)
            _pendingLocationSourceMode = _locationSourceOptions[sourceIndex].Mode;

        if (GpsTestModeSwitch.IsToggled)
            _pendingLocationSourceMode = AppConfig.LocationSourceReal;

        Preferences.Set(AppConfig.LocationSourceModePrefKey, _pendingLocationSourceMode);
        Preferences.Set(AppConfig.GpsTestModeEnabledPrefKey, GpsTestModeSwitch.IsToggled);
        Preferences.Set(PrefHapticFeedback, HapticFeedbackSwitch.IsToggled);
        Preferences.Set(PrefKeepScreenOn, KeepScreenOnSwitch.IsToggled);
        Preferences.Set(PrefLargeText, LargeTextSwitch.IsToggled);
        DeviceDisplay.Current.KeepScreenOn = KeepScreenOnSwitch.IsToggled;

        if (_mainViewModel != null)
        {
            await _mainViewModel.ApplyLocationSourceModeAsync(
                string.Equals(_pendingLocationSourceMode, AppConfig.LocationSourceSimulated, StringComparison.OrdinalIgnoreCase));
        }

        await SaveSettingsAsync();
        _hasPendingChanges = false;
    }

    private void OnLanguagePickerChanged(object sender, EventArgs e)
    {
        if (_isApplyingControls || _isLoadingLanguagePicker || LanguagePicker.SelectedIndex < 0)
            return;

        _pendingLanguageCode = LanguagePicker.SelectedIndex switch
        {
            1 => "en",
            2 => "zh",
            _ => "vi"
        };

        SwitchVoiceForLanguage(_pendingLanguageCode);
        _hasPendingChanges = true;
        _languageService.ApplyLanguage(_pendingLanguageCode);
        ReloadUIStrings();
        RefreshPlaybackModeOptions();
        RefreshLocationSourceOptions();
        SetupLocationSourcePicker();
    }

    private void OnHeaderLanguageClicked(object sender, EventArgs e)
    {
        var next = LanguageSwitcher.GetNextLanguageCode(_pendingLanguageCode);
        var idx = next switch
        {
            "en" => 1,
            "zh" => 2,
            _ => 0
        };

        LanguagePicker.SelectedIndex = idx;
    }

    private void OnLocationSourceChanged(object sender, EventArgs e)
    {
        if (_isApplyingControls || _isLoadingLocationSourcePicker || LocationSourcePicker.SelectedIndex < 0)
            return;

        var idx = LocationSourcePicker.SelectedIndex;
        if (idx >= 0 && idx < _locationSourceOptions.Count)
        {
            _pendingLocationSourceMode = _locationSourceOptions[idx].Mode;
            _hasPendingChanges = true;
            FooterPolicyLabel.Text = BuildDataSourceFooterText();
        }
    }

    private void OnGpsTestModeToggled(object sender, ToggledEventArgs e)
    {
        if (_isApplyingControls || _isLoadingGpsTestModeSwitch)
            return;

        _hasPendingChanges = true;
        UpdateGpsTestModeActionButtonText();
    }

    private async void OnGpsTestModeActionClicked(object sender, EventArgs e)
    {
        GpsTestModeActionButton.IsEnabled = false;
        GpsTestModeActionButton.Text = "...";

        try
        {
            await MovementFileLogger.LogEventAsync(
                "gps-test",
                "activate-clicked");

            if (!GpsTestModeSwitch.IsToggled)
            {
                _isLoadingGpsTestModeSwitch = true;
                GpsTestModeSwitch.IsToggled = true;
                _isLoadingGpsTestModeSwitch = false;
            }

            var realIndex = _locationSourceOptions.FindIndex(x =>
                string.Equals(x.Mode, AppConfig.LocationSourceReal, StringComparison.OrdinalIgnoreCase));
            if (realIndex >= 0)
            {
                _isLoadingLocationSourcePicker = true;
                LocationSourcePicker.SelectedIndex = realIndex;
                _isLoadingLocationSourcePicker = false;
            }

            _pendingLocationSourceMode = AppConfig.LocationSourceReal;
            _hasPendingChanges = true;

            await SaveAllPreferencesFromControlsAsync();
            await MovementFileLogger.LogEventAsync("gps-test", "saved-settings;source=real");

            var poiId = await EnsureGpsTestPoiAsync();
            await MovementFileLogger.LogEventAsync(
                "gps-test",
                $"ensure-poi-success;poiId={(poiId?.ToString() ?? "na")}");

            if (_mainViewModel != null)
            {
                await _mainViewModel.LoadAllPoisAsync(forceSyncNow: true);
                _mainViewModel.RefreshExploreState();
            }

            await MovementFileLogger.LogEventAsync(
                "gps-test",
                $"sync-finished;lat={AppConfig.GpsTestLatitude:F7};lon={AppConfig.GpsTestLongitude:F7}");

            await CustomAlert.ShowAsync(
                Localize("GPS test mode da san sang", "GPS test mode is ready", "GPS ce shi mo shi yi jiu xu"),
                Localize(
                    $"Da tao/cap nhat POI test #{poiId?.ToString() ?? "N/A"} gan toa do that. Hay ra vi tri test de kiem tra geofence.",
                    $"Test POI #{poiId?.ToString() ?? "N/A"} is ready near your real coordinate. Move to that location to verify geofence.",
                    $"ce shi dian #{poiId?.ToString() ?? "N/A"} yi jiu xu, qing yi dong dao gai wei zhi yan zheng geofence."),
                "OK",
                AlertType.Success);
        }
        catch (Exception ex)
        {
            await MovementFileLogger.LogEventAsync("gps-test-error", ex.Message);
            await CustomAlert.ShowAsync(
                Localize("Loi GPS test mode", "GPS test mode error", "GPS ce shi mo shi cuo wu"),
                ex.Message,
                "OK",
                AlertType.Error);
        }
        finally
        {
            UpdateGpsTestModeActionButtonText();
            GpsTestModeActionButton.IsEnabled = true;
        }
    }

    private async Task<int?> EnsureGpsTestPoiAsync()
    {
        var baseUrl = AppConfig.GetResolvedApiBaseUrl().TrimEnd('/');
        var url = $"{baseUrl}/{AppConfig.GpsTestEnsurePoiApiPath}";

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(AppConfig.NetworkTimeoutSeconds) };
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(new EnsureGpsTestPoiMobileRequest
            {
                Latitude = AppConfig.GpsTestLatitude,
                Longitude = AppConfig.GpsTestLongitude,
                Address = AppConfig.GpsTestAddress
            })
        };
        request.Headers.Add("X-Gps-Test-Key", AppConfig.GpsTestApiKey);

        using var response = await http.SendAsync(request);
        var payload = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Cannot ensure GPS test POI ({(int)response.StatusCode}): {payload}");

        try
        {
            using var doc = JsonDocument.Parse(payload);
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (!string.Equals(prop.Name, "POI_ID", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (prop.Value.ValueKind == JsonValueKind.Number && prop.Value.TryGetInt32(out var parsed))
                    return parsed;
            }
        }
        catch
        {
            // Ignore parse errors; endpoint succeeded and sync will still pull data.
        }

        return null;
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
            Preferences.Set(PrefHapticFeedback, true);
            Preferences.Set(PrefKeepScreenOn, false);
            Preferences.Set(PrefLargeText, false);
            Preferences.Set(PrefSettings, JsonSerializer.Serialize(_settings));

            DeviceDisplay.Current.KeepScreenOn = false;

            _languageService.ApplyLanguage(_pendingLanguageCode);

            SetupLanguagePicker();
            RefreshPlaybackModeOptions();
            RefreshLocationSourceOptions();
            SetupLocationSourcePicker();
            ApplySettingsToControls();
            ApplyUserPreferencesToControls();
            SetupGpsTestModeControls();

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
        LanguageTitleLabel.Text = Localize("Ngôn ngữ", "Language", "语言");
        TtsTitleLabel.Text = AppStrings.Settings_Tts;
        AutoPlayTitleLabel.Text = AppStrings.Settings_AutoPlay;
        AutoPlaySubtitleLabel.Text = AppStrings.Settings_AutoPlayDesc;
        VolumeTitleLabel.Text = AppStrings.Settings_VolumeShort;
        SensitivityTitleLabel.Text = AppStrings.Settings_SensitivityShort;
        SensitivityLeftLabel.Text = Localize("Mượt hơn", "Smoother", "更平滑");
        SensitivityRightLabel.Text = Localize("Chính xác hơn", "More precise", "更精准");
        HapticTitleLabel.Text = Localize("Rung phản hồi", "Haptic feedback", "触觉反馈");
        HapticSubtitleLabel.Text = Localize("Rung nhẹ khi thao tác chính", "Light vibration for key actions", "关键操作时轻微振动");
        KeepScreenOnTitleLabel.Text = Localize("Giữ màn hình sáng", "Keep screen on", "保持屏幕常亮");
        KeepScreenOnSubtitleLabel.Text = Localize("Không tắt màn hình khi đang dùng app", "Prevent screen sleep while using app", "使用应用时不自动熄屏");
        LargeTextTitleLabel.Text = Localize("Văn bản lớn", "Large text", "大号文字");
        LargeTextSubtitleLabel.Text = Localize("Ưu tiên cỡ chữ lớn hơn cho dễ đọc", "Prefer larger text for readability", "优先使用更大字号便于阅读");
        AudioPlaybackTitleLabel.Text = Localize("Chế độ phát audio", "Audio playback mode", "音频播放模式");
        AudioPlaybackSubtitleLabel.Text = Localize("Tự động hoặc phát trực tuyến", "Automatic or streaming playback", "自动或流式播放");
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
            "Bat mode rieng de test GPS that ngoai hien truong",
            "Use dedicated mode for real-world GPS testing",
            "shi yong zhuan yong mo shi jin xing shi di GPS ce shi");
        GpsTestCoordinateLabel.Text = Localize(
            "Toa do test: 10.842598, 106.608742",
            "Test coordinate: 10.842598, 106.608742",
            "ce shi zuo biao: 10.842598, 106.608742");
        UpdateGpsTestModeActionButtonText();
        LanguagePicker.Title = Localize("Chọn ngôn ngữ", "Choose language", "选择语言");
        LocationSourcePicker.Title = Localize("Chọn nguồn vị trí", "Choose location source", "选择定位来源");
        AudioPlaybackModePicker.Title = Localize("Chọn chế độ", "Choose mode", "选择模式");
        DefaultBackButton.Text = Localize("Khôi phục mặc định", "Restore defaults", "恢复默认设置");
        SaveSettingsButton.Text = Localize("Lưu thay đổi", "Save changes", "保存更改");
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

    private void OnAudioPlaybackModeChanged(object sender, EventArgs e)
    {
        if (_isApplyingControls || _isLoadingPlaybackMode || _settings == null)
            return;

        var idx = AudioPlaybackModePicker.SelectedIndex;
        if (idx < 0 || idx >= _audioPlaybackOptions.Count)
            return;

        _settings.TTS.AudioPlaybackMode = _audioPlaybackOptions[idx].Mode;
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

    private void OnSensitivityChanged(object sender, ValueChangedEventArgs e)
    {
        if (_isApplyingControls || _settings == null)
            return;

        var value = (int)e.NewValue;
        SensitivityValueLabel.Text = $"{value}m";
        _settings.Location.SensitivityRadius = value;
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

public class AudioPlaybackOption
{
    public string Mode { get; }
    public string Label { get; }

    public AudioPlaybackOption(string mode, string label)
    {
        Mode = mode;
        Label = label;
    }
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
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string Address { get; set; } = string.Empty;
}
