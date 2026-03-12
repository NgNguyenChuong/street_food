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
        await SeedTours(serviceProvider);
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

    // ─────────────────────────────────────────────────────────────
    // SEED TOURS (3 tours theo nhóm đặc điểm)
    // ─────────────────────────────────────────────────────────────
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
