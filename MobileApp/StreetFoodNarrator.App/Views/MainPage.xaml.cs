// ─────────────────────────────────────────────────────────────────────────────
// MainPage.xaml.cs  -  Core: fields, constructor, lifecycle
//
// Logic is split across partial class files:
//   MainPage.Map.cs       – OSM map, pins, zoom, header button handlers
//   MainPage.Audio.cs     – TTS play/pause, share
//   MainPage.Nav.cs       – bottom navigation tab switching
//   MainPage.PinPopup.cs  – popup when tapping a map pin
//
// ContentView components (Views/Components/) handle their own XAML and
// internal UI logic; they communicate outward only via public events.
// ─────────────────────────────────────────────────────────────────────────────

using StreetFoodNarrator.App.Core.Services;
using StreetFoodNarrator.App.ViewModels;

namespace StreetFoodNarrator.App.Views;

public partial class MainPage : ContentPage
{
    // ── Shared service dependencies ───────────────────────────────────────────
    private readonly MainViewModel _vm;
    private readonly ITTSService _tts;
    private readonly LanguageService _lang;
    private readonly IAudioCacheService? _audioCache;

    public MainPage(MainViewModel viewModel, ITTSService? ttsService = null,
        LanguageService? languageService = null, IAudioCacheService? audioCache = null)
    {
        InitializeComponent();
        BindingContext = _vm = viewModel;
        _tts        = ttsService      ?? MauiProgram.Services.GetRequiredService<ITTSService>();
        _lang       = languageService ?? MauiProgram.Services.GetRequiredService<LanguageService>();
        _audioCache = audioCache      ?? MauiProgram.Services.GetService<IAudioCacheService>();

        _vm.PropertyChanged += OnViewModelPropertyChanged;
        Loaded += OnPageLoaded;
    }

    private void OnPageLoaded(object? sender, EventArgs e)
    {
        // Wire all component events before any tab is shown
        WireComponentEvents();

        try { InitializeMap(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[MainPage] InitializeMap error: {ex}"); }

        // Vẽ test pin ngay lập tức (không chờ data load)
        Dispatcher.Dispatch(() => UpdateZonePins());

        // Nếu đã có mục tiêu điều hướng (từ màn khác), vẽ tuyến đường ngay khi map sẵn sàng.
        if (_vm.NavigationTarget != null)
        {
            _ = DrawNavigationRouteAsync();
        }

        _ = _vm.LoadAllPoisAsync().ContinueWith(t =>
        {
            if (t.Exception != null)
            {
                Console.WriteLine($"[MainPage] ❌ LoadAllPoisAsync error: {t.Exception}");
                System.Diagnostics.Debug.WriteLine($"[MainPage] LoadAllPoisAsync error: {t.Exception}");
            }

            if (!t.IsCompletedSuccessfully) return;

            Console.WriteLine($"[MainPage] ✓ LoadAllPoisAsync completed! AllPOIs.Count={_vm.AllPOIs.Count}");
            
            // Vẽ ngay tất cả pins lên bản đồ sau khi dữ liệu được tải
            MainThread.BeginInvokeOnMainThread(() =>
            {
                Console.WriteLine($"[MainPage] Calling UpdateZonePins from MainThread...");
                UpdateZonePins();
            });

            // Preload offline audio cache after POIs are loaded (background, non-blocking)
            if (_audioCache != null)
            {
                var ids = _vm.AllPOIs.Select(p => p.Id).ToList();
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
        });

        _ = _vm.StartTrackingAsync().ContinueWith(t =>
        {
            if (t.Exception != null)
                System.Diagnostics.Debug.WriteLine($"[MainPage] StartTrackingAsync error: {t.Exception}");
        }, TaskContinuationOptions.OnlyOnFaulted);
    }

    // ─── Wire component events ────────────────────────────────────────────────
    // Called once when page is loaded; subscribes to all ContentView public events.

    private void WireComponentEvents()
    {
        // Map tab overlays
        TabMapComponent.BackRequested      += OnBackClicked;
        TabMapComponent.CenterMapRequested += OnCenterMapClicked;
        TabMapComponent.SettingsRequested  += OnSettingsClicked;
        TabMapComponent.ZoomInRequested    += OnZoomInClicked;
        TabMapComponent.ZoomOutRequested   += OnZoomOutClicked;
        TabMapComponent.ResetDatabaseRequested += OnResetDatabaseClicked; // 🔄 DEBUG

        // Pin popup actions
        PinPopupComponent.PlayAudioRequested += OnPinPlayAudio;
        PinPopupComponent.NavigateRequested  += OnPinNavigate;
        PinPopupComponent.SaveRequested      += OnPinSave;
        PinPopupComponent.ViewDetailsRequested += OnPinViewDetails; // Xem thêm
        PinPopupComponent.CloseRequested     += OnPinClose;        // Đóng popup
    }

    private void OnPinClose(object? sender, EventArgs e)
    {
        _vm.IsPinPopupVisible = false;
        _vm.SelectedPinPOI    = null;
        PinPopupComponent.IsVisible = false;
    }

    // ─── ViewModel property changes ───────────────────────────────────────────

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.CurrentLat) ||
            e.PropertyName == nameof(MainViewModel.CurrentLon))
        {
            UpdateUserPin();
            if (_vm.NavigationTarget != null)
            {
                _ = DrawNavigationRouteAsync();
            }
        }
        else if (e.PropertyName == nameof(MainViewModel.ActiveZoneCount))
        {
            // Fires once after ActiveZones is fully populated (not N+1 times via CollectionChanged)
            UpdateZonePins();
        }
        else if (e.PropertyName == nameof(MainViewModel.NavigationTarget))
        {
            _ = DrawNavigationRouteAsync();
        }
        // IsApproaching: ApproachingBanner visibility is handled by binding inside TabTourView.
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _ = _tts.StopAsync();
    }
}
