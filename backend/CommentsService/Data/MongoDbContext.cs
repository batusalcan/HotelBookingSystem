using CommentsService.Models;
using MongoDB.Driver;

namespace CommentsService.Data;

public class MongoDbContext
{
    private readonly IMongoDatabase _database;
    private readonly string _collectionName;

    public MongoDbContext(IMongoClient client, IConfiguration config)
    {
        _database = client.GetDatabase(config["MongoDB:DatabaseName"] ?? "HotelBookingDb");
        _collectionName = config["MongoDB:CollectionName"] ?? "hotelReviews";
    }

    public IMongoCollection<HotelReview> HotelReviews =>
        _database.GetCollection<HotelReview>(_collectionName);
}
