using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StreetFoodNarrator.App.Core.Models;
using StreetFoodNarrator.App.Core.Services;
using System.Collections.ObjectModel;

namespace StreetFoodNarrator.App.ViewModels;

/// <summary>
/// ViewModel for POIDetailPage.
/// Manages: tab switching, audio player state, menu items, reviews.
/// </summary>
public partial class POIDetailViewModel : ObservableObject
{
    private readonly POI _poi;
    private readonly ITTSService _tts;
    private readonly LanguageService _lang;
    private readonly ILocalDatabaseService _db;
    private readonly HttpClient _httpClient;
    private readonly Action<double>? _seekCallback;
    private readonly bool _keepCurrentAudio;

    private System.Timers.Timer? _progressTimer;
    private double _currentPosition = 0;
    private double _duration = 0;
    private bool _isNativeTts = false;
    private double _nativeTtsDuration = 0;
    private DateTime _nativeTtsStartTime;
    private bool _ownsCurrentPlayback = false;
    private bool _isMenuLoaded;
    private bool _menuSyncedFromApi;

    public POI Poi => _poi;

    public ObservableCollection<MenuItemDto> MenuItems { get; } = new();
    public ObservableCollection<Review> Reviews { get; } = new();

    [ObservableProperty]
    private int _selectedTabIndex = 0; // 0=Info, 1=Menu, 2=Map

    [ObservableProperty]
    private bool _isAudioPlaying = false;

    [ObservableProperty]
    private double _audioProgress = 0; // 0.0 – 1.0

    [ObservableProperty]
    private string _audioPositionText = "00:00";

    [ObservableProperty]
    private string _audioDurationText = "00:00";

    [ObservableProperty]
    private string _audioTitle = "Thuyết minh";

    [ObservableProperty]
    private string _audioSubtitle = "Bấm play để nghe";

    // Rating display
    [ObservableProperty]
    private string _ratingText = "4.5";

    [ObservableProperty]
    private string _reviewCountText = "";

    // Heritage year from POI
    [ObservableProperty]
    private string _heritageText = "";

    // Is open / closed
    [ObservableProperty]
    private bool _isOpen = false;

    [ObservableProperty]
    private string _localizedPoiName = string.Empty;

    [ObservableProperty]
    private string _localizedPoiDescription = string.Empty;

    public POIDetailViewModel(POI poi, Action<double>? seekCallback = null, bool keepCurrentAudio = false)
    {
        _poi = poi;
        _tts = MauiProgram.Services.GetRequiredService<ITTSService>();
        _lang = MauiProgram.Services.GetRequiredService<LanguageService>();
        _db = MauiProgram.Services.GetRequiredService<ILocalDatabaseService>();
        _httpClient = MauiProgram.Services.GetRequiredService<HttpClient>();
        _seekCallback = seekCallback;
        _keepCurrentAudio = keepCurrentAudio;

        // Wire up TTS events FIRST — before any async work
        _tts.OnPlaybackEnded += OnTtsPlaybackEnded;

        // Set audio title/subtitle based on current language
        RefreshLocalizedAudioTitle();
        RefreshLocalizedContent();

        // Set rating / review count
        RatingText = _poi.Rating?.ToString("F1") ?? "4.5";
        if (_poi.NumReviews > 0)
            ReviewCountText = _poi.NumReviews.ToString("N0") + " đánh giá";
        else
            ReviewCountText = "";

        // Heritage (use EstimatedHours as proxy or a fixed placeholder)
        if (!string.IsNullOrEmpty(_poi.EstimatedHours))
            HeritageText = _poi.EstimatedHours;
        else
            HeritageText = "Vinhs Khanh";

        // Open status (simple time check)
        UpdateOpenStatus();

        if (_keepCurrentAudio)
        {
            AttachToCurrentPlayback();
        }
    }

    private void RefreshLocalizedAudioTitle()
    {
        var lang = _lang.CurrentLanguage;
        AudioTitle = lang switch
        {
            "en" => "Audio Guide",
            "zh" => "音频导览",
            _ => "Thuyết minh"
        };

        var name = _poi.GetName(lang);
        AudioSubtitle = !string.IsNullOrEmpty(name) ? name : _poi.Name_Vi;
    }

