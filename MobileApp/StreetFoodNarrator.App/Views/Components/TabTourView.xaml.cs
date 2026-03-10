// ─────────────────────────────────────────────────────────────────────────────
// TabTourView.xaml.cs  –  Tab 1: Hành trình
//
// Bottom sheet:
//   • Collapsed (30%): Drag handle + POI info + [Mini player | 3 cards]
//   • Expanded  (70%): Full audio player + Mô tả + Next stops
//
// IsNarrating (từ ViewModel):
//   true  → MiniPlayerCard hiện, InfoCardsSection ẩn
//   false → InfoCardsSection hiện, MiniPlayerCard ẩn
// ─────────────────────────────────────────────────────────────────────────────

namespace StreetFoodNarrator.App.Views.Components;

public partial class TabTourView : ContentView
{
    // ── Events ra ngoài ──────────────────────────────────────────────────────
    public event EventHandler? BackRequested;
    public event EventHandler? CenterMapRequested;
    public event EventHandler? SettingsRequested;
    public event EventHandler? ZoomInRequested;
    public event EventHandler? ZoomOutRequested;
    public event EventHandler? PlayPauseRequested;
    public event EventHandler? SeekBarDragCompleted;
    public event EventHandler? ShareRequested;

    // ── Audio state ───────────────────────────────────────────────────────────
    private int    _speedIndex      = 0;
    private bool   _isMuted         = false;
    private bool   _isDescExpanded  = false;
    private CancellationTokenSource? _waveCts;
    private CancellationTokenSource? _miniWaveCts;

    // ── Bottom Sheet state ────────────────────────────────────────────────────
    private double _collapsedHeight;
    private double _expandedHeight;
    private double _sheetMaxTranslation;
    private bool   _isExpanded = false;
    private double _panStartY  = 0;

    private readonly double[] _speeds      = { 1.0, 1.25, 1.5, 0.75 };
    private readonly string[] _speedLabels = { "1×", "1.25×", "1.5×", "0.75×" };

    // Mini waveform bars (để animate)
    private BoxView[] _miniWaves = [];

    public TabTourView()
    {
        InitializeComponent();
        SizeChanged += OnSizeChanged;
        Loaded      += OnLoaded;
    }

    // ─── Init ────────────────────────────────────────────────────────────────

    private void OnLoaded(object? sender, EventArgs e)
    {
        Loaded -= OnLoaded;
        _miniWaves = [MiniWave1, MiniWave2, MiniWave3, MiniWave4, MiniWave5,
                      MiniWave6, MiniWave7, MiniWave8, MiniWave9, MiniWave10];

        if (PlayerPanel.HeightRequest <= 0)
            InitializeBottomSheet();
    }

    private void OnSizeChanged(object? sender, EventArgs e)
    {
        if (Width <= 0 || Height <= 0) return;
        SizeChanged -= OnSizeChanged;
        InitializeBottomSheet();
    }

    private void InitializeBottomSheet()
    {
        var h = Height > 0
            ? Height
            : DeviceDisplay.MainDisplayInfo.Height / DeviceDisplay.MainDisplayInfo.Density;

        _collapsedHeight     = Math.Max(h * 0.34, 280);
        _expandedHeight      = h * 0.70;
        _sheetMaxTranslation = _expandedHeight - _collapsedHeight;

        PlayerPanel.HeightRequest  = _expandedHeight;
        PlayerPanel.TranslationY   = _sheetMaxTranslation;

        PanelToolbarControls.IsVisible = false;
        ExpandedContent.IsVisible      = false;
    }

    // ─── Public API ──────────────────────────────────────────────────────────

    /// <summary>Cập nhật icon play/pause ở CẢ 2 chỗ (mini + full)</summary>
    public void SetPlayState(bool isPlaying)
    {
        // MDI glyph: play = F040A, pause = F03E4
        var mdiIcon = isPlaying ? "\uF03E4" : "\uF040A";
        PlayPauseIcon.Text     = mdiIcon;
        MiniPlayPauseIcon.Text = mdiIcon;
    }

    /// <summary>Gọi khi bắt đầu phát audio (IsNarrating → true)</summary>
    public void StartWaveAnimation()
    {
        // Full waveform (expanded)
        _waveCts?.Cancel();
        _waveCts = new CancellationTokenSource();
        RunWaveAnimation([Wave1, Wave2, Wave3, Wave4, Wave5, Wave6, Wave7, Wave8],
                         [8, 14, 22, 18, 26, 14, 20, 10],
                         _waveCts.Token);

        // Mini waveform (collapsed)
        _miniWaveCts?.Cancel();
        _miniWaveCts = new CancellationTokenSource();
        RunWaveAnimation(_miniWaves,
                         [8, 14, 20, 16, 10, 18, 12, 16, 20, 8],
                         _miniWaveCts.Token);
    }

