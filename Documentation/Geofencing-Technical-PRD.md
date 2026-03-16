# Technical PRD: Smart Geofencing System với Overlapping & Nested Zones

**Dự án**: Street Food Narrator - Virtual Tour Guide  
**Ngày**: 11/02/2026  
**Tác giả**: Senior Mobile Solutions Architect  
**Phiên bản**: 1.0  

---

## 1. EXECUTIVE SUMMARY

### 1.1 Vấn đề cần giải quyết
Người dùng di chuyển trong khu vực có nhiều lớp địa điểm (ví dụ: Phố ẩm thực > Quán ăn > Gian hàng cụ thể). Hệ thống cần đủ thông minh để:
- Phát đúng nội dung thuyết minh tương ứng với vị trí chi tiết nhất
- Xử lý mượt mà khi người dùng di chuyển giữa các vùng chồng lấn
- Tránh spam âm thanh (debounce & cooldown)
- Hoạt động tốt cả khi offline

### 1.2 Giải pháp tổng quan
Xây dựng hệ thống **Hierarchical Priority-based Geofencing** với:
- MongoDB GeoJSON cho backend storage
- Smart zone detection algorithm trên Mobile App
- Audio ducking & state management
- Offline-first architecture với SQLite + Haversine calculation

---

## 2. TECHNICAL ARCHITECTURE

### 2.1 System Components

```
┌─────────────────────────────────────────────────────────┐
│                    MongoDB Backend                       │
│  ┌──────────────────────────────────────────────────┐  │
│  │  GeoJSON POIs với ZoneType & Priority            │  │
│  │  - Area (Level 1): Radius 500m                   │  │
│  │  - District (Level 2): Radius 100m               │  │
│  │  - Spot (Level 3): Radius 10m                    │  │
│  └──────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────┘
                          ↓ Sync
┌─────────────────────────────────────────────────────────┐
│              .NET MAUI Mobile App                        │
│  ┌──────────────────────────────────────────────────┐  │
│  │  SQLite Local Database (Offline Cache)           │  │
│  │  → Full GeoJSON + Metadata                       │  │
│  └──────────────────────────────────────────────────┘  │
│                          ↓                               │
│  ┌──────────────────────────────────────────────────┐  │
│  │  GeofenceService (Core Logic)                    │  │
│  │  • GPS Location Tracking                         │  │
│  │  • Zone Detection (Haversine)                    │  │
│  │  • Priority Sorting Algorithm                    │  │
│  │  • Cooldown & Debounce Manager                   │  │
│  └──────────────────────────────────────────────────┘  │
│                          ↓                               │
│  ┌──────────────────────────────────────────────────┐  │
│  │  AudioPlayerService                              │  │
│  │  • Play/Pause/Duck Audio                         │  │
│  │  • Queue Management                              │  │
│  └──────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────┘
```

---

## 3. DATA SCHEMA DESIGN

### 3.1 MongoDB Schema - Enhanced POI Collection

**Yêu cầu**:
- ✅ GeoJSON Point cho tọa độ
- ✅ `ZoneType` enum (Area / District / Spot)
- ✅ `Priority` integer (1-10, cao = ưu tiên)
- ✅ `TriggerRadius` (mét)

#### 3.1.1 Full Schema Specification

```json
{
  "_id": ObjectId,
  "POI_ID": Integer,
  
  // ========== GEOSPATIAL DATA (GeoJSON Standard) ==========
  "location": {
    "type": "Point",                    // GeoJSON type
    "coordinates": [105.1234, 21.5678]  // [Longitude, Latitude]
  },
  
  // ========== ZONE CLASSIFICATION ==========
  "ZoneType": "Area" | "District" | "Spot",
  "ZoneLevel": 1 | 2 | 3,  // Computed: Area=1, District=2, Spot=3
  "Priority": 5,            // 1-10, default=5, Spot > District > Area
  "TriggerRadius": 500,     // meters
  
  // ========== MULTI-LANGUAGE CONTENT ==========
  "Name_Vi": "Khu Ẩm Thực Vĩnh Khánh",
  "Name_En": "Vinh Khanh Food Street",
  "Description_Vi": "Khu ẩm thực...",
  "Description_En": "Famous food district...",
  
  // ========== AUDIO URLs ==========
  "AudioUrl_Vi": "/audio/vinh-khanh-area-vi.mp3",
  "AudioUrl_En": "/audio/vinh-khanh-area-en.mp3",
  
  // ========== METADATA ==========
  "Category": "FoodStreet" | "Restaurant" | "Stall",
  "Tags": ["Street Food", "Night Market"],
  "ImageUrl": "/images/vinh-khanh.jpg",
  "IsActive": true,
  
  // ========== COOLDOWN SETTINGS ==========
  "CooldownMinutes": 30,     // Area: 30min, Spot: 0 (play once)
  "MaxPlaysPerSession": 1,   // Limit replays
  
  // ========== RELATIONSHIPS ==========
  "ParentZoneId": null | Integer,  // Spot → Restaurant → Area
  
  // ========== TIMESTAMPS ==========
  "CreatedAt": ISODate,
  "UpdatedAt": ISODate
}
```

