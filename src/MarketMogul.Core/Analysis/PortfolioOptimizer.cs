namespace MarketMogul.Core.Analysis;

/// <param name="Slots">Emplacements de vente nécessaires : une annonce de servant contient au plus une pile.</param>
public sealed record Position(Opportunity Opportunity, int Slots);

public sealed record Portfolio(IReadOnlyList<Position> Positions, long Budget, int Slots)
{
    public long TotalCost => Positions.Sum(p => p.Opportunity.TotalCost);

    public long ExpectedProfit => Positions.Sum(p => p.Opportunity.ExpectedProfit);

    public int SlotsUsed => Positions.Sum(p => p.Slots);
}

public static class PortfolioOptimizer
{
    /// <summary>
    /// Choisit les opérations à mener avec un budget et un nombre d'emplacements de vente limités.
    /// Glouton sur le profit pondéré par la fiabilité, rapporté à la part de budget et d'emplacements consommée ;
    /// une opération trop grosse est réduite à ses annonces les moins chères plutôt qu'écartée.
    /// </summary>
    /// <param name="stackSize">Taille de pile maximale d'un objet (1 pour un équipement, 999 pour un matériau…).</param>
    public static Portfolio Optimize(
        IEnumerable<Opportunity> candidates,
        long budget,
        int slots,
        Func<uint, int> stackSize,
        TradingSettings settings)
    {
        var positions = new List<Position>();
        if (budget <= 0 || slots <= 0)
            return new Portfolio(positions, budget, slots);

        var remainingBudget = budget;
        var remainingSlots = slots;

        var ranked = candidates
            .Select(o => (Opportunity: o, Density: o.ExpectedProfit * o.Confidence
                / ((double)o.TotalCost / budget + (double)SlotsFor(o.Units, stackSize(o.ItemId)) / slots)))
            .OrderByDescending(c => c.Density)
            .Select(c => c.Opportunity);

        foreach (var opportunity in ranked)
        {
            if (remainingSlots == 0)
                break;

            var stack = stackSize(opportunity.ItemId);
            var kept = new List<Purchase>();
            foreach (var purchase in opportunity.Purchases.OrderBy(p => p.PricePerUnit))
            {
                var candidate = opportunity.WithPurchases([.. kept, purchase], settings);
                if (candidate.TotalCost > remainingBudget || SlotsFor(candidate.Units, stack) > remainingSlots)
                    break;
                kept.Add(purchase);
            }

            if (kept.Count == 0)
                continue;

            var position = opportunity.WithPurchases(kept, settings);
            if (position.ExpectedProfit <= 0)
                continue;

            var used = SlotsFor(position.Units, stack);
            positions.Add(new Position(position, used));
            remainingBudget -= position.TotalCost;
            remainingSlots -= used;
        }

        return new Portfolio(positions, budget, slots);
    }

    public static int SlotsFor(int units, int stackSize) => (units + Math.Max(1, stackSize) - 1) / Math.Max(1, stackSize);
}
