# changelog

Tai lieu tong hop tu toan bo lich su git den ngay 2026-04-13.

- Tong so commit: 135
- Nguon: git rev-list + git show (toan bo commit tren nhanh hien tai)

- So merge commit: 50
- So regular commit: 85
## tong quan theo giai doan
- 2026-01 -> 2026-02: khoi tao nen tang API + web admin/vendor + app MAUI, thiet lap model du lieu va geofencing co ban.
- 2026-03: day manh tinh nang app (POI detail, map, localization, routing offline), dong bo voi backend va seed database.
- 2026-04: tap trung fix logic, audio workflow, QR tour, VIP/subscription/payment, thong bao, dashboard analytics va release APK.

## tong hop pham vi code (toan bo lich su)
- So lieu ben duoi duoc tinh tren toan bo commit o nhanh hien tai.
- Web-Static: xuat hien trong 104 commit, tong 1129 lan cham file
- API-Controllers: xuat hien trong 67 commit, tong 161 lan cham file
- Other: xuat hien trong 51 commit, tong 119 lan cham file
- Mobile-Views: xuat hien trong 42 commit, tong 588 lan cham file
- Mobile-App: xuat hien trong 41 commit, tong 192 lan cham file
- Mobile-Services: xuat hien trong 36 commit, tong 181 lan cham file
- Mobile-ViewModels: xuat hien trong 35 commit, tong 50 lan cham file
- API-Models: xuat hien trong 31 commit, tong 86 lan cham file
- API-Data: xuat hien trong 26 commit, tong 57 lan cham file
- Mobile-Models: xuat hien trong 23 commit, tong 66 lan cham file
- Mobile-Resources: xuat hien trong 18 commit, tong 114 lan cham file
- Project-Config: xuat hien trong 18 commit, tong 28 lan cham file
- Documentation: xuat hien trong 16 commit, tong 41 lan cham file
- Mobile-Platforms: xuat hien trong 15 commit, tong 35 lan cham file
- API-Services: xuat hien trong 9 commit, tong 11 lan cham file
- Database: xuat hien trong 8 commit, tong 29 lan cham file
- Tools: xuat hien trong 2 commit, tong 4 lan cham file
- Web-Frontend: xuat hien trong 2 commit, tong 34 lan cham file
- Design: xuat hien trong 2 commit, tong 2 lan cham file

## chi tiet tung commit

### 2026-01-19 | b373d4b | Initial commit
- Commit: b373d4bdb6c5f1b85bcf1eb71774a682e7813d4f
- Loai: regular commit
- Thong ke: 1 file changed, 1 insertion(+)
- Vung anh huong: Project-Config: 1
- Dien giai: Dieu chinh cau truc ma nguon va tai nguyen de on dinh he thong.
- File thay doi tieu bieu (1):
  - README.md

### 2026-02-15 | 194880c | Tao trang quan ly admin va vendor với progress 60% va app thuyet minh (Hien tai chi dang duoc su dung boi may ao Android ) da tao UI/UX cua welcomePage va setting với progress 70%. Ke tiep se fix lai loi cua audio demo trang setting va lam phan mainPage app
- Commit: 194880c1733a4080cbe41377325d4354a6dbe6f8
- Loai: regular commit
- Thong ke: 127 files changed, 30255 insertions(+)
- Vung anh huong: Web-Frontend: 18; Mobile-Resources: 17; Mobile-App: 11; Mobile-Models: 11; API-Models: 10; Other: 9; Documentation: 8; API-Controllers: 7; API-Data: 7; Mobile-Views: 6; Mobile-Platforms: 6; Database: 6; Mobile-Services: 6; Project-Config: 3; Design: 1; Mobile-ViewModels: 1
- Dien giai: Cap nhat logic backend: endpoint, service, model hoac du lieu. Dieu chinh giao dien web admin/vendor va script tuong tac. Cap nhat ung dung mobile MAUI: man hinh, state, service va tai nguyen. Tinh chinh schema/seed/du lieu mau phuc vu API va ung dung. Bo sung cap nhat tai lieu ky thuat, huong dan test hoac mo ta thiet ke. Trong tam la sua loi va dong bo hanh vi giua cac thanh phan. Dieu chinh luong audio/TTS va cac man hinh lien quan.
- File thay doi tieu bieu (127):
  - .gitignore
  - .hintrc
  - .vscode/tasks.json
  - API/StreetFoodNarrator.API/Controllers/AudioController.cs
  - API/StreetFoodNarrator.API/Controllers/AuthController.cs
  - API/StreetFoodNarrator.API/Controllers/GeocodeController.cs
  - API/StreetFoodNarrator.API/Controllers/POIsController.cs
  - API/StreetFoodNarrator.API/Controllers/SettingsController.cs
  - API/StreetFoodNarrator.API/Controllers/TTSController.cs
  - API/StreetFoodNarrator.API/Controllers/VendorsController.cs
  - API/StreetFoodNarrator.API/Data/DbInitializer.cs
  - API/StreetFoodNarrator.API/Data/MongoDbContext.cs
  - API/StreetFoodNarrator.API/Data/MongoDbSettings.cs
  - API/StreetFoodNarrator.API/Data/MongoSequenceService.cs
  - API/StreetFoodNarrator.API/Data/PoiSeedReset.cs
  - API/StreetFoodNarrator.API/Data/seed/reset-pois.js
  - API/StreetFoodNarrator.API/Data/seed/vinh-khanh-pois.json
  - API/StreetFoodNarrator.API/Models/ApplicationRole.cs
  - API/StreetFoodNarrator.API/Models/ApplicationUser.cs
  - API/StreetFoodNarrator.API/Models/AudioContent.cs
  - API/StreetFoodNarrator.API/Models/GeoJsonLocation.cs
  - API/StreetFoodNarrator.API/Models/NarrationLog.cs
  - API/StreetFoodNarrator.API/Models/POI.cs
  - API/StreetFoodNarrator.API/Models/POI_Tour.cs
  - API/StreetFoodNarrator.API/Models/Tour.cs
  - ... va 102 file khac

### 2026-02-23 | 5d01ec7 | update readme và xóa 1 số file không cần thiết
- Commit: 5d01ec7be68a9b8ccc7bedbbd5ec924e3519550e
- Loai: regular commit
- Thong ke: 4 files changed, 477 insertions(+), 291 deletions(-)
- Vung anh huong: Documentation: 2; Other: 1; Project-Config: 1
- Dien giai: Bo sung cap nhat tai lieu ky thuat, huong dan test hoac mo ta thiet ke.
- File thay doi tieu bieu (4):
  - Documentation/README.md
  - Documentation/READY-TO-TEST.md
  - README.md
  - tts_wrapper.py

### 2026-03-03 | f81393f | Add Vietnamese language support and new features for Street Food Narrator app
- Commit: f81393ff22c767a303743408f8ddbcfb5114b258
- Loai: regular commit
- Thong ke: 72 files changed, 5971 insertions(+), 2904 deletions(-)
- Vung anh huong: Web-Frontend: 16; Mobile-Views: 10; Mobile-Resources: 9; Mobile-Services: 7; Mobile-App: 5; Other: 4; Mobile-Models: 3; Database: 3; API-Controllers: 3; API-Data: 2; Mobile-Platforms: 2; API-Services: 2; API-Models: 2; Project-Config: 1; Mobile-ViewModels: 1; Web-Static: 1; Documentation: 1
- Dien giai: Cap nhat logic backend: endpoint, service, model hoac du lieu. Dieu chinh giao dien web admin/vendor va script tuong tac. Cap nhat ung dung mobile MAUI: man hinh, state, service va tai nguyen. Tinh chinh schema/seed/du lieu mau phuc vu API va ung dung. Bo sung cap nhat tai lieu ky thuat, huong dan test hoac mo ta thiet ke. Bo sung/tang cap tinh nang moi trong luong su dung chinh. Mo rong da ngon ngu va noi dia hoa noi dung hien thi.
- File thay doi tieu bieu (72):
  - API/StreetFoodNarrator.API/Controllers/AudioController.cs
  - API/StreetFoodNarrator.API/Controllers/POIsController.cs
  - API/StreetFoodNarrator.API/Controllers/TTSController.cs
  - API/StreetFoodNarrator.API/Data/DbInitializer.cs
  - API/StreetFoodNarrator.API/Data/PoiSeedReset.cs
  - API/StreetFoodNarrator.API/Models/POI.cs
  - API/StreetFoodNarrator.API/Models/POIExtensions.cs
  - API/StreetFoodNarrator.API/Program.cs
  - API/StreetFoodNarrator.API/Services/TtsTextPreprocessor.cs
  - API/StreetFoodNarrator.API/Services/TtsVoiceCatalog.cs
  - API/StreetFoodNarrator.API/StreetFoodNarrator.API.csproj
  - API/StreetFoodNarrator.API/appsettings.json
  - API/StreetFoodNarrator.API/wwwroot/frontend/README.md
  - API/StreetFoodNarrator.API/wwwroot/frontend/analytics.html
  - API/StreetFoodNarrator.API/wwwroot/frontend/api.js
  - API/StreetFoodNarrator.API/wwwroot/frontend/audio-bulk-generate.html
  - API/StreetFoodNarrator.API/wwwroot/frontend/audio-list.html
  - API/StreetFoodNarrator.API/wwwroot/frontend/auth-check.js
  - API/StreetFoodNarrator.API/wwwroot/frontend/danh_gia_frontend_vinh_khanh.md
  - API/StreetFoodNarrator.API/wwwroot/frontend/login.html
  - API/StreetFoodNarrator.API/wwwroot/frontend/nav.js
  - API/StreetFoodNarrator.API/wwwroot/frontend/poi-create.html
  - API/StreetFoodNarrator.API/wwwroot/frontend/poi-edit.html
  - API/StreetFoodNarrator.API/wwwroot/frontend/poi-list.html
  - API/StreetFoodNarrator.API/wwwroot/frontend/register.html
  - ... va 47 file khac

### 2026-03-10 | e264937 | feat: Implement POI Detail Page with map integration and sharing features
- Commit: e26493760e8801e7e40796cb3caa1d0dd1f3296f
- Loai: regular commit
- Thong ke: 124 files changed, 19047 insertions(+), 2126 deletions(-)
- Vung anh huong: Web-Static: 48; Mobile-Views: 30; API-Models: 12; Mobile-App: 9; Mobile-Services: 9; API-Controllers: 4; Mobile-Platforms: 3; Other: 2; API-Data: 2; Mobile-Models: 2; Database: 1; Mobile-ViewModels: 1; Mobile-Resources: 1
- Dien giai: Cap nhat logic backend: endpoint, service, model hoac du lieu. Dieu chinh giao dien web admin/vendor va script tuong tac. Cap nhat ung dung mobile MAUI: man hinh, state, service va tai nguyen. Tinh chinh schema/seed/du lieu mau phuc vu API va ung dung. Bo sung/tang cap tinh nang moi trong luong su dung chinh. Tac dong truc tiep den cac luong nghiep vu tour/POI/thanh toan/goi VIP.
- File thay doi tieu bieu (124):
  - .hintrc
  - API/StreetFoodNarrator.API/Controllers/AnalyticsController.cs
  - API/StreetFoodNarrator.API/Controllers/SettingsController.cs
  - API/StreetFoodNarrator.API/Controllers/ToursController.cs
  - API/StreetFoodNarrator.API/Controllers/TranslationsController.cs
  - API/StreetFoodNarrator.API/Data/DbInitializer.cs
  - API/StreetFoodNarrator.API/Data/MongoDbContext.cs
  - API/StreetFoodNarrator.API/Models/ApplicationUser.cs
  - API/StreetFoodNarrator.API/Models/AudioContent.cs
  - API/StreetFoodNarrator.API/Models/DeviceHistory.cs
  - API/StreetFoodNarrator.API/Models/DeviceInfo.cs
  - API/StreetFoodNarrator.API/Models/MenuItem.cs
  - API/StreetFoodNarrator.API/Models/POI.cs
  - API/StreetFoodNarrator.API/Models/PoiTranslation.cs
  - API/StreetFoodNarrator.API/Models/Tour.cs
  - API/StreetFoodNarrator.API/Models/Translation.cs
  - API/StreetFoodNarrator.API/Models/UserSettings.cs
  - API/StreetFoodNarrator.API/Models/VendorProfile.cs
  - API/StreetFoodNarrator.API/Models/Zone.cs
  - API/StreetFoodNarrator.API/Properties/launchSettings.json
  - API/StreetFoodNarrator.API/wwwroot/README.md
  - API/StreetFoodNarrator.API/wwwroot/README_NEW_FRONTEND.md
  - API/StreetFoodNarrator.API/wwwroot/api.js
  - API/StreetFoodNarrator.API/wwwroot/audio-bulk-generate.html
  - API/StreetFoodNarrator.API/wwwroot/audio-list.html
  - ... va 99 file khac

### 2026-03-12 | 7c65c89 | Add MongoDB integration and seed script for database population
- Commit: 7c65c895494de777130002893ed7c1ce0a277985
- Loai: regular commit
- Thong ke: 33 files changed, 4533 insertions(+), 1391 deletions(-)
- Vung anh huong: Web-Static: 13; Mobile-Views: 4; API-Controllers: 4; Mobile-Services: 3; Database: 3; Mobile-Resources: 2; API-Data: 2; API-Models: 1; Mobile-App: 1
- Dien giai: Cap nhat logic backend: endpoint, service, model hoac du lieu. Dieu chinh giao dien web admin/vendor va script tuong tac. Cap nhat ung dung mobile MAUI: man hinh, state, service va tai nguyen. Tinh chinh schema/seed/du lieu mau phuc vu API va ung dung. Bo sung/tang cap tinh nang moi trong luong su dung chinh.
- File thay doi tieu bieu (33):
  - API/StreetFoodNarrator.API/Controllers/AudioController.cs
  - API/StreetFoodNarrator.API/Controllers/POIsController.cs
  - API/StreetFoodNarrator.API/Controllers/TTSController.cs
  - API/StreetFoodNarrator.API/Controllers/VendorsController.cs
  - API/StreetFoodNarrator.API/Data/DbInitializer.cs
  - API/StreetFoodNarrator.API/Data/seed/vinh-khanh-pois.json
  - API/StreetFoodNarrator.API/Models/POI.cs
  - API/StreetFoodNarrator.API/wwwroot/api.js
  - API/StreetFoodNarrator.API/wwwroot/audio-bulk-generate.html
  - API/StreetFoodNarrator.API/wwwroot/audio-list.html
  - API/StreetFoodNarrator.API/wwwroot/dashboard.html
  - API/StreetFoodNarrator.API/wwwroot/history.html
  - API/StreetFoodNarrator.API/wwwroot/js/audio.js
  - API/StreetFoodNarrator.API/wwwroot/poi-create.html
  - API/StreetFoodNarrator.API/wwwroot/poi-edit.html
  - API/StreetFoodNarrator.API/wwwroot/sidebar.js
  - API/StreetFoodNarrator.API/wwwroot/tour.html
  - API/StreetFoodNarrator.API/wwwroot/translation.html
  - API/StreetFoodNarrator.API/wwwroot/users.html
  - API/StreetFoodNarrator.API/wwwroot/vendors-list.html
  - Database/package-lock.json
  - Database/package.json
  - Database/run-seed.js
  - MobileApp/StreetFoodNarrator.App/AppConfig.cs
  - MobileApp/StreetFoodNarrator.App/Core/Services/IAudioCacheService.cs
  - ... va 8 file khac

### 2026-03-14 | 32c151f | Da sua lai 1 so loi logic cho trang web quan ly cua admin/vendor va bo sung them cac feature moi cho web
- Commit: 32c151fc2944087bfaa90fa46934c28a45fedcd4
- Loai: regular commit
- Thong ke: 89 files changed, 5947 insertions(+), 1493 deletions(-)
- Vung anh huong: Web-Static: 64; API-Controllers: 8; Mobile-Views: 5; Mobile-Services: 4; Other: 2; API-Models: 2; API-Services: 1; Mobile-ViewModels: 1; API-Data: 1; Mobile-Models: 1
- Dien giai: Cap nhat logic backend: endpoint, service, model hoac du lieu. Dieu chinh giao dien web admin/vendor va script tuong tac. Cap nhat ung dung mobile MAUI: man hinh, state, service va tai nguyen. Bo sung/tang cap tinh nang moi trong luong su dung chinh.
- File thay doi tieu bieu (89):
  - API/StreetFoodNarrator.API/Controllers/AudioController.cs
  - API/StreetFoodNarrator.API/Controllers/AutoTranslateController.cs
  - API/StreetFoodNarrator.API/Controllers/GeocodeController.cs
  - API/StreetFoodNarrator.API/Controllers/MenuItemsController.cs
  - API/StreetFoodNarrator.API/Controllers/POIsController.cs
  - API/StreetFoodNarrator.API/Controllers/TTSController.cs
  - API/StreetFoodNarrator.API/Controllers/ToursController.cs
  - API/StreetFoodNarrator.API/Controllers/VendorsController.cs
  - API/StreetFoodNarrator.API/Data/DbInitializer.cs
  - API/StreetFoodNarrator.API/Models/POI.cs
  - API/StreetFoodNarrator.API/Models/Tour.cs
  - API/StreetFoodNarrator.API/Program.cs
  - API/StreetFoodNarrator.API/Services/TtsTextPreprocessor.cs
  - API/StreetFoodNarrator.API/wwwroot/README_NEW_FRONTEND.md
  - API/StreetFoodNarrator.API/wwwroot/api.js
  - API/StreetFoodNarrator.API/wwwroot/audio-bulk-generate.html
  - API/StreetFoodNarrator.API/wwwroot/audio-list.html
  - API/StreetFoodNarrator.API/wwwroot/css/base.css
  - API/StreetFoodNarrator.API/wwwroot/css/components.css
  - API/StreetFoodNarrator.API/wwwroot/css/layout.css
  - API/StreetFoodNarrator.API/wwwroot/danh_gia_frontend_vinh_khanh.md
  - API/StreetFoodNarrator.API/wwwroot/dashboard.html
  - API/StreetFoodNarrator.API/wwwroot/history.html
  - API/StreetFoodNarrator.API/wwwroot/js/audio.js
  - API/StreetFoodNarrator.API/wwwroot/poi-create.html
  - ... va 64 file khac

### 2026-03-16 | 1ce1e0b | da cap nhat giao dien va chuc nang cho app (trang tour, ban do) va them lich su tour
- Commit: 1ce1e0b34ea893dc907d5dad45333beafbc377a3
- Loai: regular commit
- Thong ke: 51 files changed, 2661 insertions(+), 589 deletions(-)
- Vung anh huong: Mobile-Views: 25; Mobile-Services: 8; Web-Static: 5; Mobile-App: 4; API-Controllers: 3; Mobile-Models: 3; Other: 2; Mobile-ViewModels: 1
- Dien giai: Cap nhat logic backend: endpoint, service, model hoac du lieu. Dieu chinh giao dien web admin/vendor va script tuong tac. Cap nhat ung dung mobile MAUI: man hinh, state, service va tai nguyen. Tac dong truc tiep den cac luong nghiep vu tour/POI/thanh toan/goi VIP.
- File thay doi tieu bieu (51):
  - .dotnet/.dotnet/sdk-advertising/10.0.100/microsoft.net.workloads/10.0.105/microsoft.net.workloads.workloadset.json
  - .vscode/settings.json
  - API/StreetFoodNarrator.API/Controllers/AudioController.cs
  - API/StreetFoodNarrator.API/Controllers/MenuItemsController.cs
  - API/StreetFoodNarrator.API/Controllers/POIsController.cs
  - API/StreetFoodNarrator.API/wwwroot/poi-edit.html
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/1883a6da-11d7-4b9c-9974-176f934ca4b0.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/2870fc7f-b408-4213-b88c-71a499254288.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/9d6b58bd-ccc0-4856-9687-b355f8e17b5b.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/dd2dc22d-db32-4fe5-9a50-9f704c795aaf.jpg
  - MobileApp/StreetFoodNarrator.App/App.xaml
  - MobileApp/StreetFoodNarrator.App/Converters/BoolToColorConverter.cs
  - MobileApp/StreetFoodNarrator.App/Converters/StringFallbackConverter.cs
  - MobileApp/StreetFoodNarrator.App/Core/Models/MenuItemDto.cs
  - MobileApp/StreetFoodNarrator.App/Core/Models/POI.cs
  - MobileApp/StreetFoodNarrator.App/Core/Models/UserSession.cs
  - MobileApp/StreetFoodNarrator.App/Core/Services/IGeofenceService.cs
  - MobileApp/StreetFoodNarrator.App/Core/Services/IServices.cs
  - MobileApp/StreetFoodNarrator.App/Core/Services/ITTSService.cs
  - MobileApp/StreetFoodNarrator.App/Core/Services/Implementations/AudioCacheService.cs
  - MobileApp/StreetFoodNarrator.App/Core/Services/Implementations/LocalDatabaseService.cs
  - MobileApp/StreetFoodNarrator.App/Core/Services/Implementations/TextToSpeechService.cs
  - MobileApp/StreetFoodNarrator.App/Core/Services/LocationService.cs
  - MobileApp/StreetFoodNarrator.App/Core/Services/ZoneRepository.cs
  - MobileApp/StreetFoodNarrator.App/StreetFoodNarrator.App.csproj
  - ... va 26 file khac

### 2026-03-16 | aa31312 | Merge pull request #1 from NgNguyenChuong/app_ver2.0
- Commit: aa31312d0b22fd142d4456303f6b54bf22f990f3
- Loai: merge commit
- Thong ke: 266 files changed, 60098 insertions(+), 1 deletion(-)
- Vung anh huong: Web-Static: 97; Mobile-Views: 30; Mobile-Resources: 21; Mobile-App: 17; API-Models: 17; Mobile-Models: 13; API-Controllers: 12; Mobile-Services: 12; Other: 11; Database: 9; Mobile-Platforms: 7; API-Data: 7; Documentation: 5; Project-Config: 4; API-Services: 2; Mobile-ViewModels: 1; Design: 1
- Dien giai: Hop nhat nhanh tinh nang/develop, dong bo code va giai quyet xung dot neu co.
- File thay doi tieu bieu (266):
  - .dotnet/.dotnet/sdk-advertising/10.0.100/microsoft.net.workloads/10.0.105/microsoft.net.workloads.workloadset.json
  - .gitignore
  - .hintrc
  - .vscode/settings.json
  - .vscode/tasks.json
  - API/StreetFoodNarrator.API/Controllers/AnalyticsController.cs
  - API/StreetFoodNarrator.API/Controllers/AudioController.cs
  - API/StreetFoodNarrator.API/Controllers/AuthController.cs
  - API/StreetFoodNarrator.API/Controllers/AutoTranslateController.cs
  - API/StreetFoodNarrator.API/Controllers/GeocodeController.cs
  - API/StreetFoodNarrator.API/Controllers/MenuItemsController.cs
  - API/StreetFoodNarrator.API/Controllers/POIsController.cs
  - API/StreetFoodNarrator.API/Controllers/SettingsController.cs
  - API/StreetFoodNarrator.API/Controllers/TTSController.cs
  - API/StreetFoodNarrator.API/Controllers/ToursController.cs
  - API/StreetFoodNarrator.API/Controllers/TranslationsController.cs
  - API/StreetFoodNarrator.API/Controllers/VendorsController.cs
  - API/StreetFoodNarrator.API/Data/DbInitializer.cs
  - API/StreetFoodNarrator.API/Data/MongoDbContext.cs
  - API/StreetFoodNarrator.API/Data/MongoDbSettings.cs
  - API/StreetFoodNarrator.API/Data/MongoSequenceService.cs
  - API/StreetFoodNarrator.API/Data/PoiSeedReset.cs
  - API/StreetFoodNarrator.API/Data/seed/reset-pois.js
  - API/StreetFoodNarrator.API/Data/seed/vinh-khanh-pois.json
  - API/StreetFoodNarrator.API/Models/ApplicationRole.cs
  - ... va 241 file khac

### 2026-03-16 | b8d7741 | check
- Commit: b8d7741e82db7a6255408a28f81dcc54407e704e
- Loai: regular commit
- Thong ke: 1 file changed, 126 insertions(+)
- Vung anh huong: Project-Config: 1
- Dien giai: Dieu chinh cau truc ma nguon va tai nguyen de on dinh he thong.
- File thay doi tieu bieu (1):
  - README.md

### 2026-03-16 | d482f35 | llm prd
- Commit: d482f3541b61c0feac4bbf036cf2e0f63efbf2dc
- Loai: regular commit
- Thong ke: 2 files changed, 1078 insertions(+)
- Vung anh huong: Project-Config: 2
- Dien giai: Dieu chinh cau truc ma nguon va tai nguyen de on dinh he thong.
- File thay doi tieu bieu (2):
  - PRD-StreetFoodNarrator.docx
  - PRD-StreetFoodNarrator.md

### 2026-03-16 | 8ba69f5 | Merge pull request #2 from NgNguyenChuong/add_readme_PRD
- Commit: 8ba69f56eb305e329524b522ab896d6be60e8d8f
- Loai: merge commit
- Thong ke: 2 files changed, 1078 insertions(+)
- Vung anh huong: Project-Config: 2
- Dien giai: Hop nhat nhanh tinh nang/develop, dong bo code va giai quyet xung dot neu co.
- File thay doi tieu bieu (2):
  - PRD-StreetFoodNarrator.docx
  - PRD-StreetFoodNarrator.md