#### 3.1.2 MongoDB Index Requirements

```javascript
// Create 2dsphere index for geospatial queries
db.POIs.createIndex({ "location": "2dsphere" })

// Composite index for zone queries
db.POIs.createIndex({ 
  "IsActive": 1, 
  "ZoneType": 1, 
  "Priority": -1 
})

// Parent-child relationship
db.POIs.createIndex({ "ParentZoneId": 1 })
```

---

## 4. ZONE HIERARCHY & PRIORITY LOGIC

### 4.1 Zone Type Definitions

| ZoneType   | ZoneLevel | Typical Radius | Priority Default | Cooldown | Example                        |
|------------|-----------|----------------|------------------|----------|--------------------------------|
| **Area**   | 1         | 300-1000m      | 3                | 30min    | Khu phố ẩm thực, chợ đêm       |
| **District**| 2        | 50-200m        | 5                | 15min    | Khu nhà hàng, food court       |
| **Spot**   | 3         | 5-30m          | 8                | Once     | Quán ăn cụ thể, gian hàng      |

### 4.2 Priority Calculation Algorithm

**Formula**:
```
FinalScore = (Priority × 100) + (1000 / Radius) + (ZoneLevel × 10)
```

**Why?**:
- `Priority × 100`: Trọng số chính (admin set)
- `1000 / Radius`: Vùng nhỏ hơn = cụ thể hơn = điểm cao hơn
- `ZoneLevel × 10`: Bonus cho level cao (Spot > District > Area)

**Example**:
```
Spot (Priority=8, Radius=10m, Level=3):
  Score = 800 + 100 + 30 = 930

Area (Priority=3, Radius=500m, Level=1):
  Score = 300 + 2 + 10 = 312

→ Spot wins!
```

---

## 5. CORE ALGORITHM: ZONE DETECTION & SELECTION

### 5.1 OnLocationChanged Flow

```
User GPS Update → GeofenceService
    ↓
[1] Calculate distance to ALL cached POIs (Haversine)
    ↓
[2] Filter: Distance ≤ TriggerRadius
    → Active Zones List
    ↓
[3] Sort by FinalScore (DESC)
    ↓
[4] Get Top Zone (Highest Priority)
    ↓
[5] Check Cooldown & History
    ↓
[6] Decision:
    • If New Zone → Play Audio
    • If Nested (in larger zone) → Duck old, Play new
    • If Exited nested → Resume parent (if not expired)
```

### 5.2 Pseudo-code (C#)

