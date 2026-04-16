using OrderProcessing.Domain.Entities;

namespace OrderProcessing.Application.Interfaces.Repositories;

public interface IIncomingRequestRepository : IRepository<IncomingRequest>
{
    Task<IncomingRequest?> FindByEventAsync(string eventName, string operationId);
    Task<bool> ExistsCompletedAsync(string eventName, string operationId);
}
