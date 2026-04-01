using Microsoft.Maui.Storage;
using Microsoft.Maui.Networking;
using Microsoft.Extensions.DependencyInjection;
using Plugin.Maui.Audio;
using StreetFoodNarrator.App;
using StreetFoodNarrator.App.Core.Services;
using StreetFoodNarrator.App.Resources.Strings;
using StreetFoodNarrator.App.Helpers;
using StreetFoodNarrator.App.Core.Models;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace StreetFoodNarrator.App.Views;

public partial class SettingsPage : ContentPage
{
    private readonly LanguageService _languageService;
    private readonly ITTSService _ttsService;
    private readonly IVoicePackageService _voicePackageService;

    private ObservableCollection<VoiceItemViewModel> _voices = new();
    private ObservableCollection<VoiceItemViewModel> _allVoices = new();
    private ObservableCollection<VoicePackageViewModel> _voicePackages = new();
    private List<LanguageOption> _languages = new();
    private UserSettings? _settings;
    private bool _isVoicePickerExpanded = false;
    private bool _isVoicePackagesExpanded = false;
    private System.Timers.Timer? _volumeDebounceTimer;
    private System.Timers.Timer? _sensitivityDebounceTimer;
    private bool _isLoadingPlaybackMode;
    private readonly List<AudioPlaybackOption> _audioPlaybackOptions = new()
    {
        new AudioPlaybackOption(AudioPlaybackModes.Auto, "Tự động (khuyên dùng)"),
        new AudioPlaybackOption(AudioPlaybackModes.Stream, "Nghe online khi có mạng"),
        new AudioPlaybackOption(AudioPlaybackModes.Download, "Tải về máy trước khi phát")
    };

    // Language picker meta
    private static readonly Dictionary<string, (string Flag, string DisplayName)> LangMeta = new()
    {
        { "vi", ("\U0001F1FB\U0001F1F3", "Tiếng Việt") },
        { "en", ("\U0001F1EC\U0001F1E7", "English") },
        { "zh", ("\U0001F1E8\U0001F1F3", "中文") }
    };
    private string _currentLangCode = "vi";
    
    // Demo text for testing voices
    private static readonly Dictionary<string, string> DemoTexts = new()
    {
        { "vi", "Chào mừng bạn đến với ứng dụng" },
        { "en", "Welcome to the app" },
        { "zh", "欢迎使用应用" }
    };
    // Map voice name → static demo file in Resources/Raw
    private static readonly Dictionary<string, string> VoiceDemoFiles = new()
    {
        { "vi-VN-HoaiMyNeural",   "vi-VN-HoaiMyNeural_welcome.mp3" },
        { "en-US-JennyNeural",    "en-US-JennyNeural_welcome.mp3" },
        { "zh-CN-XiaoxiaoNeural", "zh-CN-XiaoxiaoNeural_welcome.mp3" },
    };

    private IAudioPlayer? _demoPlayer;
    public SettingsPage()
    {
        InitializeComponent();
        _languageService = new LanguageService();
        _ttsService = MauiProgram.Services.GetRequiredService<ITTSService>();
        _voicePackageService = MauiProgram.Services.GetRequiredService<IVoicePackageService>();
        AudioPlaybackModePicker.ItemsSource = _audioPlaybackOptions.Select(o => o.Label).ToList();
        SetupCustomLangPicker();
        LoadSettingsAndVoices();
        WireUpSliders();
        _ = LoadVoicePackagesAsync();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;
        UpdateOfflineBanner();
        await UpdateOfflineStats();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        Connectivity.Current.ConnectivityChanged -= OnConnectivityChanged;
    }

    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
        => MainThread.BeginInvokeOnMainThread(UpdateOfflineBanner);

    private void UpdateOfflineBanner()
    {
        var offline = Connectivity.Current.NetworkAccess != NetworkAccess.Internet &&
                      Connectivity.Current.NetworkAccess != NetworkAccess.ConstrainedInternet;
        OfflineBanner.IsVisible = offline && !OfflineBannerSessionState.IsDismissed;
    }

