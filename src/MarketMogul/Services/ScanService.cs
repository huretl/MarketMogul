using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using MarketMogul.Core.Analysis;
using MarketMogul.Core.Universalis;

namespace MarketMogul.Services;

/// <summary>Lance les scans Universalis en arrière-plan et expose le dernier résultat à l'interface.</summary>
public sealed class ScanService : IDisposable
{
    private readonly ArbitrageScanner scanner;
    private readonly ItemCatalog catalog;
    private readonly IPluginLog log;
    private readonly string dormantCachePath;
    private readonly DormantItemCache dormantCache;
    private CancellationTokenSource? cts;

    public bool IsScanning { get; private set; }
    public double Progress { get; private set; }
    public string? Error { get; private set; }
    public DateTime? LastScan { get; private set; }
    public TimeSpan? LastDuration { get; private set; }
    public ScanStats? LastStats { get; private set; }
    public WorldInfo? ScannedHome { get; private set; }
    public IReadOnlyList<Opportunity> Opportunities { get; private set; } = [];

    public ScanService(UniversalisClient client, ItemCatalog catalog, IPluginLog log, string configDirectory)
    {
        this.catalog = catalog;
        this.log = log;
        scanner = new ArbitrageScanner(client);
        dormantCachePath = Path.Combine(configDirectory, "dormant-items.json");
        dormantCache = LoadDormantCache();
    }

    public void Start(WorldInfo home, TradingSettings settings)
    {
        if (IsScanning)
            return;

        cts = new CancellationTokenSource();
        var ct = cts.Token;
        IsScanning = true;
        Progress = 0;
        Error = null;

        Task.Run(async () =>
        {
            var started = DateTime.Now;
            try
            {
                var progress = new Progress<double>(p => Progress = p);
                var result = await scanner.ScanAsync(
                    home.Id, home.DataCenter, catalog.MarketableItemIds, settings, catalog.VendorPrices, dormantCache, progress, ct);
                Opportunities = result.Opportunities;
                LastStats = result.Stats;
                ScannedHome = home;
                LastScan = DateTime.Now;
                LastDuration = LastScan - started;
                log.Information($"Scan terminé en {LastDuration:mm\\:ss} : {result.Opportunities.Count} opportunités pour {home.Name} ({home.DataCenter}), {result.Stats}.");
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                Error = e.Message;
                log.Error(e, "Échec du scan Universalis");
            }
            finally
            {
                // Même un scan interrompu a appris quels objets dorment.
                SaveDormantCache();
                IsScanning = false;
            }
        }, ct);
    }

    public void Cancel() => cts?.Cancel();

    /// <summary>Oublie les objets en sommeil : le prochain scan revérifie tout.</summary>
    public void ResetDormantCache()
    {
        dormantCache.NextCheck.Clear();
        SaveDormantCache();
    }

    public int DormantCount => dormantCache.NextCheck.Count;

    private DormantItemCache LoadDormantCache()
    {
        try
        {
            if (File.Exists(dormantCachePath))
                return JsonSerializer.Deserialize<DormantItemCache>(File.ReadAllText(dormantCachePath)) ?? new();
        }
        catch (Exception e)
        {
            log.Warning(e, "Cache des objets en sommeil illisible, on repart de zéro");
        }

        return new DormantItemCache();
    }

    private void SaveDormantCache()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(dormantCachePath)!);
            File.WriteAllText(dormantCachePath, JsonSerializer.Serialize(dormantCache));
        }
        catch (Exception e)
        {
            log.Warning(e, "Impossible d'enregistrer le cache des objets en sommeil");
        }
    }

    public void Dispose()
    {
        cts?.Cancel();
        cts?.Dispose();
    }
}
