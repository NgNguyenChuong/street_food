using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace StreetFoodNarrator.API.Data;

public class MongoSequenceService
{
    private readonly IMongoCollection<SequenceCounter> _counters;

    public MongoSequenceService(MongoDbContext context)
    {
        _counters = context.Database.GetCollection<SequenceCounter>("counters");
    }

    public async Task<int> GetNextAsync(string name)
    {
        var filter = Builders<SequenceCounter>.Filter.Eq(c => c.Name, name);
        var update = Builders<SequenceCounter>.Update.Inc(c => c.Value, 1);
        var options = new FindOneAndUpdateOptions<SequenceCounter>
        {
            IsUpsert = true,
            ReturnDocument = ReturnDocument.After
        };

        var counter = await _counters.FindOneAndUpdateAsync(filter, update, options);
        return counter.Value;
    }
}

public class SequenceCounter
{
    [BsonId]
    public string Name { get; set; } = string.Empty;

    public int Value { get; set; }
}
