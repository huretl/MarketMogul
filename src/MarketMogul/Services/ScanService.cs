using System;
using System.Collections.Generic;
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
    private CancellationTokenSource? cts;

    public bool IsScanning { get; private set; }
    public double Progress { get; private set; }
    public string? Error { get; private set; }
    public DateTime? LastScan { get; private set; }
    public WorldInfo? ScannedHome { get; private set; }
    public IReadOnlyList<Opportunity> Opportunities { get; private set; } = [];

    public ScanService(UniversalisClient client, ItemCatalog catalog, IPluginLog log)
    {
        this.catalog = catalog;
        this.log = log;
        scanner = new ArbitrageScanner(client);
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
            try
            {
                var progress = new Progress<double>(p => Progress = p);
                var result = await scanner.ScanAsync(home.Id, home.DataCenter, catalog.MarketableItemIds, settings, catalog.VendorPrices, progress, ct);
                Opportunities = result;
                ScannedHome = home;
                LastScan = DateTime.Now;
                log.Information($"Scan terminé : {result.Count} opportunités pour {home.Name} ({home.DataCenter}).");
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
                IsScanning = false;
            }
        }, ct);
    }

    public void Cancel() => cts?.Cancel();

    public void Dispose()
    {
        cts?.Cancel();
        cts?.Dispose();
    }
}