    private void OnDismissOfflineBannerClicked(object sender, EventArgs e)
    {
        OfflineBannerSessionState.IsDismissed = true;
        UpdateOfflineBanner();
    }
    
    private void SetupCustomLangPicker()
    {
        _currentLangCode = _languageService.CurrentLanguage;
        UpdateLangTriggerDisplay(_currentLangCode);
        UpdateLangCheckmarks(_currentLangCode);
    }

    private void UpdateLangTriggerDisplay(string langCode)
    {
        if (LangMeta.TryGetValue(langCode, out var meta))
        {
            LangFlagLabel.Text = meta.Flag;
            LangNameLabel.Text = meta.DisplayName;
        }
    }

    private void UpdateLangCheckmarks(string selectedCode)
    {
        LangCheckVI.IsVisible = selectedCode == "vi";
        LangCheckEN.IsVisible = selectedCode == "en";
        LangCheckZH.IsVisible = selectedCode == "zh";

        LangOptionVI.BackgroundColor = selectedCode == "vi"
            ? Color.FromArgb("#22C55E14") : Colors.Transparent;
        LangOptionEN.BackgroundColor = selectedCode == "en"
            ? Color.FromArgb("#22C55E14") : Colors.Transparent;
        LangOptionZH.BackgroundColor = selectedCode == "zh"
            ? Color.FromArgb("#22C55E14") : Colors.Transparent;
    }

    private async void OnOpenLanguagePicker(object sender, EventArgs e)
    {
        LanguagePickerModal.IsVisible = true;
        await LangSheetCard.TranslateToAsync(0, 0, 300, Easing.CubicOut);
    }

    private async Task OnCloseLanguagePicker_Async()
    {
        await LangSheetCard.TranslateToAsync(0, 400, 250, Easing.CubicIn);
        LanguagePickerModal.IsVisible = false;
    }

    private async void OnCloseLanguagePicker(object sender, EventArgs e)
        => await OnCloseLanguagePicker_Async();

    private async void OnLanguageOptionTapped(object sender, EventArgs e)
    {
        if (sender is not Grid grid) return;
        var lang = grid.GestureRecognizers
                       .OfType<TapGestureRecognizer>()
                       .FirstOrDefault()?.CommandParameter as string;
        if (lang == null)
        {
            await OnCloseLanguagePicker_Async();
            return;
        }

        _currentLangCode = lang;
        UpdateLangCheckmarks(lang);
        UpdateLangTriggerDisplay(lang);

        await Task.Delay(180);
        await OnCloseLanguagePicker_Async();

        _languageService.ApplyLanguage(lang);
        ReloadUIStrings();
        await SwitchVoiceForLanguage(lang);
    }

    private async void LoadSettingsAndVoices()
    {
        await LoadSettings();
        await LoadVoices();
    }

    private async Task LoadSettings()
    {
        try
        {
            // Try load cached settings first
            var cachedJson = Preferences.Get("UserSettings", string.Empty);
            if (!string.IsNullOrWhiteSpace(cachedJson))
            {
                try
                {
                    _settings = JsonSerializer.Deserialize<UserSettings>(cachedJson);
                }
                catch
                {
                    _settings = null;
                }
            }

            // TODO: Call API to get settings
            // Fallback to defaults
            _settings ??= new UserSettings
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
            
            // Apply to UI
            VolumeSlider.Value = _settings.TTS.Volume;
            VolumeValueLabel.Text = $"{_settings.TTS.Volume}%";
            SensitivitySlider.Value = _settings.Location.SensitivityRadius;
            SensitivityValueLabel.Text = $"{_settings.Location.SensitivityRadius}m";
            AutoPlaySwitch.IsToggled = _settings.TTS.AutoPlay;
            ApplyPlaybackModeToPicker(_settings.TTS.AudioPlaybackMode);
        }
        catch (Exception ex)
        {
            await CustomAlert.ShowAsync("Lỗi", $"Không thể tải cài đặt: {ex.Message}", "OK", AlertType.Error);
        }
    }

