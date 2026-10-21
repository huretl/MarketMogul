using System.Collections.Generic;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace MarketMogul.Services;

public sealed record ItemInfo(uint Id, string Name, uint IconId, int StackSize);

/// <summary>Objets vendables au marketboard, lus dans les données du jeu.</summary>
public sealed class ItemCatalog
{
    private readonly Dictionary<uint, ItemInfo> items = new();

    public IReadOnlyList<uint> MarketableItemIds { get; }

    public ItemCatalog(IDataManager dataManager)
    {
        var ids = new List<uint>();
        foreach (var item in dataManager.GetExcelSheet<Item>())
        {
            // Un objet sans catégorie de recherche n'apparaît pas au marketboard.
            if (item.ItemSearchCategory.RowId == 0 || item.IsUntradable)
                continue;

            items[item.RowId] = new ItemInfo(item.RowId, item.Name.ExtractText(), item.Icon, (int)item.StackSize);
            ids.Add(item.RowId);
        }

        MarketableItemIds = ids;
    }

    public ItemInfo? Get(uint itemId) => items.GetValueOrDefault(itemId);

    public int StackSize(uint itemId) => Get(itemId)?.StackSize ?? 1;
}
