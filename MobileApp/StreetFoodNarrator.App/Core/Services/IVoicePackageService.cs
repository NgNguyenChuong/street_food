using StreetFoodNarrator.App.Core.Models;

namespace StreetFoodNarrator.App.Core.Services;

/// <summary>
/// Service for managing downloadable voice packages for offline TTS.
/// Supports 3 languages: Vietnamese (vi), English (en), and Chinese (zh).
/// </summary>
public interface IVoicePackageService
{
    /// <summary>
    /// Gets the list of available voice packages (metadata from API or defaults).
    /// </summary>
    Task<List<VoicePackage>> GetAvailablePackagesAsync(CancellationToken ct = default);

    /// <summary>
    /// Downloads and installs a voice package by ID.
    /// Reports progress via IProgress.
    /// </summary>
    Task<bool> DownloadPackageAsync(string packageId, IProgress<double>? progress = null, CancellationToken ct = default);

    /// <summary>
    /// Deletes a downloaded voice package to free up storage.
    /// </summary>
    Task<bool> DeletePackageAsync(string packageId, CancellationToken ct = default);

    /// <summary>
    /// Gets all downloaded (installed) voice packages.
    /// </summary>
    Task<List<VoicePackage>> GetDownloadedPackagesAsync(CancellationToken ct = default);

    /// <summary>
    /// Checks if a specific package is downloaded.
    /// </summary>
    Task<bool> IsPackageDownloadedAsync(string packageId, CancellationToken ct = default);

    /// <summary>
    /// Gets the total size of downloaded voice packages in bytes.
    /// </summary>
    long GetDownloadedSizeBytes();

    /// <summary>
    /// Deletes all downloaded voice packages.
    /// </summary>
    Task ClearAllAsync();

    /// <summary>
    /// Gets the voice package for a specific language that is selected by user.
    /// </summary>
    Task<VoicePackage?> GetSelectedPackageForLanguageAsync(string languageCode, CancellationToken ct = default);

    /// <summary>
    /// Gets the currently selected voice package for the app.
    /// </summary>
    Task<VoicePackage?> GetCurrentSelectedPackageAsync(CancellationToken ct = default);
}
