using System.Text;
using System.Text.Json;
using RabbitMQ.Client;

namespace OrderService.Events;

public interface IEventPublisher
{
    Task PublishAsync<T>(string exchange, string routingKey, T message, string correlationId);
}

public class RabbitMqPublisher : IEventPublisher, IAsyncDisposable
{
    private readonly IConnection _connection;
    private readonly IChannel _channel;
    private readonly ILogger<RabbitMqPublisher> _logger;

    public RabbitMqPublisher(IConnection connection, ILogger<RabbitMqPublisher> logger)
    {
        _connection = connection;
        _channel = connection.CreateChannelAsync().GetAwaiter().GetResult();
        _logger = logger;
    }

    public async Task PublishAsync<T>(string exchange, string routingKey, T message, string correlationId)
    {
        await _channel.ExchangeDeclareAsync(exchange, ExchangeType.Topic, durable: true);

        var json = JsonSerializer.Serialize(message);
        var body = Encoding.UTF8.GetBytes(json);

        var props = new BasicProperties
        {
            ContentType = "application/json",
            CorrelationId = correlationId,
            MessageId = Guid.NewGuid().ToString()
        };

        await _channel.BasicPublishAsync(exchange, routingKey, false, props, body);
        _logger.LogInformation("[{CorrelationId}] Published {Event} to {Exchange}/{RoutingKey}",
            correlationId, typeof(T).Name, exchange, routingKey);
    }

    public async ValueTask DisposeAsync()
    {
        await _channel.CloseAsync();
    }
}
