using MarketMogul.Core.Analysis;

namespace MarketMogul.Core.Tests;

public class PortfolioOptimizerTests
{
    private static readonly TradingSettings Settings = new() { BuyTaxRate = 0, SellFeeRate = 0 };

    private static Opportunity Opp(uint itemId, long sellPrice, params (long Price, int Quantity)[] purchases) =>
        Opportunity.Create(
            itemId, false, sellPrice,
            purchases.Select(p => new Purchase(2, "World2", p.Price, p.Quantity)).ToList(),
            salesPerDay: 1, homeListedQuantity: 0, confidence: 1, Settings);

    private static int Stack999(uint _) => 999;

    [Fact]
    public void Prefers_best_profit_for_the_money()
    {
        var cheapGood = Opp(1, 200, (100, 10));   // coût 1 000, profit 1 000
        var expensiveMeh = Opp(2, 1100, (1000, 10)); // coût 10 000, profit 1 000

        var portfolio = PortfolioOptimizer.Optimize([expensiveMeh, cheapGood], budget: 5000, slots: 10, Stack999, Settings);

        var position = Assert.Single(portfolio.Positions);
        Assert.Equal(1u, position.Opportunity.ItemId);
    }

    [Fact]
    public void Trims_to_cheapest_listings_that_fit_budget()
    {
        var opportunity = Opp(1, 500, (100, 5), (200, 5), (300, 5));

        var portfolio = PortfolioOptimizer.Optimize([opportunity], budget: 1600, slots: 10, Stack999, Settings);

        var position = Assert.Single(portfolio.Positions);
        Assert.Equal(10, position.Opportunity.Units);
        Assert.Equal(1500, portfolio.TotalCost);
        Assert.Equal(3500, portfolio.ExpectedProfit);
    }

    [Fact]
    public void Unstackable_items_use_one_slot_per_unit()
    {
        var gear = Opp(1, 50_000, (10_000, 1), (10_000, 1), (10_000, 1));

        var portfolio = PortfolioOptimizer.Optimize([gear], budget: 1_000_000, slots: 2, _ => 1, Settings);

        Assert.Equal(2, portfolio.SlotsUsed);
        Assert.Equal(2, portfolio.Positions[0].Opportunity.Units);
    }

    [Fact]
    public void Stops_when_slots_are_exhausted()
    {
        var portfolio = PortfolioOptimizer.Optimize(
            [Opp(1, 200, (100, 1)), Opp(2, 200, (100, 1)), Opp(3, 200, (100, 1))],
            budget: 1_000_000, slots: 2, Stack999, Settings);

        Assert.Equal(2, portfolio.Positions.Count);
    }

    [Fact]
    public void Empty_without_budget()
    {
        var portfolio = PortfolioOptimizer.Optimize([Opp(1, 200, (100, 1))], budget: 0, slots: 20, Stack999, Settings);

        Assert.Empty(portfolio.Positions);
    }

    [Theory]
    [InlineData(1, 999, 1)]
    [InlineData(999, 999, 1)]
    [InlineData(1000, 999, 2)]
    [InlineData(3, 1, 3)]
    public void Slots_for_units(int units, int stack, int expected)
    {
        Assert.Equal(expected, PortfolioOptimizer.SlotsFor(units, stack));
    }
}
