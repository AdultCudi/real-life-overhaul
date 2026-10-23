using System;
using Newtonsoft.Json.Linq;
using RealLifeOverhaul;

// Only the session boundary is substituted: parser, bounds and complete-target validation are production code.
public sealed class GameInstance
{
    public System.Collections.Generic.List<Transaction> Transactions = new System.Collections.Generic.List<Transaction>();
    public System.Collections.Generic.List<BuildingRegistration> BuildingRegistrations = new System.Collections.Generic.List<BuildingRegistration>();
    public bool Available = true;
    public int Money = 123;
    public string ModData = "unchanged";
}

namespace RealLifeOverhaul
{
    internal static class CompanionGame
    {
        public static bool Available(GameInstance game) => game != null && game.Available;
    }
}

internal static partial class Program
{
    private static void CompanionOwnershipChecks()
    {
        var game = new GameInstance();
        int calls = 0;
        CompanionBridges.Ownership = (operation, targets) =>
        {
            calls++;
            return CompanionOwnership.Decision("allowed", "rlo", operation, null);
        };
        var request = new JObject { ["schemaVersion"] = 1, ["operation"] = "rent", ["phase"] = "review",
            ["targets"] = new JArray(new JObject { ["street"] = "ba:street_test", ["number"] = 1 }) };
        Check(JObject.Parse(CompanionOwnership.Query(game, request.ToString()))["decision"].ToString() == "allowed", "ownership: complete target reaches pure owner");
        Check(calls == 1 && game.Money == 123 && game.ModData == "unchanged", "ownership: successful query leaves game money and save data unchanged");
        ((JArray)request["targets"]).Add(new JObject { ["street"] = "bad", ["number"] = -1 });
        Check(JObject.Parse(CompanionOwnership.Query(game, request.ToString()))["decision"].ToString() == "unknown" && calls == 1,
            "ownership: malformed final target prevents every owner call");
        foreach (string malformed in new[] { "{}", "{\"schemaVersion\":1,\"schemaVersion\":1}", request + "{}", new string(' ', 262145) })
        {
            CompanionOwnership.Query(game, malformed);
        }
        Check(calls == 1, "ownership: duplicate, trailing, missing and oversized JSON never reaches owner");
        ((JArray)request["targets"]).RemoveAt(1);
        CompanionBridges.Ownership = (operation, targets) => throw new InvalidOperationException("synthetic owner failure");
        Check(JObject.Parse(CompanionOwnership.Query(game, request.ToString()))["decision"].ToString() == "unknown", "ownership: failed owner is unknown");
        Check(game.Money == 123 && game.ModData == "unchanged", "ownership: failure path leaves money and save data unchanged");
        CompanionBridges.Ownership = null;
        Check(JObject.Parse(CompanionOwnership.Query(game, request.ToString()))["decision"].ToString() == "unknown", "ownership: missing provider is unknown");
        game.Available = false;
        Check(JObject.Parse(CompanionOwnership.Query(game, request.ToString()))["decision"].ToString() == "unknown", "ownership: unloaded game is unknown");
    }
}
