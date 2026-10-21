using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using RealLifeOverhaul.Economy;

// A building sold that comes back as the player's (Scripts/Economy/Estate/EstateRules.Sold.cs, EstateGame.Sold.cs). A
// player's report (18 and 20 Oct 2026): an underground car park another mod sells, sold to a competitor for 3,000,000,
// was the player's again and on the market again each time the save loaded, and sold again for the same price; their
// log has the property business dropping it from its books on the sale, and its own books written in that save without
// it, yet the game's list of the player's buildings has it again as the save loads. The rule: an ownership from before
// the sale with no purchase since is the one sold, and goes back to its buyer; a purchase on the sale's day or after is
// the player's. Then the sales remembered through the save, and over the sources when the look is made (as a save is
// read before the buildings are, each game hour before the game's daily run, each morning), that a sale is remembered
// only with the game's books showing it, that nothing moves money, and that nothing runs in a Going Public session.
internal static partial class Program
{
    private static void SoldBack()
    {
        SoldBackRule();
        SoldBackMemory();
        SoldBackSave();
        SoldBackSources();
        SoldBackSpeed();
    }

    private static void SoldBackRule()
    {
        // The report: bought on day 150, sold on day 182, back at the load on day 182 with its old purchase day, no purchase since.
        Check(EstateRules.SoldBack(182, 150, -1), "sold back: the car park sold on day 182 and the player's again with its purchase day 150 and no purchase since is the ownership that was sold");
        Check(EstateRules.SoldBack(183, 182, -1) && EstateRules.SoldBack(183, 0, -1), "sold back: so is one bought the day before the sale, or on the first day");
        Check(!EstateRules.SoldBack(182, 182, -1) && !EstateRules.SoldBack(182, 190, -1),
            "sold back: one bought on the sale's day or after is a purchase of the player's (the game's purchase writes the day it is made)");
        Check(!EstateRules.SoldBack(182, 150, 182) && !EstateRules.SoldBack(182, 150, 186),
            "sold back: one with a purchase of it in the game's books since the sale is the player's, whatever day it says it was bought");
        Check(EstateRules.SoldBack(182, 150, 170), "sold back: a purchase before the sale is not one since it");
        Check(!EstateRules.SoldBack(0, -1, -1) && !EstateRules.SoldBack(-5, -9, -1), "sold back: with no sale day nothing is taken");
    }

    private static void SoldBackMemory()
    {
        var sold = new List<SoldBuilding>();
        SoldBuilding a = EstateRules.Remember(sold, "ba%3Astreet_parking~3", 182, 150, "rival_7");
        Check(a != null && sold.Count == 1 && a.SoldDay == 182 && a.BoughtDay == 150 && a.Rival == "rival_7" && !a.Told, "sold back: a sale is remembered with its day, the ownership's purchase day and the buyer");
        a.Told = true;
        SoldBuilding again = EstateRules.Remember(sold, "ba%3Astreet_parking~3", 182, 150, null);
        Check(ReferenceEquals(again, a) && sold.Count == 1 && a.Told && a.Rival == "rival_7", "sold back: the same sale seen again is one record, the player told once, the buyer kept");
        EstateRules.Remember(sold, "ba%3Astreet_parking~3", 200, 190, "rival_9");
        Check(sold.Count == 1 && a.SoldDay == 200 && a.BoughtDay == 190 && !a.Told && a.Rival == "rival_9", "sold back: a later sale of it (bought again and sold again) replaces the first, and is told about");
        Check(EstateRules.Remember(sold, null, 5, 1, null) == null && EstateRules.Remember(sold, string.Empty, 5, 1, null) == null && EstateRules.Remember(sold, "A~1", 0, 0, null) == null
              && EstateRules.Remember(null, "A~1", 5, 1, null) == null && sold.Count == 1, "sold back: a building with no place, or a sale with no day, is not remembered");
        var many = new List<SoldBuilding>();
        for (int i = 0; i < EstateCatalogue.SoldMost + 10; i++)
        {
            EstateRules.Remember(many, "Street~" + i, 1000 - i * 3 + (i % 2) * 50, 1, null);   // days out of order
        }
        int least = int.MaxValue, kept = 0;
        for (int i = 0; i < many.Count; i++)
        {
            least = Math.Min(least, many[i].SoldDay);
        }
        for (int i = 0; i < EstateCatalogue.SoldMost + 10; i++)
        {
            int day = 1000 - i * 3 + (i % 2) * 50;
            kept += day >= least ? 1 : 0;
        }
        Check(many.Count == EstateCatalogue.SoldMost && kept == EstateCatalogue.SoldMost, "sold back: at most " + EstateCatalogue.SoldMost + " sales are kept, the newest, whatever order they came in");
    }