    /// <summary>
    /// Refresh all localized strings. Call from OnAppearing to pick up language changes
    /// made while this page was open.
    /// </summary>
    public void RefreshLocalizedContent()
    {
        RefreshLocalizedAudioTitle();
        var lang = _lang.CurrentLanguage;
        LocalizedPoiName = _poi.GetName(lang);
        LocalizedPoiDescription = _poi.GetDescription(lang)
            ?? _poi.Description_Vi
            ?? _poi.Description_En
            ?? _poi.Description_Zh
            ?? string.Empty;

        if (_poi.NumReviews > 0)
        {
            ReviewCountText = lang switch
            {
                "en" => $"{_poi.NumReviews:N0} reviews",
                "zh" => $"{_poi.NumReviews:N0} 条评价",
                _ => _poi.NumReviews.ToString("N0") + " đánh giá"
            };
        }
        else
        {
            ReviewCountText = string.Empty;
        }
    }

    private void UpdateOpenStatus()
    {
        if (string.IsNullOrEmpty(_poi.OpeningHoursText))
        {
            IsOpen = true; // assume open if no info
            return;
        }
        // Parse "HH:mm - HH:mm" and check if current ICT time is within range
        try
        {
            var parts = _poi.OpeningHoursText.Split(" - ", StringSplitOptions.TrimEntries);
            if (parts.Length == 2)
            {
                var now = DateTime.Now;
                var start = TimeSpan.Parse(parts[0]);
                var end = TimeSpan.Parse(parts[1]);
                var current = now.TimeOfDay;
                IsOpen = current >= start && current <= end;
            }
            else
            {
                IsOpen = true;
            }
        }
        catch
        {
            IsOpen = true;
        }
    }

    private string ResolveLanguageCode()
    {
        return _lang.CurrentLanguage switch
        {
            "en" => "en-US",
            "zh" => "zh-CN",
            _ => "vi-VN"
        };
    }

    private string ResolveNarrationText(string languageCode)
    {
        return _poi.GetDescription(languageCode)
               ?? _poi.GetName(languageCode)
               ?? "Chao mung den voi diem tham quan.";
    }

    private double ResolveDurationOrEstimate(string? fallbackText = null)
    {
        var duration = _tts.GetDuration();
        if (duration > 0) return duration;

        var estimatedText = fallbackText ?? ResolveNarrationText(ResolveLanguageCode());
        return Math.Max(1, estimatedText.Length / 12.0);
    }

    private void AttachToCurrentPlayback()
    {
        var position = _tts.GetCurrentPosition();
        var isPlaying = _tts.IsPlaying();

        if (position <= 0 && !isPlaying)
            return;

        _ownsCurrentPlayback = false;
        _duration = ResolveDurationOrEstimate();
        _currentPosition = Math.Min(position, _duration);
        _isNativeTts = true;
        _nativeTtsDuration = _duration;
        _nativeTtsStartTime = DateTime.Now.AddSeconds(-_currentPosition);

        AudioDurationText = FormatTime(_duration);
        AudioPositionText = FormatTime(_currentPosition);
        AudioProgress = _duration > 0 ? Math.Clamp(_currentPosition / _duration, 0, 1) : 0;
        // If position is moving but service cannot report IsPlaying (native fallback),
        // keep the UI in playing state so progress remains real-time.
        IsAudioPlaying = isPlaying || position > 0;

        if (IsAudioPlaying)
            StartProgressTimer();
    }

    // ── Tab commands ─────────────────────────────────────────────────────
    [RelayCommand]
    private void SelectTab(int index) => SelectedTabIndex = index;

