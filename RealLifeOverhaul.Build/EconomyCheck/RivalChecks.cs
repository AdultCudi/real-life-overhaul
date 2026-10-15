using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Markets;
using RealLifeOverhaul.Rivals;

// Checks for the rival tycoons (Scripts/Rivals: RivalCatalogue, RivalModel, RivalRules, RivalStore): their holdings on the
// stock market, how their fortunes grow by the player's thresholds, the pace of their moves, each move's bounds whatever the
// tier and the strength, the player's counters and a takeover, the same moves from the same seed, the save's line both ways,
// a day's round in time, and every text in the three locales with the Guide's figures and What's new.
internal static partial class Program
{
    private static void Rivals()
    {
        RivalHoldings();
        RivalGrowth();
        RivalPacing();
        RivalLimits();
        RivalCounters();
        RivalTakeover();
        RivalDeterminism();
        RivalSave();
        RivalSpeed();
        RivalWords();
        RivalReview();   // the review's fixes (RivalReviewChecks.cs)
    }

    // A world for the rules: the player's worth, a few businesses of theirs, every feature a move goes through on.
    private static RivalWorld RivalWorldOf(long worth, int shops = 3, bool listed = true)
    {
        var w = new RivalWorld { Worth = worth, Level = 1000, Cycle = 1, Listed = listed, Market = true, Staff = true, Issues = true, Court = true, Paper = true, CompanyDays = listed ? 1000 : -1, YearDays = 120 };
        for (int i = 0; i < shops; i++)
        {
            w.Shops.Add(new RivalShop
            {
                Place = "Main Street~" + (10 + i), Name = "Shop " + i, District = i % 2 == 0 ? "ba:neighborhood_midtown" : "ba:neighborhood_garment",
                Type = i == 2 ? "ba:businesstype_bakery" : "ba:businesstype_clothingstore", Week = 7000L * (i + 1), Rented = i != 1, Rent = 400 + 100 * i
            });
        }
        return w;
    }

    // ---------------------------------------------------------------- the holdings on the stock market

    private static void RivalHoldings()
    {
        bool each = RivalCatalogue.Tycoons.Length == RivalCatalogue.TycoonCount;
        var ids = new HashSet<int>();
        for (int i = 0; i < RivalCatalogue.Tycoons.Length; i++)
        {
            TycoonDef t = RivalCatalogue.Tycoons[i];
            CompanyDef d = MarketCatalogue.Find(t.StockId);
            each &= d != null && d.Holding && d.Pipeline && d.Name == t.Holding && ids.Add(t.StockId) && t.Weights.Length == (int)RivalMoveKind.Count && t.StartWorth > 0;
        }
        Check(each, "rivals: three tycoons, each with a holding of its own in the market's catalogue, of the same name, kept off the start's market and the pipeline (a pipeline and holding company), a liking for each kind of move");
        bool onlyThree = true;
        foreach (CompanyDef d in MarketCatalogue.All)
        {
            onlyThree &= !d.Holding || ids.Contains(d.Id);
        }
        Check(onlyThree, "rivals: no holding in the market's catalogue that is not a tycoon's");
    }

    // ---------------------------------------------------------------- growth by the player's thresholds

