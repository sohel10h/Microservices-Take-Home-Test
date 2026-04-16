namespace OrderProcessing.Domain.Events;

public class OrderNotificationEvent
{
    public string OperationId { get; set; } = default!;
    public Guid OrderId { get; set; }
    public string Message { get; set; } = default!;
}
