using Microsoft.Maui.Storage;
using StreetFoodNarrator.App.Core.Services;
using StreetFoodNarrator.App.Resources.Strings;
using StreetFoodNarrator.App.Views;

namespace StreetFoodNarrator.App;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
        
        // Khôi phục ngôn ngữ đã lưu
        var languageService = new LanguageService();
        languageService.RestoreSavedLanguage();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new AppShell());
    }
}
