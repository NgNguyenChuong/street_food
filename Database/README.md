# 🗄️ DATABASE - Street Food Narrator

## Overview
Database seed scripts và schema definition cho MongoDB backend.

---

## 📁 File Structure

```
Database/
├── CreateSchema.sql           # MongoDB schema + indexes (legacy format)
├── SeedData.mongodb.js        # Original seed data (example/test data)
├── VinhKhanh-Real-Seed.mongodb.js  # ⭐ REAL POI DATA (Production)
├── nested-zones-seed.js       # Nested zones example
└── README.md                  # Documentation
```

---

## 🚀 Cách sử dụng (MongoDB)

### 1. Tạo collections + indexes
```bash
mongosh < d:/project/street_food/Database/CreateSchema.sql
```

### 2. Import dữ liệu THẬT (Production Data)
```bash
# Import 12 POIs thật từ phố Vĩnh Khánh (Recommended)
mongosh < d:/project/street_food/Database/VinhKhanh-Real-Seed.mongodb.js
```

### 3. Import dữ liệu mẫu (Test Data)
```bash
# Import dữ liệu test/example (Optional)
mongosh < d:/project/street_food/Database/SeedData.mongodb.js
```

---

## 🌟 VinhKhanh-Real-Seed.mongodb.js (Production Data)

**12 POIs thật được research từ:**
- 📰 Time Out Magazine "World's Coolest Streets 2025"
- ⭐ Michelin Guide Bib Gourmand 2024
- 🔍 Local reviews & field research

**Includes:**
1. **Cổng chào Phố Ẩm thực Vĩnh Khánh** (Entry Gate - Area)
2. **Ốc Vũ** - Affordable icon with legendary tamarind sauce
3. **Ốc Thảo** - Refined space, coconut-flavored specialties
4. **Ốc Sáu Nở** - Original sidewalk culture
5. **Ốc Oanh** ⭐ Michelin Bib Gourmand 2024
6. **A Fat Hot Pot** - Hong Kong retro vibes
7. **Chilli Lẩu Nướng** - Youth favorite, affordable
8. **Alo Quán** - Thai-Vietnamese fusion
9. **Ốc Đào 2** - Chain brand quality
10. **Lãng Quán** - Late night till 4 AM
11. **Ớt Xiêm Quán** - Spicy food paradise
12. **Bún Cá Châu Đốc Dì Tư** - Southern breakfast classic

**Data Fields:**
- ✅ POI_ID (1-12)
- ✅ Multi-language: Vietnamese (vi) + English (en)
- ✅ GeoJSON Location (2dsphere index compatible)
- ✅ Geofencing: TriggerRadius, Priority, ZoneType
- ✅ Rich metadata: SignatureDish, FunFact, OpeningHoursText
- ✅ Ready for audio URL population

**Usage:**
```javascript
// Auto-clears existing POIs and resets counter
use('streetfood_narrator_db');
load('Database/VinhKhanh-Real-Seed.mongodb.js');
```

---

## 📊 Data Quality

### Production Data (VinhKhanh-Real-Seed.mongodb.js)
- ✅ Real POI coordinates (GPS verified)
- ✅ Authentic descriptions (bilingual vi + en)
- ✅ Verified signature dishes
- ✅ Accurate opening hours
- ✅ Michelin/Time Out references

### Test Data (SeedData.mongodb.js)
- ⚠️ Example data only
- ⚠️ Generic descriptions
- ⚠️ For development/testing only

---

## 🎯 Geofencing Configuration

**ZoneType & Priority:**
- **Area** (Entry Gate): Priority 10, Radius 80m
- **Spot** (Restaurants): Priority 6-9, Radius 40-60m

**Cooldown Strategy:**
- Area: 0 minutes (play once per session)
- Spots: 30 minutes (prevent spam)

**Max Plays Per Session:** 1 (all POIs)

---

## 🔄 Migration Notes

### Mapping from PostgreSQL → MongoDB:
- `lat, lng` → `Location` (GeoJSON Point) + `Latitude`, `Longitude` (legacy)
- `radius` → `TriggerRadius`
- `priority` → `Priority`
- `name_vi, name_en` → `Name_Vi`, `Name_En` (Name_Zh: null)
- `description_vi, description_en` → `Description_Vi`, `Description_En`
- `signature_dish` → `SignatureDish` + `SignatureDishes` (array)
- `fun_fact` → `FunFact`
- `estimated_hours` → `OpeningHoursText`

**Removed languages:** Japanese (ja), French (fr), Korean (ko)
**Supported languages:** Vietnamese (vi), English (en), Chinese (zh - coming soon)

---

## 🛠️ Commands

```bash
# Connect to MongoDB
mongosh "mongodb://localhost:27017/streetfood_narrator_db"

# Check POI count
db.POIs.countDocuments()

# List all POIs
db.POIs.find({}, { POI_ID: 1, Name_Vi: 1, Name_En: 1 }).sort({ POI_ID: 1 })

# Find Michelin restaurant
db.POIs.find({ "Tags": "Michelin Bib Gourmand" })

# Geospatial query (find POIs near coordinate)
db.POIs.find({
  Location: {
    $near: {
      $geometry: { type: "Point", coordinates: [106.7027, 10.7615] },
      $maxDistance: 100
    }
  }
})

# Clear all POIs
db.POIs.deleteMany({})
```

---

## 📝 Next Steps

1. ✅ Import VinhKhanh-Real-Seed.mongodb.js
2. ⏳ Generate audio files for each POI (vi + en)
3. ⏳ Update AudioUrl_Vi, AudioUrl_En fields
4. ⏳ Add Chinese translations (Name_Zh, Description_Zh)
5. ⏳ Collect POI images → Update ImageUrl fields

---

## 🚨 Important Notes

- **Always use VinhKhanh-Real-Seed.mongodb.js for production/demo**
- SeedData.mongodb.js is outdated and only for testing
- POI_ID counter is auto-managed (starts at 1, increments to 12)
- Audio URLs will be populated after TTS generation
- GeoJSON format: `{ type: "Point", coordinates: [lng, lat] }`

---

## 📚 References

- [MongoDB Geospatial Queries](https://docs.mongodb.com/manual/geospatial-queries/)
- [Time Out 2025 Coolest Streets](https://www.timeout.com/coolest-streets)
- [Michelin Guide Vietnam 2024](https://guide.michelin.com/vn/en)

