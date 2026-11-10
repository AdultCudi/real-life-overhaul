using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using RealLifeOverhaul;
using RealLifeOverhaul.RivalsSeen;
using RealLifeOverhaul.Ui;

// Rivals seen around the city (Scripts/RivalsSeen, run with --rivalseen): how a business is doing and what its owner can afford from their real figures, the weekly mark, the
// car and its hold, the seeded week (every cap and rate, nobody gone anywhere, nobody in the Hamptons at lunch in town, no Hamptons in a city without them), who the world may show
// and its cost, the day's sightings (bounded, one a rival, honest: only where the week really has them and in the tone their figures give), the hour's posts through a save and a
// load, the player seen in turn, the paper, a greeting worth what the social life's own rule gives and no more, the save, old saves and a newer line, the option off and a session,
// the pages and the buttons, the sources and the texts in every language.
internal static partial class Program
{
    private static readonly string[] SeenLanguages = { "en", "pt", "zh-cn", "de", "fr", "es", "ru", "nl", "tr", "pl", "ko" };

    private static void RivalSeen()
    {
        SeenFormChecks();
        SeenMarkChecks();
        SeenCarChecks();
        SeenWeekChecks();
        SeenWorldChecks();
        SeenDayChecks();
        SeenHonestChecks();
        SeenHourChecks();
        SeenPlayerChecks();
        SeenGreetChecks();
        SeenStoreChecks();
        SeenPageChecks();
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
        for (int i = 0; i < cars; i++)
        {
            c.Ladder.Add("ba:vehicletype_car" + i.ToString("00", CultureInfo.InvariantCulture));
        }
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
            Key = "g" + id, Name = "Owner " + id, Firm = "Shop of " + id, Figure = figure, Before = before, Shops = shops, ShopsBefore = shopsBefore, District = district,
            Office = "Fifth Avenue#" + id.Length, Hamptons = hamptons
        };
    }

    private static SeenSubject SeenTycoon(int index, long worth, long weekAgo, bool pressed = false, bool gone = false)
    {
        return new SeenSubject
        {
            Key = "t" + index.ToString(CultureInfo.InvariantCulture), Name = "Tycoon " + index, Firm = "Holding " + index, Tycoon = true, Figure = worth, Before = weekAgo, ShopsBefore = -1,
            Pressed = pressed, Gone = gone
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
        Check(from.Length == SeenCatalogue.Tiers && from[1] == 15000 && from[2] == 60000 && from[3] == 200000 && from[4] == 600000, "rivals seen: an owner's five tiers of means start at 0, 15,000, 60,000, 200,000 and 600,000 a week");
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
        Check(!SeenRules.Mark(r, s, 400, 1000) && r.MarkDay == 3 && !SeenRules.Mark(null, s, 1, 1000) && !SeenRules.Mark(r, null, 1, 1000), "rivals seen: somebody gone is not marked, and nothing marks nothing");
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
        Check(SeenRules.CarTurn(r, 23) == 0 && SeenRules.CarTurn(r, 24) == 1 && SeenCatalogue.CarHoldDays == 14, "rivals seen: a car is kept fourteen days whatever the figures do, then a dearer one comes");
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
        Check(none.Of("ga") != null && none.Of("ga").Car.Length == 0 && none.Of("ga").Rung == -1 && none.Notes.Count == 0, "rivals seen: a city with no cars and no eating places gives nobody a car and writes nothing");
    }

    // ---- the week

    private static void SeenWeekChecks()
    {
        Check(SeenRules.Weekday(1) == 0 && SeenRules.Weekday(6) == SeenRules.Saturday && SeenRules.Weekday(7) == SeenRules.Sunday && SeenRules.Weekday(8) == 0 && SeenRules.Weekday(0) == 6,
            "rivals seen: day 1 is a Monday, as the game counts");
        Check(SeenRules.Who("t0") == 155512425 && SeenRules.Who("gabc") == SeenRules.Who("gabc") && SeenRules.Who("gabc") != SeenRules.Who("gabd") && SeenRules.Who(null) == SeenRules.Who(string.Empty),
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
        bool goneNowhere = true, officeHours = true, troubleNoDinner = true, lateOnlyTrouble = true, awayNotInTown = true, noCoast = true, sundayHome = true, nightNowhere = true, gradeOk = true;
        int steadyLunch = 0, steadyDays = 0, troubleLunch = 0, troubleDays = 0, thrivingLunch = 0, thrivingDays = 0, steadyDinner = 0, thrivingDinner = 0, steadyBeach = 0, steadySaturdays = 0, aloneOk = 0, alone = 0;
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
                                officeHours &= hour >= SeenCatalogue.WorkFrom && hour < SeenCatalogue.WorkTo && (f != SeenForm.Thriving || hour < SeenCatalogue.ThrivingLeaves || hour < SeenCatalogue.LunchTo)
                                               && (work || (weekday == SeenRules.Saturday && f == SeenForm.Trouble));
                            }
                            troubleNoDinner &= !(f == SeenForm.Trouble && w.Spot == SeenSpot.Dinner);
                            lateOnlyTrouble &= w.Spot != SeenSpot.Late || (f == SeenForm.Trouble && work && hour >= SeenCatalogue.WorkTo && hour < SeenCatalogue.LateTo);
                            bool coast = w.Spot == SeenSpot.Beach || w.Spot == SeenSpot.Hamptons;
                            awayNotInTown &= !(away && !work) || coast || w.Spot == SeenSpot.Nowhere;
                            sundayHome &= weekday != SeenRules.Sunday || away || w.Spot == SeenSpot.Nowhere;
                            SeenWhere inland = SeenRules.Where(5, who, f, tier, house, false, day, hour, false);
                            noCoast &= inland.Spot != SeenSpot.Beach && inland.Spot != SeenSpot.Hamptons;
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
        Check(sundayHome, "rivals seen: on a Sunday only those away in the Hamptons are seen");
        Check(noCoast, "rivals seen: in a city without the Hamptons nobody is on the beach or away for the weekend");
        Check(gradeOk && alone > 0 && aloneOk == alone, "rivals seen: an eating place's grade is the rule's for their means and form, and only a rival in trouble sits alone");
        Check(thrivingLunch == thrivingDays && SeenCatalogue.LunchOut[2] == 1000, "rivals seen: a thriving rival is out at lunch every working day");
        Check(Math.Abs(steadyLunch * 1000 / steadyDays - SeenCatalogue.LunchOut[1]) < 60 && SeenCatalogue.LunchOut[1] == 600, "rivals seen: a steady rival is out at lunch about six working days in ten ("
            + steadyLunch * 1000 / steadyDays + " per mille)");
        Check(Math.Abs(troubleLunch * 1000 / troubleDays - SeenCatalogue.LunchOut[0]) < 60 && SeenCatalogue.LunchOut[0] == 250, "rivals seen: a rival in trouble about one in four (" + troubleLunch * 1000 / troubleDays + " per mille)");
        Check(Math.Abs(steadyDinner * 1000 / steadyDays - SeenCatalogue.DinnerOut[1]) < 60 && Math.Abs(thrivingDinner * 1000 / thrivingDays - SeenCatalogue.DinnerOut[2]) < 60,
            "rivals seen: dinner out about three evenings in ten for a steady rival and seven for a thriving one (" + steadyDinner * 1000 / steadyDays + ", " + thrivingDinner * 1000 / thrivingDays + " per mille)");
        Check(Math.Abs(steadyBeach * 1000 / steadySaturdays - SeenCatalogue.SteadyBeach) < 90 && SeenCatalogue.SteadyBeach == 400, "rivals seen: a steady rival takes about four Saturdays in ten on the beach ("
            + steadyBeach * 1000 / steadySaturdays + " per mille)");
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
        Check(SeenRules.DistrictPick(9, 123, 0) == -1 && SeenRules.DistrictPick(9, 123, 5) >= 0 && SeenRules.DistrictPick(9, 123, 5) < 5 && SeenRules.DistrictPick(9, 123, 5) == SeenRules.DistrictPick(9, 123, 5),
            "rivals seen: a rival with no district of their own eats in one of the city's, the same for good");
        var f2 = new SeenFile { Seed = 5 };
        var rec = new SeenRecord { Key = "ga", Form = SeenForm.Steady, Tier = 2, MarkDay = 1 };
        SeenSubject sub = SeenOwner("a", 1, 1);
        Check(SeenRules.Where(f2, new SeenRecord { Key = "ga" }, sub, 3, 10).Spot == SeenSpot.Nowhere && SeenRules.Where(f2, rec, sub, 3, 10).Spot == SeenSpot.Office
              && SeenRules.Where(null, rec, sub, 3, 10).Spot == SeenSpot.Nowhere, "rivals seen: somebody never marked is nowhere");
        SeenSpot next = SeenRules.Next(f2, rec, sub, 3, 0, out int at);
        SeenSpot none = SeenRules.Next(f2, rec, sub, 3, 22, out int never);
        Check(next == SeenSpot.Office && at == SeenCatalogue.WorkFrom && none == SeenSpot.Nowhere && never == -1, "rivals seen: the next place of a day is the office at 9:00, and nothing after the day's last");
    }

    // ---- who the world may show, and what it costs

    private static void SeenWorldChecks()
    {
        Check(SeenCatalogue.WorldMost == 2 && SeenRules.MayPut(0, 8) && SeenRules.MayPut(1, 8) && !SeenRules.MayPut(2, 8) && !SeenRules.MayPut(3, 8) && !SeenRules.MayPut(-1, 8),
            "rivals seen: two rivals stand in the world at once and never a third");
        Check(SeenCatalogue.PoolKeep == 2 && !SeenRules.MayPut(0, 2) && SeenRules.MayPut(0, 3) && !SeenRules.MayPut(0, 0), "rivals seen: nobody is put out with only two of the living city's people left free");
        Check(SeenRules.InRing(110f * 110f) && !SeenRules.InRing(110.5f * 110.5f) && SeenRules.InRing(18f * 18f) && !SeenRules.InRing(17.5f * 17.5f) && !SeenRules.InRing(0f)
              && SeenCatalogue.NearMetres == 110 && SeenCatalogue.LeastMetres == 18, "rivals seen: a rival is put out only at a place 18 to 110 metres from the player: nothing when the player is far");
        Check(!SeenRules.LeftBehind(160f * 160f) && SeenRules.LeftBehind(160.5f * 160.5f) && SeenCatalogue.GoneMetres == 160, "rivals seen: somebody 160 metres behind is taken away");
        Check(SeenRules.Runs(true, true, false) && !SeenRules.Runs(false, true, false) && !SeenRules.Runs(true, false, false) && !SeenRules.Runs(true, true, true),
            "rivals seen: nothing runs with the option off, with the living economy off, or in a Going Public session");
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
        // the cost: an hour's look over a full cast allocates nothing and takes no time to speak of
        long least = long.MaxValue;
        for (int round = 0; round < 3; round++)
        {
            SeenRules.Due(f, cast, 3, 12, due);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int hour = 0; hour < 24; hour++)
            {
                SeenRules.Due(f, cast, 3, hour, due);
            }
            least = Math.Min(least, GC.GetAllocatedBytesForCurrentThread() - before);
        }
        Check(least == 0, "rivals seen: a day of hourly looks at who is where allocates nothing (" + least + " bytes)");
        double best = double.MaxValue;
        for (int round = 0; round < 3; round++)
        {
            var watch = Stopwatch.StartNew();
            for (int i = 0; i < 1000; i++)
            {
                SeenRules.Due(f, cast, 3 + i % 28, i % 24, due);
            }
            best = Math.Min(best, watch.Elapsed.TotalMilliseconds / 1000.0);
        }
        Check(best < 0.2, "rivals seen: an hour's look at twenty-two rivals takes " + best.ToString("0.0000", CultureInfo.InvariantCulture) + " ms (under 0.2)");
        double pass = double.MaxValue;
        for (int round = 0; round < 3; round++)
        {
            var timed = new SeenFile { Seed = 9 };
            var watch = Stopwatch.StartNew();
            for (int day = 1; day <= 100; day++)
            {
                SeenDay.Run(timed, cast, day, 1000, city);
            }
            pass = Math.Min(pass, watch.Elapsed.TotalMilliseconds / 100.0);
        }
        Check(pass < 2.0, "rivals seen: a day's pass over twenty-two rivals takes " + pass.ToString("0.000", CultureInfo.InvariantCulture) + " ms (under 2)");
    }

    // ---- the day's sightings

    private static void SeenDayChecks()
    {
        SeenCity city = SeenTown();
        List<SeenSubject> cast = SeenCast();
        var f = new SeenFile { Seed = 31337 };
        bool bounded = true, kept = true, onePerRival = true, placed = true;
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
            }
            total += today.Count;
            daysWith += today.Count > 0 ? 1 : 0;
        }
        Check(bounded && SeenCatalogue.DayMost == 3, "rivals seen: never more than three sightings in a day, over four hundred days of twenty-two rivals");
        Check(kept && SeenCatalogue.NotesKept == 24, "rivals seen: twenty-four sightings are kept and the oldest go");
        Check(onePerRival, "rivals seen: one sighting of one rival in a day at most");
        Check(placed, "rivals seen: a meal is at a real eating place of its district, the office is their own firm, and every sighting has a line and a name");
        Check(total > 300 && daysWith > 250, "rivals seen: the city does talk: " + total + " sightings on " + daysWith + " of 400 days");
        // the same day twice, and days missed
        var g = new SeenFile { Seed = 31337 };
        SeenDay.Run(g, cast, 50, 1000, city);
        string once = SeenStore.Write(g);
        int second = SeenDay.Run(g, cast, 50, 1000, city);
        Check(second == 0 && SeenStore.Write(g) == once, "rivals seen: a day's pass runs once: asked again for the same day it changes nothing");
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
        var p = new SeenFile { Seed = 1 };
        var two = new List<SeenSubject> { SeenOwner("a", 100000, 100000), SeenOwner("b", 100000, 100000) };
        SeenDay.Run(p, two, 1, 1000, city);
        p.Of("gb").Known = 45;
        p.Notes.Clear();
        SeenDay.Run(p, new List<SeenSubject>(), 2, 1000, city);
        Check(p.Of("ga") == null && p.Of("gb") != null, "rivals seen: a rival the day's list no longer has is forgotten unless they know the player or a line about them is kept");
        // the paper
        var paper = new List<SeenNote>();
        var q = new SeenFile { Seed = 3 };
        q.Notes.Add(new SeenNote { Day = 9, Hour = 12, Kind = SeenNoteKind.Place, Spot = SeenSpot.Lunch, Form = SeenForm.Steady, Key = "ga", Name = "A", Place = "P", District = "D" });
        q.Notes.Add(new SeenNote { Day = 9, Hour = 19, Kind = SeenNoteKind.Place, Spot = SeenSpot.Dinner, Form = SeenForm.Thriving, Key = "gb", Name = "B", Place = "P", District = "D" });
        q.Notes.Add(new SeenNote { Day = 9, Hour = 9, Kind = SeenNoteKind.NewCar, Form = SeenForm.Thriving, Key = "gc", Name = "C", Car = "x", Old = "y" });
        q.Notes.Add(new SeenNote { Day = 8, Hour = 12, Kind = SeenNoteKind.Place, Spot = SeenSpot.Lunch, Form = SeenForm.Steady, Key = "gd", Name = "D", Place = "P", District = "D" });
        SeenDay.Paper(q, 10, paper);
        bool first = paper.Count == 2 && paper[0].Key == "gc" && paper[1].Key == "ga";
        SeenDay.Paper(q, 10, paper);
        bool pure = paper.Count == 2 && paper[0].Key == "gc" && paper[1].Key == "ga";
        SeenDay.Paper(q, 12, paper);
        Check(first && pure && paper.Count == 0 && SeenCatalogue.PaperMost == 2, "rivals seen: the morning paper prints two of yesterday's sightings at most, a change of car first, the same however often it is asked");
        Check(SeenDay.Last(q, "ga") != null && SeenDay.Last(q, "ga").Day == 9 && SeenDay.Last(q, "zz") == null && SeenDay.Last(null, "ga") == null, "rivals seen: a rival's page finds their newest sighting");
    }

    // ---- a sighting never tells more than the rival's real state

    private static void SeenHonestChecks()
    {
        SeenCity city = SeenTown();
        List<SeenSubject> cast = SeenCast();
        var f = new SeenFile { Seed = 2718 };
        bool where = true, tone = true, figures = true, goneSilent = true, noInvented = true;
        int checkedNotes = 0;
        for (int day = 1; day <= 300; day++)
        {
            if (day == 120)
            {
                cast[1].Gone = true;      // a tycoon taken over
                cast[5].Gone = true;      // an owner defeated
            }
            if (day % 30 == 0)
            {
                for (int i = 3; i < cast.Count; i++)   // the figures move
                {
                    cast[i].Before = cast[i].Figure;
                    cast[i].Figure = cast[i].Figure * (80 + (i * 7 + day) % 45) / 100;
                }
            }
            SeenDay.Run(f, cast, day, 1000, city);
            foreach (SeenNote n in f.Notes.Where(n => n.Day == day))
            {
                SeenSubject s = cast.First(x => x.Key == n.Key);
                SeenRecord r = f.Of(n.Key);
                checkedNotes++;
                goneSilent &= !s.Gone;
                tone &= n.Form == r.Form && r.Form == SeenRules.Form(s.Tycoon, r.Figure, r.Before, r.Shops, r.ShopsBefore, s.Pressed);
                if (n.Kind == SeenNoteKind.Place)
                {
                    where &= SeenRules.Where(f, r, s, n.Day, n.Hour).Spot == n.Spot && n.Spot != SeenSpot.Nowhere && SeenDay.Writes(n.Spot, n.Form);
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
        Check(tone, "rivals seen (honest): the tone of every sighting is the form their real figures gave at the week's mark, and nothing else");
        Check(noInvented, "rivals seen (honest): a rival in trouble gets no line of a good week, a thriving one none of a bad week, and only one in trouble is written about at the office");
        Check(goneSilent, "rivals seen (honest): nothing is written about a tycoon taken over or an owner defeated, from that day on");
        // the lines themselves carry no figure: every key they can use is free of the worth, the takings and the trend
        Dictionary<string, string> english = LoadWords(System.IO.Path.Combine(DesignLocales, "en.json"));
        foreach (string[] row in SeenCatalogue.PlaceLines)
        {
            foreach (string key in row.Where(k => k.Length > 0))
            {
                figures &= english != null && english.TryGetValue(key, out string text) && !Regex.IsMatch(text, @"\{(worth|figure|takings|income|trend|amount|week)\}") && !Regex.IsMatch(text, @"\d");
            }
        }
        foreach (string key in new[] { SeenCatalogue.NewCarLine, SeenCatalogue.CarDownLine, SeenCatalogue.PlayerCarLine, SeenCatalogue.PlayerPlaceLine })
        {
            figures &= english != null && english.TryGetValue(key, out string text) && !Regex.IsMatch(text, @"\{(worth|figure|takings|income|trend|amount|week)\}") && !Regex.IsMatch(text, @"\d");
        }
        Check(figures, "rivals seen (honest): no sighting's line has a place for a worth, a week's takings or any number: it hints, and the figures stay where the player already reads them");
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
        var f = new SeenFile { Seed = 8, Day = 20, Hour = -1 };
        f.Notes.Add(new SeenNote { Day = 19, Hour = 12, Kind = SeenNoteKind.Place, Spot = SeenSpot.Lunch, Form = SeenForm.Steady, Key = "ga", Name = "A", Place = "P", District = "D" });
        f.Notes.Add(new SeenNote { Day = 20, Hour = 9, Kind = SeenNoteKind.NewCar, Form = SeenForm.Thriving, Key = "gb", Name = "B", Car = "x", Old = "y" });
        f.Notes.Add(new SeenNote { Day = 20, Hour = 12, Kind = SeenNoteKind.Place, Spot = SeenSpot.Lunch, Form = SeenForm.Steady, Key = "gc", Name = "C", Place = "P", District = "D" });
        f.Notes.Add(new SeenNote { Day = 20, Hour = 19, Kind = SeenNoteKind.Place, Spot = SeenSpot.Dinner, Form = SeenForm.Steady, Key = "gd", Name = "D", Place = "P", District = "D" });
        var sent = new List<string>();
        Action<SeenNote> post = n => sent.Add(n.Key);
        int early = SeenDay.Hour(f, 20, 8, post);
        Check(early == 0 && sent.Count == 0 && f.Notes[0].Posted && !f.Notes[1].Posted, "rivals seen: before its hour nothing goes out, and a sighting of a day that is over is marked without being sent");
        int nine = SeenDay.Hour(f, 20, 9, post);
        int again = SeenDay.Hour(f, 20, 9, post);
        Check(nine == 1 && again == 0 && sent.Count == 1 && sent[0] == "gb", "rivals seen: a sighting goes out at its hour, once");
        // a save and a load in the middle of the day
        SeenFile loaded = SeenStore.Read(SeenStore.Write(f), out int _);
        int sameHour = SeenDay.Hour(loaded, 20, 9, post);
        int noon = SeenDay.Hour(loaded, 20, 13, post);
        int evening = SeenDay.Hour(loaded, 20, 23, post);
        Check(sameHour == 0 && noon == 1 && evening == 1 && sent.Count == 3 && sent[1] == "gc" && sent[2] == "gd" && loaded.Notes.All(n => n.Posted),
            "rivals seen: after a save and a load in the middle of the day nothing goes out twice and what was still to come does");
        Check(SeenDay.Hour(loaded, 21, 9, post) == 0 && SeenDay.Hour(null, 20, 9, post) == 0, "rivals seen: an hour of a day whose pass has not run sends nothing");
        var many = new SeenFile { Seed = 8, Day = 5, Hour = -1 };
        for (int i = 0; i < 6; i++)
        {
            many.Notes.Add(new SeenNote { Day = 5, Hour = 10, Kind = SeenNoteKind.Place, Spot = SeenSpot.Lunch, Form = SeenForm.Steady, Key = "g" + i, Name = "N", Place = "P", District = "D" });
        }
        int burst = SeenDay.Hour(many, 5, 10, post) + SeenDay.Hour(many, 5, 11, post) + SeenDay.Hour(many, 5, 23, post);
        Check(burst == SeenCatalogue.PostsMost && SeenCatalogue.PostsMost == 3, "rivals seen: three posts a day at most, whatever the file holds");
        int quiet = SeenDay.Hour(new SeenFile { Seed = 8, Day = 5, Hour = -1, Notes = { new SeenNote { Day = 5, Hour = 3, Key = "ga", Name = "A" } } }, 5, 4, null);
        Check(quiet == 1, "rivals seen: with Flare away a sighting's hour still passes, and nothing waits to flood it later");
    }

    // ---- the player, seen in turn

    private static void SeenPlayerChecks()
    {
        var f = new SeenFile { Seed = 606 };
        int lines = 0, lastDay = -1000, least = int.MaxValue;
        bool named = true;
        for (int day = 1; day <= 1000; day++)
        {
            for (int hour = 8; hour < 22; hour++)
            {
                SeenNote n = SeenDay.Player(f, day, hour, true, "Sam Player", 4, "ba:vehicletype_car09", null, null);
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
                silent &= SeenDay.Player(g, day, hour, false, "Sam Player", 5, "ba:vehicletype_car11", "Cafe", "D") == null;       // no business of their own
                silent &= SeenDay.Player(g, day, hour, true, "Sam Player", 2, "ba:vehicletype_car05", null, null) == null;         // a car under the bar
                silent &= SeenDay.Player(g, day, hour, true, "Sam Player", -1, null, null, null) == null;                           // on foot in the street
                silent &= SeenDay.Player(g, day, hour, true, string.Empty, 5, "ba:vehicletype_car11", null, null) == null;         // nobody
                silent &= SeenDay.Player(g, day, hour, true, "Sam Player", -1, null, "Cafe", string.Empty) == null;                // a place nowhere
                silent &= SeenDay.Player(null, day, hour, true, "Sam Player", 5, "ba:vehicletype_car11", null, null) == null;
            }
        }
        Check(silent && g.Notes.Count == 0 && SeenCatalogue.PlayerCarRung == 3, "rivals seen: nobody without a business, in a modest car, on foot or without a name is written about");
        SeenNote table = null;
        for (int hour = 0; hour < 24 * 60 && table == null; hour++)
        {
            table = SeenDay.Player(g, 300 + hour / 24, hour % 24, true, "Sam Player", 5, "ba:vehicletype_car11", "Corner Cafe", "ba:neighborhood_midtown");
        }
        Check(table != null && table.Kind == SeenNoteKind.PlayerPlace && table.Place == "Corner Cafe" && table.District == "ba:neighborhood_midtown" && table.Car.Length == 0
              && SeenDay.LineKey(table) == SeenCatalogue.PlayerPlaceLine, "rivals seen: at a table the line names the eating place the player is really at");
    }

    // ---- a greeting

    private static void SeenGreetChecks()
    {
        SocialWorth first = SocialRules.Chat(true, true, false, true, true);
        int worth = (int)Math.Round(first.Familiarity * 10f);
        var r = new SeenRecord { Key = "ga" };
        int one = SeenRules.Greet(r, 5, true);
        Check(one == worth && worth == 45 && r.Known == 45 && r.GreetDay == 5 && r.Greets == 1, "rivals seen: the day's first hello is worth what the social life's own rule gives a first talk face to face, 4.5 points");
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
        Check(r.Known == SeenCatalogue.GreetWarmMost * 10 && SeenRules.Greet(r, 601, true) == 0 && r.Known == 1000, "rivals seen: how well a rival knows the player stops at the social life's own top of 100");
        Check(SeenRules.KnowsFrom == 300 && SeenRules.KnowsFrom == (int)(SocialRules.FriendFamiliarity * 10f), "rivals seen: a rival says where they will be next from the social life's own bar for a friend, 30 points");
        Check(SeenRules.Greet(null, 1, true) == 0, "rivals seen: nobody greets nobody");
        Check(SeenRules.HelloPick(3, 7, 9) >= 0 && SeenRules.HelloPick(3, 7, 9) <= 1 && SeenRules.HelloPick(3, 7, 9) == SeenRules.HelloPick(3, 7, 9), "rivals seen: a rival has two ways of saying hello, the same one all day");
        string world = ModSource(@"RivalsSeen\SeenDirector.World.cs");
        Check(world.Contains("SeenRules.Greet(r, day, true)") && CountOfText(world, "SeenRules.Greet(") == 1 && !ModSource(@"RivalsSeen\SeenDialog.cs").Contains("Known +")
              && !ModSource(@"RivalsSeen\SeenDialog.cs").Contains("Greet("), "rivals seen: the greeting is counted in one place, before the talk opens, and nothing said in the talk earns anything");
    }

    // ---- the save

    private static void SeenStoreChecks()
    {
        var f = new SeenFile { Seed = 123456789, Day = 77, Hour = 13, PlayerDay = 70 };
        f.Records.Add(new SeenRecord
        {
            Key = "gQ0x|;^=\"id", Name = "Zoë \"The|Fox\"; ^Vane", Figure = 9000000000L, Before = 8500000000L, Shops = 7, ShopsBefore = 6, MarkDay = 71, Form = SeenForm.Thriving, Tier = 4,
            Rung = 5, Car = "ba:vehicletype_anselmoaf90", CarDay = 60, Known = 455, GreetDay = 76, Greets = 12, MetDay = 76
        });
        f.Records.Add(new SeenRecord { Key = "t1" });
        f.Notes.Add(new SeenNote
        {
            Day = 77, Hour = 12, Kind = SeenNoteKind.Place, Spot = SeenSpot.Lunch, Form = SeenForm.Trouble, Key = "gQ0x|;^=\"id", Name = "Zoë", Firm = "Vane & Co; Ltd", Place = "Café 北京|1",
            District = "ba:neighborhood_midtown", Car = "ba:vehicletype_a", Old = "ba:vehicletype_b", Posted = true
        });
        f.Notes.Add(new SeenNote { Day = 76, Hour = 9, Kind = SeenNoteKind.PlayerCar, Name = "Sam", Car = "ba:vehicletype_c" });
        string line = SeenStore.Write(f);
        SeenFile g = SeenStore.Read(line, out int version);
        SeenRecord a = g != null && g.Records.Count == 2 ? g.Records[0] : null;
        SeenNote n = g != null && g.Notes.Count == 2 ? g.Notes[0] : null;
        Check(version == SeenStore.Version && g != null && g.Seed == 123456789 && g.Day == 77 && g.Hour == 13 && g.PlayerDay == 70, "rivals seen save: the header comes back");
        Check(a != null && a.Key == f.Records[0].Key && a.Name == f.Records[0].Name && a.Figure == 9000000000L && a.Before == 8500000000L && a.Shops == 7 && a.ShopsBefore == 6 && a.MarkDay == 71
              && a.Form == SeenForm.Thriving && a.Tier == 4 && a.Rung == 5 && a.Car == "ba:vehicletype_anselmoaf90" && a.CarDay == 60 && a.Known == 455 && a.GreetDay == 76 && a.Greets == 12
              && a.MetDay == 76, "rivals seen save: a rival's record comes back whole, whatever characters their name and key hold");
        Check(g != null && g.Records.Count == 2 && g.Records[1].Key == "t1" && g.Records[1].MarkDay == -1 && g.Records[1].Rung == -1 && g.Records[1].Car.Length == 0 && g.Records[1].ShopsBefore == 0,
            "rivals seen save: a record never marked comes back unmarked");
        Check(n != null && n.Day == 77 && n.Hour == 12 && n.Kind == SeenNoteKind.Place && n.Spot == SeenSpot.Lunch && n.Form == SeenForm.Trouble && n.Key == f.Notes[0].Key && n.Name == "Zoë"
              && n.Firm == "Vane & Co; Ltd" && n.Place == "Café 北京|1" && n.District == "ba:neighborhood_midtown" && n.Car == "ba:vehicletype_a" && n.Old == "ba:vehicletype_b" && n.Posted
              && g.Notes[1].Kind == SeenNoteKind.PlayerCar && !g.Notes[1].Posted && g.Notes[1].Key.Length == 0, "rivals seen save: a sighting comes back whole");
        Check(SeenStore.Write(g) == line, "rivals seen save: written again it is the same line");
        Check(SeenStore.Key == "reallife:rivals_seen" && SeenStore.Load(new Dictionary<string, string>(), out int none) == null && none == 0 && SeenStore.Load(null, out none) == null
              && SeenStore.Read(string.Empty, out none) == null && SeenStore.Read("RV|1|2", out none) == null && SeenStore.Read("v=x^h:1", out none) == null,
            "rivals seen save: an old save has no line and loads with nothing in it, and a line that is not this feature's is not read");
        // a line from a newer version is read as far as it goes
        string newer = "v=9^h:5|40|7|33|99|future^r:ga|Ann|100|90|3|3|36|2|1|2|ba%3Acar|30|45|39|4|39|extra|more;gb|Bob^n:40|12|0|2|1|0|ga|Ann|Firm|Place|D|car|old|extra;40|9|77|2|1|0|gz|Zed;40|9|0|55|1|0|gz|Zed"
                       + "^z:something|new^q";
        SeenFile later = SeenStore.Read(newer, out int nine);
        Check(nine == 9 && later != null && later.Seed == 5 && later.Day == 40 && later.Hour == 7 && later.PlayerDay == 33 && later.Records.Count == 2 && later.Records[0].Car == "ba:car"
              && later.Records[0].Known == 45 && later.Records[1].Key == "gb" && later.Records[1].MarkDay == -1 && later.Notes.Count == 1 && later.Notes[0].Place == "Place",
            "rivals seen save: a newer version's line is read as far as it goes: fields past the end, a part with a new letter and a sighting of an unknown kind or place are left out");
        SeenFile bad = SeenStore.Read("v=1^h:x|y|99|z^r:ga|A|-5|-9|-2|-7|-4|9|9|9||x|99999|-8|-3|-2;ga|twice;|nokey^n:1|99|0|2|1|1;short", out int _);
        Check(bad != null && bad.Seed == 0 && bad.Day == -1 && bad.Hour == 23 && bad.Records.Count == 1 && bad.Records[0].Figure == 0 && bad.Records[0].Shops == 0 && bad.Records[0].MarkDay == -1
              && bad.Records[0].Form == SeenForm.Thriving && bad.Records[0].Tier == 4 && bad.Records[0].Rung == 5 && bad.Records[0].Known == 1000 && bad.Records[0].Greets == 0
              && bad.Notes.Count == 1 && bad.Notes[0].Hour == 23, "rivals seen save: every figure of a damaged line is put inside its bounds, a rival comes once, and a record with no key is dropped");
        var full = new SeenFile();
        var text = new System.Text.StringBuilder("v=1^h:1|1|1|1^n:");
        for (int i = 0; i < 60; i++)
        {
            text.Append(i > 0 ? ";" : string.Empty).Append("1|1|0|2|1|0|g|n");
        }
        Check(SeenStore.Read(text.ToString(), out int _).Notes.Count == SeenCatalogue.NotesKept && full.Notes.Count == 0, "rivals seen save: a line with too many sightings is read up to the bound");
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
        Check(businesses.Profile.FactLabels.Length == 3 && businesses.Profile.FactLabels[0] == "Weekly Income" && businesses.Profile.FactLabels[1] == "Drives" && businesses.Profile.FactValues.Length == 3,
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
        Check(screen.Contains("seenBy.PageLines(\"g\" + d.rivalId") && screen.Contains("seenBy.FoundShort(\"g\" + d.rivalId)") && screen.Contains("you ? null : RivalsSeen.SeenDirector.Instance"),
            "rivals seen page: the Rivals app's screen asks the feature for each rival by the game's own id, never for the player");
        string page = ModSource(@"Rivals\RivalPage.cs");
        Check(page.Contains("RivalBridges.SeenLine != null ? RivalBridges.SeenLine(who) : null") && page.Contains("box.Line(seen)"), "rivals seen page: a tycoon's panel in the Finance app shows their line");
        string links = ModSource(@"Wiring\Wiring.SeenLinks.cs");
        Check(links.Contains("RivalBridges.SeenLine = tycoon =>") && links.Contains("SeenDirector.Instance.TycoonLine(tycoon)") && links.Contains("RivalBridges.SeenLine = null;")
              && links.Contains("SeenBridges.Post = (words, hour, minute) => FlareDirector.Instance != null && FlareDirector.Instance.Society(words, hour, minute)")
              && links.Contains("News.Extra = Paper;") && links.Contains("News.Extra = _paperBefore;") && links.Contains("SeenDirector.Instance?.Stories(day, into)"),
            "rivals seen page: the wiring gives the tycoons' line, Flare's account and the paper, and takes each back");
        string options = ModSource(@"RivalsSeen\SeenGuide.cs");
        Check(options.Contains("AddToggle(Settings.LivingRivalsSeenId, \"reallife_seen_enabled\", Settings.LivingRivalsSeen, v => { Settings.LivingRivalsSeen = v; })")
              && options.Contains("AddButton(\"reallife_seen_test_btn_where\", () => SeenDirector.Instance?.TestWhere())")
              && options.Contains("AddButton(\"reallife_seen_test_btn_here\", () => SeenDirector.Instance?.TestHere())")
              && options.Contains("AddButton(\"reallife_seen_test_btn_sighting\", () => SeenDirector.Instance?.TestSighting())")
              && options.Contains("AddButton(\"reallife_seen_test_btn_back\", () => SeenDirector.Instance?.TestBack())"), "rivals seen page: the option and each of the four test entries reach their calls");
        string world = ModSource(@"RivalsSeen\SeenDirector.World.cs");
        Check(world.Contains("_onHello = _door.Press(Hello);") && world.Contains("panel.Button(0, \"reallife_seen_btn_hello\", _onHello, true, true);") && world.Contains("SeenDialog.Open(new SeenTalk"),
            "rivals seen page: the panel's one button says hello, which opens the talk");
        string dialog = ModSource(@"RivalsSeen\SeenDialog.cs");
        Check(dialog.Contains("entry.OnConfirm = () => Line(SeenCatalogue.BusinessTalk[Form], true);") && dialog.Contains("entry.OnSecondOption = () => Line(SeenCatalogue.CarTalk[Form], true);")
              && dialog.Contains("entry.OnCancel = Leave;") && dialog.Contains("SeenCatalogue.HelloKnown[Form]") && dialog.Contains("SeenCatalogue.Hello[Form]")
              && dialog.Contains("if (!string.IsNullOrEmpty(_talk.Car))"), "rivals seen page: the talk's buttons ask about the business and the car (only with a car on record) and leave, each by the rival's form");
        string flare = ModSource(@"Social\Flare\FlareDirector.Society.cs");
        Check(flare.Contains("Author = FlareAuthor.Ledger, Source = FlareSource.Headline") && flare.Contains("|| Following ||") && !flare.Contains("enum "),
            "rivals seen page: a sighting on Flare is a post of the City Ledger's own account, as its headlines are: no new author, source or field, and never while following another player's feed");
    }

    // ---- the sources

    private static void SeenSourceChecks()
    {
        string folder = System.IO.Path.GetDirectoryName(ModPath(@"RivalsSeen\SeenDirector.cs"));
        string[] files = Directory.GetFiles(folder, "*.cs");
        Check(files.Length >= 13, "rivals seen sources: the feature's files are found (" + files.Length + ")");
        int randoms = 0;
        foreach (string file in files)
        {
            string text = File.ReadAllText(file);
            string name = System.IO.Path.GetFileName(file);
            Check(!text.Contains("Update()") && !text.Contains("FindObjectsByType") && !text.Contains("FindObjectOfType") && !text.Contains("OverlapSphere"),
                "rivals seen sources: " + name + " has no per-frame code, no scene search and no physics query of its own");
            Check(!text.Contains("TODO") && !text.Contains("NotImplemented") && !text.Contains("ProjectSettings"), "rivals seen sources: " + name + " has no stub and no note of work left");
            Check(!text.Contains("GetHashCode") && !text.Contains("System.Random") && !text.Contains("DateTime.Now") && !text.Contains("Environment."),
                "rivals seen sources: " + name + " draws nothing from the machine: no runtime hash, no clock, no unseeded generator");
            Check(!text.Contains("AppearanceSetter") && !text.Contains("CreatePrefab") && !text.Contains("Dress(") && !text.Contains("Instantiate(") && !text.Contains("AddComponent<Animator")
                  && !text.Contains("BonePose") && !text.Contains("HairPhysics"), "rivals seen sources: " + name + " makes no character, look, body or animation of its own");
            Check(!text.Contains("GameManager.ChangeMoney") && !text.Contains("BizBooks.Book") && !text.Contains("ChangeMoneySafe"), "rivals seen sources: " + name + " moves no money");
            randoms += CountOfText(text, "UnityEngine.Random");
            bool gameFree = name == "SeenRules.cs" || name == "SeenDay.cs" || name == "SeenModel.cs" || name == "SeenCatalogue.cs" || name == "SeenStore.cs";
            Check(!gameFree || (!text.Contains("UnityEngine") && !text.Contains("BigAmbitions") && !text.Contains("SaveGameManager")), "rivals seen sources: " + name + " is game-free where it must be");
        }
        Check(randoms == 1 && ModSource(@"RivalsSeen\SeenDirector.cs").Contains("seed != 0 ? seed : UnityEngine.Random.Range(1, int.MaxValue)"),
            "rivals seen sources: the one draw of the engine's generator is the save's seed, made once and saved, and only where the rivals have none to give");
        string dir = ModSource(@"RivalsSeen\SeenDirector.cs");
        Check(dir.Contains("StartCoroutine(Guard.Routine(Tick()))") && dir.Contains("new WaitForSecondsRealtime(1f)") && dir.Contains("_broken = true;"),
            "rivals seen sources: the timer is a guarded coroutine, once a second, and a fault switches the feature off for the session");
        int wanted = dir.IndexOf("if (!Wanted)", StringComparison.Ordinal), load = dir.IndexOf("Load();", StringComparison.Ordinal), pass = dir.IndexOf("Pass(day);", StringComparison.Ordinal);
        Check(wanted > 0 && wanted < load && load < pass && dir.Contains("SeenRules.Runs(Settings.LivingRivalsSeen, Settings.LivingPopulation, GoingPublic.InSession)")
              && dir.Contains("ClearWorld(GoingPublic.InSession ? \"a Going Public session\" : \"switched off\");"),
            "rivals seen sources: with the option off, the living economy off or a session on, the look returns before anything is read, written or put out, and whoever stands is taken away");
        Check(dir.Contains("SeenFile f = File;") && dir.Contains("public SeenFile File => Running ? _file : null;"), "rivals seen sources: the paper and the pages get nothing while the feature does not run");
        string world = ModSource(@"RivalsSeen\SeenDirector.World.cs");
        Check(world.Contains("city.People.Take(CityLook.Civilian, at,") && world.Contains("city.People.Release(s.Person)") && world.Contains("SeenRules.MayPut(_outCount, city.People.Free)")
              && world.Contains("new Standing[SeenCatalogue.WorldMost]") && world.Contains("!SeenRules.InRing(CitySpots.Flat(venue.transform.position - origin))")
              && world.Contains("(!test && CitySpots.InView(at))"),
            "rivals seen sources: a rival in the world is one of the living city's own people as that pool dresses them, under the cap, near the player and put out only out of view");
        Check(world.Contains("BuildingManager.IsInsideBuilding || uis.timeMachine.isRunning") && world.Contains("ClearWorld(\"the street is gone\")") && world.Contains("_nextLook = now + SeenCatalogue.LookSeconds;"),
            "rivals seen sources: indoors and on a time skip everybody goes, and who is near is looked at every few seconds, never per frame");
        string game = ModSource(@"RivalsSeen\SeenGame.cs");
        Check(game.Contains("TrafficTools.Load(prefab, Lane[i])") && game.Contains("!TrafficTools.Ready") && game.Contains("!car.gameObject.activeInHierarchy") && game.Contains("CitySpots.InView(at)")
              && !game.Contains("SetVehicleIgnored") && !game.Contains("isKinematic"), "rivals seen sources: their car is an idle car of the traffic pool put on a lane out of view and left to the game's traffic, never held");
        Check(game.Contains("all.Sort(ById);") && game.Contains("_eating.Sort(ByDistrictAndRent);") && game.Contains("_models.Sort(ByPrice);") && game.Contains("GameDlc.Open(")
              && game.Contains("CityCheck.Here(Hamptons)"), "rivals seen sources: the game's rivals, eating places and cars are read in an order of their own, never a dictionary's, and nothing a DLC keeps is picked");
        Check(CountOfText(ModSource(@"Wiring\Wiring.cs"), "SeenLinks") == 2, "rivals seen sources: the wiring connects and disconnects the feature");
        Check(ModSource(@"Meta\FeatureList.cs").Contains("RivalsSeen.SeenGuide.Register()") && ModSource(@"Core\FeatureOwners.cs").Contains("\"RealLifeOverhaul.RivalsSeen\", \"rivals_seen\"")
              && ModSource(@"Meta\OptionsTree.cs").Contains("S(\"rivalsseen\").For(\"rivals_seen\").Opt(\"living_rivals_seen\", \"living_population\")")
              && ModSource(@"Meta\FeatureCities.cs").Contains("new Line(\"rivals_seen\"") && ModSource(@"Core\Settings.cs").Contains("public static bool LivingRivalsSeen = true;")
              && ModSource(@"Core\Settings.cs").Contains("LivingRivalsSeen = Bool(modId, LivingRivalsSeenId, LivingRivalsSeen);"),
            "rivals seen sources: the feature has its option (on, under the living economy), its Guide page, its owner and its place in the cities list");
        string tycoons = ModSource(@"Wiring\Wiring.SeenLinks.cs");
        Check(tycoons.Contains("rivals.Running && !rivals.Resting ? rivals.File : null") && tycoons.Contains("t.ActiveDay < 0") && tycoons.Contains("Gone = !t.Active")
              && tycoons.Contains("Pressed = day < t.SlowUntil || day < t.BarredUntil"), "rivals seen sources: a tycoon still to come forward is not listed, one taken over is gone, and a counter that holds them back is read");
    }

    // ---- the texts

    private static void SeenTextChecks()
    {
        var keys = new SortedSet<string>(StringComparer.Ordinal);
        string[] sources = { @"RivalsSeen\SeenCatalogue.cs", @"RivalsSeen\SeenDirector.cs", @"RivalsSeen\SeenDirector.World.cs", @"RivalsSeen\SeenDirector.Tests.cs", @"RivalsSeen\SeenGuide.cs",
            @"RivalsSeen\SeenDialog.cs", @"RivalsSeen\SeenText.cs", @"RivalsSeen\SeenGame.cs" };
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
        Check(keys.Count >= 75, "rivals seen texts: its keys are found: " + keys.Count);
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
                Check(words.TryGetValue("reallife_seen_btn_hello", out string hello) && page.Contains("<b>" + hello + "</b>"), "rivals seen texts: the Guide names the greeting's button by its own text in " + code);
            }
        }
        string guide = ModSource(@"RivalsSeen\SeenGuide.cs");
        foreach (string fact in facts)
        {
            Check(guide.Contains(".Fact(\"" + fact.Trim('{', '}') + "\""), "rivals seen texts: the Guide's " + fact + " is filled from the rules");
        }
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
            string next = words != null && words.TryGetValue("reallife_changes_next", out string value) ? value : string.Empty;
            int mark = next.IndexOf(marks[i], StringComparison.Ordinal);
            string before = mark >= 0 ? next.Substring(0, mark) : next;
            if (i == 0)
            {
                Check(next.Contains(sentence) && next.IndexOf(sentence, StringComparison.Ordinal) < (mark < 0 ? int.MaxValue : mark), "rivals seen texts: What's new has the feature's line before the first fix mark in en");
            }
            else
            {
                Check(!next.Contains(sentence) && before.Contains(starts[i]) && before.Substring(before.IndexOf(starts[i], StringComparison.Ordinal)).Contains("Flare"),
                    "rivals seen texts: What's new has the feature's line, translated, before the first fix mark in " + SeenLanguages[i]);
            }
        }
    }
}
