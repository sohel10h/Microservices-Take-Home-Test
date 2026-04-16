using Microsoft.Extensions.Logging;
using OrderProcessing.Application.Interfaces;
using OrderProcessing.Domain.Events;
using OrderProcessing.Domain.Interfaces;

namespace OrderProcessing.Application.EventHandlers;

public class OrderNotificationEventHandler : IIntegrationEventHandler<OrderNotificationEvent>
{
    private readonly IIncomingRequestService _incomingRequests;
    private readonly ILogger<OrderNotificationEventHandler> _logger;

    private const string EventName = "OrderNotificationEvent";

    public OrderNotificationEventHandler(
        IIncomingRequestService incomingRequests,
        ILogger<OrderNotificationEventHandler> logger)
    {
        _incomingRequests = incomingRequests;
        _logger = logger;
    }

    public async Task HandleAsync(OrderNotificationEvent message, CancellationToken cancellationToken = default)
    {
        if (await _incomingRequests.HasProcessedAsync(EventName, message.OperationId))
            return;

        await _incomingRequests.StartProcessingAsync(EventName, message.OperationId);

        _logger.LogInformation("Order {OrderId} fully processed. Chain complete.", message.OrderId);

        await _incomingRequests.MarkCompletedAsync(EventName, message.OperationId);
    }
}
