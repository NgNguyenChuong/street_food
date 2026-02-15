# Implementation Checklist - Smart Geofencing System

## ✅ Hoàn thành

### 🎯 Phase 1: Documentation & Design
- [x] Technical PRD document (Geofencing-Technical-PRD.md)
- [x] Pseudo-code chi tiết (Geofencing-Pseudo-Code.md)
- [x] MongoDB seed data examples (nested-zones-seed.js)
- [x] Database documentation (NESTED_ZONES_README.md)

### 🗄️ Phase 2: Backend - MongoDB Schema
- [x] Tạo GeoJsonLocation model (GeoJsonLocation.cs)
- [x] Cập nhật POI model với location GeoJSON
- [x] Thêm fields: ZoneType, ZoneLevel, Priority, TriggerRadius
- [x] Thêm fields: CooldownMinutes, ParentZoneId, MaxPlaysPerSession
- [x] Seed data với 3-level hierarchy

### 📱 Phase 3: Mobile App - Core Models
- [x] Cập nhật POI model với geofence fields
- [x] Tạo ActiveGeofence model
- [x] Tạo ZoneHistory model cho cooldown tracking

### 🚀 Phase 4: Mobile App - GeofenceService
- [x] Tạo IGeofenceService interface
- [x] Implement GeofenceService với full logic:
  - [x] OnLocationChanged handler
  - [x] Haversine distance calculation
  - [x] Priority score algorithm
  - [x] Overlapping zones detection
  - [x] Nested zone logic (duck/unduck audio)
  - [x] Cooldown management
  - [x] Debounce anti-jitter
  - [x] Parent zone stack

---

## ⏳ Còn Phải Làm

### 📡 Phase 5: Backend API Endpoints
- [ ] Tạo API endpoint: `GET /api/pois/nearby`
  - Query params: lat, lon, radius
  - Return POIs với GeoJSON trong bán kính
- [ ] Tạo API endpoint: `GET /api/pois/{id}/children`
  - Return nested zones của một parent
- [ ] Update MongoDB indexes (2dsphere, composite)
- [ ] Testing API với Postman/HTTP files

### 🔧 Phase 6: Mobile App - Service Dependencies (**STUB MODE DONE ✅**)
- [x] **STUB**: LocationService - Fake GPS updates mỗi 5s
- [x] **STUB**: AudioPlayerService - Logs audio playback (no real sound)
- [x] **STUB**: LocalDatabaseService - In-memory storage với sample POIs
- [ ] **REAL**: Implement với .NET MAUI Geolocation API
- [ ] **REAL**: Implement với Plugin.Maui.Audio
- [ ] **REAL**: Implement với SQLite-net-pcl

### 💾 Phase 7: Local Database (SQLite)
- [ ] Tạo SQLite schema cho POIs table
- [ ] Tạo SQLite schema cho ZoneHistory table
- [ ] Migration scripts
- [ ] Sync service (MongoDB → SQLite)
- [ ] Audio file download manager cho offline

### 🎵 Phase 8: Audio Integration
- [ ] Audio ducking implementation (Volume control)
- [ ] Audio queueing system
- [ ] Background audio playback (iOS/Android permissions)
- [ ] Audio file caching strategy

### 🧪 Phase 9: Testing
- [ ] Unit tests cho priority score calculation
- [ ] Unit tests cho Haversine distance
- [ ] Unit tests cho nested zone detection
- [ ] Integration tests cho zone transitions
- [ ] GPS simulator cho field testing
- [ ] Test với kịch bản nested entry/exit

### 🔋 Phase 10: Optimization
- [ ] Battery optimization
  - Reduce GPS polling khi stationary
  - Geofence region monitoring (native APIs)
- [ ] Memory optimization
  - Limit history cache size
  - Clear old zone histories
- [ ] Performance profiling
  - Measure location processing time
  - Optimize POI cache queries

### 🧪 **Phase 10.5: Emulator Testing ✅ READY NOW!**
- [x] **Test Page Created** - GeofenceTestPage.xaml
- [x] **ViewModel Created** - GeofenceTestViewModel.cs
- [x] **Service Registration** - MauiProgram.cs
- [x] **Sample Data Loaded** - 4 POIs in-memory
- [ ] **Run on Emulator** - Test basic flow
- [ ] **Test Nested Zones** - Walk simulation
- [ ] **Test Priority Algorithm** - Overlapping zones
- [ ] **Verify Cooldown** - Re-trigger prevention
- [ ] **Check Logs** - Debug output validation

