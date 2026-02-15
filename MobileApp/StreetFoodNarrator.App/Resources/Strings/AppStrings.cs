using System.Globalization;
using System.Resources;

namespace StreetFoodNarrator.App.Resources.Strings;

public static class AppStrings
{
    private static readonly ResourceManager ResourceManager =
        new("StreetFoodNarrator.App.Resources.Strings.AppStrings", typeof(AppStrings).Assembly);

    public static CultureInfo Culture { get; private set; } = CultureInfo.CurrentUICulture;

    public static void SetCulture(string lang)
    {
        Culture = lang switch
        {
            "en" => new CultureInfo("en-US"),
            "zh" => new CultureInfo("zh-CN"),
            _ => new CultureInfo("vi-VN")
        };

        CultureInfo.CurrentCulture = Culture;
        CultureInfo.CurrentUICulture = Culture;
        CultureInfo.DefaultThreadCurrentCulture = Culture;
        CultureInfo.DefaultThreadCurrentUICulture = Culture;
    }

    private static string GetString(string name) =>
        ResourceManager.GetString(name, Culture) ?? name;

    public static string Common_OK => GetString("Common_OK");
    public static string Common_Cancel => GetString("Common_Cancel");
    public static string Common_Continue => GetString("Common_Continue");
    public static string Common_Delete => GetString("Common_Delete");
    public static string Common_Retry => GetString("Common_Retry");
    public static string Common_Close => GetString("Common_Close");

    public static string Welcome_AppTitle => GetString("Welcome_AppTitle");
    public static string Welcome_Subtitle => GetString("Welcome_Subtitle");
    public static string Welcome_StartTour => GetString("Welcome_StartTour");
    public static string Welcome_Feature_Auto_Title => GetString("Welcome_Feature_Auto_Title");
    public static string Welcome_Feature_Auto_Desc => GetString("Welcome_Feature_Auto_Desc");
    public static string Welcome_Feature_Offline_Title => GetString("Welcome_Feature_Offline_Title");
    public static string Welcome_Feature_Offline_Desc => GetString("Welcome_Feature_Offline_Desc");
    public static string Welcome_Feature_Local_Title => GetString("Welcome_Feature_Local_Title");
    public static string Welcome_Feature_Local_Desc => GetString("Welcome_Feature_Local_Desc");

    public static string Status_Downloading_Metadata_Title => GetString("Status_Downloading_Metadata_Title");
    public static string Status_Downloading_Metadata_Detail => GetString("Status_Downloading_Metadata_Detail");
    public static string Status_Downloading_Offline_Title => GetString("Status_Downloading_Offline_Title");
    public static string Status_Downloading_Offline_Detail => GetString("Status_Downloading_Offline_Detail");
    public static string Status_Ready_Title => GetString("Status_Ready_Title");
    public static string Status_Ready_Detail => GetString("Status_Ready_Detail");
    public static string Status_NeedInternet_Title => GetString("Status_NeedInternet_Title");
    public static string Status_NeedInternet_Detail => GetString("Status_NeedInternet_Detail");
    public static string Status_Error_Title => GetString("Status_Error_Title");
    public static string Status_Error_Detail => GetString("Status_Error_Detail");

    public static string Button_DownloadOfflineNow => GetString("Button_DownloadOfflineNow");
    public static string Button_ContinueOnline => GetString("Button_ContinueOnline");
    public static string Button_TryAgain => GetString("Button_TryAgain");

    public static string Info_Offline_Title => GetString("Info_Offline_Title");
    public static string Info_Offline_Message => GetString("Info_Offline_Message");
    public static string Info_DontShowAgain => GetString("Info_DontShowAgain");

    public static string Alert_NeedInternet_Title => GetString("Alert_NeedInternet_Title");
    public static string Alert_NeedInternet_Message => GetString("Alert_NeedInternet_Message");
    public static string Alert_NoNetwork_Title => GetString("Alert_NoNetwork_Title");
    public static string Alert_NoNetwork_Message => GetString("Alert_NoNetwork_Message");
    public static string Alert_UsingCellular_Title => GetString("Alert_UsingCellular_Title");
    public static string Alert_UsingCellular_Message => GetString("Alert_UsingCellular_Message");
    public static string Alert_DownloadDone_Title => GetString("Alert_DownloadDone_Title");
    public static string Alert_DownloadDone_Message => GetString("Alert_DownloadDone_Message");
    public static string Alert_ManualBrowse_Title => GetString("Alert_ManualBrowse_Title");
    public static string Alert_ManualBrowse_Message => GetString("Alert_ManualBrowse_Message");
    public static string Alert_GoogleLogin_Message => GetString("Alert_GoogleLogin_Message");
    public static string Alert_Tts_Message => GetString("Alert_Tts_Message");
    public static string Alert_CheckOffline_Message => GetString("Alert_CheckOffline_Message");
    public static string Alert_DeleteOffline_Title => GetString("Alert_DeleteOffline_Title");
    public static string Alert_DeleteOffline_Message => GetString("Alert_DeleteOffline_Message");
    public static string Alert_DeleteOffline_Done => GetString("Alert_DeleteOffline_Done");
    public static string Alert_Success_Title => GetString("Alert_Success_Title");
    public static string Alert_Notice_Title => GetString("Alert_Notice_Title");

    public static string Progress_ConnectingMongo => GetString("Progress_ConnectingMongo");
    public static string Progress_DownloadingList => GetString("Progress_DownloadingList");
    public static string Progress_DownloadingAudio => GetString("Progress_DownloadingAudio");
    public static string Progress_SavingOffline => GetString("Progress_SavingOffline");
    public static string Progress_Done => GetString("Progress_Done");

    public static string Settings_Title => GetString("Settings_Title");
    public static string Settings_General => GetString("Settings_General");
    public static string Settings_LoginGoogle => GetString("Settings_LoginGoogle");
    public static string Settings_TourExperience => GetString("Settings_TourExperience");
    public static string Settings_Tts => GetString("Settings_Tts");
    public static string Settings_TtsSubtitle => GetString("Settings_TtsSubtitle");
    public static string Settings_AutoPlay => GetString("Settings_AutoPlay");
    public static string Settings_AutoPlayDesc => GetString("Settings_AutoPlayDesc");
    public static string Settings_Volume => GetString("Settings_Volume");
    public static string Settings_VolumeShort => GetString("Settings_VolumeShort");
    public static string Settings_Sensitivity => GetString("Settings_Sensitivity");
    public static string Settings_SensitivityShort => GetString("Settings_SensitivityShort");
    public static string Settings_SensitivityNear => GetString("Settings_SensitivityNear");
    public static string Settings_SensitivityFar => GetString("Settings_SensitivityFar");
    public static string Settings_Data => GetString("Settings_Data");
    public static string Settings_CheckOffline => GetString("Settings_CheckOffline");
    public static string Settings_LastUpdated => GetString("Settings_LastUpdated");
    public static string Settings_PrivacyTerms => GetString("Settings_PrivacyTerms");
    public static string Settings_Version => GetString("Settings_Version");

    public static string Nav_Map => GetString("Nav_Map");
    public static string Nav_Tour => GetString("Nav_Tour");
    public static string Nav_Saved => GetString("Nav_Saved");
    public static string Nav_Settings => GetString("Nav_Settings");
}
