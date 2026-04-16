using Microsoft.Extensions.Logging;
using OrderProcessing.Application.Interfaces;
using OrderProcessing.Domain.Events;
using OrderProcessing.Domain.Interfaces;

namespace OrderProcessing.Application.EventHandlers;

public class OrderCreatedEventHandler : IIntegrationEventHandler<OrderCreatedEvent>
{
    private readonly IPaymentService _paymentService;
    private readonly IIncomingRequestService _incomingRequests;
    private readonly ILogger<OrderCreatedEventHandler> _logger;

    private const string EventName = "OrderCreatedEvent";

    public OrderCreatedEventHandler(
        IPaymentService paymentService,
        IIncomingRequestService incomingRequests,
        ILogger<OrderCreatedEventHandler> logger)
    {
        _paymentService = paymentService;
        _incomingRequests = incomingRequests;
        _logger = logger;
    }

    public async Task HandleAsync(OrderCreatedEvent message, CancellationToken cancellationToken = default)
    {
        if (await _incomingRequests.HasProcessedAsync(EventName, message.OperationId))
        {
            _logger.LogWarning("Duplicate event skipped. EventName: {EventName}, OperationId: {OperationId}", EventName, message.OperationId);
            return;
        }

        await _incomingRequests.StartProcessingAsync(EventName, message.OperationId);

        try
        {
            await _paymentService.ProcessPaymentAsync(message.OrderId, message.Amount, message.OperationId);
            await _incomingRequests.MarkCompletedAsync(EventName, message.OperationId);

            _logger.LogInformation("Payment processed for order {OrderId}", message.OrderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process payment for order {OrderId}", message.OrderId);
            await _incomingRequests.MarkErrorAsync(EventName, message.OperationId);
        }
    }
}
