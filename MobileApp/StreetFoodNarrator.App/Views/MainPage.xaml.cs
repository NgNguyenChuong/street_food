
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
using StreetFoodNarrator.App.ViewModels;
using System.IO;

namespace StreetFoodNarrator.App.Views;

public partial class MainPage : ContentPage
{
    private readonly MainViewModel _vm;
    private readonly ITTSService _tts;
    private readonly LanguageService _lang;
    private readonly IAudioCacheService? _audioCache;
    private readonly IVirtualTourViewModel _virtualTourVm;
    private bool _isMapInitialized;
    private Window? _lifecycleWindow;
    private bool _isWindowLifecycleHooked;
    private DateTime _lastQueueTapTime = DateTime.MinValue;
    private bool _isOpeningExploreMapPage;
    private ExploreMapPage? _cachedExploreMapPage;
    private bool _hasPrewarmedSecondaryExperiences;

    public MainPage()
        : this(null, null, null, null)
    {
    }

    public MainPage(MainViewModel? viewModel = null, ITTSService? ttsService = null,
        LanguageService? languageService = null, IAudioCacheService? audioCache = null)
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
            Loaded += OnPageLoaded;
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

    private async void OnVmSwitchToRealModeRequested(object? sender, EventArgs e)
    {
        await SwitchToRealModeAsync();
    }

    private void OnMenuButtonClicked(object? sender, EventArgs e)
        => OnSettingsClicked(sender, e);

    private async void OnProfileButtonClicked(object? sender, EventArgs e)
    {
        if (Shell.Current != null)
        {
            await Shell.Current.GoToAsync("//ProfilePage");
        }
    }

    private async void OnWelcomeButtonClicked(object? sender, EventArgs e)
    {
        if (Shell.Current != null)
        {
            await Shell.Current.GoToAsync("//WelcomePage");
        }
    }

    private void OnPrimaryExploreActionClicked(object? sender, EventArgs e)
    {
        switch (_vm.CurrentExploreState)
        {
            case MainViewModel.ExploreState.Far:
                _ = StartVirtualPreviewAsync();
                break;
            default:
                _ = OpenExploreMapPageAsync();
                break;
        }
    }

    private void OnSecondaryExploreActionClicked(object? sender, EventArgs e)
        => _ = OpenExploreMapPageAsync();

    private void OnDirectionClicked(object? sender, EventArgs e)
        => _ = OpenExploreMapPageAsync();

    private void OnShuffleClicked(object? sender, EventArgs e)
    {
        var cards = new List<MainViewModel.ExplorePoiCard>();
        if (_vm.FeaturedExplorePoi != null)
            cards.Add(_vm.FeaturedExplorePoi);
        cards.AddRange(_vm.NearbyExplorePois);

        if (cards.Count < 2)
            return;

        var rotated = cards.Skip(1).Concat(cards.Take(1)).ToList();
        _vm.FeaturedExplorePoi = rotated[0];
        _vm.ExploreHeroImage = rotated[0].ImageUrl;
        _vm.ExploreAudioQuote = rotated[0].QuoteText;

        _vm.NearbyExplorePois.Clear();
        foreach (var item in rotated.Skip(1).Take(2))
        {
            _vm.NearbyExplorePois.Add(item);
        }
    }

    private async void OnFeaturedSaveClicked(object? sender, EventArgs e)
    {
        var poi = ResolveFeaturedPoi();
        if (poi == null)
            return;

        await _vm.ToggleSavePOICommand.ExecuteAsync(poi);
    }

    private async void OnMapToolCloseClicked(object? sender, EventArgs e)
    {
        await SetMapToolVisibleAsync(false);
    }

    private async Task StartVirtualPreviewAsync()
    {
        var poi = ResolveBestStartPoi();
        if (poi == null)
            return;

        EnsureMapInitialized();
        StartContinuousTrackingIfNeeded();
        _vm.IsLegacyMapVisible = false;
        ApplyMapPresentation();
        await PrimeVirtualModeMapAsync();
        await EnableVirtualTourAsync(poi);
    }

