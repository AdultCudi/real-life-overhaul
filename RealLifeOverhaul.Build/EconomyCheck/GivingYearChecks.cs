using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using RealLifeOverhaul.Giving;
using RealLifeOverhaul.Population;
using RealLifeOverhaul.Rivals;

// Giving, the parts that run over time (run with --giving): the tax year turning when the game's does and every gift of it deducted once, across many years, a language change, a
// save and a load, the option off across a year turn and a falling cap; the month's pass kept whole across a save, a load and the option off; the loans' months; the rivals' gifts
// from over their floor; a person's warmth shared by gifts and loans; the save line under a culture with decimal commas. Each drives the shipped rules (GivingYear, GivingMonth,
// GivingLoans, GivingRules, GivingStore) with a stand-in for the game, so a rule broken fails here.
internal static partial class Program
{
    // ---- a stand-in for the game's tax: its list of deductions, its daily run, its income

    private sealed class TaxGame
    {
        public const string Key = "reallife_giving_deduct";
        public int Days = 60, Day = 1;
        public bool KeyWorks = true;          // the game's text tables hold giving's key, so the statement translates it
        public string Language = "en";
        public GivingState S = new GivingState();
        public readonly List<KeyValuePair<string, long>> List = new List<KeyValuePair<string, long>>();   // the game's list: a line for each name
        public readonly Dictionary<int, long> TaxAt = new Dictionary<int, long>();    // the day the game worked a year's tax out -> every line of its list that day
        public readonly List<KeyValuePair<int, long>> Gifts = new List<KeyValuePair<int, long>>();   // (day, amount) of each gift to a charity
        public readonly long[] Sales = new long[2000];                               // the sales of each day (the game writes day d's summary on day d + 1)
        public long Claims;                                                          // everything given to the game's tax
        public int Skipped;                                                          // years whose clearing the game skipped

        public TaxGame(int days, int seed, long scale = 1)
        {
            Days = days;
            for (int d = 0; d < Sales.Length; d++)
            {
                Sales[d] = (2000 + GivingRules.Hash(seed, 21, d, 0) % 9000) * scale;
            }
        }

        public string Words => Language == "de" ? "Spenden an Wohltaetigkeit" : Language == "fr" ? "Dons de bienfaisance" : "Charitable giving";

        public string Name => KeyWorks ? Key : Words;

        public long Total() => List.Sum(l => l.Value);

        public long Holds() => List.Where(l => GivingYear.Ours(l.Key, Key, Words)).Sum(l => l.Value);

        public long Income(int from, int to)
        {
            long sum = 0;
            for (int d = Math.Max(0, from); d <= to && d < Sales.Length; d++)
            {
                sum += Sales[d];
            }
            return sum;
        }

        public void Add(long amount)
        {
            Claims += amount;
            for (int i = 0; i < List.Count; i++)
            {
                if (List[i].Key == Name)
                {
                    List[i] = new KeyValuePair<string, long>(Name, List[i].Value + amount);
                    return;
                }
            }
            List.Add(new KeyValuePair<string, long>(Name, amount));
        }

        // GameManager.NewDay: the day goes up, then TaxHelper.RunDaily works the tax out and clears the list on the day that divides by the year.
        public void NewDay(bool unpaid = false)
        {
            Day++;
            if (Day % Days == 0)
            {
                if (unpaid)
                {
                    Skipped++;
                    return;
                }
                TaxAt[Day] = Total();
                List.Clear();
            }
        }

        // The director's day: the year turns, then today's claim.
        public void Pass()
        {
            GivingYear.Turn(S, Day, Days);
            GivingYear.ClaimToday(S, Day, Income, Holds, Add);
        }

        // The director's gift to a charity: the year turns first, the gift is counted, then the claim is made at once.
        public void Give(long amount)
        {
            GivingYear.Turn(S, Day, Days);
            S.TaxGifts += amount;
            Gifts.Add(new KeyValuePair<int, long>(Day, amount));
            GivingYear.ClaimToday(S, Day, Income, Holds, Add);
        }

        // The gifts made in the year the game works out on a day (the days from the one before's clearing day to the day before this one).
        public long GiftsOfYearEndingAt(int end) => Gifts.Where(g => g.Key >= end - Days && g.Key < end).Sum(g => g.Value);

        // What the cap lets of them be deducted, on the income the game counts (the days after the clearing day to the day before) or the income giving could know (one day less).
        public long CapAt(int end, bool known) => GivingRules.DeductionCap(Income(Math.Max(0, end - Days) + 1, known ? end - 2 : end - 1));

        public void Save() => S = GivingStore.Read(GivingStore.Write(S), out _);
    }