    private static void RivalGrowth()
    {
        // The tiers, at their thresholds and a dollar under, at the start's prices and at twice them.
        bool tiers = RivalRules.Tier(0, 1000) == 0;
        for (int i = 1; i < RivalCatalogue.Tiers; i++)
        {
            tiers &= RivalRules.Tier(RivalCatalogue.TierFrom[i], 1000) == i && RivalRules.Tier(RivalCatalogue.TierFrom[i] - 1, 1000) == i - 1
                     && RivalRules.Tier(RivalCatalogue.TierFrom[i] * 2, 2000) == i && RivalRules.Tier(RivalCatalogue.TierFrom[i] * 2 - 1, 2000) == i - 1;
        }
        Check(tiers, "rivals: the player's tier from the thresholds (quiet, tens of millions, hundreds of millions, billions, ten billion), read at today's prices");

        // Who comes forward: none while quiet, then one, two, three, and they stay when the player falls back.
        var f = RivalRules.Open(11, 1, 1000);
        int[] want = { 0, 1, 2, 3, 3 };
        bool forward = true;
        for (int tier = 0; tier < RivalCatalogue.Tiers; tier++)
        {
            RivalRules.Activate(f, tier, 10 + tier, 1000);
            int n = 0;
            for (int i = 0; i < f.Tycoons.Length; i++)
            {
                n += f.Tycoons[i].Active ? 1 : 0;
            }
            forward &= n == want[tier];
        }
        RivalRules.Activate(f, 0, 30, 1000);
        forward &= f.Tycoons[0].Active && f.Tycoons[2].Active && f.Tycoons[0].ActiveDay == 11 && f.Tycoons[2].ActiveDay == 13;
        Check(forward, "rivals: nobody comes forward while the player is quiet, then one at tens of millions, two at hundreds of millions, three at billions, in their order, and they stay");

        // A tycoon follows a player who passes a threshold: within a season they are near their aim, never past it by the catch-up alone.
        var g = RivalRules.Open(5, 1, 1000);
        RivalWorld w = RivalWorldOf(2000000000L);
        g.NextMoveDay = int.MaxValue;   // (growth alone)
        RivalRules.Day(g, w, 1, RivalCatalogue.Normal);
        Tycoon t0 = g.Tycoons[0];
        long aim = RivalRules.Aim(t0, w.Worth, RivalRules.Tier(w.Worth, 1000), RivalCatalogue.Normal, 1000);
        long start = t0.Worth;
        bool bounded = true;
        for (int d = 2; d <= 120; d++)
        {
            long before = t0.Worth;
            RivalRules.Day(g, w, d, RivalCatalogue.Normal);
            long gain = t0.Worth - before;
            bounded &= gain <= before / 50L;   // (two per cent of their worth: the catch-up's most and a steady month's thirtieth, written out here so a larger catch-up fails)
        }
        Check(aim == w.Worth / 1000L * (RivalCatalogue.Tycoons[0].TargetPermille * RivalCatalogue.StrengthAim[RivalCatalogue.Normal] / 1000), "rivals: a tycoon's aim is their share of the player's worth");
        Tycoon small = new RivalFile().Tycoons[0];
        small.Worth = RivalRules.At(small.Def.StartWorth, 1000);
        long smallGain = RivalRules.Grow(small, 900000000000L, 0, 1, 1000) - small.Worth;
        bounded &= smallGain > 0 && smallGain <= small.Worth / 50L;
        Check(bounded, "rivals: a tycoon's day never gains more than two per cent of their worth, however far behind their aim (no rival made overnight)");
        Check(start >= aim / 1000L * RivalCatalogue.ForwardShare && t0.Worth >= aim * 8 / 10 && t0.Worth <= aim + aim / 100,
            "rivals: a tycoon comes forward near a player already rich and follows them: from " + start + " to " + t0.Worth + " in 120 days, aim " + aim);

        // Quiet player: only the cycle moves them; a recession shrinks them but never under their start; a counter halves their gain.
        Tycoon q = new RivalFile().Tycoons[1];
        q.Worth = RivalRules.At(q.Def.StartWorth, 1000);
        long floor = q.Worth;
        long quietAim = RivalRules.Aim(q, 1000000L, 0, RivalCatalogue.Ruthless, 1000);
        long boom = RivalRules.Grow(q, quietAim, 0, 5, 1000) - q.Worth;
        long recession = RivalRules.Grow(q, quietAim, 3, 5, 1000);
        q.SlowUntil = 10;
        long slowed = RivalRules.Grow(q, quietAim, 0, 5, 1000) - q.Worth;
        Check(quietAim == floor && boom > 0 && recession == floor && slowed * 2 <= boom + 1 && slowed > 0,
            "rivals: with the player quiet a tycoon aims at their start alone; a boom grows them, a recession never takes them under their start, a counter halves their gain");

        // Strength: a harder setting aims higher.
        Tycoon s = new RivalFile().Tycoons[2];
        bool stronger = true;
        for (int k = 1; k < RivalCatalogue.Strengths; k++)
        {
            stronger &= RivalRules.Aim(s, 5000000000L, 3, k, 1000) > RivalRules.Aim(s, 5000000000L, 3, k - 1, 1000);
        }
        Check(stronger, "rivals: every step of the strength makes the tycoons aim higher");
    }

    // ---------------------------------------------------------------- the pace of their moves

