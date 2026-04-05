// ─────────────────────────────────────────────────────────────────────────────
// PinPopupView.xaml.cs
// Popup khi nhấn vào pin trên bản đồ
// Sự kiện: PlayAudioRequested, NavigateRequested, SaveRequested
// → MainPage.PinPopup.cs lắng nghe và xử lý
// ─────────────────────────────────────────────────────────────────────────────

namespace StreetFoodNarrator.App.Views.Components;

using StreetFoodNarrator.App.Core.Services;
using StreetFoodNarrator.App.Resources.Strings;

public partial class PinPopupView : ContentView
{
    // ── Sự kiện ra ngoài ─────────────────────────────────────────────────────
    public event EventHandler? PlayAudioRequested;
    public event EventHandler? NavigateRequested;
    public event EventHandler? SaveRequested;
    public event EventHandler? ViewDetailsRequested; // Xem thêm chi tiết POI
    public event EventHandler? CloseRequested;      // Đóng popup

    public PinPopupView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        ApplyLocalizedTexts();
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        LanguageService.LanguageChanged -= OnLanguageChanged;
        LanguageService.LanguageChanged += OnLanguageChanged;
    }

    private void OnUnloaded(object? sender, EventArgs e)
    {
        LanguageService.LanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(object? sender, string languageCode)
    {
        MainThread.BeginInvokeOnMainThread(ApplyLocalizedTexts);
    }

    private void ApplyLocalizedTexts()
    {
        ViewDetailActionLabel.Text = AppStrings.Get("Map_Action_ViewDetail_Arrow");
    }

    // ── Event forwarders ─────────────────────────────────────────────────────

    private void OnPlayAudioTapped(object sender, EventArgs e) => PlayAudioRequested?.Invoke(this, e);
    private void OnNavigateTapped(object sender, EventArgs e)  => NavigateRequested?.Invoke(this, e);
    private void OnSaveTapped(object sender, EventArgs e)      => SaveRequested?.Invoke(this, e);
    private void OnCloseTapped(object sender, EventArgs e)     => CloseRequested?.Invoke(this, e);
    
    private void OnViewDetailsTapped(object sender, EventArgs e)
    {
        Console.WriteLine("[PinPopupView] OnViewDetailsTapped - Event triggered!");
        ViewDetailsRequested?.Invoke(this, e);
    }
}
