using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using OrderProcessing.Application.EventHandlers;
using OrderProcessing.Application.Services;
using OrderProcessing.Domain.Entities;
using OrderProcessing.Domain.Enums;
using OrderProcessing.Domain.Events;
using OrderProcessing.Infrastructure.Data;
using OrderProcessing.Infrastructure.Repositories;

namespace OrderProcessing.Tests.EventHandlers;

public class OrderCreatedEventHandlerTests
{
    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task HandleAsync_ProcessesPaymentAndQueuesOutboxMessage()
    {
        var db = CreateDb();
        var paymentService = new PaymentService(
            new Repository<Payment>(db),
            new Repository<OutboxMessage>(db),
            Mock.Of<ILogger<PaymentService>>());
        var incomingService = new IncomingRequestService(new IncomingRequestRepository(db));
        var handler = new OrderCreatedEventHandler(paymentService, incomingService, Mock.Of<ILogger<OrderCreatedEventHandler>>());

        var @event = new OrderCreatedEvent
        {
            OperationId = "op-001",
            OrderId = Guid.NewGuid(),
            CustomerName = "Jane",
            CustomerEmail = "jane@example.com",
            Amount = 49.99m
        };

        await handler.HandleAsync(@event);

        Assert.Equal(1, await db.Payments.CountAsync());
        Assert.Equal(1, await db.OutboxMessages.CountAsync());

        var outbox = await db.OutboxMessages.FirstAsync();
        Assert.Equal("payment.succeeded", outbox.TopicName);

        var incoming = await db.IncomingRequests.FirstAsync();
        Assert.Equal(ProcessingStatus.Completed, incoming.Status);
        Assert.Equal("OrderCreatedEvent", incoming.EventName);
    }

    [Fact]
    public async Task HandleAsync_SkipsDuplicateEvent()
    {
        var db = CreateDb();

        db.IncomingRequests.Add(new IncomingRequest
        {
            Id = Guid.NewGuid(),
            EventName = "OrderCreatedEvent",
            OperationId = "op-dup",
            Status = ProcessingStatus.Completed,
            CreatedOnUtc = DateTime.UtcNow,
            UpdatedOnUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var paymentService = new PaymentService(
            new Repository<Payment>(db),
            new Repository<OutboxMessage>(db),
            Mock.Of<ILogger<PaymentService>>());
        var incomingService = new IncomingRequestService(new IncomingRequestRepository(db));
        var handler = new OrderCreatedEventHandler(paymentService, incomingService, Mock.Of<ILogger<OrderCreatedEventHandler>>());

        var @event = new OrderCreatedEvent
        {
            OperationId = "op-dup",
            OrderId = Guid.NewGuid(),
            CustomerName = "Jane",
            CustomerEmail = "jane@example.com",
            Amount = 49.99m
        };

        await handler.HandleAsync(@event);

        Assert.Equal(0, await db.Payments.CountAsync());
    }
}