    private static void RivalPacing()
    {
        for (int strength = 0; strength < RivalCatalogue.Strengths; strength++)
        {
            for (int tier = 0; tier < RivalCatalogue.Tiers; tier++)
            {
                long worth = tier == 0 ? 1000000L : RivalCatalogue.TierFrom[tier] * 2;
                var f = RivalRules.Open(77 + strength, 1, 1000);
                RivalWorld w = RivalWorldOf(worth);
                var land = new List<RivalMove>();
                var end = new List<RivalMove>();
                int last = -1000, moves = 0, minGap = int.MaxValue;
                bool onePending = true, telegraphed = true;
                for (int day = 1; day <= 365; day++)
                {
                    RivalDayReport r = RivalRules.Day(f, w, day, strength);
                    int pending = 0;
                    foreach (RivalMove m in f.Moves)
                    {
                        pending += m.State == RivalMoveState.Announced ? 1 : 0;
                    }
                    onePending &= pending <= 1;
                    if (r.Announced > 0)
                    {
                        RivalMove m = f.Move(r.Announced);
                        telegraphed &= m.Land - m.Announced == RivalCatalogue.TelegraphDays;
                        minGap = Math.Min(minGap, day - last);
                        last = day;
                        moves++;
                    }
                    RivalRules.Due(f, day, land, end);
                    foreach (RivalMove m in land)
                    {
                        RivalRules.Landed(f, m, day, true);
                    }
                    foreach (RivalMove m in end)
                    {
                        RivalRules.Ended(m);
                    }
                }
                if (tier == 0)
                {
                    Check(moves == 0, "rivals pace: a quiet player sees no move in a year (strength " + strength + ")");
                    continue;
                }
                int gap = RivalRules.Gap(tier, strength);
                Check(minGap >= gap, "rivals pace: never two moves closer than the gap (tier " + tier + ", strength " + strength + ": " + minGap + " against " + gap + " days)");
                Check(moves >= 365 / (gap + RivalCatalogue.JitterDays + RivalCatalogue.TelegraphDays + 1) / 2, "rivals pace: moves keep coming (tier " + tier + ", strength " + strength + ": " + moves + " in a year)");
                Check(onePending && telegraphed, "rivals pace: one move announced at a time, each " + RivalCatalogue.TelegraphDays + " days before it comes (tier " + tier + ", strength " + strength + ")");
            }
        }
        Check(RivalRules.Gap(4, RivalCatalogue.Normal) <= 4 && RivalRules.Gap(1, RivalCatalogue.Normal) >= 10 && RivalRules.Gap(1, RivalCatalogue.Gentle) > RivalRules.Gap(1, RivalCatalogue.Normal),
            "rivals pace: a move every few days at most in the end game, more than ten days apart for a player worth tens of millions, longer on Gentle");
        Check(RivalRules.Power(1, RivalCatalogue.Normal) < RivalRules.Power(2, RivalCatalogue.Normal) && RivalRules.Power(3, RivalCatalogue.Normal) < RivalRules.Power(4, RivalCatalogue.Normal),
            "rivals pace: the moves hit harder at every tier past the first");
    }

    // ---------------------------------------------------------------- each move's bounds

