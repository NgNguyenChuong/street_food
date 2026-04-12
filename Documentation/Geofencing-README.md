# 🗺️ Smart Geofencing System - Tài Liệu Hoàn Chỉnh

## 📋 Tổng Quan

Hệ thống **Smart Geofencing với Overlapping & Nested Zones** cho phép ứng dụng phát nội dung thuyết minh thông minh dựa trên vị trí GPS của người dùng. Hệ thống hỗ trợ:

✅ **Nested Zones** - Vùng chồng lấn (Spot trong District trong Area)  
✅ **Priority-based Selection** - Chọn zone theo độ ưu tiên tự động  
✅ **Audio Ducking** - Giảm âm lượng zone cha khi vào zone con  
✅ **Cooldown Management** - Chống spam audio  
✅ **Offline Support** - Hoạt động với SQLite cache  

---

## 📂 Cấu Trúc Tài Liệu

### 1️⃣ [Geofencing-Technical-PRD.md](./Geofencing-Technical-PRD.md)
**📖 Technical Product Requirements Document**

- ✨ **Architecture Overview**: System components, data flow
- 🗄️ **MongoDB Schema Design**: GeoJSON, ZoneType, Priority
- 🧮 **Priority Algorithm**: Chi tiết công thức tính điểm
- 🔄 **Zone Transition Logic**: Nested entry/exit handling
- 💾 **Offline Strategy**: Sync & local database
- 🚫 **Anti-Spam**: Debounce & cooldown mechanisms
- 📊 **Example Data**: 3-level hierarchy (Area → District → Spot)
- ⚡ **Performance**: Optimization strategies

**Dùng khi**: Cần hiểu tổng thể hệ thống, requirements, architecture

---

### 2️⃣ [Geofencing-Pseudo-Code.md](./Geofencing-Pseudo-Code.md)
**🧩 Chi Tiết Thuật Toán (Pseudo-code)**

- 🎯 **Main Flow**: OnLocationChanged step-by-step
- 📐 **Priority Score**: Formula với ví dụ tính toán cụ thể
- 🔄 **Zone Transition**: 3 cases (nested entry, exit, new zone)
- 🌐 **Haversine Distance**: Tính khoảng cách GPS
- ⏱️ **Cooldown Logic**: Check & record zone history
- 🛡️ **Debounce**: Anti-GPS jitter (time + distance)
- 📖 **Scenarios**: Kịch bản thực tế với timeline

**Dùng khi**: Implement code, hiểu logic chi tiết từng bước

---

### 3️⃣ [NESTED_ZONES_README.md](../Database/NESTED_ZONES_README.md)
**🗄️ MongoDB Seed Data Guide**

- 🌳 **Hierarchy Diagram**: Visual structure của zones
- 📊 **Sample Data**: 5 POIs (1 Area, 2 Districts, 2 Spots)
- 🔍 **Query Examples**: GeoJSON spatial queries
- 📈 **Indexes**: Required indexes (2dsphere, composite)
- 🧪 **Testing Scenarios**: Walking paths & expected behavior

**Dùng khi**: Load seed data, test MongoDB queries, hiểu data structure

---

### 4️⃣ [nested-zones-seed.js](../Database/nested-zones-seed.js)
**📜 MongoDB Seed Script**

- ⚙️ **Executable Script**: Copy-paste vào MongoDB shell
- 🏗️ **Complete Data**: Ready-to-use sample zones
- ✅ **Verification**: Auto-test geospatial queries
- 🔨 **Index Creation**: Tự động tạo cần thiết indexes

**Dùng khi**: Load data vào MongoDB lần đầu

```bash
mongosh < nested-zones-seed.js
```

---

### 5️⃣ [Geofencing-Implementation-Checklist.md](./Geofencing-Implementation-Checklist.md)
**✅ Checklist Triển Khai**

- ✅ **Completed**: Phases 1-4 (PRD, Schema, Models, Service)
- ⏳ **Pending**: Phases 5-12 (API, Dependencies, Testing)
- 🚨 **Blockers**: Dependencies cần implement
- 📅 **Timeline**: Week-by-week plan
- 🎯 **Definition of Done**: Acceptance criteria

