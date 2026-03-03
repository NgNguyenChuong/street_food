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

        // Create roles if they don't exist
        string[] roleNames = { "Admin", "Vendor", "User" };
        
        foreach (var roleName in roleNames)
        {
            var roleExist = await roleManager.RoleExistsAsync(roleName);
            if (!roleExist)
            {
                await roleManager.CreateAsync(new ApplicationRole { Name = roleName });
            }
        }

        // Create default admin user if no users exist
        if (!userManager.Users.Any())
        {
            var adminUser = new ApplicationUser
            {
                UserName = "admin@streetfood.vn",
                Email = "admin@streetfood.vn",
                FullName = "Administrator",
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
    }

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
                Description_Vi = item.DescriptionVi ?? string.Empty,
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
                IsActive = item.IsActive ?? true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        if (pois.Count > 0)
        {
            await db.POIs.InsertManyAsync(pois);
        }
    }
}

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
    public string? ImageUrl { get; set; }
    public List<string>? ImageUrls { get; set; }
    public double? Rating { get; set; }
    public int? PriceLevel { get; set; }
    public List<string>? Tags { get; set; }
    public string? Category { get; set; }
    public decimal? AveragePrice { get; set; }
    public bool? IsActive { get; set; }
    public int? TriggerRadius { get; set; }
}