    private static void RivalLimits()
    {
        bool bounded = true;
        string broke = null;
        long[] worths = { 1000L, 5000000L, 250000000L, 3000000000L, 20000000000L, 900000000000L };
        foreach (long worth in worths)
        {
            RivalWorld w = RivalWorldOf(worth);
            for (int tier = 1; tier < RivalCatalogue.Tiers; tier++)
            {
                for (int strength = 0; strength < RivalCatalogue.Strengths; strength++)
                {
                    for (int k = 0; k < (int)RivalMoveKind.Count; k++)
                    {
                        var m = new RivalMove { Kind = (RivalMoveKind)k, Rented = true };
                        RivalRules.Size(m, w, tier, strength);
                        bool ok;
                        switch (m.Kind)
                        {
                            case RivalMoveKind.Property: ok = m.Amount >= 1 && m.Amount <= RivalCatalogue.PropertyPremiumMost && m.Second >= 1 && m.Second <= RivalCatalogue.RentRiseMost; break;
                            case RivalMoveKind.Outlet: ok = m.Amount >= 1 && m.Amount <= RivalCatalogue.OutletDipMost; break;
                            case RivalMoveKind.Poach: ok = m.Amount >= 1 && m.Amount <= RivalCatalogue.PoachPremiumMost; break;
                            case RivalMoveKind.Lobby: ok = m.Amount >= 1 && m.Amount <= RivalCatalogue.LobbyWeightMost; break;
                            case RivalMoveKind.Suit:
                                ok = m.Amount >= RivalCatalogue.SuitLeast && m.Amount <= RivalCatalogue.SuitMost
                                     && (m.Amount <= worth / 10000L * RivalCatalogue.SuitShareMost || m.Amount == RivalCatalogue.SuitLeast);
                                break;
                            case RivalMoveKind.Press: ok = m.Amount == RivalCatalogue.PressCustomers; break;
                            default: ok = m.Amount == 0; break;
                        }
                        if (!ok)
                        {
                            bounded = false;
                            broke = broke ?? (m.Kind + " at worth " + worth + ", tier " + tier + ", strength " + strength + ": " + m.Amount + "/" + m.Second);
                        }
                    }
                }
            }
        }
        Check(bounded, "rivals limits: every move inside its most at every worth, tier and strength (a suit at most half a per cent of the worth and never past its cap)" + (broke != null ? "; " + broke : string.Empty));

        // The effects as the other features read them: outlets together, the premium, the rent rise, the inspections.
        var f = new RivalFile();
        for (int i = 0; i < 4; i++)
        {
            f.Moves.Add(new RivalMove { Id = i + 1, Kind = RivalMoveKind.Outlet, State = RivalMoveState.Running, District = "d", Type = "t", Amount = RivalCatalogue.OutletDipMost - 1 });
        }
        f.Moves.Add(new RivalMove { Id = 9, Kind = RivalMoveKind.Outlet, State = RivalMoveState.Announced, District = "d2", Type = "t", Amount = 5 });
        f.Moves.Add(new RivalMove { Id = 10, Kind = RivalMoveKind.Property, State = RivalMoveState.Running, District = "d", Place = "p", Amount = 999, Second = 999, Rented = true });
        f.Moves.Add(new RivalMove { Id = 11, Kind = RivalMoveKind.Lobby, State = RivalMoveState.Running, Amount = 5000 });
        Check(RivalRules.OutletDip(f, "d", "t") == -RivalCatalogue.OutletDipMost && RivalRules.OutletDip(f, "d", "other") == 0 && RivalRules.OutletDip(f, "d2", "t") == 0 && RivalRules.OutletDip(null, "d", "t") == 0,
            "rivals limits: rivals' businesses together never take more than the most from one of the player's, only of its kind in its district, and only once they are open");
        Check(RivalRules.Premium(f, "d") == RivalCatalogue.PropertyPremiumMost && RivalRules.Premium(f, "x") == 0 && RivalRules.InspectionWeight(f) == RivalCatalogue.LobbyWeightMost,
            "rivals limits: the premium on buildings and the inspections' weight never past their most, and nothing elsewhere");
        Check(RivalRules.RentRise(f, "p", 1000) == 1000L * RivalCatalogue.RentRiseMost / 1000 && RivalRules.RentRise(f, "p", 0) == 0 && RivalRules.RentRise(f, "q", 1000) == 0,
            "rivals limits: a rent rise is never more than the most on the rent, and nothing without a rent");

        // Moves are aimed at the player's best business, an outlet only where none of a rival's is, and nothing without a business.
        var g = RivalRules.Open(3, 1, 1000);
        RivalRules.Activate(g, 3, 1, 1000);
        RivalWorld two = RivalWorldOf(3000000000L);
        RivalMove p = RivalRules.Make(g, g.Tycoons[0], RivalMoveKind.Property, two, 5, 3, RivalCatalogue.Normal);
        RivalMove o1 = RivalRules.Make(g, g.Tycoons[0], RivalMoveKind.Outlet, two, 5, 3, RivalCatalogue.Normal);
        RivalMove o2 = RivalRules.Make(g, g.Tycoons[1], RivalMoveKind.Outlet, two, 5, 3, RivalCatalogue.Normal);
        Check(p != null && p.Place == two.Shops[2].Place && o1 != null && o1.Place == two.Shops[2].Place && o2 != null && o2.Place == two.Shops[1].Place,
            "rivals limits: a move is aimed at the player's best business; a second rival business goes to the next best kind and district");
        RivalWorld none = RivalWorldOf(3000000000L, 0, false);
        Check(RivalRules.Make(g, g.Tycoons[0], RivalMoveKind.Outlet, none, 5, 3, RivalCatalogue.Normal) == null && !RivalRules.Available(g, g.Tycoons[0], RivalMoveKind.Stake, none, 5, 3)
              && !RivalRules.Available(g, g.Tycoons[0], RivalMoveKind.Property, none, 5, 3) && RivalRules.Available(g, g.Tycoons[0], RivalMoveKind.Press, none, 5, 3),
            "rivals limits: nothing aimed at a business the player does not have, no stake without a listed company, a piece in the paper needs none");
        Check(!RivalRules.Available(g, g.Tycoons[0], RivalMoveKind.Stake, two, 5, RivalCatalogue.StakeTier - 1) && !RivalRules.Available(g, g.Tycoons[0], RivalMoveKind.Bid, two, 5, 3),
            "rivals limits: no stake below hundreds of millions, no bid without the same rival's stake first");
        g.Tycoons[0].StakeDay = 5;
        g.Tycoons[0].Listed = true;
        Check(RivalRules.Available(g, g.Tycoons[0], RivalMoveKind.Bid, two, 6, 3) && !RivalRules.Available(g, g.Tycoons[0], RivalMoveKind.Bid, two, 6, 2)
              && !RivalRules.Available(g, g.Tycoons[0], RivalMoveKind.Bid, two, 5 + RivalCatalogue.BidAfterStakeDays + 1, 3),
            "rivals limits: a bid only from billions, only within its days of the stake");
    }

    // ---------------------------------------------------------------- the player's counters

