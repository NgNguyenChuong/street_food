// Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬
// MainPage.VirtualTour.cs  Ã¢â‚¬â€œ  Virtual Tour Mode logic
//
// Responsibilities:
//   Ã¢â‚¬Â¢ Check GPS distance to nearest POI on page load
//   Ã¢â‚¬Â¢ Show once-per-session confirmation popup if user is > 1 km away
//   Ã¢â‚¬Â¢ Enable / disable virtual tour mode (changes IsVirtualTourActive in VM)
//   Ã¢â‚¬Â¢ Navigate map to a virtual POI location
//   Ã¢â‚¬Â¢ Smart search: wire search events from TabMapView Ã¢â€ â€™ fuzzy suggestions
// Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬

using Mapsui;
using Mapsui.Projections;
using Mapsui.Styles;
using Microsoft.Maui.Controls.Shapes;
using StreetFoodNarrator.App.Core.Models;
using StreetFoodNarrator.App.ViewModels;
using MapsColor = Mapsui.Styles.Color;
using MapsBrush = Mapsui.Styles.Brush;
using Color = Microsoft.Maui.Graphics.Color;
using Point = Microsoft.Maui.Graphics.Point;
using Brush = Microsoft.Maui.Controls.Brush;
using Mapsui.Nts;
using Mapsui.Layers;
using StreetFoodNarrator.App.Helpers;


namespace StreetFoodNarrator.App.Views;

public partial class MainPage
{
    // Ã¢â€â‚¬Ã¢â€â‚¬ State Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬
    private MemoryLayer? _virtualPinLayer;
    private CancellationTokenSource? _virtualTourCts;

    private void CleanupVirtualTourState()
    {
        _vm.IsVirtualTourActive = false;
        _vm.IsVirtualNavigation = false;
        _vm.NavigationTarget = null;
        _vm.SelectedPinPOI = null;
        _vm.IsPinPopupVisible = false;

        _virtualTourCts?.Cancel();
        _virtualTourCts = null;

        ClearVirtualPinLayer();

        if (PinPopupComponent != null)
            PinPopupComponent.IsVisible = false;

        TabMapComponent?.HideSuggestions();

        if (_isMapInitialized)
        {
            _ = DrawNavigationRouteAsync();
            UpdateZonePins();
            MapView?.RefreshGraphics();
        }
    }

    // Ã¢â€â‚¬Ã¢â€â‚¬ Wiring (called from WireComponentEvents in MainPage.xaml.cs) Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬

    private void WireVirtualTourEvents()
    {
        TabMapComponent.DisableVirtualTourRequested += (_, _) => DisableVirtualTour();
    }

    // Ã¢â€â‚¬Ã¢â€â‚¬ Called from OnPageLoaded after data is ready Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬

    internal async Task CheckAndShowVirtualTourPopupAsync()
    {
        // Only show once per session
        if (_vm.IsVirtualTourPopupShown) return;

        // Get current position (quick timeout so startup isn't blocked)
        Microsoft.Maui.Devices.Sensors.Location? loc = null;
        try
        {
            loc = await Geolocation.GetLocationAsync(
                new GeolocationRequest(GeolocationAccuracy.Low, TimeSpan.FromSeconds(5)));
        }
        catch { /* Permission denied or GPS unavailable Ã¢â€ â€™ skip popup */ }

        if (loc == null) return;

        // Find nearest POI
        var nearest = FindNearestSpotFrom(loc.Latitude, loc.Longitude);
        if (nearest == null) return;

        var distKm = HaversineKm(loc.Latitude, loc.Longitude, nearest.Latitude, nearest.Longitude);
        if (distKm <= 1.0) return; // Close enough Ã¢â‚¬â€ no popup needed

        _vm.IsVirtualTourPopupShown = true;
        await ShowVirtualTourConfirmationAsync(nearest, distKm);
    }

    // Ã¢â€â‚¬Ã¢â€â‚¬ Confirmation popup Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬

