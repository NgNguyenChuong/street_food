# Feature Commit Tracker

Tu dong sinh tu lich su git (--all) de theo doi cac chuc nang chinh trong Project-Event-Flows.

- Last updated: 2026-04-14 18:24:34
- Current branch: sub_Develop
- Nguon du lieu: git log --all --follow -S <marker> -- <file>

## Tong hop

| Flow | Nhom | Chuc nang | File | Marker | First commit (feature) | Latest commit (feature) | Trang thai |
|---|---|---|---|---|---|---|---|
| F0 | PRD | Tong quan hanh trinh he thong | [Documentation/Project-Event-Flows.md](Documentation/Project-Event-Flows.md#L7) | F0 | 2026-04-08 (beb4f40) | 2026-04-12 (18ee810) | UPDATED |
| F1 | PRD | Khoi dong app, cache, dong bo | [MobileApp/StreetFoodNarrator.App/ViewModels/MainViewModel.cs](MobileApp/StreetFoodNarrator.App/ViewModels/MainViewModel.cs#L177) | sync | 2026-02-15 (194880c) | 2026-04-11 (07ab7c0) | UPDATED |
| F2 | PRD | GPS + geofence vao/ra vung | [MobileApp/StreetFoodNarrator.App/Core/Services/LocationService.cs](MobileApp/StreetFoodNarrator.App/Core/Services/LocationService.cs#L46) | geofence | 2026-04-05 (d1c960e) | 2026-04-05 (d1c960e) | NO_CHANGE_SINCE_INTRO |
| F4 | PRD | Audio fallback 4 tang | [MobileApp/StreetFoodNarrator.App/Core/Services/AudioService.cs](MobileApp/StreetFoodNarrator.App/Core/Services/AudioService.cs#L340) | fallback | 2026-03-03 (f81393f) | 2026-04-07 (c98cef9) | UPDATED |
| F12 | PRD | QR deeplink vao POI/Tour | [API/StreetFoodNarrator.API/Program.cs](API/StreetFoodNarrator.API/Program.cs#L295) | /qr/{**deepPath} | 2026-04-09 (c968245) | 2026-04-09 (c968245) | NO_CHANGE_SINCE_INTRO |
| F17 | PRD | Vendor tao/sua POI gui duyet | [API/StreetFoodNarrator.API/Controllers/POIsController.cs](API/StreetFoodNarrator.API/Controllers/POIsController.cs#L516) | pending | 2026-02-15 (194880c) | 2026-04-08 (beb4f40) | UPDATED |
| F20 | PRD | Quan ly tour + rule free/premium | [API/StreetFoodNarrator.API/Controllers/ToursController.cs](API/StreetFoodNarrator.API/Controllers/ToursController.cs#L110) | free | 2026-04-10 (659d910) | 2026-04-10 (659d910) | NO_CHANGE_SINCE_INTRO |
| F24 | PRD | Xem va tham gia tour tren mobile | [MobileApp/StreetFoodNarrator.App/Views/MainPage.Tour.cs](MobileApp/StreetFoodNarrator.App/Views/MainPage.Tour.cs#L49) | VirtualTour | 2026-03-25 (d9c932c) | 2026-04-04 (1fc8784) | UPDATED |
| F27 | PRD | Vendor thanh toan premium, kich hoat ngay (khong can admin duyet) | [API/StreetFoodNarrator.API/Controllers/PaymentsController.cs](API/StreetFoodNarrator.API/Controllers/PaymentsController.cs#L29) | simulate-premium | 2026-04-10 (3d740ed) | 2026-04-10 (6d9d415) | UPDATED |
| F31 | PRD | Landing cong khai tai APK | [API/StreetFoodNarrator.API/wwwroot/apk-download.html](API/StreetFoodNarrator.API/wwwroot/apk-download.html#L6) | apk | 2026-04-11 (31f9cef) | 2026-04-11 (31f9cef) | NO_CHANGE_SINCE_INTRO |
| F32 | PRD | Onboarding first-run | [MobileApp/StreetFoodNarrator.App/Views/WelcomePage.xaml.cs](MobileApp/StreetFoodNarrator.App/Views/WelcomePage.xaml.cs#L1675) | first-run | 2026-04-04 (1fc8784) | 2026-04-04 (1fc8784) | NO_CHANGE_SINCE_INTRO |
| F33 | PRD | Offline 2 pha (essential + deferred) | [MobileApp/StreetFoodNarrator.App/Views/WelcomePage.xaml.cs](MobileApp/StreetFoodNarrator.App/Views/WelcomePage.xaml.cs#L98) | RunSimpleFlowAsync | 2026-02-15 (194880c) | 2026-02-15 (194880c) | NO_CHANGE_SINCE_INTRO |
| F34 | PRD | Map fallback khi offline khong cache | [MobileApp/StreetFoodNarrator.App/Views/MainPage.Map.cs](MobileApp/StreetFoodNarrator.App/Views/MainPage.Map.cs#L89) | CreateOfflineFallbackLayer | 2026-03-30 (ee37f4f) | 2026-03-30 (ee37f4f) | NO_CHANGE_SINCE_INTRO |
| F12-SUP | Support | Parser deeplink mobile | [MobileApp/StreetFoodNarrator.App/Core/Utils/QrDeepLinkManager.cs](MobileApp/StreetFoodNarrator.App/Core/Utils/QrDeepLinkManager.cs#L6) | deeplink | 2026-04-04 (1fc8784) | 2026-04-04 (1fc8784) | NO_CHANGE_SINCE_INTRO |
| F24-SUP | Support | Virtual tour view model | [MobileApp/StreetFoodNarrator.App/ViewModels/VirtualTourViewModel.cs](MobileApp/StreetFoodNarrator.App/ViewModels/VirtualTourViewModel.cs#L6) | virtual | 2026-03-25 (d9c932c) | 2026-03-25 (d9c932c) | NO_CHANGE_SINCE_INTRO |

## Chi tiet commit theo tung flow

### F0 - Tong quan hanh trinh he thong
- Nhom: PRD
- File: [Documentation/Project-Event-Flows.md](Documentation/Project-Event-Flows.md#L7)
- Marker: F0
- First commit theo chuc nang: 2026-04-08 | beb4f40 | Refactor code structure and remove redundant code blocks for improved readability and maintainability
- Latest commit theo chuc nang: 2026-04-12 | 18ee810 | fix onboarding when first time come to app
- First commit cua file: 2026-04-08 | beb4f40 | Refactor code structure and remove redundant code blocks for improved readability and maintainability
- Latest commit cua file: 2026-04-12 | 18ee810 | fix onboarding when first time come to app
- Branches co chua latest commit: app_fix, develop, origin, origin/app_fix, origin/delete_feature_no_needed, origin/develop, origin/main, sub_Develop, testFeature
- Trang thai: UPDATED

### F1 - Khoi dong app, cache, dong bo
- Nhom: PRD
- File: [MobileApp/StreetFoodNarrator.App/ViewModels/MainViewModel.cs](MobileApp/StreetFoodNarrator.App/ViewModels/MainViewModel.cs#L177)
- Marker: sync
- First commit theo chuc nang: 2026-02-15 | 194880c | Tao trang quan ly admin va vendor với progress 60% va app thuyet minh (Hien tai chi dang duoc su dung boi may ao Android ) da tao UI/UX cua welcomePage va setting với progress 70%. Ke tiep se fix lai loi cua audio demo trang setting va lam phan mainPage app
- Latest commit theo chuc nang: 2026-04-11 | 07ab7c0 | Premium: update registration flow, admin management UI, and notifications
- First commit cua file: 2026-02-15 | 194880c | Tao trang quan ly admin va vendor với progress 60% va app thuyet minh (Hien tai chi dang duoc su dung boi may ao Android ) da tao UI/UX cua welcomePage va setting với progress 70%. Ke tiep se fix lai loi cua audio demo trang setting va lam phan mainPage app
- Latest commit cua file: 2026-04-11 | 07ab7c0 | Premium: update registration flow, admin management UI, and notifications
- Branches co chua latest commit: app_fix, appQR_deployApk, develop, origin, origin/app_fix, origin/appQR_deployApk, origin/delete_feature_no_needed, origin/develop, origin/fix_duplicated_adding_user, origin/fix_logicial_problem, origin/main, origin/registerVip, origin/releaseApp, origin/userAnalytics, registerVip, releaseApp, sub_Develop, testFeature, userAnalytics
- Trang thai: UPDATED

### F2 - GPS + geofence vao/ra vung
- Nhom: PRD
- File: [MobileApp/StreetFoodNarrator.App/Core/Services/LocationService.cs](MobileApp/StreetFoodNarrator.App/Core/Services/LocationService.cs#L46)
- Marker: geofence
- First commit theo chuc nang: 2026-04-05 | d1c960e | feat: Implement localization for static texts in POI detail and tour detail pages
- Latest commit theo chuc nang: 2026-04-05 | d1c960e | feat: Implement localization for static texts in POI detail and tour detail pages
- First commit cua file: 2026-02-15 | 194880c | Tao trang quan ly admin va vendor với progress 60% va app thuyet minh (Hien tai chi dang duoc su dung boi may ao Android ) da tao UI/UX cua welcomePage va setting với progress 70%. Ke tiep se fix lai loi cua audio demo trang setting va lam phan mainPage app
- Latest commit cua file: 2026-04-11 | 31f9cef | Update QR for app and release apk
- Branches co chua latest commit: app_final, app_fix, app_ver2.5, appQR_deployApk, develop, fix_meanAudio, fix_tourApp, fix_userRoute, fix_Web, origin, origin/app_final, origin/app_fix, origin/app_ver2.5, origin/appQR_deployApk, origin/change_user, origin/conflig_resolve, origin/delete_feature_admin, origin/delete_feature_no_needed, origin/delete_POI, origin/design_popup, origin/develop, origin/feature_approved, origin/final1, origin/fix_audio, origin/fix_audio_gen, origin/fix_duplicated_adding_user, origin/fix_history, origin/fix_logicial_problem, origin/fix_meanAudio, origin/fix_side_bar, origin/fix_tourApp, origin/fix_userRoute, origin/fix_Web, origin/main, origin/qr_tour, origin/redesign_fe, origin/registerVip, origin/releaseApp, origin/submision_POI, origin/test_nha, origin/test1, origin/userAnalytics, qr_tour, registerVip, releaseApp, sub_Develop, test_nha, testFeature, userAnalytics
- Trang thai: NO_CHANGE_SINCE_INTRO

### F4 - Audio fallback 4 tang
- Nhom: PRD
- File: [MobileApp/StreetFoodNarrator.App/Core/Services/AudioService.cs](MobileApp/StreetFoodNarrator.App/Core/Services/AudioService.cs#L340)
- Marker: fallback
- First commit theo chuc nang: 2026-03-03 | f81393f | Add Vietnamese language support and new features for Street Food Narrator app
- Latest commit theo chuc nang: 2026-04-07 | c98cef9 | Enhance audio playback with fallback text and improve language selection UI
- First commit cua file: 2026-02-15 | 194880c | Tao trang quan ly admin va vendor với progress 60% va app thuyet minh (Hien tai chi dang duoc su dung boi may ao Android ) da tao UI/UX cua welcomePage va setting với progress 70%. Ke tiep se fix lai loi cua audio demo trang setting va lam phan mainPage app
- Latest commit cua file: 2026-04-11 | 31f9cef | Update QR for app and release apk
- Branches co chua latest commit: app_final, app_fix, appQR_deployApk, develop, fix_meanAudio, fix_tourApp, fix_userRoute, origin, origin/app_final, origin/app_fix, origin/appQR_deployApk, origin/conflig_resolve, origin/delete_feature_admin, origin/delete_feature_no_needed, origin/delete_POI, origin/design_popup, origin/develop, origin/fix_duplicated_adding_user, origin/fix_logicial_problem, origin/fix_meanAudio, origin/fix_tourApp, origin/fix_userRoute, origin/main, origin/qr_tour, origin/registerVip, origin/releaseApp, origin/submision_POI, origin/test_nha, origin/userAnalytics, qr_tour, registerVip, releaseApp, sub_Develop, test_nha, testFeature, userAnalytics
- Trang thai: UPDATED

### F12 - QR deeplink vao POI/Tour
- Nhom: PRD
- File: [API/StreetFoodNarrator.API/Program.cs](API/StreetFoodNarrator.API/Program.cs#L295)
- Marker: /qr/{**deepPath}
- First commit theo chuc nang: 2026-04-09 | c968245 | chore: update API and mobile app logic
- Latest commit theo chuc nang: 2026-04-09 | c968245 | chore: update API and mobile app logic
- First commit cua file: 2026-02-15 | 194880c | Tao trang quan ly admin va vendor với progress 60% va app thuyet minh (Hien tai chi dang duoc su dung boi may ao Android ) da tao UI/UX cua welcomePage va setting với progress 70%. Ke tiep se fix lai loi cua audio demo trang setting va lam phan mainPage app
- Latest commit cua file: 2026-04-12 | 25a072f | Enhance vendor profile management and localization support
- Branches co chua latest commit: app_final, app_fix, appQR_deployApk, develop, fix_meanAudio, fix_tourApp, fix_userRoute, origin, origin/app_final, origin/app_fix, origin/appQR_deployApk, origin/delete_feature_no_needed, origin/delete_POI, origin/design_popup, origin/develop, origin/fix_duplicated_adding_user, origin/fix_logicial_problem, origin/fix_meanAudio, origin/fix_tourApp, origin/fix_userRoute, origin/main, origin/qr_tour, origin/registerVip, origin/releaseApp, origin/submision_POI, origin/userAnalytics, qr_tour, registerVip, releaseApp, sub_Develop, testFeature, userAnalytics
- Trang thai: NO_CHANGE_SINCE_INTRO

### F17 - Vendor tao/sua POI gui duyet
- Nhom: PRD
- File: [API/StreetFoodNarrator.API/Controllers/POIsController.cs](API/StreetFoodNarrator.API/Controllers/POIsController.cs#L516)
- Marker: pending
- First commit theo chuc nang: 2026-02-15 | 194880c | Tao trang quan ly admin va vendor với progress 60% va app thuyet minh (Hien tai chi dang duoc su dung boi may ao Android ) da tao UI/UX cua welcomePage va setting với progress 70%. Ke tiep se fix lai loi cua audio demo trang setting va lam phan mainPage app
- Latest commit theo chuc nang: 2026-04-08 | beb4f40 | Refactor code structure and remove redundant code blocks for improved readability and maintainability
- First commit cua file: 2026-02-15 | 194880c | Tao trang quan ly admin va vendor với progress 60% va app thuyet minh (Hien tai chi dang duoc su dung boi may ao Android ) da tao UI/UX cua welcomePage va setting với progress 70%. Ke tiep se fix lai loi cua audio demo trang setting va lam phan mainPage app
- Latest commit cua file: 2026-04-12 | 2898857 | logical audio gen
- Branches co chua latest commit: app_final, app_fix, appQR_deployApk, develop, fix_meanAudio, fix_tourApp, fix_userRoute, origin, origin/app_final, origin/app_fix, origin/appQR_deployApk, origin/delete_feature_admin, origin/delete_feature_no_needed, origin/delete_POI, origin/design_popup, origin/develop, origin/fix_duplicated_adding_user, origin/fix_logicial_problem, origin/fix_meanAudio, origin/fix_tourApp, origin/fix_userRoute, origin/main, origin/qr_tour, origin/registerVip, origin/releaseApp, origin/submision_POI, origin/test_nha, origin/userAnalytics, qr_tour, registerVip, releaseApp, sub_Develop, test_nha, testFeature, userAnalytics
- Trang thai: UPDATED

### F20 - Quan ly tour + rule free/premium
- Nhom: PRD
- File: [API/StreetFoodNarrator.API/Controllers/ToursController.cs](API/StreetFoodNarrator.API/Controllers/ToursController.cs#L110)
- Marker: free
- First commit theo chuc nang: 2026-04-10 | 659d910 | feat: enhance VIP subscription features and UI updates across the app
- Latest commit theo chuc nang: 2026-04-10 | 659d910 | feat: enhance VIP subscription features and UI updates across the app
- First commit cua file: 2026-03-10 | e264937 | feat: Implement POI Detail Page with map integration and sharing features
- Latest commit cua file: 2026-04-10 | 659d910 | feat: enhance VIP subscription features and UI updates across the app
- Branches co chua latest commit: app_fix, appQR_deployApk, develop, origin, origin/app_fix, origin/appQR_deployApk, origin/delete_feature_no_needed, origin/develop, origin/fix_duplicated_adding_user, origin/fix_logicial_problem, origin/main, origin/registerVip, origin/releaseApp, origin/userAnalytics, registerVip, releaseApp, sub_Develop, testFeature, userAnalytics
- Trang thai: NO_CHANGE_SINCE_INTRO

### F24 - Xem va tham gia tour tren mobile
- Nhom: PRD
- File: [MobileApp/StreetFoodNarrator.App/Views/MainPage.Tour.cs](MobileApp/StreetFoodNarrator.App/Views/MainPage.Tour.cs#L49)
- Marker: VirtualTour
- First commit theo chuc nang: 2026-03-25 | d9c932c | Add MainPage.Tour and MainPage.VirtualTour for enhanced tour functionalities
- Latest commit theo chuc nang: 2026-04-04 | 1fc8784 | feat: Add TourDetailPopupPage for displaying tour details and interactions
- First commit cua file: 2026-03-25 | d9c932c | Add MainPage.Tour and MainPage.VirtualTour for enhanced tour functionalities
- Latest commit cua file: 2026-04-10 | 9b5e8e3 | Đăng ký vip cho app và cho phép tour có free hay không
- Branches co chua latest commit: app_final, app_fix, app_ver2.5, appQR_deployApk, develop, fix_meanAudio, fix_tourApp, fix_userRoute, fix_Web, origin, origin/app_final, origin/app_fix, origin/app_ver2.5, origin/appQR_deployApk, origin/change_user, origin/conflig_resolve, origin/delete_feature_admin, origin/delete_feature_no_needed, origin/delete_POI, origin/design_popup, origin/develop, origin/feature_approved, origin/final1, origin/fix_audio, origin/fix_audio_gen, origin/fix_duplicated_adding_user, origin/fix_history, origin/fix_logicial_problem, origin/fix_meanAudio, origin/fix_side_bar, origin/fix_tourApp, origin/fix_userRoute, origin/fix_Web, origin/main, origin/qr_tour, origin/redesign_fe, origin/registerVip, origin/releaseApp, origin/submision_POI, origin/test_nha, origin/test1, origin/userAnalytics, qr_tour, registerVip, releaseApp, sub_Develop, test_nha, testFeature, userAnalytics
- Trang thai: UPDATED

### F27 - Vendor thanh toan premium, kich hoat ngay (khong can admin duyet)
- Nhom: PRD
- File: [API/StreetFoodNarrator.API/Controllers/PaymentsController.cs](API/StreetFoodNarrator.API/Controllers/PaymentsController.cs#L29)
- Marker: simulate-premium
- First commit theo chuc nang: 2026-04-10 | 3d740ed | submision billing feature
- Latest commit theo chuc nang: 2026-04-10 | 6d9d415 | submision billing feature
- First commit cua file: 2026-04-10 | 3d740ed | submision billing feature
- Latest commit cua file: 2026-04-12 | dc974a7 | review submision feature
- Branches co chua latest commit: app_fix, appQR_deployApk, develop, origin, origin/app_fix, origin/appQR_deployApk, origin/delete_feature_no_needed, origin/develop, origin/fix_duplicated_adding_user, origin/fix_logicial_problem, origin/main, origin/registerVip, origin/releaseApp, origin/submision_POI, origin/userAnalytics, registerVip, releaseApp, sub_Develop, testFeature, userAnalytics
- Trang thai: UPDATED

### F31 - Landing cong khai tai APK
- Nhom: PRD
- File: [API/StreetFoodNarrator.API/wwwroot/apk-download.html](API/StreetFoodNarrator.API/wwwroot/apk-download.html#L6)
- Marker: apk
- First commit theo chuc nang: 2026-04-11 | 31f9cef | Update QR for app and release apk
- Latest commit theo chuc nang: 2026-04-11 | 31f9cef | Update QR for app and release apk
- First commit cua file: 2026-04-11 | 31f9cef | Update QR for app and release apk
- Latest commit cua file: 2026-04-11 | 31f9cef | Update QR for app and release apk
- Branches co chua latest commit: app_fix, appQR_deployApk, develop, origin, origin/app_fix, origin/appQR_deployApk, origin/delete_feature_no_needed, origin/develop, origin/fix_duplicated_adding_user, origin/fix_logicial_problem, origin/main, origin/releaseApp, origin/userAnalytics, registerVip, releaseApp, sub_Develop, testFeature, userAnalytics
- Trang thai: NO_CHANGE_SINCE_INTRO

### F32 - Onboarding first-run
- Nhom: PRD
- File: [MobileApp/StreetFoodNarrator.App/Views/WelcomePage.xaml.cs](MobileApp/StreetFoodNarrator.App/Views/WelcomePage.xaml.cs#L1675)
- Marker: first-run
- First commit theo chuc nang: 2026-04-04 | 1fc8784 | feat: Add TourDetailPopupPage for displaying tour details and interactions
- Latest commit theo chuc nang: 2026-04-04 | 1fc8784 | feat: Add TourDetailPopupPage for displaying tour details and interactions
- First commit cua file: 2026-02-15 | 194880c | Tao trang quan ly admin va vendor với progress 60% va app thuyet minh (Hien tai chi dang duoc su dung boi may ao Android ) da tao UI/UX cua welcomePage va setting với progress 70%. Ke tiep se fix lai loi cua audio demo trang setting va lam phan mainPage app
- Latest commit cua file: 2026-04-12 | 18ee810 | fix onboarding when first time come to app
- Branches co chua latest commit: app_final, app_fix, app_ver2.5, appQR_deployApk, develop, fix_meanAudio, fix_tourApp, fix_userRoute, fix_Web, origin, origin/app_final, origin/app_fix, origin/app_ver2.5, origin/appQR_deployApk, origin/change_user, origin/conflig_resolve, origin/delete_feature_admin, origin/delete_feature_no_needed, origin/delete_POI, origin/design_popup, origin/develop, origin/feature_approved, origin/final1, origin/fix_audio, origin/fix_audio_gen, origin/fix_duplicated_adding_user, origin/fix_history, origin/fix_logicial_problem, origin/fix_meanAudio, origin/fix_side_bar, origin/fix_tourApp, origin/fix_userRoute, origin/fix_Web, origin/main, origin/qr_tour, origin/redesign_fe, origin/registerVip, origin/releaseApp, origin/submision_POI, origin/test_nha, origin/test1, origin/userAnalytics, qr_tour, registerVip, releaseApp, sub_Develop, test_nha, testFeature, userAnalytics
- Trang thai: NO_CHANGE_SINCE_INTRO

### F33 - Offline 2 pha (essential + deferred)
- Nhom: PRD
- File: [MobileApp/StreetFoodNarrator.App/Views/WelcomePage.xaml.cs](MobileApp/StreetFoodNarrator.App/Views/WelcomePage.xaml.cs#L98)
- Marker: RunSimpleFlowAsync
- First commit theo chuc nang: 2026-02-15 | 194880c | Tao trang quan ly admin va vendor với progress 60% va app thuyet minh (Hien tai chi dang duoc su dung boi may ao Android ) da tao UI/UX cua welcomePage va setting với progress 70%. Ke tiep se fix lai loi cua audio demo trang setting va lam phan mainPage app
- Latest commit theo chuc nang: 2026-02-15 | 194880c | Tao trang quan ly admin va vendor với progress 60% va app thuyet minh (Hien tai chi dang duoc su dung boi may ao Android ) da tao UI/UX cua welcomePage va setting với progress 70%. Ke tiep se fix lai loi cua audio demo trang setting va lam phan mainPage app
- First commit cua file: 2026-02-15 | 194880c | Tao trang quan ly admin va vendor với progress 60% va app thuyet minh (Hien tai chi dang duoc su dung boi may ao Android ) da tao UI/UX cua welcomePage va setting với progress 70%. Ke tiep se fix lai loi cua audio demo trang setting va lam phan mainPage app
- Latest commit cua file: 2026-04-12 | 18ee810 | fix onboarding when first time come to app
- Branches co chua latest commit: app_final, app_fix, app_restyle_v1.0, app_ver1, app_ver2.0, app_ver2.5, appQR_deployApk, develop, feature, fix_meanAudio, fix_tourApp, fix_userRoute, fix_Web, multi_update, origin, origin/add_readme_PRD, origin/app_final, origin/app_fix, origin/app_restyle_v1.0, origin/app_ver1, origin/app_ver2.0, origin/app_ver2.5, origin/appQR_deployApk, origin/change_flow_web, origin/change_ml, origin/change_user, origin/conflig_resolve, origin/delete_feature_admin, origin/delete_feature_no_needed, origin/delete_POI, origin/design_popup, origin/develop, origin/feature, origin/feature_approved, origin/final1, origin/fix_audio, origin/fix_audio_gen, origin/fix_duplicated_adding_user, origin/fix_history, origin/fix_logicial_problem, origin/fix_meanAudio, origin/fix_side_bar, origin/fix_style, origin/fix_tourApp, origin/fix_userRoute, origin/fix_Web, origin/hidden_url, origin/main, origin/modify_fe, origin/multi_update, origin/qr_tour, origin/redesign_fe, origin/registerVip, origin/releaseApp, origin/submision_POI, origin/test_nha, origin/test1, origin/userAnalytics, origin/web_ver1.1, qr_tour, registerVip, releaseApp, sub_Develop, test_nha, testFeature, userAnalytics, web_ver1.1
- Trang thai: NO_CHANGE_SINCE_INTRO

### F34 - Map fallback khi offline khong cache
- Nhom: PRD
- File: [MobileApp/StreetFoodNarrator.App/Views/MainPage.Map.cs](MobileApp/StreetFoodNarrator.App/Views/MainPage.Map.cs#L89)
- Marker: CreateOfflineFallbackLayer
- First commit theo chuc nang: 2026-03-30 | ee37f4f | feat(routing): Implement offline routing service with Itinero integration
- Latest commit theo chuc nang: 2026-03-30 | ee37f4f | feat(routing): Implement offline routing service with Itinero integration
- First commit cua file: 2026-03-10 | e264937 | feat: Implement POI Detail Page with map integration and sharing features
- Latest commit cua file: 2026-04-08 | 16baab9 | feat: enhance audio services and UI for offline functionality, update language support, and improve map handling
- Branches co chua latest commit: app_final, app_fix, app_ver2.5, appQR_deployApk, develop, fix_meanAudio, fix_tourApp, fix_userRoute, fix_Web, origin, origin/app_final, origin/app_fix, origin/app_ver2.5, origin/appQR_deployApk, origin/change_flow_web, origin/change_ml, origin/change_user, origin/conflig_resolve, origin/delete_feature_admin, origin/delete_feature_no_needed, origin/delete_POI, origin/design_popup, origin/develop, origin/feature_approved, origin/final1, origin/fix_audio, origin/fix_audio_gen, origin/fix_duplicated_adding_user, origin/fix_history, origin/fix_logicial_problem, origin/fix_meanAudio, origin/fix_side_bar, origin/fix_style, origin/fix_tourApp, origin/fix_userRoute, origin/fix_Web, origin/main, origin/qr_tour, origin/redesign_fe, origin/registerVip, origin/releaseApp, origin/submision_POI, origin/test_nha, origin/test1, origin/userAnalytics, qr_tour, registerVip, releaseApp, sub_Develop, test_nha, testFeature, userAnalytics
- Trang thai: NO_CHANGE_SINCE_INTRO

### F12-SUP - Parser deeplink mobile
- Nhom: Support
- File: [MobileApp/StreetFoodNarrator.App/Core/Utils/QrDeepLinkManager.cs](MobileApp/StreetFoodNarrator.App/Core/Utils/QrDeepLinkManager.cs#L6)
- Marker: deeplink
- First commit theo chuc nang: 2026-04-04 | 1fc8784 | feat: Add TourDetailPopupPage for displaying tour details and interactions
- Latest commit theo chuc nang: 2026-04-04 | 1fc8784 | feat: Add TourDetailPopupPage for displaying tour details and interactions
- First commit cua file: 2026-04-04 | 1fc8784 | feat: Add TourDetailPopupPage for displaying tour details and interactions
- Latest commit cua file: 2026-04-04 | 1fc8784 | feat: Add TourDetailPopupPage for displaying tour details and interactions
- Branches co chua latest commit: app_final, app_fix, app_ver2.5, appQR_deployApk, develop, fix_meanAudio, fix_tourApp, fix_userRoute, fix_Web, origin, origin/app_final, origin/app_fix, origin/app_ver2.5, origin/appQR_deployApk, origin/change_user, origin/conflig_resolve, origin/delete_feature_admin, origin/delete_feature_no_needed, origin/delete_POI, origin/design_popup, origin/develop, origin/feature_approved, origin/final1, origin/fix_audio, origin/fix_audio_gen, origin/fix_duplicated_adding_user, origin/fix_history, origin/fix_logicial_problem, origin/fix_meanAudio, origin/fix_side_bar, origin/fix_tourApp, origin/fix_userRoute, origin/fix_Web, origin/main, origin/qr_tour, origin/redesign_fe, origin/registerVip, origin/releaseApp, origin/submision_POI, origin/test_nha, origin/test1, origin/userAnalytics, qr_tour, registerVip, releaseApp, sub_Develop, test_nha, testFeature, userAnalytics
- Trang thai: NO_CHANGE_SINCE_INTRO

### F24-SUP - Virtual tour view model
- Nhom: Support
- File: [MobileApp/StreetFoodNarrator.App/ViewModels/VirtualTourViewModel.cs](MobileApp/StreetFoodNarrator.App/ViewModels/VirtualTourViewModel.cs#L6)
- Marker: virtual
- First commit theo chuc nang: 2026-03-25 | d9c932c | Add MainPage.Tour and MainPage.VirtualTour for enhanced tour functionalities
- Latest commit theo chuc nang: 2026-03-25 | d9c932c | Add MainPage.Tour and MainPage.VirtualTour for enhanced tour functionalities
- First commit cua file: 2026-03-25 | d9c932c | Add MainPage.Tour and MainPage.VirtualTour for enhanced tour functionalities
- Latest commit cua file: 2026-03-25 | d9c932c | Add MainPage.Tour and MainPage.VirtualTour for enhanced tour functionalities
- Branches co chua latest commit: app_final, app_fix, app_restyle_v1.0, app_ver2.5, appQR_deployApk, develop, fix_meanAudio, fix_tourApp, fix_userRoute, fix_Web, origin, origin/app_final, origin/app_fix, origin/app_restyle_v1.0, origin/app_ver2.5, origin/appQR_deployApk, origin/change_flow_web, origin/change_ml, origin/change_user, origin/conflig_resolve, origin/delete_feature_admin, origin/delete_feature_no_needed, origin/delete_POI, origin/design_popup, origin/develop, origin/feature_approved, origin/final1, origin/fix_audio, origin/fix_audio_gen, origin/fix_duplicated_adding_user, origin/fix_history, origin/fix_logicial_problem, origin/fix_meanAudio, origin/fix_side_bar, origin/fix_style, origin/fix_tourApp, origin/fix_userRoute, origin/fix_Web, origin/hidden_url, origin/main, origin/qr_tour, origin/redesign_fe, origin/registerVip, origin/releaseApp, origin/submision_POI, origin/test_nha, origin/test1, origin/userAnalytics, qr_tour, registerVip, releaseApp, sub_Develop, test_nha, testFeature, userAnalytics
- Trang thai: NO_CHANGE_SINCE_INTRO

## Auto-update

- File nay duoc cap nhat boi script scripts/update-feature-commit-tracker.ps1.
- De bat tu dong, setup git hooks tai .githooks (pre-commit, post-merge, post-checkout, post-rewrite, post-fetch).
- Script dung git log --all, nen co the nhin thay cap nhat tu moi nhanh local/remote da fetch ve may.

