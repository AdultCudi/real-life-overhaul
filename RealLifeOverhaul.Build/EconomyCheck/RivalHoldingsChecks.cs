using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Rivals;
using RealLifeOverhaul.Ui;

// A player saw the rival in 19th place of 20 owning about 130 buildings. The game deals a new city's buildings to its rivals by the order of their
// ids and in fixed shares, and ranks them by weekly takings, so a rival's place and what it owns have nothing to do with each other. The rivals'
// holdings are brought to their place (RivalHoldingsRules): a curve of shares that rises with the place, dealt at once in a new game and moved gently in
// one under way, and a rival's fortune counts what it owns by one rule (RivalFortuneRules). Here: the curve, the pace, the new game's deal, a year of
// gentle days from the game's own deal, what is never touched, the fortune's parts, the sources and the English texts.
internal static partial class Program
{
    private static void RivalPlaceHoldings()
    {
        HoldingsCurve();
        HoldingsPace();
        HoldingsDeal();
        HoldingsYear();
        HoldingsPicks();
        HoldingsFortune();
        HoldingsKit();
        HoldingsSources();
        HoldingsWords();
    }

    // The game's own deal as a city of this many buildings gets it (CityGenerator.DistributeBuildingsToRivals): 24 per cent to four special rivals, half to
    // the first five others, the rest to the other ten, each group in equal chunks. The ranking by takings is not the order of ids: the places are the ids shuffled.
    private static HoldingsBook GameDeal(int total, int seed, int districts, int fixedCount, int listed)
    {
        const int Rivals = 19;
        var book = new HoldingsBook();
        book.Reset(Rivals, districts);
        var placeOfId = new int[Rivals];
        for (int i = 0; i < Rivals; i++)
        {
            placeOfId[i] = i;
        }
        var rng = new Random(seed);
        for (int i = Rivals - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (placeOfId[i], placeOfId[j]) = (placeOfId[j], placeOfId[i]);
        }
        int special = (int)Math.Round(total * 0.24), five = (int)Math.Round(total * 0.5), rest = Math.Max(1, total - special - five);
        for (int n = 0; n < total; n++)
        {
            int id = n < special ? n * 4 / Math.Max(1, special) : n < special + five ? 4 + (n - special) * 5 / Math.Max(1, five) : 9 + (n - special - five) * 10 / rest;
            long value = 60000L + rng.Next(0, 400000) + (rng.Next(0, 10) == 0 ? rng.Next(1000000, 9000000) : 0);
            book.Add(placeOfId[Math.Min(Rivals - 1, id)], value, n % districts, 0);
        }
        for (int i = 0; i < fixedCount; i++)
        {
            book.Add(HoldingsBook.Fixed, 90000L + i * 1000L, i % districts, 0);
        }
        for (int i = 0; i < listed; i++)
        {
            book.Add(HoldingsBook.Market, 70000L + i * 5000L, i % districts, 1);
        }
        return book;
    }

    private static void HoldingsCurve()
    {
        bool sums = true, falling = true, floor = true, inside = true;
        foreach (int rivals in new[] { 1, 2, 3, 5, 10, 19, 22, 30 })
        {
            foreach (int total in new[] { 0, 1, 7, 19, 38, 76, 77, 120, 500, 1200, 5000, 20000 })
            {
                var t = new int[rivals];
                RivalHoldingsRules.Targets(rivals, total, t);
                int sum = 0;
                for (int i = 0; i < rivals; i++)
                {
                    sum += t[i];
                    falling &= i == 0 || t[i] <= t[i - 1];
                    floor &= t[i] >= Math.Min(RivalHoldingsRules.Least, total / rivals);
                    inside &= t[i] >= 0 && t[i] <= total;
                }
                sums &= sum == total;
            }
        }
        Check(sums, "rivals' buildings: the shares of any number of rivals add up to the buildings they hold together, for 1 to 30 rivals and 0 to 20,000 buildings");
        Check(falling, "rivals' buildings: a place never holds more than the place above it");
        Check(floor && inside, "rivals' buildings: nobody holds fewer than the least (" + RivalHoldingsRules.Least + ") the city can give, and no share is negative or over the whole");
        var c = new int[19];
        RivalHoldingsRules.Targets(19, 1200, c);
        Check(c[18] >= 4 && c[18] <= 8 && c[0] > 25 * c[18] && c[0] < 1200 / 4,
            "rivals' buildings: of 1,200 buildings the last of 19 rivals holds a handful (" + c[18] + ") and the first many times more (" + c[0] + "), not a quarter of all");
        var big = new int[19];
        RivalHoldingsRules.Targets(19, 5000, big);
        Check(big[18] >= 4 && big[18] <= 12 && big[0] > 50 * big[18], "rivals' buildings: of 5,000 buildings, as the player's 130 for the last rival suggests, it holds " + big[18] + " and the first " + big[0]);
        var few = new int[19];
        RivalHoldingsRules.Targets(19, 30, few);
        int fewSum = 0;
        foreach (int x in few)
        {
            fewSum += x;
        }
        Check(fewSum == 30 && few[18] == 1 && few[0] > few[18], "rivals' buildings: with fewer buildings than the least for each rival, each still holds its share of the whole and the last holds one (" + few[0] + " and " + few[18] + ")");
        var same = new int[19];
        RivalHoldingsRules.Targets(19, 1200, same);
        Check(string.Join(",", same) == string.Join(",", c), "rivals' buildings: the same numbers give the same shares");
        long w0 = RivalHoldingsRules.Weight(0, 19), w18 = RivalHoldingsRules.Weight(18, 19);
        Check(w18 == 1 && w0 == 19L * 19L && RivalHoldingsRules.Weight(5, 19) == 14L * 14L && RivalHoldingsRules.Steepness == 2,
            "rivals' buildings: a place's weight is the places from it to the last, squared (" + w0 + " for the first, " + w18 + " for the last)");
    }

