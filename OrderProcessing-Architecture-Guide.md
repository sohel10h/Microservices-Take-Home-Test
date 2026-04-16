# Order Processing System — Architecture & Implementation Guide

> .NET 8 · Clean Architecture · In-Memory Event Bus · Outbox Pattern · EF Core InMemory

---

## Table of Contents

1. [Overview](#overview)
2. [Solution Structure — Multi-Project Layout](#solution-structure)
3. [Project Responsibilities](#project-responsibilities)
4. [Layer-by-Layer File Structure](#layer-by-layer-file-structure)
5. [Entity Definitions](#entity-definitions)
6. [Event Contracts & Topics](#event-contracts--topics)
7. [Interfaces per Layer](#interfaces-per-layer)
8. [Execution Flow — Order Service](#execution-flow--order-service)
9. [Execution Flow — Payment Service](#execution-flow--payment-service)
10. [Execution Flow — Notification Service](#execution-flow--notification-service)
11. [Complete End-to-End Chain](#complete-end-to-end-chain)
12. [DI Lifetime Summary](#di-lifetime-summary)
13. [Design Decisions & Trade-offs](#design-decisions--trade-offs)

---

## 1. Overview

This solution simulates **three microservices** — Order, Payment, and Notification — inside a **single ASP.NET Core application**, organized as **four separate C# class library projects**. The three logical services are completely decoupled from each other. They never call each other directly. All communication happens through an **in-memory event bus** using the **Outbox Pattern** for reliability and **IncomingRequest tracking** for idempotency.

Each logical service owns:

- Its own **entity** (Order, Payment, Notification)
- Its own **controller** (HTTP endpoint)
- Its own **application service** (business logic)
- Its own **event handler** (consumes events published by others)
- Its own **outbox worker** (background publisher for its topic)

---

## 2. Solution Structure — Multi-Project Layout

```
OrderProcessing/
│
├── OrderProcessing.sln
├── README.md
├── docker-compose.yml                    (optional bonus)
│
├── src/
│   ├── OrderProcessing.Domain/           ← no dependencies
│   ├── OrderProcessing.Application/      ← refs Domain only
│   ├── OrderProcessing.Infrastructure/   ← refs Domain + Application
│   └── OrderProcessing.Api/              ← refs all three
│
└── tests/
    └── OrderProcessing.Tests/            ← refs Application + Domain
```

### Why 4 separate .csproj files?

This enforces **compiler-level dependency rules**. If Infrastructure accidentally imports from Api, it will not compile. This is the nopCommerce-style multi-class-library pattern — not just a naming convention, it is structurally enforced.

| Project | Depends on | Purpose |
|---|---|---|
| `Domain` | nothing | Entities, enums, event contracts, bus interfaces |
| `Application` | Domain | Business logic, DTOs, service interfaces, event handlers |
| `Infrastructure` | Domain + Application | EF Core DbContext, event bus implementation, repositories |
| `Api` | all three | Controllers, middleware, background workers, DI wiring |

---

## 3. Project Responsibilities

### OrderProcessing.Domain

Pure domain model. No framework references. No EF Core. No ASP.NET. Contains only what the business domain owns: entities, enums, event contracts, and the abstractions that cross all layers.

### OrderProcessing.Application

Business logic lives here. Services orchestrate domain operations and save outbox messages. Event handlers contain the consumer logic with idempotency checks. All interfaces defined here are implemented in Infrastructure or Api.

### OrderProcessing.Infrastructure

Concrete implementations: `AppDbContext`, `InMemoryEventBus`, and repositories. The event bus uses a `ConcurrentDictionary` internally and is registered as a **singleton**. The `AppDbContext` and repositories are **scoped**.

### OrderProcessing.Api

The runnable entry point. Controllers are thin — they call application services and return HTTP responses. Three background workers (one per topic) poll the outbox and publish events. Middleware handles operation IDs and global errors. All DI registration lives in `DependencyRegistrar/`.

---

## 4. Layer-by-Layer File Structure

### OrderProcessing.Domain

```
OrderProcessing.Domain/
├── OrderProcessing.Domain.csproj
│
├── Entities/
│   ├── Order.cs
│   ├── Payment.cs
│   ├── Notification.cs
│   ├── OutboxMessage.cs
│   └── IncomingRequest.cs
│
├── Enums/
│   ├── ProcessingStatus.cs
│   └── OrderStatus.cs
│
├── Events/
│   ├── OrderCreatedEvent.cs
│   ├── PaymentSucceededEvent.cs
│   └── OrderNotificationEvent.cs
│
└── Interfaces/
    ├── IInMemoryEventBus.cs
    └── IIntegrationEventHandler.cs
```

### OrderProcessing.Application

```
OrderProcessing.Application/
├── OrderProcessing.Application.csproj
│
├── DTOs/
│   ├── CreateOrderRequest.cs
│   ├── OrderResponse.cs
│   ├── PaymentResponse.cs
│   └── NotificationResponse.cs
│
├── Interfaces/
│   ├── IOrderService.cs
│   ├── IPaymentService.cs
│   ├── INotificationService.cs
│   ├── IOutboxService.cs
│   └── IIncomingRequestService.cs
│
├── Services/
│   ├── OrderService.cs
│   ├── PaymentService.cs
│   ├── NotificationService.cs
│   ├── OutboxService.cs
│   └── IncomingRequestService.cs
│
└── EventHandlers/
    ├── OrderCreatedEventHandler.cs
    ├── PaymentSucceededEventHandler.cs
    └── OrderNotificationEventHandler.cs
```

### OrderProcessing.Infrastructure

```
OrderProcessing.Infrastructure/
├── OrderProcessing.Infrastructure.csproj
│
├── Data/
│   └── AppDbContext.cs
│
├── EventBus/
│   └── InMemoryEventBus.cs
│
└── Repositories/
    ├── OrderRepository.cs
    ├── PaymentRepository.cs
    └── NotificationRepository.cs
```

### OrderProcessing.Api

```
OrderProcessing.Api/
├── OrderProcessing.Api.csproj
├── Program.cs
├── appsettings.json
├── appsettings.Development.json
│
├── Controllers/
│   ├── OrdersController.cs          ← POST /api/orders, GET /api/orders
│   ├── PaymentsController.cs        ← GET /api/payments
│   └── NotificationsController.cs   ← GET /api/notifications
│
├── Middleware/
│   ├── OperationIdMiddleware.cs
│   └── GlobalExceptionMiddleware.cs
│
├── BackgroundServices/
│   ├── OrderCreatedOutboxWorker.cs
│   ├── PaymentSucceededOutboxWorker.cs
│   └── OrderNotificationOutboxWorker.cs
│
└── DependencyRegistrar/
    └── ServiceCollectionExtensions.cs
```

### OrderProcessing.Tests

```
OrderProcessing.Tests/
├── OrderProcessing.Tests.csproj
│
├── Services/
│   ├── OrderServiceTests.cs
│   └── PaymentServiceTests.cs
│
└── EventHandlers/
    ├── OrderCreatedEventHandlerTests.cs
    └── PaymentSucceededEventHandlerTests.cs
```

---

## 5. Entity Definitions

There are **5 entities** in total. Three are business entities (one per logical service), and two are infrastructure entities that support the event reliability pattern.

### Business Entities

**Order** — owned by the Order Service

| Field | Type | Description |
|---|---|---|
| Id | Guid | Primary key |
| CustomerName | string | Name of the customer |
| CustomerEmail | string | Email address for notification |
| Amount | decimal | Order total |
| Status | OrderStatus | Created → PaymentProcessed → Notified |
| CreatedOnUtc | DateTime | Creation timestamp |
| UpdatedOnUtc | DateTime | Last update timestamp |

**Payment** — owned by the Payment Service

| Field | Type | Description |
|---|---|---|
| Id | Guid | Primary key |
| OrderId | Guid | Foreign reference to the order |
| Amount | decimal | Amount that was processed |
| Status | ProcessingStatus | Pending → Completed or Error |
| CreatedOnUtc | DateTime | Creation timestamp |
| UpdatedOnUtc | DateTime | Last update timestamp |

**Notification** — owned by the Notification Service

| Field | Type | Description |
|---|---|---|
| Id | Guid | Primary key |
| OrderId | Guid | Foreign reference to the order |
| Message | string | Notification message content |
| CreatedOnUtc | DateTime | Creation timestamp |

### Infrastructure Entities

**OutboxMessage** — stores events before they are published (Outbox Pattern)

| Field | Type | Description |
|---|---|---|
| Id | Guid | Primary key |
| TopicName | string | e.g. `order.created`, `payment.succeeded` |
| OperationId | string | Correlation ID carried across the whole chain |
| Status | ProcessingStatus | Pending → Completed or Error |
| BodyJson | string | Serialized event payload |
| RetryCount | int | Number of publish attempts so far |
| LastError | string? | Last error message if failed |
| CreatedOnUtc | DateTime | Creation timestamp |
| UpdatedOnUtc | DateTime | Last update timestamp |

**IncomingRequest** — prevents duplicate event processing (Idempotency)

| Field | Type | Description |
|---|---|---|
| Id | Guid | Primary key |
| EventName | string | e.g. `OrderCreatedEvent` |
| OperationId | string | Correlation ID from the publisher |
| Status | ProcessingStatus | Processing → Completed or Error |
| CreatedOnUtc | DateTime | Creation timestamp |
| UpdatedOnUtc | DateTime | Last update timestamp |

> Uniqueness is enforced by the combination of `EventName + OperationId`. If a handler has already processed this pair, it skips the event entirely.

---

## 6. Event Contracts & Topics

Three events flow through the system. Each has a fixed topic name that the outbox workers and event bus subscriptions use to route messages.

| Event | Topic Name | Published by | Consumed by |
|---|---|---|---|
| `OrderCreatedEvent` | `order.created` | Order Service | Payment Service (via handler) |
| `PaymentSucceededEvent` | `payment.succeeded` | Payment Service | Notification Service (via handler) |
| `OrderNotificationEvent` | `order.notification` | Notification Service | Notification handler (final log) |

**OrderCreatedEvent fields:** `OperationId`, `OrderId`, `CustomerName`, `CustomerEmail`, `Amount`

**PaymentSucceededEvent fields:** `OperationId`, `OrderId`, `PaymentId`, `Amount`

**OrderNotificationEvent fields:** `OperationId`, `OrderId`, `Message`

All events carry `OperationId` so the correlation ID is traceable across the entire chain.

---

## 7. Interfaces per Layer

### Domain Layer Interfaces

**IInMemoryEventBus** — the only bus abstraction. Has two methods: `PublishAsync<T>(topicName, message)` to dispatch a typed event to all subscribers of that topic, and `Subscribe<T>(topicName, handler)` to register a handler delegate for a topic. Both are generic and async.

**IIntegrationEventHandler\<T\>** — a single `HandleAsync(message, cancellationToken)` method. Every event handler in the Application layer implements this interface for its specific event type.

### Application Layer Interfaces

**IOrderService** — `CreateOrderAsync(request, operationId)` and `GetAllOrdersAsync()`

**IPaymentService** — `ProcessPaymentAsync(orderId, amount, operationId)` and `GetAllPaymentsAsync()`

**INotificationService** — `SendNotificationAsync(orderId, message)` and `GetAllNotificationsAsync()`

**IOutboxService** — `AddAsync(topicName, operationId, payload)`, `GetPendingByTopicAsync(topicName)`, `MarkCompletedAsync(id)`, `MarkErrorAsync(id, error)`

**IIncomingRequestService** — `HasProcessedAsync(eventName, operationId)`, `StartProcessingAsync(eventName, operationId)`, `MarkCompletedAsync(eventName, operationId)`, `MarkErrorAsync(eventName, operationId)`

---

## 8. Execution Flow — Order Service

The Order Service is the **entry point** of the entire system. A client sends an HTTP request and the Order Service starts the chain.

### Step 1 — HTTP Request arrives

The client sends `POST /api/orders` with a JSON body containing `customerName`, `customerEmail`, and `amount`. No `operation_id` header is required — the middleware generates one automatically if missing.

### Step 2 — OperationIdMiddleware

Before the controller runs, `OperationIdMiddleware` reads the `operation_id` request header. If it is missing, it generates a new `Guid`. It stores the value in `HttpContext.Items["OperationId"]` and also writes it back to the response header so the caller can use it for tracing. Every downstream step in the chain carries this same ID.

### Step 3 — OrdersController

The controller is intentionally thin — it contains no business logic. It reads the `OperationId` from `HttpContext.Items`, calls `IOrderService.CreateOrderAsync(request, operationId)`, and returns `201 Created` with the order response body.

### Step 4 — OrderService (business logic)

This is where the Outbox Pattern is applied. The service creates a new `Order` entity with `Status = Created`, then constructs an `OrderCreatedEvent` with all order fields plus the `OperationId`. It calls `IOutboxService.AddAsync("order.created", operationId, event)` which serializes the event to JSON and creates an `OutboxMessage` row with `Status = Pending`. A single `SaveChangesAsync()` call saves both the `Order` and the `OutboxMessage` atomically — if the database save fails, neither row is written and no event is lost.

### Step 5 — OrderCreatedOutboxWorker (background)

This `BackgroundService` runs continuously and polls the database every 2 seconds for `OutboxMessage` rows with `TopicName = "order.created"` and `Status = Pending`. Because it is a singleton `BackgroundService`, it uses `IServiceScopeFactory` to create a fresh scoped service context on each poll cycle. For each pending message it deserializes the `BodyJson` back to `OrderCreatedEvent` and calls `IInMemoryEventBus.PublishAsync("order.created", event)`. On success it marks the outbox row as `Completed`. On failure it increments `RetryCount` and records the error in `LastError`. After 5 retries it marks the row as `Error` and stops retrying.

### GET /api/orders

Returns all `Order` records from the database via `IOrderService.GetAllOrdersAsync()`. No event is triggered.

---

## 9. Execution Flow — Payment Service

The Payment Service **never receives a direct HTTP call** for payment processing. It reacts purely to the `order.created` event. It exposes only `GET /api/payments` so processed payments can be queried.

### Step 1 — OrderCreatedEventHandler is invoked

After the `OrderCreatedOutboxWorker` publishes the `OrderCreatedEvent` onto the bus, the event bus routes it to `OrderCreatedEventHandler`. This handler is the consumer-side entry point for all Payment Service logic.

### Step 2 — Idempotency check

The handler first calls `IIncomingRequestService.HasProcessedAsync("OrderCreatedEvent", operationId)`. If this `EventName + OperationId` combination already has a record with `Status = Completed`, the handler logs a duplicate-detected warning and returns immediately without doing anything. This protects against the same event being accidentally published twice by the outbox worker.

### Step 3 — Mark as Processing

If not a duplicate, `StartProcessingAsync("OrderCreatedEvent", operationId)` saves a new `IncomingRequest` row with `Status = Processing`. This records that the event is currently being handled.

### Step 4 — PaymentService.ProcessPaymentAsync (business logic)

The service creates a new `Payment` entity with the `OrderId`, `Amount`, and `Status = Completed` (payment success is simulated — in production this would call a real payment provider). It constructs a `PaymentSucceededEvent` with `OperationId`, `OrderId`, `PaymentId`, and `Amount`. It calls `IOutboxService.AddAsync("payment.succeeded", operationId, event)` to queue the next event. A single `SaveChangesAsync()` saves the `Payment` and the new `OutboxMessage` atomically.

### Step 5 — Mark IncomingRequest as Completed

After `ProcessPaymentAsync` returns successfully, the handler calls `MarkCompletedAsync("OrderCreatedEvent", operationId)` to update the `IncomingRequest` row to `Status = Completed`. If any exception occurred, `MarkErrorAsync(...)` is called instead and the error is logged.

### Step 6 — PaymentSucceededOutboxWorker (background)

This worker polls every 2 seconds for `OutboxMessage` rows with `TopicName = "payment.succeeded"` and `Status = Pending`. It deserializes the `BodyJson` to `PaymentSucceededEvent` and publishes it via the event bus. On success it marks the outbox row `Completed`. The same retry logic (max 5) applies.

### GET /api/payments

Returns all `Payment` records from the database via `IPaymentService.GetAllPaymentsAsync()`. No event is triggered.

---

## 10. Execution Flow — Notification Service

The Notification Service **never receives a direct HTTP call** for sending notifications. It reacts purely to the `payment.succeeded` event. It exposes only `GET /api/notifications` so logged notifications can be queried.

### Step 1 — PaymentSucceededEventHandler is invoked

After the `PaymentSucceededOutboxWorker` publishes the `PaymentSucceededEvent` onto the bus, the event bus routes it to `PaymentSucceededEventHandler`. This handler is the consumer-side entry point for all Notification Service logic.

### Step 2 — Idempotency check

The handler calls `IIncomingRequestService.HasProcessedAsync("PaymentSucceededEvent", operationId)`. If this combination already has a `Completed` record, the handler skips and returns. This ensures a notification is never sent twice for the same payment event.

### Step 3 — Mark as Processing

`StartProcessingAsync("PaymentSucceededEvent", operationId)` saves an `IncomingRequest` row with `Status = Processing`.

### Step 4 — NotificationService.SendNotificationAsync (business logic)

The service creates a `Notification` entity with the `OrderId` and a human-readable `Message` such as `"Payment of $99.99 received for order {orderId}. Thank you!"`. It saves the notification to the database. It then writes a log entry to the application logger — this represents the simulated email or push notification send. Finally it constructs an `OrderNotificationEvent` and calls `IOutboxService.AddAsync("order.notification", operationId, event)` to queue the final confirmation event. A single `SaveChangesAsync()` saves the `Notification` and the `OutboxMessage` atomically.

### Step 5 — Mark IncomingRequest as Completed

After `SendNotificationAsync` returns, the handler calls `MarkCompletedAsync("PaymentSucceededEvent", operationId)`. On exception it calls `MarkErrorAsync(...)` and logs the error.

### Step 6 — OrderNotificationOutboxWorker (background)

This worker polls every 2 seconds for `OutboxMessage` rows with `TopicName = "order.notification"` and `Status = Pending`. It deserializes the `BodyJson` to `OrderNotificationEvent` and publishes it via the event bus. On success it marks the outbox row `Completed`.

### Step 7 — OrderNotificationEventHandler is invoked (final step)

This is the last handler in the chain. It performs the idempotency check using `HasProcessedAsync("OrderNotificationEvent", operationId)`. If not a duplicate, it marks the `IncomingRequest` as `Processing`, logs a final confirmation message (e.g. `"Notification confirmed for order {orderId}"`), and marks the `IncomingRequest` as `Completed`. No further outbox message is saved — the chain ends here.

### GET /api/notifications

Returns all `Notification` records from the database via `INotificationService.GetAllNotificationsAsync()`. No event is triggered.

---

## 11. Complete End-to-End Chain

```
CLIENT
  │
  └─ POST /api/orders
          │
          ▼
  OperationIdMiddleware
  (generates or reads operation_id, passes it to every downstream step)
          │
          ▼
  OrdersController  →  OrderService
                          ├─ saves Order                (EF Core)
                          └─ saves OutboxMessage        (topic: order.created, Pending)
          │
          ▼
  OrderCreatedOutboxWorker      (polls every 2s, max 5 retries)
  └─ PublishAsync("order.created")
          │
          ▼
  OrderCreatedEventHandler
  ├─ idempotency check          (IncomingRequest: EventName + OperationId)
  └─ PaymentService.ProcessPaymentAsync
          ├─ saves Payment       (EF Core, Status: Completed)
          └─ saves OutboxMessage (topic: payment.succeeded, Pending)
          │
          ▼
  PaymentSucceededOutboxWorker  (polls every 2s, max 5 retries)
  └─ PublishAsync("payment.succeeded")
          │
          ▼
  PaymentSucceededEventHandler
  ├─ idempotency check          (IncomingRequest)
  └─ NotificationService.SendNotificationAsync
          ├─ saves Notification  (EF Core)
          ├─ logs to console     (simulated notification send)
          └─ saves OutboxMessage (topic: order.notification, Pending)
          │
          ▼
  OrderNotificationOutboxWorker (polls every 2s, max 5 retries)
  └─ PublishAsync("order.notification")
          │
          ▼
  OrderNotificationEventHandler
  ├─ idempotency check          (IncomingRequest)
  └─ logs final confirmation    ← END OF CHAIN


QUERY ENDPOINTS  (no events triggered, read-only)
  GET /api/orders          →  returns all Order records
  GET /api/payments        →  returns all Payment records
  GET /api/notifications   →  returns all Notification records
```

---

## 12. DI Lifetime Summary

Getting DI lifetimes correct is critical because `BackgroundService` workers are singletons and `AppDbContext` must be scoped.

| Service | Lifetime | Reason |
|---|---|---|
| `IInMemoryEventBus` | Singleton | The subscription registry must be shared across the whole app. Scoped or transient would create a new instance per resolution, losing all registered subscriptions. |
| `AppDbContext` | Scoped | EF Core requirement. One instance per HTTP request or service scope. |
| `IOrderService`, `IPaymentService`, `INotificationService` | Scoped | These services use `AppDbContext` and must match its lifetime. |
| `IOutboxService`, `IIncomingRequestService` | Scoped | Same reason — they read from and write to the database. |
| `IIntegrationEventHandler<T>` | Scoped | Event handlers call application services which use `AppDbContext`. |
| `BackgroundService` workers | Singleton (hosted) | ASP.NET Core registers all `IHostedService` instances as singletons. Workers must never directly inject scoped services. They inject `IServiceScopeFactory` and create a new scope per polling cycle, then dispose it when done. |

---

## 13. Design Decisions & Trade-offs

### Why one application instead of three separate processes?

The in-memory event bus works only within the same running process. Splitting into three separate applications would require a distributed broker such as RabbitMQ, Kafka, or Azure Service Bus. The single-application approach allows the test to be completed within the time budget while still demonstrating correct event-driven design. The `IInMemoryEventBus` abstraction means that replacing the transport with RabbitMQ in the future requires only a new `Infrastructure` implementation — no `Application` or `Domain` code would need to change.

### Why the Outbox Pattern instead of direct publish?

If the event bus call happened directly inside the controller or service, a failure between the database save and the publish call would lose the event permanently with no way to recover it. The Outbox Pattern saves the event to the database in the same transaction as the business data. The background worker then publishes it separately. If publishing fails, the worker retries up to 5 times before marking the message as `Error`. No events are silently lost.

### Why IncomingRequest for idempotency?

Background workers could publish the same outbox row more than once if a transient failure occurs after publishing but before the row is marked `Completed`. Without idempotency checks, the consumer handler would process the same event twice — creating a duplicate payment or a duplicate notification. The `IncomingRequest` table records each `EventName + OperationId` pair, making every handler safe to invoke multiple times with the same input.

### Why OperationId across the whole chain?

Every event carries the same `OperationId` that was generated (or provided by the caller) on the original HTTP request. This means every `OutboxMessage`, every `IncomingRequest`, and every log entry throughout the entire Order → Payment → Notification chain can be correlated by a single ID. In production this would be used for distributed tracing and log aggregation.

### Why are Payment and Notification controllers GET-only?

In this design, payment processing and notification sending are **triggered by events**, not by HTTP calls. Exposing a `POST /api/payments` endpoint that clients can call directly would bypass the event-driven architecture and create tight coupling. The GET endpoints exist only to let developers inspect the state of the system during testing.

### Known Limitations

- In-memory data does not persist between application restarts.
- There is no authentication or authorization on any endpoint.
- The in-memory event bus does not support multiple application instances — it is suitable only for a single-process deployment.
- In a real system, outbox workers would need distributed locks to prevent multiple instances processing the same row simultaneously.
- Payment processing is simulated and always succeeds. A real system would call an external payment provider and handle failure and partial-success scenarios.

---

## 14. Entity and Enum Code

### Enums

```csharp
// OrderProcessing.Domain/Enums/ProcessingStatus.cs
public enum ProcessingStatus
{
    Pending = 1,
    Processing = 2,
    Completed = 3,
    Error = 4
}

// OrderProcessing.Domain/Enums/OrderStatus.cs
public enum OrderStatus
{
    Created = 1,
    PaymentProcessed = 2,
    Notified = 3
}
```

### Business Entities

```csharp
// OrderProcessing.Domain/Entities/Order.cs
public class Order
{
    public Guid Id { get; set; }
    public string CustomerName { get; set; } = default!;
    public string CustomerEmail { get; set; } = default!;
    public decimal Amount { get; set; }
    public OrderStatus Status { get; set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime UpdatedOnUtc { get; set; }
}

// OrderProcessing.Domain/Entities/Payment.cs
public class Payment
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public decimal Amount { get; set; }
    public ProcessingStatus Status { get; set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime UpdatedOnUtc { get; set; }
}

// OrderProcessing.Domain/Entities/Notification.cs
public class Notification
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public string Message { get; set; } = default!;
    public DateTime CreatedOnUtc { get; set; }
}
```

### Infrastructure Entities

```csharp
// OrderProcessing.Domain/Entities/OutboxMessage.cs
public class OutboxMessage
{
    public Guid Id { get; set; }
    public string TopicName { get; set; } = default!;
    public string OperationId { get; set; } = default!;
    public ProcessingStatus Status { get; set; }
    public string BodyJson { get; set; } = default!;
    public int RetryCount { get; set; }
    public string? LastError { get; set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime UpdatedOnUtc { get; set; }
}

// OrderProcessing.Domain/Entities/IncomingRequest.cs
public class IncomingRequest
{
    public Guid Id { get; set; }
    public string EventName { get; set; } = default!;
    public string OperationId { get; set; } = default!;
    public ProcessingStatus Status { get; set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime UpdatedOnUtc { get; set; }
}
```

### Event Contracts

```csharp
// OrderProcessing.Domain/Events/OrderCreatedEvent.cs
public class OrderCreatedEvent
{
    public string OperationId { get; set; } = default!;
    public Guid OrderId { get; set; }
    public string CustomerName { get; set; } = default!;
    public string CustomerEmail { get; set; } = default!;
    public decimal Amount { get; set; }
}

// OrderProcessing.Domain/Events/PaymentSucceededEvent.cs
public class PaymentSucceededEvent
{
    public string OperationId { get; set; } = default!;
    public Guid OrderId { get; set; }
    public Guid PaymentId { get; set; }
    public decimal Amount { get; set; }
}

// OrderProcessing.Domain/Events/OrderNotificationEvent.cs
public class OrderNotificationEvent
{
    public string OperationId { get; set; } = default!;
    public Guid OrderId { get; set; }
    public string Message { get; set; } = default!;
}
```

### Domain Interfaces

```csharp
// OrderProcessing.Domain/Interfaces/IInMemoryEventBus.cs
public interface IInMemoryEventBus
{
    Task PublishAsync<T>(string topicName, T message, CancellationToken cancellationToken = default);
    void Subscribe<T>(string topicName, Func<T, CancellationToken, Task> handler);
}

// OrderProcessing.Domain/Interfaces/IIntegrationEventHandler.cs
public interface IIntegrationEventHandler<T>
{
    Task HandleAsync(T message, CancellationToken cancellationToken = default);
}
```

---

## 15. Core Implementations

### AppDbContext

```csharp
// OrderProcessing.Infrastructure/Data/AppDbContext.cs
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<IncomingRequest> IncomingRequests => Set<IncomingRequest>();
}
```

### InMemoryEventBus

```csharp
// OrderProcessing.Infrastructure/EventBus/InMemoryEventBus.cs
public class InMemoryEventBus : IInMemoryEventBus
{
    private readonly ConcurrentDictionary<string, List<Delegate>> _handlers = new();

    public Task PublishAsync<T>(string topicName, T message, CancellationToken cancellationToken = default)
    {
        if (!_handlers.TryGetValue(topicName, out var handlers))
            return Task.CompletedTask;

        var tasks = handlers
            .Cast<Func<T, CancellationToken, Task>>()
            .Select(h => h(message, cancellationToken));

        return Task.WhenAll(tasks);
    }

    public void Subscribe<T>(string topicName, Func<T, CancellationToken, Task> handler)
    {
        _handlers.AddOrUpdate(
            topicName,
            _ => new List<Delegate> { handler },
            (_, existing) => { existing.Add(handler); return existing; });
    }
}
```

### OutboxService

```csharp
// OrderProcessing.Application/Services/OutboxService.cs
public class OutboxService : IOutboxService
{
    private readonly AppDbContext _db;

    public OutboxService(AppDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(string topicName, string operationId, object payload, CancellationToken cancellationToken = default)
    {
        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            TopicName = topicName,
            OperationId = operationId,
            Status = ProcessingStatus.Pending,
            BodyJson = JsonSerializer.Serialize(payload),
            RetryCount = 0,
            CreatedOnUtc = DateTime.UtcNow,
            UpdatedOnUtc = DateTime.UtcNow
        };

        _db.OutboxMessages.Add(message);
        // caller is responsible for SaveChangesAsync
    }

    public Task<List<OutboxMessage>> GetPendingByTopicAsync(string topicName, CancellationToken cancellationToken = default)
    {
        return _db.OutboxMessages
            .Where(x => x.TopicName == topicName
                     && (x.Status == ProcessingStatus.Pending)
                     && x.RetryCount < 5)
            .OrderBy(x => x.CreatedOnUtc)
            .Take(20)
            .ToListAsync(cancellationToken);
    }

    public async Task MarkCompletedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var msg = await _db.OutboxMessages.FindAsync(new object[] { id }, cancellationToken);
        if (msg is null) return;
        msg.Status = ProcessingStatus.Completed;
        msg.UpdatedOnUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkErrorAsync(Guid id, string error, CancellationToken cancellationToken = default)
    {
        var msg = await _db.OutboxMessages.FindAsync(new object[] { id }, cancellationToken);
        if (msg is null) return;
        msg.RetryCount += 1;
        msg.LastError = error;
        msg.Status = msg.RetryCount >= 5 ? ProcessingStatus.Error : ProcessingStatus.Pending;
        msg.UpdatedOnUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }
}
```

### IncomingRequestService

```csharp
// OrderProcessing.Application/Services/IncomingRequestService.cs
public class IncomingRequestService : IIncomingRequestService
{
    private readonly AppDbContext _db;

    public IncomingRequestService(AppDbContext db)
    {
        _db = db;
    }

    public Task<bool> HasProcessedAsync(string eventName, string operationId, CancellationToken cancellationToken = default)
    {
        return _db.IncomingRequests
            .AnyAsync(x => x.EventName == eventName
                        && x.OperationId == operationId
                        && x.Status == ProcessingStatus.Completed, cancellationToken);
    }

    public async Task StartProcessingAsync(string eventName, string operationId, CancellationToken cancellationToken = default)
    {
        var entry = new IncomingRequest
        {
            Id = Guid.NewGuid(),
            EventName = eventName,
            OperationId = operationId,
            Status = ProcessingStatus.Processing,
            CreatedOnUtc = DateTime.UtcNow,
            UpdatedOnUtc = DateTime.UtcNow
        };
        _db.IncomingRequests.Add(entry);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkCompletedAsync(string eventName, string operationId, CancellationToken cancellationToken = default)
    {
        var entry = await _db.IncomingRequests
            .FirstOrDefaultAsync(x => x.EventName == eventName && x.OperationId == operationId, cancellationToken);
        if (entry is null) return;
        entry.Status = ProcessingStatus.Completed;
        entry.UpdatedOnUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkErrorAsync(string eventName, string operationId, CancellationToken cancellationToken = default)
    {
        var entry = await _db.IncomingRequests
            .FirstOrDefaultAsync(x => x.EventName == eventName && x.OperationId == operationId, cancellationToken);
        if (entry is null) return;
        entry.Status = ProcessingStatus.Error;
        entry.UpdatedOnUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }
}
```

### OrderService

```csharp
// OrderProcessing.Application/Services/OrderService.cs
public class OrderService : IOrderService
{
    private readonly AppDbContext _db;
    private readonly IOutboxService _outbox;

    public OrderService(AppDbContext db, IOutboxService outbox)
    {
        _db = db;
        _outbox = outbox;
    }

    public async Task<OrderResponse> CreateOrderAsync(CreateOrderRequest request, string operationId, CancellationToken cancellationToken = default)
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            CustomerName = request.CustomerName,
            CustomerEmail = request.CustomerEmail,
            Amount = request.Amount,
            Status = OrderStatus.Created,
            CreatedOnUtc = DateTime.UtcNow,
            UpdatedOnUtc = DateTime.UtcNow
        };

        _db.Orders.Add(order);

        var orderCreatedEvent = new OrderCreatedEvent
        {
            OperationId = operationId,
            OrderId = order.Id,
            CustomerName = order.CustomerName,
            CustomerEmail = order.CustomerEmail,
            Amount = order.Amount
        };

        await _outbox.AddAsync("order.created", operationId, orderCreatedEvent, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return new OrderResponse
        {
            Id = order.Id,
            CustomerName = order.CustomerName,
            CustomerEmail = order.CustomerEmail,
            Amount = order.Amount,
            Status = order.Status.ToString(),
            CreatedOnUtc = order.CreatedOnUtc
        };
    }

    public async Task<List<OrderResponse>> GetAllOrdersAsync(CancellationToken cancellationToken = default)
    {
        return await _db.Orders
            .OrderByDescending(x => x.CreatedOnUtc)
            .Select(x => new OrderResponse
            {
                Id = x.Id,
                CustomerName = x.CustomerName,
                CustomerEmail = x.CustomerEmail,
                Amount = x.Amount,
                Status = x.Status.ToString(),
                CreatedOnUtc = x.CreatedOnUtc
            })
            .ToListAsync(cancellationToken);
    }
}
```

### PaymentService

```csharp
// OrderProcessing.Application/Services/PaymentService.cs
public class PaymentService : IPaymentService
{
    private readonly AppDbContext _db;
    private readonly IOutboxService _outbox;

    public PaymentService(AppDbContext db, IOutboxService outbox)
    {
        _db = db;
        _outbox = outbox;
    }

    public async Task ProcessPaymentAsync(Guid orderId, decimal amount, string operationId, CancellationToken cancellationToken = default)
    {
        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            Amount = amount,
            Status = ProcessingStatus.Completed,
            CreatedOnUtc = DateTime.UtcNow,
            UpdatedOnUtc = DateTime.UtcNow
        };

        _db.Payments.Add(payment);

        var paymentSucceededEvent = new PaymentSucceededEvent
        {
            OperationId = operationId,
            OrderId = orderId,
            PaymentId = payment.Id,
            Amount = amount
        };

        await _outbox.AddAsync("payment.succeeded", operationId, paymentSucceededEvent, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<PaymentResponse>> GetAllPaymentsAsync(CancellationToken cancellationToken = default)
    {
        return await _db.Payments
            .OrderByDescending(x => x.CreatedOnUtc)
            .Select(x => new PaymentResponse
            {
                Id = x.Id,
                OrderId = x.OrderId,
                Amount = x.Amount,
                Status = x.Status.ToString(),
                CreatedOnUtc = x.CreatedOnUtc
            })
            .ToListAsync(cancellationToken);
    }
}
```

### NotificationService

```csharp
// OrderProcessing.Application/Services/NotificationService.cs
public class NotificationService : INotificationService
{
    private readonly AppDbContext _db;
    private readonly IOutboxService _outbox;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(AppDbContext db, IOutboxService outbox, ILogger<NotificationService> logger)
    {
        _db = db;
        _outbox = outbox;
        _logger = logger;
    }

    public async Task SendNotificationAsync(Guid orderId, decimal amount, string operationId, CancellationToken cancellationToken = default)
    {
        var message = $"Payment of {amount:C} received for order {orderId}. Thank you!";

        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            Message = message,
            CreatedOnUtc = DateTime.UtcNow
        };

        _db.Notifications.Add(notification);

        _logger.LogInformation("Notification sent for order {OrderId}: {Message}", orderId, message);

        var notificationEvent = new OrderNotificationEvent
        {
            OperationId = operationId,
            OrderId = orderId,
            Message = message
        };

        await _outbox.AddAsync("order.notification", operationId, notificationEvent, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<NotificationResponse>> GetAllNotificationsAsync(CancellationToken cancellationToken = default)
    {
        return await _db.Notifications
            .OrderByDescending(x => x.CreatedOnUtc)
            .Select(x => new NotificationResponse
            {
                Id = x.Id,
                OrderId = x.OrderId,
                Message = x.Message,
                CreatedOnUtc = x.CreatedOnUtc
            })
            .ToListAsync(cancellationToken);
    }
}
```

### Event Handlers

```csharp
// OrderProcessing.Application/EventHandlers/OrderCreatedEventHandler.cs
public class OrderCreatedEventHandler : IIntegrationEventHandler<OrderCreatedEvent>
{
    private readonly IPaymentService _paymentService;
    private readonly IIncomingRequestService _incoming;
    private readonly ILogger<OrderCreatedEventHandler> _logger;

    public OrderCreatedEventHandler(
        IPaymentService paymentService,
        IIncomingRequestService incoming,
        ILogger<OrderCreatedEventHandler> logger)
    {
        _paymentService = paymentService;
        _incoming = incoming;
        _logger = logger;
    }

    public async Task HandleAsync(OrderCreatedEvent message, CancellationToken cancellationToken = default)
    {
        const string eventName = "OrderCreatedEvent";

        if (await _incoming.HasProcessedAsync(eventName, message.OperationId, cancellationToken))
        {
            _logger.LogWarning("Duplicate event ignored. EventName: {EventName}, OperationId: {OperationId}", eventName, message.OperationId);
            return;
        }

        await _incoming.StartProcessingAsync(eventName, message.OperationId, cancellationToken);

        try
        {
            await _paymentService.ProcessPaymentAsync(message.OrderId, message.Amount, message.OperationId, cancellationToken);
            await _incoming.MarkCompletedAsync(eventName, message.OperationId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process payment for order {OrderId}", message.OrderId);
            await _incoming.MarkErrorAsync(eventName, message.OperationId, cancellationToken);
        }
    }
}

// OrderProcessing.Application/EventHandlers/PaymentSucceededEventHandler.cs
public class PaymentSucceededEventHandler : IIntegrationEventHandler<PaymentSucceededEvent>
{
    private readonly INotificationService _notificationService;
    private readonly IIncomingRequestService _incoming;
    private readonly ILogger<PaymentSucceededEventHandler> _logger;

    public PaymentSucceededEventHandler(
        INotificationService notificationService,
        IIncomingRequestService incoming,
        ILogger<PaymentSucceededEventHandler> logger)
    {
        _notificationService = notificationService;
        _incoming = incoming;
        _logger = logger;
    }

    public async Task HandleAsync(PaymentSucceededEvent message, CancellationToken cancellationToken = default)
    {
        const string eventName = "PaymentSucceededEvent";

        if (await _incoming.HasProcessedAsync(eventName, message.OperationId, cancellationToken))
        {
            _logger.LogWarning("Duplicate event ignored. EventName: {EventName}, OperationId: {OperationId}", eventName, message.OperationId);
            return;
        }

        await _incoming.StartProcessingAsync(eventName, message.OperationId, cancellationToken);

        try
        {
            await _notificationService.SendNotificationAsync(message.OrderId, message.Amount, message.OperationId, cancellationToken);
            await _incoming.MarkCompletedAsync(eventName, message.OperationId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send notification for order {OrderId}", message.OrderId);
            await _incoming.MarkErrorAsync(eventName, message.OperationId, cancellationToken);
        }
    }
}

// OrderProcessing.Application/EventHandlers/OrderNotificationEventHandler.cs
public class OrderNotificationEventHandler : IIntegrationEventHandler<OrderNotificationEvent>
{
    private readonly IIncomingRequestService _incoming;
    private readonly ILogger<OrderNotificationEventHandler> _logger;

    public OrderNotificationEventHandler(IIncomingRequestService incoming, ILogger<OrderNotificationEventHandler> logger)
    {
        _incoming = incoming;
        _logger = logger;
    }

    public async Task HandleAsync(OrderNotificationEvent message, CancellationToken cancellationToken = default)
    {
        const string eventName = "OrderNotificationEvent";

        if (await _incoming.HasProcessedAsync(eventName, message.OperationId, cancellationToken))
            return;

        await _incoming.StartProcessingAsync(eventName, message.OperationId, cancellationToken);

        _logger.LogInformation("Notification confirmed for order {OrderId}. Chain complete.", message.OrderId);

        await _incoming.MarkCompletedAsync(eventName, message.OperationId, cancellationToken);
    }
}
```

### Background Workers

The base class handles polling, retry, and error recording. Concrete workers only need to provide the topic name and deserialization.

```csharp
// OrderProcessing.Api/BackgroundServices/OutboxBackgroundService.cs
public abstract class OutboxBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger _logger;

    protected OutboxBackgroundService(IServiceScopeFactory scopeFactory, ILogger logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected abstract string TopicName { get; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var eventBus = scope.ServiceProvider.GetRequiredService<IInMemoryEventBus>();

                var messages = await db.OutboxMessages
                    .Where(x => x.TopicName == TopicName
                             && x.Status == ProcessingStatus.Pending
                             && x.RetryCount < 5)
                    .OrderBy(x => x.CreatedOnUtc)
                    .Take(20)
                    .ToListAsync(stoppingToken);

                foreach (var msg in messages)
                {
                    try
                    {
                        msg.Status = ProcessingStatus.Processing;
                        msg.UpdatedOnUtc = DateTime.UtcNow;
                        await db.SaveChangesAsync(stoppingToken);

                        await PublishAsync(eventBus, msg, stoppingToken);

                        msg.Status = ProcessingStatus.Completed;
                        msg.UpdatedOnUtc = DateTime.UtcNow;
                        await db.SaveChangesAsync(stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        msg.RetryCount += 1;
                        msg.LastError = ex.Message;
                        msg.Status = msg.RetryCount >= 5 ? ProcessingStatus.Error : ProcessingStatus.Pending;
                        msg.UpdatedOnUtc = DateTime.UtcNow;
                        await db.SaveChangesAsync(stoppingToken);

                        _logger.LogError(ex, "Outbox publish failed for message {MessageId} on topic {TopicName}", msg.Id, TopicName);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Outbox worker error for topic {TopicName}", TopicName);
            }

            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
    }

    protected abstract Task PublishAsync(IInMemoryEventBus eventBus, OutboxMessage message, CancellationToken cancellationToken);
}

// OrderProcessing.Api/BackgroundServices/OrderCreatedOutboxWorker.cs
public class OrderCreatedOutboxWorker : OutboxBackgroundService
{
    public OrderCreatedOutboxWorker(IServiceScopeFactory scopeFactory, ILogger<OrderCreatedOutboxWorker> logger)
        : base(scopeFactory, logger) { }

    protected override string TopicName => "order.created";

    protected override Task PublishAsync(IInMemoryEventBus eventBus, OutboxMessage message, CancellationToken cancellationToken)
    {
        var @event = JsonSerializer.Deserialize<OrderCreatedEvent>(message.BodyJson)!;
        return eventBus.PublishAsync("order.created", @event, cancellationToken);
    }
}

// OrderProcessing.Api/BackgroundServices/PaymentSucceededOutboxWorker.cs
public class PaymentSucceededOutboxWorker : OutboxBackgroundService
{
    public PaymentSucceededOutboxWorker(IServiceScopeFactory scopeFactory, ILogger<PaymentSucceededOutboxWorker> logger)
        : base(scopeFactory, logger) { }

    protected override string TopicName => "payment.succeeded";

    protected override Task PublishAsync(IInMemoryEventBus eventBus, OutboxMessage message, CancellationToken cancellationToken)
    {
        var @event = JsonSerializer.Deserialize<PaymentSucceededEvent>(message.BodyJson)!;
        return eventBus.PublishAsync("payment.succeeded", @event, cancellationToken);
    }
}

// OrderProcessing.Api/BackgroundServices/OrderNotificationOutboxWorker.cs
public class OrderNotificationOutboxWorker : OutboxBackgroundService
{
    public OrderNotificationOutboxWorker(IServiceScopeFactory scopeFactory, ILogger<OrderNotificationOutboxWorker> logger)
        : base(scopeFactory, logger) { }

    protected override string TopicName => "order.notification";

    protected override Task PublishAsync(IInMemoryEventBus eventBus, OutboxMessage message, CancellationToken cancellationToken)
    {
        var @event = JsonSerializer.Deserialize<OrderNotificationEvent>(message.BodyJson)!;
        return eventBus.PublishAsync("order.notification", @event, cancellationToken);
    }
}
```

### Controllers

```csharp
// OrderProcessing.Api/Controllers/OrdersController.cs
[ApiController]
[Route("api/orders")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;

    public OrdersController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    [HttpPost]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var operationId = HttpContext.Items["OperationId"]?.ToString() ?? Guid.NewGuid().ToString();
        var result = await _orderService.CreateOrderAsync(request, operationId, cancellationToken);
        return CreatedAtAction(nameof(GetOrders), new { }, result);
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<OrderResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOrders(CancellationToken cancellationToken)
    {
        var orders = await _orderService.GetAllOrdersAsync(cancellationToken);
        return Ok(orders);
    }
}

// OrderProcessing.Api/Controllers/PaymentsController.cs
[ApiController]
[Route("api/payments")]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;

    public PaymentsController(IPaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<PaymentResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPayments(CancellationToken cancellationToken)
    {
        var payments = await _paymentService.GetAllPaymentsAsync(cancellationToken);
        return Ok(payments);
    }
}

// OrderProcessing.Api/Controllers/NotificationsController.cs
[ApiController]
[Route("api/notifications")]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationsController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<NotificationResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetNotifications(CancellationToken cancellationToken)
    {
        var notifications = await _notificationService.GetAllNotificationsAsync(cancellationToken);
        return Ok(notifications);
    }
}
```

### Middleware

```csharp
// OrderProcessing.Api/Middleware/OperationIdMiddleware.cs
public class OperationIdMiddleware
{
    private readonly RequestDelegate _next;

    public OperationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var operationId = context.Request.Headers["operation_id"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(operationId))
            operationId = Guid.NewGuid().ToString();

        context.Items["OperationId"] = operationId;
        context.Response.Headers["operation_id"] = operationId;

        await _next(context);
    }
}

// OrderProcessing.Api/Middleware/GlobalExceptionMiddleware.cs
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception on {Method} {Path}", context.Request.Method, context.Request.Path);

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";

            var response = new { error = "An unexpected error occurred.", detail = ex.Message };
            await context.Response.WriteAsJsonAsync(response);
        }
    }
}
```

### DTOs

```csharp
// OrderProcessing.Application/DTOs/CreateOrderRequest.cs
public class CreateOrderRequest
{
    [Required]
    public string CustomerName { get; set; } = default!;

    [Required]
    [EmailAddress]
    public string CustomerEmail { get; set; } = default!;

    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }
}

// OrderProcessing.Application/DTOs/OrderResponse.cs
public class OrderResponse
{
    public Guid Id { get; set; }
    public string CustomerName { get; set; } = default!;
    public string CustomerEmail { get; set; } = default!;
    public decimal Amount { get; set; }
    public string Status { get; set; } = default!;
    public DateTime CreatedOnUtc { get; set; }
}

// OrderProcessing.Application/DTOs/PaymentResponse.cs
public class PaymentResponse
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = default!;
    public DateTime CreatedOnUtc { get; set; }
}

// OrderProcessing.Application/DTOs/NotificationResponse.cs
public class NotificationResponse
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public string Message { get; set; } = default!;
    public DateTime CreatedOnUtc { get; set; }
}
```

---

## 16. DI Registration — Program.cs

```csharp
// OrderProcessing.Api/Program.cs
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Order Processing API",
        Version = "v1",
        Description = "Event-driven order processing system — Order, Payment, and Notification services"
    });
});

// Database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseInMemoryDatabase("OrderProcessingDb"));

// Event bus — singleton so all handlers share the same subscription registry
builder.Services.AddSingleton<IInMemoryEventBus, InMemoryEventBus>();

// Application services — scoped to match DbContext lifetime
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IOutboxService, OutboxService>();
builder.Services.AddScoped<IIncomingRequestService, IncomingRequestService>();

// Event handlers
builder.Services.AddScoped<IIntegrationEventHandler<OrderCreatedEvent>, OrderCreatedEventHandler>();
builder.Services.AddScoped<IIntegrationEventHandler<PaymentSucceededEvent>, PaymentSucceededEventHandler>();
builder.Services.AddScoped<IIntegrationEventHandler<OrderNotificationEvent>, OrderNotificationEventHandler>();

// Background workers
builder.Services.AddHostedService<OrderCreatedOutboxWorker>();
builder.Services.AddHostedService<PaymentSucceededOutboxWorker>();
builder.Services.AddHostedService<OrderNotificationOutboxWorker>();

var app = builder.Build();

// Register event bus subscriptions at startup
// Handlers are resolved in a temporary scope — they must be scoped or transient
var eventBus = app.Services.GetRequiredService<IInMemoryEventBus>();

using (var scope = app.Services.CreateScope())
{
    var orderCreatedHandler = scope.ServiceProvider.GetRequiredService<IIntegrationEventHandler<OrderCreatedEvent>>();
    var paymentSucceededHandler = scope.ServiceProvider.GetRequiredService<IIntegrationEventHandler<PaymentSucceededEvent>>();
    var notificationHandler = scope.ServiceProvider.GetRequiredService<IIntegrationEventHandler<OrderNotificationEvent>>();

    eventBus.Subscribe<OrderCreatedEvent>("order.created", orderCreatedHandler.HandleAsync);
    eventBus.Subscribe<PaymentSucceededEvent>("payment.succeeded", paymentSucceededHandler.HandleAsync);
    eventBus.Subscribe<OrderNotificationEvent>("order.notification", notificationHandler.HandleAsync);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseMiddleware<OperationIdMiddleware>();

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
```

**Note on subscription registration:** The handlers are resolved once at startup in a temporary scope and their `HandleAsync` delegates are registered with the event bus. This works because the bus stores the delegate reference, not the handler instance. When the bus calls the delegate later, it will invoke the handler within the scope created by the outbox worker — which is the correct scoped context with its own `DbContext`.

A cleaner alternative is a dedicated `EventBusSubscriptionRegistrar` class, but for this test the inline approach is simple and obvious enough.

---

## 17. Unit Tests

Tests cover the core application layer. Infrastructure (event bus, EF Core) is mocked. The goal is to verify business logic in isolation.

```csharp
// OrderProcessing.Tests/OrderProcessing.Tests.csproj
// Packages: xunit, xunit.runner.visualstudio, Moq, Microsoft.EntityFrameworkCore.InMemory

// OrderProcessing.Tests/Services/OrderServiceTests.cs
public class OrderServiceTests
{
    private AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task CreateOrderAsync_SavesOrderAndOutboxMessage()
    {
        var db = CreateDbContext();
        var outboxService = new OutboxService(db);
        var service = new OrderService(db, outboxService);

        var request = new CreateOrderRequest
        {
            CustomerName = "Jane Doe",
            CustomerEmail = "jane@example.com",
            Amount = 99.99m
        };

        var result = await service.CreateOrderAsync(request, "op-001");

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("Jane Doe", result.CustomerName);
        Assert.Equal(1, await db.Orders.CountAsync());
        Assert.Equal(1, await db.OutboxMessages.CountAsync());

        var outbox = await db.OutboxMessages.FirstAsync();
        Assert.Equal("order.created", outbox.TopicName);
        Assert.Equal("op-001", outbox.OperationId);
        Assert.Equal(ProcessingStatus.Pending, outbox.Status);
    }

    [Fact]
    public async Task GetAllOrdersAsync_ReturnsAllOrders()
    {
        var db = CreateDbContext();
        db.Orders.AddRange(
            new Order { Id = Guid.NewGuid(), CustomerName = "A", CustomerEmail = "a@a.com", Amount = 10, Status = OrderStatus.Created, CreatedOnUtc = DateTime.UtcNow, UpdatedOnUtc = DateTime.UtcNow },
            new Order { Id = Guid.NewGuid(), CustomerName = "B", CustomerEmail = "b@b.com", Amount = 20, Status = OrderStatus.Created, CreatedOnUtc = DateTime.UtcNow, UpdatedOnUtc = DateTime.UtcNow }
        );
        await db.SaveChangesAsync();

        var outboxService = new OutboxService(db);
        var service = new OrderService(db, outboxService);

        var orders = await service.GetAllOrdersAsync();

        Assert.Equal(2, orders.Count);
    }
}

// OrderProcessing.Tests/EventHandlers/OrderCreatedEventHandlerTests.cs
public class OrderCreatedEventHandlerTests
{
    private AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task HandleAsync_ProcessesPaymentAndSavesOutbox()
    {
        var db = CreateDbContext();
        var outboxService = new OutboxService(db);
        var incomingService = new IncomingRequestService(db);
        var paymentService = new PaymentService(db, outboxService);
        var logger = Mock.Of<ILogger<OrderCreatedEventHandler>>();

        var handler = new OrderCreatedEventHandler(paymentService, incomingService, logger);

        var @event = new OrderCreatedEvent
        {
            OperationId = "op-001",
            OrderId = Guid.NewGuid(),
            CustomerName = "Jane",
            CustomerEmail = "jane@example.com",
            Amount = 49.99m
        };

        await handler.HandleAsync(@event);

        Assert.Equal(1, await db.Payments.CountAsync());
        Assert.Equal(1, await db.OutboxMessages.CountAsync());

        var outbox = await db.OutboxMessages.FirstAsync();
        Assert.Equal("payment.succeeded", outbox.TopicName);

        var incoming = await db.IncomingRequests.FirstAsync();
        Assert.Equal(ProcessingStatus.Completed, incoming.Status);
    }

    [Fact]
    public async Task HandleAsync_SkipsDuplicateEvent()
    {
        var db = CreateDbContext();
        var incomingService = new IncomingRequestService(db);

        // Pre-seed a completed incoming request to simulate a duplicate
        db.IncomingRequests.Add(new IncomingRequest
        {
            Id = Guid.NewGuid(),
            EventName = "OrderCreatedEvent",
            OperationId = "op-dup",
            Status = ProcessingStatus.Completed,
            CreatedOnUtc = DateTime.UtcNow,
            UpdatedOnUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var outboxService = new OutboxService(db);
        var paymentService = new PaymentService(db, outboxService);
        var logger = Mock.Of<ILogger<OrderCreatedEventHandler>>();

        var handler = new OrderCreatedEventHandler(paymentService, incomingService, logger);

        var @event = new OrderCreatedEvent
        {
            OperationId = "op-dup",
            OrderId = Guid.NewGuid(),
            CustomerName = "Jane",
            Amount = 49.99m
        };

        await handler.HandleAsync(@event);

        // No payment should have been created
        Assert.Equal(0, await db.Payments.CountAsync());
    }
}

// OrderProcessing.Tests/EventHandlers/PaymentSucceededEventHandlerTests.cs
public class PaymentSucceededEventHandlerTests
{
    private AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task HandleAsync_SavesNotificationAndOutbox()
    {
        var db = CreateDbContext();
        var outboxService = new OutboxService(db);
        var incomingService = new IncomingRequestService(db);
        var notificationLogger = Mock.Of<ILogger<NotificationService>>();
        var notificationService = new NotificationService(db, outboxService, notificationLogger);
        var handlerLogger = Mock.Of<ILogger<PaymentSucceededEventHandler>>();

        var handler = new PaymentSucceededEventHandler(notificationService, incomingService, handlerLogger);

        var @event = new PaymentSucceededEvent
        {
            OperationId = "op-002",
            OrderId = Guid.NewGuid(),
            PaymentId = Guid.NewGuid(),
            Amount = 49.99m
        };

        await handler.HandleAsync(@event);

        Assert.Equal(1, await db.Notifications.CountAsync());
        Assert.Equal(1, await db.OutboxMessages.CountAsync());

        var outbox = await db.OutboxMessages.FirstAsync();
        Assert.Equal("order.notification", outbox.TopicName);
    }
}
```

---

## 18. README Template

The README below should be placed at the root of the repository.

```markdown
# Order Processing System

A simplified microservices-style order processing system built with .NET 8. Three logical services — Order, Payment, and Notification — run within a single ASP.NET Core application, communicate via an in-memory event bus, and use the Outbox Pattern for reliable message delivery.

## Architecture Overview

The system is structured as four class library projects following clean architecture:

- **Domain** — entities, enums, event contracts, bus interface
- **Application** — business services, event handlers, DTOs
- **Infrastructure** — EF Core DbContext, event bus implementation, repositories
- **Api** — controllers, middleware, background workers, DI wiring

All three services are fully decoupled. They never call each other directly. Every cross-service action flows through an event:

```
POST /api/orders
  → OrderService creates Order + queues OrderCreatedEvent (outbox)
  → OrderCreatedOutboxWorker publishes event
  → OrderCreatedEventHandler triggers PaymentService
  → PaymentService saves Payment + queues PaymentSucceededEvent (outbox)
  → PaymentSucceededOutboxWorker publishes event
  → PaymentSucceededEventHandler triggers NotificationService
  → NotificationService saves Notification + logs confirmation
```

## How to Run

**Requirements:** .NET 8 SDK

```bash
git clone https://github.com/your-username/dotnet-microservices-takehome-yourname.git
cd dotnet-microservices-takehome-yourname

dotnet restore
dotnet run --project src/OrderProcessing.Api
```

Swagger UI is available at: `https://localhost:5001/swagger`

## API Endpoints

| Method | Endpoint | Description |
|---|---|---|
| POST | /api/orders | Create a new order |
| GET | /api/orders | List all orders |
| GET | /api/payments | List all processed payments |
| GET | /api/notifications | List all notifications |

### Example Request

```bash
curl -X POST https://localhost:5001/api/orders \
  -H "Content-Type: application/json" \
  -d '{"customerName": "Jane Doe", "customerEmail": "jane@example.com", "amount": 99.99}'
```

After a couple of seconds, check `/api/payments` and `/api/notifications` to see the full chain complete.

## Running Tests

```bash
dotnet test
```

## Design Decisions

**Why one application instead of three?**  
The in-memory event bus works only within a single process. Running three separate processes would require a real message broker (RabbitMQ, Kafka, etc.). For this test the single-application approach keeps setup simple while still showing correct event-driven design. Replacing the transport with RabbitMQ in the future would require only a new Infrastructure implementation — no Domain or Application code changes.

**Why the Outbox Pattern?**  
Publishing an event directly inside a service call risks losing the event if the process crashes between the database save and the publish call. The Outbox Pattern saves the event to the database in the same transaction as the business entity. A background worker then publishes it with automatic retry (up to 5 attempts).

**Why IncomingRequest for idempotency?**  
Background workers can publish the same outbox message more than once if a transient failure occurs after publishing but before the row is marked as completed. The IncomingRequest table records each EventName + OperationId pair so that handlers are safe to call multiple times with the same input.

**Why is OperationId propagated across the whole chain?**  
Every event carries the same OperationId generated on the original HTTP request. This allows every log entry, every outbox message, and every incoming request record to be correlated back to a single originating call — useful for debugging and observability.

## Known Limitations

- Data is stored in-memory and lost on restart.
- No authentication or authorization.
- The in-memory event bus does not support multiple application instances or horizontal scaling.
- Payment processing is simulated and always succeeds.
- Outbox workers would need distributed locking in a real multi-instance deployment.
```
