namespace MarketMogul.Core.Ledger;

/// <param name="ExpectedSellPrice">Prix de revente estimé au moment de l'achat, s'il était connu.</param>
public sealed record PurchaseRecord(
    DateTime At,
    uint ItemId,
    bool Hq,
    uint WorldId,
    string WorldName,
    long PricePerUnit,
    int Quantity,
    long? ExpectedSellPrice)
{
    public long Subtotal => PricePerUnit * Quantity;
}
