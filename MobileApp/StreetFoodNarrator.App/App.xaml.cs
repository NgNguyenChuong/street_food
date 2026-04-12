using Microsoft.Maui.Storage;
using StreetFoodNarrator.App.Core.Services;
using StreetFoodNarrator.App.Core.Utils;
using StreetFoodNarrator.App.Helpers;
using StreetFoodNarrator.App.Views;

namespace StreetFoodNarrator.App;

public partial class App : Application
{
    private const string PREF_ONBOARDED = "has_onboarded";
    private const string PREF_LEGACY_ONBOARDED = "hasSeenOnboarding";
    private const string PREF_LAST_SYNC_TIME = "LastSyncTime";

    public App(LanguageService languageService, IRemoteLocalizationService remoteLocalizationService)
    {
        try
        {
            InitializeComponent();
        }
        catch (Exception ex)
        {
            AppendStartupErrorToLog(ex, "AppCtor.InitializeComponent");
        }

        try
        {
            // Restore saved language first so AppStrings culture is correct.
            languageService.RestoreSavedLanguage();
        }
        catch (Exception ex)
        {
            AppendStartupErrorToLog(ex, "AppCtor.RestoreSavedLanguage");
        }

        // Pull latest translation dictionary from backend (with offline cache fallback).
        _ = RunGuardedStartupTask(
            () => remoteLocalizationService.RefreshAsync(languageService.CurrentLanguage),
            "AppCtor.RefreshRemoteLocalization");

        try
        {
            // Keep translation dictionary synced when user changes language at runtime.
            LanguageService.LanguageChanged += (_, languageCode) =>
            {
                _ = RunGuardedStartupTask(
                    () => remoteLocalizationService.RefreshAsync(languageCode),
                    $"AppCtor.LanguageChanged.{languageCode}");
            };
        }
        catch (Exception ex)
        {
            AppendStartupErrorToLog(ex, "AppCtor.SubscribeLanguageChanged");
        }

        try
        {
            PageAppearing += OnPageAppearing;
        }
        catch (Exception ex)
        {
            AppendStartupErrorToLog(ex, "AppCtor.SubscribePageAppearing");
        }

        try
        {
            UserPreferenceEffects.ApplyCurrentPreferences();
        }
        catch (Exception ex)
        {
            AppendStartupErrorToLog(ex, "AppCtor.ApplyPreferences");
        }
    }

    private void OnPageAppearing(object? sender, Page page)
    {
        try
        {
            UserPreferenceEffects.ApplyCurrentPreferences(page);
        }
        catch (Exception ex)
        {
            AppendStartupErrorToLog(ex, "OnPageAppearing.ApplyPreferences");
        }
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        try
        {
            var hasOnboarded = Preferences.Get(PREF_ONBOARDED, false);
            if (!hasOnboarded)
            {
                // Migrate legacy onboarding/session markers so returning users skip WelcomePage.
                var legacyOnboarded = Preferences.Get(PREF_LEGACY_ONBOARDED, false);
                var hasSyncedBefore = !string.IsNullOrWhiteSpace(Preferences.Get(PREF_LAST_SYNC_TIME, ""));
                if (legacyOnboarded || hasSyncedBefore)
                {
                    hasOnboarded = true;
                    Preferences.Set(PREF_ONBOARDED, true);
                }
            }

            var rootPage = hasOnboarded
                // Returning user: show lightweight startup loading immediately, then hand off to AppShell.
                ? (Page)new StartupLoadingPage()
                // First time user: show WelcomePage via AppShell.
                : new AppShell(isOnboarding: true);

            rootPage.BackgroundColor = Color.FromArgb("#0A1612");
            return new Window(rootPage);
        }
        catch (Exception ex)
        {
            AppendStartupErrorToLog(ex, "CreateWindow");
            try
            {
                var fallbackPage = BuildStartupRecoveryPage(ex);
                return new Window(fallbackPage);
            }
            catch (Exception fallbackEx)
            {
                AppendStartupErrorToLog(fallbackEx, "CreateWindow.FallbackPage");
                return new Window(new ContentPage
                {
                    BackgroundColor = Colors.Black,
                    Content = new Label
                    {
                        Text = "Startup failed before UI init. Please reinstall latest APK and send startup_crash_log.txt.",
                        TextColor = Colors.White,
                        Margin = new Thickness(20),
                        VerticalOptions = LayoutOptions.Center,
                        HorizontalTextAlignment = TextAlignment.Center
                    }
                });
            }
        }
    }

