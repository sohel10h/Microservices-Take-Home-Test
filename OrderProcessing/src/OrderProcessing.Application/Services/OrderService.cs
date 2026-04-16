using System.Text.Json;
using Microsoft.Extensions.Logging;
using OrderProcessing.Application.DTOs;
using OrderProcessing.Application.Interfaces;
using OrderProcessing.Application.Interfaces.Repositories;
using OrderProcessing.Domain.Entities;
using OrderProcessing.Domain.Enums;
using OrderProcessing.Domain.Events;

namespace OrderProcessing.Application.Services;

public class OrderService : IOrderService
{
    private readonly IRepository<Order> _ordersRepo;
    private readonly IRepository<OutboxMessage> _outboxRepo;
    private readonly ILogger<OrderService> _logger;

    public OrderService(IRepository<Order> orders, IRepository<OutboxMessage> outbox, ILogger<OrderService> logger)
    {
        _ordersRepo = orders;
        _outboxRepo = outbox;
        _logger = logger;
    }

    public async Task<Order> CreateOrderAsync(CreateOrderRequest request, string operationId)
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            CustomerName = request.CustomerName,
            CustomerEmail = request.CustomerEmail,
            Amount = request.Amount,
            Status = OrderStatus.Created,
            CreatedOnUtc = DateTime.UtcNow,
            UpdatedOnUtc = DateTime.UtcNow
        };

        _ordersRepo.Add(order);

        _outboxRepo.Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            TopicName = "order.created",
            OperationId = operationId,
            Status = ProcessingStatus.Pending,
            BodyJson = JsonSerializer.Serialize(new OrderCreatedEvent
            {
                OperationId = operationId,
                OrderId = order.Id,
                CustomerName = order.CustomerName,
                CustomerEmail = order.CustomerEmail,
                Amount = order.Amount
            }),
            RetryCount = 0,
            CreatedOnUtc = DateTime.UtcNow,
            UpdatedOnUtc = DateTime.UtcNow
        });

        await _ordersRepo.SaveChangesAsync();

        _logger.LogInformation("Order {OrderId} created for customer {CustomerName}", order.Id, order.CustomerName);

        return order;
    }

    public async Task<List<Order>> GetAllOrdersAsync()
    {
        var orders = await _ordersRepo.GetAllAsync();
        return orders.OrderByDescending(x => x.CreatedOnUtc).ToList();
    }
}
