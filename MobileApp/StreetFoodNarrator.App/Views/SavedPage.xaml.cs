using StreetFoodNarrator.App.ViewModels;
using StreetFoodNarrator.App.Core.Services;
using StreetFoodNarrator.App.Resources.Strings;

namespace StreetFoodNarrator.App.Views;

public partial class SavedPage : ContentPage
{
    public const string OpenBrowseSegmentOnNextAppearKey = "saved_open_browse_segment_once";

    private readonly MainViewModel _vm;
    private bool _isBrowseSegment = false;
    private StreetFoodNarrator.App.Views.Components.TabMenuView? _browseContent;
    private bool _isDataWarmupRunning;
    private DateTime _lastDataWarmupUtc = DateTime.MinValue;
    private static readonly TimeSpan DataWarmupCooldown = TimeSpan.FromSeconds(8);

    public SavedPage()
    {
        InitializeComponent();
        _vm = MauiProgram.Services.GetRequiredService<MainViewModel>();
        BindingContext = _vm;
        ApplyLocalizedTexts();
        
        Console.WriteLine("[SavedPage] Initialized");
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        LanguageService.LanguageChanged -= OnLanguageChanged;
        LanguageService.LanguageChanged += OnLanguageChanged;
        Console.WriteLine("[SavedPage] OnAppearing");
        _vm.RefreshOfflineBannerSession();
        _ = EnsureSavedDataWarmupAsync();

        if (Preferences.Get(OpenBrowseSegmentOnNextAppearKey, false))
        {
            Preferences.Set(OpenBrowseSegmentOnNextAppearKey, false);
            SwitchToBrowseSegment();
        }

        ApplyLocalizedTexts();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        LanguageService.LanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(object? sender, string languageCode)
    {
        MainThread.BeginInvokeOnMainThread(ApplyLocalizedTexts);
    }

    private void ApplyLocalizedTexts()
    {
        LabelBrowse.Text = AppStrings.Get("Saved_Tab_Tours");
        LabelSaved.Text = AppStrings.Get("Saved_Tab_Saved");
        OfflineBannerLabel.Text = AppStrings.Get("Offline_Banner_Short");
    }

    private void OnSegmentBrowseTapped(object sender, EventArgs e)
    {
        SwitchToBrowseSegment();
        _ = EnsureSavedDataWarmupAsync();
    }

    private void OnSegmentSavedTapped(object sender, EventArgs e)
    {
        SwitchToSavedSegment();
    }

    private void SwitchToBrowseSegment()
    {
        if (_isBrowseSegment)
            return;

        EnsureBrowseContentCreated();

        _isBrowseSegment = true;
        BrowseHost.IsVisible = true;
        SavedContent.IsVisible = false;

        var browseBorder = LabelBrowse.Parent as Border;
        var savedBorder = LabelSaved.Parent as Border;
        if (browseBorder != null)
            browseBorder.BackgroundColor = Color.FromArgb("#F5A623");
        LabelBrowse.FontAttributes = FontAttributes.Bold;
        LabelBrowse.TextColor = Colors.White;

        if (savedBorder != null)
            savedBorder.BackgroundColor = Color.FromArgb("#1C3024");
        LabelSaved.FontAttributes = FontAttributes.None;
        LabelSaved.TextColor = Color.FromArgb("#6B7280");
    }

    private void SwitchToSavedSegment()
    {
        if (!_isBrowseSegment)
            return;

        _isBrowseSegment = false;
        BrowseHost.IsVisible = false;
        SavedContent.IsVisible = true;

        var browseBorder = LabelBrowse.Parent as Border;
        var savedBorder = LabelSaved.Parent as Border;
        if (browseBorder != null)
            browseBorder.BackgroundColor = Color.FromArgb("#1C3024");
        LabelBrowse.FontAttributes = FontAttributes.None;
        LabelBrowse.TextColor = Color.FromArgb("#6B7280");

        if (savedBorder != null)
            savedBorder.BackgroundColor = Color.FromArgb("#F5A623");
        LabelSaved.FontAttributes = FontAttributes.Bold;
        LabelSaved.TextColor = Colors.White;
    }

    private void EnsureBrowseContentCreated()
    {
        if (_browseContent != null)
            return;

        _browseContent = new StreetFoodNarrator.App.Views.Components.TabMenuView
        {
            BindingContext = _vm
        };

        BrowseHost.Content = _browseContent;
    }

    private async Task EnsureSavedDataWarmupAsync()
    {
        if (_isDataWarmupRunning)
            return;

        var hasRecentWarmup = (DateTime.UtcNow - _lastDataWarmupUtc) < DataWarmupCooldown;
        var toursReady = (_vm.AllTours?.Count ?? 0) > 0 && !_vm.IsTourDataStale;
        var savedReady = (_vm.SavedPOIs?.Count ?? 0) > 0;
        if (hasRecentWarmup && toursReady && savedReady)
            return;

        _isDataWarmupRunning = true;
        try
        {
            // Load saved POIs from local DB first for instant library content.
            if (!savedReady)
                await _vm.LoadSavedPOIsAsync();

            // Tours use cache-first strategy in ViewModel, so this should populate fast.
            if (!toursReady && !_vm.IsToursLoading)
                await _vm.LoadToursAsync(forceSyncNow: false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SavedPage] EnsureSavedDataWarmupAsync error: {ex.Message}");
        }
        finally
        {
            _lastDataWarmupUtc = DateTime.UtcNow;
            _isDataWarmupRunning = false;
        }
    }
}
