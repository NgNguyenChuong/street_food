using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Storage;
using StreetFoodNarrator.App.Resources.Strings;
using StreetFoodNarrator.App.ViewModels;

namespace StreetFoodNarrator.App.Views;

/// <summary>
/// Lightweight startup page that appears immediately so users never see a blank screen
/// while main data and shell are warming up.
/// </summary>
public sealed class StartupLoadingPage : ContentPage
{
    private bool _isBootstrapping;
    private readonly Label _statusLabel;

    public StartupLoadingPage()
    {
        BackgroundColor = Color.FromArgb("#0A1612");

        _statusLabel = new Label
        {
            Text = AppStrings.Get("Main_Status_LoadingData"),
            FontSize = 14,
            TextColor = Color.FromArgb("#9FB0A8"),
            HorizontalOptions = LayoutOptions.Center
        };

        Content = new Grid
        {
            Children =
            {
                new VerticalStackLayout
                {
                    Spacing = 20,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center,
                    Children =
                    {
                        new Label
                        {
                            Text = "Street Food Narrator",
                            FontSize = 24,
                            FontAttributes = FontAttributes.Bold,
                            TextColor = Color.FromArgb("#4BE277"),
                            HorizontalOptions = LayoutOptions.Center
                        },
                        _statusLabel,
                        new ActivityIndicator
                        {
                            IsRunning = true,
                            Color = Color.FromArgb("#4BE277"),
                            WidthRequest = 36,
                            HeightRequest = 36,
                            HorizontalOptions = LayoutOptions.Center
                        }
                    }
                }
            }
        };
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_isBootstrapping)
            return;

        _isBootstrapping = true;
        _ = BootstrapAsync();
    }

    private async Task BootstrapAsync()
    {
        try
        {
            SetStatus(AppStrings.Get("Main_Status_LoadingData"));

            var vm = MauiProgram.Services.GetRequiredService<MainViewModel>();
            await vm.LoadAllPoisAsync(forceSyncNow: false);
            vm.RefreshExploreState();

            var hasData = vm.AllPOIs.Count > 0;
            Preferences.Set(AppConfig.MainPagePrewarmReadyKey, hasData);
            if (hasData)
                Preferences.Set(AppConfig.MainPagePrewarmAtUtcKey, DateTime.UtcNow.ToString("O"));
            else
                Preferences.Remove(AppConfig.MainPagePrewarmAtUtcKey);

            SetStatus(AppStrings.Get("Main_Status_Finishing"));
        }
        catch (Exception ex)
        {
            Preferences.Set(AppConfig.MainPagePrewarmReadyKey, false);
            Preferences.Remove(AppConfig.MainPagePrewarmAtUtcKey);
            System.Diagnostics.Debug.WriteLine($"[StartupLoadingPage] BootstrapAsync error: {ex}");
        }
        finally
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                var window = Application.Current?.Windows.FirstOrDefault();
                if (window == null)
                    return;

                try
                {
                    var shell = new AppShell(isOnboarding: false);
                    window.Page = shell;
                }
                catch (Exception ex)
                {
                    App.AppendStartupErrorToLog(ex, "StartupLoadingPage.Finally");
                    window.Page = App.BuildStartupRecoveryPage(ex);
                }
            });
        }
    }

    private void SetStatus(string status)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _statusLabel.Text = status;
        });
    }
}
