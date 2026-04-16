using Microsoft.AspNetCore.Mvc;
using OrderProcessing.Api.Models;
using OrderProcessing.Application.Interfaces;

namespace OrderProcessing.Api.Controllers;

[ApiController]
[Route("api/payments")]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;

    public PaymentsController(IPaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<PaymentResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPayments()
    {
        var payments = await _paymentService.GetAllPaymentsAsync();
        var response = payments.Select(p => new PaymentResponse
        {
            Id = p.Id,
            OrderId = p.OrderId,
            Amount = p.Amount,
            Status = p.Status.ToString(),
            CreatedOnUtc = p.CreatedOnUtc
        }).ToList();

        return Ok(response);
    }
}