    // ── Audio commands ───────────────────────────────────────────────────
    [RelayCommand]
    private async Task TogglePlayPauseAsync()
    {
        if (IsAudioPlaying)
        {
            _tts.Pause();
            IsAudioPlaying = false;
            _currentPosition = _tts.GetCurrentPosition();
            AudioPositionText = FormatTime(_currentPosition);
            if (_duration > 0)
                AudioProgress = Math.Clamp(_currentPosition / _duration, 0, 1);
            return;
        }

        // If this page was opened from currently playing card, continue the same audio session.
        if (_keepCurrentAudio && _tts.GetCurrentPosition() > 0)
        {
            _tts.Resume();
            if (_tts.IsPlaying())
            {
                _ownsCurrentPlayback = false;
                _duration = ResolveDurationOrEstimate();
                _currentPosition = _tts.GetCurrentPosition();
                AudioDurationText = FormatTime(_duration);
                AudioPositionText = FormatTime(_currentPosition);
                AudioProgress = _duration > 0 ? Math.Clamp(_currentPosition / _duration, 0, 1) : 0;
                IsAudioPlaying = true;
                _isNativeTts = true;
                _nativeTtsDuration = _duration;
                _nativeTtsStartTime = DateTime.Now.AddSeconds(-_currentPosition);
                StartProgressTimer();
                return;
            }
        }

        var lang = ResolveLanguageCode();
        var text = ResolveNarrationText(lang);

        // Estimate duration in advance. If TTS reports exact duration later, timer will use that.
        _duration = ResolveDurationOrEstimate(text);
        _currentPosition = 0;
        AudioDurationText = FormatTime(_duration);
        AudioPositionText = "00:00";
        AudioProgress = 0;

        // Start UI playback state immediately so progress is real-time even when
        // SpeakAsync falls back to native TTS (which can complete only after speaking).
        _ownsCurrentPlayback = true;
        _isNativeTts = true;
        _nativeTtsDuration = _duration;
        _nativeTtsStartTime = DateTime.Now;
        IsAudioPlaying = true;
        StartProgressTimer();

        var ok = await _tts.SpeakAsync(text, lang, poiId: _poi.Id);
        if (!ok)
        {
            _isNativeTts = false;
            _ownsCurrentPlayback = false;
            _progressTimer?.Stop();
            IsAudioPlaying = false;
            AudioProgress = 0;
            AudioPositionText = "00:00";
        }
    }