**Dùng khi**: Track progress, plan sprints, assign tasks

---

## 🛠️ Code Đã Implement

### Backend (C# / ASP.NET Core)

#### 📄 [API/StreetFoodNarrator.API/Models/POI.cs](../API/StreetFoodNarrator.API/Models/POI.cs)
```csharp
// ✅ Đã thêm:
public GeoJsonLocation Location { get; set; }  // GeoJSON Point
public string ZoneType { get; set; }           // Area/District/Spot
public int ZoneLevel { get; set; }             // 1/2/3
public int Priority { get; set; }              // 1-10
public int TriggerRadius { get; set; }         // meters
public int CooldownMinutes { get; set; }       // cooldown period
public int? ParentZoneId { get; set; }         // nested relationship
public int MaxPlaysPerSession { get; set; }    // replay limit
```

#### 📄 [API/StreetFoodNarrator.API/Models/GeoJsonLocation.cs](../API/StreetFoodNarrator.API/Models/GeoJsonLocation.cs)
```csharp
// ✅ GeoJSON Point model
public class GeoJsonLocation
{
    public string Type { get; set; } = "Point";
    public double[] Coordinates { get; set; } // [Lon, Lat]
    
    public double Longitude { get; }
    public double Latitude { get; }
    
    public static FromLatLon(lat, lon) { }
}
```

---

### Mobile App (C# / .NET MAUI)

#### 📄 [MobileApp/.../Core/Models/POI.cs](../MobileApp/StreetFoodNarrator.App/Core/Models/POI.cs)
```csharp
// ✅ Đã thêm geofence fields:
public string ZoneType { get; set; }
public int ZoneLevel { get; set; }
public int Priority { get; set; }
public int CooldownMinutes { get; set; }
public int? ParentZoneId { get; set; }
```

#### 📄 [MobileApp/.../Core/Models/ActiveGeofence.cs](../MobileApp/StreetFoodNarrator.App/Core/Models/ActiveGeofence.cs)
```csharp
// ✅ Model cho active zones
public class ActiveGeofence
{
    public POI POI { get; set; }
    public double Distance { get; set; }
    public double PriorityScore { get; set; }
    public DateTime EnteredAt { get; set; }
    public bool IsActive { get; set; }
}
```

#### 📄 [MobileApp/.../Core/Models/ZoneHistory.cs](../MobileApp/StreetFoodNarrator.App/Core/Models/ZoneHistory.cs)
```csharp
// ✅ Cooldown tracking model
public class ZoneHistory
{
    public int POI_ID { get; set; }
    public DateTime LastTriggeredAt { get; set; }
    public int PlayCount { get; set; }
    public string SessionId { get; set; }
    
    public bool IsInCooldown(int cooldownMinutes) { }
}
```

#### 📄 [MobileApp/.../Services/GeofenceService.cs](../MobileApp/StreetFoodNarrator.App/Core/Services/Implementations/GeofenceService.cs)
```csharp
// ✅ FULL IMPLEMENTATION
public class GeofenceService : IGeofenceService
{
    // Core algorithm
    public async Task OnLocationChanged(UserLocation location) { }
    
    // Priority calculation
    private double CalculatePriorityScore(POI poi, double distance) { }
    
    // Nested zone handling
    private async Task HandleZoneTransition(POI old, POI new, List zones) { }
    private bool IsNestedZone(POI child, POI parent) { }
    
    // Haversine distance
    private double CalculateHaversineDistance(lat1, lon1, lat2, lon2) { }
    
    // Cooldown management
    private bool IsInCooldown(POI poi) { }
    private async Task RecordZoneEntry(POI poi) { }
    
    // Debounce
    private bool ShouldProcessLocation(UserLocation location) { }
}
```

---

## 🚀 Quick Start

### 1. Load MongoDB Seed Data

```bash
# Navigate to Database folder
cd Database

# Load seed data
mongosh < nested-zones-seed.js

# Verify data loaded
mongosh
use StreetFoodNarratorDB
db.POIs.countDocuments()  # Should be 5
```

### 2. Explore Sample Data

