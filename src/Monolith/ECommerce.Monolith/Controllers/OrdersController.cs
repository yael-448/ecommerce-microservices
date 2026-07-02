using ECommerce.Monolith.Data;
using ECommerce.Monolith.DTOs;
using ECommerce.Monolith.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Monolith.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly AppDbContext _db;

    public OrdersController(AppDbContext db) => _db = db;

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrderResponse>> GetById(Guid id)
    {
        var order = await _db.Orders
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order is null) return NotFound();

        return new OrderResponse(
            order.Id,
            order.CustomerEmail,
            order.Status.ToString(),
            order.TotalAmount,
            order.CreatedAt,
            order.Items.Select(i => new OrderItemResponse(i.ProductId, i.Product.Name, i.Quantity, i.UnitPrice)).ToList()
        );
    }

    [HttpPost]
    public async Task<ActionResult<OrderResponse>> Create(CreateOrderRequest request)
    {
        // Use a transaction to ensure atomicity (ACID)
        await using var transaction = await _db.Database.BeginTransactionAsync();

        try
        {
            var order = new Order
            {
                Id = Guid.NewGuid(),
                CustomerEmail = request.CustomerEmail,
                Status = OrderStatus.Pending
            };

            decimal total = 0;

            foreach (var item in request.Items)
            {
                var product = await _db.Products.FindAsync(item.ProductId);
                if (product is null)
                    return BadRequest($"Product {item.ProductId} not found");

                var inventory = await _db.Inventory.FirstOrDefaultAsync(i => i.ProductId == item.ProductId);
                if (inventory is null || inventory.Quantity < item.Quantity)
                    return BadRequest($"Insufficient stock for product {product.Name}");

                // Reserve inventory
                inventory.Quantity -= item.Quantity;
                inventory.LastUpdated = DateTime.UtcNow;

                order.Items.Add(new OrderItem
                {
                    Id = Guid.NewGuid(),
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    UnitPrice = product.Price
                });

                total += product.Price * item.Quantity;
            }

            order.TotalAmount = total;
            order.Status = OrderStatus.Confirmed;

            _db.Orders.Add(order);
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            var response = new OrderResponse(
                order.Id,
                order.CustomerEmail,
                order.Status.ToString(),
                order.TotalAmount,
                order.CreatedAt,
                order.Items.Select(i => new OrderItemResponse(i.ProductId, "", i.Quantity, i.UnitPrice)).ToList()
            );

            return CreatedAtAction(nameof(GetById), new { id = order.Id }, response);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