    private static void HoldingsPace()
    {
        bool ok = true;
        foreach (int day in new[] { 1, 2, 3, 4, 30 })
        {
            ok &= RivalHoldingsRules.Pace(RivalHoldingsRules.OptionOff, day, true) == RivalHoldingsPace.None;
        }
        Check(ok && RivalHoldingsRules.Pace(-1, 1, true) == RivalHoldingsPace.None && RivalHoldingsRules.Pace(9, 1, true) == RivalHoldingsPace.None,
            "rivals' buildings: with the option off the game's own deal stands on every day, and an unknown step is off");
        Check(RivalHoldingsRules.Pace(RivalHoldingsRules.OptionNewGames, 1, false) == RivalHoldingsPace.AtOnce
              && RivalHoldingsRules.Pace(RivalHoldingsRules.OptionNewGames, RivalHoldingsRules.NewGameDays, false) == RivalHoldingsPace.AtOnce
              && RivalHoldingsRules.Pace(RivalHoldingsRules.OptionAlways, 2, false) == RivalHoldingsPace.AtOnce,
            "rivals' buildings: a new game's first days are dealt at once");
        Check(RivalHoldingsRules.Pace(RivalHoldingsRules.OptionNewGames, RivalHoldingsRules.NewGameDays + 1, true) == RivalHoldingsPace.Gentle
              && RivalHoldingsRules.Pace(RivalHoldingsRules.OptionNewGames, 200, false) == RivalHoldingsPace.None
              && RivalHoldingsRules.Pace(RivalHoldingsRules.OptionAlways, 200, false) == RivalHoldingsPace.Gentle,
            "rivals' buildings: a game dealt at its start goes on gently, a save that was not is left alone unless the option says every game");
        Check(RivalHoldingsRules.Compare(500, "b", 400, "a") < 0 && RivalHoldingsRules.Compare(400, "a", 500, "b") > 0 && RivalHoldingsRules.Compare(400, "a", 400, "b") < 0
              && RivalHoldingsRules.Compare(400, "b", 400, "b") == 0, "rivals' buildings: the ranking is the dearer takings first and then the id, so a tie has one order");
        var data = new Dictionary<string, string>();
        Check(!RivalHoldingsStore.Read(data) && !RivalHoldingsStore.Read(null), "rivals' buildings: a save without the line was not dealt");
        RivalHoldingsStore.Write(data, true);
        bool dealt = RivalHoldingsStore.Read(data) && data.ContainsKey(RivalHoldingsStore.Key) && RivalHoldingsStore.Key.StartsWith("reallife:", StringComparison.Ordinal);
        RivalHoldingsStore.Write(data, false);
        Check(dealt && !RivalHoldingsStore.Read(data) && data.Count == 0, "rivals' buildings: the save's line is written for a dealt game under reallife: and taken away again");
    }

