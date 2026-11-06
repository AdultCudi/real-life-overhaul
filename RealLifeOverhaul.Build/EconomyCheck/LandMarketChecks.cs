using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Construction;

// The land market (docs/features/construction.md, "Land market"; Scripts/Construction: LandMarketRules, BuildState.Market, BuildStore.Market,
// ConstructionDirector.LandMarket, .LandMarketNpc, .LandMarketTests, ConstructionApp.Market): what the city pays for land sold back, a rival
// developer's ask and the range shown, how it answers, the limits on offering, what a rival pays for the player's land, the means of a
// rival, the cadence of the rivals' trading over two years, the save's part (a waiting offer kept, an old line read, a part of a version
// this one does not know passed over), then the sources (nothing a frame, a session rests, the works-begun refusal, the hold on a developer's
// plans while an offer waits, the tests) and the texts in every language. Each check was made to fail once.
internal static partial class Program
{
    private static void LandMarketChecks()
    {
        LandPriceChecks();
        LandAskChecks();
        LandDecisionChecks();
        LandLimitChecks();
        LandRivalChecks();
        LandCadenceChecks();
        LandStoreChecks();
        LandSourceChecks();
        LandTextChecks();
    }

    // ------------------------------------------------------------------ what the city pays

    private static void LandPriceChecks()
    {
        Check(LandMarketRules.BackPermille == 850, "land market: the city gives at least 85% of what was paid");
        Check(LandMarketRules.CityPrice(100000L, 90000L) == 90000L, "land market: land worth 90,000 that cost 100,000 sells for its value, 90,000 (more than 85% of 100,000)");
        Check(LandMarketRules.CityPrice(100000L, 70000L) == 85000L, "land market: land worth 70,000 that cost 100,000 sells for 85,000, the 85% floor");
        Check(LandMarketRules.CityPrice(60000L, 90000L) == 90000L, "land market: land that has gained in value sells for its value now, not for what was paid");
        Check(LandMarketRules.CityPrice(0L, 50000L) == 50000L && LandMarketRules.CityPrice(100000L, 0L) == 85000L && LandMarketRules.CityPrice(100000L, -5L) == 85000L,
            "land market: land given for nothing sells for its value; a value not known leaves the floor; a negative value is none");
        Check(LandMarketRules.CityPrice(1000L, 1L) == 850L, "land market: the floor is exact to the unit (85% of 1,000 is 850)");
        long paid = 1234567L, value = 1000000L;
        Check(LandMarketRules.CityPrice(paid, value) == Math.Max(paid * 850 / 1000, value), "land market: the price is the higher of the two and nothing is taken off");
        Check(LandMarketRules.SellWhy(false, true, false, false, 5000L) == null, "land market: an empty site of the player's may be sold");
        Check(LandMarketRules.SellWhy(true, true, false, false, 5000L) == "reallife_bld_answer_session_land", "land market: no land is sold in a Going Public session");
        Check(LandMarketRules.SellWhy(false, true, true, false, 5000L) == "reallife_land_answer_sell_busy", "land market: a site with a plan, a permit or works on it is refused");
        Check(LandMarketRules.SellWhy(false, true, false, true, 5000L) == "reallife_land_answer_sell_built", "land market: ground a building stood on is not bought back");
        Check(LandMarketRules.SellWhy(false, false, false, false, 5000L) == "reallife_bld_answer_no_lot" && LandMarketRules.SellWhy(false, true, false, false, 0L) == "reallife_bld_answer_no_price",
            "land market: land that is not there, or has no price, is refused");
        Check(LandMarketRules.SellWhy(true, true, true, true, 0L) == "reallife_bld_answer_session_land", "land market: a session is the first reason given");
    }

    // ------------------------------------------------------------------ what a rival asks

