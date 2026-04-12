using Microsoft.Maui.Storage;
using StreetFoodNarrator.App.Core.Services;
using StreetFoodNarrator.App.Helpers;
using StreetFoodNarrator.App.Views;

namespace StreetFoodNarrator.App;

public partial class App : Application
{
    private const string PREF_ONBOARDED = "has_onboarded";
    private const string PREF_LEGACY_ONBOARDED = "hasSeenOnboarding";
    private const string PREF_LAST_SYNC_TIME = "LastSyncTime";

    public App(LanguageService languageService, IRemoteLocalizationService remoteLocalizationService)
    {
        InitializeComponent();

        // Restore saved language first so AppStrings culture is correct.
        languageService.RestoreSavedLanguage();

        // Pull latest translation dictionary from backend (with offline cache fallback).
        _ = remoteLocalizationService.RefreshAsync(languageService.CurrentLanguage);

        // Keep translation dictionary synced when user changes language at runtime.
        LanguageService.LanguageChanged += (_, languageCode) =>
        {
            _ = remoteLocalizationService.RefreshAsync(languageCode);
        };

        PageAppearing += OnPageAppearing;
        UserPreferenceEffects.ApplyCurrentPreferences();
    }

    private void OnPageAppearing(object? sender, Page page)
    {
        UserPreferenceEffects.ApplyCurrentPreferences(page);
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var hasOnboarded = Preferences.Get(PREF_ONBOARDED, false);
        if (!hasOnboarded)
        {
            // Migrate legacy onboarding/session markers so returning users skip WelcomePage.
            var legacyOnboarded = Preferences.Get(PREF_LEGACY_ONBOARDED, false);
            var hasSyncedBefore = !string.IsNullOrWhiteSpace(Preferences.Get(PREF_LAST_SYNC_TIME, ""));
            if (legacyOnboarded || hasSyncedBefore)
            {
                hasOnboarded = true;
                Preferences.Set(PREF_ONBOARDED, true);
            }
        }

        var rootPage = hasOnboarded
            // Returning user: show lightweight startup loading immediately, then hand off to AppShell.
            ? (Page)new StartupLoadingPage()
            // First time user: show WelcomePage via AppShell.
            : new AppShell(isOnboarding: true);

        rootPage.BackgroundColor = Color.FromArgb("#0A1612");
        return new Window(rootPage);
    }

    /// <summary>Called by WelcomePage after user completes onboarding.</summary>
    public static void CompleteOnboarding()
    {
        Preferences.Set(PREF_ONBOARDED, true);
    }
}
