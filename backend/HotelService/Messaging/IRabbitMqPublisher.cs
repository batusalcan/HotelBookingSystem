using SharedKernel.Events;

namespace HotelService.Messaging;

public interface IRabbitMqPublisher
{
    Task PublishReservationCreatedAsync(ReservationCreatedEvent evt);
}
