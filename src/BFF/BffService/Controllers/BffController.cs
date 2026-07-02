using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace BffService.Controllers;

[ApiController]
[Route("bff")]
public class BffController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<BffController> _logger;

    public BffController(IHttpClientFactory httpClientFactory, ILogger<BffController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Aggregates order data + product details in a single response.
    /// This is what BFF is for: combining data from multiple services into one client-friendly response.
    /// </summary>
    [HttpGet("order-details/{orderId:guid}")]
    public async Task<IActionResult> GetOrderDetails(Guid orderId)
    {
        var orderClient = _httpClientFactory.CreateClient("OrderService");
        var productClient = _httpClientFactory.CreateClient("ProductCatalogService");

        // Get order
        var orderResponse = await orderClient.GetAsync($"/api/orders/{orderId}");
        if (!orderResponse.IsSuccessStatusCode) return NotFound("Order not found");

        var orderJson = await orderResponse.Content.ReadFromJsonAsync<JsonElement>();

        // Get product details for each item
        var enrichedItems = new List<object>();
        if (orderJson.TryGetProperty("items", out var items))
        {
            foreach (var item in items.EnumerateArray())
            {
                var productId = item.GetProperty("productId").GetString();
                object? productDetails = null;

                try
                {
                    var productResponse = await productClient.GetAsync($"/api/products/{productId}");
                    if (productResponse.IsSuccessStatusCode)
                        productDetails = await productResponse.Content.ReadFromJsonAsync<JsonElement>();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to fetch product {ProductId}", productId);
                }

                enrichedItems.Add(new
                {
                    ProductId = productId,
                    Quantity = item.GetProperty("quantity").GetInt32(),
                    UnitPrice = item.GetProperty("unitPrice").GetDecimal(),
                    ProductName = item.TryGetProperty("productName", out var pn) ? pn.GetString() : null,
                    ProductDetails = productDetails
                });
            }
        }

        return Ok(new
        {
            OrderId = orderJson.GetProperty("id").GetString(),
            CustomerEmail = orderJson.GetProperty("customerEmail").GetString(),
            Status = orderJson.GetProperty("status").ToString(),
            TotalAmount = orderJson.GetProperty("totalAmount").GetDecimal(),
            Items = enrichedItems
        });
    }

    [HttpGet("health")]
    public IActionResult Health() => Ok(new { Status = "Healthy", Service = "BFF" });
}