    private static void HoldingsDeal()
    {
        bool every = true, monotoneWorth = true, untouched = true, onceOnly = true, determinate = true, listsRight = true;
        foreach (int seed in new[] { 1, 2, 3, 17, 90 })
        {
            foreach (int total in new[] { 400, 1200, 2600 })
            {
                HoldingsBook book = GameDeal(total, seed, 10, 25, 30);
                int n = book.Count;
                var was = new int[n];
                Array.Copy(book.Owner, was, n);
                var wasValue = new long[n];
                Array.Copy(book.Value, wasValue, n);
                var before = new int[19];
                int owned = RivalHoldingsRules.Counts(book, before);
                int moved = RivalHoldingsRules.AtOnce(book);
                var after = new int[19];
                RivalHoldingsRules.Counts(book, after);
                var want = new int[19];
                RivalHoldingsRules.Targets(19, owned, want);
                every &= string.Join(",", after) == string.Join(",", want);
                for (int i = 0; i < n; i++)
                {
                    untouched &= was[i] >= 0 ? true : book.Owner[i] == was[i];   // a fixed or listed building is where it was
                    untouched &= book.Value[i] == wasValue[i];
                }
                var seenMoves = new HashSet<int>();
                int mismatched = 0;
                foreach (HoldingsMove m in book.Moves)
                {
                    onceOnly &= seenMoves.Add(m.Building);
                    listsRight &= was[m.Building] == m.From && book.Owner[m.Building] == m.To && m.From >= 0 && m.To >= 0 && m.From != m.To;
                    mismatched += was[m.Building] == book.Owner[m.Building] ? 1 : 0;
                }
                listsRight &= mismatched == 0 && moved == book.Moves.Count;
                determinate &= RivalHoldingsRules.AtOnce(book) == 0;
                // The worth follows the place: the top six hold much more than the bottom six, and the first more than the last.
                var worth = new long[19];
                for (int i = 0; i < n; i++)
                {
                    if (book.Owner[i] >= 0)
                    {
                        worth[book.Owner[i]] += book.Value[i];
                    }
                }
                long top = 0, bottom = 0;
                for (int p = 0; p < 6; p++)
                {
                    top += worth[p];
                    bottom += worth[18 - p];
                }
                monotoneWorth &= top > 10 * bottom && worth[0] > worth[18];
            }
        }
        Check(every, "rivals' buildings: a new city is dealt so that every rival holds exactly its share of the place it stands in");
        Check(untouched, "rivals' buildings: the new game's deal never moves a fixed building or one for sale, and never changes a value");
        Check(onceOnly && listsRight, "rivals' buildings: every move names a building once, from its owner to another rival, and the book is what the moves say");
        Check(determinate, "rivals' buildings: a city dealt is dealt, and a second pass moves nothing");
        Check(monotoneWorth, "rivals' buildings: the worth follows the place as the number does: the top six hold more than ten times the bottom six");
        HoldingsBook a = GameDeal(1200, 5, 10, 25, 30), b = GameDeal(1200, 5, 10, 25, 30);
        RivalHoldingsRules.AtOnce(a);
        RivalHoldingsRules.AtOnce(b);
        bool alike = a.Count == b.Count;
        for (int i = 0; i < a.Count && alike; i++)
        {
            alike &= a.Owner[i] == b.Owner[i];
        }
        Check(alike, "rivals' buildings: the same city gives the same deal");
        var one = new HoldingsBook();
        one.Reset(1, 1);
        one.Add(0, 100000L, 0, 0);
        Check(RivalHoldingsRules.AtOnce(one) == 0 && RivalHoldingsRules.Day(one, 10, 3, 3, out int s1, out int b1) == 0 && s1 == 0 && b1 == 0,
            "rivals' buildings: with one rival, or none, nothing is dealt or moved");
    }

