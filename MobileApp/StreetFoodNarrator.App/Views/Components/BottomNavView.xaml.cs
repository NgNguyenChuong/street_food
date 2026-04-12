// ─────────────────────────────────────────────────────────────────────────────
// BottomNavView.xaml.cs
// Quản lý nội bộ: cập nhật màu tab active/inactive + trượt indicator
// API công khai:
//   • event Action<int>? TabSwitchRequested  → MainPage.Nav.cs lắng nghe
//   • SetActiveTab(int index)                → MainPage.Nav.cs gọi sau khi xử lý
// ─────────────────────────────────────────────────────────────────────────────

using StreetFoodNarrator.App.Core.Services;
using StreetFoodNarrator.App.Helpers;
using StreetFoodNarrator.App.Resources.Strings;

namespace StreetFoodNarrator.App.Views.Components;

public partial class BottomNavView : ContentView
{
    // Sự kiện khi người dùng nhấn tab → MainPage.Nav.cs xử lý logic chuyển tab
    public event Action<int>? TabSwitchRequested;

    private int    _activeIndex    = 0;
    private double _itemWidth      = 0;
    private bool   _widthMeasured  = false;

    public BottomNavView()
    {
        InitializeComponent();
        ApplyLocalizedTexts();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        LanguageService.LanguageChanged -= OnLanguageChanged;
        LanguageService.LanguageChanged += OnLanguageChanged;
        ApplyLocalizedTexts();
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
        NavMapLabel.Text = AppStrings.Get("Nav_Map");
        NavTourLabel.Text = AppStrings.Get("Nav_Tour");
        NavMenuLabel.Text = AppStrings.Get("Nav_Menu");
        NavSavedLabel.Text = AppStrings.Get("Nav_Saved");
        NavSettingsLabel.Text = AppStrings.Get("Nav_Settings");
    }

    // ─── Tab tap handlers (forwarding events) ────────────────────────────────

    private void OnTabMapTapped(object sender, EventArgs e)
    {
        UserPreferenceEffects.PerformHapticClickIfEnabled();
        TabSwitchRequested?.Invoke(0);
    }

    private void OnTabTourTapped(object sender, EventArgs e)
    {
        UserPreferenceEffects.PerformHapticClickIfEnabled();
        TabSwitchRequested?.Invoke(1);
    }

    private void OnTabMenuTapped(object sender, EventArgs e)
    {
        UserPreferenceEffects.PerformHapticClickIfEnabled();
        TabSwitchRequested?.Invoke(2);
    }

    private void OnTabSavedTapped(object sender, EventArgs e)
    {
        UserPreferenceEffects.PerformHapticClickIfEnabled();
        TabSwitchRequested?.Invoke(3);
    }

    private void OnTabSettingsTapped(object sender, EventArgs e)
    {
        UserPreferenceEffects.PerformHapticClickIfEnabled();
        TabSwitchRequested?.Invoke(4);
    }

    // ─── Cập nhật giao diện tab active ───────────────────────────────────────

    /// <summary>Được gọi từ MainPage.Nav.cs sau khi xử lý chuyển tab xong.</summary>
    public void SetActiveTab(int index)
    {
        _activeIndex = index;

        var activeColor   = Microsoft.Maui.Graphics.Color.FromArgb("#22C55E");
        var inactiveColor = Microsoft.Maui.Graphics.Color.FromArgb("#6B7280");

        Label[] glyphs = [NavMapGlyph, NavTourGlyph, NavMenuGlyph, NavSavedGlyph, NavSettingsGlyph];
        Label[] labels = [NavMapLabel, NavTourLabel, NavMenuLabel, NavSavedLabel, NavSettingsLabel];

        for (int i = 0; i < glyphs.Length; i++)
        {
            bool isActive          = (i == index);
            glyphs[i].TextColor    = isActive ? activeColor : inactiveColor;
            labels[i].TextColor    = isActive ? activeColor : inactiveColor;
            labels[i].FontAttributes = isActive ? FontAttributes.Bold : FontAttributes.None;
        }

        SlideIndicator(index);
    }

    // ─── Indicator animation ─────────────────────────────────────────────────

    private void SlideIndicator(int index)
    {
        double navWidth = Width > 0 ? Width
            : DeviceDisplay.MainDisplayInfo.Width / DeviceDisplay.MainDisplayInfo.Density;
        _itemWidth = (navWidth - 32) / 5.0;
        _ = NavIndicator.TranslateToAsync(index * _itemWidth, 0, 220, Easing.CubicOut);
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (width > 0 && !_widthMeasured)
        {
            _widthMeasured          = true;
            _itemWidth              = (width - 32) / 5.0;
            NavIndicator.TranslationX = _activeIndex * _itemWidth;
        }
    }
}