```javascript
// Area (Level 1)
db.POIs.findOne({ POI_ID: 1000 })  // Khu Ẩm Thực Vĩnh Khánh

// District (Level 2)
db.POIs.findOne({ POI_ID: 1010 })  // Khu Nhà Hàng Hải Sản

// Spot (Level 3)
db.POIs.findOne({ POI_ID: 1011 })  // Quán Bà Năm
```

### 3. Test Geospatial Query

```javascript
// Find POIs near Vinh Khanh center
db.POIs.find({
    location: {
        $near: {
            $geometry: {
                type: "Point",
                coordinates: [106.6927, 10.7626]
            },
            $maxDistance: 500
        }
    }
})
```

### 4. Review Code Implementation

1. **Backend Models**: `API/StreetFoodNarrator.API/Models/`
   - POI.cs (✅ Updated)
   - GeoJsonLocation.cs (✅ New)

2. **Mobile Models**: `MobileApp/.../Core/Models/`
   - POI.cs (✅ Updated)
   - ActiveGeofence.cs (✅ New)
   - ZoneHistory.cs (✅ New)

3. **GeofenceService**: `MobileApp/.../Core/Services/Implementations/`
   - GeofenceService.cs (✅ Full implementation)

---

## 📐 Ví Dụ Priority Selection (Implementation Thực Tế)

```
Mobile code: insideZones.OrderByDescending(z => z.Priority)
               .ThenBy(z => z.DistanceFromUser)
               .ThenBy(z => z.Radius)
               .ThenBy(z => z.Id)

Scenario 1 — Multiple Spots overlap, user standing between Ốc Oanh & Ốc Vũ:
  Ốc Oanh (Spot):    Priority=9, Distance=20m  → 1st (WINNER)
  Ốc Vũ (Spot):     Priority=5, Distance=8m   → 2nd (loses despite closer!)
  Area (Area):       Priority=2, Distance=5m  → 3rd
  → Ốc Oanh WIN vì Priority cao nhất (9)

Scenario 2 — Same priority boundary (hysteresis kicks in):
  User đang ở Ốc Vũ (Priority=5, đã đi vào zone). Di chuyển đến vị trí:
    Ốc Vũ: Priority=5, Distance=25m
    Ốc Thảo: Priority=5, Distance=23m   ← cùng Priority!
  → Hysteresis: 25m - 23m = 2m ≤ 8m threshold → giữ Ốc Vũ
  → Giúp tránh zone "flapping" khi user đứng giữa 2 Spot cùng priority

Scenario 3 — User enters Area then walks into a Spot:
  1. Inside Area (Priority=2): Area plays (only zone)
  2. Enter Ốc Oanh zone (Priority=9): Ốc Oanh WIN → plays instead
  3. Exit Ốc Oanh zone (still inside Area): Area resumes (ducked earlier)
```

---

## 🔄 Zone Transition Examples

### Example 1: Nested Entry (Vào zone con)

```
User: Outside → Area
  Action: Play "Khu Ẩm Thực Vĩnh Khánh" (full volume)

User: Area → District (nested in Area)
  Action: 
    1. Duck Area audio to 20% volume
    2. Play "Khu Nhà Hàng Hải Sản"
    3. Push Area to parentStack

User: District → Spot (nested in District)
  Action:
    1. Duck District audio to 20%
    2. Play "Quán Bà Năm"
    3. Push District to parentStack
```

### Example 2: Nested Exit (Ra khỏi zone con)

```
User: Spot → District
  Action:
    1. UnDuck District audio (restore volume)
    2. Pop District from stack
    3. Resume District audio

User: District → Area
  Action:
    1. UnDuck Area audio
    2. Pop Area from stack
    3. Resume Area audio

User: Area → Outside
  Action:
    1. FadeOut and Stop audio
    2. Clear all stacks
```

---

## 🧪 Testing Checklist

### Unit Tests (Recommended)
- [ ] HaversineDistance: Known coordinates → Expected meters
- [ ] CalculatePriorityScore: Various POIs → Expected scores
- [ ] IsNestedZone: Parent-child pairs → True/False
- [ ] IsInCooldown: Various timestamps → Expected cooldown state
- [ ] ShouldProcessLocation: Various intervals → Debounce behavior

