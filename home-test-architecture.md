# Home Test Implementation Notes

## Goal
Build **one application** for the coding home test, but organize folders in a way that looks like a separated multi-project clean architecture.

The solution should stay **simple, readable, and realistic for a home test**.
It should not look over-engineered.

---

## Main Decisions

### 1. Single application, separated by folders
Because an **in-memory event bus cannot work across different applications/processes**, we will keep everything in **one application**.

But we will separate the code with folders to make it look like multiple layers/projects.

This gives:
- simple setup
- easy local run
- no external infrastructure needed
- still follows clean architecture style

---

### 2. Use Clean Architecture with NopCommerce-style foldering
We can follow a folder structure similar to nopCommerce style, where responsibilities are clearly separated.

Example:

```text
src/
 └── HomeTest.Api/
      ├── Controllers/
      ├── Middleware/
      ├── BackgroundServices/
      ├── DependencyRegistrar/
      ├── Presentation/
      ├── Core/
      │    ├── Domain/
      │    │    ├── Entities/
      │    │    ├── Enums/
      │    │    ├── Events/
      │    │    └── Interfaces/
      │    ├── Application/
      │    │    ├── DTOs/
      │    │    ├── Commands/
      │    │    ├── Services/
      │    │    ├── Interfaces/
      │    │    └── EventHandlers/
      │    └── Infrastructure/
      │         ├── Data/
      │         ├── EventBus/
      │         ├── Repositories/
      │         └── Services/
      └── Program.cs
```

This keeps the home test easy to understand.

---

## Technology Choices

- **ASP.NET Core Web API**
- **EF Core InMemory Provider**
- **In-memory Event Bus**
- **BackgroundService** for outbox processing
- **Async** for DB and message handling methods

---

## DI Lifetime Best Practice

For this test, use lifetimes carefully:

### Singleton
Use **Singleton** for services that are stateless and shared safely:
- `IInMemoryEventBus`
- event bus subscription registry if it holds subscriber mappings in memory

Why singleton?
Because the in-memory bus must be shared across the same application instance.
If scoped/transient is used, different instances may have different subscriptions.

### Scoped
Use **Scoped** for request-based services:
- EF Core `DbContext`
- repositories
- application services that use `DbContext`

Why scoped?
Because `DbContext` should be scoped per request or per service scope.

### Hosted Services
`BackgroundService` is effectively singleton, so it **must not directly keep a scoped DbContext**.
Instead, inject:
- `IServiceScopeFactory`

and create a scope inside each execution cycle.

---

## EF Core InMemory
Use EF Core InMemory with proper DI.

Example registration:

```csharp
services.AddDbContext<AppDbContext>(options =>
    options.UseInMemoryDatabase("HomeTestDb"));
```

This is enough for the test.

---

## Middleware for Operation ID
The API Gateway is expected to send `operation_id` in the header.
If it is missing, middleware should create one.

Recommended header name:

```text
operation_id
```

Behavior:
- check request header `operation_id`
- if missing, generate new `Guid`
- store it in `HttpContext.Items`
- optionally add it back to response header

Example idea:

```csharp
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
        {
            operationId = Guid.NewGuid().ToString();
        }

        context.Items["OperationId"] = operationId;
        context.Response.Headers["operation_id"] = operationId;

        await _next(context);
    }
}
```

---

## Domain Enums
Use simple enum values.

```csharp
public enum ProcessingStatus
{
    Pending = 1,
    Processing = 2,
    Completed = 3,
    Error = 4
}
```

---

## Main Tables / Entities

### 1. OutboxMessage
This stores events that need to be published later.

Fields:
- `Id : Guid`
- `TopicName : string`
- `OperationId : string`
- `Status : ProcessingStatus`
- `BodyJson : string`
- `RetryCount : int`
- `LastError : string?`
- `CreatedOnUtc : DateTime`
- `UpdatedOnUtc : DateTime`

Example:

```csharp
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
```

### 2. IncomingRequest
This is used for consumer-side idempotency.

Fields:
- `Id : Guid`
- `EventName : string`
- `OperationId : string`
- `Status : ProcessingStatus`
- `CreatedOnUtc : DateTime`
- `UpdatedOnUtc : DateTime`

> Idempotency should be checked by **EventName + OperationId**.

Example:

