using System.Text.Json;
using MarketMogul.Core.Ledger;

namespace MarketMogul.Core.Tests;

public class PurchaseRecordTests
{
    [Fact]
    public void Survives_json_round_trip()
    {
        // Le journal est sauvegardé en JSON : un record positionnel doit se relire à l'identique.
        var record = new PurchaseRecord(new DateTime(2026, 10, 21, 12, 0, 0, DateTimeKind.Utc), 42, true, 2, "Cerberus", 1500, 3, 2400);

        var json = JsonSerializer.Serialize(new List<PurchaseRecord> { record });
        var read = JsonSerializer.Deserialize<List<PurchaseRecord>>(json);

        Assert.Equal(record, Assert.Single(read!));
    }
}
