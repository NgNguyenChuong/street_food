using System.Runtime.CompilerServices;
using StreetFoodNarrator.App.Core.Services;
using StreetFoodNarrator.App.Resources.Strings;

namespace StreetFoodNarrator.App.Views.Components;

public partial class FloatingBottomNav : ContentView
{
    private bool _isNavigating;
    private DateTime _lastNavigateAtUtc = DateTime.MinValue;

    public static readonly BindableProperty ActiveTabProperty = BindableProperty.Create(
        nameof(ActiveTab), typeof(string), typeof(FloatingBottomNav), "Explore",
        propertyChanged: OnActiveTabChanged);

    public string ActiveTab
    {
        get => (string)GetValue(ActiveTabProperty);
        set => SetValue(ActiveTabProperty, value);
    }

    public FloatingBottomNav()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        ApplyLocalizedTexts();
        UpdateUI();
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
        ExploreLabel.Text = AppStrings.Get("Nav_Explore");
        SavedLabel.Text = AppStrings.Get("Nav_Library");
        SettingsLabel.Text = AppStrings.Get("Nav_Settings");
    }

    private static void OnActiveTabChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is FloatingBottomNav nav)
        {
            nav.UpdateUI();
        }
    }

    private void UpdateUI()
    {
        // Explore settings
        bool isExplore = ActiveTab == "Explore" || ActiveTab == "MainPage";
        ExploreIcon.TextColor = isExplore ? Color.FromArgb("#4BE277") : Color.FromArgb("#92A89D");
        ExploreLabel.TextColor = isExplore ? Color.FromArgb("#4BE277") : Color.FromArgb("#92A89D");
        ExploreIndicator.IsVisible = isExplore;

        // Saved settings
        bool isSaved = ActiveTab == "Saved" || ActiveTab == "SavedPage";
        SavedIcon.TextColor = isSaved ? Color.FromArgb("#4BE277") : Color.FromArgb("#92A89D");
        SavedLabel.TextColor = isSaved ? Color.FromArgb("#4BE277") : Color.FromArgb("#92A89D");
        SavedIndicator.IsVisible = isSaved;

        // Settings tab
        bool isSettings = ActiveTab == "Settings" || ActiveTab == "SettingsPage";
        SettingsIcon.TextColor = isSettings ? Color.FromArgb("#4BE277") : Color.FromArgb("#92A89D");
        SettingsLabel.TextColor = isSettings ? Color.FromArgb("#4BE277") : Color.FromArgb("#92A89D");
        SettingsIndicator.IsVisible = isSettings;
    }

    private async void OnExploreTapped(object sender, EventArgs e)
    {
        await NavigateToAsync("//MapPage", ActiveTab == "Explore" || ActiveTab == "MainPage");
    }

    private async void OnSavedTapped(object sender, EventArgs e)
    {
        await NavigateToAsync("//SavedPage", ActiveTab == "Saved" || ActiveTab == "SavedPage");
    }

    private async void OnSettingsTapped(object sender, EventArgs e)
    {
        await NavigateToAsync("//SettingsPage", ActiveTab == "Settings" || ActiveTab == "SettingsPage");
    }

    private async Task NavigateToAsync(string route, bool alreadyActive)
    {
        if (alreadyActive || _isNavigating || Shell.Current == null)
            return;

        if ((DateTime.UtcNow - _lastNavigateAtUtc).TotalMilliseconds < 250)
            return;

        _isNavigating = true;
        _lastNavigateAtUtc = DateTime.UtcNow;

        try
        {
            await Shell.Current.GoToAsync(route, false);
        }
        finally
        {
            _isNavigating = false;
        }
    }
}
