using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OrderService.Data;
using OrderService.Models;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace OrderService.Events;

public class OrderSagaConsumer : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConnection _connection;
    private readonly ILogger<OrderSagaConsumer> _logger;
    private IChannel? _channel;

    public OrderSagaConsumer(IServiceProvider serviceProvider, IConnection connection, ILogger<OrderSagaConsumer> logger)
    {
        _serviceProvider = serviceProvider;
        _connection = connection;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);
        await _channel.ExchangeDeclareAsync("orders", ExchangeType.Topic, durable: true, cancellationToken: stoppingToken);

        var queueName = "order-saga-responses";
        await _channel.QueueDeclareAsync(queueName, durable: true, exclusive: false, autoDelete: false, cancellationToken: stoppingToken);
        await _channel.QueueBindAsync(queueName, "orders", "inventory.reserved", cancellationToken: stoppingToken);
        await _channel.QueueBindAsync(queueName, "orders", "inventory.rejected", cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            var correlationId = ea.BasicProperties.CorrelationId ?? "unknown";
            var routingKey = ea.RoutingKey;
            var body = Encoding.UTF8.GetString(ea.Body.ToArray());

            _logger.LogInformation("[{CorrelationId}] Received {RoutingKey}", correlationId, routingKey);

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
                var publisher = scope.ServiceProvider.GetRequiredService<IEventPublisher>();

                if (routingKey == "inventory.reserved")
                {
                    var evt = JsonSerializer.Deserialize<InventoryReserved>(body)!;
                    await ConfirmOrder(db, publisher, evt.OrderId, correlationId);
                }
                else if (routingKey == "inventory.rejected")
                {
                    var evt = JsonSerializer.Deserialize<InventoryRejected>(body)!;
                    await RejectOrder(db, publisher, evt.OrderId, evt.Reason, correlationId);
                }

                await _channel.BasicAckAsync(ea.DeliveryTag, false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{CorrelationId}] Error processing {RoutingKey}", correlationId, routingKey);
                await _channel.BasicNackAsync(ea.DeliveryTag, false, true);
            }
        };

        await _channel.BasicConsumeAsync(queueName, autoAck: false, consumer, cancellationToken: stoppingToken);
    }

    private async Task ConfirmOrder(OrderDbContext db, IEventPublisher publisher, Guid orderId, string correlationId)
    {
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == orderId);
        if (order is null || order.Status != OrderStatus.Pending) return;

        order.Status = OrderStatus.Confirmed;
        await db.SaveChangesAsync();

        _logger.LogInformation("[{CorrelationId}] Order {OrderId} CONFIRMED", correlationId, orderId);

        await publisher.PublishAsync("orders", "order.confirmed",
            new OrderConfirmed(orderId, order.CustomerEmail, correlationId), correlationId);
    }

    private async Task RejectOrder(OrderDbContext db, IEventPublisher publisher, Guid orderId, string reason, string correlationId)
    {
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == orderId);
        if (order is null || order.Status != OrderStatus.Pending) return;

        order.Status = OrderStatus.Rejected;
        await db.SaveChangesAsync();

        _logger.LogInformation("[{CorrelationId}] Order {OrderId} REJECTED: {Reason}", correlationId, orderId, reason);

        await publisher.PublishAsync("orders", "order.rejected",
            new OrderRejected(orderId, order.CustomerEmail, reason, correlationId), correlationId);
    }
}
