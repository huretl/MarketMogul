using MarketMogul.Core.Analysis;
using MarketMogul.Core.Ledger;

namespace MarketMogul.Core.Tests;

public class PlanProgressTests
{
    private static readonly DateTime PlanTime = new(2026, 10, 21, 12, 0, 0, DateTimeKind.Utc);

    private static PlannedPurchase Planned(long price, int quantity, uint world = 2, uint item = 42, bool hq = false) =>
        new(item, hq, new Purchase(world, $"World{world}", price, quantity));

    private static PurchaseRecord Bought(long price, int quantity, uint world = 2, uint item = 42, bool hq = false, int minutesAfterPlan = 5) =>
        new(PlanTime.AddMinutes(minutesAfterPlan), item, hq, world, $"World{world}", price, quantity, null);

    [Fact]
    public void Matches_identical_listing()
    {
        var result = PlanProgress.Bought([Planned(100, 5), Planned(120, 3)], [Bought(120, 3)], PlanTime);

        Assert.Equal([false, true], result);
    }

    [Fact]
    public void One_purchase_ticks_only_one_identical_listing()
    {
        var result = PlanProgress.Bought([Planned(100, 5), Planned(100, 5)], [Bought(100, 5)], PlanTime);

        Assert.Equal([true, false], result);
    }

    [Fact]
    public void Ignores_purchases_before_plan_and_on_other_worlds_or_quality()
    {
        var result = PlanProgress.Bought(
            [Planned(100, 5)],
            [Bought(100, 5, minutesAfterPlan: -10), Bought(100, 5, world: 3), Bought(100, 5, hq: true)],
            PlanTime);

        Assert.Equal([false], result);
    }
}
