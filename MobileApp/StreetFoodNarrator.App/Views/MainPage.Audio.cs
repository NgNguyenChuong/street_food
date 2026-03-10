// ─────────────────────────────────────────────────────────────────────────────
// MainPage.Audio.cs  -  TTS playback logic (business layer only)
//
// Wave animation, UpdatePlayIcon, speed/mute, description expand are now
// self-contained in TabTourView.xaml.cs.
//
// This file owns:
//   - OnPlayPauseTapped  - fetches text from ViewModel, drives ITTSService
//                          then delegates play state & wave to TabTourComponent
//   - OnShareClicked     - Share API with current zone name
//
// These methods are wired via WireComponentEvents():
//   TabTourComponent.PlayPauseRequested  += OnPlayPauseTapped
//   TabTourComponent.ShareRequested      += OnShareClicked
// ─────────────────────────────────────────────────────────────────────────────

using Microsoft.Maui.ApplicationModel.DataTransfer;

namespace StreetFoodNarrator.App.Views;

public partial class MainPage
{
    // ── Audio state ────────────────────────────────────────────────────────────
    private bool _isPlaying;

    // ─── Play / Pause ─────────────────────────────────────────────────────────

    private async void OnPlayPauseTapped(object? sender, EventArgs e)
    {
        try
        {
            if (_isPlaying)
            {
                await _tts.StopAsync();
                _isPlaying = false;
                // TODO: Notify TourPage about play state change
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
            // TODO: Notify TourPage about play state change
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Audio] OnPlayPauseTapped: {ex}");
            _isPlaying = false;
            // TODO: Notify TourPage about play state change
        }
    }

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
}