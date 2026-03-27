using System.Collections.Specialized;
using System.ComponentModel;
using BruTile.Predefined;
using BruTile.Web;
using Mapsui;
using Mapsui.Layers;
using Mapsui.Nts;
using Mapsui.Projections;
using Mapsui.Styles;
using Mapsui.Tiling;
using Mapsui.Tiling.Layers;
using Microsoft.Extensions.DependencyInjection;
using NetTopologySuite.Geometries;
using StreetFoodNarrator.App.Core.Models;
using StreetFoodNarrator.App.ViewModels;
using MapsColor = Mapsui.Styles.Color;
using MapsBrush = Mapsui.Styles.Brush;

namespace StreetFoodNarrator.App.Views;

public partial class ExploreMapPage : ContentPage
{
    private readonly MainViewModel _vm;
    private MemoryLayer? _pinsLayer;
    private MemoryLayer? _userPinLayer;
    private bool _isMapInitialized;
    private bool _isFirstLocation = true;
    private bool _eventsHooked;
    private bool _isClosing;
    private const int DefaultMapZoomLevel = (int)AppConfig.DefaultZoom + 1;

    public ExploreMapPage()
        : this(null)
    {
    }

    public ExploreMapPage(MainViewModel? viewModel)
    {
        InitializeComponent();

        _vm = viewModel ?? ResolveRequiredService<MainViewModel>();
        BindingContext = _vm;
        TabMapComponent.BindingContext = _vm;
    }

    public void Prewarm()
    {
        try
        {
            EnsureMapInitialized();
            UpdateZonePins();
            UpdateUserPin();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ExploreMapPage] Prewarm error: {ex.Message}");
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        HookEvents();
        if (_vm.Categories.Count == 0)
            _vm.BuildMapCategories();
        EnsureMapInitialized();
        CenterMapForCurrentState();
        UpdateZonePins();
        UpdateUserPin();
        TabMapComponent.InitializeChips(_vm.SelectedCategory);
        _ = _vm.StartTrackingAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        UnhookEvents();
    }

    private void HookEvents()
    {
        if (_eventsHooked)
            return;

        _vm.PropertyChanged += OnViewModelPropertyChanged;
        _vm.AllPOIs.CollectionChanged += OnPoisCollectionChanged;
        _vm.FilteredPOIs.CollectionChanged += OnPoisCollectionChanged;

        TabMapComponent.BackRequested += OnBackRequested;
        TabMapComponent.ExploreNavRequested += OnBackRequested;
        TabMapComponent.SavedNavRequested += OnSavedRequested;
        TabMapComponent.ProfileNavRequested += OnProfileRequested;
        TabMapComponent.SettingsRequested += OnSettingsRequested;
        TabMapComponent.ZoomInRequested += OnZoomInRequested;
        TabMapComponent.ZoomOutRequested += OnZoomOutRequested;
        TabMapComponent.CenterMapRequested += OnCenterMapRequested;
        TabMapComponent.ViewDetailRequested += OnViewDetailRequested;
        TabMapComponent.PrevPoiRequested += OnPrevPoiRequested;
        TabMapComponent.NextPoiRequested += OnNextPoiRequested;
        TabMapComponent.LikeRequested += OnLikeRequested;
        TabMapComponent.CategorySelected += OnCategorySelected;
        TabMapComponent.SearchTextChanged += OnSearchTextChanged;
        TabMapComponent.ClearSearchRequested += OnClearSearchRequested;
        TabMapComponent.SuggestionSelected += OnSuggestionSelected;

        _eventsHooked = true;
    }

