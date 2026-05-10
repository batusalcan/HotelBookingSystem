namespace HotelService.Pricing;

/// <summary>Returns base price unchanged — used when no valid JWT is present in the request.</summary>
public class GuestPricingStrategy : IPricingStrategy
{
    /// <precondition>basePrice >= 0</precondition>
    /// <postcondition>returns basePrice unmodified</postcondition>
    public decimal Apply(decimal basePrice) => basePrice;
}