    // A year of gentle days from the game's own deal: the most a week, nothing fixed ever, nothing bought that was for sale a short while or that is a district's last,
    // the distance to the shares falling, and the top place's holdings rising while the last's fall.
    private static void HoldingsYear()
    {
        const int Rivals = 19, Districts = 10;
        HoldingsBook book = GameDeal(1200, 11, Districts, 25, 33);
        int n = book.Count;
        var counts = new int[Rivals];
        var targets = new int[Rivals];
        int owned0 = RivalHoldingsRules.Counts(book, counts);
        RivalHoldingsRules.Targets(Rivals, owned0, targets);
        int start = RivalHoldingsRules.Distance(counts, targets, Rivals);
        int firstBefore = counts[0], lastBefore = counts[Rivals - 1];
        bool fixedStays = true, caps = true, young = true, floor = true, weekCaps = true;
        int weekSold = 0, weekBought = 0, previous = start, worstRise = 0;
        var listedDay = new Dictionary<int, int>();
        for (int day = 10; day < 10 + 365; day++)
        {
            var wasOwner = new int[n];
            Array.Copy(book.Owner, wasOwner, n);
            RivalHoldingsRules.Day(book, day, RivalHoldingsRules.SalesPerDay, RivalHoldingsRules.BuysPerDay, out int sold, out int bought);
            var listedStart = new int[Districts];
            var listedEnd = new int[Districts];
            for (int i = 0; i < n; i++)
            {
                listedStart[book.District[i]] += wasOwner[i] == HoldingsBook.Market || (book.Owner[i] == HoldingsBook.Market && wasOwner[i] != HoldingsBook.Market) ? 1 : 0;
                listedEnd[book.District[i]] += book.Owner[i] == HoldingsBook.Market ? 1 : 0;
            }
            for (int d = 0; d < Districts; d++)
            {
                floor &= listedEnd[d] >= Math.Min(RivalHoldingsRules.ListingsLeast, listedStart[d]);
            }
            caps &= sold <= RivalHoldingsRules.SalesPerDay && bought <= RivalHoldingsRules.BuysPerDay && book.Moves.Count == sold + bought;
            weekSold += sold;
            weekBought += bought;
            foreach (HoldingsMove m in book.Moves)
            {
                if (m.To == HoldingsBook.Market)
                {
                    listedDay[m.Building] = day;
                }
                else
                {
                    young &= !listedDay.TryGetValue(m.Building, out int d) || d <= day - RivalHoldingsRules.ListedDays;
                    young &= wasOwner[m.Building] == HoldingsBook.Market;
                }
            }
            for (int i = 0; i < n; i++)
            {
                if (wasOwner[i] == HoldingsBook.Fixed)
                {
                    fixedStays &= book.Owner[i] == HoldingsBook.Fixed;
                }
            }
            if ((day - 10) % 7 == 6)
            {
                weekCaps &= weekSold <= 7 * RivalHoldingsRules.SalesPerDay && weekBought <= 7 * RivalHoldingsRules.BuysPerDay;
                weekSold = weekBought = 0;
                int owned = RivalHoldingsRules.Counts(book, counts);
                RivalHoldingsRules.Targets(Rivals, owned, targets);
                int distance = RivalHoldingsRules.Distance(counts, targets, Rivals);
                worstRise = Math.Max(worstRise, distance - previous);
                previous = distance;
            }
        }
        int ownedEnd = RivalHoldingsRules.Counts(book, counts);
        RivalHoldingsRules.Targets(Rivals, ownedEnd, targets);
        int end = RivalHoldingsRules.Distance(counts, targets, Rivals);
        Check(fixedStays, "rivals' buildings: a year of gentle days never moves a fixed building (the player's, rented by the player, a special service, a held address)");
        Check(caps && weekCaps, "rivals' buildings: a day puts up at most " + RivalHoldingsRules.SalesPerDay + " and buys at most " + RivalHoldingsRules.BuysPerDay + ", so a week at most "
              + 7 * RivalHoldingsRules.SalesPerDay + " each");
        Check(young, "rivals' buildings: a rival buys only a building that has been for sale " + RivalHoldingsRules.ListedDays + " days or more: the player has it first");
        Check(floor, "rivals' buildings: a rival never buys the last " + RivalHoldingsRules.ListingsLeast + " buildings for sale in a district");
        Check(end * 4 <= start && worstRise <= 12, "rivals' buildings: a year of gentle days brings the holdings to a quarter of the distance they began at or less (" + start + " to " + end
              + "), the worst week rising by " + worstRise);
        Check(counts[0] > firstBefore && counts[Rivals - 1] < lastBefore && counts[Rivals - 1] <= 15, "rivals' buildings: the top rival holds more by the year's end (" + firstBefore + " to "
              + counts[0] + ") and the last fewer (" + lastBefore + " to " + counts[Rivals - 1] + ")");
    }

