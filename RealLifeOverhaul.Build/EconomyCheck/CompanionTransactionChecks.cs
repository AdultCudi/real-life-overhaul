using System.Collections.Generic;
using RealLifeOverhaul;

// Substitute only native records; the owner transaction projection is linked unchanged.
public sealed class Transaction
{
    public string transactionType;
    public TransactionTime timestamp;
    public TransactionAddress address;
    public float amount;
}
public sealed class TransactionTime { public int Day; }
public sealed class TransactionAddress { public string streetName; public int streetNumber; }
public static class SaveGameManager { public static GameInstance Current; }

internal static partial class Program
{
    private static void CompanionMalformedTransactions()
    {
        var game = new GameInstance();
        game.Transactions.Add(null);
        game.Transactions.Add(new Transaction { timestamp = new TransactionTime { Day = 12 } });
        game.Transactions.Add(new Transaction { transactionType = "bill" });
        var paid = new Transaction { transactionType = "bill", timestamp = new TransactionTime { Day = 12 }, amount = -12.5f };
        game.Transactions.Add(paid);
        SaveGameManager.Current = game;
        List<object> rows = CompanionTransactions.Rows(new Dictionary<string, string> { ["bill"] = "utilities.power" });
        Check(rows.Count == 1 && CompanionSnapshot.Json(rows).Contains("\"cents\":1250"),
            "companion: null transaction, null type and missing timestamp leave valid paid rows available");
        Check(game.Transactions.Count == 4 && game.Transactions[0] == null && game.Transactions[3] == paid && paid.amount == -12.5f,
            "companion: malformed transaction projection never edits the source list or records");
        SaveGameManager.Current = null;
        var addresses = new List<(string street, int number)> { ("z", 1), ("a", 10), ("a", 2) };
        addresses.Sort((left, right) => CompanionValues.CompareAddress(left.street, left.number, right.street, right.number));
        Check(addresses[0] == ("a", 2) && addresses[1] == ("a", 10) && addresses[2] == ("z", 1),
            "companion: interior addresses sort ordinal street then numeric building number");
    }
}
