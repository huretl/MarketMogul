using System.Collections.Concurrent;
using MarketMogul.Core.Universalis;

namespace MarketMogul.Core.Analysis;

/// <param name="Checked">Objets dont l'historique a été vérifié.</param>
/// <param name="SkippedDormant">Objets ignorés car sans vente récente sur le monde d'origine (cache).</param>
/// <param name="Analyzed">Objets qui se revendent assez, dont les annonces ont été analysées.</param>
/// <param name="Failed">Objets non vérifiés : Universalis n'a pas répondu malgré plusieurs tentatives.</param>
public sealed record ScanStats(int Checked, int SkippedDormant, int Analyzed, int Failed = 0);

public sealed record ScanResult(List<Opportunity> Opportunities, ScanStats Stats);

public sealed class ArbitrageScanner(UniversalisClient client)
{
    public const int HomeListings = 20;
    public const int HomeHistoryEntries = 60;
    private const int DataCenterListings = 50;
    private const int ScanHistoryEntries = 100;

    // Part de la barre de progression occupée par la passe sur l'historique (le plus gros du nombre de requêtes).
    private const double PreFilterProgressShare = 0.7;

    /// <summary>
    /// Scanne les objets donnés et renvoie les opportunités d'arbitrage vers le monde d'origine,
    /// triées de la meilleure à la moins bonne.
    /// Deux passes : l'historique des ventes du monde d'origine pour tous les objets (rapide),
    /// puis les annonces des seuls objets qui se revendent assez.
    /// </summary>
    /// <param name="vendorPrices">Prix des objets vendus par des marchands PNJ, évalués comme une source d'achat de plus.</param>
    /// <param name="dormant">Cache des objets sans vente, mis à jour par le scan. Null pour tout vérifier.</param>
    /// <param name="progress">Avancement entre 0 et 1.</param>
    public async Task<ScanResult> ScanAsync(
        uint homeWorldId,
        string dataCenterName,
        IReadOnlyList<uint> itemIds,
        TradingSettings settings,
        IReadOnlyDictionary<uint, long>? vendorPrices = null,
        DormantItemCache? dormant = null,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var toCheck = dormant?.ItemsToCheck(homeWorldId, itemIds, now) ?? itemIds.ToList();

        var window = TimeSpan.FromDays(settings.SalesWindowDays);

        // Passe 1 : historique des ventes du monde d'origine.
        var candidates = new ConcurrentDictionary<uint, HistoryItem>();
        var historyChunks = toCheck.Chunk(UniversalisClient.MaxItemsPerRequest).ToList();
        var done = 0;
        var failed = 0;
        await Parallel.ForEachAsync(historyChunks, ct, async (chunk, token) =>
        {
            var history = await TryFetchAsync(() => client.GetHistoryAsync(homeWorldId.ToString(), chunk, ScanHistoryEntries, window, token));
            if (history is null)
            {
                Interlocked.Add(ref failed, chunk.Length);
                progress?.Report(PreFilterProgressShare * Interlocked.Increment(ref done) / historyChunks.Count);
                return;
            }

            var filteredAt = DateTimeOffset.UtcNow;
            lock (candidates)
            {
                foreach (var itemId in chunk)
                {
                    var sales = AsMarketData(history.GetValueOrDefault(itemId));
                    dormant?.Update(itemId, PreFilter.IsDormant(sales), now);
                    if (PreFilter.IsCandidate(sales, settings, filteredAt, ScanHistoryEntries))
                        candidates[itemId] = history[itemId];
                }
            }

            progress?.Report(PreFilterProgressShare * Interlocked.Increment(ref done) / historyChunks.Count);
        });

        // Passe 2 : annonces du monde d'origine et du data center.
        var opportunities = new ConcurrentBag<Opportunity>();
        var detailChunks = candidates.Keys.Order().Chunk(UniversalisClient.MaxItemsPerRequest).ToList();
        done = 0;
        await Parallel.ForEachAsync(detailChunks, ct, async (chunk, token) =>
        {
            var home = await TryFetchAsync(() => client.GetMarketDataAsync(homeWorldId.ToString(), chunk, HomeListings, 0, token));
            var dataCenter = await TryFetchAsync(() => client.GetMarketDataAsync(dataCenterName, chunk, DataCenterListings, 0, token));
            if (home is null || dataCenter is null)
            {
                Interlocked.Add(ref failed, chunk.Length);
                progress?.Report(PreFilterProgressShare + (1 - PreFilterProgressShare) * Interlocked.Increment(ref done) / detailChunks.Count);
                return;
            }

            var evaluatedAt = DateTimeOffset.UtcNow;

            foreach (var itemId in chunk)
            {
                if (!home.TryGetValue(itemId, out var homeData) || !dataCenter.TryGetValue(itemId, out var dcData))
                    continue;
                homeData.RecentHistory = candidates[itemId].Entries;

                foreach (var hq in (bool[])[false, true])
                {
                    var opportunity = OpportunityScorer.Evaluate(itemId, hq, homeData, dcData, homeWorldId, settings, evaluatedAt, ScanHistoryEntries);

                    // Les deux sources visent les mêmes acheteurs : on ne garde que la meilleure.
                    if (!hq && vendorPrices?.GetValueOrDefault(itemId) is > 0 and var vendorPrice
                        && OpportunityScorer.EvaluateVendor(itemId, vendorPrice, homeData, settings, evaluatedAt, ScanHistoryEntries) is { } fromVendor
                        && (opportunity is null || fromVendor.Score > opportunity.Score))
                        opportunity = fromVendor;

                    if (opportunity is not null)
                        opportunities.Add(opportunity);
                }
            }

            progress?.Report(PreFilterProgressShare + (1 - PreFilterProgressShare) * Interlocked.Increment(ref done) / detailChunks.Count);
        });

        progress?.Report(1);
        var stats = new ScanStats(toCheck.Count, itemIds.Count - toCheck.Count, candidates.Count, failed);
        return new ScanResult(opportunities.OrderByDescending(o => o.Score).ToList(), stats);
    }

    /// <summary>Un paquet en échec malgré les nouvelles tentatives est sauté plutôt que de faire échouer tout le scan.</summary>
    private static async Task<T?> TryFetchAsync<T>(Func<Task<T>> fetch) where T : class
    {
        try
        {
            return await fetch();
        }
        catch (Exception e) when (e is HttpRequestException or System.Text.Json.JsonException or TaskCanceledException { CancellationToken.IsCancellationRequested: false })
        {
            return null;
        }
    }

    /// <summary>Un objet absent de la réponse n'a aucune vente sur la période.</summary>
    private static ItemMarketData AsMarketData(HistoryItem? history) => new()
    {
        ItemId = history?.ItemId ?? 0,
        LastUploadTime = history?.LastUploadTime ?? 0,
        RecentHistory = history?.Entries ?? [],
    };
}