```csharp
public async Task OnLocationChanged(UserLocation userLocation)
{
    // ============================================
    // STEP 0: Debounce (anti-jitter)
    // ============================================
    if (!ShouldProcessLocation(userLocation))
        return;
    
    _lastProcessedLocation = userLocation;
    _lastProcessedTime = DateTime.UtcNow;
    
    // ============================================
    // STEP 1: Find ALL zones containing user
    // ============================================
    var activeZones = new List<(POI poi, double distance, double score)>();
    
    foreach (var poi in _cachedPOIs.Where(p => p.IsActive))
    {
        double distance = CalculateHaversineDistance(
            userLocation.Latitude, 
            userLocation.Longitude,
            poi.Location.Coordinates[1], // Latitude
            poi.Location.Coordinates[0]  // Longitude
        );
        
        if (distance <= poi.TriggerRadius)
        {
            double score = CalculatePriorityScore(poi, distance);
            activeZones.Add((poi, distance, score));
        }
    }
    
    // ============================================
    // STEP 2: Sort by Priority Score
    // ============================================
    activeZones = activeZones
        .OrderByDescending(z => z.score)
        .ThenBy(z => z.distance)  // Tie-breaker: closer wins
        .ToList();
    
    // ============================================
    // STEP 3: Get Highest Priority Zone
    // ============================================
    if (!activeZones.Any())
    {
        await HandleExitAllZones();
        return;
    }
    
    var topZone = activeZones.First().poi;
    
    // ============================================
    // STEP 4: Check if this is a NEW trigger
    // ============================================
    if (_currentActiveZone?.POI_ID == topZone.POI_ID)
    {
        // Same zone, do nothing
        return;
    }
    
    // ============================================
    // STEP 5: Check Cooldown
    // ============================================
    if (IsInCooldown(topZone))
    {
        Debug.WriteLine($"Zone {topZone.Name_Vi} is in cooldown");
        return;
    }
    
    // ============================================
    // STEP 6: Handle Zone Transition
    // ============================================
    await HandleZoneTransition(_currentActiveZone, topZone, activeZones);
}

// ============================================
// Priority Score Calculation
// ============================================
private double CalculatePriorityScore(POI poi, double distance)
{
    int zoneLevel = poi.ZoneType switch
    {
        "Area" => 1,
        "District" => 2,
        "Spot" => 3,
        _ => 1
    };
    
    return (poi.Priority * 100) 
           + (1000.0 / Math.Max(poi.TriggerRadius, 1)) 
           + (zoneLevel * 10);
}

// ============================================
// Zone Transition Logic (CRITICAL!)
// ============================================
private async Task HandleZoneTransition(
    POI? oldZone, 
    POI newZone, 
    List<(POI poi, double distance, double score)> allActiveZones)
{
    // Case 1: Entering nested zone (Spot inside Area)
    if (oldZone != null && IsNestedZone(newZone, oldZone))
    {
        Debug.WriteLine($"Nested Entry: {oldZone.Name_Vi} → {newZone.Name_Vi}");
        
        // Duck (lower volume) old audio
        await _audioService.DuckCurrentAudio(volumeLevel: 0.2f);
        
        // Play new zone audio
        await _audioService.PlayPOIAudio(newZone, _currentLanguage);
        
        // Save parent for resume later
        _parentZoneStack.Push(oldZone);
    }
    // Case 2: Exiting nested zone back to parent
    else if (oldZone != null && _parentZoneStack.Any())
    {
        var parentZone = _parentZoneStack.Peek();
        
        if (allActiveZones.Any(z => z.poi.POI_ID == parentZone.POI_ID))
        {
            Debug.WriteLine($"Returning to parent: {parentZone.Name_Vi}");
            
            // Resume parent audio
            await _audioService.UnDuckAudio();
            
            _parentZoneStack.Pop();
        }
    }
    // Case 3: Completely new zone
    else
    {
        Debug.WriteLine($"New Zone Entry: {newZone.Name_Vi}");
        
        // Stop old audio
        await _audioService.Stop();
        
        // Play new audio
        await _audioService.PlayPOIAudio(newZone, _currentLanguage);
    }
    
    // Update state
    _currentActiveZone = newZone;
    RecordZoneEntry(newZone);
}

// ============================================
// Nested Zone Detection
// ============================================
private bool IsNestedZone(POI child, POI parent)
{
    // Direct parent relationship
    if (child.ParentZoneId == parent.POI_ID)
        return true;
    
    // Implicit: child has higher priority AND smaller radius
    if (child.Priority > parent.Priority && 
        child.TriggerRadius < parent.TriggerRadius)
        return true;
    
    return false;
}

// ============================================
// Haversine Distance Calculation
// ============================================
private double CalculateHaversineDistance(
    double lat1, double lon1, 
    double lat2, double lon2)
{
    const double R = 6371000; // Earth radius in meters
    
    double dLat = ToRadians(lat2 - lat1);
    double dLon = ToRadians(lon2 - lon1);
    
    double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
               Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
               Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
    
    double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    
    return R * c; // Distance in meters
}

// ============================================
// Cooldown Management
// ============================================
private bool IsInCooldown(POI poi)
{
    if (!_zoneHistory.TryGetValue(poi.POI_ID, out var lastPlayed))
        return false;
    
    var elapsed = DateTime.UtcNow - lastPlayed;
    return elapsed.TotalMinutes < poi.CooldownMinutes;
}

private void RecordZoneEntry(POI poi)
{
    _zoneHistory[poi.POI_ID] = DateTime.UtcNow;
    
    // Persist to database
    _localDb.InsertAsync(new NarrationLog
    {
        POI_ID = poi.POI_ID,
        PlayedAt = DateTime.UtcNow,
        Language = _currentLanguage
    });
}

// ============================================
// Debounce (Anti-GPS Jitter)
// ============================================
private bool ShouldProcessLocation(UserLocation location)
{
    if (_lastProcessedLocation == null)
        return true;
    
    var timeSinceLastProcess = DateTime.UtcNow - _lastProcessedTime;
    if (timeSinceLastProcess.TotalSeconds < 3)
        return false;
    
    var distanceFromLast = CalculateHaversineDistance(
        _lastProcessedLocation.Latitude,
        _lastProcessedLocation.Longitude,
        location.Latitude,
        location.Longitude
    );
    
    // Ignore if moved less than 5 meters
    return distanceFromLast >= 5;
}
```

