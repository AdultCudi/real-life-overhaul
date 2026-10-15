using System;
using System.Collections.Generic;
using RealLifeOverhaul.Markets;
using RealLifeOverhaul.Rivals;

// Checks for the review's fixes to the rival tycoons: the rent rise capped at a business whatever runs there, the rivals
// standing still while off or at rest, the holdings read from the market each day, a new listing's guard for a stake or a bid,
// the answers' costs at the top and their limits, a takeover priced by the tycoon's fortune, each takeover's own pay day,
// tycoons spaced over days, a holding sized by its fortune on the market, and the source-level guarantees behind them.
internal static partial class Program
{
    private static void RivalReview()
    {
        RivalReviewRent();
        RivalReviewResume();
        RivalReviewListing();
        RivalReviewAnswers();
        RivalReviewTakeover();
        RivalReviewSpacing();
        RivalReviewHolding();
        RivalReviewSources();
    }

    // 1. One rented business, the hardest strength and a rich player for years: never two buyings up at once there, and the
    //    day's rise never past the most on its rent.
    private static void RivalReviewRent()
    {
        foreach (int strength in new[] { RivalCatalogue.Normal, RivalCatalogue.Ruthless })
        {
            foreach (long worth in new[] { 200000000L, 3000000000L, 20000000000L })
            {
                var f = RivalRules.Open(7, 1, 1000);
                RivalWorld w = RivalWorldOf(worth, 1);
                w.Shops[0] = new RivalShop { Place = "A~1", Name = "A", District = "d1", Type = "t1", Week = 100000, Rented = true, Rent = 1000 };
                var land = new List<RivalMove>();
                var end = new List<RivalMove>();
                int most = 0;
                long rise = 0;
                for (int day = 2; day < 2000; day++)
                {
                    RivalRules.Day(f, w, day, strength);
                    RivalRules.Due(f, day, land, end);
                    foreach (RivalMove m in land)
                    {
                        RivalRules.Landed(f, m, day, true);
                    }
                    foreach (RivalMove m in end)
                    {
                        RivalRules.Ended(m);
                    }
                    int live = 0;
                    foreach (RivalMove m in f.Moves)
                    {
                        live += m.Kind == RivalMoveKind.Property && m.Live && m.Place == "A~1" ? 1 : 0;
                    }
                    most = Math.Max(most, live);
                    rise = Math.Max(rise, RivalRules.RentRise(f, "A~1", 1000));
                }
                Check(most <= 1 && rise <= RivalCatalogue.RentRiseMost, "rivals review (rent): at worth " + worth + ", strength " + strength + ", at most one buying up at a business (" + most
                                                                        + ") and its rise never past the most (" + rise + " of " + RivalCatalogue.RentRiseMost + " on a rent of 1000)");
            }
        }
        var g = new RivalFile();
        g.Moves.Add(new RivalMove { Id = 1, Kind = RivalMoveKind.Property, State = RivalMoveState.Running, Place = "p", Rented = true, Second = 120 });
        g.Moves.Add(new RivalMove { Id = 2, Kind = RivalMoveKind.Property, State = RivalMoveState.Running, Place = "p", Rented = true, Second = 120 });
        Check(RivalRules.RentRise(g, "p", 1000) == RivalCatalogue.RentRiseMost, "rivals review (rent): two buyings up at one business together stop at the most");
    }

