using Microsoft.Maui.ApplicationModel.DataTransfer;
using StreetFoodNarrator.App.ViewModels;
using System.Net.Http.Json;

namespace StreetFoodNarrator.App.Views;

public partial class PremiumTourPaywallPage : ContentPage
{
    private const string PlanDisplayName = "Tour Explore";
    private const string PlanPricePerMonth = "36.000 VND / tháng";
    private const string PlanPriceCompact = "36.000 VND";

    private readonly string _tourName;
    private readonly string _transferContent;
    private readonly MainViewModel? _viewModel;
    private bool _isConfirmingPayment;
    private bool _isRestoringVip;
    private string _latestRecoveryCode = string.Empty;
    private string _latestInvoiceNumber = string.Empty;
    private DateTime? _latestInvoiceCreatedAtUtc;
    private DateTime? _latestExpiresAtUtc;

    public PremiumTourPaywallPage(string? tourName = null, MainViewModel? viewModel = null)
    {
        InitializeComponent();
        _viewModel = viewModel;

        _tourName = string.IsNullOrWhiteSpace(tourName)
            ? "tour tiếp theo"
            : tourName.Trim();

        _transferContent = BuildTransferContent();
        TransferContentLabel.Text = _transferContent;

        IntroTitleLabel.Text = "Mở khóa Tour Explore";
        IntroMessageLabel.Text = $"Đăng ký VIP để có thể sử dụng các chức năng hấp dẫn khác trong app và tiếp tục khám phá \"{_tourName}\".";
        IntroBenefitsLabel.Text = "• Tương tác với nhiều POI khác\n• Xem thêm nhiều tour đặc sắc";
        PaymentTitleLabel.Text = "Thanh toán gói Tour Explore";
        PaymentPriceLabel.Text = PlanPricePerMonth;
        SuccessPlanNameLabel.Text = $"Gói {PlanDisplayName}";
        SuccessPriceLabel.Text = PlanPriceCompact;
    }

    public static async Task ShowAsync(INavigation? navigation, string? tourName = null, MainViewModel? viewModel = null)
    {
        if (navigation == null)
            return;

        if (navigation.ModalStack.LastOrDefault() is PremiumTourPaywallPage)
            return;

        await navigation.PushModalAsync(new PremiumTourPaywallPage(tourName, viewModel), false);
    }

    private static string BuildTransferContent()
    {
        var modelToken = string.IsNullOrWhiteSpace(DeviceInfo.Current.Model)
            ? "UserID"
            : new string(DeviceInfo.Current.Model
                .Where(char.IsLetterOrDigit)
                .Take(12)
                .ToArray());

        if (string.IsNullOrWhiteSpace(modelToken))
            modelToken = "UserID";

        return $"SUB_Tour_Explore_{modelToken}";
    }

    private static string GetOrCreateAnonymousDeviceId()
    {
        const string key = "analytics_anonymous_device_id";
        var current = Preferences.Get(key, string.Empty);
        if (!string.IsNullOrWhiteSpace(current))
            return current;

        var created = $"m-{Guid.NewGuid():N}";
        Preferences.Set(key, created);
        return created;
    }

    private void ShowIntroStep()
    {
        IntroStep.IsVisible = true;
        PaymentStep.IsVisible = false;
    }

    private void ShowPaymentStep()
    {
        IntroStep.IsVisible = false;
        PaymentStep.IsVisible = true;
    }

    private async void OnCloseTapped(object sender, TappedEventArgs e)
    {
        await NavigateBackToTourListAsync();
    }

    private void OnSubscribeNowClicked(object sender, EventArgs e)
    {
        ShowPaymentStep();
    }

    private async void OnMaybeLaterClicked(object sender, EventArgs e)
    {
        await Navigation.PopModalAsync(false);
    }

    private void OnBackToIntroClicked(object sender, EventArgs e)
    {
        ShowIntroStep();
    }

    private async void OnCopyTransferContentClicked(object sender, EventArgs e)
    {
        try
        {
            await Clipboard.Default.SetTextAsync(_transferContent);
            CopyContentButton.Text = "Đã sao chép";
        }
        catch
        {
            CopyContentButton.Text = "Không thể sao chép";
        }
    }

