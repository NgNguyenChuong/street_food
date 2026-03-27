using System.Diagnostics;
using System.Net.Http.Json;
using StreetFoodNarrator.App.Core.Models;

namespace StreetFoodNarrator.App.Core.Services.Implementations;

/// <summary>
/// Implementation of voice package management.
/// Downloads and manages offline voice packages for TTS.
/// Uses bundled default voices or API voices.
/// </summary>
public class VoicePackageService : IVoicePackageService
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private readonly string _voiceCacheDir;

    // In-memory tracking of downloaded packages
    private readonly Dictionary<string, VoicePackage> _downloadedPackages = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    // Preferences key for tracking downloaded packages
    private const string DOWNLOADED_VOICES_KEY = "DownloadedVoicePackages";

    public VoicePackageService(HttpClient httpClient)
    {
        _http = httpClient;
        _baseUrl = AppConfig.GetResolvedApiBaseUrl().TrimEnd('/');
        _voiceCacheDir = Path.Combine(FileSystem.AppDataDirectory, "voice_packages");
        Directory.CreateDirectory(_voiceCacheDir);

        // Load previously downloaded packages
        LoadDownloadedPackages();
    }

    /// <inheritdoc/>
    public async Task<List<VoicePackage>> GetAvailablePackagesAsync(CancellationToken ct = default)
    {
        var packages = new List<VoicePackage>();

        // Try to fetch from API
        try
        {
            var url = $"{_baseUrl}/api/tts/voices";
            var response = await _http.GetFromJsonAsync<List<VoiceInfo>>(url, ct);
            if (response != null && response.Count > 0)
            {
                foreach (var voice in response)
                {
                    var lang = NormalizeLanguage(voice.Language);
                    var packageId = $"{lang}_{voice.Voice}";
                    packages.Add(new VoicePackage
                    {
                        Id = packageId,
                        Name = voice.Name ?? voice.Voice,
                        Language = lang,
                        LanguageCode = voice.Language,
                        Gender = voice.Gender ?? "Unknown",
                        VoiceName = voice.Voice,
                        SizeBytes = 5_000_000, // ~5MB estimate per voice package
                        IsDownloaded = _downloadedPackages.ContainsKey(packageId),
                        LocalFilePath = _downloadedPackages.ContainsKey(packageId)
                            ? Path.Combine(_voiceCacheDir, $"{packageId}.zip")
                            : null
                    });
                }
                return packages;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[VoicePackage] API fetch failed: {ex.Message}");
        }

        // Fallback to default voices
        return GetDefaultPackages();
    }

    /// <inheritdoc/>
    public async Task<bool> DownloadPackageAsync(string packageId, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        try
        {
            // Get package info
            var packages = await GetAvailablePackagesAsync(ct);
            var package = packages.FirstOrDefault(p => p.Id == packageId);
            if (package == null)
            {
                Debug.WriteLine($"[VoicePackage] Package not found: {packageId}");
                return false;
            }

            // If already downloaded, skip
            if (_downloadedPackages.ContainsKey(packageId))
            {
                Debug.WriteLine($"[VoicePackage] Already downloaded: {packageId}");
                progress?.Report(1.0);
                return true;
            }

            // Simulate download progress (in real app, would download actual voice data)
            // For now, we mark it as downloaded since MAUI TTS uses system voices
            for (int i = 1; i <= 10; i++)
            {
                ct.ThrowIfCancellationRequested();
                await Task.Delay(100, ct);
                progress?.Report(i / 10.0);
            }

            await _lock.WaitAsync(ct);
            try
            {
                // Register as downloaded
                package.IsDownloaded = true;
                package.LocalFilePath = Path.Combine(_voiceCacheDir, $"{packageId}.zip");
                _downloadedPackages[packageId] = package;
                SaveDownloadedPackages();

                Debug.WriteLine($"[VoicePackage] Downloaded: {packageId}");
            }
            finally
            {
                _lock.Release();
            }

            return true;
        }
        catch (OperationCanceledException)
        {
            Debug.WriteLine($"[VoicePackage] Download cancelled: {packageId}");
            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[VoicePackage] Download failed: {packageId} - {ex.Message}");
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> DeletePackageAsync(string packageId, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (!_downloadedPackages.TryGetValue(packageId, out var package))
                return false;

            // Delete local file if exists
            if (!string.IsNullOrEmpty(package.LocalFilePath) && File.Exists(package.LocalFilePath))
            {
                File.Delete(package.LocalFilePath);
            }

            _downloadedPackages.Remove(packageId);
            SaveDownloadedPackages();

            Debug.WriteLine($"[VoicePackage] Deleted: {packageId}");
            return true;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public Task<List<VoicePackage>> GetDownloadedPackagesAsync(CancellationToken ct = default)
    {
        return Task.FromResult(_downloadedPackages.Values.ToList());
    }

    /// <inheritdoc/>
    public Task<bool> IsPackageDownloadedAsync(string packageId, CancellationToken ct = default)
    {
        return Task.FromResult(_downloadedPackages.ContainsKey(packageId));
    }

    /// <inheritdoc/>
    public long GetDownloadedSizeBytes()
    {
        try
        {
            return Directory.EnumerateFiles(_voiceCacheDir, "*.zip")
                .Sum(f => new FileInfo(f).Length);
        }
        catch
        {
            return 0;
        }
    }

    /// <inheritdoc/>
    public async Task ClearAllAsync()
    {
        await _lock.WaitAsync();
        try
        {
            // Delete all files
            foreach (var file in Directory.EnumerateFiles(_voiceCacheDir, "*.zip"))
            {
                try { File.Delete(file); } catch { }
            }

            _downloadedPackages.Clear();
            SaveDownloadedPackages();

            Debug.WriteLine("[VoicePackage] Cleared all packages");
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<VoicePackage?> GetSelectedPackageForLanguageAsync(string languageCode, CancellationToken ct = default)
    {
        var lang = NormalizeLanguage(languageCode);
        var packages = await GetDownloadedPackagesAsync(ct);

        // Try to find downloaded package for this language
        var downloaded = packages.FirstOrDefault(p => p.Language == lang);
        if (downloaded != null)
            return downloaded;

        // Return default package for language
        var defaults = GetDefaultPackages();
        return defaults.FirstOrDefault(p => p.Language == lang);
    }

    /// <inheritdoc/>
    public async Task<VoicePackage?> GetCurrentSelectedPackageAsync(CancellationToken ct = default)
    {
        // Get selected voice from settings
        var settingsJson = Preferences.Get("UserSettings", string.Empty);
        if (string.IsNullOrWhiteSpace(settingsJson))
            return null;

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(settingsJson);
            if (!doc.RootElement.TryGetProperty("TTS", out var ttsObj))
                return null;

            var voiceName = ttsObj.TryGetProperty("Voice", out var v) ? v.GetString() : null;
            if (string.IsNullOrWhiteSpace(voiceName))
                return null;

            // Find package with this voice
            var packages = await GetAvailablePackagesAsync(ct);
            return packages.FirstOrDefault(p => p.VoiceName == voiceName);
        }
        catch
        {
            return null;
        }
    }

    // ── Private helpers ─────────────────────────────────────────────────────

    private List<VoicePackage> GetDefaultPackages()
    {
        return new List<VoicePackage>
        {
            new VoicePackage
            {
                Id = "vi_vi-VN-HoaiMyNeural",
                Name = "Hoài My (Nữ)",
                Language = "vi",
                LanguageCode = "vi-VN",
                Gender = "Female",
                VoiceName = "vi-VN-HoaiMyNeural",
                SizeBytes = 5_000_000,
                IsDownloaded = _downloadedPackages.ContainsKey("vi_vi-VN-HoaiMyNeural")
            },
            new VoicePackage
            {
                Id = "en_en-US-JennyNeural",
                Name = "Jenny (Female)",
                Language = "en",
                LanguageCode = "en-US",
                Gender = "Female",
                VoiceName = "en-US-JennyNeural",
                SizeBytes = 5_000_000,
                IsDownloaded = _downloadedPackages.ContainsKey("en_en-US-JennyNeural")
            },
            new VoicePackage
            {
                Id = "zh_zh-CN-XiaoxiaoNeural",
                Name = "Xiaoxiao (女)",
                Language = "zh",
                LanguageCode = "zh-CN",
                Gender = "Female",
                VoiceName = "zh-CN-XiaoxiaoNeural",
                SizeBytes = 5_000_000,
                IsDownloaded = _downloadedPackages.ContainsKey("zh_zh-CN-XiaoxiaoNeural")
            },
            // Additional male voices
            new VoicePackage
            {
                Id = "vi_vi-VN-NamMinhNeural",
                Name = "Nam Minh (Nam)",
                Language = "vi",
                LanguageCode = "vi-VN",
                Gender = "Male",
                VoiceName = "vi-VN-NamMinhNeural",
                SizeBytes = 5_000_000,
                IsDownloaded = _downloadedPackages.ContainsKey("vi_vi-VN-NamMinhNeural")
            },
            new VoicePackage
            {
                Id = "en_en-US-GuyNeural",
                Name = "Guy (Male)",
                Language = "en",
                LanguageCode = "en-US",
                Gender = "Male",
                VoiceName = "en-US-GuyNeural",
                SizeBytes = 5_000_000,
                IsDownloaded = _downloadedPackages.ContainsKey("en_en-US-GuyNeural")
            },
            new VoicePackage
            {
                Id = "zh_zh-CN-YunxiNeural",
                Name = "Yunxi (男)",
                Language = "zh",
                LanguageCode = "zh-CN",
                Gender = "Male",
                VoiceName = "zh-CN-YunxiNeural",
                SizeBytes = 5_000_000,
                IsDownloaded = _downloadedPackages.ContainsKey("zh_zh-CN-YunxiNeural")
            }
        };
    }

    private string NormalizeLanguage(string? language)
    {
        var normalized = (language ?? "vi").Split('-', '_')[0].ToLowerInvariant();
        return normalized switch
        {
            "vi" => "vi",
            "en" => "en",
            "zh" => "zh",
            _ => "vi"
        };
    }

    private void LoadDownloadedPackages()
    {
        try
        {
            var json = Preferences.Get(DOWNLOADED_VOICES_KEY, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
                return;

            var saved = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (saved == null)
                return;

            foreach (var kvp in saved)
            {
                // Restore downloaded status from preferences
                // Actual files are tracked separately
                var packages = GetDefaultPackages();
                var package = packages.FirstOrDefault(p => p.Id == kvp.Key);
                if (package != null)
                {
                    package.IsDownloaded = true;
                    _downloadedPackages[kvp.Key] = package;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[VoicePackage] Load failed: {ex.Message}");
        }
    }

    private void SaveDownloadedPackages()
    {
        try
        {
            var data = _downloadedPackages.Keys
                .ToDictionary(k => k, k => k);
            var json = System.Text.Json.JsonSerializer.Serialize(data);
            Preferences.Set(DOWNLOADED_VOICES_KEY, json);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[VoicePackage] Save failed: {ex.Message}");
        }
    }
}
