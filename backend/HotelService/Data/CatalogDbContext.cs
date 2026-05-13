using HotelService.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelService.Data;

public class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    public DbSet<Hotel> Hotels => Set<Hotel>();
    public DbSet<RoomType> RoomTypes => Set<RoomType>();
    public DbSet<InventoryBlock> InventoryBlocks => Set<InventoryBlock>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Hotel>(e =>
        {
            e.HasKey(h => h.HotelId);
            e.Property(h => h.Name).HasMaxLength(100).IsRequired();
            e.Property(h => h.Destination).HasMaxLength(100).IsRequired();
            e.Property(h => h.Latitude).HasColumnType("decimal(9,6)");
            e.Property(h => h.Longitude).HasColumnType("decimal(9,6)");
            e.Property(h => h.BaseRating).HasColumnType("decimal(3,1)");
            e.Property(h => h.ImageUrl).HasMaxLength(500);
            e.HasIndex(h => h.Destination).HasDatabaseName("IX_Hotels_Destination");
        });

        modelBuilder.Entity<RoomType>(e =>
        {
            e.HasKey(r => r.RoomTypeId);
            e.Property(r => r.TypeName).HasMaxLength(50).IsRequired();
            e.Property(r => r.BasePricePerNight).HasColumnType("decimal(18,2)");
            e.HasOne(r => r.Hotel)
             .WithMany(h => h.RoomTypes)
             .HasForeignKey(r => r.HotelId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<InventoryBlock>(e =>
        {
            e.HasKey(i => i.InventoryId);
            e.Property<uint>("xmin").HasColumnName("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            e.HasIndex(i => new { i.StartDate, i.EndDate }).HasDatabaseName("IX_InventoryBlocks_StartDate_EndDate");
            e.HasIndex(i => i.RoomTypeId).HasDatabaseName("IX_InventoryBlocks_RoomTypeId");
            e.HasOne(i => i.RoomType)
             .WithMany(r => r.InventoryBlocks)
             .HasForeignKey(i => i.RoomTypeId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        DataSeeder.Seed(modelBuilder);
    }
}
