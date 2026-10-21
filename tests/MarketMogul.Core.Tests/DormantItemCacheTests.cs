using MarketMogul.Core.Analysis;

namespace MarketMogul.Core.Tests;

public class DormantItemCacheTests
{
    private static readonly DateTime Now = new(2026, 10, 21, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Skips_dormant_items_until_recheck_delay()
    {
        var cache = new DormantItemCache();
        cache.ItemsToCheck(1, [10, 20], Now);
        cache.Update(10, dormant: true, Now);
        cache.Update(20, dormant: false, Now);

        Assert.Equal([20u], cache.ItemsToCheck(1, [10, 20], Now.AddHours(1)));
        Assert.Equal([10u, 20u], cache.ItemsToCheck(1, [10, 20], Now.AddHours(25)));
    }

    [Fact]
    public void Resets_when_home_world_changes()
    {
        var cache = new DormantItemCache();
        cache.ItemsToCheck(1, [10], Now);
        cache.Update(10, dormant: true, Now);

        Assert.Equal([10u], cache.ItemsToCheck(2, [10], Now.AddHours(1)));
    }
}
