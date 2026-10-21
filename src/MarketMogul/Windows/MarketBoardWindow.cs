using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using MarketMogul.Core.Analysis;
using MarketMogul.Services;

namespace MarketMogul.Windows;

/// <summary>
/// Panneau accroché à droite de la liste des annonces du marketboard :
/// indique, pour l'objet consulté, quelles annonces acheter pour revendre sur le monde d'origine.
/// </summary>
public class MarketBoardWindow : Window
{
    private const string AddonName = "ItemSearchResult";

    private readonly Plugin plugin;

    public MarketBoardWindow(Plugin plugin)
        : base("MarketMogul · Conseil d'achat###MarketMogulMarketBoard", ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoSavedSettings)
    {
        this.plugin = plugin;
        IsOpen = true;
        ShowCloseButton = false;
        RespectCloseHotkey = false;
        DisableWindowSounds = true;
    }

    public override bool DrawConditions()
    {
        if (!plugin.Configuration.ShowMarketBoardAssistant || plugin.Worlds.Home is null || plugin.MarketBoard.Current is null)
            return false;
        return Plugin.GameGui.GetAddonByName(AddonName).IsVisible;
    }

    public override void PreDraw()
    {
        // Suit la fenêtre du jeu, à sa droite et à sa hauteur.
        var addon = Plugin.GameGui.GetAddonByName(AddonName);
        Position = addon.Position + new Vector2(addon.ScaledWidth + 8 * ImGuiHelpers.GlobalScale, 0);
        PositionCondition = ImGuiCond.Always;
        Size = new Vector2(440 * ImGuiHelpers.GlobalScale, addon.ScaledHeight);
        SizeCondition = ImGuiCond.Always;
    }

    public override void Draw()
    {
        var offerings = plugin.MarketBoard.Current!;
        var home = plugin.Worlds.Home!;
        var item = plugin.Catalog.Get(offerings.ItemId);

        if (item is not null)
        {
            var icon = Plugin.TextureProvider.GetFromGameIcon(new GameIconLookup(item.IconId)).GetWrapOrEmpty();
            ImGui.Image(icon.Handle, new Vector2(ImGui.GetTextLineHeight() * 1.5f));
            ImGui.SameLine();
        }
        ImGui.TextUnformatted(item?.Name ?? $"Objet #{offerings.ItemId}");

        if (!plugin.Worlds.IsVisiting)
            ImGui.TextDisabled("Tu es sur ton monde d'origine : achat pour revente sur place.");

        var homeData = plugin.HomePrices.Get(home.Id, offerings.ItemId);
        if (homeData is null)
        {
            ImGui.TextDisabled(plugin.HomePrices.IsLoading(home.Id, offerings.ItemId)
                ? $"Chargement des prix de {home.Name}..."
                : $"Pas de données de vente pour {home.Name}.");
            return;
        }

        var settings = plugin.Configuration.Trading;
        var now = DateTimeOffset.UtcNow;
        var advice = new Dictionary<bool, (HomeMarketStats Stats, BuyLimits? Limits, HashSet<ulong> Buy)>();
        foreach (var hq in offerings.Listings.Select(l => l.Hq).Distinct().Order())
        {
            var stats = HomeMarketStats.From(homeData, hq, now, settings.SalesWindowDays, ArbitrageScanner.HomeHistoryEntries);
            var limits = BuyAdvisor.Limits(stats, settings);
            var listings = offerings.Listings.Where(l => l.Hq == hq).ToList();
            var buy = limits is null
                ? []
                : OrderBook.SelectListings(listings.Select(l => (l.PricePerUnit, l.Quantity)).ToList(), limits.MaxPricePerUnit, limits.MaxUnits)
                    .Select(i => listings[i].ListingId)
                    .ToHashSet();
            advice[hq] = (stats, limits, buy);
        }

        ImGui.Separator();
        foreach (var (hq, (stats, limits, _)) in advice)
            DrawLimits(home.Name, hq, stats, limits);

        DrawSummary(offerings, advice.Values.SelectMany(a => a.Buy).ToHashSet(), advice);
        ImGui.Separator();
        DrawListings(offerings, advice);
    }