    // Which building a rival lets go or buys: the cheapest for the top half, the dearest below; and the listings a rival may buy.
    private static void HoldingsPicks()
    {
        var c = new HoldingsBook();
        c.Reset(4, 2);
        for (int i = 0; i < 30; i++)
        {
            c.Add(0, 100000L + i * 1000L, 0, 0);
        }
        for (int i = 0; i < 4; i++)
        {
            c.Add(1, 300000L + i, 0, 0);
            c.Add(2, 200000L + i, 1, 0);
            c.Add(3, 150000L + i, 1, 0);
        }
        RivalHoldingsRules.Day(c, 50, 1, 0, out int s, out int b2);
        Check(s == 1 && b2 == 0 && c.Moves.Count == 1 && c.Value[c.Moves[0].Building] == 100000L && c.Moves[0].From == 0 && c.Moves[0].To == HoldingsBook.Market,
            "rivals' buildings: a rival in the top half that is over its share puts up its cheapest building first");
        var low = new HoldingsBook();
        low.Reset(4, 2);
        for (int i = 0; i < 4; i++)
        {
            low.Add(0, 90000L + i, 0, 0);
            low.Add(1, 90000L + i, 0, 0);
            low.Add(2, 90000L + i, 0, 0);
        }
        for (int i = 0; i < 30; i++)
        {
            low.Add(3, 100000L + i * 1000L, 0, 0);
        }
        RivalHoldingsRules.Day(low, 50, 1, 0, out int s2, out _);
        Check(s2 == 1 && low.Value[low.Moves[0].Building] == 129000L && low.Moves[0].From == 3, "rivals' buildings: a rival in the bottom half that is over its share puts up its dearest");
        var pool = new HoldingsBook();
        pool.Reset(2, 1);
        for (int i = 0; i < 4; i++)
        {
            pool.Add(0, 100000L, 0, 0);   // the top place holds 4 of its 8
        }
        for (int i = 0; i < 9; i++)
        {
            pool.Add(1, 100000L, 0, 0);   // the last holds 9 of its 5
        }
        for (int i = 0; i < 5; i++)
        {
            pool.Add(HoldingsBook.Market, 70000L + i, 0, 40);   // listed on day 40: not yet three days old on day 42
        }
        RivalHoldingsRules.Day(pool, 42, 0, 3, out _, out int nothing);
        Check(nothing == 0, "rivals' buildings: a listing two days old is not bought");
        RivalHoldingsRules.Day(pool, 43, 0, 3, out _, out int three);
        Check(three == 3 && pool.Moves.Count == 3, "rivals' buildings: three days on it is bought, and the district keeps its last two (5 listed, 3 bought, 2 left: " + three + ")");
        RivalHoldingsRules.Day(pool, 60, 0, 3, out _, out int last);
        Check(last == 0, "rivals' buildings: the last " + RivalHoldingsRules.ListingsLeast + " in a district stay for sale however long they have been");
    }

    private static void HoldingsFortune()
    {
        RivalFortune f = RivalFortuneRules.Make(130, 40000000L, 5000L, 200000L);
        long expect = 40000000L + RivalFortuneRules.PurseWeeks * (200000L + 5000L * 7);
        Check(f.Worth == expect && f.Property == 40000000L && f.RentWeek == 35000L && f.Trade == 200000L && f.Buildings == 130,
            "rival fortune: the parts add up by the one rule (the buildings' value plus " + RivalFortuneRules.PurseWeeks + " weeks of takings and rent): " + f.Worth);
        Check(RivalFortuneRules.PurseWeeks == RealLifeOverhaul.Economy.ServicesCatalogue.RivalPurseWeeks && RivalFortuneRules.PurseWeeks == 26,
            "rival fortune: the weeks are the half year a rival puts up when it buys a business");
        RivalFortune poor = RivalFortuneRules.Make(130, 30000000L, 0L, -900000000L);
        RivalFortune shop = RivalFortuneRules.Make(0, 0L, 0L, 90000L);
        Check(poor.Worth >= poor.Property && poor.Worth > shop.Worth, "rival fortune: a rival with 130 buildings cannot read as poorer than its property, whatever its takings, or than a shop owner");
        RivalFortune none = RivalFortuneRules.Make(-5, -1L, -3L, 0L);
        Check(none.Buildings == 0 && none.Property == 0 && none.RentWeek == 0 && none.Worth == 0, "rival fortune: nothing owned and nothing taken is worth nothing, and a negative reading is none");
        var worth = new long[] { 500L, 900L, 900L, 100L };
        Check(RivalFortuneRules.PlaceByFortune(worth, 4, 1) == 1 && RivalFortuneRules.PlaceByFortune(worth, 4, 2) == 2 && RivalFortuneRules.PlaceByFortune(worth, 4, 0) == 3
              && RivalFortuneRules.PlaceByFortune(worth, 4, 3) == 4 && RivalFortuneRules.PlaceByFortune(worth, 4, 9) == 0 && RivalFortuneRules.PlaceByFortune(null, 4, 0) == 0,
            "rival fortune: the place by fortune counts the richer first and keeps the game's order for an equal fortune");
        // A rival last by takings that owns 130 buildings shows both places.
        var ranked = new long[19];
        for (int i = 0; i < 19; i++)
        {
            ranked[i] = RivalFortuneRules.Make(i == 18 ? 130 : 3, i == 18 ? 50000000L : 400000L, i == 18 ? 20000L : 1000L, 100000L - i * 4000L).Worth;
        }
        Check(RivalFortuneRules.PlaceByFortune(ranked, 19, 18) == 1, "rival fortune: the rival last by takings that owns 130 buildings is first by fortune, and the page says both");
    }

