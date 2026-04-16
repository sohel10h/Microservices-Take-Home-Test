namespace OrderProcessing.Application.Interfaces;

public interface IIncomingRequestService
{
    Task<bool> HasProcessedAsync(string eventName, string operationId);
    Task StartProcessingAsync(string eventName, string operationId);
    Task IncrementRetryAsync(string eventName, string operationId);
    Task MarkCompletedAsync(string eventName, string operationId);
    Task MarkErrorAsync(string eventName, string operationId);
}
