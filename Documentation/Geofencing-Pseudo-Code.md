# Pseudo-code: Smart Geofencing với Overlapping Zones

## Tổng quan thuật toán

```
Khi GPS Location thay đổi:
    1. Debounce kiểm tra (chống dội GPS)
    2. Tìm TẤT CẢ zones chứa user
    3. Sắp xếp theo Priority Score
    4. Chọn zone cao nhất
    5. Kiểm tra Cooldown
    6. Xử lý chuyển đổi zone (nested logic)
    7. Phát audio tương ứng
```

---

## Pseudo-code Chi tiết

### 1. Main Entry Point

```csharp
FUNCTION OnLocationChanged(UserLocation location)
{
    // ============================================
    // STEP 0: Anti-Jitter Debounce
    // ============================================
    IF NOT ShouldProcessLocation(location) THEN
        RETURN  // Ignore this GPS update
    END IF
    
    UPDATE lastProcessedLocation = location
    UPDATE lastProcessedTime = Now()
    
    // ============================================
    // STEP 1: Find All Active Zones
    // ============================================
    activeZones = EMPTY_LIST
    
    FOR EACH poi IN cachedPOIs WHERE poi.IsActive DO
        distance = HaversineDistance(
            userLat, userLon, 
            poi.Lat, poi.Lon
        )
        
        IF distance <= poi.TriggerRadius THEN
            score = CalculatePriorityScore(poi, distance)
            ADD (poi, distance, score) TO activeZones
        END IF
    END FOR
    
    // ============================================
    // STEP 2: Sort by Priority (Descending)
    // ============================================
    SORT activeZones BY score DESC, distance ASC
    
    // ============================================
    // STEP 3: Handle Zone State
    // ============================================
    IF activeZones IS EMPTY THEN
        CALL HandleExitAllZones()
        RETURN
    END IF
    
    topZone = activeZones[0].poi
    
    // ============================================
    // STEP 4: Check if SAME zone
    // ============================================
    IF currentActiveZone.ID == topZone.ID THEN
        RETURN  // No change, stay in same zone
    END IF
    
    // ============================================
    // STEP 5: Check Cooldown
    // ============================================
    IF IsInCooldown(topZone) THEN
        LOG "Zone in cooldown, skip"
        RETURN
    END IF
    
    // ============================================
    // STEP 6: Check Max Plays Limit
    // ============================================
    IF NOT CanPlayZone(topZone) THEN
        LOG "Max plays reached, skip"
        RETURN
    END IF
    
    // ============================================
    // STEP 7: Transition Zones!
    // ============================================
    CALL HandleZoneTransition(currentActiveZone, topZone, activeZones)
}
```

---

### 2. Priority Score Calculation

```csharp
FUNCTION CalculatePriorityScore(POI poi, DOUBLE distance) -> DOUBLE
{
    score = 0
    
    // Base: User-set priority (weighted heavily)
    score += poi.Priority × 100
    
    // Bonus: Smaller radius = more specific location
    // Example: Radius 10m gets +100, Radius 500m gets +2
    score += 1000 / MAX(poi.Radius, 1)
    
    // Bonus: Zone level (Spot=3 > District=2 > Area=1)
    score += poi.ZoneLevel × 10
    
    // Small penalty: farther = slightly lower priority
    score -= distance × 0.1
    
    RETURN score
}
```

**Ví dụ Tính toán**:
```
Spot (Priority=8, Radius=10m, Level=3, Distance=5m):
  = (8×100) + (1000/10) + (3×10) - (5×0.1)
  = 800 + 100 + 30 - 0.5
  = 929.5

Area (Priority=3, Radius=500m, Level=1, Distance=50m):
  = (3×100) + (1000/500) + (1×10) - (50×0.1)
  = 300 + 2 + 10 - 5
  = 307

→ Spot sẽ được chọn (929.5 > 307)
```

---

### 3. Zone Transition Logic (CRITICAL!)

