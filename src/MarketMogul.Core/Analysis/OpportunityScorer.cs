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
        if (BuyAdvisor.Limits(stats, settings) is not { } limits)
            return null;

        var candidates = dataCenter.Listings.Where(l => l.Hq == hq && l.WorldId != homeWorldId);
        var purchases = OrderBook.PlanPurchases(candidates, limits.MaxPricePerUnit, limits.MaxUnits);
        if (purchases.Count == 0)
            return null;

        var opportunity = Opportunity.Create(
            itemId,
            hq,
            limits.TargetSellPrice,
            purchases,
            stats.SalesPerDay,
            stats.ListedQuantity,
            Confidence(stats, home, dataCenter, purchases, now),
            settings);

        if (opportunity.ExpectedProfit < settings.MinTotalProfit || opportunity.Confidence < settings.MinConfidence)
            return null;

        return opportunity;
    }

    /// <summary>
    /// Évalue l'achat d'un objet chez un marchand PNJ pour le revendre sur le monde d'origine.
    /// Le stock est illimité : on achète ce que le marché absorbe dans l'horizon.
    /// </summary>
    public static Opportunity? EvaluateVendor(
        uint itemId,
        long vendorPrice,
        ItemMarketData home,
        TradingSettings settings,
        DateTimeOffset now,
        int historyEntriesRequested)
    {
        // Les marchands PNJ ne vendent que des objets NQ.
        var stats = HomeMarketStats.From(home, hq: false, now, settings.SalesWindowDays, historyEntriesRequested);
        if (BuyAdvisor.Limits(stats, settings) is not { } limits)
            return null;
        if (BuyAdvisor.UnitProfit(vendorPrice, limits.TargetSellPrice, settings with { BuyTaxRate = 0 }) < settings.MinProfitPerUnit)
            return null;

        var opportunity = Opportunity.Create(
            itemId,
            false,
            limits.TargetSellPrice,
            [Purchase.FromVendor(vendorPrice, limits.MaxUnits)],
            stats.SalesPerDay,
            stats.ListedQuantity,
            // Seul le prix de revente est incertain : le prix du PNJ est fixe.
            Confidence(stats, home.LastUploadTime, now),
            settings);

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
        var oldestUpload = purchases
            .Select(p => dataCenter.WorldUploadTimes?.GetValueOrDefault(p.WorldId.ToString()) ?? dataCenter.LastUploadTime)
            .Append(home.LastUploadTime)
            .Min();
        return Confidence(stats, oldestUpload, now);
    }

    /// <param name="oldestUpload">Envoi Universalis le plus ancien parmi les données utilisées, en millisecondes Unix.</param>
    public static double Confidence(HomeMarketStats stats, long oldestUpload, DateTimeOffset now)
    {
        var oldestAgeHours = (now - DateTimeOffset.FromUnixTimeMilliseconds(oldestUpload)).TotalHours;
        var freshness = Math.Exp(-Math.Max(0, oldestAgeHours) / 24);

        var samples = stats.SaleSamples / (stats.SaleSamples + 3.0);

        var stability = 1.0;
        if (stats is { MedianSalePrice: > 0 and var median, LowerQuartile: { } q1, UpperQuartile: { } q3 })
            stability = 1 / (1 + (double)(q3 - q1) / median);

        return Math.Clamp(freshness * samples * stability, 0, 1);
    }
}
