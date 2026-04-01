using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Controls.Shapes;

namespace StreetFoodNarrator.App.Helpers;

/// <summary>
/// Custom Alert Dialog với theme đẹp đồng bộ app (xanh lá #22C55E)
/// </summary>
public static class CustomAlert
{
    private static readonly SemaphoreSlim AlertGate = new(1, 1);
    private const string OverlayClassId = "__custom_alert_overlay";
    private const string WrapperClassId = "__custom_alert_wrapper";

    /// <summary>
    /// Hiện alert đơn giản với 1 nút OK
    /// </summary>
    public static async Task ShowAsync(
        string title,
        string message,
        string okText = "OK",
        AlertType type = AlertType.Info)
    {
        await ShowCustomAlertAsync(title, message, okText, null, type, null);
    }

    /// <summary>
    /// Hiện alert với 2 nút (Confirm + Cancel)
    /// </summary>
    public static async Task<bool> ShowConfirmAsync(
        string title,
        string message,
        string confirmText = "Xác nhận",
        string cancelText = "Hủy",
        AlertType type = AlertType.Warning)
    {
        var r = await ShowCustomAlertAsync(title, message, confirmText, cancelText, type, null);
        return r.accepted;
    }

    /// <summary>
    /// Hiện alert với 2 nút + callback khi nhấn confirm.
    /// Callback nhận true/false tuỳ người dùng chọn nút nào.
    /// </summary>
    public static async Task ShowConfirmAsync(
        string title,
        string message,
        string confirmText,
        string cancelText,
        AlertType type,
        Action<bool> onDismiss)
    {
        var result = await ShowConfirmAsync(title, message, confirmText, cancelText, type);
        onDismiss?.Invoke(result);
    }

    /// <summary>
    /// Hiện alert với 2 nút + optional checkbox + callback.
    /// Callback nhận (accepted, dontShowAgainToday).
    /// </summary>
    public static Task ShowConfirmAsync(
        string title,
        string message,
        string confirmText,
        string cancelText,
        AlertType type,
        string? dontShowAgainText,
        Action<bool, bool> onDismiss)
    {
        return ShowCustomAlertAsync(title, message, confirmText, cancelText, type, dontShowAgainText)
            .ContinueWith(t => onDismiss?.Invoke(t.Result.accepted, t.Result.dontShowAgainToday));
    }

    // ══════════════════════════════════════════════════════════════
    // CORE ALERT BUILDER
    // ══════════════════════════════════════════════════════════════

    private static async Task<AlertResult> ShowCustomAlertAsync(
        string title,
        string message,
        string primaryText,
        string? secondaryText,
        AlertType type,
        string? dontShowAgainText)
    {
        await AlertGate.WaitAsync();
        try
        {
        System.Diagnostics.Debug.WriteLine($"[CustomAlert] Showing: {title}");

        var tcs = new TaskCompletionSource<AlertResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        bool dontShowAgain = false;

        var overlay = new Grid
        {
            BackgroundColor = Color.FromArgb("#66000000"),
            ClassId = OverlayClassId
        };

        var dialog = new Border
        {
            BackgroundColor = Color.FromArgb("#234936"),
            Stroke = Color.FromArgb("#1E3D2A"),
            StrokeThickness = 2,
            Padding = new Thickness(24, 20),
            Margin = new Thickness(32),
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            MaximumWidthRequest = 340,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(20) },
            Shadow = new Shadow
            {
                Brush = Colors.Black,
                Opacity = 0.3f,
                Radius = 20,
                Offset = new Point(0, 10)
            }
        };

        var content = new VerticalStackLayout { Spacing = 16 };

        // ── Icon + Title ──────────────────────────────────────────
        var headerStack = new HorizontalStackLayout { Spacing = 12 };

        var iconBorder = new Border
        {
            BackgroundColor = GetIconBackground(type),
            WidthRequest = 48,
            HeightRequest = 48,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(12) }
        };
        iconBorder.Content = new Label
        {
            Text = GetIcon(type),
            FontSize = 28,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };
        headerStack.Add(iconBorder);