```csharp
FUNCTION HandleZoneTransition(POI oldZone, POI newZone, LIST activeZones)
{
    LOG "Transition: {oldZone.Name} → {newZone.Name}"
    
    // ============================================
    // CASE 1: Entering NESTED zone (Child in Parent)
    // ============================================
    IF oldZone EXISTS AND IsNestedZone(newZone, oldZone) THEN
        LOG "🔹 Nested Entry"
        
        // Duck (reduce volume) parent audio
        CALL AudioService.DuckAudio(volume: 0.2)
        
        // Play child audio immediately
        CALL AudioService.Play(newZone.AudioUrl)
        
        // Save parent to stack for resume later
        PUSH oldZone TO parentZoneStack
        
        GOTO UPDATE_STATE
    END IF
    
    // ============================================
    // CASE 2: Exiting NESTED zone (back to Parent)
    // ============================================
    IF oldZone EXISTS AND parentZoneStack NOT EMPTY THEN
        parentZone = PEEK parentZoneStack
        
        // Check if parent still in range
        IF activeZones CONTAINS parentZone THEN
            LOG "🔙 Resume Parent"
            
            // Restore parent audio volume
            CALL AudioService.UnDuckAudio()
            
            POP parentZoneStack
            currentActiveZone = parentZone
            RETURN  // Don't trigger new audio
        ELSE
            // Parent out of range, clear stack
            CLEAR parentZoneStack
        END IF
    END IF
    
    // ============================================
    // CASE 3: Completely NEW zone (not nested)
    // ============================================
    LOG "🆕 New Zone"
    
    // Stop current audio
    CALL AudioService.Stop()
    
    // Play new zone audio
    CALL AudioService.Play(newZone.AudioUrl)
    
    // ============================================
    // UPDATE STATE
    // ============================================
    UPDATE_STATE:
    currentActiveZone = newZone
    CALL RecordZoneEntry(newZone)
}
```

---

### 4. Nested Zone Detection

```csharp
FUNCTION IsNestedZone(POI child, POI parent) -> BOOLEAN
{
    // Method 1: Explicit parent relationship
    IF child.ParentZoneId == parent.POI_ID THEN
        RETURN TRUE
    END IF
    
    // Method 2: Implicit hierarchy (priority + radius)
    IF child.Priority > parent.Priority AND 
       child.Radius < parent.Radius THEN
        RETURN TRUE
    END IF
    
    // Method 3: Zone level comparison
    IF child.ZoneLevel > parent.ZoneLevel THEN
        RETURN TRUE
    END IF
    
    RETURN FALSE
}
```

**Ví dụ**:
```
Child = Spot (Priority=8, Radius=10m, Level=3)
Parent = Area (Priority=3, Radius=500m, Level=1)

Method 2: 8 > 3 AND 10 < 500 → TRUE (nested)
Method 3: 3 > 1 → TRUE (nested)
```

---

### 5. Haversine Distance Calculation

```csharp
FUNCTION HaversineDistance(
    DOUBLE lat1, DOUBLE lon1,
    DOUBLE lat2, DOUBLE lon2
) -> DOUBLE
{
    CONST R = 6371000  // Earth radius in meters
    
    // Convert to radians
    φ1 = lat1 × π / 180
    φ2 = lat2 × π / 180
    Δφ = (lat2 - lat1) × π / 180
    Δλ = (lon2 - lon1) × π / 180
    
    // Haversine formula
    a = sin²(Δφ/2) + cos(φ1) × cos(φ2) × sin²(Δλ/2)
    c = 2 × atan2(√a, √(1−a))
    
    distance = R × c
    
    RETURN distance  // in meters
}
```

**Ví dụ**:
```
Point A: [10.7626, 106.6927]
Point B: [10.7629, 106.6931]

Distance = ~50 meters
```

---

### 6. Cooldown Management

```csharp
FUNCTION IsInCooldown(POI poi) -> BOOLEAN
{
    IF NOT zoneHistoryMap.Contains(poi.POI_ID) THEN
        RETURN FALSE  // Never triggered before
    END IF
    
    lastTriggered = zoneHistoryMap[poi.POI_ID].LastTriggeredAt
    elapsed = Now() - lastTriggered
    
    RETURN elapsed.TotalMinutes < poi.CooldownMinutes
}

FUNCTION RecordZoneEntry(POI poi)
{
    IF zoneHistoryMap.Contains(poi.POI_ID) THEN
        history = zoneHistoryMap[poi.POI_ID]
        history.LastTriggeredAt = Now()
        history.PlayCount++
    ELSE
        history = NEW ZoneHistory {
            POI_ID: poi.POI_ID,
            FirstPlayedAt: Now(),
            LastTriggeredAt: Now(),
            PlayCount: 1,
            SessionId: currentSessionId
        }
        zoneHistoryMap[poi.POI_ID] = history
    END IF
    
    // Save to database
    CALL Database.SaveZoneHistory(history)
    
    LOG "Recorded zone: {poi.Name} (play #{history.PlayCount})"
}
```

---

### 7. Debounce (Anti-GPS Jitter)

```csharp
FUNCTION ShouldProcessLocation(UserLocation location) -> BOOLEAN
{
    // Always process first location
    IF lastProcessedLocation IS NULL THEN
        RETURN TRUE
    END IF
    
    // Time-based debounce
    timeSinceLast = Now() - lastProcessedTime
    IF timeSinceLast.TotalSeconds < 3 THEN
        RETURN FALSE  // Too soon, skip
    END IF
    
    // Distance-based debounce
    distanceFromLast = HaversineDistance(
        lastLocation.Lat, lastLocation.Lon,
        location.Lat, location.Lon
    )
    
    IF distanceFromLast < 5 THEN
        RETURN FALSE  // Moved less than 5m, ignore
    END IF
    
    RETURN TRUE  // OK to process
}
```

