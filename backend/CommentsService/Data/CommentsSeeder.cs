using CommentsService.Models;
using MongoDB.Driver;

namespace CommentsService.Data;

public static class CommentsSeeder
{
    public static async Task SeedAsync(MongoDbContext db)
    {
        var count = await db.HotelReviews.CountDocumentsAsync(FilterDefinition<HotelReview>.Empty);
        if (count > 0) return;

        var documents = BuildSeedDocuments();
        await db.HotelReviews.InsertManyAsync(documents);
    }

    private static List<HotelReview> BuildSeedDocuments()
    {
        return
        [
            Build("11111111-0000-0000-0000-000000000001",
            [
                Review("user-001", "Ahmet Y.", "Spectacular Bosphorus views from every room. Staff were incredibly attentive and the breakfast spread was world-class.", 9.8, 9.8, 9.9, 9.5, 9.8, 8.8, "4 nights"),
                Review("user-002", "Sophie M.", "One of the finest hotels I've stayed at. The pool area overlooking the strait is breathtaking.", 9.4, 9.6, 9.8, 9.3, 9.5, 8.5, "3 nights"),
                Review("user-003", "Kemal D.", "Rooms are spacious and immaculately clean. Check-in was seamless. Will definitely return.", 8.8, 9.0, 8.7, 8.5, 9.2, 8.2, "2 nights"),
            ]),

            Build("11111111-0000-0000-0000-000000000002",
            [
                Review("user-004", "Lena K.", "Great location in Bomonti, walking distance from Taksim. Modern rooms with great city views.", 9.2, 9.4, 9.6, 8.9, 9.0, 7.8, "5 nights"),
                Review("user-005", "Marco R.", "Excellent business hotel. Meeting facilities are top-notch and the executive lounge is outstanding.", 9.0, 8.8, 9.4, 9.2, 8.6, 7.5, "3 nights"),
                Review("user-006", "Zeynep A.", "Comfortable stay overall. The rooftop bar has amazing views. Room service was a bit slow.", 8.2, 7.8, 8.4, 8.0, 8.8, 8.0, "2 nights"),
            ]),

            Build("11111111-0000-0000-0000-000000000003",
            [
                Review("user-007", "Thomas B.", "Stunning property right on the Aegean coast. The infinity pool is absolutely gorgeous.", 9.6, 9.8, 9.6, 9.4, 9.7, 9.0, "6 nights"),
                Review("user-008", "Yuki T.", "Immaculate rooms with sea views. The spa treatments were heavenly. Highly recommended.", 9.4, 9.6, 9.8, 9.2, 9.4, 8.8, "4 nights"),
                Review("user-009", "Fatma S.", "Perfect base for exploring Izmir. Friendly staff who helped with local tours.", 8.8, 8.6, 9.0, 8.4, 9.2, 8.4, "3 nights"),
            ]),

            Build("11111111-0000-0000-0000-000000000004",
            [
                Review("user-010", "Elena V.", "This adult-only paradise exceeded every expectation. Privacy, luxury, and stunning Aegean vistas.", 9.9, 9.9, 9.9, 9.8, 9.9, 9.5, "7 nights"),
                Review("user-011", "James H.", "The design is extraordinary — feels like a boutique art hotel. Food quality is superb.", 9.7, 9.8, 9.7, 9.5, 9.7, 9.2, "5 nights"),
                Review("user-012", "Mila P.", "Absolutely loved every moment. The private beach is pristine. Expensive but worth every penny.", 9.5, 9.6, 9.8, 9.3, 9.6, 9.0, "4 nights"),
            ]),

            Build("11111111-0000-0000-0000-000000000005",
            [
                Review("user-013", "Carlos F.", "Yalıkavak marina location is unbeatable. The hotel oozes sophistication.", 9.3, 9.4, 9.0, 9.4, 8.8, 8.6, "5 nights"),
                Review("user-014", "Anna S.", "Beautiful boutique hotel with superb personalised service. The sunset views are magical.", 9.6, 9.8, 9.4, 9.2, 9.4, 9.0, "4 nights"),
                Review("user-015", "Berk Ö.", "Loved the restaurant — fresh Aegean seafood every night. Rooms are chic and well-appointed.", 9.0, 9.2, 9.4, 8.8, 9.2, 8.4, "3 nights"),
            ]),

            Build("11111111-0000-0000-0000-000000000006",
            [
                Review("user-016", "Diana M.", "The golf course is impeccable and the spa facilities are among the best in Antalya.", 9.4, 9.6, 9.2, 9.4, 9.0, 8.8, "6 nights"),
                Review("user-017", "Paul N.", "Family had a wonderful time. Kids club was excellent and the buffet selection is enormous.", 8.8, 8.4, 9.0, 8.6, 9.2, 8.2, "7 nights"),
                Review("user-018", "Selin Ç.", "Ultra-luxurious resort with beautiful gardens. Every staff member went above and beyond.", 9.2, 9.0, 9.4, 9.2, 9.4, 8.6, "5 nights"),
            ]),

            Build("11111111-0000-0000-0000-000000000007",
            [
                Review("user-019", "Oliver W.", "Rixos Belek is quintessential all-inclusive luxury. The beach is perfect white sand.", 9.7, 9.8, 9.4, 9.6, 9.2, 9.0, "8 nights"),
                Review("user-020", "Hana K.", "Outstanding entertainment programme every night. Aquapark is fantastic for the whole family.", 9.2, 9.0, 9.6, 8.8, 9.4, 8.8, "7 nights"),
                Review("user-021", "Mustafa E.", "Food quality across all restaurants is consistently excellent. Rooms are spacious and modern.", 9.4, 9.6, 9.2, 9.4, 9.0, 8.6, "6 nights"),
            ]),
        ];
    }

    private static HotelReview Build(string hotelId, List<ReviewEntry> reviews)
    {
        return new HotelReview
        {
            HotelId = hotelId,
            Reviews = reviews,
            TotalReviews = reviews.Count,
            OverallScore = Math.Round(reviews.Average(r => r.Rating), 2),
            CategoryScores = new CategoryScores
            {
                Cleanliness = Math.Round(reviews.Average(r => r.CategoryRatings.Cleanliness), 2),
                Staff = Math.Round(reviews.Average(r => r.CategoryRatings.Staff), 2),
                Facilities = Math.Round(reviews.Average(r => r.CategoryRatings.Facilities), 2),
                LocationCondition = Math.Round(reviews.Average(r => r.CategoryRatings.LocationCondition), 2),
                EcoFriendly = Math.Round(reviews.Average(r => r.CategoryRatings.EcoFriendly), 2),
            }
        };
    }

    private static ReviewEntry Review(
        string userId, string author, string text,
        double rating, double cleanliness, double staff, double facilities, double location, double eco,
        string tripType)
    {
        return new ReviewEntry
        {
            ReviewId = Guid.NewGuid().ToString(),
            UserId = userId,
            Author = author,
            Text = text,
            Rating = rating,
            CategoryRatings = new CategoryScores
            {
                Cleanliness = cleanliness,
                Staff = staff,
                Facilities = facilities,
                LocationCondition = location,
                EcoFriendly = eco
            },
            TripType = tripType,
            IsVerified = true,
            Date = DateTime.UtcNow.AddDays(-Random.Shared.Next(1, 90))
        };
    }
}
