using InventoryService.Data;
using InventoryService.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventoryService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InventoryController : ControllerBase
{
    private readonly InventoryDbContext _db;

    public InventoryController(InventoryDbContext db) => _db = db;

    [HttpGet("{productId:guid}")]
    public async Task<IActionResult> GetByProductId(Guid productId)
    {
        var item = await _db.Inventory.FirstOrDefaultAsync(i => i.ProductId == productId);
        if (item is null) return NotFound();
        return Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> SetStock([FromBody] SetStockRequest request)
    {
        var item = await _db.Inventory.FirstOrDefaultAsync(i => i.ProductId == request.ProductId);
        if (item is null)
        {
            item = new InventoryItem
            {
                Id = Guid.NewGuid(),
                ProductId = request.ProductId,
                Quantity = request.Quantity
            };
            _db.Inventory.Add(item);
        }
        else
        {
            item.Quantity = request.Quantity;
            item.LastUpdated = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        return Ok(item);
    }

    [HttpGet("health")]
    public IActionResult Health() => Ok(new { Status = "Healthy", Service = "InventoryService" });
}

public record SetStockRequest(Guid ProductId, int Quantity);
