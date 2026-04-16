using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using OrderProcessing.Api.BackgroundServices;
using OrderProcessing.Api.Middleware;
using OrderProcessing.Application.EventHandlers;
using OrderProcessing.Application.Interfaces;
using OrderProcessing.Application.Interfaces.Repositories;
using OrderProcessing.Application.Settings;
using OrderProcessing.Application.Services;
using OrderProcessing.Domain.Events;
using OrderProcessing.Domain.Interfaces;
using OrderProcessing.Infrastructure.Data;
using OrderProcessing.Infrastructure.EventBus;
using OrderProcessing.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Order Processing API",
        Version = "v1",
        Description = "Event-driven order processing with outbox pattern."
    });
});

builder.Services.Configure<RetrySettings>(builder.Configuration.GetSection("RetryPolicy"));

builder.Services.AddDbContext<AppDbContext>(opts =>
    opts.UseInMemoryDatabase("OrderProcessingDb"));

builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<IOutboxRepository, OutboxRepository>();
builder.Services.AddScoped<IIncomingRequestRepository, IncomingRequestRepository>();

builder.Services.AddSingleton<IInMemoryEventBus, InMemoryEventBus>();

builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IOutboxService, OutboxService>();
builder.Services.AddScoped<IIncomingRequestService, IncomingRequestService>();

builder.Services.AddScoped<IIntegrationEventHandler<OrderCreatedEvent>, OrderCreatedEventHandler>();
builder.Services.AddScoped<IIntegrationEventHandler<PaymentSucceededEvent>, PaymentSucceededEventHandler>();
builder.Services.AddScoped<IIntegrationEventHandler<OrderNotificationEvent>, OrderNotificationEventHandler>();

builder.Services.AddHostedService<EventBusSubscriberService>();
builder.Services.AddHostedService<OrderCreatedOutboxWorker>();
builder.Services.AddHostedService<PaymentSucceededOutboxWorker>();
builder.Services.AddHostedService<OrderNotificationOutboxWorker>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseMiddleware<OperationIdMiddleware>();
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
