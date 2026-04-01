namespace StreetFoodNarrator.App.Helpers;

public static class OfflineBannerSessionState
{
    // In-memory only: resets when app process restarts.
    public static bool IsDismissed { get; set; }
}

