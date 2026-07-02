namespace OrderService.Events;

public record OrderPlaced(Guid OrderId, string CustomerEmail, List<OrderItemEvent> Items, string CorrelationId);
public record OrderItemEvent(Guid ProductId, string ProductName, int Quantity, decimal UnitPrice);
public record InventoryReserved(Guid OrderId, string CorrelationId);
public record InventoryRejected(Guid OrderId, string Reason, string CorrelationId);
public record OrderConfirmed(Guid OrderId, string CustomerEmail, string CorrelationId);
public record OrderRejected(Guid OrderId, string CustomerEmail, string Reason, string CorrelationId);