**📖 Testing Guide**: See [Geofencing-Testing-Guide.md](./Geofencing-Testing-Guide.md)

### 🎨 Phase 11: UI/UX
- [ ] Map view hiển thị active zones
- [ ] Debug panel hiển thị:
  - Current location
  - Active zones list
  - Priority scores
  - Cooldown status
- [ ] User settings:
  - Enable/disable geofencing
  - Audio volume controls
  - Language selection

### 📊 Phase 12: Analytics & Monitoring
- [ ] Log zone entries to analytics
- [ ] Track user paths (privacy-compliant)
- [ ] Monitor battery usage
- [ ] Error tracking & crash reporting

---

## 🚨 Known Issues / Blockers

### Dependencies cần implement:
1. **ILocationService**: Cần GPS tracking service
2. **IAudioPlayerService**: Cần audio player với ducking support
3. **ILocalDatabaseService**: Cần SQLite wrapper
4. **ILogger<T>**: Có sẵn trong .NET (Microsoft.Extensions.Logging)

### Platform-specific requirements:
- **Android**: 
  - Location permissions (ACCESS_FINE_LOCATION)
  - Background location (ACCESS_BACKGROUND_LOCATION)
  - Foreground service cho tracking
- **iOS**:
  - Location permissions (NSLocationWhenInUseUsageDescription)
  - Background modes (location updates)
  - Audio session configuration

---

## 📝 Next Steps (Priority Order)

### Week 1: Backend Foundation
1. ✅ Update MongoDB schema với GeoJSON ← **DONE**
2. ✅ Create seed data ← **DONE**
3. ⏳ Create API endpoints ← **TODO**
4. ⏳ Test endpoints with sample data ← **TODO**

### Week 2: Mobile Services
1. ✅ Create GeofenceService ← **DONE**
2. ⏳ Implement ILocationService ← **TODO**
3. ⏳ Implement IAudioPlayerService ← **TODO**
4. ⏳ Implement ILocalDatabaseService ← **TODO**

### Week 3: Integration
1. ⏳ Wire up GeofenceService with LocationService
2. ⏳ Implement audio ducking
3. ⏳ Sync service (API → SQLite)
4. ⏳ Test end-to-end flow

### Week 4: Polish & Testing
1. ⏳ Add debug UI
2. ⏳ Field testing with real GPS
3. ⏳ Battery optimization
4. ⏳ Bug fixes

---

## 🎯 Definition of Done

### Feature is complete when:
- [ ] User đi vào Area → Phát audio Area
- [ ] User đi vào Spot (nested) → Duck Area, phát Spot
- [ ] User thoát Spot → Resume Area audio
- [ ] Cooldown hoạt động (không spam audio)
- [ ] Debounce hoạt động (GPS không dội)
- [ ] Priority sorting đúng (Spot > District > Area)
- [ ] Offline mode hoạt động (SQLite cache)
- [ ] Battery usage < 5% per hour
- [ ] No crashes sau 1 giờ tracking

---

## 📚 Tài liệu Reference

1. [Geofencing-Technical-PRD.md](./Geofencing-Technical-PRD.md) - Technical requirements
2. [Geofencing-Pseudo-Code.md](./Geofencing-Pseudo-Code.md) - Algorithm details
3. [NESTED_ZONES_README.md](../Database/NESTED_ZONES_README.md) - Seed data guide
4. [nested-zones-seed.js](../Database/nested-zones-seed.js) - MongoDB seed script

---

## 🤝 Team Responsibilities

### Backend Developer:
- API endpoints
- MongoDB indexes
- GeoJSON queries

### Mobile Developer:
- GeofenceService (✅ Done)
- Location/Audio services
- SQLite integration
- UI/UX

### QA/Tester:
- GPS simulation testing
- Battery profiling
- Edge case testing
- User acceptance testing

---

**Last Updated**: 11/02/2026  
**Status**: Phase 1-4 Complete, Phase 5-12 Pending  
