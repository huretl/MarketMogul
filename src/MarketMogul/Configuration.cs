using System;
using Dalamud.Configuration;
using MarketMogul.Core.Analysis;

namespace MarketMogul;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public TradingSettings Trading { get; set; } = new();

    /// <summary>En mode achat, n'afficher que les achats à faire sur le monde où l'on se trouve.</summary>
    public bool OnlyCurrentWorldWhenVisiting { get; set; } = true;

    /// <summary>Panneau de conseil affiché à côté de la liste des annonces du marketboard.</summary>
    public bool ShowMarketBoardAssistant { get; set; } = true;

    /// <summary>Gils à garder de côté : le plan n'investit que ce qui dépasse.</summary>
    public long BudgetReserve { get; set; } = 0;

    /// <summary>Emplacements de vente supposés libres tant que les servants n'ont pas été consultés.</summary>
    public int FallbackListingSlots { get; set; } = 20;

    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