    private static void GivingYearChecks()
    {
        // The year as the game keeps it: a list cleared on the day that divides by the year's length.
        Check(GivingYear.Start(1, 60) == 0 && GivingYear.Start(59, 60) == 0 && GivingYear.Start(60, 60) == 60 && GivingYear.Start(61, 60) == 60 && GivingYear.Start(119, 60) == 60
              && GivingYear.Start(120, 60) == 120 && GivingYear.Start(100, 30) == 90 && GivingYear.Start(365, 365) == 365, "giving year: the year starts on the day the game clears its list, a multiple of its length");
        Check(GivingYear.IncomeFrom(0) == 1 && GivingYear.IncomeFrom(60) == 61, "giving year: its income is the sales of the days after the clearing day, as the game sums them");
        Check(GivingYear.FromSaved(61, 60) == 60 && GivingYear.FromSaved(1, 60) == 0 && GivingYear.FromSaved(60, 60) == 60 && GivingYear.FromSaved(-1, 60) == -1 && GivingYear.FromSaved(0, 60) == 0,
            "giving year: a year start saved before the year turned on the game's day is read as the game's day");
        // The year turns on the clearing day itself, so a gift on that day is in the new year.
        var turn = new GivingState { PeriodStart = 0, TaxGifts = 900, TaxClaimed = 700 };
        Check(!GivingYear.Turn(turn, 59, 60) && turn.TaxGifts == 900 && GivingYear.Turn(turn, 60, 60) && turn.PeriodStart == 60 && turn.TaxGifts == 0 && turn.TaxClaimed == 0,
            "giving year: the sums start again on the clearing day and not a day later");
        var fresh = new GivingState();
        Check(fresh.PeriodStart == -1 && !GivingYear.Turn(fresh, 200, 60) && fresh.PeriodStart == 180, "giving year: the first look only notes the year");

        // Claims: held to the gifts and to the cap, never twice, never less than giving itself recorded, never taken back.
        var s = new GivingState { PeriodStart = 0, TaxGifts = 5000 };
        long first = GivingYear.Claim(s, 0, 310000);
        long again = GivingYear.Claim(s, 0, 310000);
        Check(first == 5000 && again == 0 && s.TaxClaimed == 5000, "giving tax: the first claim is the gifts, asking again claims nothing");
        Check(GivingYear.Claim(s, 0, 310000) == 0 && GivingYear.Claim(s, 5000, 310000) == 0, "giving tax: a game list that reads nothing (the name is in another language) does not make a second claim, giving's own record is the floor");
        s.TaxGifts = 9000;
        long more = GivingYear.Claim(s, 5000, 310000);
        Check(more == 4000 && s.TaxClaimed == 9000, "giving tax: a gift made later claims only itself");
        long fell = GivingYear.Claim(s, 9000, 1000);
        Check(fell == 0 && s.TaxClaimed == 9000 && GivingYear.Claimed(s, 0) == 9000, "giving tax: a cap that falls claims nothing and takes nothing back");
        var capped = new GivingState { PeriodStart = 0, TaxGifts = 50000 };
        Check(GivingYear.Claim(capped, 0, 310000) == 18600 && GivingYear.Claim(capped, 18600, 310000) == 0, "giving tax: the claim stops at 6% of the year's income");
        Check(GivingYear.Claim(new GivingState { PeriodStart = 0, TaxGifts = -5 }, 0, 100) == 0 && GivingYear.Claim(new GivingState { PeriodStart = 0, TaxGifts = 100 }, 0, -100) == 0,
            "giving tax: a claim is never negative");
        Check(GivingYear.Ours("reallife_giving_deduct", TaxGame.Key, "Charitable giving") && GivingYear.Ours("Charitable giving", TaxGame.Key, "Charitable giving")
              && !GivingYear.Ours("Spenden", TaxGame.Key, "Charitable giving") && !GivingYear.Ours("", TaxGame.Key, "") && !GivingYear.Ours(null, TaxGame.Key, ""),
            "giving tax: the game's list is read by giving's key and by the words in use");

        // A year with a gift on each of the last three days, the first two of the next and a rich year: every one of them is in the game's list when it works the tax out.
        var game = new TaxGame(60, 5, 50);
        for (int i = 0; i < 175; i++)
        {
            game.Pass();
            if (game.Day == 20 || game.Day >= 57 && game.Day <= 59 || game.Day == 60 || game.Day == 61 || game.Day >= 117 && game.Day <= 119)
            {
                game.Give(1000 + game.Day);
            }
            game.NewDay();
        }
        long y1 = game.GiftsOfYearEndingAt(60), y2 = game.GiftsOfYearEndingAt(120);
        Check(game.TaxAt[60] == y1 && y1 == 1020 + 1057 + 1058 + 1059, "giving tax: the gifts of the last three days of the year are in the game's list when it works the tax out (" + game.TaxAt[60] + " of " + y1 + ")");
        Check(game.TaxAt[120] == y2 && y2 == 1060 + 1061 + 1117 + 1118 + 1119, "giving tax: a gift on the clearing day and the day after are in the next year, with the last days of it (" + game.TaxAt[120] + " of " + y2 + ")");
        Check(game.TaxAt.Count == 2 && game.Claims == game.TaxAt[60] + game.TaxAt[120] + game.Total(), "giving tax: nothing is claimed twice, and nothing is left unclaimed");
        // The page's figures are the game's: the year's gifts so far and what the game will deduct of them.
        game.Pass();
        game.Give(777);
        game.NewDay();
        game.Pass();
        Check(game.S.TaxGifts == 777 && game.S.TaxGifts == game.GiftsOfYearEndingAt(180) && GivingYear.Claimed(game.S, game.Holds()) == game.Total() && game.Total() == 777,
            "giving tax: the page's figures for the year are what the game will deduct (" + game.S.TaxGifts + " given, " + game.Total() + " in the game's list)");

        // The cap binds on the income the game counts, never over it, and a gift above it waits for more income.
        var rich = new TaxGame(60, 9);
        for (int i = 0; i < 61; i++)
        {
            rich.Pass();
            if (rich.Day == 30 || rich.Day == 59)
            {
                rich.Give(400000);
            }
            rich.NewDay();
        }
        Check(rich.TaxAt[60] <= rich.CapAt(60, false) && rich.TaxAt[60] == rich.CapAt(60, true), "giving tax: the deduction is the cap on the income giving could know (" + rich.TaxAt[60] + " of a cap of " + rich.CapAt(60, false) + ")");

        // A language change: the year's claim is not made again, with the key the game translates and with the words.
        foreach (bool keyWorks in new[] { true, false })
        {
            var lang = new TaxGame(60, 3, 50) { KeyWorks = keyWorks };
            lang.Pass();
            lang.Give(5000);
            lang.NewDay();
            lang.Pass();
            lang.Language = "de";
            lang.NewDay();
            lang.Pass();
            lang.Give(1000);
            lang.Language = "fr";
            lang.NewDay();
            lang.Pass();
            for (int i = 0; i < 40; i++)
            {
                lang.NewDay();
                lang.Pass();
            }
            Check(lang.Total() == 6000 && lang.Claims == 6000, "giving tax: switching language claims nothing twice (" + (keyWorks ? "the game translates giving's key" : "the words are all there is") + "): " + lang.Total() + " of 6000");
            for (int i = 0; i < 25; i++)
            {
                lang.NewDay();
                lang.Pass();
            }
            Check(lang.TaxAt[60] == 6000, "giving tax: the statement the game writes carries the year's gifts once (" + (keyWorks ? "key" : "words") + "): " + lang.TaxAt[60]);
        }

        // A save and a load of the claim, any day: the same outcome as without.
        var plain = new TaxGame(60, 4);
        var saved = new TaxGame(60, 4);
        for (int i = 0; i < 300; i++)
        {
            foreach (TaxGame g in new[] { plain, saved })
            {
                g.Pass();
                if (GivingRules.Hash(4, 31, g.Day, 0) % 6 == 0)
                {
                    g.Give(1 + GivingRules.Hash(4, 32, g.Day, 0) % 4000);
                }
                g.NewDay();
            }
            saved.Save();
        }
        Check(plain.Claims == saved.Claims && plain.TaxAt.All(kv => saved.TaxAt[kv.Key] == kv.Value) && plain.TaxAt.Count >= 4, "giving tax: a save and a load every day change no claim (" + plain.Claims + " and " + saved.Claims + ")");

        // The option off across a year turn: nothing of the old year is claimed into the new one, the new year's sums start at the clearing day, and gifts go on being deducted.
        var off = new TaxGame(60, 6);
        for (int i = 0; i < 50; i++)
        {
            off.Pass();
            if (off.Day == 10 || off.Day == 49)
            {
                off.Give(3000);
            }
            off.NewDay();
        }
        GivingState kept = GivingStore.Read(GivingStore.Write(off.S), out _);
        for (int i = 0; i < 30; i++)
        {
            off.NewDay();   // the option is off for thirty days, a year turn among them: no pass, no gift
        }
        off.S = kept;
        long beforeResume = off.Total();
        off.Pass();
        Check(off.TaxAt[60] > 0 && off.S.TaxGifts == 0 && off.S.TaxClaimed == 0 && off.Total() == beforeResume && off.S.PeriodStart == 60, "giving tax: the option back on after a year turn starts the new year and claims nothing of the old into it");
        off.Give(2000);
        for (int i = 0; i < 40; i++)
        {
            off.NewDay();
            off.Pass();
        }
        Check(off.TaxAt[120] == 2000, "giving tax: a gift after the option is back on is deducted in its year (" + off.TaxAt[120] + ")");

        // Many years at random: gifts, language changes, saves and loads, the option off for stretches. Never over the gifts of the year or the cap, and with every day played, exact.
        long worstOver = 0, years = 0, exact = 0;
        for (int seed = 1; seed <= 24; seed++)
        {
            int days = seed % 3 == 0 ? 30 : seed % 3 == 1 ? 60 : 97;
            var w = new TaxGame(days, seed) { KeyWorks = seed % 2 == 0 };
            bool offNow = false;
            int offUntil = -1;
            var missed = new HashSet<int>();   // the days the game works a tax out on, for the years a day of which the option was off
            for (int i = 0; i < days * 8; i++)
            {
                uint r = GivingRules.Hash(seed, 41, w.Day, 0);
                if (offUntil < 0 && r % 53 == 0)
                {
                    offNow = true;
                    offUntil = w.Day + 1 + (int)(r % (uint)days);
                }
                if (offNow && w.Day >= offUntil)
                {
                    offNow = false;
                    offUntil = -1;
                    w.S = GivingStore.Read(GivingStore.Write(w.S), out _);
                }
                if (offNow)
                {
                    missed.Add((w.Day / days + 1) * days);
                }
                if (!offNow)
                {
                    w.Pass();
                    if (r % 5 == 0)
                    {
                        w.Give(1 + GivingRules.Hash(seed, 42, w.Day, 0) % 30000);
                    }
                    if (r % 17 == 0)
                    {
                        w.Language = w.Language == "en" ? "de" : w.Language == "de" ? "fr" : "en";
                    }
                    if (r % 11 == 0)
                    {
                        w.Save();
                    }
                }
                w.NewDay();
            }
            foreach (KeyValuePair<int, long> t in w.TaxAt)
            {
                years++;
                long gifts = w.GiftsOfYearEndingAt(t.Key), most = Math.Min(gifts, w.CapAt(t.Key, false));
                worstOver = Math.Max(worstOver, t.Value - most);
                Check(t.Value >= 0 && t.Value <= most, "giving tax: year ending day " + t.Key + " (seed " + seed + "): " + t.Value + " is over the gifts of the year or the cap (" + most + ")");
                if (!missed.Contains(t.Key))
                {
                    exact++;
                    Check(t.Value == Math.Min(gifts, w.CapAt(t.Key, true)), "giving tax: every day played, year ending day " + t.Key + " (seed " + seed + ") deducts " + t.Value + " of " + Math.Min(gifts, w.CapAt(t.Key, true)));
                }
            }
        }
        Check(worstOver <= 0 && years >= 100 && exact >= 40, "giving tax: " + years + " tax years played through, " + exact + " of them every day exactly, none over its cap or its gifts");
    }