    private static void LandAskChecks()
    {
        Check(LandMarketRules.Premium(0, false, false) == 150 && LandMarketRules.Premium(1000, false, false) == 350 && LandMarketRules.Premium(500, false, false) == 250,
            "land market: a rival asks 15% over what it paid when its plans have begun and 35% when they are about to be decided");
        Check(LandMarketRules.Premium(500, true, false) == 300 && LandMarketRules.Premium(500, false, true) == 190 && LandMarketRules.Premium(500, true, true) == 240,
            "land market: its only site adds 5 points, being short of cash takes 6 off");
        Check(LandMarketRules.Premium(0, false, true) == 100 && LandMarketRules.Premium(-50, false, true) == 100 && LandMarketRules.Premium(5000, true, false) == 400,
            "land market: the premium never falls under 10% and an out of range progress is held to its ends");
        bool rising = true;
        for (int p = 1; p <= 1000; p++)
        {
            rising &= LandMarketRules.Premium(p, false, false) >= LandMarketRules.Premium(p - 1, false, false);
        }
        Check(rising, "land market: the further its plans have gone, the more it asks");
        Check(LandMarketRules.Progress(false, 10, 10) == 400 && LandMarketRules.Progress(true, 10, 10) == 1000 && LandMarketRules.Progress(false, 0, 50) == 0
              && LandMarketRules.Progress(true, 0, 500) == 400, "land market: progress runs from 0 (plans just begun) to 400 (drawn) and on to 1000 (the decision due)");
        bool monotone = true;
        for (int day = 1; day < 40; day++)
        {
            monotone &= LandMarketRules.Progress(true, day, 40) >= LandMarketRules.Progress(true, day - 1, 40) && LandMarketRules.Progress(false, day, 40) >= LandMarketRules.Progress(false, day - 1, 40);
        }
        Check(monotone, "land market: as the days go by its plans have gone further, never back");
        Check(LandMarketRules.Ask(100000L, 250) == 125000L && LandMarketRules.Ask(100001L, 250) == 126000L && LandMarketRules.Ask(1L, 0) == 1000L,
            "land market: the ask is what it paid and the premium, rounded up to the thousand, never under a thousand");
        bool inside = true, notMiddle = 0 == 1;
        int middles = 0;
        for (int site = 1; site <= 200; site++)
        {
            long ask = 1000000L + site * 10000L;
            LandMarketRules.Range(ask, 77, site, out long low, out long high);
            inside &= low < ask && ask <= high && low % 1000 == 0 && high % 1000 == 0;
            middles += Math.Abs((low + high) / 2 - ask) < 3000L ? 1 : 0;
        }
        notMiddle = middles < 60;
        Check(inside, "land market: the range shown holds the ask, with a thousand to spare, in whole thousands");
        Check(notMiddle, "land market: the ask is not always in the middle of the range (" + middles + " of 200 within 3,000)");
        LandMarketRules.Range(2000000L, 5, 9, out long l1, out long h1);
        LandMarketRules.Range(2000000L, 5, 9, out long l2, out long h2);
        LandMarketRules.Range(2000000L, 6, 9, out long l3, out long h3);
        Check(l1 == l2 && h1 == h2 && (l1 != l3 || h1 != h3), "land market: the range is the same for the same save and site, and another save's differs");
        long low0 = 1900000L, high0 = 2300000L;
        bool ascending = true;
        for (int i = 1; i < LandMarketRules.PricePoints; i++)
        {
            ascending &= LandMarketRules.PriceAt(low0, high0, i) > LandMarketRules.PriceAt(low0, high0, i - 1);
        }
        Check(ascending && LandMarketRules.PriceAt(low0, high0, 0) == 1710000L && LandMarketRules.PriceAt(low0, high0, LandMarketRules.PricePoints - 1) == 2530000L,
            "land market: the prices to pick from run a tenth under the range to a tenth over it, ascending");
        int start = LandMarketRules.StartStep(low0, high0);
        Check(start > 0 && start < LandMarketRules.PricePoints && LandMarketRules.PriceAt(low0, high0, start) >= (low0 + high0) / 2, "land market: the selector starts at the middle of the range");
    }

    // ------------------------------------------------------------------ how it answers

    private static void LandDecisionChecks()
    {
        Check(LandMarketRules.Decide(125000L, 125000L, false, false) == LandAnswer.Accept && LandMarketRules.Decide(130000L, 125000L, false, false) == LandAnswer.Accept,
            "land market: a rival accepts a price at or over its ask");
        Check(LandMarketRules.Decide(124999L, 125000L, false, false) == LandAnswer.Refuse && LandMarketRules.Decide(1L, 125000L, false, false) == LandAnswer.Refuse,
            "land market: a rival refuses a price under its ask, by a unit or by a lot");
        Check(LandMarketRules.Decide(999999L, 125000L, true, false) == LandAnswer.Lapse && LandMarketRules.Decide(999999L, 125000L, false, true) == LandAnswer.Lapse,
            "land market: once its works have begun, or its plans were dropped, the offer lapses whatever the price");
        Check(LandMarketRules.Decide(1L, 125000L, true, false) == LandAnswer.Lapse, "land market: works under way lapse a low offer too, they are not refused");
        // The same inputs give the same answer however often asked.
        bool same = true;
        for (int i = 0; i < 20; i++)
        {
            same &= LandMarketRules.Decide(120000L, 125000L, false, false) == LandAnswer.Refuse && LandMarketRules.Ask(100000L, 250) == 125000L;
        }
        Check(same, "land market: the answer is a rule of its inputs alone");
        Check(LandMarketRules.AnswerDay(1) == 2 && LandMarketRules.AnswerDay(5) == 8 && LandMarketRules.AnswerDay(6) == 8 && LandMarketRules.AnswerDay(7) == 8,
            "land market: an offer is answered the next working day (a Friday's on the Monday)");
    }

    // ------------------------------------------------------------------ the limits on offering

