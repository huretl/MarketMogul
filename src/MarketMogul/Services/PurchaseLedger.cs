using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Dalamud.Plugin.Services;
using MarketMogul.Core.Ledger;

namespace MarketMogul.Services;

/// <summary>Journal des achats, sauvegardé dans le dossier de configuration du plugin.</summary>
public sealed class PurchaseLedger
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string path;
    private readonly IPluginLog log;
    private readonly object sync = new();
    private List<PurchaseRecord> records = new();

    public PurchaseLedger(string directory, IPluginLog log)
    {
        path = Path.Combine(directory, "purchases.json");
        this.log = log;
        try
        {
            if (File.Exists(path))
                records = JsonSerializer.Deserialize<List<PurchaseRecord>>(File.ReadAllText(path)) ?? new();
        }
        catch (Exception e)
        {
            log.Error(e, "Journal des achats illisible, on repart d'un journal vide");
        }
    }

    /// <summary>Copie : modifiable sans risque pendant que l'interface la parcourt.</summary>
    public IReadOnlyList<PurchaseRecord> Records
    {
        get
        {
            lock (sync)
                return records.ToArray();
        }
    }

    public void Add(PurchaseRecord record)
    {
        lock (sync)
        {
            records = [.. records, record];
            Save();
        }
    }

    public void Clear()
    {
        lock (sync)
        {
            records = new();
            Save();
        }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(records, JsonOptions));
        }
        catch (Exception e)
        {
            log.Error(e, "Impossible d'enregistrer le journal des achats");
        }
    }
}
