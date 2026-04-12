# PRD Semifinal Addendum - Gap Closure (Code-Verified)

Ngày rà soát: 2026-04-12  
Phạm vi đối chiếu: API + Mobile App + tài liệu sự kiện trong repository hiện tại.

## 1) Tóm tắt nhanh 10 điểm thiếu

| ID | Nhận xét từ AI review | Kết quả đối chiếu code | Kết luận cho PRD Semifinal |
|---|---|---|---|
| 1 | Backend startup flow 4 tầng thiếu | Có trong code (check config, DB init, seeding, indexing) | Thiếu ở tài liệu, cần bổ sung section Startup |
| 2 | 3-Tier Content Fallback thiếu | Có fallback chuỗi cho POI model, nhưng chưa có cờ is_fallback trong API contract | Bổ sung rule fallback + contract response |
| 3 | Localization Hotset/Warmup/On-demand thiếu | Có sync 2 pha dữ liệu offline, chưa có chiến lược hotset/warmup riêng cho localization content | Bổ sung section chiến lược localization theo 3 flow |
| 4 | Map Pack / PMTiles / 3 mode thiếu | Chưa có PMTiles/Map Pack backend; hiện dùng cloud tile + cache + fallback layer | Cần chốt quyết định scope và nêu tác động |
| 5 | Design Patterns + Key Constants thiếu bảng tổng hợp | Constants có thật nhưng rải rác trong code | Bổ sung bảng chuẩn hóa constants/patterns |
| 6 | NFR thiếu Scalability/Maintainability/Accessibility/Testability | Mới có một phần hiệu năng/bảo mật/độ tin cậy | Bổ sung NFR đo được (measurable) |
| 7 | F34 thiếu nội dung | Đã có đầy đủ trong Project-Event-Flows.md | Bản docx bị lệch version, cần đồng bộ nội dung F34 |
| 8 | Tối ưu pin chưa chốt | Có chiến lược adaptive polling + foreground service, nhưng chưa có FR pin chính thức | Bổ sung FR battery + policy + KPI |
| 9 | Analytics tuyến ẩn danh/top/avg listen/heatmap chưa đầy đủ | Có dữ liệu và endpoint nền tảng; thiếu mô tả metric rõ ràng trong PRD Semifinal | Bổ sung metric dictionary + công thức |
| 10 | QR có hạn 5 ngày chưa rõ | Có constant 5 ngày ở Program và QrController | Bổ sung FR QR expiry mặc định 5 ngày |

---

## 2) Bằng chứng code cốt lõi

### 2.1 Backend startup, seeding, indexing, config check
- API/StreetFoodNarrator.API/Program.cs:17 -> QrCodeExpiryWindowDays = 5
- API/StreetFoodNarrator.API/Program.cs:44 -> throw MongoDbSettings not configured
- API/StreetFoodNarrator.API/Program.cs:69 -> throw JWT SecretKey not configured
- API/StreetFoodNarrator.API/Program.cs:143 -> gọi DbInitializer.Initialize
- API/StreetFoodNarrator.API/Data/DbInitializer.cs:13 -> EnsureIndexes chạy trước
- API/StreetFoodNarrator.API/Data/DbInitializer.cs:99-102 -> tạo indexes
- API/StreetFoodNarrator.API/Data/DbInitializer.cs:46-52 -> seed POI, vendor, menu, tour, analytics

### 2.2 Fallback localization/content hiện có
- API/StreetFoodNarrator.API/Models/POI.cs:207-232 -> fallback zh -> en -> vi cho name/description/audio
- MobileApp/StreetFoodNarrator.App/Core/Models/POI.cs:587-620 -> fallback zh -> en -> vi cho name/description
- MobileApp/StreetFoodNarrator.App/Core/Services/LanguageService.cs:38 -> fallback ngôn ngữ không hỗ trợ về vi
- API/StreetFoodNarrator.API/Controllers/TranslationsController.cs:198-215 -> export theo language, chưa trả cờ fallback