    private async Task ShowVirtualTourConfirmationAsync(POI nearest, double distKm)
    {
        var tcs     = new TaskCompletionSource<string>();
        var overlay = new Grid { BackgroundColor = Color.FromArgb("#CC000000"), ZIndex = 200 };

        var dialog = new Border
        {
            BackgroundColor  = Color.FromArgb("#0E1E17"),
            Stroke           = Color.FromArgb("#1C3024"),
            StrokeThickness  = 1,
            Padding          = new Thickness(24),
            Margin           = new Thickness(20),
            WidthRequest     = 340,
            MaximumHeightRequest = 520,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions   = LayoutOptions.Center,
            StrokeShape      = new RoundRectangle { CornerRadius = new CornerRadius(24) }
        };
        dialog.Shadow = new Shadow { Brush = Brush.Black, Opacity = 0.5f, Radius = 24, Offset = new Point(0, 8) };

        var stack = new VerticalStackLayout { Spacing = 20 };

        // Ã¢â€â‚¬Ã¢â€â‚¬ Map icon Ã¢â€â‚¬Ã¢â€â‚¬
        var iconBorder = new Border
        {
            BackgroundColor  = Color.FromArgb("#F9731618"),
            StrokeShape      = new RoundRectangle { CornerRadius = new CornerRadius(60) },
            WidthRequest     = 80, HeightRequest = 80,
            HorizontalOptions = LayoutOptions.Center
        };
        iconBorder.Content = new Label { Text = "🗺️", FontSize = 40, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
        stack.Add(iconBorder);

        // Ã¢â€â‚¬Ã¢â€â‚¬ Title Ã¢â€â‚¬Ã¢â€â‚¬
        stack.Add(new Label
        {
            Text = "Bạn đang ở xa khu vực",
            FontSize = 20, FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White, HorizontalOptions = LayoutOptions.Center
        });

        // Ã¢â€â‚¬Ã¢â€â‚¬ Distance block Ã¢â€â‚¬Ã¢â€â‚¬
        int walkMin = (int)Math.Ceiling(distKm * 12);
        var distStack = new VerticalStackLayout { Spacing = 6 };
        var distNameRow = new HorizontalStackLayout { Spacing = 8, HorizontalOptions = LayoutOptions.Center };
        distNameRow.Add(new Label { Text = "📍", FontSize = 16, VerticalOptions = LayoutOptions.Center });
        distNameRow.Add(new Label { Text = $"Gần nhất: {nearest.Name_Vi}", FontSize = 14, TextColor = Color.FromArgb("#9CA3AF"), VerticalOptions = LayoutOptions.Center });
        distStack.Add(distNameRow);

        var distKmLabel = new Label { HorizontalOptions = LayoutOptions.Center };
        var distFormatted = new FormattedString();
        distFormatted.Spans.Add(new Span { Text = $"{distKm:F1} km", FontSize = 28, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#F97316") });
        distFormatted.Spans.Add(new Span { Text = $" (~{walkMin} phút đi bộ)", FontSize = 13, TextColor = Color.FromArgb("#6B7280") });
        distKmLabel.FormattedText = distFormatted;
        distStack.Add(distKmLabel);
        stack.Add(distStack);

        // Ã¢â€â‚¬Ã¢â€â‚¬ Divider Ã¢â€â‚¬Ã¢â€â‚¬
        stack.Add(new BoxView { HeightRequest = 1, BackgroundColor = Color.FromArgb("#1C3024") });

        // Ã¢â€â‚¬Ã¢â€â‚¬ Don't show again checkbox Ã¢â€â‚¬Ã¢â€â‚¬
        bool dontShowAgain = false;
        var checkRow = new HorizontalStackLayout { Spacing = 8, HorizontalOptions = LayoutOptions.Center };
        var checkbox  = new CheckBox { Color = Color.FromArgb("#22C55E"), VerticalOptions = LayoutOptions.Center };
        checkbox.CheckedChanged += (_, e) => dontShowAgain = e.Value;
        checkRow.Add(checkbox);
        checkRow.Add(new Label { Text = "Không hiện lại trong phiên này", FontSize = 12, TextColor = Color.FromArgb("#6B7280"), VerticalOptions = LayoutOptions.Center });
        stack.Add(checkRow);

        // Ã¢â€â‚¬Ã¢â€â‚¬ Buttons Ã¢â€â‚¬Ã¢â€â‚¬
        var btnVirtual = new Border
        {
            BackgroundColor = Color.FromArgb("#22C55E"),
            StrokeShape     = new RoundRectangle { CornerRadius = new CornerRadius(12) },
            Padding         = new Thickness(16, 13)
        };
        var btnVirtualRow = new HorizontalStackLayout { Spacing = 8, HorizontalOptions = LayoutOptions.Center };
        btnVirtualRow.Add(new Label { Text = "🎬", FontSize = 18, VerticalOptions = LayoutOptions.Center });
        btnVirtualRow.Add(new Label { Text = "Xem tour ảo ngay", FontSize = 15, FontAttributes = FontAttributes.Bold, TextColor = Colors.White, VerticalOptions = LayoutOptions.Center });
        btnVirtual.Content = btnVirtualRow;
        var tapVirtual = new TapGestureRecognizer();
        tapVirtual.Tapped += (_, _) => { if (dontShowAgain) _vm.IsVirtualTourPopupShown = true; tcs.TrySetResult("virtual"); overlay.IsVisible = false; };
        btnVirtual.GestureRecognizers.Add(tapVirtual);
        stack.Add(btnVirtual);

        var btnLater = new Border
        {
            BackgroundColor = Colors.Transparent,
            Stroke          = Color.FromArgb("#1C3024"),
            StrokeShape     = new RoundRectangle { CornerRadius = new CornerRadius(12) },
            Padding         = new Thickness(16, 12)
        };
        var tapLater = new TapGestureRecognizer();
        tapLater.Tapped += (_, _) =>
        {
            tcs.TrySetResult("later");
            // Ã„ÂÃ¡ÂºÂ£m bÃ¡ÂºÂ£o Ã¡ÂºÂ©n vÃƒÂ  xÃƒÂ³a overlay trÃƒÂªn main thread ngay lÃ¡ÂºÂ­p tÃ¡Â»Â©c
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try { ((AbsoluteLayout)this.Content).Children.Remove(overlay); } catch { }
            });
        };
        btnLater.GestureRecognizers.Add(tapLater);
        btnLater.Content = new Label { Text = "Để sau", FontSize = 14, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#9CA3AF"), HorizontalOptions = LayoutOptions.Center };
        stack.Add(btnLater);

        dialog.Content = stack;
        overlay.Children.Add(dialog);

        var mainAbs = (AbsoluteLayout)this.Content;
        AbsoluteLayout.SetLayoutBounds(overlay, new Rect(0, 0, 1, 1));
        AbsoluteLayout.SetLayoutFlags(overlay, Microsoft.Maui.Layouts.AbsoluteLayoutFlags.All);
        mainAbs.Children.Add(overlay);

        var result = await tcs.Task;
        // Remove overlay nÃ¡ÂºÂ¿u chÃ†Â°a bÃ¡Â»â€¹ xÃƒÂ³a (trÃ†Â°Ã¡Â»Âng hÃ¡Â»Â£p "virtual" button)
        MainThread.BeginInvokeOnMainThread(() =>
        {
            try { mainAbs.Children.Remove(overlay); } catch { }
        });

        if (result == "virtual")
            await EnableVirtualTourAsync(nearest);
    }

    // Ã¢â€â‚¬Ã¢â€â‚¬ Enable / Disable Virtual Tour Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬

    private async Task EnableVirtualTourAsync(POI startingPOI)
    {
        _virtualTourVm.StartVirtualTourSession();
        _vm.IsVirtualTourActive = true;
        _vm.IsVirtualNavigation = true;
        _vm.CurrentAppMode = MainViewModel.AppMode.Virtual; // Ã°Å¸â€˜Ë† This makes VirtualModeView visible

        // Scroll carousel to the starting POI
        var idx = _vm.AllPOIs.IndexOf(startingPOI);
        if (idx < 0) idx = 0;
        _vm.PrimaryZone = startingPOI;
        _vm.PrimaryZoneName = startingPOI.Name_Vi ?? startingPOI.Name_En ?? "—";
        _vm.PrimaryZoneDesc = startingPOI.Description_Vi ?? startingPOI.Description_En ?? "";
        _vm.PrimaryZoneAddress = startingPOI.Address ?? "Đang cập nhật";
        _vm.PrimaryZoneRating = (startingPOI.Rating ?? 4.5).ToString("F1");
        var spots = _vm.AllPOIs.Where(p => p.ZoneType == "Spot").OrderBy(p => p.Id).ToList();
        int spotIdx = spots.FindIndex(p => p.Id == startingPOI.Id);
        if (spotIdx >= 0)
            _vm.CurrentStopBadge = $"{spotIdx + 1}/{spots.Count}";

        // Add virtual pin to map (distinct: purple with glow)
        AddVirtualLocationPin(startingPOI);

        // Center map on starting POI
        CenterMapOnPOI(startingPOI);
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            await Task.Delay(40);
            VirtualModeComponent.CenterVirtualMap(
                startingPOI.Latitude,
                startingPOI.Longitude,
                _vm.AllPOIs.Where(p => p.ZoneType == "Spot"),
                startingPOI);
        });

        // Start background GPS pings every 2.5 minutes (battery saving in virtual mode)
        _virtualTourCts?.Cancel();
        _virtualTourCts = new CancellationTokenSource();
        _ = RunVirtualGpsPingsAsync(_virtualTourCts.Token);
    }