    private static void SoldBackSave()
    {
        EstateState s = EstateSample();
        s.Sold.Add(new SoldBuilding { Place = "ba%3Astreet_parking~3", SoldDay = 182, BoughtDay = 150, Rival = "specialistrivals:adrianvoss", Told = true });
        s.Sold.Add(new SoldBuilding { Place = "High%20Street~12", SoldDay = 90, BoughtDay = 0 });
        string line = EstateStore.Write(s);
        int version;
        EstateState back = EstateStore.Read(line, out version);
        SoldBuilding b = back != null ? back.SoldOf("ba%3Astreet_parking~3") : null;
        Check(back != null && back.Sold.Count == 2 && b != null && b.SoldDay == 182 && b.BoughtDay == 150 && b.Told && b.Rival == "specialistrivals:adrianvoss"
              && back.SoldOf("High%20Street~12") != null && back.SoldOf("High%20Street~12").Rival == string.Empty && !back.SoldOf("High%20Street~12").Told && EstateStore.Write(back) == line,
            "sold back: the sales remembered go through the save and back, buyer and told with them, and write the same line again");
        string before = line.Substring(0, line.IndexOf("^s:", StringComparison.Ordinal)).Replace("v=" + EstateStore.Version + "^", "v=2^");
        EstateState old = EstateStore.Read(before, out version);
        Check(old != null && version == 2 && old.Sold.Count == 0 && old.Holdings.Count == s.Holdings.Count, "sold back: a save from before (version 2, no sales part) loads with none remembered and everything else as it was");
        s.Sold[0].Rival = "odd|rival;with^dividers";
        EstateState odd = EstateStore.Read(EstateStore.Write(s), out version);
        Check(odd != null && odd.Sold.Count == 2 && odd.Holdings.Count == s.Holdings.Count && odd.SoldOf("ba%3Astreet_parking~3").Rival == "odd rival with dividers",
            "sold back: a buyer's name with the line's dividers in it cannot break the line");
        EstateState wild = EstateStore.Read("v=3^h:1^s:A~1|0|5|1|x;B~2|-4|5|0|x;C~3|9|-2|7|y;C~3|10|1|0|z;D~4|5", out version);
        Check(wild != null && wild.Sold.Count == 1 && wild.Sold[0].Place == "C~3" && wild.Sold[0].BoughtDay == 0 && wild.Sold[0].Told && wild.Sold[0].Rival == "y",
            "sold back: a record with no sale day or too few fields is dropped, a building is kept once, a figure out of bounds put inside them");
    }

