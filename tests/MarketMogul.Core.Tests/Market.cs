using MarketMogul.Core.Universalis;

namespace MarketMogul.Core.Tests;

/// <summary>Petits constructeurs de données de marché pour les tests.</summary>
internal static class Market
{
    public static readonly DateTimeOffset Now = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);

    public const uint Home = 1;
    public const uint Other = 2;

    public static Sale Sold(long price, double daysAgo, int quantity = 1, bool hq = false) => new()
    {
        PricePerUnit = price,
        Quantity = quantity,
        Hq = hq,
        Timestamp = Now.AddDays(-daysAgo).ToUnixTimeSeconds(),
    };

    public static Listing Listed(long price, int quantity = 1, uint world = Other, bool hq = false) => new()
    {
        PricePerUnit = price,
        Quantity = quantity,
        Hq = hq,
        WorldId = world,
        WorldName = $"World{world}",
    };

    public static ItemMarketData Data(IEnumerable<Sale>? sales = null, IEnumerable<Listing>? listings = null, double uploadedHoursAgo = 0) => new()
    {
        ItemId = 42,
        LastUploadTime = Now.AddHours(-uploadedHoursAgo).ToUnixTimeMilliseconds(),
        RecentHistory = sales?.ToList() ?? [],
        Listings = listings?.ToList() ?? [],
    };
}
