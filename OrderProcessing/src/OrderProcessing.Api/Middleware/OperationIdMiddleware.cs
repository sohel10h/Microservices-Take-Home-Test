namespace OrderProcessing.Api.Middleware;

public class OperationIdMiddleware
{
    private readonly RequestDelegate _next;

    public OperationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var operationId = context.Request.Headers["operation_id"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(operationId))
            operationId = Guid.NewGuid().ToString();

        context.Items["OperationId"] = operationId;
        context.Response.Headers["operation_id"] = operationId;

        await _next(context);
    }
}
