using Microsoft.Maui.Storage;

namespace StreetFoodNarrator.App.Helpers;

/// <summary>
/// Applies runtime behavior driven by user preferences (haptic, keep-screen-on, large text).
/// </summary>
public static class UserPreferenceEffects
{
    public const string PrefHapticFeedback = "settings_haptic_feedback";
    public const string PrefKeepScreenOn = "settings_keep_screen_on";
    public const string PrefLargeText = "settings_large_text";

    private const double LargeTextScale = 1.14;

    private static readonly BindableProperty BaseFontSizeProperty = BindableProperty.CreateAttached(
        "BaseFontSize",
        typeof(double),
        typeof(UserPreferenceEffects),
        -1d);

    public static bool IsHapticEnabled()
        => Preferences.Get(PrefHapticFeedback, true);

    public static void PerformHapticClickIfEnabled()
    {
        if (!IsHapticEnabled())
            return;

        try
        {
            HapticFeedback.Default.Perform(HapticFeedbackType.Click);
        }
        catch
        {
            // Ignore unsupported-device errors.
        }
    }

    public static void ApplyKeepScreenOnPreference()
    {
        try
        {
            DeviceDisplay.Current.KeepScreenOn = Preferences.Get(PrefKeepScreenOn, false);
        }
        catch
        {
            // Ignore platform-level failures.
        }
    }

    public static void ApplyCurrentPreferences(Page? rootPage = null)
    {
        ApplyKeepScreenOnPreference();

        rootPage ??= GetActiveRootPage();
        if (rootPage != null)
            ApplyLargeTextPreference(rootPage);
    }

    private static Page? GetActiveRootPage()
    {
        var app = Application.Current;
        if (app == null)
            return null;

        var firstWindow = app.Windows.FirstOrDefault();
        return firstWindow?.Page;
    }

    public static void ApplyLargeTextToVisibleWindows()
    {
        ApplyLargeTextToVisibleWindows(Preferences.Get(PrefLargeText, false));
    }

    public static void ApplyLargeTextToVisibleWindows(bool enabled)
    {
        var app = Application.Current;
        if (app == null)
            return;

        foreach (var window in app.Windows)
        {
            if (window?.Page != null)
                ApplyLargeTextPreference(window.Page, enabled);
        }
    }

    public static void ApplyLargeTextPreference(Page rootPage)
    {
        ApplyLargeTextPreference(rootPage, Preferences.Get(PrefLargeText, false));
    }

    public static void ApplyLargeTextPreference(Page rootPage, bool enabled)
    {
        var scale = enabled ? LargeTextScale : 1.0;
        ApplyFontScaleRecursive(rootPage, scale);
    }

    private static void ApplyFontScaleRecursive(Element element, double scale)
    {
        ApplyScaleToElement(element, scale);

        foreach (var child in EnumerateChildren(element))
            ApplyFontScaleRecursive(child, scale);
    }

    private static IEnumerable<Element> EnumerateChildren(Element element)
    {
        switch (element)
        {
            case Shell shell when shell.CurrentPage != null:
                yield return shell.CurrentPage;
                break;
            case FlyoutPage flyout:
                if (flyout.Flyout != null)
                    yield return flyout.Flyout;
                if (flyout.Detail != null)
                    yield return flyout.Detail;
                break;
            case NavigationPage nav when nav.CurrentPage != null:
                yield return nav.CurrentPage;
                break;
            case TabbedPage tabbed:
                foreach (var childPage in tabbed.Children)
                    yield return childPage;
                break;
            case ContentPage page when page.Content != null:
                yield return page.Content;
                break;
            case ContentView contentView when contentView.Content != null:
                yield return contentView.Content;
                break;
            case Border border when border.Content != null:
                yield return border.Content;
                break;
            case ScrollView scrollView when scrollView.Content != null:
                yield return scrollView.Content;
                break;
            case Layout layout:
                foreach (var child in layout.Children)
                {
                    if (child is Element childElement)
                        yield return childElement;
                }
                break;
        }
    }

    private static void ApplyScaleToElement(Element element, double scale)
    {
        switch (element)
        {
            case Label label:
                ApplyScaledFont(label, label.FontSize, value => label.FontSize = value, scale);
                break;
            case Button button:
                ApplyScaledFont(button, button.FontSize, value => button.FontSize = value, scale);
                break;
            case Entry entry:
                ApplyScaledFont(entry, entry.FontSize, value => entry.FontSize = value, scale);
                break;
            case Editor editor:
                ApplyScaledFont(editor, editor.FontSize, value => editor.FontSize = value, scale);
                break;
            case SearchBar searchBar:
                ApplyScaledFont(searchBar, searchBar.FontSize, value => searchBar.FontSize = value, scale);
                break;
            case Picker picker:
                ApplyScaledFont(picker, picker.FontSize, value => picker.FontSize = value, scale);
                break;
            case DatePicker datePicker:
                ApplyScaledFont(datePicker, datePicker.FontSize, value => datePicker.FontSize = value, scale);
                break;
            case TimePicker timePicker:
                ApplyScaledFont(timePicker, timePicker.FontSize, value => timePicker.FontSize = value, scale);
                break;
        }
    }

    private static void ApplyScaledFont(BindableObject target, double currentFontSize, Action<double> setter, double scale)
    {
        if (currentFontSize <= 0 || double.IsNaN(currentFontSize) || double.IsInfinity(currentFontSize))
            return;

        var baseFontSize = (double)target.GetValue(BaseFontSizeProperty);
        if (baseFontSize <= 0)
        {
            baseFontSize = currentFontSize;
            target.SetValue(BaseFontSizeProperty, baseFontSize);
        }

        var scaled = Math.Round(baseFontSize * scale, 2);
        if (Math.Abs(currentFontSize - scaled) > 0.05)
            setter(scaled);
    }
}
