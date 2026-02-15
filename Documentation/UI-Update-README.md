# Street Food Narrator - UI Update

## 📱 Giao Diện Mới

Ứng dụng được thiết kế lại hoàn toàn dựa trên reference app VirtualTourGuide với các tính năng:

### ✨ Tính Năng Chính

1. **Bản đồ Mapsui Full-Screen**
   - Hiển thị vị trí người dùng  
   - Vẽ vùng geofence (circles) cho các POIs
   - Tự động căn giữa theo vị user
   - Tích hợp OpenStreetMap tiles (miễn phí)

2. **Top Bar**
   - Tiêu đề app + tọa độ GPS (khi simulate)
   - Badge hiển thị số vùng đang trong
   - GPS Status Button (tròn) - tap để Start/Stop tracking

3. **Floating Zone Card**
   - Hiển thị khi vào bất kỳ zone nào
   - Header màu gradient theo loại zone (Area/District/Spot)
   - Mô tả zone
   - Audio Player UI (play/pause, progress bar, thời gian)
   - Quick actions: Xem trên bản đồ, Chi tiết

4. **Bottom Control Panel**
   - Thông báo trạng thái khi không trong zone
   - Simulator controls (dev mode) - bước đi từng waypoint
   - Start/Stop GPS button (lớn, xanh)
   - Settings button (mở drawer)

5. **Settings Drawer**
   - Slide từ bên phải
   - Thống kê phiên: số zones đã nghe, cooldowns
   - Danh sách zones đang trong
   - Nhật ký hoạt động (logs scrollable)
   - Reset phiên button

### 🎨 Design System

**Colors:**
- Primary: #3B82F6 (Blue) - Area zones
- Success: #10B981 (Green) - GPS active, tracking
- Warning: #F59E0B (Orange) - Spot zones  
- Danger: #EF4444 (Red) - User pin
- Secondary: #8B5CF6 (Purple) - District zones

**Typography:**
- Page Title: 20pt Bold
- Zone Name: 18pt Bold
- Body: 15pt Regular
- Caption: 13pt Regular
- Badge: 11pt Bold

## 🔧 Kiến Trúc

```
Models/
├─ POI.cs               // POI entity với multi-language support
├─ UserSession.cs       // Track played zones, cooldowns, audio positions
└─ ActiveGeofence.cs    // (legacy)

Services/
├─ IServices.cs          // Interfaces: IZoneRepository, IAudioService, ILocationService
├─ ZoneRepository.cs     // Mock data provider (4 POIs: Area + 3 Spots)
├─ AudioService.cs       // Stub implementation - logs to debug console
├─ LocationService.cs    // Real GPS + SimulatedLocationService với waypoints
└─ IGeofenceService.cs   // Core geofencing logic với events

ViewModels/
└─ GeofenceTestViewModel.cs  // MVVM với events wiring

Views/
├─ GeofenceTestPage.xaml      // New UI với Mapsui map
└─ GeofenceTestPage.xaml.cs   // Map initialization & drawing

Converters/
└─ InvertedBoolConverter.cs   // ZoneTypeToColor, BoolToTrackingLabel, InverseBool
```

## 🚀 Cách Sử Dụng

### 1. Build & Run

```powershell
cd D:\project\street_food\MobileApp\StreetFoodNarrator.App
dotnet build -f net10.0-android
dotnet run -f net10.0-android
```

Hoặc dùng VS Code task: **"Run MAUI Android (Emulator)"**

### 2. Test Workflow

1. **Start GPS**: Tap GPS button ở top-right (hoặc button lớn ở bottom)
2. **Simulate Walk** (Dev Mode): Tap button 👣 để đi qua các waypoints:
   - Outside → Area (Vĩnh Khánh) → Spot (Bánh Mì) → Spot (Gỏi Cuốn)
3. **Xem Logs**: Tap Settings button ⚙️ → Check "Nhật Ký" section
4. **Reset**: Tap "Reset Phiên" trong Settings để clear history

### 3. GPS Simulation Path

```
Waypoint 1: (10.7600, 106.6900) - Ngoài khu vực
Waypoint 2: (10.7610, 106.6910) - Đang tiến đến...
Waypoint 3: (10.7620, 106.6920) - Gần Vĩnh Khánh
Waypoint 4: (10.7626, 106.6927) - Vào KHU VĨNH KHÁNH (Area) ✅
Waypoint 5: (10.7627, 106.6928) - Đang khám phá...
Waypoint 6: (10.7628, 106.6929) - Vào BÁNH MÌ BA LẸ (Spot) ✅
Waypoint 7: (10.7630, 106.6932) - Rời Bánh Mì, còn trong Area
Waypoint 8: (10.7632, 106.6935) - Vào GỎI CUỐN (Spot) ✅
Waypoint 9: (10.7640, 106.6945) - Rời Vĩnh Khánh
Waypoint 10: (10.7650, 106.6960) - Ngoài khu vực — im lặng
```

## 🧪 Mock Data

4 POIs được seed sẵn trong `ZoneRepository.cs`:

| ID | Name | Type | Lat | Lon | Radius | Priority |
|----|------|------|-----|-----|--------|----------|
| 1000 | Khu Vĩnh Khánh | Area | 10.7626 | 196.6927 | 200m | 1 |
| 1011 | Bánh Mì Ba Lẹ | Spot | 10.7628 | 106.6929 | 15m | 10 |
| 1012 | Gỏi Cuốn Tươi Ngon | Spot | 10.7632 | 106.6935 | 12m | 10 |
| 1013 | Phở Hòa | Spot | 10.7625 | 106.6925 | 10m | 10 |

## 📦 Dependencies Added

```xml
<PackageReference Include="CommunityToolkit.Maui" Version="10.1.0" />
<PackageReference Include="Mapsui" Version="5.0.0" />
<PackageReference Include="Mapsui.Tiling" Version="5.0.0" />
<PackageReference Include="Mapsui.UI.Maui" Version="5.0.0" />
<PackageReference Include="Plugin.Maui.Audio" Version="3.1.0" />
```

## ⚙️ Configuration

`AppConfig.cs`:
- `UseSimulatedGPS = true` - Dùng GPS giả lập (tốt cho emulator)
- `DefaultLatitude = 10.7626, DefaultLongitude = 106.6927` - Center map tại Vĩnh Khánh
- `DebounceMeters = 5.0, DebounceMs = 3000` - Anti-spam GPS

## 🎯 Next Steps

1. **Thay stub services bằng real implementation:**
   - AudioService → Plugin.Maui.Audio với file MP3 thật
   - ZoneRepository → MongoDB Atlas sync
   
2. **Thêm features:**
   - POI detail page
   - Search POIs
   - Filter by category
   - Offline map caching

3. **Testing:**
   - Test trên thiết bị thật với GPS thật
   - Test cooldown logic
   - Test audio ducking với file audio thật

## 📝 Changes Summary

✅ **Added:**
- Mapsui map integration
- Floating UI với modern design
- Settings drawer
- GPS simulation với 10 waypoints
- Audio player UI
- Zone type colors & emojis

✅ **Updated:**
- GeofenceTestViewModel → match VirtualTourGuide MainViewModel
- GeofenceTestPage.xaml → floating overlays design
- MauiProgram.cs → new DI registration
- Converters → added ZoneTypeToColor, BoolToTrackingLabel

✅ **Created:**
- AppConfig.cs
- UserSession.cs
- IServices.cs (IZoneRepository, IAudioService, ILocationService)
- AudioService.cs, LocationService.cs, ZoneRepository.cs
- IGeofenceService.cs (simplified implementation)

---

**Made with ❤️ for Street Food Narrator Project**