    private static void RivalCounters()
    {
        bool costs = true;
        long[] worths = { 0L, 1000000L, 300000000L, 9000000000L, 500000000000L };
        foreach (long worth in worths)
        {
            for (int k = 0; k < (int)RivalCounter.Takeover; k++)
            {
                long c = RivalRules.CounterCost((RivalCounter)k, worth, 1000);
                costs &= c >= RivalCatalogue.CounterLeast[k] && c <= RivalCatalogue.CounterMost[k] * RivalCatalogue.CounterTierScale[RivalRules.Tier(worth, 1000)];
            }
        }
        Check(costs && RivalRules.CounterCost(RivalCounter.Takeover, 1000000000L, 1000) == 0, "rivals counters: each counter costs a share of the worth between its least and its most (a takeover is priced by the market)");

        var f = RivalRules.Open(9, 1, 1000);
        RivalRules.Activate(f, 3, 1, 1000);
        RivalWorld w = RivalWorldOf(3000000000L);
        int day = 20;
        Check(RivalRules.CanCounter(f, 0, RivalCounter.Outbid, day, out RivalMove _) == RivalWhy.Nothing, "rivals counters: nothing to outbid with no buying up or business of theirs");
        RivalMove prop = RivalRules.Make(f, f.Tycoons[0], RivalMoveKind.Property, w, day, 3, RivalCatalogue.Normal);
        RivalRules.Landed(f, prop, day + 2, true);
        RivalCounterNote outbid = RivalRules.Counter(f, 0, RivalCounter.Outbid, day + 3, 1000, 1000);
        Check(outbid != null && outbid.Move == prop.Id && prop.State == RivalMoveState.Stopped && RivalRules.Premium(f, prop.District) == 0, "rivals counters: outbidding ends a buying up at once, and its premium with it");

        RivalRules.Counter(f, 1, RivalCounter.Poach, day, 1000, 1000);
        Check(f.Tycoons[1].QuietUntil == day + RivalCatalogue.PoachQuietDays && f.Tycoons[1].SlowUntil == day + RivalCatalogue.PoachSlowDays
              && RivalRules.CanCounter(f, 1, RivalCounter.Poach, day + 1, out RivalMove _) == RivalWhy.Rest
              && RivalRules.CanCounter(f, 1, RivalCounter.Poach, day + RivalCatalogue.CounterRestDays[(int)RivalCounter.Poach], out RivalMove _) == RivalWhy.None,
            "rivals counters: hiring away a rival's people quiets them and slows their growth; it waits its rest before it can be used on them again");
        Check(RivalRules.Announce(f, w, day + 1, RivalCatalogue.Normal, true) == null || f.Pending().Tycoon != 1, "rivals counters: a quieted rival makes no move");

        // A piece in the paper: an announced move of theirs never comes; with none, their next waits and their worth falls.
        var g = RivalRules.Open(21, 1, 1000);
        RivalRules.Activate(g, 3, 1, 1000);
        RivalMove announced = RivalRules.Make(g, g.Tycoons[2], RivalMoveKind.Suit, w, day, 3, RivalCatalogue.Normal);
        RivalRules.Counter(g, 2, RivalCounter.Press, day, 1000, 1000);
        var land = new List<RivalMove>();
        var end = new List<RivalMove>();
        RivalRules.Due(g, announced.Land, land, end);
        long worthBefore = g.Tycoons[1].Worth = 900000000L;
        RivalRules.Counter(g, 1, RivalCounter.Press, day, 1000, 1000);
        Check(announced.State == RivalMoveState.Stopped && land.Count == 0 && g.Tycoons[1].Worth == worthBefore - worthBefore / 1000 * RivalCatalogue.PressWorthCut
              && g.Tycoons[1].QuietUntil == day + RivalCatalogue.PressDelayDays, "rivals counters: a piece in the paper stops a move announced; with none, it delays their next and cuts their worth");

        // Lobbying back: a running lobbying ends, and no lobbying or buying up for the bar's days.
        var h = RivalRules.Open(31, 1, 1000);
        RivalRules.Activate(h, 3, 1, 1000);
        RivalMove lobby = RivalRules.Make(h, h.Tycoons[0], RivalMoveKind.Lobby, w, day, 3, RivalCatalogue.Normal);
        RivalRules.Landed(h, lobby, day + 2, true);
        RivalRules.Counter(h, 0, RivalCounter.Lobby, day + 3, 1000, 1000);
        Check(lobby.State == RivalMoveState.Stopped && RivalRules.InspectionWeight(h) == 0 && !RivalRules.Available(h, h.Tycoons[0], RivalMoveKind.Lobby, w, day + 4, 3)
              && !RivalRules.Available(h, h.Tycoons[0], RivalMoveKind.Property, w, day + 4, 3) && RivalRules.Available(h, h.Tycoons[0], RivalMoveKind.Property, w, day + 3 + RivalCatalogue.LobbyBarDays, 3),
            "rivals counters: lobbying back ends their lobbying and bars lobbying and buying up for its days");
        Check(RivalRules.CanCounter(new RivalFile(), 0, RivalCounter.Press, day, out RivalMove _) == RivalWhy.Out && RivalRules.Counter(new RivalFile(), 0, RivalCounter.Poach, day, 1, 1000) == null,
            "rivals counters: nothing against a tycoon who has not come forward");
    }

    // ---------------------------------------------------------------- a takeover of a holding