### 2026-03-25 | d9c932c | Add MainPage.Tour and MainPage.VirtualTour for enhanced tour functionalities
- Commit: d9c932ce03a20a017a708886a05a3815cca8cacd
- Loai: regular commit
- Thong ke: 78 files changed, 11702 insertions(+), 2523 deletions(-)
- Vung anh huong: Mobile-Views: 30; Mobile-Services: 15; Mobile-App: 14; Mobile-Models: 4; Other: 4; Mobile-ViewModels: 2; Mobile-Resources: 2; API-Models: 2; API-Data: 2; API-Controllers: 1; Web-Static: 1; Mobile-Platforms: 1
- Dien giai: Cap nhat logic backend: endpoint, service, model hoac du lieu. Dieu chinh giao dien web admin/vendor va script tuong tac. Cap nhat ung dung mobile MAUI: man hinh, state, service va tai nguyen. Bo sung/tang cap tinh nang moi trong luong su dung chinh. Tac dong truc tiep den cac luong nghiep vu tour/POI/thanh toan/goi VIP.
- File thay doi tieu bieu (78):
  - .dotnet/.dotnet/sdk-advertising/10.0.100/microsoft.net.workloads/10.0.105/microsoft.net.workloads.workloadset.json
  - .gitignore
  - API/StreetFoodNarrator.API/Controllers/ReviewsController.cs
  - API/StreetFoodNarrator.API/Data/MongoDbContext.cs
  - API/StreetFoodNarrator.API/Data/PoiSeedReset.cs
  - API/StreetFoodNarrator.API/Models/POI.cs
  - API/StreetFoodNarrator.API/Models/Review.cs
  - API/StreetFoodNarrator.API/Program.cs
  - API/StreetFoodNarrator.API/Properties/launchSettings.json
  - API/StreetFoodNarrator.API/wwwroot/audio-bulk-generate.html
  - MobileApp/StreetFoodNarrator.App/App.xaml
  - MobileApp/StreetFoodNarrator.App/App.xaml.cs
  - MobileApp/StreetFoodNarrator.App/AppConfig.cs
  - MobileApp/StreetFoodNarrator.App/AppShell.xaml
  - MobileApp/StreetFoodNarrator.App/AppShell.xaml.cs
  - MobileApp/StreetFoodNarrator.App/Converters/CategoryTextColorConverter.cs
  - MobileApp/StreetFoodNarrator.App/Converters/DownloadedToColorConverter.cs
  - MobileApp/StreetFoodNarrator.App/Converters/InvertedBoolConverter.cs
  - MobileApp/StreetFoodNarrator.App/Converters/RatingToColorConverter.cs
  - MobileApp/StreetFoodNarrator.App/Converters/StringNotEmptyConverter.cs
  - MobileApp/StreetFoodNarrator.App/Core/Models/MenuItemDto.cs
  - MobileApp/StreetFoodNarrator.App/Core/Models/POI.cs
  - MobileApp/StreetFoodNarrator.App/Core/Models/Review.cs
  - MobileApp/StreetFoodNarrator.App/Core/Models/VoicePackage.cs
  - MobileApp/StreetFoodNarrator.App/Core/Services/IGeofenceService.cs
  - ... va 53 file khac

### 2026-03-25 | 48a5fb4 | modify setdev fnct
- Commit: 48a5fb4ef378889afb12209b6c1f58d1f870b92f
- Loai: regular commit
- Thong ke: 1 file changed, 6 insertions(+)
- Vung anh huong: Web-Static: 1
- Dien giai: Dieu chinh giao dien web admin/vendor va script tuong tac.
- File thay doi tieu bieu (1):
  - API/StreetFoodNarrator.API/wwwroot/history.html

### 2026-03-25 | 279ce6d | Merge pull request #3 from NgNguyenChuong/modify_fe
- Commit: 279ce6dd284337fe7a75d50c48c165dc81da4fec
- Loai: merge commit
- Thong ke: 1 file changed, 6 insertions(+)
- Vung anh huong: Web-Static: 1
- Dien giai: Hop nhat nhanh tinh nang/develop, dong bo code va giai quyet xung dot neu co.
- File thay doi tieu bieu (1):
  - API/StreetFoodNarrator.API/wwwroot/history.html

### 2026-03-27 | 58a4b67 | hidden_url
- Commit: 58a4b67adf68ec2a02f39b270cbd40e8026b1628
- Loai: regular commit
- Thong ke: 28 files changed, 325 insertions(+), 112 deletions(-)
- Vung anh huong: Web-Static: 25; Other: 3
- Dien giai: Dieu chinh giao dien web admin/vendor va script tuong tac.
- File thay doi tieu bieu (28):
  - API/StreetFoodNarrator.API/Program.cs
  - API/StreetFoodNarrator.API/wwwroot/api.js
  - API/StreetFoodNarrator.API/wwwroot/audio-bulk-generate.html
  - API/StreetFoodNarrator.API/wwwroot/audio-list.html
  - API/StreetFoodNarrator.API/wwwroot/auth-check.js
  - API/StreetFoodNarrator.API/wwwroot/dashboard.html
  - API/StreetFoodNarrator.API/wwwroot/frontend_backup/nav.js
  - API/StreetFoodNarrator.API/wwwroot/frontend_backup/poi-create.html
  - API/StreetFoodNarrator.API/wwwroot/frontend_backup/sidebar.html
  - API/StreetFoodNarrator.API/wwwroot/frontend_backup/sidebar.js
  - API/StreetFoodNarrator.API/wwwroot/history.html
  - API/StreetFoodNarrator.API/wwwroot/index.html
  - API/StreetFoodNarrator.API/wwwroot/js/auth.js
  - API/StreetFoodNarrator.API/wwwroot/nav.js
  - API/StreetFoodNarrator.API/wwwroot/poi-create.html
  - API/StreetFoodNarrator.API/wwwroot/poi-detail.html
  - API/StreetFoodNarrator.API/wwwroot/poi-edit.html
  - API/StreetFoodNarrator.API/wwwroot/poi-list.html
  - API/StreetFoodNarrator.API/wwwroot/register.html
  - API/StreetFoodNarrator.API/wwwroot/sidebar.js
  - API/StreetFoodNarrator.API/wwwroot/tour.html
  - API/StreetFoodNarrator.API/wwwroot/translation.html
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/41c45f1f-04bd-4f8a-ae9b-5e96bb2e992c.png
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/97f2a026-bb5b-4a78-809d-11843db31fc3.png
  - API/StreetFoodNarrator.API/wwwroot/users.html
  - ... va 3 file khac

### 2026-03-27 | ec638ee | Merge pull request #4 from NgNguyenChuong/app_restyle_v1.0
- Commit: ec638ee7e7ce1e8fc64b880d474c2976cbf366d5
- Loai: merge commit
- Thong ke: 78 files changed, 11702 insertions(+), 2523 deletions(-)
- Vung anh huong: Mobile-Views: 30; Mobile-Services: 15; Mobile-App: 14; Mobile-Models: 4; Other: 4; Mobile-ViewModels: 2; Mobile-Resources: 2; API-Models: 2; API-Data: 2; API-Controllers: 1; Web-Static: 1; Mobile-Platforms: 1
- Dien giai: Hop nhat nhanh tinh nang/develop, dong bo code va giai quyet xung dot neu co.
- File thay doi tieu bieu (78):
  - .dotnet/.dotnet/sdk-advertising/10.0.100/microsoft.net.workloads/10.0.105/microsoft.net.workloads.workloadset.json
  - .gitignore
  - API/StreetFoodNarrator.API/Controllers/ReviewsController.cs
  - API/StreetFoodNarrator.API/Data/MongoDbContext.cs
  - API/StreetFoodNarrator.API/Data/PoiSeedReset.cs
  - API/StreetFoodNarrator.API/Models/POI.cs
  - API/StreetFoodNarrator.API/Models/Review.cs
  - API/StreetFoodNarrator.API/Program.cs
  - API/StreetFoodNarrator.API/Properties/launchSettings.json
  - API/StreetFoodNarrator.API/wwwroot/audio-bulk-generate.html
  - MobileApp/StreetFoodNarrator.App/App.xaml
  - MobileApp/StreetFoodNarrator.App/App.xaml.cs
  - MobileApp/StreetFoodNarrator.App/AppConfig.cs
  - MobileApp/StreetFoodNarrator.App/AppShell.xaml
  - MobileApp/StreetFoodNarrator.App/AppShell.xaml.cs
  - MobileApp/StreetFoodNarrator.App/Converters/CategoryTextColorConverter.cs
  - MobileApp/StreetFoodNarrator.App/Converters/DownloadedToColorConverter.cs
  - MobileApp/StreetFoodNarrator.App/Converters/InvertedBoolConverter.cs
  - MobileApp/StreetFoodNarrator.App/Converters/RatingToColorConverter.cs
  - MobileApp/StreetFoodNarrator.App/Converters/StringNotEmptyConverter.cs
  - MobileApp/StreetFoodNarrator.App/Core/Models/MenuItemDto.cs
  - MobileApp/StreetFoodNarrator.App/Core/Models/POI.cs
  - MobileApp/StreetFoodNarrator.App/Core/Models/Review.cs
  - MobileApp/StreetFoodNarrator.App/Core/Models/VoicePackage.cs
  - MobileApp/StreetFoodNarrator.App/Core/Services/IGeofenceService.cs
  - ... va 53 file khac

### 2026-03-27 | b84a0f0 | Merge branch 'develop' into hidden_url
- Commit: b84a0f07c04dc38d50be344521088c736c3009e1
- Loai: merge commit
- Thong ke: 78 files changed, 11701 insertions(+), 2530 deletions(-)
- Vung anh huong: Mobile-Views: 30; Mobile-Services: 15; Mobile-App: 14; Mobile-Models: 4; Other: 4; Mobile-ViewModels: 2; Mobile-Resources: 2; API-Models: 2; API-Data: 2; API-Controllers: 1; Web-Static: 1; Mobile-Platforms: 1
- Dien giai: Hop nhat nhanh tinh nang/develop, dong bo code va giai quyet xung dot neu co.
- File thay doi tieu bieu (78):
  - .dotnet/.dotnet/sdk-advertising/10.0.100/microsoft.net.workloads/10.0.105/microsoft.net.workloads.workloadset.json
  - .gitignore
  - API/StreetFoodNarrator.API/Controllers/ReviewsController.cs
  - API/StreetFoodNarrator.API/Data/MongoDbContext.cs
  - API/StreetFoodNarrator.API/Data/PoiSeedReset.cs
  - API/StreetFoodNarrator.API/Models/POI.cs
  - API/StreetFoodNarrator.API/Models/Review.cs
  - API/StreetFoodNarrator.API/Program.cs
  - API/StreetFoodNarrator.API/Properties/launchSettings.json
  - API/StreetFoodNarrator.API/wwwroot/audio-bulk-generate.html
  - MobileApp/StreetFoodNarrator.App/App.xaml
  - MobileApp/StreetFoodNarrator.App/App.xaml.cs
  - MobileApp/StreetFoodNarrator.App/AppConfig.cs
  - MobileApp/StreetFoodNarrator.App/AppShell.xaml
  - MobileApp/StreetFoodNarrator.App/AppShell.xaml.cs
  - MobileApp/StreetFoodNarrator.App/Converters/CategoryTextColorConverter.cs
  - MobileApp/StreetFoodNarrator.App/Converters/DownloadedToColorConverter.cs
  - MobileApp/StreetFoodNarrator.App/Converters/InvertedBoolConverter.cs
  - MobileApp/StreetFoodNarrator.App/Converters/RatingToColorConverter.cs
  - MobileApp/StreetFoodNarrator.App/Converters/StringNotEmptyConverter.cs
  - MobileApp/StreetFoodNarrator.App/Core/Models/MenuItemDto.cs
  - MobileApp/StreetFoodNarrator.App/Core/Models/POI.cs
  - MobileApp/StreetFoodNarrator.App/Core/Models/Review.cs
  - MobileApp/StreetFoodNarrator.App/Core/Models/VoicePackage.cs
  - MobileApp/StreetFoodNarrator.App/Core/Services/IGeofenceService.cs
  - ... va 53 file khac

### 2026-03-27 | 1b6a75e | Merge pull request #5 from NgNguyenChuong/hidden_url
- Commit: 1b6a75efaba78b5ad2e83c7043a35b3d61f992d1
- Loai: merge commit
- Thong ke: 28 files changed, 316 insertions(+), 111 deletions(-)
- Vung anh huong: Web-Static: 25; Other: 3
- Dien giai: Hop nhat nhanh tinh nang/develop, dong bo code va giai quyet xung dot neu co.
- File thay doi tieu bieu (28):
  - API/StreetFoodNarrator.API/Program.cs
  - API/StreetFoodNarrator.API/wwwroot/api.js
  - API/StreetFoodNarrator.API/wwwroot/audio-bulk-generate.html
  - API/StreetFoodNarrator.API/wwwroot/audio-list.html
  - API/StreetFoodNarrator.API/wwwroot/auth-check.js
  - API/StreetFoodNarrator.API/wwwroot/dashboard.html
  - API/StreetFoodNarrator.API/wwwroot/frontend_backup/nav.js
  - API/StreetFoodNarrator.API/wwwroot/frontend_backup/poi-create.html
  - API/StreetFoodNarrator.API/wwwroot/frontend_backup/sidebar.html
  - API/StreetFoodNarrator.API/wwwroot/frontend_backup/sidebar.js
  - API/StreetFoodNarrator.API/wwwroot/history.html
  - API/StreetFoodNarrator.API/wwwroot/index.html
  - API/StreetFoodNarrator.API/wwwroot/js/auth.js
  - API/StreetFoodNarrator.API/wwwroot/nav.js
  - API/StreetFoodNarrator.API/wwwroot/poi-create.html
  - API/StreetFoodNarrator.API/wwwroot/poi-detail.html
  - API/StreetFoodNarrator.API/wwwroot/poi-edit.html
  - API/StreetFoodNarrator.API/wwwroot/poi-list.html
  - API/StreetFoodNarrator.API/wwwroot/register.html
  - API/StreetFoodNarrator.API/wwwroot/sidebar.js
  - API/StreetFoodNarrator.API/wwwroot/tour.html
  - API/StreetFoodNarrator.API/wwwroot/translation.html
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/41c45f1f-04bd-4f8a-ae9b-5e96bb2e992c.png
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/97f2a026-bb5b-4a78-809d-11843db31fc3.png
  - API/StreetFoodNarrator.API/wwwroot/users.html
  - ... va 3 file khac

### 2026-03-30 | 78f16d3 | Refactor POIDetailPage and WelcomePage for improved functionality and UI responsiveness
- Commit: 78f16d3e58cd785cfa4afcf619512a325ba886b2
- Loai: regular commit
- Thong ke: 36 files changed, 5626 insertions(+), 1164 deletions(-)
- Vung anh huong: Mobile-Views: 16; Mobile-Resources: 5; Mobile-Services: 3; API-Data: 3; Mobile-App: 3; Mobile-ViewModels: 2; API-Controllers: 1; Documentation: 1; Mobile-Models: 1; Mobile-Platforms: 1
- Dien giai: Cap nhat logic backend: endpoint, service, model hoac du lieu. Cap nhat ung dung mobile MAUI: man hinh, state, service va tai nguyen. Bo sung cap nhat tai lieu ky thuat, huong dan test hoac mo ta thiet ke. Tac dong truc tiep den cac luong nghiep vu tour/POI/thanh toan/goi VIP.
- File thay doi tieu bieu (36):
  - API/StreetFoodNarrator.API/Controllers/POIsController.cs
  - API/StreetFoodNarrator.API/Data/DbInitializer.cs
  - API/StreetFoodNarrator.API/Data/PoiSeedReset.cs
  - API/StreetFoodNarrator.API/Data/seed/vinh-khanh-pois.json
  - Documentation/Geofencing-README.md
  - MobileApp/StreetFoodNarrator.App/App.xaml.cs
  - MobileApp/StreetFoodNarrator.App/AppConfig.cs
  - MobileApp/StreetFoodNarrator.App/AppShell.xaml.cs
  - MobileApp/StreetFoodNarrator.App/Core/Models/POI.cs
  - MobileApp/StreetFoodNarrator.App/Core/Services/IGeofenceService.cs
  - MobileApp/StreetFoodNarrator.App/Core/Services/LocationService.cs
  - MobileApp/StreetFoodNarrator.App/Core/Services/ZoneRepository.cs
  - MobileApp/StreetFoodNarrator.App/Platforms/Android/AndroidManifest.xml
  - MobileApp/StreetFoodNarrator.App/Resources/Strings/AppStrings.cs
  - MobileApp/StreetFoodNarrator.App/Resources/Strings/AppStrings.en.resx
  - MobileApp/StreetFoodNarrator.App/Resources/Strings/AppStrings.resx
  - MobileApp/StreetFoodNarrator.App/Resources/Strings/AppStrings.vi.resx
  - MobileApp/StreetFoodNarrator.App/Resources/Strings/AppStrings.zh.resx
  - MobileApp/StreetFoodNarrator.App/ViewModels/MainViewModel.cs
  - MobileApp/StreetFoodNarrator.App/ViewModels/POIDetailViewModel.cs
  - MobileApp/StreetFoodNarrator.App/Views/Components/TabMapView.xaml
  - MobileApp/StreetFoodNarrator.App/Views/Components/TabMapView.xaml.cs
  - MobileApp/StreetFoodNarrator.App/Views/Components/TabMenuView.xaml.cs
  - MobileApp/StreetFoodNarrator.App/Views/Components/VirtualModeView.xaml
  - MobileApp/StreetFoodNarrator.App/Views/Components/VirtualModeView.xaml.cs
  - ... va 11 file khac

### 2026-03-30 | ee37f4f | feat(routing): Implement offline routing service with Itinero integration
- Commit: ee37f4f568f7482f6af838955522821a96454a49
- Loai: regular commit
- Thong ke: 40 files changed, 2193 insertions(+), 1392 deletions(-)
- Vung anh huong: Web-Static: 15; Mobile-Views: 12; Mobile-App: 5; Database: 3; Mobile-ViewModels: 2; Mobile-Services: 2; API-Controllers: 1
- Dien giai: Cap nhat logic backend: endpoint, service, model hoac du lieu. Dieu chinh giao dien web admin/vendor va script tuong tac. Cap nhat ung dung mobile MAUI: man hinh, state, service va tai nguyen. Tinh chinh schema/seed/du lieu mau phuc vu API va ung dung. Bo sung/tang cap tinh nang moi trong luong su dung chinh.
- File thay doi tieu bieu (40):
  - API/StreetFoodNarrator.API/Controllers/POIsController.cs
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/02898ab9-3f7f-426a-ba20-5df99281b96a.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/2a0a6179-9cec-4832-8644-32dbabbb0f98.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/661a644d-11dc-40a3-b213-b6fbd710191d.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/6c544da6-836e-4850-a9e1-11702a636014.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/97d49ab2-8f51-448d-8c5b-ca49e7a07acf.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/a43c75e8-29e7-44d5-b2f5-360a7ac4e9f0.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/a9a4bb16-a411-46bc-9b21-6dfed0ccf0c3.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/aca0e66f-d1fb-4d39-8977-dff11ff04697.png
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/b4037064-674f-4954-97bf-a561d67f85ab.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/cb256bb8-35f8-4a65-b121-742e46fe29f8.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/cecb9ac6-6738-4297-ac7d-be55b5b6a3ba.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/cf944df2-5002-481d-9061-b0152d1ef0d9.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/ed277e1e-80c2-4780-abb8-7a76619412ad.png
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/f5d25a9f-88b5-4a5b-96ae-24d2429caaa3.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/ff89901d-3716-4af5-b6b0-ec1dee8c0382.jpg
  - Database/SeedData.sql
  - Database/VinhKhanh-Real-Seed.mongodb.js
  - Database/run-seed.js
  - MobileApp/StreetFoodNarrator.App/AppConfig.cs
  - MobileApp/StreetFoodNarrator.App/AppShell.xaml
  - MobileApp/StreetFoodNarrator.App/AppShell.xaml.cs
  - MobileApp/StreetFoodNarrator.App/Core/Services/IOfflineRoutingService.cs
  - MobileApp/StreetFoodNarrator.App/Core/Services/Implementations/OfflineRoutingService.cs
  - MobileApp/StreetFoodNarrator.App/MauiProgram.cs
  - ... va 15 file khac

### 2026-03-30 | c434284 | Delete .vscode directory
- Commit: c4342848a05f713268d5d8d3ada7abd37cd6a7ce
- Loai: regular commit
- Thong ke: 2 files changed, 25 deletions(-)
- Vung anh huong: Other: 2
- Dien giai: Loai bo chuc nang hoac code khong con su dung de giam do phuc tap.
- File thay doi tieu bieu (2):
  - .vscode/settings.json
  - .vscode/tasks.json

### 2026-04-01 | b907d4c | Add OfflineBannerSessionState to manage banner dismissal state
- Commit: b907d4ca6fd286a07049fcd64f26f857079a5248
- Loai: regular commit
- Thong ke: 24 files changed, 1622 insertions(+), 441 deletions(-)
- Vung anh huong: Mobile-Views: 18; Mobile-App: 3; Mobile-ViewModels: 2; Web-Static: 1
- Dien giai: Dieu chinh giao dien web admin/vendor va script tuong tac. Cap nhat ung dung mobile MAUI: man hinh, state, service va tai nguyen. Bo sung/tang cap tinh nang moi trong luong su dung chinh.
- File thay doi tieu bieu (24):
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/e713ee44-6a12-4df1-a8c7-5d4e857fc3cd.jpg
  - MobileApp/StreetFoodNarrator.App/App.xaml.cs
  - MobileApp/StreetFoodNarrator.App/Helpers/CustomAlert.cs
  - MobileApp/StreetFoodNarrator.App/Helpers/OfflineBannerSessionState.cs
  - MobileApp/StreetFoodNarrator.App/ViewModels/MainViewModel.cs
  - MobileApp/StreetFoodNarrator.App/ViewModels/POIDetailViewModel.cs
  - MobileApp/StreetFoodNarrator.App/Views/Components/TabMapView.xaml
  - MobileApp/StreetFoodNarrator.App/Views/Components/TabMapView.xaml.cs
  - MobileApp/StreetFoodNarrator.App/Views/Components/TabMenuView.xaml
  - MobileApp/StreetFoodNarrator.App/Views/Components/TabMenuView.xaml.cs
  - MobileApp/StreetFoodNarrator.App/Views/Components/TabSavedView.xaml.cs
  - MobileApp/StreetFoodNarrator.App/Views/ExploreMapPage.xaml
  - MobileApp/StreetFoodNarrator.App/Views/ExploreMapPage.xaml.cs
  - MobileApp/StreetFoodNarrator.App/Views/MainPage.PinPopup.cs
  - MobileApp/StreetFoodNarrator.App/Views/MainPage.VirtualTour.cs
  - MobileApp/StreetFoodNarrator.App/Views/MainPage.xaml
  - MobileApp/StreetFoodNarrator.App/Views/MainPage.xaml.cs
  - MobileApp/StreetFoodNarrator.App/Views/ProfilePage.xaml
  - MobileApp/StreetFoodNarrator.App/Views/ProfilePage.xaml.cs
  - MobileApp/StreetFoodNarrator.App/Views/SavedPage.xaml
  - MobileApp/StreetFoodNarrator.App/Views/SavedPage.xaml.cs
  - MobileApp/StreetFoodNarrator.App/Views/SettingsPage.xaml
  - MobileApp/StreetFoodNarrator.App/Views/SettingsPage.xaml.cs
  - MobileApp/StreetFoodNarrator.App/Views/WelcomePage.xaml.cs

### 2026-04-01 | f202e85 | Merge pull request #6 from NgNguyenChuong/develop
- Commit: f202e853b45eccaf0657724858db457fd14a9bf6
- Loai: merge commit
- Thong ke: 31 files changed, 1526 insertions(+), 111 deletions(-)
- Vung anh huong: Web-Static: 25; Project-Config: 3; Other: 3
- Dien giai: Hop nhat nhanh tinh nang/develop, dong bo code va giai quyet xung dot neu co.
- File thay doi tieu bieu (31):
  - API/StreetFoodNarrator.API/Program.cs
  - API/StreetFoodNarrator.API/wwwroot/api.js
  - API/StreetFoodNarrator.API/wwwroot/audio-bulk-generate.html
  - API/StreetFoodNarrator.API/wwwroot/audio-list.html
  - API/StreetFoodNarrator.API/wwwroot/auth-check.js
  - API/StreetFoodNarrator.API/wwwroot/dashboard.html
  - API/StreetFoodNarrator.API/wwwroot/frontend_backup/nav.js
  - API/StreetFoodNarrator.API/wwwroot/frontend_backup/poi-create.html
  - API/StreetFoodNarrator.API/wwwroot/frontend_backup/sidebar.html
  - API/StreetFoodNarrator.API/wwwroot/frontend_backup/sidebar.js
  - API/StreetFoodNarrator.API/wwwroot/history.html
  - API/StreetFoodNarrator.API/wwwroot/index.html
  - API/StreetFoodNarrator.API/wwwroot/js/auth.js
  - API/StreetFoodNarrator.API/wwwroot/nav.js
  - API/StreetFoodNarrator.API/wwwroot/poi-create.html
  - API/StreetFoodNarrator.API/wwwroot/poi-detail.html
  - API/StreetFoodNarrator.API/wwwroot/poi-edit.html
  - API/StreetFoodNarrator.API/wwwroot/poi-list.html
  - API/StreetFoodNarrator.API/wwwroot/register.html
  - API/StreetFoodNarrator.API/wwwroot/sidebar.js
  - API/StreetFoodNarrator.API/wwwroot/tour.html
  - API/StreetFoodNarrator.API/wwwroot/translation.html
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/41c45f1f-04bd-4f8a-ae9b-5e96bb2e992c.png
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/97f2a026-bb5b-4a78-809d-11843db31fc3.png
  - API/StreetFoodNarrator.API/wwwroot/users.html
  - ... va 6 file khac

