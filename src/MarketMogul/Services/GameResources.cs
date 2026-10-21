using System;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace MarketMogul.Services;

/// <summary>Ressources du personnage lues dans la mémoire du jeu. À appeler depuis le thread du jeu.</summary>
public static unsafe class GameResources
{
    private const int ListingsPerRetainer = 20;

    public static long Gil()
    {
        var inventory = InventoryManager.Instance();
        return inventory == null ? 0 : inventory->GetGil();
    }

    /// <summary>
    /// Emplacements de vente libres sur l'ensemble des servants,
    /// ou null tant que le jeu ne les a pas chargés (passage à la sonnette nécessaire).
    /// </summary>
    public static int? FreeListingSlots()
    {
        var retainers = RetainerManager.Instance();
        if (retainers == null || !retainers->IsReady)
            return null;

        var free = 0;
        var count = retainers->GetRetainerCount();
        for (var i = 0u; i < count; i++)
        {
            var retainer = retainers->GetRetainerBySortedIndex(i);
            if (retainer == null || retainer->RetainerId == 0)
                continue;
            free += Math.Max(0, ListingsPerRetainer - retainer->MarketItemCount);
        }

        return free;
    }
}