    private static void LandLimitChecks()
    {
        const long cash = 10000000L;
        string Why(bool session = false, bool started = false, bool gone = false, bool waiting = false, int day = 100, int last = 0, bool refused = false, long lastPrice = 0L,
            int raised = 0, long price = 500000L, long money = cash, int lots = 3)
        {
            return LandMarketRules.OfferWhy(session, started, gone, waiting, day, last, refused, lastPrice, raised, price, money, lots);
        }
        Check(Why() == null, "land market: a first offer for land whose plans are still plans may be made");
        Check(Why(session: true) == "reallife_bld_answer_session_land", "land market: no offer in a Going Public session");
        Check(Why(started: true) == "reallife_land_answer_started" && Why(started: true, price: 1L) == "reallife_land_answer_started",
            "land market: no offer for land whose works have begun, whatever the price");
        Check(Why(gone: true) == "reallife_land_answer_started", "land market: no offer for land whose plans were dropped");
        Check(Why(waiting: true) == "reallife_land_answer_waiting", "land market: one offer waits at a time for a lot");
        Check(Why(last: 99, refused: false, price: 500000L) == "reallife_land_answer_rest" && Why(last: 96, price: 500000L) == null && Why(last: 97, price: 500000L) == "reallife_land_answer_rest",
            "land market: one offer for a lot every four days");
        Check(Why(last: 99, refused: true, lastPrice: 400000L, price: 450000L) == null, "land market: a refused offer may be raised at once");
        Check(Why(last: 99, refused: true, lastPrice: 400000L, price: 400000L) == "reallife_land_answer_rest" && Why(last: 99, refused: true, lastPrice: 400000L, price: 300000L) == "reallife_land_answer_rest",
            "land market: the same price, or a lower one, after a refusal waits the days");
        Check(Why(last: 99, refused: true, lastPrice: 400000L, price: 450000L, raised: 2) == null && Why(last: 99, refused: true, lastPrice: 400000L, price: 450000L, raised: 3) == "reallife_land_answer_rest",
            "land market: a refused offer is raised three times, the fourth waits");
        Check(Why(last: 95, refused: true, lastPrice: 400000L, price: 450000L, raised: 3) == "reallife_land_answer_rest" && Why(last: 93, refused: true, lastPrice: 400000L, price: 450000L, raised: 3) == null,
            "land market: after the third raise is refused the developer rests six days");
        Check(Why(price: 0L) == "reallife_land_answer_price" && Why(price: -5L) == "reallife_land_answer_price", "land market: an offer needs a price");
        Check(Why(money: 499999L) == "reallife_bld_answer_no_money" && Why(money: 500000L) == null, "land market: the cash must be there to the unit");
        Check(Why(lots: BuildCatalogue.LotsMost) == "reallife_bld_answer_too_many_lots" && Why(lots: BuildCatalogue.LotsMost - 1) == null, "land market: the player must have room for one more lot");
    }

    // ------------------------------------------------------------------ the rivals' side

    private static void LandRivalChecks()
    {
        Check(LandMarketRules.Means(60, 0, 0, 1000) == 2000000L + 60 * 40000L && LandMarketRules.Means(60, 3, 2, 1000) == 2000000L + 60 * 40000L + 3 * 300000L + 2 * 150000L,
            "land market: a rival's means come from its reputation, what it has built and what it has won");
        Check(LandMarketRules.Means(60, 0, 0, 2000) == 2 * LandMarketRules.Means(60, 0, 0, 1000) && LandMarketRules.Means(-5, -1, -1, 0) == 2000000L / 1000L * 1,
            "land market: the means follow the city's prices; a negative record counts for nothing");
        Check(LandMarketRules.Short(5000001L, 5000000L) && !LandMarketRules.Short(5000000L, 5000000L) && !LandMarketRules.Short(0L, 1L), "land market: short of cash means holding more land than its means");
        bool inRange = true, varied = false;
        long first = LandMarketRules.OfferPrice(1000000L, LandReason.Blocks, 3, 1);
        for (int id = 1; id < 300; id++)
        {
            long b = LandMarketRules.OfferPrice(1000000L, LandReason.Blocks, 3, id), j = LandMarketRules.OfferPrice(1000000L, LandReason.Joins, 3, id);
            inRange &= b >= 1200000L && b <= 1301000L && j >= 1100000L && j <= 1201000L && b % 1000 == 0 && j % 1000 == 0;
            varied |= b != first;
        }
        Check(inRange && varied, "land market: a rival pays 20% to 30% over the value for land that stands in its way and 10% to 20% for land beside its own");
        Check(LandMarketRules.OfferPrice(1000000L, LandReason.Blocks, 3, 7) == LandMarketRules.OfferPrice(1000000L, LandReason.Blocks, 3, 7)
              && LandMarketRules.OfferPrice(0L, LandReason.Joins, 3, 7) >= 1000L, "land market: the price of an offer is drawn from the save and the offer, and is at least a thousand");
        Check(LandMarketRules.Unused(40, 54) && !LandMarketRules.Unused(40, 53) && !LandMarketRules.Unused(0, 500), "land market: a rival offers only for land that has stood unused fourteen days, and not while works are on it");
        Check(LandMarketRules.Due(10) == 17, "land market: the player has seven days to answer a rival's offer");
        Check(LandMarketRules.Resting(10, 29) && !LandMarketRules.Resting(10, 30) && !LandMarketRules.Resting(0, 30), "land market: a rival leaves refused land alone for twenty days");
        Check(LandMarketRules.TradePrice(100000L) == 110000L && LandMarketRules.TradePrice(100001L) == 111000L, "land market: a rival buying a site from another pays what it paid and a tenth");
    }

    // ------------------------------------------------------------------ slow, and the same everywhere