    private static void SoldBackSources()
    {
        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        string Read(string file) => File.Exists(System.IO.Path.Combine(scripts, file)) ? File.ReadAllText(System.IO.Path.Combine(scripts, file)) : string.Empty;
        string director = Read(@"Economy\Estate\EstateDirector.cs"), property = Read(@"Economy\Estate\EstateDirector.Property.cs"), game = Read(@"Economy\Estate\EstateGame.cs"),
               sold = Read(@"Economy\Estate\EstateGame.Sold.cs");

        int start = director.IndexOf("private bool StartUp()", StringComparison.Ordinal);
        int first = start >= 0 ? director.IndexOf("SoldBack(true);", start, StringComparison.Ordinal) : -1;
        int read = start >= 0 ? director.IndexOf("EstateGame.ReadHoldings(_state, out added, out dropped);", start, StringComparison.Ordinal) : -1;
        Check(first > start && first < read, "sold back: as a save is read, a building sold and put back is handed back before the buildings are read as the player's (and the game's books searched for sales from before)");
        int hour = director.IndexOf("private void OnHour()", StringComparison.Ordinal);
        int day = director.IndexOf("private void OnDay()", StringComparison.Ordinal);
        int inHour = hour >= 0 ? director.IndexOf("Part(EstatePart.Property, \"a sold building\", _soldBack);", hour, StringComparison.Ordinal) : -1;
        Check(inHour > hour && inHour < day && director.Contains("_soldBack = () => SoldBack(false);"),
            "sold back: each game hour, which the game runs first in its new day, before its daily run could sell it again; the look made once, not each hour");
        int morning = property.IndexOf("private void PropertyDay(int day)", StringComparison.Ordinal);
        int dayLook = morning >= 0 ? property.IndexOf("SoldBack(false);", morning, StringComparison.Ordinal) : -1;
        int dayRead = morning >= 0 ? property.IndexOf("EstateGame.ReadHoldings(s, out added, out dropped);", morning, StringComparison.Ordinal) : -1;
        Check(dayLook > morning && dayLook < dayRead, "sold back: and each morning before the buildings are read");
        int complete = property.IndexOf("private bool CompleteSale(EstateHolding h, int day)", StringComparison.Ordinal);
        int sell = complete >= 0 ? property.IndexOf("if (!EstateGame.Sell(h, price))", complete, StringComparison.Ordinal) : -1;
        int agency = complete >= 0 ? property.IndexOf("EstateGame.SoldByAgency(_state, h, day);", complete, StringComparison.Ordinal) : -1;
        Check(agency > sell && sell > complete, "sold back: a sale the agency completes is remembered as the game's own are");
        Check(game.Contains("int sold = SoldOn(s, s.Holdings[i]);") && sold.Contains("Transaction sale = Books.Newest(SoldKey, \"address\", name, today - EstateCatalogue.SoldLookBack);")
              && sold.Contains("if (sale == null || sale.timestamp.Day < h.BoughtDay)"),
            "sold back: a building that leaves the player's list is remembered only when the game's books have its sale, made since that ownership's purchase");
        Check(sold.Contains("Books.Newest(BoughtKey, \"address\", name, r.SoldDay)") && sold.Contains("EstateRules.SoldBack(r.SoldDay, e.purchaseDay, again)") && sold.Contains("s.Sold.RemoveAt(i);"),
            "sold back: a building held again is judged by the rule with the game's own purchases since the sale, and one bought again forgotten");
        int inSession = 0, at = 0;
        while ((at = sold.IndexOf("GoingPublic.InSession", at, StringComparison.Ordinal)) >= 0)
        {
            inSession++;
            at++;
        }
        Check(inSession == 4, "sold back: in a Going Public session nothing is remembered or handed back (who owns what is the host's record): " + inSession + " of the 4 ways in stand aside");
        Check(!sold.Contains("ChangeMoneySafe") && !sold.Contains("Crime.Pay") && sold.Contains("Release(save, e, rival);"),
            "sold back: handing a building back moves no money, and leaves it as the game's own sale does (the same Release as a sale)");
        int sellAt = game.IndexOf("public static bool Sell(EstateHolding h, int price)", StringComparison.Ordinal);
        int release = game.IndexOf("private static void Release(GameInstance save, RealEstate e, string rival)", StringComparison.Ordinal);
        string releasing = release >= 0 ? game.Substring(release, Math.Min(2200, game.Length - release)) : string.Empty;
        Check(sellAt >= 0 && game.IndexOf("Release(save, e, null);", sellAt, StringComparison.Ordinal) > sellAt && releasing.Contains("save.realEstate.Remove(e);")
              && releasing.Contains("save.buildingsForSale.RemoveAt(i);") && releasing.Contains("reg.buildingOwnerRivalId = ") && releasing.Contains("RealEstateHelper.AddNoHomeModifierIfNeeded();"),
            "sold back: the release takes it off the player's list and off the market and gives it its owner, as the game's sale does");
        Check(sold.Contains("Hamptons(e.address)"), "sold back: a house in the Hamptons is left to the game, as the property business leaves it");

        foreach (string code in new[] { "en", "pt", "zh-cn" })
        {
            string text = LocaleText(code, "reallife_estate_note_sold_back");
            Check(!string.IsNullOrEmpty(text) && text.Contains("{building}") && text.Length <= 240, "sold back: the notice is in " + code + " with the building's name, at most 240 characters (" + (text ?? string.Empty).Length + ")");
        }
        Check(director.Contains("Tell(NotificationType.Warning, \"reallife_estate_note_sold_back\", Data(\"building\", _handedBack[i]), TabMine, null);") && sold.Contains("if (!r.Told)"),
            "sold back: the player is told once a sale, in the game's own notice, which opens the Property app");
    }

    private static string LocaleText(string code, string key)
    {
        string path = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Locales", code + ".json"));
        if (!File.Exists(path))
        {
            return null;
        }
        using (var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path)))
        {
            return doc.RootElement.TryGetProperty(key, out System.Text.Json.JsonElement value) ? value.GetString() : null;
        }
    }

    private static void SoldBackSpeed()
    {
        var sold = new List<SoldBuilding>();
        for (int i = 0; i < EstateCatalogue.SoldMost; i++)
        {
            EstateRules.Remember(sold, "Street~" + i, 100 + i, 50, null);
        }
        var watch = Stopwatch.StartNew();
        int back = 0;
        for (int round = 0; round < 10000; round++)
        {
            for (int i = 0; i < sold.Count; i++)
            {
                back += EstateRules.SoldBack(sold[i].SoldDay, (round + i) % 200, -1) ? 1 : 0;
            }
            EstateRules.Remember(sold, "Street~" + (round % EstateCatalogue.SoldMost), 100 + round % 50, 50, null);
        }
        watch.Stop();
        Check(watch.Elapsed.TotalMilliseconds < 40.0, "sold back: ten thousand hours' looks over " + EstateCatalogue.SoldMost + " sales in under 40 ms ("
              + watch.Elapsed.TotalMilliseconds.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " ms; " + back + ")");
    }
}