    private void DisableVirtualTour()
    {
        _ = HandleVirtualTourExitPromptAsync();
        CleanupVirtualTourState();
        _vm.CurrentAppMode = MainViewModel.AppMode.Explore; // Exit to map mode
        _vm.IsLegacyMapVisible = true;
    }


    private void AddVirtualLocationPin(POI poi)
    {
        if (MapView?.Map == null) return;

        if (_virtualPinLayer == null)
        {
            _virtualPinLayer = new MemoryLayer("VirtualPin");
            MapView.Map.Layers.Add(_virtualPinLayer);
        }

        var (px, py) = SphericalMercator.FromLonLat(poi.Longitude, poi.Latitude);
        var feature  = new PointFeature(new MPoint(px, py));

        // Outer glow Ã¢â‚¬â€ purple
        feature.Styles.Add(new SymbolStyle
        {
            SymbolScale = 1.4,
            Fill        = new MapsBrush(new MapsColor(147, 51, 234, 60)),
            Outline     = null,
            SymbolType  = SymbolType.Ellipse
        });
        // Inner pin Ã¢â‚¬â€ distinct white/purple
        feature.Styles.Add(new SymbolStyle
        {
            SymbolScale = 0.65,
            Fill        = new MapsBrush(new MapsColor(168, 85, 247)),
            Outline     = new Pen(MapsColor.White, 3),
            SymbolType  = SymbolType.Ellipse
        });
        // Label
        feature.Styles.Add(new LabelStyle
        {
            Text               = "Ã°Å¸â€œÂ VÃ¡Â»â€¹ trÃƒÂ­ Ã¡ÂºÂ£o",
            ForeColor          = MapsColor.White,
            BackColor          = new MapsBrush(new MapsColor(88, 28, 135, 200)),
            Font               = new Mapsui.Styles.Font { FontFamily = "sans-serif", Size = 9, Bold = true },
            Offset             = new Offset(0, 22),
            HorizontalAlignment = LabelStyle.HorizontalAlignmentEnum.Center,
            VerticalAlignment  = LabelStyle.VerticalAlignmentEnum.Top,
        });

        _virtualPinLayer.Features = new[] { (Mapsui.IFeature)feature };
        _virtualPinLayer.DataHasChanged();
        MapView.RefreshGraphics();
    }

