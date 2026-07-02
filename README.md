# E-Commerce Microservices — Final Project

A production-style distributed system demonstrating the journey from monolith to microservices.

## Quick Start

```bash
docker compose up --build
```

## Architecture

- **API Gateway (YARP)** — Port 5000 (all client traffic enters here)
- **Nginx Load Balancer** — Port 8080 (ProductCatalog round-robin demo)
- **RabbitMQ Management** — Port 15672 (guest/guest)
- **Seq Log Aggregator** — Port 8081

## Services

| Service | Database | Port (internal) |
|---------|----------|----------------|
| OrderService | PostgreSQL | 8080 |
| ProductCatalogService (x2) | MongoDB | 8080 |
| InventoryService | PostgreSQL | 8080 |
| NotificationService | Redis | 8080 |
| BFF | — (aggregator) | 8080 |

## API Usage (via Gateway on port 5000)

### 1. Create a product
```bash
curl -X POST http://localhost:5000/api/products \
  -H "Content-Type: application/json" \
  -d '{"name":"Laptop","description":"Gaming laptop","price":1299.99,"category":"Electronics","attributes":{"ram":"16GB","cpu":"i7"}}'
```

### 2. Set inventory for the product
```bash
curl -X POST http://localhost:5000/api/inventory \
  -H "Content-Type: application/json" \
  -d '{"productId":"<PRODUCT_ID>","quantity":10}'
```

### 3. Place an order (starts the saga)
```bash
curl -X POST http://localhost:5000/api/orders \
  -H "Content-Type: application/json" \
  -d '{"customerEmail":"test@example.com","items":[{"productId":"<PRODUCT_ID>","productName":"Laptop","quantity":1,"unitPrice":1299.99}]}'
```

### 4. Check order status
```bash
curl http://localhost:5000/api/orders/<ORDER_ID>
```

### 5. Check notifications
```bash
curl http://localhost:5000/api/notifications/test@example.com
```

### 6. BFF — Aggregated order details
```bash
curl http://localhost:5000/bff/order-details/<ORDER_ID>
```

### 7. Prove load balancing (call multiple times, check ContainerId)
```bash
curl http://localhost:5000/api/products/health
```

## Demonstrating the Compensation Path

Place an order with quantity > available stock:
```bash
curl -X POST http://localhost:5000/api/orders \
  -H "Content-Type: application/json" \
  -d '{"customerEmail":"test@example.com","items":[{"productId":"<PRODUCT_ID>","productName":"Laptop","quantity":999,"unitPrice":1299.99}]}'
```
The order will be created as "Pending", then rejected by InventoryService, and the customer will be notified of the rejection.

## Correlation ID Tracing

Open Seq at http://localhost:8081 and filter by `CorrelationId` to see a full saga traced across all services.

## Project Structure

```
├── docker-compose.yml          # Single command to run everything
├── nginx/nginx.conf            # Load balancer config
├── docs/
│   ├── architecture.md         # ADRs, diagrams, tech decisions
│   └── stage1-monolith.md     # Monolith documentation
└── src/
    ├── Monolith/              # Stage 1 (preserved for comparison)
    ├── Services/
    │   ├── OrderService/       # PostgreSQL, RabbitMQ publisher/consumer
    │   ├── ProductCatalogService/ # MongoDB, Redis cache
    │   ├── InventoryService/   # PostgreSQL, RabbitMQ consumer
    │   └── NotificationService/ # Redis, RabbitMQ consumer
    ├── ApiGateway/            # YARP reverse proxy
    └── BFF/                   # Backend for Frontend aggregator
```

## Technologies

| Concern | Technology | Why |
|---------|-----------|-----|
| Gateway | YARP | .NET native, faster than Ocelot, MS-maintained |
| Load Balancer | Nginx | Proven, simple config |
| Messaging | RabbitMQ | Reliable, supports topic exchange for saga |
| Cache | Redis | Sub-ms latency, cache-aside pattern |
| Document DB | MongoDB | Flexible schema for product attributes |
| Relational DB | PostgreSQL | ACID for orders and inventory |
| Key-Value DB | Redis | Fast notifications storage with TTL |
| Logging | Serilog → Seq | Structured, centralized, queryable |