### 2.3 Sync 2 pha + offline fallback
- MobileApp/StreetFoodNarrator.App/Views/WelcomePage.xaml.cs:114 -> RunSimpleFlowAsync
- MobileApp/StreetFoodNarrator.App/Views/WelcomePage.xaml.cs:158,188 -> EnsureDeferredOfflineCompletionAsync
- MobileApp/StreetFoodNarrator.App/Views/WelcomePage.xaml.cs:938 -> PrimeEssentialOfflineDataAsync
- MobileApp/StreetFoodNarrator.App/Core/Services/Implementations/DataSyncService.cs:222 -> PrimeEssentialOfflineDataAsync
- MobileApp/StreetFoodNarrator.App/Core/Services/Implementations/DataSyncService.cs:260 -> EnsureDeferredOfflineCompletionAsync

### 2.4 Map hiện trạng
- MobileApp/StreetFoodNarrator.App/Views/MainPage.Map.cs:76-77 -> cloud base map Carto tile
- MobileApp/StreetFoodNarrator.App/Views/MainPage.Map.cs:84-89 -> offline không cache thì bật fallback layer
- MobileApp/StreetFoodNarrator.App/Views/MainPage.Map.cs:263,276 -> HasUsableTileCache + CreateOfflineFallbackLayer

### 2.5 Battery + tracking
- MobileApp/StreetFoodNarrator.App/Core/Services/LocationService.cs:47-60 -> xin LocationAlways + start foreground service
- MobileApp/StreetFoodNarrator.App/Core/Services/LocationService.cs:287-290 -> polling adaptive (Far=30s, Inside=2s, Near=5s)
- MobileApp/StreetFoodNarrator.App/Core/Services/LocationService.cs:240-256 -> heartbeat + stationary delay
- MobileApp/StreetFoodNarrator.App/Core/Services/IGeofenceService.cs:22-23 -> movement upload 8s / 10m

### 2.6 Analytics + QR expiry
- API/StreetFoodNarrator.API/Controllers/AnalyticsController.cs:229-248 -> overview KPI
- API/StreetFoodNarrator.API/Controllers/AnalyticsController.cs:258-289 -> top POI theo PlayCount/MeanPlay/TotalListenSeconds
- API/StreetFoodNarrator.API/Controllers/AnalyticsController.cs:94-95 -> lưu toạ độ user trong narration logs
- API/StreetFoodNarrator.API/Controllers/QrController.cs:17 -> ProductionCycleDays = 5
- API/StreetFoodNarrator.API/Controllers/QrController.cs:18 -> TestCycleMinutes = 3

---

## 3) Nội dung bổ sung đề xuất đưa vào PRD Semifinal

## 3.1 Startup Backend Flow (bổ sung mới)

### Mục tiêu
Bổ sung luồng khởi động backend theo 4 tầng để PRD phản ánh đúng code chạy thật.

### Startup Chain (4 tầng)
1. Security Config Check
- Validate MongoDbSettings và JWT SecretKey ngay khi boot.
- Nếu thiếu cấu hình bắt buộc: fail-fast, không cho app chạy nửa vời.

2. Database Bootstrap
- Khởi tạo MongoClient, MongoDbContext, service registration.
- Khởi động hosted services liên quan premium/subscription.

3. Seeding
- Seed roles (Admin/Vendor/User), admin mặc định, dữ liệu nền (POI/Vendor/Menu/Tour/Analytics).

4. Indexing
- Tạo unique/TTL/index truy vấn trước khi seed để đảm bảo integrity và hiệu năng.

### Acceptance Criteria
- Nếu thiếu JWT/Mongo config thì process dừng với lỗi rõ ràng.
- Khi DB rỗng, hệ thống seed xong và tạo admin account.
- Index critical tồn tại trước khi bắt đầu phục vụ request nghiệp vụ.

---

## 3.2 3-Tier Content Fallback cho localization (bổ sung mới)

