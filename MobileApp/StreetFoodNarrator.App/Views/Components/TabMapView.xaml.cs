// ─────────────────────────────────────────────────────────────────────────────
// TabMapView.xaml.cs
// Explore / Map Tab — Redesigned "Midnight Alchemist" theme
// ─────────────────────────────────────────────────────────────────────────────

using Microsoft.Maui.Controls;
using StreetFoodNarrator.App.Core.Models;
using StreetFoodNarrator.App.ViewModels;

namespace StreetFoodNarrator.App.Views.Components;

public partial class TabMapView : ContentView
{
    // ── Navigation events ─────────────────────────────────────────────────
    public event EventHandler? BackRequested;
    public event EventHandler? ExploreNavRequested;
    public event EventHandler? SavedNavRequested;
    public event EventHandler? ProfileNavRequested;

    // ── Map controls ──────────────────────────────────────────────────────
    public event EventHandler? SettingsRequested;           // legacy alias for FilterRequested
    public event EventHandler? FilterRequested;
    public event EventHandler? DisableVirtualTourRequested;
    public event EventHandler? CenterMapRequested;
    public event EventHandler? ZoomInRequested;
    public event EventHandler? ZoomOutRequested;

    // ── POI card actions ───────────────────────────────────────────────────
    public event EventHandler<POI>? PrevPoiRequested;
    public event EventHandler<POI>? NextPoiRequested;
    public event EventHandler<POI>? ViewDetailRequested;
    public event EventHandler<POI>? LikeRequested;           // fires when user taps like/save button

    // ── Filter chips ──────────────────────────────────────────────────────
    public event EventHandler<string>? CategorySelected;

    // ── Search ───────────────────────────────────────────────────────────
    public event EventHandler<TextChangedEventArgs>? SearchTextChanged;
    public event EventHandler? ClearSearchRequested;
    public event EventHandler<POI>? SuggestionSelected;

    // ── Debug ─────────────────────────────────────────────────────────────
    public event EventHandler? ResetDatabaseRequested;

    // MDI icon codes
private const string IconHeartOutline = "\U000F1437";
private const string IconHeartFilled  = "\U000F1438";