    private async Task LoadVoices()
    {
        try
        {
            // Load voices from TTS service API
            var currentLang = _languageService.CurrentLanguage;
            var voices = await _ttsService.GetVoicesAsync();
            
            if (voices.Count == 0)
            {
                // Fallback to hardcoded list if API fails
                voices = new List<VoiceInfo>
                {
                    new VoiceInfo { Language = "vi-VN", Voice = "vi-VN-HoaiMyNeural", Name = "Hoài My (Nữ)", Gender = "Female" },
                    new VoiceInfo { Language = "en-US", Voice = "en-US-JennyNeural", Name = "Jenny (Female)", Gender = "Female" },
                    new VoiceInfo { Language = "zh-CN", Voice = "zh-CN-XiaoxiaoNeural", Name = "Xiaoxiao (女)", Gender = "Female" },
                };
            }
            
            // Store all voices
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
            
            // If no voice is selected, select the first one
            if (_settings != null && !_allVoices.Any(v => v.IsSelected) && _allVoices.Count > 0)
            {
                _allVoices[0].IsSelected = true;
                _settings.TTS.Voice = _allVoices[0].Voice;
            }
            
            // Filter voices based on current language
            FilterVoicesByLanguage(currentLang);
            
            VoiceListView.ItemsSource = _voices;
            UpdateSelectedVoiceDisplay();
        }
        catch (Exception ex)
        {
            await CustomAlert.ShowAsync("Lỗi", $"Không thể tải danh sách giọng nói: {ex.Message}", "OK", AlertType.Error);
        }
    }

