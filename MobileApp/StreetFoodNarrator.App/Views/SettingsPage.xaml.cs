using Microsoft.Maui.Storage;
using StreetFoodNarrator.App;
using StreetFoodNarrator.App.Resources.Strings;
using StreetFoodNarrator.App.Helpers;
using StreetFoodNarrator.App.Core.Models;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace StreetFoodNarrator.App.Views;

public partial class SettingsPage : ContentPage
{
    private const string LangVi = "vi";
    private const string LangEn = "en";
    private const string LangZh = "zh";
    private const string LangJa = "ja";
    private const string LangKo = "ko";
    private const string LangFr = "fr";
    
    private ObservableCollection<VoiceItemViewModel> _voices = new();
    private ObservableCollection<VoiceItemViewModel> _allVoices = new();
    private List<LanguageOption> _languages = new();
    private UserSettings? _settings;
    private bool _isVoicePickerExpanded = false;
    private System.Timers.Timer? _volumeDebounceTimer;
    private System.Timers.Timer? _sensitivityDebounceTimer;
    
    // Demo text for testing voices
    private static readonly Dictionary<string, string> DemoTexts = new()
    {
        { "vi", "Chào mừng bạn đến với ứng dụng" },
        { "en", "Welcome to the app" },
        { "zh", "欢迎使用应用" },
        { "ja", "アプリへようこそ" },
        { "ko", "앱에 오신 것을 환영합니다" },
        { "fr", "Bienvenue dans l'application" }
    };
    
    public SettingsPage()
    {
        InitializeComponent();
        SetupLanguagePicker();
        var lang = Preferences.Get(AppConfig.LanguagePrefKey, LangVi);
        ApplyLanguage(lang);
        LoadSettingsAndVoices();
        WireUpSliders();
    }
    
