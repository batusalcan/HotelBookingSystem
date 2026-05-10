using HotelService.Pricing;

namespace HotelService.Tests;

public class PricingStrategyTests
{
    [Theory]
    [InlineData(1000, 850)]
    [InlineData(200, 170)]
    [InlineData(333.33, 283.33)]
    public void AuthenticatedPricingStrategy_Apply_Returns15PercentDiscount(decimal input, decimal expected)
    {
        var strategy = new AuthenticatedPricingStrategy();
        Assert.Equal(expected, strategy.Apply(input));
    }

    [Theory]
    [InlineData(1000)]
    [InlineData(0)]
    [InlineData(999.99)]
    public void GuestPricingStrategy_Apply_ReturnsOriginalPrice(decimal input)
    {
        var strategy = new GuestPricingStrategy();
        Assert.Equal(input, strategy.Apply(input));
    }
}
