namespace ECommerce.Monolith.DTOs;

public record CreateProductRequest(string Name, string Description, decimal Price, string Category, int InitialStock);
public record ProductResponse(Guid Id, string Name, string Description, decimal Price, string Category, DateTime CreatedAt);
public record CreateOrderRequest(string CustomerEmail, List<OrderItemRequest> Items);
public record OrderItemRequest(Guid ProductId, int Quantity);
public record OrderResponse(Guid Id, string CustomerEmail, string Status, decimal TotalAmount, DateTime CreatedAt, List<OrderItemResponse> Items);
public record OrderItemResponse(Guid ProductId, string ProductName, int Quantity, decimal UnitPrice);
public record InventoryResponse(Guid ProductId, string ProductName, int Quantity);