### 2026-04-01 | 069084c | Delete fix-cache-issue.md
- Commit: 069084c387fe408251cad5b3bc4dbeaed515d958
- Loai: regular commit
- Thong ke: 1 file changed, 95 deletions(-)
- Vung anh huong: Other: 1
- Dien giai: Trong tam la sua loi va dong bo hanh vi giua cac thanh phan. Loai bo chuc nang hoac code khong con su dung de giam do phuc tap.
- File thay doi tieu bieu (1):
  - fix-cache-issue.md

### 2026-04-01 | 0bddcf8 | Delete test-rewrite.md
- Commit: 0bddcf8237a36e04d4e18dc1cd42f7ed49564c5f
- Loai: regular commit
- Thong ke: 1 file changed, 82 deletions(-)
- Vung anh huong: Other: 1
- Dien giai: Loai bo chuc nang hoac code khong con su dung de giam do phuc tap.
- File thay doi tieu bieu (1):
  - test-rewrite.md

### 2026-04-01 | ef478ac | feat: Implement user management features and pagination
- Commit: ef478ac4e5436b29dd128873ba84dde41fe21712
- Loai: regular commit
- Thong ke: 19 files changed, 1340 insertions(+), 854 deletions(-)
- Vung anh huong: Web-Static: 12; API-Controllers: 3; API-Data: 3; API-Models: 1
- Dien giai: Cap nhat logic backend: endpoint, service, model hoac du lieu. Dieu chinh giao dien web admin/vendor va script tuong tac. Bo sung/tang cap tinh nang moi trong luong su dung chinh.
- File thay doi tieu bieu (19):
  - API/StreetFoodNarrator.API/Controllers/AudioController.cs
  - API/StreetFoodNarrator.API/Controllers/POIsController.cs
  - API/StreetFoodNarrator.API/Controllers/UsersController.cs
  - API/StreetFoodNarrator.API/Data/DbInitializer.cs
  - API/StreetFoodNarrator.API/Data/MongoDbContext.cs
  - API/StreetFoodNarrator.API/Data/seed/reset-pois.js
  - API/StreetFoodNarrator.API/Models/SubmissionIdempotency.cs
  - API/StreetFoodNarrator.API/wwwroot/api.js
  - API/StreetFoodNarrator.API/wwwroot/audio-bulk-generate.html
  - API/StreetFoodNarrator.API/wwwroot/audio-list.html
  - API/StreetFoodNarrator.API/wwwroot/dashboard.html
  - API/StreetFoodNarrator.API/wwwroot/js/modules.js
  - API/StreetFoodNarrator.API/wwwroot/poi-create.html
  - API/StreetFoodNarrator.API/wwwroot/poi-edit.html
  - API/StreetFoodNarrator.API/wwwroot/poi-list.html
  - API/StreetFoodNarrator.API/wwwroot/sidebar.js
  - API/StreetFoodNarrator.API/wwwroot/translation.html
  - API/StreetFoodNarrator.API/wwwroot/users.html
  - API/StreetFoodNarrator.API/wwwroot/vendors-list.html

### 2026-04-01 | 7c967fb | Merge pull request #7 from NgNguyenChuong/app_ver2.5
- Commit: 7c967fb3d685f273cb361fac19c840cf43a5793d
- Loai: merge commit
- Thong ke: 92 files changed, 9793 insertions(+), 3065 deletions(-)
- Vung anh huong: Web-Static: 28; Mobile-Views: 25; Mobile-App: 8; API-Data: 5; Mobile-Resources: 5; Mobile-Services: 5; Other: 4; API-Controllers: 3; Database: 3; Mobile-ViewModels: 2; Documentation: 1; Mobile-Models: 1; API-Models: 1; Mobile-Platforms: 1
- Dien giai: Hop nhat nhanh tinh nang/develop, dong bo code va giai quyet xung dot neu co.
- File thay doi tieu bieu (92):
  - .vscode/settings.json
  - .vscode/tasks.json
  - API/StreetFoodNarrator.API/Controllers/AudioController.cs
  - API/StreetFoodNarrator.API/Controllers/POIsController.cs
  - API/StreetFoodNarrator.API/Controllers/UsersController.cs
  - API/StreetFoodNarrator.API/Data/DbInitializer.cs
  - API/StreetFoodNarrator.API/Data/MongoDbContext.cs
  - API/StreetFoodNarrator.API/Data/PoiSeedReset.cs
  - API/StreetFoodNarrator.API/Data/seed/reset-pois.js
  - API/StreetFoodNarrator.API/Data/seed/vinh-khanh-pois.json
  - API/StreetFoodNarrator.API/Models/SubmissionIdempotency.cs
  - API/StreetFoodNarrator.API/wwwroot/api.js
  - API/StreetFoodNarrator.API/wwwroot/audio-bulk-generate.html
  - API/StreetFoodNarrator.API/wwwroot/audio-list.html
  - API/StreetFoodNarrator.API/wwwroot/dashboard.html
  - API/StreetFoodNarrator.API/wwwroot/js/modules.js
  - API/StreetFoodNarrator.API/wwwroot/poi-create.html
  - API/StreetFoodNarrator.API/wwwroot/poi-edit.html
  - API/StreetFoodNarrator.API/wwwroot/poi-list.html
  - API/StreetFoodNarrator.API/wwwroot/sidebar.js
  - API/StreetFoodNarrator.API/wwwroot/translation.html
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/02898ab9-3f7f-426a-ba20-5df99281b96a.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/2a0a6179-9cec-4832-8644-32dbabbb0f98.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/661a644d-11dc-40a3-b213-b6fbd710191d.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/6c544da6-836e-4850-a9e1-11702a636014.jpg
  - ... va 67 file khac

### 2026-04-01 | 5f03b05 | push thử
- Commit: 5f03b0532ab37f59321009cafac3c0e741fb8852
- Loai: regular commit
- Thong ke: 1 file changed, 1 insertion(+)
- Vung anh huong: Other: 1
- Dien giai: Dieu chinh cau truc ma nguon va tai nguyen de on dinh he thong.
- File thay doi tieu bieu (1):
  - push.txt

### 2026-04-01 | c873612 | test
- Commit: c873612065581c53e8f4aa884ceb5065f8f4b40e
- Loai: regular commit
- Thong ke: 1 file changed, 1 insertion(+), 1 deletion(-)
- Vung anh huong: Other: 1
- Dien giai: Dieu chinh cau truc ma nguon va tai nguyen de on dinh he thong.
- File thay doi tieu bieu (1):
  - push.txt

### 2026-04-01 | 69e4f21 | Merge pull request #8 from NgNguyenChuong/app_ver2.5
- Commit: 69e4f211e77adfdad4a66a3e44e14cd73b89f4c5
- Loai: merge commit
- Thong ke: 1 file changed, 1 insertion(+)
- Vung anh huong: Other: 1
- Dien giai: Hop nhat nhanh tinh nang/develop, dong bo code va giai quyet xung dot neu co.
- File thay doi tieu bieu (1):
  - push.txt

### 2026-04-04 | e9f068d | change tour
- Commit: e9f068d4d5ff229f0d8d9b9d80b7103e126ba8c5
- Loai: regular commit
- Thong ke: 1 file changed, 217 insertions(+), 36 deletions(-)
- Vung anh huong: Web-Static: 1
- Dien giai: Dieu chinh giao dien web admin/vendor va script tuong tac. Tac dong truc tiep den cac luong nghiep vu tour/POI/thanh toan/goi VIP.
- File thay doi tieu bieu (1):
  - API/StreetFoodNarrator.API/wwwroot/tour.html

### 2026-04-04 | 12d25a7 | change adi bulk
- Commit: 12d25a7a9a2440b26210d63880f9d2b08f5f4c2e
- Loai: regular commit
- Thong ke: 1 file changed, 44 insertions(+), 1 deletion(-)
- Vung anh huong: Web-Static: 1
- Dien giai: Dieu chinh giao dien web admin/vendor va script tuong tac.
- File thay doi tieu bieu (1):
  - API/StreetFoodNarrator.API/wwwroot/api.js

### 2026-04-04 | dc05bd4 | change audio style
- Commit: dc05bd419ec66ce081f31950d6b7d6e88fd5e2e0
- Loai: regular commit
- Thong ke: 1 file changed, 76 insertions(+), 27 deletions(-)
- Vung anh huong: Web-Static: 1
- Dien giai: Dieu chinh giao dien web admin/vendor va script tuong tac. Dieu chinh luong audio/TTS va cac man hinh lien quan.
- File thay doi tieu bieu (1):
  - API/StreetFoodNarrator.API/wwwroot/audio-list.html

### 2026-04-04 | f63a8ef | Merge pull request #9 from NgNguyenChuong/change_ml
- Commit: f63a8ef43fd083c8407176b46028838e46bde577
- Loai: merge commit
- Thong ke: 3 files changed, 337 insertions(+), 64 deletions(-)
- Vung anh huong: Web-Static: 3
- Dien giai: Hop nhat nhanh tinh nang/develop, dong bo code va giai quyet xung dot neu co.
- File thay doi tieu bieu (3):
  - API/StreetFoodNarrator.API/wwwroot/api.js
  - API/StreetFoodNarrator.API/wwwroot/audio-list.html
  - API/StreetFoodNarrator.API/wwwroot/tour.html

### 2026-04-04 | 4d845c6 | modify style in vendor page
- Commit: 4d845c6017a3d21fc467c895bf093b8b8736ce31
- Loai: regular commit
- Thong ke: 1 file changed, 76 insertions(+), 25 deletions(-)
- Vung anh huong: Web-Static: 1
- Dien giai: Dieu chinh giao dien web admin/vendor va script tuong tac.
- File thay doi tieu bieu (1):
  - API/StreetFoodNarrator.API/wwwroot/vendors-list.html

### 2026-04-04 | 29931de | slation modify style
- Commit: 29931de9cfe09f6fec73114ab2e737b0cdd0654a
- Loai: regular commit
- Thong ke: 1 file changed, 53 insertions(+), 15 deletions(-)
- Vung anh huong: Web-Static: 1
- Dien giai: Dieu chinh giao dien web admin/vendor va script tuong tac.
- File thay doi tieu bieu (1):
  - API/StreetFoodNarrator.API/wwwroot/translation.html

### 2026-04-04 | 7451831 | usser
- Commit: 745183127bc330b5477e907e35af1e75196f1ba4
- Loai: regular commit
- Thong ke: 1 file changed, 44 insertions(+), 13 deletions(-)
- Vung anh huong: Web-Static: 1
- Dien giai: Dieu chinh giao dien web admin/vendor va script tuong tac.
- File thay doi tieu bieu (1):
  - API/StreetFoodNarrator.API/wwwroot/users.html

### 2026-04-04 | 4ff1013 | Merge pull request #10 from NgNguyenChuong/fix_style
- Commit: 4ff1013b18a04d0d6aacac9e813b47bba53a3ca4
- Loai: merge commit
- Thong ke: 3 files changed, 173 insertions(+), 53 deletions(-)
- Vung anh huong: Web-Static: 3
- Dien giai: Hop nhat nhanh tinh nang/develop, dong bo code va giai quyet xung dot neu co.
- File thay doi tieu bieu (3):
  - API/StreetFoodNarrator.API/wwwroot/translation.html
  - API/StreetFoodNarrator.API/wwwroot/users.html
  - API/StreetFoodNarrator.API/wwwroot/vendors-list.html

### 2026-04-04 | 9ebc1b7 | history style changing
- Commit: 9ebc1b7dfb85d20e323747061b86c21fe3b083d1
- Loai: regular commit
- Thong ke: 1 file changed, 25 insertions(+), 8 deletions(-)
- Vung anh huong: Web-Static: 1
- Dien giai: Dieu chinh giao dien web admin/vendor va script tuong tac.
- File thay doi tieu bieu (1):
  - API/StreetFoodNarrator.API/wwwroot/history.html

### 2026-04-04 | 4ab276c | Merge pull request #11 from NgNguyenChuong/fix_style
- Commit: 4ab276cebe88f7354ff83092243b70d1135e2646
- Loai: merge commit
- Thong ke: 1 file changed, 25 insertions(+), 8 deletions(-)
- Vung anh huong: Web-Static: 1
- Dien giai: Hop nhat nhanh tinh nang/develop, dong bo code va giai quyet xung dot neu co.
- File thay doi tieu bieu (1):
  - API/StreetFoodNarrator.API/wwwroot/history.html

### 2026-04-04 | e56502b | feat: Add image upload functionality and update Tour model to include ImageUrl
- Commit: e56502b7608ed249abcfbf9925eac8fec981f45a
- Loai: regular commit
- Thong ke: 6 files changed, 146 insertions(+), 23 deletions(-)
- Vung anh huong: Other: 2; API-Controllers: 2; Web-Static: 1; API-Models: 1
- Dien giai: Cap nhat logic backend: endpoint, service, model hoac du lieu. Dieu chinh giao dien web admin/vendor va script tuong tac. Bo sung/tang cap tinh nang moi trong luong su dung chinh. Tac dong truc tiep den cac luong nghiep vu tour/POI/thanh toan/goi VIP.
- File thay doi tieu bieu (6):
  - API/StreetFoodNarrator.API/Controllers/POIsController.cs
  - API/StreetFoodNarrator.API/Controllers/ToursController.cs
  - API/StreetFoodNarrator.API/Models/Tour.cs
  - API/StreetFoodNarrator.API/Uploads/tour-images/2c444d6a-332d-4160-af60-9d15c6e5f39a.jpg
  - API/StreetFoodNarrator.API/Uploads/tour-images/ec6c5e21-a9bf-42b0-a280-2955ae5c1fca.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/tour-images/60446877-4562-4c1c-816a-1c2e9fb9bbb3.jpg

### 2026-04-04 | 1fc8784 | feat: Add TourDetailPopupPage for displaying tour details and interactions
- Commit: 1fc87840d20c2c543e8b472ebcfcdcb02bf5e2a1
- Loai: regular commit
- Thong ke: 74 files changed, 6760 insertions(+), 2876 deletions(-)
- Vung anh huong: Mobile-Views: 28; Mobile-Resources: 11; Web-Static: 9; Mobile-App: 9; Mobile-Services: 6; Documentation: 4; Tools: 2; Mobile-Models: 2; Other: 1; Mobile-Platforms: 1; Mobile-ViewModels: 1
- Dien giai: Dieu chinh giao dien web admin/vendor va script tuong tac. Cap nhat ung dung mobile MAUI: man hinh, state, service va tai nguyen. Bo sung cap nhat tai lieu ky thuat, huong dan test hoac mo ta thiet ke. Bo sung/tang cap tinh nang moi trong luong su dung chinh. Tac dong truc tiep den cac luong nghiep vu tour/POI/thanh toan/goi VIP.
- File thay doi tieu bieu (74):
  - API/StreetFoodNarrator.API/wwwroot/api.js
  - API/StreetFoodNarrator.API/wwwroot/audio-list.html
  - API/StreetFoodNarrator.API/wwwroot/history.html
  - API/StreetFoodNarrator.API/wwwroot/poi-edit.html
  - API/StreetFoodNarrator.API/wwwroot/poi-list.html
  - API/StreetFoodNarrator.API/wwwroot/tour.html
  - API/StreetFoodNarrator.API/wwwroot/translation.html
  - API/StreetFoodNarrator.API/wwwroot/users.html
  - API/StreetFoodNarrator.API/wwwroot/vendors-list.html
  - Documentation/Screenshots/qr-mainpage.png
  - Documentation/Screenshots/qr-mainpage.txt
  - Documentation/Screenshots/qr-test-poi2.png
  - Documentation/Screenshots/qr-test-poi2.txt
  - MobileApp/StreetFoodNarrator.App/App.xaml.cs
  - MobileApp/StreetFoodNarrator.App/AppConfig.cs
  - MobileApp/StreetFoodNarrator.App/AppShell.xaml
  - MobileApp/StreetFoodNarrator.App/AppShell.xaml.cs
  - MobileApp/StreetFoodNarrator.App/Core/Models/MenuItemDto.cs
  - MobileApp/StreetFoodNarrator.App/Core/Models/POI.cs
  - MobileApp/StreetFoodNarrator.App/Core/Services/IGeofenceService.cs
  - MobileApp/StreetFoodNarrator.App/Core/Services/IRemoteLocalizationService.cs
  - MobileApp/StreetFoodNarrator.App/Core/Services/Implementations/RemoteLocalizationService.cs
  - MobileApp/StreetFoodNarrator.App/Core/Services/LanguageService.cs
  - MobileApp/StreetFoodNarrator.App/Core/Services/LocationService.cs
  - MobileApp/StreetFoodNarrator.App/Core/Services/ZoneRepository.cs
  - ... va 49 file khac

### 2026-04-04 | 1436651 | Merge pull request #12 from NgNguyenChuong/app_ver2.5
- Commit: 14366516bd3c6642824a29f350b73d0357b50d7f
- Loai: merge commit
- Thong ke: 73 files changed, 6371 insertions(+), 2774 deletions(-)
- Vung anh huong: Mobile-Views: 28; Mobile-Resources: 11; Mobile-App: 9; Mobile-Services: 6; Documentation: 4; Other: 3; Web-Static: 3; Tools: 2; Mobile-Models: 2; API-Controllers: 2; Mobile-Platforms: 1; API-Models: 1; Mobile-ViewModels: 1
- Dien giai: Hop nhat nhanh tinh nang/develop, dong bo code va giai quyet xung dot neu co.
- File thay doi tieu bieu (73):
  - API/StreetFoodNarrator.API/Controllers/POIsController.cs
  - API/StreetFoodNarrator.API/Controllers/ToursController.cs
  - API/StreetFoodNarrator.API/Models/Tour.cs
  - API/StreetFoodNarrator.API/Uploads/tour-images/2c444d6a-332d-4160-af60-9d15c6e5f39a.jpg
  - API/StreetFoodNarrator.API/Uploads/tour-images/ec6c5e21-a9bf-42b0-a280-2955ae5c1fca.jpg
  - API/StreetFoodNarrator.API/wwwroot/poi-edit.html
  - API/StreetFoodNarrator.API/wwwroot/poi-list.html
  - API/StreetFoodNarrator.API/wwwroot/uploads/tour-images/60446877-4562-4c1c-816a-1c2e9fb9bbb3.jpg
  - Documentation/Screenshots/qr-mainpage.png
  - Documentation/Screenshots/qr-mainpage.txt
  - Documentation/Screenshots/qr-test-poi2.png
  - Documentation/Screenshots/qr-test-poi2.txt
  - MobileApp/StreetFoodNarrator.App/App.xaml.cs
  - MobileApp/StreetFoodNarrator.App/AppConfig.cs
  - MobileApp/StreetFoodNarrator.App/AppShell.xaml
  - MobileApp/StreetFoodNarrator.App/AppShell.xaml.cs
  - MobileApp/StreetFoodNarrator.App/Core/Models/MenuItemDto.cs
  - MobileApp/StreetFoodNarrator.App/Core/Models/POI.cs
  - MobileApp/StreetFoodNarrator.App/Core/Services/IGeofenceService.cs
  - MobileApp/StreetFoodNarrator.App/Core/Services/IRemoteLocalizationService.cs
  - MobileApp/StreetFoodNarrator.App/Core/Services/Implementations/RemoteLocalizationService.cs
  - MobileApp/StreetFoodNarrator.App/Core/Services/LanguageService.cs
  - MobileApp/StreetFoodNarrator.App/Core/Services/LocationService.cs
  - MobileApp/StreetFoodNarrator.App/Core/Services/ZoneRepository.cs
  - MobileApp/StreetFoodNarrator.App/Core/Utils/Constants.cs
  - ... va 48 file khac

### 2026-04-05 | 44d5f7c | Refactor MainPage and POIDetailPage for Localization and UI Improvements
- Commit: 44d5f7cfe710d8fef8dd5099a71f6e6a0a1b102b
- Loai: regular commit
- Thong ke: 35 files changed, 1370 insertions(+), 211 deletions(-)
- Vung anh huong: Mobile-Views: 24; Mobile-Resources: 5; Mobile-App: 2; Mobile-ViewModels: 2; Mobile-Models: 2
- Dien giai: Cap nhat ung dung mobile MAUI: man hinh, state, service va tai nguyen. Mo rong da ngon ngu va noi dia hoa noi dung hien thi. Tac dong truc tiep den cac luong nghiep vu tour/POI/thanh toan/goi VIP.
- File thay doi tieu bieu (35):
  - MobileApp/StreetFoodNarrator.App/App.xaml.cs
  - MobileApp/StreetFoodNarrator.App/AppShell.xaml.cs
  - MobileApp/StreetFoodNarrator.App/Core/Models/MenuItemDto.cs
  - MobileApp/StreetFoodNarrator.App/Core/Models/POI.cs
  - MobileApp/StreetFoodNarrator.App/Resources/Strings/AppStrings.cs
  - MobileApp/StreetFoodNarrator.App/Resources/Strings/AppStrings.en.resx
  - MobileApp/StreetFoodNarrator.App/Resources/Strings/AppStrings.resx
  - MobileApp/StreetFoodNarrator.App/Resources/Strings/AppStrings.vi.resx
  - MobileApp/StreetFoodNarrator.App/Resources/Strings/AppStrings.zh.resx
  - MobileApp/StreetFoodNarrator.App/ViewModels/MainViewModel.cs
  - MobileApp/StreetFoodNarrator.App/ViewModels/POIDetailViewModel.cs
  - MobileApp/StreetFoodNarrator.App/Views/Components/FloatingBottomNav.xaml.cs
  - MobileApp/StreetFoodNarrator.App/Views/Components/PinPopupView.xaml
  - MobileApp/StreetFoodNarrator.App/Views/Components/TabMapView.xaml
  - MobileApp/StreetFoodNarrator.App/Views/Components/TabMenuView.xaml
  - MobileApp/StreetFoodNarrator.App/Views/Components/TabMenuView.xaml.cs
  - MobileApp/StreetFoodNarrator.App/Views/Components/TabSavedView.xaml
  - MobileApp/StreetFoodNarrator.App/Views/Components/TabSavedView.xaml.cs
  - MobileApp/StreetFoodNarrator.App/Views/Components/VirtualModeView.xaml
  - MobileApp/StreetFoodNarrator.App/Views/ExploreMapPage.xaml
  - MobileApp/StreetFoodNarrator.App/Views/ExploreMapPage.xaml.cs
  - MobileApp/StreetFoodNarrator.App/Views/MainPage.Map.cs
  - MobileApp/StreetFoodNarrator.App/Views/MainPage.PinPopup.cs
  - MobileApp/StreetFoodNarrator.App/Views/MainPage.Tour.cs
  - MobileApp/StreetFoodNarrator.App/Views/MainPage.VirtualTour.cs
  - ... va 10 file khac

### 2026-04-05 | d1c960e | feat: Implement localization for static texts in POI detail and tour detail pages
- Commit: d1c960e0ba27c4a7d65af2dce1c4452fb74552ee
- Loai: regular commit
- Thong ke: 44 files changed, 3674 insertions(+), 676 deletions(-)
- Vung anh huong: Mobile-Views: 28; Mobile-Services: 5; Mobile-Resources: 4; Mobile-Platforms: 3; Mobile-App: 2; Mobile-Models: 1; Mobile-ViewModels: 1
- Dien giai: Cap nhat ung dung mobile MAUI: man hinh, state, service va tai nguyen. Bo sung/tang cap tinh nang moi trong luong su dung chinh. Mo rong da ngon ngu va noi dia hoa noi dung hien thi. Tac dong truc tiep den cac luong nghiep vu tour/POI/thanh toan/goi VIP.
- File thay doi tieu bieu (44):
  - MobileApp/StreetFoodNarrator.App/App.xaml.cs
  - MobileApp/StreetFoodNarrator.App/Core/Models/POI.cs
  - MobileApp/StreetFoodNarrator.App/Core/Services/Implementations/DataSyncService.cs
  - MobileApp/StreetFoodNarrator.App/Core/Services/Implementations/OfflineRouteCacheService.cs
  - MobileApp/StreetFoodNarrator.App/Core/Services/Implementations/PoiImageCacheService.cs
  - MobileApp/StreetFoodNarrator.App/Core/Services/Implementations/TextToSpeechService.cs
  - MobileApp/StreetFoodNarrator.App/Core/Services/LocationService.cs
  - MobileApp/StreetFoodNarrator.App/Helpers/CustomAlert.cs
  - MobileApp/StreetFoodNarrator.App/Platforms/Android/AndroidManifest.xml
  - MobileApp/StreetFoodNarrator.App/Platforms/Android/MainActivity.cs
  - MobileApp/StreetFoodNarrator.App/Platforms/Android/Resources/values/styles.xml
  - MobileApp/StreetFoodNarrator.App/Resources/Strings/AppStrings.en.resx
  - MobileApp/StreetFoodNarrator.App/Resources/Strings/AppStrings.resx
  - MobileApp/StreetFoodNarrator.App/Resources/Strings/AppStrings.vi.resx
  - MobileApp/StreetFoodNarrator.App/Resources/Strings/AppStrings.zh.resx
  - MobileApp/StreetFoodNarrator.App/ViewModels/MainViewModel.cs
  - MobileApp/StreetFoodNarrator.App/Views/Components/BottomNavView.xaml.cs
  - MobileApp/StreetFoodNarrator.App/Views/Components/FloatingBottomNav.xaml
  - MobileApp/StreetFoodNarrator.App/Views/Components/PinPopupView.xaml
  - MobileApp/StreetFoodNarrator.App/Views/Components/PinPopupView.xaml.cs
  - MobileApp/StreetFoodNarrator.App/Views/Components/TabMapView.xaml
  - MobileApp/StreetFoodNarrator.App/Views/Components/TabMapView.xaml.cs
  - MobileApp/StreetFoodNarrator.App/Views/Components/TabMenuView.xaml
  - MobileApp/StreetFoodNarrator.App/Views/Components/TabMenuView.xaml.cs
  - MobileApp/StreetFoodNarrator.App/Views/Components/TabSavedView.xaml
  - ... va 19 file khac