    // 2. Off or at rest, nothing moves: the missed days are not played, and every date moves on by them.
    private static void RivalReviewResume()
    {
        var f = RivalRules.Open(3, 1, 1000);
        RivalRules.Activate(f, 3, 1, 1000);
        f.SettledDay = 10;
        f.NextMoveDay = 15;
        f.Tycoons[0].QuietUntil = 18;
        f.Tycoons[1].QuietUntil = 5;
        f.Tycoons[2].BreakUp = 100;
        f.Tycoons[2].BreakUpDay = 13;
        var announced = new RivalMove { Id = 50, Kind = RivalMoveKind.Suit, State = RivalMoveState.Announced, Announced = 10, Land = 12, End = 12 };
        var running = new RivalMove { Id = 51, Kind = RivalMoveKind.Outlet, State = RivalMoveState.Running, Land = 8, End = 20, District = "d", Type = "t", Amount = 5 };
        f.Moves.Add(announced);
        f.Moves.Add(running);
        long worth0 = f.Tycoons[0].Worth;
        int skipped = RivalRules.Resume(f, 40);
        var land = new List<RivalMove>();
        var end = new List<RivalMove>();
        RivalRules.Due(f, 40, land, end);
        RivalDayReport r = RivalRules.Day(f, RivalWorldOf(3000000000L), 40, RivalCatalogue.Normal);
        Check(skipped == 29 && f.SettledDay == 40 && announced.Land == 41 && running.End == 49 && f.NextMoveDay >= 44 && land.Count == 0 && end.Count == 0 && r.Days == 1,
            "rivals review (resume): after 29 days off nothing is played or landed, and the announced move, the running one and the next move each move on by them");
        Check(f.Tycoons[0].QuietUntil == 47 && f.Tycoons[1].QuietUntil == 5 && f.Tycoons[2].BreakUpDay == 42 && f.Tycoons[0].Worth < worth0 + worth0 / 20,
            "rivals review (resume): a bar still in force and a break-up's day move on; a bar already over does not; the fortunes do not grow for the days off");
        Check(RivalRules.Resume(f, 41) == 0 && RivalRules.Resume(new RivalFile(), 50) == 0, "rivals review (resume): nothing skipped the day after, nor for a new file");
    }

    // 3 and 4. A stake needs the tycoon's holding listed here; a stake or a bid waits for the player's company to have traded a quarter.
    private static void RivalReviewListing()
    {
        var f = RivalRules.Open(4, 1, 1000);
        RivalRules.Activate(f, 3, 1, 1000);
        Tycoon t = f.Tycoons[0];
        RivalWorld w = RivalWorldOf(5000000000L);
        w.YearDays = 120;
        w.CompanyDays = 200;
        t.Listed = false;
        bool unlisted = !RivalRules.Available(f, t, RivalMoveKind.Stake, w, 50, 3);
        t.Listed = true;
        bool listed = RivalRules.Available(f, t, RivalMoveKind.Stake, w, 50, 3);
        w.CompanyDays = 120 / 4 - 1;
        bool young = !RivalRules.Available(f, t, RivalMoveKind.Stake, w, 50, 3);
        t.StakeDay = 45;
        bool youngBid = !RivalRules.Available(f, t, RivalMoveKind.Bid, w, 50, 3);
        w.CompanyDays = 120 / 4;
        bool oldBid = RivalRules.Available(f, t, RivalMoveKind.Bid, w, 50, 3);
        w.CompanyDays = -1;
        bool none = !RivalRules.CompanyOld(w);
        Check(unlisted && listed, "rivals review (listing): no stake while the tycoon's holding is not on the market here");
        Check(young && youngBid && oldBid && none, "rivals review (listing): no stake or bid against a company that has traded less than a quarter of the market's year");
    }