    private void StartProgressTimer()
    {
        _progressTimer?.Stop();
        _progressTimer?.Dispose();
        _progressTimer = new System.Timers.Timer(350);
        _progressTimer.Elapsed += (s, e) =>
        {
            var duration = _tts.GetDuration();
            if (duration <= 0)
            {
                duration = _duration > 0 ? _duration : _nativeTtsDuration;
            }

            var position = _tts.GetCurrentPosition();
            if (position <= 0 && IsAudioPlaying && duration > 0)
            {
                // Native fallback may not expose live player position.
                var elapsed = (DateTime.Now - _nativeTtsStartTime).TotalSeconds;
                position = Math.Min(elapsed, duration);
            }

            _currentPosition = Math.Max(0, position);
            _duration = Math.Max(duration, _duration);
            var progress = _duration > 0 ? _currentPosition / _duration : 0;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                AudioDurationText = FormatTime(_duration);
                AudioPositionText = FormatTime(_currentPosition);
                AudioProgress = Math.Clamp(progress, 0, 1);
            });
        };
        _progressTimer.Start();
    }

    private void OnTtsPlaybackEnded()
    {
        _progressTimer?.Stop();
        _isNativeTts = false;
        _ownsCurrentPlayback = false;
        MainThread.BeginInvokeOnMainThread(() =>
        {
            IsAudioPlaying = false;
            AudioProgress = 0;
            AudioPositionText = "00:00";
        });
    }

    public void OnSeek(double normalizedValue)
    {
        var duration = _tts.GetDuration();
        if (duration <= 0)
            duration = _duration > 0 ? _duration : _nativeTtsDuration;
        if (duration <= 0)
            return;

        var seekPos = Math.Clamp(normalizedValue, 0, 1) * duration;
        _nativeTtsStartTime = DateTime.Now.AddSeconds(-seekPos);
        _currentPosition = seekPos;
        AudioPositionText = FormatTime(_currentPosition);
        AudioProgress = Math.Clamp(normalizedValue, 0, 1);

        // Notify TTS to seek if supported
        _tts.Seek(seekPos);
    }

    private static string FormatTime(double seconds)
    {
        if (seconds < 0) seconds = 0;
        var ts = TimeSpan.FromSeconds(seconds);
        return ts.Hours > 0
            ? ts.ToString(@"hh\:mm\:ss")
            : ts.ToString(@"mm\:ss");
    }

    // ── Load menu ───────────────────────────────────────────────────────
    public async Task LoadMenuItemsAsync(bool forceReload = false)
    {
        var netAccess = Microsoft.Maui.Networking.Connectivity.Current.NetworkAccess;
        var isOnline = netAccess is Microsoft.Maui.Networking.NetworkAccess.Internet
                    or Microsoft.Maui.Networking.NetworkAccess.ConstrainedInternet;

        if (!forceReload && _isMenuLoaded && MenuItems.Count > 0)
        {
            // If previous load only came from local/fallback, retry API whenever internet is available.
            if (!isOnline || _menuSyncedFromApi)
                return;
        }

        try
        {
            if (isOnline)
            {
                var url = AppConfig.BuildApiUrl($"api/MenuItems?poiId={_poi.Id}&page=1&pageSize=50");
                var resp = await _httpClient.GetAsync(url);
                if (resp.IsSuccessStatusCode)
                {
                    var content = await resp.Content.ReadAsStringAsync();
                    var result = System.Text.Json.JsonSerializer.Deserialize<MenuItemResponse>(
                        content, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (result?.Data != null && result.Data.Any())
                    {
                        await _db.SaveMenuItemsAsync(result.Data);
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            MenuItems.Clear();
                            foreach (var item in result.Data) MenuItems.Add(item);
                        });
                        _menuSyncedFromApi = true;
                        _isMenuLoaded = true;
                        return;
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[POIDetailVM] Menu API failed for POI {_poi.Id}: {(int)resp.StatusCode}");
                }
            }

            // Offline fallback
            var local = await _db.GetMenuItemsByPoiAsync(_poi.Id);
            if (local != null && local.Any())
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    MenuItems.Clear();
                    foreach (var item in local) MenuItems.Add(item);
                });
                _menuSyncedFromApi = false;
                _isMenuLoaded = true;
                return;
            }

            // Last fallback so POIDetail menu is never blank.
            var fallback = BuildFallbackMenuItems();
            MainThread.BeginInvokeOnMainThread(() =>
            {
                MenuItems.Clear();
                foreach (var item in fallback) MenuItems.Add(item);
            });
            _menuSyncedFromApi = false;
            _isMenuLoaded = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[POIDetailVM] LoadMenuItemsAsync: {ex}");
        }
    }

    private List<MenuItemDto> BuildFallbackMenuItems()
    {
        var names = _poi.DisplaySignatureDishes;
        var list = new List<MenuItemDto>();
        var baseId = _poi.Id * 1000;
        var basePrice = _poi.AveragePrice ?? 45000;

        for (var i = 0; i < names.Count; i++)
        {
            list.Add(new MenuItemDto
            {
                MenuItemId = baseId + i + 1,
                POI_ID = _poi.Id,
                Name_Vi = names[i],
                Name_En = names[i],
                Name_Zh = names[i],
                Description_Vi = _poi.Description_Vi ?? "Món ăn đặc trưng của quán.",
                Description_En = _poi.Description_En ?? _poi.Description_Vi ?? "Signature local dish.",
                Description_Zh = _poi.Description_Zh ?? _poi.Description_Vi ?? "招牌菜",
                ImageUrl = _poi.ImageUrl ?? string.Empty,
                Price = Math.Max(10000, basePrice + (i * 10000)),
                PriceUnit = "VND",
                IsSignatureDish = i == 0
            });
        }

        if (list.Count == 0)
        {
            list.Add(new MenuItemDto
            {
                MenuItemId = baseId + 1,
                POI_ID = _poi.Id,
                Name_Vi = "Món đặc trưng",
                Name_En = "Signature Dish",
                Name_Zh = "招牌菜",
                Description_Vi = _poi.Description_Vi ?? "Thông tin menu đang cập nhật.",
                Description_En = _poi.Description_En ?? "Menu information is updating.",
                Description_Zh = _poi.Description_Zh ?? "菜单信息正在更新。",
                ImageUrl = _poi.ImageUrl ?? string.Empty,
                Price = Math.Max(10000, basePrice),
                PriceUnit = "VND",
                IsSignatureDish = true
            });
        }

        return list;
    }

    // ── Load reviews ───────────────────────────────────────────────────
    public async Task LoadReviewsAsync()
    {
        try
        {
            var local = await _db.GetReviewsByPoiAsync(_poi.Id);
            MainThread.BeginInvokeOnMainThread(() =>
            {
                Reviews.Clear();
                foreach (var r in local) Reviews.Add(r);
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[POIDetailVM] LoadReviewsAsync: {ex}");
        }
    }

    public void Cleanup()
    {
        // STOP TIMER FIRST — prevents TimerCallback from firing after we unsubscribe
        _progressTimer?.Stop();
        _progressTimer?.Dispose();
        _progressTimer = null;

        // THEN unsubscribe events — safe now that timer is stopped
        _tts.OnPlaybackEnded -= OnTtsPlaybackEnded;
        // Keep global playback alive when leaving POIDetail so audio can continue
        // seamlessly across pages (MainPage/ExploreMap/POIDetail).
    }
}