### 2026-04-05 | 6427696 | Merge pull request #13 from NgNguyenChuong/app_ver2.5
- Commit: 642769641daafddff8dc9674e905973d8e004b05
- Loai: merge commit
- Thong ke: 53 files changed, 5012 insertions(+), 855 deletions(-)
- Vung anh huong: Mobile-Views: 33; Mobile-Services: 5; Mobile-Resources: 5; Mobile-App: 3; Mobile-Platforms: 3; Mobile-Models: 2; Mobile-ViewModels: 2
- Dien giai: Hop nhat nhanh tinh nang/develop, dong bo code va giai quyet xung dot neu co.
- File thay doi tieu bieu (53):
  - MobileApp/StreetFoodNarrator.App/App.xaml.cs
  - MobileApp/StreetFoodNarrator.App/AppShell.xaml.cs
  - MobileApp/StreetFoodNarrator.App/Core/Models/MenuItemDto.cs
  - MobileApp/StreetFoodNarrator.App/Core/Models/POI.cs
  - MobileApp/StreetFoodNarrator.App/Core/Services/Implementations/DataSyncService.cs
  - MobileApp/StreetFoodNarrator.App/Core/Services/Implementations/OfflineRouteCacheService.cs
  - MobileApp/StreetFoodNarrator.App/Core/Services/Implementations/PoiImageCacheService.cs
  - MobileApp/StreetFoodNarrator.App/Core/Services/Implementations/TextToSpeechService.cs
  - MobileApp/StreetFoodNarrator.App/Core/Services/LocationService.cs
  - MobileApp/StreetFoodNarrator.App/Helpers/CustomAlert.cs
  - MobileApp/StreetFoodNarrator.App/Platforms/Android/AndroidManifest.xml
  - MobileApp/StreetFoodNarrator.App/Platforms/Android/MainActivity.cs
  - MobileApp/StreetFoodNarrator.App/Platforms/Android/Resources/values/styles.xml
  - MobileApp/StreetFoodNarrator.App/Resources/Strings/AppStrings.cs
  - MobileApp/StreetFoodNarrator.App/Resources/Strings/AppStrings.en.resx
  - MobileApp/StreetFoodNarrator.App/Resources/Strings/AppStrings.resx
  - MobileApp/StreetFoodNarrator.App/Resources/Strings/AppStrings.vi.resx
  - MobileApp/StreetFoodNarrator.App/Resources/Strings/AppStrings.zh.resx
  - MobileApp/StreetFoodNarrator.App/ViewModels/MainViewModel.cs
  - MobileApp/StreetFoodNarrator.App/ViewModels/POIDetailViewModel.cs
  - MobileApp/StreetFoodNarrator.App/Views/Components/BottomNavView.xaml.cs
  - MobileApp/StreetFoodNarrator.App/Views/Components/FloatingBottomNav.xaml
  - MobileApp/StreetFoodNarrator.App/Views/Components/FloatingBottomNav.xaml.cs
  - MobileApp/StreetFoodNarrator.App/Views/Components/PinPopupView.xaml
  - MobileApp/StreetFoodNarrator.App/Views/Components/PinPopupView.xaml.cs
  - ... va 28 file khac

### 2026-04-06 | 366d8c6 | Add LanguageSwitcher and MovementFileLogger helpers
- Commit: 366d8c6c8b08695996461a6537ae78d55d8a3525
- Loai: regular commit
- Thong ke: 50 files changed, 5530 insertions(+), 1628 deletions(-)
- Vung anh huong: Mobile-Views: 15; Web-Static: 15; API-Controllers: 4; API-Models: 4; Mobile-Services: 3; Mobile-App: 3; Mobile-ViewModels: 2; Other: 2; API-Data: 1; Mobile-Models: 1
- Dien giai: Cap nhat logic backend: endpoint, service, model hoac du lieu. Dieu chinh giao dien web admin/vendor va script tuong tac. Cap nhat ung dung mobile MAUI: man hinh, state, service va tai nguyen. Bo sung/tang cap tinh nang moi trong luong su dung chinh. Mo rong da ngon ngu va noi dia hoa noi dung hien thi.
- File thay doi tieu bieu (50):
  - API/StreetFoodNarrator.API/Controllers/AnalyticsController.cs
  - API/StreetFoodNarrator.API/Controllers/AudioController.cs
  - API/StreetFoodNarrator.API/Controllers/POIsController.cs
  - API/StreetFoodNarrator.API/Controllers/ToursController.cs
  - API/StreetFoodNarrator.API/Data/DbInitializer.cs
  - API/StreetFoodNarrator.API/Models/AudioContent.cs
  - API/StreetFoodNarrator.API/Models/NarrationLog.cs
  - API/StreetFoodNarrator.API/Models/POI.cs
  - API/StreetFoodNarrator.API/Models/Tour.cs
  - API/StreetFoodNarrator.API/Uploads/tour-images/532a5dc5-7b25-4663-9327-1bd8b0d31b38.jpg
  - API/StreetFoodNarrator.API/Uploads/tour-images/8fdf478d-a2f0-42c0-95a2-2841dce920cd.jpg
  - API/StreetFoodNarrator.API/wwwroot/api.js
  - API/StreetFoodNarrator.API/wwwroot/audio-bulk-generate.html
  - API/StreetFoodNarrator.API/wwwroot/audio-list.html
  - API/StreetFoodNarrator.API/wwwroot/dashboard.html
  - API/StreetFoodNarrator.API/wwwroot/history.html
  - API/StreetFoodNarrator.API/wwwroot/poi-create.html
  - API/StreetFoodNarrator.API/wwwroot/poi-detail.html
  - API/StreetFoodNarrator.API/wwwroot/poi-edit.html
  - API/StreetFoodNarrator.API/wwwroot/poi-list.html
  - API/StreetFoodNarrator.API/wwwroot/sidebar.js
  - API/StreetFoodNarrator.API/wwwroot/tour.html
  - API/StreetFoodNarrator.API/wwwroot/translation.html
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/dab07122-e643-439b-a9fd-11a84434d490.jpg
  - API/StreetFoodNarrator.API/wwwroot/users.html
  - ... va 25 file khac

### 2026-04-06 | bcef8de | Merge pull request #14 from NgNguyenChuong/fix_Web
- Commit: bcef8de7b6dc4092988fa1812cb690022c47ea8e
- Loai: merge commit
- Thong ke: 50 files changed, 5530 insertions(+), 1628 deletions(-)
- Vung anh huong: Mobile-Views: 15; Web-Static: 15; API-Controllers: 4; API-Models: 4; Mobile-Services: 3; Mobile-App: 3; Mobile-ViewModels: 2; Other: 2; API-Data: 1; Mobile-Models: 1
- Dien giai: Hop nhat nhanh tinh nang/develop, dong bo code va giai quyet xung dot neu co.
- File thay doi tieu bieu (50):
  - API/StreetFoodNarrator.API/Controllers/AnalyticsController.cs
  - API/StreetFoodNarrator.API/Controllers/AudioController.cs
  - API/StreetFoodNarrator.API/Controllers/POIsController.cs
  - API/StreetFoodNarrator.API/Controllers/ToursController.cs
  - API/StreetFoodNarrator.API/Data/DbInitializer.cs
  - API/StreetFoodNarrator.API/Models/AudioContent.cs
  - API/StreetFoodNarrator.API/Models/NarrationLog.cs
  - API/StreetFoodNarrator.API/Models/POI.cs
  - API/StreetFoodNarrator.API/Models/Tour.cs
  - API/StreetFoodNarrator.API/Uploads/tour-images/532a5dc5-7b25-4663-9327-1bd8b0d31b38.jpg
  - API/StreetFoodNarrator.API/Uploads/tour-images/8fdf478d-a2f0-42c0-95a2-2841dce920cd.jpg
  - API/StreetFoodNarrator.API/wwwroot/api.js
  - API/StreetFoodNarrator.API/wwwroot/audio-bulk-generate.html
  - API/StreetFoodNarrator.API/wwwroot/audio-list.html
  - API/StreetFoodNarrator.API/wwwroot/dashboard.html
  - API/StreetFoodNarrator.API/wwwroot/history.html
  - API/StreetFoodNarrator.API/wwwroot/poi-create.html
  - API/StreetFoodNarrator.API/wwwroot/poi-detail.html
  - API/StreetFoodNarrator.API/wwwroot/poi-edit.html
  - API/StreetFoodNarrator.API/wwwroot/poi-list.html
  - API/StreetFoodNarrator.API/wwwroot/sidebar.js
  - API/StreetFoodNarrator.API/wwwroot/tour.html
  - API/StreetFoodNarrator.API/wwwroot/translation.html
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/dab07122-e643-439b-a9fd-11a84434d490.jpg
  - API/StreetFoodNarrator.API/wwwroot/users.html
  - ... va 25 file khac

### 2026-04-06 | 2976e4c | DONE
- Commit: 2976e4c691a87d6a7689a837b5dbd81626956990
- Loai: regular commit
- Thong ke: 4 files changed, 43 insertions(+), 77 deletions(-)
- Vung anh huong: Web-Static: 4
- Dien giai: Dieu chinh giao dien web admin/vendor va script tuong tac.
- File thay doi tieu bieu (4):
  - API/StreetFoodNarrator.API/wwwroot/dashboard.html
  - API/StreetFoodNarrator.API/wwwroot/poi-edit.html
  - API/StreetFoodNarrator.API/wwwroot/poi-list.html
  - API/StreetFoodNarrator.API/wwwroot/users.html

### 2026-04-06 | 32bf5b8 | Merge pull request #15 from NgNguyenChuong/final1
- Commit: 32bf5b8cfe38554b62c4d2275bfcd03aa1645b5a
- Loai: merge commit
- Thong ke: 4 files changed, 43 insertions(+), 77 deletions(-)
- Vung anh huong: Web-Static: 4
- Dien giai: Hop nhat nhanh tinh nang/develop, dong bo code va giai quyet xung dot neu co.
- File thay doi tieu bieu (4):
  - API/StreetFoodNarrator.API/wwwroot/dashboard.html
  - API/StreetFoodNarrator.API/wwwroot/poi-edit.html
  - API/StreetFoodNarrator.API/wwwroot/poi-list.html
  - API/StreetFoodNarrator.API/wwwroot/users.html

### 2026-04-06 | 9af313c | add user detail
- Commit: 9af313cd131c5414ccd242bc052d36c35acc76d9
- Loai: regular commit
- Thong ke: 5 files changed, 563 insertions(+)
- Vung anh huong: Web-Static: 5
- Dien giai: Dieu chinh giao dien web admin/vendor va script tuong tac. Bo sung/tang cap tinh nang moi trong luong su dung chinh.
- File thay doi tieu bieu (5):
  - API/StreetFoodNarrator.API/wwwroot/sidebar.js
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/5ea740ed-229e-4084-a01b-684c6294be9e.png
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/75106fda-28b8-4d82-9229-6c393d75b417.png
  - API/StreetFoodNarrator.API/wwwroot/vendor-profile.html
  - API/StreetFoodNarrator.API/wwwroot/vendors-list.html

### 2026-04-06 | d45d39d | Merge pull request #16 from NgNguyenChuong/change_user
- Commit: d45d39d5a2115599f69a7702acaf8c958dae6a6f
- Loai: merge commit
- Thong ke: 5 files changed, 563 insertions(+)
- Vung anh huong: Web-Static: 5
- Dien giai: Hop nhat nhanh tinh nang/develop, dong bo code va giai quyet xung dot neu co.
- File thay doi tieu bieu (5):
  - API/StreetFoodNarrator.API/wwwroot/sidebar.js
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/5ea740ed-229e-4084-a01b-684c6294be9e.png
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/75106fda-28b8-4d82-9229-6c393d75b417.png
  - API/StreetFoodNarrator.API/wwwroot/vendor-profile.html
  - API/StreetFoodNarrator.API/wwwroot/vendors-list.html

### 2026-04-06 | f436a1e | audio add feature is fixed
- Commit: f436a1e1a01c1bae5a265bf9f3967dcab251f523
- Loai: regular commit
- Thong ke: 5 files changed, 174 insertions(+), 46 deletions(-)
- Vung anh huong: Web-Static: 2; API-Controllers: 2; Project-Config: 1
- Dien giai: Cap nhat logic backend: endpoint, service, model hoac du lieu. Dieu chinh giao dien web admin/vendor va script tuong tac. Trong tam la sua loi va dong bo hanh vi giua cac thanh phan. Bo sung/tang cap tinh nang moi trong luong su dung chinh. Dieu chinh luong audio/TTS va cac man hinh lien quan.
- File thay doi tieu bieu (5):
  - API/StreetFoodNarrator.API/Controllers/AudioController.cs
  - API/StreetFoodNarrator.API/Controllers/TTSController.cs
  - API/StreetFoodNarrator.API/wwwroot/audio-bulk-generate.html
  - API/StreetFoodNarrator.API/wwwroot/audio-list.html
  - requirements.txt

### 2026-04-06 | 162e4d4 | add audio style change
- Commit: 162e4d45e15779548c5a68ec1cdd480e9f896261
- Loai: regular commit
- Thong ke: 1 file changed, 43 insertions(+)
- Vung anh huong: Web-Static: 1
- Dien giai: Dieu chinh giao dien web admin/vendor va script tuong tac. Bo sung/tang cap tinh nang moi trong luong su dung chinh. Dieu chinh luong audio/TTS va cac man hinh lien quan.
- File thay doi tieu bieu (1):
  - API/StreetFoodNarrator.API/wwwroot/api.js

### 2026-04-06 | c4c3b91 | Merge pull request #17 from NgNguyenChuong/fix_audio
- Commit: c4c3b916ea8f47334f3a980e32c8bd7278dccc35
- Loai: merge commit
- Thong ke: 6 files changed, 217 insertions(+), 46 deletions(-)
- Vung anh huong: Web-Static: 3; API-Controllers: 2; Project-Config: 1
- Dien giai: Hop nhat nhanh tinh nang/develop, dong bo code va giai quyet xung dot neu co.
- File thay doi tieu bieu (6):
  - API/StreetFoodNarrator.API/Controllers/AudioController.cs
  - API/StreetFoodNarrator.API/Controllers/TTSController.cs
  - API/StreetFoodNarrator.API/wwwroot/api.js
  - API/StreetFoodNarrator.API/wwwroot/audio-bulk-generate.html
  - API/StreetFoodNarrator.API/wwwroot/audio-list.html
  - requirements.txt

### 2026-04-06 | 6642edc | Stop tracking uploads in wwwroot/uploads
- Commit: 6642edc03cf8af08f010fe8caacd596b0072d713
- Loai: regular commit
- Thong ke: 71 files changed, 0 insertions(+), 0 deletions(-)
- Vung anh huong: Web-Static: 71
- Dien giai: Dieu chinh giao dien web admin/vendor va script tuong tac.
- File thay doi tieu bieu (71):
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/00c024be-6d87-4fb2-921b-a02d2c154c07.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/02898ab9-3f7f-426a-ba20-5df99281b96a.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/0b382c6f-c083-4bc2-9729-2cad451080ac.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/1883a6da-11d7-4b9c-9974-176f934ca4b0.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/21700d26-eeee-4bd3-9abb-a3e45282d080.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/26424778-dd2a-4121-a4a6-62ddf83492e0.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/2715567f-d7e5-4fa5-b924-ecf5e1a21cd0.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/2870fc7f-b408-4213-b88c-71a499254288.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/2a0a6179-9cec-4832-8644-32dbabbb0f98.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/2ec0aad6-15e4-4d02-8056-62b8b49054ba.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/39465a82-6a6b-4bc2-9ca0-2c230fff29e1.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/41c45f1f-04bd-4f8a-ae9b-5e96bb2e992c.png
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/4b18ebb2-da8b-48d2-a10d-eecf1e69fd85.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/5006424c-b84d-4d8c-a00e-ed9f8ff07440.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/58ffc6f0-df4f-463d-9194-b529e7c5ed40.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/5cb5bb1b-e3ff-442d-a0dd-c3bfc9423376.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/5ea740ed-229e-4084-a01b-684c6294be9e.png
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/643b585d-8b8f-418b-9827-8618c57b2361.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/661a644d-11dc-40a3-b213-b6fbd710191d.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/6c544da6-836e-4850-a9e1-11702a636014.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/6d8de26e-5bab-4111-95ad-3327ec74d4a4.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/722713dd-f4bc-4851-b702-22c4488f041b.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/75106fda-28b8-4d82-9229-6c393d75b417.png
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/7bd7a271-4d11-4bdd-ae26-790e814e05ee.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/7c456959-cfe9-40fe-a4d0-c89b145b14a8.jpg
  - ... va 46 file khac

### 2026-04-07 | 79fc056 | temp
- Commit: 79fc0560bcbc3310d9b0f7fcc90c3ee54cecb0cd
- Loai: regular commit
- Thong ke: 84 files changed, 200 insertions(+), 112 deletions(-)
- Vung anh huong: Web-Static: 79; API-Controllers: 2; Project-Config: 1; Mobile-App: 1; Other: 1
- Dien giai: Cap nhat logic backend: endpoint, service, model hoac du lieu. Dieu chinh giao dien web admin/vendor va script tuong tac.
- File thay doi tieu bieu (84):
  - .gitignore
  - API/StreetFoodNarrator.API/Controllers/AudioController.cs
  - API/StreetFoodNarrator.API/Controllers/POIsController.cs
  - API/StreetFoodNarrator.API/wwwroot/api.js
  - API/StreetFoodNarrator.API/wwwroot/audio-list.html
  - API/StreetFoodNarrator.API/wwwroot/dashboard.html
  - API/StreetFoodNarrator.API/wwwroot/frontend_backup/sidebar.js
  - API/StreetFoodNarrator.API/wwwroot/poi-edit.html
  - API/StreetFoodNarrator.API/wwwroot/poi-list.html
  - API/StreetFoodNarrator.API/wwwroot/sidebar.js
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/00c024be-6d87-4fb2-921b-a02d2c154c07.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/02898ab9-3f7f-426a-ba20-5df99281b96a.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/0b382c6f-c083-4bc2-9729-2cad451080ac.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/1883a6da-11d7-4b9c-9974-176f934ca4b0.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/21700d26-eeee-4bd3-9abb-a3e45282d080.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/26424778-dd2a-4121-a4a6-62ddf83492e0.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/2715567f-d7e5-4fa5-b924-ecf5e1a21cd0.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/2870fc7f-b408-4213-b88c-71a499254288.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/2a0a6179-9cec-4832-8644-32dbabbb0f98.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/2ec0aad6-15e4-4d02-8056-62b8b49054ba.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/39465a82-6a6b-4bc2-9ca0-2c230fff29e1.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/41c45f1f-04bd-4f8a-ae9b-5e96bb2e992c.png
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/4b18ebb2-da8b-48d2-a10d-eecf1e69fd85.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/5006424c-b84d-4d8c-a00e-ed9f8ff07440.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/58ffc6f0-df4f-463d-9194-b529e7c5ed40.jpg
  - ... va 59 file khac

### 2026-04-07 | 0f74f19 | change all flow
- Commit: 0f74f199d307187423ca5055378e5f61990cffe2
- Loai: regular commit
- Thong ke: 3 files changed, 389 insertions(+), 426 deletions(-)
- Vung anh huong: Web-Static: 2; API-Controllers: 1
- Dien giai: Cap nhat logic backend: endpoint, service, model hoac du lieu. Dieu chinh giao dien web admin/vendor va script tuong tac.
- File thay doi tieu bieu (3):
  - API/StreetFoodNarrator.API/Controllers/AudioController.cs
  - API/StreetFoodNarrator.API/wwwroot/audio-list.html
  - API/StreetFoodNarrator.API/wwwroot/sidebar.js

### 2026-04-07 | bd2b1e2 | Merge pull request #18 from NgNguyenChuong/test1
- Commit: bd2b1e27130af0b8d4ba7b20fdf03583e66d0f29
- Loai: merge commit
- Thong ke: 84 files changed, 579 insertions(+), 528 deletions(-)
- Vung anh huong: Web-Static: 79; API-Controllers: 2; Project-Config: 1; Mobile-App: 1; Other: 1
- Dien giai: Hop nhat nhanh tinh nang/develop, dong bo code va giai quyet xung dot neu co.
- File thay doi tieu bieu (84):
  - .gitignore
  - API/StreetFoodNarrator.API/Controllers/AudioController.cs
  - API/StreetFoodNarrator.API/Controllers/POIsController.cs
  - API/StreetFoodNarrator.API/wwwroot/api.js
  - API/StreetFoodNarrator.API/wwwroot/audio-list.html
  - API/StreetFoodNarrator.API/wwwroot/dashboard.html
  - API/StreetFoodNarrator.API/wwwroot/frontend_backup/sidebar.js
  - API/StreetFoodNarrator.API/wwwroot/poi-edit.html
  - API/StreetFoodNarrator.API/wwwroot/poi-list.html
  - API/StreetFoodNarrator.API/wwwroot/sidebar.js
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/00c024be-6d87-4fb2-921b-a02d2c154c07.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/02898ab9-3f7f-426a-ba20-5df99281b96a.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/0b382c6f-c083-4bc2-9729-2cad451080ac.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/1883a6da-11d7-4b9c-9974-176f934ca4b0.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/21700d26-eeee-4bd3-9abb-a3e45282d080.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/26424778-dd2a-4121-a4a6-62ddf83492e0.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/2715567f-d7e5-4fa5-b924-ecf5e1a21cd0.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/2870fc7f-b408-4213-b88c-71a499254288.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/2a0a6179-9cec-4832-8644-32dbabbb0f98.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/2ec0aad6-15e4-4d02-8056-62b8b49054ba.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/39465a82-6a6b-4bc2-9ca0-2c230fff29e1.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/41c45f1f-04bd-4f8a-ae9b-5e96bb2e992c.png
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/4b18ebb2-da8b-48d2-a10d-eecf1e69fd85.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/5006424c-b84d-4d8c-a00e-ed9f8ff07440.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/58ffc6f0-df4f-463d-9194-b529e7c5ed40.jpg
  - ... va 59 file khac

### 2026-04-07 | c98cef9 | Enhance audio playback with fallback text and improve language selection UI
- Commit: c98cef948133d84fc8adf534319c70ec75848792
- Loai: regular commit
- Thong ke: 13 files changed, 147 insertions(+), 47 deletions(-)
- Vung anh huong: Mobile-Views: 5; Mobile-Services: 3; Mobile-App: 3; Other: 1; Mobile-ViewModels: 1
- Dien giai: Cap nhat ung dung mobile MAUI: man hinh, state, service va tai nguyen. Bo sung/tang cap tinh nang moi trong luong su dung chinh. Mo rong da ngon ngu va noi dia hoa noi dung hien thi. Dieu chinh luong audio/TTS va cac man hinh lien quan.
- File thay doi tieu bieu (13):
  - .gitignore
  - MobileApp/StreetFoodNarrator.App/AppConfig.cs
  - MobileApp/StreetFoodNarrator.App/Core/Services/AudioService.cs
  - MobileApp/StreetFoodNarrator.App/Core/Services/IGeofenceService.cs
  - MobileApp/StreetFoodNarrator.App/Core/Services/IServices.cs
  - MobileApp/StreetFoodNarrator.App/Helpers/LanguageSwitcher.cs
  - MobileApp/StreetFoodNarrator.App/MauiProgram.cs
  - MobileApp/StreetFoodNarrator.App/ViewModels/MainViewModel.cs
  - MobileApp/StreetFoodNarrator.App/Views/Components/TabMapView.xaml
  - MobileApp/StreetFoodNarrator.App/Views/Components/TabMapView.xaml.cs
  - MobileApp/StreetFoodNarrator.App/Views/ExploreMapPage.xaml.cs
  - MobileApp/StreetFoodNarrator.App/Views/SettingsPage.xaml
  - MobileApp/StreetFoodNarrator.App/Views/SettingsPage.xaml.cs