    private static void LandCadenceChecks()
    {
        bool gaps = true;
        for (int day = 1; day < 2000; day++)
        {
            int next = LandMarketRules.NextMove(11, day);
            gaps &= next - day >= 8 && next - day <= 12 && next == LandMarketRules.NextMove(11, day);
        }
        Check(gaps, "land market: the rivals' next move is eight to twelve days on, the same for the same save and day");
        // Two years of the rivals' passes, as the director runs them: a move when the day comes, the month's limit holding it back.
        var state = new BuildState();
        int nextDay = 0, moves = 0, worst = 0, minGap = int.MaxValue, lastMove = -1;
        var days = new List<int>();
        for (int day = 1; day <= 720; day++)
        {
            if (nextDay <= 0)
            {
                nextDay = day + LandMarketRules.FirstDay;
                continue;
            }
            if (day < nextDay)
            {
                continue;
            }
            if (LandMarketRules.MonthFull(state.MarketMoves, state.MarketMoves.Length, day))
            {
                nextDay = day + LandMarketRules.GapLeast;
                continue;
            }
            state.NoteMove(day);
            nextDay = LandMarketRules.NextMove(11, day);
            days.Add(day);
            moves++;
            minGap = lastMove >= 0 ? Math.Min(minGap, day - lastMove) : minGap;
            lastMove = day;
        }
        for (int i = 0; i < days.Count; i++)
        {
            int inMonth = days.Count(d => d >= days[i] && d < days[i] + 30);
            worst = Math.Max(worst, inMonth);
        }
        Check(moves >= 40 && moves <= 100, "land market: over two years the rivals make a few moves a month, " + moves + " in 720 days");
        Check(worst <= LandMarketRules.MonthMost, "land market: never more than " + LandMarketRules.MonthMost + " moves in any thirty days (the most in a run: " + worst + ")");
        Check(minGap >= LandMarketRules.GapLeast, "land market: two moves are at least eight days apart (the least: " + minGap + ")");
        bool full = LandMarketRules.MonthFull(new[] { 100, 105, 110, 0 }, 4, 120) && !LandMarketRules.MonthFull(new[] { 100, 105, 0, 0 }, 4, 120) && !LandMarketRules.MonthFull(new[] { 100, 105, 110, 0 }, 4, 131);
        Check(full, "land market: three moves in thirty days fill the month, and the oldest falls out after it");
        var kinds = new bool[2];
        for (int day = 1; day < 200; day++)
        {
            kinds[LandMarketRules.MoveKind(11, day)] = true;
        }
        Check(kinds[0] && kinds[1] && LandMarketRules.MoveKind(11, 50) == LandMarketRules.MoveKind(11, 50), "land market: the move of the day is one of the two kinds, the same for the same day");
        BuildState a = new BuildState();
        for (int i = 1; i <= 7; i++)
        {
            a.NoteMove(i * 10);
        }
        Check(a.MarketMoves.Length == LandMarketRules.MonthMost + 1 && a.MarketMoves[a.MarketMoves.Length - 1] == 70 && a.MarketMoves[0] == 40, "land market: only the latest moves are kept");
    }

    // ------------------------------------------------------------------ the save

    private static BuildState LandStateWithOffers()
    {
        var s = new BuildState { Seed = 7 };
        s.NextMarketDay = 40;
        s.CityLandBack = 123456L;
        s.NoteMove(10);
        s.NoteMove(21);
        s.Offers.Add(new LandOffer
        {
            Id = 5, Side = LandOfferSide.Mine, State = LandOfferState.Waiting, Lot = "uptown_1_02_0003", Spaces = 2, Grow = 3, Rival = 4, Holding = string.Empty, Use = BuildUse.Shops,
            Price = 2600000L, Ask = 0L, Made = 30, Due = 31, Raised = 1
        });
        s.Offers.Add(new LandOffer
        {
            Id = 6, Side = LandOfferSide.Theirs, State = LandOfferState.Waiting, Lot = "uptown_1_02_0010", Spaces = 1, Rival = 2, Holding = "Ostrander & Sons; Co|x", Use = BuildUse.Offices, Reason = LandReason.Joins,
            Design = "design a|b", Price = 1750000L, Made = 32, Due = 39
        });
        s.Offers.Add(new LandOffer
        {
            Id = 7, Side = LandOfferSide.Mine, State = LandOfferState.Refused, Lot = "uptown_1_02_0003", Spaces = 2, Grow = 3, Rival = 4, Use = BuildUse.Shops, Price = 2400000L, Ask = 2550000L,
            Made = 20, Due = 21, Answered = 21
        });
        return s;
    }

