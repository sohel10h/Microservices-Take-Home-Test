using OrderProcessing.Domain.Enums;

namespace OrderProcessing.Domain.Entities;

public class IncomingRequest
{
    public Guid Id { get; set; }
    public string EventName { get; set; } = default!;
    public string OperationId { get; set; } = default!;
    public ProcessingStatus Status { get; set; }
    public int RetryCount { get; set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime UpdatedOnUtc { get; set; }
}
