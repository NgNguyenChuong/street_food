using StreetFoodNarrator.App.ViewModels;
using StreetFoodNarrator.App.Views;

namespace StreetFoodNarrator.App.Views.Components;

public partial class TabMenuView : ContentView
{
    public TabMenuView()
    {
        InitializeComponent();
        if (BindingContext == null)
            BindingContext = MauiProgram.Services.GetRequiredService<MainViewModel>();
    }

    private async void OnTourDetailClicked(object sender, EventArgs e)
    {
        if (sender is not BindableObject bindable || bindable.BindingContext is not MainViewModel.TourListItem tour)
            return;

        if (BindingContext is not MainViewModel vm)
            return;

        await Navigation.PushModalAsync(new TourDetailPopupPage(vm, tour), false);
    }
}
