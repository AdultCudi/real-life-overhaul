using System;
using System.Collections.Generic;
using System.IO;
using RealLifeOverhaul;

// The start days of the player's businesses (Scripts/Core/FoundingRules.cs, FoundingStore.cs, FoundingGame.cs). The case: a player's "Years since
// your first business" on the Going public page read 0.3 years of the 2.0 asked, though their oldest business, a software studio, was a year and
// a quarter old and the second, an AI lab, was the one counted. The game writes a business's start day (creationDay) when it is started or taken
// over; the moving company's move (BizManTransfer) copies the name, type and staff to the other building, shuts the old one down
// (BusinessHelper.ShutdownBusiness sets its day to -1) and does not copy the day, so the business is left with the day of its new building: -1 for
// one rented empty, which the page leaves out as unknown, and the next oldest business then decided the years. Reproduced here as the game does
// it: a business with a day, moved, its old building at -1 and its new one at -1 or at the day of the business it replaced.
internal static partial class Program
{
    private const string FoundA = "Studio", FoundB = "Lab";
    private const string FoundP1 = "main~1", FoundP2 = "main~2", FoundP3 = "main~3", FoundP4 = "main~4";

    private static void Founding()
    {
        FoundingMoved();
        FoundingUnknown();
        FoundingBusinesses();
        FoundingEstimate();
        FoundingSaveLine();
        FoundingSources();
    }

    private static FoundingSeen FoundSeen(string place, string name, int created) => new FoundingSeen { Place = place, Name = name, Created = created };

    // A business with a day of 40 and another of 600; the older is moved to an empty building (day -1), then the sample runs.
    private static void FoundingMoved()
    {
        var file = new FoundingFile();
        var now = new List<FoundingSeen> { FoundSeen(FoundP1, FoundA, 40), FoundSeen(FoundP2, FoundB, 600) };
        FoundingRules.Sample(file, now, 650, null);
        Check(FoundingRules.DayOf(file, FoundP1, FoundA, 40) == 40 && FoundingRules.DayOf(file, FoundP2, FoundB, 600) == 600, "founding: each business keeps the day the game gave it");

        // the move: the studio is now at 3 with the day -1; building 1 is shut (no business there)
        now = new List<FoundingSeen> { FoundSeen(FoundP3, FoundA, -1), FoundSeen(FoundP2, FoundB, 600) };
        Check(FoundingRules.DayOf(file, FoundP3, FoundA, -1) == 0, "founding: before the sample the moved business has no day, as the page saw it");
        int changed = FoundingRules.Sample(file, now, 651, null);
        Check(changed == 1 && FoundingRules.DayOf(file, FoundP3, FoundA, -1) == 40, "founding: a business moved to an empty building keeps its day (" + FoundingRules.DayOf(file, FoundP3, FoundA, -1) + ")");
        Check(file.At(FoundP1, FoundA) == null && file.Records.Count == 2, "founding: the record follows the business, not left behind (" + file.Records.Count + " kept)");

        // the page: the earliest of the days of the businesses running, as ListingLinks.Facts asks
        var days = new List<int> { FoundingRules.DayOf(file, FoundP3, FoundA, -1), FoundingRules.DayOf(file, FoundP2, FoundB, 600) };
        Check(RealLifeOverhaul.Markets.ListingBook.EarliestDay(days) == 40, "founding: the page counts the years from the older business that was moved, not the next oldest (day " + RealLifeOverhaul.Markets.ListingBook.EarliestDay(days) + ")");

        // the same with the new building holding the day of a business the move replaced: the kept day is not given up for it
        now = new List<FoundingSeen> { FoundSeen(FoundP4, FoundA, 500), FoundSeen(FoundP2, FoundB, 600) };
        FoundingRules.Sample(file, now, 652, null);
        Check(FoundingRules.DayOf(file, FoundP4, FoundA, 500) == 40, "founding: a building that held another business's day (500) does not make the moved business newer");
        FoundingRules.Sample(file, now, 653, null);
        Check(FoundingRules.DayOf(file, FoundP4, FoundA, 500) == 40, "founding: nor does the next sample");

        // two moves in a row, and a swap of two buildings between two businesses
        now = new List<FoundingSeen> { FoundSeen(FoundP1, FoundA, -1), FoundSeen(FoundP2, FoundB, 600) };
        FoundingRules.Sample(file, now, 654, null);
        Check(FoundingRules.DayOf(file, FoundP1, FoundA, -1) == 40, "founding: a second move keeps the day too");
        now = new List<FoundingSeen> { FoundSeen(FoundP1, FoundB, -1), FoundSeen(FoundP2, FoundA, -1) };
        FoundingRules.Sample(file, now, 655, null);
        Check(FoundingRules.DayOf(file, FoundP2, FoundA, -1) == 40 && FoundingRules.DayOf(file, FoundP1, FoundB, -1) == 600, "founding: two businesses swapping buildings each keep their own day");

        // the failing case, put on purpose: with no record the moved business is unknown, which is what the page showed
        var bare = new FoundingFile();
        Check(FoundingRules.DayOf(bare, FoundP3, FoundA, -1) == 0 && FoundingRules.DayOf(null, FoundP3, FoundA, -1) == 0, "founding: with no record a business with no day is unknown, never day 0 or a day of the page's");
    }