    private async void OnConfirmTransferClicked(object sender, EventArgs e)
    {
        if (_isConfirmingPayment || _isRestoringVip)
            return;

        _isConfirmingPayment = true;
        ConfirmTransferButton.IsEnabled = false;
        RestoreVipButton.IsEnabled = false;

        try
        {
            var status = await ConfirmDevicePaymentAsync();
            if (status == null || !status.IsVip)
            {
                await DisplayAlertAsync(
                    "Chưa thể xác nhận",
                    "Không thể xác nhận thanh toán lúc này. Vui lòng kiểm tra mạng và thử lại.",
                    "OK");
                return;
            }

            if (_viewModel != null)
            {
                _viewModel.ApplyVipSubscriptionFromServer(status.IsVip, status.InvoiceCreatedAtUtc, status.ExpiresAtUtc);
                await _viewModel.RefreshVipSubscriptionStatusAsync(force: true);
            }

            _latestInvoiceNumber = status.InvoiceNumber ?? "-";
            _latestInvoiceCreatedAtUtc = status.InvoiceCreatedAtUtc;
            _latestExpiresAtUtc = status.ExpiresAtUtc;
            _latestRecoveryCode = string.IsNullOrWhiteSpace(status.RecoveryCode)
                ? "-"
                : status.RecoveryCode.Trim().ToUpperInvariant();

            ShowSuccessStep();
        }
        catch
        {
            await DisplayAlertAsync(
                "Chưa thể xác nhận",
                "Không thể xác nhận thanh toán lúc này. Vui lòng thử lại sau.",
                "OK");
        }
        finally
        {
            _isConfirmingPayment = false;
            ConfirmTransferButton.IsEnabled = true;
            RestoreVipButton.IsEnabled = true;
        }
    }

    private void ShowSuccessStep()
    {
        SuccessPlanNameLabel.Text = $"Gói {PlanDisplayName}";
        SuccessPriceLabel.Text = PlanPriceCompact;
        SuccessRecoveryCodeLabel.Text = string.IsNullOrWhiteSpace(_latestRecoveryCode) ? "-" : _latestRecoveryCode;
        CopyRecoveryCodeButton.Text = "Sao chép";
        SuccessStep.IsVisible = true;
    }

    private async void OnSuccessCloseTapped(object sender, TappedEventArgs e)
    {
        await NavigateBackToTourListAsync();
    }

    private async void OnStartExploreNowClicked(object sender, EventArgs e)
    {
        await NavigateBackToTourListAsync();
    }

