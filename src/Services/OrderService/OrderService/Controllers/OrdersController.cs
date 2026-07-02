using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderService.Data;
using OrderService.Events;
using OrderService.Models;

namespace OrderService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly OrderDbContext _db;
    private readonly IEventPublisher _publisher;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(OrderDbContext db, IEventPublisher publisher, ILogger<OrdersController> logger)
    {
        _db = db;
        _publisher = publisher;
        _logger = logger;
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var order = await _db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id);
        if (order is null) return NotFound();
        return Ok(order);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOrderRequest request)
    {
        var correlationId = Guid.NewGuid().ToString();
        _logger.LogInformation("[{CorrelationId}] Creating order for {Email}", correlationId, request.CustomerEmail);

        var order = new Order
        {
            Id = Guid.NewGuid(),
            CustomerEmail = request.CustomerEmail,
            Status = OrderStatus.Pending,
            TotalAmount = request.Items.Sum(i => i.UnitPrice * i.Quantity)
        };

        foreach (var item in request.Items)
        {
            order.Items.Add(new OrderItem
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                ProductId = item.ProductId,
                ProductName = item.ProductName,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice
            });
        }

        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        // Publish OrderPlaced event to start the saga
        var orderPlacedEvent = new OrderPlaced(
            order.Id,
            order.CustomerEmail,
            order.Items.Select(i => new OrderItemEvent(i.ProductId, i.ProductName, i.Quantity, i.UnitPrice)).ToList(),
            correlationId
        );

        await _publisher.PublishAsync("orders", "order.placed", orderPlacedEvent, correlationId);

        _logger.LogInformation("[{CorrelationId}] Order {OrderId} placed, saga started", correlationId, order.Id);

        return CreatedAtAction(nameof(GetById), new { id = order.Id }, new { order.Id, order.Status, correlationId });
    }

    [HttpGet("health")]
    public IActionResult Health() => Ok(new { Status = "Healthy", Service = "OrderService" });
}

public record CreateOrderRequest(string CustomerEmail, List<CreateOrderItemRequest> Items);
public record CreateOrderItemRequest(Guid ProductId, string ProductName, int Quantity, decimal UnitPrice);
