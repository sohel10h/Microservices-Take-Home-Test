using OrderProcessing.Application.Interfaces;
using OrderProcessing.Domain.Entities;
using OrderProcessing.Domain.Interfaces;

namespace OrderProcessing.Api.BackgroundServices;

public abstract class OutboxBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    protected readonly IInMemoryEventBus EventBus;
    protected readonly ILogger Logger;

    protected OutboxBackgroundService(IServiceScopeFactory scopeFactory, IInMemoryEventBus eventBus, ILogger logger)
    {
        _scopeFactory = scopeFactory;
        EventBus = eventBus;
        Logger = logger;
    }

    protected abstract string TopicName { get; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var outboxService = scope.ServiceProvider.GetRequiredService<IOutboxService>();

                var messages = await outboxService.GetPendingByTopicAsync(TopicName);

                foreach (var msg in messages)
                {
                    try
                    {
                        await PublishAsync(msg, stoppingToken);
                        await outboxService.MarkCompletedAsync(msg.Id);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "Publish failed for message {MessageId} on topic {TopicName}", msg.Id, TopicName);
                        await outboxService.MarkErrorAsync(msg.Id, ex.Message);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Outbox worker error for topic {TopicName}", TopicName);
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    protected abstract Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken);
}