    // ---- the month's pass

    private sealed class MonthGame : IMonthWorld
    {
        public int Day = 100, Level = 1000;
        public readonly int[] Standing = new int[3], PayOuts = new int[3], Rivals = new int[GivingState.Rivals];
        public int NeighbourCalls;
        public readonly long[] Worth = { 50000000L, 0L, 3000000L };
        public readonly long[] Floor = { 40000000L, 60000000L, 3000000L };
        public long Left;     // what left the rivals
        public GivingState S;

        int IMonthWorld.Day => Day;

        int IMonthWorld.Level => Level;

        void IMonthWorld.PayStanding(StandingGift g, int month)
        {
            Standing[g.Charity]++;
            g.PaidTotal += GivingRules.At(g.Amount, Level);
        }

        long IMonthWorld.RivalWorth(int tycoon) => Worth[tycoon];

        long IMonthWorld.RivalPays(int tycoon, long gift)
        {
            long paid = GivingRules.RivalPaid(gift, Worth[tycoon], Floor[tycoon]);
            Worth[tycoon] -= paid;
            Left += paid;
            Rivals[tycoon]++;
            return paid;
        }

        void IMonthWorld.PayOut(Charity charity, int month)
        {
            PayOuts[(int)charity]++;
            S.Book(charity).LastMonth = month;
        }

