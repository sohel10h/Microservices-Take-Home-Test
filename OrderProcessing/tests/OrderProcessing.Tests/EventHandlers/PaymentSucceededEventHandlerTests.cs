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

public class PaymentSucceededEventHandlerTests
{
    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task HandleAsync_SavesNotificationAndQueuesOutboxMessage()
    {
        var db = CreateDb();
        var notificationService = new NotificationService(
            new Repository<Notification>(db),
            new Repository<OutboxMessage>(db),
            Mock.Of<ILogger<NotificationService>>());
        var incomingService = new IncomingRequestService(new IncomingRequestRepository(db));
        var handler = new PaymentSucceededEventHandler(notificationService, incomingService, Mock.Of<ILogger<PaymentSucceededEventHandler>>());

        var @event = new PaymentSucceededEvent
        {
            OperationId = "op-002",
            OrderId = Guid.NewGuid(),
            PaymentId = Guid.NewGuid(),
            Amount = 49.99m
        };

        await handler.HandleAsync(@event);

        Assert.Equal(1, await db.Notifications.CountAsync());
        Assert.Equal(1, await db.OutboxMessages.CountAsync());

        var outbox = await db.OutboxMessages.FirstAsync();
        Assert.Equal("order.notification", outbox.TopicName);

        var incoming = await db.IncomingRequests.FirstAsync();
        Assert.Equal(ProcessingStatus.Completed, incoming.Status);
        Assert.Equal("PaymentSucceededEvent", incoming.EventName);
    }

    [Fact]
    public async Task HandleAsync_SkipsDuplicateEvent()
    {
        var db = CreateDb();

        db.IncomingRequests.Add(new IncomingRequest
        {
            Id = Guid.NewGuid(),
            EventName = "PaymentSucceededEvent",
            OperationId = "op-dup",
            Status = ProcessingStatus.Completed,
            CreatedOnUtc = DateTime.UtcNow,
            UpdatedOnUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var notificationService = new NotificationService(
            new Repository<Notification>(db),
            new Repository<OutboxMessage>(db),
            Mock.Of<ILogger<NotificationService>>());
        var incomingService = new IncomingRequestService(new IncomingRequestRepository(db));
        var handler = new PaymentSucceededEventHandler(notificationService, incomingService, Mock.Of<ILogger<PaymentSucceededEventHandler>>());

        var @event = new PaymentSucceededEvent
        {
            OperationId = "op-dup",
            OrderId = Guid.NewGuid(),
            PaymentId = Guid.NewGuid(),
            Amount = 49.99m
        };

        await handler.HandleAsync(@event);

        Assert.Equal(0, await db.Notifications.CountAsync());
    }
}