    private async Task LoadVoicePackagesAsync()
    {
        try
        {
            var packages = await _voicePackageService.GetAvailablePackagesAsync();
            var selectedVoice = _settings?.TTS.Voice;

            _voicePackages.Clear();
            foreach (var pkg in packages)
            {
                pkg.IsSelected = pkg.VoiceName == selectedVoice;
                _voicePackages.Add(new VoicePackageViewModel(pkg));
            }

            VoicePackageListView.ItemsSource = _voicePackages;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Settings] LoadVoicePackages failed: {ex.Message}");
        }
    }

    private async Task DownloadVoicePackageAsync(VoicePackageViewModel packageVm)
    {
        if (packageVm.IsDownloading || packageVm.IsDownloaded)
            return;

        packageVm.IsDownloading = true;

        try
        {
            var progress = new Progress<double>(p =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                    packageVm.DownloadProgress = p);
            });

            var success = await _voicePackageService.DownloadPackageAsync(
                packageVm.Id, progress);

            if (success)
            {
                packageVm.IsDownloaded = true;
                packageVm.IsDownloading = false;
                packageVm.DownloadProgress = 1.0;
            }
            else
            {
                packageVm.IsDownloading = false;
                packageVm.DownloadProgress = 0;
            }
        }
        catch (Exception ex)
        {
            packageVm.IsDownloading = false;
            System.Diagnostics.Debug.WriteLine($"[Settings] DownloadVoicePackage failed: {ex.Message}");
        }
    }

    private async Task SelectVoicePackageAsync(VoicePackageViewModel packageVm)
    {
        // Deselect all
        foreach (var pkg in _voicePackages)
            pkg.IsSelected = false;

        // Select this one
        packageVm.IsSelected = true;

        // Update settings
        if (_settings != null)
        {
            _settings.TTS.Voice = packageVm.VoiceName;
            await SaveSettings();

            // Update voice list too
            foreach (var v in _allVoices)
                v.IsSelected = v.Voice == packageVm.VoiceName;

            UpdateSelectedVoiceDisplay();
        }
    }

    private void FilterVoicesByLanguage(string langCode)
    {
        // Chỉ 3 ngôn ngữ
        var voiceLangPrefix = langCode switch
        {
            "vi" => "vi-VN",
            "en" => "en-US",
            "zh" => "zh-CN",
            _ => "vi-VN"
        };
        
        // Filter voices by language
        _voices.Clear();
        foreach (var voice in _allVoices.Where(v => v.Language.StartsWith(voiceLangPrefix, StringComparison.OrdinalIgnoreCase)))
        {
            _voices.Add(voice);
        }
    }

    private void WireUpSliders()
    {
        VolumeSlider.ValueChanged += OnVolumeChanged;
        SensitivitySlider.ValueChanged += OnSensitivityChanged;
        AutoPlaySwitch.Toggled += OnAutoPlayToggled;
    }

    private void OnVolumeChanged(object? sender, ValueChangedEventArgs e)
    {
        var value = (int)e.NewValue;
        VolumeValueLabel.Text = $"{value}%";
        
        // Debounce save
        _volumeDebounceTimer?.Stop();
        _volumeDebounceTimer = new System.Timers.Timer(500);
        _volumeDebounceTimer.Elapsed += async (s, ev) =>
        {
            _volumeDebounceTimer.Stop();
            if (_settings != null)
            {
                _settings.TTS.Volume = value;
                await SaveSettings();
            }
        };
        _volumeDebounceTimer.Start();
    }

    private void OnSensitivityChanged(object? sender, ValueChangedEventArgs e)
    {
        var value = (int)e.NewValue;
        SensitivityValueLabel.Text = $"{value}m";
        
        // Debounce save
        _sensitivityDebounceTimer?.Stop();
        _sensitivityDebounceTimer = new System.Timers.Timer(500);
        _sensitivityDebounceTimer.Elapsed += async (s, ev) =>
        {
            _sensitivityDebounceTimer.Stop();
            if (_settings != null)
            {
                _settings.Location.SensitivityRadius = value;
                await SaveSettings();
            }
        };
        _sensitivityDebounceTimer.Start();
    }

    private async void OnAutoPlayToggled(object? sender, ToggledEventArgs e)
    {
        if (_settings != null)
        {
            _settings.TTS.AutoPlay = e.Value;
            await SaveSettings();
        }
    }

    private async void OnAudioPlaybackModeChanged(object? sender, EventArgs e)
    {
        if (_isLoadingPlaybackMode || _settings == null) return;

        var idx = AudioPlaybackModePicker.SelectedIndex;
        if (idx < 0 || idx >= _audioPlaybackOptions.Count) return;

        _settings.TTS.AudioPlaybackMode = _audioPlaybackOptions[idx].Mode;
        await SaveSettings();
    }

    private void ApplyPlaybackModeToPicker(string? mode)
    {
        var normalized = string.IsNullOrWhiteSpace(mode)
            ? AudioPlaybackModes.Auto
            : mode.Trim().ToLowerInvariant();

        var index = _audioPlaybackOptions.FindIndex(x => x.Mode == normalized);
        if (index < 0) index = 0;

        _isLoadingPlaybackMode = true;
        AudioPlaybackModePicker.SelectedIndex = index;
        _isLoadingPlaybackMode = false;
    }

    private async Task SaveSettings()
    {
        try
        {
            // TODO: Call API to save settings
            // For now, just save to preferences
            var json = JsonSerializer.Serialize(_settings);
            Preferences.Set("UserSettings", json);
        }
        catch (Exception ex)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await CustomAlert.ShowAsync("Lỗi", $"Không thể lưu cài đặt: {ex.Message}", "OK", AlertType.Error);
            });
        }
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        try
        {
            var shellNav = Shell.Current?.Navigation;
            if (shellNav != null)
            {
                // Always return to the immediate previous page if Settings was opened modally.
                var modalStack = shellNav.ModalStack;
                if (modalStack.Count > 0 && ReferenceEquals(modalStack[^1], this))
                {
                    await shellNav.PopModalAsync();
                    return;
                }
            }

            // Non-modal fallback: return to previous page in current navigation stack.
            if (Navigation?.NavigationStack?.Count > 1)
            {
                await Navigation.PopAsync();
                return;
            }

            // Last resort only when no previous page is available.
            if (Shell.Current != null)
            {
                await Shell.Current.GoToAsync("//MapPage");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Settings] OnBackClicked: {ex}");
        }
    }

    //  (OnLanguagePickerSelectionChanged removed – replaced by OnLanguageOptionTapped)

    private void ReloadUIStrings()
    {        
        TitleLabel.Text = AppStrings.Settings_Title;
        GeneralSectionLabel.Text = AppStrings.Settings_General;
        TourSectionLabel.Text = AppStrings.Settings_TourExperience;
        TtsTitleLabel.Text = AppStrings.Settings_Tts;
        TtsSubtitleLabel.Text = AppStrings.Settings_TtsSubtitle;
        AutoPlayTitleLabel.Text = AppStrings.Settings_AutoPlay;
        AutoPlaySubtitleLabel.Text = AppStrings.Settings_AutoPlayDesc;
        VolumeTitleLabel.Text = AppStrings.Settings_Volume;
        VolumeValueLabel.Text = AppStrings.Settings_VolumeShort;
        SensitivityTitleLabel.Text = AppStrings.Settings_Sensitivity;
        SensitivityValueLabel.Text = AppStrings.Settings_SensitivityShort;
        SensitivityLeftLabel.Text = AppStrings.Settings_SensitivityNear;
        SensitivityRightLabel.Text = AppStrings.Settings_SensitivityFar;
        DataSectionLabel.Text = AppStrings.Settings_Data;
        CheckOfflineButton.Text = AppStrings.Settings_CheckOffline;
        LastUpdatedLabel.Text = AppStrings.Settings_LastUpdated;
    }

    private async Task SwitchVoiceForLanguage(string lang)
    {
        if (_settings == null || _allVoices.Count == 0) return;
        
        // Map language code to voice language prefix - CHỈ 3 NGÔN NGỮ
        var voiceLangPrefix = lang switch
        {
            "vi" => "vi-VN",
            "en" => "en-US",
            "zh" => "zh-CN",
            _ => "vi-VN"
        };
        
        // Filter voice list by selected language
        FilterVoicesByLanguage(lang);
        
        // Find first voice matching the language from all voices
        var matchingVoice = _allVoices.FirstOrDefault(v => v.Language.StartsWith(voiceLangPrefix));
        if (matchingVoice != null)
        {
            // Update selection in both collections
            foreach (var v in _allVoices)
            {
                v.IsSelected = v.Voice == matchingVoice.Voice;
            }
            foreach (var v in _voices)
            {
                v.IsSelected = v.Voice == matchingVoice.Voice;
            }
            
            // Update settings
            _settings.TTS.Voice = matchingVoice.Voice;
            await SaveSettings();
            
            UpdateSelectedVoiceDisplay();
        }
    }



    private void OnTtsClicked(object sender, EventArgs e)
    {
        _isVoicePickerExpanded = !_isVoicePickerExpanded;
        VoicePickerContainer.IsVisible = _isVoicePickerExpanded;
        TtsExpandIcon.Text = _isVoicePickerExpanded ? "▲" : "▼";
    }

    private void OnVoicePackagesClicked(object sender, EventArgs e)
    {
        _isVoicePackagesExpanded = !_isVoicePackagesExpanded;
        VoicePackagesContainer.IsVisible = _isVoicePackagesExpanded;
        VoicePackagesExpandIcon.Text = _isVoicePackagesExpanded ? "▲" : "▼";
    }

    private async void OnVoicePackageDownloadTapped(object sender, EventArgs e)
    {
        if (sender is Grid grid && grid.BindingContext is VoicePackageViewModel pkgVm)
        {
            if (pkgVm.IsDownloaded)
            {
                // Already downloaded - show info or delete option
                return;
            }
            await DownloadVoicePackageAsync(pkgVm);
        }
    }

    private async void OnVoicePackageSelected(object sender, EventArgs e)
    {
        if (sender is Grid grid && grid.BindingContext is VoicePackageViewModel pkgVm)
        {
            await SelectVoicePackageAsync(pkgVm);
        }
    }

    private async void OnVoiceSelected(object sender, EventArgs e)
    {
        if (sender is not Grid grid) return;
        if (grid.BindingContext is not VoiceItemViewModel voice) return;
        
        // Update selection in both collections
        foreach (var v in _allVoices)
        {
            v.IsSelected = v.Voice == voice.Voice;
        }
        foreach (var v in _voices)
        {
            v.IsSelected = v.Voice == voice.Voice;
        }
        
        // Update settings
        if (_settings != null)
        {
            _settings.TTS.Voice = voice.Voice;
            await SaveSettings();
        }
        
        UpdateSelectedVoiceDisplay();
    }

    private void UpdateSelectedVoiceDisplay()
    {
        var selected = _voices.FirstOrDefault(v => v.IsSelected);
        if (selected != null)
        {
            SelectedVoiceName.Text = selected.Name;
            SelectedVoiceLanguage.Text = selected.Language;
            TtsSubtitleLabel.Text = selected.Name;
        }
    }

    private async void OnTestVoiceClicked(object sender, EventArgs e)
    {
        if (_settings == null) return;

        TestVoiceButton.IsEnabled = false;
        TestVoiceButton.Text = "⏳";

        try
        {
            var voiceName = _settings.TTS.Voice;
            System.Diagnostics.Debug.WriteLine($"[TTS] Demo voice: {voiceName}");

            if (!VoiceDemoFiles.TryGetValue(voiceName, out var demoFile))
            {
                // Fallback: pick by language
                var lang = _languageService.CurrentLanguage;
                demoFile = lang switch
                {
                    "en" => "en-US-JennyNeural_welcome.mp3",
                    "zh" => "zh-CN-XiaoxiaoNeural_welcome.mp3",
                    _    => "vi-VN-HoaiMyNeural_welcome.mp3"
                };
            }

            // Stop previous demo playback
            if (_demoPlayer != null)
            {
                _demoPlayer.Stop();
                _demoPlayer.Dispose();
                _demoPlayer = null;
            }

            // Open bundled asset and play
            var stream = await FileSystem.Current.OpenAppPackageFileAsync(demoFile);
            _demoPlayer = AudioManager.Current.CreatePlayer(stream);
            _demoPlayer.Play();
        }
        catch (Exception ex)
        {
            await CustomAlert.ShowAsync(
                "Lỗi",
                $"Không thể phát thử giọng đọc: {ex.Message}",
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
                    "Lỗi",
                    "Native fallback TTS không phát được trên thiết bị này.",
                    "OK",
                    AlertType.Error);
            }
        }
        catch (Exception ex)
        {
            await CustomAlert.ShowAsync(
                "Lỗi",
                $"Test fallback TTS thất bại: {ex.Message}",
                "OK",
                AlertType.Error);
        }
        finally
        {
            TestFallbackButton.IsEnabled = true;
            TestFallbackButton.Text = "N";
        }
    }

    private async void OnCheckOfflineClicked(object sender, EventArgs e)
    {
        if (sender is not Button button) return;
        
        var isWifi = Connectivity.Current.ConnectionProfiles.Contains(ConnectionProfile.WiFi);
        if (!isWifi)
        {
            var confirm = await DisplayAlert(
                AppStrings.Alert_UsingCellular_Title,
                AppStrings.Alert_UsingCellular_Message,
                AppStrings.Common_Continue, AppStrings.Common_Cancel);
            if (!confirm) return;
        }

        // Show loading state
        button.IsEnabled = false;
        var originalText = button.Text;
        button.Text = "⏳ Đang kiểm tra...";
        
        try
        {
            var repo = MauiProgram.Services.GetRequiredService<IZoneRepository>();
            var db = MauiProgram.Services.GetRequiredService<ILocalDatabaseService>();
            var audioCache = MauiProgram.Services.GetRequiredService<IAudioCacheService>();

            button.Text = "⏳ Đang tải POI...";
            await repo.SyncFromMongoAsync();
            await repo.LoadLocalAsync();
            var poiIds = repo.GetAllActiveZones().Select(p => p.Id).ToList();
            
            button.Text = "⏳ Đang tải Menu...";
            using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            foreach (var id in poiIds)
            {
                try {
                    var menuUrl = $"{AppConfig.GetResolvedApiBaseUrl()}api/MenuItems?poiId={id}&page=1&pageSize=50";
                    var response = await client.GetAsync(menuUrl);
                    if (response.IsSuccessStatusCode) {
                        var content = await response.Content.ReadAsStringAsync();
                        var result = JsonSerializer.Deserialize<MenuItemResponse>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        if (result?.Data != null && result.Data.Any()) {
                            await db.SaveMenuItemsAsync(result.Data);
                        }
                    }
                } catch { /* ignore individual failures */ }
            }

            var progress = new Progress<(int done, int total)>(p =>
                MainThread.BeginInvokeOnMainThread(() =>
                    button.Text = $"⏳ Đang tải audio {p.done}/{p.total}..."));

            await audioCache.PreloadAllAsync(poiIds, progress);
            
            // Reset button
            button.IsEnabled = true;
            button.Text = originalText;
            
            Preferences.Set("LastSyncTime", DateTime.Now.ToString("dd/MM HH:mm"));
            await UpdateOfflineStats();
            
            // Show success result
            await CustomAlert.ShowAsync(
                "Đã cập nhật",
                "Dữ liệu offline đã được cập nhật phiên bản mới nhất.",
                "OK",
                AlertType.Success);
        }
        catch (Exception ex)
        {
            button.IsEnabled = true;
            button.Text = originalText;
            
            await CustomAlert.ShowAsync(
                "Lỗi cập nhật",
                $"Không thể tải dữ liệu offline.\nChi tiết: {ex.Message}",
                "OK",
                AlertType.Error);
        }
    }

    private async void OnDeleteDataClicked(object sender, EventArgs e)
    {
        var confirm = await CustomAlert.ShowConfirmAsync(
            "Xóa Audio Offline?",
            "Việc xóa này sẽ chỉ xóa tập tin âm thanh tải về, giải phóng bộ nhớ. " +
            "Bạn có chắc chắn muốn xóa không?",
            "Xóa",
            "Hủy",
            AlertType.Warning);
        
        if (confirm)
        {
            try
            {
                var audioCache = MauiProgram.Services.GetRequiredService<IAudioCacheService>();
                await audioCache.ClearAsync();
                
                await UpdateOfflineStats();

                await CustomAlert.ShowAsync(
                    "Đã xóa",
                    "Dữ liệu audio offline đã được xóa.\n\n" +
                    "Vào Cài đặt > Tải dữ liệu offline để tải lại.",
                    "OK",
                    AlertType.Success);
            }
            catch (Exception ex)
            {
                await CustomAlert.ShowAsync(
                    "Lỗi",
                    $"Lỗi khi xóa: {ex.Message}",
                    "OK",
                    AlertType.Error);
            }
        }
    }

    private async void OnClearMapCacheClicked(object sender, EventArgs e)
    {
        var confirm = await DisplayAlert(
            "Xóa bộ nhớ cache bản đồ?",
            "Bản đồ sẽ cần tải lại khi có internet.",
            "Xóa",
            "Hủy"
        );

        if (confirm)
        {
            try
            {
                var cacheDir = Path.Combine(
                    FileSystem.AppDataDirectory,
                    "map_cache"
                );

                if (Directory.Exists(cacheDir))
                {
                    Directory.Delete(cacheDir, true);
                    await DisplayAlert("Thành công", "Đã xóa cache bản đồ", "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Lỗi", $"Không thể xóa cache: {ex.Message}", "OK");
            }
        }
    }

    private async Task UpdateOfflineStats()
    {
        try
        {
            var repo = MauiProgram.Services.GetRequiredService<IZoneRepository>();
            var audioCache = MauiProgram.Services.GetRequiredService<IAudioCacheService>();
            
            var poiCount = repo.GetAllActiveZones().Count();
            var cacheSizeBytes = audioCache.GetCacheSizeBytes();
            var cacheSizeMb = cacheSizeBytes / (1024 * 1024.0);
            
            // Lấy thời gian cập nhật cuối từ Preferences (giả sử repo lưu lại)
            var lastSyncStr = Preferences.Get("LastSyncTime", "Chưa rõ");

            MainThread.BeginInvokeOnMainThread(() =>
            {
                OfflineDataInfoLabel.Text = $"{poiCount} quán • {cacheSizeMb:F1} MB • Cập nhật: {lastSyncStr}";
            });
        }
        catch { /* Silent fail */ }
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
            if (_isSelected != value)
            {
                _isSelected = value;
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }
    }
    
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
}

public class LanguageOption
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
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
