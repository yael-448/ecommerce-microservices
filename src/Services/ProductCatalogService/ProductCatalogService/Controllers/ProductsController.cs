using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using MongoDB.Driver;
using ProductCatalogService.Models;

namespace ProductCatalogService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IMongoCollection<Product> _products;
    private readonly IDistributedCache _cache;
    private readonly ILogger<ProductsController> _logger;

    public ProductsController(IMongoDatabase db, IDistributedCache cache, ILogger<ProductsController> logger)
    {
        _products = db.GetCollection<Product>("products");
        _cache = cache;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var cacheKey = "products:all";
        var cached = await _cache.GetStringAsync(cacheKey);

        if (cached is not null)
        {
            _logger.LogInformation("CACHE HIT for {CacheKey}", cacheKey);
            return Ok(JsonSerializer.Deserialize<List<Product>>(cached));
        }

        _logger.LogInformation("CACHE MISS for {CacheKey}", cacheKey);
        var products = await _products.Find(_ => true).ToListAsync();

        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(products),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) });

        return Ok(products);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var cacheKey = $"products:{id}";
        var cached = await _cache.GetStringAsync(cacheKey);

        if (cached is not null)
        {
            _logger.LogInformation("CACHE HIT for {CacheKey}", cacheKey);
            return Ok(JsonSerializer.Deserialize<Product>(cached));
        }

        _logger.LogInformation("CACHE MISS for {CacheKey}", cacheKey);
        var product = await _products.Find(p => p.Id == id).FirstOrDefaultAsync();
        if (product is null) return NotFound();

        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(product),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10) });

        return Ok(product);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProductRequest request)
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            Price = request.Price,
            Category = request.Category,
            Attributes = request.Attributes ?? new()
        };

        await _products.InsertOneAsync(product);

        // Invalidate the "all products" cache
        await _cache.RemoveAsync("products:all");
        _logger.LogInformation("Cache invalidated: products:all (new product created)");

        return CreatedAtAction(nameof(GetById), new { id = product.Id }, product);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProductRequest request)
    {
        var update = Builders<Product>.Update
            .Set(p => p.Name, request.Name)
            .Set(p => p.Description, request.Description)
            .Set(p => p.Price, request.Price)
            .Set(p => p.Category, request.Category)
            .Set(p => p.Attributes, request.Attributes ?? new());

        var result = await _products.UpdateOneAsync(p => p.Id == id, update);
        if (result.MatchedCount == 0) return NotFound();

        // Invalidate cache for this product and the list
        await _cache.RemoveAsync($"products:{id}");
        await _cache.RemoveAsync("products:all");
        _logger.LogInformation("Cache invalidated for product {ProductId} (update)", id);

        return NoContent();
    }

    [HttpGet("health")]
    public IActionResult Health() => Ok(new
    {
        Status = "Healthy",
        Service = "ProductCatalogService",
        ContainerId = Environment.MachineName
    });
}

public record CreateProductRequest(string Name, string Description, decimal Price, string Category, Dictionary<string, string>? Attributes);
public record UpdateProductRequest(string Name, string Description, decimal Price, string Category, Dictionary<string, string>? Attributes);
