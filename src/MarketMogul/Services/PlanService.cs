using System;
using System.Collections.Generic;
using MarketMogul.Core.Analysis;

namespace MarketMogul.Services;

/// <summary>
/// Plan d'achat : les opportunités du dernier scan, réparties selon le budget et les emplacements de vente.
/// Figé jusqu'au prochain scan ou recalcul, pour ne pas changer pendant qu'on achète.
/// </summary>
public sealed class PlanService(ScanService scanner, Configuration configuration, ItemCatalog catalog)
{
    private IReadOnlyList<Opportunity>? basedOn;

    public Portfolio? Current { get; private set; }

    /// <summary>Vrai si les emplacements viennent des servants, faux s'il s'agit de la valeur des réglages.</summary>
    public bool SlotsFromRetainers { get; private set; }

    /// <summary>En UTC : les achats faits depuis sont cochés dans le plan.</summary>
    public DateTime ComputedAt { get; private set; }

    /// <summary>Recalcule le plan si un nouveau scan est arrivé. À appeler depuis le thread du jeu.</summary>
    public void RefreshIfStale()
    {
        if (!ReferenceEquals(basedOn, scanner.Opportunities))
            Recompute();
    }

    public void Recompute()
    {
        var budget = Math.Max(0, GameResources.Gil() - configuration.BudgetReserve);
        var retainerSlots = GameResources.FreeListingSlots();

        basedOn = scanner.Opportunities;
        SlotsFromRetainers = retainerSlots is not null;
        Current = PortfolioOptimizer.Optimize(
            basedOn, budget, retainerSlots ?? configuration.FallbackListingSlots, catalog.StackSize, configuration.Trading);
        ComputedAt = DateTime.UtcNow;
    }
}