### Rule chuẩn hóa
- Tier 1: requested language
- Tier 2: English
- Tier 3: Vietnamese gốc

### Contract đề xuất cho API response
Thêm metadata cho từng field localized:
- resolved_language
- is_fallback
- fallback_source (requested -> en hoặc requested -> vi)

### Ghi chú hiện trạng
- Fallback chuỗi cho POI đã có ở model level (đặc biệt nhánh zh).
- Export dictionary translation hiện chưa trả cờ fallback và không áp dụng chain đầy đủ khi value trống.

### Action đề xuất
- Chuẩn hóa fallback tại tầng DTO mapping để client không tự đoán.
- Bổ sung is_fallback=true khi không lấy được bản dịch đúng requested language.

---

## 3.3 Localization Strategy: On-demand, Hotset, Warmup (bổ sung mới)

### Hiện trạng
- Có chiến lược dữ liệu 2 pha tổng quát (essential trước, full nền sau).
- Chưa có luồng hotset/warmup chuyên biệt cho localization content.

### Đặc tả đề xuất
1. On-demand
- Khi mở POI hoặc đổi ngôn ngữ, lấy ngay key thiếu (delta).

2. Hotset
- Dựa vị trí hiện tại, preload localization cho 10 POI gần nhất.

3. Warmup
- Sau khi app sẵn sàng, chạy nền để lấp đầy full corpus localization.

### Acceptance Criteria
- Tỉ lệ hiển thị text rỗng khi đổi ngôn ngữ < 1% trong session online.
- Thời gian render first screen không bị tăng > 10% do warmup.

---

## 3.4 Map Pack / PMTiles / 3 chế độ bản đồ (bổ sung quyết định scope)

### Hiện trạng thực tế
- V1: Cloud tile + persistent cache + offline fallback grid.
- Chưa có PMTiles map pack và backend map pack service.

### Đề xuất ghi PRD rõ ràng theo roadmap
- Mode A (đang có): Cloud mode
- Mode B (planned): Offline Pack mode (PMTiles/pack file)
- Mode C (planned): Hybrid mode (cloud + pack)

### Tác động nếu chưa có Mode B/C
- Offline sâu vẫn điều hướng được ở mức cơ bản nhưng không có full fidelity tiles.
- Cần mô tả rõ để tránh hiểu nhầm đây là bug mà thực tế là giới hạn scope.

---

## 3.5 Design Patterns và Key Constants (bổ sung bảng chuẩn hóa)

### Pattern chính đang dùng
| Pattern | Áp dụng |
|---|---|
| Offline-first + Deferred completion | WelcomePage + DataSyncService |
| Fail-fast config validation | Program startup config check |
| Graceful degradation | Map fallback layer, remote-local translation fallback |
| Role-based moderation workflow | Admin/Vendor review flows |
| Idempotency (analytics/progress) | SubmissionIdempotency usage |

### Key Constants đề xuất đưa vào Appendix
| Constant | Giá trị | Nguồn |
|---|---:|---|
| Spot geofence fallback radius | 30m | AppConfig, POIsController |
| Spot radius clamp | 15m-40m | AppConfig |
| Debounce | 3000ms | AppConfig |
| Spot cooldown max | 5 phút | POIsController |
| Tracking poll interval | Inside 2s / Near 5s / Far 30s | LocationService |
| Movement ping analytics | 8s hoặc 10m | GeofenceService |
| QR production expiry | 5 ngày | Program + QrController |
| QR test rotation | 3 phút | QrController |

---

## 3.6 NFR còn thiếu (bổ sung)

### Scalability
- NFR-SCAL-01: API phải duy trì p95 latency < 500ms cho endpoint read chính dưới tải mục tiêu của phase hiện tại.
- NFR-SCAL-02: Dashboard analytics không timeout ở khung thời gian 30 ngày với pagination chuẩn.

### Maintainability
- NFR-MAIN-01: Tài liệu hóa constants nghiệp vụ tại 1 appendix thống nhất.
- NFR-MAIN-02: Mọi rule moderation/fallback phải có test regression trước release.