    private void ClearVirtualPinLayer()
    {
        if (_virtualPinLayer == null || MapView?.Map == null) return;
        _virtualPinLayer.Features = Array.Empty<Mapsui.IFeature>();
        _virtualPinLayer.DataHasChanged();
        MapView.RefreshGraphics();
    }

    internal void CenterMapOnPOI(POI poi)
    {
        if (MapView?.Map == null) return;
        var (px, py) = SphericalMercator.FromLonLat(poi.Longitude, poi.Latitude);
        MapView.Map.Navigator.CenterOn(new MPoint(px, py));
        ZoomToDefaultLevel();
    }

    private async Task RunVirtualGpsPingsAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMinutes(2.5), ct).ContinueWith(_ => { });
            if (ct.IsCancellationRequested) break;
            // Quiet background ping Ã¢â‚¬â€ we don't need to re-center map in virtual mode
            try
            {
                await Geolocation.GetLocationAsync(
                    new GeolocationRequest(GeolocationAccuracy.Low, TimeSpan.FromSeconds(6)));
            }
            catch { /* OK to fail silently in virtual mode */ }
        }
    }

    // Ã¢â€â‚¬Ã¢â€â‚¬ Smart Search handlers Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬

    private void OnSearchChanged(object? sender, TextChangedEventArgs e)
    {
        // The VM's SearchQuery is bound via XAML; suggestions come from PerformFuzzySearchAsync
        // triggered by OnSearchQueryChanged Ã¢â‚¬â€ we just need to sync suggestions display.
        _ = UpdateSuggestionsDropdownAsync();
    }

    private async Task UpdateSuggestionsDropdownAsync()
    {
        await Task.Delay(140); // Let debounce in VM settle
        MainThread.BeginInvokeOnMainThread(() =>
        {
            TabMapComponent.ShowSuggestions(_vm.SearchSuggestions);
        });
    }

    private void OnClearSearch(object? sender, EventArgs e)
    {
        _vm.SearchQuery = "";
        TabMapComponent.HideSuggestions();
        UpdateZonePins();
    }

    private void OnSuggestionSelected(object? sender, POI poi)
    {
        _vm.SearchQuery = poi.Name_Vi ?? poi.Name_En ?? "";
        TabMapComponent.HideSuggestions();
        CenterMapOnPOI(poi);
        _vm.SelectedPinPOI = poi;
        _vm.IsPinPopupVisible = false;
        PinPopupComponent.IsVisible = false;
        UpdateZonePins();
    }

    // Ã¢â€â‚¬Ã¢â€â‚¬ Utility Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬

    private POI? FindNearestSpotFrom(double lat, double lon)
    {
        var source = _vm.AllPOIs.Count > 0 ? _vm.AllPOIs : null;
        if (source == null || source.Count == 0) return null;
        return source
            .Where(p => p.ZoneType == "Spot")
            .OrderBy(p => HaversineKm(lat, lon, p.Latitude, p.Longitude))
            .FirstOrDefault();
    }

    private static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371;
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLon = (lon2 - lon1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180)
              * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static Task<bool> DisplayAlertAsync(string title, string msg, string accept, string cancel)
        => CustomAlert.ShowConfirmAsync(title, msg, accept, cancel, AlertType.Warning);

    private static Task DisplayInfoAsync(string title, string msg, string accept)
        => CustomAlert.ShowAsync(title, msg, accept, AlertType.Info);

    private async Task HandleVirtualJournalPlaybackStartedAsync(POI poi)
    {
        if (_vm.CurrentAppMode != MainViewModel.AppMode.Virtual)
            return;

        var added = _vm.MarkJournalPoiCompleted(poi);
        if (!added)
            return;

        _vm.RefreshJournalState();
        _virtualTourVm.OnPoiViewed(poi.Id);
        await MaybeShowVirtualToRealSuggestionAsync();
    }
    private async Task MaybeShowVirtualToRealSuggestionAsync()
    {
        if (_vm.CurrentAppMode != MainViewModel.AppMode.Virtual)
            return;
        if (!_vm.ShouldShowJournalMilestonePopup())
            return;

        // Mark first to prevent duplicate popups when playback events race.
        _vm.MarkJournalMilestonePopupShownToday();

        var shouldSwitchToReal = await MainThread.InvokeOnMainThreadAsync(() =>
            DisplayAlertAsync(
                "Ban da kham pha 4 quan roi",
                "Ban da nghe du nhieu diem trong tour ao. Muon chuyen sang tour that ngay bay gio khong?",
                "Xem tour that",
                "De sau"));

        if (!shouldSwitchToReal)
            return;

        var isInsideFoodArea = await IsInsideFoodAreaNowAsync();
        if (!isInsideFoodArea)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
                DisplayInfoAsync(
                    "Ban dang o xa khu vuc am thuc",
                    "Hien chua the chuyen sang tour that. Hay di chuyen den khu vuc am thuc roi thu lai.",
                    "Da hieu"));
            return;
        }

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            await StopNarrationAsync(resetProgress: false, clearResumeState: false);
            await SwitchToRealModeAsync();
        });
    }

    private async Task<bool> IsInsideFoodAreaNowAsync()
    {
        double lat;
        double lon;

        try
        {
            var request = new GeolocationRequest(GeolocationAccuracy.Best, TimeSpan.FromSeconds(4));
            var loc = await Geolocation.GetLocationAsync(request);
            if (loc != null)
            {
                lat = loc.Latitude;
                lon = loc.Longitude;
                _vm.CurrentLat = lat;
                _vm.CurrentLon = lon;
                _vm.HasLocationFix = true;
            }
            else
            {
                lat = _vm.CurrentLat;
                lon = _vm.CurrentLon;
            }
        }
        catch
        {
            lat = _vm.CurrentLat;
            lon = _vm.CurrentLon;
        }

        if (lat == 0 || lon == 0)
            return _vm.IsInsideAnyZone || _vm.CurrentExploreState == MainViewModel.ExploreState.InZone;

        var nearest = FindNearestSpotFrom(lat, lon);
        if (nearest == null)
            return false;

        var distanceMeters = HaversineKm(lat, lon, nearest.Latitude, nearest.Longitude) * 1000d;
        var inZoneRadius = Math.Max(nearest.Radius, 60);
        return distanceMeters <= inZoneRadius;
    }

    private async Task HandleVirtualTourExitPromptAsync()
    {
        if (_vm.CurrentAppMode != MainViewModel.AppMode.Virtual)
            return;

        await _virtualTourVm.EndVirtualTourSessionAsync(evaluatePromptOnExit: true);
    }

}

