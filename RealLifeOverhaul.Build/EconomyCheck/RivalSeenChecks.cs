using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using RealLifeOverhaul;
using RealLifeOverhaul.Rivals;
using RealLifeOverhaul.RivalsSeen;
using RealLifeOverhaul.Ui;

// Rivals seen around the city (Scripts/RivalsSeen, run with --rivalseen): how a business is doing and what its owner can afford from their real figures, the weekly
// mark, the car and its hold, the seeded week (every cap and rate, nobody gone anywhere, nobody in the Hamptons at lunch in town, no Hamptons in a city without them),
// when the feature starts (never before the rivals' own feature has made up its mind) and its seed, the tycoons as the rivals' own file has them, who the world may
// show and when they go (one placing a look, never while spoken to), where by a door they can stand, the greeting's glance, the city's table, the day's sightings
// (bounded, one place line a rival, honest: only where the week really has them and in the tone their figures give, in every language), the hour's posts (nothing
// of somebody gone, three a day with the player's own line among them), the player seen in turn, a greeting worth what the social life's own rule gives and no
// more, the tests taken back, the save, old saves and a newer line, the pages made only when something changed, the costs of the shipped passes, the sources and
// the texts in every language.
internal static partial class Program
{
    private static readonly string[] SeenLanguages = { "en", "pt", "zh-cn", "de", "fr", "es", "ru", "nl", "tr", "pl", "ko" };

    private static void RivalSeen()
    {
        SeenFormChecks();
        SeenMarkChecks();
        SeenCarChecks();
        SeenWeekChecks();
        SeenLookChecks();
        SeenTycoonChecks();
        SeenWorldChecks();
        SeenPutChecks();
        SeenDoorChecks();
        SeenGlanceChecks();
        SeenTableChecks();
        SeenDayChecks();
        SeenHonestChecks();
        SeenHourChecks();
        SeenPlayerChecks();
        SeenGreetChecks();
        SeenTestBackChecks();
        SeenStoreChecks();
        SeenPageChecks();
        SeenCostChecks();
        SeenSourceChecks();
        SeenTextChecks();
    }

    // ---- a small city to be seen in

    private sealed class SeenCity : ISeenCity
    {
        public readonly List<string> Ladder = new List<string>();
        public readonly List<string> Areas = new List<string>();
        public readonly Dictionary<string, List<string>> Eating = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        public int ShutFrom = 99;   // every eating place is shut from this hour on
        public int Asked;

        public int Cars => Ladder.Count;
        public string Car(int index) => index >= 0 && index < Ladder.Count ? Ladder[index] : string.Empty;
        public int Districts => Areas.Count;
        public string District(int index) => index >= 0 && index < Areas.Count ? Areas[index] : string.Empty;

        public bool Eatery(string district, int grade, int pick, int day, int hour, out string name)
        {
            Asked++;
            name = string.Empty;
            if (hour >= ShutFrom || !Eating.TryGetValue(district ?? string.Empty, out List<string> places))
            {
                return false;
            }
            int at = SeenRules.VenueIndex(places.Count, grade, pick);
            name = at >= 0 ? places[at] : string.Empty;
            return at >= 0;
        }
    }

    private static SeenCity SeenTown(int cars = 12)
    {
        var c = new SeenCity();
        c.Ladder.AddRange(SeenLadder(cars));
        foreach (string d in new[] { "ba:neighborhood_garment", "ba:neighborhood_midtown", "ba:neighborhood_murrayhill" })
        {
            c.Areas.Add(d);
            var places = new List<string>();
            for (int i = 0; i < 6; i++)
            {
                places.Add(d.Substring(16) + " eatery " + i);
            }
            c.Eating[d] = places;
        }
        c.Eating["ba:neighborhood_thehamptons"] = new List<string> { "Ocean Crest Cafe" };
        return c;
    }

    private static SeenSubject SeenOwner(string id, long figure, long before, int shops = 3, int shopsBefore = 3, bool hamptons = false, string district = "ba:neighborhood_midtown")
    {
        return new SeenSubject
        {
            Key = "g" + id, Id = id, Name = "Owner " + id, Firm = "Shop of " + id, Figure = figure, Before = before, Shops = shops, ShopsBefore = shopsBefore,
            District = district, Office = "Fifth Avenue#" + id.Length, Hamptons = hamptons
        };
    }

    private static SeenSubject SeenTycoon(int index, long worth, long weekAgo, bool pressed = false, bool gone = false)
    {
        return new SeenSubject
        {
            Key = "t" + index.ToString(CultureInfo.InvariantCulture), Index = index, Name = "Tycoon " + index, Firm = "Holding " + index, Tycoon = true, Figure = worth,
            Before = weekAgo, ShopsBefore = -1, Pressed = pressed, Gone = gone
        };
    }

    // Twenty-two rivals of every sort, as a full city has them: three tycoons and nineteen owners.
    private static List<SeenSubject> SeenCast()
    {
        var all = new List<SeenSubject> { SeenTycoon(0, 52000000, 50000000), SeenTycoon(1, 70000000, 70000000), SeenTycoon(2, 120000000, 125000000) };
        for (int i = 0; i < 19; i++)
        {
            long before = 20000L + i * 30000L;
            long now = before * (i % 3 == 0 ? 120 : i % 3 == 1 ? 100 : 80) / 100;
            all.Add(SeenOwner("r" + i.ToString("00", CultureInfo.InvariantCulture), now, before, 2 + i % 4, 2 + i % 4, i % 5 == 0,
                i % 4 == 3 ? string.Empty : "ba:neighborhood_" + (i % 2 == 0 ? "midtown" : "garment")));
        }
        return all;
    }

    // A figure that moves by num/den every week from day 1, as a rival's real figures would: the week's mark (days 1, 8, 15 and on) reads that week's figure.
    private static long SeenWeekFigure(long start, int num, int den, int day)
    {
        long figure = start;
        for (int week = (day - 1) / SeenRules.WeekDays; week > 0; week--)
        {
            figure = figure * num / den;
        }
        return figure;
    }

    // The words of a locale as the line builder asks for them: a key's text, and a car or a district named once by the last part of its id, as the game's own
    // words are made once.
    private sealed class SeenWords : ISeenWords
    {
        private readonly Dictionary<string, string> _words;
        private readonly Dictionary<string, string> _cars = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _districts = new Dictionary<string, string>(StringComparer.Ordinal);

        public SeenWords(Dictionary<string, string> words)
        {
            _words = words ?? new Dictionary<string, string>();
        }

        public string T(string key) => key != null && _words.TryGetValue(key, out string text) ? text : key ?? string.Empty;

        public string Car(string type) => Named(_cars, "Motor ", type);

        public string District(string key) => Named(_districts, "Quarter ", key);