        var titleLabel = new Label
        {
            Text = title,
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White,
            VerticalOptions = LayoutOptions.Center
        };
        headerStack.Add(titleLabel);
        content.Add(headerStack);

        // ── Message ────────────────────────────────────────────────
        var messageLabel = new Label
        {
            Text = message,
            FontSize = 14,
            TextColor = Color.FromArgb("#94A3B8"),
            LineBreakMode = LineBreakMode.WordWrap
        };
        content.Add(messageLabel);

        // ── Optional checkbox ──────────────────────────────────────
        if (!string.IsNullOrEmpty(dontShowAgainText))
        {
            var checkRow = new HorizontalStackLayout { Spacing = 8 };
            var checkbox = new CheckBox
            {
                Color = Color.FromArgb("#22C55E"),
                VerticalOptions = LayoutOptions.Center
            };
            checkbox.CheckedChanged += (_, e) => dontShowAgain = e.Value;
            checkRow.Add(checkbox);
            checkRow.Add(new Label
            {
                Text = dontShowAgainText,
                FontSize = 13,
                TextColor = Color.FromArgb("#94A3B8"),
                VerticalOptions = LayoutOptions.Center
            });
            content.Add(checkRow);
        }

        // ── Buttons ───────────────────────────────────────────────
        var isClosing = false;

        async Task ResolveAndCloseAsync(bool accepted)
        {
            if (isClosing)
                return;

            isClosing = true;
            try
            {
                await CloseDialogAsync(overlay);
            }
            finally
            {
                tcs.TrySetResult(new AlertResult(accepted, dontShowAgain));
            }
        }