    // 5. The answers at the top cost their share of the worth; each is used once against one move; a piece in the paper never stops a bid.
    private static void RivalReviewAnswers()
    {
        bool scale = true;
        string broke = null;
        for (int tier = 2; tier < RivalCatalogue.Tiers; tier++)
        {
            long worth = RivalCatalogue.TierFrom[tier] * 3;
            for (int k = 0; k < (int)RivalCounter.Takeover; k++)
            {
                long cost = RivalRules.CounterCost((RivalCounter)k, worth, 1000);
                long share = worth / 10000L * RivalCatalogue.CounterShare[k];
                if (cost < share)
                {
                    scale = false;
                    broke = broke ?? ((RivalCounter)k + " at " + worth + ": " + cost + " against " + share);
                }
            }
        }
        long top = RivalRules.CounterCost(RivalCounter.Press, 50000000000L, 1000);
        Check(scale && top >= 50000000000L / 10000L * RivalCatalogue.CounterShare[(int)RivalCounter.Press], "rivals review (answers): from hundreds of millions up every answer costs its full share of the worth ("
                                                                                                           + top + " for a piece in the paper at 50 billion)" + (broke != null ? "; " + broke : string.Empty));
        var f = RivalRules.Open(6, 1, 1000);
        RivalRules.Activate(f, 3, 1, 1000);
        RivalWorld w = RivalWorldOf(5000000000L);
        RivalMove bid = RivalRules.Make(f, f.Tycoons[2], RivalMoveKind.Bid, w, 20, 3, RivalCatalogue.Normal);
        RivalCounterNote press = RivalRules.Counter(f, 2, RivalCounter.Press, 20, 1000, 1000);
        Check(press != null && press.Move == 0 && bid.State == RivalMoveState.Announced, "rivals review (answers): a piece in the paper never stops a bid (the founder's defences answer a bid)");
        RivalMove lobby = RivalRules.Make(f, f.Tycoons[0], RivalMoveKind.Lobby, w, 30, 3, RivalCatalogue.Normal);
        RivalRules.Landed(f, lobby, 32, true);
        RivalRules.Counter(f, 0, RivalCounter.Lobby, 33, 1000, 1000);
        lobby.State = RivalMoveState.Running;   // (were it still running, the same answer is not used on it twice)
        Check(RivalRules.CanCounter(f, 0, RivalCounter.Lobby, 33 + RivalCatalogue.CounterRestDays[(int)RivalCounter.Lobby], out RivalMove _) == RivalWhy.Rest,
            "rivals review (answers): each answer once against one move");
        RivalMove prop = RivalRules.Make(f, f.Tycoons[1], RivalMoveKind.Property, w, 40, 3, RivalCatalogue.Normal);
        RivalRules.Counter(f, 1, RivalCounter.Outbid, 40, 1000, 1000);
        RivalMove outlet = RivalRules.Make(f, f.Tycoons[1], RivalMoveKind.Outlet, w, 41, 3, RivalCatalogue.Normal);
        Check(prop.State == RivalMoveState.Stopped && outlet != null && RivalRules.CanCounter(f, 1, RivalCounter.Outbid, 41, out RivalMove _) == RivalWhy.Rest
              && RivalRules.CanCounter(f, 1, RivalCounter.Outbid, 40 + RivalCatalogue.CounterRestDays[(int)RivalCounter.Outbid], out RivalMove _) == RivalWhy.None,
            "rivals review (answers): outbidding the same rival rests a week");
    }

    // 5. A takeover costs about the tycoon's fortune at every tier, and grows with it.
    private static void RivalReviewTakeover()
    {
        bool range = true;
        string broke = null;
        for (int tier = 1; tier < RivalCatalogue.Tiers; tier++)
        {
            for (int k = 0; k < RivalCatalogue.TycoonCount; k++)
            {
                var f = new RivalFile();
                Tycoon t = f.Tycoons[k];
                long fortune = RivalRules.Aim(t, RivalCatalogue.TierFrom[tier] * 2, tier, RivalCatalogue.Normal, 1000);
                // A holding listed at its fortune: some shares at a price, its most held by the player.
                long shares = 50000000L;
                int mid = (int)Math.Max(1L, fortune * 100L / shares);
                long held = shares / 10;
                RivalRules.TakeoverTerms(shares, held, mid, fortune, out long cost, out long breakUp);
                long ownPaid = held * mid / 100L;
                long net = cost - breakUp;   // (the player's own shares bought and paid back at the same price)
                if (net < fortune / 5 || net > fortune / 2 || ownPaid > fortune / 5)
                {
                    range = false;
                    broke = broke ?? ("tier " + tier + ", tycoon " + k + ": fortune " + fortune + ", net " + net);
                }
                RivalRules.TakeoverTerms(shares, held, mid, fortune * 3, out long later, out long _);
                range &= later >= cost * 29 / 10;
            }
        }
        Check(range, "rivals review (takeover): a takeover's net cost is a fifth to a half of the tycoon's fortune at every tier, and grows with the fortune" + (broke != null ? "; " + broke : string.Empty));
        var g = RivalRules.Open(2, 1, 1000);
        RivalRules.Activate(g, 3, 1, 1000);
        RivalRules.Defeat(g, 0, 10, 1, 500);
        RivalRules.Defeat(g, 1, 12, 1, 700);
        long a = RivalRules.BreakUpDue(g.Tycoons[0], 10 + RivalCatalogue.BreakUpDays) + RivalRules.BreakUpDue(g.Tycoons[1], 10 + RivalCatalogue.BreakUpDays);
        long b = RivalRules.BreakUpDue(g.Tycoons[1], 12 + RivalCatalogue.BreakUpDays);
        Check(a == 500 && b == 700 && RivalRules.BreakUpToCome(g, out int _) == 0, "rivals review (takeover): each takeover's parts are paid on its own day, a second one moving neither");
    }

