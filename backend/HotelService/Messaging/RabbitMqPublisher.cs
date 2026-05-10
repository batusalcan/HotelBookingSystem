using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using SharedKernel.Events;

namespace HotelService.Messaging;

/// <summary>
/// Singleton IConnection — one connection pool per service instance.
/// IModel channels are created per-publish and disposed immediately (not thread-safe to share).
/// </summary>
public class RabbitMqPublisher : IRabbitMqPublisher, IDisposable
{
    private readonly IConnection _connection;
    private readonly string _exchange;
    private readonly string _routingKey;
    private readonly ILogger<RabbitMqPublisher> _logger;

    public RabbitMqPublisher(IConfiguration config, ILogger<RabbitMqPublisher> logger)
    {
        _logger = logger;
        _exchange = config["RabbitMQ:Exchange"] ?? "hotel.reservations";
        _routingKey = config["RabbitMQ:RoutingKey"] ?? "reservation.created";

        var factory = new ConnectionFactory
        {
            HostName = config["RabbitMQ:Host"] ?? "localhost",
            Port = int.Parse(config["RabbitMQ:Port"] ?? "5672"),
            UserName = config["RabbitMQ:Username"] ?? "guest",
            Password = config["RabbitMQ:Password"] ?? "guest"
        };

        try
        {
            _connection = factory.CreateConnection();
            _logger.LogInformation("RabbitMQ connection established");
        }
        catch (Exception ex)
        {
            _logger.LogWarning("RabbitMQ unavailable: {Message}. Events will not be published.", ex.Message);
            _connection = null!;
        }
    }

    /// <precondition>evt is not null and contains valid booking data</precondition>
    /// <postcondition>ReservationCreatedEvent is published to hotel.reservations exchange</postcondition>
    public Task PublishReservationCreatedAsync(ReservationCreatedEvent evt)
    {
        if (_connection is null || !_connection.IsOpen)
        {
            _logger.LogWarning("RabbitMQ not connected — skipping event publish for BookingId={BookingId}", evt.BookingId);
            return Task.CompletedTask;
        }

        using var channel = _connection.CreateModel();
        channel.ExchangeDeclare(_exchange, ExchangeType.Direct, durable: true);
        channel.QueueDeclare("reservation.created", durable: true, exclusive: false, autoDelete: false);
        channel.QueueBind("reservation.created", _exchange, _routingKey);

        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(evt));
        var props = channel.CreateBasicProperties();
        props.Persistent = true;
        props.ContentType = "application/json";

        channel.BasicPublish(_exchange, _routingKey, props, body);
        _logger.LogInformation("Published ReservationCreatedEvent BookingId={BookingId}", evt.BookingId);

        return Task.CompletedTask;
    }

    public void Dispose() => _connection?.Dispose();
}