    private static void LandStoreChecks()
    {
        BuildState s = LandStateWithOffers();
        string line = BuildStore.Write(s);
        BuildState back = BuildStore.Read(line, out int version);
        Check(line.Contains("^m:1|40|123456|") && version == 1 && back != null, "land market: the save has the market's part, its own version first, and the line's version is as before");
        bool same = back != null && back.Offers.Count == 3 && back.NextMarketDay == 40 && back.CityLandBack == 123456L && back.MarketMoves.SequenceEqual(s.MarketMoves);
        LandOffer w = back?.Offer(5), t = back?.Offer(6), r = back?.Offer(7);
        same &= w != null && w.Waiting && w.Side == LandOfferSide.Mine && w.Lot == "uptown_1_02_0003" && w.Spaces == 2 && w.Grow == 3 && w.Rival == 4 && w.Price == 2600000L && w.Due == 31 && w.Raised == 1 && w.Use == BuildUse.Shops;
        same &= t != null && t.Waiting && t.Side == LandOfferSide.Theirs && t.Holding == "Ostrander & Sons; Co|x" && t.Design == "design a|b" && t.Reason == LandReason.Joins && t.Use == BuildUse.Offices && t.Due == 39;
        same &= r != null && r.State == LandOfferState.Refused && r.Ask == 2550000L && r.Answered == 21;
        Check(same, "land market: an offer waiting is saved and comes back whole (and one answered, with its text fields escaped)");
        Check(back != null && BuildStore.Write(back) == line, "land market: the line written from what was read is the line as it was");
        Check(back != null && back.WaitingOn("uptown_1_02_0003")?.Id == 5 && back.LastOn(LandOfferSide.Mine, "uptown_1_02_0003")?.Id == 5 && back.TheirsWaiting() == 1,
            "land market: the waiting offer is found by its lot after a load");
        // an older line has no market part
        string older = line.Substring(0, line.IndexOf("^m:", StringComparison.Ordinal));
        BuildState old = BuildStore.Read(older, out _);
        Check(old != null && old.Offers.Count == 0 && old.NextMarketDay == 0 && old.CityLandBack == 0L && BuildStore.Write(old) == older, "land market: a line from before the market reads as one with no offers, and is written back as it was");
        Check(!BuildStore.Write(new BuildState { Seed = 1 }).Contains("^m:"), "land market: a save that never used the market gains nothing");
        // a part of a version this one cannot place is passed over
        BuildState odd = BuildStore.Read(older + "^m:0|9|9|1,2;1|0|0", out _), odd2 = BuildStore.Read(older + "^m:x|9", out _);
        Check(odd != null && odd.Offers.Count == 0 && odd.NextMarketDay == 0 && odd2 != null && odd2.Offers.Count == 0, "land market: a market part with a version it cannot read is passed over, the rest of the line loads");
        // a newer part with more fields: read as far as this knows it
        BuildState wide = BuildStore.Read(line + "|extra|more", out _);
        Check(wide != null && wide.Offer(6) != null && wide.Offers.Count == 3, "land market: a record with fields from a newer version is read as far as this one knows it");
        // bad figures are bounded
        BuildState bad = BuildStore.Read(older + "^m:1|-5|-9|-1,x;1|7|9|99|-3|99|9|9|5|6|9|-1|100|-5|uptown_1_02_0003||;2|0|0|1|0|0|0|0|0|0|0|0|100|0|||;0|0|0|1|0|0|0|0|0|0|0|0|100|0|lotx||", out _);
        LandOffer b = bad?.Offer(1);
        Check(bad != null && bad.NextMarketDay == 0 && bad.CityLandBack == 0L && bad.Offers.Count == 1 && b != null && b.Side == LandOfferSide.Theirs && b.State == LandOfferState.Withdrawn
              && b.Spaces == BuildCatalogue.SiteSpacesMost && b.Raised == LandMarketRules.RaisesMost && b.Price == 100L && b.Ask == 0L && b.Answered == 0 && bad.MarketMoves.All(d => d >= 0),
            "land market: odd figures in the part are held to their ranges, and a record with no lot or no id is left out");
        // the cap
        var many = new BuildState { Seed = 2 };
        for (int i = 1; i <= BuildState.OffersMost + 20; i++)
        {
            many.Offers.Add(new LandOffer { Id = i, Side = LandOfferSide.Mine, State = i <= 5 ? LandOfferState.Waiting : LandOfferState.Refused, Lot = "l" + i, Price = 1000L * i, Made = i });
        }
        many.TidyOffers();
        BuildState capped = BuildStore.Read(BuildStore.Write(many), out _);
        Check(many.Offers.Count == BuildState.OffersMost && Enumerable.Range(1, 5).All(i => many.Offer(i) != null) && capped != null && capped.Offers.Count <= BuildState.OffersMost,
            "land market: the offers kept are capped, those waiting are never let go, the oldest answered go first");
    }

    // ------------------------------------------------------------------ the sources

