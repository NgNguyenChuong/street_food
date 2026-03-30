using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Hosting;
using MongoDB.Driver;
using StreetFoodNarrator.API.Models;
using System.Text.Json;

namespace StreetFoodNarrator.API.Data;

public static class DbInitializer
{
    public static async Task Initialize(IServiceProvider serviceProvider)
    {
        await EnsureIndexes(serviceProvider);

        var roleManager = serviceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        // Tạo roles nếu chưa tồn tại
        string[] roleNames = { "Admin", "Vendor", "User" };
        foreach (var roleName in roleNames)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
                await roleManager.CreateAsync(new ApplicationRole { Name = roleName });
        }

        // Tạo tài khoản admin mặc định nếu chưa có user nào
        if (!userManager.Users.Any())
        {
            var adminUser = new ApplicationUser
            {
                UserName = "admin@streetfood.vn",
                Email = "admin@streetfood.vn",
                FullName = "Antran",
                EmailConfirmed = true,
                CreatedAt = DateTime.UtcNow
            };

            var result = await userManager.CreateAsync(adminUser, "Admin@123");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, "Admin");
                Console.WriteLine("✅ Admin account created: admin@streetfood.vn / Admin@123");
            }
        }

        await SeedPois(serviceProvider);
        await SeedVendors(serviceProvider, userManager);
        // Keep vendor-POI ownership mapping in sync for existing datasets.
        await LinkExistingPoisToVendors(serviceProvider);
        await SeedMenuItems(serviceProvider);
        await SeedTours(serviceProvider);
        await SeedAnalyticsData(serviceProvider, userManager);
    }

    private static async Task EnsureIndexes(IServiceProvider serviceProvider)
    {
        var db = serviceProvider.GetRequiredService<MongoDbContext>();

        var poiIdIndex = new CreateIndexModel<POI>(
            Builders<POI>.IndexKeys.Ascending(p => p.POI_ID),
            new CreateIndexOptions { Unique = true, Name = "ux_poi_poi_id" });

        var poiVendorIndex = new CreateIndexModel<POI>(
            Builders<POI>.IndexKeys.Ascending(p => p.VendorId),
            new CreateIndexOptions { Name = "ix_poi_vendor_id" });

        var vendorIdIndex = new CreateIndexModel<VendorProfile>(
            Builders<VendorProfile>.IndexKeys.Ascending(v => v.VendorId),
            new CreateIndexOptions { Unique = true, Name = "ux_vendor_profile_vendor_id" });

        await db.POIs.Indexes.CreateManyAsync(new[] { poiIdIndex, poiVendorIndex });
        await db.VendorProfiles.Indexes.CreateOneAsync(vendorIdIndex);
    }

    // ─────────────────────────────────────────────────────────────
    // SEED POIs
    // ─────────────────────────────────────────────────────────────
    private static async Task SeedPois(IServiceProvider serviceProvider)
    {
        var db = serviceProvider.GetRequiredService<MongoDbContext>();
        var sequence = serviceProvider.GetRequiredService<MongoSequenceService>();
        var env = serviceProvider.GetRequiredService<IHostEnvironment>();

        var existing = await db.POIs.CountDocumentsAsync(Builders<POI>.Filter.Empty);
        if (existing > 0) return;

        var seedPath = Path.Combine(env.ContentRootPath, "Data", "seed", "vinh-khanh-pois.json");
        if (!File.Exists(seedPath)) return;

        var json = await File.ReadAllTextAsync(seedPath);
        var items = JsonSerializer.Deserialize<List<SeedPoi>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new List<SeedPoi>();

        if (!items.Any()) return;

        var pois = new List<POI>();
        foreach (var item in items)
        {
            var nextId = await sequence.GetNextAsync("poi_id");
            pois.Add(new POI
            {
                POI_ID = nextId,
                Name_Vi = item.NameVi ?? string.Empty,
                Name_En = item.NameEn,
                Name_Zh = item.NameZh,
                Description_Vi = item.DescriptionVi ?? string.Empty,
                Description_En = item.DescriptionEn,
                Description_Zh = item.DescriptionZh,
                Address = item.Address,
                Location = GeoJsonLocation.FromLatLon((double)item.Latitude, (double)item.Longitude),
                Category = item.Category,
                SignatureDishes = item.SignatureDishes ?? (!string.IsNullOrWhiteSpace(item.SignatureDish) ? new List<string> { item.SignatureDish } : null),
                Specialties = item.Specialties,
                History = item.History,
                Story = item.Story,
                OpeningHours = item.OpeningHours,
                OpeningHoursText = item.OpeningHoursText,
                PhoneNumber = item.PhoneNumber,
                AveragePrice = item.AveragePrice,
                PriceLevel = item.PriceLevel,
                Rating = item.Rating,
                Tags = item.Tags,
                ImageUrl = item.ImageUrl,
                ImageUrls = item.ImageUrls,
                TriggerRadius = item.TriggerRadius ?? 50,
                ZoneType = item.ZoneType ?? "Spot",
                ZoneLevel = item.ZoneLevel ?? 3,
                Priority = item.Priority ?? 5,
                FunFact = item.FunFact,
                IsActive = item.IsActive ?? true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        if (pois.Count > 0)
            await db.POIs.InsertManyAsync(pois);

        Console.WriteLine($"✅ Seeded {pois.Count} POIs từ Phố Ẩm thực Vĩnh Khánh.");
    }

    // ─────────────────────────────────────────────────────────────
    // SEED VENDORS (mỗi quán = 1 vendor user + vendor profile)
    // ─────────────────────────────────────────────────────────────
    private static async Task SeedVendors(IServiceProvider serviceProvider, UserManager<ApplicationUser> userManager)
    {
        var db = serviceProvider.GetRequiredService<MongoDbContext>();
        var sequence = serviceProvider.GetRequiredService<MongoSequenceService>();

        var existingVendors = await db.VendorProfiles.CountDocumentsAsync(Builders<VendorProfile>.Filter.Empty);
        if (existingVendors > 0) return;

        // Danh sách vendor: (email, fullName, businessName, address, phone, poi_ids[])
        // POI_ID tương ứng với thứ tự seed (bắt đầu từ 1)
        // POI 1 = Cổng chào (không có vendor)
        var vendors = new List<(string email, string fullName, string businessName, string address, string phone, int poiId)>
        {
            ("ocvu@streetfood.vn",        "Chủ quán Ốc Vũ",               "Ốc Vũ",                         "37 Đ. Vĩnh Khánh, P.8, Q.4, TP.HCM",    "+84 908 935 592", 2),
            ("octhao@streetfood.vn",      "Chủ quán Ốc Thảo",             "Ốc Thảo",                       "383 Đ. Vĩnh Khánh, P.8, Q.4, TP.HCM",   "+84 932 117 078", 3),
            ("ocsaunor@streetfood.vn",    "Chủ quán Ốc Sáu Nở",           "Ốc Sáu Nở",                    "128 Đ. Vĩnh Khánh, P.8, Q.4, TP.HCM",   "+84 908 355 999", 4),
            ("ocoanh@streetfood.vn",      "Chủ quán Ốc Oanh",             "Ốc Oanh (Michelin Bib Gourmand)","534 Đ. Vĩnh Khánh, P.8, Q.4, TP.HCM",   "+84 937 916 159", 5),
            ("afat@streetfood.vn",        "Chủ quán A Fat Hot Pot",       "A Fat Hot Pot",                 "668 Đ. Vĩnh Khánh, P.8, Q.4, TP.HCM",   "+84 903 070 002", 6),
            ("chilli@streetfood.vn",      "Chủ quán Chilli",              "Chilli Lẩu Nướng Tự Chọn",     "232 Đ. Vĩnh Khánh, P.8, Q.4, TP.HCM",   "+84 902 935 667", 7),
            ("comchay@streetfood.vn",     "Chủ quán Cơm Cháy",           "Cơm Cháy Kho Quẹt",            "944 Đ. Vĩnh Khánh, P.8, Q.4, TP.HCM",   "+84 888 488 333", 8),
            ("bonephuong@streetfood.vn",  "Chủ quán Bò Né Phượng",       "Bò Né Phượng",                  "712 Đ. Vĩnh Khánh, P.8, Q.4, TP.HCM",   "+84 352 499 883", 9),
            ("lang@streetfood.vn",        "Chủ quán Lãng Restaurant",    "Lãng Restaurant",               "424 Đ. Vĩnh Khánh, P.8, Q.4, TP.HCM",   "+84 888 833 111", 10),
            ("ocdem@streetfood.vn",       "Chủ quán Ốc Đêm Vĩnh Khánh", "Ốc Đêm Vĩnh Khánh",           "474 Đ. Vĩnh Khánh, P.8, Q.4, TP.HCM",   "+84 397 190 292", 11),
            ("ahien@streetfood.vn",       "Chủ quán A Hiền",             "Quán ăn A Hiền",                "74 Đ. Vĩnh Khánh, P.8, Q.4, TP.HCM",    "+84 394 444 177", 12),
            ("ocphat@streetfood.vn",      "Chủ quán Ốc Phát",            "Ốc Phát",                       "361 Đ. Vĩnh Khánh, P.8, Q.4, TP.HCM",   "+84 906 039 288", 13),
            ("shaokao@streetfood.vn",     "Chủ quán SHAOKAO",            "SHAOKAO Trung-Việt Vĩnh Khánh","424/B Đ. Vĩnh Khánh, P.8, Q.4, TP.HCM", "+84 788 792 556", 14),
        };

        int createdCount = 0;
        foreach (var (email, fullName, businessName, address, phone, poiId) in vendors)
        {
            // Tạo user với role Vendor
            var existingUser = await userManager.FindByEmailAsync(email);
            if (existingUser != null) continue;

            var vendor = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FullName = fullName,
                EmailConfirmed = true,
                CreatedAt = DateTime.UtcNow
            };

            var result = await userManager.CreateAsync(vendor, "Vendor@123");
            if (!result.Succeeded)
            {
                Console.WriteLine($"❌ Không tạo được user {email}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                continue;
            }

            await userManager.AddToRoleAsync(vendor, "Vendor");

            // Tạo VendorProfile
            var nextVendorId = await sequence.GetNextAsync("vendor_id");
            var profile = new VendorProfile
            {
                VendorId = nextVendorId,
                UserId = vendor.Id.ToString(),
                ContactName = fullName,
                ContactEmail = email,
                BusinessName = businessName,
                ContactPhone = phone,
                Address = address,
                IsVerified = false,
                VerificationStatus = "pending",
                CreatedAt = DateTime.UtcNow
            };

            await db.VendorProfiles.InsertOneAsync(profile);

            // Gắn VendorId vào POI tương ứng
            var poiFilter = Builders<POI>.Filter.Eq(p => p.POI_ID, poiId);
            var poiUpdate = Builders<POI>.Update.Set(p => p.VendorId, nextVendorId);
            await db.POIs.UpdateOneAsync(poiFilter, poiUpdate);

            createdCount++;
        }

        Console.WriteLine($"✅ Seeded {createdCount} vendors (tất cả status: pending, password: Vendor@123).");
    }

    /// <summary>
    /// One-off utility: link existing POIs to VendorProfiles using the seed email → POI_ID mapping.
    /// Useful when vendors already exist but POIs were imported separately.
    /// </summary>
    public static async Task LinkExistingPoisToVendors(IServiceProvider serviceProvider)
    {
        var db = serviceProvider.GetRequiredService<MongoDbContext>();

        var mappings = new List<(string Email, int PoiId)>
        {
            ("ocvu@streetfood.vn",       2),
            ("octhao@streetfood.vn",     3),
            ("ocsaunor@streetfood.vn",   4),
            ("ocoanh@streetfood.vn",     5),
            ("afat@streetfood.vn",       6),
            ("chilli@streetfood.vn",     7),
            ("comchay@streetfood.vn",    8),
            ("bonephuong@streetfood.vn", 9),
            ("lang@streetfood.vn",       10),
            ("ocdem@streetfood.vn",      11),
            ("ahien@streetfood.vn",      12),
            ("ocphat@streetfood.vn",     13),
            ("shaokao@streetfood.vn",    14),
        };

        var updated = 0;
        var missingVendors = new List<string>();
        var missingPois = new List<int>();

        foreach (var (email, poiId) in mappings)
        {
            var vendor = await db.VendorProfiles.Find(v => v.ContactEmail == email).FirstOrDefaultAsync();
            if (vendor == null)
            {
                missingVendors.Add(email);
                continue;
            }

            var result = await db.POIs.UpdateOneAsync(
                p => p.POI_ID == poiId,
                Builders<POI>.Update.Set(p => p.VendorId, vendor.VendorId));

            if (result.MatchedCount == 0)
            {
                missingPois.Add(poiId);
                continue;
            }

            if (result.ModifiedCount > 0) updated++;
        }

        Console.WriteLine($"✅ Linked POIs: updated={updated}");
        if (missingVendors.Count > 0)
            Console.WriteLine($"⚠️ Missing vendors: {string.Join(", ", missingVendors)}");
        if (missingPois.Count > 0)
            Console.WriteLine($"⚠️ Missing POIs: {string.Join(", ", missingPois)}");
    }

    // ─────────────────────────────────────────────────────────────
    // SEED TOURS (3 tours theo nhóm đặc điểm)
    // ─────────────────────────────────────────────────────────────
    private static async Task SeedMenuItems(IServiceProvider serviceProvider)
    {
        var db = serviceProvider.GetRequiredService<MongoDbContext>();
        var sequence = serviceProvider.GetRequiredService<MongoSequenceService>();

        var existingPoiIds = await db.MenuItems
            .Find(m => !m.IsDeleted)
            .Project(m => m.POI_ID)
            .ToListAsync();
        var existingPoiSet = existingPoiIds.ToHashSet();

        var pois = await db.POIs
            .Find(p => p.IsActive && p.DeletedAt == null && p.ZoneType == "Spot")
            .SortBy(p => p.POI_ID)
            .ToListAsync();

        if (pois.Count == 0)
            return;

        var now = DateTime.UtcNow;
        var items = new List<MenuItem>();

        foreach (var poi in pois)
        {
            if (existingPoiSet.Contains(poi.POI_ID))
                continue;

            var dishNames = (poi.SignatureDishes ?? new List<string>())
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(4)
                .ToList();

            if (dishNames.Count == 0)
            {
                dishNames = new List<string>
                {
                    "Mon dac trung",
                    "Mon ban chay",
                    "Mon duoc yeu thich"
                };
            }

            var basePrice = poi.AveragePrice.GetValueOrDefault(45000m);
            if (basePrice < 10000m)
                basePrice = 10000m;

            for (var i = 0; i < dishNames.Count; i++)
            {
                var itemName = dishNames[i];
                items.Add(new MenuItem
                {
                    MenuItemId = await sequence.GetNextAsync("menu_item_id"),
                    POI_ID = poi.POI_ID,
                    VendorId = poi.VendorId ?? 0,
                    Name_Vi = itemName,
                    Name_En = itemName,
                    Name_Zh = itemName,
                    Description_Vi = poi.Description_Vi,
                    Description_En = poi.Description_En ?? poi.Description_Vi,
                    Description_Zh = poi.Description_Zh ?? poi.Description_Vi,
                    ImageUrl = poi.ImageUrl,
                    ImageUrls = poi.ImageUrls,
                    Price = basePrice + (i * 10000m),
                    PriceUnit = "VND",
                    Category = poi.Category,
                    Tags = poi.Tags,
                    IsSignatureDish = i == 0,
                    IsAvailable = true,
                    SortOrder = i,
                    IsDeleted = false,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }
        }

        if (items.Count > 0)
            await db.MenuItems.InsertManyAsync(items);

        Console.WriteLine($"Seeded {items.Count} menu items.");
    }

    private static async Task SeedTours(IServiceProvider serviceProvider)
    {
        var db = serviceProvider.GetRequiredService<MongoDbContext>();
        var sequence = serviceProvider.GetRequiredService<MongoSequenceService>();

        var existing = await db.Tours.CountDocumentsAsync(Builders<Tour>.Filter.Empty);
        if (existing > 0) return;

        // ── Tour 1: Ốc Huyền Thoại ──────────────────────────────
        // Các quán ốc nổi tiếng nhất phố (đầu phố – đuôi phố)
        // POI: Ốc Vũ(2), Ốc Sáu Nở(4), Ốc Oanh(5), Ốc Phát(13)
        var tourOcHuyenThoai = new Tour
        {
            Tour_ID = await sequence.GetNextAsync("tour_id"),
            TourName = "Tour Ốc Huyền Thoại",
            Description = "Hành trình khám phá 4 quán ốc danh tiếng nhất Phố Vĩnh Khánh: từ Ốc Vũ bình dân đến Ốc Oanh được Michelin vinh danh. Trải nghiệm sự đa dạng trong văn hóa ăn ốc vỉa hè Sài Gòn qua những con ốc tươi, nước chấm đặc trưng và không khí đường phố sôi động.",
            EstimatedDurationMinutes = 90,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        await db.Tours.InsertOneAsync(tourOcHuyenThoai);

        var poiTourOcHuyenThoai = new List<POI_Tour>
        {
            new() { POI_ID = 2,  Tour_ID = tourOcHuyenThoai.Tour_ID, OrderIndex = 1 }, // Ốc Vũ
            new() { POI_ID = 4,  Tour_ID = tourOcHuyenThoai.Tour_ID, OrderIndex = 2 }, // Ốc Sáu Nở
            new() { POI_ID = 5,  Tour_ID = tourOcHuyenThoai.Tour_ID, OrderIndex = 3 }, // Ốc Oanh (Michelin)
            new() { POI_ID = 13, Tour_ID = tourOcHuyenThoai.Tour_ID, OrderIndex = 4 }, // Ốc Phát
        };
        await db.POITours.InsertManyAsync(poiTourOcHuyenThoai);

        // ── Tour 2: Lẩu & Nướng Vĩnh Khánh ─────────────────────
        // Các quán chuyên lẩu nướng độc đáo
        // POI: Chilli(7), A Fat Hot Pot(6), SHAOKAO(14), Lãng Restaurant(10)
        var tourLauNuong = new Tour
        {
            Tour_ID = await sequence.GetNextAsync("tour_id"),
            TourName = "Tour Lẩu & Nướng Đặc Sắc",
            Description = "Khám phá thế giới lẩu và nướng đa văn hóa trên Phố Vĩnh Khánh: từ BBQ ngoài trời phong cách trẻ của Chilli, lẩu Hong Kong retro của A Fat, đến nướng Trung-Việt của SHAOKAO và nhạc sống tại Lãng Restaurant. Mỗi quán mang một hơi thở ẩm thực riêng biệt.",
            EstimatedDurationMinutes = 120,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        await db.Tours.InsertOneAsync(tourLauNuong);

        var poiTourLauNuong = new List<POI_Tour>
        {
            new() { POI_ID = 7,  Tour_ID = tourLauNuong.Tour_ID, OrderIndex = 1 }, // Chilli BBQ
            new() { POI_ID = 6,  Tour_ID = tourLauNuong.Tour_ID, OrderIndex = 2 }, // A Fat Hot Pot
            new() { POI_ID = 14, Tour_ID = tourLauNuong.Tour_ID, OrderIndex = 3 }, // SHAOKAO
            new() { POI_ID = 10, Tour_ID = tourLauNuong.Tour_ID, OrderIndex = 4 }, // Lãng Restaurant
        };
        await db.POITours.InsertManyAsync(poiTourLauNuong);

        // ── Tour 3: Ẩm Thực Về Khuya ────────────────────────────
        // Các quán mở cửa khuya, dành cho "cú đêm"
        // POI: Ốc Thảo(3, 24/7), Ốc Đêm Vĩnh Khánh(11), Bò Né Phượng(9), Cơm Cháy(8)
        var tourVeKhuya = new Tour
        {
            Tour_ID = await sequence.GetNextAsync("tour_id"),
            TourName = "Tour Ẩm Thực Về Khuya",
            Description = "Dành cho những \"cú đêm\" thực thụ! Hành trình này đưa bạn qua các quán mở cửa từ khuya đến sáng: Ốc Thảo 24/7, Ốc Đêm Vĩnh Khánh náo nhiệt, Bò Né Phượng chảo gang nóng hổi, và Cơm Cháy Kho Quẹt dân dã. Sài Gòn không ngủ – và bạn cũng vậy.",
            EstimatedDurationMinutes = 90,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        await db.Tours.InsertOneAsync(tourVeKhuya);

        var poiTourVeKhuya = new List<POI_Tour>
        {
            new() { POI_ID = 3,  Tour_ID = tourVeKhuya.Tour_ID, OrderIndex = 1 }, // Ốc Thảo (24/7)
            new() { POI_ID = 11, Tour_ID = tourVeKhuya.Tour_ID, OrderIndex = 2 }, // Ốc Đêm Vĩnh Khánh
            new() { POI_ID = 9,  Tour_ID = tourVeKhuya.Tour_ID, OrderIndex = 3 }, // Bò Né Phượng
            new() { POI_ID = 8,  Tour_ID = tourVeKhuya.Tour_ID, OrderIndex = 4 }, // Cơm Cháy Kho Quẹt
        };
        await db.POITours.InsertManyAsync(poiTourVeKhuya);

        Console.WriteLine("✅ Seeded 3 tours: 'Tour Ốc Huyền Thoại', 'Tour Lẩu & Nướng Đặc Sắc', 'Tour Ẩm Thực Về Khuya'.");
    }

    // ─────────────────────────────────────────────────────────────
    // SEED ANALYTICS DATA (for history.html)
    // ─────────────────────────────────────────────────────────────
    private static async Task SeedAnalyticsData(IServiceProvider serviceProvider, UserManager<ApplicationUser> userManager)
    {
        var db = serviceProvider.GetRequiredService<MongoDbContext>();
        var sequence = serviceProvider.GetRequiredService<MongoSequenceService>();

        var now = DateTime.UtcNow;
        var rng = new Random(20260314);

        var pois = await db.POIs
            .Find(p => p.DeletedAt == null && p.IsActive)
            .SortBy(p => p.POI_ID)
            .ToListAsync();

        if (pois.Count == 0)
        {
            Console.WriteLine("⚠️ Skip analytics seed: no active POIs.");
            return;
        }

        var devices = await SeedDevicesIfEmpty(db, sequence, now, rng);
        await SeedNarrationLogsIfEmpty(db, sequence, userManager, pois, devices, now, rng);
    }

    private static async Task<List<DeviceInfo>> SeedDevicesIfEmpty(
        MongoDbContext db,
        MongoSequenceService sequence,
        DateTime now,
        Random rng)
    {
        var existingDevices = await db.Devices.CountDocumentsAsync(Builders<DeviceInfo>.Filter.Empty);
        if (existingDevices > 0)
        {
            return await db.Devices
                .Find(Builders<DeviceInfo>.Filter.Empty)
                .SortByDescending(d => d.LastSeen)
                .Limit(50)
                .ToListAsync();
        }

        var templates = new List<(string platform, string model, string osVersion, string appVersion, string lang)>
        {
            ("iOS", "iPhone 15 Pro", "17.4", "1.2.0", "vi"),
            ("iOS", "iPhone 13", "16.7", "1.1.8", "en"),
            ("iOS", "iPhone 12", "16.6", "1.1.5", "vi"),
            ("iOS", "iPad Air 5", "17.2", "1.2.1", "zh"),
            ("Android", "Samsung Galaxy S23", "Android 14", "1.2.0", "vi"),
            ("Android", "Google Pixel 8", "Android 14", "1.1.9", "en"),
            ("Android", "Xiaomi 13T", "Android 14", "1.1.7", "vi"),
            ("Android", "OPPO Reno11", "Android 14", "1.1.8", "vi"),
            ("Android", "Vivo V30", "Android 14", "1.1.6", "en"),
            ("Android", "Samsung Galaxy A54", "Android 13", "1.1.4", "vi")
        };

        var devicesToInsert = new List<DeviceInfo>();
        for (var i = 0; i < templates.Count; i++)
        {
            var t = templates[i];
            var firstSeen = now.AddDays(-rng.Next(15, 120)).AddHours(-rng.Next(0, 23));
            var lastSeen = now.AddHours(-rng.Next(0, 200));
            if (lastSeen < firstSeen)
            {
                lastSeen = firstSeen.AddHours(rng.Next(1, 240));
            }

            var device = new DeviceInfo
            {
                Device_ID = await sequence.GetNextAsync("Device_ID"),
                DeviceId = $"sfn-{t.platform.ToLowerInvariant()}-{Guid.NewGuid().ToString("N")[..12]}",
                Platform = t.platform,
                Model = t.model,
                OsVersion = t.osVersion,
                AppVersion = t.appVersion,
                PreferredLanguage = t.lang,
                FirstSeen = firstSeen,
                LastSeen = lastSeen,
                TotalSessions = rng.Next(4, 52),
                TotalPOIsViewed = rng.Next(12, 220),
                TotalAudioPlayed = rng.Next(8, 180)
            };

            devicesToInsert.Add(device);
        }

        await db.Devices.InsertManyAsync(devicesToInsert);
        Console.WriteLine($"✅ Seeded {devicesToInsert.Count} devices for analytics.");
        return devicesToInsert;
    }

    private static async Task SeedNarrationLogsIfEmpty(
        MongoDbContext db,
        MongoSequenceService sequence,
        UserManager<ApplicationUser> userManager,
        List<POI> pois,
        List<DeviceInfo> devices,
        DateTime now,
        Random rng)
    {
        var existingLogs = await db.NarrationLogs.CountDocumentsAsync(Builders<NarrationLog>.Filter.Empty);
        if (existingLogs > 0) return;

        var logs = new List<NarrationLog>();
        var triggerTypes = new[] { "Auto", "Manual", "Proximity" };
        var userIds = userManager.Users.Select(u => u.Id.ToString()).Take(20).ToList();

        var totalLogs = Math.Clamp((devices.Count > 0 ? devices.Count * 18 : 120), 120, 300);
        for (var i = 0; i < totalLogs; i++)
        {
            var poi = pois[rng.Next(pois.Count)];
            var poiLat = poi.Location?.Latitude ?? 10.762;
            var poiLon = poi.Location?.Longitude ?? 106.682;

            var minutesAgo = rng.Next(20, 60 * 24 * 21); // ~21 days
            var triggeredAt = now.AddMinutes(-minutesAgo);

            var hasUserId = userIds.Count > 0 && rng.NextDouble() > 0.28;
            var userId = hasUserId ? userIds[rng.Next(userIds.Count)] : null;

            var userLat = poiLat + (rng.NextDouble() - 0.5) * 0.0012;
            var userLon = poiLon + (rng.NextDouble() - 0.5) * 0.0012;

            logs.Add(new NarrationLog
            {
                Log_ID = await sequence.GetNextAsync("Log_ID"),
                POI_ID = poi.POI_ID,
                UserId = userId,
                TriggeredAt = triggeredAt,
                TriggerType = triggerTypes[rng.Next(triggerTypes.Length)],
                UserLatitude = (decimal)userLat,
                UserLongitude = (decimal)userLon,
                WasPlayed = rng.NextDouble() > 0.18
            });
        }

        if (logs.Count > 0)
        {
            await db.NarrationLogs.InsertManyAsync(logs.OrderByDescending(l => l.TriggeredAt));
            Console.WriteLine($"✅ Seeded {logs.Count} narration logs for analytics history.");
        }
    }
}

// ─────────────────────────────────────────────────────────────────
// MODEL cho seed JSON
// ─────────────────────────────────────────────────────────────────
public class SeedPoi
{
    public string? NameVi { get; set; }
    public string? NameEn { get; set; }
    public string? NameJa { get; set; }
    public string? NameFr { get; set; }
    public string? NameKo { get; set; }
    public string? NameZh { get; set; }
    public string? DescriptionVi { get; set; }
    public string? DescriptionEn { get; set; }
    public string? DescriptionJa { get; set; }
    public string? DescriptionFr { get; set; }
    public string? DescriptionKo { get; set; }
    public string? DescriptionZh { get; set; }
    public string? History { get; set; }
    public string? Story { get; set; }
    public string? Address { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public List<string>? OpeningHours { get; set; }
    public string? OpeningHoursText { get; set; }
    public string? PhoneNumber { get; set; }
    public List<string>? Specialties { get; set; }
    public List<string>? SignatureDishes { get; set; }
    public string? SignatureDish { get; set; }
    public string? FunFact { get; set; }
    public string? ImageUrl { get; set; }
    public List<string>? ImageUrls { get; set; }
    public double? Rating { get; set; }
    public int? PriceLevel { get; set; }
    public List<string>? Tags { get; set; }
    public string? Category { get; set; }
    public decimal? AveragePrice { get; set; }
    public bool? IsActive { get; set; }
    public int? TriggerRadius { get; set; }
    public string? ZoneType { get; set; }
    public int? ZoneLevel { get; set; }
    public int? Priority { get; set; }
}