    // The kit's Rivals app with a fortune: the league's column, the chosen rival's facts, the place by fortune only where it differs from the game's, nothing for the player or a defeated rival.
    private static void HoldingsKit()
    {
        UiWords en = UiWords("en");
        var v = new RivalsView { Money = m => "$" + ((long)m).ToString(System.Globalization.CultureInfo.InvariantCulture), Today = 30 };
        RivalFortune rich = RivalFortuneRules.Make(130, 50000000L, 20000L, 100000L), plain = RivalFortuneRules.Make(5, 2000000L, 1000L, 300000L);
        v.League = new[]
        {
            new RivalEntry { Name = "A Rival", WeeklyIncome = 300000f, Businesses = 4, WeekAgo = 290000f, HasFortune = true, Buildings = 5, Property = plain.Property, RentWeek = plain.RentWeek, Fortune = plain.Worth, FortunePlace = 2, RivalPlace = 1, Rivals = 3 },
            new RivalEntry { Name = "The Last", WeeklyIncome = 100000f, Businesses = 2, WeekAgo = 90000f, HasFortune = true, Buildings = 130, Property = rich.Property, RentWeek = rich.RentWeek, Fortune = rich.Worth, FortunePlace = 1, RivalPlace = 3, Rivals = 3 },
            new RivalEntry { Name = "A Player", You = true, WeeklyIncome = 50000f, Businesses = 1, WeekAgo = 40000f },
            new RivalEntry { Name = "Gone", Defeated = true }
        };
        var m = new UiRivalsModel();
        v.Chosen = 1;
        RivalsViews.Rivals(m, en, v);
        int fortune = -1;
        for (int i = 0; i < m.Table.Columns.Length; i++)
        {
            fortune = m.Table.Columns[i].Title == "Fortune" ? i : fortune;
        }
        Check(fortune > 0 && m.Table.Columns[fortune].Kind == UiCellKind.Money && m.Table.Rows[1].Cells[fortune] == "$" + rich.Worth && m.Table.Rows[0].Cells[fortune] == "$" + plain.Worth
              && m.Table.Rows[2].Cells[fortune].Length == 0 && m.Table.Rows[3].Cells[fortune].Length == 0 && fortune < RivalsViews.ColumnChange,
            "rival fortune: the league has a Fortune column before the week's change, empty for the player and a defeated rival");
        bool cells = true;
        for (int r = 0; r < m.Table.Rows.Length; r++)
        {
            cells &= m.Table.Rows[r].Cells.Length == m.Table.Columns.Length && m.Table.Rows[r].Values.Length == m.Table.Columns.Length && m.Table.Rows[r].Tones.Length == m.Table.Columns.Length;
        }
        Check(cells, "rival fortune: every league row has a cell, a value and a tone for every column");
        string facts = string.Join("|", m.Profile.FactLabels) + "#" + string.Join("|", m.Profile.FactValues);
        Check(m.Profile.FactLabels.Length == m.Profile.FactValues.Length && facts.Contains("Buildings") && facts.Contains("130 buildings worth $50000000") && facts.Contains("Rent a week") && facts.Contains("$140000")
              && facts.Contains("Fortune") && facts.Contains("By fortune") && facts.Contains("1 of 3, the game's 3"),
            "rival fortune: the chosen rival's head says its buildings and their worth, the rent a week, the fortune and, as it differs from the game's, the place by fortune: " + facts);
        v.Chosen = 0;
        v.League[0].FortunePlace = 1;
        v.League[0].RivalPlace = 1;
        RivalsViews.Rivals(m, en, v);
        facts = string.Join("|", m.Profile.FactLabels);
        Check(!facts.Contains("By fortune") && facts.Contains("Fortune") && string.Join("|", m.Profile.FactValues).Contains("5 buildings worth $2000000"),
            "rival fortune: where the place by fortune is the game's own it is not said twice");
        v.League[0].Buildings = 1;
        RivalsViews.Rivals(m, en, v);
        Check(string.Join("|", m.Profile.FactValues).Contains("1 building worth"), "rival fortune: one building is not 'buildings'");
        v.Chosen = 2;
        RivalsViews.Rivals(m, en, v);
        Check(!string.Join("|", m.Profile.FactLabels).Contains("Fortune") && !string.Join("|", m.Profile.FactLabels).Contains("Buildings"), "rival fortune: the player's own head has no fortune of the rivals' rule");
    }

