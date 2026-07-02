# Architecture Document — E-Commerce Microservices System

## Final Architecture Diagram

```
                          ┌──────────────────┐
                          │   Client/Browser  │
                          └────────┬─────────┘
                                   │
                          ┌────────▼─────────┐
                          │   API Gateway     │
                          │     (YARP)        │
                          │    Port 5000      │
                          └──┬───┬───┬───┬───┘
                             │   │   │   │
              ┌──────────────┘   │   │   └──────────────────┐
              │                  │   │                       │
    ┌─────────▼──────┐  ┌──────▼───▼─────┐  ┌─────────────▼───────┐
    │  BFF Service    │  │  OrderService   │  │  NotificationService │
    │  (Aggregator)   │  │  Port 8080      │  │  Port 8080           │
    └────────┬────────┘  └───────┬─────────┘  └──────────┬──────────┘
             │                   │                        │
             │           ┌───────▼─────────┐              │
             │           │   PostgreSQL     │              │
             │           │   (orders DB)    │              │
             │           └─────────────────┘              │
             │                                            │
    ┌────────▼────────────────────────────────────────────▼──────┐
    │                     RabbitMQ (Message Broker)               │
    │  Exchanges: orders (topic)                                  │
    │  Queues: inventory-reserve, order-saga-responses,           │
    │          notifications                                      │
    └───────────────────────┬────────────────────────────────────┘
                            │
              ┌─────────────▼──────────────┐
              │      InventoryService       │
              │      Port 8080              │
              └─────────────┬──────────────┘
                            │
              ┌─────────────▼──────────────┐
              │      PostgreSQL             │
              │      (inventory DB)         │
              └────────────────────────────┘

    ┌─────────────────────────────────────────────────────────┐
    │            Nginx Load Balancer (Port 8080)               │
    │                     Round Robin                           │
    └────────────┬───────────────────────────┬────────────────┘
                 │                           │
    ┌────────────▼────────┐     ┌────────────▼────────┐
    │ ProductCatalog-1     │     │ ProductCatalog-2     │
    │ (replica 1)          │     │ (replica 2)          │
    └──────────┬───────────┘     └──────────┬──────────┘
               │                            │
    ┌──────────▼────────────────────────────▼──────────┐
    │                  MongoDB                          │
    │            (productcatalog DB)                    │
    └──────────────────────────────────────────────────┘
               │
    ┌──────────▼──────────────────────────────────────┐
    │                   Redis                          │
    │   (Cache for ProductCatalog + NotificationStore) │
    └─────────────────────────────────────────────────┘

    ┌──────────────────────────────────────────────────┐
    │          Seq (Structured Log Aggregator)          │
    │               Port 8081 (UI)                     │
    └──────────────────────────────────────────────────┘
```

---

## Architecture Decision Records (ADRs)

### ADR-001: OrderService → PostgreSQL (Relational)

**Context:** Orders involve financial transactions with multiple items, requiring atomic operations across tables (Orders + OrderItems).

**Decision:** Use PostgreSQL as a relational database.

**Rationale:**
- **ACID compliance** is mandatory for financial data. When creating an order, we must atomically: insert the order, insert all order items, and update the status — all within a single transaction.
- Orders have a **fixed, predictable schema** — OrderId, CustomerEmail, Status, TotalAmount, Items[]. No schema flexibility needed.
- **Strong consistency** (CP in CAP theorem) — we can never have a "phantom order" that was half-written.
- Referential integrity between Orders and OrderItems via foreign keys prevents orphan records.

**Consequences:** Cannot horizontally scale writes easily, but order volume is typically lower than product reads.

---

### ADR-002: ProductCatalogService → MongoDB (Document Store)

**Context:** Product catalogs have varying attributes per category (electronics have "RAM", "CPU" while clothing has "size", "color", "material").

**Decision:** Use MongoDB as a document database.

**Rationale:**
- **Flexible schema** — each product document can have different attributes without ALTER TABLE migrations. Adding a new product category doesn't require schema changes.
- **Read-heavy workload** — catalogs are read 100x more than written. MongoDB's document model allows fetching a complete product in a single read (no JOINs).
- **BASE model** — eventual consistency is acceptable for catalog data. Seeing a slightly stale product description for 50ms is harmless.
- **AP in CAP theorem** — we prioritize availability and partition tolerance for catalog reads.
- Horizontal scaling via sharding when product catalog grows to millions.

