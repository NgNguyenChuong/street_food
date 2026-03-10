// ─────────────────────────────────────────────────────────────────────────────
// TabMapView.xaml.cs
// Tab 0: Map overlays (top bar, legend, zoom controls)
// Sự kiện ra ngoài → MainPage.Map.cs lắng nghe
// ─────────────────────────────────────────────────────────────────────────────

namespace StreetFoodNarrator.App.Views.Components;

public partial class TabMapView : ContentView
{
    // ── Sự kiện ra ngoài ─────────────────────────────────────────────────────
    public event EventHandler? BackRequested;
    public event EventHandler? CenterMapRequested;
    public event EventHandler? SettingsRequested;
    public event EventHandler? ZoomInRequested;
    public event EventHandler? ZoomOutRequested;
    public event EventHandler? ResetDatabaseRequested; // 🔄 DEBUG event

    public TabMapView()
    {
        InitializeComponent();
    }

    // ── Event forwarders ─────────────────────────────────────────────────────

    private void OnBackTapped(object sender, EventArgs e)      => BackRequested?.Invoke(this, e);
    private void OnCenterMapTapped(object sender, EventArgs e) => CenterMapRequested?.Invoke(this, e);
    private void OnZoomInTapped(object sender, EventArgs e)    => ZoomInRequested?.Invoke(this, e);
    private void OnZoomOutTapped(object sender, EventArgs e)   => ZoomOutRequested?.Invoke(this, e);
    private void OnSettingsTapped(object sender, EventArgs e)  => SettingsRequested?.Invoke(this, e);
    private void OnResetDatabaseTapped(object sender, EventArgs e) => ResetDatabaseRequested?.Invoke(this, e);
}