        void IMonthWorld.Neighbours(int month)
        {
            NeighbourCalls++;
            S.NeighbourMonth = month;
        }
    }

    private static GivingState MonthState()
    {
        var s = new GivingState { Seed = 33, Month = 4, PeriodStart = 0 };
        s.Standing.Add(new StandingGift { Charity = 0, Amount = 250, Since = 20 });
        s.Standing.Add(new StandingGift { Charity = 2, Amount = 100, Since = 20 });
        s.Book(Charity.FoodBank).PlayerMonth = 900;
        s.MonthGiven = 900;
        return s;
    }

    private static void GivingMonthChecks()
    {
        // A whole pass, uninterrupted: every standing gift once, every rival with a worth once, every charity once, the neighbours once, and the month marked done at the end only.
        var s = MonthState();
        var w = new MonthGame { S = s };
        Check(GivingMonth.Begin(s, 5) && s.MonthStep == 0 && s.MonthNumber == 5 && s.Month == 4 && !GivingMonth.Begin(s, 6), "giving month: a pass begins for a month past the last done, and only one at a time");
        int steps = 0;
        while (s.MonthStep >= 0 && steps < 20)
        {
            GivingMonth.Step(s, w);
            steps++;
            Check(s.MonthStep < 0 ? s.Month == 5 : s.Month == 4, "giving month: the month is marked done when its last step is, not before (step " + steps + ")");
        }
        Check(steps == GivingMonth.Steps && w.Standing[0] == 1 && w.Standing[2] == 1 && w.Standing[1] == 0 && w.Rivals[0] == 1 && w.Rivals[1] == 0 && w.Rivals[2] == 1 && w.PayOuts.All(n => n == 1)
              && w.NeighbourCalls == 1 && s.MonthGiven == 0 && s.Book(Charity.FoodBank).PlayerMonth == 0, "giving month: a pass does each of its steps once and starts the counters again (" + steps + " steps)");
        Check(w.Left > 0 && s.Book(Charity.FoodBank).Pot + s.Book(Charity.Housing).Pot + s.Book(Charity.Training).Pot == w.Left && s.RivalBooks[0].Year + s.RivalBooks[2].Year == w.Left,
            "giving month: every dollar a rival gives leaves their worth and fills a pot, none of it the player's");

        // Interrupted at every step by a save and a load, or by the option off (the same line kept): the pass goes on at its step and nothing is done twice or dropped.
        for (int stop = 0; stop < GivingMonth.Steps; stop++)
        {
            foreach (bool throughLine in new[] { true, false })
            {
                var s2 = MonthState();
                var w2 = new MonthGame { S = s2 };
                GivingMonth.Begin(s2, 5);
                for (int i = 0; i < stop; i++)
                {
                    GivingMonth.Step(s2, w2);
                }
                if (throughLine)
                {
                    s2 = GivingStore.Read(GivingStore.Write(s2), out _);
                    w2.S = s2;
                }
                Check(s2.MonthStep == stop && s2.MonthNumber == 5, "giving month: a save at step " + stop + " keeps the step it was at (" + s2.MonthStep + ")");
                Check(!GivingMonth.Begin(s2, 6), "giving month: the pass under way is not begun again after a load at step " + stop);
                while (s2.MonthStep >= 0)
                {
                    GivingMonth.Step(s2, w2);
                }
                Check(s2.Month == 5 && w2.Standing[0] == 1 && w2.Standing[2] == 1 && w2.Rivals[0] == 1 && w2.Rivals[2] == 1 && w2.PayOuts.All(n => n == 1) && w2.NeighbourCalls == 1,
                    "giving month: the rest of the month runs after a save at step " + stop + ", each thing once (" + string.Join(",", w2.PayOuts) + ")");
            }
        }

        // A state put back to before a step that had run (the tests' put-back, a restored line) does not pay a standing gift, a rival, a pot or the neighbours a second time.
        var s3 = MonthState();
        var w3 = new MonthGame { S = s3 };
        GivingMonth.Begin(s3, 5);
        while (s3.MonthStep >= 0)
        {
            GivingMonth.Step(s3, w3);
        }
        s3.MonthStep = 0;
        s3.MonthNumber = 5;
        while (s3.MonthStep >= 0)
        {
            GivingMonth.Step(s3, w3);
        }
        Check(w3.Standing[0] == 1 && w3.Standing[2] == 1 && w3.Rivals[0] == 1 && w3.Rivals[2] == 1 && w3.PayOuts.All(n => n == 1) && w3.NeighbourCalls == 1,
            "giving month: a month whose steps run again pays no standing gift, rival, pot or neighbours twice");
        var s4 = MonthState();
        s4.Standing[0].Month = 5;   // a standing gift set during the pass has its first turn the month after
        var w4 = new MonthGame { S = s4 };
        GivingMonth.Begin(s4, 5);
        while (s4.MonthStep >= 0)
        {
            GivingMonth.Step(s4, w4);
        }
        Check(w4.Standing[0] == 0 && w4.Standing[2] == 1, "giving month: a standing gift set in the month's pass is first paid the month after");

        // Months go by: a pass for each month, one at a time; a long time away runs one pass for the month it is now.
        var s5 = MonthState();
        var w5 = new MonthGame { S = s5 };
        for (int month = 5; month <= 8; month++)
        {
            Check(GivingMonth.Begin(s5, month), "giving month: the pass for month " + month + " begins");
            while (s5.MonthStep >= 0)
            {
                GivingMonth.Step(s5, w5);
            }
        }
        Check(w5.Standing[0] == 4 && w5.Standing[2] == 4 && w5.PayOuts.All(n => n == 4) && w5.NeighbourCalls == 4 && w5.Rivals[0] == 4 && s5.Month == 8, "giving month: four months, four passes, each thing four times");
        Check(GivingMonth.Begin(s5, 30) && s5.MonthNumber == 30, "giving month: after a long time away one pass runs, for the month it is now");
        var first = new GivingState();
        Check(!GivingMonth.Begin(first, 7) && first.Month == 7 && first.MonthStep == -1, "giving month: the very first look only notes the month");

        // A rival at or under their floor gives nothing; over it, no more than the part over; and what the rivals raise a worth to is that floor.
        Check(GivingRules.RivalPaid(1000, 5000, 5000) == 0 && GivingRules.RivalPaid(1000, 4000, 5000) == 0 && GivingRules.RivalPaid(1000, 5400, 5000) == 400 && GivingRules.RivalPaid(1000, 9000, 5000) == 1000
              && GivingRules.RivalPaid(-5, 9000, 5000) == 0, "giving rivals: a gift is only from worth over the floor");
        var s6 = new GivingState { Seed = 33, Month = 4, PeriodStart = 0 };
        var w6 = new MonthGame { S = s6 };
        w6.Worth[0] = w6.Floor[0];
        w6.Worth[1] = 0;
        w6.Worth[2] = w6.Floor[2] + 100;
        GivingMonth.Begin(s6, 5);
        while (s6.MonthStep >= 0)
        {
            GivingMonth.Step(s6, w6);
        }
        Check(w6.Worth[0] == w6.Floor[0] && w6.Worth[2] == w6.Floor[2] && w6.Left == 100 && s6.RivalBooks[0].Total == 0 && s6.RivalBooks[2].Total == 100, "giving rivals: a tycoon at the floor gives nothing, one just over gives the part over (" + w6.Left + ")");
        var tycoon = new Tycoon { Index = 0, ActiveDay = 1 };
        long floor = RivalRules.At(tycoon.Def.StartWorth, 1000);
        tycoon.Worth = floor + 5000;
        long gave = GivingRules.RivalPaid(100000, tycoon.Worth, floor);
        tycoon.Worth -= gave;
        long grown = RivalRules.Grow(tycoon, floor, 0, 10, 1000);
        Check(gave == 5000 && tycoon.Worth == floor && grown >= floor && grown <= floor + floor / 100, "giving rivals: the floor is the worth the rivals' growth restores (" + floor + "), so what is given from over it stays given");
        tycoon.Worth = 0;
        Check(RivalRules.Grow(tycoon, floor, 0, 10, 1000) == floor, "giving rivals: a worth under the floor is raised to it by the rivals' day, so a gift from there would come back");
    }

