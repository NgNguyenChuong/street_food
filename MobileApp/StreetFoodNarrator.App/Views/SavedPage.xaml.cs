using StreetFoodNarrator.App.ViewModels;

namespace StreetFoodNarrator.App.Views;

public partial class SavedPage : ContentPage
{
    private readonly MainViewModel _vm;
    private bool _isBrowseSegment = false;
    private StreetFoodNarrator.App.Views.Components.TabMenuView? _browseContent;

    public SavedPage()
    {
        InitializeComponent();
        _vm = MauiProgram.Services.GetRequiredService<MainViewModel>();
        BindingContext = _vm;
        
        Console.WriteLine("[SavedPage] Initialized");
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Console.WriteLine("[SavedPage] OnAppearing");
        _vm.RefreshOfflineBannerSession();

        if (_vm.AllPOIs.Count == 0)
        {
            _ = _vm.LoadAllPoisAsync(forceSyncNow: false);
        }

        if ((_vm.AllTours?.Count ?? 0) == 0 || _vm.IsTourDataStale)
        {
            _ = _vm.LoadToursAsync(forceSyncNow: false);
        }
    }

    private void OnSegmentBrowseTapped(object sender, EventArgs e)
    {
        if (_isBrowseSegment) return;
        EnsureBrowseContentCreated();
        
        _isBrowseSegment = true;
        BrowseHost.IsVisible = true;
        SavedContent.IsVisible = false;
        
        // Update UI
        var browseBorder = (Border)sender;
        var savedBorder = (Border)LabelSaved.Parent;
        
        browseBorder.BackgroundColor = Color.FromArgb("#F5A623");
        LabelBrowse.FontAttributes = FontAttributes.Bold;
        LabelBrowse.TextColor = Colors.White;
        
        savedBorder.BackgroundColor = Color.FromArgb("#1C3024");
        LabelSaved.FontAttributes = FontAttributes.None;
        LabelSaved.TextColor = Color.FromArgb("#6B7280");

        if ((_vm.AllTours?.Count ?? 0) == 0 || _vm.IsTourDataStale)
        {
            _ = _vm.LoadToursAsync(forceSyncNow: false);
        }
    }

    private void OnSegmentSavedTapped(object sender, EventArgs e)
    {
        if (!_isBrowseSegment) return;
        
        _isBrowseSegment = false;
        BrowseHost.IsVisible = false;
        SavedContent.IsVisible = true;
        
        // Update UI
        var browseBorder = (Border)LabelBrowse.Parent;
        var savedBorder = (Border)sender;
        
        browseBorder.BackgroundColor = Color.FromArgb("#1C3024");
        LabelBrowse.FontAttributes = FontAttributes.None;
        LabelBrowse.TextColor = Color.FromArgb("#6B7280");
        
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
}
