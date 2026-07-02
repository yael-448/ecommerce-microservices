using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using InventoryService.Data;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace InventoryService.Events;

// Event contracts
public record OrderPlaced(Guid OrderId, string CustomerEmail, List<OrderItemEvent> Items, string CorrelationId);
public record OrderItemEvent(Guid ProductId, string ProductName, int Quantity, decimal UnitPrice);
public record InventoryReserved(Guid OrderId, string CorrelationId);
public record InventoryRejected(Guid OrderId, string Reason, string CorrelationId);

public class InventoryConsumer : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConnection _connection;
    private readonly ILogger<InventoryConsumer> _logger;
    private IChannel? _channel;

    public InventoryConsumer(IServiceProvider serviceProvider, IConnection connection, ILogger<InventoryConsumer> logger)
    {
        _serviceProvider = serviceProvider;
        _connection = connection;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);
        await _channel.ExchangeDeclareAsync("orders", ExchangeType.Topic, durable: true, cancellationToken: stoppingToken);

        var queueName = "inventory-reserve";
        await _channel.QueueDeclareAsync(queueName, durable: true, exclusive: false, autoDelete: false, cancellationToken: stoppingToken);
        await _channel.QueueBindAsync(queueName, "orders", "order.placed", cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            var correlationId = ea.BasicProperties.CorrelationId ?? "unknown";
            var body = Encoding.UTF8.GetString(ea.Body.ToArray());

            _logger.LogInformation("[{CorrelationId}] Received order.placed event", correlationId);

            try
            {
                var orderPlaced = JsonSerializer.Deserialize<OrderPlaced>(body)!;
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();

                await ReserveInventory(db, orderPlaced, correlationId);
                await _channel.BasicAckAsync(ea.DeliveryTag, false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{CorrelationId}] Error processing order.placed", correlationId);
                await _channel.BasicNackAsync(ea.DeliveryTag, false, true);
            }
        };

        await _channel.BasicConsumeAsync(queueName, autoAck: false, consumer, cancellationToken: stoppingToken);
    }

    private async Task ReserveInventory(InventoryDbContext db, OrderPlaced orderPlaced, string correlationId)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();

        try
        {
            foreach (var item in orderPlaced.Items)
            {
                var inventory = await db.Inventory.FirstOrDefaultAsync(i => i.ProductId == item.ProductId);
                if (inventory is null || inventory.Quantity < item.Quantity)
                {
                    await transaction.RollbackAsync();
                    var reason = $"Insufficient stock for product {item.ProductName} (requested: {item.Quantity}, available: {inventory?.Quantity ?? 0})";
                    _logger.LogWarning("[{CorrelationId}] {Reason}", correlationId, reason);

                    await PublishAsync("inventory.rejected",
                        new InventoryRejected(orderPlaced.OrderId, reason, correlationId), correlationId);
                    return;
                }

                inventory.Quantity -= item.Quantity;
                inventory.LastUpdated = DateTime.UtcNow;
            }

            await db.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation("[{CorrelationId}] Inventory reserved for order {OrderId}", correlationId, orderPlaced.OrderId);
            await PublishAsync("inventory.reserved",
                new InventoryReserved(orderPlaced.OrderId, correlationId), correlationId);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private async Task PublishAsync<T>(string routingKey, T message, string correlationId)
    {
        var json = JsonSerializer.Serialize(message);
        var body = Encoding.UTF8.GetBytes(json);

        var props = new BasicProperties
        {
            ContentType = "application/json",
            CorrelationId = correlationId
        };

        await _channel!.BasicPublishAsync("orders", routingKey, false, props, body);
        _logger.LogInformation("[{CorrelationId}] Published {RoutingKey}", correlationId, routingKey);
    }
}
