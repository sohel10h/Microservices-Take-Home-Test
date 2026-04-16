using System.ComponentModel.DataAnnotations;

namespace OrderProcessing.Application.DTOs;

public class CreateOrderRequest
{
    [Required]
    public string CustomerName { get; set; } = default!;

    [Required]
    [EmailAddress]
    public string CustomerEmail { get; set; } = default!;

    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than zero.")]
    public decimal Amount { get; set; }
}