### Integration Tests
- [ ] Load POIs from SQLite
- [ ] OnLocationChanged full flow
- [ ] Zone transition (nested entry)
- [ ] Zone transition (nested exit)
- [ ] Cooldown enforcement

### Field Tests (GPS Simulator)
- [ ] Walk from outside → Area → District → Spot
- [ ] Verify audio plays in correct order
- [ ] Verify ducking/unduck works
- [ ] Verify cooldown prevents re-trigger
- [ ] Battery usage < 5% per hour

---

## 📊 MongoDB Indexes Required

```javascript
// CRITICAL: Geospatial index
db.POIs.createIndex({ "location": "2dsphere" })

// Performance indexes
db.POIs.createIndex({ "IsActive": 1, "ZoneType": 1, "Priority": -1 })
db.POIs.createIndex({ "ParentZoneId": 1 })
db.POIs.createIndex({ "POI_ID": 1 })
```

---

## ⚠️ Important Notes

### GeoJSON Coordinate Order
```json
// ✅ CORRECT (Longitude first!)
"coordinates": [106.6927, 10.7626]  // [Lon, Lat]

// ❌ WRONG
"coordinates": [10.7626, 106.6927]  // [Lat, Lon]
```

### Priority Hierarchy
```
Spot (7-10) > District (4-6) > Area (1-3)
```
**Convention: Priority 1-10, HIGHER = more important.**
**Why?** Spot là cụ thể nhất → Quan trọng nhất → Priority cao nhất. Admin form ghi rõ "Higher wins when zones overlap". Mobile code dùng `OrderByDescending(Priority)`.

### Cooldown Strategy
```
Area:     30 minutes  (avoid spam, khu vực lớn)
District: 15 minutes  (medium zones)
Spot:     0 minutes   (play once only, điểm cụ thể)
```

---

## 📚 External References

- [GeoJSON Specification](https://datatracker.ietf.org/doc/html/rfc7946)
- [MongoDB Geospatial Queries](https://www.mongodb.com/docs/manual/geospatial-queries/)
- [Haversine Formula](https://en.wikipedia.org/wiki/Haversine_formula)
- [.NET MAUI Geolocation](https://learn.microsoft.com/en-us/dotnet/maui/platform-integration/device/geolocation)

---

## 🤝 Contribution Guidelines

### Khi thêm POI mới:
1. Xác định ZoneType (Area / District / Spot)
2. Set Priority phù hợp (Spot=7-10, District=4-6, Area=1-3; HIGHER = more important)
3. Set TriggerRadius hợp lý (Spot=10m, District=100m, Area=500m)
4. Set ParentZoneId nếu là nested zone
5. Test với GPS simulator

### Khi modify algorithm:
1. Update Geofencing-Pseudo-Code.md
2. Update Geofencing-Technical-PRD.md
3. Add unit tests
4. Update this README

---

## 📞 Support

- **Technical Questions**: Xem [Geofencing-Technical-PRD.md](./Geofencing-Technical-PRD.md)
- **Algorithm Details**: Xem [Geofencing-Pseudo-Code.md](./Geofencing-Pseudo-Code.md)
- **Database Setup**: Xem [NESTED_ZONES_README.md](../Database/NESTED_ZONES_README.md)
- **Progress Tracking**: Xem [Geofencing-Implementation-Checklist.md](./Geofencing-Implementation-Checklist.md)

---

**Document Version**: 1.0  
**Last Updated**: 11/02/2026  
**Status**: Phase 1-4 Complete ✅  
**Author**: Senior Mobile Solutions Architect  

---

## 🎯 Next Steps

1. ⏳ Implement API endpoints (`/api/pois/nearby`)
2. ⏳ Implement ILocationService dependencies
3. ⏳ Implement IAudioPlayerService with ducking
4. ⏳ Test end-to-end với GPS simulator

**See**: [Geofencing-Implementation-Checklist.md](./Geofencing-Implementation-Checklist.md) for detailed roadmap.