    private static void RivalTakeover()
    {
        RivalRules.TakeoverTerms(10000000L, 1000000L, 4000, 0L, out long cost, out long breakUp);
        long others = 9000000L * 40L;
        Check(cost == others + others / 10000L * RivalCatalogue.TakeoverPremiumBp && breakUp == others / 1000L * RivalCatalogue.BreakUpPermille,
            "rivals takeover: the shares not held at the price and the premium; the parts bring back their share of what those shares were worth (" + cost + ", " + breakUp + ")");
        RivalRules.TakeoverTerms(4000000000000L, 0, 99999999, 0L, out long big, out long _);
        Check(big > 0, "rivals takeover: the largest holdings at the largest prices without passing the long's range");
        var f = RivalRules.Open(41, 1, 1000);
        RivalRules.Activate(f, 3, 1, 1000);
        Check(RivalRules.CanTakeOver(f, 0, false, 100, 100) == RivalWhy.NoHolding && RivalRules.CanTakeOver(f, 0, true, 99, 100) == RivalWhy.Short && RivalRules.CanTakeOver(f, 0, true, 100, 100) == RivalWhy.None
              && RivalRules.CanTakeOver(f, 0, true, 0, 0) == RivalWhy.Short, "rivals takeover: only with the holding listed and the most one holder may have");
        RivalWorld w = RivalWorldOf(3000000000L);
        RivalMove m = RivalRules.Make(f, f.Tycoons[0], RivalMoveKind.Outlet, w, 10, 3, RivalCatalogue.Normal);
        RivalRules.Defeat(f, 0, 12, cost, breakUp);
        bool noMore = true;
        for (int d = 12; d < 200; d++)
        {
            RivalMove n = RivalRules.Announce(f, w, d, RivalCatalogue.Ruthless, true);
            noMore &= n == null || n.Tycoon != 0;
            if (n != null)
            {
                n.State = RivalMoveState.Over;
            }
        }
        long early = RivalRules.BreakUpDue(f.Tycoons[0], 12 + RivalCatalogue.BreakUpDays - 1);
        long due = RivalRules.BreakUpDue(f.Tycoons[0], 12 + RivalCatalogue.BreakUpDays);
        long again = RivalRules.BreakUpDue(f.Tycoons[0], 12 + RivalCatalogue.BreakUpDays + 1);
        Check(m.State == RivalMoveState.Stopped && noMore && early == 0 && due == breakUp && again == 0 && RivalRules.CanTakeOver(f, 0, true, 100, 100) == RivalWhy.Out,
            "rivals takeover: a tycoon taken over is out for good, their moves stopped, the parts paid once on their day");
        var rows = new List<RichRow>();
        RivalRules.RichList(f, 1, 1000, "You", rows);
        bool out0 = true;
        foreach (RichRow r in rows)
        {
            out0 &= r.Who != 0;
        }
        Check(out0 && rows.Count == RivalCatalogue.RichListSize - 1, "rivals takeover: a tycoon taken over is off the rich list");
    }

    // ---------------------------------------------------------------- the same moves from the same seed

    private static void RivalDeterminism()
    {
        string a = RivalRun(1234), b = RivalRun(1234), c = RivalRun(4321);
        Check(a == b, "rivals determinism: the same seed and the same days give the same rivals, line for line");
        Check(a != c, "rivals determinism: another seed gives other moves");
        Check(RivalRules.SeedFrom(77UL, 5) == RivalRules.SeedFrom(77UL, 5) && RivalRules.SeedFrom(77UL, 5) != RivalRules.SeedFrom(78UL, 5) && RivalRules.SeedFrom(77UL, 5) >= 0,
            "rivals determinism: a new file's seed from the playthrough and the day, the same every time");
        bool spread = true;
        var seen = new int[7];
        for (int i = 0; i < 7000; i++)
        {
            seen[RivalRules.Draw(99, 2, i, 3, 7)]++;
        }
        foreach (int n in seen)
        {
            spread &= n > 800 && n < 1200;
        }
        Check(spread && RivalRules.Draw(1, 1, 1, 1, 1) == 0 && RivalRules.Draw(1, 1, 1, 1, 0) == 0, "rivals determinism: the draws spread evenly and need no engine's random numbers");
    }

    // A year of a rich player's rivals, every move landed or failed by its day's number, as a save line.
    private static string RivalRun(int seed)
    {
        var f = RivalRules.Open(seed, 1, 1000);
        RivalWorld w = RivalWorldOf(4000000000L);
        var land = new List<RivalMove>();
        var end = new List<RivalMove>();
        for (int day = 1; day <= 365; day++)
        {
            w.Cycle = day / 90 % 4;
            w.Worth += 10000000L;
            RivalRules.Day(f, w, day, RivalCatalogue.Hard);
            RivalRules.Due(f, day, land, end);
            foreach (RivalMove m in land)
            {
                RivalRules.Landed(f, m, day, day % 5 != 0);
            }
            foreach (RivalMove m in end)
            {
                RivalRules.Ended(m);
            }
            if (day % 40 == 0)
            {
                RivalRules.Counter(f, day / 40 % 3, (RivalCounter)(day / 40 % 4), day, 5000, 1000);
            }
        }
        return RivalStore.Write(f);
    }

    // ---------------------------------------------------------------- the save's line

