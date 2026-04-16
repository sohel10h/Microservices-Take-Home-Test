using Microsoft.EntityFrameworkCore;
using OrderProcessing.Application.Interfaces.Repositories;
using OrderProcessing.Domain.Entities;
using OrderProcessing.Domain.Enums;
using OrderProcessing.Infrastructure.Data;

namespace OrderProcessing.Infrastructure.Repositories;

public class OutboxRepository : Repository<OutboxMessage>, IOutboxRepository
{
    public OutboxRepository(AppDbContext db) : base(db) { }

    public Task<List<OutboxMessage>> GetPendingByTopicAsync(string topicName, int maxRetryAttempts, int batchSize)
    {
        return Db.OutboxMessages
            .Where(x => x.TopicName == topicName && x.Status == ProcessingStatus.Pending && x.RetryCount < maxRetryAttempts)
            .OrderBy(x => x.CreatedOnUtc)
            .Take(batchSize)
            .ToListAsync();
    }
}
