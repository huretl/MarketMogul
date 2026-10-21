namespace MarketMogul.Core.Analysis;

/// <summary>
/// Objets qui ne se vendent pas sur le monde d'origine, à ne revérifier qu'après un délai.
/// Lié à un monde : changer de monde d'origine repart d'un cache vide.
/// </summary>
public sealed class DormantItemCache
{
    public static readonly TimeSpan RecheckAfter = TimeSpan.FromHours(24);

    public uint HomeWorldId { get; set; }

    /// <summary>Objet → date (UTC) à partir de laquelle le revérifier.</summary>
    public Dictionary<uint, DateTime> NextCheck { get; set; } = new();

    public List<uint> ItemsToCheck(uint homeWorldId, IEnumerable<uint> itemIds, DateTime now)
    {
        if (homeWorldId != HomeWorldId)
        {
            HomeWorldId = homeWorldId;
            NextCheck.Clear();
        }

        return itemIds.Where(id => !NextCheck.TryGetValue(id, out var next) || next <= now).ToList();
    }

    public void Update(uint itemId, bool dormant, DateTime now)
    {
        if (dormant)
            NextCheck[itemId] = now + RecheckAfter;
        else
            NextCheck.Remove(itemId);
    }
}
