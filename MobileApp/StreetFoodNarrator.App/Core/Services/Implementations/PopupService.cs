using StreetFoodNarrator.App.Core.Services;
using StreetFoodNarrator.App.Helpers;
using Microsoft.Maui.Storage;

namespace StreetFoodNarrator.App.Core.Services.Implementations;

public class PopupService : IPopupService
{
    public async Task<bool> ShowCompletionPopup()
    {
        return await CustomAlert.ShowConfirmAsync(
            "🎉 Bạn đã khám phá xong!",
            "Giờ hãy đến Vĩnh Khánh để trải nghiệm thật.",
            "Trải nghiệm ngay",
            "Để sau",
            AlertType.Success);
    }

    /// <summary>
    /// Shows the completion popup with a "Không hiện thông báo này hôm nay" checkbox.
    /// Calls onAccepted(true/false) after the dialog is dismissed.
    /// </summary>
    public Task ShowCompletionPopupAsync(Func<bool, Task> onAccepted)
    {
        return Task.Run(async () =>
        {
            bool accepted = false;
            bool dontShowAgainToday = false;

            var tcs = new TaskCompletionSource();

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                CustomAlert.ShowConfirmAsync(
                    "🎉 Bạn đã khám phá xong!",
                    "Giờ hãy đến Vĩnh Khánh để trải nghiệm thật.",
                    "Trải nghiệm ngay",
                    "Để sau",
                    AlertType.Success,
                    "Không hiện thông báo này hôm nay",
                    (a, d) =>
                    {
                        accepted = a;
                        dontShowAgainToday = d;
                        if (dontShowAgainToday)
                            SetSuppressUntilTomorrow();
                        SetLastShownTime();
                        tcs.TrySetResult();
                    });
            });

            await tcs.Task;
            await onAccepted(accepted);
        });
    }

    public void SetLastShownTime(DateTime? utcNow = null)
    {
        var value = (utcNow ?? DateTime.UtcNow).ToString("O");
        Preferences.Set(VirtualTourPromptPreferenceKeys.LastPromptShownUtc, value);
    }

    public DateTime? GetLastShownTime()
    {
        var raw = Preferences.Get(VirtualTourPromptPreferenceKeys.LastPromptShownUtc, string.Empty);
        if (DateTime.TryParse(raw, out var dt))
            return dt.ToUniversalTime();

        return null;
    }

    /// <summary>
    /// Sets a flag so the popup is suppressed for the rest of today.
    /// Clears itself automatically when date changes.
    /// </summary>
    public void SetSuppressUntilTomorrow()
    {
        var tomorrow = DateTime.Today.AddDays(1).ToString("yyyy-MM-dd");
        Preferences.Set(VirtualTourPromptPreferenceKeys.SuppressUntilDate, tomorrow);
    }

    /// <summary>
    /// Returns true if suppression is still active (today hasn't passed).
    /// </summary>
    public bool IsSuppressedToday()
    {
        var suppressUntil = Preferences.Get(VirtualTourPromptPreferenceKeys.SuppressUntilDate, string.Empty);
        if (string.IsNullOrEmpty(suppressUntil))
            return false;

        if (DateTime.TryParse(suppressUntil, out var suppressDate))
        {
            if (DateTime.Today >= suppressDate)
            {
                // Suppression period expired — clear it
                Preferences.Remove(VirtualTourPromptPreferenceKeys.SuppressUntilDate);
                return false;
            }
            return true;
        }
        return false;
    }
}
