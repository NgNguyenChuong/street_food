// ============================================
// MongoDB Seed Data - Nested Geofence Zones
// Example: Vinh Khanh Food Street (3-level hierarchy)
// ============================================

// Command to run: mongosh < nested-zones-seed.js

use StreetFoodNarratorDB;

// ============================================
// LEVEL 1 - AREA: Khu Ẩm Thực Vĩnh Khánh (Large Zone)
// ============================================
db.POIs.insertOne({
    "POI_ID": 1000,
    
    // GeoJSON Location (Center of food street)
    "location": {
        "type": "Point",
        "coordinates": [106.6927, 10.7626] // [Longitude, Latitude]
    },
    
    // Legacy fields (backward compatibility)
    "Latitude": 10.7626,
    "Longitude": 106.6927,
    
    // Zone Configuration
    "ZoneType": "Area",
    "ZoneLevel": 1,
    "Priority": 3,
    "TriggerRadius": 500, // 500 meters
    "CooldownMinutes": 30,
    "MaxPlaysPerSession": 3,
    "ParentZoneId": null,
    
    // Multi-language Content
    "Name_Vi": "Khu Ẩm Thực Vĩnh Khánh",
    "Name_En": "Vinh Khanh Food Street",
    "Description_Vi": "Khu ẩm thực nổi tiếng với hơn 50 gian hàng đường phố, chuyên về các món ăn đặc sản miền Nam. Hoạt động sôi động từ 17h đến 23h mỗi ngày.",
    "Description_En": "Famous food street with over 50 street food stalls, specializing in Southern Vietnamese cuisine. Bustling from 5 PM to 11 PM daily.",
    
    // Audio Files
    "AudioUrl_Vi": "/audio/areas/vinh-khanh-overview-vi.mp3",
    "AudioUrl_En": "/audio/areas/vinh-khanh-overview-en.mp3",
    
    // Metadata
    "Category": "FoodStreet",
    "Tags": ["Street Food", "Night Market", "Local Cuisine"],
    "ImageUrl": "/images/vinh-khanh-street.jpg",
    "Address": "Đường Vĩnh Khánh, Phường 8, Quận 4, TP.HCM",
    
    "IsActive": true,
    "CreatedAt": new Date("2026-01-15T10:00:00Z"),
    "UpdatedAt": new Date("2026-02-11T08:00:00Z")
});

// ============================================
// LEVEL 2 - DISTRICT: Khu Nhà Hàng Hải Sản (Medium Zone)
// ============================================
db.POIs.insertOne({
    "POI_ID": 1010,
    
    "location": {
        "type": "Point",
        "coordinates": [106.6930, 10.7628]
    },
    
    "Latitude": 10.7628,
    "Longitude": 106.6930,
    
    // Zone Configuration
    "ZoneType": "District",
    "ZoneLevel": 2,
    "Priority": 5,
    "TriggerRadius": 100, // 100 meters
    "CooldownMinutes": 15,
    "MaxPlaysPerSession": 2,
    "ParentZoneId": 1000, // ⚠️ Child of Vinh Khanh Area
    
    "Name_Vi": "Khu Nhà Hàng Hải Sản",
    "Name_En": "Seafood Restaurant District",
    "Description_Vi": "Tập trung 15 nhà hàng hải sản tươi sống, được vận chuyển trực tiếp từ biển. Đặc biệt nổi tiếng với cua, ghẹ, tôm hùm và các loại ốc.",
    "Description_En": "15 fresh seafood restaurants with daily catches from the sea. Famous for crabs, lobsters, and various shellfish.",
    
    "AudioUrl_Vi": "/audio/districts/seafood-cluster-vi.mp3",
    "AudioUrl_En": "/audio/districts/seafood-cluster-en.mp3",
    
    "Category": "RestaurantCluster",
    "Tags": ["Seafood", "Fresh Fish", "Local Restaurants"],
    "ImageUrl": "/images/seafood-district.jpg",
    "Address": "Vĩnh Khánh (đoạn hải sản), Quận 4",
    
    "IsActive": true,
    "CreatedAt": new Date("2026-01-15T10:30:00Z"),
    "UpdatedAt": new Date("2026-02-11T08:00:00Z")
});

// ============================================
// LEVEL 3 - SPOT: Quán Hải Sản Bà Năm (Specific Restaurant)
// ============================================
db.POIs.insertOne({
    "POI_ID": 1011,
    
    "location": {
        "type": "Point",
        "coordinates": [106.6931, 10.7629]
    },
    
    "Latitude": 10.7629,
    "Longitude": 106.6931,
    
    // Zone Configuration
    "ZoneType": "Spot",
    "ZoneLevel": 3,
    "Priority": 8, // ⚠️ High priority (most specific)
    "TriggerRadius": 10, // 10 meters (very small)
    "CooldownMinutes": 0, // Play once only
    "MaxPlaysPerSession": 1,
    "ParentZoneId": 1010, // ⚠️ Child of Seafood District
    
    "Name_Vi": "Quán Hải Sản Bà Năm",
    "Name_En": "Ba Nam Seafood Restaurant",
    "Description_Vi": "Quán hải sản gia đình 30 năm tuổi, nổi tiếng với món cua rang me và ghẹ hấp sả. Bà chủ Năm được mệnh danh là 'bà đầu bếp vàng' về hải sản.",
    "Description_En": "30-year-old family seafood restaurant, famous for tamarind stir-fried crab and lemongrass steamed crab. Owner Ba Nam is known as the 'golden chef' of seafood.",
    
    "AudioUrl_Vi": "/audio/spots/ba-nam-restaurant-vi.mp3",
    "AudioUrl_En": "/audio/spots/ba-nam-restaurant-en.mp3",
    
    "Category": "Restaurant",
    "Tags": ["Seafood", "Family Restaurant", "Local Famous"],
    "SignatureDish": "Cua rang me",
    "SignatureDishes": ["Cua rang me", "Ghẹ hấp sả", "Ốc hương xào bơ tỏi"],
    "ImageUrl": "/images/ba-nam-restaurant.jpg",
    "Address": "123 Vĩnh Khánh, P.8, Q.4, TP.HCM",
    "PhoneNumber": "0908123456",
    "OpeningHoursText": "17:00 - 23:00 (Thứ 2 - Chủ Nhật)",
    "AveragePrice": 250000,
    "PriceLevel": 2,
    "Rating": 4.7,
    
    "IsActive": true,
    "CreatedAt": new Date("2026-01-15T11:00:00Z"),
    "UpdatedAt": new Date("2026-02-11T08:00:00Z")
});

