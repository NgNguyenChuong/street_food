using MongoDB.Driver;
using StreetFoodNarrator.API.Models;

namespace StreetFoodNarrator.API.Data;

public class MongoDbContext
{
    public IMongoDatabase Database { get; }

    public MongoDbContext(IMongoClient client, MongoDbSettings settings)
    {
        Database = client.GetDatabase(settings.DatabaseName);
    }

    public IMongoCollection<POI> POIs => Database.GetCollection<POI>("pois");
    public IMongoCollection<AudioContent> AudioContents => Database.GetCollection<AudioContent>("audio_contents");
    public IMongoCollection<Tour> Tours => Database.GetCollection<Tour>("tours");
    public IMongoCollection<POI_Tour> POITours => Database.GetCollection<POI_Tour>("poi_tours");
    public IMongoCollection<NarrationLog> NarrationLogs => Database.GetCollection<NarrationLog>("narration_logs");
    public IMongoCollection<VendorProfile> VendorProfiles => Database.GetCollection<VendorProfile>("vendor_profiles");
    public IMongoCollection<UserSettings> UserSettings => Database.GetCollection<UserSettings>("user_settings");
}
