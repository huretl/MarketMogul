using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Network.Structures;
using Dalamud.Plugin.Services;

namespace MarketMogul.Services;

public sealed record LiveListing(ulong ListingId, long PricePerUnit, int Quantity, bool Hq, string RetainerName);

public sealed record LiveOfferings(uint ItemId, DateTime ReceivedAt, IReadOnlyList<LiveListing> Listings);

/// <summary>
/// Annonces reçues par le jeu à l'ouverture d'un objet au marketboard : plus fraîches que n'importe quel scan.
/// Le serveur les envoie par paquets, qu'on regroupe par requête.
/// </summary>
public sealed class MarketBoardWatcher : IDisposable
{
    private readonly IMarketBoard marketBoard;
    private uint currentRequestId;

    /// <summary>Remplacé en bloc à chaque paquet : lisible sans verrou depuis le thread de dessin.</summary>
    public LiveOfferings? Current { get; private set; }

    public MarketBoardWatcher(IMarketBoard marketBoard)
    {
        this.marketBoard = marketBoard;
        marketBoard.OfferingsReceived += OnOfferingsReceived;
    }

    private void OnOfferingsReceived(IMarketBoardCurrentOfferings offerings)
    {
        var received = offerings.ItemListings
            .Select(l => new LiveListing(l.ListingId, l.PricePerUnit, (int)l.ItemQuantity, l.IsHq, l.RetainerName))
            .ToList();
        if (received.Count == 0)
            return;

        var itemId = offerings.ItemListings[0].ItemId;
        var requestId = (uint)offerings.RequestId;
        var previous = Current;

        if (previous is null || previous.ItemId != itemId || requestId != currentRequestId)
        {
            currentRequestId = requestId;
            Current = new LiveOfferings(itemId, DateTime.UtcNow, received);
            return;
        }

        Current = previous with { Listings = [.. previous.Listings, .. received] };
    }

    public void Dispose()
    {
        marketBoard.OfferingsReceived -= OnOfferingsReceived;
    }
}
