namespace OrderProcessing.Domain.Events;

public class PaymentSucceededEvent
{
    public string OperationId { get; set; } = default!;
    public Guid OrderId { get; set; }
    public Guid PaymentId { get; set; }
    public decimal Amount { get; set; }
}
