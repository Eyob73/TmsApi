using System.ComponentModel.DataAnnotations;

public class PaymentOptions
{
    [Required]
    public required string GatewayUrl { get; init; } = default!;

    [Range(100, 100000)]
    public required decimal MaxDepositBirr { get; init; }
}