---

## Kịch bản Thực tế

### Scenario 1: Đi từ ngoài vào gian hàng cụ thể

```
User Path:
  [Outside] → [Enter Area] → [Enter District] → [Enter Spot]

Timeline:
  T=0s   : Lat=10.7600, Lon=106.6900 (Outside all zones)
           → No active zones
  
  T=30s  : Lat=10.7626, Lon=106.6927 (Inside Area, R=500m)
           → activeZones = [Area(score=307)]
           → Play "Khu Ẩm Thực Vĩnh Khánh" audio
  
  T=60s  : Lat=10.7628, Lon=106.6930 (Inside District, R=100m)
           → activeZones = [District(score=507), Area(307)]
           → District wins! IsNested(District, Area) = TRUE
           → Duck Area audio to 20%
           → Play "Khu Hải Sản" audio
           → parentStack = [Area]
  
  T=90s  : Lat=10.7629, Lon=106.6931 (Inside Spot, R=10m)
           → activeZones = [Spot(929), District(507), Area(307)]
           → Spot wins! IsNested(Spot, District) = TRUE
           → Duck District audio to 20%
           → Play "Quán Bà Năm" audio
           → parentStack = [Area, District]
```

### Scenario 2: Thoát khỏi gian hàng, quay lại khu vực

```
User Path:
  [Spot] → [Exit Spot] → [Exit District] → [Exit Area]

Timeline:
  T=0s   : Inside Spot (Quán Bà Năm)
           → Playing Spot audio
  
  T=30s  : Lat=10.7628, Lon=106.6930 (Exited Spot, still in District)
           → activeZones = [District(507), Area(307)]
           → District wins
           → parentStack has District
           → UnDuck audio → Resume District audio
           → POP parentStack → [Area]
  
  T=60s  : Lat=10.7626, Lon=106.6927 (Exited District, still in Area)
           → activeZones = [Area(307)]
           → parentStack has Area
           → UnDuck audio → Resume Area audio
           → POP parentStack → []
  
  T=90s  : Lat=10.7600, Lon=106.6900 (Outside all)
           → activeZones = []
           → FadeOut and Stop all audio
```

### Scenario 3: Di chuyển giữa 2 Spots cùng cấp

```
User Path:
  [Quán Bà Năm] → [Quán Bà Sáu]

Timeline:
  T=0s   : Inside Spot A (Bà Năm)
           → Playing Spot A audio
  
  T=20s  : Lat=10.7627, Lon=106.6932 (Inside Spot B, Bà Sáu)
           → activeZones = [SpotB(929), SpotA(920)] // Both in range
           → SpotB slightly higher score
           → NOT nested (same level)
           → Stop Spot A audio
           → Play Spot B audio (new zone)
```

---

## Các Thông số Recommended

| Parameter               | Area    | District | Spot    | Lý do                          |
|-------------------------|---------|----------|---------|--------------------------------|
| **Radius**              | 300-1000m | 50-200m | 5-30m  | Phạm vi phù hợp từng loại      |
| **Priority**            | 3       | 5        | 8       | Spot quan trọng hơn            |
| **CooldownMinutes**     | 30      | 15       | 0       | Area spam nhiều, Spot 1 lần    |
| **MaxPlaysPerSession**  | 3       | 2        | 1       | Giới hạn replay                |

---

## Tóm tắt Flow

```
GPS Update
    ↓
[Debounce] → Skip nếu < 3s hoặc < 5m
    ↓
[Find Zones] → Tính distance với tất cả POIs
    ↓
[Filter] → Chỉ giữ zones có distance ≤ Radius
    ↓
[Score] → Priority×100 + 1000/Radius + Level×10
    ↓
[Sort] → Zones theo score (DESC)
    ↓
[Pick Top] → Chọn zone điểm cao nhất
    ↓
[Check Cooldown] → Skip nếu vừa phát gần đây
    ↓
[Nested?]
    ├─ Yes → Duck parent, play child, push stack
    ├─ Exit nested? → Unduck, resume parent
    └─ New zone → Stop old, play new
    ↓
[Record History] → Lưu timestamps cho cooldown
```

---

## Notes Quan trọng

1. **GeoJSON coordinates**: `[Longitude, Latitude]` (không phải Lat, Lon!)
2. **2dsphere Index**: Bắt buộc phải có trong MongoDB
3. **Priority > Radius**: Khi tính score, priority quan trọng hơn
4. **Parent Stack**: Dùng Stack để resume khi exit nested zones
5. **Cooldown per POI**: Mỗi POI có cooldown riêng
6. **Session ID**: Reset mỗi lần mở app để track plays per session

---

**Document Version**: 1.0  
**Last Updated**: 11/02/2026  
