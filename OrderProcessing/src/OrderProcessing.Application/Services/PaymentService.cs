using System.Text.Json;
using Microsoft.Extensions.Logging;
using OrderProcessing.Application.Interfaces;
using OrderProcessing.Application.Interfaces.Repositories;
using OrderProcessing.Domain.Entities;
using OrderProcessing.Domain.Enums;
using OrderProcessing.Domain.Events;

namespace OrderProcessing.Application.Services;

public class PaymentService : IPaymentService
{
    private readonly IRepository<Payment> _paymentsRepo;
    private readonly IRepository<OutboxMessage> _outboxRepo;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(IRepository<Payment> payments, IRepository<OutboxMessage> outbox, ILogger<PaymentService> logger)
    {
        _paymentsRepo = payments;
        _outboxRepo = outbox;
        _logger = logger;
    }

    public async Task ProcessPaymentAsync(Guid orderId, decimal amount, string operationId)
    {
        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            Amount = amount,
            Status = ProcessingStatus.Completed,
            CreatedOnUtc = DateTime.UtcNow,
            UpdatedOnUtc = DateTime.UtcNow
        };

        _paymentsRepo.Add(payment);

        _outboxRepo.Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            TopicName = "payment.succeeded",
            OperationId = operationId,
            Status = ProcessingStatus.Pending,
            BodyJson = JsonSerializer.Serialize(new PaymentSucceededEvent
            {
                OperationId = operationId,
                OrderId = orderId,
                PaymentId = payment.Id,
                Amount = amount
            }),
            RetryCount = 0,
            CreatedOnUtc = DateTime.UtcNow,
            UpdatedOnUtc = DateTime.UtcNow
        });

        await _paymentsRepo.SaveChangesAsync();

        _logger.LogInformation("Payment {PaymentId} processed for order {OrderId}", payment.Id, orderId);
    }

    public async Task<List<Payment>> GetAllPaymentsAsync()
    {
        var payments = await _paymentsRepo.GetAllAsync();
        return payments.OrderByDescending(x => x.CreatedOnUtc).ToList();
    }
}
