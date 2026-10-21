namespace MarketMogul.Core.Analysis;

/// <summary>Limites d'achat d'un objet pour qu'il reste rentable une fois revendu sur le monde d'origine.</summary>
public sealed record BuyLimits(long TargetSellPrice, long MaxPricePerUnit, int MaxUnits);

public static class BuyAdvisor
{
    /// <summary>Renvoie null si l'objet ne se revend pas assez sur le monde d'origine pour le style choisi.</summary>
    public static BuyLimits? Limits(HomeMarketStats stats, TradingSettings settings)
    {
        if (stats.TargetSellPrice is not { } sellPrice || stats.SalesPerDay < settings.MinSalesPerDay)
            return null;

        var netSellPrice = sellPrice * (1 - settings.SellFeeRate);
        var maxPrice = (long)Math.Floor((netSellPrice - settings.MinProfitPerUnit) / (1 + settings.BuyTaxRate));
        var maxUnits = Math.Max(1, (int)Math.Floor(stats.SalesPerDay * settings.HorizonDays));
        return new BuyLimits(sellPrice, maxPrice, maxUnits);
    }

    /// <summary>Profit net attendu pour une unité achetée à <paramref name="pricePerUnit"/>.</summary>
    public static long UnitProfit(long pricePerUnit, long sellPrice, TradingSettings settings) =>
        (long)Math.Floor(sellPrice * (1 - settings.SellFeeRate) - pricePerUnit * (1 + settings.BuyTaxRate));
}