    private static void LandSourceChecks()
    {
        string rules = ModSource(@"Construction\LandMarketRules.cs"), director = ModSource(@"Construction\ConstructionDirector.LandMarket.cs"), npc = ModSource(@"Construction\ConstructionDirector.LandMarketNpc.cs");
        string tests = ModSource(@"Construction\ConstructionDirector.LandMarketTests.cs"), app = ModSource(@"Construction\ConstructionApp.Market.cs"), market = ModSource(@"Construction\ConstructionDirector.Market.cs");
        string grow = ModSource(@"Construction\ConstructionDirector.Grow.cs"), store = ModSource(@"Construction\BuildStore.cs"), guide = ModSource(@"Construction\ConstructionGuide.cs");
        string stateM = ModSource(@"Construction\BuildState.Market.cs"), storeM = ModSource(@"Construction\BuildStore.Market.cs");
        Check(!Regex.IsMatch(rules + director + npc + tests + app + stateM + storeM, @"void (Update|LateUpdate|FixedUpdate)\("), "land market sources: nothing is run a frame, every part is on a press or the day's pass");
        string day = MethodText(market, "private void MarketDay(");
        int session = day.IndexOf("if (!GoingPublic.InSession)", StringComparison.Ordinal), pass = Regex.Match(day, @"(?m)^\s*LandMarketDay\(day\);").Index;
        Check(session >= 0 && pass > session && pass < day.IndexOf("GrowDay(day)", StringComparison.Ordinal), "land market sources: the day's pass rests in a Going Public session and comes before the growth, which it holds");
        Check(MethodText(director, "private void LandMarketDay(").Contains("Settings.ConstructionDevelopers && DistrictLives()") && MethodText(director, "private void LandMarketDay(").Contains("LandMarketMoves(day)"),
            "land market sources: the rivals' trading follows the developers' option and the district");
        Check(MethodText(director, "public string SellBlock(").Contains("GoingPublic.InSession") && MethodText(director, "public string OfferBlock(").Contains("GoingPublic.InSession")
              && MethodText(director, "private bool SitePlanned(").Contains("_state.OnLot(firstId) != null") && MethodText(director, "private bool SitePlanned(").Contains("ParkOf(firstId) != null")
              && MethodText(director, "private bool SitePlanned(").Contains("GrowOf(firstId) != null"), "land market sources: selling and offering are refused in a session, and for a site with a plan, a car park or a grown building on it");
        Check(MethodText(director, "public string Sell(").Contains("\"reallife_transaction_bld_land_sold\"") && MethodText(director, "private void TakeRivalLand(").Contains("\"reallife_transaction_bld_land_rival\"")
              && MethodText(director, "public string Accept(").Contains("\"reallife_transaction_bld_land_rival_sold\""), "land market sources: each money move is a line of its own in the game's books");
        Check(MethodText(director, "private void TakeRivalLand(").Contains("g.Stage = GrowStage.Dropped") && MethodText(director, "private void AnswerOffer(").Contains("LandMarketRules.Decide(o.Price, ask, started, gone)"),
            "land market sources: a developer's answer is the rules' and its plans are dropped when it sells");
        Check(MethodText(director, "public string Offer(").Contains("LandMarketRules.AnswerDay(day)") && MethodText(director, "public string Sell(").Contains("_state.Lots.Remove(own)"),
            "land market sources: an offer is answered the next working day, and a sale removes the site from the player's land");
        Check(grow.Contains("&& _state.WaitingOn(g.Lot) == null)") && MethodText(grow, "private void GrowDrop(").Contains("LandSoldBack(g)"),
            "land market sources: a developer's plans wait while an offer for its land waits, and land its plans gave up goes back to the city");
        Check(store.Contains("WriteMarket(t, s);") && store.Contains("ReadMarket(part[0], body, s)"), "land market sources: the market's part is written and read with the others");
        Check(npc.Contains("RedevelopRules.PickOf(") && npc.Contains("LandMarketRules.NextMove(") && !npc.Contains("UnityEngine.Random") && !npc.Contains("DateTime") && !npc.Contains("new System.Random"),
            "land market sources: the rivals' trading is drawn from the save's seed and the day, never an unseeded chance");
        Check(MethodText(npc, "private void LandMarketMoves(").Contains("LandMarketRules.MonthFull(") && MethodText(npc, "internal bool LandSellOff(").Contains("ShortOfCash(g)")
              && MethodText(npc, "internal bool LandSellOff(").Contains("LandMarketRules.TradePrice(") && MethodText(npc, "internal bool LandSellOff(").Contains("LandSoldBack(seller)"),
            "land market sources: a developer short of cash sells a site to another developer or back to the city, within the month's limit");
        Check(npc.Contains("LandMarketRules.OfferPrice(") && npc.Contains("MarketTell(NotificationType.Info, \"reallife_land_notice_offer_in\"") && npc.Contains("LandMarketRules.Unused("),
            "land market sources: a rival's offer for the player's land is priced by the rules, only for land that has stood unused, and the player is told");
        Check(app.Contains("\"ofr:\"") && app.Contains("\"rvl:\"") && app.Contains("_director.Offer(growId, price)") && app.Contains("_director.Accept(id)") && app.Contains("_director.Refuse(id)") && app.Contains("_director.Sell(firstId)"),
            "land market sources: the Land tab has the rivals' sites, their offers, Offer to buy, Accept, Refuse and Sell this land");
        Check(ModSource(@"Construction\ConstructionApp.Land.cs").Contains("MarketButtons(box, lot.Id)") && ModSource(@"Construction\ConstructionApp.Land.cs").Contains("WriteMarketRows(box, ref selected, out bool marketPage)"),
            "land market sources: a site's page offers the sale and the neighbour a rival holds, and the tab lists the market's rows");
        Check(ModSource(@"Construction\ConstructionApp.cs").Contains("ConstructionDirector.TabLand ? \"ofr:\" + _wantPick"), "land market sources: a notice about an offer opens the Land tab on it");
        string[] testNames = { "an empty site of yours sold back to the city", "an offer to a rival developer accepted", "an offer under the ask refused and raised", "a rival's offer for your land accepted" };
        string[] methods = { "TestLandSellNow", "TestLandOfferAcceptedNow", "TestLandOfferRefusedNow", "TestLandRivalOfferNow" };
        for (int i = 0; i < testNames.Length; i++)
        {
            Check(tests.Contains("const string test = \"" + testNames[i] + "\";") && guide.Contains("\"Construction check (land market: " + testNames[i] + ")\"") && guide.Contains("ConstructionDirector." + methods[i] + "()")
                  && tests.Contains("public static bool " + methods[i] + "()"), "land market sources: the test '" + testNames[i] + "' is a public static method, in Options > Tests with the line it writes");
        }
        Check(tests.Contains("const string LandCheck = \"Construction check (land market: \"") && tests.Contains("\": \" + (pass ? \"PASS\" : \"FAIL\") + \".\""), "land market sources: the tests write a Construction check line ending PASS or FAIL");
        Check(Regex.Matches(tests, "LandTestPutBack\\(").Count >= 6 && tests.Contains("TestLandKey"), "land market sources: each test puts back the land, the developer's site and the money it moved");
        Check(ModSource(@"Construction\ConstructionTables.cs").Contains("LandRival = 4, LandOfferIn = 5") && ModSource(@"Construction\ConstructionTables.cs").Contains("\"reallife_land_kind_rival\"")
              && ModSource(@"Construction\ConstructionTables.cs").Contains("\"reallife_land_kind_offer\""), "land market sources: the Land tab's filter has the two new kinds of row");
        Check(rules.Contains("PlanningRules.AfterWorkingDays(day, 1)") && !rules.Contains("using UnityEngine") && !rules.Contains("GoingPublic."), "land market sources: the rules are game-free");
    }

