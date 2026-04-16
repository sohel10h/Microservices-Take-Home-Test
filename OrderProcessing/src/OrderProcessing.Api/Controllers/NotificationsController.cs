using Microsoft.AspNetCore.Mvc;
using OrderProcessing.Api.Models;
using OrderProcessing.Application.Interfaces;

namespace OrderProcessing.Api.Controllers;

[ApiController]
[Route("api/notifications")]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationsController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<NotificationResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetNotifications()
    {
        var notifications = await _notificationService.GetAllNotificationsAsync();
        var response = notifications.Select(n => new NotificationResponse
        {
            Id = n.Id,
            OrderId = n.OrderId,
            Message = n.Message,
            CreatedOnUtc = n.CreatedOnUtc
        }).ToList();

        return Ok(response);
    }
}
