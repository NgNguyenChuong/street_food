// ─────────────────────────────────────────────────────────────────────────────
// MainPage.Tour.cs  –  Real Mode (Tour) and Audio playback logic
// ─────────────────────────────────────────────────────────────────────────────

using Mapsui;
using Mapsui.Layers;
using Mapsui.Projections;
using Mapsui.Styles;
using Mapsui.Nts;
using NetTopologySuite.Geometries;
using MapsColor = Mapsui.Styles.Color;
using MapsBrush = Mapsui.Styles.Brush;
using StreetFoodNarrator.App.ViewModels;
using StreetFoodNarrator.App.Core.Services;
using StreetFoodNarrator.App.Core.Models;
using StreetFoodNarrator.App.Core.Utils;
using StreetFoodNarrator.App.Views.Components;
using StreetFoodNarrator.App.Helpers;
using StreetFoodNarrator.App.Resources.Strings;

namespace StreetFoodNarrator.App.Views;

public partial class MainPage
{
    private bool _isPlaying = false;
    private CancellationTokenSource? _progressCts;
    private int _currentSpotIndex = 0;
    private bool _pendingAutoStart = false;
    private double _prevSimLat = 0;
    private double _prevSimLon = 0;
    private int? _currentNarrationPoiId;
    private int? _pausedNarrationPoiId;
    private double _pausedNarrationPositionSeconds;
    private string? _pausedNarrationText;
    private string? _pausedNarrationLanguage;
    private DateTime _lastTapTime = DateTime.MinValue;
    private DateTime _lastSeekControlTapTime = DateTime.MinValue;

    private void WireTourEvents()
    {
        TabTourComponent.PlayPauseRequested    += OnPlayPauseTapped;
        TabTourComponent.RewindRequested       += OnRewindTapped;
        TabTourComponent.ForwardRequested      += OnForwardTapped;
        TabTourComponent.MuteChangeRequested   += OnMuteChange;
        TabTourComponent.SpeedChangeRequested  += OnSpeedChange;
        TabTourComponent.SeekBarDragCompleted  += OnSeekBarCompleted;
        TabTourComponent.VolumeChangeRequested += OnVolumeChange;

        TabTourComponent.StartVirtualTourRequested    += OnVTStart;
        TabTourComponent.StopVirtualTourRequested     += OnVTStop;
        TabTourComponent.ContinueVirtualTourRequested += OnVTContinue;
        TabTourComponent.RestartVirtualTourRequested  += OnVTRestart;
        TabTourComponent.ClearTourRequested           += OnClearTourRequested;

        _tts.OnPlaybackEnded += OnTtsPlaybackEnded;
    }

