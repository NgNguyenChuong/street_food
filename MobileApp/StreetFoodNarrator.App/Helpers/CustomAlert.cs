using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Controls.Shapes;

namespace StreetFoodNarrator.App.Helpers;

/// <summary>
/// Custom Alert Dialog với theme đẹp đồng bộ app (xanh lá #22C55E)
/// </summary>
public static class CustomAlert
{
    /// <summary>
    /// Hiện alert đơn giản với 1 nút OK
    /// </summary>
    public static async Task ShowAsync(
        string title, 
        string message, 
        string okText = "OK",
        AlertType type = AlertType.Info)
    {
        await ShowCustomAlertAsync(title, message, okText, null, type);
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
        return await ShowCustomAlertAsync(title, message, confirmText, cancelText, type);
    }

    // CORE ALERT BUILDER
 
    private static async Task<bool> ShowCustomAlertAsync(
        string title,
        string message,
        string primaryText,
        string? secondaryText,
        AlertType type)
    {
        System.Diagnostics.Debug.WriteLine($"[CustomAlert] Showing: {title}");
        
        var tcs = new TaskCompletionSource<bool>();

        var overlay = new Grid
        {
            BackgroundColor = Color.FromArgb("#CC000000")
        };

        var dialog = new Border
        {
            BackgroundColor = Color.FromArgb("#0F1F17"),
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

        // ──────────────────────────────────────────────────────────
        // Icon + Title
        // ──────────────────────────────────────────────────────────
        var headerStack = new HorizontalStackLayout { Spacing = 12 };

        // Icon based on type
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

        // Title
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

        // ──────────────────────────────────────────────────────────
        // Message
        // ──────────────────────────────────────────────────────────
        var messageLabel = new Label
        {
            Text = message,
            FontSize = 14,
            TextColor = Color.FromArgb("#94A3B8"),
            LineBreakMode = LineBreakMode.WordWrap
        };
        content.Add(messageLabel);

        // ──────────────────────────────────────────────────────────
        // Buttons
        // ──────────────────────────────────────────────────────────
        if (string.IsNullOrEmpty(secondaryText))
        {
            // Single button (OK only)
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
            okButton.Clicked += (s, e) =>
            {
                tcs.SetResult(true);
                CloseDialog(overlay);
            };
            content.Add(okButton);
        }
        else
        {
            // Two buttons (Confirm + Cancel)
            var buttonGrid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
                },
                ColumnSpacing = 10
            };

            // Cancel button
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
            cancelButton.Clicked += (s, e) =>
            {
                tcs.SetResult(false);
                CloseDialog(overlay);
            };
            Grid.SetColumn(cancelButton, 0);
            buttonGrid.Add(cancelButton);

            // Confirm button
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
            confirmButton.Clicked += (s, e) =>
            {
                tcs.SetResult(true);
                CloseDialog(overlay);
            };
            Grid.SetColumn(confirmButton, 1);
            buttonGrid.Add(confirmButton);

            content.Add(buttonGrid);
        }

        dialog.Content = content;
        overlay.Children.Add(dialog);

        // Add to current page - improved logic
        Page? currentPage = null;
        
        // Try multiple ways to get current page
        if (Application.Current?.Windows?.FirstOrDefault() is Window window)
        {
            currentPage = window.Page;
            System.Diagnostics.Debug.WriteLine($"[CustomAlert] Got window.Page: {currentPage?.GetType().Name}");
        }
        
        // If in NavigationPage, get current page
        if (currentPage is NavigationPage navPage)
        {
            currentPage = navPage.CurrentPage;
            System.Diagnostics.Debug.WriteLine($"[CustomAlert] Navigation page detected, current page: {currentPage?.GetType().Name}");
        }
        
        if (currentPage is ContentPage contentPage && contentPage.Content != null)
        {
            System.Diagnostics.Debug.WriteLine($"[CustomAlert] Adding overlay to: {contentPage.GetType().Name}");
            
            var originalContent = contentPage.Content;
            
            // Always wrap in Grid to ensure overlay works
            var wrapper = new Grid();
            
            // Remove original content and add to wrapper
            contentPage.Content = null;
            wrapper.Children.Add(originalContent);
            wrapper.Children.Add(overlay);
            
            contentPage.Content = wrapper;
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[CustomAlert] ERROR: Could not find ContentPage to show alert! Page type: {currentPage?.GetType().Name}");
        }

        // Animate in
        dialog.Opacity = 0;
        dialog.Scale = 0.8;
        await Task.WhenAll(
            dialog.FadeToAsync(1, 250, Easing.CubicOut),
            dialog.ScaleToAsync(1, 250, Easing.CubicOut)
        );

        return await tcs.Task;
    }

    private static async void CloseDialog(Grid overlay)
    {
        // Fade out animation
        if (overlay.Children.FirstOrDefault() is Border dialog)
        {
            await Task.WhenAll(
                dialog.FadeToAsync(0, 200, Easing.CubicIn),
                dialog.ScaleToAsync(0.8, 200, Easing.CubicIn)
            );
        }
        
        // Remove overlay from parent
        if (overlay.Parent is Grid parentGrid)
        {
            parentGrid.Children.Remove(overlay);
            
            // If wrapper was temporary, restore original content
            if (parentGrid.Children.Count == 1 && parentGrid.Parent is ContentPage page)
            {
                if (parentGrid.Children[0] is View originalContent)
                {
                    parentGrid.Children.Clear();
                    page.Content = originalContent;
                }
            }
        }
    }
    // STYLING HELPERS
   
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
            AlertType.Success => Color.FromArgb("#22C55E20"),
            AlertType.Error => Color.FromArgb("#EF444420"),
            AlertType.Warning => Color.FromArgb("#F59E0B20"),
            AlertType.Info => Color.FromArgb("#3B82F620"),
            _ => Color.FromArgb("#3B82F620")
        };
    }

    private static Color GetButtonColor(AlertType type)
    {
        return type switch
        {
            AlertType.Success => Color.FromArgb("#22C55E"),
            AlertType.Error => Color.FromArgb("#EF4444"),
            AlertType.Warning => Color.FromArgb("#F59E0B"),
            AlertType.Info => Color.FromArgb("#3B82F6"),
            _ => Color.FromArgb("#22C55E")
        };
    }
}

// ══════════════════════════════════════════════════════════════
// ALERT TYPES
// ══════════════════════════════════════════════════════════════

public enum AlertType
{
    Success,  
    Error,    
    Warning,  
    Info  
}
