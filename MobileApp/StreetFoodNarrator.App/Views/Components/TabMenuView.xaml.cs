using StreetFoodNarrator.App.ViewModels;

namespace StreetFoodNarrator.App.Views.Components;

public partial class TabMenuView : ContentView
{
    public TabMenuView()
    {
        InitializeComponent();
        if (BindingContext == null)
            BindingContext = MauiProgram.Services.GetRequiredService<MainViewModel>();
    }
}
