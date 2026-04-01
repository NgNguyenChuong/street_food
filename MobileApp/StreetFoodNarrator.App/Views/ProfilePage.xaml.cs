using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Storage;
using Microsoft.Maui.Networking;
using StreetFoodNarrator.App;
using StreetFoodNarrator.App.Core.Models;
using StreetFoodNarrator.App.Core.Services;
using StreetFoodNarrator.App.Helpers;
using StreetFoodNarrator.App.ViewModels;

namespace StreetFoodNarrator.App.Views;

public partial class ProfilePage : ContentPage
{
    public ObservableCollection<TourHistorySessionItem> TourHistories { get; } = new();
    public Command<TourHistorySessionItem> ReplayTourCommand { get; }

    public ProfilePage()
    {
        InitializeComponent();
        ReplayTourCommand = new Command<TourHistorySessionItem>(OnReplayTour);
        BindingContext = this;
        Console.WriteLine("[ProfilePage] Initialized");
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        Console.WriteLine("[ProfilePage] OnAppearing");
        Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;
        UpdateOfflineBanner();
        await LoadTourHistoryAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        Connectivity.Current.ConnectivityChanged -= OnConnectivityChanged;
    }

    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
        => MainThread.BeginInvokeOnMainThread(UpdateOfflineBanner);

    private void UpdateOfflineBanner()
    {
        var offline = Connectivity.Current.NetworkAccess != NetworkAccess.Internet &&
                      Connectivity.Current.NetworkAccess != NetworkAccess.ConstrainedInternet;
        OfflineBanner.IsVisible = offline && !OfflineBannerSessionState.IsDismissed;
    }

    private void OnDismissOfflineBannerClicked(object sender, EventArgs e)
    {
        OfflineBannerSessionState.IsDismissed = true;
        UpdateOfflineBanner();
    }

    private async Task LoadTourHistoryAsync()
    {
        try
        {
            var db = MauiProgram.Services.GetRequiredService<ILocalDatabaseService>();
            var repo = MauiProgram.Services.GetRequiredService<IZoneRepository>();
            await repo.LoadLocalAsync();

            var poiMap = repo.GetAllActiveZones().ToDictionary(p => p.Id, p => p);
            var histories = await db.GetAllZoneHistoriesAsync();

            var grouped = histories
                .Where(h => !string.IsNullOrWhiteSpace(h.SessionId))
                .GroupBy(h => h.SessionId)
                .OrderByDescending(g => g.Max(x => x.LastTriggeredAt))
                .ToList();

            MainThread.BeginInvokeOnMainThread(() =>
            {
                TourHistories.Clear();
                foreach (var g in grouped)
                {
                    var sessionId = g.Key;
                    var start = g.Min(x => x.FirstPlayedAt);
                    var end = g.Max(x => x.LastTriggeredAt);
                    var duration = end - start;
                    var totalStops = g.Select(x => x.POI_ID).Distinct().Count();
                    var totalPlays = g.Sum(x => x.PlayCount);

                    var stops = g
                        .OrderBy(x => x.LastTriggeredAt)
                        .Select(x =>
                        {
                            poiMap.TryGetValue(x.POI_ID, out var poi);
                            var name = poi?.Name_Vi ?? poi?.Name_En ?? $"POI #{x.POI_ID}";
                            return new TourHistoryStopItem
                            {
                                Poi = poi,
                                Name = name,
                                TimeText = x.LastTriggeredAt.ToLocalTime().ToString("HH:mm"),
                                PlayCount = x.PlayCount
                            };
                        })
                        .ToList();

                    var item = new TourHistorySessionItem(sessionId)
                    {
                        Title = $"Tour ngày {start.ToLocalTime():dd/MM/yyyy}",
                        Summary = $"{totalStops} điểm • {FormatDuration(duration)} • {totalPlays} lần phát",
                        Stops = new ObservableCollection<TourHistoryStopItem>(stops),
                        Rating = Preferences.Get($"tour_rating_{sessionId}", 0d),
                        Note = Preferences.Get($"tour_note_{sessionId}", string.Empty)
                    };

                    TourHistories.Add(item);
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProfilePage] Load history error: {ex}");
        }
    }

    private async void OnReplayTour(TourHistorySessionItem? session)
    {
        if (session == null || session.Stops.Count == 0)
            return;

        var vm = MauiProgram.Services.GetRequiredService<MainViewModel>();
        var stops = session.Stops
            .Select(s => s.Poi)
            .Where(p => p != null)
            .Cast<POI>()
            .ToList();

        if (stops.Count == 0)
            return;

        vm.RequestedTourStops = stops;
        vm.AutoStartRequestedTour = true;

        if (Shell.Current != null)
            await Shell.Current.GoToAsync("//MapPage");
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalMinutes < 1) return "<1 phút";
        if (duration.TotalHours < 1) return $"{(int)duration.TotalMinutes} phút";
        return $"{(int)duration.TotalHours} giờ {duration.Minutes} phút";
    }

    private async void OnSettingsTapped(object sender, EventArgs e)
    {
        if (Shell.Current is AppShell shell)
        {
            await Shell.Current.Navigation.PushModalAsync(shell.GetCachedSettingsPage());
            return;
        }

        await Navigation.PushModalAsync(new SettingsPage());
    }
}

public class TourHistorySessionItem : INotifyPropertyChanged
{
    private double _rating;
    private string _note = string.Empty;

    public TourHistorySessionItem(string sessionId)
    {
        SessionId = sessionId;
    }

    public string SessionId { get; }
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public ObservableCollection<TourHistoryStopItem> Stops { get; set; } = new();

    public double Rating
    {
        get => _rating;
        set
        {
            if (Math.Abs(_rating - value) < 0.001) return;
            _rating = value;
            Preferences.Set($"tour_rating_{SessionId}", value);
            OnPropertyChanged();
        }
    }

    public string Note
    {
        get => _note;
        set
        {
            if (_note == value) return;
            _note = value ?? string.Empty;
            Preferences.Set($"tour_note_{SessionId}", _note);
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class TourHistoryStopItem
{
    public POI? Poi { get; set; }
    public string Name { get; set; } = string.Empty;
    public string TimeText { get; set; } = string.Empty;
    public int PlayCount { get; set; }
}