### 2026-04-07 | ccd6498 | Merge remote-tracking branch 'origin/develop' into test_nha
- Commit: ccd649826c60afbb085e519ebfc96bb41d85b8ee
- Loai: merge commit
- Thong ke: 11 files changed, 576 insertions(+), 525 deletions(-)
- Vung anh huong: Web-Static: 7; API-Controllers: 2; Project-Config: 1; Other: 1
- Dien giai: Hop nhat nhanh tinh nang/develop, dong bo code va giai quyet xung dot neu co.
- File thay doi tieu bieu (11):
  - .gitignore
  - API/StreetFoodNarrator.API/Controllers/AudioController.cs
  - API/StreetFoodNarrator.API/Controllers/POIsController.cs
  - API/StreetFoodNarrator.API/wwwroot/api.js
  - API/StreetFoodNarrator.API/wwwroot/audio-list.html
  - API/StreetFoodNarrator.API/wwwroot/dashboard.html
  - API/StreetFoodNarrator.API/wwwroot/frontend_backup/sidebar.js
  - API/StreetFoodNarrator.API/wwwroot/poi-edit.html
  - API/StreetFoodNarrator.API/wwwroot/poi-list.html
  - API/StreetFoodNarrator.API/wwwroot/sidebar.js
  - global.json

### 2026-04-07 | 81ed006 | Add uploads/images directory to .gitignore
- Commit: 81ed006f949f39c9a105188c5c8ee8043f8902d7
- Loai: regular commit
- Thong ke: 1 file changed, 1 insertion(+), 1 deletion(-)
- Vung anh huong: Other: 1
- Dien giai: Bo sung/tang cap tinh nang moi trong luong su dung chinh.
- File thay doi tieu bieu (1):
  - .gitignore

### 2026-04-07 | 3dfa2d0 | Update SDK version to 10.0.103 in global.json
- Commit: 3dfa2d0ecadc4189880cdc00bb6804433ffe6ccd
- Loai: regular commit
- Thong ke: 1 file changed, 1 insertion(+), 1 deletion(-)
- Vung anh huong: Project-Config: 1
- Dien giai: Dieu chinh cau truc ma nguon va tai nguyen de on dinh he thong.
- File thay doi tieu bieu (1):
  - global.json

### 2026-04-07 | 8e1ead2 | fix audio gen
- Commit: 8e1ead2275fb395c3771f37769b5f6a3d0821b8a
- Loai: regular commit
- Thong ke: 13 files changed, 138 insertions(+), 92 deletions(-)
- Vung anh huong: Web-Static: 11; Other: 1; API-Controllers: 1
- Dien giai: Cap nhat logic backend: endpoint, service, model hoac du lieu. Dieu chinh giao dien web admin/vendor va script tuong tac. Trong tam la sua loi va dong bo hanh vi giua cac thanh phan. Dieu chinh luong audio/TTS va cac man hinh lien quan.
- File thay doi tieu bieu (13):
  - API/StreetFoodNarrator.API/Controllers/POIsController.cs
  - API/StreetFoodNarrator.API/wwwroot/audio-bulk-generate.html
  - API/StreetFoodNarrator.API/wwwroot/audio-list.html
  - API/StreetFoodNarrator.API/wwwroot/poi-edit.html
  - API/StreetFoodNarrator.API/wwwroot/poi-list.html
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/01126ef3-514d-4297-8a9b-e8a0327876e3.png
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/0507f81a-de5f-46dc-8e82-639308bfa694.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/221a2512-b8ad-4d81-9b2e-d5612f523841.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/2a08a521-03bb-4026-9399-d89b20ff2386.png
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/4265f3ff-2b77-48a5-bc64-c1e646f5b2c3.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/86ebc2bd-f34e-4436-a217-1aa9050f0c67.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/a8d2697d-84b5-4163-961c-914ca2dc34be.jpg
  - tts_wrapper.py

### 2026-04-07 | a7b4bc2 | Merge pull request #19 from NgNguyenChuong/fix_audio_gen
- Commit: a7b4bc229066160e7cc3127be89bf7215c29d65c
- Loai: merge commit
- Thong ke: 13 files changed, 138 insertions(+), 92 deletions(-)
- Vung anh huong: Web-Static: 11; Other: 1; API-Controllers: 1
- Dien giai: Hop nhat nhanh tinh nang/develop, dong bo code va giai quyet xung dot neu co.
- File thay doi tieu bieu (13):
  - API/StreetFoodNarrator.API/Controllers/POIsController.cs
  - API/StreetFoodNarrator.API/wwwroot/audio-bulk-generate.html
  - API/StreetFoodNarrator.API/wwwroot/audio-list.html
  - API/StreetFoodNarrator.API/wwwroot/poi-edit.html
  - API/StreetFoodNarrator.API/wwwroot/poi-list.html
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/01126ef3-514d-4297-8a9b-e8a0327876e3.png
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/0507f81a-de5f-46dc-8e82-639308bfa694.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/221a2512-b8ad-4d81-9b2e-d5612f523841.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/2a08a521-03bb-4026-9399-d89b20ff2386.png
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/4265f3ff-2b77-48a5-bc64-c1e646f5b2c3.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/86ebc2bd-f34e-4436-a217-1aa9050f0c67.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/a8d2697d-84b5-4163-961c-914ca2dc34be.jpg
  - tts_wrapper.py

### 2026-04-07 | b74ccc9 | feat(web): update admin web/audio workflow and notifications
- Commit: b74ccc9e21a48dc99065d0b91423c574dab05cb6
- Loai: regular commit
- Thong ke: 25 files changed, 1904 insertions(+), 685 deletions(-)
- Vung anh huong: Web-Static: 15; API-Controllers: 4; Other: 2; API-Models: 2; API-Data: 1; API-Services: 1
- Dien giai: Cap nhat logic backend: endpoint, service, model hoac du lieu. Dieu chinh giao dien web admin/vendor va script tuong tac. Bo sung/tang cap tinh nang moi trong luong su dung chinh. Dieu chinh luong audio/TTS va cac man hinh lien quan.
- File thay doi tieu bieu (25):
  - API/StreetFoodNarrator.API/Controllers/AudioController.cs
  - API/StreetFoodNarrator.API/Controllers/NotificationsController.cs
  - API/StreetFoodNarrator.API/Controllers/POIsController.cs
  - API/StreetFoodNarrator.API/Controllers/VendorsController.cs
  - API/StreetFoodNarrator.API/Data/MongoDbContext.cs
  - API/StreetFoodNarrator.API/Hubs/NotificationsHub.cs
  - API/StreetFoodNarrator.API/Models/AppNotification.cs
  - API/StreetFoodNarrator.API/Models/POI.cs
  - API/StreetFoodNarrator.API/Program.cs
  - API/StreetFoodNarrator.API/Services/NotificationService.cs
  - API/StreetFoodNarrator.API/wwwroot/api.js
  - API/StreetFoodNarrator.API/wwwroot/audio-bulk-generate.html
  - API/StreetFoodNarrator.API/wwwroot/audio-list.html
  - API/StreetFoodNarrator.API/wwwroot/dashboard.html
  - API/StreetFoodNarrator.API/wwwroot/history.html
  - API/StreetFoodNarrator.API/wwwroot/poi-create.html
  - API/StreetFoodNarrator.API/wwwroot/poi-detail.html
  - API/StreetFoodNarrator.API/wwwroot/poi-edit.html
  - API/StreetFoodNarrator.API/wwwroot/poi-list.html
  - API/StreetFoodNarrator.API/wwwroot/sidebar.js
  - API/StreetFoodNarrator.API/wwwroot/tour.html
  - API/StreetFoodNarrator.API/wwwroot/translation.html
  - API/StreetFoodNarrator.API/wwwroot/users.html
  - API/StreetFoodNarrator.API/wwwroot/vendor-profile.html
  - API/StreetFoodNarrator.API/wwwroot/vendors-list.html

### 2026-04-07 | 2744918 | Merge branch 'conflig_resolve' into test_nha
- Commit: 274491823b47362e1ee010a850c078a6f59aac63
- Loai: merge commit
- Thong ke: 13 files changed, 169 insertions(+), 84 deletions(-)
- Vung anh huong: Web-Static: 11; Other: 1; API-Controllers: 1
- Dien giai: Hop nhat nhanh tinh nang/develop, dong bo code va giai quyet xung dot neu co.
- File thay doi tieu bieu (13):
  - API/StreetFoodNarrator.API/Controllers/POIsController.cs
  - API/StreetFoodNarrator.API/wwwroot/audio-bulk-generate.html
  - API/StreetFoodNarrator.API/wwwroot/audio-list.html
  - API/StreetFoodNarrator.API/wwwroot/poi-edit.html
  - API/StreetFoodNarrator.API/wwwroot/poi-list.html
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/01126ef3-514d-4297-8a9b-e8a0327876e3.png
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/0507f81a-de5f-46dc-8e82-639308bfa694.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/221a2512-b8ad-4d81-9b2e-d5612f523841.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/2a08a521-03bb-4026-9399-d89b20ff2386.png
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/4265f3ff-2b77-48a5-bc64-c1e646f5b2c3.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/86ebc2bd-f34e-4436-a217-1aa9050f0c67.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/a8d2697d-84b5-4163-961c-914ca2dc34be.jpg
  - tts_wrapper.py

### 2026-04-07 | 279f0bd | Merge pull request #20 from NgNguyenChuong/test_nha
- Commit: 279f0bdaa9cee44360b7fe5de6357c9ea60a8e2b
- Loai: merge commit
- Thong ke: 112 files changed, 2044 insertions(+), 686 deletions(-)
- Vung anh huong: Web-Static: 87; Mobile-Views: 5; Mobile-App: 4; API-Controllers: 4; Mobile-Services: 3; Other: 3; API-Models: 2; API-Data: 1; Project-Config: 1; API-Services: 1; Mobile-ViewModels: 1
- Dien giai: Hop nhat nhanh tinh nang/develop, dong bo code va giai quyet xung dot neu co.
- File thay doi tieu bieu (112):
  - .gitignore
  - API/StreetFoodNarrator.API/Controllers/AudioController.cs
  - API/StreetFoodNarrator.API/Controllers/NotificationsController.cs
  - API/StreetFoodNarrator.API/Controllers/POIsController.cs
  - API/StreetFoodNarrator.API/Controllers/VendorsController.cs
  - API/StreetFoodNarrator.API/Data/MongoDbContext.cs
  - API/StreetFoodNarrator.API/Hubs/NotificationsHub.cs
  - API/StreetFoodNarrator.API/Models/AppNotification.cs
  - API/StreetFoodNarrator.API/Models/POI.cs
  - API/StreetFoodNarrator.API/Program.cs
  - API/StreetFoodNarrator.API/Services/NotificationService.cs
  - API/StreetFoodNarrator.API/wwwroot/api.js
  - API/StreetFoodNarrator.API/wwwroot/audio-bulk-generate.html
  - API/StreetFoodNarrator.API/wwwroot/audio-list.html
  - API/StreetFoodNarrator.API/wwwroot/dashboard.html
  - API/StreetFoodNarrator.API/wwwroot/history.html
  - API/StreetFoodNarrator.API/wwwroot/poi-create.html
  - API/StreetFoodNarrator.API/wwwroot/poi-detail.html
  - API/StreetFoodNarrator.API/wwwroot/poi-edit.html
  - API/StreetFoodNarrator.API/wwwroot/poi-list.html
  - API/StreetFoodNarrator.API/wwwroot/sidebar.js
  - API/StreetFoodNarrator.API/wwwroot/tour.html
  - API/StreetFoodNarrator.API/wwwroot/translation.html
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/00c024be-6d87-4fb2-921b-a02d2c154c07.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/02898ab9-3f7f-426a-ba20-5df99281b96a.jpg
  - ... va 87 file khac

### 2026-04-07 | 7938903 | Merge pull request #21 from NgNguyenChuong/conflig_resolve
- Commit: 793890366d1c1092fb2bfdfc9d09f51286ecfd09
- Loai: merge commit
- Thong ke: 0 files changed, 0 insertions(+), 0 deletions(-)
- Vung anh huong: Other: 0
- Dien giai: Hop nhat nhanh tinh nang/develop, dong bo code va giai quyet xung dot neu co.
- File thay doi: khong co file thay doi truc tiep (chu yeu merge).

### 2026-04-07 | 4d6709a | delete feature admin role
- Commit: 4d6709a236971c7a67d690b7c2fa79eed6ee4add
- Loai: regular commit
- Thong ke: 7 files changed, 146 insertions(+), 35 deletions(-)
- Vung anh huong: Web-Static: 6; API-Controllers: 1
- Dien giai: Cap nhat logic backend: endpoint, service, model hoac du lieu. Dieu chinh giao dien web admin/vendor va script tuong tac. Bo sung/tang cap tinh nang moi trong luong su dung chinh. Loai bo chuc nang hoac code khong con su dung de giam do phuc tap.
- File thay doi tieu bieu (7):
  - API/StreetFoodNarrator.API/Controllers/MenuItemsController.cs
  - API/StreetFoodNarrator.API/wwwroot/api.js
  - API/StreetFoodNarrator.API/wwwroot/poi-detail.html
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/396a1e42-a78d-4a4d-b9e3-224cae876f8b.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/65622ffe-65dc-432d-a661-ed5f0b9762d9.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/7641f5ca-11bf-4032-bc8f-ab5a8cd9fa39.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/7daf3c6f-4d56-42d1-ad84-14ade8a1215a.jpg

### 2026-04-08 | 3be9de5 | deltete feature_admin
- Commit: 3be9de5942c1547f219f7c6b01b340c949c07fc1
- Loai: regular commit
- Thong ke: 4 files changed, 14 insertions(+), 11 deletions(-)
- Vung anh huong: Web-Static: 3; API-Controllers: 1
- Dien giai: Cap nhat logic backend: endpoint, service, model hoac du lieu. Dieu chinh giao dien web admin/vendor va script tuong tac. Bo sung/tang cap tinh nang moi trong luong su dung chinh.
- File thay doi tieu bieu (4):
  - API/StreetFoodNarrator.API/Controllers/AudioController.cs
  - API/StreetFoodNarrator.API/wwwroot/audio-bulk-generate.html
  - API/StreetFoodNarrator.API/wwwroot/audio-list.html
  - API/StreetFoodNarrator.API/wwwroot/nav.js

### 2026-04-08 | 11f91f1 | Merge pull request #22 from NgNguyenChuong/delete_feature_admin
- Commit: 11f91f17430b3e53fb00c57696997db9e8c5d048
- Loai: merge commit
- Thong ke: 11 files changed, 160 insertions(+), 46 deletions(-)
- Vung anh huong: Web-Static: 9; API-Controllers: 2
- Dien giai: Hop nhat nhanh tinh nang/develop, dong bo code va giai quyet xung dot neu co.
- File thay doi tieu bieu (11):
  - API/StreetFoodNarrator.API/Controllers/AudioController.cs
  - API/StreetFoodNarrator.API/Controllers/MenuItemsController.cs
  - API/StreetFoodNarrator.API/wwwroot/api.js
  - API/StreetFoodNarrator.API/wwwroot/audio-bulk-generate.html
  - API/StreetFoodNarrator.API/wwwroot/audio-list.html
  - API/StreetFoodNarrator.API/wwwroot/nav.js
  - API/StreetFoodNarrator.API/wwwroot/poi-detail.html
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/396a1e42-a78d-4a4d-b9e3-224cae876f8b.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/65622ffe-65dc-432d-a661-ed5f0b9762d9.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/7641f5ca-11bf-4032-bc8f-ab5a8cd9fa39.jpg
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/7daf3c6f-4d56-42d1-ad84-14ade8a1215a.jpg

### 2026-04-08 | 16baab9 | feat: enhance audio services and UI for offline functionality, update language support, and improve map handling
- Commit: 16baab97ddc6b90a556c189b704ccb578d86cc02
- Loai: regular commit
- Thong ke: 11 files changed, 153 insertions(+), 143 deletions(-)
- Vung anh huong: Mobile-Views: 6; Mobile-Services: 3; Mobile-App: 2
- Dien giai: Cap nhat ung dung mobile MAUI: man hinh, state, service va tai nguyen. Bo sung/tang cap tinh nang moi trong luong su dung chinh. Mo rong da ngon ngu va noi dia hoa noi dung hien thi. Dieu chinh luong audio/TTS va cac man hinh lien quan.
- File thay doi tieu bieu (11):
  - MobileApp/StreetFoodNarrator.App/AppConfig.cs
  - MobileApp/StreetFoodNarrator.App/Core/Services/AudioService.cs
  - MobileApp/StreetFoodNarrator.App/Core/Services/Implementations/AudioCacheService.cs
  - MobileApp/StreetFoodNarrator.App/Core/Services/Implementations/TextToSpeechService.cs
  - MobileApp/StreetFoodNarrator.App/Helpers/LanguageSwitcher.cs
  - MobileApp/StreetFoodNarrator.App/Views/ExploreMapPage.xaml.cs
  - MobileApp/StreetFoodNarrator.App/Views/MainPage.Map.cs
  - MobileApp/StreetFoodNarrator.App/Views/MainPage.VirtualTour.cs
  - MobileApp/StreetFoodNarrator.App/Views/POIDetailPage.xaml
  - MobileApp/StreetFoodNarrator.App/Views/POIDetailPage.xaml.cs
  - MobileApp/StreetFoodNarrator.App/Views/WelcomePage.xaml.cs

### 2026-04-08 | beb4f40 | Refactor code structure and remove redundant code blocks for improved readability and maintainability
- Commit: beb4f4080e612fe84527ae3a84c82f223959533f
- Loai: regular commit
- Thong ke: 24 files changed, 1728 insertions(+), 831 deletions(-)
- Vung anh huong: Web-Static: 14; Documentation: 3; Mobile-Services: 2; API-Controllers: 2; Other: 1; Mobile-Views: 1; Mobile-App: 1
- Dien giai: Cap nhat logic backend: endpoint, service, model hoac du lieu. Dieu chinh giao dien web admin/vendor va script tuong tac. Cap nhat ung dung mobile MAUI: man hinh, state, service va tai nguyen. Bo sung cap nhat tai lieu ky thuat, huong dan test hoac mo ta thiet ke. Loai bo chuc nang hoac code khong con su dung de giam do phuc tap.
- File thay doi tieu bieu (24):
  - API/StreetFoodNarrator.API/Controllers/AudioController.cs
  - API/StreetFoodNarrator.API/Controllers/POIsController.cs
  - API/StreetFoodNarrator.API/wwwroot/audio-bulk-generate.html
  - API/StreetFoodNarrator.API/wwwroot/audio-list.html
  - API/StreetFoodNarrator.API/wwwroot/dashboard.html
  - API/StreetFoodNarrator.API/wwwroot/history.html
  - API/StreetFoodNarrator.API/wwwroot/poi-create.html
  - API/StreetFoodNarrator.API/wwwroot/poi-detail.html
  - API/StreetFoodNarrator.API/wwwroot/poi-edit.html
  - API/StreetFoodNarrator.API/wwwroot/poi-list.html
  - API/StreetFoodNarrator.API/wwwroot/sidebar.js
  - API/StreetFoodNarrator.API/wwwroot/tour.html
  - API/StreetFoodNarrator.API/wwwroot/translation.html
  - API/StreetFoodNarrator.API/wwwroot/users.html
  - API/StreetFoodNarrator.API/wwwroot/vendor-profile.html
  - API/StreetFoodNarrator.API/wwwroot/vendors-list.html
  - Documentation/Geofencing-Implementation-Checklist.md
  - Documentation/Geofencing-Pseudo-Code.md
  - Documentation/Project-Event-Flows.md
  - MobileApp/StreetFoodNarrator.App/AppConfig.cs
  - MobileApp/StreetFoodNarrator.App/Core/Services/ITTSService.cs
  - MobileApp/StreetFoodNarrator.App/Core/Services/Implementations/TextToSpeechService.cs
  - MobileApp/StreetFoodNarrator.App/Views/ExploreMapPage.xaml.cs
  - tts_wrapper.py

### 2026-04-08 | 8d2fb35 | remove feature approve
- Commit: 8d2fb35339f1d3b5f87011e7b09ca7c5f781ef1c
- Loai: regular commit
- Thong ke: 1 file changed, 12 insertions(+), 45 deletions(-)
- Vung anh huong: API-Controllers: 1
- Dien giai: Cap nhat logic backend: endpoint, service, model hoac du lieu. Bo sung/tang cap tinh nang moi trong luong su dung chinh. Loai bo chuc nang hoac code khong con su dung de giam do phuc tap.
- File thay doi tieu bieu (1):
  - API/StreetFoodNarrator.API/Controllers/POIsController.cs

### 2026-04-08 | 264310f | Merge pull request #25 from NgNguyenChuong/feature_approved
- Commit: 264310f62a05f609fc626a2f9b1067257f06a4e8
- Loai: merge commit
- Thong ke: 1 file changed, 12 insertions(+), 45 deletions(-)
- Vung anh huong: API-Controllers: 1
- Dien giai: Hop nhat nhanh tinh nang/develop, dong bo code va giai quyet xung dot neu co.
- File thay doi tieu bieu (1):
  - API/StreetFoodNarrator.API/Controllers/POIsController.cs

### 2026-04-08 | 3b592e3 | enhance side bar
- Commit: 3b592e3c1d035157b1ddb5a334a99908952988e4
- Loai: regular commit
- Thong ke: 1 file changed, 34 insertions(+), 7 deletions(-)
- Vung anh huong: Web-Static: 1
- Dien giai: Dieu chinh giao dien web admin/vendor va script tuong tac. Bo sung/tang cap tinh nang moi trong luong su dung chinh.
- File thay doi tieu bieu (1):
  - API/StreetFoodNarrator.API/wwwroot/sidebar.js

### 2026-04-08 | 3e57dd0 | Merge pull request #26 from NgNguyenChuong/fix_side_bar
- Commit: 3e57dd08b33de29193ee1418d01878b644167e00
- Loai: merge commit
- Thong ke: 1 file changed, 34 insertions(+), 7 deletions(-)
- Vung anh huong: Web-Static: 1
- Dien giai: Hop nhat nhanh tinh nang/develop, dong bo code va giai quyet xung dot neu co.
- File thay doi tieu bieu (1):
  - API/StreetFoodNarrator.API/wwwroot/sidebar.js

### 2026-04-09 | 9db2d26 | delete feature import long lat
- Commit: 9db2d264ec4c16c48fe5c00523fa62f3cb5966ea
- Loai: regular commit
- Thong ke: 4 files changed, 65 insertions(+), 41 deletions(-)
- Vung anh huong: Web-Static: 2; API-Controllers: 1; API-Models: 1
- Dien giai: Cap nhat logic backend: endpoint, service, model hoac du lieu. Dieu chinh giao dien web admin/vendor va script tuong tac. Bo sung/tang cap tinh nang moi trong luong su dung chinh. Loai bo chuc nang hoac code khong con su dung de giam do phuc tap.
- File thay doi tieu bieu (4):
  - API/StreetFoodNarrator.API/Controllers/POIsController.cs
  - API/StreetFoodNarrator.API/Models/POI.cs
  - API/StreetFoodNarrator.API/wwwroot/poi-create.html
  - API/StreetFoodNarrator.API/wwwroot/poi-detail.html

### 2026-04-09 | 7e6c0dc | Update mobile app features and fixes
- Commit: 7e6c0dc203c3873508ba26059b4f24daa652e903
- Loai: regular commit
- Thong ke: 23 files changed, 1176 insertions(+), 426 deletions(-)
- Vung anh huong: Mobile-Views: 15; Mobile-Services: 4; Mobile-App: 3; Mobile-ViewModels: 1
- Dien giai: Cap nhat ung dung mobile MAUI: man hinh, state, service va tai nguyen. Trong tam la sua loi va dong bo hanh vi giua cac thanh phan. Bo sung/tang cap tinh nang moi trong luong su dung chinh.
- File thay doi tieu bieu (23):
  - MobileApp/StreetFoodNarrator.App/AppConfig.cs
  - MobileApp/StreetFoodNarrator.App/Core/Services/Implementations/AudioCacheService.cs
  - MobileApp/StreetFoodNarrator.App/Core/Services/Implementations/DataSyncService.cs
  - MobileApp/StreetFoodNarrator.App/Core/Services/Implementations/TextToSpeechService.cs
  - MobileApp/StreetFoodNarrator.App/Core/Services/ZoneRepository.cs
  - MobileApp/StreetFoodNarrator.App/Helpers/CustomAlert.cs
  - MobileApp/StreetFoodNarrator.App/Helpers/LanguageSwitcher.cs
  - MobileApp/StreetFoodNarrator.App/ViewModels/MainViewModel.cs
  - MobileApp/StreetFoodNarrator.App/Views/Components/TabMapView.xaml.cs
  - MobileApp/StreetFoodNarrator.App/Views/Components/TabMenuView.xaml
  - MobileApp/StreetFoodNarrator.App/Views/Components/TabSavedView.xaml
  - MobileApp/StreetFoodNarrator.App/Views/Components/VirtualModeView.xaml
  - MobileApp/StreetFoodNarrator.App/Views/Components/VirtualModeView.xaml.cs
  - MobileApp/StreetFoodNarrator.App/Views/ExploreMapPage.xaml
  - MobileApp/StreetFoodNarrator.App/Views/ExploreMapPage.xaml.cs
  - MobileApp/StreetFoodNarrator.App/Views/MainPage.VirtualTour.cs
  - MobileApp/StreetFoodNarrator.App/Views/MainPage.xaml
  - MobileApp/StreetFoodNarrator.App/Views/MainPage.xaml.cs
  - MobileApp/StreetFoodNarrator.App/Views/POIDetailPage.xaml.cs
  - MobileApp/StreetFoodNarrator.App/Views/SavedPage.xaml.cs
  - MobileApp/StreetFoodNarrator.App/Views/SettingsPage.xaml
  - MobileApp/StreetFoodNarrator.App/Views/SettingsPage.xaml.cs
  - MobileApp/StreetFoodNarrator.App/Views/WelcomePage.xaml.cs

