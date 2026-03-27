using System.Collections.Generic;
using Microsoft.Maui.Controls;
using StreetFoodNarrator.App.Core.Models;
using StreetFoodNarrator.App.ViewModels;

namespace StreetFoodNarrator.App.Views.Components;

public partial class VirtualModeView : ContentView
{
    // ── Events ────────────────────────────────────────────────────────────────
    public event EventHandler? BackRequested;
    public event EventHandler? HeaderSettingsRequested;

    // ── Carousel / Swipe event (kept for MainPage compatibility) ──────────────
    public event EventHandler<POI>? POISwiped;

    // Audio events
    public event EventHandler? PlayPauseRequested;
    public event EventHandler? RewindRequested;
    public event EventHandler? ForwardRequested;

    // Navigation tab events
    public event EventHandler? ExploreNavRequested;
    public event EventHandler? SavedNavRequested;
    public event EventHandler? ProfileNavRequested;
    public event EventHandler? SettingsNavRequested;

    // POI events
    public event EventHandler<POI>? QueueItemTapped;
    public event EventHandler<POI>? QueueSeeMoreTapped;
    public event EventHandler? CurrentlyPlayingSeeMoreTapped;

    private MainViewModel? _boundVm;

    public VirtualModeView()
    {
        InitializeComponent();
        BindingContextChanged += OnBindingContextChanged;
    }

    private void OnBindingContextChanged(object? sender, EventArgs e)
    {
        if (_boundVm != null)
        {
            _boundVm.PropertyChanged -= OnViewModelPropertyChanged;
            _boundVm = null;
        }

        if (BindingContext is MainViewModel vm)
        {
            _boundVm = vm;
            _boundVm.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // Respond to any VM property changes if needed
    }

    // ── Header Actions ────────────────────────────────────────────────────────

    private void OnBackTapped(object? sender, EventArgs e)
        => BackRequested?.Invoke(this, EventArgs.Empty);

    private void OnHeaderSettingsTapped(object? sender, EventArgs e)
        => HeaderSettingsRequested?.Invoke(this, EventArgs.Empty);

    // ── Audio Controls ───────────────────────────────────────────────────────

    private void OnPlayPauseTapped(object? sender, EventArgs e)
        => PlayPauseRequested?.Invoke(this, EventArgs.Empty);

    private void OnRewindTapped(object? sender, EventArgs e)
        => RewindRequested?.Invoke(this, EventArgs.Empty);

    private void OnForwardTapped(object? sender, EventArgs e)
        => ForwardRequested?.Invoke(this, EventArgs.Empty);

    // ── Currently Playing Hero Card ──────────────────────────────────────────

    private void OnCurrentlyPlayingSeeMoreTapped(object? sender, EventArgs e)
        => CurrentlyPlayingSeeMoreTapped?.Invoke(this, EventArgs.Empty);

    // ── Queue Items ───────────────────────────────────────────────────────────

    private void OnQueueItemTapped(object? sender, TappedEventArgs e)
    {
        if (sender is View view && view.BindingContext is POI poi)
            QueueItemTapped?.Invoke(this, poi);
    }

    private void OnQueueSeeMoreTapped(object? sender, TappedEventArgs e)
    {
        if (sender is View view && view.BindingContext is POI poi)
            QueueSeeMoreTapped?.Invoke(this, poi);
    }

    // ── Bottom Navigation ────────────────────────────────────────────────────

    private void OnExploreNavTapped(object? sender, EventArgs e)
        => ExploreNavRequested?.Invoke(this, EventArgs.Empty);

    private void OnSavedNavTapped(object? sender, EventArgs e)
        => SavedNavRequested?.Invoke(this, EventArgs.Empty);

    private void OnProfileNavTapped(object? sender, EventArgs e)
        => ProfileNavRequested?.Invoke(this, EventArgs.Empty);

    private void OnSettingsNavTapped(object? sender, EventArgs e)
        => SettingsNavRequested?.Invoke(this, EventArgs.Empty);

    // ── Map centering (kept for MainPage compatibility) ──────────────────────
    public void CenterVirtualMap(double lat, double lon, IEnumerable<POI>? allPois = null, POI? primaryPoi = null)
    {
        // Deprecated: map tab removed from new Journal UI
    }
}
