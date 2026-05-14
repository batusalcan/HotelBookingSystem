using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using SharedKernel.Events;

namespace NotificationService.Messaging;

/// <summary>
/// Always-on IHostedService that subscribes to the reservation.created queue.
/// ACKs on success; NACKs and requeues on failure (BP-07).
/// </summary>
public class RabbitMqConsumer : IHostedService, IDisposable
{
    private readonly INotificationFactory _factory;
    private readonly ILogger<RabbitMqConsumer> _logger;
    private readonly string _exchange;
    private readonly string _queue;
    private readonly string _routingKey;
    private IConnection? _connection;
    private IModel? _channel;

    public RabbitMqConsumer(
        IConfiguration config,
        INotificationFactory factory,
        ILogger<RabbitMqConsumer> logger)
    {
        _factory = factory;
        _logger = logger;
        _exchange = config["RabbitMQ:Exchange"] ?? "hotel.reservations";
        _queue = config["RabbitMQ:Queue"] ?? "reservation.created";
        _routingKey = config["RabbitMQ:RoutingKey"] ?? "reservation.created";

        var connFactory = new ConnectionFactory
        {
            HostName = config["RabbitMQ:Host"] ?? "localhost",
            Port = int.Parse(config["RabbitMQ:Port"] ?? "5672"),
            UserName = config["RabbitMQ:Username"] ?? "guest",
            Password = config["RabbitMQ:Password"] ?? "guest",
            VirtualHost = config["RabbitMQ:VirtualHost"] ?? "/",
            Ssl = new SslOption
            {
                Enabled = bool.Parse(config["RabbitMQ:Ssl"] ?? "false"),
                ServerName = config["RabbitMQ:Host"] ?? "localhost"
            }
        };

        try
        {
            _connection = connFactory.CreateConnection();
        }
        catch (Exception ex)
        {
            _logger.LogWarning("RabbitMQ unavailable on startup: {Message}. Queue consumer will not start.", ex.Message);
        }
    }

    /// <precondition>RabbitMQ is reachable; exchange and queue names are configured</precondition>
    /// <postcondition>Consumer is subscribed to reservation.created queue and ready to process messages</postcondition>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_connection is null || !_connection.IsOpen)
        {
            _logger.LogWarning("RabbitMQ not connected — queue consumer will not start.");
            return Task.CompletedTask;
        }

        _channel = _connection.CreateModel();
        _channel.ExchangeDeclare(_exchange, ExchangeType.Direct, durable: true);
        _channel.QueueDeclare(_queue, durable: true, exclusive: false, autoDelete: false);
        _channel.QueueBind(_queue, _exchange, _routingKey);
        _channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

        var consumer = new EventingBasicConsumer(_channel);
        consumer.Received += OnMessageReceived;
        _channel.BasicConsume(_queue, autoAck: false, consumer: consumer);

        _logger.LogInformation("RabbitMQ consumer started. Listening on queue={Queue}", _queue);
        return Task.CompletedTask;
    }

    private void OnMessageReceived(object? sender, BasicDeliverEventArgs ea)
    {
        var deliveryTag = ea.DeliveryTag;
        try
        {
            var json = Encoding.UTF8.GetString(ea.Body.ToArray());
            var evt = JsonSerializer.Deserialize<ReservationCreatedEvent>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidOperationException("Deserialized event is null.");

            var notification = _factory.Create(NotificationType.BookingConfirmation);
            notification.Send(evt);

            _channel!.BasicAck(deliveryTag, multiple: false);
            _logger.LogInformation("ACK sent for BookingId={BookingId}", evt.BookingId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process message DeliveryTag={Tag}. Sending NACK + requeue.", deliveryTag);
            _channel!.BasicNack(deliveryTag, multiple: false, requeue: true);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _channel?.Close();
        _connection?.Close();
        _logger.LogInformation("RabbitMQ consumer stopped.");
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
    }
}
