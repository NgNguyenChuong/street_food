# 🍜 Street Food Narrator - Hệ Thống Thuyết Minh Tự Động Đa Ngôn Ngữ

![.NET 10.0](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)
![MAUI](https://img.shields.io/badge/MAUI-10.0-512BD4?logo=dotnet)
![MongoDB](https://img.shields.io/badge/MongoDB-2.25-47A248?logo=mongodb)
![C#](https://img.shields.io/badge/C%23-12.0-239120?logo=csharp)
![University Project](https://img.shields.io/badge/Project-University-blue)

## 📖 Giới Thiệu Dự Án

**Street Food Narrator** là hệ thống ứng dụng di động thông minh giúp du khách tự động nghe thuyết minh về các quán ăn và điểm tham quan tại **Phố Ẩm Thực Vĩnh Khánh** (Quận 4, TP.HCM) bằng công nghệ định vị GPS và geofencing.

> 🎓 **Lưu ý**: Đây là project đồ án tốt nghiệp đại học, tập trung vào các tính năng core và khả thi trong thời gian học kỳ. 

### 🎯 Mục Tiêu Dự Án

- **Nâng cao trải nghiệm du khách**: Cung cấp thông tin chi tiết, sinh động về các điểm ẩm thực mà không cần hướng dẫn viên
- **Đa ngôn ngữ**: Hỗ trợ nhiều ngôn ngữ (Việt, Anh, Nhật, Hàn...) để thu hút du khách quốc tế
- **Tự động hóa thông minh**: Tự động phát nội dung thuyết minh khi người dùng đi vào vùng geofence
- **Onboarding nhanh chóng**: Đăng nhập Google hoặc Guest Mode - không cần đăng ký phức tạp
- **Hoạt động offline**: Tải trước dữ liệu để sử dụng mà không cần Internet liên tục
---

## 🗂️ Cấu Trúc Thư Mục

```
street_food/
│
├── 📁 API/                              # Backend REST API
│   └── StreetFoodNarrator.API/
│       ├── Controllers/                  # 7 Controllers (POIs, Audio, Auth, TTS, Vendors...)
│       ├── Models/                       # Data models (POI, Tour, User, Audio...)
│       ├── Data/                         # MongoDB context, seed data
│       └── Program.cs                    # Entry point (.NET 10.0)
│
├── 📁 MobileApp/                        # Mobile Application (MAUI)
│   └── StreetFoodNarrator.App/
│       ├── Views/                        # UI Pages (Welcome, Main, Settings)
│       ├── ViewModels/                   # MVVM ViewModels
│       ├── Core/
│       │   ├── Services/                 # Location, Geofence, Audio Services
│       │   ├── Models/                   # Domain models
│       │   └── Managers/                 # Business logic managers
│       ├── Data/
│       │   ├── DTOs/                     # Data transfer objects
│       │   └── Repositories/             # Local database (SQLite)
│       └── Resources/                    # Images, fonts, audio files
│
├── 📁 Database/                         # MongoDB Scripts & Documentation
│   ├── SeedData.mongodb.js              # Sample data
│   ├── nested-zones-seed.js             # Geofence zones data
│   └── CreateSchema.sql                 # SQL schema (legacy)
│
├── 📁 Documentation/                    # Technical Documentation
│   ├── Geofencing-Technical-PRD.md      # System architecture & requirements
│   ├── Geofencing-Pseudo-Code.md        # Algorithm implementation guide
│   ├── Geofencing-Implementation-Checklist.md
│   ├── Geofencing-Testing-Guide.md      # QA testing scenarios
│   └── READY-TO-TEST.md                 # Quick start guide
│
├── 📁 Design/                           # UI/UX Assets
│   ├── Icons/                           # App icons
│   └── Logo/                            # Brand logos
│
├── 📁 Tools/                            # Utility scripts
├── 📁 scripts/                          # Build & deployment scripts
│   └── vscode-run-android.ps1           # Android emulator launcher
│
├── street_food.sln                      # Visual Studio Solution
├── global.json                          # .NET SDK version
├── plan.md                              # Project requirements (Vietnamese)
└── README.md                            # This file
```

---

## ✨ Tính Năng Chính

### 🎯 1. **Smart Geofencing System** (⭐ Tính năng nổi bật)

**Hệ thống định vị thông minh với vùng chồng lấn đa cấp:**

- ✅ **Nested Zones (Vùng lồng nhau)**: Hỗ trợ 3 cấp độ
  - 🏞️ **Area** (Khu vực lớn - 500m): "Phố Ẩm Thực Vĩnh Khánh"
  - 🏘️ **District** (Cụm gian hàng - 100m): "Cụm hải sản"
  - 📍 **Spot** (Quán cụ thể - 10-15m): "Quán Bà Năm"

- ✅ **Priority-based Selection**: Tự động chọn vùng ưu tiên cao nhất
  - Công thức thông minh: `Score = (11 - ZoneLevel) × 100 + Priority × 10 - Distance`
  - Vùng nhỏ hơn (Spot) được ưu tiên hơn vùng lớn (Area)

- ✅ **Audio Ducking**: Giảm âm lượng nội dung cha khi vào vùng con
  - Vào District → Audio của Area giảm xuống 20%
  - Vào Spot → Audio của District pause

- ✅ **Anti-Spam Protection**:
  - **Cooldown**: Không phát lại cùng POI trong 10 phút
  - **Debounce**: Chống nhiễu GPS (5s + 10m threshold)

### 🗣️ 2. **Thuyết Minh Đa Ngôn Ngữ**

- **Text-to-Speech (TTS)**: Tích hợp Google TTS / Azure Cognitive Services
- **Pre-recorded Audio**: Giọng đọc chuyên nghiệp, tự nhiên
- **Ngôn ngữ hỗ trợ**: 🇻🇳 Tiếng Việt | 🇬🇧 English | 🇯🇵 日本語 | 🇰🇷 한국어
- **Audio Queue Management**: Không phát trùng lặp, tự động dừng khi có thông báo

### 📍 3. **GPS Tracking Real-time**

- **Foreground + Background tracking**: Hoạt động ngay cả khi app ở chế độ nền
- **Battery Optimization**: Cập nhật vị trí mỗi 10 giây (có thể điều chỉnh)
- **High Accuracy**: Sử dụng FusedLocationProviderClient (Android) + CLLocationManager (iOS)

### 🗺️ 4. **Map View & POI Management**

- Hiển thị vị trí người dùng trên bản đồ (Microsoft.Maui.Maps)
- Hiển thị tất cả POIs với icons phân loại
- Highlight POI gần nhất / đang active
- Xem chi tiết POI: Mô tả, ảnh, audio

### 📱 5. **QR Code Activation**

- Quét QR code tại điểm dừng xe buýt để kích hoạt nội dung ngay
- Không cần GPS, phù hợp cho trường hợp quan tâm trước khi đến
- QR có thời hạn (5 ngày)

### 🔐 6. **User Management & Authentication**

**Phiên bản hiện tại (Simple Authentication):**
- 🌐 **Đăng nhập Google OAuth 2.0**: Nhanh chóng, bảo mật, không cần tạo mật khẩu mới
- 👤 **Guest Mode (Tiếp tục với tư cách khách)**: Dùng ngay không cần đăng nhập
  - Dữ liệu lưu local trên thiết bị
  - Không đồng bộ giữa các thiết bị
  
**Kế hoạch tương lai:**
- Đăng ký tài khoản đầy đủ để lưu lịch sử, preferences
- Đồng bộ dữ liệu đa thiết bị
- 3 vai trò:
  - **Tourist**: Người dùng app mobile
  - **Vendor**: Chủ gian hàng quản lý thông tin quán của mình
  - **Admin**: Quản lý toàn bộ hệ thống

### 📊 7. **Analytics & Reporting** (Đang phát triển)

- Lưu tuyến di chuyển của người dùng (ẩn danh - GDPR compliant)
- Top POIs được nghe nhiều nhất
- Thời gian trung bình nghe mỗi POI
- Heatmap vị trí user
- Thống kê món ăn/gian hàng phổ biến nhất

---

## 🚀 Tính Năng Nổi Bật Thu Hút Khách Hàng

### 🌟 1. **Trải Nghiệm Hands-free Hoàn Toàn**
> Không cần cầm điện thoại, mở app - chỉ cần để trong túi, hệ thống tự động phát audio khi bạn đi qua các điểm. Như có hướng dẫn viên riêng!

### 🧠 2. **AI-powered Priority System**
> Thuật toán thông minh tự động xác định bạn đang ở vị trí nào (quán cụ thể? cụm gian hàng? hay khu vực chung?) và phát nội dung phù hợp nhất. Không bao giờ bị spam hoặc nghe nhầm thông tin.

### 🌐 3. **Truly Offline-first**
> Tải một lần, dùng mãi mãi. Không lo hết data giữa chợ. Phù hợp cho du khách quốc tế không có SIM Việt Nam.

### 🎭 4. **Localized Content**
> Không chỉ dịch ngôn ngữ - nội dung được "bản địa hóa" cho từng quốc gia:
> - Người Nhật nghe về nguồn gốc hải sản tươi sống
> - Người Hàn nghe về độ cay và cách ăn kèm
> - Người Việt nghe về lịch sử và câu chuyện chủ quán

### ⚡ 5. **Zero Friction Onboarding**
> Đăng nhập Google trong 2 giây hoặc dùng ngay với Guest Mode - không cần đăng ký tài khoản phức tạp. Quét QR là khám phá ngay. Giảm 90% barrier to entry.

### 📈 6. **Data-driven Insights cho Vendors**
> Chủ quán biết:
> - Bao nhiêu người dừng tại quán
> - Thời gian trung bình họ nghe thuyết minh
> - Ngôn ngữ nào phổ biến nhất (→ biết khách chủ yếu từ đâu)
> - So sánh với các quán khác trong khu

---

## 💾 Lấy Dữ Liệu Như Thế Nào?

### 1. **Dữ Liệu POI (Points of Interest)**

**Nguồn:**
- ✍️ **Nhập thủ công ban đầu**: Admin nhập thông tin quán ăn (tên, mô tả, tọa độ GPS, bán kính)
- 🏪 **Vendor tự cập nhật**: Chủ quán có dashboard riêng để chỉnh sửa
- 🗺️ **Google Maps API**: Lấy tọa độ chính xác khi biết địa chỉ

**Dữ liệu bao gồm:**
```json
{
  "name": "Quán Bà Năm",
  "location": {
    "type": "Point",
    "coordinates": [106.6950, 10.7597]  // [longitude, latitude]
  },
  "radius": 15,  // meters
  "zoneType": "Spot",  // Area/District/Spot
  "priority": 8,  // 1-10
  "description": {
    "vi": "Nổi tiếng với hải sản tươi sống...",
    "en": "Famous for fresh seafood...",
    "ja": "新鮮なシーフードで有名...",
    "ko": "신선한 해산물로 유명..."
  },
  "audioUrls": {
    "vi": "/audio/ba-nam-vi.mp3",
    "en": "/audio/ba-nam-en.mp3"
  }
}
```

### 2. **Dữ Liệu GPS của User**

**Nguồn:**
- 📡 **Device GPS**: Android FusedLocationProvider / iOS CLLocationManager
- **Chế độ:**
  - Foreground: Cập nhật mỗi 5-10 giây
  - Background: Geofence monitoring (battery-efficient)

**Quyền riêng tư:**
- ✅ Chỉ lưu dữ liệu ẩn danh cho analytics
- ✅ Không chia sẻ với bên thứ 3
- ✅ User có thể tắt tracking bất kỳ lúc nào

### 3. **Nội Dung Thuyết Minh**

**Nguồn:**
- ✍️ **Biên tập viên/Admin tạo nội dung**: Viết script thuyết minh
- 🗣️ **Text-to-Speech API**: Azure Cognitive Services / Google Cloud TTS
- 🎙️ **Thu âm chuyên nghiệp**: Thuê giọng đọc cho chất lượng cao
- 🌐 **Dịch thuật**: Dịch viên native speaker hoặc AI translation + review

### 4. **Hình Ảnh & Media**

**Nguồn:**
- 📸 **Chủ quán cung cấp**: ảnh món ăn, không gian quán
- 📷 **Photographer thuê riêng**: chụp professional cho marketing
- 🌐 **Crawl từ Google/Facebook** (với permission)

### 5. **Dữ Liệu Analytics**

**Nguồn:**
- 📊 **MongoDB Time-series Collections**: Lưu events từ app
  - `user_entered_zone`
  - `audio_played`
  - `audio_completed`
  - `user_exited_zone`
- 🔍 **Query & Aggregation**: Pipeline để tính toán insights

---

## 🛠️ Công Nghệ Đang Sử Dụng

### **Backend (API Server)**

| Công nghệ | Phiên bản | Mô tả |
|-----------|-----------|-------|
| **C# .NET** | 10.0 | Framework chính |
| **ASP.NET Core Web API** | 10.0 | RESTful API |
| **MongoDB** | 2.25.0 | Database chính (NoSQL) |
| **MongoDB.Driver** | 2.25.0 | .NET driver cho MongoDB |
| **ASP.NET Identity** | 10.0 | User management (kế hoạch) |
| **JWT Bearer** | 10.0.2 | Token-based auth (kế hoạch) |
| **Google OAuth 2.0** | - | Authentication hiện tại |
| **AspNetCore.Identity.MongoDbCore** | 3.1.0 | Identity with MongoDB (kế hoạch) |
| **Swagger / OpenAPI** | 10.1.1 | API documentation |

**Tại sao chọn MongoDB?**
- ✅ GeoJSON native support (geospatial queries)
- ✅ Flexible schema (dễ thêm ngôn ngữ mới)
- ✅ Horizontal scaling (chuẩn bị cho nhiều khu phố khác)

### **Mobile App**

| Công nghệ | Phiên bản | Mô tả |
|-----------|-----------|-------|
| **.NET MAUI** | 10.0 | Cross-platform framework (Android + iOS) |
| **C#** | 12.0 | Ngôn ngữ lập trình |
| **Microsoft.Maui.Controls** | 10.0 | UI framework |
| **Microsoft.Maui.Maps** | 10.0 | Map integration |
| **Essentials: Geolocation** | - | GPS tracking |
| **SQLite** | - | Local database (offline storage) |
| **CommunityToolkit.Mvvm** | - | MVVM helpers |
| **Newtonsoft.Json** | - | JSON serialization |

**Tại sao chọn MAUI?**
- ✅ Single codebase cho Android + iOS (tiết kiệm 50% công sức)
- ✅ Native performance
- ✅ Full access native APIs (GPS, TTS, Background services)
- ✅ Microsoft long-term support

### **Development Tools**

- **IDE**: Visual Studio 2022 / Visual Studio Code
- **Version Control**: Git
- **Build**: MSBuild, dotnet CLI
- **Emulator**: Android Emulator / iOS Simulator
- **API Testing**: Swagger UI, Postman
- **Database GUI**: MongoDB Compass

### **Cloud & DevOps** (Kế hoạch)

- **Hosting**: Azure App Service / AWS EC2
- **Database**: MongoDB Atlas (managed)
- **Storage**: Azure Blob Storage (audio files)
- **CDN**: Azure CDN (tối ưu tốc độ download)
- **CI/CD**: GitHub Actions
- **Monitoring**: Application Insights

---

## 📅 Dự Định Phát Triển Thêm

### ✅ **Phase 1: PoC (Proof of Concept)** - Đã hoàn thành
- [x] Geofencing core logic
- [x] GPS tracking (stub)
- [x] Audio playback system
- [x] MongoDB schema với GeoJSON
- [x] API Controllers cơ bản
- [x] Mobile app UI/UX prototype

### 🔄 **Phase 2: MVP (Minimum Viable Product)** - Đang làm (80%)
- [x] Real GPS tracking (foreground + background)
- [x] Offline database (SQLite)
- [ ] Sync mechanism (online ↔ offline)
- [x] Multi-language support (UI + content)
- [ ] Google OAuth 2.0 integration
- [ ] Guest mode implementation
- [ ] QR code activation
- [ ] Real TTS integration
- [ ] Pre-recorded audio playback

### 🚀 **Phase 3: Production Ready** - Kế hoạch Q2 2026
- [ ] Google OAuth authentication
- [ ] Guest mode hoàn chỉnh
- [ ] Vendor dashboard (web) - cơ bản
- [ ] Admin CMS (web portal) - cơ bản
- [ ] Push notifications
- [ ] Rate & review POIs
- [ ] Social sharing (Facebook, Instagram)

### 🌟 **Phase 4: Tính Năng Mở Rộng** (Sau khi bảo vệ đồ án)

> 💡 **Lưu ý**: Các tính năng này không bắt buộc cho project đại học, chỉ phát triển nếu có thời gian hoặc sau khi tốt nghiệp.

**1. Enhanced User Experience**
- Favorite POIs & custom tours
- Lịch sử các điểm đã ghé
- Gợi ý dựa trên thời gian trong ngày
- Dark mode

**2. Basic Social Features**
- Share POI qua social media
- Check-in tại POIs
- Review & rating system

**3. Improved Analytics**
- Dashboard cho vendors: traffic, thời gian nghe TB
- Admin analytics: Popular POIs, peak hours
- Export reports (PDF/Excel)

**4. Content Management**
- Web portal cho admin upload audio/images
- Bulk import POIs từ CSV
- Preview audio trước khi publish

**5. Performance Optimization**
- Audio caching thông minh
- Background sync khi có WiFi
- Reduce battery consumption

---

## 🏃‍♂️ Hướng Dẫn Chạy Nhanh

### **Prerequisites**

```powershell
# 1. Cài đặt .NET 10 SDK
dotnet --version  # Phải >= 10.0

# 2. Cài đặt MAUI workload
dotnet workload install maui

# 3. Cài MongoDB (hoặc dùng MongoDB Atlas cloud)
# Download tại: https://www.mongodb.com/try/download/community

# 4. Cài Android SDK (cho emulator)
# Thông qua Visual Studio Installer
```

### **Chạy API Server**

```powershell
cd API\StreetFoodNarrator.API

# Cập nhật connection string trong appsettings.json
# "ConnectionString": "mongodb://localhost:27017"

# Load seed data
mongosh < ..\..\Database\nested-zones-seed.js

# Run API
dotnet run

# API chạy tại: https://localhost:7148
# Swagger: https://localhost:7148/swagger
```

### **Chạy Mobile App**

**Option 1: Visual Studio**
1. Mở `street_food.sln`
2. Set startup project → `StreetFoodNarrator.App`
3. Chọn Android Emulator
4. Nhấn F5

**Option 2: Command Line**
```powershell
cd MobileApp\StreetFoodNarrator.App
dotnet build -t:Run -f net10.0-android
```

**Option 3: VS Code Task**
```powershell
# Ctrl + Shift + P → "Run Task" → "Run MAUI Android (Emulator)"
```

### **Test Geofencing**

1. Mở app → Tap "Initialize Services"
2. Tap "🚶 Simulate Walk" để giả lập đi bộ qua các vùng
3. Quan sát logs: Zone transitions, audio ducking, priority scores
4. Tap "🗑️ Reset History" để test lại với cooldown cleared

> Xem chi tiết: [READY-TO-TEST.md](Documentation/READY-TO-TEST.md)

---

## 📚 Tài Liệu Kỹ Thuật

| Tài liệu | Mô tả |
|----------|-------|
| [Geofencing-Technical-PRD.md](Documentation/Geofencing-Technical-PRD.md) | System architecture, requirements, data model |
| [Geofencing-Pseudo-Code.md](Documentation/Geofencing-Pseudo-Code.md) | Thuật toán chi tiết (pseudo-code) |
| [Geofencing-Implementation-Checklist.md](Documentation/Geofencing-Implementation-Checklist.md) | Progress tracking |
| [Geofencing-Testing-Guide.md](Documentation/Geofencing-Testing-Guide.md) | QA test scenarios |
| [READY-TO-TEST.md](Documentation/READY-TO-TEST.md) | Quick start guide |
| [plan.md](plan.md) | Requirements gốc (Vietnamese) |

---

## 👥 Vai Trò trong Project

- **Tourist/Du khách**: Sử dụng mobile app để nghe thuyết minh tự động
- **Vendor/Chủ gian hàng**: Quản lý thông tin quán, xem analytics (qua web)
- **Admin**: Quản lý toàn bộ hệ thống, POIs, users, content (qua CMS)

---

## 📞 Liên Hệ & Đóng Góp

Project này đang trong giai đoạn phát triển tích cực. Mọi ý kiến đóng góp, báo lỗi, đề xuất tính năng đều được hoan nghênh!

---

## 📄 License

Copyright © 2026 Street Food Narrator Project. All rights reserved.

---

<div align="center">
  <b>🍜 Made with ❤️ for Vĩnh Khánh Food Street 🍜</b>
</div>