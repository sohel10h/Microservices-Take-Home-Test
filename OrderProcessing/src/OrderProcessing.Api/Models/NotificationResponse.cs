namespace OrderProcessing.Api.Models;

public class NotificationResponse
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public string Message { get; set; } = default!;
    public DateTime CreatedOnUtc { get; set; }
}
