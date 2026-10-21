using System.Net.Http.Json;

namespace MarketMogul.Core.Universalis;

public sealed class UniversalisClient : IDisposable
{
    public const int MaxItemsPerRequest = 100;

    private readonly HttpClient http;
    private readonly SemaphoreSlim throttle;
    private readonly TimeSpan minDelayBetweenRequests;
    private DateTime lastRequest = DateTime.MinValue;

    /// <param name="requestsPerSecond">Universalis tolère ~25 req/s ; on reste volontairement bien en dessous.</param>
    public UniversalisClient(string userAgent, double requestsPerSecond = 5, HttpClient? httpClient = null)
    {
        http = httpClient ?? new HttpClient { BaseAddress = new Uri("https://universalis.app/api/v2/") };
        http.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
        throttle = new SemaphoreSlim(1, 1);
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

            await WaitForSlotAsync(ct);
            var response = await http.GetFromJsonAsync<MultiItemResponse>(url, ct);
            if (response is null)
                continue;

            foreach (var item in response.Items.Values)
                result[item.ItemId] = item;
        }

        return result;
    }

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
    }
}
