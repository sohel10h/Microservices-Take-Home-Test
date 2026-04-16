using Microsoft.Extensions.Logging;
using OrderProcessing.Application.Interfaces;
using OrderProcessing.Domain.Events;
using OrderProcessing.Domain.Interfaces;

namespace OrderProcessing.Application.EventHandlers;

public class PaymentSucceededEventHandler : IIntegrationEventHandler<PaymentSucceededEvent>
{
    private readonly INotificationService _notificationService;
    private readonly IIncomingRequestService _incomingRequests;
    private readonly ILogger<PaymentSucceededEventHandler> _logger;

    private const string EventName = "PaymentSucceededEvent";

    public PaymentSucceededEventHandler(
        INotificationService notificationService,
        IIncomingRequestService incomingRequests,
        ILogger<PaymentSucceededEventHandler> logger)
    {
        _notificationService = notificationService;
        _incomingRequests = incomingRequests;
        _logger = logger;
    }

    public async Task HandleAsync(PaymentSucceededEvent message, CancellationToken cancellationToken = default)
    {
        if (await _incomingRequests.HasProcessedAsync(EventName, message.OperationId))
        {
            _logger.LogWarning("Duplicate event skipped. EventName: {EventName}, OperationId: {OperationId}", EventName, message.OperationId);
            return;
        }

        await _incomingRequests.StartProcessingAsync(EventName, message.OperationId);

        try
        {
            await _notificationService.SendNotificationAsync(message.OrderId, message.Amount, message.OperationId);
            await _incomingRequests.MarkCompletedAsync(EventName, message.OperationId);

            _logger.LogInformation("Notification sent for order {OrderId}", message.OrderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send notification for order {OrderId}", message.OrderId);
            await _incomingRequests.MarkErrorAsync(EventName, message.OperationId);
        }
    }
}
