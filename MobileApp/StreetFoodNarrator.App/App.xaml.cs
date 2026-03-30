using Microsoft.Maui.Storage;
using StreetFoodNarrator.App.Core.Services;
using StreetFoodNarrator.App.Resources.Strings;
using StreetFoodNarrator.App.Views;

namespace StreetFoodNarrator.App;

public partial class App : Application
{
    private const string PREF_ONBOARDED = "has_onboarded";

    public App()
    {
        InitializeComponent();

        // Khôi phục ngôn ngữ đã lưu
        var languageService = new LanguageService();
        languageService.RestoreSavedLanguage();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var hasOnboarded = Preferences.Get(PREF_ONBOARDED, false);

        if (hasOnboarded)
        {
            // Returning user: skip WelcomePage, go directly to MainPage
            return new Window(new AppShell(isOnboarding: false));
        }

        // First time user: show WelcomePage via AppShell
        return new Window(new AppShell(isOnboarding: true));
    }

    /// <summary>Called by WelcomePage after user completes onboarding.</summary>
    public static void CompleteOnboarding()
    {
        Preferences.Set(PREF_ONBOARDED, true);
    }
}
