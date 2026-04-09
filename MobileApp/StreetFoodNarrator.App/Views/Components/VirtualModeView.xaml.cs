using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using StreetFoodNarrator.App.Core.Models;
using StreetFoodNarrator.App.Core.Services;
using StreetFoodNarrator.App.Helpers;
using StreetFoodNarrator.App.Resources.Strings;
using StreetFoodNarrator.App.ViewModels;

namespace StreetFoodNarrator.App.Views.Components;

public partial class VirtualModeView : ContentView
{
    public string HeaderTitleText => AppStrings.Get("Virtual_Header_Title");
    public string ProximityBannerText => AppStrings.Get("Virtual_Proximity_Banner");
    public string TourTitleText => AppStrings.Get("Virtual_Tour_Title");
    public string ProgressLabelText => AppStrings.Get("Virtual_Progress_Label");
    public string CurrentlyPlayingHeaderText => AppStrings.Get("Virtual_CurrentlyPlaying_Header");
    public string NowPlayingBadgeText => AppStrings.Get("Virtual_NowPlaying_Badge");
    public string LegendaryRecipeText => AppStrings.Get("Virtual_Legendary_Recipe");
    public string SeeMoreText => AppStrings.Get("Virtual_SeeMore");
    public string QueueHeaderText => AppStrings.Get("Virtual_Queue_Header");
    public string QueueItemHeaderText => AppStrings.Get("Virtual_Queue_Item_Header");
    public string QueueEmptyText => AppStrings.Get("Virtual_Queue_Empty");
    public string AudioStreetSuffixText => AppStrings.Get("Virtual_Audio_StreetSuffix");
    public string HeaderLanguageText => LanguageSwitcher.GetHeaderLabel(_languageService?.CurrentLanguage ?? "vi");
    public string BottomExploreText => AppStrings.Get("Nav_Explore");
    public string BottomLibraryText => AppStrings.Get("Nav_Library");
    public string BottomSettingsText => AppStrings.Get("Nav_Settings");

    // Events
    public event EventHandler? BackRequested;
    public event EventHandler? HeaderSettingsRequested;

    // Carousel / Swipe event (kept for MainPage compatibility)
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
    public event EventHandler? CurrentlyPlayingSeeMoreTapped;

    private MainViewModel? _boundVm;
    private LanguageService? _languageService;

    public VirtualModeView()
    {
        _languageService = MauiProgram.Services.GetService<LanguageService>();
        InitializeComponent();
        BindingContextChanged += OnBindingContextChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        RefreshLocalizedTexts();
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
        MainThread.BeginInvokeOnMainThread(RefreshLocalizedTexts);
    }

    private void RefreshLocalizedTexts()
    {
        OnPropertyChanged(nameof(HeaderTitleText));
        OnPropertyChanged(nameof(ProximityBannerText));
        OnPropertyChanged(nameof(TourTitleText));
        OnPropertyChanged(nameof(ProgressLabelText));
        OnPropertyChanged(nameof(CurrentlyPlayingHeaderText));
        OnPropertyChanged(nameof(NowPlayingBadgeText));
        OnPropertyChanged(nameof(LegendaryRecipeText));
        OnPropertyChanged(nameof(SeeMoreText));
        OnPropertyChanged(nameof(QueueHeaderText));
        OnPropertyChanged(nameof(QueueItemHeaderText));
        OnPropertyChanged(nameof(QueueEmptyText));
        OnPropertyChanged(nameof(AudioStreetSuffixText));
        OnPropertyChanged(nameof(HeaderLanguageText));
        OnPropertyChanged(nameof(BottomExploreText));
        OnPropertyChanged(nameof(BottomLibraryText));
        OnPropertyChanged(nameof(BottomSettingsText));
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

    // Header actions
    private void OnBackTapped(object? sender, EventArgs e)
        => BackRequested?.Invoke(this, EventArgs.Empty);

    private async void OnHeaderLanguageTapped(object? sender, EventArgs e)
    {
        var hostPage = ResolveHostPage();
        if (hostPage == null)
            return;

        _languageService ??= MauiProgram.Services.GetService<LanguageService>();
        if (_languageService == null)
            return;

        await LanguageSwitcher.ShowLanguagePickerAsync(hostPage, _languageService);
    }

    private void OnHeaderSettingsTapped(object? sender, EventArgs e)
        => HeaderSettingsRequested?.Invoke(this, EventArgs.Empty);

    // Audio controls
    private void OnPlayPauseTapped(object? sender, EventArgs e)
        => PlayPauseRequested?.Invoke(this, EventArgs.Empty);

    private void OnRewindTapped(object? sender, EventArgs e)
        => RewindRequested?.Invoke(this, EventArgs.Empty);

    private void OnForwardTapped(object? sender, EventArgs e)
        => ForwardRequested?.Invoke(this, EventArgs.Empty);

    // Currently playing hero card
    private void OnCurrentlyPlayingSeeMoreTapped(object? sender, EventArgs e)
        => CurrentlyPlayingSeeMoreTapped?.Invoke(this, EventArgs.Empty);

    // Queue items
    private void OnQueueItemTapped(object? sender, TappedEventArgs e)
    {
        if (sender is View view && view.BindingContext is POI poi)
            QueueItemTapped?.Invoke(this, poi);
    }

    // Bottom navigation
    private void OnExploreNavTapped(object? sender, EventArgs e)
        => ExploreNavRequested?.Invoke(this, EventArgs.Empty);

    private void OnSavedNavTapped(object? sender, EventArgs e)
        => SavedNavRequested?.Invoke(this, EventArgs.Empty);

    private void OnProfileNavTapped(object? sender, EventArgs e)
        => ProfileNavRequested?.Invoke(this, EventArgs.Empty);

    private void OnSettingsNavTapped(object? sender, EventArgs e)
        => SettingsNavRequested?.Invoke(this, EventArgs.Empty);

    private Page? ResolveHostPage()
    {
        Element? current = this;
        while (current != null)
        {
            if (current is Page page)
                return page;

            current = current.Parent;
        }

        return Shell.Current?.CurrentPage;
    }

    // Map centering (kept for MainPage compatibility)
    public void CenterVirtualMap(double lat, double lon, IEnumerable<POI>? allPois = null, POI? primaryPoi = null)
    {
        // Deprecated: map tab removed from new Journal UI
    }
}
