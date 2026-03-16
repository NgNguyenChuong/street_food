# PRD — Hệ Thống Du Lịch Ẩm Thực Đường Phố Quận 4
## Product Requirements Document (PRD)
## Mục Lục (Chuẩn Hóa Theo HTML Presentation)

### A. Skeleton theo presentation
1. [Overview](#3-kiến-trúc-tổng-quan)
2. [Startup](#11-module-6--offline--caching)
3. [Geofence](#10-module-5--gps--geofencing)
4. [Content](#6-module-1--content--poi)
5. [Audio](#7-module-2--audio--tts)
6. [Auth](#8-module-3--authentication--rbac)
7. [Localization](#9-module-4--localization-i18n)
8. [Offline](#11-module-6--offline--caching)
9. [Maps](#12-module-7--maps)
10. [Admin + AI](#13-module-8--admin-dashboard-web-portal)
11. [Patterns / Constants](#appendix-a--constants-reference)
12. [Full Flow + Stack + Cost](#4-tech-stack)

### B. Mục lục chi tiết PRD
1. [Tổng Quan Dự Án](#1-tổng-quan-dự-án)
2. [Mục Tiêu & Phạm Vi](#2-mục-tiêu--phạm-vi)
3. [Kiến Trúc Tổng Quan](#3-kiến-trúc-tổng-quan)
4. [Tech Stack](#4-tech-stack)
5. [Trạng Thái Hiện Tại](#5-trạng-thái-hiện-tại)
6. [Module 1 — Content / POI](#6-module-1--content--poi)
7. [Module 2 — Audio / TTS](#7-module-2--audio--tts)
8. [Module 3 — Authentication & RBAC](#8-module-3--authentication--rbac)
9. [Module 4 — Localization (i18n)](#9-module-4--localization-i18n)
10. [Module 5 — GPS & Geofencing](#10-module-5--gps--geofencing)
11. [Module 6 — Offline & Caching](#11-module-6--offline--caching)
12. [Module 7 — Maps](#12-module-7--maps)
13. [Module 8 — Admin Dashboard (Web Portal)](#13-module-8--admin-dashboard-web-portal)
14. [Module 9 — Owner Portal](#14-module-9--owner-portal)
15. [Module 10 — AI Advisor](#15-module-10--ai-advisor)
16. [Module 11 — Mobile App (.NET MAUI)](#16-module-11--mobile-app-net-maui)
17. [Data Models](#17-data-models)
18. [API Endpoints](#18-api-endpoints)
19. [Non-Functional Requirements](#19-non-functional-requirements)
20. [Lộ Trình Triển Khai](#20-lộ-trình-triển-khai)
21. [Definition of Done](#21-definition-of-done)

---

## 1. Tổng Quan Dự Án

### 1.1 Mô tả
**Street Food Narrator** là hệ thống du lịch ẩm thực đường phố thông minh dành cho Quận 4, TP.HCM. Hệ thống tự động phát hướng dẫn âm thanh đa ngôn ngữ khi người dùng đi gần các điểm ẩm thực (Point of Interest - POI), hoạt động hoàn toàn offline, hỗ trợ khách du lịch nước ngoài khám phá ẩm thực địa phương.

### 1.2 Đối Tượng Người Dùng

| Vai trò | Mô tả |
|---|---|
| **Tourist (Khách du lịch)** | Người dùng app, đi bộ khám phá ẩm thực, nghe hướng dẫn audio |
| **POI Owner (Chủ quán)** | Đăng ký, quản lý thông tin gian hàng của mình |
| **Admin** | Quản trị viên hệ thống, duyệt nội dung, quản lý users |

### 1.3 Vấn Đề Cần Giải Quyết
- Khách nước ngoài không đọc được menu/biển hiệu tiếng Việt
- Không có hướng dẫn âm thanh tự động theo vị trí GPS
- Không có nền tảng để chủ quán tự cập nhật thông tin
- Hệ thống phụ thuộc hoàn toàn vào kết nối mạng

---

## 2. Mục Tiêu & Phạm Vi

### 2.1 Mục Tiêu
- Phát âm thanh hướng dẫn tự động khi người dùng vào geofence của POI
- Hỗ trợ tối thiểu 5 ngôn ngữ: vi, en, zh, ja, ko
- Hoạt động offline hoàn toàn (audio + map + data)
- Chi phí API = $0 (Edge-TTS + deep-translator)
- Chủ quán có thể tự quản lý thông tin của mình

### 2.2 Phạm Vi
- **Địa lý:** Quận 4, TP.HCM; tập trung kênh Vĩnh Khánh
- **Nền tảng:** Mobile App (Android/iOS) + Web Admin Portal
- **Backend:** ASP.NET Core API + MongoDB

### 2.3 Ngoài Phạm Vi (v2.0)
- QR Code activation (Phase 3)
- Push notifications (Phase 3)
- Thanh toán / voucher
- Rating & review công khai

---

## 3. Kiến Trúc Tổng Quan

```
┌──────────────────────────────────────────────────────┐
│                   CONSUMER LAYER                      │
│                                                       │
│  ┌─────────────────────┐   ┌────────────────────────┐│
│  │  Mobile App          │   │   Web Admin/Owner      ││
│  │  .NET MAUI           │   │   Portal (HTML/JS/CSS) ││
│  │  Android + iOS       │   │   Served từ API        ││
│  └──────────┬──────────┘   └────────────┬───────────┘│
└─────────────┼────────────────────────────┼────────────┘
              │ HTTP/REST                  │ HTTP/REST
              ▼                            ▼
┌──────────────────────────────────────────────────────┐
│                  API LAYER (ASP.NET Core)              │
│                                                       │
│  /api/pois    /api/audio   /api/auth   /api/tts       │
│  /api/tours   /api/menus   /api/ai     /api/maps      │
│  /api/localization  /api/analytics  /api/owner        │
└──────────────────────────┬───────────────────────────┘
                           │
              ┌────────────┴──────────────┐
              ▼                           ▼
┌─────────────────────┐       ┌──────────────────────┐
│  MongoDB             │       │  File System          │
│  quan4_culinary      │       │  /Uploads/audio       │
│  9 collections       │       │  /maps/pmtiles        │
└─────────────────────┘       └──────────────────────┘
              │
              ▼
┌─────────────────────┐
│  External Services   │
│  Edge-TTS (free)     │
│  deep-translator     │
│  Gemini 2.0 Flash    │
│  OSRM (routing)      │
└─────────────────────┘
```

### 3.1 Mobile App Internal Architecture
```
┌──────────────────┐
│  Views (XAML)     │ ← MainPage, TourPage, POIDetailPage, SettingsPage
│  + Code-behind   │
└────────┬─────────┘
         │ binds to
┌────────▼─────────┐
│  MainViewModel   │ ← Singleton, tất cả observable state
│  (MVVM)          │
└────────┬─────────┘
         │ calls
┌────────▼────────────────────────────────────────┐
│  Services                                        │
│  IGeofenceService • ITTSService • IAudioService  │
│  ILocalDatabaseService • IZoneRepository         │
│  ILocationService • IAudioCacheService           │
└────────┬────────────────────────────────────────┘
         │
┌────────▼─────────────────────────┐
│  Data Sources                     │
│  SQLite (local) • API • JSON file │
└──────────────────────────────────┘
```

---

## 4. Tech Stack

### 4.1 Backend
| Thành phần | Technology | Ghi chú |
|---|---|---|
| Web Framework | ASP.NET Core 10 | Hiện có |
| Database | MongoDB (Motor) | Hiện có |
| ORM/Driver | MongoDB.Driver | Hiện có |
| Auth | JWT + ASP.NET Identity + MongoDbCore | Hiện có — cần nâng cấp RBAC |
| TTS | Edge-TTS (Python, qua tts_wrapper.py) | Hiện có |
| Translation | deep-translator / Google Translate | **Cần thêm** |
| AI | Google Gemini 2.0 Flash API | **Cần thêm** |
| PII Encryption | AES-256 (hoặc tương đương .NET) | **Cần thêm** |
| Background Tasks | IHostedService / Channel<T> | **Cần thêm** |
| SSE | ASP.NET Core SSE | **Cần thêm** |
| Maps | PMTiles static files | **Cần thêm** |

### 4.2 Mobile App
| Thành phần | Technology | Ghi chú |
|---|---|---|
| Framework | .NET MAUI 10 | Hiện có |
| Maps | Mapsui (OSM tiles) | Hiện có |
| Audio | Plugin.Maui.Audio | Hiện có |
| Local DB | SQLite (sqlite-net) | Hiện có |
| MVVM | CommunityToolkit.Mvvm | Hiện có |
| GPS | MAUI Geolocation + Android ForegroundService | Hiện có |
| TTS | Offline cache → API Edge-TTS → MAUI TTS | Hiện có |

### 4.3 Web Admin Portal
| Thành phần | Technology | Ghi chú |
|---|---|---|
| UI | Vanilla HTML/JS/CSS | Hiện có — cần mở rộng |
| Auth | JWT cookie (httpOnly) | **Cần nâng cấp** |
| Map Editor | Leaflet.js / MapLibre-GL | **Cần thêm** |

---

## 5. Trạng Thái Hiện Tại

### 5.1 ✅ Đã Hoàn Thành

| Feature | Chi tiết |
|---|---|
| Geofence engine | Haversine, debounce 3s, cooldown, ConstrainedInternet fix |
| GPS tracking | Real GPS + Android ForegroundService + SimulatedGPS |
| SQLite caching | POI, MenuItemDto, ZoneHistory tables |
| API sync | Version-based incremental sync, bundled JSON fallback |
| Audio playback | Per-zone players, volume, seek, speed |
| TTS pipeline | Edge-TTS → MP3 cache → native MAUI TTS fallback |
| POI Detail Page | Mini-map, menu items, TTS narration, share |
| Tour Page | Virtual tour, TTS per stop, progress, chips |
| Liked/Saved POIs | Heart toggle, SQLite persistence |
| Tour History | ZoneHistory grouped by session (ProfilePage) |
| Admin/Vendor web portal | CRUD POIs, audio, bulk TTS, analytics (basic) |
| Auth | JWT login/register, Admin/Vendor/Tourist roles |
| Multi-language content | vi/en/zh fields, LanguageService |
| Offline badge | DataSourceKind enum |

### 5.2 ⚠️ Đang Dở / Partial

| Feature | Vấn đề cụ thể |
|---|---|
| Tour nav commands | `PrevStop`, `NextStop`, `Bookmark`, `SelectStop` là stubs với `// TODO` |
| Settings API sync | `SettingsPage` chỉ dùng Preferences local, có 2 `// TODO: Call API` |
| ProfilePage replay | `OnReplayTour` command tồn tại nhưng navigation chưa được wired |
| AppConfig secrets | `MongoConnectionString`, `MaptilerApiKey` vẫn là `YOUR_VALUE_HERE` |
| SimulatedGPS | `UseSimulatedGPS = true` hardcoded — chưa có UI toggle |
| ShowDetails từ Map | `LatestStatus = "Chi tiết đang được phát triển"` — placeholder |
| RBAC | Chỉ có Admin/Vendor/Tourist — chưa dynamic, chưa đủ permissions |
| Owner Portal web | Có trang vendors nhưng chưa đủ flow đăng ký → admin duyệt |

### 5.3 ❌ Chưa Có

| Feature | Mô tả |
|---|---|
| Dynamic RBAC | 29 permissions / 9 domains + roles lưu DB |
| Proper localization | Collection `poi_localizations` riêng cho bản dịch per (poi_id, lang) |
| Localization flows | Hotset, On-demand, Warmup endpoints |
| Audio Pack download | Manifest + SHA-256 verify + offline pack |
| AI Advisor | Gemini 2.0 Flash integration cho enhance description |
| Owner registration flow | pending → admin approval workflow |
| PII Encryption | Mã hoá số CCCD chủ quán |
| Audit Logs | Collection `audit_logs`, ghi lại mọi action |
| SSE Audio Tasks | Server-Sent Events cho progress bulk TTS |
| PMTiles Map Pack | Offline map pack cho Quận 4 |
| Background TTS | IHostedService xử lý queue TTS song song |
| poi_owner_registrations | Collection + approval workflow |
| poi_submissions | Owner gửi bài → admin duyệt |
| Google OAuth / Guest mode | Login không cần tài khoản cho tourist |
| 2dsphere index | Geospatial queries `$nearSphere` trên `pois.location` |

---

## 6. Module 1 — Content / POI

### 6.1 Requirements

**API Endpoints cần có:**

| Method | Path | Mô tả | Status |
|---|---|---|---|
| GET | `/api/pois` | Lấy danh sách POI (pagination, filter) | ✅ Có |
| GET | `/api/pois/{id}` | Chi tiết POI | ✅ Có |
| POST | `/api/pois` | Tạo POI mới | ✅ Có |
| PUT | `/api/pois/{id}` | Cập nhật POI | ✅ Có |
| DELETE | `/api/pois/{id}` | Xóa mềm POI (cascade: localizations + audio files + images) | ⚠️ Partial (cần cascade delete đúng) |
| GET | `/api/pois/load-all` | Tải toàn bộ POI kèm localization (dùng cho mobile sync) | ⚠️ Cần thêm `?lang=`, version check |
| GET | `/api/pois/nearby` | POI trong bán kính (2dsphere `$nearSphere`) | ❌ Chưa có |
| POST | `/api/pois/{id}/images` | Upload ảnh (max 8, mỗi ảnh max 5MB) | ✅ Có |
| DELETE | `/api/pois/{id}/images/{imgIdx}` | Xóa ảnh theo index | ❌ Chưa có |

**Acceptance Criteria:**
- `load-all` trả về mảng POI đã hydrate localizations theo `?lang=`, áp dụng 3-Tier Content Fallback
- `load-all` hỗ trợ `?version=` để incremental sync (chỉ trả POI thay đổi sau timestamp)
- `audio_url` được decorate `?v={mtime}&l={lang}` trước khi trả về client
- Soft delete: set `is_deleted=true`, không xóa cứng ngay (cascade sau cron job)
- 2dsphere index trên `pois.location` cho geospatial queries

**MongoDB Collection `pois`:**
```json
{
  "_id": "ObjectId",
  "name_vi": "string",
  "description_vi": "string",
  "type": "string (street_food|restaurant|cafe|...)",
  "status": "string (active|pending|inactive)",
  "location": { "type": "Point", "coordinates": [lng, lat] },
  "geofence_radius": 30,
  "zone_level": "int (1=Area, 2=District, 3=Spot)",
  "audio_priority": "int",
  "address": "string",
  "open_hours": "string",
  "price_range": "string",
  "signature_dish": "string",
  "signature_dishes_json": "string (JSON array)",
  "image_urls": ["string"],
  "is_deleted": false,
  "created_at": "datetime",
  "updated_at": "datetime",
  "owner_id": "ObjectId (nullable)",
  "data_version": "int (auto-increment for sync)"
}
```

---

## 7. Module 2 — Audio / TTS

### 7.1 Requirements

**API Endpoints:**

| Method | Path | Mô tả | Status |
|---|---|---|---|
| GET | `/api/audio` | Danh sách audio content | ✅ Có |
| POST | `/api/audio` | Upload file audio (MP3/WAV/M4A max 25MB) | ✅ Có |
| DELETE | `/api/audio/{id}` | Xóa audio | ✅ Có |
| POST | `/api/audio/tts` | Synthesize text → MP3 (Tier 2 TTS) | ✅ Có (via Python wrapper) |
| POST | `/api/audio/bulk-generate` | Khởi động task bulk TTS cho nhiều POI | ⚠️ Có — cần SSE progress |
| GET | `/api/audio/tasks/stream` | SSE stream: progress bulk TTS real-time | ❌ Chưa có SSE |
| POST | `/api/audio/tasks/{id}/pause` | Pause task | ❌ Chưa có |
| POST | `/api/audio/tasks/{id}/resume` | Resume task | ❌ Chưa có |
| POST | `/api/audio/tasks/{id}/cancel` | Cancel task | ❌ Chưa có |
| GET | `/api/audio/pack-manifest` | Manifest audio pack (SHA-256 per file, total_bytes) | ❌ Chưa có |

**TTS Pipeline (4-Tier Hybrid):**

| Tier | Cách hoạt động | Latency | Điều kiện |
|---|---|---|---|
| Tier 1 | Pre-generated MP3 từ cache file | 0ms | `audio_url` tồn tại và không bị `is_fallback` |
| Tier 1.5 | On-demand translate + Edge-TTS | 2-5s | Có `is_fallback=true` trong localization |
| Tier 2 | Cloud TTS: `POST /api/audio/tts` | 3-8s | Không có pre-gen audio |
| Tier 3 | Native MAUI TTS (offline fallback) | 0ms (local) | Không có network |

**Background TTS Task Manager:**
- Semaphore max 3 concurrent tasks
- Task states: `queued → running → paused → completed/failed/cancelled`
- SSE: `GET /api/audio/tasks/stream` → EventSource → Admin UI progress bar
- Cache key: `MD5(text + ":" + lang)` → nếu file tồn tại = cache hit, skip

**Audio Pack Manifest** (`GET /api/audio/pack-manifest?lang=vi`):
```json
{
  "lang": "vi",
  "pack_version": "20260316",
  "total_files": 45,
  "total_bytes": 12345678,
  "files": [
    { "poi_id": "1", "url": "/uploads/audio/poi_1_vi.mp3", "sha256": "abc..." }
  ]
}
```

**Acceptance Criteria:**
- Edge-TTS invocation qua `tts_wrapper.py` — gọi async, không block main thread
- Disk cache: nếu `MD5(text:lang).mp3` tồn tại → skip synthesis
- Response headers: `X-Cache: HIT/MISS`, `X-Static-Url: <versioned url>`
- SSE stream format: `data: {"task_id":"...", "status":"running", "progress":45}\n\n`
- Mobile app pre-downloads audio pack theo manifest trước khi đi tour

---

## 8. Module 3 — Authentication & RBAC

### 8.1 Current State vs Target

**Hiện tại:** Admin/Vendor/Tourist roles cơ bản, JWT Bearer token.

**Target:** Dynamic RBAC — 29 permissions / 9 domains; roles lưu MongoDB; JWT payload chứa permissions list.

### 8.2 Permission Domains (9 domains, 29 permissions tổng)

| Domain | Permissions |
|---|---|
| `poi` | read, create, update, delete, approve, toggle |
| `menu` | read, create, update, delete |
| `user` | read, create, update, delete |
| `role` | read, create, update, delete |
| `analytics` | view, export, view_own |
| `audit` | read, manage |
| `system` | config, logs, backup |
| `owner` | register, access, submit_poi, manage_own_poi |
| `content` | moderate, publish |

### 8.3 Default Roles (seeded on startup)

| Role | Priority | Permissions |
|---|---|---|
| `super_admin` | 0 | TẤT CẢ 29 permissions |
| `admin` | 1 | poi:\*, menu:\*, user:\*, analytics:view/export, audit:\*, content:\* |
| `poi_owner` | 10 | poi:read, owner:\*, menu:read/create/update, analytics:view_own |
| `user` | 100 | poi:read, menu:read, owner:register |

### 8.4 API Endpoints

| Method | Path | Mô tả | Status |
|---|---|---|---|
| POST | `/api/auth/register` | Đăng ký tài khoản | ✅ Có |
| POST | `/api/auth/login` | Đăng nhập → JWT + refresh token | ✅ Có — cần httpOnly cookie |
| POST | `/api/auth/refresh` | Refresh access token | ❌ Chưa có |
| POST | `/api/auth/logout` | Xóa cookie | ❌ Chưa có |
| POST | `/api/auth/change-password` | Đổi mật khẩu | ❌ Chưa có |
| GET | `/api/auth/me` | Profile + permissions | ⚠️ Cần trả permissions list |
| GET | `/api/roles` | Danh sách roles | ❌ Chưa có |
| POST | `/api/roles` | Tạo role mới | ❌ Chưa có |
| PUT | `/api/roles/{id}` | Cập nhật role permissions | ❌ Chưa có |
| DELETE | `/api/roles/{id}` | Xóa role | ❌ Chưa có |

### 8.5 Security Requirements
- **Access Token**: JWT, 30 phút, httpOnly cookie + Bearer header (dual-mode)
- **Refresh Token**: JWT, 7 ngày, httpOnly cookie
- **Cookie**: `SameSite=Lax`, `Secure` (HTTPS), `HttpOnly`
- **Role Cache TTL**: 300 giây (tránh query DB mỗi request)
- **JWT payload**: chứa `permissions[]` list → không cần query DB per request
- **PII Encryption**: Số CCCD chủ quán → AES-256 + base64 prefix `"v1:"` → lưu DB. Sau 180 ngày → auto redacted. Không bao giờ return plaintext khi không cần.

### 8.6 MongoDB Collections cần thêm
- `roles`: `{ _id, name, permissions[], priority, is_system, created_at }`
- `audit_logs`: `{ _id, action, user_id, resource_type, resource_id, details, ip, timestamp }`

---

## 9. Module 4 — Localization (i18n)

### 9.1 Concept

Tách bản dịch ra collection riêng `poi_localizations`. Mỗi POI × Ngôn ngữ = 1 document.

**MongoDB Collection `poi_localizations`:**
```json
{
  "_id": "ObjectId",
  "poi_id": "ObjectId",
  "lang": "en",
  "name": "string",
  "description": "string",
  "signature_dish": "string",
  "audio_url": "/uploads/audio/poi_1_en.mp3",
  "audio_mtime": "datetime",
  "is_fallback": false,
  "generated_at": "datetime"
}
```
- **Compound Index**: `{ poi_id: 1, lang: 1 }` unique

### 9.2 3-Tier Content Fallback

Khi trả POI data cho ngôn ngữ `target`:
1. **Tier 1**: Tìm `poi_localizations` với `lang=target` và `is_fallback=false` → dùng
2. **Tier 2**: Nếu không có → tìm `lang=en` → đánh dấu `is_fallback=true` trong response
3. **Tier 3**: Nếu không có `en` → dùng trường gốc tiếng Việt từ `pois`, `audio_url = null`

### 9.3 API Endpoints

| Method | Path | Mô tả | Status |
|---|---|---|---|
| GET | `/api/pois/load-all?lang=en` | Tải toàn bộ POI hydrate localization | ⚠️ Cần cải tiến |
| POST | `/api/localizations/on-demand` | Dịch + TTS ngay 1 POI cho lang yêu cầu | ❌ Chưa có |
| POST | `/api/localizations/prepare-hotset` | Dịch trước top 10 POI gần nhất | ❌ Chưa có |
| POST | `/api/localizations/warmup` | Dịch toàn bộ corpus (background) | ❌ Chưa có |
| GET | `/api/localizations/{poi_id}` | Xem tất cả bản dịch của 1 POI | ❌ Chưa có |
| PUT | `/api/localizations/{poi_id}/{lang}` | Admin sửa bản dịch thủ công | ⚠️ Có (TranslationsController) |
| DELETE | `/api/localizations/{poi_id}/{lang}` | Xóa bản dịch | ❌ Chưa có |

### 9.4 Localization Flows

**Flow 1 — Bulk Load (mobile app startup):**
- `GET /api/pois/load-all?lang=ja` → hydrate `poi_localizations` → 3-Tier Fallback → trả JSON
- `audio_url` decorated với `?v={mtime}&l={lang}` để cache-bust khi thay đổi

**Flow 2 — Hotset (khi mở app lần đầu):**
- Mobile app lấy GPS → tìm top 10 POI trong 1.5km
- `POST /api/localizations/prepare-hotset` với `{ poi_ids: [...], lang: "ja" }`
- Server dịch + TTS song song (Semaphore 3) → trả sau ≤ 5s
- Mobile app cập nhật audio cache cho 10 POI đó

**Flow 3 — On-demand (user vào zone POI chưa có lang):**
- Geofence detect POI có `is_fallback=true`
- `POST /api/localizations/on-demand` với `{ poi_id, lang }`
- Server dịch + TTS ngay (2-3s) → trả `audio_url` mới
- Rate limit: 30 req / 10 phút per client
- Mobile app update local cache, phát audio

**Flow 4 — Warmup (background):**
- `POST /api/localizations/warmup?lang=en` → job dịch toàn bộ POI cho lang đó
- Response ngay: `{ "status": "started", "estimated_time_seconds": 120 }`
- Khi xong: mobile app download audio pack
- Sau warmup: user có thể đi tour 100% offline

### 9.5 Mobile App Localization Cache
- Audio files lưu tại `audio_cache/{lang}/poi_{id}.mp3`
- Maximum 300 files / ngôn ngữ (LRU eviction khi đầy)
- Maximum 3 ngôn ngữ active đồng thời
- SQLite: `AudioCacheEntry` table tracking `{poi_id, lang, file_path, sha256, downloaded_at}`

---

## 10. Module 5 — GPS & Geofencing

### 10.1 Current State
Đã implement tốt. Các cải tiến cần thiết:

### 10.2 Improvements Needed

**SimulatedGPS toggle (Mobile App):**
- `AppConfig.UseSimulatedGPS` cần đọc từ `Preferences` thay vì hardcode `true`
- `SettingsPage` cần toggle UI để bật/tắt GPS simulation
- Default: `false` (production), `true` (development)

**Virtual Tour auto-activation:**
- Khi user > 1km khỏi trung tâm Vĩnh Khánh → tự động kích hoạt virtual tour mode
- Cần thanh thông báo "Bạn đang ở xa khu vực — đang dùng Tour ảo"

**Background GPS Service (Android):**
- `GpsForegroundService` hiện có — cần notification text cập nhật khi có POI mới
- Battery optimization: throttle GPS polling to 5s khi app ở background

### 10.3 Geofencing Constants (cần đồng bộ với backend)

| Constant | Value | Location |
|---|---|---|
| GPS Throttle | 5s | LocationService |
| Geofence Debounce | 3s (confirm ENTER) | GeofenceService |
| Geofence Default Radius | 30m | AppConfig / POI field |
| Geofence Cooldown | 5 phút per zone | GeofenceService |
| Heartbeat | 1s | GeofenceService |
| Prefetch Queue | Top 3 / batch, gate ≥ 30s | AudioCacheService |
| Hotset Nearby Radius | 1500m | ZoneRepository |

---

## 11. Module 6 — Offline & Caching

### 11.1 Mobile App — SQLite Local Cache

**Tables hiện có:**
- `POIs` (cần thêm trường `data_version` cho incremental sync)
- `MenuItems`
- `ZoneHistory`

**Tables cần thêm:**
- `AudioCacheEntries`: `{ id, poi_id, lang, file_path, sha256, file_size, downloaded_at, last_used_at }`

### 11.2 Offline Data Strategy

**Startup flow (mobile):**
1. Load SQLite cache → hiển thị ngay (0ms wait)
2. Check network (`Internet` hoặc `ConstrainedInternet`)
3. Nếu online: `GET /api/pois/load-all?version={local_version}` → merge diff
4. Nếu offline: dùng cache, set badge `⚡ Offline`

**Audio offline strategy:**
- Predownload: `AudioCacheService.PreDownloadNearbyAudioAsync(radius: 1500m)`
- On-demand: khi vào zone → check local file → nếu không có → download
- Pack mode: user có thể download toàn bộ audio pack trước chuyến đi

### 11.3 API — Audio Pack Download Flow

```
1. Mobile app: GET /api/audio/pack-manifest?lang=vi
2. Server trả: { pack_version, total_files: N, files: [...{url, sha256}] }
3. Mobile app: download từng file song song (max 4 concurrent)
4. Verify SHA-256 per file sau khi download
5. Lưu vào SQLite AudioCacheEntries
6. Set Preferences: "audio_pack_lang_vi_version" = pack_version
```

### 11.4 Incremental POI Sync

```
1. Mobile: GET /api/pois/load-all?lang=vi&version=42
2. Server: trả chỉ POI có data_version > 42
3. Mobile: upsert vào SQLite, giữ IsLikedByUser
4. Mobile: update Preferences "pois_data_version" = new_version
5. Mobile: set CurrentDataSource = LiveApi (xóa offline badge)
```

---

## 12. Module 7 — Maps

### 12.1 Current State
Mobile app dùng Mapsui + OSM tile cache. Không có offline map pack.

### 12.2 API Map Pack Endpoints (cần thêm)

| Method | Path | Mô tả |
|---|---|---|
| GET | `/api/maps/offline-manifest` | Manifest: bbox, checksums SHA-256, size, asset URLs |
| GET | `/api/maps/packs/{version}/{file}` | Serve file PMTiles (Range Requests) |
| GET | `/api/maps/styles/{path}` | Style JSON + sprite |
| GET | `/api/maps/fonts/{fontstack}/{range}.pbf` | Glyph PBFs |

**Security:** Mọi path trong `/api/maps/` phải resolve trong base dir — chặn Path Traversal attack.

### 12.3 Map Modes (Mobile App)

| Mode | Mô tả | Điều kiện |
|---|---|---|
| Online | OSM tiles via Mapsui (hiện tại) | Có mạng |
| Offline SQLite | OSM tiles cached trong SQLite (SqliteTileCache) | Đã cache trước |
| PMTiles (future) | Offline vector tiles Quận 4 | Đã download map pack |

### 12.4 POI geofence_radius trên Map
- Admin có thể kéo thả bán kính geofence trực tiếp trên map editor trong web portal
- Map editor dùng Leaflet.js hoặc MapLibre với editable circles

---

## 13. Module 8 — Admin Dashboard (Web Portal)

### 13.1 Current State
Static HTML/JS/CSS trong `wwwroot/`. Đã có: POI CRUD, audio management, basic analytics, tour management.

### 13.2 Improvements / Missing Features

**Auth nâng cấp:**
- [ ] httpOnly cookie (access_token 30min + refresh_token 7 ngày)
- [ ] Refresh token auto-rotate logic trong `auth-check.js`
- [ ] Logout endpoint xóa cookie

**POI Management cải tiến:**
- [ ] Map editor: kéo thả pin + chỉnh bán kính geofence trực tiếp
- [ ] Bulk import POI từ CSV/JSON
- [ ] Localization tab: xem/sửa bản dịch từng ngôn ngữ per POI
- [ ] AI Enhance button → gọi `/api/ai/enhance-description`

**Audio Tasks (SSE):**
- [ ] Page `audio-bulk-generate.html`: progress bar realtime qua EventSource
- [ ] Nút Pause / Resume / Cancel per task
- [ ] Hiển thị log output từng task

**Owner Management:**
- [ ] Trang duyệt đơn đăng ký chủ quán (`poi_owner_registrations`)
- [ ] Approve → set `is_verified=true` + gán role `poi_owner`
- [ ] Reject với lý do

**Audit Logs:**
- [ ] Trang `/admin/audit-logs.html` hiển thị `audit_logs` có filter
- [ ] Export CSV

**Analytics Dashboard:**
- [ ] Biểu đồ: Top 10 POI được nghe nhiều nhất
- [ ] Biểu đồ: Số lượt phát theo ngày/tuần
- [ ] Heatmap vị trí người dùng (nếu có consent)

**Role Management:**
- [ ] CRUD roles + permissions matrix (checkbox UI)

---

## 14. Module 9 — Owner Portal

### 14.1 Concept
Chủ quán (POI Owner) có portal riêng để quản lý gian hàng của mình — sau khi admin duyệt đơn đăng ký.

### 14.2 Registration Flow

```
1. Chủ quán: POST /api/auth/register với role = "poi_owner_applicant"
   Body: { name, email, password, phone, cccd (sẽ encrypt), business_name }
2. Server: tạo user (unverified) + tạo poi_owner_registrations (pending)
3. Admin portal: thấy badge "N đơn chờ duyệt"
4. Admin duyệt → PATCH /api/owner-registrations/{id}/approve
   Server: set user.is_verified=true + assign role "poi_owner"
5. Chủ quán nhận email thông báo → đăng nhập → vào /owner
```

### 14.3 Owner Portal Features

**Manage My POI:**
- [ ] Xem danh sách POI mình sở hữu
- [ ] Submit chỉnh sửa → `poi_submissions` (pending) → admin duyệt
- [ ] Upload ảnh gian hàng (max 8 ảnh)
- [ ] Cập nhật menu items
- [ ] Xem analytics: lượt nghe audio của gian hàng mình

**AI Advisor:**
- [ ] Nút "AI Enhance" → gọi `/api/ai/enhance-description` với rate limit 10/ngày
- [ ] Hiển thị bản suggest → chủ quán chọn apply hay không

### 14.4 API Endpoints

| Method | Path | Mô tả | Status |
|---|---|---|---|
| POST | `/api/owner/register` | Đăng ký chủ quán | ❌ Cần thêm |
| GET | `/api/owner/my-pois` | Danh sách POI của tôi | ❌ Cần thêm |
| PUT | `/api/owner/pois/{id}` | Update POI (chỉ quán mình) | ❌ Cần thêm |
| POST | `/api/owner/submissions` | Gửi yêu cầu chỉnh sửa POI | ❌ Cần thêm |
| GET | `/api/owner/analytics` | Analytics của quán mình | ❌ Cần thêm |
| GET | `/api/owner-registrations` | Admin: danh sách đơn chờ duyệt | ❌ Cần thêm |
| PATCH | `/api/owner-registrations/{id}/approve` | Admin: duyệt đơn | ❌ Cần thêm |
| PATCH | `/api/owner-registrations/{id}/reject` | Admin: từ chối đơn | ❌ Cần thêm |

### 14.5 MongoDB Collections cần thêm
- `poi_owner_registrations`: `{ _id, user_id, status, cccd_encrypted, business_name, created_at, reviewed_at, reviewed_by }`
- `poi_submissions`: `{ _id, poi_id, owner_id, changes_json, status, submitted_at, reviewed_at, reviewed_by }`

---

## 15. Module 10 — AI Advisor

### 15.1 Concept
Dùng Google Gemini 2.0 Flash để gợi ý cải thiện mô tả POI cho chủ quán/admin.

### 15.2 API Endpoint

**POST `/api/ai/enhance-description`**

Request:
```json
{
  "poi_id": "string",
  "current_description": "string (tiếng Việt)",
  "target_lang": "vi"
}
```

Response:
```json
{
  "enhanced_text": "string",
  "word_count": 245,
  "note": "Gợi ý từ AI — vui lòng review trước khi lưu"
}
```

**Prompt Engineering Rules:**
- KHÔNG được bịa thông tin mới (tên món, giá, địa chỉ)
- CÓ THỂ thêm tính từ tích cực và phong phú hơn về trải nghiệm
- Target 200-300 từ
- Giữ nguyên facts (tên, giá, giờ mở cửa)

### 15.3 Rate Limiting

| Role | Giới hạn |
|---|---|
| poi_owner | 10 lần / ngày (reset 00:00) |
| admin | Unlimited |
| super_admin | Unlimited |

**MongoDB Collection:** `ai_usage_limits`: `{ user_id, date_str, count, last_used_at }`

### 15.4 Config
- `GEMINI_API_KEY` trong `appsettings.json` (tách ra `appsettings.secrets.json` / env var)
- Timeout: 30 giây
- On failure: trả error message rõ ràng, không crash

---

## 16. Module 11 — Mobile App (.NET MAUI)

### 16.1 Pending TODOs (phải xong trước launch)

**Tour Navigation Commands (MainViewModel.cs ~line 441):**
- [ ] `PrevStop()`: giảm `CurrentStopIndex` (min = 0), update `CurrentPOI`, trigger TTS
- [ ] `NextStop()`: tăng `CurrentStopIndex` (max = TourPOIs.Count - 1), update TTS
- [ ] `Bookmark()`: gọi `ToggleSavePOIAsync(CurrentPOI)`
- [ ] `SelectStop(int stopIndex)`: set `CurrentStopIndex = stopIndex`, navigate map, trigger TTS

**Settings API Sync (SettingsPage.xaml.cs):**
- [ ] Load settings: `GET /api/settings` → fallback to Preferences nếu offline
- [ ] Save settings: `PUT /api/settings` → persist to API + Preferences
- [ ] Sync on app resume

**ProfilePage Replay Tour:**
- [ ] `OnReplayTour(session)`: load POIs từ session ZoneHistory → set Tour stops → navigate to TourPage

**AppConfig Secrets:**
- [ ] `MaptilerApiKey`: đọc từ `appsettings.json` embedded resource (không hardcode)
- [ ] `ApiBaseUrl`: đọc từ `appsettings.json` (khác nhau giữa Debug/Release)
- [ ] `UseSimulatedGPS`: đọc từ `Preferences`, default `false`

**ShowDetails từ Map Tab:**
- [ ] `ShowDetails(POI)`: navigate to `POIDetailPage` via `Shell.Current.Navigation.PushModalAsync`

**Guest Mode / No Account:**
- [ ] Tourist có thể dùng app không cần đăng nhập
- [ ] Saved POIs và settings lưu local (SQLite + Preferences)
- [ ] Option "Đăng nhập / Đăng ký" trong Settings để sync cloud

### 16.2 AppConfig.cs cần refactor

```csharp
// Hiện tại (cần thay đổi):
public static string MaptilerApiKey = "YOUR_VALUE_HERE"; // BAD

// Target:
public static string MaptilerApiKey
  => AppSettings.GetValue("MaptilerApiKey") ?? string.Empty;
```

- Tạo `appsettings.json` embedded resource trong MAUI project
- Đọc qua `Microsoft.Extensions.Configuration`
- `appsettings.Debug.json` và `appsettings.Release.json` tách biệt

### 16.3 UI/UX Improvements

**SimulatedGPS Toggle:**
- Trong `SettingsPage`: thêm toggle "GPS ảo" (chỉ hiển thị khi `#if DEBUG`)
- Khi bật: hiển thị badge "🎮 GPS ảo" trên map tab

**Offline Mode Indicator:**
- Badge rõ hơn: `⚡ Offline — dữ liệu {N} ngày trước`
- Tap vào badge → show dialog mô tả trạng thái kết nối

**Audio Pack Download UI:**
- `SettingsPage`: section "Tải về offline"
- Hiển thị: ngôn ngữ đang active, dung lượng, nút "Tải gói audio" per ngôn ngữ
- Progress bar khi download

**Tour Page:**
- Stop list scrollable (hiện tại bị cắt nếu nhiều stops)
- Swipe left/right để next/prev stop
- Long-press stop → bookmark

---

## 17. Data Models

### 17.1 MongoDB Collections Summary

| Collection | Module | Status |
|---|---|---|
| `pois` | content | ✅ Có — cần thêm `data_version`, `2dsphere` index |
| `menus` (MenuItems) | content | ✅ Có |
| `audio_contents` | audio | ✅ Có |
| `narration_logs` | analytics | ✅ Có |
| `user_settings` | settings | ✅ Có — cần API sync |
| `poi_tours` | tours | ✅ Có |
| `vendor_profiles` | vendors | ✅ Có — cần refactor thành owner |
| `poi_localizations` | localization | ❌ Cần tạo mới |
| `roles` | auth | ❌ Cần tạo mới |
| `audit_logs` | auth | ❌ Cần tạo mới |
| `poi_owner_registrations` | owner | ❌ Cần tạo mới |
| `poi_submissions` | owner | ❌ Cần tạo mới |
| `ai_usage_limits` | ai | ❌ Cần tạo mới |

### 17.2 Key Indexes cần tạo

```javascript
// 2dsphere cho geospatial queries
db.pois.createIndex({ "location": "2dsphere" })

// Compound index cho localization
db.poi_localizations.createIndex({ "poi_id": 1, "lang": 1 }, { unique: true })

// Index cho sync version
db.pois.createIndex({ "data_version": 1 })

// Index cho audit logs
db.audit_logs.createIndex({ "timestamp": -1 })
db.audit_logs.createIndex({ "user_id": 1, "timestamp": -1 })

// Index cho AI rate limit
db.ai_usage_limits.createIndex({ "user_id": 1, "date_str": 1 }, { unique: true })
```

---

## 18. API Endpoints

### 18.1 Complete API List (Target State)

| Prefix | Module | Endpoints (Count) | Status |
|---|---|---|---|
| `/api/pois` | Content | 9 endpoints | Partial |
| `/api/audio` | Audio/TTS | 10 endpoints | Partial |
| `/api/auth` | Auth | 7 endpoints | Partial |
| `/api/roles` | RBAC | 5 endpoints | ❌ Missing |
| `/api/localizations` | i18n | 7 endpoints | ❌ Missing |
| `/api/tours` | Tours | 5 endpoints | ✅ Có |
| `/api/menus` | Content | 5 endpoints | ✅ Có |
| `/api/owner` | Owner | 5 endpoints | ❌ Missing |
| `/api/owner-registrations` | Owner | 3 endpoints | ❌ Missing |
| `/api/ai` | AI Advisor | 2 endpoints | ❌ Missing |
| `/api/maps` | Maps | 4 endpoints | ❌ Missing |
| `/api/analytics` | Analytics | 4 endpoints | Partial |
| `/api/settings` | Settings | 2 endpoints | Partial |

### 18.2 Versioning
- Tất cả endpoints dùng prefix `/api/` (không versioned v1 cho đơn giản)
- Breaking changes sẽ thêm `/api/v2/` khi cần

---

## 19. Non-Functional Requirements

### 19.1 Performance
| Metric | Target |
|---|---|
| `GET /api/pois/load-all` response time | < 1s (< 200 POIs) |
| Audio TTS synthesis (Tier 2) | < 8s |
| On-demand localization (Tier 1.5) | < 5s |
| App startup → map visible | < 3s |
| Map pin render (50 POIs) | < 500ms |

### 19.2 Offline Capability
| Scenario | Behavior |
|---|---|
| Không có internet khi mở app | Load từ SQLite cache, hiện offline badge |
| Internet mất khi đang tour | Phát từ local audio cache, không interrupt |
| Vào zone POI chưa có audio | Dùng native TTS fallback |
| Không có GPS | Disable geofencing, hiện thông báo |

### 19.3 Security
- Tất cả API cần auth đều yêu cầu JWT (Bearer hoặc Cookie)
- Permission check: mỗi endpoint decorated với `[RequirePermission("poi:delete")]`
- SQL Injection: không dùng raw query, dùng MongoDB Driver LINQ
- Path Traversal: resolve và validate path trong base dir (map/audio endpoints)
- XSS: sanitize text trước khi lưu DB và render HTML
- CORS: whitelist explicit origins, không dùng wildcard `*` trong production
- Rate limiting: endpoints nhạy cảm (TTS, AI, on-demand translate)

### 19.4 Reliability
- Database: MongoDB replica set (hoặc Atlas free tier)
- TTS failure: retry 1 lần → fallback gracefully
- API timeout: 30s cho AI, 10s cho TTS
- Graceful degradation: app hoạt động khi API down (dùng cache)

---

## 20. Lộ Trình Triển Khai

### Phase 1 — Fix Critical Mobile App TODOs (Sprint 1, 1-2 tuần)

| Task | File | Priority |
|---|---|---|
| Implement `PrevStop/NextStop/SelectStop/Bookmark` | MainViewModel.cs | P0 |
| Fix `ShowDetails` từ Map tab | MainPage.Map.cs | P0 |
| Refactor AppConfig — đọc từ appsettings.json | AppConfig.cs | P0 |
| Wire `SettingsPage` → API sync | SettingsPage.xaml.cs | P1 |
| Wire `ProfilePage` replay tour | ProfilePage.xaml.cs | P1 |
| SimulatedGPS toggle từ SettingsPage | SettingsPage.xaml | P1 |
| Set `UseSimulatedGPS = false` default | AppConfig.cs | P0 |

### Phase 2 — Backend: RBAC + Owner Portal (Sprint 2, 2-3 tuần)

| Task | Status |
|---|---|
| Tạo collection `roles` + seed 4 default roles | ❌ |
| Dynamic RBAC: JWT payload chứa permissions[] | ❌ |
| `[RequirePermission]` attribute + middleware | ❌ |
| httpOnly cookie auth + refresh token | ❌ |
| Owner registration flow + approval | ❌ |
| `poi_owner_registrations` collection | ❌ |
| `audit_logs` collection + auto-logging middleware | ❌ |
| PII encryption cho CCCD | ❌ |
| Role management UI trong Admin portal | ❌ |
| Owner portal web pages | ❌ |

### Phase 3 — Backend: Localization System (Sprint 3, 2-3 tuần)

| Task | Status |
|---|---|
| Tạo collection `poi_localizations` + compound index | ❌ |
| 3-Tier Content Fallback logic | ❌ |
| `GET /api/pois/load-all?lang=&version=` cải tiến | ❌ |
| `POST /api/localizations/on-demand` | ❌ |
| `POST /api/localizations/prepare-hotset` | ❌ |
| `POST /api/localizations/warmup` (background) | ❌ |
| Mobile app: integrate hotset call on startup | ❌ |
| Mobile app: on-demand call khi vào zone is_fallback | ❌ |
| 2dsphere index + `/api/pois/nearby` endpoint | ❌ |

### Phase 4 — AI Advisor + SSE Audio Tasks (Sprint 4, 1-2 tuần)

| Task | Status |
|---|---|
| Gemini API integration | ❌ |
| `POST /api/ai/enhance-description` endpoint | ❌ |
| `ai_usage_limits` collection + rate limit | ❌ |
| AI Advisor button trong Admin/Owner portal | ❌ |
| Background TTS task manager (IHostedService) | ⚠️ Cần refactor |
| SSE endpoint `GET /api/audio/tasks/stream` | ❌ |
| Admin portal: realtime progress bar via EventSource | ❌ |
| Audio pack manifest endpoint | ❌ |

### Phase 5 — Offline Audio Pack + Map Pack (Sprint 5, 2-3 tuần)

| Task | Status |
|---|---|
| Audio pack manifest endpoint + SHA-256 | ❌ |
| Mobile app: Audio Pack download UI + progress | ❌ |
| SQLite AudioCacheEntries table | ❌ |
| LRU eviction cho audio cache | ❌ |
| PMTiles map pack API endpoints | ❌ |
| Path Traversal security cho map endpoints | ❌ |
| Mobile app: cải thiện tile cache (SqliteTileCache) | ⚠️ Có — cần test |

### Phase 6 — Polish & Production Readiness (Sprint 6, 1-2 tuần)

| Task | Status |
|---|---|
| Incremental POI sync (`data_version` field) | ❌ |
| Export analytics CSV | ❌ |
| Analytics dashboard biểu đồ | ❌ |
| Performance testing (load-all < 1s) | ❌ |
| Security audit (CORS, Content-Security-Policy) | ❌ |
| Mobile app: Guest mode (no login required) | ❌ |
| Documentation cập nhật API (Swagger) | ⚠️ Cần kiểm tra |
| Deployment checklist | ❌ |

---

## 21. Definition of Done

### 21.1 Feature Done Criteria
- [ ] Code implemented và compile không lỗi
- [ ] Unit test (nếu là service/logic phức tạp)
- [ ] Manual test trên Android emulator VÀ real device
- [ ] API test qua Postman/Swagger
- [ ] Không có lỗi trong console log bình thường
- [ ] Offline scenario test (tắt WiFi)
- [ ] Code review (self-review hoặc pair)

### 21.2 Sprint Done Criteria
- [ ] Tất cả P0 tasks của phase đó hoàn thành
- [ ] Không có TODO comments chưa xử lý trong code mới
- [ ] MEMORY.md cập nhật pattern mới

### 21.3 Release Done Criteria
- [ ] Tất cả Phase 1-4 hoàn thành
- [ ] App chạy hoàn toàn offline (phase 5)
- [ ] Admin portal đầy đủ chức năng
- [ ] Owner portal hoạt động
- [ ] AppConfig không còn `YOUR_VALUE_HERE`
- [ ] `UseSimulatedGPS = false` trong Release build
- [ ] Secrets không commit vào Git (dùng appsettings.secrets.json + .gitignore)

---

## Appendix A — Constants Reference

| Constant | Value | Vị trí |
|---|---|---|
| Access Token Expire | 30 phút | AuthController |
| Refresh Token Expire | 7 ngày | AuthController |
| Role Cache TTL | 300s | RBAC Middleware |
| Max Concurrent TTS | 3 | Background TTS Service |
| PII Retention Days | 180 ngày | Owner Service |
| Max POI Images | 8 | POI Service |
| Max Image Size | 5 MB | POI Service |
| On-Demand Rate Limit | 30 req / 10 phút | Localization Service |
| Hotset Max POI IDs | 10 | Localization Service |
| AI Daily Limit (Owner) | 10 | AI Service |
| GPS Throttle | 5s | LocationService |
| Geofence Debounce | 3s | GeofenceService |
| Geofence Default Radius | 30m | AppConfig / POI |
| Geofence Cooldown | 5 phút | GeofenceService |
| Heartbeat Interval | 1s | GeofenceService |
| Audio Cache Max Per Lang | 300 files | AudioCacheService |
| Max Language Caches | 3 | AudioCacheService |
| Hotset Nearby Radius | 1500m | ZoneRepository |
| POI Cache TTL | 15 phút | ZoneRepository |
| Audio Max File Size | 25 MB | AudioController |
| Q4 Bounding Box | 106.69–106.715, 10.745–10.765 | AppConfig |

---

## Appendix B — File Paths Reference

| File | Mục đích |
|---|---|
| `MobileApp/.../ViewModels/MainViewModel.cs` | Tất cả observable state + commands |
| `MobileApp/.../Core/Services/IServices.cs` | Tất cả service interfaces |
| `MobileApp/.../Core/Services/GeofenceService.cs` | Geofencing engine |
| `MobileApp/.../Core/Services/ZoneRepository.cs` | POI data layer + sync |
| `MobileApp/.../Core/Services/Implementations/LocalDatabaseService.cs` | SQLite |
| `MobileApp/.../Core/Services/Implementations/TextToSpeechService.cs` | TTS |
| `MobileApp/.../Views/TourPage.xaml.cs` | Tour page logic |
| `MobileApp/.../Views/POIDetailPage.xaml.cs` | POI detail modal |
| `MobileApp/.../AppConfig.cs` | App-wide config (cần refactor) |
| `MobileApp/.../MauiProgram.cs` | DI registration |
| `API/.../Controllers/POIsController.cs` | POI CRUD |
| `API/.../Controllers/TTSController.cs` | TTS synthesis |
| `API/.../Controllers/AudioController.cs` | Audio file management |
| `API/.../Data/MongoDbContext.cs` | MongoDB collections |
| `API/.../Program.cs` | DI, middleware, startup |
| `API/wwwroot/*.html` | Web admin/vendor portal |
| `tts_wrapper.py` | Python Edge-TTS CLI wrapper |

---

*PRD này cover toàn bộ hệ thống dựa trên architecture presentation và trạng thái hiện tại của project. Cập nhật khi có thay đổi requirement.*
