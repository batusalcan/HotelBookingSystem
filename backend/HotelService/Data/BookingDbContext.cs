using HotelService.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelService.Data;

public class BookingDbContext(DbContextOptions<BookingDbContext> options) : DbContext(options)
{
    public DbSet<Booking> Bookings => Set<Booking>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Booking>(e =>
        {
            e.HasKey(b => b.BookingId);
            e.Property(b => b.UserId).HasMaxLength(100).IsRequired();
            e.Property(b => b.TotalAmount).HasColumnType("decimal(18,2)");
            e.Property(b => b.Status).HasMaxLength(20).HasDefaultValue("Confirmed");
            e.HasIndex(b => b.UserId).HasDatabaseName("IX_Bookings_UserId");
            e.HasIndex(b => b.HotelId).HasDatabaseName("IX_Bookings_HotelId");
        });
    }
}
