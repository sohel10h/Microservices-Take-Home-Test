using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using OrderProcessing.Application.Services;
using OrderProcessing.Domain.Enums;
using OrderProcessing.Infrastructure.Data;
using OrderProcessing.Infrastructure.Repositories;

namespace OrderProcessing.Tests.Services;

public class PaymentServiceTests
{
    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task ProcessPaymentAsync_SavesPaymentAndOutboxMessage()
    {
        var db = CreateDb();
        var service = new PaymentService(
            new Repository<Domain.Entities.Payment>(db),
            new Repository<Domain.Entities.OutboxMessage>(db),
            Mock.Of<ILogger<PaymentService>>());

        var orderId = Guid.NewGuid();

        await service.ProcessPaymentAsync(orderId, 75.00m, "op-pay-001");

        Assert.Equal(1, await db.Payments.CountAsync());
        Assert.Equal(1, await db.OutboxMessages.CountAsync());

        var payment = await db.Payments.FirstAsync();
        Assert.Equal(orderId, payment.OrderId);
        Assert.Equal(75.00m, payment.Amount);
        Assert.Equal(ProcessingStatus.Completed, payment.Status);

        var outbox = await db.OutboxMessages.FirstAsync();
        Assert.Equal("payment.succeeded", outbox.TopicName);
        Assert.Equal("op-pay-001", outbox.OperationId);
    }

    [Fact]
    public async Task GetAllPaymentsAsync_ReturnsAllPayments()
    {
        var db = CreateDb();

        db.Payments.AddRange(
            new Domain.Entities.Payment { Id = Guid.NewGuid(), OrderId = Guid.NewGuid(), Amount = 10, Status = ProcessingStatus.Completed, CreatedOnUtc = DateTime.UtcNow, UpdatedOnUtc = DateTime.UtcNow },
            new Domain.Entities.Payment { Id = Guid.NewGuid(), OrderId = Guid.NewGuid(), Amount = 20, Status = ProcessingStatus.Completed, CreatedOnUtc = DateTime.UtcNow, UpdatedOnUtc = DateTime.UtcNow }
        );
        await db.SaveChangesAsync();

        var service = new PaymentService(
            new Repository<Domain.Entities.Payment>(db),
            new Repository<Domain.Entities.OutboxMessage>(db),
            Mock.Of<ILogger<PaymentService>>());

        var payments = await service.GetAllPaymentsAsync();

        Assert.Equal(2, payments.Count);
    }
}