    private async void OnCopyRecoveryCodeClicked(object sender, EventArgs e)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_latestRecoveryCode) || _latestRecoveryCode == "-")
                return;

            await Clipboard.Default.SetTextAsync(_latestRecoveryCode);
            CopyRecoveryCodeButton.Text = "Đã sao chép";
        }
        catch
        {
            CopyRecoveryCodeButton.Text = "Lỗi";
        }
    }

    private async void OnDownloadInvoiceClicked(object sender, EventArgs e)
    {
        try
        {
            var invoiceCreatedText = FormatLocalDate(_latestInvoiceCreatedAtUtc);
            var expiresText = FormatLocalDate(_latestExpiresAtUtc);
            var invoiceSummary =
                $"Hoa don: {_latestInvoiceNumber}\n" +
                $"Goi: {PlanDisplayName}\n" +
                $"Gia: {PlanPricePerMonth}\n" +
                $"Ngay tao: {invoiceCreatedText}\n" +
                $"Hieu luc den: {expiresText}\n" +
                $"Ma khoi phuc: {_latestRecoveryCode}";

            await Clipboard.Default.SetTextAsync(invoiceSummary);
            await DisplayAlertAsync(
                "Hóa đơn demo",
                "Đã sao chép thông tin hóa đơn vào clipboard (bản demo chưa xuất PDF thật).",
                "OK");
        }
        catch
        {
            await DisplayAlertAsync("Lỗi", "Không thể tạo hóa đơn demo lúc này.", "OK");
        }
    }

    private async void OnRestoreVipClicked(object sender, EventArgs e)
    {
        if (_isConfirmingPayment || _isRestoringVip)
            return;

        var recoveryCode = await DisplayPromptAsync(
            "Khôi phục VIP",
            "Nhập mã khôi phục (ví dụ: VIP-ABCDE-12345)",
            "Khôi phục",
            "Hủy",
            "VIP-XXXXX-XXXXX",
            maxLength: 32,
            keyboard: Keyboard.Text);

        if (string.IsNullOrWhiteSpace(recoveryCode))
            return;

        _isRestoringVip = true;
        ConfirmTransferButton.IsEnabled = false;
        RestoreVipButton.IsEnabled = false;

        try
        {
            var status = await RestoreDeviceVipAsync(recoveryCode.Trim());
            if (status == null)
            {
                await DisplayAlertAsync(
                    "Khôi phục thất bại",
                    "Không tìm thấy mã khôi phục hoặc hệ thống đang bận. Vui lòng thử lại.",
                    "OK");
                return;
            }

            if (!status.IsVip)
            {
                await DisplayAlertAsync(
                    "Mã đã hết hạn",
                    "Mã khôi phục hợp lệ nhưng gói VIP đã hết hiệu lực. Vui lòng gia hạn để tiếp tục.",
                    "OK");
                return;
            }

            if (_viewModel != null)
            {
                _viewModel.ApplyVipSubscriptionFromServer(status.IsVip, status.InvoiceCreatedAtUtc, status.ExpiresAtUtc);
                await _viewModel.RefreshVipSubscriptionStatusAsync(force: true);
            }

            var expiresText = status.ExpiresAtUtc.HasValue
                ? status.ExpiresAtUtc.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm")
                : "-";

            await DisplayAlertAsync(
                "Khôi phục thành công",
                $"VIP đã được khôi phục trên thiết bị này.\nHiệu lực đến: {expiresText}",
                "OK");

            await Navigation.PopModalAsync(false);
        }
        catch
        {
            await DisplayAlertAsync(
                "Khôi phục thất bại",
                "Không thể khôi phục VIP lúc này. Vui lòng thử lại sau.",
                "OK");
        }
        finally
        {
            _isRestoringVip = false;
            ConfirmTransferButton.IsEnabled = true;
            RestoreVipButton.IsEnabled = true;
        }
    }

    private async Task<SubscriptionStatusDto?> ConfirmDevicePaymentAsync()
    {
        var deviceId = GetOrCreateAnonymousDeviceId();
        var url = AppConfig.BuildApiUrl("api/Subscriptions/confirm-device-payment");
        var payload = new ConfirmDevicePaymentDto
        {
            DeviceId = deviceId,
            TransferContent = _transferContent,
            Platform = DeviceInfo.Current.Platform.ToString(),
            Model = DeviceInfo.Current.Model,
            OsVersion = DeviceInfo.Current.VersionString,
            AppVersion = AppInfo.Current.VersionString
        };

        using var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        using var response = await client.PostAsJsonAsync(url, payload);
        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<SubscriptionStatusDto>();
    }

    private async Task<SubscriptionStatusDto?> RestoreDeviceVipAsync(string recoveryCode)
    {
        var deviceId = GetOrCreateAnonymousDeviceId();
        var url = AppConfig.BuildApiUrl("api/Subscriptions/restore-device-vip");
        var payload = new RestoreDeviceVipDto
        {
            DeviceId = deviceId,
            RecoveryCode = recoveryCode,
            Platform = DeviceInfo.Current.Platform.ToString(),
            Model = DeviceInfo.Current.Model,
            OsVersion = DeviceInfo.Current.VersionString,
            AppVersion = AppInfo.Current.VersionString
        };

        using var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        using var response = await client.PostAsJsonAsync(url, payload);
        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<SubscriptionStatusDto>();
    }

    private async Task NavigateBackToTourListAsync()
    {
        var navigation = Navigation;
        if (navigation == null)
            return;

        while (navigation.ModalStack.LastOrDefault() is Page top &&
               (top is PremiumTourPaywallPage || top is TourDetailPopupPage))
        {
            await navigation.PopModalAsync(false);
        }
    }

    private static string FormatLocalDate(DateTime? value)
    {
        if (!value.HasValue)
            return "-";

        return value.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
    }

    private sealed class ConfirmDevicePaymentDto
    {
        public string DeviceId { get; set; } = string.Empty;
        public string? TransferContent { get; set; }
        public string? Platform { get; set; }
        public string? Model { get; set; }
        public string? OsVersion { get; set; }
        public string? AppVersion { get; set; }
    }

    private sealed class SubscriptionStatusDto
    {
        public bool IsVip { get; set; }
        public string? InvoiceNumber { get; set; }
        public string? RecoveryCode { get; set; }
        public DateTime? InvoiceCreatedAtUtc { get; set; }
        public DateTime? ExpiresAtUtc { get; set; }
    }

    private sealed class RestoreDeviceVipDto
    {
        public string DeviceId { get; set; } = string.Empty;
        public string RecoveryCode { get; set; } = string.Empty;
        public string? Platform { get; set; }
        public string? Model { get; set; }
        public string? OsVersion { get; set; }
        public string? AppVersion { get; set; }
    }
}
