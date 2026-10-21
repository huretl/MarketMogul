using MarketMogul.Core.Universalis;

namespace MarketMogul.Core.Analysis;

/// <summary>État du marché sur le monde d'origine, pour une qualité (NQ ou HQ) d'un objet.</summary>
public sealed record HomeMarketStats(
    long? MedianSalePrice,
    long? LowerQuartile,
    long? UpperQuartile,
    double SalesPerDay,
    int SaleSamples,
    long? LowestListing,
    int ListedQuantity)
{
    /// <summary>
    /// Prix auquel on peut raisonnablement revendre : la médiane des ventes récentes,
    /// ou juste sous l'annonce la plus basse si la concurrence affiche moins cher.
    /// </summary>
    public long? TargetSellPrice => MedianSalePrice switch
    {
        null => null,
        var median when LowestListing is { } lowest && lowest <= median => Math.Max(1, lowest - 1),
        var median => median,
    };

    /// <param name="entriesRequested">Nombre de ventes demandées à l'API : si on l'atteint, l'historique est tronqué.</param>
    public static HomeMarketStats From(ItemMarketData home, bool hq, DateTimeOffset now, int windowDays, int entriesRequested)
    {
        var windowStart = now.AddDays(-windowDays).ToUnixTimeSeconds();
        var sales = home.RecentHistory.Where(s => s.Hq == hq && s.Timestamp >= windowStart).ToList();
        var prices = sales.Select(s => s.PricePerUnit).Order().ToList();

        // Historique tronqué : la fenêtre réellement couverte s'arrête à la plus ancienne vente reçue.
        var coveredDays = (double)windowDays;
        if (home.RecentHistory.Count >= entriesRequested && home.RecentHistory.Count > 0)
        {
            var oldest = DateTimeOffset.FromUnixTimeSeconds(home.RecentHistory.Min(s => s.Timestamp));
            coveredDays = Math.Clamp((now - oldest).TotalDays, 1, windowDays);
        }

        var listings = home.Listings.Where(l => l.Hq == hq).ToList();

        return new HomeMarketStats(
            MedianSalePrice: Percentile(prices, 0.5),
            LowerQuartile: Percentile(prices, 0.25),
            UpperQuartile: Percentile(prices, 0.75),
            SalesPerDay: sales.Sum(s => s.Quantity) / coveredDays,
            SaleSamples: sales.Count,
            LowestListing: listings.Count == 0 ? null : listings.Min(l => l.PricePerUnit),
            ListedQuantity: listings.Sum(l => l.Quantity));
    }

    private static long? Percentile(IReadOnlyList<long> sorted, double p)
    {
        if (sorted.Count == 0)
            return null;
        return sorted[(int)Math.Round(p * (sorted.Count - 1))];
    }
}
