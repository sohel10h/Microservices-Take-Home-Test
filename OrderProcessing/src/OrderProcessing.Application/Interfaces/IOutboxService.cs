using OrderProcessing.Domain.Entities;

namespace OrderProcessing.Application.Interfaces;

public interface IOutboxService
{
    Task<List<OutboxMessage>> GetPendingByTopicAsync(string topicName);
    Task MarkCompletedAsync(Guid id);
    Task MarkErrorAsync(Guid id, string error);
}