    private static void HoldingsSources()
    {
        string rules = ModSource(@"Economy\Estate\RivalHoldingsRules.cs");
        string game = ModSource(@"Economy\Estate\EstateGame.Holdings.cs");
        string director = ModSource(@"Economy\Estate\EstateDirector.Holdings.cs");
        string main = ModSource(@"Economy\Estate\EstateDirector.cs");
        string settings = ModSource(@"Core\Settings.cs");
        string mod = ModSource(@"Core\RealLifeOverhaulMod.cs");
        string tree = ModSource(@"Meta\OptionsTree.cs");
        string guide = ModSource(@"Rivals\RivalGuide.cs");
        string page = ModSource(@"Rivals\RivalPage.cs");
        string screen = ModSource(@"Ui\Rivals\RivalsScreen.cs");
        string views = ModSource(@"Ui\RivalsViews.cs");
        string ranking = ModSource(@"Economy\Estate\RivalRanking.cs");
        string fortune = ModSource(@"Rivals\RivalFortuneGame.cs");
        Check(game.Contains("reg.BuildingOwnedByPlayer || reg.RentedByPlayer") && game.Contains("building.SpecialService != null") && game.Contains("building.IsHamptonsHouse()") && game.Contains("GameDlc.Gated(building)")
              && game.Contains("GoingPublic.Mine(reg) || GoingPublic.Foreign(reg)") && game.Contains("RivalsHelper.IgnoredAddresses.Contains(reg.Address)"),
            "rivals' buildings: the book never takes in a building the player owns or rents, a special service, a Hamptons house, a DLC's, one the game's ignore list holds, or another player's");
        Check(game.Contains("RealEstateHelper.SetBuildingForSale(reg)") && game.Contains("save.buildingsForSale.RemoveAt(s)") && game.Contains("reg.buildingOwnerRivalId = HoldingsIds[move.To];"),
            "rivals' buildings: a sale is the game's own SetBuildingForSale, a purchase writes the owner and takes the listing off as the game's rivals' purchase does");
        Check(ranking.Contains("RivalHoldingsRules.Compare(") && ranking.Contains("RivalsHelper.IsRivalDefeated(rival.id)") && ranking.Contains("rival.WeeklyIncome")
              && game.Contains("RivalRanking.Read(HoldingsRanked)"), "rivals' buildings: the places are the game's own ranking, by weekly takings, without the defeated");
        Check(director.Contains("GoingPublic.InSession") && director.Contains("RivalHoldingsRules.Pace(Settings.RivalsHoldings, day, _holdingsDealt)") && director.Contains("_holdingsBroken = true;"),
            "rivals' buildings: the part rests in a Going Public session, reads the option and the day, and a fault stops only itself");
        Check(main.Contains("HoldingsDay(today);") && main.Contains("HoldingsDay(day);") && main.Contains("RivalHoldingsStore.Read(save.modData)") && main.Contains("RivalHoldingsStore.Write(save.modData, _holdingsDealt);"),
            "rivals' buildings: dealt at a start and on every day, and the save's line read and written");
        int bad = 0;
        foreach (string code in new[] { rules, game, director, ranking })
        {
            bad += code.Contains("void Update(") || code.Contains("LateUpdate(") || code.Contains("FixedUpdate(") || code.Contains("OnGUI(") ? 1 : 0;
        }
        Check(bad == 0, "rivals' buildings: nothing of the part runs per frame");
        Check(settings.Contains("RivalsHoldingsId = \"rivals_holdings\"") && settings.Contains("public static int RivalsHoldings = 1;") && settings.Contains("RivalsHoldings = Int(modId, RivalsHoldingsId, RivalsHoldings);")
              && RivalHoldingsRules.OptionNewGames == 1, "rivals' buildings: the option is saved and its default is the new-game step, which leaves a save under way as it was");
        Check(mod.Contains("AddDropdown(Settings.RivalsHoldingsId, \"reallife_rival_holdings\"") && mod.Contains("\"reallife_rival_holdings_game\", \"reallife_rival_holdings_new\", \"reallife_rival_holdings_all\"")
              && mod.Contains("AddButton(\"reallife_rival_test_holdings\"") && tree.Contains("\"rivals_holdings\")") && guide.Contains("Settings.RivalsHoldingsId") && guide.Contains("RivalHoldingsStore.Key")
              && guide.Contains("\"reallife_rival_test_holdings\""), "rivals' buildings: the option has its three steps, its test button, its place on the page, and the feature owns it and its save line");
        Check(screen.Contains("RivalFortuneGame.Of(d.rivalId, d.ownedBuildings, d.weeklyIncome, today)") && screen.Contains("RivalFortuneRules.PlaceByFortune(")
              && views.Contains("reallife_ui_rivals_fortune") && views.Contains("e.HasFortune ? Money(v, e.Fortune)") && views.Contains("FortuneFacts(")
              && ModSource(@"Ui\UiScreens.Persona.cs").Contains("\"reallife_ui_rivals_by_fortune_value\""),
            "rival fortune: the kit's Rivals app shows the buildings, their value, rent and the fortune, a column of it, and the place by fortune where it differs from the game's");
        Check(page.Contains("WriteProperty(row);") && page.Contains("RivalFortuneGame.Read(Fortunes, day)") && page.Contains("KindProperty = 3") && page.Contains("\"reallife_rival_kind_property\""),
            "rival fortune: the Finance app's Rivals page, on the old interface, lists the game's rivals with their fortune and what it is made of");
        Check(fortune.Contains("GameRegistrations.BuildingOf(owned[i])") && fortune.Contains("k.Day == day && k.Count == count"),
            "rival fortune: a reading is kept for the day and the count, and a registration's building is read through the guard");
        // The game's ranking number is not written anywhere: nothing of the mod assigns a rival's weekly income or its history.
        int writes = 0;
        foreach (string code in new[] { rules, game, director, ranking, fortune, ModSource(@"Rivals\RivalFortuneRules.cs"), screen, views })
        {
            writes += code.Contains("weeklyIncomeHistory =") || code.Contains("dailyIncomes.Add") || code.Contains("dailyIncomes[") ? 1 : 0;
        }
        Check(writes == 0, "rival fortune: the game's ranking number, a rival's takings, is never written by the mod");
    }