    private void ShowLegacyMapMode()
    {
        CleanupVirtualTourState();
        _vm.CurrentAppMode = MainViewModel.AppMode.Explore;
        EnsureMapInitialized();
        StartContinuousTrackingIfNeeded();
        _vm.IsLegacyMapVisible = true;
        SyncExplorePresentationState();
        ApplyMapPresentation();
        ScheduleInteractiveMapRefresh();
        _ = SetMapToolVisibleAsync(false);
    }

    private void ShowExploreStateMode()
    {
        _vm.CurrentAppMode = MainViewModel.AppMode.Explore;
        _vm.IsLegacyMapVisible = false;
        SyncExplorePresentationState();
        ApplyMapPresentation();
    }

    private async Task OpenExploreMapPageAsync()
    {
        if (_isOpeningExploreMapPage)
            return;

        _isOpeningExploreMapPage = true;

        try
        {
            _vm.CurrentAppMode = MainViewModel.AppMode.Explore;
            _vm.IsLegacyMapVisible = false;
            SyncExplorePresentationState();
            ApplyMapPresentation();

            var mapPage = GetOrCreateExploreMapPage();
            mapPage.Prewarm();
            await Navigation.PushAsync(mapPage);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainPage] OpenExploreMapPageAsync error: {ex}");
        }
        finally
        {
            _isOpeningExploreMapPage = false;
        }
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

    private ExploreMapPage GetOrCreateExploreMapPage()
    {
        _cachedExploreMapPage ??= ResolveRequiredService<ExploreMapPage>();
        return _cachedExploreMapPage;
    }

