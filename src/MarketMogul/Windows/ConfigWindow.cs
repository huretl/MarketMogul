using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using MarketMogul.Core.Analysis;

namespace MarketMogul.Windows;

public class ConfigWindow : Window
{
    private readonly Configuration configuration;

    public ConfigWindow(Plugin plugin) : base("MarketMogul · Réglages###MarketMogulConfig")
    {
        Size = new Vector2(440, 420);
        SizeCondition = ImGuiCond.FirstUseEver;

        configuration = plugin.Configuration;
    }

    public override void Draw()
    {
        var trading = configuration.Trading;
        var changed = false;

        ImGui.TextUnformatted("Style de trading");
        changed |= StyleOption(trading, TradingStyle.Steady, "Petits gains réguliers", "Objets qui se vendent vite, revendus en 3 jours max.");
        changed |= StyleOption(trading, TradingStyle.Balanced, "Équilibré", "Revente en 7 jours max.");
        changed |= StyleOption(trading, TradingStyle.BigHits, "Gros coups", "Grosses marges, revente jusqu'à 3 semaines.");

        ImGui.Separator();

        var minUnit = (int)trading.MinProfitPerUnit;
        if (ImGui.InputInt("Profit min. par unité", ref minUnit, 100, 1000))
        {
            trading.MinProfitPerUnit = minUnit < 0 ? 0 : minUnit;
            changed = true;
        }

        var minTotal = (int)trading.MinTotalProfit;
        if (ImGui.InputInt("Profit min. par objet", ref minTotal, 1000, 10000))
        {
            trading.MinTotalProfit = minTotal < 0 ? 0 : minTotal;
            changed = true;
        }

        var minConfidence = (float)trading.MinConfidence * 100;
        if (ImGui.SliderFloat("Fiabilité min.", ref minConfidence, 0, 100, "%.0f %%"))
        {
            trading.MinConfidence = minConfidence / 100;
            changed = true;
        }

        var sellFee = (float)trading.SellFeeRate * 100;
        if (ImGui.SliderFloat("Frais à la revente", ref sellFee, 0, 10, "%.0f %%"))
        {
            trading.SellFeeRate = sellFee / 100;
            changed = true;
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Hypothèse prudente par défaut (5 %). Mets 0 si tu constates que tes servants touchent le prix affiché.");

        ImGui.Separator();

        var onlyHere = configuration.OnlyCurrentWorldWhenVisiting;
        if (ImGui.Checkbox("En visite, n'afficher que les achats du monde actuel", ref onlyHere))
        {
            configuration.OnlyCurrentWorldWhenVisiting = onlyHere;
            changed = true;
        }

        var reserve = (int)configuration.BudgetReserve;
        if (ImGui.InputInt("Gils à garder de côté", ref reserve, 10_000, 100_000))
        {
            configuration.BudgetReserve = reserve < 0 ? 0 : reserve;
            changed = true;
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Le plan investit tes gils d'inventaire moins cette réserve.");

        var fallbackSlots = configuration.FallbackListingSlots;
        if (ImGui.InputInt("Emplacements de vente libres", ref fallbackSlots, 1, 20))
        {
            configuration.FallbackListingSlots = Math.Clamp(fallbackSlots, 0, 200);
            changed = true;
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Utilisé tant que tes servants n'ont pas été consultés. Ensuite, le nombre réel est lu dans le jeu.");

        ImGui.Separator();

        var assistant = configuration.ShowMarketBoardAssistant;
        if (ImGui.Checkbox("Conseil d'achat à côté du marketboard", ref assistant))
        {
            configuration.ShowMarketBoardAssistant = assistant;
            changed = true;
        }

        ImGui.Spacing();
        ImGui.TextDisabled("Les changements s'appliquent au prochain scan ou recalcul du plan.");

        if (changed)
            configuration.Save();
    }

    private static bool StyleOption(TradingSettings trading, TradingStyle style, string label, string tooltip)
    {
        var clicked = ImGui.RadioButton(label, trading.Style == style);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(tooltip);
        if (clicked)
            trading.Style = style;
        return clicked;
    }
}
