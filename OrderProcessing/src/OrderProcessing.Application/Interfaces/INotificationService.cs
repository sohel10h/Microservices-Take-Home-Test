using OrderProcessing.Domain.Entities;

namespace OrderProcessing.Application.Interfaces;

public interface INotificationService
{
    Task SendNotificationAsync(Guid orderId, decimal amount, string operationId);
    Task<List<Notification>> GetAllNotificationsAsync();
}
