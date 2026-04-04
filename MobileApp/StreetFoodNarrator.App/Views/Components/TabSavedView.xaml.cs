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
    private bool _showingSavedTours = true;

    public TabSavedView()
    {
        InitializeComponent();
        if (BindingContext == null)
            BindingContext = MauiProgram.Services.GetRequiredService<MainViewModel>();

        ApplySavedSegmentVisualState();
    }

    private async void OnCardSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Deselect immediately so user can tap the same card again
        SavedPoiList.SelectedItem = null;

        if (e.CurrentSelection.FirstOrDefault() is POI poi)
        {
            await Shell.Current.Navigation.PushModalAsync(new POIDetailPage(poi, keepCurrentAudio: true), false);
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

    private void OnSavedTourSegmentTapped(object sender, EventArgs e)
    {
        if (_showingSavedTours)
            return;

        _showingSavedTours = true;
        ApplySavedSegmentVisualState();
    }

    private void OnSavedPoiSegmentTapped(object sender, EventArgs e)
    {
        if (!_showingSavedTours)
            return;

        _showingSavedTours = false;
        ApplySavedSegmentVisualState();
    }

    private void ApplySavedSegmentVisualState()
    {
        SavedTourList.IsVisible = _showingSavedTours;
        SavedPoiList.IsVisible = !_showingSavedTours;

        SavedTourSegment.BackgroundColor = _showingSavedTours
            ? Color.FromArgb("#F5A623")
            : Color.FromArgb("#1C3024");
        SavedTourLabel.TextColor = _showingSavedTours
            ? Colors.White
            : Color.FromArgb("#6B7280");
        SavedTourLabel.FontAttributes = _showingSavedTours
            ? FontAttributes.Bold
            : FontAttributes.None;

        SavedPoiSegment.BackgroundColor = !_showingSavedTours
            ? Color.FromArgb("#F5A623")
            : Color.FromArgb("#1C3024");
        SavedPoiLabel.TextColor = !_showingSavedTours
            ? Colors.White
            : Color.FromArgb("#6B7280");
        SavedPoiLabel.FontAttributes = !_showingSavedTours
            ? FontAttributes.Bold
            : FontAttributes.None;
    }

    private async void OnSavedTourDetailClicked(object sender, EventArgs e)
    {
        if (sender is BindableObject bindable && bindable.BindingContext is MainViewModel.TourListItem tour)
        {
            var vm = BindingContext as MainViewModel;
            if (vm != null)
                await Navigation.PushModalAsync(new TourDetailPopupPage(vm, tour), false);
        }
    }

    private async void OnSavedTourStartClicked(object sender, EventArgs e)
    {
        if (sender is BindableObject bindable && bindable.BindingContext is MainViewModel.TourListItem tour)
        {
            var vm = BindingContext as MainViewModel;
            if (vm != null)
                await vm.StartTourNowCommand.ExecuteAsync(tour);
        }
    }

    private void OnSavedTourHeartTapped(object sender, EventArgs e)
    {
        if (sender is BindableObject bindable && bindable.BindingContext is MainViewModel.TourListItem tour)
        {
            var vm = BindingContext as MainViewModel;
            vm?.ToggleSaveTourCommand.Execute(tour);
        }
    }

    private async void OnDetailTapped(object sender, EventArgs e)
    {
        if (sender is BindableObject bindable && bindable.BindingContext is POI poi)
            await Shell.Current.Navigation.PushModalAsync(new POIDetailPage(poi, keepCurrentAudio: true), false);
    }
}
