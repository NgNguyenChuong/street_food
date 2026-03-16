using StreetFoodNarrator.App.ViewModels;

namespace StreetFoodNarrator.App.Views;

public partial class SavedPage : ContentPage
{
    private readonly MainViewModel _vm;
    private bool _isBrowseSegment = false;

    public SavedPage()
    {
        InitializeComponent();
        _vm = MauiProgram.Services.GetRequiredService<MainViewModel>();
        BindingContext = _vm;
        
        Console.WriteLine("[SavedPage] Initialized");
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        Console.WriteLine("[SavedPage] OnAppearing");

        if (_vm.AllPOIs.Count == 0)
        {
            await _vm.LoadAllPoisAsync();
        }
    }

    private void OnSegmentBrowseTapped(object sender, EventArgs e)
    {
        if (_isBrowseSegment) return;
        
        _isBrowseSegment = true;
        BrowseContent.IsVisible = true;
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
    }

    private void OnSegmentSavedTapped(object sender, EventArgs e)
    {
        if (!_isBrowseSegment) return;
        
        _isBrowseSegment = false;
        BrowseContent.IsVisible = false;
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
}