        private static string Named(Dictionary<string, string> made, string kind, string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return string.Empty;
            }
            if (!made.TryGetValue(id, out string name))
            {
                name = kind + id.Substring(id.LastIndexOf('_') + 1);
                made[id] = name;
            }
            return name;
        }
    }

    // A business of the city as the game's list has it, for the table: the building the table holds is this object, never looked into.
    private sealed class SeenPlace
    {
        public string Type, District, Street, Name;
        public int Number, Rent;
        public float X, Z;
    }

    // Each rival's own premises at an address of its own: the owners on Office Row, the tycoons on Tycoon Plaza, the offices standing at (1000 + i, 50).
    private static void SeenOffices(List<SeenSubject> cast)
    {
        for (int i = 0; i < cast.Count; i++)
        {
            cast[i].Office = (cast[i].Tycoon ? "Tycoon Plaza#" : "Office Row#") + (10 + i).ToString(CultureInfo.InvariantCulture);
        }
    }

    // Six hundred established businesses at addresses of their own on twelve streets of five districts, every tenth a restaurant and every tenth a coffee shop,
    // and the cast's premises among them.
    private static List<SeenPlace> SeenStreets(List<SeenSubject> cast)
    {
        string[] districts = { "ba:neighborhood_midtown", "ba:neighborhood_garment", "ba:neighborhood_murrayhill", "ba:neighborhood_thehamptons", "ba:neighborhood_hellskitchen" };
        var all = new List<SeenPlace>(600 + cast.Count);
        for (int i = 0; i < 600; i++)
        {
            string type = i % 10 == 0 ? "ba:businesstype_restaurant" : i % 10 == 5 ? "ba:businesstype_coffeeshop" : i % 3 == 0 ? "ba:businesstype_office" : "ba:businesstype_giftshop";
            all.Add(new SeenPlace
            {
                Type = type, District = districts[i / 5 % districts.Length], Street = "Street " + (char)('A' + i % 12), Number = i + 1, Name = "Place " + i,
                Rent = 100 + i * 37 % 900, X = i % 30 * 20f, Z = i / 30 * 20f
            });
        }
        for (int i = 0; i < cast.Count; i++)
        {
            int cut = cast[i].Office.LastIndexOf('#');
            all.Insert(i * 25 % all.Count, new SeenPlace
            {
                Type = "ba:businesstype_office", District = "ba:neighborhood_midtown", Street = cast[i].Office.Substring(0, cut),
                Number = int.Parse(cast[i].Office.Substring(cut + 1), CultureInfo.InvariantCulture), Name = cast[i].Firm, Rent = 500, X = 1000f + i, Z = 50f
            });
        }
        return all;
    }

    // The day's table as SeenGame.Refresh feeds it: the cars, then each established business, kept only as an eating place or at one of the day's offices.
    private static void SeenFeed(SeenTable table, List<SeenSubject> subjects, List<SeenPlace> city, List<string> cars)
    {
        table.Begin(subjects, "ba:neighborhood_thehamptons");
        for (int i = 0; i < cars.Count; i++)
        {
            table.AddCar(cars[i]);
        }
        for (int i = 0; i < city.Count; i++)
        {
            SeenPlace p = city[i];
            bool eats = SeenTable.Eats(p.Type);
            int office = table.OfficeAt(p.Street, p.Number);
            if (!eats && office < 0)
            {
                continue;
            }
            table.SetOffice(office, p, p.X, p.Z);
            if (eats)
            {
                table.AddEating(p.District, p.Street, p.Number, p.Name, p.Rent, p.X, p.Z, p);
            }
        }
        table.Done();
    }

    private static List<string> SeenLadder(int cars = 12)
    {
        var ladder = new List<string>(cars);
        for (int i = 0; i < cars; i++)
        {
            ladder.Add("ba:vehicletype_car" + i.ToString("00", CultureInfo.InvariantCulture));
        }
        return ladder;
    }

    // The least of three rounds of a pass, each of `reps` runs after one to warm it: milliseconds and bytes a run.
    private static void SeenCost(Action run, int reps, out double ms, out long bytes)
    {
        ms = double.MaxValue;
        bytes = long.MaxValue;
        run();
        for (int round = 0; round < 3; round++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            var watch = Stopwatch.StartNew();
            for (int i = 0; i < reps; i++)
            {
                run();
            }
            watch.Stop();
            ms = Math.Min(ms, watch.Elapsed.TotalMilliseconds / reps);
            bytes = Math.Min(bytes, (GC.GetAllocatedBytesForCurrentThread() - before) / reps);
        }
    }

    private static string SeenMs(double ms) => ms.ToString("0.0000", CultureInfo.InvariantCulture);

    // ---- how the business is doing, and the means

    private static void SeenFormChecks()
    {
        Check(SeenRules.Trend(108, 100) == 80 && SeenRules.Trend(92, 100) == -80 && SeenRules.Trend(100, 0) == 0 && SeenRules.Trend(0, 100) == -1000 && SeenRules.Trend(100000, 1) == 10000,
            "rivals seen: the week's change is per mille of what it was, nothing with nothing before, held between -1000 and 10000");
        Check(SeenRules.Form(false, 108000, 100000, 3, 3, false) == SeenForm.Thriving && SeenRules.Form(false, 107900, 100000, 3, 3, false) == SeenForm.Steady
              && SeenCatalogue.OwnerThrive == 80, "rivals seen: an owner thrives from a week of takings 8% up, and not a penny sooner");
        Check(SeenRules.Form(false, 92000, 100000, 3, 3, false) == SeenForm.Trouble && SeenRules.Form(false, 92100, 100000, 3, 3, false) == SeenForm.Steady
              && SeenCatalogue.OwnerTrouble == 80, "rivals seen: an owner is in trouble from a week of takings 8% down");
        Check(SeenRules.Form(false, 100000, 100000, 2, 3, false) == SeenForm.Trouble && SeenRules.Form(false, 100000, 100000, 4, 3, false) == SeenForm.Thriving
              && SeenRules.Form(false, 120000, 100000, 2, 3, false) == SeenForm.Thriving && SeenRules.Form(false, 90000, 100000, 4, 3, false) == SeenForm.Trouble,
            "rivals seen: a business lost is trouble and one won is thriving, unless the takings say otherwise by their own bar");
        Check(SeenRules.Form(false, 500000, 0, 3, -1, false) == SeenForm.Steady && SeenRules.Form(true, 90000000, 0, 0, -1, false) == SeenForm.Steady,
            "rivals seen: with no week before to compare with a rival is steady: nothing is guessed");
        Check(SeenRules.Form(true, 100800000, 100000000, 0, -1, false) == SeenForm.Thriving && SeenRules.Form(true, 100700000, 100000000, 0, -1, false) == SeenForm.Steady
              && SeenCatalogue.TycoonThrive == 8, "rivals seen: a tycoon thrives from a worth 8 per mille up in the week");
        Check(SeenRules.Form(true, 99900000, 100000000, 0, -1, false) == SeenForm.Trouble && SeenRules.Form(true, 100000000, 100000000, 0, -1, false) == SeenForm.Steady
              && SeenRules.Form(true, 105000000, 100000000, 0, -1, true) == SeenForm.Trouble && SeenCatalogue.TycoonTrouble == 1,
            "rivals seen: a tycoon is in trouble when their worth fell in the week or a counter of the player's holds them back, and steady on a week that stood still");
        long[] from = SeenCatalogue.OwnerTierFrom;
        Check(from.Length == SeenCatalogue.Tiers && from[1] == 15000 && from[2] == 60000 && from[3] == 200000 && from[4] == 600000,
            "rivals seen: an owner's five tiers of means start at 0, 15,000, 60,000, 200,000 and 600,000 a week");
        Check(SeenRules.Tier(false, 14999, 1000) == 0 && SeenRules.Tier(false, 15000, 1000) == 1 && SeenRules.Tier(false, 59999, 1000) == 1 && SeenRules.Tier(false, 200000, 1000) == 3
              && SeenRules.Tier(false, 600000, 1000) == 4 && SeenRules.Tier(false, 5000000000L, 1000) == 4, "rivals seen: an owner's tier is read from the week's takings");
        Check(SeenRules.Tier(false, 30000, 2000) == 1 && SeenRules.Tier(false, 29999, 2000) == 0 && SeenRules.Tier(false, 7500, 500) == 1 && SeenRules.Tier(false, 15000, 1) == 1,
            "rivals seen: the tiers follow the city's prices, and a price level out of its range is held inside it");
        Check(SeenRules.Tier(true, 20000000, 1000) == 3 && SeenRules.Tier(true, 99999999, 1000) == 3 && SeenRules.Tier(true, 100000000, 1000) == 4 && SeenRules.Tier(true, 0, 1000) == 3,
            "rivals seen: a tycoon is never less than rich, and very rich from 100 million");
    }

    // ---- the weekly mark

    private static void SeenMarkChecks()
    {
        var r = new SeenRecord { Key = "ga" };
        SeenSubject s = SeenOwner("a", 110000, 100000);
        Check(SeenRules.Mark(r, s, 10, 1000) && r.MarkDay == 10 && r.Figure == 110000 && r.Before == 100000 && r.Form == SeenForm.Thriving && r.Tier == 2 && r.Name == "Owner a",
            "rivals seen: the first mark sets the figure against the week before as the source has it");
        s.Figure = 50000;
        Check(!SeenRules.Mark(r, s, 16, 1000) && r.Form == SeenForm.Thriving && r.Figure == 110000, "rivals seen: nothing is read again within the week: a rival's week is one week");
        Check(SeenRules.Mark(r, s, 17, 1000) && r.Before == 110000 && r.Figure == 50000 && r.Form == SeenForm.Trouble && r.Tier == 1,
            "rivals seen: on the seventh day the figure is set against the last mark's and the form and the means are read again");
        s.Figure = 50000;
        Check(SeenRules.Mark(r, s, 3, 1000) && r.MarkDay == 3, "rivals seen: a day before the mark (an older save loaded) marks again and never waits for a day that is past");
        s.Gone = true;
        Check(!SeenRules.Mark(r, s, 400, 1000) && r.MarkDay == 3 && !SeenRules.Mark(null, s, 1, 1000) && !SeenRules.Mark(r, null, 1, 1000),
            "rivals seen: somebody gone is not marked, and nothing marks nothing");
        var t = new SeenRecord { Key = "t0" };
        SeenRules.Mark(t, SeenTycoon(0, 70000000, 70000000), 14, 1000);
        Check(t.Form == SeenForm.Steady && t.Tier == 3, "rivals seen: a tycoon marked on the day of the rich list's own mark, when the week's change is nil, is steady and not in trouble");
        // a year and a month turn: the mark counts days, so nothing happens at either
        var y = new SeenRecord { Key = "gy" };
        SeenSubject ys = SeenOwner("y", 100000, 100000);
        int marks = 0;
        for (int day = 355; day <= 375; day++)
        {
            marks += SeenRules.Mark(y, ys, day, 1000) ? 1 : 0;
        }
        Check(marks == 3 && y.MarkDay == 369, "rivals seen: over a year's and a month's turn a rival is marked every seventh day and no oftener");
    }

    // ---- the car

    private static void SeenCarChecks()
    {
        Check(SeenCatalogue.Rungs == 6 && SeenRules.Rung(0, SeenForm.Trouble) == 0 && SeenRules.Rung(0, SeenForm.Thriving) == 1 && SeenRules.Rung(2, SeenForm.Steady) == 2
              && SeenRules.Rung(4, SeenForm.Thriving) == 5 && SeenRules.Rung(4, SeenForm.Trouble) == 3 && SeenRules.Rung(99, SeenForm.Thriving) == 5 && SeenRules.Rung(-3, SeenForm.Trouble) == 0,
            "rivals seen: the car's rung is the tier of means, one up thriving and one down in trouble, on a ladder of six");
        bool inside = true, rising = true, back = true;
        for (int cars = 1; cars <= 30; cars++)
        {
            int last = -1;
            for (int rung = 0; rung < SeenCatalogue.Rungs; rung++)
            {
                int low = SeenRules.CarIndex(rung, cars, 0), high = SeenRules.CarIndex(rung, cars, 999);
                inside &= low >= 0 && high < cars && low <= high;
                rising &= low >= last;
                back &= cars < SeenCatalogue.Rungs || (SeenRules.RungOf(low, cars) == rung && SeenRules.RungOf(high, cars) == rung);
                last = low;
            }
        }
        Check(inside && rising && back, "rivals seen: every rung has its own stretch of the ladder, dearer rung dearer cars, and a car's rung is read back from its place");
        Check(SeenRules.CarIndex(3, 0, 500) == -1 && SeenRules.RungOf(-1, 12) == -1 && SeenRules.RungOf(4, 0) == -1 && SeenRules.RungOf(99, 12) == 5, "rivals seen: no cars, no car");
        var r = new SeenRecord { Key = "ga", Tier = 2, Form = SeenForm.Steady };
        Check(SeenRules.CarTurn(r, 10) == 2, "rivals seen: a rival with no car on record takes one quietly");
        r.Rung = 2;
        r.Car = "ba:vehicletype_car04";
        r.CarDay = 10;
        r.Form = SeenForm.Thriving;
        Check(SeenRules.CarTurn(r, 23) == 0 && SeenRules.CarTurn(r, 24) == 1 && SeenCatalogue.CarHoldDays == 14,
            "rivals seen: a car is kept fourteen days whatever the figures do, then a dearer one comes");
        r.Form = SeenForm.Trouble;
        Check(SeenRules.CarTurn(r, 24) == -1, "rivals seen: ...or a cheaper one when the business is in trouble");
        r.Form = SeenForm.Steady;
        Check(SeenRules.CarTurn(r, 500) == 0, "rivals seen: the same rung keeps the same car for good");
        // through the day's pass: the first car makes no line, a change makes one that names both cars
        SeenCity city = SeenTown();
        var f = new SeenFile { Seed = 77 };
        var list = new List<SeenSubject> { SeenOwner("a", 100000, 100000) };
        SeenDay.Run(f, list, 1, 1000, city);
        SeenRecord rec = f.Of("ga");
        string first = rec != null ? rec.Car : null;
        Check(rec != null && rec.Rung == 2 && !string.IsNullOrEmpty(first) && !f.Notes.Any(n => n.Kind == SeenNoteKind.NewCar || n.Kind == SeenNoteKind.CarDown),
            "rivals seen: the day's pass gives a rival the car of their rung, and the first car is not news: what they drove before is not known");
        int news = 0;
        for (int day = 2; day <= 20; day++)
        {
            list[0].Figure = day < 9 ? 400000 : 600000;   // a much better week, and a better one again
            SeenDay.Run(f, list, day, 1000, city);
            news += f.Notes.Count(n => n.Day == day && n.Kind == SeenNoteKind.NewCar);
        }
        SeenNote car = f.Notes.FirstOrDefault(n => n.Kind == SeenNoteKind.NewCar);
        Check(news == 1 && car != null && car.Day == 15 && car.Old == first && car.Car == rec.Car && car.Car != first && rec.Rung == 5 && car.Form == SeenForm.Thriving,
            "rivals seen: a rival whose takings rose is seen in a dearer car once, on the day the hold of the old one ends, and the line names both");
        for (int day = 21; day <= 40; day++)
        {
            list[0].Figure = day < 23 ? 20000 : 10000;   // a bad week, and a worse one
            SeenDay.Run(f, list, day, 1000, city);
        }
        SeenNote down = f.Notes.LastOrDefault(n => n.Kind == SeenNoteKind.CarDown);
        Check(down != null && down.Old == car.Car && down.Form == SeenForm.Trouble && rec.Rung == 0 && SeenDay.LineKey(down) == SeenCatalogue.CarDownLine,
            "rivals seen: a rival whose takings fell is seen in a cheaper car, sold down");
        var none = new SeenFile { Seed = 77 };
        SeenDay.Run(none, new List<SeenSubject> { SeenOwner("a", 100000, 100000) }, 1, 1000, new SeenCity());
        Check(none.Of("ga") != null && none.Of("ga").Car.Length == 0 && none.Of("ga").Rung == -1 && none.Notes.Count == 0,
            "rivals seen: a city with no cars and no eating places gives nobody a car and writes nothing");
        // somebody who goes while a change of car is due: their record stays as it was and no line is written
        var left = new SeenFile { Seed = 77 };
        var leaver = new List<SeenSubject> { SeenOwner("a", 100000, 100000) };
        SeenDay.Run(left, leaver, 1, 1000, city);
        for (int day = 2; day <= 40; day++)
        {
            leaver[0].Figure = 400000;      // marked thriving on day 8: a dearer car is due when the hold ends on day 15
            leaver[0].Gone = day > 10;      // ...but they are defeated on day 11
            SeenDay.Run(left, leaver, day, 1000, city);
        }
        Check(left.Of("ga") != null && left.Of("ga").Rung == 2 && left.Of("ga").MarkDay == 8 && !left.Notes.Any(n => n.Day > 10),
            "rivals seen: a rival defeated while a change of car was due keeps the record they had, and nothing more is written about them");
    }

    // ---- the week

    private static void SeenWeekChecks()
    {
        Check(SeenRules.Weekday(1) == 0 && SeenRules.Weekday(6) == SeenRules.Saturday && SeenRules.Weekday(7) == SeenRules.Sunday && SeenRules.Weekday(8) == 0 && SeenRules.Weekday(0) == 6,
            "rivals seen: day 1 is a Monday, as the game counts");
        Check(SeenRules.Who("t0") == 155512425 && SeenRules.Who("gabc") == SeenRules.Who("gabc") && SeenRules.Who("gabc") != SeenRules.Who("gabd")
              && SeenRules.Who(null) == SeenRules.Who(string.Empty),
            "rivals seen: a rival's key is hashed by a fixed rule (never the runtime's own string hash), so every machine draws the same week");
        bool same = true, differ = false, inRange = true;
        for (int i = 0; i < 2000; i++)
        {
            int a = SeenRules.Roll(31, i, i * 7, 3), b = SeenRules.Roll(31, i, i * 7, 3), c = SeenRules.Roll(32, i, i * 7, 3);
            same &= a == b;
            differ |= a != c;
            inRange &= a >= 0 && a <= 999;
        }
        Check(same && differ && inRange, "rivals seen: a draw is 0 to 999, the same for the same seed, rival, day and purpose, another for another seed");
        // every hour of four weeks, for every form, tier and house
        bool goneNowhere = true, officeHours = true, troubleNoDinner = true, lateOnlyTrouble = true, awayNotInTown = true, noCoast = true, sundayHome = true, sundayInland = true;
        bool nightNowhere = true, gradeOk = true;
        int steadyLunch = 0, steadyDays = 0, troubleLunch = 0, troubleDays = 0, thrivingLunch = 0, thrivingDays = 0, steadyDinner = 0, thrivingDinner = 0, steadyBeach = 0;
        int steadySaturdays = 0, aloneOk = 0, alone = 0;
        for (int who = 0; who < 40; who++)
        {
            for (int form = 0; form < 3; form++)
            {
                for (int tier = 0; tier < SeenCatalogue.Tiers; tier++)
                {
                    for (int day = 1; day <= 28; day++)
                    {
                        int weekday = SeenRules.Weekday(day);
                        bool work = weekday < SeenRules.Saturday, house = who % 2 == 0;
                        var f = (SeenForm)form;
                        bool away = f == SeenForm.Thriving && (house || tier >= SeenCatalogue.HamptonsTier);
                        bool lunched = false, dined = false, beached = false;
                        for (int hour = 0; hour < 24; hour++)
                        {
                            SeenWhere w = SeenRules.Where(5, who, f, tier, house, false, day, hour);
                            goneNowhere &= SeenRules.Where(5, who, f, tier, house, true, day, hour).Spot == SeenSpot.Nowhere;
                            nightNowhere &= (hour >= SeenCatalogue.WorkFrom && hour < SeenCatalogue.LateTo) || w.Spot == SeenSpot.Nowhere;
                            if (w.Spot == SeenSpot.Office)
                            {
                                officeHours &= hour >= SeenCatalogue.WorkFrom && hour < SeenCatalogue.WorkTo
                                               && (f != SeenForm.Thriving || hour < SeenCatalogue.ThrivingLeaves || hour < SeenCatalogue.LunchTo)
                                               && (work || (weekday == SeenRules.Saturday && f == SeenForm.Trouble));
                            }
                            troubleNoDinner &= !(f == SeenForm.Trouble && w.Spot == SeenSpot.Dinner);
                            lateOnlyTrouble &= w.Spot != SeenSpot.Late || (f == SeenForm.Trouble && work && hour >= SeenCatalogue.WorkTo && hour < SeenCatalogue.LateTo);
                            bool coast = w.Spot == SeenSpot.Beach || w.Spot == SeenSpot.Hamptons;
                            awayNotInTown &= !(away && !work) || coast || w.Spot == SeenSpot.Nowhere;
                            sundayHome &= weekday != SeenRules.Sunday || away || w.Spot == SeenSpot.Nowhere;
                            SeenWhere inland = SeenRules.Where(5, who, f, tier, house, false, day, hour, false);
                            noCoast &= inland.Spot != SeenSpot.Beach && inland.Spot != SeenSpot.Hamptons;
                            sundayInland &= weekday != SeenRules.Sunday || inland.Spot == SeenSpot.Nowhere;
                            gradeOk &= w.Grade >= 0 && w.Grade < SeenCatalogue.Grades && ((w.Spot != SeenSpot.Lunch && w.Spot != SeenSpot.Dinner) || w.Grade == SeenRules.Grade(tier, f));
                            lunched |= w.Spot == SeenSpot.Lunch;
                            dined |= w.Spot == SeenSpot.Dinner;
                            beached |= w.Spot == SeenSpot.Beach;
                            if (w.Spot == SeenSpot.Lunch && work)
                            {
                                alone++;
                                aloneOk += w.Alone == (f == SeenForm.Trouble) ? 1 : 0;
                            }
                        }
                        if (work && tier == 2)
                        {
                            steadyDays += f == SeenForm.Steady ? 1 : 0;
                            steadyLunch += f == SeenForm.Steady && lunched ? 1 : 0;
                            steadyDinner += f == SeenForm.Steady && dined ? 1 : 0;
                            troubleDays += f == SeenForm.Trouble ? 1 : 0;
                            troubleLunch += f == SeenForm.Trouble && lunched ? 1 : 0;
                            thrivingDays += f == SeenForm.Thriving ? 1 : 0;
                            thrivingLunch += f == SeenForm.Thriving && lunched ? 1 : 0;
                            thrivingDinner += f == SeenForm.Thriving && dined ? 1 : 0;
                        }
                        if (weekday == SeenRules.Saturday && f == SeenForm.Steady && tier == 2)
                        {
                            steadySaturdays++;
                            steadyBeach += beached ? 1 : 0;
                        }
                    }
                }
            }
        }
        Check(goneNowhere, "rivals seen: somebody gone by their source's rules (taken over, defeated) is nowhere at any hour of any day");
        Check(nightNowhere, "rivals seen: nobody is anywhere before 9:00 or from 21:00");
        Check(officeHours, "rivals seen: the office is kept from 9:00 to 18:00 on working days, an hour less by a thriving rival, and on a Saturday only by one in trouble");
        Check(troubleNoDinner && SeenCatalogue.DinnerOut[0] == 0, "rivals seen: a rival in trouble is never out at dinner");
        Check(lateOnlyTrouble, "rivals seen: only a rival in trouble works late, from 18:00 to 21:00 on a working day");
        Check(awayNotInTown, "rivals seen: a thriving rival with a house in the Hamptons or the means for it is there at the weekend, and never at lunch, at dinner or at the office in town");
        Check(sundayHome && sundayInland,
            "rivals seen: on a Sunday only a thriving rival with a house in the Hamptons or the means for it is seen, on the beach there, and in a city without the Hamptons nobody is");
        Check(noCoast, "rivals seen: in a city without the Hamptons nobody is on the beach or away for the weekend");
        Check(gradeOk && alone > 0 && aloneOk == alone, "rivals seen: an eating place's grade is the rule's for their means and form, and only a rival in trouble sits alone");
        Check(thrivingLunch == thrivingDays && SeenCatalogue.LunchOut[2] == 1000, "rivals seen: a thriving rival is out at lunch every working day");
        Check(Math.Abs(steadyLunch * 1000 / steadyDays - SeenCatalogue.LunchOut[1]) < 60 && SeenCatalogue.LunchOut[1] == 600,
            "rivals seen: a steady rival is out at lunch about six working days in ten (" + steadyLunch * 1000 / steadyDays + " per mille)");
        Check(Math.Abs(troubleLunch * 1000 / troubleDays - SeenCatalogue.LunchOut[0]) < 60 && SeenCatalogue.LunchOut[0] == 250,
            "rivals seen: a rival in trouble about one in four (" + troubleLunch * 1000 / troubleDays + " per mille)");
        Check(Math.Abs(steadyDinner * 1000 / steadyDays - SeenCatalogue.DinnerOut[1]) < 60 && Math.Abs(thrivingDinner * 1000 / thrivingDays - SeenCatalogue.DinnerOut[2]) < 60,
            "rivals seen: dinner out about three evenings in ten for a steady rival and seven for a thriving one (" + steadyDinner * 1000 / steadyDays + ", "
            + thrivingDinner * 1000 / thrivingDays + " per mille)");
        Check(Math.Abs(steadyBeach * 1000 / steadySaturdays - SeenCatalogue.SteadyBeach) < 90 && SeenCatalogue.SteadyBeach == 400,
            "rivals seen: a steady rival takes about four Saturdays in ten on the beach (" + steadyBeach * 1000 / steadySaturdays + " per mille)");
        Check(SeenRules.Grade(0, SeenForm.Steady) == 0 && SeenRules.Grade(0, SeenForm.Thriving) == 1 && SeenRules.Grade(1, SeenForm.Trouble) == 0 && SeenRules.Grade(2, SeenForm.Steady) == 1
              && SeenRules.Grade(3, SeenForm.Steady) == 2 && SeenRules.Grade(4, SeenForm.Thriving) == 2 && SeenRules.Grade(3, SeenForm.Trouble) == 1,
            "rivals seen: where they eat fits their means: a step smarter when the business thrives, a step cheaper in trouble");
        bool thirds = true;
        for (int count = 1; count <= 20; count++)
        {
            for (int grade = 0; grade < SeenCatalogue.Grades; grade++)
            {
                int low = SeenRules.VenueIndex(count, grade, 0), high = SeenRules.VenueIndex(count, grade, 999);
                thirds &= low >= 0 && high < count && low <= high && (count < 3 || (low >= grade * count / 3 && high < Math.Max(grade * count / 3 + 1, (grade + 1) * count / 3)));
            }
        }
        Check(thirds && SeenRules.VenueIndex(0, 1, 500) == -1 && SeenRules.VenueIndex(9, 0, 999) == 2 && SeenRules.VenueIndex(9, 2, 0) == 6 && SeenRules.VenueIndex(9, 7, 0) == 6,
            "rivals seen: each grade has its third of a district's eating places, the cheapest address first, and none where there is none");
        Check(SeenRules.DistrictPick(9, 123, 0) == -1 && SeenRules.DistrictPick(9, 123, 5) >= 0 && SeenRules.DistrictPick(9, 123, 5) < 5
              && SeenRules.DistrictPick(9, 123, 5) == SeenRules.DistrictPick(9, 123, 5),
            "rivals seen: a rival with no district of their own eats in one of the city's, the same for good");
        var f2 = new SeenFile { Seed = 5 };
        var rec = new SeenRecord { Key = "ga", Form = SeenForm.Steady, Tier = 2, MarkDay = 1 };
        SeenSubject sub = SeenOwner("a", 1, 1);
        Check(SeenRules.Where(f2, new SeenRecord { Key = "ga" }, sub, 3, 10).Spot == SeenSpot.Nowhere && SeenRules.Where(f2, rec, sub, 3, 10).Spot == SeenSpot.Office
              && SeenRules.Where(null, rec, sub, 3, 10).Spot == SeenSpot.Nowhere, "rivals seen: somebody never marked is nowhere");
        SeenSpot next = SeenRules.Next(f2, rec, sub, 3, 0, out int at);
        SeenSpot none = SeenRules.Next(f2, rec, sub, 3, 22, out int never);
        Check(next == SeenSpot.Office && at == SeenCatalogue.WorkFrom && none == SeenSpot.Nowhere && never == -1,
            "rivals seen: the next place of a day is the office at 9:00, and nothing after the day's last");
    }

    // ---- when the feature starts, and its seed

    private static void SeenLookChecks()
    {
        Check(SeenRules.RivalsState(true, true, false, true, false) == SeenRules.RivalsOn && SeenRules.RivalsState(true, true, false, false, false) == SeenRules.RivalsWait
              && SeenRules.RivalsState(true, true, false, true, true) == SeenRules.RivalsWait && SeenRules.RivalsState(false, true, false, false, false) == SeenRules.RivalsOff
              && SeenRules.RivalsState(true, false, false, true, false) == SeenRules.RivalsOff && SeenRules.RivalsState(true, true, true, true, false) == SeenRules.RivalsOff,
            "rivals seen look: the rivals' own feature is on while it runs awake, still to start while it is made and not yet running or at rest, and off when never made, switched off or broken");
        Check(SeenRules.Look(false, true, false, SeenRules.RivalsOn) == SeenStep.Wait && SeenRules.Look(false, false, true, SeenRules.RivalsOn) == SeenStep.Wait,
            "rivals seen look: before a save is ready nothing is read, written or put away");
        Check(SeenRules.Look(true, false, true, SeenRules.RivalsOn) == SeenStep.PutAway && SeenRules.Look(true, false, false, SeenRules.RivalsOn) == SeenStep.Wait
              && SeenRules.Look(true, false, true, SeenRules.RivalsWait) == SeenStep.PutAway, "rivals seen look: with the feature not wanted what was loaded is put away, and nothing else is done");
        Check(SeenRules.Look(true, true, false, SeenRules.RivalsWait) == SeenStep.Wait && SeenRules.Look(true, true, true, SeenRules.RivalsWait) == SeenStep.Wait
              && SeenRules.Look(true, true, false, SeenRules.RivalsOn) == SeenStep.Go && SeenRules.Look(true, true, false, SeenRules.RivalsOff) == SeenStep.Go,
            "rivals seen look: nothing is loaded or passed until the rivals' own feature is running or known to be off");
        Check(!SeenRules.ReadAgain(10, 10, SeenRules.RivalsOn, SeenRules.RivalsOn) && SeenRules.ReadAgain(11, 10, SeenRules.RivalsOn, SeenRules.RivalsOn)
              && SeenRules.ReadAgain(10, 10, SeenRules.RivalsOff, SeenRules.RivalsOn) && SeenRules.ReadAgain(10, -1, SeenRules.RivalsOn, -1),
            "rivals seen look: the day's rivals are read again on a new day, on the first look after a load, and when the rivals' own feature comes on or goes off");
        // what a pass made before the tycoons are there would do: on a new day their records go (the day's list does not have them); the look waits for them instead
        List<SeenSubject> cast = SeenCast();
        List<SeenSubject> owners = cast.Where(s => !s.Tycoon).ToList();
        var early = new SeenFile { Seed = 5 };
        SeenDay.Run(early, cast, 9, 1000, SeenTown());
        SeenDay.Run(early, owners, 10, 1000, SeenTown());
        var waited = new SeenFile { Seed = 5 };
        SeenDay.Run(waited, cast, 9, 1000, SeenTown());
        int passes = 0;
        foreach (int rivals in new[] { SeenRules.RivalsWait, SeenRules.RivalsWait, SeenRules.RivalsOn })   // three looks of a load landing on day 10
        {
            if (SeenRules.Look(true, true, true, rivals) == SeenStep.Go)
            {
                passes += SeenDay.Run(waited, rivals == SeenRules.RivalsOn ? cast : owners, 10, 1000, SeenTown()) >= 0 ? 1 : 0;
            }
        }
        Check(early.Of("t0") == null && passes == 1 && waited.Day == 10 && waited.Of("t0") != null && waited.Of("t1") != null && waited.Of("t2") != null,
            "rivals seen look: a load landing on a new day while the rivals' own feature still starts plays the day once, with the tycoons, and their records stay");
        // the rivals' own feature going off in the middle of a day: the day's rivals are read again, the day is not played twice and nothing is pruned
        var h = new SeenFile { Seed = 5 };
        SeenDay.Run(h, cast, 20, 1000, SeenTown());
        string before = SeenStore.Write(h);
        int again = SeenDay.Run(h, owners, 20, 1000, SeenTown());
        Check(again == 0 && SeenStore.Write(h) == before && h.Of("t0") != null, "rivals seen look: read again in the middle of a day, the day is not played twice and the tycoons' records stay");
        // the seed: a hash of the playthrough's own id, never a draw of the machine
        var seeds = new HashSet<int>();
        bool positive = true;
        for (int i = 0; i < 5000; i++)
        {
            int seed = SeenRules.SeedOf("playthrough-" + i.ToString(CultureInfo.InvariantCulture));
            positive &= seed > 0;
            seeds.Add(seed);
        }
        Check(positive && seeds.Count == 5000 && SeenRules.SeedOf("abc") == 1149057939 && SeenRules.SeedOf(null) == SeenRules.SeedOf(string.Empty) && SeenRules.SeedOf(null) == 1527344009,
            "rivals seen look: a new file's seed is a fixed hash of the playthrough's own id: the same id gives the same seed on every machine, five thousand ids five thousand seeds, never 0");
        // a file read against the game's day: what is past today is brought back
        var later = new SeenFile { Seed = 3, Day = 50, PlayerDay = 60 };
        later.Records.Add(new SeenRecord { Key = "ga", CarDay = 70 });
        later.Notes.Add(new SeenNote { Day = 55, Key = "ga" });
        later.Notes.Add(new SeenNote { Day = 48, Key = "ga" });
        int moved = SeenRules.Settle(later, 50);
        Check(moved == 3 && later.PlayerDay == 50 && later.Records[0].CarDay == 50 && later.Notes.Count == 1 && later.Notes[0].Day == 48 && SeenRules.Settle(null, 5) == 0,
            "rivals seen look: on load a day of the player's last line, a car's day or a sighting past the game's day is brought back to today");
    }

    // ---- the tycoons, as the rivals' own file has them

    private static void SeenTycoonChecks()
    {
        RivalFile r = RivalRules.Open(77, 1, 1000);
        r.Tycoons[0].ActiveDay = 5;
        r.Tycoons[0].SlowUntil = 50;
        r.Tycoons[1].ActiveDay = 9;
        r.Tycoons[1].DefeatedDay = 40;      // taken over
        r.Tycoons[2].ActiveDay = -1;        // still to come forward
        r.Outlets.Add(new RivalOutlet { Tycoon = 0, Seat = "Office Row#12", District = "ba:neighborhood_midtown" });
        r.Outlets.Add(new RivalOutlet { Tycoon = 0, Seat = "Office Row#14", District = "ba:neighborhood_garment" });
        r.Outlets.Add(new RivalOutlet { Tycoon = 1 });
        var into = new List<SeenSubject>();
        Wiring.SeenTycoons.Map(r, 45, into);
        SeenSubject a = into.Count == 2 ? into[0] : null, b = into.Count == 2 ? into[1] : null;
        Check(a != null && a.Key == "t0" && a.Index == 0 && a.Tycoon && a.Name == r.Tycoons[0].Def.Name && a.Firm == r.Tycoons[0].Def.Holding && a.Figure == r.Tycoons[0].Worth
              && a.Pressed && !a.Gone && a.Shops == 2 && a.Office == "Office Row#12" && a.District == "ba:neighborhood_midtown" && b != null && b.Key == "t1" && b.Gone && b.Shops == 1
              && b.Office.Length == 0, "rivals seen tycoons: a tycoon come forward is listed with their worth, their outlets and their first seat as their office, one taken over as gone, "
                                       + "and one still to come not at all");
        var later = new List<SeenSubject>();
        Wiring.SeenTycoons.Map(r, 50, later);
        Wiring.SeenTycoons.Map(null, 50, later);
        Check(later.Count == 2 && !later[0].Pressed, "rivals seen tycoons: a counter holds a tycoon back until its day, and no file lists nobody");
        Check(Wiring.SeenTycoons.Here(r, 0) && !Wiring.SeenTycoons.Here(r, 1) && !Wiring.SeenTycoons.Here(r, 2) && !Wiring.SeenTycoons.Here(r, 9) && !Wiring.SeenTycoons.Here(r, -1)
              && !Wiring.SeenTycoons.Here(null, 0), "rivals seen tycoons: asked as the hour turns, a tycoon is there while come forward and not taken over");
    }

    // ---- who the world may show, and when they go

    private static void SeenWorldChecks()
    {
        Check(SeenCatalogue.WorldMost == 2 && SeenRules.MayPut(0, 8) && SeenRules.MayPut(1, 8) && !SeenRules.MayPut(2, 8) && !SeenRules.MayPut(3, 8) && !SeenRules.MayPut(-1, 8),
            "rivals seen: two rivals stand in the world at once and never a third");
        Check(SeenCatalogue.PoolKeep == 2 && !SeenRules.MayPut(0, 2) && SeenRules.MayPut(0, 3) && !SeenRules.MayPut(0, 0),
            "rivals seen: nobody is put out with only two of the living city's people left free");
        Check(SeenRules.InRing(110f * 110f) && !SeenRules.InRing(110.5f * 110.5f) && SeenRules.InRing(18f * 18f) && !SeenRules.InRing(17.5f * 17.5f) && !SeenRules.InRing(0f)
              && SeenCatalogue.NearMetres == 110 && SeenCatalogue.LeastMetres == 18,
            "rivals seen: a rival is put out only at a place 18 to 110 metres from the player: nothing when the player is far");
        Check(!SeenRules.LeftBehind(160f * 160f) && SeenRules.LeftBehind(160.5f * 160.5f) && SeenCatalogue.GoneMetres == 160, "rivals seen: somebody 160 metres behind is taken away");
        Check(SeenRules.Runs(true, true, false) && !SeenRules.Runs(false, true, false) && !SeenRules.Runs(true, false, false) && !SeenRules.Runs(true, true, true),
            "rivals seen: nothing runs with the option off, with the living economy off, or in a Going Public session");
        Check(SeenRules.MayStand(false, false) && !SeenRules.MayStand(true, false) && SeenRules.MayStand(true, true) && SeenRules.MayStand(false, true),
            "rivals seen: nobody pops in: a rival appears only where the player is not looking, but for a test's, put in front of the player");
        float far = 161f * 161f, near = 50f * 50f;
        Check(SeenRules.Leaves(true, false, near, false, true, true) == SeenLeave.Taken && SeenRules.Leaves(true, true, near, true, true, true) == SeenLeave.Taken,
            "rivals seen: somebody the living city took back is gone, whatever else");
        Check(SeenRules.Leaves(false, true, far, false, false, false) == SeenLeave.Stays && SeenRules.Leaves(false, true, near, false, false, false) == SeenLeave.Stays,
            "rivals seen: nobody goes while the player is talking with them, not even left behind or with their hour over");
        Check(SeenRules.Leaves(false, false, far, false, true, true) == SeenLeave.Behind && SeenRules.Leaves(false, false, far, true, true, true) == SeenLeave.Behind,
            "rivals seen: somebody left 160 metres behind goes, a test's too");
        Check(SeenRules.Leaves(false, false, near, false, false, false) == SeenLeave.Over && SeenRules.Leaves(false, false, near, false, false, true) == SeenLeave.Stays
              && SeenRules.Leaves(false, false, near, false, true, false) == SeenLeave.Stays && SeenRules.Leaves(false, false, near, true, false, false) == SeenLeave.Stays,
            "rivals seen: somebody whose hour at the place is over goes once nobody is looking; while it lasts, and for a test's, they stay");
        var keys = new[] { "ga", "gb" };
        Check(SeenRules.IsOut("ga", keys, 1) && !SeenRules.IsOut("gb", keys, 1) && SeenRules.IsOut("gb", keys, 2) && !SeenRules.IsOut("gc", keys, 2) && !SeenRules.IsOut("ga", null, 2),
            "rivals seen: who stands already is read from those out, and nobody stands twice");
        SeenCity city = SeenTown();
        List<SeenSubject> cast = SeenCast();
        var f = new SeenFile { Seed = 4242 };
        SeenDay.Run(f, cast, 3, 1000, city);
        var due = new List<SeenDue>(32);
        SeenRules.Due(f, cast, 3, 10, due);
        bool ordered = true, real = true;
        for (int i = 0; i < due.Count; i++)
        {
            ordered &= i == 0 || due[i - 1].Order <= due[i].Order;
            SeenSubject s = cast[due[i].Subject];
            real &= due[i].Where.Spot == SeenRules.Where(f, f.Of(s.Key), s, 3, 10).Spot && due[i].Where.Spot != SeenSpot.Nowhere && due[i].Pick == SeenRules.VenuePick(f.Seed, SeenRules.Who(s.Key), 3);
        }
        Check(due.Count == cast.Count && ordered && real, "rivals seen: at 10:00 on a working day every rival is somewhere (the office), in their seeded turn: " + due.Count + " of " + cast.Count);
        var again = new List<SeenDue>(32);
        var other = new SeenFile { Seed = 4242 };
        SeenDay.Run(other, SeenCast(), 3, 1000, SeenTown());
        SeenRules.Due(other, SeenCast(), 3, 10, again);
        bool identical = again.Count == due.Count;
        for (int i = 0; identical && i < due.Count; i++)
        {
            identical &= again[i].Subject == due[i].Subject && again[i].Where.Spot == due[i].Where.Spot && again[i].Pick == due[i].Pick && again[i].Order == due[i].Order;
        }
        Check(identical, "rivals seen: another machine with the same save has the same rivals at the same places in the same turn");
        SeenRules.Due(f, cast, 3, 3, due);
        int night = due.Count;
        cast[0].Gone = true;
        SeenRules.Due(f, cast, 3, 10, due);
        Check(night == 0 && due.Count == cast.Count - 1 && !due.Any(d => d.Subject == 0), "rivals seen: nobody is due at night, and a tycoon taken over in the middle of the week is not due at once");
        cast[0].Gone = false;
        SeenRules.Due(null, cast, 3, 10, due);
        Check(due.Count == 0, "rivals seen: no file, nobody due");
    }

    // ---- who is tried in a look

    private static void SeenPutChecks()
    {
        // by hand: the first in the hour's turn with a building, near enough and not too near, who does not stand already and whose placing has not failed
        var three = new List<SeenSubject> { SeenOwner("a", 1, 1), SeenOwner("b", 1, 1), SeenOwner("c", 1, 1) };
        var hand = new List<SeenDue>
        {
            new SeenDue { Subject = 0, Building = null, X = 50f },          // no building of the city at their place
            new SeenDue { Subject = 1, Building = "b", X = 10f },           // 10 metres: they would appear beside the player
            new SeenDue { Subject = 2, Building = "c", X = 200f }           // 200 metres: too far
        };
        var none = new string[SeenCatalogue.WorldMost];
        int nobody = SeenRules.PutPick(three, hand, none, 0, 10, 0f, 0f);
        SeenDue c = hand[2];
        c.X = 60f;
        hand[2] = c;
        int third = SeenRules.PutPick(three, hand, none, 0, 10, 0f, 0f);
        SeenDue a = hand[0];
        a.Building = "a";
        hand[0] = a;
        int first = SeenRules.PutPick(three, hand, none, 0, 10, 0f, 0f);
        int standing = SeenRules.PutPick(three, hand, new[] { "ga", null }, 1, 10, 0f, 0f);
        int capped = SeenRules.PutPick(three, hand, none, 2, 10, 0f, 0f), pool = SeenRules.PutPick(three, hand, none, 0, 2, 0f, 0f);
        SeenRules.Fail(hand, 0);
        int afterFail = SeenRules.PutPick(three, hand, none, 0, 10, 0f, 0f);
        SeenRules.Fail(hand, 2);
        int allFailed = SeenRules.PutPick(three, hand, none, 0, 10, 0f, 0f);
        SeenRules.Fail(hand, 9);
        SeenRules.Fail(null, 0);
        Check(nobody == -1 && third == 2 && first == 0 && standing == 2 && capped == -1 && pool == -1 && afterFail == 2 && allFailed == -1 && hand[0].Failed && !hand[1].Failed,
            "rivals seen put: the one tried in a look is the first in the turn with a building 18 to 110 metres away who does not stand already and has not failed this hour; "
            + "nobody at the cap or with the living city's pool short");
        // a street where no rival's door has a spot to stand: every placing fails, an hour of looks
        List<SeenSubject> cast = SeenCast();
        SeenOffices(cast);
        var table = new SeenTable();
        SeenFeed(table, cast, SeenStreets(cast), SeenLadder());
        var f = new SeenFile { Seed = 4242 };
        SeenDay.Run(f, cast, 3, 1000, table);
        var due = new List<SeenDue>(32);
        table.Places(f, cast, 3, 10, due);   // a Wednesday at 10:00: everybody at their office, the offices at (1000 + i, 50)
        Check(due.Count == cast.Count && due.All(d => d.Building != null && !d.Failed && !string.IsNullOrEmpty(d.Place)),
            "rivals seen put: when the hour turns everybody due has the building they are at, its name and where it stands: " + due.Count + " of " + cast.Count);
        float x = 1000f, z = 120f;           // the player, about 70 metres from every office
        int looks = 3600 / SeenCatalogue.LookSeconds, tried = 0;
        var once = new HashSet<int>();
        for (int look = 0; look < looks; look++)
        {
            int pick = SeenRules.PutPick(cast, due, none, 0, 10, x, z);
            if (pick < 0)
            {
                continue;
            }
            tried++;
            once.Add(pick);
            SeenRules.Fail(due, pick);       // no spot by the door: the placing failed
        }
        Check(tried == cast.Count && once.Count == cast.Count && SeenCatalogue.LookSeconds == 6,
            "rivals seen put: where no door has a spot to stand, an hour of looks (" + looks + ") tries " + tried + " placings, one a look and each rival once, where every look tried all "
            + cast.Count + " before (" + looks * cast.Count + " an hour)");
        table.Places(f, cast, 3, 11, due);
        Check(due.All(d => !d.Failed) && SeenRules.PutPick(cast, due, none, 0, 10, x, z) >= 0, "rivals seen put: when the hour turns a placing that failed is tried again");
    }

    // ---- a place to stand by a door

    private static void SeenDoorChecks()
    {
        string spots = ModSource(@"CityLife\CitySpots.cs");
        Match keep = Regex.Match(spots, @"private const float DoorKeepOut = ([0-9.]+)f;");
        float keepOut = keep.Success ? float.Parse(keep.Groups[1].Value, CultureInfo.InvariantCulture) : -1f;
        Check(keep.Success && keepOut == SeenCatalogue.DoorKeepOut && spots.Contains("Physics.CheckSphere(spot + Vector3.up, DoorKeepOut, _doorMask"),
            "rivals seen door: the living city keeps " + keepOut.ToString(CultureInfo.InvariantCulture) + " m round a door clear of people, the figure the spots by a door are set against");
        var along = new List<float>();
        bool sides = true, nearerFirst = true;
        for (int attempt = 0; SeenRules.DoorSpot(attempt, out int side, out float metres); attempt++)
        {
            sides &= side == (attempt % 2 == 0 ? -1 : 1);
            nearerFirst &= along.Count == 0 || metres >= along[along.Count - 1];
            along.Add(metres);
        }
        Check(along.Count == 4 && sides && nearerFirst && !SeenRules.DoorSpot(-1, out int _, out float _),
            "rivals seen door: four spots by a door at most, either side in turn, the nearer first: four spot tests a look at the most");
        // the nearest a spot comes to a door's collider is along the kerb less the door's half width (the 0.7 m out from the door only adds to it)
        bool clear = along.Count > 0 && along.All(m => m - SeenCatalogue.DoorHalf > keepOut);
        Check(clear, "rivals seen door: every spot (" + string.Join(", ", along.Select(m => m.ToString("0.0", CultureInfo.InvariantCulture))) + " m along) less a door's half width ("
                     + SeenCatalogue.DoorHalf.ToString("0.0", CultureInfo.InvariantCulture) + " m) is past the keep-out, so a spot by a door can pass the living city's own test");
    }

    // ---- the greeting's glance

    // A run past somebody standing at the origin, along a line b metres off from 40 m before to 40 m after, the first look at a moment of the second: true if a look
    // or a glance found the runner within the panel's reach. With glance off, a look a second and nothing between.
    private static bool SeenRunPast(float b, float phase, bool glance)
    {
        float speed = SeenCatalogue.RunMetres, reach = SeenCatalogue.TalkMetres;
        for (float t = phase; t < 80f / speed;)
        {
            float x = -40f + speed * t;
            float sqr = x * x + b * b;
            if (sqr < reach * reach)
            {
                return true;
            }
            t += (glance ? SeenRules.Glance(sqr) : 10) / 10f;
        }
        return false;
    }

    private static void SeenGlanceChecks()
    {
        float near = SeenCatalogue.GlanceMetres;
        Check(SeenRules.Glance(near * near) == SeenCatalogue.GlanceTenths && SeenRules.Glance(near * near + 1f) == 10 && SeenRules.Glance(float.MaxValue) == 10 && SeenRules.Glance(0f) == 2
              && SeenCatalogue.GlanceTenths == 2 && SeenCatalogue.GlanceMetres == 12,
            "rivals seen glance: the greeting's panel is looked at every 0.2 s while a rival stands within 12 m, else once a second");
        int passes = 0, missed = 0, missedBefore = 0;
        for (int lateral = 0; lateral <= 38; lateral++)
        {
            for (int phase = 0; phase < 100; phase++)
            {
                passes++;
                missed += SeenRunPast(lateral / 10f, phase / 100f, true) ? 0 : 1;
                missedBefore += SeenRunPast(lateral / 10f, phase / 100f, false) ? 0 : 1;
            }
        }
        Check(missed == 0 && missedBefore > 0 && SeenCatalogue.RunMetres == 6, "rivals seen glance: a runner passing a rival within the panel's 4 m is caught in all " + passes
                                                                              + " passes, where a look a second missed " + missedBefore);
    }

    // ---- the city's table

    private static void SeenTableChecks()
    {
        List<SeenSubject> cast = SeenCast();
        SeenOffices(cast);
        List<SeenPlace> city = SeenStreets(cast);
        var table = new SeenTable();
        SeenFeed(table, cast, city, SeenLadder());
        int eateries = city.Count(p => SeenTable.Eats(p.Type));
        Check(eateries == 120 && table.Eateries == eateries && table.Offices == cast.Count && table.OfficesFound == cast.Count && table.Cars == 12 && table.Districts == 4,
            "rivals seen table: only the eating places and the day's offices are kept: " + table.Eateries + " eating places and " + table.OfficesFound + " of " + table.Offices
            + " offices of " + city.Count + " businesses, and every district but the Hamptons for a working day's meal");
        bool found = true;
        foreach (SeenSubject s in cast)
        {
            found &= table.Office(s.Office, out object building, out float x, out float z) && building is SeenPlace p && p.Name == s.Firm && x == p.X && z == p.Z;
        }
        Check(found && !table.Office("Nowhere Street#1", out object missing, out float _, out float _) && missing == null && !table.Office(null, out missing, out float _, out float _),
            "rivals seen table: each rival's own premises are found by their address, and an address the city has not is not");
        Check(table.OfficeAt("Office Row", 13) >= 0 && table.OfficeAt("Office Row", 12) < 0 && table.OfficeAt("Tycoon Plaza", 12) >= 0 && table.OfficeAt("Office Ro", 13) < 0
              && table.OfficeAt("Office Row ", 13) < 0 && table.OfficeAt(null, 13) < 0, "rivals seen table: a business is matched to an office by its street and number exactly");
        // the city read in another order gives the same table
        var shuffled = city.Select((p, i) => new { p, k = i * 7919 % city.Count }).OrderBy(t => t.k).Select(t => t.p).ToList();
        var other = new SeenTable();
        SeenFeed(other, cast, shuffled, SeenLadder());
        bool same = other.Districts == table.Districts;
        for (int d = 0; d < table.Districts; d++)
        {
            same &= other.District(d) == table.District(d);
            for (int grade = 0; grade < SeenCatalogue.Grades; grade++)
            {
                foreach (int pick in new[] { 0, 250, 500, 750, 999 })
                {
                    same &= table.Eatery(table.District(d), grade, pick, 3, 12, out string one) == other.Eatery(table.District(d), grade, pick, 3, 12, out string two) && one == two;
                }
            }
        }
        Check(same && shuffled.Count == city.Count && !shuffled.SequenceEqual(city),
            "rivals seen table: the city read in any order gives the same table: every grade and pick names the same place");
        // the order of addresses as their keys always compared, with no key made
        string[] streets = { string.Empty, "A", "AB", "Ab", "B", "Street A", "Street A2", "Street", "St#", "Zeta", "Ångström" };
        int[] numbers = { int.MinValue, -12, -1, 0, 1, 2, 9, 10, 11, 19, 100, 101, 999, 1000, int.MaxValue };
        bool ordered = true, parsed = true;
        foreach (string sa in streets)
        {
            foreach (int na in numbers)
            {
                string ka = sa + "#" + na.ToString(CultureInfo.InvariantCulture);
                parsed &= SeenRules.Address(ka, out int cut, out int number) && cut == sa.Length && number == na;
                foreach (string sb in streets)
                {
                    foreach (int nb in numbers)
                    {
                        int want = Math.Sign(string.CompareOrdinal(ka, sb + "#" + nb.ToString(CultureInfo.InvariantCulture)));
                        ordered &= Math.Sign(SeenTable.AddressOrder(sa, na, sb, nb)) == want;
                    }
                }
            }
        }
        Check(ordered, "rivals seen table: two addresses are ordered as their keys always compared, character by character, with no key made");
        Check(parsed && !SeenRules.Address("Street", out int _, out int _) && !SeenRules.Address("Street#", out int _, out int _) && !SeenRules.Address("Street#01", out int _, out int _)
              && !SeenRules.Address("Street#1a", out int _, out int _) && !SeenRules.Address("Street#99999999999", out int _, out int _) && !SeenRules.Address(null, out int _, out int _),
            "rivals seen table: an office's address is read back to its street and number, and a key that is not an address is not one");
        // a timetable: an eating place shut at that hour is not named
        var shut = new SeenTable { Open = (building, day, hour) => hour < 15 };
        SeenFeed(shut, cast, city, SeenLadder());
        Check(shut.Eatery(table.District(0), 1, 500, 3, 12, out string lunch) && lunch.Length > 0 && !shut.Eatery(table.District(0), 1, 500, 3, 19, out string dinner) && dinner.Length == 0,
            "rivals seen table: a meal names only an eating place open at that hour by its own timetable");
        var cars = new SeenTable();
        cars.Begin(cast, "x");
        cars.AddCar(null);
        cars.AddCar("ba:vehicletype_a");
        cars.Done();
        Check(cars.Cars == 1 && cars.CarPlace("ba:vehicletype_a") == 0 && cars.CarPlace("ba:vehicletype_b") == -1 && cars.CarPlace(null) == -1 && cars.Eateries == 0,
            "rivals seen table: the car ladder holds the game's cars in their order, and a car not on it has no place");
        table.Forget();
        Check(table.Eateries == 0 && table.Offices == 0 && table.Cars == 0 && table.Districts == 0, "rivals seen table: forgotten, it holds nothing");
    }

    // ---- the day's sightings

    private static void SeenDayChecks()
    {
        SeenCity city = SeenTown();
        List<SeenSubject> cast = SeenCast();
        var f = new SeenFile { Seed = 31337 };
        bool bounded = true, kept = true, onePerRival = true, placed = true, officeSaturday = true;
        int total = 0, daysWith = 0;
        for (int day = 1; day <= 400; day++)
        {
            int made = SeenDay.Run(f, cast, day, 1000, city);
            List<SeenNote> today = f.Notes.Where(n => n.Day == day).ToList();
            bounded &= made <= SeenCatalogue.DayMost && today.Count <= SeenCatalogue.DayMost && made == today.Count;
            kept &= f.Notes.Count <= SeenCatalogue.NotesKept;
            onePerRival &= today.Where(n => n.Kind == SeenNoteKind.Place).GroupBy(n => n.Key).All(g => g.Count() == 1);
            foreach (SeenNote n in today)
            {
                bool meal = n.Spot == SeenSpot.Lunch || n.Spot == SeenSpot.Dinner;
                placed &= n.Kind != SeenNoteKind.Place || !meal || (n.Place.Length > 0 && n.District.Length > 0 && city.Eating.ContainsKey(n.District) && city.Eating[n.District].Contains(n.Place));
                placed &= n.Kind != SeenNoteKind.Place || (n.Spot != SeenSpot.Office && n.Spot != SeenSpot.Late) || n.Firm.Length > 0;
                placed &= SeenDay.LineKey(n).Length > 0 && n.Name.Length > 0;
                officeSaturday &= n.Spot != SeenSpot.Office || SeenRules.Weekday(n.Day) == SeenRules.Saturday;
            }
            total += today.Count;
            daysWith += today.Count > 0 ? 1 : 0;
        }
        Check(bounded && SeenCatalogue.DayMost == 3, "rivals seen: never more than three sightings in a day, over four hundred days of twenty-two rivals");
        Check(kept && SeenCatalogue.NotesKept == 24, "rivals seen: twenty-four sightings are kept and the oldest go");
        Check(onePerRival, "rivals seen: one place line of one rival in a day at most (a change of car is news of its own)");
        Check(placed, "rivals seen: a meal is at a real eating place of its district, the office is their own firm, and every sighting has a line and a name");
        Check(officeSaturday && SeenDay.Writes(SeenSpot.Office, SeenForm.Trouble, 6) && !SeenDay.Writes(SeenSpot.Office, SeenForm.Trouble, 3) && !SeenDay.Writes(SeenSpot.Office, SeenForm.Trouble, 7)
              && SeenDay.Writes(SeenSpot.Late, SeenForm.Trouble, 3), "rivals seen: the office's line (there all Saturday) is written on a Saturday only, and working late on a working day");
        Check(total > 300 && daysWith > 250, "rivals seen: the city does talk: " + total + " sightings on " + daysWith + " of 400 days");
        Check(SeenDay.HourCount == 4 && SeenDay.HourAt(0) == 12 && SeenDay.HourAt(1) == 19 && SeenDay.HourAt(2) == 17 && SeenDay.HourAt(3) == 11 && SeenDay.HourAt(4) == -1 && SeenDay.HourAt(-1) == -1,
            "rivals seen: the hours a sighting may be written at are 12:00, 19:00, 17:00 and 11:00, each once");
        // the chance is rolled at each of those hours where the week has them at a place with a line, the first that comes up kept: thriving rivals, out more, are seen more
        double expected = 0.0, spread = 0.0, once = 0.0;
        int seenDays = 0;
        for (int who = 0; who < 30; who++)
        {
            var one = new List<SeenSubject> { SeenOwner("p" + who.ToString(CultureInfo.InvariantCulture), 300000, 250000) };
            var file = new SeenFile { Seed = 777 };
            for (int day = 1; day <= 100; day++)
            {
                one[0].Figure = SeenWeekFigure(300000, 12, 10, day);   // 20% up every week: thriving at every mark, and from the fourth tier away at the weekend
                SeenDay.Run(file, one, day, 1000, city);
                SeenRecord r = file.Of(one[0].Key);
                int places = 0;
                for (int k = 0; k < SeenDay.HourCount; k++)
                {
                    SeenWhere w = SeenRules.Where(file, r, one[0], day, SeenDay.HourAt(k));
                    places += w.Spot != SeenSpot.Nowhere && SeenDay.Writes(w.Spot, r.Form, day) ? 1 : 0;
                }
                double p = 1.0 - Math.Pow(1.0 - SeenCatalogue.SightingChance / 1000.0, places);
                expected += p;
                spread += p * (1.0 - p);
                once += places > 0 ? SeenCatalogue.SightingChance / 1000.0 : 0.0;
                seenDays += file.Notes.Any(n => n.Day == day && n.Kind == SeenNoteKind.Place) ? 1 : 0;
            }
        }
        Check(Math.Abs(seenDays - expected) <= 4.0 * Math.Sqrt(spread) && SeenCatalogue.SightingChance == 180,
            "rivals seen: the chance of 180 per mille is rolled at each hour a rival is at a place with a line: " + seenDays + " days seen of 3000, " + expected.ToString("0") + " by the hour, "
            + once.ToString("0") + " had it been once a day");
        // the same day twice, and days missed
        var g = new SeenFile { Seed = 31337 };
        SeenDay.Run(g, cast, 50, 1000, city);
        string first = SeenStore.Write(g);
        int second = SeenDay.Run(g, cast, 50, 1000, city);
        Check(second == 0 && SeenStore.Write(g) == first, "rivals seen: a day's pass runs once: asked again for the same day it changes nothing");
        SeenDay.Run(g, cast, 90, 1000, city);
        Check(!g.Notes.Any(n => n.Day > 50 && n.Day < 90) && g.Day == 90, "rivals seen: days missed (a long sleep, a load) are not played: nothing is written about a day nobody saw");
        // an eating place shut at that hour is no place for a meal
        SeenCity shut = SeenTown();
        shut.ShutFrom = 0;
        var h = new SeenFile { Seed = 31337 };
        bool noMeals = true;
        for (int day = 1; day <= 60; day++)
        {
            SeenDay.Run(h, cast, day, 1000, shut);
            noMeals &= !h.Notes.Any(n => n.Spot == SeenSpot.Lunch || n.Spot == SeenSpot.Dinner);
        }
        Check(noMeals && shut.Asked > 0, "rivals seen: with every eating place shut nobody is written about at a meal");
        // a rival with no district of their own eats in one of the city's
        SeenSubject roamer = SeenOwner("roam", 400000, 300000, 3, 3, false, string.Empty);
        Check(city.Areas.Contains(SeenDay.DistrictOf(f, roamer, city)) && SeenDay.DistrictOf(f, roamer, city) == SeenDay.DistrictOf(f, roamer, city)
              && SeenDay.DistrictOf(f, SeenOwner("x", 1, 1), city) == "ba:neighborhood_midtown" && SeenDay.DistrictOf(f, roamer, new SeenCity()).Length == 0,
            "rivals seen: a rival eats in their own district, or with none of their own in one of the city's by the save's seed");
        // records of rivals no longer listed go once nothing is kept of them
        var p2 = new SeenFile { Seed = 1 };
        var two = new List<SeenSubject> { SeenOwner("a", 100000, 100000), SeenOwner("b", 100000, 100000) };
        SeenDay.Run(p2, two, 1, 1000, city);
        p2.Of("gb").Known = 45;
        p2.Notes.Clear();
        SeenDay.Run(p2, new List<SeenSubject>(), 2, 1000, city);
        Check(p2.Of("ga") == null && p2.Of("gb") != null, "rivals seen: a rival the day's list no longer has is forgotten unless they know the player or a line about them is kept");
        // the paper
        var paper = new List<SeenNote>();
        var q = new SeenFile { Seed = 3 };
        q.Notes.Add(new SeenNote { Day = 9, Hour = 12, Kind = SeenNoteKind.Place, Spot = SeenSpot.Lunch, Form = SeenForm.Steady, Key = "ga", Name = "A", Place = "P", District = "D" });
        q.Notes.Add(new SeenNote { Day = 9, Hour = 19, Kind = SeenNoteKind.Place, Spot = SeenSpot.Dinner, Form = SeenForm.Thriving, Key = "gb", Name = "B", Place = "P", District = "D" });
        q.Notes.Add(new SeenNote { Day = 9, Hour = 9, Kind = SeenNoteKind.NewCar, Form = SeenForm.Thriving, Key = "gc", Name = "C", Car = "x", Old = "y" });
        q.Notes.Add(new SeenNote { Day = 8, Hour = 12, Kind = SeenNoteKind.Place, Spot = SeenSpot.Lunch, Form = SeenForm.Steady, Key = "gd", Name = "D", Place = "P", District = "D" });
        SeenDay.Paper(q, 10, paper);
        bool firstPaper = paper.Count == 2 && paper[0].Key == "gc" && paper[1].Key == "ga";
        SeenDay.Paper(q, 10, paper);
        bool pure = paper.Count == 2 && paper[0].Key == "gc" && paper[1].Key == "ga";
        SeenDay.Paper(q, 12, paper);
        Check(firstPaper && pure && paper.Count == 0 && SeenCatalogue.PaperMost == 2,
            "rivals seen: the morning paper prints two of yesterday's sightings at most, a change of car first, the same however often it is asked");
        Check(SeenDay.Last(q, "ga") != null && SeenDay.Last(q, "ga").Day == 9 && SeenDay.Last(q, "zz") == null && SeenDay.Last(null, "ga") == null,
            "rivals seen: a rival's page finds their newest sighting");
    }

    // ---- a sighting never tells more than the rival's real state

    // A note of every line a sighting can have, with names that hold no figure: the office's on a Saturday, where its line is written.
    private static List<SeenNote> SeenEveryLine()
    {
        var all = new List<SeenNote>();
        for (int spot = 0; spot < (int)SeenSpot.Count; spot++)
        {
            for (int form = 0; form < (int)SeenForm.Count; form++)
            {
                if (SeenDay.Writes((SeenSpot)spot, (SeenForm)form))
                {
                    all.Add(new SeenNote
                    {
                        Day = 6, Hour = 12, Kind = SeenNoteKind.Place, Spot = (SeenSpot)spot, Form = (SeenForm)form, Key = "ga", Name = "Avery Stone", Firm = "Stone and Lane",
                        Place = "The Harbour Room", District = "ba:neighborhood_midtown", Car = "ba:vehicletype_bimmer"
                    });
                }
            }
        }
        foreach (SeenNoteKind kind in new[] { SeenNoteKind.NewCar, SeenNoteKind.CarDown, SeenNoteKind.PlayerCar, SeenNoteKind.PlayerPlace })
        {
            all.Add(new SeenNote
            {
                Day = 6, Hour = 9, Kind = kind, Form = SeenForm.Steady, Key = "ga", Name = "Avery Stone", Place = "The Harbour Room", District = "ba:neighborhood_midtown",
                Car = "ba:vehicletype_bimmer", Old = "ba:vehicletype_ferano"
            });
        }
        return all;
    }

    private static void SeenHonestChecks()
    {
        SeenCity city = SeenTown();
        List<SeenSubject> cast = SeenCast();
        // each rival's figures are set so that their form is known before any rule is asked: takings or worth 20% up every week (thriving), 20% down (in trouble),
        // flat (steady); the first tycoon is held back by a counter (in trouble) and the third's worth grows 1% a week (thriving)
        var designed = new Dictionary<string, SeenForm>(StringComparer.Ordinal);
        var start = new long[cast.Count];
        var num = new int[cast.Count];
        var den = new int[cast.Count];
        for (int i = 0; i < cast.Count; i++)
        {
            SeenSubject s = cast[i];
            SeenForm form = s.Tycoon ? (SeenForm)i : (SeenForm)(i % 3);
            designed[s.Key] = form;
            s.Pressed = s.Tycoon && form == SeenForm.Trouble;
            s.ShopsBefore = s.Tycoon ? -1 : s.Shops;
            num[i] = s.Tycoon ? (form == SeenForm.Thriving ? 101 : 1) : form == SeenForm.Thriving ? 12 : form == SeenForm.Trouble ? 8 : 1;
            den[i] = s.Tycoon ? (form == SeenForm.Thriving ? 100 : 1) : form == SeenForm.Steady ? 1 : 10;
            start[i] = s.Tycoon ? 60000000L + i * 10000000L : form == SeenForm.Trouble ? 1000000000L : 100000L + i * 1000L;
            s.Before = start[i] * den[i] / num[i];
        }
        var f = new SeenFile { Seed = 2718 };
        bool where = true, tone = true, goneSilent = true, noInvented = true;
        int checkedNotes = 0;
        for (int day = 1; day <= 300; day++)
        {
            if (day == 120)
            {
                cast[1].Gone = true;      // a tycoon taken over
                cast[5].Gone = true;      // an owner defeated
            }
            for (int i = 0; i < cast.Count; i++)
            {
                cast[i].Figure = SeenWeekFigure(start[i], num[i], den[i], day);
            }
            SeenDay.Run(f, cast, day, 1000, city);
            foreach (SeenNote n in f.Notes.Where(n => n.Day == day))
            {
                SeenSubject s = cast.First(x => x.Key == n.Key);
                SeenRecord r = f.Of(n.Key);
                checkedNotes++;
                goneSilent &= !s.Gone;
                tone &= n.Form == designed[n.Key];
                if (n.Kind == SeenNoteKind.Place)
                {
                    where &= SeenRules.Where(f, r, s, n.Day, n.Hour).Spot == n.Spot && n.Spot != SeenSpot.Nowhere && SeenDay.Writes(n.Spot, n.Form, n.Day);
                }
                else
                {
                    where &= n.Car == r.Car && n.Old.Length > 0 && n.Old != n.Car && (n.Kind == SeenNoteKind.NewCar) == (city.Ladder.IndexOf(n.Car) > city.Ladder.IndexOf(n.Old));
                }
                string key = SeenDay.LineKey(n);
                noInvented &= key.Length > 0 && (n.Form != SeenForm.Trouble || !key.Contains("thriving")) && (n.Form != SeenForm.Thriving || !key.Contains("trouble"))
                              && (n.Form == SeenForm.Trouble || (n.Spot != SeenSpot.Late && n.Spot != SeenSpot.Office));
            }
        }
        Check(checkedNotes > 200, "rivals seen (honest): " + checkedNotes + " sightings were looked at");
        Check(where, "rivals seen (honest): every sighting is of a rival where their week really has them at that hour, and a change of car names the cars they really had");
        Check(tone, "rivals seen (honest): the tone of every sighting is the form its rival's figures were set to give (up, down or flat by the week), and nothing else");
        Check(noInvented, "rivals seen (honest): a rival in trouble gets no line of a good week, a thriving one none of a bad week, and only one in trouble is written about at the office");
        Check(goneSilent, "rivals seen (honest): nothing is written about a tycoon taken over or an owner defeated, from that day on");
        // the lines carry no figure, in any language: no place for one in the text, and none in a line filled with names that hold none
        var keys = new List<string>();
        SeenLines.Keys(keys);
        List<SeenNote> every = SeenEveryLine();
        bool noPlace = keys.Count > 0, noNumber = true;
        int languages = 0;
        foreach (string code in SeenLanguages)
        {
            Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(DesignLocales, code + ".json"));
            if (words == null)
            {
                continue;
            }
            languages++;
            foreach (string key in keys)
            {
                noPlace &= words.TryGetValue(key, out string text) && !Regex.IsMatch(text, @"\{(worth|figure|takings|income|trend|amount|week|money|price)\}") && !Regex.IsMatch(text, @"\d");
            }
            var w = new SeenWords(words);
            foreach (SeenNote n in every)
            {
                string line = SeenLines.Line(n, w);
                noNumber &= line.Length > 0 && line.IndexOf('{') < 0 && !Regex.IsMatch(line, @"\d");
            }
        }
        Check(noPlace && languages == SeenLanguages.Length, "rivals seen (honest): in every language no sighting's line has a place for a worth, a week's takings or any number");
        Check(noNumber && every.Count == keys.Count, "rivals seen (honest): in every language every sighting's line comes out whole, with no number in it: it hints, and the figures stay "
                                                       + "where the player already reads them");
        Check(!SeenDay.Writes(SeenSpot.Nowhere, SeenForm.Steady) && !SeenDay.Writes(SeenSpot.Office, SeenForm.Steady) && SeenDay.Writes(SeenSpot.Office, SeenForm.Trouble)
              && !SeenDay.Writes(SeenSpot.Dinner, SeenForm.Trouble) && SeenDay.Writes(SeenSpot.Hamptons, SeenForm.Thriving) && !SeenDay.Writes(SeenSpot.Hamptons, SeenForm.Steady)
              && !SeenDay.Writes((SeenSpot)99, SeenForm.Steady) && !SeenDay.Writes(SeenSpot.Lunch, (SeenForm)9),
            "rivals seen (honest): a place has a line only for the forms that can be there, and nothing is written of the rest");
        string dir = ModSource(@"RivalsSeen\SeenDirector.cs") + ModSource(@"RivalsSeen\SeenDirector.World.cs") + ModSource(@"RivalsSeen\SeenGame.cs") + ModSource(@"RivalsSeen\SeenDay.cs");
        Check(!dir.Contains("BizBooks") && !dir.Contains("ChangeMoney") && !dir.Contains("TransactionInfo") && !dir.Contains("Stake(") && !dir.Contains("FameDirector"),
            "rivals seen (honest): seeing a rival moves no money, no shares and no standing: nothing is gained that the pages did not already say");
    }

    // ---- the hour's posts, through a save and a load

    private static void SeenHourChecks()
    {
        var four = new List<SeenSubject> { SeenOwner("a", 1, 1), SeenOwner("b", 1, 1), SeenOwner("c", 1, 1), SeenOwner("d", 1, 1) };
        var f = new SeenFile { Seed = 8, Day = 20, Hour = -1 };
        f.Notes.Add(new SeenNote { Day = 19, Hour = 12, Kind = SeenNoteKind.Place, Spot = SeenSpot.Lunch, Form = SeenForm.Steady, Key = "ga", Name = "A", Place = "P", District = "D" });
        f.Notes.Add(new SeenNote { Day = 20, Hour = 9, Kind = SeenNoteKind.NewCar, Form = SeenForm.Thriving, Key = "gb", Name = "B", Car = "x", Old = "y" });
        f.Notes.Add(new SeenNote { Day = 20, Hour = 12, Kind = SeenNoteKind.Place, Spot = SeenSpot.Lunch, Form = SeenForm.Steady, Key = "gc", Name = "C", Place = "P", District = "D" });
        f.Notes.Add(new SeenNote { Day = 20, Hour = 19, Kind = SeenNoteKind.Place, Spot = SeenSpot.Dinner, Form = SeenForm.Steady, Key = "gd", Name = "D", Place = "P", District = "D" });
        var sent = new List<string>();
        Action<SeenNote> post = n => sent.Add(n.Key);
        int early = SeenDay.Hour(f, four, 20, 8, post);
        Check(early == 0 && sent.Count == 0 && f.Notes[0].Posted && !f.Notes[1].Posted,
            "rivals seen: before its hour nothing goes out, and a sighting of a day that is over is marked without being sent");
        int nine = SeenDay.Hour(f, four, 20, 9, post);
        int again = SeenDay.Hour(f, four, 20, 9, post);
        Check(nine == 1 && again == 0 && sent.Count == 1 && sent[0] == "gb", "rivals seen: a sighting goes out at its hour, once");
        int earlier = SeenDay.Hour(f, four, 20, 5, post);
        Check(earlier == 0 && f.Hour == 9 && sent.Count == 1, "rivals seen: an hour already gone through is not gone through again, and the file's hour never goes back");
        // a save and a load in the middle of the day
        SeenFile loaded = SeenStore.Read(SeenStore.Write(f), out int _);
        int sameHour = SeenDay.Hour(loaded, four, 20, 9, post);
        int noon = SeenDay.Hour(loaded, four, 20, 13, post);
        int evening = SeenDay.Hour(loaded, four, 20, 23, post);
        Check(sameHour == 0 && noon == 1 && evening == 1 && sent.Count == 3 && sent[1] == "gc" && sent[2] == "gd" && loaded.Notes.All(n => n.Posted),
            "rivals seen: after a save and a load in the middle of the day nothing goes out twice and what was still to come does");
        Check(SeenDay.Hour(loaded, four, 21, 9, post) == 0 && SeenDay.Hour(null, four, 20, 9, post) == 0, "rivals seen: an hour of a day whose pass has not run sends nothing");
        var many = new SeenFile { Seed = 8, Day = 5, Hour = -1 };
        for (int i = 0; i < 6; i++)
        {
            many.Notes.Add(new SeenNote { Day = 5, Hour = 10, Kind = SeenNoteKind.Place, Spot = SeenSpot.Lunch, Form = SeenForm.Steady, Key = "g" + i, Name = "N", Place = "P", District = "D" });
        }
        int burst = SeenDay.Hour(many, null, 5, 10, post) + SeenDay.Hour(many, null, 5, 11, post) + SeenDay.Hour(many, null, 5, 23, post);
        Check(burst == SeenCatalogue.PostsMost && SeenCatalogue.PostsMost == 3 && SeenDay.Posted(many, 5) == 3, "rivals seen: three posts a day at most, whatever the file holds");
        int quiet = SeenDay.Hour(new SeenFile { Seed = 8, Day = 5, Hour = -1, Notes = { new SeenNote { Day = 5, Hour = 3, Key = "ga", Name = "A" } } }, null, 5, 4, null);
        Check(quiet == 1, "rivals seen: with Flare away a sighting's hour still passes, and nothing waits to flood it later");
        // somebody gone between the morning's plan and their hour: nothing of them goes out, nor into the next paper
        var g = new SeenFile { Seed = 8, Day = 30, Hour = -1 };
        g.Notes.Add(new SeenNote { Day = 30, Hour = 12, Kind = SeenNoteKind.Place, Spot = SeenSpot.Lunch, Form = SeenForm.Steady, Key = "ga", Name = "A", Place = "P", District = "D" });
        g.Notes.Add(new SeenNote { Day = 30, Hour = 12, Kind = SeenNoteKind.Place, Spot = SeenSpot.Lunch, Form = SeenForm.Steady, Key = "gb", Name = "B", Place = "P", District = "D" });
        g.Notes.Add(new SeenNote { Day = 30, Hour = 19, Kind = SeenNoteKind.Place, Spot = SeenSpot.Dinner, Form = SeenForm.Thriving, Key = "gc", Name = "C", Place = "P", District = "D" });
        g.Notes.Add(new SeenNote { Day = 30, Hour = 10, Kind = SeenNoteKind.PlayerCar, Name = "Sam", Car = "x", Posted = true });
        var day30 = new List<SeenSubject> { SeenOwner("a", 1, 1), SeenOwner("b", 1, 1), SeenOwner("c", 1, 1) };
        sent.Clear();
        SeenDay.Hour(g, day30, 30, 10, post);
        day30[0].Gone = true;          // defeated at 10:00, as the hour's own test finds (SeenGame.Still)
        day30.RemoveAt(2);             // and another no longer on the day's list at all
        int lunch = SeenDay.Hour(g, day30, 30, 12, post);
        var paper = new List<SeenNote>();
        SeenDay.Paper(g, 31, paper);
        Check(lunch == 1 && sent.Count == 1 && sent[0] == "gb" && g.Notes.All(n => n.Key != "ga" && n.Key != "gc") && paper.All(n => n.Key != "ga" && n.Key != "gc")
              && g.Notes.Any(n => n.Kind == SeenNoteKind.PlayerCar) && SeenDay.Here(null, "gz") && !SeenDay.Here(day30, "gz"),
            "rivals seen: a rival defeated or no longer listed since the morning is not seen at their lunch or dinner, nor in the next paper; the rest go out and the player's line stays");
    }

    // ---- the player, seen in turn

    // A file of a day with some of the day's posts out already.
    private static SeenFile SeenPostsOut(int day, int count)
    {
        var f = new SeenFile { Seed = 606, Day = day, Hour = -1 };
        for (int i = 0; i < count; i++)
        {
            f.Notes.Add(new SeenNote { Day = day, Hour = 0, Kind = SeenNoteKind.Place, Spot = SeenSpot.Lunch, Form = SeenForm.Steady, Key = "g" + i, Name = "N", Place = "P", District = "D" });
        }
        SeenDay.Hour(f, null, day, 0, null);
        f.Notes.Add(new SeenNote { Day = day, Hour = 23, Kind = SeenNoteKind.Place, Spot = SeenSpot.Dinner, Form = SeenForm.Steady, Key = "gz", Name = "Z", Place = "P", District = "D" });
        return f;
    }

    private static void SeenPlayerChecks()
    {
        var f = new SeenFile { Seed = 606 };
        int lines = 0, lastDay = -1000, least = int.MaxValue;
        bool named = true;
        for (int day = 1; day <= 1000; day++)
        {
            for (int hour = 8; hour < 22; hour++)
            {
                SeenNote n = SeenDay.Player(f, day, hour, true, "Sam Player", 4, "ba:vehicletype_car09", null, null, null);
                if (n == null)
                {
                    continue;
                }
                lines++;
                least = lastDay > -1000 ? Math.Min(least, day - lastDay) : least;
                lastDay = day;
                named &= n.Kind == SeenNoteKind.PlayerCar && n.Car == "ba:vehicletype_car09" && n.Name == "Sam Player" && n.Key.Length == 0 && SeenDay.LineKey(n) == SeenCatalogue.PlayerCarLine;
            }
        }
        Check(lines > 20 && lines <= 1000 / SeenCatalogue.PlayerGapDays + 1 && least >= SeenCatalogue.PlayerGapDays && SeenCatalogue.PlayerGapDays == 6,
            "rivals seen: the player out in a good car is written about now and then, never twice within six days (" + lines + " lines in 1000 days, the nearest two " + least + " days apart)");
        Check(named && f.Notes.Count <= SeenCatalogue.NotesKept, "rivals seen: the line names the car the player really drives, and the lines kept stay within the bound");
        var g = new SeenFile { Seed = 606 };
        bool silent = true;
        for (int day = 1; day <= 200; day++)
        {
            for (int hour = 0; hour < 24; hour++)
            {
                silent &= SeenDay.Player(g, day, hour, false, "Sam Player", 5, "ba:vehicletype_car11", "Cafe", "D", null) == null;       // no business of their own
                silent &= SeenDay.Player(g, day, hour, true, "Sam Player", 2, "ba:vehicletype_car05", null, null, null) == null;         // a car under the bar
                silent &= SeenDay.Player(g, day, hour, true, "Sam Player", -1, null, null, null, null) == null;                           // on foot in the street
                silent &= SeenDay.Player(g, day, hour, true, string.Empty, 5, "ba:vehicletype_car11", null, null, null) == null;         // nobody
                silent &= SeenDay.Player(g, day, hour, true, "Sam Player", -1, null, "Cafe", string.Empty, null) == null;                // a place nowhere
                silent &= SeenDay.Player(null, day, hour, true, "Sam Player", 5, "ba:vehicletype_car11", null, null, null) == null;
            }
        }
        Check(silent && g.Notes.Count == 0 && SeenCatalogue.PlayerCarRung == 3, "rivals seen: nobody without a business, in a modest car, on foot or without a name is written about");
        SeenNote table = null;
        for (int hour = 0; hour < 24 * 60 && table == null; hour++)
        {
            table = SeenDay.Player(g, 300 + hour / 24, hour % 24, true, "Sam Player", 5, "ba:vehicletype_car11", "Corner Cafe", "ba:neighborhood_midtown", null);
        }
        Check(table != null && table.Kind == SeenNoteKind.PlayerPlace && table.Place == "Corner Cafe" && table.District == "ba:neighborhood_midtown" && table.Car.Length == 0
              && SeenDay.LineKey(table) == SeenCatalogue.PlayerPlaceLine, "rivals seen: at a table the line names the eating place the player is really at");
        // the player's own line is one of the day's posts: refused with three out, and counted when it goes
        int pd = -1, ph = -1;
        for (int day = 300; day < 400 && pd < 0; day++)
        {
            for (int hour = 8; hour < 22 && pd < 0; hour++)
            {
                if (SeenDay.Player(new SeenFile { Seed = 606 }, day, hour, true, "Sam Player", 5, "ba:vehicletype_car11", null, null, null) != null)
                {
                    pd = day;
                    ph = hour;
                }
            }
        }
        SeenFile full = SeenPostsOut(pd, 3), room = SeenPostsOut(pd, 2);
        var posted = new List<SeenNote>();
        SeenNote refused = SeenDay.Player(full, pd, ph, true, "Sam Player", 5, "ba:vehicletype_car11", null, null, posted.Add);
        SeenNote mine = SeenDay.Player(room, pd, ph, true, "Sam Player", 5, "ba:vehicletype_car11", null, null, posted.Add);
        int afterMine = SeenDay.Hour(room, null, pd, 23, null);
        Check(pd > 0 && refused == null && full.PlayerDay < 0 && mine != null && mine.Posted && posted.Count == 1 && posted[0] == mine && SeenDay.Posted(room, pd) == 3 && afterMine == 0,
            "rivals seen: the player's own line is one of the day's three posts: with three out it waits, and when it goes the day's last sighting is held back");
    }

    // ---- a greeting

    private static void SeenGreetChecks()
    {
        SocialWorth first = SocialRules.Chat(true, true, false, true, true);
        int worth = (int)Math.Round(first.Familiarity * 10f);
        var r = new SeenRecord { Key = "ga" };
        int one = SeenRules.Greet(r, 5, true);
        Check(one == worth && worth == 45 && r.Known == 45 && r.GreetDay == 5 && r.Greets == 1,
            "rivals seen: the day's first hello is worth what the social life's own rule gives a first talk face to face, 4.5 points");
        int more = 0;
        for (int i = 0; i < 1000; i++)
        {
            more += SeenRules.Greet(r, 5, i % 2 == 0);
        }
        Check(more == 0 && r.Known == 45 && r.Greets == 1001, "rivals seen: a thousand more hellos on the same day gain nothing");
        Check(SeenRules.Greet(r, 6, false) == worth && r.Known == 90, "rivals seen: the next day's first hello counts again, warm or not the same");
        for (int day = 7; day < 600; day++)
        {
            SeenRules.Greet(r, day, true);
        }
        Check(r.Known == SeenCatalogue.GreetWarmMost * 10 && SeenRules.Greet(r, 601, true) == 0 && r.Known == 1000,
            "rivals seen: how well a rival knows the player stops at the social life's own top of 100");
        Check(SeenRules.KnowsFrom == 300 && SeenRules.KnowsFrom == (int)(SocialRules.FriendFamiliarity * 10f),
            "rivals seen: a rival says where they will be next from the social life's own bar for a friend, 30 points");
        Check(SeenRules.Greet(null, 1, true) == 0, "rivals seen: nobody greets nobody");
        Check(SeenRules.HelloPick(3, 7, 9) >= 0 && SeenRules.HelloPick(3, 7, 9) <= 1 && SeenRules.HelloPick(3, 7, 9) == SeenRules.HelloPick(3, 7, 9),
            "rivals seen: a rival has two ways of saying hello, the same one all day");
        // saying hello: the greeting counted once a day before the talk opens, the talk made of values only
        var f = new SeenFile { Seed = 3 };
        f.Records.Add(new SeenRecord { Key = "ga", Name = "Ann", Form = SeenForm.Thriving, Car = "ba:vehicletype_car04", Known = 290, MarkDay = 1 });
        SeenTalk hello = SeenRules.Hello(f, "ga", "Ann Vale", 40);
        SeenTalk twice = SeenRules.Hello(f, "ga", null, 40);
        Check(hello != null && hello.Gained == worth && !hello.Known && hello.Form == SeenForm.Thriving && hello.Car == "ba:vehicletype_car04" && hello.Name == "Ann Vale"
              && hello.Pick == SeenRules.HelloPick(3, SeenRules.Who("ga"), 40) && twice != null && twice.Gained == 0 && twice.Known && twice.Name == "Ann" && f.Of("ga").Known == 335
              && f.Of("ga").Greets == 2 && SeenRules.Hello(f, "zz", "Z", 40) == null && SeenRules.Hello(null, "ga", "A", 40) == null && SeenRules.Hello(f, null, "A", 40) == null,
            "rivals seen: saying hello counts the day's greeting once, before the talk opens: a second hello that day gains nothing, and somebody not on record is not greeted");
        bool values = typeof(SeenTalk).GetFields().All(x => x.FieldType.IsValueType || x.FieldType == typeof(string));
        int counted = 0;
        foreach (string file in Directory.GetFiles(System.IO.Path.GetDirectoryName(ModPath(@"RivalsSeen\SeenRules.cs")), "*.cs"))
        {
            counted += System.IO.Path.GetFileName(file) == "SeenRules.cs" ? 0 : CountOfText(File.ReadAllText(file), "Greet(");
        }
        Check(values && counted == 0 && !ModSource(@"RivalsSeen\SeenDialog.cs").Contains("Known +"),
            "rivals seen: the talk holds values only, never the record, and nothing but saying hello counts a greeting");
    }

    // ---- the tests taken back

    private static void SeenTestBackChecks()
    {
        List<SeenSubject> cast = SeenCast().Take(8).ToList();
        SeenOffices(cast);
        var table = new SeenTable();
        SeenFeed(table, cast, SeenStreets(cast), SeenLadder());
        var f = new SeenFile { Seed = 4242 };
        var twin = new SeenFile { Seed = 4242 };
        for (int day = 1; day <= 17; day++)
        {
            SeenDay.Run(f, cast, day, 1000, table);
            SeenDay.Run(twin, cast, day, 1000, table);
            SeenDay.Hour(f, cast, day, 11, null);
            SeenDay.Hour(twin, cast, day, 11, null);
        }
        bool full = f.Notes.Count == SeenCatalogue.NotesKept;
        int postedBefore = SeenDay.Posted(f, 17);
        var due = new List<SeenDue>(16);
        table.Places(f, cast, 17, 12, due);   // a Wednesday at noon
        SeenNote test = SeenDay.TestNote(f, cast, due, 17, 12, table);
        SeenNote second = SeenDay.TestNote(f, cast, due, 17, 12, table);
        bool shown = second != null && ReferenceEquals(SeenDay.Last(f, second.Key), second);
        Check(test != null && test.Test && test.Posted && shown && SeenDay.Posted(f, 17) == postedBefore && SeenDay.Writes(test.Spot, test.Form, 17)
              && SeenRules.Where(f, f.Of(test.Key), cast.First(s => s.Key == test.Key), 17, 12).Spot == test.Spot && full && f.Notes.Count == SeenCatalogue.NotesKept + 2,
            "rivals seen tests: a test's sighting is of somebody where their week has them, marked as a test's, on their page at once, on top of the day's posts and kept beside "
            + "the days' sightings, a full list of them too");
        // the days go on after the tests: marks, cars, sightings and their posts, the list trimmed every day
        for (int day = 18; day <= 30; day++)
        {
            SeenDay.Run(f, cast, day, 1000, table);
            SeenDay.Run(twin, cast, day, 1000, table);
            SeenDay.Hour(f, cast, day, 20, null);
            SeenDay.Hour(twin, cast, day, 20, null);
        }
        SeenRules.Greet(f.Of(cast[3].Key), 30, true);
        SeenRules.Greet(twin.Of(cast[3].Key), 30, true);
        int taken = SeenDay.TakeBack(f);
        Check(taken == 2 && SeenStore.Write(f) == SeenStore.Write(twin) && SeenDay.TakeBack(f) == 0 && SeenDay.TakeBack(null) == 0,
            "rivals seen tests: taken back days later, the tests' sightings go and nothing else: the file is the one the days alone made, marks, cars, sightings, posts and "
            + "greetings kept, none of the days' sightings lost to make room");
        table.Places(f, cast, 30, 12, due);   // a Tuesday at noon
        int tried = 0;
        for (int i = 0; i < 6; i++)
        {
            tried += SeenDay.TestNote(f, cast, due, 30, 12, table) != null ? 1 : 0;
        }
        bool capped = tried == 6 && f.Notes.Count(n => n.Test) == SeenCatalogue.TestNotesKept && f.Notes.Count(n => !n.Test) == twin.Notes.Count;
        SeenDay.TakeBack(f);
        Check(capped && SeenStore.Write(f) == SeenStore.Write(twin), "rivals seen tests: a test pressed often keeps its own last four sightings and never more, the days' untouched");
        // a day whose only sightings are a test's: the next morning's paper prints them until they are taken back
        var lone = new SeenFile { Seed = 4242 };
        SeenDay.Run(lone, cast, 17, 1000, table);
        lone.Notes.Clear();
        table.Places(lone, cast, 17, 12, due);
        SeenNote alone = SeenDay.TestNote(lone, cast, due, 17, 12, table);
        var paper = new List<SeenNote>();
        SeenDay.Paper(lone, 18, paper);
        bool printed = alone != null && paper.Count == 1 && ReferenceEquals(paper[0], alone) && ReferenceEquals(SeenDay.Last(lone, alone.Key), alone);
        SeenDay.TakeBack(lone);
        SeenDay.Paper(lone, 18, paper);
        Check(printed && paper.Count == 0 && SeenDay.Last(lone, alone.Key) == null, "rivals seen tests: a test's sighting taken back is off the pages and out of the paper");
    }

    // ---- the save

    private static void SeenStoreChecks()
    {
        var f = new SeenFile { Seed = 123456789, Day = 77, Hour = 13, PlayerDay = 70 };
        f.Records.Add(new SeenRecord
        {
            Key = "gQ0x|;^=\"id", Name = "Zoë \"The|Fox\"; ^Vane", Figure = 9000000000L, Before = 8500000000L, Shops = 7, ShopsBefore = 6, MarkDay = 71, Form = SeenForm.Thriving, Tier = 4,
            Rung = 5, Car = "ba:vehicletype_anselmoaf90", CarDay = 60, Known = 455, GreetDay = 76, Greets = 12
        });
        f.Records.Add(new SeenRecord { Key = "t1" });
        f.Notes.Add(new SeenNote
        {
            Day = 77, Hour = 12, Kind = SeenNoteKind.Place, Spot = SeenSpot.Lunch, Form = SeenForm.Trouble, Key = "gQ0x|;^=\"id", Name = "Zoë", Firm = "Vane & Co; Ltd", Place = "Café 北京|1",
            District = "ba:neighborhood_midtown", Car = "ba:vehicletype_a", Old = "ba:vehicletype_b", Posted = true
        });
        f.Notes.Add(new SeenNote { Day = 76, Hour = 9, Kind = SeenNoteKind.PlayerCar, Name = "Sam", Car = "ba:vehicletype_c" });
        f.Notes.Add(new SeenNote { Day = 77, Hour = 11, Kind = SeenNoteKind.Place, Spot = SeenSpot.Office, Form = SeenForm.Trouble, Key = "t1", Name = "T", Firm = "H", Posted = true, Test = true });
        string line = SeenStore.Write(f);
        SeenFile g = SeenStore.Read(line, out int version);
        SeenRecord a = g != null && g.Records.Count == 2 ? g.Records[0] : null;
        SeenNote n = g != null && g.Notes.Count == 3 ? g.Notes[0] : null;
        Check(version == SeenStore.Version && g != null && g.Seed == 123456789 && g.Day == 77 && g.Hour == 13 && g.PlayerDay == 70, "rivals seen save: the header comes back");
        Check(a != null && a.Key == f.Records[0].Key && a.Name == f.Records[0].Name && a.Figure == 9000000000L && a.Before == 8500000000L && a.Shops == 7 && a.ShopsBefore == 6 && a.MarkDay == 71
              && a.Form == SeenForm.Thriving && a.Tier == 4 && a.Rung == 5 && a.Car == "ba:vehicletype_anselmoaf90" && a.CarDay == 60 && a.Known == 455 && a.GreetDay == 76 && a.Greets == 12,
            "rivals seen save: a rival's record comes back whole, whatever characters their name and key hold");
        Check(g != null && g.Records.Count == 2 && g.Records[1].Key == "t1" && g.Records[1].MarkDay == -1 && g.Records[1].Rung == -1 && g.Records[1].Car.Length == 0 && g.Records[1].ShopsBefore == 0,
            "rivals seen save: a record never marked comes back unmarked");
        Check(n != null && n.Day == 77 && n.Hour == 12 && n.Kind == SeenNoteKind.Place && n.Spot == SeenSpot.Lunch && n.Form == SeenForm.Trouble && n.Key == f.Notes[0].Key && n.Name == "Zoë"
              && n.Firm == "Vane & Co; Ltd" && n.Place == "Café 北京|1" && n.District == "ba:neighborhood_midtown" && n.Car == "ba:vehicletype_a" && n.Old == "ba:vehicletype_b" && n.Posted && !n.Test
              && g.Notes[1].Kind == SeenNoteKind.PlayerCar && !g.Notes[1].Posted && g.Notes[1].Key.Length == 0 && g.Notes[2].Test && g.Notes[2].Posted,
            "rivals seen save: a sighting comes back whole, a test's marked as a test's");
        Check(SeenStore.Write(g) == line, "rivals seen save: written again it is the same line");
        Check(SeenStore.Key == "reallife:rivals_seen" && SeenStore.Load(new Dictionary<string, string>(), out int none) == null && none == 0 && SeenStore.Load(null, out none) == null
              && SeenStore.Read(string.Empty, out none) == null && SeenStore.Read("RV|1|2", out none) == null && SeenStore.Read("v=x^h:1", out none) == null,
            "rivals seen save: an old save has no line and loads with nothing in it, and a line that is not this feature's is not read");
        // a line of before: a record's sixteenth field, the day the player last stood near them, is read past and not written again
        string old = "v=1^h:7|40|9|-1000^r:ga|Ann|100|90|3|3|36|2|1|2|ba%3Acar|30|45|39|4|39^n:40|12|0|2|1|1|ga|Ann|Firm|Place|D|car|old";
        SeenFile o = SeenStore.Read(old, out int ov);
        Check(ov == 1 && o != null && o.Records.Count == 1 && o.Records[0].Greets == 4 && o.Records[0].GreetDay == 39 && o.Records[0].Known == 45 && o.Notes.Count == 1 && !o.Notes[0].Test
              && o.Notes[0].Posted && SeenStore.Write(o) == "v=1^h:7|40|9|-1000^r:ga|Ann|100|90|3|3|36|2|1|2|ba%3Acar|30|45|39|4^n:40|12|0|2|1|1|ga|Ann|Firm|Place|D|car|old|0",
            "rivals seen save: a line of before loads whole and is written again without the day the player last stood near a rival; its sightings are not a test's");
        // a line from a newer version is read as far as it goes
        string newer = "v=9^h:5|40|7|33|99|future^r:ga|Ann|100|90|3|3|36|2|1|2|ba%3Acar|30|45|39|4|39|extra|more;gb|Bob^n:40|12|0|2|1|0|ga|Ann|Firm|Place|D|car|old|0|extra;"
                       + "40|9|77|2|1|0|gz|Zed;40|9|0|55|1|0|gz|Zed^z:something|new^q";
        SeenFile later = SeenStore.Read(newer, out int nine);
        Check(nine == 9 && later != null && later.Seed == 5 && later.Day == 40 && later.Hour == 7 && later.PlayerDay == 33 && later.Records.Count == 2 && later.Records[0].Car == "ba:car"
              && later.Records[0].Known == 45 && later.Records[1].Key == "gb" && later.Records[1].MarkDay == -1 && later.Notes.Count == 1 && later.Notes[0].Place == "Place",
            "rivals seen save: a newer version's line is read as far as it goes: fields past the end, a part with a new letter and a sighting of an unknown kind or place are left out");
        SeenFile bad = SeenStore.Read("v=1^h:x|y|99|z^r:ga|A|-5|-9|-2|-7|-4|9|9|9||x|99999|-8|-3|-2;ga|twice;|nokey^n:1|99|0|2|1|1;short", out int _);
        Check(bad != null && bad.Seed == 0 && bad.Day == -1 && bad.Hour == 23 && bad.Records.Count == 1 && bad.Records[0].Figure == 0 && bad.Records[0].Shops == 0 && bad.Records[0].MarkDay == -1
              && bad.Records[0].Form == SeenForm.Thriving && bad.Records[0].Tier == 4 && bad.Records[0].Rung == 5 && bad.Records[0].Known == 1000 && bad.Records[0].Greets == 0
              && bad.Notes.Count == 1 && bad.Notes[0].Hour == 23, "rivals seen save: every figure of a damaged line is put inside its bounds, a rival comes once, and a record with no key is dropped");
        // a day past the line's own (the player's last line, a car, a sighting) is brought back to it, so no gap waits for a day that never came
        SeenFile ahead = SeenStore.Read("v=1^h:5|40|7|95^r:ga|Ann|100|90|3|3|36|2|1|2|ba%3Acar|70|45|39|4^n:40|12|0|2|1|1|ga|Ann|F|P|D|c|o|0;52|12|0|2|1|0|ga|Ann|F|P|D|c|o|0", out int _);
        SeenFile lost = SeenStore.Read("v=1^h:5|40|7|-99999999", out int _);
        bool bounded = ahead.PlayerDay == 40 && ahead.Records[0].CarDay == 40 && ahead.Notes.Count == 1 && ahead.Notes[0].Day == 40 && lost.PlayerDay == -1000;
        int written = 0;
        for (int hour = 0; hour < 24 * 30; hour++)
        {
            written += SeenDay.Player(ahead, 46 + hour / 24, hour % 24, true, "Sam Player", 5, "ba:vehicletype_car11", null, null, null) != null ? 1 : 0;
        }
        Check(bounded && written > 0,
            "rivals seen save: a damaged or future day in the line is brought back inside it, so the player's lines and the cars go on: " + written + " line(s) in the next 30 days");
        var text = new System.Text.StringBuilder("v=1^h:1|1|1|1^n:");
        for (int i = 0; i < 60; i++)
        {
            text.Append(i > 0 ? ";" : string.Empty).Append("1|1|0|2|1|0|g|n|f|p|d|c|o|").Append(i % 4 == 0 ? "1" : "0");   // every fourth a test's
        }
        SeenFile many = SeenStore.Read(text.ToString(), out int _);
        Check(many.Notes.Count(n => !n.Test) == SeenCatalogue.NotesKept && many.Notes.Count(n => n.Test) == SeenCatalogue.TestNotesKept && SeenCatalogue.TestNotesKept == 4,
            "rivals seen save: a line with too many sightings is read up to the bounds, the days' and a test's each to its own");
        // a culture with a decimal comma and other digits groups writes and reads the same line
        CultureInfo was = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            string german = SeenStore.Write(f);
            CultureInfo.CurrentCulture = new CultureInfo("ar-SA");
            string arabic = SeenStore.Write(SeenStore.Read(german, out int _));
            Check(german == line && arabic == line, "rivals seen save: the line is the same under a culture with a decimal comma and under one with other digits");
        }
        finally
        {
            CultureInfo.CurrentCulture = was;
        }
    }

    // ---- the pages and the buttons

    private static void SeenPageChecks()
    {
        UiWords en = UiWords("en");
        UiRivalsModel league = UiSamples.Rivals(en, "league");
        Check(league.Profile.FactLabels.Length == 2 && league.Profile.FactLabels[0] == "Drives" && league.Profile.FactValues[0] == "Mersaidi S500" && league.Profile.FactLabels[1] == "Usually"
              && league.Profile.FactValues[1] == "Out most days", "rivals seen page: the chosen rival's head in the Rivals app says what they drive and where they are usually found");
        UiRivalsModel businesses = UiSamples.Rivals(en, "businesses");
        Check(businesses.Profile.FactLabels.Length == 3 && businesses.Profile.FactLabels[0] == "Weekly Income" && businesses.Profile.FactLabels[1] == "Drives"
              && businesses.Profile.FactValues.Length == 3,
            "rivals seen page: beside their tables the weekly income stays first and the two facts follow");
        var v = new RivalsView
        {
            League = new[]
            {
                new RivalEntry { Name = "You", You = true, Car = "A car", Found = "Somewhere" }, new RivalEntry { Name = "Gone", Defeated = true, Car = "A car", Found = "Somewhere" },
                new RivalEntry { Name = "Unseen" }, new RivalEntry { Name = "Half", Car = "A car" }
            }
        };
        bool none = true;
        for (int chosen = 0; chosen < v.League.Length; chosen++)
        {
            v.Chosen = chosen;
            var m = new UiRivalsModel();
            RivalsViews.Rivals(m, en, v);
            none &= m.Profile.FactLabels.Length == 0 && m.Profile.FactValues.Length == 0;
        }
        Check(none, "rivals seen page: nothing is said of the player themself, of a defeated rival, or of one with no car or no habit on record");
        string screen = ModSource(@"Ui\Rivals\RivalsScreen.cs");
        int loopEnd = screen.IndexOf("_lines.Add(line);", StringComparison.Ordinal), ask = screen.IndexOf("seenBy.Owner(seenId, out string car, out string found)", StringComparison.Ordinal);
        Check(loopEnd > 0 && ask > loopEnd && CountOfText(screen, ".Owner(") == 1 && screen.Contains("seenId = you || d.isDefeated ? null : d.rivalId;"),
            "rivals seen page: the Rivals app asks the feature once a rebuild, for the one rival whose head shows, never for the player or a defeated rival");
        // the lines made only when something they say has changed
        Dictionary<string, string> english = LoadWords(System.IO.Path.Combine(DesignLocales, "en.json"));
        var w = new SeenWords(english);
        List<SeenSubject> cast = SeenCast();
        var f = new SeenFile { Seed = 11 };
        SeenDay.Run(f, cast, 3, 1000, SeenTown());
        SeenRecord r5 = f.Of("gr05");
        var page = new SeenPage();
        bool told = page.Owner(f, cast, "r05", 1, w, out string car, out string found);
        int made = page.Made;
        bool same = true;
        for (int i = 0; i < 100; i++)
        {
            same &= page.Owner(f, cast, "r05", 1, w, out string car2, out string found2) && ReferenceEquals(car2, car) && ReferenceEquals(found2, found);
        }
        Check(told && r5 != null && car == w.Car(r5.Car) && found == english[SeenCatalogue.FoundKeys[(int)r5.Form]] && made == 1 && same && page.Made == 1,
            "rivals seen page: the chosen rival's car and habit are made once, and asked again with nothing changed the same are given and nothing is made");
        page.Owner(f, cast, "r05", 2, w, out string _, out string _);
        page.Owner(f, cast, "r06", 2, w, out string _, out string _);
        cast[3 + 5].Gone = true;
        bool gone = !page.Owner(f, cast, "r05", 3, w, out string goneCar, out string goneFound) && goneCar == null && goneFound == null;
        bool stranger = !page.Owner(f, cast, "nobody", 3, w, out string _, out string _) && !page.Owner(null, cast, "r05", 4, w, out string _, out string _);
        Check(page.Made == 6 && gone && stranger, "rivals seen page: after a change, or for another rival, the lines are made again; nothing for somebody gone or not on record");
        cast[3 + 5].Gone = false;
        SeenRecord t1 = f.Of("t1");
        string tycoon = page.Tycoon(f, cast, 1, 3, 10, 7, w);
        int afterTycoon = page.Made;
        bool kept = ReferenceEquals(page.Tycoon(f, cast, 1, 3, 10, 7, w), tycoon) && page.Made == afterTycoon;
        page.Tycoon(f, cast, 1, 3, 11, 7, w);
        Check(t1 != null && tycoon == SeenLines.Tycoon(f, t1, cast[1], 3, 10, w) && tycoon.Length > 0 && kept && page.Made == afterTycoon + 1 && page.Tycoon(f, cast, 7, 3, 11, 7, w).Length == 0,
            "rivals seen page: a tycoon's line is made again only when the hour, the rival or something it says has changed; nothing for a place on the rich list nobody holds");
        page.Forget();
        page.Owner(f, cast, "r05", 7, w, out string _, out string _);
        Check(page.Made == afterTycoon + 3, "rivals seen page: forgotten (a load, the option off), the next question makes its answer");
        string rivalPage = ModSource(@"Rivals\RivalPage.cs");
        Check(rivalPage.Contains("RivalBridges.SeenLine != null ? RivalBridges.SeenLine(who) : null") && rivalPage.Contains("box.Line(seen)"),
            "rivals seen page: a tycoon's panel in the Finance app shows their line");
        string links = ModSource(@"Wiring\Wiring.SeenLinks.cs");
        Check(links.Contains("RivalBridges.SeenLine = tycoon =>") && links.Contains("SeenDirector.Instance.TycoonLine(tycoon)") && links.Contains("RivalBridges.SeenLine = null;")
              && links.Contains("SeenBridges.Post = (words, hour, minute) => FlareDirector.Instance != null && FlareDirector.Instance.Society(words, hour, minute)")
              && links.Contains("News.Extra = Paper;") && links.Contains("News.Extra = _paperBefore;") && links.Contains("SeenDirector.Instance?.Stories(day, into)")
              && links.Contains("SeenTycoons.Map(Rivals(), RivalGame.Day, into)") && links.Contains("f == null || SeenTycoons.Here(f, index)")
              && links.Contains("SeenRules.RivalsState(rivals != null, Settings.Rivals, rivals != null && rivals.Broken, rivals != null && rivals.Running, rivals != null && rivals.Resting)"),
            "rivals seen page: the wiring gives the rivals' own state, the tycoons, Flare's account and the paper, and takes each back");
        string options = ModSource(@"RivalsSeen\SeenGuide.cs");
        Check(options.Contains("AddToggle(Settings.LivingRivalsSeenId, \"reallife_seen_enabled\", Settings.LivingRivalsSeen, v => { Settings.LivingRivalsSeen = v; })")
              && options.Contains("AddButton(\"reallife_seen_test_btn_where\", () => SeenDirector.Instance?.TestWhere())")
              && options.Contains("AddButton(\"reallife_seen_test_btn_here\", () => SeenDirector.Instance?.TestHere())")
              && options.Contains("AddButton(\"reallife_seen_test_btn_sighting\", () => SeenDirector.Instance?.TestSighting())")
              && options.Contains("AddButton(\"reallife_seen_test_btn_back\", () => SeenDirector.Instance?.TestBack())"),
            "rivals seen page: the option and each of the four test entries reach their calls");
        string world = ModSource(@"RivalsSeen\SeenDirector.World.cs");
        Check(world.Contains("_onHello = _door.Press(Hello);") && world.Contains("panel.Button(0, \"reallife_seen_btn_hello\", _onHello, true, true);")
              && world.Contains("SeenDialog.Open(talk, s.Person.Character);"), "rivals seen page: the panel's one button says hello, which opens the talk");
        string dialog = ModSource(@"RivalsSeen\SeenDialog.cs");
        Check(dialog.Contains("entry.OnConfirm = () => Line(SeenCatalogue.BusinessTalk[Form], true);") && dialog.Contains("entry.OnSecondOption = () => Line(SeenCatalogue.CarTalk[Form], true);")
              && dialog.Contains("entry.OnCancel = Leave;") && dialog.Contains("SeenCatalogue.HelloKnown[Form]") && dialog.Contains("SeenCatalogue.Hello[Form]")
              && dialog.Contains("if (!string.IsNullOrEmpty(_talk.Car))"),
            "rivals seen page: the talk's buttons ask about the business and the car (only with a car on record) and leave, each by the rival's form");
        string flare = ModSource(@"Social\Flare\FlareDirector.Society.cs");
        Check(flare.Contains("Author = FlareAuthor.Ledger, Source = FlareSource.Headline") && flare.Contains("|| Following ||") && !flare.Contains("enum "),
            "rivals seen page: a sighting on Flare is a post of the City Ledger's own account, as its headlines are: no new author, source or field, and never while following another player's feed");
    }

    // ---- what the shipped passes cost

    private static void SeenCostChecks()
    {
        List<SeenSubject> cast = SeenCast();
        SeenOffices(cast);
        List<SeenPlace> city = SeenStreets(cast);
        List<string> cars = SeenLadder();
        var table = new SeenTable();
        Dictionary<string, string> english = LoadWords(System.IO.Path.Combine(DesignLocales, "en.json"));
        var w = new SeenWords(english);
        // the day's table: the city's businesses and cars read in, as SeenGame.Refresh feeds it once a game day
        SeenCost(() => SeenFeed(table, cast, city, cars), 200, out double tableMs, out long tableBytes);
        // the day's pass over the table
        var f = new SeenFile { Seed = 9 };
        int passDay = 0;
        SeenCost(() => SeenDay.Run(f, cast, ++passDay, 1000, table), 300, out double dayMs, out long dayBytes);
        // the hour's places: who is where and the building each is at, as the hour turns
        var due = new List<SeenDue>(32);
        int placeHour = 0;
        SeenCost(() => table.Places(f, cast, f.Day, 9 + placeHour++ % 12, due), 500, out double placesMs, out long placesBytes);
        // a look with every placing failing, at its worst: every rival but the last in the turn failed already, so the whole list is walked, and the one tried fails
        var workday = new SeenFile { Seed = 9 };
        SeenDay.Run(workday, cast, 3, 1000, table);
        table.Places(workday, cast, 3, 10, due);
        for (int i = 0; i < due.Count - 1; i++)
        {
            SeenRules.Fail(due, i);
        }
        SeenDue last = due[due.Count - 1];
        var outKeys = new string[SeenCatalogue.WorldMost];
        int picked = 0;
        SeenCost(() =>
        {
            int pick = SeenRules.PutPick(cast, due, outKeys, 0, 10, 1000f, 120f);
            if (pick >= 0)
            {
                picked++;
                SeenRules.Fail(due, pick);
                due[pick] = last;
            }
        }, 5000, out double lookMs, out long lookBytes);
        // the hour's posting: three sightings go out, each in words as the director's Post makes them; and an hour with nothing to send
        var hour = new SeenFile { Seed = 9, Day = 40, Hour = -1 };
        for (int i = 0; i < 3; i++)
        {
            hour.Notes.Add(new SeenNote { Day = 40, Hour = 12, Kind = SeenNoteKind.Place, Spot = SeenSpot.Lunch, Form = (SeenForm)i, Key = cast[3 + i].Key, Name = cast[3 + i].Name,
                Place = "The Harbour Room", District = "ba:neighborhood_midtown" });
        }
        int words = 0;
        Action<SeenNote> post = n => words += SeenLines.Line(n, w).Length;
        SeenCost(() =>
        {
            hour.Hour = 11;
            for (int i = 0; i < hour.Notes.Count; i++)
            {
                hour.Notes[i].Posted = false;
            }
            SeenDay.Hour(hour, cast, 40, 12, post);
        }, 2000, out double postMs, out long postBytes);
        SeenCost(() =>
        {
            hour.Hour = 12;
            SeenDay.Hour(hour, cast, 40, 13, post);
        }, 2000, out double quietMs, out long quietBytes);
        // the app's lines: the chosen rival's head on every rebuild with nothing changed, once after a change, and a tycoon's line after a change
        var page = new SeenPage();
        SeenCost(() => page.Owner(f, cast, "r05", 1, w, out string _, out string _), 5000, out double pageMs, out long pageBytes);
        int revision = 1;
        SeenCost(() => page.Owner(f, cast, "r05", ++revision, w, out string _, out string _), 5000, out double changedMs, out long changedBytes);
        SeenCost(() => page.Tycoon(f, cast, 1, f.Day, 10, ++revision, w), 5000, out double tycoonMs, out long tycoonBytes);
        Console.WriteLine("  rivals seen costs (a run, the least of three rounds): the day's table " + SeenMs(tableMs) + " ms, " + tableBytes + " B; the day's pass " + SeenMs(dayMs) + " ms, "
                          + dayBytes + " B; the hour's places " + SeenMs(placesMs) + " ms, " + placesBytes + " B; a look whose placing fails " + SeenMs(lookMs) + " ms, " + lookBytes + " B;");
        Console.WriteLine("  the hour posting three " + SeenMs(postMs) + " ms, " + postBytes + " B, with nothing to post " + SeenMs(quietMs) + " ms, " + quietBytes + " B; the app's head unchanged "
                          + SeenMs(pageMs) + " ms, " + pageBytes + " B, after a change " + SeenMs(changedMs) + " ms, " + changedBytes + " B; a tycoon's line after a change " + SeenMs(tycoonMs)
                          + " ms, " + tycoonBytes + " B");
        Check(table.Eateries == 120 && tableMs < 0.5 && tableBytes <= 256, "rivals seen cost: the day's table of " + city.Count + " businesses takes " + SeenMs(tableMs) + " ms and "
                                                                          + tableBytes + " bytes (no string made for any business)");
        Check(dayMs < 2.0 && dayBytes < 8192, "rivals seen cost: the day's pass over twenty-two rivals takes " + SeenMs(dayMs) + " ms and " + dayBytes + " bytes (its sightings)");
        Check(due.Count == cast.Count && placesMs < 0.2 && placesBytes == 0, "rivals seen cost: the hour's places take " + SeenMs(placesMs) + " ms and allocate nothing (" + placesBytes + " bytes)");
        Check(picked > 0 && lookMs < 0.05 && lookBytes == 0, "rivals seen cost: a look whose one placing fails takes " + SeenMs(lookMs) + " ms of the feature's own and allocates nothing ("
                                                             + lookBytes + " bytes), with at most four spot tests by the door on top");
        Check(words > 0 && postMs < 0.2 && postBytes < 4096 && quietBytes == 0, "rivals seen cost: an hour posting three sightings takes " + SeenMs(postMs) + " ms and " + postBytes
                                                                               + " bytes (their words), an hour with nothing to post " + quietBytes + " bytes");
        Check(pageBytes == 0 && changedBytes == 0 && pageMs < 0.01 && tycoonBytes < 2048, "rivals seen cost: the app's head asked on a rebuild with nothing changed allocates nothing (" + pageBytes
                                                                                         + " bytes) and after a change " + changedBytes + "; a tycoon's line " + tycoonBytes + " bytes");
    }

    // ---- the sources

    private static void SeenSourceChecks()
    {
        string folder = System.IO.Path.GetDirectoryName(ModPath(@"RivalsSeen\SeenDirector.cs"));
        var files = new List<string>(Directory.GetFiles(folder, "*.cs")) { ModPath(@"Wiring\Wiring.SeenLinks.cs"), ModPath(@"Wiring\Wiring.SeenTycoons.cs") };
        Check(files.Count >= 18, "rivals seen sources: the feature's files are found (" + files.Count + ")");
        string[] gameFree = { "SeenRules.cs", "SeenDay.cs", "SeenFile.cs", "SeenCatalogue.cs", "SeenStore.cs", "SeenTable.cs", "SeenLines.cs", "SeenPage.cs", "Wiring.SeenTycoons.cs" };
        int randoms = 0;
        foreach (string file in files)
        {
            string text = File.ReadAllText(file);
            string name = System.IO.Path.GetFileName(file);
            Check(!text.Contains("Update()") && !text.Contains("FindObjectsByType") && !text.Contains("FindObjectOfType") && !text.Contains("OverlapSphere") && !text.Contains("Physics.")
                  && !text.Contains("NavMesh.") && !text.Contains("Raycast(") && !text.Contains("CheckSphere(") && !text.Contains("CheckCapsule("),
                "rivals seen sources: " + name + " has no per-frame code, no scene search and no physics or nav mesh query of its own");
            Check(!text.Contains("TODO") && !text.Contains("NotImplemented") && !text.Contains("ProjectSettings"), "rivals seen sources: " + name + " has no stub and no note of work left");
            Check(!text.Contains("GetHashCode") && !text.Contains("System.Random") && !text.Contains("DateTime.Now") && !text.Contains("Environment."),
                "rivals seen sources: " + name + " draws nothing from the machine: no runtime hash, no clock, no unseeded generator");
            Check(!text.Contains("AppearanceSetter") && !text.Contains("CreatePrefab") && !text.Contains("Dress(") && !text.Contains("Instantiate(") && !text.Contains("AddComponent<Animator")
                  && !text.Contains("BonePose") && !text.Contains("HairPhysics"), "rivals seen sources: " + name + " makes no character, look, body or animation of its own");
            Check(!text.Contains("GameManager.ChangeMoney") && !text.Contains("BizBooks.Book") && !text.Contains("ChangeMoneySafe"), "rivals seen sources: " + name + " moves no money");
            randoms += CountOfText(text, "Random");
            Check(!gameFree.Contains(name) || (!text.Contains("UnityEngine") && !text.Contains("BigAmbitions") && !text.Contains("SaveGameManager") && !text.Contains("using Localizor")),
                "rivals seen sources: " + name + " is game-free where it must be");
        }
        Check(randoms == 0, "rivals seen sources: nothing is drawn from the engine's generator or any other: every draw is a hash of the save's seed, the seed a hash of the playthrough's own id");
        string dir = ModSource(@"RivalsSeen\SeenDirector.cs");
        Check(dir.Contains("StartCoroutine(Guard.Routine(Tick()))") && dir.Contains("new WaitForSecondsRealtime(1f)") && dir.Contains("new WaitForSecondsRealtime(SeenCatalogue.GlanceTenths / 10f)")
              && dir.Contains("_broken = true;") && dir.Contains("yield return glancing ? _waitGlance : _waitLook;"),
            "rivals seen sources: the timer is a guarded coroutine, a look a second and a glance every 0.2 s only while a rival stands near, and a fault switches the feature off for the session");
        int step = dir.IndexOf("SeenStep step = SeenRules.Look(ready, Wanted, _loaded, rivals);", StringComparison.Ordinal), load = dir.IndexOf("Load();", StringComparison.Ordinal);
        int pass = dir.IndexOf("!Pass(day, rivals)", StringComparison.Ordinal);
        Check(step > 0 && step < load && load < pass && dir.Contains("SeenRules.Runs(Settings.LivingRivalsSeen, Settings.LivingPopulation, GoingPublic.InSession)")
              && dir.Contains("ClearWorld(GoingPublic.InSession ? \"a Going Public session\" : \"switched off\");")
              && dir.Contains("_file.Seed = SeenRules.SeedOf(SeenBridges.Playthrough != null ? SeenBridges.Playthrough() : null);") && dir.Contains("SeenRules.Settle(_file, SeenGame.Day)"),
            "rivals seen sources: the look asks the rule before anything is loaded or passed, a new file's seed is the playthrough's, a loaded file is settled against the day, and switched off "
            + "whoever stands goes");
        int ready = dir.IndexOf("if (!SeenGame.CityReady())", StringComparison.Ordinal), refresh = dir.IndexOf("if (!SeenGame.City.Refresh(_subjects))", StringComparison.Ordinal);
        int stamp = dir.IndexOf("_readDay = day;", StringComparison.Ordinal);
        Check(ready > 0 && ready < refresh && refresh < stamp && CountOfText(dir, "_readDay = day;") == 1,
            "rivals seen sources: the day is stamped as read only once the city's buildings and the day's rivals are read, so a table is never left empty for the day");
        int still = dir.IndexOf("went = SeenGame.Still(_subjects);", StringComparison.Ordinal), hour = dir.IndexOf("SeenDay.Hour(_file, _subjects, day, hour, _post)", StringComparison.Ordinal);
        int mine = dir.IndexOf("SeenDay.Player(_file, day, hour,", StringComparison.Ordinal);
        Check(still > 0 && still < hour && hour < mine && dir.Contains("rung, car, table ? place : null, district, _post);") && !dir.Contains("mine.Posted = true"),
            "rivals seen sources: as the hour turns whether the rivals are still there is asked first, then the hour's posts go, then the player's line, a post of the day's like the others");
        Check(dir.Contains("SeenFile f = File;") && dir.Contains("public SeenFile File => Running ? _file : null;"),
            "rivals seen sources: the paper and the pages get nothing while the feature does not run");
        string world = ModSource(@"RivalsSeen\SeenDirector.World.cs");
        Check(world.Contains("SeenRules.PutPick(_subjects, _due, _outKeys, _outCount, city.People.Free, origin.x, origin.z)") && world.Contains("SeenRules.Fail(_due, pick);")
              && world.Contains("SeenRules.Leaves(lost, talking, CitySpots.Flat(s.At - origin), s.Test, still, inView)")
              && world.Contains("bool talking = !lost && SeenGame.TalkingWith(s.Person.Character);")
              && world.Contains("_glance = _outCount > 0 && SeenRules.Glance(Nearest(origin)) < 10;") && world.Contains("SeenRules.Hello(_file, s.Key, s.Name, SeenGame.Day)")
              && CountOfText(world, "PutOne(city, origin);") == 1 && world.Contains("_nextLook = now + SeenCatalogue.LookSeconds;")
              && world.Contains("SeenGame.City.Table.Places(_file, _subjects, day, hour, _due);"),
            "rivals seen sources: the world's decisions are the rules' (who is tried, a failure kept for the hour, who goes, the glance, the greeting), one placing a look");
        Check(world.Contains("city.People.Take(CityLook.Civilian, at,") && world.Contains("city.People.Release(s.Person)") && world.Contains("new Standing[SeenCatalogue.WorldMost]")
              && world.Contains("BuildingManager.IsInsideBuilding || uis.timeMachine.isRunning") && world.Contains("ClearWorld(\"the street is gone\")"),
            "rivals seen sources: a rival in the world is one of the living city's own people as that pool dresses them, and indoors or on a time skip everybody goes");
        int leave = world.IndexOf("private void Leave(", StringComparison.Ordinal);
        int end = leave > 0 ? world.IndexOf("SeenGame.EndTalk(s.Person.Character);", leave, StringComparison.Ordinal) : -1;
        int release = leave > 0 ? world.IndexOf("city.People.Release(s.Person);", leave, StringComparison.Ordinal) : -1;
        Check(leave > 0 && end > leave && end < release,
            "rivals seen sources: whoever must go anyway (indoors, the option, a fault) has our greeting with them ended before the living city has them back");
        string game = ModSource(@"RivalsSeen\SeenGame.cs");
        Check(game.Contains("SeenRules.DoorSpot(attempt, out int side, out float metres)") && game.Contains("SeenRules.MayStand(CitySpots.InView(spot), test)")
              && game.Contains("CitySpots.OffPavement = off;")
              && game.Contains("CitySpots.Blocked = blocked;") && game.Contains("CitySpots.NearDoor = near;"),
            "rivals seen sources: the spots by a door are the rule's, only out of view, and a spot refused leaves the living city's counts as they were");
        Check(game.Contains("if (TalkingWith(who) && Dialogue().dialog is SeenDialog)") && game.Contains("ReferenceEquals(ui.npcInDialog, who)"),
            "rivals seen sources: only our own greeting is ended, and only with the person it holds");
        Check(game.Contains("TrafficTools.Load(prefab, Lane[i])") && game.Contains("!TrafficTools.Ready") && game.Contains("car.gameObject.activeInHierarchy") && game.Contains("CitySpots.InView(at)")
              && game.Contains("SeenRules.CarId(type, out int from, out int length)") && !game.Contains("SetVehicleIgnored") && !game.Contains("isKinematic"),
            "rivals seen sources: their car is an idle car of the traffic pool named as the game names a type's car, put on a lane out of view and left to the game's traffic, never held");
        Check(game.Contains("all.Sort(ById);") && game.Contains("_models.Sort(ByPrice);") && game.Contains("GameDlc.Open(") && game.Contains("CityCheck.Here(Hamptons)"),
            "rivals seen sources: the game's rivals and cars are read in an order of their own, never a dictionary's, and nothing a DLC keeps is picked");
        Check(game.Contains("weekly = shops != null ? d.WeeklyIncome : 0f;") && game.Contains("s.Figure = (long)Math.Max(0.0, Math.Round(weekly));")
              && game.Contains("e.Item1 >= day - SeenRules.WeekDays"),
            "rivals seen sources: an owner's figure is the weekly income the game's Rivals app shows, the figure the game's own history keeps for the week before");
        Check(game.Contains("!SeenTable.Eats(r.businessTypeName) || GoingPublic.Mine(r)"), "rivals seen sources: at a table of the player's own restaurant the player is not out to eat");
        Check(game.Contains("Gone = RivalsHelper.IsRivalDefeated(d.id)") && game.Contains("RivalsHelper.IsRivalDefeated(s.Id)") && game.Contains("!SeenBridges.TycoonHere(s.Index)"),
            "rivals seen sources: whether a rival is gone is the game's own test for an owner and the rivals' file for a tycoon, asked in the morning and again as each hour turns");
        Check(CountOfText(ModSource(@"Wiring\Wiring.cs"), "SeenLinks") == 2, "rivals seen sources: the wiring connects and disconnects the feature");
        Check(ModSource(@"Meta\FeatureList.cs").Contains("RivalsSeen.SeenGuide.Register()") && ModSource(@"Core\FeatureOwners.cs").Contains("\"RealLifeOverhaul.RivalsSeen\", \"rivals_seen\"")
              && ModSource(@"Meta\OptionsTree.cs").Contains("S(\"rivalsseen\").For(\"rivals_seen\").Opt(\"living_rivals_seen\", \"living_population\")")
              && ModSource(@"Meta\FeatureCities.cs").Contains("new Line(\"rivals_seen\"") && ModSource(@"Core\Settings.cs").Contains("public static bool LivingRivalsSeen = true;")
              && ModSource(@"Core\Settings.cs").Contains("LivingRivalsSeen = Bool(modId, LivingRivalsSeenId, LivingRivalsSeen);"),
            "rivals seen sources: the feature has its option (on, under the living economy), its Guide page, its owner and its place in the cities list");
    }

    // ---- the texts

    private static void SeenTextChecks()
    {
        var keys = new SortedSet<string>(StringComparer.Ordinal);
        string[] sources = { @"RivalsSeen\SeenCatalogue.cs", @"RivalsSeen\SeenDirector.cs", @"RivalsSeen\SeenDirector.World.cs", @"RivalsSeen\SeenDirector.Tests.cs", @"RivalsSeen\SeenGuide.cs",
            @"RivalsSeen\SeenDialog.cs", @"RivalsSeen\SeenText.cs", @"RivalsSeen\SeenGame.cs", @"RivalsSeen\SeenLines.cs", @"RivalsSeen\SeenPage.cs" };
        foreach (string source in sources)
        {
            foreach (Match m in Regex.Matches(ModSource(source), "\"(reallife_[a-z0-9_]+)\""))
            {
                keys.Add(m.Groups[1].Value);
            }
        }
        foreach (string k in new[] { "reallife_opt_sec_rivalsseen", "reallife_opt_tip_living_rivals_seen", "reallife_ui_rivals_drives", "reallife_ui_rivals_found" })
        {
            keys.Add(k);
        }
        keys.Remove("reallife_seen_calldialogtype");
        Check(keys.Count >= 75 && keys.Contains(SeenLines.CarKey) && keys.Contains(SeenLines.TodayKey), "rivals seen texts: its keys are found: " + keys.Count);
        string[] notices = { "reallife_seen_note_player", "reallife_seen_test_off", "reallife_seen_test_session", "reallife_seen_test_where", "reallife_seen_test_nobody", "reallife_seen_test_here",
            "reallife_seen_test_sighting", "reallife_seen_test_nothing", "reallife_seen_test_back", "reallife_seen_test_back_none" };
        string[] facts = { "{seen_world_most}", "{seen_near}", "{seen_car_hold}", "{seen_day_most}", "{seen_player_gap}", "{seen_known}", "{seen_gain}" };
        Dictionary<string, string> english = LoadWords(System.IO.Path.Combine(DesignLocales, "en.json"));
        foreach (string code in SeenLanguages)
        {
            Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(DesignLocales, code + ".json"));
            Check(words != null, "rivals seen texts: " + code + ".json is read");
            if (words == null)
            {
                continue;
            }
            foreach (string key in keys)
            {
                bool there = words.TryGetValue(key, out string value) && value.Length > 0;
                Check(there, "rivals seen texts: " + key + " is in " + code + ".json");
                if (!there)
                {
                    continue;
                }
                Check(!value.Contains('!') && !value.Contains((char)0xFF01) && !value.Contains((char)0x2014) && !value.Contains((char)0x2013) && !value.Contains(" - "),
                    "rivals seen texts: " + key + " has no exclamation mark or dash in " + code);
                if (english != null && english.TryGetValue(key, out string en))
                {
                    var wanted = new SortedSet<string>(Regex.Matches(en, @"\{[a-z_0-9]+\}").Cast<Match>().Select(m => m.Value));
                    var got = new SortedSet<string>(Regex.Matches(value, @"\{[a-z_0-9]+\}").Cast<Match>().Select(m => m.Value));
                    Check(wanted.SetEquals(got), "rivals seen texts: " + key + " keeps its {names} in " + code);
                    Check(Regex.Matches(en, "<b>").Count == Regex.Matches(value, "<b>").Count && Regex.Matches(en, @"\n").Count == Regex.Matches(value, @"\n").Count,
                        "rivals seen texts: " + key + " keeps its tags and lines in " + code);
                }
            }
            foreach (string key in notices)
            {
                Check(words.TryGetValue(key, out string value) && value.Length <= 240, "rivals seen texts: the notice " + key + " is at most 240 characters in " + code);
            }
            // a sighting's line with long names in it is still a notice's length: it is quoted in two notices
            foreach (string[] row in SeenCatalogue.PlaceLines)
            {
                foreach (string key in row.Where(k => k.Length > 0))
                {
                    Check(words.TryGetValue(key, out string value) && value.Length <= 170, "rivals seen texts: the line " + key + " leaves room for its names in a notice in " + code);
                }
            }
            if (words.TryGetValue("reallife_guide_rivals_seen", out string page))
            {
                foreach (string fact in facts)
                {
                    Check(page.Contains(fact), "rivals seen texts: the Guide page has " + fact + " in " + code);
                }
                Check(words.TryGetValue("reallife_seen_btn_hello", out string hello) && page.Contains("<b>" + hello + "</b>"),
                    "rivals seen texts: the Guide names the greeting's button by its own text in " + code);
            }
        }
        string guide = ModSource(@"RivalsSeen\SeenGuide.cs");
        foreach (string fact in facts)
        {
            Check(guide.Contains(".Fact(\"" + fact.Trim('{', '}') + "\""), "rivals seen texts: the Guide's " + fact + " is filled from the rules");
        }
        Check(guide.Contains(".Fact(\"seen_day_most\", () => SeenCatalogue.PostsMost.ToString(Inv))"),
            "rivals seen texts: the Guide's posts a day are the cap the hour's posts and the player's line share");
        foreach (string[] row in SeenCatalogue.PlaceLines)
        {
            Check(row.Length == (int)SeenForm.Count, "rivals seen texts: every place has a slot for each form");
        }
        Check(SeenCatalogue.PlaceLines.Length == (int)SeenSpot.Count && SeenCatalogue.SpotNames.Length == (int)SeenSpot.Count && SeenCatalogue.HabitKeys.Length == (int)SeenForm.Count
              && SeenCatalogue.FoundKeys.Length == (int)SeenForm.Count && SeenCatalogue.Hello.Length == (int)SeenForm.Count && SeenCatalogue.HelloKnown.Length == (int)SeenForm.Count
              && SeenCatalogue.BusinessTalk.Length == (int)SeenForm.Count && SeenCatalogue.CarTalk.Length == (int)SeenForm.Count && SeenCatalogue.Hello.All(h => h.Length == 2),
            "rivals seen texts: every place, form and greeting has its key");
        // What's new: the feature's line in the next row of every language, before the first fix mark.
        string sentence = "Rival owners and the tycoons of the rich list are now seen around the city";
        string[] starts =   // how the line begins in each language, as it was translated
        {
            sentence, "Donos rivais e os magnatas do rank", "对手业主和富豪榜上的大亨如今", "Konkurrenten und die Tycoons der R",
            "Les patrons concurrents et les mag", "Los dueños rivales y los magnates ",
            "Владельцы-конкуренты и магнаты из ",
            "Rivaliserende zakenlieden en de ty", "Rakip işletme sahipleri ve Zenginl", "Właściciele konkurencyjnych firm i",
            "경쟁사 사업자와 부자 순위"
        };
        string[] marks = { "Fixed: ", "Corrigido: ", "已修复：", "Behoben: ", "Corrigé : ", "Corregido: ", "Исправлено: ", "Opgelost: ",
            "Düzeltildi: ", "Naprawiono: ", "수정: " };
        for (int i = 0; i < SeenLanguages.Length; i++)
        {
            Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(DesignLocales, SeenLanguages[i] + ".json"));
            string next = WhatsNewText(words, WhatsNewKey(LoadWords(System.IO.Path.Combine(DesignLocales, "en.json")), sentence));   // the row that holds the line: the next row, or the version's once numbered
            int mark = next.IndexOf(marks[i], StringComparison.Ordinal);
            string before = mark >= 0 ? next.Substring(0, mark) : next;
            if (i == 0)
            {
                Check(next.Contains(sentence) && next.IndexOf(sentence, StringComparison.Ordinal) < (mark < 0 ? int.MaxValue : mark),
                    "rivals seen texts: What's new has the feature's line before the first fix mark in en");
            }
            else
            {
                Check(!next.Contains(sentence) && before.Contains(starts[i]) && before.Substring(before.IndexOf(starts[i], StringComparison.Ordinal)).Contains("Flare"),
                    "rivals seen texts: What's new has the feature's line, translated, before the first fix mark in " + SeenLanguages[i]);
            }
        }
    }
}