// ============================================
// LEVEL 3 - SPOT: Quán Hải Sản Bà Sáu (Another Specific Restaurant)
// ============================================
db.POIs.insertOne({
    "POI_ID": 1012,
    
    "location": {
        "type": "Point",
        "coordinates": [106.6932, 10.7627]
    },
    
    "Latitude": 10.7627,
    "Longitude": 106.6932,
    
    "ZoneType": "Spot",
    "ZoneLevel": 3,
    "Priority": 8,
    "TriggerRadius": 12,
    "CooldownMinutes": 0,
    "MaxPlaysPerSession": 1,
    "ParentZoneId": 1010,
    
    "Name_Vi": "Quán Hải Sản Bà Sáu",
    "Name_En": "Ba Sau Seafood Restaurant",
    "Description_Vi": "Chuyên về tôm hùm nướng pho mai và mực nướng sa tế. Không gian sạch sẽ, thoáng mát với view nhìn ra sông.",
    "Description_En": "Specializing in cheese-grilled lobster and satay grilled squid. Clean, airy space with river view.",
    
    "AudioUrl_Vi": "/audio/spots/ba-sau-restaurant-vi.mp3",
    "AudioUrl_En": "/audio/spots/ba-sau-restaurant-en.mp3",
    
    "Category": "Restaurant",
    "Tags": ["Seafood", "River View", "Lobster"],
    "SignatureDish": "Tôm hùm nướng pho mai",
    "ImageUrl": "/images/ba-sau-restaurant.jpg",
    "Address": "125 Vĩnh Khánh, P.8, Q.4, TP.HCM",
    "PhoneNumber": "0907234567",
    "AveragePrice": 350000,
    "PriceLevel": 3,
    
    "IsActive": true,
    "CreatedAt": new Date("2026-01-15T11:15:00Z")
});

// ============================================
// LEVEL 2 - DISTRICT: Khu Ăn Vặt (Snack Area)
// ============================================
db.POIs.insertOne({
    "POI_ID": 1020,
    
    "location": {
        "type": "Point",
        "coordinates": [106.6925, 10.7624]
    },
    
    "Latitude": 10.7624,
    "Longitude": 106.6925,
    
    "ZoneType": "District",
    "ZoneLevel": 2,
    "Priority": 5,
    "TriggerRadius": 80,
    "CooldownMinutes": 20,
    "MaxPlaysPerSession": 2,
    "ParentZoneId": 1000,
    
    "Name_Vi": "Khu Ăn Vặt Vỉa Hè",
    "Name_En": "Street Snack Zone",
    "Description_Vi": "Tập trung các xe đẩy bán đồ ăn vặt: bánh tráng trộn, xôi, chè, bắp nướng. Giá rất phải chăng từ 10-30 ngàn đồng.",
    "Description_En": "Street food carts selling snacks: mixed rice paper, sticky rice, sweet soup, grilled corn. Very affordable 10-30k VND.",
    
    "AudioUrl_Vi": "/audio/districts/snack-zone-vi.mp3",
    "AudioUrl_En": "/audio/districts/snack-zone-en.mp3",
    
    "Category": "StreetFood",
    "Tags": ["Cheap Eats", "Street Snacks", "Night Food"],
    "ImageUrl": "/images/snack-zone.jpg",
    
    "IsActive": true,
    "CreatedAt": new Date("2026-01-15T12:00:00Z")
});

// ============================================
// CREATE GEOSPATIAL INDEX (CRITICAL!)
// ============================================
db.POIs.createIndex({ "location": "2dsphere" });

// ============================================
// CREATE COMPOSITE INDEX FOR QUERIES
// ============================================
db.POIs.createIndex({ 
    "IsActive": 1, 
    "ZoneType": 1, 
    "Priority": -1 
});

db.POIs.createIndex({ "ParentZoneId": 1 });
db.POIs.createIndex({ "POI_ID": 1 });

// ============================================
// VERIFY DATA
// ============================================
print("\n========================================");
print("✅ Seed Data Loaded Successfully!");
print("========================================\n");

print("Total POIs inserted: " + db.POIs.countDocuments());

print("\n--- Zone Hierarchy ---");
print("Area (Level 1): " + db.POIs.countDocuments({ ZoneType: "Area" }));
print("District (Level 2): " + db.POIs.countDocuments({ ZoneType: "District" }));
print("Spot (Level 3): " + db.POIs.countDocuments({ ZoneType: "Spot" }));

print("\n--- Test Geospatial Query ---");
print("POIs near Vinh Khanh center (500m radius):");

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
}).forEach(function(poi) {
    print("  - " + poi.Name_Vi + " (" + poi.ZoneType + ", Priority: " + poi.Priority + ")");
});

print("\n========================================");
print("🎯 Ready for Testing!");
print("========================================\n");
