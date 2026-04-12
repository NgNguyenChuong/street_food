namespace StreetFoodNarrator.App.Core.Models;

/// <summary>
/// Represents a downloadable voice package for offline TTS.
/// Contains metadata about a voice that can be pre-downloaded for offline use.
/// </summary>
public class VoicePackage
{
    /// <summary>
    /// Unique identifier for the voice package.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the voice (e.g., "Hoài My", "Minh Hoàng").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Language code: "vi", "en", or "zh".
    /// </summary>
    public string Language { get; set; } = string.Empty;

    /// <summary>
    /// Full language code: "vi-VN", "en-US", "zh-CN".
    /// </summary>
    public string LanguageCode { get; set; } = string.Empty;

    /// <summary>
    /// Gender: "Male" or "Female".
    /// </summary>
    public string Gender { get; set; } = string.Empty;

    /// <summary>
    /// Name of the voice engine/model (e.g., "vi-VN-HoaiMyNeural").
    /// </summary>
    public string VoiceName { get; set; } = string.Empty;

    /// <summary>
    /// Size of the voice package in bytes.
    /// </summary>
    public long SizeBytes { get; set; }

    /// <summary>
    /// Whether this voice package has been downloaded.
    /// </summary>
    public bool IsDownloaded { get; set; }

    /// <summary>
    /// Download progress (0.0 - 1.0).
    /// </summary>
    public double DownloadProgress { get; set; }

    /// <summary>
    /// Whether the voice is currently selected for use.
    /// </summary>
    public bool IsSelected { get; set; }

    /// <summary>
    /// File path where the voice package is stored (if downloaded).
    /// </summary>
    public string? LocalFilePath { get; set; }
}

/// <summary>
/// ViewModel for displaying voice packages in the UI.
/// </summary>
public class VoicePackageViewModel : System.ComponentModel.INotifyPropertyChanged
{
    private bool _isDownloaded;
    private double _downloadProgress;
    private bool _isSelected;
    private bool _isDownloading;

    public VoicePackage Package { get; }

    public VoicePackageViewModel(VoicePackage package)
    {
        Package = package;
        _isDownloaded = package.IsDownloaded;
        _isSelected = package.IsSelected;
    }

    public string Id => Package.Id;
    public string Name => Package.Name;
    public string Language => Package.LanguageCode;
    public string Gender => Package.Gender;
    public string VoiceName => Package.VoiceName;
    public long SizeBytes => Package.SizeBytes;

    public string SizeDisplay
    {
        get
        {
            if (SizeBytes < 1024 * 1024)
                return $"{SizeBytes / 1024.0:F1} KB";
            return $"{SizeBytes / (1024.0 * 1024.0):F1} MB";
        }
    }

    public bool IsDownloaded
    {
        get => _isDownloaded;
        set
        {
            if (_isDownloaded != value)
            {
                _isDownloaded = value;
                OnPropertyChanged(nameof(IsDownloaded));
                OnPropertyChanged(nameof(DownloadStatusText));
                OnPropertyChanged(nameof(DownloadIcon));
            }
        }
    }

    public double DownloadProgress
    {
        get => _downloadProgress;
        set
        {
            if (Math.Abs(_downloadProgress - value) > 0.001)
            {
                _downloadProgress = value;
                OnPropertyChanged(nameof(DownloadProgress));
                OnPropertyChanged(nameof(DownloadStatusText));
            }
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
                OnPropertyChanged(nameof(SelectedIcon));
            }
        }
    }

    public bool IsDownloading
    {
        get => _isDownloading;
        set
        {
            if (_isDownloading != value)
            {
                _isDownloading = value;
                OnPropertyChanged(nameof(IsDownloading));
                OnPropertyChanged(nameof(DownloadStatusText));
                OnPropertyChanged(nameof(DownloadIcon));
            }
        }
    }

    public string DownloadStatusText
    {
        get
        {
            if (IsDownloading)
                return $"Đang tải... {(int)(DownloadProgress * 100)}%";
            if (IsDownloaded)
                return $"Đã tải ({SizeDisplay})";
            return $"Chưa tải ({SizeDisplay})";
        }
    }

    public string DownloadIcon
    {
        get
        {
            if (IsDownloading)
                return "\uE895"; // Downloading
            if (IsDownloaded)
                return "\uE73E"; // Checkmark
            return "\uE896"; // Download
        }
    }

    public string SelectedIcon => IsSelected ? "\uE73E" : ""; // Checkmark or empty

    public string GenderIcon => Gender == "Female" ? "\uF00D3" : "\uF004E"; // Female/Male icon

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged(string name)
        => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
}