        if (string.IsNullOrEmpty(secondaryText))
        {
            var okButton = new Button
            {
                Text = primaryText,
                BackgroundColor = GetButtonColor(type),
                TextColor = Colors.White,
                FontSize = 15,
                FontAttributes = FontAttributes.Bold,
                HeightRequest = 48,
                CornerRadius = 12,
                Shadow = new Shadow
                {
                    Brush = GetButtonColor(type),
                    Opacity = 0.3f,
                    Radius = 12,
                    Offset = new Point(0, 4)
                }
            };
            okButton.Clicked += async (s, e) => await ResolveAndCloseAsync(true);
            content.Add(okButton);
        }
        else
        {
            var buttonGrid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
                },
                ColumnSpacing = 10
            };

            var cancelButton = new Button
            {
                Text = secondaryText,
                BackgroundColor = Color.FromArgb("#1A2925"),
                TextColor = Color.FromArgb("#94A3B8"),
                BorderColor = Color.FromArgb("#2A3F37"),
                BorderWidth = 1,
                FontSize = 15,
                HeightRequest = 48,
                CornerRadius = 12
            };
            cancelButton.Clicked += async (s, e) => await ResolveAndCloseAsync(false);
            Grid.SetColumn(cancelButton, 0);
            buttonGrid.Add(cancelButton);

            var confirmButton = new Button
            {
                Text = primaryText,
                BackgroundColor = GetButtonColor(type),
                TextColor = Colors.White,
                FontSize = 15,
                FontAttributes = FontAttributes.Bold,
                HeightRequest = 48,
                CornerRadius = 12
            };
            confirmButton.Clicked += async (s, e) => await ResolveAndCloseAsync(true);
            Grid.SetColumn(confirmButton, 1);
            buttonGrid.Add(confirmButton);

            content.Add(buttonGrid);
        }

        dialog.Content = content;
        overlay.Children.Add(dialog);

        // Add to current page
        Page? currentPage = null;
        if (Shell.Current != null)
            currentPage = Shell.Current.CurrentPage;
        else if (Application.Current?.Windows?.FirstOrDefault() is Window window)
        {
            currentPage = window.Page;
            if (currentPage is NavigationPage navPage)
                currentPage = navPage.CurrentPage;
        }

        if (currentPage is ContentPage contentPage && contentPage.Content != null)
        {
            if (contentPage.Content is Layout rootLayout)
            {
                RemoveStaleOverlays(rootLayout);
                rootLayout.Children.Add(overlay);
            }
            else
            {
                var originalContent = contentPage.Content;
                var wrapper = new Grid { ClassId = WrapperClassId };
                contentPage.Content = null;
                wrapper.Children.Add(originalContent);
                wrapper.Children.Add(overlay);
                contentPage.Content = wrapper;
            }
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[CustomAlert] ERROR: Could not find ContentPage!");
        }

        // Animate in
        dialog.Opacity = 0;
        dialog.Scale = 0.8;
        await Task.WhenAll(
            dialog.FadeToAsync(1, 140, Easing.CubicOut),
            dialog.ScaleToAsync(1, 140, Easing.CubicOut)
        );

        return await tcs.Task;
        }
        finally
        {
            AlertGate.Release();
        }
    }

    private static async Task CloseDialogAsync(Grid overlay)
    {
        try
        {
            if (overlay.Children.FirstOrDefault() is Border dialog)
            {
                await Task.WhenAll(
                    dialog.FadeToAsync(0, 120, Easing.CubicIn),
                    dialog.ScaleToAsync(0.8, 120, Easing.CubicIn)
                );
            }
        }
        catch
        {
            // Ignore animation errors; always continue removal.
        }
        finally
        {
            if (overlay.Parent is Layout parentLayout)
            {
                parentLayout.Children.Remove(overlay);

                if (parentLayout is Grid parentGrid &&
                    parentGrid.ClassId == WrapperClassId &&
                    parentGrid.Children.Count == 1 &&
                    parentGrid.Parent is ContentPage page)
                {
                    if (parentGrid.Children[0] is View originalContent)
                    {
                        parentGrid.Children.Clear();
                        page.Content = originalContent;
                    }
                }
            }
        }
    }

    private static void RemoveStaleOverlays(Layout layout)
    {
        var stale = layout.Children
            .OfType<View>()
            .Where(c => c.ClassId == OverlayClassId)
            .ToList();

        foreach (var ov in stale)
            layout.Children.Remove(ov);

        foreach (var childLayout in layout.Children.OfType<Layout>().ToList())
            RemoveStaleOverlays(childLayout);
    }

    // ══════════════════════════════════════════════════════════════
    // STYLING HELPERS
    // ══════════════════════════════════════════════════════════════

    private static string GetIcon(AlertType type)
    {
        return type switch
        {
            AlertType.Success => "✓",
            AlertType.Error => "✕",
            AlertType.Warning => "⚠",
            AlertType.Info => "ℹ",
            _ => "ℹ"
        };
    }

    private static Color GetIconBackground(AlertType type)
    {
        return type switch
        {
            AlertType.Success => Color.FromArgb("#07f151"),
            AlertType.Error => Color.FromArgb("#ef4444db"),
            AlertType.Warning => Color.FromArgb("#f59f0b"),
            AlertType.Info => Color.FromArgb("#3b83f6"),
            _ => Color.FromArgb("#3b83f6ef")
        };
    }

    private static Color GetButtonColor(AlertType type)
    {
        return type switch
        {
            AlertType.Success => Color.FromArgb("#22C55E"),
            AlertType.Error => Color.FromArgb("#EF4444"),
            AlertType.Warning => Color.FromArgb("#F59E0B"),
            AlertType.Info => Color.FromArgb("#367ae614"),
            _ => Color.FromArgb("#22C55E")
        };
    }
}

// ══════════════════════════════════════════════════════════════
// ALERT RESULT + TYPES
// ══════════════════════════════════════════════════════════════

public readonly struct AlertResult
{
    public bool accepted { get; }
    public bool dontShowAgainToday { get; }

    public AlertResult(bool accepted, bool dontShowAgainToday)
    {
        this.accepted = accepted;
        this.dontShowAgainToday = dontShowAgainToday;
    }
}

public enum AlertType
{
    Success,
    Error,
    Warning,
    Info
}
