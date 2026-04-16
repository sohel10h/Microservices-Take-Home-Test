using Microsoft.Extensions.Options;
using OrderProcessing.Application.Interfaces;
using OrderProcessing.Application.Interfaces.Repositories;
using OrderProcessing.Application.Settings;
using OrderProcessing.Domain.Entities;
using OrderProcessing.Domain.Enums;

namespace OrderProcessing.Application.Services;

public class OutboxService : IOutboxService
{
    private readonly IOutboxRepository _outbox;
    private readonly RetrySettings _retrySettings;

    public OutboxService(IOutboxRepository outbox, IOptions<RetrySettings> retryOptions)
    {
        _outbox = outbox;
        _retrySettings = retryOptions.Value;
    }

    public Task<List<OutboxMessage>> GetPendingByTopicAsync(string topicName)
        => _outbox.GetPendingByTopicAsync(
            topicName,
            _retrySettings.MaxRetryAttempts,
            _retrySettings.PendingBatchSize);

    public async Task MarkCompletedAsync(Guid id)
    {
        var msg = await _outbox.FindByIdAsync(id);
        if (msg is null) return;

        msg.Status = ProcessingStatus.Completed;
        msg.UpdatedOnUtc = DateTime.UtcNow;
        await _outbox.SaveChangesAsync();
    }

    public async Task MarkErrorAsync(Guid id, string error)
    {
        var msg = await _outbox.FindByIdAsync(id);
        if (msg is null) return;

        msg.RetryCount++;
        msg.LastError = error;
        msg.Status = msg.RetryCount >= _retrySettings.MaxRetryAttempts ? ProcessingStatus.Error : ProcessingStatus.Pending;
        msg.UpdatedOnUtc = DateTime.UtcNow;
        await _outbox.SaveChangesAsync();
    }
}
