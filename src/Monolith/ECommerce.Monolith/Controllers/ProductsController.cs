using ECommerce.Monolith.Data;
using ECommerce.Monolith.DTOs;
using ECommerce.Monolith.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Monolith.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly AppDbContext _db;

    public ProductsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<List<ProductResponse>>> GetAll()
    {
        var products = await _db.Products.ToListAsync();
        return products.Select(p => new ProductResponse(p.Id, p.Name, p.Description, p.Price, p.Category, p.CreatedAt)).ToList();
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductResponse>> GetById(Guid id)
    {
        var p = await _db.Products.FindAsync(id);
        if (p is null) return NotFound();
        return new ProductResponse(p.Id, p.Name, p.Description, p.Price, p.Category, p.CreatedAt);
    }

    [HttpPost]
    public async Task<ActionResult<ProductResponse>> Create(CreateProductRequest request)
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            Price = request.Price,
            Category = request.Category
        };

        _db.Products.Add(product);

        var inventory = new InventoryItem
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            Quantity = request.InitialStock
        };

        _db.Inventory.Add(inventory);
        await _db.SaveChangesAsync();

        var response = new ProductResponse(product.Id, product.Name, product.Description, product.Price, product.Category, product.CreatedAt);
        return CreatedAtAction(nameof(GetById), new { id = product.Id }, response);
    }
}
