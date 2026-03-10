namespace StreetFoodNarrator.App.Views;

public partial class ProfilePage : ContentPage
{
    public ProfilePage()
    {
        InitializeComponent();
        Console.WriteLine("[ProfilePage] Initialized");
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Console.WriteLine("[ProfilePage] OnAppearing");
    }

    private async void OnSettingsTapped(object sender, EventArgs e)
    {
        // Navigate to Settings page
        await Shell.Current.GoToAsync("SettingsPage");
    }
}
