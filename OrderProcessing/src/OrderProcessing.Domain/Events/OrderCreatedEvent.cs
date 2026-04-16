namespace OrderProcessing.Domain.Events;

public class OrderCreatedEvent
{
    public string OperationId { get; set; } = default!;
    public Guid OrderId { get; set; }
    public string CustomerName { get; set; } = default!;
    public string CustomerEmail { get; set; } = default!;
    public decimal Amount { get; set; }
}
