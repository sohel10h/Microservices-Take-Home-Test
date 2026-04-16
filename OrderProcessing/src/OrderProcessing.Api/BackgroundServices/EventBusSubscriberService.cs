using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using OrderProcessing.Application.Interfaces;
using OrderProcessing.Application.Settings;
using OrderProcessing.Domain.Events;
using OrderProcessing.Domain.Interfaces;

namespace OrderProcessing.Api.BackgroundServices;

public class EventBusSubscriberService : IHostedService
{
    private readonly IInMemoryEventBus _eventBus;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RetrySettings _retrySettings;
    private readonly ILogger<EventBusSubscriberService> _logger;

    public EventBusSubscriberService(
        IInMemoryEventBus eventBus,
        IServiceScopeFactory scopeFactory,
        IOptions<RetrySettings> retryOptions,
        ILogger<EventBusSubscriberService> logger)
    {
        _eventBus = eventBus;
        _scopeFactory = scopeFactory;
        _retrySettings = retryOptions.Value;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _eventBus.Subscribe<OrderCreatedEvent>("order.created", async (msg, ct) =>
        {
            var pipeline = BuildRetryPipeline("OrderCreatedEvent", msg.OperationId);
            await pipeline.ExecuteAsync(async token =>
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var handler = scope.ServiceProvider.GetRequiredService<IIntegrationEventHandler<OrderCreatedEvent>>();
                await handler.HandleAsync(msg, token);
            }, ct);
        });

        _eventBus.Subscribe<PaymentSucceededEvent>("payment.succeeded", async (msg, ct) =>
        {
            var pipeline = BuildRetryPipeline("PaymentSucceededEvent", msg.OperationId);
            await pipeline.ExecuteAsync(async token =>
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var handler = scope.ServiceProvider.GetRequiredService<IIntegrationEventHandler<PaymentSucceededEvent>>();
                await handler.HandleAsync(msg, token);
            }, ct);
        });

        _eventBus.Subscribe<OrderNotificationEvent>("order.notification", async (msg, ct) =>
        {
            var pipeline = BuildRetryPipeline("OrderNotificationEvent", msg.OperationId);
            await pipeline.ExecuteAsync(async token =>
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var handler = scope.ServiceProvider.GetRequiredService<IIntegrationEventHandler<OrderNotificationEvent>>();
                await handler.HandleAsync(msg, token);
            }, ct);
        });

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private ResiliencePipeline BuildRetryPipeline(string eventName, string operationId)
    {
        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = _retrySettings.MaxRetryAttempts,
                Delay = TimeSpan.FromSeconds(_retrySettings.RetryDelaySeconds),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                OnRetry = async args =>
                {
                    _logger.LogWarning(
                        args.Outcome.Exception,
                        "Retry {Attempt} of {Max} for {EventName}/{OperationId}. Waiting {Delay}ms.",
                        args.AttemptNumber + 1, _retrySettings.MaxRetryAttempts,
                        eventName, operationId,
                        args.RetryDelay.TotalMilliseconds);

                    await using var scope = _scopeFactory.CreateAsyncScope();
                    var incomingRequests = scope.ServiceProvider.GetRequiredService<IIncomingRequestService>();
                    await incomingRequests.IncrementRetryAsync(eventName, operationId);
                }
            })
            .Build();
    }
}
