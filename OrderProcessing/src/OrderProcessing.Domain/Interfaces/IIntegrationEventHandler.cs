namespace OrderProcessing.Domain.Interfaces;

public interface IIntegrationEventHandler<T>
{
    Task HandleAsync(T message, CancellationToken cancellationToken = default);
}