```csharp
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

Recommended unique check:
- `EventName + OperationId`

Even if InMemory EF does not enforce DB unique index strongly like a real DB, still implement the validation in code.

---

## Event Names / Topic Names
Even for an in-memory event bus, we should still use **topic/event names**.

Suggested topics:
- `order.created`
- `payment.succeeded`
- `order.notification`

This makes the design similar to RabbitMQ/Kafka style, even though the transport is in-memory.

---

## In-Memory Event Bus Design
We should use a proper interface and class.

### Interface
```csharp
public interface IInMemoryEventBus
{
    Task PublishAsync<T>(string topicName, T message, CancellationToken cancellationToken = default);
    void Subscribe<T>(string topicName, Func<T, CancellationToken, Task> handler);
}
```

### Why this is enough
- `PublishAsync` keeps async style
- `Subscribe` maps topic name to handlers
- simple for home test
- does not add unnecessary complexity

### Simple implementation idea
- keep a `ConcurrentDictionary<string, List<Delegate>>`
- when publishing, find handlers by topic
- invoke handlers asynchronously

Example skeleton:

```csharp
public class InMemoryEventBus : IInMemoryEventBus
{
    private readonly ConcurrentDictionary<string, List<Delegate>> _handlers = new();

    public Task PublishAsync<T>(string topicName, T message, CancellationToken cancellationToken = default)
    {
        if (_handlers.TryGetValue(topicName, out var handlers))
        {
            var tasks = handlers
                .Cast<Func<T, CancellationToken, Task>>()
                .Select(x => x(message, cancellationToken));

            return Task.WhenAll(tasks);
        }

        return Task.CompletedTask;
    }

    public void Subscribe<T>(string topicName, Func<T, CancellationToken, Task> handler)
    {
        _handlers.AddOrUpdate(
            topicName,
            _ => new List<Delegate> { handler },
            (_, existing) =>
            {
                existing.Add(handler);
                return existing;
            });
    }
}
```

---

## Should we return confirmation from event bus?
For this test, the event bus publish method can complete successfully if:
- handler executed successfully, or
- no subscriber exists and we accept no-op behavior

But because we are using **outbox pattern**, the main reliability should come from:
- storing in outbox first
- background service reads pending rows
- retries when consumer/publish fails

So we should **not depend only on direct publish result**.

---

## Outbox Pattern Flow

### API request flow
1. API receives request
2. middleware ensures `operation_id`
3. application service saves main business data
4. application service writes outbox row with:
   - topic name
   - operation id
   - status = `Pending`
   - body json
5. save changes
6. return success to client

### Background service flow
1. background service reads pending outbox rows for its topic
2. mark row as `Processing`
3. publish to in-memory event bus
4. if success, mark `Completed`
5. if fail, increment retry count
6. if retry count >= 5, mark `Error`
7. else set status back to `Pending`

---

## Why 3 BackgroundService classes?
The requirement says we need 3 background services for:
- `OrderCreatedEvent`
- `PaymentSucceededEvent`
- `OrderNotificationEvent`

So each worker can filter by topic.

Suggested topic mapping:

| Background Service | Topic Name |
|---|---|
| `OrderCreatedOutboxWorker` | `order.created` |
| `PaymentSucceededOutboxWorker` | `payment.succeeded` |
| `OrderNotificationOutboxWorker` | `order.notification` |

This keeps the code very easy for reviewers.

---

## Base Background Worker Design
Instead of duplicating all logic, create a reusable base class.

Example idea:

```csharp
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
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var eventBus = scope.ServiceProvider.GetRequiredService<IInMemoryEventBus>();

                var messages = await dbContext.OutboxMessages
                    .Where(x => x.TopicName == TopicName
                             && (x.Status == ProcessingStatus.Pending || x.Status == ProcessingStatus.Error)
                             && x.RetryCount < 5)
                    .OrderBy(x => x.CreatedOnUtc)
                    .Take(20)
                    .ToListAsync(stoppingToken);

                foreach (var message in messages)
                {
                    try
                    {
                        message.Status = ProcessingStatus.Processing;
                        message.UpdatedOnUtc = DateTime.UtcNow;
                        await dbContext.SaveChangesAsync(stoppingToken);

                        await PublishMessageAsync(eventBus, message, stoppingToken);

                        message.Status = ProcessingStatus.Completed;
                        message.UpdatedOnUtc = DateTime.UtcNow;
                        await dbContext.SaveChangesAsync(stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        message.RetryCount += 1;
                        message.LastError = ex.Message;
                        message.Status = message.RetryCount >= 5
                            ? ProcessingStatus.Error
                            : ProcessingStatus.Pending;
                        message.UpdatedOnUtc = DateTime.UtcNow;

                        await dbContext.SaveChangesAsync(stoppingToken);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Outbox worker failed for topic {TopicName}", TopicName);
            }

            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
        }
    }

    protected abstract Task PublishMessageAsync(
        IInMemoryEventBus eventBus,
        OutboxMessage message,
        CancellationToken cancellationToken);
}
```

Then each concrete service just provides the topic name and deserialization logic.

---

## Retry Logic
Max retry count: **5**

Behavior:
- first failure -> retry count 1
- continue until retry count reaches 5
- after 5th failure -> mark `Error`

This is enough for a home test.

If needed, small delay can be added between cycles. No need to introduce Polly unless the test explicitly requires it.

Reason:
- Polly is good, but for a home test it may add extra complexity
- simple retry count in outbox row is easier to explain

---

## Idempotency on Consumer Side
This is very important.

When the consumer receives an event:
1. check if `IncomingRequest` already exists with same:
   - `EventName`
   - `OperationId`
2. if exists and already completed, skip processing
3. if not exists, create incoming row with `Processing`
4. execute business logic
5. mark incoming row as `Completed`
6. if failed, mark as `Error`

This prevents duplicate insert or duplicate action.

### Why use EventName + OperationId?
Because the same operation should not be processed multiple times for the same event.

Example:
- operation id = `abc-123`
- event name = `payment.succeeded`

If the event is delivered twice, second one should be ignored.

---

## Consumer Handler Pattern
Each event handler should stay simple.

Example interface:

```csharp
public interface IIntegrationEventHandler<T>
{
    Task HandleAsync(T message, CancellationToken cancellationToken = default);
}
```

Example handler responsibilities:
- validate idempotency
- save incoming request row
- perform business logic
- update incoming request status

---

## Example Event Contracts
Keep event contracts small.

```csharp
public class OrderCreatedEvent
{
    public string OperationId { get; set; } = default!;
    public Guid OrderId { get; set; }
    public string CustomerName { get; set; } = default!;
}

