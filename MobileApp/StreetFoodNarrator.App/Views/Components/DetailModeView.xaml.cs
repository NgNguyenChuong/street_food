using System;
using Microsoft.Maui.Controls;

namespace StreetFoodNarrator.App.Views.Components;

public partial class DetailModeView : ContentView
{
    public event EventHandler? BackRequested;
    public event EventHandler? ShareRequested;

    public DetailModeView()
    {
        InitializeComponent();
    }

    private void OnBackTapped(object sender, TappedEventArgs e)
    {
        BackRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnShareTapped(object sender, TappedEventArgs e)
    {
        ShareRequested?.Invoke(this, EventArgs.Empty);
    }
}