    // A business with no day of the game's and no record stays unknown; it does not count as day 0 or as the day it was first seen.
    private static void FoundingUnknown()
    {
        var file = new FoundingFile();
        var now = new List<FoundingSeen> { FoundSeen(FoundP1, FoundA, -1), FoundSeen(FoundP2, FoundB, 0) };
        int asked = 0;
        FoundingRules.Sample(file, now, 700, (place, name) => { asked++; return 0; });
        Check(file.Records.Count == 0 && asked == 2, "founding: a business with no day of the game's and no estimate is not given one (" + file.Records.Count + " kept, " + asked + " asked)");
        Check(FoundingRules.DayOf(file, FoundP1, FoundA, -1) == 0 && FoundingRules.DayOf(file, FoundP2, FoundB, 0) == 0, "founding: an unknown business reads as unknown, not day 0");
        var days = new List<int> { FoundingRules.DayOf(file, FoundP1, FoundA, -1), FoundingRules.DayOf(file, FoundP2, FoundB, 0) };
        Check(RealLifeOverhaul.Markets.ListingBook.EarliestDay(days) == 0, "founding: the page counts no years from businesses of unknown day");
        // a name that is not there changes nothing, and a null list is harmless
        Check(FoundingRules.Sample(null, now, 700, null) == 0 && FoundingRules.Sample(file, null, 700, null) == 0, "founding: no file or no list is harmless");
        // an estimate is only taken when it is a day that has been (not in the future, not 0)
        FoundingRules.Sample(file, now, 700, (place, name) => 701);
        Check(file.Records.Count == 0, "founding: an estimate from the future is not kept");
    }

