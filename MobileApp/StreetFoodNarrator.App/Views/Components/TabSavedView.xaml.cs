// ─────────────────────────────────────────────────────────────────────────────
// TabSavedView.xaml.cs
// Tab 3: Đã lưu – hiển thị danh sách POI đã thích
// Card tap → POIDetailPage, Heart tap → bỏ thích (toggle)
// ─────────────────────────────────────────────────────────────────────────────

using StreetFoodNarrator.App.Core.Models;
using StreetFoodNarrator.App.ViewModels;
using StreetFoodNarrator.App.Views;

namespace StreetFoodNarrator.App.Views.Components;

public partial class TabSavedView : ContentView
{
    public TabSavedView()
    {
        InitializeComponent();
        if (BindingContext == null)
            BindingContext = MauiProgram.Services.GetRequiredService<MainViewModel>();
    }

    private async void OnCardSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Deselect immediately so user can tap the same card again
        SavedList.SelectedItem = null;

        if (e.CurrentSelection.FirstOrDefault() is POI poi)
        {
            await Shell.Current.Navigation.PushModalAsync(new POIDetailPage(poi, keepCurrentAudio: true));
        }
    }

    private void OnHeartTapped(object sender, EventArgs e)
    {
        if (sender is VisualElement el && el.BindingContext is POI poi)
        {
            var vm = BindingContext as MainViewModel;
            vm?.ToggleSavePOICommand.Execute(poi);
        }
    }

    private async void OnDetailTapped(object sender, EventArgs e)
    {
        if (sender is BindableObject bindable && bindable.BindingContext is POI poi)
            await Shell.Current.Navigation.PushModalAsync(new POIDetailPage(poi, keepCurrentAudio: true));
    }
}
