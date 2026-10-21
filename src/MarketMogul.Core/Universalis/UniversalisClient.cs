using System.Net.Http.Json;

namespace MarketMogul.Core.Universalis;

public sealed class UniversalisClient : IDisposable
{
    public const int MaxItemsPerRequest = 100;

    private readonly HttpClient http;
    private readonly SemaphoreSlim throttle;
    private readonly SemaphoreSlim concurrency;
    private readonly TimeSpan minDelayBetweenRequests;
    private DateTime lastRequest = DateTime.MinValue;

    /// <param name="requestsPerSecond">Universalis tolère ~25 req/s ; on reste volontairement bien en dessous.</param>
    /// <param name="maxConcurrentRequests">Universalis tolère 8 connexions simultanées par IP.</param>
    public UniversalisClient(string userAgent, double requestsPerSecond = 10, int maxConcurrentRequests = 4, HttpClient? httpClient = null)
    {
        http = httpClient ?? new HttpClient { BaseAddress = new Uri("https://universalis.app/api/v2/") };
        http.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
        throttle = new SemaphoreSlim(1, 1);
        concurrency = new SemaphoreSlim(maxConcurrentRequests, maxConcurrentRequests);
        minDelayBetweenRequests = TimeSpan.FromSeconds(1 / requestsPerSecond);
    }

    /// <summary>
    /// Données de marché pour un monde, un data center ou une région.
    /// Les ids sont envoyés par paquets de 100 (limite de l'API).
    /// </summary>
    public async Task<Dictionary<uint, ItemMarketData>> GetMarketDataAsync(
        string worldDcOrRegion,
        IReadOnlyCollection<uint> itemIds,
        int listings,
        int entries,
        CancellationToken ct = default)
    {
        var result = new Dictionary<uint, ItemMarketData>(itemIds.Count);
        foreach (var chunk in itemIds.Chunk(MaxItemsPerRequest))
        {
            // Avec un seul id, l'API renvoie l'objet directement au lieu du format multi-objets.
            // On double l'id pour garder un format de réponse unique.
            var ids = chunk.Length == 1 ? [chunk[0], chunk[0]] : chunk;
            var url = $"{Uri.EscapeDataString(worldDcOrRegion)}/{string.Join(',', ids)}?listings={listings}&entries={entries}";

            var response = await GetAsync<MultiItemResponse>(url, ct);
            if (response is null)
                continue;

            foreach (var item in response.Items.Values)
                result[item.ItemId] = item;
        }

        return result;
    }

    /// <summary>Ventes récentes par objet, limitées à <paramref name="maxEntries"/> et aux <paramref name="within"/> dernières.</summary>
    public async Task<Dictionary<uint, HistoryItem>> GetHistoryAsync(
        string worldDcOrRegion,
        IReadOnlyCollection<uint> itemIds,
        int maxEntries,
        TimeSpan within,
        CancellationToken ct = default)
    {
        var result = new Dictionary<uint, HistoryItem>(itemIds.Count);
        foreach (var chunk in itemIds.Chunk(MaxItemsPerRequest))
        {
            // Même particularité que l'endpoint de marché avec un seul id.
            var ids = chunk.Length == 1 ? [chunk[0], chunk[0]] : chunk;
            var url = $"history/{Uri.EscapeDataString(worldDcOrRegion)}/{string.Join(',', ids)}"
                      + $"?entriesToReturn={maxEntries}&entriesWithin={(long)within.TotalSeconds}";

            var response = await GetAsync<HistoryResponse>(url, ct);
            if (response is null)
                continue;

            foreach (var item in response.Items.Values)
                result[item.ItemId] = item;
        }

        return result;
    }

    private const int MaxAttempts = 3;

    private async Task<T?> GetAsync<T>(string url, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            await concurrency.WaitAsync(ct);
            try
            {
                await WaitForSlotAsync(ct);
                return await http.GetFromJsonAsync<T>(url, ct);
            }
            catch (Exception e) when (attempt < MaxAttempts && IsTransient(e, ct))
            {
                // Erreur passagère (surcharge, coupure réseau) : on réessaie un peu plus tard.
            }
            finally
            {
                concurrency.Release();
            }

            await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), ct);
        }
    }

    private static bool IsTransient(Exception e, CancellationToken ct) => e switch
    {
        HttpRequestException { StatusCode: null } => true,
        HttpRequestException { StatusCode: var status } => (int)status! == 429 || (int)status >= 500,
        // Délai d'attente du HttpClient, pas une annulation demandée par l'appelant.
        TaskCanceledException => !ct.IsCancellationRequested,
        _ => false,
    };

    private async Task WaitForSlotAsync(CancellationToken ct)
    {
        await throttle.WaitAsync(ct);
        try
        {
            var wait = lastRequest + minDelayBetweenRequests - DateTime.UtcNow;
            if (wait > TimeSpan.Zero)
                await Task.Delay(wait, ct);
            lastRequest = DateTime.UtcNow;
        }
        finally
        {
            throttle.Release();
        }
    }

    public void Dispose()
    {
        http.Dispose();
        throttle.Dispose();
        concurrency.Dispose();
    }
}
