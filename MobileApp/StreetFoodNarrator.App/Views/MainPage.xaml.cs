using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;
using StreetFoodNarrator.App.ViewModels;

namespace StreetFoodNarrator.App.Views;

public partial class MainPage : ContentPage
{
    private readonly MainViewModel _vm;
    private Pin? _userPin;

    public MainPage(MainViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _vm = viewModel;

        // Wire map updates
        _vm.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.CurrentLat))
                UpdateUserPin();
        };
        _vm.ActiveZones.CollectionChanged += (s, e) => UpdateZonePins();

        // Initialize map after page loads
        Loaded += OnPageLoaded;
    }

    private void OnPageLoaded(object? sender, EventArgs e)
    {
        InitializeMap();
    }

    private void InitializeMap()
    {
        if (MapView == null)
            return;

        // Center map on Vinh Khanh area
        var center = new Location(AppConfig.DefaultLatitude, AppConfig.DefaultLongitude);
        var span = new MapSpan(center, latitudeDegrees: 0.01, longitudeDegrees: 0.01);
        MapView.MoveToRegion(span);

        // Add user pin (initial position)
        _userPin = new Pin
        {
            Label = "Vi tri cua ban",
            Type = PinType.Place,
            Location = center
        };
        MapView.Pins.Add(_userPin);

        // Draw initial zone pins
        UpdateZonePins();
    }

    private void UpdateZonePins()
    {
        if (MapView == null)
            return;

        // Remove old zone pins (keep user pin)
        var zonePins = MapView.Pins.Where(p => p != _userPin).ToList();
        foreach (var pin in zonePins)
            MapView.Pins.Remove(pin);

        // Add current active zones
        foreach (var zone in _vm.ActiveZones)
        {
            var pin = new Pin
            {
                Label = zone.Name_Vi ?? zone.GetName("vi"),
                Type = PinType.Place,
                Location = new Location(zone.Latitude, zone.Longitude)
            };

            MapView.Pins.Add(pin);
        }
    }

    private void UpdateUserPin()
    {
        if (MapView == null || _userPin == null || _vm.CurrentLat == 0)
            return;

        // Update user pin location
        _userPin.Location = new Location(_vm.CurrentLat, _vm.CurrentLon);

        // Center map on user location
        var span = new MapSpan(_userPin.Location, latitudeDegrees: 0.005, longitudeDegrees: 0.005);
        MapView.MoveToRegion(span);
    }
}
