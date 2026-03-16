// ─────────────────────────────────────────────────────────────────────────────
// TabMenuView.xaml.cs
// Tab 2: Menu quán ăn – filter chips tự quản lý (không cần callback ra ngoài)
// SearchQuery + ApplyFilter() gọi trực tiếp qua BindingContext (MainViewModel)
// ─────────────────────────────────────────────────────────────────────────────

using StreetFoodNarrator.App.ViewModels;
using StreetFoodNarrator.App.Core.Models;
using StreetFoodNarrator.App.Views;

namespace StreetFoodNarrator.App.Views.Components;

public partial class TabMenuView : ContentView
{
    public TabMenuView()
    {
        InitializeComponent();
        if (BindingContext == null)
            BindingContext = MauiProgram.Services.GetRequiredService<MainViewModel>();
    }

    // ─── Filter chip style ───────────────────────────────────────────────────

    private void ApplyChipStyle(Border? active)
    {
        var chips = new[] { FilterChipAll, FilterChipPho, FilterChipBanhMi, FilterChipCom, FilterChipChe };
        foreach (var chip in chips)
        {
            bool isActive        = chip == active;
            chip.BackgroundColor = Microsoft.Maui.Graphics.Color.FromArgb(isActive ? "#22C55E" : "#0E1E17");
            chip.Stroke          = new SolidColorBrush(
                Microsoft.Maui.Graphics.Color.FromArgb(isActive ? "#22C55E" : "#1C3024"));
        }
    }

    // ─── Filter handlers ────────────────────────────────────────────────────

    private void OnFilterAll(object sender, EventArgs e)
    {
        if (BindingContext is MainViewModel vm) { vm.SearchQuery = ""; vm.ApplyFilter(); }
        ApplyChipStyle(FilterChipAll);
    }

    private void OnFilterPho(object sender, EventArgs e)
    {
        if (BindingContext is MainViewModel vm) { vm.SearchQuery = "phở"; vm.ApplyFilter(); }
        ApplyChipStyle(FilterChipPho);
    }

    private void OnFilterBanhMi(object sender, EventArgs e)
    {
        if (BindingContext is MainViewModel vm) { vm.SearchQuery = "bánh mì"; vm.ApplyFilter(); }
        ApplyChipStyle(FilterChipBanhMi);
    }

    private void OnFilterCom(object sender, EventArgs e)
    {
        if (BindingContext is MainViewModel vm) { vm.SearchQuery = "cơm"; vm.ApplyFilter(); }
        ApplyChipStyle(FilterChipCom);
    }

    private void OnFilterChe(object sender, EventArgs e)
    {
        if (BindingContext is MainViewModel vm) { vm.SearchQuery = "chè"; vm.ApplyFilter(); }
        ApplyChipStyle(FilterChipChe);
    }

    private void OnSaveTapped(object sender, EventArgs e)
    {
        if (BindingContext is not MainViewModel vm) return;
        if (sender is not BindableObject bindable) return;
        if (bindable.BindingContext is not POI poi) return;

        vm.ToggleSavePOICommand.Execute(poi);
        // Rebuild FilteredPOIs so heart color updates immediately
        vm.ApplyFilter();
    }

    private async void OnDetailTapped(object sender, EventArgs e)
    {
        if (sender is BindableObject bindable && bindable.BindingContext is POI poi)
            await Shell.Current.Navigation.PushModalAsync(new POIDetailPage(poi));
    }
}
