using System.Text.Json;
using OrderProcessing.Domain.Entities;
using OrderProcessing.Domain.Events;
using OrderProcessing.Domain.Interfaces;

namespace OrderProcessing.Api.BackgroundServices;

public class PaymentSucceededOutboxWorker : OutboxBackgroundService
{
    public PaymentSucceededOutboxWorker(IServiceScopeFactory scopeFactory, IInMemoryEventBus eventBus, ILogger<PaymentSucceededOutboxWorker> logger)
        : base(scopeFactory, eventBus, logger) { }

    protected override string TopicName => "payment.succeeded";

    protected override Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        var @event = JsonSerializer.Deserialize<PaymentSucceededEvent>(message.BodyJson)!;
        return EventBus.PublishAsync("payment.succeeded", @event, cancellationToken);
    }
}