    private void OnTourPageLoaded()
    {
        if (_vm.AutoStartRequestedTour && _vm.CurrentAppMode == MainViewModel.AppMode.Virtual)
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

    private void OnTourAppearing()
    {
        Console.WriteLine("[Tour] OnAppearing");

        if (_vm.AutoStartRequestedTour && _vm.CurrentAppMode == MainViewModel.AppMode.Virtual)
        {
            _vm.AutoStartRequestedTour = false;
            OnVTStart(this, EventArgs.Empty);
        }
    }

    private void OnTourDisappearing()
    {
        Console.WriteLine("[Tour] OnDisappearing");
        StopVirtualTour(showChoice: false);
        StopProgressTimer();
        TabTourComponent.SetVirtualTourState(VirtualTourState.Idle);
    }

    // ─── Audio playback ───────────────────────────────────────────────────────

    private async void OnPlayPauseTapped(object? sender, EventArgs e)
    {
        // Debounce: ignore taps within 400ms of each other
        var now = DateTime.UtcNow;
        if ((now - _lastTapTime).TotalMilliseconds < 400) return;
        _lastTapTime = now;

        try
        {
            var zone = _vm.PrimaryZone;
            if (zone == null) return;

            if (_isPlaying)
            {
                // ✅ Pause audio properly in both real and virtual modes (do not force-stop virtual tour)
                _tts.Pause();
                _isPlaying = false;
                _vm.IsAudioPlaying = false;
                TabTourComponent.SetPlayState(false);
                TabTourComponent.StopWaveAnimation();
                StopProgressTimer();

                // Cache position để resume sau
                CacheNarrationResumeState(zone);
                _pausedNarrationPositionSeconds = _tts.GetCurrentPosition();
                return;
            }

            if (!_vm.IsPoiAccessibleForCurrentSubscription(zone))
            {
                await PremiumTourPaywallPage.ShowAsync(
                    Shell.Current?.Navigation ?? Navigation,
                    zone.GetDisplayName(_lang.CurrentLanguage),
                    _vm);
                return;
            }

            var (text, lang) = BuildNarrationPayload(zone);
            var canResumeCurrentPoi = _pausedNarrationPoiId == zone.Id &&
                                      string.Equals(_pausedNarrationText, text, StringComparison.Ordinal) &&
                                      string.Equals(_pausedNarrationLanguage, lang, StringComparison.Ordinal);
            var resumePosition = canResumeCurrentPoi
                ? _pausedNarrationPositionSeconds
                : 0;

            // ✅ If resuming same POI, just resume playback instead of re-speaking
            if (canResumeCurrentPoi && resumePosition > 0.5)
            {
                _tts.Resume();
                _isPlaying = true;
                _vm.IsAudioPlaying = true;
                TabTourComponent.SetPlayState(true);
                TabTourComponent.StartWaveAnimation();
                StartProgressTimer();
                ClearNarrationResumeState();
                if (_vm.CurrentAppMode == MainViewModel.AppMode.Virtual)
                {
                    await HandleVirtualJournalPlaybackStartedAsync(zone);
                }
                return;
            }

            var ok = await _tts.SpeakAsync(text, lang, poiId: zone.Id);
            _isPlaying = ok;
            _vm.IsAudioPlaying = ok;
            TabTourComponent.SetPlayState(ok);
            if (ok)
            {
                _currentNarrationPoiId = zone.Id;
                if (resumePosition > 0.5)
                {
                    await Task.Delay(180);
                    _tts.Seek(resumePosition);
                }

                await SaveTourHistoryAsync(zone);
                await SyncAudioDurationAsync();
                _vm.IsNarrating = true;
                TabTourComponent.StartWaveAnimation();
                StartProgressTimer();
                ClearNarrationResumeState();
                if (_vm.CurrentAppMode == MainViewModel.AppMode.Virtual)
                {
                    await HandleVirtualJournalPlaybackStartedAsync(zone);
                }
            }
            else
            {
                ResetNarrationUiState(resetProgress: false);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TourPage] OnPlayPauseTapped: {ex}");
            ResetNarrationUiState(resetProgress: false);
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

    private void OnRewindTapped(object? sender, EventArgs e)
    {
        // Debounce seek controls
        var now = DateTime.UtcNow;
        if ((now - _lastSeekControlTapTime).TotalMilliseconds < 300) return;
        _lastSeekControlTapTime = now;

        var positionSec = Math.Max(0, _tts.GetCurrentPosition() - 10);
        _tts.Seek(positionSec);
    }

    private void OnForwardTapped(object? sender, EventArgs e)
    {
        // Debounce seek controls
        var now = DateTime.UtcNow;
        if ((now - _lastSeekControlTapTime).TotalMilliseconds < 300) return;
        _lastSeekControlTapTime = now;

        var durationSec = _tts.GetDuration();
        var target = _tts.GetCurrentPosition() + 10;
        if (durationSec > 0)
            target = Math.Min(durationSec, target);
        _tts.Seek(target);
    }

    private void OnTtsPlaybackEnded()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _isPlaying = false;
            _currentNarrationPoiId = null;
            ClearNarrationResumeState();
            StopProgressTimer();
            _vm.IsAudioPlaying = false;
            _vm.AudioProgress    = 1.0;
            _vm.AudioTimeElapsed = _vm.AudioDuration;
            _vm.IsNarrating      = false;
            TabTourComponent.SetPlayState(false);
            TabTourComponent.StopWaveAnimation();
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
                var delayMs = _vm.CurrentAppMode == MainViewModel.AppMode.Virtual ? 750 : 500;
                await Task.Delay(delayMs, token).ConfigureAwait(false);
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

    private async Task SyncAudioDurationAsync(CancellationToken cancellationToken = default)
    {
        var delaysMs = new[] { 0, 100, 150, 250 };
        foreach (var delayMs in delaysMs)
        {
            if (delayMs > 0)
                await Task.Delay(delayMs, cancellationToken);

            var durationSec = _tts.GetDuration();
            if (durationSec > 0)
            {
                _vm.AudioDuration = FormatDuration(durationSec);
                return;
            }
        }
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

    // ─── Virtual Tour ──────────────────────────────────────────────────────────

    private void OnVTStart(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_vm.VirtualTourStatus))
        {
            _ = CustomAlert.ShowAsync(
                AppStrings.Get("Main_Virtual_NotAvailable_Title"),
                AppStrings.Get("Main_Virtual_NotAvailable_Message"),
                AppStrings.Get("Common_OK"),
                AlertType.Warning);
            return;
        }

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
            UpdateZonePins();
            TabTourComponent.SetVirtualTourState(VirtualTourState.Idle);
        });
    }

