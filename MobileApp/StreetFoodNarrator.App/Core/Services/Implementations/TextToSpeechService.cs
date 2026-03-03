using System.Net.Http.Json;
using StreetFoodNarrator.App.Core.Models;
using Plugin.Maui.Audio;

namespace StreetFoodNarrator.App.Core.Services.Implementations;

/// <summary>
/// Text-to-Speech service sử dụng Edge-TTS từ backend API
/// </summary>
public class TextToSpeechService : ITTSService
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly IAudioManager _audioManager;
    private IAudioPlayer? _currentPlayer;

    public TextToSpeechService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _baseUrl = "http://10.0.2.2:5004"; // Android emulator localhost → host:5004
        _audioManager = AudioManager.Current;
    }

    public bool IsAvailable => true;

    /// <summary>
    /// Phát text với ngôn ngữ và voice được chỉ định
    /// </summary>
    public async Task<bool> SpeakAsync(string text, string languageCode, string? voiceName = null, CancellationToken cancellationToken = default)
    {
        try
        {
            // Stop any current playback
            await StopAsync();
            
            // Build request
            var request = new
            {
                text = text,
                language = languageCode,
                voice = voiceName
            };

            // Call API to generate audio
            var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/api/tts/generate", request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errBody = await response.Content.ReadAsStringAsync(cancellationToken);
                System.Diagnostics.Debug.WriteLine($"[TTS] API error: {response.StatusCode} - {errBody}");
                return false;
            }

            var result = await response.Content.ReadFromJsonAsync<TtsGenerateResponse>(cancellationToken: cancellationToken);
            if (result == null || string.IsNullOrEmpty(result.FilePath))
            {
                return false;
            }

            // Play audio file
            var audioUrl = $"{_baseUrl}{result.FilePath}";
            System.Diagnostics.Debug.WriteLine($"[TTS] Playing audio: {audioUrl}");
            
            // Download and play audio using Plugin.Maui.Audio
            var audioBytes = await _httpClient.GetByteArrayAsync(audioUrl, cancellationToken);
            var audioStream = new MemoryStream(audioBytes);
            _currentPlayer = _audioManager.CreatePlayer(audioStream);
            _currentPlayer.Play();
            
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TTS] Error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Lấy danh sách voices cho ngôn ngữ
    /// </summary>
    public async Task<List<VoiceInfo>> GetVoicesAsync(string? languageCode = null)
    {
        try
        {
            var url = string.IsNullOrWhiteSpace(languageCode)
                ? $"{_baseUrl}/api/tts/voices"
                : $"{_baseUrl}/api/tts/voices?language={MapToApiLanguageCode(languageCode)}";
            var response = await _httpClient.GetFromJsonAsync<List<VoiceInfo>>(url);
            return response ?? new List<VoiceInfo>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TTS] Get voices error: {ex.Message}");
            return new List<VoiceInfo>();
        }
    }

    /// <summary>
    /// Dừng phát
    /// </summary>
    public async Task StopAsync()
    {
        try
        {
            if (_currentPlayer != null)
            {
                _currentPlayer.Stop();
                _currentPlayer.Dispose();
                _currentPlayer = null;
            }
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TTS] Stop error: {ex.Message}");
        }
    }

    private string MapToApiLanguageCode(string languageCode)
    {
        var normalized = languageCode.Trim().ToLowerInvariant();
        if (normalized.StartsWith("vi")) return "vi-VN";
        if (normalized.StartsWith("en")) return "en-US";
        if (normalized.StartsWith("zh")) return "zh-CN";
        return "vi-VN";
    }

    private class TtsGenerateResponse
    {
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string Language { get; set; } = string.Empty;
        public string Voice { get; set; } = string.Empty;
    }
}
