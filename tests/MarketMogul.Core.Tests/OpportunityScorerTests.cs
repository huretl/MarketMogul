using MarketMogul.Core.Analysis;
using static MarketMogul.Core.Tests.Market;

namespace MarketMogul.Core.Tests;

public class OpportunityScorerTests
{
    private static readonly TradingSettings Settings = new()
    {
        Style = TradingStyle.Balanced,
        MinProfitPerUnit = 100,
        MinTotalProfit = 1000,
        MinConfidence = 0,
    };

    // Revente stable à 10 000, une vente par jour.
    private static readonly IEnumerable<Core.Universalis.Sale> SteadySales =
        Enumerable.Range(1, 10).Select(d => Sold(10_000, d));

    [Fact]
    public void Finds_profit_after_taxes()
    {
        var home = Data(SteadySales);
        var dc = Data(listings: [Listed(5000, 2)]);

        var opportunity = OpportunityScorer.Evaluate(42, false, home, dc, Home, Settings, Now, 60);

        Assert.NotNull(opportunity);
        Assert.Equal(2, opportunity.Units);
        Assert.Equal(10_500, opportunity.TotalCost);       // 2 × 5000 × 1,05
        Assert.Equal(19_000, opportunity.ExpectedRevenue); // 2 × 10 000 × 0,95
        Assert.Equal(8_500, opportunity.ExpectedProfit);
    }

    [Fact]
    public void Ignores_listings_on_home_world()
    {
        var home = Data(SteadySales);
        var dc = Data(listings: [Listed(5000, 2, world: Home)]);

        Assert.Null(OpportunityScorer.Evaluate(42, false, home, dc, Home, Settings, Now, 60));
    }

    [Fact]
    public void Rejects_spread_eaten_by_taxes()
    {
        // 9 500 × 1,05 = 9 975 d'achat contre 9 500 nets à la revente.
        var home = Data(SteadySales);
        var dc = Data(listings: [Listed(9500, 2)]);

        Assert.Null(OpportunityScorer.Evaluate(42, false, home, dc, Home, Settings, Now, 60));
    }

    [Fact]
    public void Rejects_items_that_do_not_sell()
    {
        var home = Data([Sold(10_000, 13)]);
        var dc = Data(listings: [Listed(1000)]);

        Assert.Null(OpportunityScorer.Evaluate(42, false, home, dc, Home, Settings, Now, 60));
    }

    [Fact]
    public void Buys_no_more_than_the_market_absorbs_within_horizon()
    {
        // ~1 vente / jour, horizon de 7 jours en style équilibré.
        var home = Data(SteadySales);
        var dc = Data(listings: Enumerable.Range(0, 20).Select(_ => Listed(5000)));

        var opportunity = OpportunityScorer.Evaluate(42, false, home, dc, Home, Settings, Now, 60);

        Assert.NotNull(opportunity);
        Assert.Equal(5, opportunity.Units); // 10 ventes / 14 jours × 7 jours
    }

    [Fact]
    public void Stale_data_lowers_confidence()
    {
        var dc = Data(listings: [Listed(5000, 2)]);

        var fresh = OpportunityScorer.Evaluate(42, false, Data(SteadySales), dc, Home, Settings, Now, 60);
        var stale = OpportunityScorer.Evaluate(42, false, Data(SteadySales, uploadedHoursAgo: 48), dc, Home, Settings, Now, 60);

        Assert.True(stale!.Confidence < fresh!.Confidence / 5);
    }

    [Fact]
    public void Vendor_purchase_is_untaxed_and_sized_by_demand()
    {
        var opportunity = OpportunityScorer.EvaluateVendor(42, vendorPrice: 5000, Data(SteadySales), Settings, Now, 60);

        Assert.NotNull(opportunity);
        var purchase = Assert.Single(opportunity.Purchases);
        Assert.True(purchase.IsVendor);
        Assert.Equal(5, opportunity.Units);              // 10 ventes / 14 jours × 7 jours
        Assert.Equal(25_000, opportunity.TotalCost);     // pas de taxe chez un PNJ
        Assert.Equal(47_500, opportunity.ExpectedRevenue);
    }

    [Fact]
    public void Vendor_rejected_when_resale_does_not_cover_price()
    {
        Assert.Null(OpportunityScorer.EvaluateVendor(42, vendorPrice: 9_500, Data(SteadySales), Settings, Now, 60));
    }
}
