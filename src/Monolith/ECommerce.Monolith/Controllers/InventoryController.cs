using ECommerce.Monolith.Data;
using ECommerce.Monolith.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Monolith.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InventoryController : ControllerBase
{
    private readonly AppDbContext _db;

    public InventoryController(AppDbContext db) => _db = db;

    [HttpGet("{productId:guid}")]
    public async Task<ActionResult<InventoryResponse>> GetByProductId(Guid productId)
    {
        var item = await _db.Inventory
            .Include(i => i.Product)
            .FirstOrDefaultAsync(i => i.ProductId == productId);

        if (item is null) return NotFound();

        return new InventoryResponse(item.ProductId, item.Product.Name, item.Quantity);
    }
}
