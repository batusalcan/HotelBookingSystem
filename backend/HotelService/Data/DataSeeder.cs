using HotelService.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelService.Data;

public static class DataSeeder
{
    public static void Seed(ModelBuilder modelBuilder)
    {
        var hotels = new[]
        {
            new Hotel { HotelId = Guid.Parse("11111111-0000-0000-0000-000000000001"), Name = "Swissôtel The Bosphorus Istanbul", Destination = "Istanbul", Latitude = 41.04350m, Longitude = 29.00810m, BaseRating = 9.2m, TotalReviews = 1284, IsActive = true, ImageUrl = "https://images.unsplash.com/photo-1560347876-aeef00ee58a1?w=800&q=80", Description = "Perched above the Bosphorus Strait with panoramic views of two continents, Swissôtel The Bosphorus combines Swiss precision with Istanbul's timeless elegance. The hotel features award-winning spa facilities, multiple fine-dining restaurants, and a rooftop infinity pool overlooking the historic skyline." },
            new Hotel { HotelId = Guid.Parse("11111111-0000-0000-0000-000000000002"), Name = "Hilton Istanbul Bomonti", Destination = "Istanbul", Latitude = 41.06110m, Longitude = 28.98360m, BaseRating = 8.8m, TotalReviews = 876, IsActive = true, ImageUrl = "https://images.unsplash.com/photo-1542314831-068cd1dbfeeb?w=800&q=80", Description = "Located in Istanbul's vibrant Bomonti district, this modern high-rise hotel offers sweeping city views from its Sky Bar and rooftop pool. With spacious contemporary rooms, a fully equipped fitness center, and direct access to local nightlife, it's the ideal urban retreat." },
            new Hotel { HotelId = Guid.Parse("11111111-0000-0000-0000-000000000003"), Name = "Swissôtel Büyük Efes Izmir", Destination = "Izmir", Latitude = 38.41920m, Longitude = 27.12610m, BaseRating = 9.0m, TotalReviews = 643, IsActive = true, ImageUrl = "https://images.unsplash.com/photo-1520250497591-112f2f40a3f4?w=800&q=80", Description = "Izmir's most iconic luxury hotel, the Büyük Efes sits in the heart of the city overlooking the Gulf of İzmir. The landmark property features beautifully appointed rooms, a grand swimming pool, and is steps away from the lively Kordon waterfront promenade." },
            new Hotel { HotelId = Guid.Parse("11111111-0000-0000-0000-000000000004"), Name = "Hyde Bodrum - Yetişkin Oteli", Destination = "Bodrum", Latitude = 37.03380m, Longitude = 27.43060m, BaseRating = 9.6m, TotalReviews = 163, IsActive = true, ImageUrl = "https://images.unsplash.com/photo-1571896349842-33c89424de2d?w=800&q=80", Description = "An exclusive adults-only retreat set on the crystal-clear waters of the Bodrum Peninsula. Hyde Bodrum offers private beach access, sunset cocktail terraces, and a serene infinity pool, creating a sophisticated escape where style meets the Aegean Sea." },
            new Hotel { HotelId = Guid.Parse("11111111-0000-0000-0000-000000000005"), Name = "MGallery The Bodrum Hotel Yalıkavak", Destination = "Bodrum", Latitude = 37.10430m, Longitude = 27.28170m, BaseRating = 9.4m, TotalReviews = 302, IsActive = true, ImageUrl = "https://images.unsplash.com/photo-1445019980597-93fa8acb246c?w=800&q=80", Description = "Nestled in the exclusive marina of Yalıkavak, this boutique MGallery property captures the charm of traditional Bodrum architecture with world-class amenities. Enjoy private sea access, a tranquil spa, and gourmet dining with views of luxury yachts and the Aegean." },
            new Hotel { HotelId = Guid.Parse("11111111-0000-0000-0000-000000000006"), Name = "Regnum Carya Golf & Spa Resort", Destination = "Antalya", Latitude = 36.85740m, Longitude = 30.75300m, BaseRating = 9.1m, TotalReviews = 529, IsActive = true, ImageUrl = "https://images.unsplash.com/photo-1578683010236-d716f9a3f461?w=800&q=80", Description = "A grand beachfront resort on the Belek Riviera, Regnum Carya combines championship golf courses, a sprawling water park, and an award-winning spa. With multiple pools, private beach, and all-inclusive dining options, this is Antalya's premier family and leisure destination." },
            new Hotel { HotelId = Guid.Parse("11111111-0000-0000-0000-000000000007"), Name = "Rixos Premium Belek", Destination = "Antalya", Latitude = 36.86980m, Longitude = 31.06490m, BaseRating = 9.3m, TotalReviews = 1102, IsActive = true, ImageUrl = "https://images.unsplash.com/photo-1582719508461-905c673771fd?w=800&q=80", Description = "Rixos Premium Belek sets the standard for Turkish Riviera luxury with its extensive Ultra All-Inclusive concept. Set among lush pine forests steps from the Mediterranean, guests enjoy multiple pools, a private beach, a top-tier spa, and over 20 restaurants and bars." },
        };

        var roomTypes = new[]
        {
            // Istanbul Hotel 1
            new RoomType { RoomTypeId = Guid.Parse("22222222-0000-0000-0000-000000000001"), HotelId = Guid.Parse("11111111-0000-0000-0000-000000000001"), TypeName = "Standard", MaxGuests = 2, BasePricePerNight = 4200m },
            new RoomType { RoomTypeId = Guid.Parse("22222222-0000-0000-0000-000000000002"), HotelId = Guid.Parse("11111111-0000-0000-0000-000000000001"), TypeName = "Family", MaxGuests = 4, BasePricePerNight = 7800m },
            // Istanbul Hotel 2
            new RoomType { RoomTypeId = Guid.Parse("22222222-0000-0000-0000-000000000003"), HotelId = Guid.Parse("11111111-0000-0000-0000-000000000002"), TypeName = "Standard", MaxGuests = 2, BasePricePerNight = 3500m },
            new RoomType { RoomTypeId = Guid.Parse("22222222-0000-0000-0000-000000000004"), HotelId = Guid.Parse("11111111-0000-0000-0000-000000000002"), TypeName = "Family", MaxGuests = 4, BasePricePerNight = 6200m },
            // Izmir
            new RoomType { RoomTypeId = Guid.Parse("22222222-0000-0000-0000-000000000005"), HotelId = Guid.Parse("11111111-0000-0000-0000-000000000003"), TypeName = "Standard", MaxGuests = 2, BasePricePerNight = 3100m },
            new RoomType { RoomTypeId = Guid.Parse("22222222-0000-0000-0000-000000000006"), HotelId = Guid.Parse("11111111-0000-0000-0000-000000000003"), TypeName = "Family", MaxGuests = 4, BasePricePerNight = 5800m },
            // Bodrum 1
            new RoomType { RoomTypeId = Guid.Parse("22222222-0000-0000-0000-000000000007"), HotelId = Guid.Parse("11111111-0000-0000-0000-000000000004"), TypeName = "Standard", MaxGuests = 2, BasePricePerNight = 10948m },
            new RoomType { RoomTypeId = Guid.Parse("22222222-0000-0000-0000-000000000008"), HotelId = Guid.Parse("11111111-0000-0000-0000-000000000004"), TypeName = "Family", MaxGuests = 4, BasePricePerNight = 18500m },
            // Bodrum 2
            new RoomType { RoomTypeId = Guid.Parse("22222222-0000-0000-0000-000000000009"), HotelId = Guid.Parse("11111111-0000-0000-0000-000000000005"), TypeName = "Standard", MaxGuests = 2, BasePricePerNight = 9458m },
            new RoomType { RoomTypeId = Guid.Parse("22222222-0000-0000-0000-000000000010"), HotelId = Guid.Parse("11111111-0000-0000-0000-000000000005"), TypeName = "Family", MaxGuests = 4, BasePricePerNight = 15900m },
            // Antalya 1
            new RoomType { RoomTypeId = Guid.Parse("22222222-0000-0000-0000-000000000011"), HotelId = Guid.Parse("11111111-0000-0000-0000-000000000006"), TypeName = "Standard", MaxGuests = 2, BasePricePerNight = 6500m },
            new RoomType { RoomTypeId = Guid.Parse("22222222-0000-0000-0000-000000000012"), HotelId = Guid.Parse("11111111-0000-0000-0000-000000000006"), TypeName = "Family", MaxGuests = 4, BasePricePerNight = 11200m },
            // Antalya 2
            new RoomType { RoomTypeId = Guid.Parse("22222222-0000-0000-0000-000000000013"), HotelId = Guid.Parse("11111111-0000-0000-0000-000000000007"), TypeName = "Standard", MaxGuests = 2, BasePricePerNight = 8200m },
            new RoomType { RoomTypeId = Guid.Parse("22222222-0000-0000-0000-000000000014"), HotelId = Guid.Parse("11111111-0000-0000-0000-000000000007"), TypeName = "Family", MaxGuests = 4, BasePricePerNight = 14600m },
        };

        // Build inventory blocks covering today + 90 days. Each block spans 30 days.
        // Use fixed dates relative to a known reference so migrations are deterministic.
        var refDate = new DateOnly(2026, 5, 10);
        var blocks = new List<InventoryBlock>();
        int blockIndex = 1;

        foreach (var rt in roomTypes)
        {
            // Block 1: next 30 days — healthy stock
            blocks.Add(new InventoryBlock
            {
                InventoryId = Guid.Parse($"33333333-0000-0000-0000-{blockIndex:D12}"),
                RoomTypeId = rt.RoomTypeId,
                StartDate = refDate,
                EndDate = refDate.AddDays(30),
                TotalCount = 10,
                AvailableCount = 10,
                IsAvailable = true
            });
            blockIndex++;

            // Block 2: days 31-60 — low stock (triggers nightly cron alert at <20%)
            blocks.Add(new InventoryBlock
            {
                InventoryId = Guid.Parse($"33333333-0000-0000-0000-{blockIndex:D12}"),
                RoomTypeId = rt.RoomTypeId,
                StartDate = refDate.AddDays(30),
                EndDate = refDate.AddDays(60),
                TotalCount = 10,
                AvailableCount = 1,
                IsAvailable = true
            });
            blockIndex++;

            // Block 3: days 61-90
            blocks.Add(new InventoryBlock
            {
                InventoryId = Guid.Parse($"33333333-0000-0000-0000-{blockIndex:D12}"),
                RoomTypeId = rt.RoomTypeId,
                StartDate = refDate.AddDays(60),
                EndDate = refDate.AddDays(90),
                TotalCount = 10,
                AvailableCount = 8,
                IsAvailable = true
            });
            blockIndex++;
        }

        modelBuilder.Entity<Hotel>().HasData(hotels);
        modelBuilder.Entity<RoomType>().HasData(roomTypes);
        modelBuilder.Entity<InventoryBlock>().HasData(blocks.ToArray());
    }
}