---

## 6. DATA SYNC & OFFLINE STRATEGY

### 6.1 Sync Flow

```
[MongoDB Backend]
      ↓ HTTP API
[Mobile App Sync Service]
      ↓ Transform
[SQLite Local DB]
      ↓ Query
[GeofenceService]
```

### 6.2 Sync Implementation

```csharp
public class GeofenceSyncService
{
    public async Task SyncPOIsAsync()
    {
        try
        {
            // 1. Get user's current region (city/district)
            var userRegion = await _locationService.GetCurrentRegion();
            
            // 2. Fetch POIs within reasonable bounds (50km radius)
            var response = await _apiClient.GetAsync(
                $"/api/pois/nearby?lat={userRegion.Lat}&lon={userRegion.Lon}&radius=50000"
            );
            
            var pois = await response.Content.ReadFromJsonAsync<List<POI>>();
            
            // 3. Save to local SQLite
            await _localDb.DeleteAllAsync<POI>(); // Clear old data
            await _localDb.InsertAllAsync(pois);
            
            // 4. Download audio files for offline use
            foreach (var poi in pois.Where(p => !string.IsNullOrEmpty(p.AudioUrl_Vi)))
            {
                await _audioDownloader.DownloadIfNotExistsAsync(poi);
            }
            
            Debug.WriteLine($"Synced {pois.Count} POIs");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Sync failed: {ex.Message}");
        }
    }
}
```

### 6.3 Local Database Schema (SQLite)

```sql
CREATE TABLE POIs (
    POI_ID INTEGER PRIMARY KEY,
    Name_Vi TEXT NOT NULL,
    Name_En TEXT,
    
    -- GeoJSON stored as JSON string
    Location_GeoJSON TEXT NOT NULL,
    
    -- Extracted for quick queries
    Latitude REAL NOT NULL,
    Longitude REAL NOT NULL,
    
    ZoneType TEXT NOT NULL, -- 'Area', 'District', 'Spot'
    ZoneLevel INTEGER NOT NULL,
    Priority INTEGER DEFAULT 5,
    TriggerRadius INTEGER NOT NULL,
    
    AudioUrl_Vi TEXT,
    AudioLocalPath_Vi TEXT, -- Path to downloaded file
    
    CooldownMinutes INTEGER DEFAULT 30,
    ParentZoneId INTEGER,
    
    IsActive INTEGER DEFAULT 1,
    CreatedAt TEXT,
    UpdatedAt TEXT
);

CREATE INDEX idx_location ON POIs(Latitude, Longitude);
CREATE INDEX idx_zone ON POIs(ZoneType, Priority);
CREATE INDEX idx_parent ON POIs(ParentZoneId);
```

---

## 7. ANTI-SPAM MECHANISMS

### 7.1 Debounce (GPS Jitter Protection)

**Problem**: GPS có thể nhảy 5-10m mỗi giây gây trigger liên tục