### 2026-04-09 | 4cc8359 | design poi list
- Commit: 4cc8359df4853bdf4a3e8a2beb55dd7fcc308c90
- Loai: regular commit
- Thong ke: 1 file changed, 17 insertions(+), 8 deletions(-)
- Vung anh huong: Web-Static: 1
- Dien giai: Dieu chinh giao dien web admin/vendor va script tuong tac. Tac dong truc tiep den cac luong nghiep vu tour/POI/thanh toan/goi VIP.
- File thay doi tieu bieu (1):
  - API/StreetFoodNarrator.API/wwwroot/poi-list.html

### 2026-04-09 | e0769dc | okok
- Commit: e0769dc2ac213e57576e78e1609971d31ba429e4
- Loai: regular commit
- Thong ke: 2 files changed, 35 insertions(+)
- Vung anh huong: Project-Config: 1; Other: 1
- Dien giai: Dieu chinh cau truc ma nguon va tai nguyen de on dinh he thong.
- File thay doi tieu bieu (2):
  - .dockerignore
  - Dockerfile

### 2026-04-09 | 6dfe737 | Merge pull request #27 from NgNguyenChuong/redesign_fe
- Commit: 6dfe737c475a883e0d736874571fdc13afab7298
- Loai: merge commit
- Thong ke: 1 file changed, 17 insertions(+), 8 deletions(-)
- Vung anh huong: Web-Static: 1
- Dien giai: Hop nhat nhanh tinh nang/develop, dong bo code va giai quyet xung dot neu co.
- File thay doi tieu bieu (1):
  - API/StreetFoodNarrator.API/wwwroot/poi-list.html

### 2026-04-09 | 8438947 | Merge origin/develop and keep API from develop
- Commit: 8438947faa7064bf37d240d0f9dd577575badb62
- Loai: merge commit
- Thong ke: 103 files changed, 888 insertions(+), 2072 deletions(-)
- Vung anh huong: Web-Static: 92; API-Controllers: 5; Other: 2; API-Models: 2; API-Data: 1; API-Services: 1
- Dien giai: Hop nhat nhanh tinh nang/develop, dong bo code va giai quyet xung dot neu co.
- File thay doi tieu bieu (103):
  - API/StreetFoodNarrator.API/Controllers/AudioController.cs
  - API/StreetFoodNarrator.API/Controllers/MenuItemsController.cs
  - API/StreetFoodNarrator.API/Controllers/NotificationsController.cs
  - API/StreetFoodNarrator.API/Controllers/POIsController.cs
  - API/StreetFoodNarrator.API/Controllers/VendorsController.cs
  - API/StreetFoodNarrator.API/Data/MongoDbContext.cs
  - API/StreetFoodNarrator.API/Hubs/NotificationsHub.cs
  - API/StreetFoodNarrator.API/Models/AppNotification.cs
  - API/StreetFoodNarrator.API/Models/POI.cs
  - API/StreetFoodNarrator.API/Program.cs
  - API/StreetFoodNarrator.API/Services/NotificationService.cs
  - API/StreetFoodNarrator.API/wwwroot/api.js
  - API/StreetFoodNarrator.API/wwwroot/audio-bulk-generate.html
  - API/StreetFoodNarrator.API/wwwroot/audio-list.html
  - API/StreetFoodNarrator.API/wwwroot/dashboard.html
  - API/StreetFoodNarrator.API/wwwroot/history.html
  - API/StreetFoodNarrator.API/wwwroot/nav.js
  - API/StreetFoodNarrator.API/wwwroot/poi-create.html
  - API/StreetFoodNarrator.API/wwwroot/poi-detail.html
  - API/StreetFoodNarrator.API/wwwroot/poi-edit.html
  - API/StreetFoodNarrator.API/wwwroot/poi-list.html
  - API/StreetFoodNarrator.API/wwwroot/sidebar.js
  - API/StreetFoodNarrator.API/wwwroot/tour.html
  - API/StreetFoodNarrator.API/wwwroot/translation.html
  - API/StreetFoodNarrator.API/wwwroot/uploads/images/00c024be-6d87-4fb2-921b-a02d2c154c07.jpg
  - ... va 78 file khac

### 2026-04-09 | 95442d6 | delete feature delete poi
- Commit: 95442d6acc373cbc5a485b2706db4bb3cf142306
- Loai: regular commit
- Thong ke: 2 files changed, 10 insertions(+), 42 deletions(-)
- Vung anh huong: Web-Static: 2
- Dien giai: Dieu chinh giao dien web admin/vendor va script tuong tac. Bo sung/tang cap tinh nang moi trong luong su dung chinh. Loai bo chuc nang hoac code khong con su dung de giam do phuc tap. Tac dong truc tiep den cac luong nghiep vu tour/POI/thanh toan/goi VIP.
- File thay doi tieu bieu (2):
  - API/StreetFoodNarrator.API/wwwroot/audio-list.html
  - API/StreetFoodNarrator.API/wwwroot/poi-list.html

### 2026-04-09 | 1559dd5 | fix mean listen poi
- Commit: 1559dd5b116a515fe19afd4e50f4bc87c9030f11
- Loai: regular commit
- Thong ke: 6 files changed, 107 insertions(+), 38 deletions(-)
- Vung anh huong: Other: 5; Web-Static: 1
- Dien giai: Dieu chinh giao dien web admin/vendor va script tuong tac. Trong tam la sua loi va dong bo hanh vi giua cac thanh phan. Tac dong truc tiep den cac luong nghiep vu tour/POI/thanh toan/goi VIP.
- File thay doi tieu bieu (6):
  - API/StreetFoodNarrator.API/Uploads/tour-images/224b81f4-6a84-4383-991d-7b4e26cddca2.jpg
  - API/StreetFoodNarrator.API/Uploads/tour-images/59e787dc-9fea-438b-b843-9bdb8da10688.jpg
  - API/StreetFoodNarrator.API/Uploads/tour-images/ab71c762-d0cc-4a68-88e8-4b563b6e8202.jpg
  - API/StreetFoodNarrator.API/Uploads/tour-images/b220b638-99f6-4de7-8dbe-840e99498381.jpg
  - API/StreetFoodNarrator.API/Uploads/tour-images/b9be9ff0-2b2c-4730-882a-26ccab589f1c.jpg
  - API/StreetFoodNarrator.API/wwwroot/users.html

### 2026-04-09 | 90e61f0 | Merge pull request #29 from NgNguyenChuong/fix_history
- Commit: 90e61f0bb12b821be4275c3e4da2cd0817d53735
- Loai: merge commit
- Thong ke: 8 files changed, 117 insertions(+), 80 deletions(-)
- Vung anh huong: Other: 5; Web-Static: 3
- Dien giai: Hop nhat nhanh tinh nang/develop, dong bo code va giai quyet xung dot neu co.
- File thay doi tieu bieu (8):
  - API/StreetFoodNarrator.API/Uploads/tour-images/224b81f4-6a84-4383-991d-7b4e26cddca2.jpg
  - API/StreetFoodNarrator.API/Uploads/tour-images/59e787dc-9fea-438b-b843-9bdb8da10688.jpg
  - API/StreetFoodNarrator.API/Uploads/tour-images/ab71c762-d0cc-4a68-88e8-4b563b6e8202.jpg
  - API/StreetFoodNarrator.API/Uploads/tour-images/b220b638-99f6-4de7-8dbe-840e99498381.jpg
  - API/StreetFoodNarrator.API/Uploads/tour-images/b9be9ff0-2b2c-4730-882a-26ccab589f1c.jpg
  - API/StreetFoodNarrator.API/wwwroot/audio-list.html
  - API/StreetFoodNarrator.API/wwwroot/poi-list.html
  - API/StreetFoodNarrator.API/wwwroot/users.html

### 2026-04-09 | c968245 | chore: update API and mobile app logic
- Commit: c968245fee212e8caca84253e44e31ba91b27f16
- Loai: regular commit
- Thong ke: 13 files changed, 1135 insertions(+), 269 deletions(-)
- Vung anh huong: Mobile-Views: 4; Mobile-ViewModels: 2; Mobile-App: 2; Other: 2; API-Controllers: 1; Web-Static: 1; Documentation: 1
- Dien giai: Cap nhat logic backend: endpoint, service, model hoac du lieu. Dieu chinh giao dien web admin/vendor va script tuong tac. Cap nhat ung dung mobile MAUI: man hinh, state, service va tai nguyen. Bo sung cap nhat tai lieu ky thuat, huong dan test hoac mo ta thiet ke.
- File thay doi tieu bieu (13):
  - API/StreetFoodNarrator.API/Controllers/POIsController.cs
  - API/StreetFoodNarrator.API/Program.cs
  - API/StreetFoodNarrator.API/appsettings.json
  - API/StreetFoodNarrator.API/wwwroot/poi-list.html
  - Documentation/Project-Class-Diagram-Complete.puml
  - MobileApp/StreetFoodNarrator.App/AppConfig.cs
  - MobileApp/StreetFoodNarrator.App/Core/Utils/Constants.cs
  - MobileApp/StreetFoodNarrator.App/ViewModels/MainViewModel.cs
  - MobileApp/StreetFoodNarrator.App/ViewModels/POIDetailViewModel.cs
  - MobileApp/StreetFoodNarrator.App/Views/ExploreMapPage.xaml.cs
  - MobileApp/StreetFoodNarrator.App/Views/MainPage.xaml.cs
  - MobileApp/StreetFoodNarrator.App/Views/SettingsPage.xaml
  - MobileApp/StreetFoodNarrator.App/Views/SettingsPage.xaml.cs

### 2026-04-09 | caf1a47 | Merge pull request #30 from NgNguyenChuong/app_final
- Commit: caf1a4754bd077beb39594710957fa7799f84336
- Loai: merge commit
- Thong ke: 47 files changed, 4174 insertions(+), 1583 deletions(-)
- Vung anh huong: Mobile-Views: 18; Mobile-Services: 8; Mobile-App: 6; Other: 5; Documentation: 4; Mobile-ViewModels: 2; Project-Config: 2; API-Controllers: 1; Web-Static: 1
- Dien giai: Hop nhat nhanh tinh nang/develop, dong bo code va giai quyet xung dot neu co.
- File thay doi tieu bieu (47):
  - .dockerignore
  - .gitignore
  - API/StreetFoodNarrator.API/Controllers/POIsController.cs
  - API/StreetFoodNarrator.API/Program.cs
  - API/StreetFoodNarrator.API/appsettings.json
  - API/StreetFoodNarrator.API/wwwroot/poi-list.html
  - Dockerfile
  - Documentation/Geofencing-Implementation-Checklist.md
  - Documentation/Geofencing-Pseudo-Code.md
  - Documentation/Project-Class-Diagram-Complete.puml
  - Documentation/Project-Event-Flows.md
  - MobileApp/StreetFoodNarrator.App/AppConfig.cs
  - MobileApp/StreetFoodNarrator.App/Core/Services/AudioService.cs
  - MobileApp/StreetFoodNarrator.App/Core/Services/IGeofenceService.cs
  - MobileApp/StreetFoodNarrator.App/Core/Services/IServices.cs
  - MobileApp/StreetFoodNarrator.App/Core/Services/ITTSService.cs
  - MobileApp/StreetFoodNarrator.App/Core/Services/Implementations/AudioCacheService.cs
  - MobileApp/StreetFoodNarrator.App/Core/Services/Implementations/DataSyncService.cs
  - MobileApp/StreetFoodNarrator.App/Core/Services/Implementations/TextToSpeechService.cs
  - MobileApp/StreetFoodNarrator.App/Core/Services/ZoneRepository.cs
  - MobileApp/StreetFoodNarrator.App/Core/Utils/Constants.cs
  - MobileApp/StreetFoodNarrator.App/Helpers/CustomAlert.cs
  - MobileApp/StreetFoodNarrator.App/Helpers/LanguageSwitcher.cs
  - MobileApp/StreetFoodNarrator.App/MauiProgram.cs
  - MobileApp/StreetFoodNarrator.App/StreetFoodNarrator.App.csproj
  - ... va 22 file khac

### 2026-04-10 | fd48dfb | delete feature delete poi Permanant
- Commit: fd48dfb62355ac3bdc0da1e2cea15e3ca4a9d55f
- Loai: regular commit
- Thong ke: 2 files changed, 34 insertions(+), 21 deletions(-)
- Vung anh huong: Web-Static: 1; API-Controllers: 1
- Dien giai: Cap nhat logic backend: endpoint, service, model hoac du lieu. Dieu chinh giao dien web admin/vendor va script tuong tac. Bo sung/tang cap tinh nang moi trong luong su dung chinh. Loai bo chuc nang hoac code khong con su dung de giam do phuc tap. Tac dong truc tiep den cac luong nghiep vu tour/POI/thanh toan/goi VIP.
- File thay doi tieu bieu (2):
  - API/StreetFoodNarrator.API/Controllers/POIsController.cs
  - API/StreetFoodNarrator.API/wwwroot/poi-edit.html

### 2026-04-10 | 96b52fb | tour logic enhance
- Commit: 96b52fb17bace64776b2e0c287e0d283cfb05b1d
- Loai: regular commit
- Thong ke: 5 files changed, 168 insertions(+), 16 deletions(-)
- Vung anh huong: Web-Static: 3; API-Controllers: 2
- Dien giai: Cap nhat logic backend: endpoint, service, model hoac du lieu. Dieu chinh giao dien web admin/vendor va script tuong tac. Bo sung/tang cap tinh nang moi trong luong su dung chinh. Tac dong truc tiep den cac luong nghiep vu tour/POI/thanh toan/goi VIP.
- File thay doi tieu bieu (5):
  - API/StreetFoodNarrator.API/Controllers/POIsController.cs
  - API/StreetFoodNarrator.API/Controllers/ToursController.cs
  - API/StreetFoodNarrator.API/wwwroot/poi-detail.html
  - API/StreetFoodNarrator.API/wwwroot/poi-list.html
  - API/StreetFoodNarrator.API/wwwroot/tour.html

### 2026-04-10 | 06865a7 | Merge pull request #31 from NgNguyenChuong/delete_POI
- Commit: 06865a70705da78c75d0d8126cca91ba0caa10f8
- Loai: merge commit
- Thong ke: 6 files changed, 202 insertions(+), 37 deletions(-)
- Vung anh huong: Web-Static: 4; API-Controllers: 2
- Dien giai: Hop nhat nhanh tinh nang/develop, dong bo code va giai quyet xung dot neu co.
- File thay doi tieu bieu (6):
  - API/StreetFoodNarrator.API/Controllers/POIsController.cs
  - API/StreetFoodNarrator.API/Controllers/ToursController.cs
  - API/StreetFoodNarrator.API/wwwroot/poi-detail.html
  - API/StreetFoodNarrator.API/wwwroot/poi-edit.html
  - API/StreetFoodNarrator.API/wwwroot/poi-list.html
  - API/StreetFoodNarrator.API/wwwroot/tour.html

### 2026-04-10 | bb22b90 | Fix tính năng đồng bộ tb thời gian nghe từng poi
- Commit: bb22b904517e508006c67ca76dfad91f140d621d
- Loai: regular commit
- Thong ke: 11 files changed, 657 insertions(+), 415 deletions(-)
- Vung anh huong: Web-Static: 5; Mobile-Services: 2; API-Controllers: 2; API-Models: 1; Mobile-Models: 1
- Dien giai: Cap nhat logic backend: endpoint, service, model hoac du lieu. Dieu chinh giao dien web admin/vendor va script tuong tac. Cap nhat ung dung mobile MAUI: man hinh, state, service va tai nguyen. Trong tam la sua loi va dong bo hanh vi giua cac thanh phan. Tac dong truc tiep den cac luong nghiep vu tour/POI/thanh toan/goi VIP.
- File thay doi tieu bieu (11):
  - API/StreetFoodNarrator.API/Controllers/AnalyticsController.cs
  - API/StreetFoodNarrator.API/Controllers/POIsController.cs
  - API/StreetFoodNarrator.API/Models/POI.cs
  - API/StreetFoodNarrator.API/wwwroot/dashboard.html
  - API/StreetFoodNarrator.API/wwwroot/history.html
  - API/StreetFoodNarrator.API/wwwroot/js/modules.js
  - API/StreetFoodNarrator.API/wwwroot/js/ui.js
  - API/StreetFoodNarrator.API/wwwroot/users.html
  - MobileApp/StreetFoodNarrator.App/Core/Models/PoiApiModels.cs
  - MobileApp/StreetFoodNarrator.App/Core/Services/AudioService.cs
  - MobileApp/StreetFoodNarrator.App/Core/Services/Implementations/TextToSpeechService.cs

### 2026-04-10 | fc5e75b | Merge pull request #32 from NgNguyenChuong/fix_meanAudio
- Commit: fc5e75b132c80775c1a0eab3aaa73b6d68190aa4
- Loai: merge commit
- Thong ke: 11 files changed, 657 insertions(+), 415 deletions(-)
- Vung anh huong: Web-Static: 5; Mobile-Services: 2; API-Controllers: 2; API-Models: 1; Mobile-Models: 1
- Dien giai: Hop nhat nhanh tinh nang/develop, dong bo code va giai quyet xung dot neu co.
- File thay doi tieu bieu (11):
  - API/StreetFoodNarrator.API/Controllers/AnalyticsController.cs
  - API/StreetFoodNarrator.API/Controllers/POIsController.cs
  - API/StreetFoodNarrator.API/Models/POI.cs
  - API/StreetFoodNarrator.API/wwwroot/dashboard.html
  - API/StreetFoodNarrator.API/wwwroot/history.html
  - API/StreetFoodNarrator.API/wwwroot/js/modules.js
  - API/StreetFoodNarrator.API/wwwroot/js/ui.js
  - API/StreetFoodNarrator.API/wwwroot/users.html
  - MobileApp/StreetFoodNarrator.App/Core/Models/PoiApiModels.cs
  - MobileApp/StreetFoodNarrator.App/Core/Services/AudioService.cs
  - MobileApp/StreetFoodNarrator.App/Core/Services/Implementations/TextToSpeechService.cs

### 2026-04-10 | 7924337 | feat: implement QR tour functionality with modal and link handling
- Commit: 7924337b41f04da4ffea90b9a9979594c012b10e
- Loai: regular commit
- Thong ke: 3 files changed, 227 insertions(+), 35 deletions(-)
- Vung anh huong: Mobile-Views: 2; Web-Static: 1
- Dien giai: Dieu chinh giao dien web admin/vendor va script tuong tac. Cap nhat ung dung mobile MAUI: man hinh, state, service va tai nguyen. Bo sung/tang cap tinh nang moi trong luong su dung chinh. Tac dong truc tiep den cac luong nghiep vu tour/POI/thanh toan/goi VIP.
- File thay doi tieu bieu (3):
  - API/StreetFoodNarrator.API/wwwroot/tour.html
  - MobileApp/StreetFoodNarrator.App/Views/MainPage.xaml.cs
  - MobileApp/StreetFoodNarrator.App/Views/SavedPage.xaml.cs

### 2026-04-10 | 7847e96 | Merge pull request #33 from NgNguyenChuong/qr_tour
- Commit: 7847e96bda50d9e1d01b37ac755e642008c99127
- Loai: merge commit
- Thong ke: 3 files changed, 227 insertions(+), 35 deletions(-)
- Vung anh huong: Mobile-Views: 2; Web-Static: 1
- Dien giai: Hop nhat nhanh tinh nang/develop, dong bo code va giai quyet xung dot neu co.
- File thay doi tieu bieu (3):
  - API/StreetFoodNarrator.API/wwwroot/tour.html
  - MobileApp/StreetFoodNarrator.App/Views/MainPage.xaml.cs
  - MobileApp/StreetFoodNarrator.App/Views/SavedPage.xaml.cs

### 2026-04-10 | fcde6b2 | add full ds alert
- Commit: fcde6b2bf63f6ddb3339512109cd2d103a99989b
- Loai: regular commit
- Thong ke: 18 files changed, 1269 insertions(+), 165 deletions(-)
- Vung anh huong: Web-Static: 18
- Dien giai: Dieu chinh giao dien web admin/vendor va script tuong tac. Bo sung/tang cap tinh nang moi trong luong su dung chinh.
- File thay doi tieu bieu (18):
  - API/StreetFoodNarrator.API/wwwroot/api.js
  - API/StreetFoodNarrator.API/wwwroot/audio-bulk-generate.html
  - API/StreetFoodNarrator.API/wwwroot/audio-list.html
  - API/StreetFoodNarrator.API/wwwroot/history.html
  - API/StreetFoodNarrator.API/wwwroot/js/audio.js
  - API/StreetFoodNarrator.API/wwwroot/js/modules.js
  - API/StreetFoodNarrator.API/wwwroot/js/poi.js
  - API/StreetFoodNarrator.API/wwwroot/js/ui.js
  - API/StreetFoodNarrator.API/wwwroot/nav.js
  - API/StreetFoodNarrator.API/wwwroot/poi-create.html
  - API/StreetFoodNarrator.API/wwwroot/poi-detail.html
  - API/StreetFoodNarrator.API/wwwroot/poi-edit.html
  - API/StreetFoodNarrator.API/wwwroot/poi-list.html
  - API/StreetFoodNarrator.API/wwwroot/sidebar.js
  - API/StreetFoodNarrator.API/wwwroot/tour.html
  - API/StreetFoodNarrator.API/wwwroot/translation.html
  - API/StreetFoodNarrator.API/wwwroot/users.html
  - API/StreetFoodNarrator.API/wwwroot/vendors-list.html

### 2026-04-10 | 129e1a9 | Delete API/StreetFoodNarrator.API/wwwroot/history.html
- Commit: 129e1a95288387304c6ebd7d838faa019fc74e4d
- Loai: regular commit
- Thong ke: 1 file changed, 381 deletions(-)
- Vung anh huong: Web-Static: 1
- Dien giai: Dieu chinh giao dien web admin/vendor va script tuong tac. Loai bo chuc nang hoac code khong con su dung de giam do phuc tap.
- File thay doi tieu bieu (1):
  - API/StreetFoodNarrator.API/wwwroot/history.html

### 2026-04-10 | 7205e8d | Merge pull request #34 from NgNguyenChuong/design_popup
- Commit: 7205e8d9279a6fe62d127a3f579d7cdc87af73e7
- Loai: merge commit
- Thong ke: 17 files changed, 1218 insertions(+), 149 deletions(-)
- Vung anh huong: Web-Static: 17
- Dien giai: Hop nhat nhanh tinh nang/develop, dong bo code va giai quyet xung dot neu co.
- File thay doi tieu bieu (17):
  - API/StreetFoodNarrator.API/wwwroot/api.js
  - API/StreetFoodNarrator.API/wwwroot/audio-bulk-generate.html
  - API/StreetFoodNarrator.API/wwwroot/audio-list.html
  - API/StreetFoodNarrator.API/wwwroot/js/audio.js
  - API/StreetFoodNarrator.API/wwwroot/js/modules.js
  - API/StreetFoodNarrator.API/wwwroot/js/poi.js
  - API/StreetFoodNarrator.API/wwwroot/js/ui.js
  - API/StreetFoodNarrator.API/wwwroot/nav.js
  - API/StreetFoodNarrator.API/wwwroot/poi-create.html
  - API/StreetFoodNarrator.API/wwwroot/poi-detail.html
  - API/StreetFoodNarrator.API/wwwroot/poi-edit.html
  - API/StreetFoodNarrator.API/wwwroot/poi-list.html
  - API/StreetFoodNarrator.API/wwwroot/sidebar.js
  - API/StreetFoodNarrator.API/wwwroot/tour.html
  - API/StreetFoodNarrator.API/wwwroot/translation.html
  - API/StreetFoodNarrator.API/wwwroot/users.html
  - API/StreetFoodNarrator.API/wwwroot/vendors-list.html

### 2026-04-10 | 5c2748c | feat: add functionality to retrieve all POIs and update tour catalog handling
- Commit: 5c2748c2bd0b7f08969903ab05c6149e22c37be3
- Loai: regular commit
- Thong ke: 7 files changed, 185 insertions(+), 19 deletions(-)
- Vung anh huong: Mobile-Views: 4; Mobile-Services: 2; Mobile-ViewModels: 1
- Dien giai: Cap nhat ung dung mobile MAUI: man hinh, state, service va tai nguyen. Bo sung/tang cap tinh nang moi trong luong su dung chinh. Tac dong truc tiep den cac luong nghiep vu tour/POI/thanh toan/goi VIP.
- File thay doi tieu bieu (7):
  - MobileApp/StreetFoodNarrator.App/Core/Services/IServices.cs
  - MobileApp/StreetFoodNarrator.App/Core/Services/Implementations/LocalDatabaseService.cs
  - MobileApp/StreetFoodNarrator.App/ViewModels/MainViewModel.cs
  - MobileApp/StreetFoodNarrator.App/Views/Components/TabMenuView.xaml
  - MobileApp/StreetFoodNarrator.App/Views/Components/TabSavedView.xaml
  - MobileApp/StreetFoodNarrator.App/Views/TourDetailPopupPage.xaml
  - MobileApp/StreetFoodNarrator.App/Views/TourDetailPopupPage.xaml.cs

