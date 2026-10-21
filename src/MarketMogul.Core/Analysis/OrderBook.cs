using MarketMogul.Core.Universalis;

namespace MarketMogul.Core.Analysis;

public static class OrderBook
{
    /// <summary>
    /// Parcourt les annonces de la moins chère à la plus chère et garde celles qui restent rentables.
    /// Une annonce s'achète en entier : on saute celles qui dépasseraient la quantité voulue.
    /// </summary>
    /// <returns>Les indices des annonces à acheter dans <paramref name="listings"/>.</returns>
    public static List<int> SelectListings(IReadOnlyList<(long PricePerUnit, int Quantity)> listings, long maxPricePerUnit, int maxUnits)
    {
        var selected = new List<int>();
        var remaining = maxUnits;

        foreach (var i in Enumerable.Range(0, listings.Count).OrderBy(i => listings[i].PricePerUnit))
        {
            var (price, quantity) = listings[i];
            if (remaining <= 0 || price > maxPricePerUnit)
                break;
            if (quantity > remaining)
                continue;

            selected.Add(i);
            remaining -= quantity;
        }

        return selected;
    }

    public static List<Purchase> PlanPurchases(IEnumerable<Listing> listings, long maxPricePerUnit, int maxUnits)
    {
        var all = listings.ToList();
        return SelectListings(all.Select(l => (l.PricePerUnit, l.Quantity)).ToList(), maxPricePerUnit, maxUnits)
            .Select(i => all[i])
            .Select(l => new Purchase(l.WorldId ?? 0, l.WorldName ?? "?", l.PricePerUnit, l.Quantity))
            .ToList();
    }
}
