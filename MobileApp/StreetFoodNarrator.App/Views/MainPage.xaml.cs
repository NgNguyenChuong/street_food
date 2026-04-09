
// MainPage.xaml.cs  -  Core: fields, constructor, lifecycle
//
// Logic is split across partial class files:
//   MainPage.Map.cs       OSM map, pins, zoom, header button handlers
//   MainPage.Audio.cs     TTS play/pause, share
//   MainPage.Nav.cs       bottom navigation tab switching
//   MainPage.PinPopup.cs  popup when tapping a map pin
//
// ContentView components (Views/Components/) handle their own XAML and
// internal UI logic; they communicate outward only via public events.
// MainPage wires these events to ViewModel commands and cross-component logic.

using Microsoft.Extensions.DependencyInjection;
using StreetFoodNarrator.App.Core.Services;
using StreetFoodNarrator.App.Core.Services.Implementations;
using StreetFoodNarrator.App.Core.Utils;
using StreetFoodNarrator.App.Helpers;
using StreetFoodNarrator.App.Resources.Strings;
using StreetFoodNarrator.App.ViewModels;
using System.IO;

namespace StreetFoodNarrator.App.Views;

public partial class MainPage : ContentPage
{
    private const string AutoOpenInZoneOnNextMainPageKey = "auto_open_inzone_on_next_mainpage";
    private const string AutoOpenExploreMapOnNextMainPageKey = AppConfig.AutoOpenExploreMapOnNextMainPageKey;
    private const string HasOnboardedPreferenceKey = "has_onboarded";
    private const int MainPagePrewarmMaxAgeSeconds = 180;
    private const int MapOpenMinimumLoadingMs = 0;
    private readonly MainViewModel _vm;
    private readonly ITTSService _tts;
    private readonly LanguageService _lang;
    private readonly IAudioCacheService? _audioCache;
    private readonly HttpClient? _httpClient;
    private readonly IOfflineRoutingService? _offlineRouting;
    private readonly IVirtualTourViewModel _virtualTourVm;
    private bool _isMapInitialized;
    private Window? _lifecycleWindow;
    private bool _isWindowLifecycleHooked;
    private DateTime _lastQueueTapTime = DateTime.MinValue;
    private CancellationTokenSource? _queueAudioSwitchCts;
    private bool _preserveNarrationOnNextDisappearing;
    private bool _isNavigatingToPoiDetail;
    private DateTime _lastPoiDetailNavigationAt = DateTime.MinValue;
    private bool _isOpeningExploreMapPage;
    private bool _isNearSuggestionPopupOpen;
    private bool _isHandlingQrDeepLink;
    private MainViewModel.ExplorePoiCard? _pendingNearSuggestionCard;
    private StreetFoodNarrator.App.Core.Models.POI? _pendingNearSuggestionPoi;
    private ExploreMapPage? _cachedExploreMapPage;
    private ExploreMapPage? _cachedNearFocusExploreMapPage;
    private MainViewModel.ExploreState _lastObservedExploreState = MainViewModel.ExploreState.Far;
    private bool _hasObservedExploreState;
    private bool _isStateTransitionPromptOpen;
    private bool _dismissNearTransitionSuggestion;
    private bool _dismissFarTransitionSuggestion;
    private bool _autoOpenInZoneDirectly;
    private bool _isInitialLoadCompleted;
    private bool _hasShownOfflineCapabilityNoticeThisSession;
    private bool _isMapPrewarmQueued;
    private bool _isLiveSyncTickInProgress;
    private IDispatcherTimer? _exploreAudioUiTimer;
    private IDispatcherTimer? _liveSyncTimer;
    private static readonly TimeSpan LiveSyncNearInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan LiveSyncFarInterval = TimeSpan.FromSeconds(60);

    private static bool IsAutoExploreMapNavigationEnabled()
        => false;

    private string Ui(string vi, string en, string? zh = null)
        => _lang.CurrentLanguage switch
        {
            "en" => en,
            "zh" => zh ?? en,
            _ => vi
        };

    private static AlertType ResolveAlertTypeFromTitle(string title)
    {
        var normalized = (title ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized.Contains("lỗi") || normalized.Contains("error") || normalized.Contains("không thể"))
            return AlertType.Error;
        if (normalized.Contains("cảnh báo") || normalized.Contains("warning"))
            return AlertType.Warning;
        return AlertType.Info;
    }

    private new Task DisplayAlertAsync(string title, string message, string cancel)
        => CustomAlert.ShowAsync(title, message, cancel, ResolveAlertTypeFromTitle(title));

    public MainPage()
        : this(null, null, null, null, null)
    {
    }

    public MainPage(MainViewModel? viewModel = null, ITTSService? ttsService = null,
        LanguageService? languageService = null, IAudioCacheService? audioCache = null,
        IOfflineRoutingService? offlineRouting = null)
    {
        try
        {
            InitializeComponent();
        }
        catch (Exception ex)
        {
            // AOT/trimming crash guard: if InitializeComponent fails in release, log and rethrow
            LogCrash("MainPage.InitializeComponent", ex);
            throw;
        }

        try
        {
            // Resolve MainViewModel with fallback - avoid direct call in constructor parameter
            if (viewModel == null)
            {
                try
                {
                    viewModel = ResolveRequiredService<MainViewModel>();
                }
                catch (Exception ex)
                {
                    LogCrash("MainPage MainViewModel resolution", ex);
                    System.Diagnostics.Debug.WriteLine($"[MainPage] Failed to resolve MainViewModel: {ex.Message}");
                    throw new InvalidOperationException("Unable to initialize MainViewModel. Service provider may not be ready.", ex);
                }
            }

            BindingContext = _vm = viewModel;
            _lastObservedExploreState = _vm.CurrentExploreState;
            _hasObservedExploreState = true;

            // Resolve ITTSService
            if (ttsService == null)
            {
                try
                {
                    ttsService = ResolveRequiredService<ITTSService>();
                }
                catch (Exception ex)
                {
                    LogCrash("MainPage ITTSService resolution", ex);
                    System.Diagnostics.Debug.WriteLine($"[MainPage] Failed to resolve ITTSService: {ex.Message}");
                    throw new InvalidOperationException("Unable to initialize ITTSService.", ex);
                }
            }
            _tts = ttsService;

            // Resolve LanguageService
            if (languageService == null)
            {
                try
                {
                    languageService = ResolveRequiredService<LanguageService>();
                }
                catch (Exception ex)
                {
                    LogCrash("MainPage LanguageService resolution", ex);
                    System.Diagnostics.Debug.WriteLine($"[MainPage] Failed to resolve LanguageService: {ex.Message}");
                    throw new InvalidOperationException("Unable to initialize LanguageService.", ex);
                }
            }
            _lang = languageService;

            // Resolve IAudioCacheService (optional)
            _audioCache = audioCache ?? ResolveOptionalService<IAudioCacheService>();
            _httpClient = ResolveOptionalService<HttpClient>();
            _offlineRouting = offlineRouting ?? ResolveOptionalService<IOfflineRoutingService>();

            // Resolve IVirtualTourViewModel
            try
            {
                _virtualTourVm = ResolveRequiredService<IVirtualTourViewModel>();
            }
            catch (Exception ex)
            {
                LogCrash("MainPage IVirtualTourViewModel resolution", ex);
                System.Diagnostics.Debug.WriteLine($"[MainPage] Failed to resolve IVirtualTourViewModel: {ex.Message}");
                throw new InvalidOperationException("Unable to initialize IVirtualTourViewModel.", ex);
            }

            _virtualTourVm.ConfigureContext(
                distanceProviderMeters: GetCurrentDistanceToTourAreaMeters,
                switchToRealModeAsync: SwitchToRealModeAsync,
                switchToExploreFar: SwitchToExploreFar);

            _vm.PropertyChanged += OnViewModelPropertyChanged;
            _vm.SwitchToRealModeRequested += OnVmSwitchToRealModeRequested;
            LanguageService.LanguageChanged += OnLanguageChanged;
            QrDeepLinkManager.PendingDeepLinkChanged += OnQrDeepLinkPendingChanged;
            Loaded += OnPageLoaded;
            Unloaded += OnPageUnloaded;
            ApplyStaticLocalizedTexts();
        }
        catch (Exception ex)
        {
            LogCrash("MainPage full constructor", ex);
            throw;
        }
    }

