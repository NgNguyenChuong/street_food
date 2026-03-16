// ─────────────────────────────────────────────────────────────────────────────
// TourPage.xaml.cs  –  Tab Tour (Hành trình)
// ─────────────────────────────────────────────────────────────────────────────

using Mapsui;
using Mapsui.Layers;
using Mapsui.Projections;
using Mapsui.Styles;
using Mapsui.Tiling;
using Mapsui.Tiling.Layers;
using BruTile.Predefined;
using Mapsui.Nts;
using NetTopologySuite.Geometries;
using MapsColor = Mapsui.Styles.Color;
using MapsBrush = Mapsui.Styles.Brush;
using StreetFoodNarrator.App.ViewModels;
using StreetFoodNarrator.App.Core.Services;
using StreetFoodNarrator.App.Core.Models;
using StreetFoodNarrator.App.Views.Components;

namespace StreetFoodNarrator.App.Views;

public partial class TourPage : ContentPage
{
    private readonly MainViewModel _vm;
    private readonly ITTSService _tts;
    private readonly LanguageService _lang;
    private MemoryLayer? _tourPinsLayer;
    private bool _mapInitialized = false;
    private bool _isPlaying = false;
    private CancellationTokenSource? _virtualTourCts;
    private CancellationTokenSource? _progressCts;
    private int _currentSpotIndex = 0;
    private bool _pendingAutoStart = false;
    private double _prevSimLat = 0;
    private double _prevSimLon = 0;
    private const int DefaultMapZoomLevel = (int)AppConfig.DefaultZoom + 1;

    private void ZoomToDefaultLevel()
    {
        if (TourMapView?.Map?.Navigator == null) return;

        var resolutions = TourMapView.Map.Navigator.Resolutions;
        if (resolutions == null) return;

        var maxLevel = Math.Max(0, resolutions.Count() - 1);
        var level = Math.Clamp(DefaultMapZoomLevel, 0, maxLevel);
        TourMapView.Map.Navigator.ZoomToLevel(level);
    }

    public TourPage()
    {
        InitializeComponent();
        _vm = MauiProgram.Services.GetRequiredService<MainViewModel>();
        _tts = MauiProgram.Services.GetRequiredService<ITTSService>();
        _lang = MauiProgram.Services.GetRequiredService<LanguageService>();
        BindingContext = _vm;
        WireComponentEvents();
        Loaded += OnPageLoaded;
        Console.WriteLine("[TourPage] Initialized");
    }

    private void WireComponentEvents()
    {
        TourContent.BackRequested      += OnBackClicked;
        TourContent.CenterMapRequested += OnCenterMapClicked;
        TourContent.ZoomInRequested    += OnZoomInClicked;
        TourContent.ZoomOutRequested   += OnZoomOutClicked;
        TourContent.PlayPauseRequested    += OnPlayPauseTapped;
        TourContent.ShareRequested        += OnShareTapped;
        TourContent.MuteChangeRequested   += OnMuteChange;
        TourContent.SpeedChangeRequested  += OnSpeedChange;
        TourContent.SeekBarDragCompleted  += OnSeekBarCompleted;
        TourContent.VolumeChangeRequested += OnVolumeChange;

        TourContent.StartVirtualTourRequested    += OnVTStart;
        TourContent.StopVirtualTourRequested     += OnVTStop;
        TourContent.ContinueVirtualTourRequested += OnVTContinue;
        TourContent.RestartVirtualTourRequested  += OnVTRestart;

        _tts.OnPlaybackEnded += OnTtsPlaybackEnded;
    }

    private void OnBackClicked(object? sender, EventArgs e)
    {
        try
        {
            if (Application.Current?.Windows.Count > 0)
            {
                Application.Current.Windows[0].Page = new NavigationPage(new WelcomePage());
            }
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Tour] OnBackClicked: {ex}"); }
    }