    private void SetupLanguagePicker()
    {
        _languages = new List<LanguageOption>
        {
            new() { Code = "vi", Name = "🇻🇳 Tiếng Việt" },
            new() { Code = "en", Name = "🇬🇧 English" },
            new() { Code = "zh", Name = "🇨🇳 中文" },
            new() { Code = "ja", Name = "🇯🇵 日本語" },
            new() { Code = "ko", Name = "🇰🇷 한국어" },
            new() { Code = "fr", Name = "🇫🇷 Français" }
        };
        
        LanguagePicker.ItemsSource = _languages;
        LanguagePicker.ItemDisplayBinding = new Binding("Name");
        
        var currentLang = Preferences.Get(AppConfig.LanguagePrefKey, LangVi);
        var selectedIndex = _languages.FindIndex(l => l.Code == currentLang);
        if (selectedIndex >= 0)
        {
            LanguagePicker.SelectedIndex = selectedIndex;
        }
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
            // TODO: Call API to get settings
            // For now, use defaults
            _settings = new UserSettings
            {
                TTS = new TTSSettings
                {
                    Voice = "vi-VN-HoaiMyNeural",
                    Volume = 80,
                    AutoPlay = true
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
            // TODO: Call API to get voices
            // For now, use hardcoded list
            var voices = new List<VoiceInfo>
            {
                new VoiceInfo { Language = "vi-VN", Voice = "vi-VN-HoaiMyNeural", Name = "Hoài My (Nữ)", Gender = "Female" },
                new VoiceInfo { Language = "vi-VN", Voice = "vi-VN-NamMinhNeural", Name = "Nam Minh (Nam)", Gender = "Male" },
                new VoiceInfo { Language = "en-US", Voice = "en-US-JennyNeural", Name = "Jenny (Female)", Gender = "Female" },
                new VoiceInfo { Language = "en-US", Voice = "en-US-GuyNeural", Name = "Guy (Male)", Gender = "Male" },
                new VoiceInfo { Language = "ja-JP", Voice = "ja-JP-NanamiNeural", Name = "Nanami (女性)", Gender = "Female" },
                new VoiceInfo { Language = "ja-JP", Voice = "ja-JP-KeitaNeural", Name = "Keita (男性)", Gender = "Male" },
                new VoiceInfo { Language = "ko-KR", Voice = "ko-KR-SunHiNeural", Name = "Sun-Hi (여성)", Gender = "Female" },
                new VoiceInfo { Language = "ko-KR", Voice = "ko-KR-InJoonNeural", Name = "In-Joon (남성)", Gender = "Male" },
                new VoiceInfo { Language = "zh-CN", Voice = "zh-CN-XiaoxiaoNeural", Name = "Xiaoxiao (女)", Gender = "Female" },
                new VoiceInfo { Language = "zh-CN", Voice = "zh-CN-YunxiNeural", Name = "Yunxi (男)", Gender = "Male" },
                new VoiceInfo { Language = "fr-FR", Voice = "fr-FR-DeniseNeural", Name = "Denise (Femme)", Gender = "Female" },
                new VoiceInfo { Language = "fr-FR", Voice = "fr-FR-HenriNeural", Name = "Henri (Homme)", Gender = "Male" }
            };
            
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
            
            // Filter voices based on current language
            var currentLang = Preferences.Get(AppConfig.LanguagePrefKey, LangVi);
            FilterVoicesByLanguage(currentLang);
            
            VoiceListView.ItemsSource = _voices;
            UpdateSelectedVoiceDisplay();
        }
        catch (Exception ex)
        {
            await CustomAlert.ShowAsync("Lỗi", $"Không thể tải danh sách giọng nói: {ex.Message}", "OK", AlertType.Error);
        }
    }
    
    private void FilterVoicesByLanguage(string langCode)
    {
    
        var voiceLangPrefix = langCode switch
        {
            "vi" => "vi-VN",
            "en" => "en-US",
            "zh" => "zh-CN",
            "ja" => "ja-JP",
            "ko" => "ko-KR",
            "fr" => "fr-FR",
            _ => "vi-VN"
        };
        
        // Filter voices by language
        _voices.Clear();
        foreach (var voice in _allVoices.Where(v => v.Language == voiceLangPrefix))
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
        await Navigation.PopAsync();
    }

    private async void OnLanguagePickerSelectionChanged(object sender, EventArgs e)
    {
        if (LanguagePicker.SelectedIndex < 0) return;
        
        var selectedLang = _languages[LanguagePicker.SelectedIndex];
        var lang = selectedLang.Code;
        
        Preferences.Set(AppConfig.LanguagePrefKey, lang);
        ApplyLanguage(lang);
        
        // Auto-switch TTS voice based on language
        await SwitchVoiceForLanguage(lang);
    }

    private void ApplyLanguage(string lang)
    {
        AppStrings.SetCulture(lang);
        
        // Filter voice list by selected language
        FilterVoicesByLanguage(lang);
        
        TitleLabel.Text = AppStrings.Settings_Title;
        GeneralSectionLabel.Text = AppStrings.Settings_General;
        GoogleLoginLabel.Text = AppStrings.Settings_LoginGoogle;
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
        
        // Map language code to voice language prefix
        var voiceLangPrefix = lang switch
        {
            "vi" => "vi-VN",
            "en" => "en-US",
            "zh" => "zh-CN",
            "ja" => "ja-JP",
            "ko" => "ko-KR",
            "fr" => "fr-FR",
            _ => "vi-VN"
        };
        
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

    private async void OnGoogleLoginClicked(object sender, EventArgs e)
    {
        await CustomAlert.ShowAsync(
            AppStrings.Alert_Notice_Title,
            AppStrings.Alert_GoogleLogin_Message,
            AppStrings.Common_OK,
            AlertType.Info);
    }

    private void OnTtsClicked(object sender, EventArgs e)
    {
        _isVoicePickerExpanded = !_isVoicePickerExpanded;
        VoicePickerContainer.IsVisible = _isVoicePickerExpanded;
        TtsExpandIcon.Text = _isVoicePickerExpanded ? "▲" : "▼";
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
        }
    }

    private async void OnTestVoiceClicked(object sender, EventArgs e)
    {
        if (_settings == null) return;
        
        TestVoiceButton.IsEnabled = false;
        TestVoiceButton.Text = "⏳";
        
        try
        {
            // Get current language and demo text
            var currentLang = Preferences.Get(AppConfig.LanguagePrefKey, LangVi);
            var demoText = DemoTexts.ContainsKey(currentLang) 
                ? DemoTexts[currentLang] 
                : DemoTexts["vi"];
            
            // Call API to generate test audio
            using var http = new HttpClient { BaseAddress = new Uri(AppConfig.ApiBaseUrl) };
            http.DefaultRequestHeaders.Add("Accept", "application/json");
            
            var requestBody = new
            {
                text = demoText,
                language = _settings.TTS.Voice.Split('-')[0] + "-" + _settings.TTS.Voice.Split('-')[1] // e.g., "vi-VN"
            };
            
            var json = System.Text.Json.JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            
            var response = await http.PostAsync("api/tts/generate", content);
            
            if (response.IsSuccessStatusCode)
            {
                await CustomAlert.ShowAsync(
                    "✓ Đã phát thử",
                    $"Giọng đọc đã được tạo thành công với nội dung: \"{demoText}\"",
                    "OK",
                    AlertType.Success);
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                await CustomAlert.ShowAsync(
                    "Lỗi",
                    $"Không thể tạo audio: {response.StatusCode}",
                    "OK",
                    AlertType.Error);
            }
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

    private async void OnCheckOfflineClicked(object sender, EventArgs e)
    {
        if (sender is not Button button) return;
        
        // Show loading state
        button.IsEnabled = false;
        var originalText = button.Text;
        button.Text = "⏳ Đang kiểm tra...";
        
        try
        {
            // Simulate data check (replace with real implementation)
            await Task.Delay(1500);
            
            // Reset button
            button.IsEnabled = true;
            button.Text = originalText;
            
            // Show success result
            await CustomAlert.ShowAsync(
                "✓ Đã cập nhật",
                "Dữ liệu offline đã được cập nhật lên phiên bản mới nhất.\n\n" +
                "7 địa điểm • 2.4 MB • Cập nhật lúc 18:30",
                "OK",
                AlertType.Success);
        }
        catch (Exception)
        {
            button.IsEnabled = true;
            button.Text = originalText;
            
            await CustomAlert.ShowAsync(
                "Lỗi kiểm tra",
                "Không thể kiểm tra dữ liệu offline. Vui lòng thử lại sau.",
                "OK",
                AlertType.Error);
        }
    }

    private async void OnDeleteDataClicked(object sender, EventArgs e)
    {
        var confirm = await CustomAlert.ShowConfirmAsync(
            "⚠️ Xóa dữ liệu offline?",
            "Bạn sẽ cần tải lại dữ liệu để sử dụng offline.\n\n" +
            "Bạn có chắc chắn muốn xóa không?",
            "Xóa",
            "Hủy",
            AlertType.Warning);
        
        if (confirm)
        {
            // TODO: Clear offline data implementation
            await Task.Delay(500); // Simulate deletion
            
            await CustomAlert.ShowAsync(
                "Đã xóa",
                "Dữ liệu offline đã được xóa.\n\n" +
                "Vào Cài đặt > Tải dữ liệu offline để tải lại.",
                "OK",
                AlertType.Success);
        }
    }
}

public class VoiceItemViewModel
{
    public string Voice { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
    public bool IsSelected { get; set; }
}

public class LanguageOption
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}
