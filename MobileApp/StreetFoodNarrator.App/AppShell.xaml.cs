using StreetFoodNarrator.App.Views;

namespace StreetFoodNarrator.App;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        
        // Đăng ký routes cho các trang KHÔNG nằm trong TabBar
        Routing.RegisterRoute("POIDetailPage", typeof(POIDetailPage));
        Routing.RegisterRoute("SettingsPage", typeof(SettingsPage));
    }
}
