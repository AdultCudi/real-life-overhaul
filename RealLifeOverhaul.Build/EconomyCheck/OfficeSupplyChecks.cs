using System;
using System.Collections.Generic;
using RealLifeOverhaul;

// The office supply (Scripts/Core/OfficeSupply): the count of the city's office buildings by who has them, written once a
// city load, with the empty ones the mod holds and those left for the player; and the refusal of the mod's buildings
// following each feature's own option at once, the open pages too (SpecialNameBook.LookShown).
internal static partial class Program
{
    private static void OfficeSupply()
    {
        // ---------------------------------------------------------------- who has a building
        Check(OfficeTally.Classify(true, false, null, null, true, false, 0) == LetState.Players && OfficeTally.Classify(false, true, "rival_1", "ba:businesstype_bakery", false, true, 3) == LetState.Players,
            "office supply: the player's building (or another player's in a session) is theirs whatever is in it");
        Check(OfficeTally.Classify(false, false, "rival_1", "ba:businesstype_headquarters", false, true, 0) == LetState.Rival, "office supply: a rival's");
        Check(OfficeTally.Classify(false, false, null, "ba:businesstype_lawfirm", false, true, 0) == LetState.Business, "office supply: a business of the game's own");
        Check(OfficeTally.Classify(false, false, null, "ba:businesstype_empty", true, false, 0) == LetState.ToLet && OfficeTally.Classify(false, false, "", "", true, false, 0) == LetState.ToLet,
            "office supply: empty and to let: nobody's, no business, nothing left in it");
        Check(OfficeTally.Classify(false, false, null, null, false, false, 0) == LetState.Other && OfficeTally.Classify(false, false, null, null, true, true, 0) == LetState.Other
              && OfficeTally.Classify(false, false, null, null, true, false, 2) == LetState.Other, "office supply: not to let, or a layout or furniture left: neither empty nor anybody's business");
        Check(OfficeTally.Feature("techhq_tower_2") == "the tech companies' towers" && OfficeTally.Feature("utilities_office") == "City Utilities" && OfficeTally.Feature("campus") == "the university"
              && OfficeTally.Feature("police_4") == "the police stations" && OfficeTally.Feature(null) == null, "office supply: the features named by their ids");

        // ---------------------------------------------------------------- a city before the towers and after
        // A city like a player's: 85 office buildings, 13 of them empty, and all 13 held by City Utilities,
        // the university and eleven headquarters (the log said 11 of the 12 companies found one). After: three towers.
        var before = new OfficeTally();
        var after = new OfficeTally();
        for (int i = 0; i < 85; i++)
        {
            string district = "ba:neighborhood_" + (i % 5);
            LetState state = i < 13 ? LetState.ToLet : i < 60 ? LetState.Business : i < 80 ? LetState.Rival : i < 83 ? LetState.Players : LetState.Other;
            string held = i == 0 ? "utilities_office" : i == 1 ? "campus" : i < 13 ? "techhq_" + (i - 2) : null;
            before.Add(district, state, held);
            after.Add(district, state, i == 0 ? "utilities_office" : i == 1 ? "campus" : i < 5 ? "techhq_tower_" + (i - 2) : null);
        }
        Check(before.Count == 85 && before.Empty == 13 && before.Held == 13 && before.Free == 0, "office supply: before, 13 empty offices, all 13 held by the mod, none left for the player");
        Check(after.Empty == 13 && after.Held == 5 && after.Free == 8, "office supply: with the towers, 5 held and 8 left for the player of the same 13");
        Check(after.Of(LetState.Business) == 47 && after.Of(LetState.Rival) == 20 && after.Of(LetState.Players) == 3 && after.Of(LetState.Other) == 2, "office supply: the rest by who has them");
        after.District("ba:neighborhood_0", out int empty0, out int free0);
        after.District("ba:neighborhood_4", out int empty4, out int free4);
        Check(empty0 == 3 && free0 == 2 && empty4 == 2 && free4 == 1, "office supply: by district, the empty ones and those left for the player (" + empty0 + "/" + free0 + ", " + empty4 + "/" + free4 + ")");
        string line = after.Line("office");
        Check(line.StartsWith("Office supply: 85 office buildings", StringComparison.Ordinal) && line.Contains("13 stood empty and to let before the mod held any")
              && line.Contains("the mod holds 5 of them (City Utilities 1, the tech companies' towers 3, the university 1)") && line.Contains("8 are left for the player")
              && line.Contains("ba:neighborhood_0 3/2"), "office supply: the log's line says it all: " + line);
        var busy = new OfficeTally();
        busy.Add("d", LetState.Business, "utilities_office");
        Check(busy.Held == 0 && busy.Line("office").Contains("held by the mod with a business or a player in: City Utilities 1"), "office supply: a feature's building with a business in it is not one taken from the empty ones");
        Check(new OfficeTally().Line("retail").StartsWith("Office supply: 0 retail buildings", StringComparison.Ordinal), "office supply: a kind with none says so");

        // ---------------------------------------------------------------- the refusal follows the switch at once
        var book = new SpecialNameBook();
        bool on = true;
        book.Set("techhq_tower_0", "Broadway", 1, SpecialKind.Other, "Harbourgate Tower", "reallife_hq_tower_card_type", () => on, literal: true,
            tenants: new[] { "Codeharbour Software", "Skyvault Systems" });
        book.Set("utilities_office", "Seventh Street", 17, SpecialKind.Civic, "reallife_util_desk_title");
        book.LookShown();
        int stamp = book.Stamp;
        Check(!book.LookShown() && book.Stamp == stamp, "off the market: with no option switched, the book's stamp stays (nothing is made again)");
        on = false;
        Check(book.LookShown() && book.Stamp != stamp && !book.Refuses("Broadway", 1, false, false) && book.Refuses("Seventh Street", 17, false, false),
            "off the market: a feature switched off: its building is to let at once, and the stamp moves so that an open page shows it");
        stamp = book.Stamp;
        on = true;
        Check(book.LookShown() && book.Stamp != stamp && book.Refuses("Broadway", 1, false, false), "off the market: switched on again, the building is off the market again");
        Check(!book.Refuses("Broadway", 1, true, false) && !book.Refuses("Broadway", 1, false, true), "off the market: ...but one the player took meanwhile stays theirs (the tower moves)");
        SpecialEntry tower = book.At("Broadway", 1);
        Check(tower != null && tower.Tenants.Length == 2 && tower.Tenants[1] == "Skyvault Systems" && book.At("Seventh Street", 17).Tenants == null,
            "special names: a tower names its companies by floor; a building of one feature has none");
        on = false;
        Check(book.At("Broadway", 1) == null, "special names: ...and none is given while its feature is off");
    }
}