    // A rich player on a first day meets the tycoons over three days, one a day.
    private static void RivalReviewSpacing()
    {
        var f = RivalRules.Open(5, 1, 1000);
        RivalWorld w = RivalWorldOf(5000000000L);
        int[] came = new int[3];
        for (int day = 1; day <= 3; day++)
        {
            RivalRules.Day(f, w, day, RivalCatalogue.Normal);
            for (int i = 0; i < f.Tycoons.Length; i++)
            {
                came[day - 1] += f.Tycoons[i].ActiveDay >= 0 ? 1 : 0;
            }
        }
        Check(came[0] == 1 && came[1] == 2 && came[2] == 3, "rivals review (spacing): a player rich from the start meets the tycoons one a day (" + came[0] + ", " + came[1] + ", " + came[2] + ")");
    }

    // A holding on the market is sized by its tycoon's fortune; a holding with no tycoon leaves it.
    private static void RivalReviewHolding()
    {
        ulong seed = 0xABCDEF01UL;
        var world = new OwnWorld(seed) { Company = false };
        MarketState state = MarketSetup.Open(seed, 100, 0, 60, world.Env(100), seed, null, null, null);
        world.State = () => state;
        var engine = new MarketEngine(state, new MarketTape(seed), world);
        bool sized = true;
        string broke = null;
        long[] fortunes = { 40000000L, 900000000L, 12000000000L };
        for (int i = 0; i < RivalCatalogue.TycoonCount; i++)
        {
            TycoonDef d = RivalCatalogue.Tycoons[i];
            bool listed = engine.ListHolding(d.StockId, fortunes[i]);
            Stock s = state.Stock(d.StockId);
            double cap = s != null ? s.Shares * (double)s.MidCents / 100.0 : 0.0;
            if (!listed || s == null || Math.Abs(cap / fortunes[i] - 1.0) > 0.05)
            {
                sized = false;
                broke = broke ?? (d.Holding + ": " + cap.ToString("N0") + " against " + fortunes[i]);
            }
        }
        Check(sized, "rivals review (holding): a holding comes to the market worth its tycoon's fortune, within five per cent" + (broke != null ? "; " + broke : string.Empty));
        Stock first = state.Stock(RivalCatalogue.Tycoons[0].StockId);
        long before = first != null ? first.Shares : 0;
        bool again = engine.ListHolding(RivalCatalogue.Tycoons[0].StockId, fortunes[0] * 10);
        Check(again && first != null && first.Shares == before && !engine.ListHolding(1, fortunes[0]), "rivals review (holding): a holding listed already is left as it is, and no other company is a holding");
        bool gone = engine.DelistHolding(RivalCatalogue.Tycoons[1].StockId);
        Stock second = state.Stock(RivalCatalogue.Tycoons[1].StockId);
        Check(gone && second != null && !second.Listed && !engine.DelistHolding(1), "rivals review (holding): a holding with no tycoon leaves the market; no other company is taken off that way");
    }

    // What the review asked of the game side, held at the source: the holdings read before the day's rules, the stake's demand
    // and the new listing's guard, the break-ups paid when the rivals stop, the before-tests copy this session's alone.
    private static void RivalReviewSources()
    {
        string root = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        string director = System.IO.File.ReadAllText(System.IO.Path.Combine(root, @"Rivals\RivalDirector.cs"));
        string market = System.IO.File.ReadAllText(System.IO.Path.Combine(root, @"Markets\MarketDirector.Rivals.cs"));
        int read = director.IndexOf("Holdings();", StringComparison.Ordinal), rules = director.IndexOf("RivalRules.Day(_file, _world", StringComparison.Ordinal);
        Check(read > 0 && rules > read, "rivals review (sources): the holdings are read from the market each day before the rules look at them");
        Check(!market.Contains("State.Day + 1000") && market.Contains("|| Young(own)") && market.Contains("Young(own) || !_engine.BidBy"),
            "rivals review (sources): a rival's stake reads the company as it is today, and neither a stake nor a bid touches a company newer than a quarter");
        Check(director.Contains("PayBreakUps(Save.Day);") && director.Contains("PayBreakUps(int.MaxValue);") && director.Contains("Save.modData?.Remove(RivalStore.TestKey);")
              && director.Contains("Resumed(Save.Day);") && director.Contains("Resumed(day);"),
            "rivals review (sources): break-ups paid when the rivals stop or are forgotten, the copy from before the tests dropped at a start, and the rivals resumed after being off or at rest");
    }
}
