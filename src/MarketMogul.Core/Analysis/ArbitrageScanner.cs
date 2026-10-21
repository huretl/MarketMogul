using MarketMogul.Core.Universalis;

namespace MarketMogul.Core.Analysis;

public sealed class ArbitrageScanner(UniversalisClient client)
{
    private const int HomeListings = 20;
    private const int HomeHistoryEntries = 60;
    private const int DataCenterListings = 50;

    /// <summary>
    /// Scanne les objets donnés et renvoie les opportunités d'arbitrage vers le monde d'origine,
    /// triées de la meilleure à la moins bonne.
    /// </summary>
    /// <param name="progress">Avancement entre 0 et 1.</param>
    public async Task<List<Opportunity>> ScanAsync(
        uint homeWorldId,
        string dataCenterName,
        IReadOnlyList<uint> itemIds,
        TradingSettings settings,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        var opportunities = new List<Opportunity>();
        var chunks = itemIds.Chunk(UniversalisClient.MaxItemsPerRequest).ToList();

        for (var i = 0; i < chunks.Count; i++)
        {
            var chunk = chunks[i];
            var home = await client.GetMarketDataAsync(homeWorldId.ToString(), chunk, HomeListings, HomeHistoryEntries, ct);
            var dataCenter = await client.GetMarketDataAsync(dataCenterName, chunk, DataCenterListings, 0, ct);
            var now = DateTimeOffset.UtcNow;

            foreach (var itemId in chunk)
            {
                if (!home.TryGetValue(itemId, out var homeData) || !dataCenter.TryGetValue(itemId, out var dcData))
                    continue;

                foreach (var hq in (bool[])[false, true])
                {
                    var opportunity = OpportunityScorer.Evaluate(itemId, hq, homeData, dcData, homeWorldId, settings, now, HomeHistoryEntries);
                    if (opportunity is not null)
                        opportunities.Add(opportunity);
                }
            }

            progress?.Report((i + 1) / (double)chunks.Count);
        }

        return opportunities.OrderByDescending(o => o.Score).ToList();
    }
}
