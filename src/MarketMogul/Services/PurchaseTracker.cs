using System;
using Dalamud.Game.Network.Structures;
using Dalamud.Plugin.Services;
using MarketMogul.Core.Ledger;

namespace MarketMogul.Services;

/// <summary>
/// Enregistre les achats faits au marketboard. Le jeu envoie d'abord la demande d'achat (prix, quantité),
/// puis la confirmation : on n'enregistre qu'un achat confirmé.
/// </summary>
public sealed class PurchaseTracker : IDisposable
{
    // Les objets HQ sont parfois identifiés avec ce décalage.
    private const uint HqOffset = 1_000_000;
    private static readonly TimeSpan ConfirmationDelay = TimeSpan.FromSeconds(30);

    private sealed record Pending(DateTime At, uint ItemId, bool Hq, int Quantity, long PricePerUnit);

    private readonly IMarketBoard marketBoard;
    private readonly WorldTracker worlds;
    private readonly PurchaseLedger ledger;
    private readonly Func<uint, bool, long?> expectedSellPrice;
    private Pending? pending;

    public PurchaseTracker(IMarketBoard marketBoard, WorldTracker worlds, PurchaseLedger ledger, Func<uint, bool, long?> expectedSellPrice)
    {
        this.marketBoard = marketBoard;
        this.worlds = worlds;
        this.ledger = ledger;
        this.expectedSellPrice = expectedSellPrice;
        marketBoard.PurchaseRequested += OnPurchaseRequested;
        marketBoard.ItemPurchased += OnItemPurchased;
    }

    private void OnPurchaseRequested(IMarketBoardPurchaseHandler request)
    {
        pending = new Pending(DateTime.UtcNow, request.CatalogId % HqOffset, request.IsHq, (int)request.ItemQuantity, request.PricePerUnit);
    }

    private void OnItemPurchased(IMarketBoardPurchase purchase)
    {
        var request = pending;
        if (request is null || worlds.Current is not { } world)
            return;
        if (request.ItemId != purchase.CatalogId % HqOffset || request.Quantity != (int)purchase.ItemQuantity
            || DateTime.UtcNow - request.At > ConfirmationDelay)
            return;

        pending = null;
        ledger.Add(new PurchaseRecord(
            DateTime.UtcNow, request.ItemId, request.Hq, world.Id, world.Name,
            request.PricePerUnit, request.Quantity, expectedSellPrice(request.ItemId, request.Hq)));
    }

    public void Dispose()
    {
        marketBoard.PurchaseRequested -= OnPurchaseRequested;
        marketBoard.ItemPurchased -= OnItemPurchased;
    }
}