    private void OnCenterMapClicked(object? sender, EventArgs e)
    {
        if (TourMapView?.Map == null) return;

        if (_vm.CurrentLat != 0)
        {
            var (ux, uy) = SphericalMercator.FromLonLat(_vm.CurrentLon, _vm.CurrentLat);
            TourMapView.Map.Navigator.CenterOn(new MPoint(ux, uy));
        }
        else
        {
            var (cx, cy) = SphericalMercator.FromLonLat(
                AppConfig.DefaultLongitude, AppConfig.DefaultLatitude);
            TourMapView.Map.Navigator.CenterOn(new MPoint(cx, cy));
        }
    }

    private void OnZoomInClicked(object? sender, EventArgs e)
        => TourMapView?.Map?.Navigator.ZoomIn(300);

    private void OnZoomOutClicked(object? sender, EventArgs e)
        => TourMapView?.Map?.Navigator.ZoomOut(300);

    // ─── Audio playback ───────────────────────────────────────────────────────

    private async void OnPlayPauseTapped(object? sender, EventArgs e)
    {
        try
        {
            if (_isPlaying)
            {
                // Nếu đang trong virtual tour, dừng và hiện choice panel
                if (_vm.IsVirtualTourActive)
                {
                    StopVirtualTour(showChoice: true);
                    return;
                }

                await _tts.StopAsync();
                _isPlaying = false;
                StopProgressTimer();
                TourContent.SetPlayState(false);
                TourContent.StopWaveAnimation();
                _vm.IsNarrating = false;
                return;
            }

            var zone = _vm.PrimaryZone;
            if (zone == null) return;

            string lang = _lang.CurrentLanguage switch
            {
                "en" => "en-US",
                "zh" => "zh-CN",
                _    => "vi-VN"
            };

            var text = _lang.CurrentLanguage switch
            {
                "en" => zone.Description_En ?? zone.Name_En ?? zone.Name_Vi,
                "zh" => zone.Description_Zh ?? zone.Name_Zh ?? zone.Name_En ?? zone.Name_Vi,
                _    => zone.Description_Vi ?? zone.Name_Vi ?? zone.Name_En
            } ?? "Chao mung den voi diem tham quan.";

            var ok = await _tts.SpeakAsync(text, lang, poiId: zone.Id);
            _isPlaying = ok;
            TourContent.SetPlayState(ok);
            if (ok)
            {
                await SaveTourHistoryAsync(zone);
                // Plugin.Maui.Audio cần vài tick để điền Duration sau Play()
                await Task.Delay(150);
                var durationSec = _tts.GetDuration();
                if (durationSec > 0)
                    _vm.AudioDuration = FormatDuration(durationSec);
                _vm.IsNarrating = true;
                TourContent.StartWaveAnimation();
                StartProgressTimer();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TourPage] OnPlayPauseTapped: {ex}");
            _isPlaying = false;
            StopProgressTimer();
            TourContent.SetPlayState(false);
            TourContent.StopWaveAnimation();
        }
    }

    private void OnMuteChange(object? sender, bool isMuted)
        => _tts.SetVolume(isMuted ? 0.0 : 1.0);

    private void OnVolumeChange(object? sender, double volume)
        => _tts.SetVolume(volume);

    private void OnSpeedChange(object? sender, double speed)
        => _tts.SetSpeed(speed);

    private void OnSeekBarCompleted(object? sender, double progress)
    {
        var positionSec = progress * _tts.GetDuration();
        _tts.Seek(positionSec);
    }

