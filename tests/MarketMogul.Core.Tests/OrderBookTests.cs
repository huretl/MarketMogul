using MarketMogul.Core.Analysis;
using static MarketMogul.Core.Tests.Market;

namespace MarketMogul.Core.Tests;

public class OrderBookTests
{
    [Fact]
    public void Buys_cheapest_first_and_stops_above_max_price()
    {
        var purchases = OrderBook.PlanPurchases([Listed(300), Listed(100), Listed(200), Listed(500)], maxPricePerUnit: 300, maxUnits: 10);

        Assert.Equal([100, 200, 300], purchases.Select(p => p.PricePerUnit));
    }

    [Fact]
    public void Stops_at_max_units()
    {
        var purchases = OrderBook.PlanPurchases([Listed(100, 3), Listed(110, 3), Listed(120, 3)], maxPricePerUnit: 1000, maxUnits: 6);

        Assert.Equal(6, purchases.Sum(p => p.Quantity));
    }

    [Fact]
    public void Skips_stacks_too_large_to_buy_whole()
    {
        var purchases = OrderBook.PlanPurchases([Listed(100, 99), Listed(150, 2)], maxPricePerUnit: 1000, maxUnits: 5);

        var purchase = Assert.Single(purchases);
        Assert.Equal(150, purchase.PricePerUnit);
    }
}
