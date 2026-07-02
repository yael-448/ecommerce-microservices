# Stage 1 - Monolith Documentation

## Architecture Diagram

```
┌─────────────────────────────────────────────┐
│              Client (Browser/Postman)         │
└─────────────────────┬───────────────────────┘
                      │ HTTP
                      ▼
┌─────────────────────────────────────────────┐
│         ECommerce.Monolith API (.NET 8)      │
│                  Port 5000                    │
│  ┌───────────┬───────────┬───────────────┐  │
│  │ Products  │  Orders   │  Inventory    │  │
│  │Controller │Controller │  Controller   │  │
│  └───────────┴───────────┴───────────────┘  │
│  ┌─────────────────────────────────────────┐│
│  │          Entity Framework Core           ││
│  └─────────────────────┬───────────────────┘│
└────────────────────────┼────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────┐
│        PostgreSQL Database (port 5432)        │
│  Tables: Products, Inventory, Orders,        │
│          OrderItems                           │
└─────────────────────────────────────────────┘
```

## API Endpoints

| Method | Endpoint                    | Description                          |
|--------|-----------------------------|--------------------------------------|
| GET    | /api/products               | List all products                    |
| GET    | /api/products/{id}          | Get product by ID                    |
| POST   | /api/products               | Create product (with initial stock)  |
| GET    | /api/orders/{id}            | Get order by ID                      |
| POST   | /api/orders                 | Place an order (reserves inventory)  |
| GET    | /api/inventory/{productId}  | Check stock for a product            |

## 3 Scalability Problems with This Architecture

### 1. Single Point of Failure (SPOF)
The entire application is a single process. If the API crashes, ALL functionality (products, orders, inventory) goes down simultaneously. There is no isolation between domains — a memory leak in product listing affects order processing.

### 2. Cannot Scale Independently
Product browsing typically has 10x-100x more traffic than order placement. In a monolith, you must scale the ENTIRE application even if only the product catalog needs more instances. This wastes resources and money.

### 3. Database Bottleneck
All domains share one PostgreSQL instance. Heavy inventory updates lock rows that order processing needs. A slow product search query can exhaust connection pool slots needed for time-sensitive order transactions. As data grows, you cannot optimize the database schema independently per domain.
