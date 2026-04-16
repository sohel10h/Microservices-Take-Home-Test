using System.Collections.Concurrent;
using OrderProcessing.Domain.Interfaces;

namespace OrderProcessing.Infrastructure.EventBus;

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