    // ---- the population goes on from what a charity changed

    private static void GivingPopulationChecks()
    {
        // The population books a household's day and closes its month with relative changes to its savings and debt (SpendRules.Book, Roll) and raises a worker's skill by a step (LabourRules),
        // so what a charity put in a household or a person is the figure the next day starts from, not a value written over.
        PopulationState s = GivingCity(400, 13);
        var cut = new int[(int)Spend.Digital + 1];
        GivingPeople.PayFoodBank(s, 300000, 1000, out _);
        GivingPeople.PayHousing(s, 300000, 1000, out _);
        int taught = GivingPeople.PayTraining(s, 20000, 1000, out _);
        var debt = new int[s.Households.Count];
        long net = 0, moneyAfter = 0;
        var skill = new byte[s.People.Count];
        for (int h = 0; h < s.Households.Count; h++)
        {
            debt[h] = s.Households.Debt[h];
            net += (long)s.Households.Savings[h] - s.Households.Debt[h];
        }
        for (int p = 0; p < s.People.Count; p++)
        {
            skill[p] = s.People.Skill[p];
        }
        for (int h = 0; h < s.Households.Count; h++)
        {
            SpendRules.Book(s, h, default(DayIncome), cut, 0);
            SpendRules.Roll(s, h);
            moneyAfter += (long)s.Households.Savings[h] - s.Households.Debt[h];
        }
        bool debtHeld = true, skillHeld = true;
        for (int h = 0; h < s.Households.Count; h++)
        {
            debtHeld &= s.Households.Debt[h] <= debt[h];
        }
        for (int p = 0; p < s.People.Count; p++)
        {
            skillHeld &= s.People.Skill[p] == skill[p];
        }
        Check(taught > 0 && net == moneyAfter && debtHeld && skillHeld, "giving city: the population's day and month go on from what a charity changed: no debt paid off comes back, no skill is written over, no money appears or goes (" + net + " and " + moneyAfter + ")");
    }