    private void OnVTContinue(object? sender, EventArgs e)
        => _ = StartVirtualTourAsync(_currentSpotIndex);

    private void OnVTRestart(object? sender, EventArgs e)
    {
        _vm.CurrentLat = 0;
        _vm.CurrentLon = 0;
        _prevSimLat = 0;
        _prevSimLon = 0;
        UpdateZonePins();
        _ = StartVirtualTourAsync(0);
    }

    private void OnClearTourRequested(object? sender, EventArgs e)
    {
        _vm.RequestedTourStops = null;
        _vm.ClearTourOverride();
        _vm.AutoStartRequestedTour = false;
        DisableVirtualTour();
    }

    private void StopVirtualTour(bool showChoice = false)
    {
        _virtualTourCts?.Cancel();
        if (showChoice)
            MainThread.BeginInvokeOnMainThread(() =>
                TabTourComponent.SetVirtualTourState(VirtualTourState.Choice));
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
            spots = _vm.GetRuntimeSpotPool().ToList();
            if (!_vm.HasActiveTourOverride)
            {
                double startLat = AppConfig.DefaultLatitude;
                double startLon = AppConfig.DefaultLongitude;
                spots = spots
                    .OrderBy(p =>
                    {
                        double dLat = p.Latitude - startLat;
                        double dLon = p.Longitude - startLon;
                        return Math.Sqrt(dLat * dLat + dLon * dLon);
                    })
                    .ToList();
            }
        }

        if (spots.Count == 0)
        {
            await CustomAlert.ShowAsync(
                AppStrings.Get("Alert_Notice_Title"),
                AppStrings.Get("Main_NoAttractions_Message"),
                AppStrings.Get("Common_OK"),
                AlertType.Info);
            return;
        }

        _vm.IsVirtualTourActive = true;
        MainThread.BeginInvokeOnMainThread(() =>
            TabTourComponent.SetVirtualTourState(VirtualTourState.Running));

        Console.WriteLine($"[TourPage] 🎬 Starting virtual tour from {startFromIndex}/{spots.Count}");

        bool completedNormally = false;
        try
        {
            for (int i = startFromIndex; i < spots.Count; i++)
            {
                if (token.IsCancellationRequested) break;

                _currentSpotIndex = i;
                var poi = spots[i];

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    TabTourComponent.UpdateVirtualTourStopLabel($"{i + 1}/{spots.Count} — {poi.GetDisplayName(_lang.CurrentLanguage)}");
                });

                double startLat, startLon;
                if (_prevSimLat == 0)
                {
                    startLat = AppConfig.DefaultLatitude;
                    startLon = AppConfig.DefaultLongitude;
                }
                else
                {
                    startLat = _prevSimLat;
                    startLon = _prevSimLon;
                }

                string destName = poi.GetDisplayName(_lang.CurrentLanguage);
                await SimulateWalkToAsync(startLat, startLon, poi.Latitude, poi.Longitude, token, destName);
                if (token.IsCancellationRequested) break;

                _vm.IsApproaching = false;
                _prevSimLat = _vm.CurrentLat;
                _prevSimLon = _vm.CurrentLon;

                _vm.PrimaryZone       = poi;
                _vm.PrimaryZoneName   = poi.GetDisplayName(_lang.CurrentLanguage);
                _vm.PrimaryZoneDesc   = poi.GetDisplayDescription(_lang.CurrentLanguage);
                _vm.PrimaryZoneAddress= poi.Address ?? "Địa chỉ đang cập nhật";
                _vm.PrimaryZoneRating = (poi.Rating ?? 4.5).ToString("F1");
                _vm.PrimaryZoneEmoji  = "🍽️";

