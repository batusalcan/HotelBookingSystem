namespace HotelService.Pricing;

/// <summary>Applies a 15% discount — used when a valid user JWT is present in the request.</summary>
public class AuthenticatedPricingStrategy : IPricingStrategy
{
    private const decimal DiscountRate = 0.15m;

    /// <precondition>basePrice >= 0</precondition>
    /// <postcondition>returns basePrice * 0.85 (15% discount applied)</postcondition>
    public decimal Apply(decimal basePrice) => Math.Round(basePrice * (1 - DiscountRate), 2);
}
