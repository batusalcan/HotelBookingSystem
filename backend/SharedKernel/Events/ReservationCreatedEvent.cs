namespace SharedKernel.Events;

// AMQP contract: published by BookHotelService, consumed by NotificationService
// Exchange: hotel.reservations | Queue: reservation.created | Routing Key: reservation.created
public class ReservationCreatedEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public Guid BookingId { get; init; }
    public string UserId { get; init; } = string.Empty;
    public Guid HotelId { get; init; }
    public string HotelName { get; init; } = string.Empty;
    public Guid RoomTypeId { get; init; }
    public DateOnly CheckInDate { get; init; }
    public DateOnly CheckOutDate { get; init; }
    public int GuestCount { get; init; }
    public decimal TotalAmount { get; init; }
    public DateTime PublishedAt { get; init; } = DateTime.UtcNow;
}
