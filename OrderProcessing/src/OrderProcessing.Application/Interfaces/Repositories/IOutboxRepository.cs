using OrderProcessing.Domain.Entities;

namespace OrderProcessing.Application.Interfaces.Repositories;

public interface IOutboxRepository : IRepository<OutboxMessage>
{
    Task<List<OutboxMessage>> GetPendingByTopicAsync(string topicName, int maxRetryAttempts, int batchSize);
}
