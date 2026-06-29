namespace StreetFoodNarrator.App.Views;

public partial class LegalPage : ContentPage
{
    public enum LegalType { Terms, Privacy }

    private static readonly (string Icon, string Title, string Subtitle, (string Heading, string Body)[] Sections)[] LegalContent =
    [
        // Terms
        (
            "📜",
            "Điều khoản sử dụng",
            "Cập nhật: 01/01/2026 — Có hiệu lực toàn quốc.",
            [
                ("1. Chấp thuận điều khoản",
                    "Bằng việc sử dụng ứng dụng Street Food Narrator , bạn đồng ý rằng mình đã đọc, hiểu và chấp thuận các điều khoản dưới đây."),
                ("2. Mô tả dịch vụ",
                    "SFNarrator là nền tảng hướng dẫn du lịch ẩm thực tại phố Vĩnh Khánh, TP.HCM. Ứng dụng cung cấp thông tin quán ăn, bản đồ, hướng dẫn audio đa ngôn ngữ và lộ trình tour."),
                ("3. Gói VIP & thanh toán",
                    "Gói VIP có giá 36.000 VNĐ/tháng, thanh toán qua chuyển khoản ngân hàng. Sau khi thanh toán được xác nhận, tài khoản VIP được kích hoạt trong vòng 24 giờ. Mã khôi phục do hệ thống cấp cần được lưu lại để sử dụng trên thiết bị mới."),
                ("4. Chính sách hoàn tiền",
                    "Không hoàn tiền sau khi gói VIP đã được kích hoạt. Trong trường hợp lỗi hệ thống nghiêm trọng không thể cung cấp dịch vụ, ban quản trị sẽ xét hoàn tiền theo từng trường hợp cụ thể."),
                ("5. Quyền sở hữu nội dung",
                    "Toàn bộ nội dung bao gồm ảnh, âm thanh, bản mô tả và lộ trình tour là tài sản của SFNarrator. Không sao chép, phân phối hoặc sử dụng thương mại khi chưa có sự cho phép bằng văn bản."),
                ("6. Giới hạn trách nhiệm",
                    "SFNarrator cung cấp thông tin tham khảo. Thông tin về giờ mở cửa, giá cả có thể thay đổi mà không báo trước. Không chịu trách nhiệm về sai lệch giữa thông tin hiển thị và thực tế."),
                ("7. Thay đổi điều khoản",
                    "Chúng tôi có quyền cập nhật điều khoản bất cứ lúc nào. Việc tiếp tục sử dụng dịch vụ sau khi thay đổi được xem là đồng ý với điều khoản mới."),
                ("Liên hệ", "streetfoodnarrator@gmail.com")
            ]
        ),
        // Privacy
        (
            "🔒",
            "Chính sách bảo mật",
            "Cập nhật: 01/01/2026 — Áp dụng cho ứng dụng Street Food Narrator.",
            [
                ("1. Thông tin thu thập",
                    "Chúng tôi thu thập: (a) ID thiết bị được tạo ngẫu nhiên từ phía máy khách; (b) thông tin đơn hàng và mã hóa đơn khi mua VIP; (c) dữ liệu sử dụng ẩn danh (chế độ, ngôn ngữ, quán xem).\n\nChúng tôi KHÔNG thu thập tên, email, số điện thoại hay số CMND."),
                ("2. Mục đích sử dụng dữ liệu",
                    "Dữ liệu được dùng để: xác nhận và duy trì gói VIP; cải thiện chất lượng dịch vụ; thống kê ẩn danh.\n\nChúng tôi KHÔNG bán hoặc chia sẻ dữ liệu cá nhân cho bên thứ ba."),
                ("3. Lưu trữ dữ liệu",
                    "Dữ liệu giao dịch được lưu trong 12 tháng. Dữ liệu sử dụng ẩn danh có thể giữ lâu hơn cho mục đích thống kê."),
                ("4. Bảo mật",
                    "Thông tin được truyền qua HTTPS mã hóa. Chúng tôi áp dụng các biện pháp bảo mật phù hợp để chống truy cập trái phép."),
                ("5. Cookie & lưu trữ cục bộ",
                    "Ứng dụng sử dụng bộ nhớ cục bộ để lưu ngôn ngữ, ID thiết bị và danh sách quán đã lưu. Dữ liệu này nằm toàn bộ trên thiết bị của bạn và không được đồng bộ lên máy chủ."),
                ("6. Quyền của người dùng",
                    "Bạn có quyền yêu cầu xóa dữ liệu liên quan đến ID thiết bị của mình bằng cách gửi email đính kèm ID thiết bị."),
                ("Liên hệ", "streetfoodnarrator@gmail.com")
            ]
        )
    ];

    public LegalPage(LegalType type)
    {
        InitializeComponent();
        var idx = type == LegalType.Terms ? 0 : 1;
        var (icon, title, subtitle, sections) = LegalContent[idx];

        TitleLabel.Text = title;
        IntroIcon.Text = icon;
        IntroTitle.Text = title;
        IntroSubtitle.Text = subtitle;

        foreach (var (heading, body) in sections)
        {
            var isContact = heading == "Liên hệ";
            var section = new Border
            {
                BackgroundColor = Color.FromArgb(isContact ? "#0A1E14" : "#0F1D18"),
                Stroke = Color.FromArgb(isContact ? "#22C55E33" : "#1E3D2A"),
                StrokeThickness = 1,
                Padding = new Thickness(16, 14),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = new CornerRadius(14) }
            };

            var inner = new VerticalStackLayout { Spacing = 6 };
            inner.Add(new Label
            {
                Text = heading,
                FontSize = 13,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb(isContact ? "#86EFAC" : "#22C55E")
            });
            inner.Add(new Label
            {
                Text = body,
                FontSize = 13,
                TextColor = Color.FromArgb("#94A3B8"),
                LineBreakMode = LineBreakMode.WordWrap
            });
            section.Content = inner;
            ContentStack.Add(section);
        }
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        try
        {
            if (Navigation.ModalStack.Count > 0)
                await Navigation.PopModalAsync(false);
            else
                await Navigation.PopAsync(false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LegalPage] OnBackClicked: {ex}");
        }
    }
}