**Consequences:** No ACID transactions across documents, but catalog operations are single-document writes.

---

### ADR-003: NotificationService → Redis (Key-Value Store)

**Context:** Notifications are transient messages with simple access patterns: write-once, read-by-key, auto-expire.

**Decision:** Use Redis as the primary data store for notifications.

**Rationale:**
- **Key-Value model** perfectly fits the access pattern: `notification:{orderId}` → notification JSON, `notifications:{email}` → list of notifications.
- **Sub-millisecond latency** — notifications should be instantly queryable after being stored.
- **TTL support** — notifications naturally expire; Redis's built-in TTL eliminates cleanup jobs.
- **BASE model** — losing a notification on crash is acceptable (it's informational, not transactional).
- Also serves as **distributed cache** for ProductCatalogService (dual purpose).

**Consequences:** Data is not durable by default (persistence can be configured but adds latency). No complex queries possible.

---

### ADR-004: YARP as API Gateway (instead of Ocelot)

**Context:** Need a reverse proxy/API gateway to route all client traffic.

**Decision:** Use YARP (Yet Another Reverse Proxy) by Microsoft.

**Rationale:**
- Native .NET integration — runs as middleware, not a separate process
- Higher performance than Ocelot (benchmarked 2-3x throughput)
- Actively maintained by Microsoft (Ocelot has fewer maintainers)
- Built-in load balancing policies (RoundRobin used for ProductCatalog)
- Configuration-driven routing via appsettings.json

---

## Saga Flow (Choreography-based)

```
OrderService          RabbitMQ           InventoryService      NotificationService
    │                    │                      │                      │
    │ OrderPlaced ──────►│                      │                      │
    │                    │ order.placed ────────►│                      │
    │                    │                      │ Reserve stock         │
    │                    │                      │                      │
    │                    │◄─ inventory.reserved ─│ (Happy path)         │
    │◄── inv.reserved ───│                      │                      │
    │ Confirm order      │                      │                      │
    │                    │                      │                      │
    │ OrderConfirmed ───►│                      │                      │
    │                    │ order.confirmed ─────────────────────────────►│
    │                    │                      │                      │ Notify customer
    │                    │                      │                      │
    
    === COMPENSATION (Failure Path) ===
    
    │                    │◄─ inventory.rejected ─│ (Insufficient stock) │
    │◄── inv.rejected ───│                      │                      │
    │ Reject order       │                      │                      │
    │                    │                      │                      │
    │ OrderRejected ────►│                      │                      │
    │                    │ order.rejected ──────────────────────────────►│
    │                    │                      │                      │ Notify rejection
```

---

## Technology Stack Summary

| Component | Technology | NoSQL Family | CAP | Consistency Model |
|-----------|-----------|--------------|-----|-------------------|
| OrderService DB | PostgreSQL | N/A (Relational) | CP | Strong (ACID) |
| ProductCatalog DB | MongoDB | Document | AP | Eventual (BASE) |
| Notification Store | Redis | Key-Value | AP | Eventual (BASE) |
| Cache | Redis | Key-Value | AP | Eventual |
| Message Broker | RabbitMQ | N/A | — | At-least-once delivery |
| API Gateway | YARP | — | — | — |
| Load Balancer | Nginx | — | — | Round Robin |
| Log Aggregator | Seq | — | — | — |
| Logging | Serilog | — | — | Structured |

---

## Correlation ID Strategy

Every request generates a unique `CorrelationId` (GUID) at the OrderService when an order is placed. This ID:
1. Is logged by OrderService when creating the order
2. Is embedded in the RabbitMQ message properties (`BasicProperties.CorrelationId`)
3. Is extracted by InventoryService when processing the reservation
4. Is forwarded in the response event (InventoryReserved/Rejected)
5. Is extracted again by OrderService when confirming/rejecting
6. Is forwarded to NotificationService in the final event

This allows querying Seq with a single CorrelationId to see the complete saga journey across all services.
