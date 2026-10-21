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

namespace MarketMogul.Windows;

public class MainWindow : Window
{
    private readonly Plugin plugin;

    public MainWindow(Plugin plugin) : base("MarketMogul###MarketMogulMain")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(760, 360),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };

        this.plugin = plugin;
    }

    public override void Draw()
    {
        var worlds = plugin.Worlds;
        if (worlds.Home is null || worlds.Current is null)
        {
            ImGui.TextDisabled("Connecte-toi avec un personnage pour commencer.");
            return;
        }

        DrawHeader();
        ImGui.Separator();

        var scanner = plugin.Scanner;
        if (scanner.ScannedHome is null)
        {
            ImGui.TextDisabled("Lance un scan pour obtenir des recommandations (environ une minute).");
            return;
        }

        if (scanner.ScannedHome.Id != worlds.Home.Id)
        {
            ImGui.TextColored(ImGuiColors.DalamudOrange, $"Le dernier scan concerne {scanner.ScannedHome.Name} : relance un scan pour ce personnage.");
            return;
        }

        using var tabs = ImRaii.TabBar("tabs");
        if (!tabs.Success)
            return;

        using (var tab = ImRaii.TabItem("Plan"))
        {
            if (tab.Success)
                DrawPlan();
        }

        using (var tab = ImRaii.TabItem("Toutes les opportunités"))
        {
            if (tab.Success)
                DrawOpportunities();
        }
    }

    private void DrawHeader()
    {
        var worlds = plugin.Worlds;
        var scanner = plugin.Scanner;

        if (worlds.IsVisiting)
            ImGui.TextColored(ImGuiColors.HealerGreen, $"MODE ACHAT · {worlds.Current!.Name}");
        else
            ImGui.TextColored(ImGuiColors.ParsedGold, $"MODE VENTE · {worlds.Home!.Name}");

        ImGui.SameLine();
        ImGui.TextDisabled($"Origine : {worlds.Home!.Name} ({worlds.Home.DataCenter})");

        if (worlds.IsOnOtherDataCenter)
            ImGui.TextColored(ImGuiColors.DalamudOrange, "Tu es sur un autre data center : les recommandations ne couvrent que le tien.");

        if (scanner.IsScanning)
        {
            ImGui.ProgressBar((float)scanner.Progress, new Vector2(200 * ImGuiHelpers.GlobalScale, 0), $"Scan {scanner.Progress:P0}");
            ImGui.SameLine();
            if (ImGui.Button("Annuler"))
                scanner.Cancel();
        }
        else if (ImGui.Button("Scanner le marché"))
        {
            scanner.Start(worlds.Home, plugin.Configuration.Trading);
        }

        ImGui.SameLine();
        if (ImGui.Button("Réglages"))
            plugin.ToggleConfigUi();

        if (scanner.LastScan is { } last)
        {
            ImGui.SameLine();
            ImGui.TextDisabled($"Dernier scan : {last:HH:mm} ({(int)(DateTime.Now - last).TotalMinutes} min)");
        }

        if (scanner.Error is { } error)
            ImGui.TextColored(ImGuiColors.DalamudRed, $"Erreur : {error}");
    }

    /// <summary>Le plan, groupé par monde : un itinéraire d'achat.</summary>
    private void DrawPlan()
    {
        var worlds = plugin.Worlds;
        var plan = plugin.Plan;
        plan.RefreshIfStale();
        if (plan.Current is not { } portfolio)
            return;

        ImGui.TextUnformatted($"Budget : {portfolio.Budget:N0} gils");
        ImGui.SameLine();
        ImGui.TextDisabled(plugin.Configuration.BudgetReserve > 0 ? $"(inventaire − réserve de {plugin.Configuration.BudgetReserve:N0})" : "(inventaire)");
        ImGui.SameLine();
        ImGui.TextUnformatted($"· Emplacements : {portfolio.SlotsUsed} / {portfolio.Slots}");
        ImGui.SameLine();
        ImGui.TextDisabled(plan.SlotsFromRetainers ? "(servants)" : "(réglages, passe à la sonnette pour le nombre réel)");
        ImGui.SameLine();
        if (ImGui.SmallButton("Recalculer"))
            plan.Recompute();

        if (portfolio.Positions.Count == 0)
        {
            ImGui.TextDisabled(portfolio.Budget == 0 ? "Pas de gils à investir." : "Aucune opération ne tient dans ton budget et tes emplacements.");
            return;
        }

        ImGui.TextColored(ImGuiColors.ParsedGold,
            $"Investissement {portfolio.TotalCost:N0} · profit attendu ~{portfolio.ExpectedProfit:N0} gils · {portfolio.Positions.Count} objets");
        ImGui.Spacing();

        var byWorld = portfolio.Positions
            .SelectMany(p => p.Opportunity.Purchases.Select(purchase => (p.Opportunity, Purchase: purchase)))
            .GroupBy(x => (x.Purchase.WorldId, x.Purchase.WorldName))
            .OrderByDescending(g => g.Key.WorldId == worlds.Current!.Id)
            .ThenByDescending(g => g.Sum(x => x.Purchase.Subtotal))
            .ToList();

        uint? onlyWorld = worlds.IsVisiting && plugin.Configuration.OnlyCurrentWorldWhenVisiting ? worlds.Current!.Id : null;

        using var child = ImRaii.Child("plan", Vector2.Zero);
        foreach (var group in byWorld)
        {
            if (onlyWorld is { } id && group.Key.WorldId != id)
                continue;

            var here = group.Key.WorldId == worlds.Current!.Id;
            var title = $"{group.Key.WorldName} · {group.Sum(x => x.Purchase.Quantity)} unités · {group.Sum(x => x.Purchase.Subtotal):N0} gils";
            using (ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.HealerGreen, here))
            {
                if (!ImGui.CollapsingHeader($"{title}{(here ? "  ← tu es ici" : "")}###{group.Key.WorldId}", ImGuiTreeNodeFlags.DefaultOpen))
                    continue;
            }

            const ImGuiTableFlags flags = ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp;
            using var table = ImRaii.Table($"world-{group.Key.WorldId}", 4, flags);
            if (!table.Success)
                continue;

            ImGui.TableSetupColumn("Objet", ImGuiTableColumnFlags.None, 3);
            ImGui.TableSetupColumn("Annonce", ImGuiTableColumnFlags.None, 1.6f);
            ImGui.TableSetupColumn("Revente / u", ImGuiTableColumnFlags.None, 1.2f);
            ImGui.TableSetupColumn("Profit", ImGuiTableColumnFlags.None, 1.2f);
            ImGui.TableHeadersRow();

            foreach (var (opportunity, purchase) in group.OrderBy(x => x.Opportunity.ItemId).ThenBy(x => x.Purchase.PricePerUnit))
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                DrawItemName(opportunity.ItemId, opportunity.Hq);

                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"{purchase.Quantity} × {purchase.PricePerUnit:N0}");

                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"{opportunity.TargetSellPrice:N0}");

                ImGui.TableNextColumn();
                var settings = plugin.Configuration.Trading;
                var profit = BuyAdvisor.UnitProfit(purchase.PricePerUnit, opportunity.TargetSellPrice, settings) * purchase.Quantity;
                ImGui.TextColored(ImGuiColors.HealerGreen, $"+{profit:N0}");
            }
        }
    }

    private void DrawOpportunities()
    {
        var worlds = plugin.Worlds;
        var scanner = plugin.Scanner;

        uint? onlyWorld = worlds.IsVisiting && plugin.Configuration.OnlyCurrentWorldWhenVisiting ? worlds.Current!.Id : null;
        var rows = scanner.Opportunities
            .Where(o => onlyWorld is null || o.Purchases.Any(p => p.WorldId == onlyWorld))
            .ToList();

        if (rows.Count == 0)
        {
            ImGui.TextDisabled(onlyWorld is null ? "Aucune opportunité avec les réglages actuels." : "Rien à acheter sur ce monde.");
            return;
        }

        ImGui.TextUnformatted($"{rows.Count} opportunités, sans limite de budget · profit attendu total ~{rows.Sum(o => o.ExpectedProfit):N0} gils");

        const ImGuiTableFlags flags = ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.ScrollY
                                      | ImGuiTableFlags.Resizable | ImGuiTableFlags.SizingStretchProp;
        using var table = ImRaii.Table("opportunities", 8, flags);
        if (!table.Success)
            return;

        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableSetupColumn("Objet", ImGuiTableColumnFlags.None, 3);
        ImGui.TableSetupColumn("Acheter sur", ImGuiTableColumnFlags.None, 2.5f);
        ImGui.TableSetupColumn("Qté", ImGuiTableColumnFlags.None, 0.6f);
        ImGui.TableSetupColumn("Coût", ImGuiTableColumnFlags.None, 1.2f);
        ImGui.TableSetupColumn("Revente / u", ImGuiTableColumnFlags.None, 1.2f);
        ImGui.TableSetupColumn("Profit", ImGuiTableColumnFlags.None, 1.2f);
        ImGui.TableSetupColumn("Jours", ImGuiTableColumnFlags.None, 0.7f);
        ImGui.TableSetupColumn("Fiabilité", ImGuiTableColumnFlags.None, 0.9f);
        ImGui.TableHeadersRow();

        foreach (var opportunity in rows)
            DrawRow(opportunity, onlyWorld);
    }

    private void DrawItemName(uint itemId, bool hq)
    {
        var item = plugin.Catalog.Get(itemId);
        if (item is not null)
        {
            var icon = Plugin.TextureProvider.GetFromGameIcon(new GameIconLookup(item.IconId, hq)).GetWrapOrEmpty();
            ImGui.Image(icon.Handle, new Vector2(ImGui.GetTextLineHeight()));
            ImGui.SameLine();
        }
        ImGui.TextUnformatted((item?.Name ?? $"#{itemId}") + (hq ? " (HQ)" : ""));
    }

    private void DrawRow(Opportunity o, uint? highlightWorld)
    {
        ImGui.TableNextRow();

        ImGui.TableNextColumn();
        DrawItemName(o.ItemId, o.Hq);

        ImGui.TableNextColumn();
        var byWorld = o.Purchases.GroupBy(p => p.WorldName).Select(g => $"{g.Key} ×{g.Sum(p => p.Quantity)}");
        ImGui.TextUnformatted(string.Join(", ", byWorld));
        if (ImGui.IsItemHovered())
        {
            var detail = o.Purchases
                .Where(p => highlightWorld is null || p.WorldId == highlightWorld)
                .Select(p => $"{p.WorldName} : {p.Quantity} × {p.PricePerUnit:N0}");
            ImGui.SetTooltip($"Annonces à acheter :\n{string.Join("\n", detail)}");
        }

        ImGui.TableNextColumn();
        ImGui.TextUnformatted(o.Units.ToString());

        ImGui.TableNextColumn();
        ImGui.TextUnformatted($"{o.TotalCost:N0}");

        ImGui.TableNextColumn();
        ImGui.TextUnformatted($"{o.TargetSellPrice:N0}");

        ImGui.TableNextColumn();
        ImGui.TextColored(ImGuiColors.HealerGreen, $"+{o.ExpectedProfit:N0}");
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip($"Rendement : {o.Roi:P0}\nProfit / jour : {o.ProfitPerDay:N0}\nVentes / jour sur ton monde : {o.SalesPerDay:0.#}\nDéjà en vente sur ton monde : {o.HomeListedQuantity}");

        ImGui.TableNextColumn();
        ImGui.TextUnformatted($"{o.DaysToSell:0.#}");

        ImGui.TableNextColumn();
        var color = o.Confidence switch
        {
            >= 0.6 => ImGuiColors.HealerGreen,
            >= 0.3 => ImGuiColors.DalamudYellow,
            _ => ImGuiColors.DalamudOrange,
        };
        ImGui.TextColored(color, $"{o.Confidence:P0}");
    }
}
