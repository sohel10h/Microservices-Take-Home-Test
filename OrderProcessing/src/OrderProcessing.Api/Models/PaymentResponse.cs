namespace OrderProcessing.Api.Models;

public class PaymentResponse
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = default!;
    public DateTime CreatedOnUtc { get; set; }
}
