using Microsoft.EntityFrameworkCore;
using OrderProcessing.Application.Interfaces.Repositories;
using OrderProcessing.Domain.Entities;
using OrderProcessing.Domain.Enums;
using OrderProcessing.Infrastructure.Data;

namespace OrderProcessing.Infrastructure.Repositories;

public class IncomingRequestRepository : Repository<IncomingRequest>, IIncomingRequestRepository
{
    public IncomingRequestRepository(AppDbContext db) : base(db) { }

    public Task<IncomingRequest?> FindByEventAsync(string eventName, string operationId)
    {
        return Db.IncomingRequests
            .FirstOrDefaultAsync(x => x.EventName == eventName && x.OperationId == operationId);
    }

    public Task<bool> ExistsCompletedAsync(string eventName, string operationId)
    {
        return Db.IncomingRequests
            .AnyAsync(x => x.EventName == eventName
                        && x.OperationId == operationId
                        && x.Status == ProcessingStatus.Completed);
    }
}