    /// <summary>Called by WelcomePage after user completes onboarding.</summary>
    public static void CompleteOnboarding()
    {
        Preferences.Set(PREF_ONBOARDED, true);
    }

    internal static void AppendStartupErrorToLog(Exception ex, string stage)
    {
        StartupDiagnostics.Append(ex, stage);
    }

    private static async Task RunGuardedStartupTask(Func<Task> taskFactory, string stage)
    {
        try
        {
            await taskFactory();
        }
        catch (Exception ex)
        {
            AppendStartupErrorToLog(ex, stage);
        }
    }

    internal static ContentPage BuildStartupRecoveryPage(Exception ex)
    {
        var detail = $"{ex.GetType().Name}: {ex.Message}";

        var statusLabel = new Label
        {
            Text = "Ứng dụng vừa gặp lỗi khởi động. Bạn có thể thử lại hoặc mở luồng onboarding.",
            FontSize = 14,
            TextColor = Color.FromArgb("#B5C8BF"),
            HorizontalTextAlignment = TextAlignment.Center
        };

        var detailLabel = new Label
        {
            Text = detail,
            FontSize = 12,
            TextColor = Color.FromArgb("#7F9289"),
            HorizontalTextAlignment = TextAlignment.Center,
            LineBreakMode = LineBreakMode.WordWrap
        };

        var retryButton = new Button
        {
            Text = "Thử khởi động lại",
            BackgroundColor = Color.FromArgb("#22C55E"),
            TextColor = Colors.White,
            FontAttributes = FontAttributes.Bold,
            CornerRadius = 12,
            HeightRequest = 46
        };

        retryButton.Clicked += (_, _) =>
        {
            try
            {
                var window = Current?.Windows.FirstOrDefault();
                if (window != null)
                {
                    window.Page = new AppShell(isOnboarding: false);
                }
            }
            catch (Exception retryEx)
            {
                AppendStartupErrorToLog(retryEx, "RecoveryRetry");
                statusLabel.Text = "Vẫn chưa thể khởi động. Vui lòng thử cài lại APK mới nhất.";
                detailLabel.Text = $"{retryEx.GetType().Name}: {retryEx.Message}";
            }
        };

        var onboardingButton = new Button
        {
            Text = "Mở onboarding",
            BackgroundColor = Color.FromArgb("#12261D"),
            BorderColor = Color.FromArgb("#254539"),
            BorderWidth = 1,
            TextColor = Color.FromArgb("#A7DDBD"),
            CornerRadius = 12,
            HeightRequest = 42
        };

        onboardingButton.Clicked += (_, _) =>
        {
            try
            {
                var window = Current?.Windows.FirstOrDefault();
                if (window != null)
                {
                    window.Page = new AppShell(isOnboarding: true);
                }
            }
            catch (Exception onboardingEx)
            {
                AppendStartupErrorToLog(onboardingEx, "RecoveryOnboarding");
                statusLabel.Text = "Không thể mở onboarding. Vui lòng gửi log startup cho dev.";
                detailLabel.Text = $"{onboardingEx.GetType().Name}: {onboardingEx.Message}";
            }
        };

        return new ContentPage
        {
            BackgroundColor = Color.FromArgb("#0A1612"),
            Content = new ScrollView
            {
                Content = new VerticalStackLayout
                {
                    Padding = new Thickness(24, 70, 24, 24),
                    Spacing = 14,
                    Children =
                    {
                        new Label
                        {
                            Text = "Street Food Narrator",
                            FontSize = 26,
                            FontAttributes = FontAttributes.Bold,
                            TextColor = Color.FromArgb("#4BE277"),
                            HorizontalTextAlignment = TextAlignment.Center
                        },
                        new Label
                        {
                            Text = "Phát hiện lỗi khi mở ứng dụng",
                            FontSize = 18,
                            FontAttributes = FontAttributes.Bold,
                            TextColor = Color.FromArgb("#E7F6EC"),
                            HorizontalTextAlignment = TextAlignment.Center
                        },
                        statusLabel,
                        detailLabel,
                        retryButton,
                        onboardingButton
                    }
                }
            }
        };
    }
}