    private static void RivalSave()
    {
        var f = RivalRules.Open(55, 3, 1200);
        RivalWorld w = RivalWorldOf(30000000000L);
        for (int day = 3; day < 90; day++)
        {
            RivalRules.Day(f, w, day, RivalCatalogue.Ruthless);
        }
        f.Tycoons[1].CounterDay[2] = 44;
        f.Tycoons[2].BreakUp = 123456789012L;
        f.Tycoons[2].BreakUpDay = 99;
        if (f.Moves.Count > 0)
        {
            f.Moves[0].Name = "A|B\nC";
            f.Moves[0].Who = "Ann Lee";
            f.Moves[0].Answered = 0;
        }
        if (f.Moves.Count > 1)
        {
            f.Moves[1].Answered = 5;
        }
        RivalRules.Counter(f, 0, RivalCounter.Poach, 80, 777, 1200);
        string line = RivalStore.Write(f);
        RivalFile back = RivalStore.Read(line, out int version);
        Check(back != null && version == RivalStore.Version && RivalStore.Write(back) == line, "rivals save: the line read back is written again exactly (" + f.Moves.Count + " moves, " + f.Counters.Count + " counters)");
        Check(back != null && back.Moves.Count > 0 && back.Moves[0].Name == "A/B C" && back.Moves[0].Who == "Ann Lee" && back.Tycoons[2].BreakUp == 123456789012L && back.Tycoons[2].BreakUpDay == 99 && back.Tycoons[1].CounterDay[2] == 44
              && (back.Moves.Count < 2 || back.Moves[1].Answered == 5),
            "rivals save: names cleaned of the dividers, large sums and the counters' days kept");

        // An older line (a move without its last field), a later version's (more fields, a line of a new letter, a kind and a state not known), damaged ones.
        string older = line.Replace("|Ann Lee|0", string.Empty);
        RivalFile old = RivalStore.Read(older, out int _);
        Check(old != null && old.Moves.Count == back.Moves.Count && old.Moves[0].Who == string.Empty, "rivals save: a move written without its employee's name reads with none");
        string later = line.Replace("RV|1|", "RV|2|") + "|99" + "\nZ|1|2|3" + "\nM|900|0|42|0|1|3|5|1|0|0|0|p|n|d|t|w" + "\nM|901|0|1|9|1|3|5|1|0|0|0|p|n|d|t|w" + "\nC|902|0|8|1|1|0";
        RivalFile newer = RivalStore.Read(later, out int laterVersion);
        Check(newer != null && laterVersion == 2 && newer.Moves.Count == back.Moves.Count && newer.Counters.Count == back.Counters.Count && newer.Move(900) == null && newer.Move(901) == null,
            "rivals save: a later version's line is read as far as it goes; a move or a counter of a kind or a state it does not know is dropped, never read as another");
        Check(RivalStore.Read("RV|1|x", out int _) == null && RivalStore.Read("XX|1|1|1|1|1|1|1|1|1|1|1|1|1|1", out int _) == null && RivalStore.Read(string.Empty, out int _) == null
              && RivalStore.Read("RV|1|1|1|1|1|1|1|1|1|1|zz", out int _) == null,
            "rivals save: a damaged line, another feature's and an empty one are not read (the save keeps them as they were)");
        var data = new Dictionary<string, string> { { RivalStore.Key, line } };
        RivalFile loaded = RivalStore.Load(data);
        Check(loaded != null && RivalStore.Write(loaded) == line && RivalStore.Load(new Dictionary<string, string>()) == null, "rivals save: the save's entry loads, and a save without one has none");
    }

    // ---------------------------------------------------------------- a day's round in time

