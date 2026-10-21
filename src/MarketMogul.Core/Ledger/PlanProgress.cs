using MarketMogul.Core.Analysis;

namespace MarketMogul.Core.Ledger;

/// <summary>Une annonce prévue par le plan.</summary>
public sealed record PlannedPurchase(uint ItemId, bool Hq, Purchase Purchase);

public static class PlanProgress
{
    /// <summary>
    /// Indique quelles annonces du plan ont été achetées depuis <paramref name="since"/>.
    /// Un achat ne coche qu'une seule annonce : même objet, qualité, monde, prix et quantité.
    /// </summary>
    public static bool[] Bought(IReadOnlyList<PlannedPurchase> planned, IEnumerable<PurchaseRecord> records, DateTime since)
    {
        var bought = new bool[planned.Count];
        foreach (var record in records.Where(r => r.At >= since))
        {
            for (var i = 0; i < planned.Count; i++)
            {
                var p = planned[i];
                if (!bought[i] && p.ItemId == record.ItemId && p.Hq == record.Hq && p.Purchase.WorldId == record.WorldId
                    && p.Purchase.PricePerUnit == record.PricePerUnit && p.Purchase.Quantity == record.Quantity)
                {
                    bought[i] = true;
                    break;
                }
            }
        }

        return bought;
    }
}
