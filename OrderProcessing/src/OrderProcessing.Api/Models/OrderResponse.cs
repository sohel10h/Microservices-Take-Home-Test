namespace OrderProcessing.Api.Models;

public class OrderResponse
{
    public Guid Id { get; set; }
    public string CustomerName { get; set; } = default!;
    public string CustomerEmail { get; set; } = default!;
    public decimal Amount { get; set; }
    public string Status { get; set; } = default!;
    public DateTime CreatedOnUtc { get; set; }
}
