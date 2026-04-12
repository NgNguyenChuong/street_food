using System.Text.Json;
using MongoDB.Driver;
using StreetFoodNarrator.API.Models;

namespace StreetFoodNarrator.API.Data;

public static class PoiSeedReset
{
    public static async Task RunAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<MongoDbContext>();
        var sequence = services.GetRequiredService<MongoSequenceService>();
        var env = services.GetRequiredService<IHostEnvironment>();

        var seedPath = Path.Combine(env.ContentRootPath, "Data", "seed", "vinh-khanh-pois.json");
        if (!File.Exists(seedPath))
        {
            throw new FileNotFoundException($"Seed file not found: {seedPath}");
        }

        var json = await File.ReadAllTextAsync(seedPath);
        var items = JsonSerializer.Deserialize<List<SeedPoi>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new List<SeedPoi>();

        if (items.Count == 0)
        {
            throw new InvalidOperationException("Seed file is empty or invalid.");
        }

        // Remove all existing POIs and Reviews
        await db.POIs.DeleteManyAsync(_ => true);
        await db.Reviews.DeleteManyAsync(_ => true);

        var now = DateTime.UtcNow;
        var pois = new List<POI>();
        var reviews = new List<Review>();

        var nextId = 1;
        foreach (var item in items)
        {
            pois.Add(new POI
            {
                POI_ID = nextId++,
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
                NumReviews = 2, // Seeding 2 mock reviews
                Tags = item.Tags,
                ImageUrl = item.ImageUrl,
                ImageUrls = item.ImageUrls,
                TriggerRadius = item.TriggerRadius ?? 50,
                ZoneType = item.ZoneType ?? "Spot",
                ZoneLevel = item.ZoneLevel ?? 3,
                Priority = item.Priority ?? 5,
                IsActive = item.IsActive ?? true,
                CreatedAt = now,
                UpdatedAt = now
            });
            
            // Add two mock reviews
            reviews.Add(new Review
            {
                POI_ID = nextId - 1,
                UserName = "Android SM-G998B",
                Rating = 5,
                Comment = "Quán ăn rất ngon, ốc tươi và gia vị đậm đà. Mình sẽ ghé lại sớm!",
                CreatedAt = now.AddDays(-2)
            });

            reviews.Add(new Review
            {
                POI_ID = nextId - 1,
                UserName = "iOS iPhone 13",
                Rating = 4,
                Comment = "Món ăn ổn, phục vụ nhanh nhạy nhưng giá hơi cao so với mặt bằng chung.",
                CreatedAt = now.AddDays(-1)
            });
        }

        await db.POIs.InsertManyAsync(pois);
        if (reviews.Any())
        {
            await db.Reviews.InsertManyAsync(reviews);
        }

        await db.Database.GetCollection<SequenceCounter>("counters")
            .UpdateOneAsync(
                c => c.Name == "poi_id",
                Builders<SequenceCounter>.Update.Set(c => c.Value, pois.Count),
                new UpdateOptions { IsUpsert = true });
    }
}