    private static void DrawLimits(string homeName, bool hq, HomeMarketStats stats, BuyLimits? limits)
    {
        var quality = hq ? "HQ" : "NQ";
        if (limits is null)
        {
            ImGui.TextDisabled(stats.TargetSellPrice is null
                ? $"{quality} : aucune vente récente sur {homeName}."
                : $"{quality} : se vend trop peu sur {homeName} ({stats.SalesPerDay:0.##} / jour).");
            return;
        }

        ImGui.TextUnformatted($"{quality} : revente ~{limits.TargetSellPrice:N0} sur {homeName}, {stats.SalesPerDay:0.#} ventes / jour");
        ImGui.TextColored(ImGuiColors.HealerGreen, $"     Achète jusqu'à {limits.MaxPricePerUnit:N0} / u, {limits.MaxUnits} unités max");
    }

    private void DrawSummary(LiveOfferings offerings, HashSet<ulong> toBuy, Dictionary<bool, (HomeMarketStats Stats, BuyLimits? Limits, HashSet<ulong> Buy)> advice)
    {
        var settings = plugin.Configuration.Trading;
        var buys = offerings.Listings.Where(l => toBuy.Contains(l.ListingId)).ToList();
        if (buys.Count == 0)
        {
            ImGui.TextColored(ImGuiColors.DalamudGrey, "Rien de rentable à acheter ici.");
            return;
        }

        var cost = buys.Sum(l => l.PricePerUnit * l.Quantity * (1 + settings.BuyTaxRate));
        var profit = buys.Sum(l => BuyAdvisor.UnitProfit(l.PricePerUnit, advice[l.Hq].Limits!.TargetSellPrice, settings) * l.Quantity);
        ImGui.TextColored(ImGuiColors.ParsedGold, $"À acheter : {buys.Sum(l => l.Quantity)} unités · coût {cost:N0} · profit ~{profit:N0}");
    }

    private void DrawListings(LiveOfferings offerings, Dictionary<bool, (HomeMarketStats Stats, BuyLimits? Limits, HashSet<ulong> Buy)> advice)
    {
        var settings = plugin.Configuration.Trading;

        const ImGuiTableFlags flags = ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY | ImGuiTableFlags.SizingStretchProp;
        using var table = ImRaii.Table("live-listings", 5, flags);
        if (!table.Success)
            return;

        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableSetupColumn("Prix / u", ImGuiTableColumnFlags.None, 1.2f);
        ImGui.TableSetupColumn("Qté", ImGuiTableColumnFlags.None, 0.6f);
        ImGui.TableSetupColumn("Servant", ImGuiTableColumnFlags.None, 1.6f);
        ImGui.TableSetupColumn("Profit / u", ImGuiTableColumnFlags.None, 1.1f);
        ImGui.TableSetupColumn("Conseil", ImGuiTableColumnFlags.None, 1f);
        ImGui.TableHeadersRow();

        var buyRowColor = ImGui.GetColorU32(ImGuiColors.HealerGreen with { W = 0.18f });

        foreach (var listing in offerings.Listings.OrderBy(l => l.PricePerUnit))
        {
            var (_, limits, buy) = advice[listing.Hq];
            var unitProfit = limits is null ? (long?)null : BuyAdvisor.UnitProfit(listing.PricePerUnit, limits.TargetSellPrice, settings);
            var shouldBuy = buy.Contains(listing.ListingId);

            ImGui.TableNextRow();
            if (shouldBuy)
                ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg1, buyRowColor);

            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"{listing.PricePerUnit:N0}{(listing.Hq ? " HQ" : "")}");

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(listing.Quantity.ToString());

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(listing.RetainerName);

            ImGui.TableNextColumn();
            if (unitProfit is { } p)
                ImGui.TextColored(p > 0 ? ImGuiColors.HealerGreen : ImGuiColors.DalamudGrey, $"{p:+#,0;-#,0;0}");
            else
                ImGui.TextDisabled("-");

            ImGui.TableNextColumn();
            if (shouldBuy)
                ImGui.TextColored(ImGuiColors.HealerGreen, "ACHETER");
            else if (unitProfit > 0 && limits is not null && listing.PricePerUnit <= limits.MaxPricePerUnit)
                ImGui.TextColored(ImGuiColors.DalamudYellow, "Hors quota");
            else
                ImGui.TextDisabled("Trop cher");

            if (ImGui.IsItemHovered() && !shouldBuy && unitProfit > 0 && limits is not null && listing.PricePerUnit <= limits.MaxPricePerUnit)
                ImGui.SetTooltip("Rentable, mais tu aurais plus que ce que ton monde peut absorber dans l'horizon de ton style de trading.");
        }
    }
}
