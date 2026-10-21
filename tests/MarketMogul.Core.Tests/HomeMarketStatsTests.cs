using MarketMogul.Core.Analysis;
using static MarketMogul.Core.Tests.Market;

namespace MarketMogul.Core.Tests;

public class HomeMarketStatsTests
{
    [Fact]
    public void Median_ignores_outlier_sales()
    {
        var home = Data([Sold(1000, 1), Sold(1100, 2), Sold(999_999, 3)]);

        var stats = HomeMarketStats.From(home, hq: false, Now, windowDays: 14, entriesRequested: 60);

        Assert.Equal(1100, stats.MedianSalePrice);
    }

    [Fact]
    public void Sales_outside_window_or_other_quality_are_ignored()
    {
        var home = Data([Sold(1000, 1), Sold(5000, 1, hq: true), Sold(1000, 30)]);

        var stats = HomeMarketStats.From(home, hq: false, Now, windowDays: 14, entriesRequested: 60);

        Assert.Equal(1, stats.SaleSamples);
        Assert.Equal(1 / 14.0, stats.SalesPerDay, precision: 6);
    }

    [Fact]
    public void Velocity_uses_covered_period_when_history_is_truncated()
    {
        // 4 ventes reçues sur 4 demandées : l'historique ne couvre que 2 jours, pas 14.
        var home = Data([Sold(1000, 0.5, 2), Sold(1000, 1), Sold(1000, 1.5), Sold(1000, 2)]);

        var stats = HomeMarketStats.From(home, hq: false, Now, windowDays: 14, entriesRequested: 4);

        Assert.Equal(5 / 2.0, stats.SalesPerDay, precision: 6);
    }

    [Fact]
    public void Target_price_undercuts_cheaper_competitor()
    {
        var home = Data([Sold(1000, 1)], [Listed(900, world: Home)]);

        var stats = HomeMarketStats.From(home, hq: false, Now, 14, 60);

        Assert.Equal(899, stats.TargetSellPrice);
    }

    [Fact]
    public void Target_price_is_median_when_listings_are_above_it()
    {
        var home = Data([Sold(1000, 1)], [Listed(1500, world: Home)]);

        var stats = HomeMarketStats.From(home, hq: false, Now, 14, 60);

        Assert.Equal(1000, stats.TargetSellPrice);
    }

    [Fact]
    public void No_target_price_without_sales()
    {
        var stats = HomeMarketStats.From(Data(listings: [Listed(1500, world: Home)]), hq: false, Now, 14, 60);

        Assert.Null(stats.TargetSellPrice);
    }
}
