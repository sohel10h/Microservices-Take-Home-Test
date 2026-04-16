using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using OrderProcessing.Application.DTOs;
using OrderProcessing.Application.Services;
using OrderProcessing.Domain.Enums;
using OrderProcessing.Infrastructure.Data;
using OrderProcessing.Infrastructure.Repositories;

namespace OrderProcessing.Tests.Services;

public class OrderServiceTests
{
    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task CreateOrderAsync_SavesOrderAndOutboxMessage()
    {
        var db = CreateDb();
        var service = new OrderService(
            new Repository<Domain.Entities.Order>(db),
            new Repository<Domain.Entities.OutboxMessage>(db),
            Mock.Of<ILogger<OrderService>>());

        var request = new CreateOrderRequest
        {
            CustomerName = "Jane Doe",
            CustomerEmail = "jane@example.com",
            Amount = 99.99m
        };

        var order = await service.CreateOrderAsync(request, "op-001");

        Assert.NotEqual(Guid.Empty, order.Id);
        Assert.Equal("Jane Doe", order.CustomerName);
        Assert.Equal(Domain.Enums.OrderStatus.Created, order.Status);

        Assert.Equal(1, await db.Orders.CountAsync());
        Assert.Equal(1, await db.OutboxMessages.CountAsync());

        var outbox = await db.OutboxMessages.FirstAsync();
        Assert.Equal("order.created", outbox.TopicName);
        Assert.Equal("op-001", outbox.OperationId);
        Assert.Equal(ProcessingStatus.Pending, outbox.Status);
    }

    [Fact]
    public async Task CreateOrderAsync_OutboxBodyJson_ContainsOrderId()
    {
        var db = CreateDb();
        var service = new OrderService(
            new Repository<Domain.Entities.Order>(db),
            new Repository<Domain.Entities.OutboxMessage>(db),
            Mock.Of<ILogger<OrderService>>());

        var order = await service.CreateOrderAsync(new CreateOrderRequest
        {
            CustomerName = "Bob",
            CustomerEmail = "bob@example.com",
            Amount = 50m
        }, "op-002");

        var outbox = await db.OutboxMessages.FirstAsync();
        Assert.Contains(order.Id.ToString(), outbox.BodyJson);
    }

    [Fact]
    public async Task GetAllOrdersAsync_ReturnsAllOrders()
    {
        var db = CreateDb();

        db.Orders.AddRange(
            new Domain.Entities.Order { Id = Guid.NewGuid(), CustomerName = "A", CustomerEmail = "a@a.com", Amount = 10, Status = Domain.Enums.OrderStatus.Created, CreatedOnUtc = DateTime.UtcNow, UpdatedOnUtc = DateTime.UtcNow },
            new Domain.Entities.Order { Id = Guid.NewGuid(), CustomerName = "B", CustomerEmail = "b@b.com", Amount = 20, Status = Domain.Enums.OrderStatus.Created, CreatedOnUtc = DateTime.UtcNow, UpdatedOnUtc = DateTime.UtcNow }
        );
        await db.SaveChangesAsync();

        var service = new OrderService(
            new Repository<Domain.Entities.Order>(db),
            new Repository<Domain.Entities.OutboxMessage>(db),
            Mock.Of<ILogger<OrderService>>());

        var orders = await service.GetAllOrdersAsync();

        Assert.Equal(2, orders.Count);
    }
}
