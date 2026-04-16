using OrderProcessing.Application.Interfaces;
using OrderProcessing.Application.Interfaces.Repositories;
using OrderProcessing.Domain.Entities;
using OrderProcessing.Domain.Enums;

namespace OrderProcessing.Application.Services;

public class IncomingRequestService : IIncomingRequestService
{
    private readonly IIncomingRequestRepository _repo;

    public IncomingRequestService(IIncomingRequestRepository repo) => _repo = repo;

    public Task<bool> HasProcessedAsync(string eventName, string operationId)
    {
        return _repo.ExistsCompletedAsync(eventName, operationId);
    }
     

    public async Task StartProcessingAsync(string eventName, string operationId)
    {
        // Idempotent check  if a record already exists than skip the insert.
        var existing = await _repo.FindByEventAsync(eventName, operationId);
        if (existing != null) return;

        _repo.Add(new IncomingRequest
        {
            Id = Guid.NewGuid(),
            EventName = eventName,
            OperationId = operationId,
            Status = ProcessingStatus.Processing,
            RetryCount = 0,
            CreatedOnUtc = DateTime.UtcNow,
            UpdatedOnUtc = DateTime.UtcNow
        });
        await _repo.SaveChangesAsync();
    }

    public async Task IncrementRetryAsync(string eventName, string operationId)
    {
        var entry = await _repo.FindByEventAsync(eventName, operationId);
        if (entry is null) return;

        entry.RetryCount++;
        entry.UpdatedOnUtc = DateTime.UtcNow;
        await _repo.SaveChangesAsync();
    }

    public async Task MarkCompletedAsync(string eventName, string operationId)
    {
        var entry = await _repo.FindByEventAsync(eventName, operationId);
        if (entry is null) return;

        entry.Status = ProcessingStatus.Completed;
        entry.UpdatedOnUtc = DateTime.UtcNow;
        await _repo.SaveChangesAsync();
    }

    public async Task MarkErrorAsync(string eventName, string operationId)
    {
        var entry = await _repo.FindByEventAsync(eventName, operationId);
        if (entry is null) return;

        entry.Status = ProcessingStatus.Error;
        entry.UpdatedOnUtc = DateTime.UtcNow;
        await _repo.SaveChangesAsync();
    }
}
