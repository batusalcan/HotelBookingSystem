using SharedKernel.Events;

namespace NotificationService.Messaging;

public class BookingConfirmationNotification(ILogger<BookingConfirmationNotification> logger) : INotification
{
    /// <precondition>payload is a non-null ReservationCreatedEvent</precondition>
    /// <postcondition>Booking confirmation message logged to console (simulated email/SMS delivery)</postcondition>
    public void Send(object payload)
    {
        if (payload is not ReservationCreatedEvent evt) return;

        logger.LogInformation(
            "[BOOKING CONFIRMATION] BookingId={BookingId} | UserId={UserId} | Hotel={HotelName} | " +
            "CheckIn={CheckIn} | CheckOut={CheckOut} | Guests={Guests} | Total={Total:C}",
            evt.BookingId, evt.UserId, evt.HotelName,
            evt.CheckInDate, evt.CheckOutDate, evt.GuestCount, evt.TotalAmount);
    }
}