    private static void RivalSpeed()
    {
        double us = RivalDays(2000);
        Check(us < 200.0, "rivals speed: a day's round with every move kept takes " + us.ToString("0.0") + " microseconds on average (under 200; it runs once a game day)");
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static double RivalDays(int days)
    {
        var f = RivalRules.Open(8, 1, 1000);
        RivalWorld w = RivalWorldOf(40000000000L, 12);
        var land = new List<RivalMove>();
        var end = new List<RivalMove>();
        var sw = Stopwatch.StartNew();
        for (int day = 1; day <= days; day++)
        {
            RivalRules.Day(f, w, day, RivalCatalogue.Ruthless);
            RivalRules.Due(f, day, land, end);
            foreach (RivalMove m in land)
            {
                RivalRules.Landed(f, m, day, true);
            }
            foreach (RivalMove m in end)
            {
                RivalRules.Ended(m);
            }
            RivalRules.OutletDip(f, "ba:neighborhood_midtown", "ba:businesstype_bakery");
            RivalRules.Premium(f, "ba:neighborhood_midtown");
        }
        sw.Stop();
        return sw.Elapsed.TotalMilliseconds * 1000.0 / days;
    }

    // ---------------------------------------------------------------- the words

    private static void RivalWords()
    {
        string root = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul"));
        string folder = System.IO.Path.Combine(root, "Locales");
        Dictionary<string, string> en = LoadWords(System.IO.Path.Combine(folder, "en.json"));
        Dictionary<string, string> pt = LoadWords(System.IO.Path.Combine(folder, "pt.json"));
        Dictionary<string, string> zh = LoadWords(System.IO.Path.Combine(folder, "zh-cn.json"));
        Check(en != null && pt != null && zh != null, "rivals words: the three locales are where the checks look for them (" + folder + ")");
        if (en == null || pt == null || zh == null)
        {
            return;
        }
        // Every key the rivals' sources name, and those the catalogue builds.
        var keys = new SortedSet<string>(StringComparer.Ordinal);
        var literal = new Regex("\"(reallife_[a-z0-9_]+)\"");
        string[] sources =
        {
            @"Scripts\Rivals", @"Scripts\Wiring\RivalLinks.cs", @"Scripts\Markets\MarketDirector.Rivals.cs", @"Scripts\Justice\RivalClaims.cs", @"Scripts\Economy\Finance\FinanceApp.Rivals.cs"
        };
        foreach (string s in sources)
        {
            string path = System.IO.Path.Combine(root, s);
            string[] files = System.IO.Directory.Exists(path) ? System.IO.Directory.GetFiles(path, "*.cs") : new[] { path };
            foreach (string file in files)
            {
                foreach (Match m in literal.Matches(System.IO.File.ReadAllText(file)))
                {
                    string k = m.Groups[1].Value;
                    if (!k.EndsWith("_", StringComparison.Ordinal))
                    {
                        keys.Add(k);
                    }
                }
            }
        }
        foreach (string[] list in new[] { RivalCatalogue.MoveNames, RivalCatalogue.MoveAbout, RivalCatalogue.CounterNames, RivalCatalogue.StateNames, RivalCatalogue.StrengthNames, RivalCatalogue.TierNames })
        {
            foreach (string k in list)
            {
                keys.Add(k);
            }
        }
        foreach (string k in new[] { "reallife_guide_rivals", "reallife_transaction_rival_rent", "reallife_transaction_rival_counter", "reallife_transaction_rival_breakup", "reallife_transaction_shares_rival_takeover" })
        {
            keys.Add(k);
            if (k.StartsWith("reallife_transaction_", StringComparison.Ordinal))
            {
                keys.Add(k + "_label");
            }
        }
        var missing = new List<string>();
        var holes = new Regex(@"\{[a-z_]+\}");
        foreach (string key in keys)
        {
            if (!en.TryGetValue(key, out string e) || !pt.TryGetValue(key, out string p) || !zh.TryGetValue(key, out string z))
            {
                missing.Add(key);
                continue;
            }
            string want = Holes(holes, e);
            if (Holes(holes, p) != want || Holes(holes, z) != want)
            {
                missing.Add(key + " (its {names} differ)");
            }
        }
        Check(missing.Count == 0 && keys.Count > 150, "rivals words: every text is in en.json, pt.json and zh-cn.json with the same {names} (" + keys.Count + " keys" + (missing.Count > 0 ? "; " + string.Join(", ", missing) : string.Empty) + ")");
        // The notices fit the game's toast.
        var longNotes = new List<string>();
        foreach (string key in keys)
        {
            if ((key.StartsWith("reallife_rival_note_", StringComparison.Ordinal) || key.StartsWith("reallife_rival_came_", StringComparison.Ordinal) || key.StartsWith("reallife_rival_done_", StringComparison.Ordinal))
                && en.TryGetValue(key, out string e) && e.Length > 240)
            {
                longNotes.Add(key);
            }
        }
        Check(longNotes.Count == 0, "rivals words: every notice at most 240 characters in English" + (longNotes.Count > 0 ? ": " + string.Join(", ", longNotes) : string.Empty));
        // The Guide's figures: every {name} on its page is one RivalGuide fills in.
        string guide = System.IO.File.ReadAllText(System.IO.Path.Combine(root, @"Scripts\Rivals\RivalGuide.cs"));
        var unknown = new List<string>();
        foreach (Match m in holes.Matches(en["reallife_guide_rivals"]))
        {
            string name = m.Value.Substring(1, m.Value.Length - 2);
            if (!guide.Contains(".Fact(\"" + name + "\""))
            {
                unknown.Add(name);
            }
        }
        Check(unknown.Count == 0, "rivals words: every figure on the Guide's page is filled from the rules" + (unknown.Count > 0 ? ": " + string.Join(", ", unknown) : string.Empty));
        // What's new: in the next row until its version is made, and in that version's own row after.
        bool line = false;
        foreach (KeyValuePair<string, string> row in en)
        {
            if (row.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) && row.Value.Contains("Rival tycoons"))
            {
                line = !row.Value.StartsWith("New: ", StringComparison.Ordinal);
                break;
            }
        }
        Check(line, "rivals words: What's new has a line for the rival tycoons, without a \"New: \" prefix");
    }
}
