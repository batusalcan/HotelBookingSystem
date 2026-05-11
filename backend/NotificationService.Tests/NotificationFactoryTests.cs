using Microsoft.Extensions.Logging.Abstractions;
using NotificationService.Messaging;
using Xunit;

namespace NotificationService.Tests;

public class NotificationFactoryTests
{
    private static NotificationFactory BuildFactory() => new(
        new BookingConfirmationNotification(NullLogger<BookingConfirmationNotification>.Instance),
        new LowCapacityAlertNotification(NullLogger<LowCapacityAlertNotification>.Instance));

    [Fact]
    public void Create_BookingConfirmation_ReturnsBookingConfirmationNotification()
    {
        var notification = BuildFactory().Create(NotificationType.BookingConfirmation);
        Assert.IsType<BookingConfirmationNotification>(notification);
    }

    [Fact]
    public void Create_LowCapacity_ReturnsLowCapacityAlertNotification()
    {
        var notification = BuildFactory().Create(NotificationType.LowCapacity);
        Assert.IsType<LowCapacityAlertNotification>(notification);
    }

    [Fact]
    public void Create_UnknownType_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => BuildFactory().Create((NotificationType)999));
    }
}
