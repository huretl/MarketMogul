using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using MarketMogul.Core.Analysis;
using MarketMogul.Core.Universalis;

namespace MarketMogul.Services;

/// <summary>Données de marché du monde d'origine pour un objet, récupérées à la demande et gardées en cache.</summary>
public sealed class HomePriceService(UniversalisClient client, IPluginLog log)
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

    private sealed record Entry(DateTime FetchedAt, ItemMarketData? Data);

    private readonly ConcurrentDictionary<(uint World, uint Item), Entry> cache = new();
    private readonly ConcurrentDictionary<(uint World, uint Item), bool> loading = new();

    public bool IsLoading(uint homeWorldId, uint itemId) => loading.ContainsKey((homeWorldId, itemId));

    /// <summary>
    /// Renvoie les données en cache (éventuellement périmées) et lance une mise à jour si besoin.
    /// Null tant que le premier chargement n'est pas terminé.
    /// </summary>
    public ItemMarketData? Get(uint homeWorldId, uint itemId)
    {
        var key = (homeWorldId, itemId);
        var cached = cache.GetValueOrDefault(key);
        if ((cached is null || DateTime.UtcNow - cached.FetchedAt > CacheDuration) && loading.TryAdd(key, true))
            _ = FetchAsync(key);
        return cached?.Data;
    }

    private async Task FetchAsync((uint World, uint Item) key)
    {
        try
        {
            var data = await client.GetMarketDataAsync(
                key.World.ToString(), [key.Item], ArbitrageScanner.HomeListings, ArbitrageScanner.HomeHistoryEntries);
            cache[key] = new Entry(DateTime.UtcNow, data.GetValueOrDefault(key.Item));
        }
        catch (Exception e)
        {
            log.Warning(e, $"Impossible de récupérer les prix de l'objet {key.Item}");
            // On retient l'échec pour ne pas relancer la requête à chaque frame.
            cache[key] = new Entry(DateTime.UtcNow, cache.GetValueOrDefault(key)?.Data);
        }
        finally
        {
            loading.TryRemove(key, out _);
        }
    }
}
