namespace MarketMogul.Core.Analysis;

public sealed record Purchase(uint WorldId, string WorldName, long PricePerUnit, int Quantity)
{
    public long Subtotal => PricePerUnit * Quantity;
}

public sealed record Opportunity(
    uint ItemId,
    bool Hq,
    long TargetSellPrice,
    IReadOnlyList<Purchase> Purchases,
    // Taxe d'achat comprise.
    long TotalCost,
    // Frais de vente déduits.
    long ExpectedRevenue,
    double SalesPerDay,
    int HomeListedQuantity,
    double Confidence)
{
    public int Units => Purchases.Sum(p => p.Quantity);

    public long ExpectedProfit => ExpectedRevenue - TotalCost;

    public double Roi => TotalCost == 0 ? 0 : (double)ExpectedProfit / TotalCost;

    /// <summary>En supposant qu'on se place en tête des annonces (sous-cotation).</summary>
    public double DaysToSell => Units / SalesPerDay;

    /// <summary>Profit ramené à la journée : un petit profit rapide peut battre un gros profit lent.</summary>
    public double ProfitPerDay => ExpectedProfit / Math.Max(1, DaysToSell);

    /// <summary>Critère de tri : profit par jour pondéré par la fiabilité des données.</summary>
    public double Score => ProfitPerDay * Confidence;

    public static Opportunity Create(
        uint itemId,
        bool hq,
        long targetSellPrice,
        IReadOnlyList<Purchase> purchases,
        double salesPerDay,
        int homeListedQuantity,
        double confidence,
        TradingSettings settings) =>
        new(
            ItemId: itemId,
            Hq: hq,
            TargetSellPrice: targetSellPrice,
            Purchases: purchases,
            TotalCost: (long)Math.Ceiling(purchases.Sum(p => p.Subtotal) * (1 + settings.BuyTaxRate)),
            ExpectedRevenue: (long)Math.Floor(targetSellPrice * (1 - settings.SellFeeRate) * purchases.Sum(p => p.Quantity)),
            SalesPerDay: salesPerDay,
            HomeListedQuantity: homeListedQuantity,
            Confidence: confidence);

    /// <summary>La même opération, réduite aux achats donnés (par exemple pour tenir dans un budget).</summary>
    public Opportunity WithPurchases(IReadOnlyList<Purchase> purchases, TradingSettings settings) =>
        Create(ItemId, Hq, TargetSellPrice, purchases, SalesPerDay, HomeListedQuantity, Confidence, settings);
}
