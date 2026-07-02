using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using StackExchange.Redis;

namespace NotificationService.Events;

public record OrderConfirmed(Guid OrderId, string CustomerEmail, string CorrelationId);
public record OrderRejected(Guid OrderId, string CustomerEmail, string Reason, string CorrelationId);

public class NotificationConsumer : BackgroundService
{
    private readonly IConnection _rabbitConnection;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<NotificationConsumer> _logger;
    private IChannel? _channel;

    public NotificationConsumer(IConnection rabbitConnection, IConnectionMultiplexer redis, ILogger<NotificationConsumer> logger)
    {
        _rabbitConnection = rabbitConnection;
        _redis = redis;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _channel = await _rabbitConnection.CreateChannelAsync(cancellationToken: stoppingToken);
        await _channel.ExchangeDeclareAsync("orders", ExchangeType.Topic, durable: true, cancellationToken: stoppingToken);

        var queueName = "notifications";
        await _channel.QueueDeclareAsync(queueName, durable: true, exclusive: false, autoDelete: false, cancellationToken: stoppingToken);
        await _channel.QueueBindAsync(queueName, "orders", "order.confirmed", cancellationToken: stoppingToken);
        await _channel.QueueBindAsync(queueName, "orders", "order.rejected", cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            var correlationId = ea.BasicProperties.CorrelationId ?? "unknown";
            var routingKey = ea.RoutingKey;
            var body = Encoding.UTF8.GetString(ea.Body.ToArray());
            var db = _redis.GetDatabase();

            _logger.LogInformation("[{CorrelationId}] NotificationService received {RoutingKey}", correlationId, routingKey);

            try
            {
                if (routingKey == "order.confirmed")
                {
                    var evt = JsonSerializer.Deserialize<OrderConfirmed>(body)!;
                    var notification = new
                    {
                        Type = "OrderConfirmed",
                        evt.OrderId,
                        evt.CustomerEmail,
                        Message = $"Your order {evt.OrderId} has been confirmed!",
                        Timestamp = DateTime.UtcNow
                    };

                    await db.StringSetAsync($"notification:{evt.OrderId}", JsonSerializer.Serialize(notification));
                    await db.ListRightPushAsync($"notifications:{evt.CustomerEmail}", JsonSerializer.Serialize(notification));

                    _logger.LogInformation("[{CorrelationId}] Notification sent to {Email}: Order {OrderId} CONFIRMED",
                        correlationId, evt.CustomerEmail, evt.OrderId);
                }
                else if (routingKey == "order.rejected")
                {
                    var evt = JsonSerializer.Deserialize<OrderRejected>(body)!;
                    var notification = new
                    {
                        Type = "OrderRejected",
                        evt.OrderId,
                        evt.CustomerEmail,
                        Message = $"Your order {evt.OrderId} was rejected: {evt.Reason}",
                        Timestamp = DateTime.UtcNow
                    };

                    await db.StringSetAsync($"notification:{evt.OrderId}", JsonSerializer.Serialize(notification));
                    await db.ListRightPushAsync($"notifications:{evt.CustomerEmail}", JsonSerializer.Serialize(notification));

                    _logger.LogInformation("[{CorrelationId}] Notification sent to {Email}: Order {OrderId} REJECTED - {Reason}",
                        correlationId, evt.CustomerEmail, evt.OrderId, evt.Reason);
                }

                await _channel.BasicAckAsync(ea.DeliveryTag, false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{CorrelationId}] Error processing notification", correlationId);
                await _channel.BasicNackAsync(ea.DeliveryTag, false, true);
            }
        };

        await _channel.BasicConsumeAsync(queueName, autoAck: false, consumer, cancellationToken: stoppingToken);
    }
}