    private void UnhookEvents()
    {
        if (!_eventsHooked)
            return;

        _vm.PropertyChanged -= OnViewModelPropertyChanged;
        _vm.AllPOIs.CollectionChanged -= OnPoisCollectionChanged;
        _vm.FilteredPOIs.CollectionChanged -= OnPoisCollectionChanged;

        TabMapComponent.BackRequested -= OnBackRequested;
        TabMapComponent.ExploreNavRequested -= OnBackRequested;
        TabMapComponent.SavedNavRequested -= OnSavedRequested;
        TabMapComponent.ProfileNavRequested -= OnProfileRequested;
        TabMapComponent.SettingsRequested -= OnSettingsRequested;
        TabMapComponent.ZoomInRequested -= OnZoomInRequested;
        TabMapComponent.ZoomOutRequested -= OnZoomOutRequested;
        TabMapComponent.CenterMapRequested -= OnCenterMapRequested;
        TabMapComponent.ViewDetailRequested -= OnViewDetailRequested;
        TabMapComponent.PrevPoiRequested -= OnPrevPoiRequested;
        TabMapComponent.NextPoiRequested -= OnNextPoiRequested;
        TabMapComponent.LikeRequested -= OnLikeRequested;
        TabMapComponent.CategorySelected -= OnCategorySelected;
        TabMapComponent.SearchTextChanged -= OnSearchTextChanged;
        TabMapComponent.ClearSearchRequested -= OnClearSearchRequested;
        TabMapComponent.SuggestionSelected -= OnSuggestionSelected;

        if (MapView != null)
            MapView.Info -= OnMapInfoTapped;

        _eventsHooked = false;
    }

    private void EnsureMapInitialized()
    {
        if (_isMapInitialized)
            return;

        if (MapView?.Map == null)
            return;

        var cacheDb = Path.Combine(FileSystem.AppDataDirectory, "map_cache", "tiles.db");
        var tileCache = new StreetFoodNarrator.App.Services.SimpleTileCache(cacheDb);
        var tileSource = new HttpTileSource(
            new GlobalSphericalMercator(),
            "https://a.basemaps.cartocdn.com/rastertiles/voyager/{z}/{x}/{y}.png",
            name: "Carto",
            persistentCache: tileCache);

        MapView.Map.Layers.Clear();
        MapView.Map.Widgets.Clear();
        MapView.Map.Layers.Add(new TileLayer(tileSource) { Name = "BaseMap" });

        var darkOverlay = new MemoryLayer("DarkOverlay")
        {
            Style = new VectorStyle
            {
                Fill = new MapsBrush(new MapsColor(8, 22, 12, 110)),
                Outline = null
            }
        };

        var worldExtent = new[]
        {
            new MPoint(-20037508.34, -20037508.34),
            new MPoint(20037508.34, -20037508.34),
            new MPoint(20037508.34, 20037508.34),
            new MPoint(-20037508.34, 20037508.34),
            new MPoint(-20037508.34, -20037508.34)
        };
        var overlayPolygon = new Polygon(
            new LinearRing(worldExtent.Select(p => new Coordinate(p.X, p.Y)).ToArray()));
        darkOverlay.Features = new[] { new GeometryFeature { Geometry = overlayPolygon } };
        MapView.Map.Layers.Add(darkOverlay);

        _userPinLayer = new MemoryLayer("UserPin");
        _pinsLayer = new MemoryLayer("Pins");
        MapView.Map.Layers.Add(_userPinLayer);
        MapView.Map.Layers.Add(_pinsLayer);

        MapView.InputTransparent = false;
        MapView.CascadeInputTransparent = false;
        MapView.Info -= OnMapInfoTapped;
        MapView.Info += OnMapInfoTapped;
        MapView.Map.Navigator.RotationLock = true;
        MapView.UseFling = true;

        var (cx, cy) = SphericalMercator.FromLonLat(AppConfig.DefaultLongitude, AppConfig.DefaultLatitude);
        MapView.Map.Navigator.CenterOn(new MPoint(cx, cy));
        ZoomToDefaultLevel();
        _isMapInitialized = true;
    }

    private void ZoomToDefaultLevel()
    {
        if (MapView?.Map?.Navigator == null)
            return;

        var resolutions = MapView.Map.Navigator.Resolutions;
        if (resolutions == null)
            return;

        var maxLevel = Math.Max(0, resolutions.Count() - 1);
        var level = Math.Clamp(DefaultMapZoomLevel, 0, maxLevel);
        MapView.Map.Navigator.ZoomToLevel(level);
    }

