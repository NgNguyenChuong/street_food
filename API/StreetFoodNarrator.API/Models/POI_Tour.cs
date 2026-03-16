namespace StreetFoodNarrator.API.Models;

public class POI_Tour
{
    public int POI_ID { get; set; }
    [MongoDB.Bson.Serialization.Attributes.BsonIgnore]
    public POI POI { get; set; } = null!;
    
    public int Tour_ID { get; set; }
    [MongoDB.Bson.Serialization.Attributes.BsonIgnore]
    public Tour Tour { get; set; } = null!;
    
    public int OrderIndex { get; set; }
}