    private void PrewarmSecondaryExperiences()
    {
        if (_hasPrewarmedSecondaryExperiences)
            return;

        _hasPrewarmedSecondaryExperiences = true;

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                await Task.Delay(180);
                EnsureMapInitialized();
                var exploreMapPage = GetOrCreateExploreMapPage();
                exploreMapPage.Prewarm();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainPage] PrewarmSecondaryExperiences error: {ex.Message}");
            }
        });
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
        var spots = _vm.AllPOIs.Where(p => p.ZoneType == "Spot").ToList();
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
        _vm.CurrentAppMode = MainViewModel.AppMode.Real;
        _vm.IsLegacyMapVisible = true;
        StartContinuousTrackingIfNeeded();
        ApplyMapPresentation();
        await Task.CompletedTask;
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
        var serviceName = typeof(T).Name;
        var providers = new IServiceProvider?[]
        {
            Application.Current?.Handler?.MauiContext?.Services,
            Application.Current?.Windows.FirstOrDefault()?.Page?.Handler?.MauiContext?.Services,
            MauiProgram.Services
        };

        List<Exception> resolveErrors = new();
        foreach (var provider in providers)
        {
            if (provider == null) continue;
            try
            {
                System.Diagnostics.Debug.WriteLine($"[ServiceResolution] Attempting to get {serviceName} from provider...");
                var service = provider.GetRequiredService<T>();
                System.Diagnostics.Debug.WriteLine($"[ServiceResolution] ✓ Successfully resolved {serviceName}");
                return service;
            }
            catch (ObjectDisposedException ode)
            {
                // Provider is stale, keep looking for an active one
                System.Diagnostics.Debug.WriteLine($"[ServiceResolution] Provider stale for {serviceName}: {ode.Message}");
                resolveErrors.Add(ode);
            }
            catch (Exception ex)
            {
                // Catch other exceptions (InvalidOperationException, etc.) but keep trying other providers
                System.Diagnostics.Debug.WriteLine($"[ServiceResolution] Failed to resolve {serviceName} from provider: {ex.GetType().Name}: {ex.Message}");
                resolveErrors.Add(ex);
            }
        }

        // If we get here, no provider worked
        var errorMsg = $"No active service provider found for {serviceName}. Tried {resolveErrors.Count} provider(s).";
        System.Diagnostics.Debug.WriteLine($"[ServiceResolution] ✗ {errorMsg}");
        foreach (var err in resolveErrors)
        {
            System.Diagnostics.Debug.WriteLine($"[ServiceResolution]   Error: {err.GetType().Name}: {err.Message}");
        }
        throw new InvalidOperationException(errorMsg, resolveErrors.FirstOrDefault());
    }

    private static T? ResolveOptionalService<T>() where T : class
    {
        var serviceName = typeof(T).Name;
        var providers = new IServiceProvider?[]
        {
            Application.Current?.Handler?.MauiContext?.Services,
            Application.Current?.Windows.FirstOrDefault()?.Page?.Handler?.MauiContext?.Services,
            MauiProgram.Services
        };

        List<Exception> resolveErrors = new();
        foreach (var provider in providers)
        {
            if (provider == null) continue;
            try
            {
                System.Diagnostics.Debug.WriteLine($"[ServiceResolution] Attempting to get optional {serviceName} from provider...");
                var service = provider.GetService<T>();
                if (service != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[ServiceResolution] ✓ Successfully resolved optional {serviceName}");
                    return service;
                }
            }
            catch (ObjectDisposedException ode)
            {
                // Provider is stale, keep looking for an active one
                System.Diagnostics.Debug.WriteLine($"[ServiceResolution] Provider stale for optional {serviceName}: {ode.Message}");
                resolveErrors.Add(ode);
            }
            catch (Exception ex)
            {
                // Catch other exceptions but keep trying
                System.Diagnostics.Debug.WriteLine($"[ServiceResolution] Failed to resolve optional {serviceName}: {ex.GetType().Name}: {ex.Message}");
                resolveErrors.Add(ex);
            }
        }

        System.Diagnostics.Debug.WriteLine($"[ServiceResolution] Optional service {serviceName} not found or not available, returning null");
        return null;
    }

    private void OnPageLoaded(object? sender, EventArgs e)
    {
        try
        {
            // Wire all component events before any tab is shown
            WireComponentEvents();
            OnTourPageLoaded();
            HookWindowLifecycle();

            // Begin loading data — hide loading overlay when done
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
    }

    private void OnAppWindowResumed(object? sender, EventArgs e)
    {
        _virtualTourVm.OnAppResumed();
    }

    private async Task InitializePageAsync()
    {
        // Start GPS in background so first render is not blocked by sensor timeout.
        var gpsInitTask = TryInitializeCurrentLocationAsync();

        //Tai du lieu POI truoc de co thong tin hien thi tren ban do cang som cang tot, sau do moi an loading overlay de nguoi dung thay map ngay khi da co data.
        SetLoadingStatus("Đang tải dữ liệu...");
        try
        {
            await _vm.LoadAllPoisAsync(forceSyncNow: false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainPage] LoadAllPoisAsync error: {ex}");
        }

        SetLoadingStatus($"Đã tải {_vm.AllPOIs.Count} địa điểm...");
        await Task.Delay(300); // Cho UI cap nhat

        SetLoadingStatus("Đang xác định vị trí của bạn...");
        try
        {
            await gpsInitTask;
            _vm.RefreshExploreState();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainPage] GPS/state refresh error: {ex.Message}");
        }

        // Hide loading overlay â€” crash guard prevents blank screen on failure
        try
        {
            HideLoadingOverlay();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainPage] HideLoadingOverlay error: {ex.Message}");
        }

        PrewarmSecondaryExperiences();

        // Show virtual tour popup AFTER overlay is hidden

        // Preload audio for nearby POIs so that if user taps one, it can play immediately without waiting for load.
        if (_audioCache != null)
        {
            try
            {
                var ids = _vm.AllPOIs
                    .Where(p => p.ZoneType != "Area" && p.ZoneType != "District")
                    .OrderBy(p =>
                    {
                        var latDiff = p.Latitude - AppConfig.DefaultLatitude;
                        var lonDiff = p.Longitude - AppConfig.DefaultLongitude;
                        return (latDiff * latDiff) + (lonDiff * lonDiff);
                    })
                    .Take(4)
                    .Select(p => p.Id)
                    .ToList();
                if (ids.Count > 0)
                {
                    _ = _audioCache.PreloadAllAsync(ids).ContinueWith(pt =>
                    {
                        if (pt.Exception != null)
                            System.Diagnostics.Debug.WriteLine(
                                $"[MainPage] AudioCache preload error: {pt.Exception.InnerException?.Message}");
                    }, TaskContinuationOptions.OnlyOnFaulted);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainPage] Audio preload error: {ex.Message}");
            }
        }

        // Initial GPS attempt already completed before loading overlay was hidden.

        // Start continuous tracking now so that if user moves to map immediately, we already have a location fix and can show them on the map.
        try
        {
            _ = _vm.StartTrackingAsync().ContinueWith(t =>
            {
                if (t.Exception != null)
                    System.Diagnostics.Debug.WriteLine($"[MainPage] StartTrackingAsync error: {t.Exception}");
            }, TaskContinuationOptions.OnlyOnFaulted);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainPage] StartTrackingAsync launch error: {ex.Message}");
        }
    }    private async Task PrimeVirtualModeMapAsync()
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
        try
        {
            var loc = await Geolocation.GetLocationAsync(
                new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(6)));
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
            await LoadingOverlay.FadeToAsync(0, 400);
            LoadingOverlay.IsVisible = false;
            ApplyMapPresentation();
        });
    }

    // Called once when page is loaded; subscribes to all ContentView public events.

    private void WireComponentEvents()
    {
        // Map tab overlays — null-check because Loaded fires before XAML fully resolves in release
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
            VirtualModeComponent.QueueSeeMoreTapped += OnJournalQueueSeeMoreTapped;
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
        CleanupVirtualTourState();
        ShowExploreStateMode();
        await StopNarrationAsync(resetProgress: true, clearResumeState: true);
    }

    private void OnJournalSavedNavClicked(object? sender, EventArgs e)
    {
        if (Shell.Current != null)
        {
            _ = Shell.Current.GoToAsync("//SavedPage");
        }
    }

    private void OnJournalProfileNavClicked(object? sender, EventArgs e)
    {
        if (Shell.Current != null)
        {
            _ = Shell.Current.GoToAsync("//ProfilePage");
        }
    }

    // These handlers are for the Virtual Mode header and bottom nav buttons; they do the same thing as the regular Explore/Saved/Profile nav buttons but are duplicated here because they are in a different ContentView and wiring them to the same handler caused some weird timing issues with the map tool visibility logic. This is a simpler solution given the tight timeline, but if we have time later we can refactor to unify these handlers and fix the underlying issue.
    private void OnSavedNavClicked(object? sender, EventArgs e)
        => _ = Shell.Current?.GoToAsync("//SavedPage");

    private void OnProfileNavClicked(object? sender, EventArgs e)
        => _ = Shell.Current?.GoToAsync("//ProfilePage");

    // Map pin like/save button tapped — toggle save state for this POI and update map pins to reflect new state.
    private async void OnMapLikeRequested(object? sender, Core.Models.POI poi)
    {
        if (poi == null) return;
        await _vm.ToggleSavePOICommand.ExecuteAsync(poi);
        UpdateZonePins();
    }

    /// <summary>Explore nav tap — go to Explore mode.</summary>
    private void OnExploreNavTapped(object? sender, EventArgs e)
    {
        _vm.CurrentAppMode = MainViewModel.AppMode.Explore;
        _vm.IsLegacyMapVisible = false;
    }

    // Map category chip selected — filter POIs by this category and update map pins.
    private void OnCategorySelected(object? sender, string category)
    {
        _vm.SelectCategoryCommand.Execute(category);
        TabMapComponent?.HighlightSelectedChip(category);
        if (_vm.SelectedPinPOI != null && !_vm.FilteredPOIs.Any(p => p.Id == _vm.SelectedPinPOI.Id))
            _vm.SelectedPinPOI = null;
        UpdateZonePins();
    }

    // Search query changed — update FilteredPOIs based on search and update map pins.
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
        // Debounce: ignore rapid taps within 400ms
        var now = DateTime.UtcNow;
        if ((now - _lastQueueTapTime).TotalMilliseconds < 400) return;
        _lastQueueTapTime = now;

        if (poi == null) return;

        // Update PrimaryZone immediately so audio player shows correct POI info
        _vm.PrimaryZone = poi;
        _vm.PrimaryZoneName = poi.Name_Vi ?? poi.Name_En ?? "-";
        _vm.PrimaryZoneDesc = poi.Description_Vi ?? poi.Description_En ?? "";
        _vm.PrimaryZoneAddress = poi.Address ?? "Đang cập nhật địa chỉ...";

        // Only refresh the queue list - do NOT update Currently Playing card YET
        // The card updates when audio actually starts playing (in OnPlayPauseTapped)
        _vm.RefreshJournalQueueOnly();

        // Switch audio in background — fire and forget so UI is instant
        _ = SwitchAudioForQueueItemAsync();
    }

    private async Task SwitchAudioForQueueItemAsync()
    {
        try
        {
            await StopNarrationAsync(resetProgress: true, clearResumeState: true);
            // Give a tiny moment for TTS to reset before starting new narration
            await Task.Delay(50);
            OnPlayPauseTapped(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Queue] SwitchAudio error: {ex}");
        }
    }

    private async void OnJournalQueueSeeMoreTapped(object? sender, Core.Models.POI poi)
    {
        if (poi == null) return;

        // Stop current audio so that when user goes back from POIDetailPage, we don't have two audio sources competing (the one for the POI they tapped on, and the one for the PrimaryZone which is still technically "playing" until we stop it here). We also reset progress and clear resume state so that if they do go back to the journal and tap play again, it starts from the beginning of the new POI's audio instead of resuming the old one.
        await StopNarrationAsync(resetProgress: true, clearResumeState: true);

        // Show POIDetailPage for this POI  audio stays stopped
        await Shell.Current.Navigation.PushModalAsync(new POIDetailPage(poi));
    }

    private async void OnJournalCurrentlyPlayingSeeMoreTapped(object? sender, EventArgs e)
    {
        if (_vm.PrimaryZone == null) return;

        // Stop audio and navigate to POIDetailPage
        await StopNarrationAsync(resetProgress: true, clearResumeState: true);
        await Shell.Current.Navigation.PushModalAsync(new POIDetailPage(_vm.PrimaryZone));
    }

    private void OnDetailShareClicked(object? sender, EventArgs e)
    {
        var poi = _vm.PrimaryZone;
        if (poi == null) return;
        _ = Share.Default.RequestAsync(new ShareTextRequest
        {
            Title = poi.Name_Vi ?? poi.Name_En ?? "Street Food Narrator",
            Text  = $"{poi.Name_Vi ?? poi.Name_En}\n{poi.Address ?? ""}\n{poi.Description_Vi ?? poi.Description_En ?? ""}"
        });
    }

    // Map category chip selected — update UI to highlight selected chip.
    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.CurrentLat) ||
            e.PropertyName == nameof(MainViewModel.CurrentLon))
        {
            //Update user pin (lightweight - no POI redraw)
            UpdateUserPin();

            //Debounce route redraw (FIX 4: only redraw every 3 seconds)
            if (_vm.NavigationTarget != null)
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
            UpdateZonePins();
        }
        else if (e.PropertyName == nameof(MainViewModel.NavigationTarget))
        {
            _lastRouteRedraw = DateTime.Now; // Reset debounce for explicit navigation
            _ = DrawNavigationRouteAsync().ContinueWith(t =>
            {
                if (t.Exception != null)
                    System.Diagnostics.Debug.WriteLine($"[MainPage] DrawNavigationRoute error: {t.Exception}");
            }, TaskContinuationOptions.OnlyOnFaulted);
        }
        else if (e.PropertyName == nameof(MainViewModel.IsVirtualNavigation))
        {
            _lastRouteRedraw = DateTime.Now;
            _ = DrawNavigationRouteAsync().ContinueWith(t =>
            {
                if (t.Exception != null)
                    System.Diagnostics.Debug.WriteLine($"[MainPage] DrawNavigationRoute error: {t.Exception}");
            }, TaskContinuationOptions.OnlyOnFaulted);
        }
        else if (e.PropertyName == nameof(MainViewModel.CurrentAppMode))
        {
            ApplyMapPresentation();
            SyncExplorePresentationState();

            if (_vm.CurrentAppMode == MainViewModel.AppMode.Virtual)
            {
                _vm.RefreshJournalState();
                _ = PrimeVirtualModeMapAsync();
            }
        }
        else if (e.PropertyName == nameof(MainViewModel.CurrentExploreState) && MapView != null && !MapToolOverlay.IsVisible)
        {
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
        OnTourAppearing();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        OnTourDisappearing();
        UnhookWindowLifecycle();
        _ = StopNarrationAsync(resetProgress: true, clearResumeState: true);
    }
}

