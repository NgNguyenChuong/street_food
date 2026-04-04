using System.Text.Json;
using StreetFoodNarrator.App.Resources.Strings;

namespace StreetFoodNarrator.App.Core.Services.Implementations;

public class RemoteLocalizationService : IRemoteLocalizationService
{
    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _syncLock = new(1, 1);

    public RemoteLocalizationService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task RefreshAsync(string languageCode, CancellationToken cancellationToken = default)
    {
        var lang = NormalizeLanguage(languageCode);

        await _syncLock.WaitAsync(cancellationToken);
        try
        {
            var loadedRemote = await TryLoadFromServerAsync(lang, cancellationToken);
            if (loadedRemote)
                return;

            var loadedCache = await TryLoadFromCacheAsync(lang, cancellationToken);
            if (!loadedCache)
                AppStrings.ClearRuntimeOverrides();
        }
        finally
        {
            _syncLock.Release();
        }
    }

    private async Task<bool> TryLoadFromServerAsync(string lang, CancellationToken cancellationToken)
    {
        try
        {
            var network = Connectivity.Current.NetworkAccess;
            if (network != NetworkAccess.Internet && network != NetworkAccess.ConstrainedInternet)
                return false;

            var baseUrl = AppConfig.GetResolvedApiBaseUrl();
            var uri = new Uri(new Uri(baseUrl), $"api/translations/export?language={Uri.EscapeDataString(lang)}");

            using var response = await _httpClient.GetAsync(uri, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return false;

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var dictionary = await JsonSerializer.DeserializeAsync<Dictionary<string, string>>(stream, cancellationToken: cancellationToken);
            if (dictionary == null || dictionary.Count == 0)
                return false;

            AppStrings.SetRuntimeOverrides(dictionary);
            await SaveCacheAsync(lang, dictionary, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RemoteLocalization] Server fetch failed: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> TryLoadFromCacheAsync(string lang, CancellationToken cancellationToken)
    {
        try
        {
            var cachePath = GetCacheFilePath(lang);
            if (!File.Exists(cachePath))
                return false;

            var json = await File.ReadAllTextAsync(cachePath, cancellationToken);
            var dictionary = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (dictionary == null || dictionary.Count == 0)
                return false;

            AppStrings.SetRuntimeOverrides(dictionary);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RemoteLocalization] Cache load failed: {ex.Message}");
            return false;
        }
    }

    private static async Task SaveCacheAsync(string lang, Dictionary<string, string> dictionary, CancellationToken cancellationToken)
    {
        try
        {
            var cachePath = GetCacheFilePath(lang);
            var json = JsonSerializer.Serialize(dictionary);
            await File.WriteAllTextAsync(cachePath, json, cancellationToken);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RemoteLocalization] Cache save failed: {ex.Message}");
        }
    }

    private static string GetCacheFilePath(string lang)
        => Path.Combine(FileSystem.AppDataDirectory, $"ui_translations_{lang}.json");

    private static string NormalizeLanguage(string languageCode)
        => languageCode?.ToLowerInvariant() switch
        {
            "en" or "en-us" => "en",
            "zh" or "zh-cn" => "zh",
            _ => "vi"
        };
}
