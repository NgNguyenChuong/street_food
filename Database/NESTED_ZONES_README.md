# Nested Geofence Zones - MongoDB Seed Data

## Overview
This seed data demonstrates a 3-level hierarchical geofence zone system for Vinh Khanh Food Street area.

## Zone Hierarchy

```
Level 1 (Area) - Khu Ẩm Thực Vĩnh Khánh
├── Level 2 (District) - Khu Nhà Hàng Hải Sản
│   ├── Level 3 (Spot) - Quán Hải Sản Bà Năm
│   └── Level 3 (Spot) - Quán Hải Sản Bà Sáu
└── Level 2 (District) - Khu Ăn Vặt Vỉa Hè
```

## Zone Details

### Level 1: Area (POI_ID: 1000)
- **Name**: Khu Ẩm Thực Vĩnh Khánh
- **Type**: Area
- **Radius**: 500m
- **Priority**: 3
- **Cooldown**: 30 minutes
- **Location**: [106.6927, 10.7626]

### Level 2: Districts
1. **Khu Nhà Hàng Hải Sản** (POI_ID: 1010)
   - Radius: 100m
   - Priority: 5
   - Cooldown: 15 minutes
   - Parent: 1000

2. **Khu Ăn Vặt Vỉa Hè** (POI_ID: 1020)
   - Radius: 80m
   - Priority: 5
   - Cooldown: 20 minutes
   - Parent: 1000

### Level 3: Spots
1. **Quán Hải Sản Bà Năm** (POI_ID: 1011)
   - Radius: 10m
   - Priority: 8 (HIGH)
   - Cooldown: 0 (play once)
   - Parent: 1010

2. **Quán Hải Sản Bà Sáu** (POI_ID: 1012)
   - Radius: 12m
   - Priority: 8 (HIGH)
   - Cooldown: 0 (play once)
   - Parent: 1010

## How to Load

```bash
# From MongoDB shell
mongosh < nested-zones-seed.js

# Or connect first, then load
mongosh
use StreetFoodNarratorDB
load('nested-zones-seed.js')
```

## Testing Scenarios

### Scenario 1: Walk from outside to specific restaurant
1. **Start**: Outside all zones
2. **Walk to**: [106.6927, 10.7626] → Enter **Area** → Play Area audio
3. **Walk to**: [106.6930, 10.7628] → Enter **District** → Duck Area, Play District
4. **Walk to**: [106.6931, 10.7629] → Enter **Spot** → Duck District, Play Spot

### Scenario 2: Walk between two spots
1. **Start at**: Quán Bà Năm [106.6931, 10.7629]
2. **Walk to**: Quán Bà Sáu [106.6932, 10.7627]
3. **Expected**: Stop Bà Năm → Play Bà Sáu (both same priority, but new zone wins)

### Scenario 3: Exit nested zone
1. **Start at**: Quán Bà Năm (Spot)
2. **Walk out**: Exit Spot, still in District
3. **Expected**: Resume District audio (unduck)

## Geospatial Query Examples

### Find all POIs near a point
```javascript
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

### Find POIs within radius
```javascript
db.POIs.find({
    location: {
        $geoWithin: {
            $centerSphere: [
                [106.6927, 10.7626], 
                500 / 6378100 // Radius in radians (500m / Earth radius)
            ]
        }
    }
})
```

### Find nested zones
```javascript
// Find all children of Area 1000
db.POIs.find({ ParentZoneId: 1000 })

// Find zone hierarchy
db.POIs.aggregate([
    { $match: { ZoneType: "Area" } },
    {
        $lookup: {
            from: "POIs",
            localField: "POI_ID",
            foreignField: "ParentZoneId",
            as: "children"
        }
    }
])
```

## Indexes Required

```javascript
// Geospatial index (REQUIRED)
db.POIs.createIndex({ "location": "2dsphere" })

// Performance indexes
db.POIs.createIndex({ "IsActive": 1, "ZoneType": 1, "Priority": -1 })
db.POIs.createIndex({ "ParentZoneId": 1 })
db.POIs.createIndex({ "POI_ID": 1 })
```

## Notes

- **GeoJSON Format**: Always `[Longitude, Latitude]` (not Lat, Lon!)
- **2dsphere Index**: Required for `$near` and `$geoWithin` queries
- **Priority Logic**: Higher number = more important (Spot=8 > District=5 > Area=3)
- **Cooldown**: Area zones have longer cooldown (30min) to prevent spam

## API Endpoint to Use

```http
GET /api/pois/nearby?lat=10.7626&lon=106.6927&radius=1000
```

Returns all POIs within 1km, sorted by distance.
