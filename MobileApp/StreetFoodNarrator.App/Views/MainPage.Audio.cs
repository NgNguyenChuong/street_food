// ─────────────────────────────────────────────────────────────────────────────
// MainPage.Audio.cs  -  Audio utilities for the Map page
//
// Map page audio is minimal: pin popup plays via OnPinPlayAudio (PinPopup.cs).
// This file keeps shared helpers only.
// ─────────────────────────────────────────────────────────────────────────────

using Microsoft.Maui.ApplicationModel.DataTransfer;

namespace StreetFoodNarrator.App.Views;

public partial class MainPage
{
    // ─── Share ────────────────────────────────────────────────────────────────

    private async void OnShareClicked(object? sender, EventArgs e)
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
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Audio] OnShareClicked: {ex}"); }
    }

    // ─── Utility ──────────────────────────────────────────────────────────────

    internal static string FormatDuration(double seconds)
    {
        var t = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return t.TotalMinutes >= 60
            ? $"{(int)t.TotalHours}:{t.Minutes:D2}:{t.Seconds:D2}"
            : $"{(int)t.TotalMinutes}:{t.Seconds:D2}";
    }
}