public class PaymentSucceededEvent
{
    public string OperationId { get; set; } = default!;
    public Guid OrderId { get; set; }
    public decimal Amount { get; set; }
}

public class OrderNotificationEvent
{
    public string OperationId { get; set; } = default!;
    public Guid OrderId { get; set; }
    public string Message { get; set; } = default!;
}
```

---

## Example Application Flow

### Create Order API
- create order
- save `OutboxMessage` with topic `order.created`
- worker publishes event
- handler processes event
- handler writes `IncomingRequest`

### Payment Success API
- save payment result
- save `OutboxMessage` with topic `payment.succeeded`
- worker publishes event
- handler processes event idempotently

### Notification Flow
- save `OutboxMessage` with topic `order.notification`
- worker publishes event
- notification handler processes it once only

---

## Registration Example
Keep dependency registration clean.

```csharp
services.AddDbContext<AppDbContext>(options =>
    options.UseInMemoryDatabase("HomeTestDb"));

services.AddSingleton<IInMemoryEventBus, InMemoryEventBus>();

services.AddScoped<IOrderService, OrderService>();
services.AddScoped<IPaymentService, PaymentService>();
services.AddScoped<IOutboxService, OutboxService>();
services.AddScoped<IIncomingRequestService, IncomingRequestService>();