    private void UpdateZonePins()
    {
        if (!_isMapInitialized || _pinsLayer == null || MapView?.Map == null)
            return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (_pinsLayer == null)
                return;

            var nearbyIds = _vm.ActiveZones.Select(z => z.Id).ToHashSet();
            var visiblePoiIds = _vm.FilteredPOIs.Select(p => p.Id).ToHashSet();
            var features = new List<IFeature>();

            foreach (var poi in _vm.AllPOIs)
            {
                if (poi.ZoneType == "Area" || poi.ZoneType == "District")
                    continue;

                if (visiblePoiIds.Count > 0 && !visiblePoiIds.Contains(poi.Id))
                    continue;

                var (px, py) = SphericalMercator.FromLonLat(poi.Longitude, poi.Latitude);
                var poiFeature = new PointFeature(new MPoint(px, py));
                poiFeature["POI_ID"] = poi.Id;

                MapsColor fillColor;
                if (_vm.SavedPOIIds.Contains(poi.Id))
                    fillColor = new MapsColor(251, 191, 36);
                else if (_vm.VisitedPOIIds.Contains(poi.Id))
                    fillColor = new MapsColor(147, 51, 234);
                else if (nearbyIds.Contains(poi.Id))
                    fillColor = new MapsColor(59, 130, 246);
                else
                    fillColor = new MapsColor(249, 115, 22);

                poiFeature.Styles.Add(new SymbolStyle
                {
                    SymbolScale = 0.5,
                    Fill = new MapsBrush(fillColor),
                    Outline = new Pen(MapsColor.White, 2.5f),
                    SymbolType = SymbolType.Ellipse
                });

                var labelText = poi.Name_Vi ?? poi.Name_En ?? string.Empty;
                if (labelText.Length > 15)
                    labelText = labelText[..15];

                poiFeature.Styles.Add(new LabelStyle
                {
                    Text = labelText,
                    ForeColor = MapsColor.White,
                    BackColor = new MapsBrush(new MapsColor(10, 16, 12, 220)),
                    Font = new Mapsui.Styles.Font { FontFamily = "sans-serif", Size = 8 },
                    Offset = new Offset(0, 18),
                    HorizontalAlignment = LabelStyle.HorizontalAlignmentEnum.Center,
                    VerticalAlignment = LabelStyle.VerticalAlignmentEnum.Top
                });

                features.Add(poiFeature);
            }

            _pinsLayer.Features = features;
            _pinsLayer.DataHasChanged();
            MapView.RefreshGraphics();
        });
    }

    private void UpdateUserPin()
    {
        if (!_isMapInitialized || _userPinLayer == null || MapView?.Map == null)
            return;

        if (_vm.CurrentLat == 0)
            return;

        var (ux, uy) = SphericalMercator.FromLonLat(_vm.CurrentLon, _vm.CurrentLat);
        var userFeature = new PointFeature(new MPoint(ux, uy));
        userFeature.Styles.Add(new SymbolStyle
        {
            SymbolScale = 0.6,
            Fill = new MapsBrush(new MapsColor(34, 197, 94)),
            Outline = new Pen(MapsColor.White, 3),
            SymbolType = SymbolType.Ellipse
        });

        _userPinLayer.Features = new[] { userFeature };
        _userPinLayer.DataHasChanged();

        if (_isFirstLocation)
        {
            CenterMapForCurrentState();
            _isFirstLocation = false;
        }

        MapView.RefreshGraphics();
    }

    private void OnMapInfoTapped(object? sender, Mapsui.MapInfoEventArgs e)
    {
        var worldPos = e.WorldPosition;
        if (worldPos == null)
            return;

        POI? nearest = null;
        double minDist = 600;

        foreach (var poi in _vm.AllPOIs)
        {
            if (poi.ZoneType == "Area" || poi.ZoneType == "District")
                continue;

            var (px, py) = SphericalMercator.FromLonLat(poi.Longitude, poi.Latitude);
            var dist = Math.Sqrt(Math.Pow(worldPos.X - px, 2) + Math.Pow(worldPos.Y - py, 2));
            if (dist < minDist)
            {
                minDist = dist;
                nearest = poi;
            }
        }

        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (nearest == null)
                return;

            _vm.VisitedPOIIds.Add(nearest.Id);
            _vm.SelectedPinPOI = nearest;
            UpdateZonePins();
        });
    }

    private async void OnBackRequested(object? sender, EventArgs e)
    {
        await ClosePageAsync();
    }

    private async void OnSavedRequested(object? sender, EventArgs e)
    {
        await ClosePageAsync();
        if (Shell.Current != null)
            await Shell.Current.GoToAsync("//SavedPage");
    }

    private async void OnProfileRequested(object? sender, EventArgs e)
    {
        await ClosePageAsync();
        if (Shell.Current != null)
            await Shell.Current.GoToAsync("//ProfilePage");
    }

    private async Task ClosePageAsync()
    {
        if (_isClosing)
            return;

        _isClosing = true;

        try
        {
            _vm.SelectedPinPOI = null;
            TabMapComponent.HideSuggestions();
            await Navigation.PopAsync();
        }
        finally
        {
            _isClosing = false;
        }
    }

    private async void OnSettingsRequested(object? sender, EventArgs e)
    {
        try
        {
            if (Shell.Current is AppShell shell)
            {
                await Shell.Current.Navigation.PushModalAsync(shell.GetCachedSettingsPage());
                return;
            }

            var settingsPage = ResolveRequiredService<SettingsPage>();
            await Navigation.PushModalAsync(settingsPage);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ExploreMapPage] OnSettingsRequested: {ex}");
        }
    }

    private void OnZoomInRequested(object? sender, EventArgs e)
        => MapView?.Map?.Navigator.ZoomIn(300);

    private void OnZoomOutRequested(object? sender, EventArgs e)
        => MapView?.Map?.Navigator.ZoomOut(300);

    private void OnCenterMapRequested(object? sender, EventArgs e)
    {
        EnsureMapInitialized();
        CenterMapForCurrentState();
    }

    private async void OnViewDetailRequested(object? sender, POI poi)
    {
        if (poi == null)
            return;

        _vm.SelectedPinPOI = poi;
        _vm.PrimaryZone = poi;
        _vm.PrimaryZoneName = poi.Name_Vi ?? poi.Name_En ?? "—";
        _vm.PrimaryZoneDesc = poi.Description_Vi ?? poi.Description_En ?? "";
        _vm.PrimaryZoneAddress = poi.Address ?? "Đang cập nhật";
        _vm.PrimaryZoneRating = (poi.Rating ?? 4.5).ToString("F1");
        await Shell.Current.Navigation.PushModalAsync(new POIDetailPage(poi));
    }

    private void OnPrevPoiRequested(object? sender, POI poi)
    {
        var spots = GetNavigablePois();
        if (spots.Count == 0)
            return;

        var idx = spots.FindIndex(p => p.Id == poi.Id);
        var prevIdx = (idx - 1 + spots.Count) % spots.Count;
        var prevPoi = spots[prevIdx];
        _vm.SelectedPinPOI = prevPoi;
        CenterMapOnPoi(prevPoi);
        UpdateZonePins();
    }

    private void OnNextPoiRequested(object? sender, POI poi)
    {
        var spots = GetNavigablePois();
        if (spots.Count == 0)
            return;

        var idx = spots.FindIndex(p => p.Id == poi.Id);
        var nextIdx = (idx + 1) % spots.Count;
        var nextPoi = spots[nextIdx];
        _vm.SelectedPinPOI = nextPoi;
        CenterMapOnPoi(nextPoi);
        UpdateZonePins();
    }

    private async void OnLikeRequested(object? sender, POI poi)
    {
        if (poi == null)
            return;

        await _vm.ToggleSavePOICommand.ExecuteAsync(poi);
        UpdateZonePins();
        TabMapComponent.RefreshLikeIcon();
    }

    private void OnCategorySelected(object? sender, string category)
    {
        _vm.SelectCategoryCommand.Execute(category);
        TabMapComponent.HighlightSelectedChip(category);

        if (_vm.SelectedPinPOI != null && !_vm.FilteredPOIs.Any(p => p.Id == _vm.SelectedPinPOI.Id))
            _vm.SelectedPinPOI = null;

        UpdateZonePins();
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        _vm.SearchQuery = e.NewTextValue ?? string.Empty;
        _ = UpdateSuggestionsDropdownAsync();

        if (_vm.SelectedPinPOI != null && !_vm.FilteredPOIs.Any(p => p.Id == _vm.SelectedPinPOI.Id))
            _vm.SelectedPinPOI = null;

        UpdateZonePins();
    }

    private void OnClearSearchRequested(object? sender, EventArgs e)
    {
        _vm.SearchQuery = string.Empty;
        TabMapComponent.HideSuggestions();
        UpdateZonePins();
    }

    private void OnSuggestionSelected(object? sender, POI poi)
    {
        _vm.SearchQuery = poi.Name_Vi ?? poi.Name_En ?? string.Empty;
        TabMapComponent.HideSuggestions();
        CenterMapOnPoi(poi);
        _vm.VisitedPOIIds.Add(poi.Id);
        _vm.SelectedPinPOI = poi;
        UpdateZonePins();
    }

    private async Task UpdateSuggestionsDropdownAsync()
    {
        await Task.Delay(140);
        MainThread.BeginInvokeOnMainThread(() => TabMapComponent.ShowSuggestions(_vm.SearchSuggestions));
    }

    private void CenterMapOnPoi(POI poi)
    {
        if (MapView?.Map == null)
            return;

        var (px, py) = SphericalMercator.FromLonLat(poi.Longitude, poi.Latitude);
        MapView.Map.Navigator.CenterOn(new MPoint(px, py));
        ZoomToDefaultLevel();
    }

    private void CenterMapForCurrentState()
    {
        if (MapView?.Map == null)
            return;

        var shouldCenterOnFoodZone =
            _vm.CurrentExploreState == MainViewModel.ExploreState.Far ||
            _vm.CurrentLat == 0 ||
            _vm.CurrentLon == 0;

        if (shouldCenterOnFoodZone)
        {
            var (zoneX, zoneY) = SphericalMercator.FromLonLat(AppConfig.DefaultLongitude, AppConfig.DefaultLatitude);
            MapView.Map.Navigator.CenterOn(new MPoint(zoneX, zoneY));
            ZoomToDefaultLevel();
            return;
        }

        var (ux, uy) = SphericalMercator.FromLonLat(_vm.CurrentLon, _vm.CurrentLat);
        MapView.Map.Navigator.CenterOn(new MPoint(ux, uy));
        ZoomToDefaultLevel();
    }

    private List<POI> GetNavigablePois()
    {
        var filteredSpots = _vm.FilteredPOIs.Where(p => p.ZoneType == "Spot").ToList();
        if (filteredSpots.Count > 0)
            return filteredSpots;

        return _vm.AllPOIs.Where(p => p.ZoneType == "Spot").ToList();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.CurrentLat) ||
            e.PropertyName == nameof(MainViewModel.CurrentLon))
        {
            UpdateUserPin();
        }
        else if (e.PropertyName == nameof(MainViewModel.ActiveZoneCount) ||
                 e.PropertyName == nameof(MainViewModel.SelectedPinPOI))
        {
            UpdateZonePins();
        }
        else if (e.PropertyName == nameof(MainViewModel.Categories))
        {
            TabMapComponent.InitializeChips(_vm.SelectedCategory);
        }
    }

    private void OnPoisCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => UpdateZonePins();

    private static T ResolveRequiredService<T>() where T : notnull
    {
        var providers = new IServiceProvider?[]
        {
            Application.Current?.Handler?.MauiContext?.Services,
            Application.Current?.Windows.FirstOrDefault()?.Page?.Handler?.MauiContext?.Services,
            MauiProgram.Services
        };

        foreach (var provider in providers)
        {
            if (provider == null)
                continue;

            try
            {
                return provider.GetRequiredService<T>();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        throw new InvalidOperationException($"Unable to resolve service {typeof(T).Name}.");
    }
}
