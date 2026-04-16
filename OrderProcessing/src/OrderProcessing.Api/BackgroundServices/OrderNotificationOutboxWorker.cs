using System.Text.Json;
using OrderProcessing.Domain.Entities;
using OrderProcessing.Domain.Events;
using OrderProcessing.Domain.Interfaces;

namespace OrderProcessing.Api.BackgroundServices;

public class OrderNotificationOutboxWorker : OutboxBackgroundService
{
    public OrderNotificationOutboxWorker(IServiceScopeFactory scopeFactory, IInMemoryEventBus eventBus, ILogger<OrderNotificationOutboxWorker> logger)
        : base(scopeFactory, eventBus, logger) { }

    protected override string TopicName => "order.notification";

    protected override Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        var @event = JsonSerializer.Deserialize<OrderNotificationEvent>(message.BodyJson)!;
        return EventBus.PublishAsync("order.notification", @event, cancellationToken);
    }
}