    // ---- loans: the person who pays back, a person's warmth shared by gifts and loans

    private static PopulationState LoanCity(out int socialIndex, out int household)
    {
        PopulationState s = GivingCity(300, 5);
        socialIndex = 7;
        int p = 14;   // a person of household 7 (two a household)
        s.Links.Add(new PersonLink { Kind = LinkKind.Met, Key = "social:7", Person = s.People.Id[p], Slot = p });
        s.People.Flags[p] |= PersonFlags.Linked;
        household = s.People.Household[p];
        return s;
    }

    private static void GivingLoanTurnChecks()
    {
        PopulationState s = LoanCity(out int who, out int h);
        var st = new GivingState();
        long before = (long)s.Households.Savings[h] - s.Households.Debt[h];
        Check(GivingLoans.Check(st, s, who, 4000, 1000, out int p, out int hh) == GiveResult.Done && hh == h && p == 14, "giving loans: a resident the population holds can be lent to");
        Check(GivingLoans.Check(st, s, 99, 4000, 1000, out _, out _) == GiveResult.NoPerson && GivingLoans.Check(st, s, who, 0, 1000, out _, out _) == GiveResult.Nothing
              && GivingLoans.Check(st, s, who, 10001, 1000, out _, out _) == GiveResult.Limit && GivingLoans.Check(st, s, who, 20000, 2000, out _, out _) == GiveResult.Done,
            "giving loans: nobody the population does not hold, nothing for no amount, nothing over the most at today's prices");
        LoanOut loan = GivingLoans.Make(st, s, h, who, "A Name", 4000, 400, 5);
        Check(((long)s.Households.Savings[h] - s.Households.Debt[h]) - before == 4000 && loan.Remaining == 4000 && loan.Part == 667 && loan.Next == 405 && st.LoanOf(who) == loan,
            "giving loans: a loan lands in the household whole and is written down");
        Check(GivingLoans.Check(st, s, who, 100, 1000, out _, out _) == GiveResult.Limit, "giving loans: one loan at a time for a person");
        var many = new GivingState();
        for (int i = 0; i < GivingCatalogue.LoansMost; i++)
        {
            many.Loans.Add(new LoanOut { Person = 100 + i, Principal = 10, Remaining = 10 });
        }
        Check(GivingLoans.Check(many, s, who, 100, 1000, out _, out _) == GiveResult.Limit, "giving loans: eight open at the most");
        // Months go by: the person pays what the household can spare, never more than lent in all, and the household's net gain is never more than the loan.
        s.Households.Savings[h] = 6000;
        int day = 405, months = 0;
        long paidAll = 0;
        while (loan.State == LoanState.Open && months < 400)
        {
            LoanTurn turn = GivingLoans.Collect(loan, s, day, 5, out long took);
            paidAll += took;
            if (turn != LoanTurn.None)
            {
                s.Households.Savings[h] += 120;   // the household saves a little each month
                day += 5;
            }
            else
            {
                day++;
            }
            months++;
        }
        Check(loan.State == LoanState.Repaid && paidAll == 4000 && loan.Paid == 4000 && loan.Remaining == 0, "giving loans: a household that can pay a loan back pays all of it and not a dollar more (" + paidAll + ")");
        Check(GivingLoans.Collect(loan, s, day + 100, 5, out long after) == LoanTurn.None && after == 0, "giving loans: a closed loan asks for nothing");
        // A household with nothing to spare: three months of nothing and it is written off, with nothing booked.
        PopulationState poor = LoanCity(out int who2, out int h2);
        poor.Households.Savings[h2] = 0;
        var st2 = new GivingState();
        LoanOut bad = GivingLoans.Make(st2, poor, h2, who2, "Other", 1000, 400, 5);
        poor.Households.Savings[h2] = 0;
        LoanTurn last = LoanTurn.None;
        long gotBack = 0;
        for (int d = 405; d < 440 && bad.State == LoanState.Open; d += 5)
        {
            last = GivingLoans.Collect(bad, poor, d, 5, out long t);
            gotBack += t;
        }
        Check(bad.State == LoanState.WrittenOff && last == LoanTurn.WrittenOff && gotBack == 0 && bad.Misses == GivingCatalogue.LoanMisses && bad.Remaining == 1000, "giving loans: three months of nothing writes a loan off, nothing taken back");
        // Closed loans are trimmed to the most a page keeps.
        var trim = new GivingState();
        for (int i = 0; i < GivingCatalogue.LoanKept + 5; i++)
        {
            trim.Loans.Add(new LoanOut { Person = i, Principal = 10, State = LoanState.Repaid });
        }
        trim.Loans.Add(new LoanOut { Person = 99, Principal = 10, Remaining = 10 });
        GivingLoans.Trim(trim);
        Check(trim.Loans.Count == GivingCatalogue.LoanKept + 1 && trim.OpenLoans() == 1, "giving loans: the oldest closed loans are dropped and an open one never is");

        // Warmth: gifts and loans are counted in one window, so the same sum earns the same however it is split, a loan counts for its share, and a tiny loan again and again is no free warmth.
        var tiny = new PersonGiving { Person = 1 };
        int aSum = 0, tSum = 0;
        for (int i = 0; i < 40; i++)
        {
            GivingRules.PersonGain(tiny, GivingRules.LoanCounts(50), 100 + i / 4, true, out int a, out int t);
            aSum += a;
            tSum += t;
        }
        var once = new PersonGiving { Person = 2 };
        GivingRules.PersonGain(once, GivingRules.LoanCounts(50) * 40, 100, true, out int a1, out int t1);
        Check(aSum == a1 && tSum == t1 && aSum <= GivingCatalogue.AffectionMost * 10 && tSum <= GivingCatalogue.TrustMost * 10, "giving warmth: forty tiny loans in a window earn what one loan of their sum earns (" + aSum / 10f + " affection)");
        var gift = new PersonGiving { Person = 3 };
        GivingRules.PersonGain(gift, 2000, 100, true, out int ag, out int tg);
        var loanOnly = new PersonGiving { Person = 4 };
        GivingRules.PersonGain(loanOnly, GivingRules.LoanCounts(2000), 100, true, out int al, out int tl);
        Check(GivingRules.LoanCounts(2000) == 1000 && al < ag && tl < tg && al > 0, "giving warmth: a loan earns less than a gift of the same size (" + al / 10f + " against " + ag / 10f + ")");
        var both = new PersonGiving { Person = 5 };
        GivingRules.PersonGain(both, 2000, 100, true, out int ab, out int tb);
        GivingRules.PersonGain(both, GivingRules.LoanCounts(2000), 101, true, out int ab2, out int tb2);
        var sum = new PersonGiving { Person = 6 };
        GivingRules.PersonGain(sum, 3000, 100, true, out int as3, out int ts3);
        Check(ab + ab2 == as3 && tb + tb2 == ts3, "giving warmth: a gift and a loan to one person share the window");
        long loanRepeat = 0;
        var again2 = new PersonGiving { Person = 7 };
        for (int round = 0; round < 12; round++)
        {
            GivingRules.PersonGain(again2, GivingRules.LoanCounts(10000), 100 + round * 31, true, out int a, out _);
            loanRepeat += a;
        }
        Check(loanRepeat <= 12L * GivingCatalogue.AffectionMost * 10, "giving warmth: a loan every window earns the window's most and no more (" + loanRepeat / 10f + " in a year of windows)");
    }

