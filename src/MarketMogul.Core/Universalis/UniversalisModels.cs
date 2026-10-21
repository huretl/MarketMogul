using System.Text.Json.Serialization;

namespace MarketMogul.Core.Universalis;

// Sous-ensemble de la réponse de l'API v2 de Universalis : seuls les champs utilisés sont mappés.
// Doc : https://docs.universalis.app/

public sealed class MultiItemResponse
{
    [JsonPropertyName("items")]
    public Dictionary<string, ItemMarketData> Items { get; set; } = new();

    [JsonPropertyName("unresolvedItems")]
    public List<uint> UnresolvedItems { get; set; } = new();
}

public sealed class ItemMarketData
{
    [JsonPropertyName("itemID")]
    public uint ItemId { get; set; }

    /// <summary>Dernier envoi de données, en millisecondes Unix.</summary>
    [JsonPropertyName("lastUploadTime")]
    public long LastUploadTime { get; set; }

    [JsonPropertyName("listings")]
    public List<Listing> Listings { get; set; } = new();

    [JsonPropertyName("recentHistory")]
    public List<Sale> RecentHistory { get; set; } = new();

    /// <summary>Date du dernier envoi par monde (id du monde → millisecondes Unix). Rempli pour les requêtes data center.</summary>
    [JsonPropertyName("worldUploadTimes")]
    public Dictionary<string, long>? WorldUploadTimes { get; set; }
}

public sealed class Listing
{
    [JsonPropertyName("pricePerUnit")]
    public long PricePerUnit { get; set; }

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }

    [JsonPropertyName("hq")]
    public bool Hq { get; set; }

    /// <summary>Absent pour une requête sur un seul monde.</summary>
    [JsonPropertyName("worldID")]
    public uint? WorldId { get; set; }

    [JsonPropertyName("worldName")]
    public string? WorldName { get; set; }

    [JsonPropertyName("lastReviewTime")]
    public long LastReviewTime { get; set; }
}

public sealed class Sale
{
    [JsonPropertyName("pricePerUnit")]
    public long PricePerUnit { get; set; }

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }

    [JsonPropertyName("hq")]
    public bool Hq { get; set; }

    /// <summary>Secondes Unix.</summary>
    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }
}

/// <summary>Réponse de l'endpoint d'historique (/api/v2/history) : bien plus rapide que l'historique de l'endpoint de marché.</summary>
public sealed class HistoryResponse
{
    [JsonPropertyName("items")]
    public Dictionary<string, HistoryItem> Items { get; set; } = new();
}

public sealed class HistoryItem
{
    [JsonPropertyName("itemID")]
    public uint ItemId { get; set; }

    [JsonPropertyName("lastUploadTime")]
    public long LastUploadTime { get; set; }

    [JsonPropertyName("entries")]
    public List<Sale> Entries { get; set; } = new();
}