### Accessibility
- NFR-A11Y-01: Mobile phải có text alternative cho nội dung audio (transcript/fun-fact/description).
- NFR-A11Y-02: Contrast và kích thước font tối thiểu cho mode outdoor.

### Testability
- NFR-TEST-01: Có test deterministic cho geofence transition (enter/exit/cooldown).
- NFR-TEST-02: Có test contract cho fallback localization chain và QR expiry.

---

## 3.7 F34 đồng bộ vào PRD Semifinal

### Kết luận đối chiếu
F34 đã có đầy đủ trong tài liệu sự kiện nội bộ (Project-Event-Flows.md), nhưng bản docx semifinal đang thiếu nội dung chi tiết.

### Nội dung cần đưa vào PRD
- Khi offline và không có tile cache: bật fallback grid layer, vẫn giữ POI pins/route overlay.
- Khi tile runtime lỗi: catch exception và chuyển fallback, không crash.
- UI thông báo rõ đang ở chế độ bản đồ ngoại tuyến cơ bản.

---

## 3.8 Tối ưu pin (đóng ambiguity trong plan)

### Hiện trạng code
- Adaptive polling theo proximity (2s/5s/30s).
- Chống nhiễu GPS bằng accuracy gate + movement threshold + heartbeat.
- Android dùng foreground service khi tracking background.
- Chưa thấy cơ chế WakeLock tùy biến trong source ứng dụng.

### FR đề xuất bổ sung
- FR-BAT-01: Tracking interval phải adaptive theo Inside/Near/Far.
- FR-BAT-02: Khi user đứng yên, tự động tăng delay lấy vị trí.
- FR-BAT-03: Không dùng wake lock thủ công trừ khi có benchmark chứng minh cần thiết.
- FR-BAT-04: Ghi telemetry battery impact theo phiên để theo dõi regression.

---

## 3.9 Analytics bổ sung metric dictionary (đóng ambiguity)

### Đã có nền tảng dữ liệu
- Trajectory ẩn danh: narration_logs/mobile có lat/lon + anonymous device id.
- Top địa điểm: GetTopPOIs.
- Avg listen time: MeanPlay per POI.
- Heatmap: narration logs toạ độ + dashboard heatmap.

### Bổ sung vào PRD (định nghĩa metric)
- Metric A: Anonymous trajectory points = số bản ghi ActionType=LocationPing.
- Metric B: Top POIs = sort theo TotalListenSeconds, tie-break PlayCount.
- Metric C: Avg listen time per POI = MeanPlay (giây).
- Metric D: Heatmap intensity = số log theo bucket geo + time window.

---

## 3.10 QR expiry mặc định 5 ngày (chốt rõ trong PRD)

### Rule đề xuất
- QR campaign production mặc định có chu kỳ hết hạn 5 ngày.
- Admin test rotation dùng chu kỳ 3 phút, tự rollback về production khi hết hạn.

### Acceptance Criteria
- /api/Qr/admin/status luôn trả expiresAtUtc và remaining rõ ràng.
- Landing /qr phải hiển thị trạng thái expired nếu quá hạn.

---

## 4) Backlog kỹ thuật sau khi cập nhật PRD

### P0 (nên làm ngay)
1. Đồng bộ bản docx semifinal với F34 và startup flow.
2. Thêm bảng constants/patterns vào PRD chính.
3. Ghi rõ QR expiry 5 ngày trong FR QR.

### P1 (nâng chất lượng)
1. Chuẩn hóa API localization trả is_fallback + resolved_language.
2. Bổ sung Hotset/Warmup localization thực thi (không chỉ mô tả).
3. Bổ sung test regression cho fallback chain và geofence battery profile.

### P2 (roadmap)
1. Triển khai Map Pack/PMTiles và 3-mode map chính thức.
2. Bổ sung benchmark scalability định lượng theo phase.