### 2026-04-10 | 845742c | Merge pull request #35 from NgNguyenChuong/fix_tourApp
- Commit: 845742c7199593a615227a23bbaaf2d840810781
- Loai: merge commit
- Thong ke: 7 files changed, 185 insertions(+), 19 deletions(-)
- Vung anh huong: Mobile-Views: 4; Mobile-Services: 2; Mobile-ViewModels: 1
- Dien giai: Hop nhat nhanh tinh nang/develop, dong bo code va giai quyet xung dot neu co.
- File thay doi tieu bieu (7):
  - MobileApp/StreetFoodNarrator.App/Core/Services/IServices.cs
  - MobileApp/StreetFoodNarrator.App/Core/Services/Implementations/LocalDatabaseService.cs
  - MobileApp/StreetFoodNarrator.App/ViewModels/MainViewModel.cs
  - MobileApp/StreetFoodNarrator.App/Views/Components/TabMenuView.xaml
  - MobileApp/StreetFoodNarrator.App/Views/Components/TabSavedView.xaml
  - MobileApp/StreetFoodNarrator.App/Views/TourDetailPopupPage.xaml
  - MobileApp/StreetFoodNarrator.App/Views/TourDetailPopupPage.xaml.cs

### 2026-04-10 | 183026b | feat: push updates for notifications and gps flow
- Commit: 183026bf6be2ce8cf7aff9bbde33a641045dfac7
- Loai: regular commit
- Thong ke: 14 files changed, 1133 insertions(+), 288 deletions(-)
- Vung anh huong: Web-Static: 4; API-Controllers: 4; Mobile-Views: 2; Mobile-Services: 2; Mobile-App: 1; Mobile-ViewModels: 1
- Dien giai: Cap nhat logic backend: endpoint, service, model hoac du lieu. Dieu chinh giao dien web admin/vendor va script tuong tac. Cap nhat ung dung mobile MAUI: man hinh, state, service va tai nguyen. Bo sung/tang cap tinh nang moi trong luong su dung chinh.
- File thay doi tieu bieu (14):
  - API/StreetFoodNarrator.API/Controllers/AnalyticsController.cs
  - API/StreetFoodNarrator.API/Controllers/AudioController.cs
  - API/StreetFoodNarrator.API/Controllers/NotificationsController.cs
  - API/StreetFoodNarrator.API/Controllers/POIsController.cs
  - API/StreetFoodNarrator.API/wwwroot/api.js
  - API/StreetFoodNarrator.API/wwwroot/audio-list.html
  - API/StreetFoodNarrator.API/wwwroot/sidebar.js
  - API/StreetFoodNarrator.API/wwwroot/users.html
  - MobileApp/StreetFoodNarrator.App/Core/Services/IGeofenceService.cs
  - MobileApp/StreetFoodNarrator.App/Core/Services/LocationService.cs
  - MobileApp/StreetFoodNarrator.App/Core/Utils/VinhKhanhAreaGuard.cs
  - MobileApp/StreetFoodNarrator.App/ViewModels/POIDetailViewModel.cs
  - MobileApp/StreetFoodNarrator.App/Views/SettingsPage.xaml
  - MobileApp/StreetFoodNarrator.App/Views/SettingsPage.xaml.cs

### 2026-04-10 | 6b35131 | Merge pull request #36 from NgNguyenChuong/fix_userRoute
- Commit: 6b35131001ea4f0bfa9318ff038c902ad9beb75b
- Loai: merge commit
- Thong ke: 14 files changed, 1133 insertions(+), 288 deletions(-)
- Vung anh huong: Web-Static: 4; API-Controllers: 4; Mobile-Views: 2; Mobile-Services: 2; Mobile-App: 1; Mobile-ViewModels: 1
- Dien giai: Hop nhat nhanh tinh nang/develop, dong bo code va giai quyet xung dot neu co.
- File thay doi tieu bieu (14):
  - API/StreetFoodNarrator.API/Controllers/AnalyticsController.cs
  - API/StreetFoodNarrator.API/Controllers/AudioController.cs
  - API/StreetFoodNarrator.API/Controllers/NotificationsController.cs
  - API/StreetFoodNarrator.API/Controllers/POIsController.cs
  - API/StreetFoodNarrator.API/wwwroot/api.js
  - API/StreetFoodNarrator.API/wwwroot/audio-list.html
  - API/StreetFoodNarrator.API/wwwroot/sidebar.js
  - API/StreetFoodNarrator.API/wwwroot/users.html
  - MobileApp/StreetFoodNarrator.App/Core/Services/IGeofenceService.cs
  - MobileApp/StreetFoodNarrator.App/Core/Services/LocationService.cs
  - MobileApp/StreetFoodNarrator.App/Core/Utils/VinhKhanhAreaGuard.cs
  - MobileApp/StreetFoodNarrator.App/ViewModels/POIDetailViewModel.cs
  - MobileApp/StreetFoodNarrator.App/Views/SettingsPage.xaml
  - MobileApp/StreetFoodNarrator.App/Views/SettingsPage.xaml.cs

### 2026-04-10 | 9b5e8e3 | Đăng ký vip cho app và cho phép tour có free hay không
- Commit: 9b5e8e3dcdb3b9dc93e7906873f6ab3a9ced1288
- Loai: regular commit
- Thong ke: 26 files changed, 2210 insertions(+), 152 deletions(-)
- Vung anh huong: Mobile-Views: 16; API-Controllers: 2; Mobile-ViewModels: 2; API-Data: 2; API-Models: 2; Web-Static: 1; Mobile-App: 1
- Dien giai: Cap nhat logic backend: endpoint, service, model hoac du lieu. Dieu chinh giao dien web admin/vendor va script tuong tac. Cap nhat ung dung mobile MAUI: man hinh, state, service va tai nguyen. Tac dong truc tiep den cac luong nghiep vu tour/POI/thanh toan/goi VIP.
- File thay doi tieu bieu (26):
  - API/StreetFoodNarrator.API/Controllers/SubscriptionsController.cs
  - API/StreetFoodNarrator.API/Controllers/ToursController.cs
  - API/StreetFoodNarrator.API/Data/DbInitializer.cs
  - API/StreetFoodNarrator.API/Data/MongoDbContext.cs
  - API/StreetFoodNarrator.API/Models/DeviceSubscription.cs
  - API/StreetFoodNarrator.API/Models/Tour.cs
  - API/StreetFoodNarrator.API/wwwroot/tour.html
  - MobileApp/StreetFoodNarrator.App/Core/Utils/VipAudioPreviewGate.cs
  - MobileApp/StreetFoodNarrator.App/ViewModels/MainViewModel.cs
  - MobileApp/StreetFoodNarrator.App/ViewModels/POIDetailViewModel.cs
  - MobileApp/StreetFoodNarrator.App/Views/Components/TabMapView.xaml
  - MobileApp/StreetFoodNarrator.App/Views/Components/TabMenuView.xaml
  - MobileApp/StreetFoodNarrator.App/Views/Components/TabMenuView.xaml.cs
  - MobileApp/StreetFoodNarrator.App/Views/Components/TabSavedView.xaml
  - MobileApp/StreetFoodNarrator.App/Views/Components/TabSavedView.xaml.cs
  - MobileApp/StreetFoodNarrator.App/Views/Components/TabTourView.xaml
  - MobileApp/StreetFoodNarrator.App/Views/Components/VirtualModeView.xaml
  - MobileApp/StreetFoodNarrator.App/Views/ExploreMapPage.xaml
  - MobileApp/StreetFoodNarrator.App/Views/ExploreMapPage.xaml.cs
  - MobileApp/StreetFoodNarrator.App/Views/MainPage.PinPopup.cs
  - MobileApp/StreetFoodNarrator.App/Views/MainPage.Tour.cs
  - MobileApp/StreetFoodNarrator.App/Views/MainPage.xaml
  - MobileApp/StreetFoodNarrator.App/Views/MainPage.xaml.cs
  - MobileApp/StreetFoodNarrator.App/Views/PremiumTourPaywallPage.xaml
  - MobileApp/StreetFoodNarrator.App/Views/PremiumTourPaywallPage.xaml.cs
  - ... va 1 file khac

### 2026-04-10 | efddf8f | Merge remote-tracking branch 'origin/develop' into registerVip
- Commit: efddf8fb508229fe294785641ff6bb8bfb468a2f
- Loai: merge commit
- Thong ke: 17 files changed, 1218 insertions(+), 149 deletions(-)
- Vung anh huong: Web-Static: 17
- Dien giai: Hop nhat nhanh tinh nang/develop, dong bo code va giai quyet xung dot neu co.
- File thay doi tieu bieu (17):
  - API/StreetFoodNarrator.API/wwwroot/api.js
  - API/StreetFoodNarrator.API/wwwroot/audio-bulk-generate.html
  - API/StreetFoodNarrator.API/wwwroot/audio-list.html
  - API/StreetFoodNarrator.API/wwwroot/js/audio.js
  - API/StreetFoodNarrator.API/wwwroot/js/modules.js
  - API/StreetFoodNarrator.API/wwwroot/js/poi.js
  - API/StreetFoodNarrator.API/wwwroot/js/ui.js
  - API/StreetFoodNarrator.API/wwwroot/nav.js
  - API/StreetFoodNarrator.API/wwwroot/poi-create.html
  - API/StreetFoodNarrator.API/wwwroot/poi-detail.html
  - API/StreetFoodNarrator.API/wwwroot/poi-edit.html
  - API/StreetFoodNarrator.API/wwwroot/poi-list.html
  - API/StreetFoodNarrator.API/wwwroot/sidebar.js
  - API/StreetFoodNarrator.API/wwwroot/tour.html
  - API/StreetFoodNarrator.API/wwwroot/translation.html
  - API/StreetFoodNarrator.API/wwwroot/users.html
  - API/StreetFoodNarrator.API/wwwroot/vendors-list.html

### 2026-04-10 | 3d740ed | submision billing feature
- Commit: 3d740edd9460743f7fe67e759fb3bc363c80229f
- Loai: regular commit
- Thong ke: 14 files changed, 1302 insertions(+), 16 deletions(-)
- Vung anh huong: Web-Static: 7; API-Controllers: 3; API-Data: 2; API-Models: 2
- Dien giai: Cap nhat logic backend: endpoint, service, model hoac du lieu. Dieu chinh giao dien web admin/vendor va script tuong tac. Bo sung/tang cap tinh nang moi trong luong su dung chinh.
- File thay doi tieu bieu (14):
  - API/StreetFoodNarrator.API/Controllers/POIsController.cs
  - API/StreetFoodNarrator.API/Controllers/PaymentsController.cs
  - API/StreetFoodNarrator.API/Controllers/ToursController.cs
  - API/StreetFoodNarrator.API/Data/DbInitializer.cs
  - API/StreetFoodNarrator.API/Data/MongoDbContext.cs
  - API/StreetFoodNarrator.API/Models/ServiceSubmission.cs
  - API/StreetFoodNarrator.API/Models/VendorProfile.cs
  - API/StreetFoodNarrator.API/wwwroot/api.js
  - API/StreetFoodNarrator.API/wwwroot/audio-bulk-generate.html
  - API/StreetFoodNarrator.API/wwwroot/audio-list.html
  - API/StreetFoodNarrator.API/wwwroot/payment-management.html
  - API/StreetFoodNarrator.API/wwwroot/poi-create.html
  - API/StreetFoodNarrator.API/wwwroot/sidebar.js
  - API/StreetFoodNarrator.API/wwwroot/tour.html

### 2026-04-10 | 6d9d415 | submision billing feature
- Commit: 6d9d41575d7973fc7b91b5bfb1a2bc0036638899
- Loai: regular commit
- Thong ke: 5 files changed, 203 insertions(+)
- Vung anh huong: Web-Static: 2; API-Controllers: 1; API-Services: 1; Other: 1
- Dien giai: Cap nhat logic backend: endpoint, service, model hoac du lieu. Dieu chinh giao dien web admin/vendor va script tuong tac. Bo sung/tang cap tinh nang moi trong luong su dung chinh.
- File thay doi tieu bieu (5):
  - API/StreetFoodNarrator.API/Controllers/PaymentsController.cs
  - API/StreetFoodNarrator.API/Program.cs
  - API/StreetFoodNarrator.API/Services/PremiumExpiryMonitorService.cs
  - API/StreetFoodNarrator.API/wwwroot/api.js
  - API/StreetFoodNarrator.API/wwwroot/payment-management.html

### 2026-04-10 | 8511ecc | Merge pull request #37 from NgNguyenChuong/submision_POI
- Commit: 8511ecc428df66c9773e14de3fa79991c6c99f2f
- Loai: merge commit
- Thong ke: 16 files changed, 1505 insertions(+), 16 deletions(-)
- Vung anh huong: Web-Static: 7; API-Controllers: 3; API-Models: 2; API-Data: 2; Other: 1; API-Services: 1
- Dien giai: Hop nhat nhanh tinh nang/develop, dong bo code va giai quyet xung dot neu co.
- File thay doi tieu bieu (16):
  - API/StreetFoodNarrator.API/Controllers/POIsController.cs
  - API/StreetFoodNarrator.API/Controllers/PaymentsController.cs
  - API/StreetFoodNarrator.API/Controllers/ToursController.cs
  - API/StreetFoodNarrator.API/Data/DbInitializer.cs
  - API/StreetFoodNarrator.API/Data/MongoDbContext.cs
  - API/StreetFoodNarrator.API/Models/ServiceSubmission.cs
  - API/StreetFoodNarrator.API/Models/VendorProfile.cs
  - API/StreetFoodNarrator.API/Program.cs
  - API/StreetFoodNarrator.API/Services/PremiumExpiryMonitorService.cs
  - API/StreetFoodNarrator.API/wwwroot/api.js
  - API/StreetFoodNarrator.API/wwwroot/audio-bulk-generate.html
  - API/StreetFoodNarrator.API/wwwroot/audio-list.html
  - API/StreetFoodNarrator.API/wwwroot/payment-management.html
  - API/StreetFoodNarrator.API/wwwroot/poi-create.html
  - API/StreetFoodNarrator.API/wwwroot/sidebar.js
  - API/StreetFoodNarrator.API/wwwroot/tour.html

### 2026-04-10 | 659d910 | feat: enhance VIP subscription features and UI updates across the app
- Commit: 659d91074cca320c3636b1aaed6db87e871ab1ab
- Loai: regular commit
- Thong ke: 9 files changed, 313 insertions(+), 67 deletions(-)
- Vung anh huong: Mobile-Views: 6; API-Controllers: 1; Mobile-ViewModels: 1; Web-Static: 1
- Dien giai: Cap nhat logic backend: endpoint, service, model hoac du lieu. Dieu chinh giao dien web admin/vendor va script tuong tac. Cap nhat ung dung mobile MAUI: man hinh, state, service va tai nguyen. Bo sung/tang cap tinh nang moi trong luong su dung chinh. Tac dong truc tiep den cac luong nghiep vu tour/POI/thanh toan/goi VIP.
- File thay doi tieu bieu (9):
  - API/StreetFoodNarrator.API/Controllers/ToursController.cs
  - API/StreetFoodNarrator.API/wwwroot/tour.html
  - MobileApp/StreetFoodNarrator.App/ViewModels/MainViewModel.cs
  - MobileApp/StreetFoodNarrator.App/Views/Components/TabMenuView.xaml
  - MobileApp/StreetFoodNarrator.App/Views/MainPage.xaml.cs
  - MobileApp/StreetFoodNarrator.App/Views/PremiumTourPaywallPage.xaml
  - MobileApp/StreetFoodNarrator.App/Views/PremiumTourPaywallPage.xaml.cs
  - MobileApp/StreetFoodNarrator.App/Views/SettingsPage.xaml
  - MobileApp/StreetFoodNarrator.App/Views/SettingsPage.xaml.cs

### 2026-04-10 | 24ded5e | Merge branch 'develop' into registerVip
- Commit: 24ded5e8b92cdd233484af380504d860e98083e8
- Loai: merge commit
- Thong ke: 16 files changed, 1504 insertions(+), 16 deletions(-)
- Vung anh huong: Web-Static: 7; API-Controllers: 3; API-Models: 2; API-Data: 2; Other: 1; API-Services: 1
- Dien giai: Hop nhat nhanh tinh nang/develop, dong bo code va giai quyet xung dot neu co.
- File thay doi tieu bieu (16):
  - API/StreetFoodNarrator.API/Controllers/POIsController.cs
  - API/StreetFoodNarrator.API/Controllers/PaymentsController.cs
  - API/StreetFoodNarrator.API/Controllers/ToursController.cs
  - API/StreetFoodNarrator.API/Data/DbInitializer.cs
  - API/StreetFoodNarrator.API/Data/MongoDbContext.cs
  - API/StreetFoodNarrator.API/Models/ServiceSubmission.cs
  - API/StreetFoodNarrator.API/Models/VendorProfile.cs
  - API/StreetFoodNarrator.API/Program.cs
  - API/StreetFoodNarrator.API/Services/PremiumExpiryMonitorService.cs
  - API/StreetFoodNarrator.API/wwwroot/api.js
  - API/StreetFoodNarrator.API/wwwroot/audio-bulk-generate.html
  - API/StreetFoodNarrator.API/wwwroot/audio-list.html
  - API/StreetFoodNarrator.API/wwwroot/payment-management.html
  - API/StreetFoodNarrator.API/wwwroot/poi-create.html
  - API/StreetFoodNarrator.API/wwwroot/sidebar.js
  - API/StreetFoodNarrator.API/wwwroot/tour.html

### 2026-04-10 | 8d43639 | Merge pull request #38 from NgNguyenChuong/registerVip
- Commit: 8d43639e3beacd9b8f927cf6a0afc998807c7d69
- Loai: merge commit
- Thong ke: 28 files changed, 2486 insertions(+), 183 deletions(-)
- Vung anh huong: Mobile-Views: 18; API-Controllers: 2; Mobile-ViewModels: 2; API-Data: 2; API-Models: 2; Web-Static: 1; Mobile-App: 1
- Dien giai: Hop nhat nhanh tinh nang/develop, dong bo code va giai quyet xung dot neu co.
- File thay doi tieu bieu (28):
  - API/StreetFoodNarrator.API/Controllers/SubscriptionsController.cs
  - API/StreetFoodNarrator.API/Controllers/ToursController.cs
  - API/StreetFoodNarrator.API/Data/DbInitializer.cs
  - API/StreetFoodNarrator.API/Data/MongoDbContext.cs
  - API/StreetFoodNarrator.API/Models/DeviceSubscription.cs
  - API/StreetFoodNarrator.API/Models/Tour.cs
  - API/StreetFoodNarrator.API/wwwroot/tour.html
  - MobileApp/StreetFoodNarrator.App/Core/Utils/VipAudioPreviewGate.cs
  - MobileApp/StreetFoodNarrator.App/ViewModels/MainViewModel.cs
  - MobileApp/StreetFoodNarrator.App/ViewModels/POIDetailViewModel.cs
  - MobileApp/StreetFoodNarrator.App/Views/Components/TabMapView.xaml
  - MobileApp/StreetFoodNarrator.App/Views/Components/TabMenuView.xaml
  - MobileApp/StreetFoodNarrator.App/Views/Components/TabMenuView.xaml.cs
  - MobileApp/StreetFoodNarrator.App/Views/Components/TabSavedView.xaml
  - MobileApp/StreetFoodNarrator.App/Views/Components/TabSavedView.xaml.cs
  - MobileApp/StreetFoodNarrator.App/Views/Components/TabTourView.xaml
  - MobileApp/StreetFoodNarrator.App/Views/Components/VirtualModeView.xaml
  - MobileApp/StreetFoodNarrator.App/Views/ExploreMapPage.xaml
  - MobileApp/StreetFoodNarrator.App/Views/ExploreMapPage.xaml.cs
  - MobileApp/StreetFoodNarrator.App/Views/MainPage.PinPopup.cs
  - MobileApp/StreetFoodNarrator.App/Views/MainPage.Tour.cs
  - MobileApp/StreetFoodNarrator.App/Views/MainPage.xaml
  - MobileApp/StreetFoodNarrator.App/Views/MainPage.xaml.cs
  - MobileApp/StreetFoodNarrator.App/Views/PremiumTourPaywallPage.xaml
  - MobileApp/StreetFoodNarrator.App/Views/PremiumTourPaywallPage.xaml.cs
  - ... va 3 file khac

### 2026-04-11 | 07ab7c0 | Premium: update registration flow, admin management UI, and notifications
- Commit: 07ab7c0bdedd3de2ae2b537e41bddbdbb97f7740
- Loai: regular commit
- Thong ke: 22 files changed, 1403 insertions(+), 227 deletions(-)
- Vung anh huong: Mobile-Views: 10; Mobile-Resources: 4; Web-Static: 3; API-Controllers: 2; Mobile-App: 2; Mobile-ViewModels: 1
- Dien giai: Cap nhat logic backend: endpoint, service, model hoac du lieu. Dieu chinh giao dien web admin/vendor va script tuong tac. Cap nhat ung dung mobile MAUI: man hinh, state, service va tai nguyen.
- File thay doi tieu bieu (22):
  - API/StreetFoodNarrator.API/Controllers/PaymentsController.cs
  - API/StreetFoodNarrator.API/Controllers/SubscriptionsController.cs
  - API/StreetFoodNarrator.API/wwwroot/api.js
  - API/StreetFoodNarrator.API/wwwroot/payment-management.html
  - API/StreetFoodNarrator.API/wwwroot/sidebar.js
  - MobileApp/StreetFoodNarrator.App/Helpers/CustomAlert.cs
  - MobileApp/StreetFoodNarrator.App/Helpers/UserPreferenceEffects.cs
  - MobileApp/StreetFoodNarrator.App/Resources/Strings/AppStrings.en.resx
  - MobileApp/StreetFoodNarrator.App/Resources/Strings/AppStrings.resx
  - MobileApp/StreetFoodNarrator.App/Resources/Strings/AppStrings.vi.resx
  - MobileApp/StreetFoodNarrator.App/Resources/Strings/AppStrings.zh.resx
  - MobileApp/StreetFoodNarrator.App/ViewModels/MainViewModel.cs
  - MobileApp/StreetFoodNarrator.App/Views/Components/BottomNavView.xaml.cs
  - MobileApp/StreetFoodNarrator.App/Views/Components/FloatingBottomNav.xaml.cs
  - MobileApp/StreetFoodNarrator.App/Views/Components/TabSavedView.xaml
  - MobileApp/StreetFoodNarrator.App/Views/MainPage.xaml.cs
  - MobileApp/StreetFoodNarrator.App/Views/POIDetailPage.xaml.cs
  - MobileApp/StreetFoodNarrator.App/Views/PremiumTourPaywallPage.xaml
  - MobileApp/StreetFoodNarrator.App/Views/PremiumTourPaywallPage.xaml.cs
  - MobileApp/StreetFoodNarrator.App/Views/SettingsPage.xaml
  - MobileApp/StreetFoodNarrator.App/Views/SettingsPage.xaml.cs
  - MobileApp/StreetFoodNarrator.App/Views/WelcomePage.xaml.cs

### 2026-04-11 | 31f9cef | Update QR for app and release apk
- Commit: 31f9cef33c06bfb5e589ac7bf69fc58d145d7685
- Loai: regular commit
- Thong ke: 21 files changed, 3592 insertions(+), 297 deletions(-)
- Vung anh huong: Web-Static: 5; Mobile-Services: 4; Mobile-App: 3; Mobile-Models: 2; API-Controllers: 2; Other: 2; API-Models: 1; API-Data: 1; Documentation: 1
- Dien giai: Cap nhat logic backend: endpoint, service, model hoac du lieu. Dieu chinh giao dien web admin/vendor va script tuong tac. Cap nhat ung dung mobile MAUI: man hinh, state, service va tai nguyen. Bo sung cap nhat tai lieu ky thuat, huong dan test hoac mo ta thiet ke. Lien quan den dong goi/release va san sang phat hanh. Tac dong truc tiep den cac luong nghiep vu tour/POI/thanh toan/goi VIP.
- File thay doi tieu bieu (21):
  - API/StreetFoodNarrator.API/Controllers/POIsController.cs
  - API/StreetFoodNarrator.API/Controllers/QrController.cs
  - API/StreetFoodNarrator.API/Data/MongoDbContext.cs
  - API/StreetFoodNarrator.API/Models/QrCampaignState.cs
  - API/StreetFoodNarrator.API/Program.cs
  - API/StreetFoodNarrator.API/appsettings.json
  - API/StreetFoodNarrator.API/wwwroot/apk-download.html
  - API/StreetFoodNarrator.API/wwwroot/dashboard.html
  - API/StreetFoodNarrator.API/wwwroot/poi-create.html
  - API/StreetFoodNarrator.API/wwwroot/poi-edit.html
  - API/StreetFoodNarrator.API/wwwroot/users.html
  - Documentation/Project-Event-Flows.md
  - MobileApp/StreetFoodNarrator.App/App.xaml.cs
  - MobileApp/StreetFoodNarrator.App/AppConfig.cs
  - MobileApp/StreetFoodNarrator.App/AppShell.xaml.cs
  - MobileApp/StreetFoodNarrator.App/Core/Models/POI.cs
  - MobileApp/StreetFoodNarrator.App/Core/Models/PoiApiModels.cs
  - MobileApp/StreetFoodNarrator.App/Core/Services/AudioService.cs
  - MobileApp/StreetFoodNarrator.App/Core/Services/IGeofenceService.cs
  - MobileApp/StreetFoodNarrator.App/Core/Services/LocationService.cs
  - MobileApp/StreetFoodNarrator.App/Core/Services/ZoneRepository.cs

