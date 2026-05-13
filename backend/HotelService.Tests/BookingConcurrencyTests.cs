using HotelService.Data;
using HotelService.DTOs;
using HotelService.Entities;
using HotelService.Messaging;
using HotelService.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SharedKernel.Events;
using SharedKernel.Exceptions;

namespace HotelService.Tests;

/// <summary>
/// Tests the optimistic concurrency path in BookingService:
/// when PostgreSQL increments xmin between the client's read and write,
/// SaveChangesAsync throws DbUpdateConcurrencyException which must surface as ConflictException.
/// </summary>
public class BookingConcurrencyTests
{
    // ── Test doubles ─────────────────────────────────────────────────────────

    /// <summary>CatalogDbContext that always throws DbUpdateConcurrencyException on save.</summary>
    private sealed class ThrowConcurrencyContext(DbContextOptions<CatalogDbContext> options)
        : CatalogDbContext(options)
    {
        public override Task<int> SaveChangesAsync(CancellationToken ct = default)
            => throw new DbUpdateConcurrencyException("Simulated RowVersion mismatch");
    }

    private sealed class NullPublisher : IRabbitMqPublisher
    {
        public Task PublishReservationCreatedAsync(ReservationCreatedEvent evt) => Task.CompletedTask;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static DbContextOptions<CatalogDbContext> InMemoryCatalogOptions(string name)
        => new DbContextOptionsBuilder<CatalogDbContext>()
            .UseInMemoryDatabase(name)
            .Options;

    private static DbContextOptions<BookingDbContext> InMemoryBookingOptions(string name)
        => new DbContextOptionsBuilder<BookingDbContext>()
            .UseInMemoryDatabase(name)
            .Options;

    // ── Tests ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that a DbUpdateConcurrencyException (SQL RowVersion mismatch)
    /// is translated to ConflictException — preventing overbooking.
    /// </summary>
    [Fact]
    public async Task CreateBooking_RowVersionMismatch_ThrowsConflictException()
    {
        var dbName = "concurrency_" + Guid.NewGuid();
        var catalogOptions = InMemoryCatalogOptions(dbName);

        var hotelId = Guid.NewGuid();
        var roomTypeId = Guid.NewGuid();
        var inventoryId = Guid.NewGuid();

        // Seed the shared in-memory store with a valid inventory block
        await using (var seedCtx = new CatalogDbContext(catalogOptions))
        {
            seedCtx.Hotels.Add(new Hotel
            {
                HotelId = hotelId,
                Name = "Concurrency Test Hotel",
                Destination = "Istanbul",
                Latitude = 41.0m,
                Longitude = 29.0m,
                IsActive = true
            });
            seedCtx.RoomTypes.Add(new RoomType
            {
                RoomTypeId = roomTypeId,
                HotelId = hotelId,
                TypeName = "Standard",
                MaxGuests = 2,
                BasePricePerNight = 1000m
            });
            seedCtx.InventoryBlocks.Add(new InventoryBlock
            {
                InventoryId = inventoryId,
                RoomTypeId = roomTypeId,
                StartDate = new DateOnly(2026, 6, 1),
                EndDate = new DateOnly(2026, 6, 30),
                TotalCount = 5,
                AvailableCount = 5,
                IsAvailable = true
            });
            await seedCtx.SaveChangesAsync();
        }

        // The throw context shares the same in-memory DB — can read, but SaveChanges explodes
        await using var throwCtx = new ThrowConcurrencyContext(catalogOptions);
        await using var bookingCtx = new BookingDbContext(
            InMemoryBookingOptions("booking_" + Guid.NewGuid()));

        var service = new BookingService(
            throwCtx, bookingCtx, new NullPublisher(), NullLogger<BookingService>.Instance);

        var request = new CreateBookingRequest
        {
            HotelId = hotelId,
            RoomTypeId = roomTypeId,
            InventoryId = inventoryId,
            StartDate = new DateOnly(2026, 6, 10),
            EndDate = new DateOnly(2026, 6, 15),
            GuestCount = 1,
            RowVersion = 1u
        };

        // Precondition guards pass; SaveChanges throws → ConflictException expected
        await Assert.ThrowsAsync<ConflictException>(() =>
            service.CreateBookingAsync(request, "user-1", isAuthenticated: true));
    }
}