    private void OnTtsPlaybackEnded()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _isPlaying = false;
            StopProgressTimer();
            _vm.AudioProgress    = 1.0;
            _vm.AudioTimeElapsed = _vm.AudioDuration;
            _vm.IsNarrating      = false;
            TourContent.SetPlayState(false);
            TourContent.StopWaveAnimation();
        });
    }

    private void StartProgressTimer()
    {
        StopProgressTimer();
        _progressCts = new CancellationTokenSource();
        var token = _progressCts.Token;
        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(500, token).ConfigureAwait(false);
                if (token.IsCancellationRequested) break;
                var pos      = _tts.GetCurrentPosition();
                var duration = _tts.GetDuration();
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    _vm.AudioTimeElapsed = FormatDuration(pos);
                    if (duration > 0)
                        _vm.AudioProgress = Math.Clamp(pos / duration, 0.0, 1.0);
                });
            }
        }, token);
    }

    private void StopProgressTimer()
    {
        _progressCts?.Cancel();
        _progressCts = null;
    }

    private static string FormatDuration(double seconds)
    {
        var t = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return t.TotalMinutes >= 60
            ? $"{(int)t.TotalHours}:{t.Minutes:D2}:{t.Seconds:D2}"
            : $"{(int)t.TotalMinutes}:{t.Seconds:D2}";
    }

    private async Task SaveTourHistoryAsync(POI poi)
    {
        try
        {
            var db = MauiProgram.Services.GetRequiredService<ILocalDatabaseService>();
            var session = MauiProgram.Services.GetRequiredService<UserSession>();
            var now = DateTime.UtcNow;
            var history = await db.GetZoneHistoryAsync(session.SessionId, poi.Id);
            if (history == null)
            {
                history = new ZoneHistory
                {
                    POI_ID = poi.Id,
                    FirstPlayedAt = now,
                    LastTriggeredAt = now,
                    PlayCount = 1,
                    Language = "vi",
                    SessionId = session.SessionId
                };
            }
            else
            {
                history.LastTriggeredAt = now;
                history.PlayCount += 1;
            }

            await db.SaveZoneHistoryAsync(history);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TourPage] Save history error: {ex.Message}");
        }
    }

    private async void OnShareTapped(object? sender, EventArgs e)
    {
        try
        {
            await Share.Default.RequestAsync(new ShareTextRequest
            {
                Title = "Chia se diem den",
                Text  = $"Toi dang o {_vm.PrimaryZoneName} trong tour am thuc!",
                Uri   = "https://streetfoodnarrator.app"
            });
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[TourPage] OnShareTapped: {ex}"); }
    }

    private void OnPageLoaded(object? sender, EventArgs e)
    {
        // Khởi tạo bản đồ chỉ một lần khi page load
        if (!_mapInitialized)
        {
            _mapInitialized = true;
            try
            {
                InitializeTourMap();
                // OnAppearing fires BEFORE Loaded, so pins weren't added
                // (because _tourPinsLayer was still null). Load them now.
                if (_vm.AllPOIs.Count > 0)
                {
                    UpdateTourPins();
                }
                else
                {
                    _ = _vm.LoadAllPoisAsync().ContinueWith(_ =>
                        MainThread.BeginInvokeOnMainThread(UpdateTourPins));
                }
            }
            catch (Exception ex) { Console.WriteLine($"[TourPage] InitializeTourMap error: {ex.Message}"); }
        }

        if (_vm.AutoStartRequestedTour)
        {
            _vm.AutoStartRequestedTour = false;
            _pendingAutoStart = true;
        }

        if (_pendingAutoStart)
        {
            _pendingAutoStart = false;
            OnVTStart(this, EventArgs.Empty);
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Console.WriteLine("[TourPage] OnAppearing");
        TourContent.IsVisible = true;

        // Tải POIs nếu chưa có và cập nhật pins
        if (_vm.AllPOIs.Count == 0)
        {
            _ = _vm.LoadAllPoisAsync().ContinueWith(_ =>
                MainThread.BeginInvokeOnMainThread(UpdateTourPins));
        }
        else
        {
            UpdateTourPins();
        }

        if (_vm.AutoStartRequestedTour)
        {
            if (_mapInitialized)
            {
                _vm.AutoStartRequestedTour = false;
                OnVTStart(this, EventArgs.Empty);
            }
            else
            {
                _pendingAutoStart = true;
            }
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        Console.WriteLine("[TourPage] OnDisappearing");
        StopVirtualTour(showChoice: false);
        StopProgressTimer();
        TourContent.SetVirtualTourState(VirtualTourState.Idle);
    }

    // ─── Virtual Tour ──────────────────────────────────────────────────────────

    private void OnVTStart(object? sender, EventArgs e)
    {
        _currentSpotIndex = 0;
        _ = StartVirtualTourAsync(0);
    }

    private void OnVTStop(object? sender, EventArgs e)
    {
        _virtualTourCts?.Cancel();
        _vm.CurrentLat = 0;
        _vm.CurrentLon = 0;
        _prevSimLat = 0;
        _prevSimLon = 0;
        _vm.IsApproaching = false;
        MainThread.BeginInvokeOnMainThread(() =>
        {
            UpdateTourPins();
            TourContent.SetVirtualTourState(VirtualTourState.Idle);
        });
    }

    private void OnVTContinue(object? sender, EventArgs e)
        => _ = StartVirtualTourAsync(_currentSpotIndex);

    private void OnVTRestart(object? sender, EventArgs e)
    {
        // Reset location point to start position
        _vm.CurrentLat = 0;
        _vm.CurrentLon = 0;
        _prevSimLat = 0;
        _prevSimLon = 0;
        UpdateTourPins();
        _ = StartVirtualTourAsync(0);
    }

    private void StopVirtualTour(bool showChoice = false)
    {
        _virtualTourCts?.Cancel();
        if (showChoice)
            MainThread.BeginInvokeOnMainThread(() =>
                TourContent.SetVirtualTourState(VirtualTourState.Choice));
    }

    private async Task StartVirtualTourAsync(int startFromIndex = 0)
    {
        _virtualTourCts?.Cancel();
        _virtualTourCts = new CancellationTokenSource();
        var token = _virtualTourCts.Token;

        List<POI> spots;
        if (_vm.RequestedTourStops != null && _vm.RequestedTourStops.Count > 0)
        {
            spots = _vm.RequestedTourStops;
            _vm.RequestedTourStops = null;
        }
        else
        {
            // Sắp xếp các Spot POI theo khoảng cách từ điểm xuất phát mặc định
            double startLat = AppConfig.DefaultLatitude;
            double startLon = AppConfig.DefaultLongitude;

            spots = _vm.AllPOIs
                .Where(p => p.ZoneType == "Spot")
                .OrderBy(p =>
                {
                    double dLat = p.Latitude  - startLat;
                    double dLon = p.Longitude - startLon;
                    return Math.Sqrt(dLat * dLat + dLon * dLon);
                })
                .ToList();
        }

        if (spots.Count == 0)
        {
            await DisplayAlert("Thông báo", "Không có điểm tham quan nào.", "OK");
            return;
        }

        _vm.IsVirtualTourActive = true;
        MainThread.BeginInvokeOnMainThread(() =>
            TourContent.SetVirtualTourState(VirtualTourState.Running));

        Console.WriteLine($"[TourPage] 🎬 Starting virtual tour from {startFromIndex}/{spots.Count}");

        bool completedNormally = false;
        try
        {
            for (int i = startFromIndex; i < spots.Count; i++)
            {
                if (token.IsCancellationRequested) break;

                _currentSpotIndex = i;
                var poi = spots[i];

                // Cập nhật trạng thái UI
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    TourContent.UpdateVirtualTourStopLabel($"{i + 1}/{spots.Count} — {poi.Name_Vi ?? poi.Name_En}");
                });

                // Đặt vị trí GPS giả — mô phỏng bước đi từ vị trí trước → gần quán
                double startLat, startLon;
                if (_prevSimLat == 0)
                {
                    // Điểm đầu tiên: xuất phát từ tọa độ mặc định của khu vực
                    startLat = AppConfig.DefaultLatitude;
                    startLon = AppConfig.DefaultLongitude;
                }
                else
                {
                    startLat = _prevSimLat;
                    startLon = _prevSimLon;
                }

                string destName = poi.Name_Vi ?? poi.Name_En ?? "";
                await SimulateWalkToAsync(startLat, startLon, poi.Latitude, poi.Longitude, token, destName);
                if (token.IsCancellationRequested) break;

                // Đã "đến nơi" — tắt banner approaching
                _vm.IsApproaching = false;

                // Lưu vị trí "đã đến" (dừng trước quán một chút, do SimulateWalkToAsync)
                _prevSimLat = _vm.CurrentLat;
                _prevSimLon = _vm.CurrentLon;

                // Cập nhật PrimaryZone và các field hiển thị
                _vm.PrimaryZone       = poi;
                _vm.PrimaryZoneName   = poi.Name_Vi ?? poi.Name_En ?? "—";
                _vm.PrimaryZoneDesc   = poi.Description_Vi ?? poi.Description_En ?? "Không có mô tả.";
                _vm.PrimaryZoneAddress= poi.Address ?? "Địa chỉ đang cập nhật";
                _vm.PrimaryZoneRating = (poi.Rating ?? 4.5).ToString("F1");
                _vm.PrimaryZoneEmoji  = "🍽️";

                // Cập nhật badge stop
                int idx = spots.FindIndex(p => p.Id == poi.Id);
                _vm.CurrentStopBadge = idx >= 0 ? $"{idx + 1}/{spots.Count}" : "—";

                // Căn giữa bản đồ về quán này
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    UpdateTourPins();
                    if (TourMapView?.Map?.Navigator != null)
                    {
                        var (px, py) = Mapsui.Projections.SphericalMercator.FromLonLat(poi.Longitude, poi.Latitude);
                        TourMapView.Map.Navigator.CenterOn(new Mapsui.MPoint(px, py));
                    }
                });

                // Phát audio
                string lang = _lang.CurrentLanguage switch
                {
                    "en" => "en-US",
                    "zh" => "zh-CN",
                    _    => "vi-VN"
                };
                string text = (_lang.CurrentLanguage switch
                {
                    "en" => poi.Description_En ?? poi.Name_En,
                    "zh" => poi.Description_Zh ?? poi.Name_Zh ?? poi.Name_Vi,
                    _    => poi.Description_Vi ?? poi.Name_Vi
                }) ?? poi.Name_Vi ?? "Điểm tham quan";

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    _isPlaying = true;
                    TourContent.SetPlayState(true);
                    TourContent.StartWaveAnimation();
                });

                await SaveTourHistoryAsync(poi);
                await _tts.SpeakAsync(text, lang, poiId: poi.Id);

                // Đợi audio sẵn sàng, lấy duration và bắt đầu progress timer
                if (!token.IsCancellationRequested)
                {
                    await Task.Delay(150);
                    var durationSec = _tts.GetDuration();
                    if (durationSec > 0)
                        _vm.AudioDuration = FormatDuration(durationSec);
                    _vm.IsNarrating = true;
                    StartProgressTimer();
                }

                // Đứng lại 10 giây tại quán (có thể bị interrupt bởi token)
                try { await Task.Delay(10_000, token); }
                catch (OperationCanceledException) { break; }

                // Dừng audio, progress timer, chuyển quán tiếp theo
                StopProgressTimer();
                await _tts.StopAsync();
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    _isPlaying = false;
                    _vm.IsNarrating = false;
                    _vm.AudioProgress = 0;
                    TourContent.SetPlayState(false);
                    TourContent.StopWaveAnimation();
                });

                Console.WriteLine($"[TourPage] ✓ Completed spot {i + 1}/{spots.Count}: {poi.Name_Vi}");

                // Nghỉ 1.5s trước khi chuyển
                try { await Task.Delay(1_500, token); }
                catch (OperationCanceledException) { break; }
            }

            completedNormally = !token.IsCancellationRequested;

            // Hoàn thành tất cả điểm
            if (completedNormally)
            {
                Console.WriteLine("[TourPage] 🎉 Virtual tour completed!");
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await DisplayAlert(
                        "Tour hoàn thành! 🎉",
                        $"Bạn đã tham quan tất cả {spots.Count} điểm ẩm thực trên Phố Vĩnh Khánh!",
                        "OK");
                });
            }
        }
        finally
        {
            _vm.IsVirtualTourActive = false;
            await _tts.StopAsync();
            _isPlaying = false;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                TourContent.SetPlayState(false);
                TourContent.StopWaveAnimation();
                // Nếu tour chạy xong tự nhiên → về Idle; nếu bị cancel → OnVTStop đã set Choice
                if (completedNormally)
                    TourContent.SetVirtualTourState(VirtualTourState.Idle);
            });

            Console.WriteLine("[TourPage] Virtual tour stopped/completed.");
        }
    }

    // ─── Mô phỏng đi bộ đến quán ─────────────────────────────────────────────

    /// <summary>
    /// Nội suy vị trí người dùng từ (fromLat,fromLon) → 85% đường đến (toLat,toLon)
    /// theo <paramref name="steps"/> bước, mỗi bước cách nhau <paramref name="stepMs"/> ms.
    /// Cập nhật _vm.CurrentLat/Lon và bản đồ sau mỗi bước.
    /// </summary>
    private async Task SimulateWalkToAsync(
        double fromLat, double fromLon,
        double toLat,   double toLon,
        CancellationToken token,
        string destinationName = "",
        int steps  = 20,
        int stepMs = 250)
    {
        const double stopFraction = 0.85; // dừng trước quán ~15%

        // Hiện banner + đặt tên điểm đến
        _vm.ApproachingZoneName = destinationName;
        _vm.IsApproaching = true;

        for (int step = 1; step <= steps; step++)
        {
            if (token.IsCancellationRequested) return;

            double t = (double)step / steps * stopFraction;
            _vm.CurrentLat = fromLat + (toLat - fromLat) * t;
            _vm.CurrentLon = fromLon + (toLon - fromLon) * t;

            // Cập nhật khoảng cách thực tế đến điểm đến
            var distMeters = HaversineDistance(_vm.CurrentLat, _vm.CurrentLon, toLat, toLon);
            _vm.ApproachingDistance = ((int)Math.Round(distMeters)).ToString();

            double capLat = _vm.CurrentLat;
            double capLon = _vm.CurrentLon;
            MainThread.BeginInvokeOnMainThread(() =>
            {
                UpdateTourPins();
                if (TourMapView?.Map?.Navigator != null)
                {
                    var (ux, uy) = SphericalMercator.FromLonLat(capLon, capLat);
                    TourMapView.Map.Navigator.CenterOn(new MPoint(ux, uy));
                }
            });

            try { await Task.Delay(stepMs, token); }
            catch (OperationCanceledException) { return; }
        }
    }

    private static double HaversineDistance(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371000;
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLon = (lon2 - lon1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180)
              * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    // ─── Khởi tạo bản đồ Tour ─────────────────────────────────────────────────

    private void InitializeTourMap()
    {
        if (TourMapView?.Map == null) return;

        // Tile layer OSM với SQLite cache offline
        try
        {
            var cacheDir  = Path.Combine(FileSystem.AppDataDirectory, "tile_cache");
            Directory.CreateDirectory(cacheDir);
            var cacheDb   = Path.Combine(cacheDir, "osm_tour.db");
            var tileCache = new SqliteTileCache(cacheDb);
            var tileSource = KnownTileSources.Create(
                KnownTileSource.OpenStreetMap,
                persistentCache: tileCache);
            TourMapView.Map.Layers.Add(new TileLayer(tileSource) { Name = "OSM" });
            Console.WriteLine("[TourPage] ✓ OSM tile layer added");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TourPage] ⚠️ OSM offline: {ex.Message}");
        }

        // Dark overlay để hòa hợp theme tối
        var darkOverlay = new MemoryLayer("DarkOverlay");
        var worldCoords = new[]
        {
            new Coordinate(-20037508.34, -20037508.34),
            new Coordinate(20037508.34, -20037508.34),
            new Coordinate(20037508.34, 20037508.34),
            new Coordinate(-20037508.34, 20037508.34),
            new Coordinate(-20037508.34, -20037508.34)
        };
        var worldPoly = new Polygon(new LinearRing(worldCoords));
        darkOverlay.Features = new[] { new GeometryFeature { Geometry = worldPoly } };
        darkOverlay.Style = new VectorStyle
        {
            Fill    = new MapsBrush(new MapsColor(8, 22, 12, 180)),
            Outline = null
        };
        TourMapView.Map.Layers.Add(darkOverlay);

        // Layer pins
        _tourPinsLayer = new MemoryLayer("TourPins");
        TourMapView.Map.Layers.Add(_tourPinsLayer);

        // Cấu hình bản đồ
        TourMapView.Map.Widgets.Clear();
        TourMapView.Map.Navigator.RotationLock = true;
        TourMapView.UseFling = true;
        TourMapView.InputTransparent = false;
        TourMapView.CascadeInputTransparent = false;

        // Căn giữa Vĩnh Khánh
        Dispatcher.Dispatch(async () =>
        {
            await Task.Delay(150);
            var (cx, cy) = SphericalMercator.FromLonLat(
                AppConfig.DefaultLongitude, AppConfig.DefaultLatitude);
            TourMapView?.Map?.Navigator.CenterOn(new MPoint(cx, cy));
            ZoomToDefaultLevel();
        });

        Console.WriteLine("[TourPage] ✓ InitializeTourMap completed");
    }

    // ─── Cập nhật pins bản đồ Tour ───────────────────────────────────────────

    private void UpdateTourPins()
    {
        if (_tourPinsLayer == null || TourMapView?.Map == null) return;

        var features = new List<IFeature>();
        var activePOIId = _vm.PrimaryZone?.Id ?? -1;

        // Vị trí người dùng – chấm xanh lá
        if (_vm.CurrentLat != 0)
        {
            var (ux, uy) = SphericalMercator.FromLonLat(_vm.CurrentLon, _vm.CurrentLat);
            var userFeature = new PointFeature(new MPoint(ux, uy));
            userFeature.Styles.Add(new SymbolStyle
            {
                SymbolScale = 0.6,
                Fill        = new MapsBrush(new MapsColor(34, 197, 94)),
                Outline     = new Pen(MapsColor.White, 3)
            });
            features.Add(userFeature);
        }

        // Tất cả POI Spot – highlight active POI bằng màu vàng cam nổi bật
        foreach (var poi in _vm.AllPOIs)
        {
            if (poi.ZoneType == "Area" || poi.ZoneType == "District") continue;

            var (px, py) = SphericalMercator.FromLonLat(poi.Longitude, poi.Latitude);
            var f = new PointFeature(new MPoint(px, py));
            f["POI_ID"] = poi.Id;

            // Active POI: vàng lớn + pulse ring | Đã ghé: tím | Còn lại: cam
            bool isActive  = poi.Id == activePOIId;
            bool isVisited = !isActive && _vm.VisitedPOIIds.Contains(poi.Id);
            var fillColor = isActive
                ? new MapsColor(251, 191, 36)     // Vàng  – điểm hiện tại
                : isVisited
                    ? new MapsColor(147, 51, 234) // Tím   – đã ghé
                    : new MapsColor(249, 115, 22);// Cam   – chưa ghé

            // Glow ring
            f.Styles.Add(new SymbolStyle
            {
                SymbolScale = isActive ? 1.4 : isVisited ? 1.15 : 1.0,
                Fill        = new MapsBrush(new MapsColor(fillColor.R, fillColor.G, fillColor.B, isActive ? 130 : isVisited ? 100 : 70)),
                Outline     = null,
                SymbolType  = SymbolType.Ellipse
            });
            // Dot chính
            f.Styles.Add(new SymbolStyle
            {
                SymbolScale = isActive ? 0.7 : isVisited ? 0.52 : 0.45,
                Fill        = new MapsBrush(fillColor),
                Outline     = new Pen(MapsColor.White, isActive ? 3f : 2f),
                SymbolType  = SymbolType.Ellipse
            });
            // Label tên
            var label = poi.Name_Vi ?? poi.Name_En ?? "";
            if (label.Length > 15) label = label[..15];
            f.Styles.Add(new LabelStyle
            {
                Text              = label,
                ForeColor         = MapsColor.White,
                BackColor         = new MapsBrush(new MapsColor(10, 16, 12, 210)),
                Font              = new Mapsui.Styles.Font { FontFamily = "sans-serif", Size = isActive ? 9 : 8, Bold = isActive },
                Offset            = new Offset(0, isActive ? 22 : 18),
                HorizontalAlignment = LabelStyle.HorizontalAlignmentEnum.Center,
                VerticalAlignment = LabelStyle.VerticalAlignmentEnum.Top,
                MaxWidth          = 90,
                WordWrap          = LabelStyle.LineBreakMode.NoWrap
            });

            features.Add(f);
        }

        _tourPinsLayer.Features = features;
        _tourPinsLayer.DataHasChanged();
        TourMapView.RefreshGraphics();
        Console.WriteLine($"[TourPage] ✓ UpdateTourPins: {features.Count} features");
    }
}
