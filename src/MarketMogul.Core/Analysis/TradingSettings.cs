namespace MarketMogul.Core.Analysis;

public enum TradingStyle
{
    /// <summary>Petits gains réguliers : objets qui se vendent vite, rotation rapide du capital.</summary>
    Steady,
    Balanced,
    /// <summary>Gros coups : grosses marges, quitte à attendre plusieurs semaines la vente.</summary>
    BigHits,
}

public sealed record TradingSettings
{
    public TradingStyle Style { get; set; } = TradingStyle.Balanced;

    /// <summary>Taxe payée par l'acheteur en plus du prix affiché.</summary>
    public double BuyTaxRate { get; set; } = 0.05;

    /// <summary>
    /// Part du prix de vente perdue côté vendeur. Hypothèse prudente par défaut :
    /// mieux vaut sous-estimer un profit que le surestimer.
    /// </summary>
    public double SellFeeRate { get; set; } = 0.05;

    public long MinProfitPerUnit { get; set; } = 200;

    public long MinTotalProfit { get; set; } = 10_000;

    public double MinConfidence { get; set; } = 0.25;

    /// <summary>Fenêtre de ventes passées utilisée pour le prix de revente et la vitesse de vente.</summary>
    public int SalesWindowDays { get; set; } = 14;

    /// <summary>Nombre de jours pendant lesquels on accepte d'attendre que le stock acheté se vende.</summary>
    public double HorizonDays => Style switch
    {
        TradingStyle.Steady => 3,
        TradingStyle.Balanced => 7,
        _ => 21,
    };

    /// <summary>En dessous de cette vitesse (unités vendues / jour sur le monde d'origine), l'objet est ignoré.</summary>
    public double MinSalesPerDay => Style switch
    {
        TradingStyle.Steady => 1,
        TradingStyle.Balanced => 0.3,
        _ => 0.05,
    };
}
