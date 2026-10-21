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

    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
