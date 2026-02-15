using Microsoft.Maui.Storage;
using StreetFoodNarrator.App.Resources.Strings;
using StreetFoodNarrator.App.Views;

namespace StreetFoodNarrator.App;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
        AppStrings.SetCulture(Preferences.Get(AppConfig.LanguagePrefKey, "vi"));
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var page = new WelcomePage();
        return new Window(new NavigationPage(page));
    }
}
