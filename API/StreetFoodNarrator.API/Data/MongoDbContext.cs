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
    public IMongoCollection<Translation> Translations => Database.GetCollection<Translation>("translations");
    public IMongoCollection<DeviceInfo> Devices => Database.GetCollection<DeviceInfo>("devices");
    public IMongoCollection<Review> Reviews => Database.GetCollection<Review>("reviews");
    public IMongoCollection<DeviceSubscription> DeviceSubscriptions => Database.GetCollection<DeviceSubscription>("device_subscriptions");

    // Thêm các collections mới từ schema update
    public IMongoCollection<Zone> Zones => Database.GetCollection<Zone>("zones");
    public IMongoCollection<MenuItem> MenuItems => Database.GetCollection<MenuItem>("menu_items");
    public IMongoCollection<PoiTranslation> PoiTranslations => Database.GetCollection<PoiTranslation>("poi_translations");
    public IMongoCollection<DeviceHistory> DeviceHistories => Database.GetCollection<DeviceHistory>("device_history");
    public IMongoCollection<SubmissionIdempotency> SubmissionIdempotencies => Database.GetCollection<SubmissionIdempotency>("submission_idempotency");
    public IMongoCollection<ServiceSubmission> ServiceSubmissions => Database.GetCollection<ServiceSubmission>("submissions");
    public IMongoCollection<QrCampaignState> QrCampaignStates => Database.GetCollection<QrCampaignState>("qr_campaign_states");
}