**Solution**:
```csharp
private const int MIN_TIME_BETWEEN_CHECKS_SECONDS = 3;
private const int MIN_DISTANCE_MOVEMENT_METERS = 5;

private bool ShouldProcessLocation(UserLocation location)
{
    // Time-based debounce
    if ((DateTime.UtcNow - _lastProcessedTime).TotalSeconds < MIN_TIME_BETWEEN_CHECKS_SECONDS)
        return false;
    
    // Distance-based debounce
    if (_lastLocation != null)
    {
        var distance = CalculateDistance(_lastLocation, location);
        if (distance < MIN_DISTANCE_MOVEMENT_METERS)
            return false;
    }
    
    return true;
}
```

### 7.2 Cooldown (Per-Zone)

**Problem**: User có thể đi vào/ra zone nhiều lần trong ngày

**Solution**:
```csharp
private Dictionary<int, DateTime> _zoneCooldowns = new();

private bool IsInCooldown(POI poi)
{
    if (!_zoneCooldowns.TryGetValue(poi.POI_ID, out var lastTriggered))
        return false;
    
    var elapsed = DateTime.UtcNow - lastTriggered;
    return elapsed.TotalMinutes < poi.CooldownMinutes;
}

// Cooldown by zone type
private int GetCooldownMinutes(string zoneType)
{
    return zoneType switch
    {
        "Area" => 30,      // 30 minutes
        "District" => 15,  // 15 minutes
        "Spot" => 0,       // Play once per session
        _ => 10
    };
}
```

### 7.3 Session-based Limits

```csharp
public class ZonePlayHistory
{
    public int POI_ID { get; set; }
    public DateTime FirstPlayed { get; set; }
    public int PlayCount { get; set; }
}

private async Task<bool> CanPlayZone(POI poi)
{
    var history = await _localDb.Table<ZonePlayHistory>()
        .Where(h => h.POI_ID == poi.POI_ID)
        .FirstOrDefaultAsync();
    
    if (history == null)
        return true;
    
    // Spots: play once per session
    if (poi.ZoneType == "Spot" && history.PlayCount >= 1)
        return false;
    
    // Areas: max 3 times per day
    if (poi.ZoneType == "Area")
    {
        var todayCount = await _localDb.Table<ZonePlayHistory>()
            .Where(h => h.POI_ID == poi.POI_ID 
                     && h.FirstPlayed >= DateTime.Today)
            .CountAsync();
        
        return todayCount < 3;
    }
    
    return true;
}
```

---

## 8. EXAMPLE DATA - NESTED ZONES

### 8.1 Example: Vinh Khanh Food Street

#### Zone Level 1 - Area (Khu phố)

```json
{
  "_id": ObjectId("..."),
  "POI_ID": 1000,
  "Name_Vi": "Khu Ẩm Thực Vĩnh Khánh",
  "Name_En": "Vinh Khanh Food Street",
  
  "location": {
    "type": "Point",
    "coordinates": [106.6927, 10.7626]
  },
  
  "ZoneType": "Area",
  "ZoneLevel": 1,
  "Priority": 3,
  "TriggerRadius": 500,
  
  "Description_Vi": "Khu ẩm thực nổi tiếng với hơn 50 gian hàng đường phố...",
  "AudioUrl_Vi": "/audio/vinh-khanh-overview-vi.mp3",
  
  "Category": "FoodStreet",
  "CooldownMinutes": 30,
  "ParentZoneId": null,
  
  "IsActive": true,
  "CreatedAt": "2026-01-15T10:00:00Z"
}
```

#### Zone Level 2 - District (Khu nhà hàng)

```json
{
  "_id": ObjectId("..."),
  "POI_ID": 1010,
  "Name_Vi": "Khu Nhà Hàng Hải Sản",
  "Name_En": "Seafood Restaurant District",
  
  "location": {
    "type": "Point",
    "coordinates": [106.6930, 10.7628]
  },
  
  "ZoneType": "District",
  "ZoneLevel": 2,
  "Priority": 5,
  "TriggerRadius": 100,
  
  "Description_Vi": "Tập trung 15 nhà hàng hải sản tươi sống...",
  "AudioUrl_Vi": "/audio/seafood-district-vi.mp3",
  
  "Category": "RestaurantCluster",
  "CooldownMinutes": 15,
  "ParentZoneId": 1000,
  
  "IsActive": true
}
```

