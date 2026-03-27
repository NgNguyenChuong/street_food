using CommunityToolkit.Mvvm.ComponentModel;
using StreetFoodNarrator.App.Core.Services;

namespace StreetFoodNarrator.App.ViewModels;

public partial class VirtualTourViewModel : ObservableObject, IVirtualTourViewModel
{
    private readonly ITourEngagementService _engagementService;
    private readonly IPopupService _popupService;

    private CancellationTokenSource? _idlePromptCts;
    private DateTime _lastInteractionUtc = DateTime.UtcNow;
    private bool _sessionActive;
    private bool _promptInFlight;
    private bool _promptQueued;
    private DateTime? _promptQueuedAtUtc;
    private DateTime _lastEligibilityCheckUtc = DateTime.MinValue;

    private Func<double>? _distanceProviderMeters;
    private Func<Task>? _switchToRealModeAsync;
    private Action? _switchToExploreFar;

    public VirtualTourViewModel(
        ITourEngagementService engagementService,
        IPopupService popupService)
    {
        _engagementService = engagementService;
        _popupService = popupService;
    }

    private const double NearDistanceMeters = 500.0;

    [ObservableProperty] private int viewedPoiCount;
    [ObservableProperty] private double sessionSeconds;
    [ObservableProperty] private DateTime? lastPromptShownUtc;

    public void ConfigureContext(Func<double> distanceProviderMeters, Func<Task> switchToRealModeAsync, Action switchToExploreFar)
    {
        _distanceProviderMeters = distanceProviderMeters;
        _switchToRealModeAsync = switchToRealModeAsync;
        _switchToExploreFar = switchToExploreFar;
        LastPromptShownUtc = _popupService.GetLastShownTime();
        ViewedPoiCount = _engagementService.GetViewedPoiCount();
        SessionSeconds = _engagementService.GetTotalVirtualTime().TotalSeconds;
    }

    public void StartVirtualTourSession()
    {
        if (_sessionActive)
            return;

        _sessionActive = true;
        _promptQueued = false;
        _promptQueuedAtUtc = null;
        _promptInFlight = false;
        _lastEligibilityCheckUtc = DateTime.MinValue;

        _engagementService.StartSession();
        RegisterInteraction();
        StartIdleWatcher();

        ViewedPoiCount = _engagementService.GetViewedPoiCount();
        SessionSeconds = _engagementService.GetTotalVirtualTime().TotalSeconds;
    }

    public async Task<VirtualPromptDecision> EndVirtualTourSessionAsync(bool evaluatePromptOnExit)
    {
        if (!_sessionActive)
            return VirtualPromptDecision.NotShown;

        StopIdleWatcher();
        _sessionActive = false;
        _engagementService.EndSession();

        ViewedPoiCount = _engagementService.GetViewedPoiCount();
        SessionSeconds = _engagementService.GetTotalVirtualTime().TotalSeconds;

        if (!evaluatePromptOnExit || _promptInFlight)
            return VirtualPromptDecision.NotShown;

        return await TryShowPromptAsync(VirtualPromptTrigger.Exit);
    }

    public void OnPoiViewed(int poiId)
    {
        if (poiId <= 0)
            return;

        _engagementService.TrackPOIViewed(poiId);
        ViewedPoiCount = _engagementService.GetViewedPoiCount();
        SessionSeconds = _engagementService.GetTotalVirtualTime().TotalSeconds;

        // Queue the prompt (idle watcher will show it after 1 second idle)
        EvaluateEligibilityAndQueuePrompt(force: false);
        // Reset idle timer so popup waits for a short idle moment
        RegisterInteraction();
    }

    public void RegisterInteraction()
    {
        _lastInteractionUtc = DateTime.UtcNow;
    }

    public void OnAppBackgrounded()
    {
        if (!_sessionActive)
            return;

        _engagementService.EndSession();
        StopIdleWatcher();
    }

    public void OnAppResumed()
    {
        if (!_sessionActive)
            return;

        _engagementService.StartSession();
        RegisterInteraction();
        StartIdleWatcher();
    }

    private bool CanQueuePrompt()
    {
        var distance = _distanceProviderMeters?.Invoke() ?? 0;
        return _engagementService.ShouldShowCompletionPrompt(distance);
    }

    private void EvaluateEligibilityAndQueuePrompt(bool force = false)
    {
        var nowUtc = DateTime.UtcNow;
        if (!force && (nowUtc - _lastEligibilityCheckUtc).TotalSeconds < 3)
            return;

        _lastEligibilityCheckUtc = nowUtc;
        if (_promptInFlight)
            return;

        if (CanQueuePrompt())
        {
            _promptQueued = true;
            _promptQueuedAtUtc ??= nowUtc;

            // ✅ Show popup IMMEDIATELY when eligibility is met (no idle wait needed)
            if (force)
            {
                _ = MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await TryShowPromptAsync(VirtualPromptTrigger.Idle);
                });
            }
        }
        else
        {
            _promptQueued = false;
            _promptQueuedAtUtc = null;
        }
    }

    private void StartIdleWatcher()
    {
        StopIdleWatcher();
        _idlePromptCts = new CancellationTokenSource();
        var token = _idlePromptCts.Token;

        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                if (token.IsCancellationRequested || !_sessionActive || !_promptQueued || _promptInFlight)
                    continue;

                SessionSeconds = _engagementService.GetTotalVirtualTime().TotalSeconds;
                var idleSeconds = (DateTime.UtcNow - _lastInteractionUtc).TotalSeconds;
                if (idleSeconds < 1)
                    continue;

                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await TryShowPromptAsync(VirtualPromptTrigger.Idle);
                });
            }
        }, token);
    }

    private void StopIdleWatcher()
    {
        _idlePromptCts?.Cancel();
        _idlePromptCts = null;
    }

    private async Task<VirtualPromptDecision> TryShowPromptAsync(VirtualPromptTrigger trigger)
    {
        if (_promptInFlight)
            return VirtualPromptDecision.NotShown;

        // Check if user suppressed this popup for today
        if (_popupService.IsSuppressedToday())
            return VirtualPromptDecision.NotShown;

        if (!CanQueuePrompt())
            return VirtualPromptDecision.NotShown;

        _promptInFlight = true;
        _promptQueued = false;
        LastPromptShownUtc = _popupService.GetLastShownTime();

        // Show popup with "Không hiện thông báo này hôm nay" checkbox
        await _popupService.ShowCompletionPopupAsync(
            onAccepted: async accepted =>
            {
                _promptInFlight = false; // Reset ONLY after dialog is dismissed

                if (!accepted)
                {
                    // Dismissed — allow future queue if user keeps engaging
                    if (trigger == VirtualPromptTrigger.Idle)
                        SessionSeconds = _engagementService.GetTotalVirtualTime().TotalSeconds;
                    return;
                }

                // User tapped "Trải nghiệm ngay" — check GPS
                var distance = _distanceProviderMeters?.Invoke() ?? 0;
                if (distance > 0 && distance <= NearDistanceMeters)
                {
                    if (_switchToRealModeAsync != null)
                        await _switchToRealModeAsync.Invoke();
                }
                else
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        _switchToExploreFar?.Invoke();
                    });
                }
            });

        return VirtualPromptDecision.StartedRealTour;
    }
}
