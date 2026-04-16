using OrderProcessing.Domain.Entities;

namespace OrderProcessing.Application.Interfaces;

public interface IPaymentService
{
    Task ProcessPaymentAsync(Guid orderId, decimal amount, string operationId);
    Task<List<Payment>> GetAllPaymentsAsync();
}