    private static void HoldingsWords()
    {
        Dictionary<string, string>[] langs = LanguageWords(out string[] codes);
        if (!LanguagesRead("rivals' buildings", langs, codes))
        {
            return;
        }
        Dictionary<string, string> en = langs[0];
        string lacking = string.Empty;
        foreach (string key in new[]
                 {
                     "reallife_rival_holdings", "reallife_rival_holdings_game", "reallife_rival_holdings_new", "reallife_rival_holdings_all", "reallife_opt_tip_rivals_holdings",
                     "reallife_ui_rivals_fortune", "reallife_ui_rivals_property", "reallife_ui_rivals_property_one", "reallife_ui_rivals_property_many", "reallife_ui_rivals_rent",
                     "reallife_ui_rivals_by_fortune", "reallife_ui_rivals_by_fortune_value", "reallife_rival_kind_property", "reallife_rival_property_note",
                     "reallife_rival_test_holdings", "reallife_estate_test_rivals", "reallife_estate_test_rivals_off"
                 })
        {
            lacking += en.ContainsKey(key) ? string.Empty : " " + key;
        }
        Check(lacking.Length == 0, "rivals' buildings: every text is in English:" + lacking);
        string tip = en.TryGetValue("reallife_opt_tip_rivals_holdings", out string t) ? t : string.Empty;
        Check(tip.Length > 0 && tip.Length <= 190 && tip.Split(' ').Length <= 32, "rivals' buildings: the option's tip is at most 32 words and 190 characters (" + tip.Split(' ').Length + ", " + tip.Length + ")");
        Check(en.TryGetValue("reallife_estate_test_rivals", out string note) && note.Length <= 240 && note.Contains("{sold}") && note.Contains("{bought}") && note.Contains("{held}")
              && en.TryGetValue("reallife_ui_rivals_by_fortune_value", out string by) && by.Contains("{place}") && by.Contains("{count}") && by.Contains("{game}")
              && en.TryGetValue("reallife_rival_property_note", out string row) && row.Contains("{count}") && row.Contains("{value}") && row.Contains("{rent}") && row.Contains("{trade}"),
            "rivals' buildings: the texts keep their names in curly brackets and the notice is at most 240 characters");
    }
}
