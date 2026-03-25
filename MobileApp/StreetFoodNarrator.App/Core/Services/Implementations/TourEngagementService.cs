using Microsoft.Maui.Storage;
using StreetFoodNarrator.App.Core.Services;

namespace StreetFoodNarrator.App.Core.Services.Implementations;

public class TourEngagementService : ITourEngagementService
{
    private readonly HashSet<int> _viewedPoiIds = new();
    private DateTime? _sessionStartUtc;
    private TimeSpan _persistedVirtualDuration = TimeSpan.Zero;

    public TourEngagementService()
    {
        RestoreFromPreferences();
    }

    public void TrackPOIViewed(int? poiId = null)
    {
        if (poiId.HasValue && poiId.Value > 0)
        {
            _viewedPoiIds.Add(poiId.Value);
            SaveViewedPoiIds();
            Preferences.Set(VirtualTourPromptPreferenceKeys.ViewedPoiCount, _viewedPoiIds.Count);
            return;
        }

        var count = Preferences.Get(VirtualTourPromptPreferenceKeys.ViewedPoiCount, 0);
        Preferences.Set(VirtualTourPromptPreferenceKeys.ViewedPoiCount, count + 1);
    }

    public void StartSession()
    {
        if (_sessionStartUtc.HasValue)
            return;

        _sessionStartUtc = DateTime.UtcNow;
        Preferences.Set(VirtualTourPromptPreferenceKeys.SessionStartUtc, _sessionStartUtc.Value.ToString("O"));
    }

    public void EndSession()
    {
        if (!_sessionStartUtc.HasValue)
            return;

        var nowUtc = DateTime.UtcNow;
        var sessionElapsed = nowUtc - _sessionStartUtc.Value;
        if (sessionElapsed > TimeSpan.Zero)
        {
            _persistedVirtualDuration += sessionElapsed;
            Preferences.Set(
                VirtualTourPromptPreferenceKeys.TotalVirtualSeconds,
                (int)Math.Max(0, _persistedVirtualDuration.TotalSeconds));
        }

        _sessionStartUtc = null;
        Preferences.Remove(VirtualTourPromptPreferenceKeys.SessionStartUtc);
    }

    public bool ShouldShowCompletionPrompt(double distanceMeters)
    {
        // If distance is 0 or very small, treat as "unknown location" (emulator / no GPS)
        // — still eligible as long as other conditions pass.
        // If distance > 0, require user to be far enough from the food area.
        if (distanceMeters > VirtualTourPromptPolicy.MinDistanceMeters)
            return false;

        var viewedEnough = GetViewedPoiCount() >= VirtualTourPromptPolicy.MinViewedPois;
        var spentEnoughTime = GetTotalVirtualTime().TotalSeconds >= VirtualTourPromptPolicy.MinVirtualSeconds;
        if (!viewedEnough && !spentEnoughTime)
            return false;

        var lastPrompt = Preferences.Get(VirtualTourPromptPreferenceKeys.LastPromptShownUtc, string.Empty);
        if (DateTime.TryParse(lastPrompt, out var lastPromptUtc))
        {
            if ((DateTime.UtcNow - lastPromptUtc.ToUniversalTime()) < VirtualTourPromptPolicy.Cooldown)
                return false;
        }

        return true;
    }

    public int GetViewedPoiCount()
    {
        if (_viewedPoiIds.Count > 0)
            return _viewedPoiIds.Count;

        return Preferences.Get(VirtualTourPromptPreferenceKeys.ViewedPoiCount, 0);
    }

    public TimeSpan GetTotalVirtualTime()
    {
        var total = _persistedVirtualDuration;
        if (_sessionStartUtc.HasValue)
        {
            total += DateTime.UtcNow - _sessionStartUtc.Value;
        }

        return total;
    }

    private void RestoreFromPreferences()
    {
        var persistedSeconds = Preferences.Get(VirtualTourPromptPreferenceKeys.TotalVirtualSeconds, 0);
        _persistedVirtualDuration = TimeSpan.FromSeconds(Math.Max(0, persistedSeconds));

        var viewedCsv = Preferences.Get(VirtualTourPromptPreferenceKeys.ViewedPoiIds, string.Empty);
        if (!string.IsNullOrWhiteSpace(viewedCsv))
        {
            foreach (var raw in viewedCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (int.TryParse(raw, out var poiId) && poiId > 0)
                    _viewedPoiIds.Add(poiId);
            }
        }

        var startUtcRaw = Preferences.Get(VirtualTourPromptPreferenceKeys.SessionStartUtc, string.Empty);
        if (DateTime.TryParse(startUtcRaw, out var restoredStartUtc))
        {
            _sessionStartUtc = restoredStartUtc.ToUniversalTime();
        }

        if (_viewedPoiIds.Count > 0)
        {
            Preferences.Set(VirtualTourPromptPreferenceKeys.ViewedPoiCount, _viewedPoiIds.Count);
        }
    }

    private void SaveViewedPoiIds()
    {
        if (_viewedPoiIds.Count == 0)
        {
            Preferences.Remove(VirtualTourPromptPreferenceKeys.ViewedPoiIds);
            return;
        }

        var csv = string.Join(",", _viewedPoiIds.OrderBy(x => x));
        Preferences.Set(VirtualTourPromptPreferenceKeys.ViewedPoiIds, csv);
    }
}
