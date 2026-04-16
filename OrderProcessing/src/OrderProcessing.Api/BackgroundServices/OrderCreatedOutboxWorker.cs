using System.Text.Json;
using OrderProcessing.Domain.Entities;
using OrderProcessing.Domain.Events;
using OrderProcessing.Domain.Interfaces;

namespace OrderProcessing.Api.BackgroundServices;

public class OrderCreatedOutboxWorker : OutboxBackgroundService
{
    public OrderCreatedOutboxWorker(IServiceScopeFactory scopeFactory, IInMemoryEventBus eventBus, ILogger<OrderCreatedOutboxWorker> logger)
        : base(scopeFactory, eventBus, logger) { }

    protected override string TopicName => "order.created";

    protected override Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        var @event = JsonSerializer.Deserialize<OrderCreatedEvent>(message.BodyJson)!;
        return EventBus.PublishAsync("order.created", @event, cancellationToken);
    }
}
