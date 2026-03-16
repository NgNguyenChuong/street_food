const fs = require("fs");
const path = require("path");

const dbName = "StreetFoodNarratorDB";
const jsonPath = path.join(__dirname, "vinh-khanh-pois.json");

if (!fs.existsSync(jsonPath)) {
  throw new Error(`Seed file not found: ${jsonPath}`);
}

const raw = fs.readFileSync(jsonPath, "utf8");
const items = JSON.parse(raw);

if (!Array.isArray(items) || items.length === 0) {
  throw new Error("Seed file is empty or invalid.");
}

const now = new Date();
const pois = items.map((item, idx) => ({
  POI_ID: idx + 1,
  Name_Vi: item.nameVi || "",
  Name_En: item.nameEn || null,
  Name_Ja: item.nameJa || null,
  Name_Fr: item.nameFr || null,
  Name_Ko: item.nameKo || null,
  Name_Zh: item.nameZh || null,
  Description_Vi: item.descriptionVi || "",
  Description_En: item.descriptionEn || null,
  Description_Ja: item.descriptionJa || null,
  Description_Fr: item.descriptionFr || null,
  Description_Ko: item.descriptionKo || null,
  Description_Zh: item.descriptionZh || null,
  Latitude: item.latitude || 0,
  Longitude: item.longitude || 0,
  Address: item.address || null,
  SignatureDish: item.signatureDish || null,
  SignatureDishes: Array.isArray(item.signatureDishes) ? item.signatureDishes : null,
  Specialties: Array.isArray(item.specialties) ? item.specialties : null,
  FunFact: item.funFact || null,
  History: item.history || null,
  Story: item.story || null,
  EstimatedHours: item.estimatedHours || null,
  OpeningHours: Array.isArray(item.openingHours) ? item.openingHours : null,
  OpeningHoursText: item.openingHoursText || null,
  PhoneNumber: item.phoneNumber || null,
  AveragePrice: item.averagePrice || null,
  PriceLevel: item.priceLevel || null,
  Rating: item.rating || null,
  Tags: Array.isArray(item.tags) ? item.tags : null,
  Category: item.category || null,
  ImageUrl: item.imageUrl || null,
  ImageUrls: Array.isArray(item.imageUrls) ? item.imageUrls : null,
  TriggerRadius: item.triggerRadius || 50,
  IsActive: item.isActive !== false,
  VendorId: item.vendorId || null,
  CreatedAt: now,
  UpdatedAt: now,
  DeletedAt: null
}));

const dbRef = db.getSiblingDB(dbName);
const deleteResult = dbRef.pois.deleteMany({});
const insertResult = dbRef.pois.insertMany(pois);
dbRef.counters.updateOne(
  { Name: "poi_id" },
  { $set: { Value: pois.length } },
  { upsert: true }
);

print(`Deleted: ${deleteResult.deletedCount}`);
print(`Inserted: ${insertResult.insertedCount}`);
