using MarketMogul.Core.Analysis;
using static MarketMogul.Core.Tests.Market;

namespace MarketMogul.Core.Tests;

public class PreFilterTests
{
    private static readonly TradingSettings Settings = new() { Style = TradingStyle.Balanced, MinProfitPerUnit = 100 };

    [Fact]
    public void Keeps_item_that_sells_regularly()
    {
        var history = Data(Enumerable.Range(1, 10).Select(d => Sold(10_000, d)));

        Assert.True(PreFilter.IsCandidate(history, Settings, Now, 100));
    }

    [Fact]
    public void Keeps_item_whose_hq_sells()
    {
        var history = Data(Enumerable.Range(1, 10).Select(d => Sold(10_000, d, hq: true)));

        Assert.True(PreFilter.IsCandidate(history, Settings, Now, 100));
    }

    [Fact]
    public void Drops_item_that_rarely_sells()
    {
        var history = Data([Sold(10_000, 13)]);

        Assert.False(PreFilter.IsCandidate(history, Settings, Now, 100));
    }

    [Fact]
    public void Dormant_only_without_any_sale()
    {
        Assert.True(PreFilter.IsDormant(Data()));
        Assert.False(PreFilter.IsDormant(Data([Sold(10_000, 13)])));
    }
}