    public TabMapView()
    {
        InitializeComponent();

        // Update like icon when CurrentPOI changes
        if (BindingContext is MainViewModel vm)
        {
            vm.PropertyChanged += OnVmPropertyChanged;
        }
        Loaded += (_, _) =>
        {
            if (BindingContext is MainViewModel vm2)
            {
                vm2.PropertyChanged += OnVmPropertyChanged;
            }
        };
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.CurrentPOI) ||
            e.PropertyName == nameof(MainViewModel.SelectedPinPOI))
        {
            RefreshLikeIcon();
        }
    }

    /// <summary>Updates the Like icon to filled/unfilled based on CurrentPOI.IsLikedByUser.</summary>
    public void RefreshLikeIcon()
    {
        try
        {
            if (LikeIcon == null) return;

            if (BindingContext is MainViewModel vm && vm.CurrentPOI != null)
            {
                LikeIcon.Text = vm.CurrentPOI.IsLikedByUser
                    ? IconHeartFilled
                    : IconHeartOutline;
                LikeIcon.TextColor = vm.CurrentPOI.IsLikedByUser
                    ? Color.FromArgb("#6BFF8F")
                    : Color.FromArgb("#6BFF8F"); // same color, different icon
            }
            else
            {
                LikeIcon.Text = IconHeartOutline;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TabMapView] RefreshLikeIcon error: {ex.Message}");
        }
    }

    // ── Navigation ─────────────────────────────────────────────────────────

    private void OnBackTapped(object sender, EventArgs e)
        => BackRequested?.Invoke(this, e);

    private void OnExploreNavTapped(object sender, EventArgs e)
        => ExploreNavRequested?.Invoke(this, e);

    private void OnSavedNavTapped(object sender, EventArgs e)
        => SavedNavRequested?.Invoke(this, e);

    private void OnProfileNavTapped(object sender, EventArgs e)
        => ProfileNavRequested?.Invoke(this, e);

    // ── Map Controls ───────────────────────────────────────────────────────

    private void OnFilterTapped(object sender, EventArgs e)
    {
        FilterRequested?.Invoke(this, e);
        SettingsRequested?.Invoke(this, e);
    }

    private void OnCenterMapTapped(object sender, EventArgs e)
        => CenterMapRequested?.Invoke(this, e);

    private void OnZoomInTapped(object sender, EventArgs e)
        => ZoomInRequested?.Invoke(this, e);

    private void OnZoomOutTapped(object sender, EventArgs e)
        => ZoomOutRequested?.Invoke(this, e);

    private void OnDisableVirtualTourTapped(object sender, EventArgs e)
        => DisableVirtualTourRequested?.Invoke(this, e);

    // ── POI Card ──────────────────────────────────────────────────────────

    private void OnPrevPoiTapped(object sender, EventArgs e)
    {
        if (BindingContext is MainViewModel vm && vm.SelectedPinPOI != null)
            PrevPoiRequested?.Invoke(this, vm.SelectedPinPOI);
    }

    private void OnNextPoiTapped(object sender, EventArgs e)
    {
        if (BindingContext is MainViewModel vm && vm.SelectedPinPOI != null)
            NextPoiRequested?.Invoke(this, vm.SelectedPinPOI);
    }

    private void OnViewDetailTapped(object sender, EventArgs e)
    {
        if (BindingContext is MainViewModel vm && vm.SelectedPinPOI != null)
            ViewDetailRequested?.Invoke(this, vm.SelectedPinPOI);
    }

    /// <summary>
    /// Fires LikeRequested with the CurrentPOI so MainPage can toggle the save state.
    /// Refreshes the like icon after the event is handled.
    /// </summary>
    private void OnLikeTapped(object sender, EventArgs e)
    {
        if (BindingContext is MainViewModel vm && vm.CurrentPOI != null)
        {
            LikeRequested?.Invoke(this, vm.CurrentPOI);
            // Refresh icon after state change
            Dispatcher.Dispatch(RefreshLikeIcon);
        }
    }

    // ── Filter Chips ──────────────────────────────────────────────────────

    /// <summary>
    /// Taps a category chip. Extracts category string from the Border's BindingContext.
    /// Highlights the selected chip and fires CategorySelected.
    /// </summary>
    private void OnCategoryChipTapped(object sender, EventArgs e)
    {
        if (sender is View border && border.BindingContext is string category)
        {
            HighlightSelectedChip(category);
            CategorySelected?.Invoke(this, category);
        }
    }

    /// <summary>
    /// Updates chip background colors: selected chip = green, others = default.
    /// Called after a category is selected.
    /// </summary>
    public void HighlightSelectedChip(string selectedCategory)
    {
        if (FilterChipsContainer == null) return;

        foreach (var child in FilterChipsContainer.Children)
        {
            if (child is Border chip && chip.BindingContext is string chipCat)
            {
                if (chipCat == selectedCategory)
                {
                    // Selected: green background
                    chip.BackgroundColor = Color.FromArgb("#22C55E");
                    if (chip.Content is Label lbl)
                        lbl.TextColor = Color.FromArgb("#003915");
                }
                else
                {
                    // Default: dark surface
                    chip.BackgroundColor = Color.FromArgb("#2B3733");
                    if (chip.Content is Label lbl)
                        lbl.TextColor = Color.FromArgb("#BCCBB9");
                }
            }
        }
    }

    // ── Search ─────────────────────────────────────────────────────────────

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        => SearchTextChanged?.Invoke(this, e);

    private void OnClearSearchTapped(object sender, EventArgs e)
    {
        SearchEntry.Text = "";
        ClearSearchRequested?.Invoke(this, e);
    }

    private void OnSuggestionTapped(object sender, EventArgs e)
    {
        if (sender is View v && v.BindingContext is POI poi)
        {
            SuggestionsDropdown.IsVisible = false;
            SuggestionSelected?.Invoke(this, poi);
        }
    }

    // ── Public helpers ─────────────────────────────────────────────────────

    public void ShowSuggestions(IEnumerable<POI> suggestions)
    {
        try
        {
            if (SuggestionsList != null)
                SuggestionsList.ItemsSource = null;

            if (SuggestionsDropdown != null)
                SuggestionsDropdown.IsVisible = false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TabMapView] ShowSuggestions error: {ex.Message}");
        }
    }

    public void HideSuggestions()
    {
        try
        {
            if (SuggestionsList != null)
                SuggestionsList.ItemsSource = null;

            if (SuggestionsDropdown != null)
                SuggestionsDropdown.IsVisible = false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TabMapView] HideSuggestions error: {ex.Message}");
        }
    }

    /// <summary>
    /// Call this after the category list is populated so the first chip
    /// ("Tất cả") gets the selected highlight.
    /// </summary>
    public void InitializeChips(string selectedCategory = "Tất cả")
    {
        try
        {
            Dispatcher.Dispatch(() => HighlightSelectedChip(selectedCategory));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TabMapView] InitializeChips error: {ex.Message}");
        }
    }
}