                int idx = spots.FindIndex(p => p.Id == poi.Id);
                if (idx < 0)
                    idx = i;
                UpdateBottomSheetForVirtualStop(spots, idx, poi);
                _vm.CurrentStopBadge = idx >= 0 ? $"{idx + 1}/{spots.Count}" : "—";

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    UpdateZonePins();
                    if (MapView?.Map?.Navigator != null)
                    {
                        var (px, py) = Mapsui.Projections.SphericalMercator.FromLonLat(poi.Longitude, poi.Latitude);
                        MapView.Map.Navigator.CenterOn(new Mapsui.MPoint(px, py));
                    }
                });

                string lang = _lang.CurrentLanguage switch
                {
                    "en" => "en-US",
                    "zh" => "zh-CN",
                    _    => "vi-VN"
                };
                string text = poi.GetDisplayDescription(_lang.CurrentLanguage);
                if (string.IsNullOrWhiteSpace(text))
                    text = poi.GetDisplayName(_lang.CurrentLanguage);
                if (string.IsNullOrWhiteSpace(text))
                    text = Ui("Điểm tham quan", "Attraction", "景点");

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    _isPlaying = true;
                    _vm.IsAudioPlaying = true;
                    TabTourComponent.SetPlayState(true);
                    TabTourComponent.StartWaveAnimation();
                });

                await SaveTourHistoryAsync(poi);
                await _tts.SpeakAsync(text, lang, poiId: poi.Id);

                if (!token.IsCancellationRequested)
                {
                    await SyncAudioDurationAsync(token);
                    _vm.IsNarrating = true;
                    StartProgressTimer();
                }

                try { await Task.Delay(10_000, token); }
                catch (OperationCanceledException) { break; }

                StopProgressTimer();
                await _tts.StopAsync();
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    _isPlaying = false;
                    _vm.IsAudioPlaying = false;
                    _vm.IsNarrating = false;
                    _vm.AudioProgress = 0;
                    TabTourComponent.SetPlayState(false);
                    TabTourComponent.StopWaveAnimation();
                });

                try { await Task.Delay(1_500, token); }
                catch (OperationCanceledException) { break; }
            }

            completedNormally = !token.IsCancellationRequested;

            if (completedNormally)
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await CustomAlert.ShowAsync(
                        "Tour hoàn thành! 🎉",
                        $"Bạn đã tham quan tất cả {spots.Count} điểm ẩm thực trên Phố Vĩnh Khánh!",
                        "OK", AlertType.Success);
                });
            }
        }
        finally
        {
            _vm.IsVirtualTourActive = false;
            await _tts.StopAsync();
            _isPlaying = false;
            _vm.IsAudioPlaying = false;
            _currentNarrationPoiId = null;
            ClearNarrationResumeState();

            MainThread.BeginInvokeOnMainThread(() =>
            {
                TabTourComponent.SetPlayState(false);
                TabTourComponent.StopWaveAnimation();
                if (completedNormally)
                    TabTourComponent.SetVirtualTourState(VirtualTourState.Idle);
            });
        }
    }

    private static bool IsSamePoi(POI? first, POI? second)
        => first != null && second != null && first.Id == second.Id;

    private (string Text, string Language) BuildNarrationPayload(POI zone)
    {
        var language = _lang.CurrentLanguage switch
        {
            "en" => "en-US",
            "zh" => "zh-CN",
            _ => "vi-VN"
        };

        var text = zone.GetDisplayDescription(_lang.CurrentLanguage);
        if (string.IsNullOrWhiteSpace(text))
            text = zone.GetDisplayName(_lang.CurrentLanguage);
        if (string.IsNullOrWhiteSpace(text))
            text = Ui("Chao mung den voi diem tham quan.", "Welcome to this attraction.", "欢迎来到这个景点。");

        return (text, language);
    }

    private void CacheNarrationResumeState(POI zone)
    {
        var (text, language) = BuildNarrationPayload(zone);
        _pausedNarrationPoiId = zone.Id;
        _pausedNarrationText = text;
        _pausedNarrationLanguage = language;
        _pausedNarrationPositionSeconds = Math.Max(0, _tts.GetCurrentPosition());
    }

    private void ClearNarrationResumeState()
    {
        _pausedNarrationPoiId = null;
        _pausedNarrationText = null;
        _pausedNarrationLanguage = null;
        _pausedNarrationPositionSeconds = 0;
    }

    private void ResetNarrationUiState(bool resetProgress)
    {
        _isPlaying = false;
        _vm.IsAudioPlaying = false;
        _vm.IsNarrating = false;
        _currentNarrationPoiId = null;
        StopProgressTimer();
        TabTourComponent.SetPlayState(false);
        TabTourComponent.StopWaveAnimation();

        if (resetProgress)
        {
            _vm.AudioProgress = 0;
            _vm.AudioTimeElapsed = "0:00";
        }
    }

    private async Task StopNarrationAsync(bool resetProgress, bool clearResumeState)
    {
        await _tts.StopAsync();
        ResetNarrationUiState(resetProgress);

        if (clearResumeState)
        {
            ClearNarrationResumeState();
        }
    }

    private async Task SimulateWalkToAsync(
        double fromLat, double fromLon,
        double toLat,   double toLon,
        CancellationToken token,
        string destinationName = "",
        int steps  = 20,
        int stepMs = 250)
    {
        // Keep a little more distance from the POI center while still staying in the arrival zone.
        const double stopFraction = 0.60;

        _vm.ApproachingZoneName = destinationName;
        _vm.IsApproaching = true;

        for (int step = 1; step <= steps; step++)
        {
            if (token.IsCancellationRequested) return;

            double t = (double)step / steps * stopFraction;
            _vm.CurrentLat = fromLat + (toLat - fromLat) * t;
            _vm.CurrentLon = fromLon + (toLon - fromLon) * t;

            var distMeters = HaversineDistance(_vm.CurrentLat, _vm.CurrentLon, toLat, toLon);
            _vm.ApproachingDistance = ((int)Math.Round(distMeters)).ToString();

            var shouldRefreshMapThisStep = step == 1 || step == steps || step % 2 == 0;
            if (shouldRefreshMapThisStep)
            {
                double capLat = _vm.CurrentLat;
                double capLon = _vm.CurrentLon;
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    UpdateZonePins();
                    if (MapView?.Map?.Navigator != null)
                    {
                        var (ux, uy) = SphericalMercator.FromLonLat(capLon, capLat);
                        MapView.Map.Navigator.CenterOn(new MPoint(ux, uy));
                    }
                });
            }

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

    private void UpdateBottomSheetForVirtualStop(List<POI> spots, int idx, POI poi)
    {
        _vm.PrimaryZone = poi;
        _vm.PrimaryZoneName = poi.GetDisplayName(_lang.CurrentLanguage);
        _vm.PrimaryZoneType = poi.ZoneType ?? "Spot";
        _vm.PrimaryZoneDesc = poi.GetDisplayDescription(_lang.CurrentLanguage);
        _vm.PrimaryZoneAddress = poi.Address ?? "Địa chỉ đang cập nhật";
        _vm.PrimaryZoneRating = (poi.Rating ?? 4.5).ToString("F1");
        _vm.PrimaryZoneEmoji = "🍽️";
        _vm.CurrentStopIndex = idx + 1;
        _vm.CurrentStopBadge = $"{idx + 1}/{spots.Count}";

        var curDist = HaversineDistance(_vm.CurrentLat, _vm.CurrentLon, poi.Latitude, poi.Longitude);
        _vm.CurrentDistText = curDist < 1000 ? $"{curDist:F0}m" : $"{curDist / 1000:F1}km";
        _vm.CurrentWalkTimeText = $"~{Math.Max(1, (int)Math.Ceiling(curDist / 80.0))} phút";

        _vm.CategoryName = poi.Category ?? poi.Type ?? "Ẩm thực";
        _vm.ChipCategory = $"🍽️ {_vm.CategoryName}";
        var hours = poi.EstimatedHours ?? poi.OpeningHoursText;
        _vm.HasOpenHours = !string.IsNullOrWhiteSpace(hours);
        _vm.ChipOpenHours = _vm.HasOpenHours ? $"⏰ {hours}" : "";
        _vm.HasPrice = poi.AveragePrice.HasValue;
        _vm.ChipPrice = _vm.HasPrice ? $"💵 {poi.AveragePrice:N0}₫" : "";

        _vm.NextStop1Index = 0;
        _vm.NextStop1Name = "-";
        _vm.NextStop1Dist = "-";
        _vm.NextStop2Index = 0;
        _vm.NextStop2Name = "-";
        _vm.NextStop2Dist = "-";

        if (idx + 1 < spots.Count)
        {
            var next1 = spots[idx + 1];
            var d1 = HaversineDistance(_vm.CurrentLat, _vm.CurrentLon, next1.Latitude, next1.Longitude);
            _vm.NextStop1Index = idx + 2;
            _vm.NextStop1Name = next1.GetDisplayName(_lang.CurrentLanguage);
            _vm.NextStop1Dist = $"~{d1:F0}m • {Math.Max(1, (int)Math.Ceiling(d1 / 80.0))} phút";
        }

        if (idx + 2 < spots.Count)
        {
            var next2 = spots[idx + 2];
            var d2 = HaversineDistance(_vm.CurrentLat, _vm.CurrentLon, next2.Latitude, next2.Longitude);
            _vm.NextStop2Index = idx + 3;
            _vm.NextStop2Name = next2.GetDisplayName(_lang.CurrentLanguage);
            _vm.NextStop2Dist = $"~{d2:F0}m • {Math.Max(1, (int)Math.Ceiling(d2 / 80.0))} phút";
        }
    }
}
