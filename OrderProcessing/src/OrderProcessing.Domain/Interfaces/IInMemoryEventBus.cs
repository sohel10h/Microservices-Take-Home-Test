namespace OrderProcessing.Domain.Interfaces;

public interface IInMemoryEventBus
{
    Task PublishAsync<T>(string topicName, T message, CancellationToken cancellationToken = default);
    void Subscribe<T>(string topicName, Func<T, CancellationToken, Task> handler);
}
