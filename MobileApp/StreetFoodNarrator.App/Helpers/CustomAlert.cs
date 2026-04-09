using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Devices;

namespace StreetFoodNarrator.App.Helpers;

/// <summary>
/// Custom Alert Dialog với theme đẹp đồng bộ app (xanh lá #22C55E)
/// </summary>
public static class CustomAlert
{
    private static readonly SemaphoreSlim AlertGate = new(1, 1);
    private const string OverlayClassId = "__custom_alert_overlay";
    private const string WrapperClassId = "__custom_alert_wrapper";
    private const uint EnterAnimationMs = 70;
    private const uint ExitAnimationMs = 60;

    // Android renders multiple shadows/scale effects less smoothly on mid/low-end devices.
    private static bool UseLightweightEffects =>
        DeviceInfo.Platform == DevicePlatform.Android || DeviceInfo.Idiom == DeviceIdiom.Phone;

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
        return ShowConfirmWithOptionalFlagAsync(title, message, confirmText, cancelText, type, dontShowAgainText, onDismiss);
    }

    private static async Task ShowConfirmWithOptionalFlagAsync(
        string title,
        string message,
        string confirmText,
        string cancelText,
        AlertType type,
        string? dontShowAgainText,
        Action<bool, bool> onDismiss)
    {
        var result = await ShowCustomAlertAsync(title, message, confirmText, cancelText, type, dontShowAgainText);
        onDismiss?.Invoke(result.accepted, result.dontShowAgainToday);
    }

    /// <summary>
    /// Shows a single-choice list dialog and returns selected option index.
    /// Returns null if user cancels.
    /// </summary>
    public static async Task<int?> ShowSelectionAsync(
        string title,
        IReadOnlyList<string> options,
        int selectedIndex = -1,
        string cancelText = "Hủy",
        Page? hostPage = null)
    {
        if (options == null || options.Count == 0)
            return null;

        await AlertGate.WaitAsync();
        try
        {
            var tcs = new TaskCompletionSource<int?>(TaskCreationOptions.RunContinuationsAsynchronously);

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
                Padding = new Thickness(20, 18),
                Margin = new Thickness(32),
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                MaximumWidthRequest = 340,
                StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(20) }
            };

            if (!UseLightweightEffects)
            {
                dialog.Shadow = new Shadow
                {
                    Brush = Colors.Black,
                    Opacity = 0.3f,
                    Radius = 20,
                    Offset = new Point(0, 10)
                };
            }

            var content = new VerticalStackLayout { Spacing = 12 };
            content.Add(new Label
            {
                Text = title,
                FontSize = 20,
                FontAttributes = FontAttributes.Bold,
                TextColor = Colors.White
            });

            var optionsStack = new VerticalStackLayout { Spacing = 8 };
            for (var i = 0; i < options.Count; i++)
            {
                var idx = i;
                var isSelected = idx == selectedIndex;

                var optionBorder = new Border
                {
                    BackgroundColor = isSelected ? Color.FromArgb("#2A5A44") : Color.FromArgb("#1A2925"),
                    Stroke = isSelected ? Color.FromArgb("#22C55E") : Color.FromArgb("#2A3F37"),
                    StrokeThickness = 1,
                    Padding = new Thickness(14, 12),
                    StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(12) }
                };

                optionBorder.Content = new Label
                {
                    Text = options[idx],
                    FontSize = 16,
                    TextColor = isSelected ? Color.FromArgb("#EAFBF0") : Color.FromArgb("#D8E6DF")
                };

                optionBorder.GestureRecognizers.Add(new TapGestureRecognizer
                {
                    Command = new Command(async () =>
                    {
                        await CloseDialogAsync(overlay);
                        tcs.TrySetResult(idx);
                    })
                });

                optionsStack.Add(optionBorder);
            }

            content.Add(optionsStack);

            var cancelButton = new Button
            {
                Text = cancelText,
                BackgroundColor = Color.FromArgb("#1A2925"),
                TextColor = Color.FromArgb("#94A3B8"),
                BorderColor = Color.FromArgb("#2A3F37"),
                BorderWidth = 1,
                FontSize = 15,
                HeightRequest = 46,
                CornerRadius = 12
            };
            cancelButton.Clicked += async (_, _) =>
            {
                await CloseDialogAsync(overlay);
                tcs.TrySetResult(null);
            };
            content.Add(cancelButton);

            dialog.Content = content;
            overlay.Children.Add(dialog);

            Page? currentPage = hostPage;
            if (currentPage == null && Shell.Current != null)
                currentPage = Shell.Current.CurrentPage;
            else if (currentPage == null && Application.Current?.Windows?.FirstOrDefault() is Window window)
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
                    AttachOverlay(rootLayout, overlay);
                }
                else
                {
                    var originalContent = contentPage.Content;
                    var wrapper = new Grid { ClassId = WrapperClassId };
                    contentPage.Content = null;
                    wrapper.Children.Add(originalContent);
                    AttachOverlay(wrapper, overlay);
                    contentPage.Content = wrapper;
                }
            }

            dialog.Opacity = 0;
            if (UseLightweightEffects)
            {
                dialog.Scale = 1;
                await dialog.FadeToAsync(1, EnterAnimationMs, Easing.CubicOut);
            }
            else
            {
                dialog.Scale = 0.9;
                await Task.WhenAll(
                    dialog.FadeToAsync(1, EnterAnimationMs, Easing.CubicOut),
                    dialog.ScaleToAsync(1, EnterAnimationMs, Easing.CubicOut)
                );
            }

            return await tcs.Task;
        }
        finally
        {
            AlertGate.Release();
        }
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
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(20) }
        };
        if (!UseLightweightEffects)
        {
            dialog.Shadow = new Shadow
            {
                Brush = Colors.Black,
                Opacity = 0.3f,
                Radius = 20,
                Offset = new Point(0, 10)
            };
        }

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
                CornerRadius = 12
            };
            if (!UseLightweightEffects)
            {
                okButton.Shadow = new Shadow
                {
                    Brush = GetButtonColor(type),
                    Opacity = 0.3f,
                    Radius = 12,
                    Offset = new Point(0, 4)
                };
            }
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
                AttachOverlay(rootLayout, overlay);
            }
            else
            {
                var originalContent = contentPage.Content;
                var wrapper = new Grid { ClassId = WrapperClassId };
                contentPage.Content = null;
                wrapper.Children.Add(originalContent);
                AttachOverlay(wrapper, overlay);
                contentPage.Content = wrapper;
            }
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[CustomAlert] ERROR: Could not find ContentPage!");
        }

        // Animate in
        dialog.Opacity = 0;
        if (UseLightweightEffects)
        {
            dialog.Scale = 1;
            await dialog.FadeToAsync(1, EnterAnimationMs, Easing.CubicOut);
        }
        else
        {
            dialog.Scale = 0.9;
            await Task.WhenAll(
                dialog.FadeToAsync(1, EnterAnimationMs, Easing.CubicOut),
                dialog.ScaleToAsync(1, EnterAnimationMs, Easing.CubicOut)
            );
        }

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
                if (UseLightweightEffects)
                {
                    await dialog.FadeToAsync(0, ExitAnimationMs, Easing.CubicIn);
                }
                else
                {
                    await Task.WhenAll(
                        dialog.FadeToAsync(0, ExitAnimationMs, Easing.CubicIn),
                        dialog.ScaleToAsync(0.9, ExitAnimationMs, Easing.CubicIn)
                    );
                }
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

    private static void AttachOverlay(Layout layout, Grid overlay)
    {
        overlay.ZIndex = int.MaxValue;

        if (layout is Grid hostGrid)
        {
            var rowCount = hostGrid.RowDefinitions?.Count ?? 0;
            var colCount = hostGrid.ColumnDefinitions?.Count ?? 0;

            Grid.SetRow(overlay, 0);
            Grid.SetColumn(overlay, 0);
            Grid.SetRowSpan(overlay, Math.Max(1, rowCount));
            Grid.SetColumnSpan(overlay, Math.Max(1, colCount));
        }
        else if (layout is AbsoluteLayout)
        {
            AbsoluteLayout.SetLayoutFlags(overlay, Microsoft.Maui.Layouts.AbsoluteLayoutFlags.All);
            AbsoluteLayout.SetLayoutBounds(overlay, new Rect(0, 0, 1, 1));
        }

        layout.Children.Add(overlay);
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
