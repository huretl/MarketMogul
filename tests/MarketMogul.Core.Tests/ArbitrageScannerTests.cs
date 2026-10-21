using System.Net;
using System.Text.Json;
using MarketMogul.Core.Analysis;
using MarketMogul.Core.Universalis;
using static MarketMogul.Core.Tests.Market;

namespace MarketMogul.Core.Tests;

public class ArbitrageScannerTests
{
    /// <summary>Faux Universalis : répond selon le chemin demandé et compte les requêtes.</summary>
    private sealed class FakeUniversalis : HttpMessageHandler
    {
        public List<string> Requests { get; } = new();
        public required Func<string, object?> Respond { get; init; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            lock (Requests)
                Requests.Add(path);
            // Null simule une erreur serveur.
            if (Respond(path) is not { } body)
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            var json = JsonSerializer.Serialize(body);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
        }
    }

    private static readonly TradingSettings Settings = new() { Style = TradingStyle.Balanced, MinProfitPerUnit = 100, MinTotalProfit = 0, MinConfidence = 0 };

    [Fact]
    public async Task Fetches_listings_only_for_items_that_sell_and_caches_dormant_items()
    {
        // Objet 1 : se vend et rentable. Objet 2 : se vend mais aucune annonce rentable. Objet 3 : aucune vente.
        var steadySales = Enumerable.Range(1, 10).Select(d => Sold(10_000, d)).ToList();
        var history = new HistoryResponse
        {
            Items = new()
            {
                ["1"] = new HistoryItem { ItemId = 1, Entries = steadySales },
                ["2"] = new HistoryItem { ItemId = 2, Entries = steadySales },
            },
        };
        MultiItemResponse Listings(bool dataCenter) => new()
        {
            Items = new()
            {
                ["1"] = WithId(Data(listings: dataCenter ? [Listed(5_000, 2)] : []), 1),
                ["2"] = WithId(Data(listings: dataCenter ? [Listed(20_000, 2)] : []), 2),
            },
        };

        var fake = new FakeUniversalis
        {
            Respond = path => path.Contains("/history/") ? history : Listings(dataCenter: path.Contains("/Chaos/")),
        };
        using var client = new UniversalisClient("test", requestsPerSecond: 1000,
            httpClient: new HttpClient(fake) { BaseAddress = new Uri("https://universalis.test/api/v2/") });
        var cache = new DormantItemCache();

        var result = await new ArbitrageScanner(client).ScanAsync(Home, "Chaos", [1, 2, 3], Settings, dormant: cache);

        Assert.Equal(new ScanStats(Checked: 3, SkippedDormant: 0, Analyzed: 2), result.Stats);
        Assert.Equal(1u, Assert.Single(result.Opportunities).ItemId);
        Assert.All(fake.Requests.Where(r => !r.Contains("/history/")), r => Assert.EndsWith("/1,2", r));

        // Second scan : l'objet sans vente n'est plus vérifié.
        var second = await new ArbitrageScanner(client).ScanAsync(Home, "Chaos", [1, 2, 3], Settings, dormant: cache);
        Assert.Equal(1, second.Stats.SkippedDormant);
    }

    private static ItemMarketData WithId(ItemMarketData data, uint id)
    {
        data.ItemId = id;
        return data;
    }

    [Fact]
    public async Task Retries_transient_errors_then_skips_failing_chunk()
    {
        var calls = 0;
        var fake = new FakeUniversalis
        {
            // Une erreur passagère, puis le serveur reste en panne.
            Respond = path => Interlocked.Increment(ref calls) == 1 ? null : path.Contains("/history/") ? null : new MultiItemResponse(),
        };
        using var client = new UniversalisClient("test", requestsPerSecond: 1000,
            httpClient: new HttpClient(fake) { BaseAddress = new Uri("https://universalis.test/api/v2/") });

        var result = await new ArbitrageScanner(client).ScanAsync(Home, "Chaos", [1, 2], Settings);

        Assert.Equal(3, fake.Requests.Count); // 3 tentatives
        Assert.Equal(2, result.Stats.Failed);
        Assert.Empty(result.Opportunities);
    }
}
