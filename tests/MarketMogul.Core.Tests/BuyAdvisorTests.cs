using MarketMogul.Core.Analysis;

namespace MarketMogul.Core.Tests;

public class BuyAdvisorTests
{
    private static readonly TradingSettings Settings = new() { Style = TradingStyle.Balanced, MinProfitPerUnit = 100 };

    private static HomeMarketStats Stats(long? median, double salesPerDay) =>
        new(median, median, median, salesPerDay, SaleSamples: 10, LowestListing: null, ListedQuantity: 0);

    [Fact]
    public void Max_price_leaves_min_profit_after_taxes()
    {
        var limits = BuyAdvisor.Limits(Stats(10_000, 1), Settings);

        Assert.NotNull(limits);
        // (10 000 × 0,95 − 100) / 1,05
        Assert.Equal(8952, limits.MaxPricePerUnit);
        Assert.True(BuyAdvisor.UnitProfit(limits.MaxPricePerUnit, 10_000, Settings) >= 100);
        Assert.Equal(7, limits.MaxUnits);
    }

    [Fact]
    public void No_limits_when_item_does_not_sell()
    {
        Assert.Null(BuyAdvisor.Limits(Stats(10_000, 0.01), Settings));
        Assert.Null(BuyAdvisor.Limits(Stats(null, 5), Settings));
    }
}
