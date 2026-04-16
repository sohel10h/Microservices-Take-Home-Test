using OrderProcessing.Application.DTOs;
using OrderProcessing.Domain.Entities;

namespace OrderProcessing.Application.Interfaces;

public interface IOrderService
{
    Task<Order> CreateOrderAsync(CreateOrderRequest request, string operationId);
    Task<List<Order>> GetAllOrdersAsync();
}
