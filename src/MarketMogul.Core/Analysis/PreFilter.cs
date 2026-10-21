using MarketMogul.Core.Universalis;

namespace MarketMogul.Core.Analysis;

/// <summary>
/// Tri à partir de l'historique des ventes du monde d'origine, avant de récupérer les annonces.
/// Les annonces du monde d'origine ne peuvent que baisser le prix de revente : un objet écarté ici ne serait pas rentable.
/// </summary>
public static class PreFilter
{
    /// <summary>L'objet se revend assez sur le monde d'origine (en NQ ou en HQ) pour mériter l'analyse des annonces.</summary>
    public static bool IsCandidate(ItemMarketData homeHistory, TradingSettings settings, DateTimeOffset now, int entriesRequested) =>
        new[] { false, true }.Any(hq =>
            BuyAdvisor.Limits(HomeMarketStats.From(homeHistory, hq, now, settings.SalesWindowDays, entriesRequested), settings) is not null);

    /// <summary>Aucune vente sur la période : inutile de revérifier l'objet avant un moment.</summary>
    public static bool IsDormant(ItemMarketData homeHistory) => homeHistory.RecentHistory.Count == 0;
}