    /// <summary>Gọi khi dừng audio (IsNarrating → false hoặc pause)</summary>
    public void StopWaveAnimation()
    {
        _waveCts?.Cancel();
        _miniWaveCts?.Cancel();

        BoxView[] allWaves = [Wave1, Wave2, Wave3, Wave4, Wave5, Wave6, Wave7, Wave8,
                              MiniWave1, MiniWave2, MiniWave3, MiniWave4, MiniWave5,
                              MiniWave6, MiniWave7, MiniWave8, MiniWave9, MiniWave10];
        foreach (var w in allWaves) w.ScaleY = 1.0;
    }

    private static void RunWaveAnimation(BoxView[] waves, double[] baseH, CancellationToken token)
    {
        _ = Task.Run(async () =>
        {
            var rand = new Random();
            while (!token.IsCancellationRequested)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    for (int i = 0; i < waves.Length; i++)
                    {
                        double target = baseH[i] + rand.NextDouble() * 8 - 4;
                        waves[i].AnchorY = 1.0;
                        waves[i].ScaleY  = Math.Clamp(target / baseH[i], 0.15, 1.8);
                    }
                });
                await Task.Delay(400, token);
            }
        }, token);
    }

    // ─── Header ──────────────────────────────────────────────────────────────

    private void OnHeaderBackTapped(object sender, EventArgs e)
        => BackRequested?.Invoke(this, e);

    private void OnCenterMapTapped(object sender, EventArgs e)
        => CenterMapRequested?.Invoke(this, e);

    private void OnZoomInTapped(object sender, EventArgs e)
        => ZoomInRequested?.Invoke(this, e);

    private void OnZoomOutTapped(object sender, EventArgs e)
        => ZoomOutRequested?.Invoke(this, e);

    // ─── Audio controls ───────────────────────────────────────────────────────

    private void OnPlayPauseTapped(object sender, EventArgs e)
        => PlayPauseRequested?.Invoke(this, e);

    private void OnSeekBarDragCompleted(object sender, EventArgs e)
        => SeekBarDragCompleted?.Invoke(this, e);

    private void OnSpeedTapped(object sender, EventArgs e)
    {
        _speedIndex     = (_speedIndex + 1) % _speeds.Length;
        SpeedLabel.Text = _speedLabels[_speedIndex];
    }

    private void OnMuteTapped(object sender, EventArgs e)
    {
        _isMuted      = !_isMuted;
        MuteIcon.Text = _isMuted ? "\uF0580" : "\uF057E"; // MDI volume-off / volume-high
    }

    private void OnShareTapped(object sender, EventArgs e)
        => ShareRequested?.Invoke(this, e);

    // ─── Description expand/collapse ─────────────────────────────────────────

    private void OnExpandDescTapped(object sender, EventArgs e)
    {
        _isDescExpanded           = !_isDescExpanded;
        DescriptionLabel.MaxLines = _isDescExpanded ? int.MaxValue : 2;
        DescriptionLabel.LineBreakMode = _isDescExpanded
            ? LineBreakMode.WordWrap
            : LineBreakMode.TailTruncation;
        ExpandDescLabel.Text = _isDescExpanded ? "Thu gọn ›" : "Xem thêm ›";
    }

    // ─── Bottom sheet pan gesture ─────────────────────────────────────────────

    private void OnBottomSheetPan(object sender, PanUpdatedEventArgs e)
    {
        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _panStartY = PlayerPanel.TranslationY;
                break;

            case GestureStatus.Running:
                var newY = Math.Clamp(_panStartY + e.TotalY, 0, _sheetMaxTranslation);
                PlayerPanel.TranslationY = newY;

                // Hiện ExpandedContent sớm khi user bắt đầu kéo lên
                if (!ExpandedContent.IsVisible && newY < _sheetMaxTranslation * 0.8)
                    ExpandedContent.IsVisible = true;
                break;

            case GestureStatus.Completed:
                var current = PlayerPanel.TranslationY;
                if (!_isExpanded && e.TotalY < -40)
                    ExpandPanel();
                else if (_isExpanded && e.TotalY > 40)
                    CollapsePanel();
                else
                    _ = current < _sheetMaxTranslation / 2 ? ExpandPanel() : CollapsePanel();
                break;
        }
    }

    private async Task ExpandPanel()
    {
        _isExpanded = true;
        ExpandedContent.IsVisible      = true;
        PanelToolbarControls.IsVisible = true;

        ExpandedContent.Opacity = 0;
        await Task.WhenAll(
            PlayerPanel.TranslateTo(0, 0, 260, Easing.CubicOut),
            ExpandedContent.FadeTo(1, 220, Easing.CubicOut)
        );
    }

    private async Task CollapsePanel()
    {
        _isExpanded = false;

        await Task.WhenAll(
            PlayerPanel.TranslateTo(0, _sheetMaxTranslation, 260, Easing.CubicOut),
            ExpandedContent.FadeTo(0, 180, Easing.CubicIn)
        );

        ExpandedContent.IsVisible      = false;
        PanelToolbarControls.IsVisible = false;

        // Reset scroll lên đầu
        // (ScrollView không có tên — nếu cần đặt x:Name="ExpandedScroll" rồi gọi ScrollToAsync)
    }
}
