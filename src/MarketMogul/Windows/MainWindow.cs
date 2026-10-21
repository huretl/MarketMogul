using System;
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
        DrawOpportunities();
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

    private void DrawOpportunities()
    {
        var worlds = plugin.Worlds;
        var scanner = plugin.Scanner;

        if (scanner.ScannedHome is null)
        {
            ImGui.TextDisabled("Lance un scan pour obtenir des recommandations (environ une minute).");
            return;
        }

        if (scanner.ScannedHome.Id != worlds.Home!.Id)
        {
            ImGui.TextColored(ImGuiColors.DalamudOrange, $"Le dernier scan concerne {scanner.ScannedHome.Name} : relance un scan pour ce personnage.");
            return;
        }

        uint? onlyWorld = worlds.IsVisiting && plugin.Configuration.OnlyCurrentWorldWhenVisiting ? worlds.Current!.Id : null;
        var rows = scanner.Opportunities
            .Where(o => onlyWorld is null || o.Purchases.Any(p => p.WorldId == onlyWorld))
            .ToList();

        if (rows.Count == 0)
        {
            ImGui.TextDisabled(onlyWorld is null ? "Aucune opportunité avec les réglages actuels." : "Rien à acheter sur ce monde.");
            return;
        }

        ImGui.TextUnformatted($"{rows.Count} opportunités · profit attendu total ~{rows.Sum(o => o.ExpectedProfit):N0} gils");

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

    private void DrawRow(Opportunity o, uint? highlightWorld)
    {
        ImGui.TableNextRow();

        ImGui.TableNextColumn();
        var item = plugin.Catalog.Get(o.ItemId);
        if (item is not null)
        {
            var icon = Plugin.TextureProvider.GetFromGameIcon(new GameIconLookup(item.IconId, o.Hq)).GetWrapOrEmpty();
            ImGui.Image(icon.Handle, new Vector2(ImGui.GetTextLineHeight()));
            ImGui.SameLine();
        }
        ImGui.TextUnformatted((item?.Name ?? $"#{o.ItemId}") + (o.Hq ? " (HQ)" : ""));

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
