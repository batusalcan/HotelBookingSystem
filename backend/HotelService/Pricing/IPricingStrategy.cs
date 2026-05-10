namespace HotelService.Pricing;

public interface IPricingStrategy
{
    decimal Apply(decimal basePrice);
}