### 2026-04-11 | fc26551 | Update QR for app and release apk
- Commit: fc26551a20cd9f6ed98ee51511f7228ad536e3ce
- Loai: regular commit
- Thong ke: 0 files changed, 0 insertions(+), 0 deletions(-)
- Vung anh huong: Other: 0
- Dien giai: Lien quan den dong goi/release va san sang phat hanh. Tac dong truc tiep den cac luong nghiep vu tour/POI/thanh toan/goi VIP.
- File thay doi: khong co file thay doi truc tiep (chu yeu merge).

### 2026-04-12 | 4e80a12 | Merge pull request #39 from NgNguyenChuong/registerVip
- Commit: 4e80a126f0bcab15b033b79d403acfb9427fe997
- Loai: merge commit
- Thong ke: 22 files changed, 1403 insertions(+), 227 deletions(-)
- Vung anh huong: Mobile-Views: 10; Mobile-Resources: 4; Web-Static: 3; API-Controllers: 2; Mobile-App: 2; Mobile-ViewModels: 1
- Dien giai: Hop nhat nhanh tinh nang/develop, dong bo code va giai quyet xung dot neu co.
- File thay doi tieu bieu (22):
  - API/StreetFoodNarrator.API/Controllers/PaymentsController.cs
  - API/StreetFoodNarrator.API/Controllers/SubscriptionsController.cs
  - API/StreetFoodNarrator.API/wwwroot/api.js
  - API/StreetFoodNarrator.API/wwwroot/payment-management.html
  - API/StreetFoodNarrator.API/wwwroot/sidebar.js
  - MobileApp/StreetFoodNarrator.App/Helpers/CustomAlert.cs
  - MobileApp/StreetFoodNarrator.App/Helpers/UserPreferenceEffects.cs
  - MobileApp/StreetFoodNarrator.App/Resources/Strings/AppStrings.en.resx
  - MobileApp/StreetFoodNarrator.App/Resources/Strings/AppStrings.resx
  - MobileApp/StreetFoodNarrator.App/Resources/Strings/AppStrings.vi.resx
  - MobileApp/StreetFoodNarrator.App/Resources/Strings/AppStrings.zh.resx
  - MobileApp/StreetFoodNarrator.App/ViewModels/MainViewModel.cs
  - MobileApp/StreetFoodNarrator.App/Views/Components/BottomNavView.xaml.cs
  - MobileApp/StreetFoodNarrator.App/Views/Components/FloatingBottomNav.xaml.cs
  - MobileApp/StreetFoodNarrator.App/Views/Components/TabSavedView.xaml
  - MobileApp/StreetFoodNarrator.App/Views/MainPage.xaml.cs
  - MobileApp/StreetFoodNarrator.App/Views/POIDetailPage.xaml.cs
  - MobileApp/StreetFoodNarrator.App/Views/PremiumTourPaywallPage.xaml
  - MobileApp/StreetFoodNarrator.App/Views/PremiumTourPaywallPage.xaml.cs
  - MobileApp/StreetFoodNarrator.App/Views/SettingsPage.xaml
  - MobileApp/StreetFoodNarrator.App/Views/SettingsPage.xaml.cs
  - MobileApp/StreetFoodNarrator.App/Views/WelcomePage.xaml.cs

### 2026-04-12 | 60cb530 | Merge pull request #40 from NgNguyenChuong/releaseApp
- Commit: 60cb5304374167132264b1e0e51e0e17310a2875
- Loai: merge commit
- Thong ke: 21 files changed, 3592 insertions(+), 297 deletions(-)
- Vung anh huong: Web-Static: 5; Mobile-Services: 4; Mobile-App: 3; Mobile-Models: 2; API-Controllers: 2; Other: 2; API-Models: 1; API-Data: 1; Documentation: 1
- Dien giai: Hop nhat nhanh tinh nang/develop, dong bo code va giai quyet xung dot neu co.
- File thay doi tieu bieu (21):
  - API/StreetFoodNarrator.API/Controllers/POIsController.cs
  - API/StreetFoodNarrator.API/Controllers/QrController.cs
  - API/StreetFoodNarrator.API/Data/MongoDbContext.cs
  - API/StreetFoodNarrator.API/Models/QrCampaignState.cs
  - API/StreetFoodNarrator.API/Program.cs
  - API/StreetFoodNarrator.API/appsettings.json
  - API/StreetFoodNarrator.API/wwwroot/apk-download.html
  - API/StreetFoodNarrator.API/wwwroot/dashboard.html
  - API/StreetFoodNarrator.API/wwwroot/poi-create.html
  - API/StreetFoodNarrator.API/wwwroot/poi-edit.html
  - API/StreetFoodNarrator.API/wwwroot/users.html
  - Documentation/Project-Event-Flows.md
  - MobileApp/StreetFoodNarrator.App/App.xaml.cs
  - MobileApp/StreetFoodNarrator.App/AppConfig.cs
  - MobileApp/StreetFoodNarrator.App/AppShell.xaml.cs
  - MobileApp/StreetFoodNarrator.App/Core/Models/POI.cs
  - MobileApp/StreetFoodNarrator.App/Core/Models/PoiApiModels.cs
  - MobileApp/StreetFoodNarrator.App/Core/Services/AudioService.cs
  - MobileApp/StreetFoodNarrator.App/Core/Services/IGeofenceService.cs
  - MobileApp/StreetFoodNarrator.App/Core/Services/LocationService.cs
  - MobileApp/StreetFoodNarrator.App/Core/Services/ZoneRepository.cs

### 2026-04-12 | 311c4c5 | temp
- Commit: 311c4c5f6cfddd72f233a0aed4ed3f1731ded4cc
- Loai: regular commit
- Thong ke: 13 files changed, 53 insertions(+), 47 deletions(-)
- Vung anh huong: Web-Static: 9; Database: 1; Mobile-App: 1; Other: 1; API-Data: 1
- Dien giai: Cap nhat logic backend: endpoint, service, model hoac du lieu. Dieu chinh giao dien web admin/vendor va script tuong tac. Tinh chinh schema/seed/du lieu mau phuc vu API va ung dung.
- File thay doi tieu bieu (13):
  - API/StreetFoodNarrator.API/Data/DbInitializer.cs
  - API/StreetFoodNarrator.API/Program.cs
  - API/StreetFoodNarrator.API/wwwroot/audio-bulk-generate.html
  - API/StreetFoodNarrator.API/wwwroot/auth-check.js
  - API/StreetFoodNarrator.API/wwwroot/frontend_backup/audio-bulk-generate.html
  - API/StreetFoodNarrator.API/wwwroot/frontend_backup/auth-check.js
  - API/StreetFoodNarrator.API/wwwroot/frontend_backup/poi-create.html
  - API/StreetFoodNarrator.API/wwwroot/frontend_backup/vendor-dashboard.html
  - API/StreetFoodNarrator.API/wwwroot/js/modules.js
  - API/StreetFoodNarrator.API/wwwroot/poi-create.html
  - API/StreetFoodNarrator.API/wwwroot/poi-detail.html
  - Database/nested-zones-seed.js
  - MobileApp/StreetFoodNarrator.App/AppConfig.cs

### 2026-04-12 | 03170f6 | temp
- Commit: 03170f62ff0d41a081d2783e7e3c2fa1de580706
- Loai: merge commit
- Thong ke: 43 files changed, 4995 insertions(+), 524 deletions(-)
- Vung anh huong: Mobile-Views: 10; Web-Static: 8; Mobile-App: 5; Mobile-Services: 4; Mobile-Resources: 4; API-Controllers: 4; Mobile-Models: 2; Other: 2; Mobile-ViewModels: 1; Documentation: 1; API-Models: 1; API-Data: 1
- Dien giai: Hop nhat nhanh tinh nang/develop, dong bo code va giai quyet xung dot neu co.
- File thay doi tieu bieu (43):
  - API/StreetFoodNarrator.API/Controllers/POIsController.cs
  - API/StreetFoodNarrator.API/Controllers/PaymentsController.cs
  - API/StreetFoodNarrator.API/Controllers/QrController.cs
  - API/StreetFoodNarrator.API/Controllers/SubscriptionsController.cs
  - API/StreetFoodNarrator.API/Data/MongoDbContext.cs
  - API/StreetFoodNarrator.API/Models/QrCampaignState.cs
  - API/StreetFoodNarrator.API/Program.cs
  - API/StreetFoodNarrator.API/appsettings.json
  - API/StreetFoodNarrator.API/wwwroot/api.js
  - API/StreetFoodNarrator.API/wwwroot/apk-download.html
  - API/StreetFoodNarrator.API/wwwroot/dashboard.html
  - API/StreetFoodNarrator.API/wwwroot/payment-management.html
  - API/StreetFoodNarrator.API/wwwroot/poi-create.html
  - API/StreetFoodNarrator.API/wwwroot/poi-edit.html
  - API/StreetFoodNarrator.API/wwwroot/sidebar.js
  - API/StreetFoodNarrator.API/wwwroot/users.html
  - Documentation/Project-Event-Flows.md
  - MobileApp/StreetFoodNarrator.App/App.xaml.cs
  - MobileApp/StreetFoodNarrator.App/AppConfig.cs
  - MobileApp/StreetFoodNarrator.App/AppShell.xaml.cs
  - MobileApp/StreetFoodNarrator.App/Core/Models/POI.cs
  - MobileApp/StreetFoodNarrator.App/Core/Models/PoiApiModels.cs
  - MobileApp/StreetFoodNarrator.App/Core/Services/AudioService.cs
  - MobileApp/StreetFoodNarrator.App/Core/Services/IGeofenceService.cs
  - MobileApp/StreetFoodNarrator.App/Core/Services/LocationService.cs
  - ... va 18 file khac

### 2026-04-12 | dc974a7 | review submision feature
- Commit: dc974a7ac60e041055dcc4c159fd2576188a8add
- Loai: regular commit
- Thong ke: 6 files changed, 219 insertions(+), 57 deletions(-)
- Vung anh huong: Web-Static: 5; API-Controllers: 1
- Dien giai: Cap nhat logic backend: endpoint, service, model hoac du lieu. Dieu chinh giao dien web admin/vendor va script tuong tac. Bo sung/tang cap tinh nang moi trong luong su dung chinh.
- File thay doi tieu bieu (6):
  - API/StreetFoodNarrator.API/Controllers/PaymentsController.cs
  - API/StreetFoodNarrator.API/wwwroot/audio-list.html
  - API/StreetFoodNarrator.API/wwwroot/payment-management.html
  - API/StreetFoodNarrator.API/wwwroot/poi-list.html
  - API/StreetFoodNarrator.API/wwwroot/sidebar.js
  - API/StreetFoodNarrator.API/wwwroot/tour.html

### 2026-04-12 | e40d05a | delete all audio rejected
- Commit: e40d05a06107d5158631c5be7dab37a7efe99bb6
- Loai: regular commit
- Thong ke: 1 file changed, 42 deletions(-)
- Vung anh huong: Web-Static: 1
- Dien giai: Dieu chinh giao dien web admin/vendor va script tuong tac. Loai bo chuc nang hoac code khong con su dung de giam do phuc tap. Dieu chinh luong audio/TTS va cac man hinh lien quan.
- File thay doi tieu bieu (1):
  - API/StreetFoodNarrator.API/wwwroot/audio-list.html

### 2026-04-12 | 25a072f | Enhance vendor profile management and localization support
- Commit: 25a072fccf25a659bc7153164580d396ed970483
- Loai: regular commit
- Thong ke: 11 files changed, 1139 insertions(+), 152 deletions(-)
- Vung anh huong: Web-Static: 5; API-Controllers: 3; Mobile-Services: 1; API-Models: 1; Other: 1
- Dien giai: Cap nhat logic backend: endpoint, service, model hoac du lieu. Dieu chinh giao dien web admin/vendor va script tuong tac. Cap nhat ung dung mobile MAUI: man hinh, state, service va tai nguyen. Bo sung/tang cap tinh nang moi trong luong su dung chinh. Mo rong da ngon ngu va noi dia hoa noi dung hien thi.
- File thay doi tieu bieu (11):
  - API/StreetFoodNarrator.API/Controllers/AnalyticsController.cs
  - API/StreetFoodNarrator.API/Controllers/AuthController.cs
  - API/StreetFoodNarrator.API/Controllers/VendorsController.cs
  - API/StreetFoodNarrator.API/Models/NarrationLog.cs
  - API/StreetFoodNarrator.API/Program.cs
  - API/StreetFoodNarrator.API/wwwroot/api.js
  - API/StreetFoodNarrator.API/wwwroot/index.html
  - API/StreetFoodNarrator.API/wwwroot/register.html
  - API/StreetFoodNarrator.API/wwwroot/users.html
  - API/StreetFoodNarrator.API/wwwroot/vendor-profile.html
  - MobileApp/StreetFoodNarrator.App/Core/Services/IGeofenceService.cs

### 2026-04-12 | 2898857 | logical audio gen
- Commit: 2898857359519a8cd13e2882b7324f52187fabfb
- Loai: regular commit
- Thong ke: 7 files changed, 147 insertions(+), 150 deletions(-)
- Vung anh huong: Web-Static: 5; API-Controllers: 2
- Dien giai: Cap nhat logic backend: endpoint, service, model hoac du lieu. Dieu chinh giao dien web admin/vendor va script tuong tac. Trong tam la sua loi va dong bo hanh vi giua cac thanh phan. Dieu chinh luong audio/TTS va cac man hinh lien quan.
- File thay doi tieu bieu (7):
  - API/StreetFoodNarrator.API/Controllers/POIsController.cs
  - API/StreetFoodNarrator.API/Controllers/TranslationsController.cs
  - API/StreetFoodNarrator.API/wwwroot/audio-bulk-generate.html
  - API/StreetFoodNarrator.API/wwwroot/dashboard.html
  - API/StreetFoodNarrator.API/wwwroot/frontend_backup/audio-bulk-generate.html
  - API/StreetFoodNarrator.API/wwwroot/sidebar.js
  - API/StreetFoodNarrator.API/wwwroot/translation.html

### 2026-04-12 | 18ee810 | fix onboarding when first time come to app
- Commit: 18ee810445c5e521704b5d6d3e8258b4b16bd132
- Loai: regular commit
- Thong ke: 12 files changed, 2581 insertions(+), 214 deletions(-)
- Vung anh huong: Mobile-App: 5; Mobile-Views: 3; Documentation: 2; Mobile-Platforms: 2
- Dien giai: Cap nhat ung dung mobile MAUI: man hinh, state, service va tai nguyen. Bo sung cap nhat tai lieu ky thuat, huong dan test hoac mo ta thiet ke. Trong tam la sua loi va dong bo hanh vi giua cac thanh phan.
- File thay doi tieu bieu (12):
  - Documentation/PRD-Semifinal-Gap-Closure-Addendum.md
  - Documentation/Project-Event-Flows.md
  - MobileApp/StreetFoodNarrator.App/App.xaml.cs
  - MobileApp/StreetFoodNarrator.App/AppConfig.cs
  - MobileApp/StreetFoodNarrator.App/AppShell.xaml.cs
  - MobileApp/StreetFoodNarrator.App/Core/Utils/StartupDiagnostics.cs
  - MobileApp/StreetFoodNarrator.App/MauiProgram.cs
  - MobileApp/StreetFoodNarrator.App/Platforms/Android/MainActivity.cs
  - MobileApp/StreetFoodNarrator.App/Platforms/Android/MainApplication.cs
  - MobileApp/StreetFoodNarrator.App/Views/PremiumTourPaywallPage.xaml.cs
  - MobileApp/StreetFoodNarrator.App/Views/StartupLoadingPage.cs
  - MobileApp/StreetFoodNarrator.App/Views/WelcomePage.xaml.cs

### 2026-04-12 | 722226b | Merge pull request #42 from NgNguyenChuong/fix_logicial_problem
- Commit: 722226b1ba5689bb0a4999057191c8fee98c18cd
- Loai: merge commit
- Thong ke: 7 files changed, 147 insertions(+), 150 deletions(-)
- Vung anh huong: Web-Static: 5; API-Controllers: 2
- Dien giai: Hop nhat nhanh tinh nang/develop, dong bo code va giai quyet xung dot neu co.
- File thay doi tieu bieu (7):
  - API/StreetFoodNarrator.API/Controllers/POIsController.cs
  - API/StreetFoodNarrator.API/Controllers/TranslationsController.cs
  - API/StreetFoodNarrator.API/wwwroot/audio-bulk-generate.html
  - API/StreetFoodNarrator.API/wwwroot/dashboard.html
  - API/StreetFoodNarrator.API/wwwroot/frontend_backup/audio-bulk-generate.html
  - API/StreetFoodNarrator.API/wwwroot/sidebar.js
  - API/StreetFoodNarrator.API/wwwroot/translation.html

### 2026-04-12 | 4484519 | temp
- Commit: 4484519670b74aeb32c6a1a334b8e0e6d523e34c
- Loai: regular commit
- Thong ke: 1 file changed, 18 insertions(+), 20 deletions(-)
- Vung anh huong: API-Controllers: 1
- Dien giai: Cap nhat logic backend: endpoint, service, model hoac du lieu.
- File thay doi tieu bieu (1):
  - API/StreetFoodNarrator.API/Controllers/AudioController.cs

### 2026-04-12 | ca8bb4e | Merge pull request #43 from NgNguyenChuong/fix_duplicated_adding_user
- Commit: ca8bb4e9a08d562cf8e2d92b7759be27725a83a8
- Loai: merge commit
- Thong ke: 1 file changed, 18 insertions(+), 20 deletions(-)
- Vung anh huong: API-Controllers: 1
- Dien giai: Hop nhat nhanh tinh nang/develop, dong bo code va giai quyet xung dot neu co.
- File thay doi tieu bieu (1):
  - API/StreetFoodNarrator.API/Controllers/AudioController.cs

### 2026-04-12 | 03147aa | Merge pull request #41 from NgNguyenChuong/app_fix
- Commit: 03147aacfd19033fa7bf452e6d54aaec91baad56
- Loai: merge commit
- Thong ke: 23 files changed, 3720 insertions(+), 366 deletions(-)
- Vung anh huong: Web-Static: 5; Mobile-App: 5; Mobile-Views: 3; API-Controllers: 3; Mobile-Platforms: 2; Documentation: 2; Other: 1; API-Models: 1; Mobile-Services: 1
- Dien giai: Hop nhat nhanh tinh nang/develop, dong bo code va giai quyet xung dot neu co.
- File thay doi tieu bieu (23):
  - API/StreetFoodNarrator.API/Controllers/AnalyticsController.cs
  - API/StreetFoodNarrator.API/Controllers/AuthController.cs
  - API/StreetFoodNarrator.API/Controllers/VendorsController.cs
  - API/StreetFoodNarrator.API/Models/NarrationLog.cs
  - API/StreetFoodNarrator.API/Program.cs
  - API/StreetFoodNarrator.API/wwwroot/api.js
  - API/StreetFoodNarrator.API/wwwroot/index.html
  - API/StreetFoodNarrator.API/wwwroot/register.html
  - API/StreetFoodNarrator.API/wwwroot/users.html
  - API/StreetFoodNarrator.API/wwwroot/vendor-profile.html
  - Documentation/PRD-Semifinal-Gap-Closure-Addendum.md
  - Documentation/Project-Event-Flows.md
  - MobileApp/StreetFoodNarrator.App/App.xaml.cs
  - MobileApp/StreetFoodNarrator.App/AppConfig.cs
  - MobileApp/StreetFoodNarrator.App/AppShell.xaml.cs
  - MobileApp/StreetFoodNarrator.App/Core/Services/IGeofenceService.cs
  - MobileApp/StreetFoodNarrator.App/Core/Utils/StartupDiagnostics.cs
  - MobileApp/StreetFoodNarrator.App/MauiProgram.cs
  - MobileApp/StreetFoodNarrator.App/Platforms/Android/MainActivity.cs
  - MobileApp/StreetFoodNarrator.App/Platforms/Android/MainApplication.cs
  - MobileApp/StreetFoodNarrator.App/Views/PremiumTourPaywallPage.xaml.cs
  - MobileApp/StreetFoodNarrator.App/Views/StartupLoadingPage.cs
  - MobileApp/StreetFoodNarrator.App/Views/WelcomePage.xaml.cs

### 2026-04-12 | 0d524a5 | no needed
- Commit: 0d524a5a14c1cb83a8131169ca59dc267c756766
- Loai: regular commit
- Thong ke: 1 file changed, 4 insertions(+), 59 deletions(-)
- Vung anh huong: Web-Static: 1
- Dien giai: Dieu chinh giao dien web admin/vendor va script tuong tac.
- File thay doi tieu bieu (1):
  - API/StreetFoodNarrator.API/wwwroot/audio-bulk-generate.html

### 2026-04-12 | 687211c | Merge pull request #45 from NgNguyenChuong/delete_feature_no_needed
- Commit: 687211ca5652d02956d63034a08e9c1e34b92c05
- Loai: merge commit
- Thong ke: 1 file changed, 4 insertions(+), 59 deletions(-)
- Vung anh huong: Web-Static: 1
- Dien giai: Hop nhat nhanh tinh nang/develop, dong bo code va giai quyet xung dot neu co.
- File thay doi tieu bieu (1):
  - API/StreetFoodNarrator.API/wwwroot/audio-bulk-generate.html

### 2026-04-12 | be7c7ea | temp
- Commit: be7c7ea915ab5e8176fb93b17c0b6fcdab01c7bb
- Loai: regular commit
- Thong ke: 1 file changed, 7 insertions(+), 5 deletions(-)
- Vung anh huong: Web-Static: 1
- Dien giai: Dieu chinh giao dien web admin/vendor va script tuong tac.
- File thay doi tieu bieu (1):
  - API/StreetFoodNarrator.API/wwwroot/vendors-list.html

### 2026-04-12 | aa804f1 | Add validation and error handling for vendor profile fields in edit form
- Commit: aa804f106b532f8cd1b3fa1db6dc6ce7fd7bb234
- Loai: regular commit
- Thong ke: 2 files changed, 487 insertions(+), 85 deletions(-)
- Vung anh huong: Web-Static: 1; API-Controllers: 1
- Dien giai: Cap nhat logic backend: endpoint, service, model hoac du lieu. Dieu chinh giao dien web admin/vendor va script tuong tac. Trong tam la sua loi va dong bo hanh vi giua cac thanh phan. Bo sung/tang cap tinh nang moi trong luong su dung chinh.
- File thay doi tieu bieu (2):
  - API/StreetFoodNarrator.API/Controllers/VendorsController.cs
  - API/StreetFoodNarrator.API/wwwroot/vendor-profile.html

### 2026-04-12 | edcede2 | Merge pull request #46 from NgNguyenChuong/userAnalytics
- Commit: edcede270f1a81d2b1f1af7cab16713b4545843e
- Loai: merge commit
- Thong ke: 2 files changed, 487 insertions(+), 85 deletions(-)
- Vung anh huong: Web-Static: 1; API-Controllers: 1
- Dien giai: Hop nhat nhanh tinh nang/develop, dong bo code va giai quyet xung dot neu co.
- File thay doi tieu bieu (2):
  - API/StreetFoodNarrator.API/Controllers/VendorsController.cs
  - API/StreetFoodNarrator.API/wwwroot/vendor-profile.html

### 2026-04-12 | 9297fdc | Merge pull request #47 from NgNguyenChuong/delete_feature_no_needed
- Commit: 9297fdca32317b1b681118d0554ef8d711bb1dea
- Loai: merge commit
- Thong ke: 1 file changed, 7 insertions(+), 5 deletions(-)
- Vung anh huong: Web-Static: 1
- Dien giai: Hop nhat nhanh tinh nang/develop, dong bo code va giai quyet xung dot neu co.
- File thay doi tieu bieu (1):
  - API/StreetFoodNarrator.API/wwwroot/vendors-list.html

### 2026-04-12 | b9df168 | RELEASE
- Commit: b9df168f90b5810708fa43fbdbfbe56088a00727
- Loai: regular commit
- Thong ke: 1 file changed, 2 insertions(+), 105 deletions(-)
- Vung anh huong: Web-Static: 1
- Dien giai: Dieu chinh giao dien web admin/vendor va script tuong tac. Lien quan den dong goi/release va san sang phat hanh.
- File thay doi tieu bieu (1):
  - API/StreetFoodNarrator.API/wwwroot/poi-create.html

