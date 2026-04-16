using System.Text.Json;
using Microsoft.Extensions.Logging;
using OrderProcessing.Application.Interfaces;
using OrderProcessing.Application.Interfaces.Repositories;
using OrderProcessing.Domain.Entities;
using OrderProcessing.Domain.Enums;
using OrderProcessing.Domain.Events;

namespace OrderProcessing.Application.Services;

public class NotificationService : INotificationService
{
    private readonly IRepository<Notification> _notificationsRepo;
    private readonly IRepository<OutboxMessage> _outboxRepo;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(IRepository<Notification> notifications, IRepository<OutboxMessage> outbox, ILogger<NotificationService> logger)
    {
        _notificationsRepo = notifications;
        _outboxRepo = outbox;
        _logger = logger;
    }

    public async Task SendNotificationAsync(Guid orderId, decimal amount, string operationId)
    {
        var message = $"Payment of {amount:C} received for order {orderId}. Thank you!";

        _notificationsRepo.Add(new Notification
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            Message = message,
            CreatedOnUtc = DateTime.UtcNow
        });

        _logger.LogInformation("[NOTIFICATION] Order {OrderId}: {Message}", orderId, message);

        _outboxRepo.Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            TopicName = "order.notification",
            OperationId = operationId,
            Status = ProcessingStatus.Pending,
            BodyJson = JsonSerializer.Serialize(new OrderNotificationEvent
            {
                OperationId = operationId,
                OrderId = orderId,
                Message = message
            }),
            RetryCount = 0,
            CreatedOnUtc = DateTime.UtcNow,
            UpdatedOnUtc = DateTime.UtcNow
        });

        await _notificationsRepo.SaveChangesAsync();
    }

    public async Task<List<Notification>> GetAllNotificationsAsync()
    {
        var notifications = await _notificationsRepo.GetAllAsync();
        return notifications.OrderByDescending(x => x.CreatedOnUtc).ToList();
    }
}
