using MarketMogul.Core.Universalis;

namespace MarketMogul.Core.Analysis;

public static class OrderBook
{
    /// <summary>
    /// Parcourt les annonces de la moins chère à la plus chère et garde celles qui restent rentables.
    /// Une annonce s'achète en entier : on saute celles qui dépasseraient la quantité voulue.
    /// </summary>
    public static List<Purchase> PlanPurchases(IEnumerable<Listing> listings, long maxPricePerUnit, int maxUnits)
    {
        var purchases = new List<Purchase>();
        var remaining = maxUnits;

        foreach (var listing in listings.OrderBy(l => l.PricePerUnit))
        {
            if (remaining <= 0 || listing.PricePerUnit > maxPricePerUnit)
                break;
            if (listing.Quantity > remaining)
                continue;

            purchases.Add(new Purchase(listing.WorldId ?? 0, listing.WorldName ?? "?", listing.PricePerUnit, listing.Quantity));
            remaining -= listing.Quantity;
        }

        return purchases;
    }
}