services.AddHostedService<OrderCreatedOutboxWorker>();
services.AddHostedService<PaymentSucceededOutboxWorker>();
services.AddHostedService<OrderNotificationOutboxWorker>();
```

For subscriptions, register them at startup in a simple initialization block.

Example idea:

```csharp
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
```

A cleaner option is a dedicated subscription registrar class.

---

## Suggested Interfaces

### Outbox service
```csharp
public interface IOutboxService
{
    Task AddAsync(string topicName, string operationId, object payload, CancellationToken cancellationToken = default);
}
```

### Incoming request service
```csharp
public interface IIncomingRequestService
{
    Task<bool> HasProcessedAsync(string eventName, string operationId, CancellationToken cancellationToken = default);
    Task StartProcessingAsync(string eventName, string operationId, CancellationToken cancellationToken = default);
    Task MarkCompletedAsync(string eventName, string operationId, CancellationToken cancellationToken = default);
    Task MarkErrorAsync(string eventName, string operationId, CancellationToken cancellationToken = default);
}
```

---

## Async Best Practice
Use async for:
- controller actions
- EF Core queries and saves
- outbox insert
- event publish
- event handlers
- background workers

Examples:
- `ToListAsync`
- `FirstOrDefaultAsync`
- `SaveChangesAsync`
- `PublishAsync`
- `HandleAsync`

This matches best practice and looks modern.

---

## Keep the Code Simple
Because this is a home test:

### Do
- use simple names
- use clear interfaces
- use minimal abstraction
- write small classes
- keep one responsibility per class
- use comments only where needed

### Do not
- overuse generic patterns
- add MediatR unless required
- add distributed message broker
- add complicated factory layers
- add too many extension classes
- add too much enterprise complexity

The reviewer should feel:
> this candidate understands architecture, but still knows how to keep things practical.

---

## Important Note About In-Memory Event Bus
The in-memory event bus works **only inside the same running application instance**.
It does **not** work:
- across multiple servers
- across multiple applications
- after app restart

So for this test:
- it is acceptable
- keep one application
- structure folders to simulate separated layers

If this were real production, we would replace it with:
- RabbitMQ
- Kafka
- Azure Service Bus
- or another distributed broker

---

## Recommended Simple Final Design

### Layers
- **Api** → controllers, middleware, hosted services
- **Domain** → entities, enums, event contracts, interfaces
- **Application** → business services, handler logic
- **Infrastructure** → EF Core, repositories, event bus

### Data consistency
- request saves business data + outbox row
- background worker publishes from outbox
- consumer ensures idempotency with `IncomingRequest`
- retries up to 5 times

### Reliability
- operation id from middleware
- topic-based outbox processing
- idempotent consumer
- retry with max 5

---

## Very Short Summary
For the home test, the best approach is:

- build **one ASP.NET Core application**
- organize folders like **multi-layer clean architecture**
- use **EF Core InMemory** with proper scoped DI
- use **singleton in-memory event bus**
- save events in **outbox table** first
- create **3 BackgroundService** workers by topic
- use **IncomingRequest** table for idempotency using `EventName + OperationId`
- use **async** everywhere for DB and bus operations
- keep the implementation **simple and readable**

---

## Optional Folder Example (More Detailed)

```text
HomeTest.Api/
 ├── BackgroundServices/
 │    ├── OutboxBackgroundService.cs
 │    ├── OrderCreatedOutboxWorker.cs
 │    ├── PaymentSucceededOutboxWorker.cs
 │    └── OrderNotificationOutboxWorker.cs
 ├── Controllers/
 │    ├── OrdersController.cs
 │    └── PaymentsController.cs
 ├── Middleware/
 │    └── OperationIdMiddleware.cs
 ├── DependencyRegistrar/
 │    └── ServiceCollectionExtensions.cs
 ├── Core/
 │    ├── Domain/
 │    │    ├── Entities/
 │    │    │    ├── Order.cs
 │    │    │    ├── Payment.cs
 │    │    │    ├── OutboxMessage.cs
 │    │    │    └── IncomingRequest.cs
 │    │    ├── Enums/
 │    │    │    └── ProcessingStatus.cs
 │    │    ├── Events/
 │    │    │    ├── OrderCreatedEvent.cs
 │    │    │    ├── PaymentSucceededEvent.cs
 │    │    │    └── OrderNotificationEvent.cs
 │    │    └── Interfaces/
 │    │         ├── IInMemoryEventBus.cs
 │    │         └── IIntegrationEventHandler.cs
 │    ├── Application/
 │    │    ├── Interfaces/
 │    │    │    ├── IOrderService.cs
 │    │    │    ├── IPaymentService.cs
 │    │    │    ├── IOutboxService.cs
 │    │    │    └── IIncomingRequestService.cs
 │    │    ├── Services/
 │    │    │    ├── OrderService.cs
 │    │    │    ├── PaymentService.cs
 │    │    │    ├── OutboxService.cs
 │    │    │    └── IncomingRequestService.cs
 │    │    └── EventHandlers/
 │    │         ├── OrderCreatedEventHandler.cs
 │    │         ├── PaymentSucceededEventHandler.cs
 │    │         └── OrderNotificationEventHandler.cs
 │    └── Infrastructure/
 │         ├── Data/
 │         │    └── AppDbContext.cs
 │         ├── EventBus/
 │         │    └── InMemoryEventBus.cs
 │         └── Repositories/
 │              ├── OrderRepository.cs
 │              └── PaymentRepository.cs
 └── Program.cs
```

---

## Final Recommendation
This design is strong enough for the test because it shows:
- clean separation
- understanding of DI lifetimes
- async usage
- outbox pattern
- idempotent consumer
- background workers
- topic-based event handling

But at the same time it remains **simple enough to look handwritten and practical**.