    // Records follow the businesses over days: seen, not seen for a while, gone, same name started again, the cap.
    private static void FoundingBusinesses()
    {
        var file = new FoundingFile();
        var now = new List<FoundingSeen> { FoundSeen(FoundP1, FoundA, 10) };
        FoundingRules.Sample(file, now, 20, null);
        // the business is not seen for a few days (its building is not loaded), then is again: the record stays
        FoundingRules.Sample(file, new List<FoundingSeen>(), 22, null);
        Check(file.Records.Count == 1, "founding: a business not seen for two days keeps its record");
        FoundingRules.Sample(file, now, 23, null);
        Check(file.Records.Count == 1 && file.Records[0].Seen == 23 && file.Records[0].Day == 10, "founding: seen again, the record is the same");
        // it closes: the record goes after the grace
        FoundingRules.Sample(file, new List<FoundingSeen>(), 23 + FoundingRules.Grace + 1, null);
        Check(file.Records.Count == 0, "founding: a business gone for good loses its record after " + FoundingRules.Grace + " days");
        // two businesses of one name: only the one that moved takes the record of the building it left
        file = new FoundingFile();
        FoundingRules.Sample(file, new List<FoundingSeen> { FoundSeen(FoundP1, FoundA, 10), FoundSeen(FoundP2, FoundA, 30) }, 40, null);
        FoundingRules.Sample(file, new List<FoundingSeen> { FoundSeen(FoundP3, FoundA, -1), FoundSeen(FoundP2, FoundA, 30) }, 41, null);
        Check(FoundingRules.DayOf(file, FoundP3, FoundA, -1) == 10 && FoundingRules.DayOf(file, FoundP2, FoundA, 30) == 30 && file.Records.Count == 2,
            "founding: of two businesses with one name the one moved takes its own building's record (" + FoundingRules.DayOf(file, FoundP3, FoundA, -1) + ")");
        // a new business starts at a building where another was kept: the game's day is the new one's
        file = new FoundingFile();
        FoundingRules.Sample(file, new List<FoundingSeen> { FoundSeen(FoundP1, FoundA, 10) }, 40, null);
        FoundingRules.Sample(file, new List<FoundingSeen> { FoundSeen(FoundP1, FoundB, 45) }, 46, null);
        Check(FoundingRules.DayOf(file, FoundP1, FoundB, 45) == 45, "founding: a different business at the same building counts from its own start (" + FoundingRules.DayOf(file, FoundP1, FoundB, 45) + ")");
        // the cap: more than the most are cut, the longest unseen first
        file = new FoundingFile();
        var many = new List<FoundingSeen>();
        for (int i = 0; i < FoundingRules.RecordsMost + 10; i++)
        {
            many.Add(FoundSeen("street~" + i, "Business " + i, 1 + i));
        }
        FoundingRules.Sample(file, many, 500, null);
        Check(file.Records.Count == FoundingRules.RecordsMost, "founding: no more than " + FoundingRules.RecordsMost + " records are kept (" + file.Records.Count + ")");
        // determinism: the same sequence gives the same records
        var a = new FoundingFile();
        var b = new FoundingFile();
        for (int pass = 0; pass < 2; pass++)
        {
            FoundingFile f = pass == 0 ? a : b;
            FoundingRules.Sample(f, new List<FoundingSeen> { FoundSeen(FoundP1, FoundA, 40), FoundSeen(FoundP2, FoundB, 600) }, 650, null);
            FoundingRules.Sample(f, new List<FoundingSeen> { FoundSeen(FoundP3, FoundA, -1), FoundSeen(FoundP2, FoundB, 600) }, 651, null);
        }
        Check(FoundingStore.Write(a) == FoundingStore.Write(b), "founding: the same days give the same records on every machine");
        // the cost: a pass over sixty-four records and sixty-four businesses
        var watch = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < 200; i++)
        {
            FoundingRules.Sample(file, many, 500 + i / 24, null);
        }
        double each = watch.Elapsed.TotalMilliseconds / 200;
        Check(each < 2.0, "founding: a sample of " + many.Count + " businesses against " + file.Records.Count + " records costs " + each.ToString("0.000") + " ms (under 2)");
    }

    // A business moved before the days were kept takes the estimate once; the estimate is lowered by the game's own day if it ever gives an earlier one.
    private static void FoundingEstimate()
    {
        var file = new FoundingFile();
        var now = new List<FoundingSeen> { FoundSeen(FoundP3, FoundA, -1), FoundSeen(FoundP2, FoundB, 600) };
        int asked = 0;
        Func<string, string, int> estimate = (place, name) => { asked++; return place == FoundP3 && name == FoundA ? 300 : 0; };
        FoundingRules.Sample(file, now, 700, estimate);
        Check(asked == 1 && FoundingRules.DayOf(file, FoundP3, FoundA, -1) == 300 && file.At(FoundP3, FoundA).Guess, "founding: a business moved before the days were kept takes the estimate, asked once (" + asked + ")");
        FoundingRules.Sample(file, now, 701, estimate);
        Check(asked == 1, "founding: the estimate is not asked for again once kept");
        Check(FoundingRules.DayOf(file, FoundP3, FoundA, 120) == 120, "founding: an estimate gives way to an earlier day of the game's");
        var days = new List<int> { FoundingRules.DayOf(file, FoundP3, FoundA, -1), FoundingRules.DayOf(file, FoundP2, FoundB, 600) };
        Check(RealLifeOverhaul.Markets.ListingBook.EarliestDay(days) == 300, "founding: the page counts the years from the estimate, which beats the next oldest business");
        // a kept day is not an estimate: the game's later day does not move it
        var kept = new FoundingFile();
        FoundingRules.Sample(kept, new List<FoundingSeen> { FoundSeen(FoundP1, FoundA, 40) }, 100, null);
        Check(!kept.At(FoundP1, FoundA).Guess && FoundingRules.DayOf(kept, FoundP1, FoundA, 500) == 40, "founding: a day the game gave is kept over a later one it reads at the same building");
    }

    private static void FoundingSaveLine()
    {
        // a name the line cannot hold is written as the line holds it, and read back the same by the sample
        string clean = RealLifeOverhaul.Tech.TechWriter.Clean("Joe's | Cafe; ~");
        var cleaned = new FoundingFile();
        FoundingRules.Sample(cleaned, new List<FoundingSeen> { FoundSeen("Main%20Street~1", clean, 40), FoundSeen(FoundP3, FoundA, -1) }, 700, (p, n) => 300);
        string line = FoundingStore.Write(cleaned);
        FoundingFile back = FoundingStore.Read(line, out int version);
        Check(version == FoundingStore.Version && back.Records.Count == 2 && FoundingStore.Write(back) == line, "founding: the save line reads back as it was written (" + line + ")");
        Check(FoundingRules.DayOf(back, "Main%20Street~1", clean, -1) == 40 && FoundingRules.DayOf(back, FoundP3, FoundA, -1) == 300 && back.At(FoundP3, FoundA).Guess, "founding: after a save and a load the days are the same and an estimate is still one");
        Check(FoundingStore.Read("v=1", out _).Records.Count == 0, "founding: a line with no records reads as none");
        Check(FoundingStore.Read("v=2~r:main%7E1|Studio|40|50|0|extra;main%7E2|Lab|0|50|0", out _).Records.Count == 1, "founding: a later version's extra field is read as nothing, and a record with no day is left out");
        bool threw = false;
        try
        {
            FoundingStore.Read("garbage", out _);
        }
        catch (FormatException)
        {
            threw = true;
        }
        Check(threw, "founding: a line that is not one of the mod's is refused, so the save keeps it as it was read");
    }

    // The features that count a business's age ask Core/FoundingGame, and none reads creationDay itself; the watch is started with the city; no handler on the game's day.
    private static void FoundingSources()
    {
        string scripts = System.IO.Path.Combine(LocalesFolder(), @"..\Scripts");
        string Read(string rel) => File.ReadAllText(System.IO.Path.Combine(scripts, rel));
        string listing = Read(@"Wiring\ListingLinks.cs");
        Check(listing.Contains("FoundingGame.Of(reg)") && !listing.Contains("creationDay") && !listing.Contains("ServicesGame.Created"), "founding: the Going public page counts the years from the day kept, not from the game's");
        string ventures = Read(@"Economy\Ventures\VenturesGame.cs");
        Check(ventures.Contains("FoundingGame.Of(r)") && !ventures.Contains("Mathf.Max(1, r.creationDay)"), "founding: an investor's view of a business's age is from the day kept");
        Check(Read(@"Social\Flare\FlareGame.cs").Contains("FoundingGame.Of(all[i])") && Read(@"Wiring\Wiring.TextsCity.cs").Contains("FoundingGame.Of(reg)"), "founding: Flare's opening day and the paper's newest business are from the day kept");
        string watch = Read(@"Core\FoundingWatch.cs");
        string game = Read(@"Core\FoundingGame.cs");
        Check(Read(@"Core\RealLifeOverhaulMod.cs").Contains("Guard.Add<FoundingWatch>(_root)") && !watch.Contains("onNewDay") && !watch.Contains("Update(") && watch.Contains("Guard.Routine(Tick())"),
            "founding: the watch starts with the city, on a timer of its own, with no handler on the game's day and no Update");
        Check(game.Contains("GoingPublic.Mine(reg)") && game.Contains("GoingPublic.IsGuest") && game.Contains("SaveLines.IsKept(") && game.Contains("Meta.SaveTools.Dormant") && !game.Contains("creationDay ="),
            "founding: only the player's own businesses are read, a guest's left as they are, an unreadable line kept, the game's fields never written");
        Check(Read(@"Wiring\Wiring.cs").Contains("FoundingLinks.Connect") && Read(@"Wiring\Wiring.cs").Contains("FoundingLinks.Disconnect"), "founding: the estimate is wired and unwired with the city");
    }
}
