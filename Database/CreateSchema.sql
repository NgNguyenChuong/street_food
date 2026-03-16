/*
MongoDB initialization script (mongosh)
This file replaces the old SQL schema. Run with:
  mongosh < d:/project/street_food/Database/CreateSchema.sql
*/

db = db.getSiblingDB('StreetFoodNarratorDB');

// Collections
['pois','audio_contents','tours','poi_tours','narration_logs','vendor_profiles','counters'].forEach((name) => {
  if (!db.getCollectionNames().includes(name)) {
    db.createCollection(name);
  }
});

// Indexes
try { db.pois.createIndex({ POI_ID: 1 }, { unique: true }); } catch (e) {}
try { db.audio_contents.createIndex({ AudioContent_ID: 1 }, { unique: true }); } catch (e) {}
try { db.audio_contents.createIndex({ POI_ID: 1 }); } catch (e) {}
try { db.tours.createIndex({ Tour_ID: 1 }, { unique: true }); } catch (e) {}
try { db.poi_tours.createIndex({ POI_ID: 1, Tour_ID: 1 }, { unique: true }); } catch (e) {}
try { db.pois.createIndex({ VendorId: 1 }); } catch (e) {}
try { db.vendor_profiles.createIndex({ VendorId: 1 }, { unique: true }); } catch (e) {}