#### Zone Level 3 - Spot (Quán cụ thể)

```json
{
  "_id": ObjectId("..."),
  "POI_ID": 1011,
  "Name_Vi": "Quán Hải Sản Bà Năm",
  "Name_En": "Ba Nam Seafood Restaurant",
  
  "location": {
    "type": "Point",
    "coordinates": [106.6931, 10.7629]
  },
  
  "ZoneType": "Spot",
  "ZoneLevel": 3,
  "Priority": 8,
  "TriggerRadius": 10,
  
  "Description_Vi": "Quán hải sản 30 năm tuổi, đặc sản cua rang me...",
  "AudioUrl_Vi": "/audio/ba-nam-restaurant-vi.mp3",
  
  "Category": "Restaurant",
  "SignatureDish": "Cua rang me",
  "CooldownMinutes": 0,
  "ParentZoneId": 1010,
  
  "IsActive": true
}
```

### 8.2 Relationship Diagram

```
+---------------------------------------------+
|  Area: Khu Ẩm Thực Vĩnh Khánh              |
|  Radius: 500m | Priority: 3                 |
|                                             |
|   +-------------------------------------+   |
|   |  District: Khu Nhà Hàng Hải Sản    |   |
|   |  Radius: 100m | Priority: 5        |   |
|   |                                     |   |
|   |   +--------------------------+     |   |
|   |   |  Spot: Quán Bà Năm      |     |   |
|   |   |  Radius: 10m | Pri: 8   |     |   |
|   |   +--------------------------+     |   |
|   |                                     |   |
|   |   +--------------------------+     |   |
|   |   |  Spot: Quán Bà Sáu      |     |   |
|   |   +--------------------------+     |   |
|   +-------------------------------------+   |
+---------------------------------------------+
```

---

## 9. TESTING SCENARIOS

### 9.1 Test Case 1: Nested Entry
```
User Movement: 
  Outside → Area → District → Spot

Expected Audio Sequence:
  1. Enter Area → Play "Khu Ẩm Thực" (full)
  2. Enter District → Duck Area to 20% → Play "Khu Hải Sản"
  3. Enter Spot → Duck District to 20% → Play "Quán Bà Năm"
```

### 9.2 Test Case 2: Nested Exit
```
User Movement:
  Spot → District → Area → Outside

Expected Audio Sequence:
  1. Exit Spot → Resume District audio (unduck)
  2. Exit District → Resume Area audio (unduck)
  3. Exit Area → Fade out → Stop
```

### 9.3 Test Case 3: Cooldown Enforcement
```
User Movement:
  Enter Area → Exit → Re-enter Area (within 30 min)

Expected:
  1st entry: Play audio
  2nd entry: Silent (cooldown active)
```

### 9.4 Test Case 4: Priority Override
```
Two Spots side-by-side:
  Spot A: Priority 8, Radius 15m
  Spot B: Priority 10, Radius 10m

User stands at intersection (both in range):
Expected: Play Spot B (higher priority)
```

---

## 10. PERFORMANCE CONSIDERATIONS

### 10.1 Optimization Strategies

1. **Spatial Indexing**
   - Use 2dsphere index in MongoDB
   - Pre-filter POIs by bounding box before Haversine calculation

2. **Caching**
   ```csharp
   // Cache POIs in memory, refresh every 6 hours
   private List<POI> _cachedPOIs;
   private DateTime _lastCacheUpdate;
   
   private async Task<List<POI>> GetRelevantPOIs(UserLocation location)
   {
       if (_cachedPOIs == null || 
           (DateTime.UtcNow - _lastCacheUpdate).TotalHours > 6)
       {
           _cachedPOIs = await _localDb.Table<POI>()
               .Where(p => p.IsActive)
               .ToListAsync();
           
           _lastCacheUpdate = DateTime.UtcNow;
       }
       
       return _cachedPOIs;
   }
   ```

3. **Battery Optimization**
   ```csharp
   // Reduce GPS polling when stationary
   if (_isUserStationary)
   {
       _locationService.SetDesiredAccuracy(100); // meters
       _locationService.SetUpdateInterval(30);   // seconds
   }
   else
   {
       _locationService.SetDesiredAccuracy(10);  // meters
       _locationService.SetUpdateInterval(5);    // seconds
   }
   ```

