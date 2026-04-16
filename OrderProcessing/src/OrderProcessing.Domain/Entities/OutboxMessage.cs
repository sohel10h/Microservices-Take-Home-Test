using OrderProcessing.Domain.Enums;

namespace OrderProcessing.Domain.Entities;

public class OutboxMessage
{
    public Guid Id { get; set; }
    public string TopicName { get; set; } = default!;
    public string OperationId { get; set; } = default!;
    public ProcessingStatus Status { get; set; }
    public string BodyJson { get; set; } = default!;
    public int RetryCount { get; set; }
    public string? LastError { get; set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime UpdatedOnUtc { get; set; }
}