    // ---- the save line under a culture with a decimal comma

    private static void GivingCultureChecks()
    {
        var s = new GivingState { Seed = 99, LastDay = 400, Month = 6, PeriodStart = 360, TaxGifts = 1234567, TaxClaimed = 1000001, MonthGiven = 12345, MonthStep = 3, MonthNumber = 7, NeighbourMoney = 1500000, NeighbourMonth = -1 };   // (a negative figure: a culture with a minus sign of its own would not read it back)
        s.Book(Charity.FoodBank).Pot = 4200123;
        s.Standing.Add(new StandingGift { Charity = 1, Amount = 250, Since = 300, PaidTotal = 15000, Month = 6 });
        s.Loans.Add(new LoanOut { Person = 3, Name = "Ana Ruiz, Jr.", Principal = 1200, Remaining = 800, Made = 380, Part = 200, Next = 410, Paid = 400 });
        s.PersonOf(3, true).Given = 3000;
        s.Notes.Add(new GivingNote { Day = 399, Charity = 1, Amount = 30000, Giver = -1 });
        CultureInfo was = CultureInfo.CurrentCulture, wasUi = CultureInfo.CurrentUICulture;
        string reference = null;
        int tried = 0;
        // Two made-up cultures with a decimal comma, a dot for groups and a minus sign of their own (they work where the machine holds no culture data), then the real ones there are.
        var commas = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        commas.NumberFormat.NumberDecimalSeparator = ",";
        commas.NumberFormat.NumberGroupSeparator = ".";
        commas.NumberFormat.NegativeSign = "−";
        var spaced = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        spaced.NumberFormat.NumberDecimalSeparator = ",";
        spaced.NumberFormat.NumberGroupSeparator = " ";
        spaced.NumberFormat.NegativeSign = "-";
        var cultures = new List<KeyValuePair<string, CultureInfo>> { new KeyValuePair<string, CultureInfo>("a decimal comma", commas), new KeyValuePair<string, CultureInfo>("a decimal comma and thin spaces", spaced) };
        foreach (string name in new[] { "de-DE", "fr-FR", "ru-RU", "sv-SE", "tr-TR", "pl-PL", "pt-BR", "nl-NL" })
        {
            try
            {
                cultures.Add(new KeyValuePair<string, CultureInfo>(name, new CultureInfo(name)));
            }
            catch (CultureNotFoundException)
            {
            }
        }
        try
        {
            foreach (KeyValuePair<string, CultureInfo> entry in cultures)
            {
                string name = entry.Key;
                CultureInfo culture = entry.Value;
                CultureInfo.CurrentCulture = culture;
                CultureInfo.CurrentUICulture = culture;
                string line = GivingStore.Write(s);
                GivingState back = GivingStore.Read(line, out int version);
                reference = reference ?? line;
                tried++;
                Check(line == reference && version == GivingStore.Version && back != null && GivingStore.Write(back) == line && back.TaxGifts == 1234567 && back.TaxClaimed == 1000001
                      && back.Book(Charity.FoodBank).Pot == 4200123 && back.Loans[0].Name == "Ana Ruiz, Jr." && back.MonthStep == 3 && back.MonthNumber == 7 && back.Standing[0].Month == 6
                      && back.NeighbourMoney == 1500000 && back.NeighbourMonth == -1, "giving save: the line is written and read the same under " + name + " (decimal comma, group marks and signs of its own)");
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = was;
            CultureInfo.CurrentUICulture = wasUi;
        }
        Check(tried >= 3, "giving save: the line was tried under " + tried + " cultures");
        // A line from before the month went by steps and the year turned on the game's day still loads: no pass under way, the standing gift has no month yet.
        GivingState old = GivingStore.Read("v=1^h:99|400|6|361|12000|7000|6|2500|1|2|390|5|800|4000|12|399^b:4200|0|0|0|0|0|0|0|0|0|-1|0|0^s:1|250|300|1|1500", out _);
        Check(old != null && old.MonthStep == -1 && old.MonthNumber == 0 && old.Standing[0].Month == -1 && old.PeriodStart == 361 && GivingYear.FromSaved(old.PeriodStart, 60) == 360 && old.TaxGifts == 12000,
            "giving save: a line from before this version loads, with no pass under way, and its year start is the game's day");
        GivingState hostile = GivingStore.Read("v=1^h:1|1|1|1|1|1|1|1|1|1|1|1|1|1|1|1|99|-4", out _);
        Check(hostile != null && hostile.MonthStep == -1 && hostile.MonthNumber == 0, "giving save: a pass step out of its range reads as no pass under way");
    }
}