    private static void LogCrash(string location, Exception ex)
    {
        try
        {
            var logPath = Path.Combine(FileSystem.AppDataDirectory, "crash_log.txt");
            var crashLog = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] CRASH [{location}]: {ex.Message}\n{ex.StackTrace}\n\n";
            File.AppendAllText(logPath, crashLog);
            System.Diagnostics.Debug.WriteLine($"[CRASH LOGGED] [{location}] {ex.Message}");
        }
        catch { /* safe */ }
    }

    private void OnPageUnloaded(object? sender, EventArgs e)
    {
        _vm.PropertyChanged -= OnViewModelPropertyChanged;
        _vm.SwitchToRealModeRequested -= OnVmSwitchToRealModeRequested;
        LanguageService.LanguageChanged -= OnLanguageChanged;
        QrDeepLinkManager.PendingDeepLinkChanged -= OnQrDeepLinkPendingChanged;
        Loaded -= OnPageLoaded;
        Unloaded -= OnPageUnloaded;
    }

    private void OnLanguageChanged(object? sender, string languageCode)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            ApplyStaticLocalizedTexts();
            _vm.RefreshLanguageDependentUi();
            SyncExplorePresentationState();
        });
    }

    private void ApplyStaticLocalizedTexts()
    {
        MainHeaderTitleLabel.Text = Ui("PHỐ ẨM THỰC VĨNH KHÁNH", "VINH KHANH STREET FOOD", "永庆美食街");
        MainHeaderLanguageButton.Text = LanguageSwitcher.GetHeaderLabel(_lang.CurrentLanguage);
        FarHeadlineLine1Label.Text = AppStrings.Get("Main_Far_Headline1");
        FarHeadlineLine2Label.Text = AppStrings.Get("Main_Far_Headline2");
        FarDescriptionLabel.Text = AppStrings.Get("Main_Far_Description");
        NearMapOnlyButton.Text = AppStrings.Get("Main_Action_ViewMap");

        InZoneTitleLabel.Text = AppStrings.Get("Main_InZone_Title");
        InZoneAreaLabel.Text = AppStrings.Get("Main_InZone_Area");
        InZoneDescriptionLabel.Text = AppStrings.Get("Main_InZone_Description");
        InZoneNowPlayingTagLabel.Text = AppStrings.Get("Main_NowPlaying_Tag");
        InZoneContinueButton.Text = AppStrings.Get("Main_Action_ContinueExplore");
        InZoneSeeLocationButton.Text = AppStrings.Get("Main_Action_ViewYourLocation");

        InZoneDashboardTitleLabel.Text = AppStrings.Get("Main_InZone_Title");
        InZoneDashboardNarratingTagLabel.Text = AppStrings.Get("Main_Narrating_Tag");
        DirectionActionButton.Text = AppStrings.Get("Main_Action_Directions");
        ShuffleActionButton.Text = AppStrings.Get("Main_Action_SwitchSpot");
        MapActionButton.Text = AppStrings.Get("Main_Action_ViewMap_Multiline");
        NearYouTitleLabel.Text = AppStrings.Get("Main_NearYou_Title");
        NearYouSeeAllLabel.Text = AppStrings.Get("Main_NearYou_SeeAll");

        NearPopupExploreNowButton.Text = AppStrings.Get("Main_Action_ExploreNow");
        NearPopupCancelButton.Text = AppStrings.Get("Common_Cancel");

        MapToolTitleLabel.Text = AppStrings.Get("Main_MapTool_Title");
        MapToolSubtitleLabel.Text = AppStrings.Get("Main_MapTool_Subtitle");
        MapToolDescriptionLabel.Text = AppStrings.Get("Main_MapTool_Description");
        MapToolCenterButton.Text = AppStrings.Get("Main_Action_CenterMap");
        MapToolCloseButton.Text = AppStrings.Get("Main_Action_CloseMap");
        OfflineBannerTextLabel.Text = AppStrings.Get("Offline_Banner_Full");
        LoadingTitleLabel.Text = Ui("Phố Ẩm Thực Vĩnh Khánh", "Vinh Khanh Street Food", "永庆美食街");
        if (LoadingStatusLabel != null && string.IsNullOrWhiteSpace(LoadingStatusLabel.Text))
            LoadingStatusLabel.Text = AppStrings.Get("Main_Status_LoadingData");
    }

    private void OnQrDeepLinkPendingChanged(object? sender, EventArgs e)
    {
        if (!IsVisible)
            return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            _ = HandlePendingQrDeepLinkAsync();
        });
    }

    private async void OnVmSwitchToRealModeRequested(object? sender, EventArgs e)
    {
        await SwitchToRealModeAsync();
    }

    private void OnMenuButtonClicked(object? sender, EventArgs e)
        => OnSettingsClicked(sender, e);

    private async void OnHeaderLanguageClicked(object? sender, EventArgs e)
    {
        await LanguageSwitcher.ShowLanguagePickerAsync(this, _lang);
    }

    private async void OnProfileButtonClicked(object? sender, EventArgs e)
    {
        if (Shell.Current != null)
        {
            await Shell.Current.GoToAsync("//SettingsPage", false);
        }
    }

    private async void OnWelcomeButtonClicked(object? sender, EventArgs e)
    {
        // Button is hidden. Does nothing.
    }

    private async void OnLogoutButtonClicked(object? sender, EventArgs e)
    {
        try
        {
            bool confirmed = await CustomAlert.ShowConfirmAsync(
                AppStrings.Get("Main_Logout_Title"),
                AppStrings.Get("Main_Logout_Confirm"),
                AppStrings.Get("Main_Logout_Title"),
                AppStrings.Get("Common_Cancel"),
                AlertType.Warning);

            if (!confirmed)
                return;

            // Clear navigation/session preferences so app returns to onboarding shell.
            Preferences.Remove(HasOnboardedPreferenceKey);
            Preferences.Remove("hasSeenOnboarding"); // legacy key
            Preferences.Remove(AutoOpenInZoneOnNextMainPageKey);

            // Exit app immediately after confirmed logout.
            QuitApplication();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainPage] OnLogoutButtonClicked error: {ex}");
        }
        finally
        {
            RemoveDanglingAlertOverlayIfAny();
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
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainPage] QuitApplication fallback: {ex.Message}");
            Environment.Exit(0);
        }
    }

    private async void OnPrimaryExploreActionClicked(object? sender, EventArgs e)
    {
        try
        {
            if (_vm.CurrentExploreState == MainViewModel.ExploreState.InZone)
            {
                _preserveNarrationOnNextDisappearing = true;
            }

            switch (_vm.CurrentExploreState)
            {
                case MainViewModel.ExploreState.Far:
                    await StartVirtualPreviewAsync();
                    break;
                case MainViewModel.ExploreState.Near:
                    await HandleNearPrimaryActionAsync();
                    break;
                case MainViewModel.ExploreState.InZone:
                    await StartRealGuidanceFromExploreAsync();
                    break;
                default:
                    await StartRealGuidanceFromExploreAsync();
                    break;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainPage] OnPrimaryExploreActionClicked error: {ex}");
            await DisplayAlertAsync(
                Ui("Lỗi", "Error", "错误"),
                Ui(
                    "Không thể mở chế độ này lúc này. Vui lòng thử lại.",
                    "Cannot open this mode right now. Please try again.",
                    "当前无法打开该模式。请重试。"),
                "OK");
        }
    }

    private async Task HandleNearPrimaryActionAsync()
    {
        var startFromNearTour = _vm.NearPrimaryIsTour;
        var handled = _vm.ActivateNearPrimaryChoice();
        if (!handled)
        {
            await DisplayAlertAsync(
                Ui("Không thể bắt đầu", "Cannot start", "无法开始"),
                Ui(
                    "Chưa thể kích hoạt hành trình này lúc này. Vui lòng thử lại.",
                    "This route cannot be activated right now. Please try again.",
                    "当前无法激活该路线。请重试。"),
                "OK");
            return;
        }

        // Near primary is a tour suggestion: start real guidance so target is resolved from the active tour pool.
        if (startFromNearTour)
        {
            await StartRealGuidanceFromExploreAsync();
            return;
        }

        await SetMapToolVisibleAsync(false);
        await OpenExploreMapForFeaturedPoiAsync(triggerFeaturedPoi: true);
    }

    private async void OnSecondaryExploreActionClicked(object? sender, EventArgs e)
    {
        try
        {
            if (_vm.CurrentExploreState == MainViewModel.ExploreState.Far && _vm.HasActiveTourOverride)
            {
                var shouldClearTour = await DisplayAlertAsync(
                    Ui("Đang dùng tour đã chọn", "Using selected tour", "正在使用已选行程"),
                    Ui(
                        "Bạn có muốn bỏ tour đã chọn để quay về tuyến tự động không?",
                        "Do you want to clear the selected tour and return to automatic routing?",
                        "是否清除已选行程并返回自动路线？"),
                    Ui("Bỏ tour", "Clear tour", "清除行程"),
                    Ui("Giữ tour", "Keep tour", "保留行程"));
                if (!shouldClearTour)
                    return;

                _vm.ClearTourOverride();
            }

            if (_vm.CurrentExploreState == MainViewModel.ExploreState.InZone)
            {
                _preserveNarrationOnNextDisappearing = true;
            }

            await SetMapToolVisibleAsync(false);
            await OpenExploreMapForFeaturedPoiAsync(triggerFeaturedPoi: true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainPage] OnSecondaryExploreActionClicked error: {ex}");
            await DisplayAlertAsync(
                Ui("Lỗi", "Error", "错误"),
                Ui(
                    "Không thể mở bản đồ lúc này. Vui lòng thử lại.",
                    "Cannot open map right now. Please try again.",
                    "当前无法打开地图。请重试。"),
                "OK");
        }
    }

    private async void OnDirectionClicked(object? sender, EventArgs e)
    {
        try
        {
            await StartRealGuidanceFromExploreAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainPage] OnDirectionClicked error: {ex}");
            await DisplayAlertAsync(
                Ui("Lỗi", "Error", "错误"),
                Ui(
                    "Không thể mở bản đồ lúc này. Vui lòng thử lại.",
                    "Cannot open map right now. Please try again.",
                    "当前无法打开地图。请重试。"),
                "OK");
        }
    }
    private void OnInZoneMiniPlayPauseClicked(object? sender, EventArgs e)
    {
        try
        {
            EnsurePrimaryZoneFromCurrentAudioContext();
            OnPlayPauseTapped(sender, e);
            UpdateInZoneMiniAudioUi();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainPage] OnInZoneMiniPlayPauseClicked error: {ex}");
        }
    }
    private void OnShuffleClicked(object? sender, EventArgs e)
    {
        var cards = new List<MainViewModel.ExplorePoiCard>();
        if (_vm.FeaturedExplorePoi != null)
            cards.Add(_vm.FeaturedExplorePoi);
        cards.AddRange(_vm.NearbyExplorePois);

        if (cards.Count < 1)
            return;

        var rotated = cards.Count == 1
            ? new List<MainViewModel.ExplorePoiCard> { cards[0] }
            : cards.Skip(1).Concat(cards.Take(1)).ToList();
        _vm.FeaturedExplorePoi = rotated[0];
        _vm.ExploreHeroImage = rotated[0].ImageUrl;
        _vm.ExploreAudioQuote = rotated[0].QuoteText;

        // FIX: Defer collection modification to avoid "Cannot change ObservableCollection
        // during a CollectionChanged event"
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _vm.NearbyExplorePois.Clear();
            foreach (var item in rotated.Skip(1).Take(2))
            {
                _vm.NearbyExplorePois.Add(item);
            }
        });
    }

    private async void OnFeaturedSaveClicked(object? sender, EventArgs e)
    {
        var poi = ResolveFeaturedPoi();
        if (poi == null)
            return;

        await _vm.ToggleSavePOICommand.ExecuteAsync(poi);
    }

    private async void OnNearSuggestionCardTapped(object? sender, TappedEventArgs e)
    {
        if (_isNearSuggestionPopupOpen || _isOpeningExploreMapPage)
            return;

        if ((sender as BindableObject)?.BindingContext is not MainViewModel.ExplorePoiCard card)
            return;

        var poi = _vm.AllPOIs.FirstOrDefault(p => p.ZoneType == "Spot" && p.Id == card.Id);
        try
        {
            await ShowNearSuggestionPopupAsync(card, poi);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainPage] OnNearSuggestionCardTapped error: {ex}");
        }
    }

    private async void OnNearMapOnlyClicked(object? sender, EventArgs e)
    {
        try
        {
            await SetMapToolVisibleAsync(false);
            await OpenExploreMapForFeaturedPoiAsync(triggerFeaturedPoi: true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainPage] OnNearMapOnlyClicked error: {ex}");
            await DisplayAlertAsync(
                Ui("Lỗi", "Error", "错误"),
                Ui(
                    "Không thể mở bản đồ lúc này. Vui lòng thử lại.",
                    "Cannot open map right now. Please try again.",
                    "当前无法打开地图。请重试。"),
                "OK");
        }
    }

    private void PromoteSuggestionAsFeatured(MainViewModel.ExplorePoiCard selectedCard)
    {
        var previousFeatured = _vm.FeaturedExplorePoi;
        _vm.FeaturedExplorePoi = selectedCard;
        _vm.ExploreHeroImage = selectedCard.ImageUrl;
        _vm.ExploreAudioQuote = selectedCard.QuoteText;

        var reorderedSuggestions = new List<MainViewModel.ExplorePoiCard>();
        if (previousFeatured != null && previousFeatured.Id != selectedCard.Id)
        {
            reorderedSuggestions.Add(previousFeatured);
        }

        foreach (var item in _vm.NearbyExplorePois)
        {
            if (item.Id == selectedCard.Id || reorderedSuggestions.Any(x => x.Id == item.Id))
                continue;

            reorderedSuggestions.Add(item);
        }

        _vm.NearbyExplorePois = new System.Collections.ObjectModel.ObservableCollection<MainViewModel.ExplorePoiCard>(
            reorderedSuggestions.Take(2));
    }

    private async Task ShowNearSuggestionPopupAsync(
        MainViewModel.ExplorePoiCard card,
        StreetFoodNarrator.App.Core.Models.POI? poi)
    {
        _pendingNearSuggestionCard = card;
        _pendingNearSuggestionPoi = poi;
        _isNearSuggestionPopupOpen = true;

        NearPopupImage.Source = card.ImageUrl;
        NearPopupTitleLabel.Text = card.Name;
        NearPopupRatingLabel.Text = $"★ {(poi?.Rating ?? card.Rating):F1}";

        var areaText = ResolvePopupAreaText(poi, card);
        NearPopupDistanceAreaLabel.Text = $"📍 {FormatPopupDistance(card.DistanceMeters)} • {areaText.ToUpperInvariant()}";

        var tagLine = ResolvePopupTagLine(poi, card).ToUpperInvariant();
        NearPopupTagLineLabel.Text = $"🔥 {tagLine}";

        NearPopupDescriptionLabel.Text = ResolvePopupDescription(poi, card);

        var popupTags = BuildPopupHashtags(poi, card);
        NearPopupChip1Label.Text = popupTags.ElementAtOrDefault(0) ?? "#MONNGON";
        NearPopupChip2Label.Text = popupTags.ElementAtOrDefault(1) ?? "#MONNGON";
        NearPopupChip3Label.Text = popupTags.ElementAtOrDefault(2) ?? "#KHAMPHA";

        NearSuggestionPopupOverlay.IsVisible = true;
        NearSuggestionPopupOverlay.InputTransparent = false;
        NearSuggestionPopupOverlay.Opacity = 0;
        await NearSuggestionPopupOverlay.FadeToAsync(1, 140);
    }

    private async Task HideNearSuggestionPopupAsync()
    {
        if (!NearSuggestionPopupOverlay.IsVisible)
        {
            _isNearSuggestionPopupOpen = false;
            _pendingNearSuggestionCard = null;
            _pendingNearSuggestionPoi = null;
            return;
        }

        await NearSuggestionPopupOverlay.FadeToAsync(0, 120);
        NearSuggestionPopupOverlay.IsVisible = false;
        _isNearSuggestionPopupOpen = false;
        _pendingNearSuggestionCard = null;
        _pendingNearSuggestionPoi = null;
    }

    private async void OnNearPopupCancelClicked(object? sender, EventArgs e)
    {
        try
        {
            await HideNearSuggestionPopupAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainPage] OnNearPopupCancelClicked error: {ex}");
        }
    }

    private async void OnNearPopupExploreNowClicked(object? sender, EventArgs e)
    {
        if (_pendingNearSuggestionCard == null || _isOpeningExploreMapPage)
            return;

        var selected = _pendingNearSuggestionCard;
        try
        {
            await HideNearSuggestionPopupAsync();
            PromoteSuggestionAsFeatured(selected);
            _vm.ActivateNearPoiFallbackMode();
            await SetMapToolVisibleAsync(false);
            await OpenExploreMapForFeaturedPoiAsync(triggerFeaturedPoi: true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainPage] OnNearPopupExploreNowClicked error: {ex}");
        }
    }

    private static string FormatPopupDistance(double meters)
    {
        if (meters <= 0) return "—";
        if (meters < 1000) return $"{meters:F0}M";
        return $"{meters / 1000:F1}KM";
    }

    private static string ResolvePopupAreaText(
        StreetFoodNarrator.App.Core.Models.POI? poi,
        MainViewModel.ExplorePoiCard card)
    {
        if (!string.IsNullOrWhiteSpace(card.AreaText))
            return card.AreaText;

        var address = poi?.Address;
        if (string.IsNullOrWhiteSpace(address))
            return "KHU ẨM THỰC";

        var parts = address.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 ? parts[1] : parts[0];
    }

    private static string ResolvePopupTagLine(
        StreetFoodNarrator.App.Core.Models.POI? poi,
        MainViewModel.ExplorePoiCard card)
    {
        if (!string.IsNullOrWhiteSpace(poi?.Category))
            return poi.Category!;
        if (!string.IsNullOrWhiteSpace(poi?.Type))
            return poi.Type;
        if (!string.IsNullOrWhiteSpace(card.TagText))
            return card.TagText;
        return "Món nên thử";
    }

    private static string ResolvePopupDescription(
        StreetFoodNarrator.App.Core.Models.POI? poi,
        MainViewModel.ExplorePoiCard card)
    {
        var raw = poi?.DisplayDescription ?? card.ShortDescription ?? card.QuoteText;
        if (string.IsNullOrWhiteSpace(raw))
            return "Khám phá hương vị địa phương đặc sắc ngay gần bạn.";

        var cleaned = raw.Replace("\"", "").Replace("\r", " ").Replace("\n", " ").Trim();
        return cleaned.Length > 140 ? $"{cleaned[..140].Trim()}..." : cleaned;
    }

    private static IReadOnlyList<string> BuildPopupHashtags(
        StreetFoodNarrator.App.Core.Models.POI? poi,
        MainViewModel.ExplorePoiCard card)
    {
        var rawTags = new List<string>();

        if (!string.IsNullOrWhiteSpace(poi?.Category))
            rawTags.Add(poi.Category!);
        if (!string.IsNullOrWhiteSpace(poi?.Type))
            rawTags.Add(poi.Type);
        if (!string.IsNullOrWhiteSpace(poi?.SignatureDish))
            rawTags.Add(poi.SignatureDish!);
        if (poi?.DisplaySignatureDishes != null)
            rawTags.AddRange(poi.DisplaySignatureDishes.Take(2));
        if (!string.IsNullOrWhiteSpace(card.TagText))
            rawTags.Add(card.TagText);

        var hashtags = new List<string>(capacity: 3);
        foreach (var raw in rawTags)
        {
            var hashtag = ToHashtag(raw);
            if (string.IsNullOrWhiteSpace(hashtag) || hashtags.Contains(hashtag))
                continue;

            hashtags.Add(hashtag);
            if (hashtags.Count == 3)
                break;
        }

        while (hashtags.Count < 3)
        {
            hashtags.Add(hashtags.Count switch
            {
                0 => "#MONNGON",
                1 => "#STREETFOOD",
                _ => "#KHAMPHA"
            });
        }

        return hashtags;
    }

    private static string ToHashtag(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
            return string.Empty;

        var normalized = new string(source
            .Trim()
            .Where(char.IsLetterOrDigit)
            .ToArray());

        if (string.IsNullOrWhiteSpace(normalized))
            return string.Empty;

        if (normalized.Length > 14)
            normalized = normalized[..14];

        return $"#{normalized.ToUpperInvariant()}";
    }

    private async void OnMapToolCloseClicked(object? sender, EventArgs e)
    {
        await SetMapToolVisibleAsync(false);
    }

    private async Task<bool> EnsureSpotDataReadyAsync()
    {
        if (_vm.AllPOIs.Any(p => p.ZoneType == "Spot"))
            return true;

        try
        {
            await _vm.LoadAllPoisAsync(forceSyncNow: false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainPage] EnsureSpotDataReadyAsync load error: {ex}");
        }

        return _vm.AllPOIs.Any(p => p.ZoneType == "Spot");
    }

    private async Task StartVirtualPreviewAsync()
    {
        await NotifyOfflineCapabilityNoticeOnceAsync();

        var hasSpotData = await EnsureSpotDataReadyAsync();
        if (!hasSpotData)
        {
            await DisplayAlertAsync("Đang tải dữ liệu", "Dữ liệu tour chưa sẵn sàng. Vui lòng thử lại sau vài giây.", "OK");
            return;
        }

        var poi = ResolveBestStartPoi();
        if (poi == null)
        {
            await DisplayAlertAsync("Chưa có điểm tour", "Không tìm thấy điểm bắt đầu phù hợp. Vui lòng thử lại.", "OK");
            return;
        }

        try
        {
            EnsureMapInitialized();
            StartContinuousTrackingIfNeeded();
            _vm.IsLegacyMapVisible = false;
            ApplyMapPresentation();
            await EnableVirtualTourAsync(poi);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainPage] StartVirtualPreviewAsync error: {ex}");
            ShowExploreStateMode();
            await DisplayAlertAsync("Không thể vào tour ảo", "Đã có lỗi khi mở tour ảo. Vui lòng thử lại.", "OK");
        }
    }

    private async Task HandlePendingQrDeepLinkAsync()
    {
        if (_isHandlingQrDeepLink)
            return;

        _isHandlingQrDeepLink = true;
        try
        {
            var rawUrl = QrDeepLinkManager.ConsumePending();
            if (string.IsNullOrWhiteSpace(rawUrl))
                return;

            if (!QrDeepLinkManager.TryParse(rawUrl, out var payload, out var parseError))
            {
                await DisplayAlertAsync(
                    Ui("QR không hợp lệ", "Invalid QR", "二维码无效"),
                    parseError,
                    "OK");
                return;
            }

            if (payload.IsExpired(DateTimeOffset.UtcNow))
            {
                await DisplayAlertAsync(
                    Ui("QR đã hết hạn", "QR expired", "二维码已过期"),
                    Ui(
                        $"Mã QR này đã quá hạn {Constants.QR_CODE_EXPIRY_DAYS} ngày. Vui lòng dùng mã mới tại điểm dừng xe buýt.",
                        $"This QR is older than {Constants.QR_CODE_EXPIRY_DAYS} days. Please use a newly generated code at the bus stop.",
                        $"此二维码已超过 {Constants.QR_CODE_EXPIRY_DAYS} 天。请在公交站使用新生成的二维码。"),
                    "OK");
                return;
            }

            if (payload.OpenMainPageOnly)
            {
                _vm.CurrentAppMode = MainViewModel.AppMode.Explore;
                _vm.IsLegacyMapVisible = false;
                SyncExplorePresentationState();
                ApplyMapPresentation();
                return;
            }

            await EnsureSpotDataReadyAsync();

            StreetFoodNarrator.App.Core.Models.POI? targetPoi = null;

            if (payload.PoiId.HasValue)
            {
                targetPoi = _vm.AllPOIs.FirstOrDefault(p => p.Id == payload.PoiId.Value)
                    ?? _vm.FilteredPOIs.FirstOrDefault(p => p.Id == payload.PoiId.Value);
            }
            else if (!string.IsNullOrWhiteSpace(payload.TourId))
            {
                if (_vm.AllTours.Count == 0)
                    await _vm.LoadToursAsync(forceSyncNow: false);

                var tour = _vm.AllTours.FirstOrDefault(t =>
                    string.Equals(t.Id, payload.TourId, StringComparison.OrdinalIgnoreCase));

                if (tour == null)
                {
                    await DisplayAlertAsync(
                        Ui("Không tìm thấy tour", "Tour not found", "未找到行程"),
                        Ui(
                            "Tour trong mã QR chưa có dữ liệu trên máy. Vui lòng đồng bộ rồi thử lại.",
                            "The tour in this QR is not available on this device yet. Please sync and try again.",
                            "此二维码中的行程尚未同步到设备。请同步后重试。"),
                        "OK");
                    return;
                }

                var orderedStops = ResolveTourStopsForQr(tour);
                if (orderedStops.Count == 0)
                {
                    await DisplayAlertAsync(
                        Ui("Tour chưa sẵn sàng", "Tour not ready", "行程尚未就绪"),
                        Ui(
                            "Tour trong mã QR chưa có điểm dừng hợp lệ để mở bản đồ.",
                            "The tour in this QR does not have valid stops to open on map.",
                            "此二维码中的行程没有可用于打开地图的有效站点。"),
                        "OK");
                    return;
                }

                _vm.ActivateTourOverride(tour, orderedStops);
                _vm.RequestedTourStops = orderedStops;
                _vm.AutoStartRequestedTour = false;
                _vm.AutoOpenRequestedTourOnMap = false;

                targetPoi = orderedStops[0];
            }

            if (payload.PoiId.HasValue && targetPoi == null)
            {
                await DisplayAlertAsync(
                    Ui("Không tìm thấy điểm", "Stop not found", "未找到站点"),
                    Ui(
                        "Điểm đến trong QR chưa có dữ liệu trên máy. Vui lòng đồng bộ rồi thử lại.",
                        "The destination in this QR is not available on this device yet. Please sync and try again.",
                        "此二维码中的目的地尚未同步到设备。请同步后重试。"),
                    "OK");
                return;
            }

            if (targetPoi == null)
            {
                await DisplayAlertAsync(
                    Ui("QR chưa hỗ trợ", "QR not supported", "二维码暂不支持"),
                    Ui(
                        "Mã QR này chưa có điểm dừng hợp lệ để mở bản đồ.",
                        "This QR does not contain a valid stop to open on map.",
                        "此二维码不包含可用于打开地图的有效站点。"),
                    "OK");
                return;
            }

            _vm.CurrentAppMode = MainViewModel.AppMode.Explore;
            _vm.IsLegacyMapVisible = false;
            _vm.NavigationTarget = targetPoi;
            _vm.SelectedPinPOI = targetPoi;
            _vm.PrimaryZone = targetPoi;
            _vm.PrimaryZoneName = targetPoi.GetName(_lang.CurrentLanguage);
            _vm.PrimaryZoneType = targetPoi.ZoneType ?? targetPoi.Category ?? string.Empty;
            _vm.PrimaryZoneDesc = targetPoi.GetDescription(_lang.CurrentLanguage) ?? string.Empty;
            _vm.PrimaryZoneAddress = targetPoi.Address ?? Ui("Đang cập nhật", "Updating", "更新中");
            _vm.PrimaryZoneRating = (targetPoi.Rating ?? 4.5).ToString("F1");
            _vm.VisitedPOIIds.Add(targetPoi.Id);

            if (payload.PoiId.HasValue)
                await StartQrPoiPlaybackAsync(targetPoi);

            SyncExplorePresentationState();
            ApplyMapPresentation();
            await OpenExploreMapPageAsync(nearFocusMode: false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainPage] HandlePendingQrDeepLinkAsync error: {ex}");
        }
        finally
        {
            _isHandlingQrDeepLink = false;
        }
    }

    private async Task StartQrPoiPlaybackAsync(StreetFoodNarrator.App.Core.Models.POI targetPoi)
    {
        try
        {
            _vm.SetJournalCurrentlyPlayingFromPoi(targetPoi);
            _queueAudioSwitchCts?.Cancel();
            var cts = new CancellationTokenSource();
            _queueAudioSwitchCts = cts;
            await SwitchAudioForQueueItemAsync(cts.Token);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainPage] StartQrPoiPlaybackAsync error: {ex}");
        }
    }

    private List<StreetFoodNarrator.App.Core.Models.POI> ResolveTourStopsForQr(MainViewModel.TourListItem tour)
    {
        var activeSpots = _vm.AllPOIs
            .Where(p => p.ZoneType == "Spot" && p.IsActive)
            .ToList();

        if (activeSpots.Count == 0)
            return new List<StreetFoodNarrator.App.Core.Models.POI>();

        var byId = activeSpots.ToDictionary(p => p.Id);
        var requestedById = new List<StreetFoodNarrator.App.Core.Models.POI>();
        var seen = new HashSet<int>();

        foreach (var poiId in tour.PoiIds)
        {
            if (byId.TryGetValue(poiId, out var poi) && seen.Add(poi.Id))
                requestedById.Add(poi);
        }

        if (requestedById.Count > 0)
            return OrderByCurrentLocation(requestedById);

        var requestedByName = new List<StreetFoodNarrator.App.Core.Models.POI>();
        foreach (var poiName in tour.PoiNames)
        {
            if (string.IsNullOrWhiteSpace(poiName))
                continue;

            var normalized = poiName.Trim().ToLowerInvariant();
            var matchedPoi = activeSpots.FirstOrDefault(p =>
                string.Equals((p.Name_Vi ?? string.Empty).Trim(), poiName.Trim(), StringComparison.OrdinalIgnoreCase) ||
                string.Equals((p.Name_En ?? string.Empty).Trim(), poiName.Trim(), StringComparison.OrdinalIgnoreCase) ||
                string.Equals((p.Name_Zh ?? string.Empty).Trim(), poiName.Trim(), StringComparison.OrdinalIgnoreCase) ||
                (p.Name_Vi ?? string.Empty).Trim().ToLowerInvariant() == normalized ||
                (p.Name_En ?? string.Empty).Trim().ToLowerInvariant() == normalized ||
                (p.Name_Zh ?? string.Empty).Trim().ToLowerInvariant() == normalized);

            if (matchedPoi != null && seen.Add(matchedPoi.Id))
                requestedByName.Add(matchedPoi);
        }

        if (requestedByName.Count > 0)
            return OrderByCurrentLocation(requestedByName);

        return OrderByCurrentLocation(activeSpots).Take(Math.Max(1, tour.PoiCount)).ToList();
    }

    private List<StreetFoodNarrator.App.Core.Models.POI> OrderByCurrentLocation(IEnumerable<StreetFoodNarrator.App.Core.Models.POI> candidates)
    {
        var list = candidates.ToList();
        if (!_vm.HasLocationFix)
            return list;

        return list
            .OrderBy(p => Math.Abs(p.Latitude - _vm.CurrentLat) + Math.Abs(p.Longitude - _vm.CurrentLon))
            .ToList();
    }

    private void ShowLegacyMapMode()
    {
        CleanupVirtualTourState();
        _vm.CurrentAppMode = MainViewModel.AppMode.Explore;
        EnsureMapInitialized();
        StartContinuousTrackingIfNeeded();
        _vm.IsLegacyMapVisible = false;
        _vm.RefreshExploreState();
        TabMapComponent?.InitializeChips(_vm.SelectedCategory);
        SyncExplorePresentationState();
        ApplyMapPresentation();
        ScheduleInteractiveMapRefresh();
        _ = SetMapToolVisibleAsync(false);
    }

    private void ShowExploreStateMode()
    {
        _vm.CurrentAppMode = MainViewModel.AppMode.Explore;
        _vm.IsLegacyMapVisible = false;
        if (_vm.CurrentExploreState == MainViewModel.ExploreState.InZone)
            _vm.ViewState = MainViewModel.MapViewState.InZoneMinimized;
        SyncExplorePresentationState();
        ApplyMapPresentation();
    }

    private async Task HandlePendingRequestedTourAsync()
    {
        if (!_vm.AutoOpenRequestedTourOnMap)
            return;

        var hasRequestedStops = _vm.RequestedTourStops != null && _vm.RequestedTourStops.Count > 0;
        _vm.AutoOpenRequestedTourOnMap = false;
        if (!hasRequestedStops)
            return;

        if (_vm.CurrentExploreState == MainViewModel.ExploreState.Far)
            return;

        if (_vm.CurrentExploreState == MainViewModel.ExploreState.InZone)
            return;

        await StartRealGuidanceFromExploreAsync();
    }

    private async Task OpenExploreMapPageAsync(bool nearFocusMode = false)
    {
        if (_isOpeningExploreMapPage)
            return;

        var mapOpenStartedUtc = DateTime.UtcNow;
        _isOpeningExploreMapPage = true;
        ShowMapOpeningOverlay();

        try
        {
            await NotifyOfflineCapabilityNoticeOnceAsync();

            if (!_vm.AllPOIs.Any(p => p.ZoneType == "Spot"))
            {
                // Do not block navigation; warm up data in the background.
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _vm.LoadAllPoisAsync(forceSyncNow: false);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[MainPage] Background LoadAllPoisAsync error: {ex}");
                    }
                });
            }

            // Keep MainPage in Explore-state mode, then open the dedicated map page.
            _vm.CurrentAppMode = MainViewModel.AppMode.Explore;
            _vm.IsLegacyMapVisible = false;
            SyncExplorePresentationState();
            ApplyMapPresentation();

            var remainingLoading = TimeSpan.FromMilliseconds(MapOpenMinimumLoadingMs) - (DateTime.UtcNow - mapOpenStartedUtc);
            if (remainingLoading > TimeSpan.Zero)
                await Task.Delay(remainingLoading);

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                var nav = await ResolveNavigationReadyAsync();
                if (nav == null)
                {
                    await DisplayAlertAsync("Không thể mở bản đồ", "Ngữ cảnh điều hướng chưa sẵn sàng. Vui lòng thử lại.", "OK");
                    return;
                }

                if (nav.NavigationStack.Count > 0 &&
                    nav.NavigationStack[nav.NavigationStack.Count - 1] is ExploreMapPage topMapPage &&
                    topMapPage.IsNearFocusMode == nearFocusMode)
                {
                    return;
                }

                var exploreMapPage = GetOrCreateExploreMapPage(nearFocusMode);
                if (exploreMapPage.Parent != null)
                {
                    exploreMapPage = new ExploreMapPage(_vm, nearFocusMode);
                    SetCachedExploreMapPage(nearFocusMode, exploreMapPage);
                }

                await PushExploreMapPageWithRetryAsync(nav, exploreMapPage);
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainPage] OpenExploreMapPageAsync error: {ex}");
            await DisplayAlertAsync("Không thể mở bản đồ", $"Đã có lỗi khi mở bản đồ: {ex.Message}", "OK");
        }
        finally
        {
            _isOpeningExploreMapPage = false;
            HideMapOpeningOverlay();
        }
    }

    private static bool HasInternetAccessNow()
        => Connectivity.Current.NetworkAccess == NetworkAccess.Internet ||
           Connectivity.Current.NetworkAccess == NetworkAccess.ConstrainedInternet;

    private async Task NotifyOfflineCapabilityNoticeOnceAsync()
    {
        if (_hasShownOfflineCapabilityNoticeThisSession)
            return;

        var hasInternet = HasInternetAccessNow();
        var hasFullOfflinePackage = Preferences.Get("has_full_offline", false);
        if (hasInternet || hasFullOfflinePackage)
            return;

        _hasShownOfflineCapabilityNoticeThisSession = true;
        await CustomAlert.ShowAsync(
            AppStrings.Get("Main_OfflineBasic_Title"),
            AppStrings.Get("Main_OfflineBasic_Message"),
            AppStrings.Get("Main_OfflineBasic_Ack"),
            AlertType.Info);
    }

    private void ShowMapOpeningOverlay()
    {
        if (!_isInitialLoadCompleted)
            return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            SetLoadingStatus(AppStrings.Get("Main_Status_OpeningMap"));
            LoadingOverlay.IsVisible = true;
            LoadingOverlay.Opacity = 1;
        });
    }

    private void HideMapOpeningOverlay()
    {
        if (!_isInitialLoadCompleted)
            return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            LoadingOverlay.IsVisible = false;
            LoadingOverlay.Opacity = 0;
        });
    }

    private void SyncExplorePresentationState()
    {
        if (StateContentLayer != null)
            StateContentLayer.IsVisible = _vm.IsExploreStateScreenVisible;

        if (FarStateHeroLayer != null)
            FarStateHeroLayer.IsVisible = _vm.IsFarHeroVisible;
    }

    private void ScheduleInteractiveMapRefresh()
    {
        if (!_isMapInitialized || MapView == null)
            return;

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                await Task.Delay(90);

                if (MapView == null || !MapView.IsVisible)
                    return;

                UpdateZonePins();
                UpdateUserPin();
                MapView.RefreshGraphics();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainPage] ScheduleInteractiveMapRefresh error: {ex.Message}");
            }
        });
    }

    private ExploreMapPage GetOrCreateExploreMapPage(bool nearFocusMode)
    {
        if (nearFocusMode)
        {
            _cachedNearFocusExploreMapPage ??= new ExploreMapPage(_vm, true);
            return _cachedNearFocusExploreMapPage;
        }

        _cachedExploreMapPage ??= new ExploreMapPage(_vm, false);
        return _cachedExploreMapPage;
    }

    private void SetCachedExploreMapPage(bool nearFocusMode, ExploreMapPage page)
    {
        if (nearFocusMode)
        {
            _cachedNearFocusExploreMapPage = page;
            return;
        }

        _cachedExploreMapPage = page;
    }


    private async void OpenMapToolForFeaturedPoi()
    {
        var poi = ResolveFeaturedPoi();
        if (poi != null)
        {
            _vm.NavigationTarget = poi;
            _vm.IsVirtualNavigation = false;
            _ = DrawNavigationRouteAsync();
        }

        await SetMapToolVisibleAsync(true);
    }

    private StreetFoodNarrator.App.Core.Models.POI? ResolveBestStartPoi()
    {
        var spots = _vm.GetRuntimeSpotPool().ToList();
        if (spots.Count == 0)
            return null;

        if (_vm.HasLocationFix)
        {
            return spots
                .OrderBy(p => Math.Abs(p.Latitude - _vm.CurrentLat) + Math.Abs(p.Longitude - _vm.CurrentLon))
                .FirstOrDefault();
        }

        return ResolveFeaturedPoi() ?? spots.FirstOrDefault();
    }

    private StreetFoodNarrator.App.Core.Models.POI? ResolveFeaturedPoi()
    {
        var featuredId = _vm.FeaturedExplorePoi?.Id;
        if (featuredId == null)
            return null;

        return _vm.AllPOIs.FirstOrDefault(p => p.Id == featuredId.Value)
            ?? _vm.PrimaryZone;
    }

    private async Task SetMapToolVisibleAsync(bool isVisible)
    {
        if (MapToolOverlay == null || MapView == null)
            return;

        if (isVisible)
        {
            MapToolOverlay.IsVisible = true;
            MapView.InputTransparent = false;
            MapView.Opacity = 0.92;
            await MapToolOverlay.FadeToAsync(1, 180);
            return;
        }

        await MapToolOverlay.FadeToAsync(0, 160);
        MapToolOverlay.IsVisible = false;
        ApplyMapPresentation();
    }

    private void ApplyMapPresentation()
    {
        if (MapView == null)
            return;

        var shouldShowInteractiveMap =
            _vm.IsMapModeVisible ||
            _vm.IsRealMode ||
            MapToolOverlay?.IsVisible == true;

        // Explore map mode now lives in ExploreMapPage, so MainPage's map should
        // only exist while MainPage is actively in Real/Virtual/map-tool flows.
        MapView.IsVisible = shouldShowInteractiveMap;
        MapView.InputTransparent = !shouldShowInteractiveMap;
        MapView.Opacity = shouldShowInteractiveMap ? 1.0 : 0.0;
        if (TabMapComponent != null)
            TabMapComponent.IsVisible = _vm.IsMapModeVisible;

        if (shouldShowInteractiveMap && _isMapInitialized)
        {
            UpdateZonePins();
            UpdateUserPin();
            MapView.RefreshGraphics();
        }
    }

    private void EnsureMapInitialized()
    {
        if (_isMapInitialized)
            return;

        try
        {
            InitializeMap();
            _isMapInitialized = true;
            UpdateZonePins();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainPage] EnsureMapInitialized error: {ex}");
        }
    }

    private void StartContinuousTrackingIfNeeded()
    {
        _ = _vm.StartTrackingAsync().ContinueWith(t =>
        {
            if (t.Exception != null)
                System.Diagnostics.Debug.WriteLine($"[MainPage] StartTrackingAsync error: {t.Exception}");
        }, TaskContinuationOptions.OnlyOnFaulted);
    }

    private double GetCurrentDistanceToTourAreaMeters()
    {
        if (!_vm.HasLocationFix)
            return 0;

        var nearestSpot = _vm.AllPOIs
            .Where(p => p.ZoneType == "Spot")
            .OrderBy(p => Math.Abs(p.Latitude - _vm.CurrentLat) + Math.Abs(p.Longitude - _vm.CurrentLon))
            .FirstOrDefault();

        if (nearestSpot == null)
            return 0;

        const double earthRadiusMeters = 6371000;
        var dLat = (nearestSpot.Latitude - _vm.CurrentLat) * Math.PI / 180;
        var dLon = (nearestSpot.Longitude - _vm.CurrentLon) * Math.PI / 180;
        var lat1 = _vm.CurrentLat * Math.PI / 180;
        var lat2 = nearestSpot.Latitude * Math.PI / 180;

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(lat1) * Math.Cos(lat2)
              * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        return earthRadiusMeters * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private async Task SwitchToRealModeAsync()
    {
        _vm.IsVirtualTourActive = false;
        _vm.IsVirtualNavigation = false;
        _vm.CurrentAppMode = MainViewModel.AppMode.Explore;
        _vm.IsLegacyMapVisible = false;
        StartContinuousTrackingIfNeeded();
        await PrimeInitialExploreStateAsync();
        _vm.RefreshExploreState();
        SyncExplorePresentationState();
        ApplyMapPresentation();

        if (_vm.CurrentExploreState == MainViewModel.ExploreState.Far)
            return;

        await StartRealGuidanceFromExploreAsync();
    }

    private void SwitchToExploreFar()
    {
        _vm.IsVirtualTourActive = false;
        _vm.IsVirtualNavigation = false;
        _vm.CurrentAppMode = MainViewModel.AppMode.Explore;
        _vm.IsLegacyMapVisible = false;
        _vm.CurrentExploreState = MainViewModel.ExploreState.Far;
    }

    private static T ResolveRequiredService<T>() where T : notnull
    {
        return MauiProgram.Services.GetRequiredService<T>();
    }

    private static T? ResolveOptionalService<T>() where T : class
    {
        return MauiProgram.Services.GetService<T>();
    }

    private void OnPageLoaded(object? sender, EventArgs e)
    {
        try
        {
            // Wire all component events before any tab is shown
            WireComponentEvents();
            OnTourPageLoaded();
            HookWindowLifecycle();

            // Begin loading data - hide loading overlay when done
            _ = InitializePageAsync();
        }
        catch (Exception ex)
        {
            LogCrash("MainPage.OnPageLoaded", ex);
        }
    }

    private void HookWindowLifecycle()
    {
        if (_isWindowLifecycleHooked)
            return;

        _lifecycleWindow = Application.Current?.Windows.FirstOrDefault();
        if (_lifecycleWindow == null)
            return;

        _lifecycleWindow.Stopped += OnAppWindowStopped;
        _lifecycleWindow.Resumed += OnAppWindowResumed;
        _isWindowLifecycleHooked = true;
    }

    private void UnhookWindowLifecycle()
    {
        if (!_isWindowLifecycleHooked || _lifecycleWindow == null)
            return;

        _lifecycleWindow.Stopped -= OnAppWindowStopped;
        _lifecycleWindow.Resumed -= OnAppWindowResumed;
        _lifecycleWindow = null;
        _isWindowLifecycleHooked = false;
    }

    private void OnAppWindowStopped(object? sender, EventArgs e)
    {
        _virtualTourVm.OnAppBackgrounded();
        _vm.ReduceTrackingForBackground();
        StopLiveSyncTimer();
    }

    private void OnAppWindowResumed(object? sender, EventArgs e)
    {
        _virtualTourVm.OnAppResumed();
        _vm.RestoreTrackingFromBackground();
        StartLiveSyncTimer();
    }

    private async Task InitializePageAsync()
    {
        var usePrewarmedData = TryConsumeMainPagePrewarmFlag();
        if (!usePrewarmedData)
        {
            // Load POI data from cache immediately - no network needed if cached
            SetLoadingStatus(AppStrings.Get("Main_Status_LoadingData"));
            try
            {
                await _vm.LoadAllPoisAsync(forceSyncNow: false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainPage] LoadAllPoisAsync error: {ex}");
            }
        }
        else
        {
            SetLoadingStatus(AppStrings.Get("Main_Status_Finishing"));
        }

        // Prime initial Explore state BEFORE hiding overlay to avoid Far->Near flicker.
        SetLoadingStatus(AppStrings.Get("Main_Status_GettingLocation"));
        await PrimeInitialExploreStateAsync();

        // Hide loading only after initial state is decided.
        try { HideLoadingOverlay(); } catch { /* safe */ }
        _isInitialLoadCompleted = true;
        StartDeferredMapPrewarm();
        _ = TryAutoOpenInZoneMapAsync();

        // Continue location bootstrap asynchronously without blocking initial render.
        _ = BootstrapLocationAndTrackingAsync();

        StartOpportunisticMediaWarmup();

    }

    private void StartOpportunisticMediaWarmup()
    {
        if (_vm.AllPOIs.Count == 0)
            return;

        var hasInternet = Connectivity.Current.NetworkAccess == NetworkAccess.Internet ||
                          Connectivity.Current.NetworkAccess == NetworkAccess.ConstrainedInternet;
        if (!hasInternet)
            return;

        var spotPois = _vm.AllPOIs
            .Where(p => p.IsActive && p.ZoneType != "Area" && p.ZoneType != "District")
            .ToList();
        if (spotPois.Count == 0)
            return;

        if (_audioCache != null)
            _ = Task.Run(() => WarmupAudioOfflineCacheAsync(spotPois));

        if (_httpClient != null)
            _ = Task.Run(() => WarmupImageOfflineCacheAsync(spotPois));
    }

    private async Task WarmupAudioOfflineCacheAsync(IReadOnlyCollection<StreetFoodNarrator.App.Core.Models.POI> spots)
    {
        if (_audioCache == null)
            return;

        try
        {
            var preferredLanguage = _lang.CurrentLanguage switch
            {
                "en" => "en-US",
                "zh" => "zh-CN",
                _ => "vi-VN"
            };

            var priorityIds = spots
                .OrderBy(p =>
                {
                    var latDiff = p.Latitude - AppConfig.DefaultLatitude;
                    var lonDiff = p.Longitude - AppConfig.DefaultLongitude;
                    return (latDiff * latDiff) + (lonDiff * lonDiff);
                })
                .Take(4)
                .Select(p => p.Id)
                .ToList();

            if (priorityIds.Count > 0)
                await _audioCache.PreloadAllAsync(priorityIds);

            foreach (var poi in spots)
            {
                try
                {
                    await using var stream = await _audioCache.GetOrDownloadCachedStreamAsync(poi.Id, preferredLanguage);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[MainPage] Preferred audio warmup failed for POI {poi.Id}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainPage] Audio warmup error: {ex.Message}");
        }
    }

    private async Task WarmupImageOfflineCacheAsync(IReadOnlyCollection<StreetFoodNarrator.App.Core.Models.POI> spots)
    {
        if (_httpClient == null)
            return;

        try
        {
            var baseUrl = AppConfig.GetResolvedApiBaseUrl().TrimEnd('/');
            foreach (var poi in spots)
            {
                if (string.IsNullOrWhiteSpace(poi.ImageUrl))
                    continue;

                try
                {
                    await PoiImageCacheService.EnsureCachedAsync(_httpClient, baseUrl, poi.ImageUrl);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[MainPage] Image warmup failed for POI {poi.Id}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainPage] Image warmup error: {ex.Message}");
        }
    }

    private async Task PrewarmExploreMapsAsync()
    {
        try
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                var shouldPrewarmNearFocus = _vm.CurrentExploreState == MainViewModel.ExploreState.Near;
                var mapPage = GetOrCreateExploreMapPage(shouldPrewarmNearFocus);
                mapPage.Prewarm();
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainPage] PrewarmExploreMapsAsync error: {ex}");
        }
    }

    private void StartDeferredMapPrewarm()
    {
        if (_isMapPrewarmQueued)
            return;

        _isMapPrewarmQueued = true;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(1200);
                if (_isOpeningExploreMapPage)
                    return;

                await PrewarmExploreMapsAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainPage] Deferred prewarm error: {ex}");
            }
        });
    }

    private bool TryConsumeMainPagePrewarmFlag()
    {
        try
        {
            var ready = Preferences.Get(AppConfig.MainPagePrewarmReadyKey, false);
            Preferences.Set(AppConfig.MainPagePrewarmReadyKey, false);
            if (!ready)
                return false;

            var rawUtc = Preferences.Get(AppConfig.MainPagePrewarmAtUtcKey, string.Empty);
            Preferences.Remove(AppConfig.MainPagePrewarmAtUtcKey);
            if (DateTime.TryParse(rawUtc, out var prewarmAtUtc))
            {
                var age = DateTime.UtcNow - prewarmAtUtc.ToUniversalTime();
                if (age.TotalSeconds > MainPagePrewarmMaxAgeSeconds)
                    return false;
            }

            return _vm.AllPOIs.Count > 0;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainPage] TryConsumeMainPagePrewarmFlag error: {ex.Message}");
            return false;
        }
    }

    private async Task BootstrapLocationAndTrackingAsync()
    {
        // Start GPS tracking
        try
        {
            // In simulated mode, never seed coordinates from real device GPS.
            if (!_vm.IsSimulated)
                await TryInitializeCurrentLocationAsync();
            _vm.RefreshExploreState();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainPage] GPS init error: {ex.Message}");
        }

        // Start continuous GPS tracking on main thread because permission prompts are UI-bound.
        try
        {
            await MainThread.InvokeOnMainThreadAsync(async () => await _vm.StartTrackingAsync());
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainPage] StartTrackingAsync error: {ex.Message}");
        }
    }

    private async Task PrimeVirtualModeMapAsync()
    {
        try
        {
            var startPoi = _vm.PrimaryZone
                ?? _vm.AllPOIs.FirstOrDefault(p => p.ZoneType == "Spot");
            if (startPoi == null) return;

            var spotPois = _vm.AllPOIs.Where(p => p.ZoneType == "Spot").ToList();

            // Let layout complete at least one frame so MapControl has a valid size.
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await Task.Delay(40);
                VirtualModeComponent.CenterVirtualMap(
                    startPoi.Latitude,
                    startPoi.Longitude,
                    spotPois,
                    startPoi);
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainPage] PrimeVirtualModeMapAsync error: {ex.Message}");
        }
    }

    private async Task TryInitializeCurrentLocationAsync()
    {
        if (_vm.IsSimulated)
            return;

        Location? loc = null;

        try
        {
            // Offline-friendly: use last known GPS first if available.
            loc = await Geolocation.GetLastKnownLocationAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainPage] LastKnown location error: {ex.Message}");
        }

        try
        {
            if (loc == null)
            {
                loc = await Geolocation.GetLocationAsync(
                    new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(6)));
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainPage] GPS init active request error: {ex.Message}");
        }

        try
        {
            if (loc == null)
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                loc = await Geolocation.GetLocationAsync(
                    new GeolocationRequest(GeolocationAccuracy.Low, TimeSpan.FromSeconds(3)),
                    cts.Token);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainPage] GPS init quick fallback error: {ex.Message}");
        }

        try
        {
            if (loc == null) return;

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                _vm.HasLocationFix = true;
                _vm.CurrentLat = loc.Latitude;
                _vm.CurrentLon = loc.Longitude;
                _vm.RefreshExploreState();

                if (_isMapInitialized && MapView?.Map != null)
                {
                    var (px, py) = Mapsui.Projections.SphericalMercator.FromLonLat(loc.Longitude, loc.Latitude);
                    MapView.Map.Navigator.CenterOn(new Mapsui.MPoint(px, py));
                    MapView.Map.Navigator.ZoomTo(MapView.Map.Navigator.Resolutions[15]);
                }
                UpdateUserPin();
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainPage] GPS init error: {ex.Message}");
        }
    }

    private void SetLoadingStatus(string message)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (LoadingStatusLabel != null)
                LoadingStatusLabel.Text = message;
        });
    }

    private void HideLoadingOverlay()
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            if (LoadingOverlay == null) return;
            await LoadingOverlay.FadeToAsync(0, 200);
            LoadingOverlay.IsVisible = false;
            ApplyMapPresentation();
        });
    }

    // Called once when page is loaded; subscribes to all ContentView public events.

    private void WireComponentEvents()
    {
        // Map tab overlays - null-check because Loaded fires before XAML fully resolves in release
        if (TabMapComponent != null)
        {
            TabMapComponent.BackRequested      += OnBackClicked;
            TabMapComponent.ExploreNavRequested += OnExploreNavTapped;
            TabMapComponent.CenterMapRequested += OnCenterMapClicked;
            TabMapComponent.SettingsRequested  += OnSettingsClicked;
            TabMapComponent.ZoomInRequested    += OnZoomInClicked;
            TabMapComponent.ZoomOutRequested   += OnZoomOutClicked;
            TabMapComponent.ResetDatabaseRequested += OnResetDatabaseClicked;
            // New navigation
            TabMapComponent.SavedNavRequested  += OnSavedNavClicked;
            TabMapComponent.ProfileNavRequested += OnProfileNavClicked;
            // POI card
            TabMapComponent.ViewDetailRequested += OnMapViewDetail;
            TabMapComponent.PrevPoiRequested   += OnMapPrevPoi;
            TabMapComponent.NextPoiRequested  += OnMapNextPoi;
            TabMapComponent.LikeRequested     += OnMapLikeRequested;
            // Filter chips
            TabMapComponent.CategorySelected  += OnCategorySelected;
            // Initialize chip highlight after categories are loaded
            TabMapComponent.InitializeChips(_vm.SelectedCategory);
            // Refresh chip highlights when POI data is reloaded (e.g. after sync)
            _vm.CategoriesUpdated += (_, _) =>
            {
                TabMapComponent?.InitializeChips(_vm.SelectedCategory);
            };
            // Search
            TabMapComponent.SearchTextChanged  += OnSearchQueryChanged;
            TabMapComponent.ClearSearchRequested += OnClearSearch;
            TabMapComponent.SuggestionSelected += OnSuggestionSelected;
            WireVirtualTourEvents();
        }

        // Tour tab overlays (Real Mode)
        if (TabTourComponent != null)
        {
            TabTourComponent.BackRequested      += OnBackClicked;
            TabTourComponent.CenterMapRequested += OnCenterMapClicked;
            TabTourComponent.ZoomInRequested    += OnZoomInClicked;
            TabTourComponent.ZoomOutRequested   += OnZoomOutClicked;
            TabTourComponent.ShareRequested     += OnShareClicked;
            WireTourEvents();
        }

        // Virtual Mode overlays (new Journal / Virtual Food Tour)
        if (VirtualModeComponent != null)
        {
            VirtualModeComponent.BackRequested += OnJournalExploreNavClicked;
            VirtualModeComponent.HeaderSettingsRequested += OnJournalSettingsNavClicked;
            VirtualModeComponent.ExploreNavRequested += OnJournalExploreNavClicked;
            VirtualModeComponent.SavedNavRequested += OnJournalSavedNavClicked;
            VirtualModeComponent.ProfileNavRequested += OnJournalProfileNavClicked;
            VirtualModeComponent.SettingsNavRequested += OnJournalSettingsNavClicked;
            VirtualModeComponent.PlayPauseRequested += OnPlayPauseTapped;
            VirtualModeComponent.RewindRequested += OnRewindTapped;
            VirtualModeComponent.ForwardRequested += OnForwardTapped;
            VirtualModeComponent.QueueItemTapped += OnJournalQueueItemTapped;
            VirtualModeComponent.CurrentlyPlayingSeeMoreTapped += OnJournalCurrentlyPlayingSeeMoreTapped;
        }

        // Pin popup actions
        if (PinPopupComponent != null)
        {
            PinPopupComponent.PlayAudioRequested += OnPinPlayAudio;
            PinPopupComponent.NavigateRequested  += OnPinNavigate;
            PinPopupComponent.SaveRequested      += OnPinSave;
            PinPopupComponent.ViewDetailsRequested += OnPinViewDetails;
            PinPopupComponent.CloseRequested     += OnPinClose;
        }

        // Detail Mode actions
        if (DetailModeComponent != null)
        {
            DetailModeComponent.BackRequested += OnDetailBackClicked;
            DetailModeComponent.ShareRequested += OnDetailShareClicked;
        }

    }

    private void OnPinClose(object? sender, EventArgs e)
    {
        _vm.IsPinPopupVisible = false;
        _vm.SelectedPinPOI    = null;
        PinPopupComponent.IsVisible = false;
    }

    private void OnDetailBackClicked(object? sender, EventArgs e)
    {
        // Go back to Explore mode when leaving Detail Mode
        _vm.CurrentAppMode = MainViewModel.AppMode.Explore;
    }

    // Journal / Virtual Tour navigation buttons (in Virtual Mode header and bottom nav) all go through the same handler to avoid code duplication since they do the same thing: switch to Explore mode and exit virtual tour if active.
    private async void OnJournalExploreNavClicked(object? sender, EventArgs e)
    {
        var shouldKeepAudio = _vm.CurrentExploreState == MainViewModel.ExploreState.InZone &&
                              (_tts.IsPlaying() || _vm.IsAudioPaused || _vm.IsAudioPlaying);

        if (shouldKeepAudio)
            _preserveNarrationOnNextDisappearing = true;

        CleanupVirtualTourState();
        ShowExploreStateMode();

        if (!shouldKeepAudio)
            await StopNarrationAsync(resetProgress: true, clearResumeState: true);

        UpdateInZoneMiniAudioUi();
    }

    private void OnJournalSavedNavClicked(object? sender, EventArgs e)
    {
        if (Shell.Current != null)
        {
            _ = Shell.Current.GoToAsync("//SavedPage", false);
        }
    }

    private void OnJournalProfileNavClicked(object? sender, EventArgs e)
    {
        if (Shell.Current != null)
        {
            _ = Shell.Current.GoToAsync("//SettingsPage", false);
        }
    }

    // These handlers are for the Virtual Mode header and bottom nav buttons; they do the same thing as the regular Explore/Saved/Profile nav buttons but are duplicated here because they are in a different ContentView and wiring them to the same handler caused some weird timing issues with the map tool visibility logic. This is a simpler solution given the tight timeline, but if we have time later we can refactor to unify these handlers and fix the underlying issue.
    private void OnSavedNavClicked(object? sender, EventArgs e)
        => _ = Shell.Current?.GoToAsync("//SavedPage", false);

    private void OnProfileNavClicked(object? sender, EventArgs e)
        => _ = Shell.Current?.GoToAsync("//SettingsPage", false);

    // Map pin like/save button tapped - toggle save state for this POI and update map pins to reflect new state.
    private async void OnMapLikeRequested(object? sender, Core.Models.POI poi)
    {
        if (poi == null) return;
        try
        {
            await _vm.ToggleSavePOICommand.ExecuteAsync(poi);
            _vm.SelectedPinPOI = _vm.AllPOIs.FirstOrDefault(p => p.Id == poi.Id) ?? poi;
            UpdateZonePins();
            TabMapComponent?.RefreshLikeIcon();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainPage] OnMapLikeRequested error: {ex}");
        }
    }

    /// <summary>Explore nav tap - go to Explore mode.</summary>
    private void OnExploreNavTapped(object? sender, EventArgs e)
    {
        _vm.CurrentAppMode = MainViewModel.AppMode.Explore;
        _vm.IsLegacyMapVisible = false;
    }

    // Map category chip selected - filter POIs by this category and update map pins.
    private void OnCategorySelected(object? sender, string category)
    {
        _vm.SelectCategoryCommand.Execute(category);
        TabMapComponent?.HighlightSelectedChip(category);
        if (_vm.SelectedPinPOI != null && !_vm.FilteredPOIs.Any(p => p.Id == _vm.SelectedPinPOI.Id))
            _vm.SelectedPinPOI = null;
        UpdateZonePins();
    }

    // Search query changed - update FilteredPOIs based on search and update map pins.
    private void OnSearchQueryChanged(object? sender, Microsoft.Maui.Controls.TextChangedEventArgs e)
    {
        _vm.SearchQuery = e.NewTextValue ?? "";
        _ = UpdateSuggestionsDropdownAsync();
        if (_vm.SelectedPinPOI != null && !_vm.FilteredPOIs.Any(p => p.Id == _vm.SelectedPinPOI.Id))
            _vm.SelectedPinPOI = null;
        UpdateZonePins();
    }

    private void OnJournalSettingsNavClicked(object? sender, EventArgs e)
        => OnSettingsClicked(sender, e);

    private async void OnJournalQueueItemTapped(object? sender, Core.Models.POI poi)
    {
        if (_isNavigatingToPoiDetail) return;

        // Debounce: keep responsive queue switching while still preventing double taps.
        var now = DateTime.UtcNow;
        if ((now - _lastQueueTapTime).TotalMilliseconds < 160) return;
        _lastQueueTapTime = now;

        if (poi == null) return;
        if (_vm.PrimaryZone?.Id == poi.Id) return;

        // Update PrimaryZone + hero card immediately so switch feels instant.
        _vm.PrimaryZone = poi;
        _vm.PrimaryZoneName = poi.GetDisplayName(_lang.CurrentLanguage);
        _vm.PrimaryZoneDesc = poi.GetDisplayDescription(_lang.CurrentLanguage);
        _vm.PrimaryZoneAddress = poi.Address ?? "Đang cập nhật địa chỉ...";
        _vm.SetJournalCurrentlyPlayingFromPoi(poi);

        // Refresh queue order under the selected currently-playing POI.
        _vm.RefreshJournalQueueOnly();

        // Keep card switching responsive: only switch audio automatically if something is already playing/paused.
        var shouldSwitchAudio = _tts.IsPlaying() || _vm.IsAudioPlaying || _vm.IsAudioPaused;
        if (!shouldSwitchAudio)
            return;

        // Switch audio in background - fire and forget so UI is instant
        _queueAudioSwitchCts?.Cancel();
        _queueAudioSwitchCts = new CancellationTokenSource();
        _ = SwitchAudioForQueueItemAsync(_queueAudioSwitchCts.Token);
    }

    private async Task PrimeInitialExploreStateAsync()
    {
        try
        {
            if (_vm.IsSimulated)
            {
                // Prime with simulated provider first to avoid using stale/default coordinates.
                await _vm.StartTrackingAsync();
                var waitStarted = DateTime.UtcNow;
                while (!_vm.HasLocationFix && (DateTime.UtcNow - waitStarted).TotalMilliseconds < 700)
                    await Task.Delay(50);

                await MainThread.InvokeOnMainThreadAsync(() => _vm.RefreshExploreState());
                return;
            }

            var quickLocation = await TryGetQuickInitialLocationAsync();
            if (quickLocation != null)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    _vm.HasLocationFix = true;
                    _vm.CurrentLat = quickLocation.Latitude;
                    _vm.CurrentLon = quickLocation.Longitude;
                    _vm.RefreshExploreState();
                });
            }
            else
            {
                await MainThread.InvokeOnMainThreadAsync(() => _vm.RefreshExploreState());
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainPage] PrimeInitialExploreStateAsync error: {ex.Message}");
            try
            {
                await MainThread.InvokeOnMainThreadAsync(() => _vm.RefreshExploreState());
            }
            catch
            {
                // Best effort only
            }
        }
    }

    private async Task OpenExploreMapForFeaturedPoiAsync(bool triggerFeaturedPoi)
    {
        if (triggerFeaturedPoi)
        {
            var featuredPoi = ResolveFeaturedPoi();
            if (featuredPoi != null)
            {
                _vm.NavigationTarget = featuredPoi;
                _vm.SelectedPinPOI = featuredPoi;
                _vm.IsVirtualNavigation = false;
            }
        }
        else
        {
            _vm.NavigationTarget = null;
            _vm.SelectedPinPOI = null;
            _vm.IsVirtualNavigation = false;
        }

        var shouldOpenNearRoutingMode = _vm.CurrentExploreState == MainViewModel.ExploreState.Near ||
                                        _vm.CurrentExploreState == MainViewModel.ExploreState.InZone;
        await OpenExploreMapPageAsync(nearFocusMode: shouldOpenNearRoutingMode);
    }

    private async Task StartRealGuidanceFromExploreAsync()
    {
        var hasSpotData = await EnsureSpotDataReadyAsync();
        if (!hasSpotData)
        {
            await DisplayAlertAsync("Đang tải dữ liệu", "Dữ liệu tour chưa sẵn sàng. Vui lòng thử lại sau vài giây.", "OK");
            return;
        }

        StreetFoodNarrator.App.Core.Models.POI? guidePoi;
        if (_vm.HasActiveTourOverride)
        {
            // For an active tour, always start from the nearest stop in that tour.
            guidePoi = ResolveBestStartPoi();
        }
        else
        {
            var featuredPoi = ResolveFeaturedPoi();
            guidePoi = featuredPoi ?? ResolveBestStartPoi();
        }
        if (guidePoi == null)
        {
            await DisplayAlertAsync("Chưa có điểm tour", "Không tìm thấy điểm bắt đầu phù hợp. Vui lòng thử lại.", "OK");
            return;
        }

        _vm.NavigationTarget = guidePoi;
        _vm.SelectedPinPOI = guidePoi;
        _vm.IsVirtualNavigation = false;
        _vm.CurrentAppMode = MainViewModel.AppMode.Explore;
        _vm.IsLegacyMapVisible = false;
        _vm.RefreshExploreState();
        SyncExplorePresentationState();
        ApplyMapPresentation();

        var shouldOpenNearRoutingMode = _vm.CurrentExploreState == MainViewModel.ExploreState.Near ||
                                        _vm.CurrentExploreState == MainViewModel.ExploreState.InZone;
        await OpenExploreMapPageAsync(nearFocusMode: shouldOpenNearRoutingMode);
    }

    private static async Task<Location?> TryGetQuickInitialLocationAsync()
    {
        try
        {
            var lastKnown = await Geolocation.GetLastKnownLocationAsync();
            if (lastKnown != null)
                return lastKnown;
        }
        catch
        {
            // Ignore and fallback to quick active request.
        }

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1.5));
            return await Geolocation.GetLocationAsync(
                new GeolocationRequest(GeolocationAccuracy.Low, TimeSpan.FromSeconds(2)),
                cts.Token);
        }
        catch
        {
            return null;
        }
    }

    private async Task SwitchAudioForQueueItemAsync(CancellationToken token)
    {
        try
        {
            if (token.IsCancellationRequested) return;
            if (_tts.IsPlaying() || _vm.IsAudioPlaying)
                await StopNarrationAsync(resetProgress: true, clearResumeState: true);
            if (token.IsCancellationRequested) return;

            // Programmatic queue switch should not be blocked by tap debounce.
            _lastTapTime = DateTime.MinValue;
            await MainThread.InvokeOnMainThreadAsync(() => OnPlayPauseTapped(this, EventArgs.Empty));
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Queue] SwitchAudio error: {ex}");
        }
    }

    private async void OnJournalCurrentlyPlayingSeeMoreTapped(object? sender, EventArgs e)
    {
        var targetPoi = _vm.JournalCurrentlyPlayingPoi ?? _vm.PrimaryZone;
        if (targetPoi == null) return;
        if (!TryBeginPoiDetailNavigation()) return;

        try
        {
            _queueAudioSwitchCts?.Cancel();

            // Keep current playback running and let POIDetailPage attach to it.
            _preserveNarrationOnNextDisappearing = true;
            var nav = Shell.Current?.Navigation;
            if (nav == null)
            {
                _preserveNarrationOnNextDisappearing = false;
                return;
            }

            await nav.PushModalAsync(new POIDetailPage(targetPoi, keepCurrentAudio: true), false);
        }
        catch (Exception ex)
        {
            _preserveNarrationOnNextDisappearing = false;
            System.Diagnostics.Debug.WriteLine($"[MainPage] Open POIDetail keep-audio failed: {ex}");
        }
        finally
        {
            _isNavigatingToPoiDetail = false;
        }
    }

    private bool TryBeginPoiDetailNavigation()
    {
        var now = DateTime.UtcNow;
        if (_isNavigatingToPoiDetail)
            return false;

        if ((now - _lastPoiDetailNavigationAt).TotalMilliseconds < 700)
            return false;

        _isNavigatingToPoiDetail = true;
        _lastPoiDetailNavigationAt = now;
        return true;
    }

    private async Task StopNarrationForNavigationAsync()
    {
        try
        {
            var stopTask = StopNarrationAsync(resetProgress: true, clearResumeState: true);
            var completed = await Task.WhenAny(stopTask, Task.Delay(900));
            if (completed == stopTask)
            {
                await stopTask;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[MainPage] StopNarration timeout before navigation, continuing.");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainPage] StopNarrationForNavigationAsync error: {ex}");
        }
    }

    private void OnDetailShareClicked(object? sender, EventArgs e)
    {
        var poi = _vm.PrimaryZone;
        if (poi == null) return;
        _ = Share.Default.RequestAsync(new ShareTextRequest
        {
            Title = string.IsNullOrWhiteSpace(poi.DisplayName) ? "Street Food Narrator" : poi.DisplayName,
            Text  = $"{poi.DisplayName}\n{poi.Address ?? ""}\n{poi.DisplayDescription}"
        });
    }

    // Map category chip selected - update UI to highlight selected chip.
    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.CurrentLat) ||
            e.PropertyName == nameof(MainViewModel.CurrentLon))
        {
            if (MapView?.IsVisible == true)
            {
                //Update user pin (lightweight - no POI redraw)
                UpdateUserPin();
            }

            //Debounce route redraw (FIX 4: only redraw every 3 seconds)
            if (_vm.NavigationTarget != null && MapView?.IsVisible == true)
            {
                var now = DateTime.Now;
                if ((now - _lastRouteRedraw).TotalMilliseconds >= 3000)
                {
                    _lastRouteRedraw = now;
                    _ = DrawNavigationRouteAsync().ContinueWith(t =>
                    {
                        if (t.Exception != null)
                            System.Diagnostics.Debug.WriteLine($"[MainPage] DrawNavigationRoute error: {t.Exception}");
                    }, TaskContinuationOptions.OnlyOnFaulted);
                }
            }
        }
        else if (e.PropertyName == nameof(MainViewModel.ActiveZoneCount))
        {
            // Fires once after ActiveZones is fully populated (not N+1 times via CollectionChanged)
            if (MapView?.IsVisible == true)
                UpdateZonePins();
        }
        else if (e.PropertyName == nameof(MainViewModel.NavigationTarget))
        {
            if (MapView?.IsVisible != true)
                return;

            _lastRouteRedraw = DateTime.Now; // Reset debounce for explicit navigation
            _ = DrawNavigationRouteAsync().ContinueWith(t =>
            {
                if (t.Exception != null)
                    System.Diagnostics.Debug.WriteLine($"[MainPage] DrawNavigationRoute error: {t.Exception}");
            }, TaskContinuationOptions.OnlyOnFaulted);
        }
        else if (e.PropertyName == nameof(MainViewModel.IsVirtualNavigation))
        {
            if (MapView?.IsVisible != true)
                return;

            _lastRouteRedraw = DateTime.Now;
            _ = DrawNavigationRouteAsync().ContinueWith(t =>
            {
                if (t.Exception != null)
                    System.Diagnostics.Debug.WriteLine($"[MainPage] DrawNavigationRoute error: {t.Exception}");
            }, TaskContinuationOptions.OnlyOnFaulted);
        }
        else if (e.PropertyName == nameof(MainViewModel.CurrentAppMode))
        {
            UpdateLiveSyncTimerInterval();
            ApplyMapPresentation();
            SyncExplorePresentationState();

            if (_vm.CurrentAppMode == MainViewModel.AppMode.Virtual)
            {
                _ = MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await Task.Yield();
                    if (_vm.JournalQueueItems.Count == 0 || _vm.JournalCurrentlyPlayingPoi == null)
                        _vm.RefreshJournalState();
                    else
                        _vm.RefreshJournalQueueOnly();
                });
            }
        }
        else if (e.PropertyName == nameof(MainViewModel.CurrentExploreState) && MapView != null && !MapToolOverlay.IsVisible)
        {
            UpdateLiveSyncTimerInterval();
            var currentState = _vm.CurrentExploreState;
            var previousState = _lastObservedExploreState;
            if (!_hasObservedExploreState)
            {
                previousState = currentState;
                _hasObservedExploreState = true;
            }
            _lastObservedExploreState = currentState;

            if (currentState == MainViewModel.ExploreState.Far)
            {
                _dismissNearTransitionSuggestion = false;
                _ = PromptNearOrInZoneToFarSuggestionAsync(previousState, currentState);
            }
            else
            {
                _dismissFarTransitionSuggestion = false;
            }

            if (currentState == MainViewModel.ExploreState.Near &&
                previousState == MainViewModel.ExploreState.Far)
            {
                _ = PromptFarToNearSuggestionAsync();
            }

            if (IsAutoExploreMapNavigationEnabled() &&
                currentState == MainViewModel.ExploreState.InZone)
            {
                _ = TryAutoOpenInZoneMapAsync();
            }

            ApplyMapPresentation();
            SyncExplorePresentationState();
        }
        else if (e.PropertyName == nameof(MainViewModel.IsLegacyMapVisible))
        {
            ApplyMapPresentation();
            SyncExplorePresentationState();

            if (_vm.IsLegacyMapVisible)
                ScheduleInteractiveMapRefresh();
        }
        // IsApproaching: ApproachingBanner visibility is handled by binding inside TabTourView.
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        RemoveDanglingAlertOverlayIfAny();
        if (!ReferenceEquals(BindingContext, _vm))
            BindingContext = _vm;
        _vm.RefreshOfflineBannerSession();

        _autoOpenInZoneDirectly = Preferences.Get(AutoOpenInZoneOnNextMainPageKey, false);
        if (_autoOpenInZoneDirectly)
            Preferences.Set(AutoOpenInZoneOnNextMainPageKey, false);
        var autoOpenExploreMapFromSettings = Preferences.Get(AutoOpenExploreMapOnNextMainPageKey, false);
        if (autoOpenExploreMapFromSettings)
            Preferences.Set(AutoOpenExploreMapOnNextMainPageKey, false);
        OnTourAppearing();
        StartExploreAudioUiTimer();
        StartLiveSyncTimer();
        EnsurePrimaryZoneFromCurrentAudioContext();
        UpdateInZoneMiniAudioUi();
        _vm.RefreshExploreState();
        if (_vm.CurrentExploreState == MainViewModel.ExploreState.InZone)
            _vm.ViewState = MainViewModel.MapViewState.InZoneMinimized;
        ApplyInZoneMinimizedForSavedTabReturn();
        if (IsAutoExploreMapNavigationEnabled())
        {
            if (ShouldAutoRestoreExploreMapAfterSavedTab())
            {
                _ = OpenExploreMapPageAsync(nearFocusMode: true);
            }
            else
            {
                _ = TryAutoOpenInZoneMapAsync();
            }
        }
        if (autoOpenExploreMapFromSettings)
        {
            _ = MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await Task.Yield();
                if (!_isOpeningExploreMapPage)
                    await OpenExploreMapPageAsync(nearFocusMode: false);
            });
        }
        _ = HandlePendingQrDeepLinkAsync();
        _ = HandlePendingRequestedTourAsync();
    }

    private void ApplyInZoneMinimizedForSavedTabReturn()
    {
        if (_vm.CurrentExploreState != MainViewModel.ExploreState.InZone)
            return;

        var previousRoute = AppShell.PreviousNonSettingsRoute;
        var currentRoute = AppShell.LastNonSettingsRoute;

        var cameFromSaved = !string.IsNullOrWhiteSpace(previousRoute) &&
                            previousRoute.Contains("SavedPage", StringComparison.OrdinalIgnoreCase);
        var nowAtMap = string.IsNullOrWhiteSpace(currentRoute) ||
                       currentRoute.Contains("MapPage", StringComparison.OrdinalIgnoreCase);

        if (!cameFromSaved || !nowAtMap)
            return;

        _vm.CurrentAppMode = MainViewModel.AppMode.Explore;
        _vm.IsLegacyMapVisible = false;
        _vm.ViewState = MainViewModel.MapViewState.InZoneMinimized;
        SyncExplorePresentationState();
        ApplyMapPresentation();
    }

    private bool ShouldAutoRestoreExploreMapAfterSavedTab()
    {
        if (!_isInitialLoadCompleted)
            return false;

        if (_vm.AutoOpenRequestedTourOnMap)
            return false;

        if (_vm.CurrentExploreState != MainViewModel.ExploreState.InZone)
            return false;

        if (_vm.CurrentAppMode != MainViewModel.AppMode.Explore)
            return false;

        if (_isOpeningExploreMapPage || _isNearSuggestionPopupOpen)
            return false;

        var previousRoute = AppShell.PreviousNonSettingsRoute;
        var currentRoute = AppShell.LastNonSettingsRoute;

        var cameFromSaved = !string.IsNullOrWhiteSpace(previousRoute) &&
                            previousRoute.Contains("SavedPage", StringComparison.OrdinalIgnoreCase);
        var nowAtMap = string.IsNullOrWhiteSpace(currentRoute) ||
                       currentRoute.Contains("MapPage", StringComparison.OrdinalIgnoreCase);

        return cameFromSaved && nowAtMap;
    }

    private void RemoveDanglingAlertOverlayIfAny()
    {
        try
        {
            if (Content is not Layout rootLayout)
                return;

            RemoveViewsByClassIdRecursive(rootLayout, "__custom_alert_overlay");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainPage] RemoveDanglingAlertOverlayIfAny error: {ex.Message}");
        }
    }

    private static void RemoveViewsByClassIdRecursive(Layout layout, string classId)
    {
        var staleViews = layout.Children
            .OfType<View>()
            .Where(v => string.Equals(v.ClassId, classId, StringComparison.Ordinal))
            .ToList();

        foreach (var view in staleViews)
            layout.Children.Remove(view);

        foreach (var childLayout in layout.Children.OfType<Layout>().ToList())
            RemoveViewsByClassIdRecursive(childLayout, classId);
    }

    private void StartExploreAudioUiTimer()
    {
        if (_exploreAudioUiTimer != null || Dispatcher == null)
            return;

        _exploreAudioUiTimer = Dispatcher.CreateTimer();
        _exploreAudioUiTimer.Interval = TimeSpan.FromMilliseconds(400);
        _exploreAudioUiTimer.Tick += (_, _) =>
        {
            if (!IsVisible)
                return;

            if (_vm.CurrentAppMode != MainViewModel.AppMode.Explore)
                return;

            EnsurePrimaryZoneFromCurrentAudioContext();
            UpdateInZoneMiniAudioUi();
        };
        _exploreAudioUiTimer.Start();
    }

    private void StopExploreAudioUiTimer()
    {
        if (_exploreAudioUiTimer == null)
            return;

        _exploreAudioUiTimer.Stop();
        _exploreAudioUiTimer = null;
    }

    private void StartLiveSyncTimer()
    {
        if (_liveSyncTimer != null || Dispatcher == null)
            return;

        _liveSyncTimer = Dispatcher.CreateTimer();
        _liveSyncTimer.Interval = GetLiveSyncTimerInterval();
        _liveSyncTimer.Tick += async (_, _) =>
        {
            if (!IsVisible || _vm.CurrentAppMode != MainViewModel.AppMode.Explore)
                return;
            if (!IsCurrentMapShellRoute())
                return;
            if (_isLiveSyncTickInProgress)
                return;

            UpdateLiveSyncTimerInterval();

            _isLiveSyncTickInProgress = true;
            try
            {
                await _vm.LiveSyncIfDueAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainPage] LiveSync timer error: {ex.Message}");
            }
            finally
            {
                _isLiveSyncTickInProgress = false;
            }
        };
        _liveSyncTimer.Start();
    }

    private TimeSpan GetLiveSyncTimerInterval()
    {
        return _vm.CurrentExploreState == MainViewModel.ExploreState.Far
            ? LiveSyncFarInterval
            : LiveSyncNearInterval;
    }

    private void UpdateLiveSyncTimerInterval()
    {
        if (_liveSyncTimer == null)
            return;

        var targetInterval = GetLiveSyncTimerInterval();
        if (_liveSyncTimer.Interval != targetInterval)
            _liveSyncTimer.Interval = targetInterval;
    }

    private static bool IsCurrentMapShellRoute()
    {
        try
        {
            var route = Shell.Current?.CurrentState?.Location?.OriginalString ?? string.Empty;
            if (string.IsNullOrWhiteSpace(route))
                return true;

            return route.Contains("MapPage", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return true;
        }
    }

    private void StopLiveSyncTimer()
    {
        if (_liveSyncTimer == null)
            return;

        _liveSyncTimer.Stop();
        _liveSyncTimer = null;
    }

    private void EnsurePrimaryZoneFromCurrentAudioContext()
    {
        if (_vm.PrimaryZone != null)
            return;

        if (_vm.PlayingPoiId.HasValue)
        {
            var playingPoi = _vm.AllPOIs.FirstOrDefault(p => p.Id == _vm.PlayingPoiId.Value);
            if (playingPoi != null)
            {
                _vm.PrimaryZone = playingPoi;
                _vm.PrimaryZoneName = playingPoi.GetDisplayName(_lang.CurrentLanguage);
                return;
            }
        }

        var featuredPoiId = _vm.FeaturedExplorePoi?.Id;
        if (featuredPoiId.HasValue)
        {
            var featuredPoi = _vm.AllPOIs.FirstOrDefault(p => p.Id == featuredPoiId.Value);
            if (featuredPoi != null)
            {
                _vm.PrimaryZone = featuredPoi;
                _vm.PrimaryZoneName = featuredPoi.GetDisplayName(_lang.CurrentLanguage);
            }
        }
    }

    private void UpdateInZoneMiniAudioUi()
    {
        var current = Math.Max(0, _tts.GetCurrentPosition());
        var duration = Math.Max(0, _tts.GetDuration());
        if (duration > 0 && current > duration)
            current = duration;

        _vm.IsAudioPlaying = _tts.IsPlaying();
        _vm.AudioProgress = duration > 0 ? Math.Clamp(current / duration, 0, 1) : 0;
        _vm.AudioTimeElapsed = FormatDuration(current);
        _vm.AudioDuration = duration > 0 ? FormatDuration(duration) : "0:00";

        if (InZoneMiniPlayPauseButton != null)
            InZoneMiniPlayPauseButton.Text = _tts.IsPlaying() ? "\U000F03E4" : "\U000F040A";
    }

    private async Task PromptFarToNearSuggestionAsync()
    {
        try
        {
            if (_dismissNearTransitionSuggestion || _isStateTransitionPromptOpen)
                return;

            if (!IsTopNavigationPage())
                return;

            if (_vm.CurrentExploreState != MainViewModel.ExploreState.Near)
                return;

            if (_vm.CurrentAppMode != MainViewModel.AppMode.Explore)
                return;

            if (_isOpeningExploreMapPage || _isNearSuggestionPopupOpen)
                return;

            _isStateTransitionPromptOpen = true;
            await MainThread.InvokeOnMainThreadAsync(() =>
                DisplayAlertAsync(
                    "Bạn đã đến gần khu ẩm thực",
                    "Ứng dụng đã chuyển sang trạng thái Gần. Bạn có thể bắt đầu chỉ đường ngay khi sẵn sàng.",
                    "Đã hiểu"));

            _dismissNearTransitionSuggestion = true;

            // Apply Near state immediately after user acknowledges, no extra delay.
            _vm.CurrentExploreState = MainViewModel.ExploreState.Near;
            _vm.CurrentAppMode = MainViewModel.AppMode.Explore;
            _vm.IsLegacyMapVisible = false;
            SyncExplorePresentationState();
            ApplyMapPresentation();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainPage] PromptFarToNearSuggestionAsync error: {ex}");
        }
        finally
        {
            _isStateTransitionPromptOpen = false;
        }
    }

    private async Task PromptNearOrInZoneToFarSuggestionAsync(
        MainViewModel.ExploreState previousState,
        MainViewModel.ExploreState currentState)
    {
        try
        {
            if (_dismissFarTransitionSuggestion || _isStateTransitionPromptOpen)
                return;

            if (currentState != MainViewModel.ExploreState.Far)
                return;

            if (previousState != MainViewModel.ExploreState.Near &&
                previousState != MainViewModel.ExploreState.InZone)
                return;

            var canShowAlert = IsTopNavigationPage();

            _isStateTransitionPromptOpen = true;
            if (canShowAlert)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                    CustomAlert.ShowAsync(
                        "Bạn đã ra xa khu ẩm thực ",
                        "Ứng dụng sẽ giữ ở chế độ khám phá. Bạn có thể mở xem thử tour bất cứ lúc nào.",
                        "Đã hiểu",
                        AlertType.Info));
            }

            _dismissFarTransitionSuggestion = true;
            if (_vm.CurrentAppMode != MainViewModel.AppMode.Explore)
            {
                _vm.CurrentAppMode = MainViewModel.AppMode.Explore;
                _vm.IsLegacyMapVisible = false;
                SyncExplorePresentationState();
                ApplyMapPresentation();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainPage] PromptNearOrInZoneToFarSuggestionAsync error: {ex}");
        }
        finally
        {
            _isStateTransitionPromptOpen = false;
        }
    }

    private async Task TryAutoOpenInZoneMapAsync()
    {
        try
        {
            if (!IsAutoExploreMapNavigationEnabled())
                return;

            if (!_isInitialLoadCompleted)
                return;

            if (LoadingOverlay?.IsVisible == true)
                return;

            if (_isStateTransitionPromptOpen)
                return;

            if (!IsTopNavigationPage())
                return;

            // User just minimized ExploreMap InZone sheet and returned to MainPage.
            // Do not prompt again in this handoff transition.
            if (_vm.ViewState == MainViewModel.MapViewState.InZoneMinimized)
                return;

            if (_vm.CurrentExploreState != MainViewModel.ExploreState.InZone)
                return;

            if (_vm.CurrentAppMode != MainViewModel.AppMode.Explore)
                return;

            if (_isOpeningExploreMapPage || _isNearSuggestionPopupOpen)
                return;

            _autoOpenInZoneDirectly = false;
            await OpenExploreMapForFeaturedPoiAsync(triggerFeaturedPoi: true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainPage] TryAutoOpenInZoneMapAsync error: {ex}");
        }
        finally
        {
            _isStateTransitionPromptOpen = false;
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        StopExploreAudioUiTimer();
        StopLiveSyncTimer();

        // Opening POIDetail from VirtualMode is a modal transition; keep current narration alive.
        if (_preserveNarrationOnNextDisappearing)
        {
            _preserveNarrationOnNextDisappearing = false;
            return;
        }

        OnTourDisappearing();
        UnhookWindowLifecycle();
        _ = StopNarrationAsync(resetProgress: true, clearResumeState: true);
    }

    private bool IsTopNavigationPage()
    {
        var nav = Shell.Current?.Navigation ?? Navigation;
        if (nav == null)
            return false;

        if (nav.ModalStack.Count > 0)
            return ReferenceEquals(nav.ModalStack[nav.ModalStack.Count - 1], this);

        if (nav.NavigationStack.Count > 0)
            return ReferenceEquals(nav.NavigationStack[nav.NavigationStack.Count - 1], this);

        return IsVisible;
    }

    private async Task<INavigation?> ResolveNavigationReadyAsync(int retries = 4)
    {
        for (var attempt = 0; attempt < retries; attempt++)
        {
            var nav = Shell.Current?.Navigation
                ?? Application.Current?.Windows.FirstOrDefault()?.Page?.Navigation
                ?? Navigation;

            if (nav != null)
                return nav;

            await Task.Delay(120);
        }

        return null;
    }

    private static async Task PushExploreMapPageWithRetryAsync(INavigation nav, ExploreMapPage exploreMapPage)
    {
        try
        {
            await nav.PushAsync(exploreMapPage, false);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Pending Navigations", StringComparison.OrdinalIgnoreCase))
        {
            await Task.Delay(180);
            await nav.PushAsync(exploreMapPage, false);
        }
    }
}
