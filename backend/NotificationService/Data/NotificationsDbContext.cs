using Microsoft.EntityFrameworkCore;

namespace NotificationService.Data;

public class NotificationsDbContext(DbContextOptions<NotificationsDbContext> options) : DbContext(options)
{
    public DbSet<NotificationAlert> NotificationAlerts => Set<NotificationAlert>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<NotificationAlert>(e =>
        {
            e.HasKey(x => x.NotificationId);
            e.Property(x => x.HotelName).HasMaxLength(200).IsRequired();
            e.Property(x => x.RoomTypeName).HasMaxLength(100).IsRequired();
            e.HasIndex(x => x.CreatedAt);
        });
    }
}