    // ------------------------------------------------------------------ the texts

    private static void LandTextChecks()
    {
        var keys = new SortedSet<string>(StringComparer.Ordinal);
        foreach (string file in new[]
                 {
                     @"Construction\LandMarketRules.cs", @"Construction\ConstructionDirector.LandMarket.cs", @"Construction\ConstructionDirector.LandMarketNpc.cs", @"Construction\ConstructionDirector.LandMarketTests.cs",
                     @"Construction\ConstructionApp.Market.cs", @"Construction\ConstructionGuide.cs", @"Construction\ConstructionTables.cs"
                 })
        {
            foreach (Match m in Regex.Matches(ModSource(file), "\"(reallife_(?:land|transaction_bld_land|transaction_bld_test_land)_[a-z0-9_]*[a-z0-9]|reallife_transaction_bld_test_land)\""))
            {
                keys.Add(m.Groups[1].Value);
            }
        }
        foreach (string k in new[] { "reallife_transaction_bld_land_sold", "reallife_transaction_bld_land_rival", "reallife_transaction_bld_land_rival_sold", "reallife_transaction_bld_test_land" })
        {
            keys.Add(k);
        }
        Check(keys.Count >= 60, "land market texts: their keys are found: " + keys.Count);
        Dictionary<string, string>[] words = LanguageWords(out string[] codes);
        if (!LanguagesRead("land market texts", words, codes))
        {
            return;
        }
        for (int l = 0; l < words.Length && l < 3; l++)
        {
            string missing = null;
            foreach (string key in keys)
            {
                if (!words[l].TryGetValue(key, out string value) || value.Length == 0 || value.Contains('!') || value.Contains((char)0x2014) || value.Contains((char)0x2013) || value.Contains(" - ") || value.Contains("  "))
                {
                    missing = missing ?? key;
                }
                else if (l > 0 && words[0].TryGetValue(key, out string en) && NamesOf(en) != NamesOf(value))
                {
                    missing = missing ?? key + " (its names in curly brackets)";
                }
            }
            Check(missing == null, "land market texts: every key is in " + codes[l] + ".json, in the style, with the English names (" + (missing ?? "all") + ")");
        }
        foreach (string key in keys.Where(k => k.StartsWith("reallife_land_notice_", StringComparison.Ordinal) || k.StartsWith("reallife_land_answer_", StringComparison.Ordinal)))
        {
            for (int l = 0; l < words.Length && l < 3; l++)
            {
                string text = words[l].TryGetValue(key, out string t) ? t : string.Empty;
                Check(text.Length > 0 && text.Length <= 240, "land market texts: " + key + " at most 240 characters in " + codes[l] + " (" + text.Length + ")");
            }
        }
        var fixedNames = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "reallife_land_notice_offer_in", "{amount},{developer},{name}" }, { "reallife_land_notice_accepted", "{amount},{developer},{name}" }, { "reallife_land_notice_refused", "{amount},{developer},{name}" },
            { "reallife_land_notice_lapsed", "{amount},{developer},{name}" }, { "reallife_land_notice_nomoney", "{amount},{developer},{name}" },
            { "reallife_land_paper_bought", "{count},{name}" }, { "reallife_land_paper_back", "{count},{name}" }, { "reallife_land_paper_traded", "{count},{name}" },
            { "reallife_transaction_bld_land_sold", "{name}" }, { "reallife_transaction_bld_land_rival", "{name}" }, { "reallife_transaction_bld_land_rival_sold", "{name}" },
            { "reallife_land_confirm_offer_body", "{amount},{name}" }, { "reallife_land_confirm_accept_body", "{amount},{name}" }, { "reallife_land_confirm_sell_body", "{amount},{name}" },
            { "reallife_land_sell_note", "{paid},{share}" }, { "reallife_land_value_range", "{high},{low}" }, { "reallife_land_state_offer", "{day},{developer}" }
        };
        string fault = null;
        foreach (KeyValuePair<string, string> pair in fixedNames)
        {
            for (int l = 0; l < words.Length && l < 3; l++)
            {
                if (!words[l].TryGetValue(pair.Key, out string text) || NamesOf(text) != pair.Value)
                {
                    fault = fault ?? pair.Key + " in " + codes[l];
                }
            }
        }
        Check(fault == null, "land market texts: the notices, papers, books and questions carry the names the director gives them (" + (fault ?? "all") + ")");
        // The names a text asks for are the names the call gives it.
        int calls = 0;
        var wrong = new List<string>();
        foreach (string file in new[] { @"Construction\ConstructionApp.Market.cs" })
        {
            string source = ModSource(file);
            foreach (Match m in Regex.Matches(source, @"(?:ConstructionText\.)?(?:Fill|T)\(\s*""(reallife_land_[a-z0-9_]+)""\s*,"))
            {
                int open = source.IndexOf('(', m.Index + (m.Value.StartsWith("ConstructionText", StringComparison.Ordinal) ? "ConstructionText.".Length : 0));
                List<string> parts = SplitTopLevel(BalancedArguments(source, open));
                var given = new List<string>();
                for (int i = 1; i + 1 < parts.Count; i += 2)
                {
                    Match name = Regex.Match(parts[i].Trim(), "^\"([a-z0-9_]+)\"$");
                    if (name.Success)
                    {
                        given.Add("{" + name.Groups[1].Value + "}");
                    }
                }
                given.Sort(StringComparer.Ordinal);
                if (words[0].TryGetValue(m.Groups[1].Value, out string text))
                {
                    calls++;
                    if (NamesOf(text) != string.Join(",", given))
                    {
                        wrong.Add(m.Groups[1].Value + " (asks " + NamesOf(text) + ", given " + string.Join(",", given) + ")");
                    }
                }
            }
        }
        Check(calls >= 8 && wrong.Count == 0, "land market texts: " + calls + " texts are filled with the names they ask for (" + (wrong.Count == 0 ? "all" : string.Join("; ", wrong.Take(3))) + ")");
        // The Guide's section, in en, pt and zh-cn, with its facts; the lines of each are the same.
        string[] facts = { "land_back", "land_premium_least", "land_premium_most", "land_gap_days", "land_raises", "land_offer_days", "land_unused_days", "land_month_most" };
        string guideSource = ModSource(@"Construction\ConstructionGuide.cs");
        var sections = new string[Math.Min(3, words.Length)][];
        for (int l = 0; l < sections.Length; l++)
        {
            string page = words[l].TryGetValue("reallife_guide_construction", out string g) ? g : string.Empty;
            string[] heads = Regex.Matches(page, "\n# [^\n]*").Cast<Match>().Select(m => m.Value).ToArray();
            int at = Array.FindIndex(heads, h => h.Contains(l == 0 ? "Land market" : l == 1 ? "Mercado de terrenos" : "土地市场"));
            string section = string.Empty;
            if (at >= 0)
            {
                int from = page.IndexOf(heads[at], StringComparison.Ordinal) + 1;
                int to = at + 1 < heads.Length ? page.IndexOf(heads[at + 1], from, StringComparison.Ordinal) : page.Length;
                section = page.Substring(from, to - from).TrimEnd('\n');
            }
            sections[l] = section.Split('\n');
            Check(section.Length > 600 && facts.All(f => section.Contains("{" + f + "}")), "land market texts: the Guide has the land market's section in " + codes[l] + ", with its figures");
            Check(at > 0 && heads[at - 1].Contains(l == 0 ? "Joining and splitting land" : l == 1 ? "Juntar e dividir" : "合并与拆分"), "land market texts: the section follows the joining and splitting of land in " + codes[l]);
        }
        foreach (string f in facts)
        {
            Check(guideSource.Contains(".Fact(\"" + f + "\""), "land market texts: the Guide fact " + f + " is registered");
        }
        bool sameLines = sections.Length == 3 && sections[0].Length == sections[1].Length && sections[0].Length == sections[2].Length;
        for (int i = 0; sameLines && i < sections[0].Length; i++)
        {
            sameLines &= sections[0][i].StartsWith("- ", StringComparison.Ordinal) == sections[1][i].StartsWith("- ", StringComparison.Ordinal)
                         && sections[0][i].StartsWith("- ", StringComparison.Ordinal) == sections[2][i].StartsWith("- ", StringComparison.Ordinal) && NamesOf(sections[0][i]) == NamesOf(sections[1][i])
                         && NamesOf(sections[0][i]) == NamesOf(sections[2][i]);
        }
        Check(sameLines, "land market texts: the Guide section has the same lines, bullets and figures in en, pt and zh-cn");
        // What's new: the feature line once, in the row that holds it, among the feature lines, in every language.
        var starts = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "en", "Land market:" }, { "pt", "Mercado de terrenos:" }, { "zh-cn", "土地市场：" }, { "de", "Grundstücksmarkt:" }, { "fr", "Marché foncier :" }, { "es", "Mercado de terrenos:" },
            { "ru", "Рынок земли:" }, { "nl", "Grondmarkt:" }, { "tr", "Arsa piyasası:" }, { "pl", "Rynek gruntów:" }, { "ko", "토지 시장:" }
        };
        string[] marks = { "Fixed: ", "Corrigido: ", "已修复：", "Behoben: ", "Corrigé : ", "Corregido: ", "Исправлено: ", "Opgelost: ", "Düzeltildi: ", "Naprawiono: ", "수정: " };
        for (int l = 0; l < words.Length; l++)
        {
            string start = starts.TryGetValue(codes[l], out string s) ? s : "?";
            string next = FixRow(words[l], start);
            bool has = start.Length > 3 && next.Contains(start);
            Check(has, "land market texts: What's new has the land market's line in " + codes[l]);
            if (has)
            {
                int at = next.IndexOf(start, StringComparison.Ordinal);
                bool fixBefore = marks.Any(mark => { int f = next.IndexOf(mark, StringComparison.Ordinal); return f >= 0 && f < at; });
                Check(next.IndexOf(start, at + 1, StringComparison.Ordinal) < 0 && !fixBefore && !next.Contains("  ") && !next.Contains('!'),
                    "land market texts: its line once in its row, among the feature lines (no fix line before it), no double space or exclamation mark, in " + codes[l]);
            }
        }
    }
}
