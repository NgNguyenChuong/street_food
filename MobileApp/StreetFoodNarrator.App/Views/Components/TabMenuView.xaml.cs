using StreetFoodNarrator.App.ViewModels;
using StreetFoodNarrator.App.Views;
using StreetFoodNarrator.App.Core.Services;
using StreetFoodNarrator.App.Resources.Strings;

namespace StreetFoodNarrator.App.Views.Components;

public partial class TabMenuView : ContentView
{
    public TabMenuView()
    {
        InitializeComponent();
        if (BindingContext == null)
            BindingContext = MauiProgram.Services.GetRequiredService<MainViewModel>();

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
        MainThread.BeginInvokeOnMainThread(() =>
        {
            ApplyLocalizedTexts();
            RefreshTourCardBindings();
        });
    }

    private void ApplyLocalizedTexts()
    {
        TourSearchBar.Placeholder = AppStrings.Get("TabMenu_SearchPlaceholder");
        OfflineTourNoticeLabel.Text = AppStrings.Get("TabMenu_OfflineNotice");
        EmptyTourLabel.Text = AppStrings.Get("TabMenu_EmptyTour");
    }

    private void RefreshTourCardBindings()
    {
        if (TourListView == null)
            return;

        var source = TourListView.ItemsSource;
        TourListView.ItemsSource = null;
        TourListView.ItemsSource = source;
    }

    private async void OnTourDetailClicked(object sender, EventArgs e)
    {
        if (sender is not BindableObject bindable || bindable.BindingContext is not MainViewModel.TourListItem tour)
            return;

        if (BindingContext is not MainViewModel vm)
            return;

        if (vm.IsTourLockedByFreeTrial(tour))
        {
            await PremiumTourPaywallPage.ShowAsync(Navigation, tour.Name, vm);
            return;
        }

        await Navigation.PushModalAsync(new TourDetailPopupPage(vm, tour), false);
    }

    private async void OnLockedTourTapped(object sender, TappedEventArgs e)
    {
        if (sender is not BindableObject bindable || bindable.BindingContext is not MainViewModel.TourListItem tour)
            return;

        var vm = BindingContext as MainViewModel;
        await PremiumTourPaywallPage.ShowAsync(Navigation, tour.Name, vm);
    }
}