### 10.2 Memory Management

```csharp
// Limit zone history size
private const int MAX_HISTORY_SIZE = 100;

private void RecordZoneEntry(POI poi)
{
    if (_zoneHistory.Count > MAX_HISTORY_SIZE)
    {
        var oldest = _zoneHistory
            .OrderBy(kvp => kvp.Value)
            .First()
            .Key;
        
        _zoneHistory.Remove(oldest);
    }
    
    _zoneHistory[poi.POI_ID] = DateTime.UtcNow;
}
```

---

## 11. ERROR HANDLING

### 11.1 GPS Signal Loss

```csharp
private async Task OnLocationError(Exception ex)
{
    Debug.WriteLine($"Location error: {ex.Message}");
    
    // Show user notification
    await _notificationService.ShowAsync(
        "GPS Signal Lost", 
        "Audio narration paused until GPS is restored"
    );
    
    // Pause current audio
    await _audioService.Pause();
    
    // Retry with lower accuracy
    await _locationService.RequestLocationAsync(accuracy: 100);
}
```

### 11.2 Network Errors (During Sync)

```csharp
private async Task SyncWithRetry(int maxRetries = 3)
{
    for (int i = 0; i < maxRetries; i++)
    {
        try
        {
            await SyncPOIsAsync();
            return;
        }
        catch (HttpRequestException ex)
        {
            if (i == maxRetries - 1)
            {
                // Use stale local data
                Debug.WriteLine("Using cached data");
                return;
            }
            
            await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, i))); // Exponential backoff
        }
    }
}
```

---

## 12. FUTURE ENHANCEMENTS

### 12.1 Machine Learning Integration
- Predict user path based on historical data
- Pre-load audio files before user reaches zone
- Personalized zone priorities based on user interests

### 12.2 Dynamic Radius Adjustment
- Adjust trigger radius based on GPS accuracy
- Larger radius in low-accuracy conditions

### 12.3 Multi-path Audio
```csharp
// Play different audio if user approaches from different directions
public string GetDirectionalAudio(POI poi, double userBearing)
{
    if (userBearing >= 0 && userBearing < 90)
        return poi.AudioUrl_FromNorth;
    else if (userBearing >= 90 && userBearing < 180)
        return poi.AudioUrl_FromEast;
    // ...
}
```

---

## 13. IMPLEMENTATION CHECKLIST

### Phase 1: Backend (Week 1-2)
- [ ] Update MongoDB POI schema with GeoJSON
- [ ] Add ZoneType, Priority, TriggerRadius fields
- [ ] Create 2dsphere indexes
- [ ] Implement nearby POIs API endpoint
- [ ] Create seed data with nested zones

### Phase 2: Mobile Core (Week 3-4)
- [ ] Create Geofence models
- [ ] Implement GeofenceService
- [ ] Haversine distance calculation
- [ ] Priority sorting algorithm
- [ ] Cooldown & debounce logic

### Phase 3: Audio Integration (Week 5)
- [ ] Implement audio ducking
- [ ] Zone transition audio manager
- [ ] Parent zone stack management
- [ ] Play/pause/resume logic

### Phase 4: Offline & Sync (Week 6)
- [ ] SQLite schema for local cache
- [ ] Sync service implementation
- [ ] Audio file download manager
- [ ] Background sync worker

### Phase 5: Testing & Optimization (Week 7-8)
- [ ] Unit tests for priority algorithm
- [ ] Integration tests for zone transitions
- [ ] Field testing with GPS simulator
- [ ] Performance profiling
- [ ] Battery usage optimization

---

## 14. CONCLUSION

Hệ thống **Hierarchical Priority-based Geofencing** này cung cấp:

✅ **Smart Zone Detection** - Tự động chọn zone phù hợp nhất  
✅ **Smooth Audio Transitions** - Ducking/Resume mượt mà  
✅ **Offline-First** - Hoạt động tốt không cần mạng  
✅ **Battery Efficient** - Tối ưu GPS polling  
✅ **Anti-Spam** - Debounce + Cooldown  
✅ **Scalable** - Hỗ trợ hàng ngàn POIs  

**Next Steps**: Implement Phase 1 Backend changes.

---

**Document Version**: 1.0  
**Last Updated**: 11/02/2026  
**Status**: Ready for Implementation  
