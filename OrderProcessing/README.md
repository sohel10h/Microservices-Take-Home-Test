# Order Processing System

A simplified microservices-style order processing system built with .NET 8. Three logical services — Order, Payment, and Notification — are structured as separate projects within one ASP.NET Core application, communicate exclusively through an in-memory event bus, and use the Outbox Pattern for reliable event delivery.

---

## How to Run

**Requires:** .NET 8 SDK

```bash
git clone <repository-url>
cd OrderProcessing
dotnet run --project src/OrderProcessing.Api
```

Swagger UI opens at `http://localhost:5080/swagger` (or the URL shown in the terminal).

---

## Running Tests

```bash
dotnet test
```

All 9 tests should pass.

---

## Project Structure

```
OrderProcessing/
├── src/
│   ├── OrderProcessing.Domain/        ← entities, enums, event contracts, bus interface
│   ├── OrderProcessing.Application/   ← business logic, DTOs, event handlers
│   ├── OrderProcessing.Infrastructure/← EF Core, event bus, repositories
│   └── OrderProcessing.Api/           ← controllers, middleware, background workers
└── tests/
    └── OrderProcessing.Tests/         ← xUnit tests for services and event handlers
```

Each project references only what it needs:
- Domain has no dependencies
- Application references Domain only
- Infrastructure references Domain and Application
- Api references all three

---

## API Endpoints

| Method | Endpoint | Description |
|---|---|---|
| `POST` | `/api/orders` | Create a new order |
| `GET` | `/api/orders` | List all orders |
| `GET` | `/api/payments` | List all processed payments |
| `GET` | `/api/notifications` | List all sent notifications |

### Example

```bash
curl -X POST http://localhost:5080/api/orders \
  -H "Content-Type: application/json" \
  -d '{"customerName": "Jane Doe", "customerEmail": "jane@example.com", "amount": 99.99}'
```

Wait 2–4 seconds, then check `/api/payments` and `/api/notifications` to see the chain complete.

You can also pass an `operation_id` header to trace a request through the system:

```bash
curl -X POST http://localhost:5080/api/orders \
  -H "Content-Type: application/json" \
  -H "operation_id: my-trace-id-001" \
  -d '{"customerName": "Jane Doe", "customerEmail": "jane@example.com", "amount": 99.99}'
```

---

## Architecture Overview

The three services are fully decoupled — they never call each other directly. All cross-service communication flows through events:

```
POST /api/orders
  → OperationIdMiddleware (generates or reads operation_id header)
  → OrdersController → OrderService
      ├─ saves Order to database
      └─ saves OutboxMessage (topic: order.created, status: Pending)

  → OrderCreatedOutboxWorker (polls every 2s)
      └─ publishes OrderCreatedEvent to event bus

  → OrderCreatedEventHandler
      ├─ idempotency check (IncomingRequest table)
      └─ PaymentService.ProcessPaymentAsync
          ├─ saves Payment to database
          └─ saves OutboxMessage (topic: payment.succeeded, status: Pending)

  → PaymentSucceededOutboxWorker (polls every 2s)
      └─ publishes PaymentSucceededEvent to event bus

  → PaymentSucceededEventHandler
      ├─ idempotency check
      └─ NotificationService.SendNotificationAsync
          ├─ saves Notification to database
          ├─ logs notification message to console
          └─ saves OutboxMessage (topic: order.notification, status: Pending)

  → OrderNotificationOutboxWorker (polls every 2s)
      └─ publishes OrderNotificationEvent to event bus

  → OrderNotificationEventHandler
      ├─ idempotency check
      └─ logs final confirmation ← end of chain
```

---

## Design Decisions

**Why one application instead of three separate processes?**

The in-memory event bus only works within the same process. Splitting into three processes would require a real message broker (RabbitMQ, Kafka, Azure Service Bus). For this test the single-application approach keeps setup to zero while still demonstrating correct event-driven design. The `IInMemoryEventBus` abstraction means swapping in RabbitMQ later requires only a new Infrastructure implementation — nothing in Application or Domain changes.

**Why the Outbox Pattern?**

Publishing an event directly in the service call risks losing it if the process crashes between saving the entity and publishing the event. The Outbox Pattern saves both the business entity and the outbox message in the same `SaveChangesAsync` call. The background worker then publishes the event separately with retry support (up to 5 attempts). No events are silently lost.

**Why IncomingRequest for idempotency?**

Background workers can publish the same outbox row more than once if a transient failure occurs after publishing but before the row is marked completed. Without idempotency checks the consumer would process the same event twice — creating a duplicate payment or duplicate notification. The `IncomingRequest` table records each `EventName + OperationId` pair so handlers are safe to call multiple times.

**Why is OperationId propagated through the whole chain?**

Every event carries the same `OperationId` from the original HTTP request. This lets you correlate every log entry, every outbox message, and every incoming request record back to a single originating call — useful for debugging.

**Why are Payment and Notification controllers GET-only?**

Payment processing and notification sending are triggered by events, not by HTTP calls. A `POST /api/payments` endpoint would bypass the event-driven design and tightly couple the services. The GET endpoints exist only to inspect system state during testing.

---

## Known Limitations

- Data is stored in-memory and lost on application restart.
- No authentication or authorization on any endpoint.
- The in-memory event bus does not support multiple application instances — it works only within a single process.
- Payment processing is simulated and always succeeds.
- In a real multi-instance deployment, outbox workers would need distributed locking to prevent multiple instances from processing the same row.
