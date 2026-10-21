using MarketMogul.Core.Universalis;

namespace MarketMogul.Core.Analysis;

public static class OpportunityScorer
{
    /// <summary>
    /// Évalue l'achat d'un objet sur les autres mondes du data center pour le revendre sur le monde d'origine.
    /// Renvoie null si l'opération ne respecte pas les critères de <paramref name="settings"/>.
    /// </summary>
    public static Opportunity? Evaluate(
        uint itemId,
        bool hq,
        ItemMarketData home,
        ItemMarketData dataCenter,
        uint homeWorldId,
        TradingSettings settings,
        DateTimeOffset now,
        int historyEntriesRequested)
    {
        var stats = HomeMarketStats.From(home, hq, now, settings.SalesWindowDays, historyEntriesRequested);
        if (stats.TargetSellPrice is not { } sellPrice || stats.SalesPerDay < settings.MinSalesPerDay)
            return null;

        var netSellPrice = sellPrice * (1 - settings.SellFeeRate);
        var maxBuyPrice = (long)Math.Floor((netSellPrice - settings.MinProfitPerUnit) / (1 + settings.BuyTaxRate));
        var maxUnits = Math.Max(1, (int)Math.Floor(stats.SalesPerDay * settings.HorizonDays));

        var candidates = dataCenter.Listings.Where(l => l.Hq == hq && l.WorldId != homeWorldId);
        var purchases = OrderBook.PlanPurchases(candidates, maxBuyPrice, maxUnits);
        if (purchases.Count == 0)
            return null;

        var units = purchases.Sum(p => p.Quantity);
        var opportunity = new Opportunity(
            ItemId: itemId,
            Hq: hq,
            TargetSellPrice: sellPrice,
            Purchases: purchases,
            TotalCost: (long)Math.Ceiling(purchases.Sum(p => p.Subtotal) * (1 + settings.BuyTaxRate)),
            ExpectedRevenue: (long)Math.Floor(netSellPrice * units),
            SalesPerDay: stats.SalesPerDay,
            HomeListedQuantity: stats.ListedQuantity,
            Confidence: Confidence(stats, home, dataCenter, purchases, now));

        if (opportunity.ExpectedProfit < settings.MinTotalProfit || opportunity.Confidence < settings.MinConfidence)
            return null;

        return opportunity;
    }

    /// <summary>
    /// Fiabilité entre 0 et 1, produit de trois facteurs :
    /// fraîcheur des données, nombre de ventes observées et stabilité du prix de revente.
    /// </summary>
    public static double Confidence(
        HomeMarketStats stats,
        ItemMarketData home,
        ItemMarketData dataCenter,
        IEnumerable<Purchase> purchases,
        DateTimeOffset now)
    {
        // Une annonce vue il y a longtemps a de bonnes chances d'avoir déjà été achetée.
        var uploadTimes = purchases
            .Select(p => dataCenter.WorldUploadTimes?.GetValueOrDefault(p.WorldId.ToString()) ?? dataCenter.LastUploadTime)
            .Append(home.LastUploadTime);
        var oldestAgeHours = (now - DateTimeOffset.FromUnixTimeMilliseconds(uploadTimes.Min())).TotalHours;
        var freshness = Math.Exp(-Math.Max(0, oldestAgeHours) / 24);

        var samples = stats.SaleSamples / (stats.SaleSamples + 3.0);

        var stability = 1.0;
        if (stats is { MedianSalePrice: > 0 and var median, LowerQuartile: { } q1, UpperQuartile: { } q3 })
            stability = 1 / (1 + (double)(q3 - q1) / median);

        return Math.Clamp(freshness * samples * stability, 0, 1);
    }
}
