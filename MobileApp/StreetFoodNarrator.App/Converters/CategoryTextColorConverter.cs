using System.Globalization;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace StreetFoodNarrator.App.Converters;

/// <summary>
/// Returns text color for a category chip based on its selected state.
/// </summary>
public class CategoryTextColorConverter : IValueConverter
{
    // In MAUI, VisualState is managed via VisualStateManager — this converter
    // is a fallback used inside the Label.TextColor binding in the chip template.
    // We return the default unselected color; selected state is handled via VSM.
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return Color.FromArgb("#BCCBB9"); // on-surface-variant
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
