using System.Runtime.CompilerServices;

namespace StreetFoodNarrator.App.Views.Components;

public partial class FloatingBottomNav : ContentView
{
    public static readonly BindableProperty ActiveTabProperty = BindableProperty.Create(
        nameof(ActiveTab), typeof(string), typeof(FloatingBottomNav), "Explore",
        propertyChanged: OnActiveTabChanged);

    public string ActiveTab
    {
        get => (string)GetValue(ActiveTabProperty);
        set => SetValue(ActiveTabProperty, value);
    }

    public FloatingBottomNav()
    {
        InitializeComponent();
        UpdateUI();
    }

    private static void OnActiveTabChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is FloatingBottomNav nav)
        {
            nav.UpdateUI();
        }
    }

    private void UpdateUI()
    {
        // Explore settings
        bool isExplore = ActiveTab == "Explore" || ActiveTab == "MainPage";
        ExploreIcon.TextColor = isExplore ? Color.FromArgb("#4BE277") : Color.FromArgb("#92A89D");
        ExploreLabel.TextColor = isExplore ? Color.FromArgb("#4BE277") : Color.FromArgb("#92A89D");
        ExploreIndicator.IsVisible = isExplore;

        // Saved settings
        bool isSaved = ActiveTab == "Saved" || ActiveTab == "SavedPage";
        SavedIcon.TextColor = isSaved ? Color.FromArgb("#4BE277") : Color.FromArgb("#92A89D");
        SavedLabel.TextColor = isSaved ? Color.FromArgb("#4BE277") : Color.FromArgb("#92A89D");
        SavedIndicator.IsVisible = isSaved;

        // Profile settings
        bool isProfile = ActiveTab == "Profile" || ActiveTab == "ProfilePage";
        ProfileIcon.TextColor = isProfile ? Color.FromArgb("#4BE277") : Color.FromArgb("#92A89D");
        ProfileLabel.TextColor = isProfile ? Color.FromArgb("#4BE277") : Color.FromArgb("#92A89D");
        ProfileIndicator.IsVisible = isProfile;
    }

    private async void OnExploreTapped(object sender, EventArgs e)
    {
        if (ActiveTab != "Explore" && ActiveTab != "MainPage")
            await Shell.Current.GoToAsync("//MapPage");
    }

    private async void OnSavedTapped(object sender, EventArgs e)
    {
        if (ActiveTab != "Saved" && ActiveTab != "SavedPage")
            await Shell.Current.GoToAsync("//SavedPage");
    }

    private async void OnProfileTapped(object sender, EventArgs e)
    {
        if (ActiveTab != "Profile" && ActiveTab != "ProfilePage")
            await Shell.Current.GoToAsync("//ProfilePage");
    }
}
