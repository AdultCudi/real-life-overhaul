using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using RealLifeOverhaul.Economy;

// The estate agency's book of other owners' blocks (Scripts/Economy/Estate/AgencyBook*.cs, run with --estate-agency): the fee and the letting fee by the rule, a call for pitches
// drawn from a seed (bounded, never twice the same owner and district, rivals of the save or landlords of the city), rival agencies by a rule fixed by the save and the rival (the
// same whatever order the rivals are listed in), a pitch refused for want of agents, hours or room, the owner's choice (a better rival wins, a tie is the player's, an unpitched call
// goes to a rival or nobody) and every outcome told once, the agents' hours shared out between the blocks and the player's own buildings, a block's days (tenants, calls, the
// building's condition, the city's inspection) by the property business's own measures, the month's fee paid once and only once (also across a save and a load), the standard
// withholding half of a fee and a third miss ending the block, the line in the save (round trip, junk, a line from before, a newer line), the player's own buildings run by his
// agency for no fee, the books' lists of keys and the texts.
internal static partial class Program
{
    private static void EstateAgencyBook()
    {
        AgencyBookFees();
        AgencyBookOffers();
        AgencyBookBids();
        AgencyBookPitch();
        AgencyBookDecide();
        AgencyBookHours();
        AgencyBookDays();
        AgencyBookMonths();
        AgencyBookNothingPaysTwice();
        AgencyBookSaveChecks();
        AgencyBookOwn();
        AgencyBookSources();
        AgencyBookTexts();
    }

    private static AgencyFile BookAgency(int capacity = 60, int skill = 60)
    {
        var a = new AgencyFile { Office = "Office%20Row~1", Capacity = capacity, Negotiators = 2, Skill = skill };
        a.Districts.Add(new DistrictRep { Name = "ba:neighborhood_midtown", SqmPrice = 12000, Rep = 400 });
        a.Districts.Add(new DistrictRep { Name = "ba:neighborhood_garment", SqmPrice = 7000, Rep = 200 });
        return a;
    }

    private static List<AgencyLandlord> BookLandlords(int count = 8)
    {
        var l = new List<AgencyLandlord>(count);
        for (int i = 0; i < count; i++)
        {
            l.Add(new AgencyLandlord { Id = "rival-" + (i * 7 % 13).ToString("00"), Units = 20 + i * 45 });
        }
        return l;
    }

    // A block held, as a win would leave it.
    private static ManagedBlock BookHeld(AgencyFile a, int units = 12, int let = 10, int cond = 700, int standard = 450, int fee = 100, int served = 1000, int rent = 50)
    {
        var b = new ManagedBlock
        {
            Id = a.Book.NextId++, Owner = "city:1", District = "ba:neighborhood_midtown", Stage = (int)BlockStage.Held, Units = units, Let = let, Sqm = 60, RentDay = rent, FeeAsk = fee,
            FeePct = 100, Standard = standard, Cond = cond, Served = served, NextInspect = 40
        };
        a.Book.Blocks.Add(b);
        return b;
    }

    private static void AgencyBookFees()
    {
        Check(AgencyBookRules.MonthFee(10000, 100, 100) == 1000 && AgencyBookRules.MonthFee(10000, 100, 80) == 800 && AgencyBookRules.MonthFee(0, 100, 100) == 0 && AgencyBookRules.MonthFee(-50, 100, 100) == 0,
            "agency book: a month's fee is the share of the rent collected at the owner's fee and the pitch, and never negative");
        Check(AgencyBookRules.MonthFee(333, 75, 100) == EstateCatalogue.Share(333, 75) && AgencyBookRules.MonthFee(1000000, 110, 100) == 110000, "agency book: a fee is rounded to the dollar the way every share of the property business is");
        Check(AgencyBookRules.LettingFee(50) == 50 * EstateCatalogue.AgentLetDays && AgencyBookRules.LettingFee(0) == 0 && AgencyBookCatalogue.LettingFeeDays == EstateCatalogue.AgentLetDays,
            "agency book: a letting fee is the days of rent a letting agent charges, the property business's own figure");
        Check(AgencyRules.Commission(true, 200000) == EstateCatalogue.Share(200000, EstateCatalogue.CommissionSale)
              && AgencyRules.Commission(false, 100000) == EstateCatalogue.Share(EstateCatalogue.Share(100000, EstateCatalogue.AgencyYield), EstateCatalogue.CommissionLet),
            "agency book: the commission on a sale and a letting of an instruction is as it was");
        Check(AgencyBookCatalogue.FeeAskLeast < EstateCatalogue.ManagerShare && AgencyBookCatalogue.FeeAskMost > EstateCatalogue.ManagerShare, "agency book: owners ask fees either side of a property manager's cut");
        Check(AgencyBookRules.Hours(12) == 12 * AgencyBookCatalogue.HoursPerUnit && AgencyBookRules.Hours(-3) == 0, "agency book: a block needs hours a day for each flat");
        Check(AgencyBookRules.FlatRentDay(60, 12000, 1000, 1000, 60) >= 1 && AgencyBookRules.FlatRentDay(60, 12000, 1000, 2000, 60) > AgencyBookRules.FlatRentDay(60, 12000, 1000, 1000, 60)
              && AgencyBookRules.FlatRentDay(1, 1, 1000, 1000, 60) == 1, "agency book: a flat's rent follows the district's price and the market, and is at least a dollar a day");
    }

    private static void AgencyBookOffers()
    {
        AgencyFile off = BookAgency();
        off.Office = string.Empty;
        Check(AgencyBookRules.Draw(off, BookLandlords(), 5, 10, Climate(), 1000) == null, "agency book: no office, no call for pitches");
        AgencyFile none = BookAgency();
        none.Districts.Clear();
        Check(AgencyBookRules.Draw(none, BookLandlords(), 5, 10, Climate(), 1000) == null, "agency book: no district known, no call for pitches");
        AgencyFile a = BookAgency(), b = BookAgency();
        ManagedBlock x = AgencyBookRules.Draw(a, BookLandlords(), 77, 10, Climate(), 1000), y = AgencyBookRules.Draw(b, BookLandlords(), 77, 10, Climate(), 1000);
        Check(x != null && y != null && x.Owner == y.Owner && x.District == y.District && x.Units == y.Units && x.FeeAsk == y.FeeAsk && x.Standard == y.Standard && x.RentDay == y.RentDay
              && x.Bids.Count == y.Bids.Count && x.Until == 10 + AgencyBookCatalogue.OfferOpenDays, "agency book: the same seed and day make the same call for pitches");
        Check(AgencyBookRules.Draw(a, BookLandlords(), 77, 11, Climate(), 1000) == null, "agency book: a second call comes no sooner than " + AgencyBookCatalogue.OfferEveryDays + " days after the first");
        int rivals = 0, city = 0, bad = 0, most = 0, feeLow = int.MaxValue, feeHigh = 0, unitsLow = int.MaxValue, unitsHigh = 0;
        var seen = new HashSet<string>();
        for (int seed = 1; seed <= 40; seed++)
        {
            AgencyFile f = BookAgency();
            for (int day = 1; day <= 200; day++)
            {
                ManagedBlock o = AgencyBookRules.Draw(f, BookLandlords(), seed, day, Climate(), 1000);
                if (day % 9 == 0)
                {
                    f.Book.Blocks.Clear();   // the owners have chosen
                }
                if (o == null)
                {
                    continue;
                }
                rivals += AgencyBookRules.CityNumber(o.Owner) < 0 ? 1 : 0;
                city += AgencyBookRules.CityNumber(o.Owner) >= 0 ? 1 : 0;
                bad += o.FeeAsk < AgencyBookCatalogue.FeeAskLeast || o.FeeAsk > AgencyBookCatalogue.FeeAskMost || o.Units < AgencyBookCatalogue.UnitsLeast || o.Units > AgencyBookCatalogue.UnitsMost
                       || !AgencyBookCatalogue.Standards.Contains(o.Standard) || o.RentDay < 1 || o.Let > o.Units || o.Cond < AgencyBookCatalogue.StartCondLeast || o.Cond > AgencyBookCatalogue.StartCondMost ? 1 : 0;
                feeLow = Math.Min(feeLow, o.FeeAsk);
                feeHigh = Math.Max(feeHigh, o.FeeAsk);
                unitsLow = Math.Min(unitsLow, o.Units);
                unitsHigh = Math.Max(unitsHigh, o.Units);
                most = Math.Max(most, f.Book.Count(BlockStage.Offered));
                seen.Add(o.Owner + "|" + o.District);
            }
        }
        Check(rivals > 100 && city > 100 && bad == 0, "agency book: calls come from the rivals of the save and from landlords of the city, every figure inside its bounds (" + rivals + " rival, " + city + " city, " + bad + " bad)");
        Check(most <= AgencyBookCatalogue.OffersMost, "agency book: no more calls stand at once than " + AgencyBookCatalogue.OffersMost + " (" + most + ")");
        Check(feeLow < 80 && feeHigh > 100 && unitsLow <= 6 && unitsHigh >= 22, "agency book: the fees asked and the flats in a block cover their ranges (" + feeLow + " to " + feeHigh + ", " + unitsLow + " to " + unitsHigh + ")");
        AgencyFile alone = BookAgency();
        bool onlyCity = true;
        for (int day = 1; day <= 120; day++)
        {
            ManagedBlock o = AgencyBookRules.Draw(alone, null, 9, day, Climate(), 1000);
            onlyCity &= o == null || AgencyBookRules.CityNumber(o.Owner) >= 0;
            alone.Book.Blocks.Clear();
        }
        Check(onlyCity, "agency book: with no rival who is a landlord, the calls are the city's landlords' alone");
        AgencyFile twice = BookAgency();
        bool repeated = false;
        int drawn = 0;
        for (int day = 1; day <= 600; day++)
        {
            drawn += AgencyBookRules.Draw(twice, null, 31, day, Climate(), 1000) != null ? 1 : 0;
            if (day % 7 == 0 && twice.Book.Blocks.Count > 0)
            {
                twice.Book.Blocks.RemoveAt(0);
            }
            repeated |= twice.Book.Blocks.GroupBy(z => z.Owner + "|" + z.District).Any(g => g.Count() > 1);
        }
        Check(!repeated && drawn > 30, "agency book: one owner never asks twice for the same district at once (" + drawn + " calls)");
    }

    private static void AgencyBookBids()
    {
        List<AgencyLandlord> list = BookLandlords(12), reversed = BookLandlords(12);
        reversed.Reverse();
        int differs = 0, over = 0, ownBid = 0, outside = 0, total = 0;
        for (int id = 1; id <= 200; id++)
        {
            var one = new ManagedBlock { Id = id, Owner = list[id % list.Count].Id };
            var two = new ManagedBlock { Id = id, Owner = one.Owner };
            AgencyBookRules.Bid(11, one, list);
            AgencyBookRules.Bid(11, two, reversed);
            bool same = one.Bids.Count == two.Bids.Count;
            for (int i = 0; same && i < one.Bids.Count; i++)
            {
                same = one.Bids[i].Rival == two.Bids[i].Rival && one.Bids[i].Pct == two.Bids[i].Pct && one.Bids[i].Skill == two.Bids[i].Skill;
            }
            differs += same ? 0 : 1;
            over += one.Bids.Count > AgencyBookCatalogue.RivalBidsMost ? 1 : 0;
            ownBid += one.Bids.Any(bid => bid.Rival == one.Owner) ? 1 : 0;
            outside += one.Bids.Count(bid => bid.Pct < AgencyBookCatalogue.BidPctLeast || bid.Pct > AgencyBookCatalogue.BidPctMost || bid.Skill < AgencyBookCatalogue.RivalSkillLeast
                                             || bid.Skill > AgencyBookCatalogue.RivalSkillMost);
            total += one.Bids.Count;
        }
        Check(differs == 0, "agency book: the rival agencies that pitch do not depend on the order the game lists the rivals in");
        Check(over == 0 && ownBid == 0 && outside == 0 && total > 100, "agency book: at most " + AgencyBookCatalogue.RivalBidsMost + " rival pitches, never the owner's own, every pitch inside its bounds (" + total + " in all)");
        int runs = 0;
        for (int i = 0; i < 400; i++)
        {
            runs += AgencyBookRules.RunsAgency(3, "rival-" + i) ? 1 : 0;
        }
        Check(runs > 140 && runs < 260 && AgencyBookRules.RunsAgency(3, "rival-9") == AgencyBookRules.RunsAgency(3, "rival-9") && !AgencyBookRules.RunsAgency(3, string.Empty),
            "agency book: about half of the rivals who are landlords run an agency, each one's answer fixed by the save (" + runs + " of 400)");
        bool differsBySave = false;
        for (int i = 0; i < 40; i++)
        {
            differsBySave |= AgencyBookRules.RunsAgency(3, "rival-" + i) != AgencyBookRules.RunsAgency(4, "rival-" + i);
        }
        Check(differsBySave, "agency book: another save has other rivals running agencies");
        Check(AgencyBookRules.RivalSkill(0) == AgencyBookCatalogue.RivalSkillLeast && AgencyBookRules.RivalSkill(100000) == AgencyBookCatalogue.RivalSkillMost && AgencyBookRules.RivalSkill(200) > AgencyBookRules.RivalSkill(40),
            "agency book: a rival who holds more has the better agents, between the least and the most");
        var none = new ManagedBlock { Id = 5, Owner = "x" };
        Check(AgencyBookRules.Bid(11, none, null) == 0 && AgencyBookRules.Bid(11, none, new List<AgencyLandlord>()) == 0, "agency book: with no rival who is a landlord, nobody pitches against the player");
    }

    private static ManagedBlock BookOffer(AgencyFile a, int units = 12, params AgencyBid[] bids)
    {
        var b = new ManagedBlock
        {
            Id = a.Book.NextId++, Owner = "city:2", District = "ba:neighborhood_midtown", Stage = (int)BlockStage.Offered, Units = units, Sqm = 60, RentDay = 40, FeeAsk = 90, Standard = 450,
            Until = 16, Cond = 700, Let = units - 1
        };
        b.Bids.AddRange(bids);
        a.Book.Blocks.Add(b);
        return b;
    }

    private static void AgencyBookPitch()
    {
        AgencyFile a = BookAgency(capacity: 30);
        ManagedBlock o = BookOffer(a, 12);
        AgencyFile shut = BookAgency();
        shut.Office = string.Empty;
        Check(AgencyBookRules.Pitch(shut, o.Id, false) == PitchResult.NoAgency, "agency book: no pitch without an agency");
        Check(AgencyBookRules.Pitch(a, 999, false) == PitchResult.NoOffer, "agency book: no pitch for a call that is not there");
        a.Negotiators = 0;
        Check(AgencyBookRules.Pitch(a, o.Id, false) == PitchResult.NoAgents, "agency book: no pitch with no agents at the office");
        a.Negotiators = 1;
        a.Capacity = 5;
        Check(AgencyBookRules.Pitch(a, o.Id, false) == PitchResult.NoHours && !o.Pitched, "agency book: no pitch when the agents have not the hours to spare");
        a.Capacity = 30;
        Check(AgencyBookRules.Pitch(a, o.Id, true) == PitchResult.Pitched && o.Pitched && o.FeePct == AgencyBookCatalogue.UndercutPct, "agency book: a pitch that undercuts is made at " + AgencyBookCatalogue.UndercutPct + "% of the fee");
        Check(AgencyBookRules.Pitch(a, o.Id, false) == PitchResult.Already, "agency book: a block is pitched for once");
        Check(AgencyBookRules.Withdraw(a.Book, o.Id) && !o.Pitched && o.FeePct == 100 && !AgencyBookRules.Withdraw(a.Book, o.Id), "agency book: a pitch can be taken back, once");
        ManagedBlock p = BookOffer(a, 12), q = BookOffer(a, 12), r = BookOffer(a, 12);
        Check(AgencyBookRules.Pitch(a, p.Id, false) == PitchResult.Pitched && AgencyBookRules.Pitch(a, q.Id, false) == PitchResult.Pitched && AgencyBookRules.Pitch(a, r.Id, false) == PitchResult.NoHours,
            "agency book: the hours pitched for already are not spare again (30 tenths: two blocks of 12, not three)");
        AgencyFile full = BookAgency(capacity: 500);
        for (int i = 0; i < AgencyBookCatalogue.BlocksMost; i++)
        {
            BookHeld(full);
        }
        ManagedBlock late = BookOffer(full, 4);
        Check(AgencyBookRules.Pitch(full, late.Id, false) == PitchResult.Full, "agency book: no pitch past the blocks the agency can hold");
    }

    private static void AgencyBookDecide()
    {
        // A better rival wins; the player's block goes with them and is told once.
        AgencyFile a = BookAgency(capacity: 100, skill: 20);
        a.Districts[0].Rep = 0;
        ManagedBlock lose = BookOffer(a, 8, new AgencyBid { Rival = "rival-01", Pct = 90, Skill = 80 });
        lose.Pitched = true;
        var events = new List<BlockEvent>();
        AgencyBookRules.Decide(a, 15, events);
        Check(events.Count == 0 && lose.Stage == (int)BlockStage.Offered, "agency book: the owner does not choose before the last day");
        AgencyBookRules.Decide(a, 16, events);
        Check(events.Count == 1 && events[0].Kind == BlockEvent.LostToRival && events[0].Name == "rival-01" && lose.Stage == (int)BlockStage.Lost && lose.Why == (int)BlockWhy.Rival && lose.Rival == "rival-01"
              && a.Book.Lost == 1 && a.Book.Won == 0, "agency book: a rival with the better agents and the lower fee is chosen, and the loss is told once with their name");
        events.Clear();
        AgencyBookRules.Decide(a, 16, events);
        AgencyBookRules.Decide(a, 17, events);
        Check(events.Count == 0 && a.Book.Lost == 1, "agency book: a choice made is not made again on the next day");
        // The player wins with the better pitch, and the block is held.
        AgencyFile b = BookAgency(capacity: 100, skill: 90);
        ManagedBlock win = BookOffer(b, 8, new AgencyBid { Rival = "rival-02", Pct = 100, Skill = 50 });
        win.Pitched = true;
        int rep = b.Districts[0].Rep;
        events.Clear();
        AgencyBookRules.Decide(b, 16, events);
        Check(events.Count == 1 && events[0].Kind == BlockEvent.Won && win.Stage == (int)BlockStage.Held && b.Book.Won == 1 && win.Bids.Count == 0 && b.Districts[0].Rep == rep + AgencyBookCatalogue.RepWon && win.NextInspect > 16,
            "agency book: the better pitch wins the block, which is then held, with the district's reputation up");
        // A tie goes to the player.
        AgencyFile c = BookAgency(capacity: 100, skill: 50);
        c.Districts[0].Rep = 0;
        ManagedBlock tie = BookOffer(c, 8, new AgencyBid { Rival = "rival-03", Pct = 100, Skill = 50 });
        tie.Pitched = true;
        Check(AgencyBookRules.Winner(tie, 50, 0) == -1, "agency book: a tie is the player's");
        tie.FeePct = AgencyBookCatalogue.UndercutPct;
        Check(AgencyBookRules.Winner(tie, 40, 0) == -1 && AgencyBookRules.Winner(tie, 39, 0) == 0, "agency book: undercutting the fee by a fifth is worth twenty points, ten of the agents' skill"
              );
        Check(AgencyBookRules.Score(50, 0, 80) == AgencyBookRules.Score(50, 0, 100) + 20 && AgencyBookRules.Score(60, 0, 100) == AgencyBookRules.Score(50, 0, 100) + 2 * AgencyBookCatalogue.SkillWeight * 5,
            "agency book: a point of the agents' skill counts double a point of fee");
        // A call nobody pitched for goes to a rival who wanted it, else nobody; it costs the player no pitch lost.
        AgencyFile d = BookAgency();
        ManagedBlock open = BookOffer(d, 8, new AgencyBid { Rival = "rival-04", Pct = 95, Skill = 70 }), unwanted = BookOffer(d, 8);
        events.Clear();
        AgencyBookRules.Decide(d, 16, events);
        Check(open.Why == (int)BlockWhy.Rival && unwanted.Why == (int)BlockWhy.Nobody && d.Book.Lost == 0 && events.All(e => e.Kind == BlockEvent.Unwanted), "agency book: a call nobody pitched for goes to a rival or to nobody, and is no loss of the agency's");
        // Lost blocks kept for the page are bounded.
        AgencyFile e = BookAgency();
        for (int i = 0; i < 12; i++)
        {
            ManagedBlock x = BookOffer(e, 4);
            x.Until = 16;
        }
        AgencyBookRules.Decide(e, 16, new List<BlockEvent>());
        Check(e.Book.Blocks.Count <= AgencyBookCatalogue.LostKept, "agency book: only the newest lost blocks are kept (" + e.Book.Blocks.Count + ")");
        // Resigning and closing.
        AgencyFile f = BookAgency();
        ManagedBlock held = BookHeld(f);
        Check(AgencyBookRules.Resign(f, held.Id) && held.Stage == (int)BlockStage.Lost && held.Why == (int)BlockWhy.Resigned && !AgencyBookRules.Resign(f, held.Id), "agency book: a block handed back is lost once");
        BookHeld(f);
        BookOffer(f, 6).Pitched = true;
        AgencyBookRules.CloseAll(f);
        Check(f.Book.Count(BlockStage.Held) == 0 && f.Book.Count(BlockStage.Offered) == 0, "agency book: shutting the agency ends every block and call");
    }

    private static void AgencyBookHours()
    {
        AgencyFile a = BookAgency(capacity: 60);
        BookHeld(a, 12);
        BookHeld(a, 12);
        AgencyBookRules.Allocate(a, 10);
        Check(a.Book.Served == 1000 && a.Reserved == 34 && !a.Book.OwnPaused && a.Book.Blocks.All(b => b.Served == 1000), "agency book: with the hours there, every block and every building under the agency is served in full");
        a.Capacity = 20;
        AgencyBookRules.Allocate(a, 10);
        Check(a.Book.Served == 588 && a.Reserved == 20 && !a.Book.OwnPaused, "agency book: short of hours, everything is served in proportion (588 of 1000)");
        a.Capacity = 15;
        AgencyBookRules.Allocate(a, 10);
        Check(a.Book.OwnPaused && a.Book.Served == 625 && a.Reserved == 15, "agency book: under " + AgencyBookCatalogue.OwnCoverLeast / 10 + "% the player's own buildings wait and the blocks have the hours");
        AgencyFile off = BookAgency();
        off.Office = string.Empty;
        AgencyBookRules.Allocate(off, 5);
        Check(off.Reserved == 0, "agency book: a shut agency holds no hours");
        Check(AgencyBookRules.Cover(10, 0) == 1000 && AgencyBookRules.Cover(0, 10) == 0 && AgencyBookRules.Cover(50, 10) == 1000, "agency book: what the hours cover is a share of what is needed, all of it when nothing is");
        // The instructions have the hours that are left: nothing changes with nothing reserved.
        AgencyFile ins = BookAgency(capacity: 30);
        ins.Open.Add(new Instruction { Id = 1, District = "ba:neighborhood_midtown", Sale = false, Sqm = 60, Value = 100000, Ask = 1000, Stage = (int)InstructionStage.Marketing, Hours = 100, Day = 90 });
        AgencyFile reserved = BookAgency(capacity: 30);
        reserved.Open.Add(new Instruction { Id = 1, District = "ba:neighborhood_midtown", Sale = false, Sqm = 60, Value = 100000, Ask = 1000, Stage = (int)InstructionStage.Marketing, Hours = 100, Day = 90 });
        reserved.Reserved = 30;
        var ev = new List<AgencyEvent>();
        AgencyRules.Day(ins, 5, 20, Climate(), ev);
        AgencyRules.Day(reserved, 5, 20, Climate(), ev);
        Check(ins.Open[0].Done == 30 && reserved.Open[0].Done == 0, "agency book: the hours the blocks and the buildings of his own take are not the instructions' (" + ins.Open[0].Done + " against " + reserved.Open[0].Done + ")");
    }

    private static void AgencyBookDays()
    {
        var events = new List<BlockEvent>();
        AgencyFile a = BookAgency();
        ManagedBlock b = BookHeld(a, 12, 10, 700);
        bool within = true;
        for (int day = 1; day <= 400 && b.Held; day++)
        {
            AgencyBookRules.BlockDay(a, b, 5, day, Climate(), events);
            within &= b.Let >= 0 && b.Let <= b.Units && b.Happy >= 0 && b.Happy <= 1000 && b.Cond >= 0 && b.Cond <= 1000 && b.Calls.Count <= AgencyBookCatalogue.CallsMost && b.Collected >= 0;
        }
        Check(within, "agency book: a block's flats let, tenants' satisfaction, condition and open calls stay inside their bounds over a year");
        AgencyFile twin1 = BookAgency(), twin2 = BookAgency();
        ManagedBlock t1 = BookHeld(twin1, 12, 10, 700), t2 = BookHeld(twin2, 12, 10, 700);
        for (int day = 1; day <= 120; day++)
        {
            AgencyBookRules.BlockDay(twin1, t1, 5, day, Climate(), events);
            AgencyBookRules.BlockDay(twin2, t2, 5, day, Climate(), events);
        }
        Check(t1.Let == t2.Let && t1.Happy == t2.Happy && t1.Cond == t2.Cond && t1.Collected == t2.Collected && t1.Calls.Count == t2.Calls.Count && t1.Stage == t2.Stage, "agency book: the same seed gives a block the same history");
        // Looked after against left alone: better kept, happier, more let.
        long servedHappy = 0, starvedHappy = 0, servedLet = 0, starvedLet = 0;
        int servedCond = 0, starvedCond = 0;
        for (int seed = 1; seed <= 20; seed++)
        {
            AgencyFile s1 = BookAgency(), s2 = BookAgency();
            ManagedBlock good = BookHeld(s1, 12, 10, 700, served: 1000), bad = BookHeld(s2, 12, 10, 700, served: 0);
            for (int day = 1; day <= 60 && good.Held && bad.Held; day++)
            {
                good.NextInspect = bad.NextInspect = 100000;   // (the city's look is judged below)
                AgencyBookRules.BlockDay(s1, good, seed, day, Climate(), events);
                AgencyBookRules.BlockDay(s2, bad, seed, day, Climate(), events);
            }
            servedHappy += good.Happy;
            starvedHappy += bad.Happy;
            servedLet += good.Let;
            starvedLet += bad.Let;
            servedCond += good.Cond;
            starvedCond += bad.Cond;
        }
        Check(servedHappy > starvedHappy && servedLet > starvedLet && servedCond > starvedCond, "agency book: agents with the hours keep tenants happier, more flats let and the building up (" + servedHappy / 20 + ", " + servedLet / 20 + ", " + servedCond / 20 + " against "
              + starvedHappy / 20 + ", " + starvedLet / 20 + ", " + starvedCond / 20 + ")");
        Check(AgencyBookRules.Target(b, 10) >= 0 && AgencyBookRules.ReletChance(0, 1000) > AgencyBookRules.ReletChance(3, 1000) && AgencyBookRules.ReletChance(1, 1000) > AgencyBookRules.ReletChance(1, 400)
              && AgencyBookRules.ReletChance(0, 1000) <= AgencyBookCatalogue.ReletMost, "agency book: flats are let again sooner in a boom and with the agents' hours covered");
        // A letting is paid for once and counted in the month.
        AgencyFile l = BookAgency();
        ManagedBlock gap = BookHeld(l, 12, 0, 800);
        int letFees = 0;
        events.Clear();
        for (int day = 1; day <= 30; day++)
        {
            AgencyBookRules.BlockDay(l, gap, 3, day, Climate(), events);
        }
        foreach (BlockEvent e in events)
        {
            letFees += e.Kind == BlockEvent.Letting ? e.Amount : 0;
        }
        Check(letFees > 0 && letFees == l.Book.MonthLettings && letFees == gap.LettingsDone * AgencyBookRules.LettingFee(gap.RentDay), "agency book: each flat let again pays the days' rent once, and the month counts them (" + letFees + ")");
        // The city's inspection: a pass sets the next, a failure is a strike and a fine, three in a row end the block.
        AgencyFile i = BookAgency();
        ManagedBlock run = BookHeld(i, 12, 10, 100, served: 0);
        run.NextInspect = 5;
        events.Clear();
        AgencyBookRules.BlockDay(i, run, 3, 5, Climate(), events);
        BlockEvent fined = events.FirstOrDefault(x => x.Kind == BlockEvent.Fined);
        Check(fined.Kind == BlockEvent.Fined && fined.Amount == EstateRules.InspectFine(1, 1000) && run.Strikes == 1 && run.NextInspect == 5 + EstateCatalogue.ReinspectDays,
            "agency book: a block that fails the city's inspection is fined and looked at again within " + EstateCatalogue.ReinspectDays + " days");
        for (int day = run.NextInspect; run.Held && day < 60; day = run.NextInspect)
        {
            AgencyBookRules.BlockDay(i, run, 3, day, Climate(), events);
        }
        Check(!run.Held && run.Why == (int)BlockWhy.Inspection && events.Any(x => x.Kind == BlockEvent.Dropped) && i.Book.Lost == 1,
            "agency book: " + AgencyBookCatalogue.StrikesMost + " failed inspections in a row end the block");
        AgencyFile pass = BookAgency();
        ManagedBlock fine = BookHeld(pass, 12, 10, 900);
        fine.NextInspect = 5;
        fine.Strikes = 2;
        AgencyBookRules.BlockDay(pass, fine, 3, 5, Climate(), events);
        Check(fine.Strikes == 0 && fine.NextInspect == 5 + EstateCatalogue.InspectEvery && fine.Held, "agency book: a building that passes starts its strikes again and is looked at in " + EstateCatalogue.InspectEvery + " days");
    }

    private static void AgencyBookMonths()
    {
        var events = new List<BlockEvent>();
        AgencyFile a = BookAgency();
        ManagedBlock good = BookHeld(a, 12, 10, 700, standard: 450);
        good.Collected = 20000;
        good.HappySum = 600 * 5;
        good.HappyDays = 5;
        ManagedBlock offer = BookOffer(a, 6);
        offer.Collected = 999;   // (a call has no rent to pay a fee on)
        int rep = a.Districts[0].Rep;
        Check(AgencyBookRules.MonthEnd(a, 1, events), "agency book: a month turning is done");
        BlockEvent fee = events.FirstOrDefault(e => e.Kind == BlockEvent.Fee);
        Check(events.Count(e => e.Kind == BlockEvent.Fee) == 1 && fee.Amount == AgencyBookRules.MonthFee(20000, 100, 100) && good.Collected == 0 && good.HappyDays == 0 && good.Months == 1 && good.Missed == 0
              && a.Book.LastMonthFees == fee.Amount && a.Districts[0].Rep == rep + AgencyBookCatalogue.RepKept, "agency book: a block that kept its tenants as the owner asked pays the whole fee and the month starts again");
        events.Clear();
        Check(!AgencyBookRules.MonthEnd(a, 1, events) && !AgencyBookRules.MonthEnd(a, 0, events) && events.Count == 0, "agency book: the same month is never settled twice");
        // Under the standard: half the fee is held back, and three months in a row end the block.
        AgencyFile b = BookAgency();
        ManagedBlock poor = BookHeld(b, 12, 10, 300, standard: 580);
        int paid = 0, held = 0;
        for (int month = 1; month <= 3; month++)
        {
            poor.Collected = 10000;
            poor.HappySum = 400 * 5;
            poor.HappyDays = 5;
            events.Clear();
            AgencyBookRules.MonthEnd(b, month, events);
            paid += events.Where(e => e.Kind == BlockEvent.Fee).Sum(e => e.Amount);
            held += events.Where(e => e.Kind == BlockEvent.Penalty).Sum(e => e.Amount);
        }
        int whole = AgencyBookRules.MonthFee(10000, 100, 100);
        Check(held == EstateCatalogue.Share(whole, AgencyBookCatalogue.PenaltyPct * 10) * 3 && paid == (whole - EstateCatalogue.Share(whole, AgencyBookCatalogue.PenaltyPct * 10)) * 3 && paid + held == whole * 3,
            "agency book: a month under the owner's standard pays half of the fee and holds half back, the two making the whole (" + paid + " and " + held + ")");
        Check(!poor.Held && poor.Why == (int)BlockWhy.Standard && events.Any(e => e.Kind == BlockEvent.Dropped) && b.Book.Lost == 1, "agency book: " + AgencyBookCatalogue.MissesMost + " months in a row under the standard end the block");
        AgencyFile c = BookAgency();
        ManagedBlock mixed = BookHeld(c, 12, 10, 700, standard: 520);
        for (int month = 1; month <= 6; month++)
        {
            mixed.Collected = 5000;
            mixed.HappyDays = 5;
            mixed.HappySum = (month % 3 == 0 ? 700 : 400) * 5;   // two bad months, then a good one: never three in a row
            AgencyBookRules.MonthEnd(c, month, events);
        }
        Check(mixed.Held && mixed.Months == 6, "agency book: a good month starts the count of bad ones again");
        Check(AgencyBookRules.FeesToCome(a.Book) == 0 && AgencyBookRules.RentRoll(good, 5) == good.Let * good.RentDay * 5, "agency book: the page's figures are the blocks' own");
    }

    // Runs a year of an agency's book the way the director does and adds up what it would pay. With reload, the book is written to its line and read back every day.
    private static long AgencyBookYear(int seed, bool reload, out int months, out long expected)
    {
        AgencyFile a = BookAgency(capacity: 400, skill: 85);
        List<AgencyLandlord> landlords = BookLandlords();
        var events = new List<BlockEvent>();
        long paid = 0;
        expected = 0;
        months = 0;
        int month = 0;
        for (int day = 1; day <= 420; day++)
        {
            month = (day - 1) / 5;
            events.Clear();
            AgencyBookRules.Decide(a, day, events);
            AgencyBookRules.Allocate(a, 0);
            for (int i = 0; i < a.Book.Blocks.Count; i++)
            {
                AgencyBookRules.BlockDay(a, a.Book.Blocks[i], seed, day, Climate(), events);
            }
            // what the month's fees must be, worked out apart from the rules
            var held = a.Book.Blocks.Where(b => b.Held).ToList();
            var want = new Dictionary<int, int>();
            if (month > a.Book.Month)
            {
                foreach (ManagedBlock b in held)
                {
                    int average = b.HappyDays > 0 ? b.HappySum / b.HappyDays : Math.Max(0, b.Happy);
                    int whole = (int)(((long)b.Collected * ((long)b.FeeAsk * b.FeePct / 100) + 500) / 1000);
                    want[b.Id] = whole - (average >= b.Standard ? 0 : (int)(((long)whole * 500 + 500) / 1000));
                }
            }
            bool turned = AgencyBookRules.MonthEnd(a, month, events);
            months += turned ? 1 : 0;
            if (turned)
            {
                foreach (KeyValuePair<int, int> w in want)
                {
                    expected += w.Value;
                }
            }
            ManagedBlock arrived = AgencyBookRules.Draw(a, landlords, seed, day, Climate(), 1000);
            if (arrived != null && day % 3 == 0)
            {
                AgencyBookRules.Pitch(a, arrived.Id, day % 2 == 0);
            }
            foreach (BlockEvent e in events)
            {
                paid += e.Kind == BlockEvent.Fee || e.Kind == BlockEvent.Letting ? e.Amount : 0;
                expected += e.Kind == BlockEvent.Letting ? e.Amount : 0;
            }
            if (reload)
            {
                var back = new AgencyBook();
                AgencyBookStore.Read(back, AgencyBookStore.Write(a.Book));
                a.Book.Blocks.Clear();
                a.Book.Blocks.AddRange(back.Blocks);
                a.Book.NextId = back.NextId;
                a.Book.LastOfferDay = back.LastOfferDay;
                a.Book.MonthLettings = back.MonthLettings;
                a.Book.LastMonthFees = back.LastMonthFees;
                a.Book.LastMonthPenalty = back.LastMonthPenalty;
                a.Book.LastMonthLettings = back.LastMonthLettings;
                a.Book.Won = back.Won;
                a.Book.Lost = back.Lost;
                a.Book.Month = back.Month;
            }
        }
        return paid;
    }

    private static void AgencyBookNothingPaysTwice()
    {
        bool same = true, exact = true;
        long totalPaid = 0;
        int months = 0;
        for (int seed = 1; seed <= 12; seed++)
        {
            long exp1, exp2;
            int m1, m2;
            long straight = AgencyBookYear(seed, false, out m1, out exp1), reloaded = AgencyBookYear(seed, true, out m2, out exp2);
            same &= straight == reloaded && m1 == m2;
            exact &= straight == exp1 && reloaded == exp2;
            totalPaid += straight;
            months = Math.Max(months, m1);
        }
        Check(totalPaid > 0 && months >= 60, "agency book: a year of the book pays fees (" + totalPaid + " over " + months + " months)");
        Check(same, "agency book: saving and loading the book every day pays exactly what not doing so pays, nothing twice and nothing lost");
        Check(exact, "agency book: every fee and letting fee paid is one the rules' figures call for, once");
        AgencyFile a = BookAgency();
        ManagedBlock b = BookHeld(a);
        b.Collected = 12000;
        b.HappyDays = 4;
        b.HappySum = 2400;
        var events = new List<BlockEvent>();
        AgencyBookRules.MonthEnd(a, 4, events);
        var back = new AgencyBook();
        AgencyBookStore.Read(back, AgencyBookStore.Write(a.Book));
        var a2 = BookAgency();
        a2.Book.Blocks.AddRange(back.Blocks);
        a2.Book.Month = back.Month;
        var again = new List<BlockEvent>();
        Check(!AgencyBookRules.MonthEnd(a2, 4, again) && again.Count == 0 && back.Month == 4, "agency book: a month settled before a save is not settled again after the load");
    }

    private static void AgencyBookSaveChecks()
    {
        AgencyFile a = BookAgency(capacity: 400, skill: 85);
        var events = new List<BlockEvent>();
        for (int day = 1; day <= 150; day++)
        {
            AgencyBookRules.Decide(a, day, events);
            AgencyBookRules.Allocate(a, 0);
            foreach (ManagedBlock b in a.Book.Blocks.ToList())
            {
                AgencyBookRules.BlockDay(a, b, 9, day, Climate(), events);
            }
            AgencyBookRules.MonthEnd(a, (day - 1) / 5, events);
            ManagedBlock o = AgencyBookRules.Draw(a, BookLandlords(), 9, day, Climate(), 1000);
            if (o != null)
            {
                AgencyBookRules.Pitch(a, o.Id, true);
            }
            events.Clear();
        }
        string line = AgencyBookStore.Write(a.Book);
        var back = new AgencyBook();
        bool read = AgencyBookStore.Read(back, line);
        Check(a.Book.Blocks.Count > 0 && read && AgencyBookStore.Write(back) == line, "agency book: the line in the save reads back to the same line (" + a.Book.Blocks.Count + " blocks)");
        Check(line.StartsWith("v=" + AgencyBookStore.Version + "^", StringComparison.Ordinal) && AgencyBookStore.Key == "reallife:estate_agency_book", "agency book: the line has its own key and a version");
        var junk = new AgencyBook();
        bool threw = false;
        foreach (string text in new[] { string.Empty, "v=", "v=1", "v=1^h:x", "v=1^h:1|2^b:1|a", "garbage", "v=1^h:1|1|1|1|1|1|1|1|1^b:;;;|||", "v=1^h:1^b:3|o|d|9|9|99999|9|9|9|9|9|9|9|9|9|9|9|9|9|9|9|9|9|9|9|9|9||" })
        {
            try
            {
                AgencyBookStore.Read(junk, text);
            }
            catch (Exception)
            {
                threw = true;
            }
        }
        Check(!threw, "agency book: a line nobody can read is read past, never thrown at");
        var fresh = new AgencyBook();
        AgencyBookStore.Read(fresh, new Dictionary<string, string>());
        Check(fresh.Blocks.Count == 0 && fresh.NextId == 1 && fresh.Month == 0, "agency book: a save from before the book has nothing in it");
        string wild = "v=1^h:1|0|0|0|0|0|0|0|0^b:7|city%3A1|ba%3Aneighborhood_midtown|1|0|9999|-5|0|40|900|100|0|0|1|99999|-9|2000|9|0|0|0|0|0|0|0|0|0||";
        var clamped = new AgencyBook();
        AgencyBookStore.Read(clamped, wild);
        ManagedBlock w = clamped.Blocks.Count > 0 ? clamped.Blocks[0] : null;
        Check(w != null && w.Units == AgencyBookCatalogue.UnitsMost && w.Let <= w.Units && w.Cond <= 1000 && w.Happy <= 1000 && w.RentDay >= 1 && w.Sqm >= 1, "agency book: every figure of a line is put inside its bounds on reading");
        string newer = line.Replace(";", "|x|y;") + "|more";
        var onward = new AgencyBook();
        Check(AgencyBookStore.Read(onward, newer) && onward.Blocks.Count == back.Blocks.Count, "agency book: fields a later version adds at the end of a record are read past");
        var data = new Dictionary<string, string>();
        var state = new EstateState { Seed = 4 };
        state.Agency.Book.Blocks.AddRange(back.Blocks);
        state.Agency.Book.NextId = back.NextId;
        EstateExtraStore.Write(state, data);
        var other = new EstateState();
        EstateExtraStore.Read(other, data);
        Check(data.ContainsKey(AgencyBookStore.Key) && other.Agency.Book.Blocks.Count == back.Blocks.Count, "agency book: the book is saved and read with the property business's other lines");
        var older = new EstateState();
        EstateExtraStore.Read(older, new Dictionary<string, string> { { EstateExtraStore.RentRingKey, "v=1|0|0|" } });
        Check(older.Agency.Book.Blocks.Count == 0, "agency book: a save made before the book loads as before");
    }

    private static void AgencyBookOwn()
    {
        EstateHolding h = Building();
        h.Units[0].Applicants.Add(new RentApplicant { Id = 1, Kind = 2, Offer = 40, Record = 80, Term = 90, Lapse = 9 });
        EstateRules.Accept(h, 1, 1);
        h.Units[0].Call = 2;
        h.Units[0].CallDay = 1;
        h.Units[0].CallCost = 100;
        h.Manager = (int)EstateManager.Agency;
        Check(!EstateRules.Runs(h) && !EstateRules.Handles(h) && EstateRules.ApplicantLift(h) == 1000 && !EstateRules.ManagerDay(h, 0, 5).Contractor, "agency book: a building under the agency whose agents are short of hours is the player's to run");
        h.Covered = true;
        Check(EstateRules.Runs(h) && EstateRules.Handles(h) && EstateRules.ApplicantLift(h) == EstateCatalogue.ManagedApplicants && EstateRules.ManagerDay(h, 0, 5).Contractor,
            "agency book: a building under the agency with its agents' hours is run as a property manager runs it");
        h.Manager = (int)EstateManager.Manager;
        h.Covered = false;
        Check(EstateRules.Runs(h) && EstateRules.Handles(h), "agency book: a property manager runs a building whatever the agency's hours");
        h.Manager = (int)EstateManager.Agent;
        Check(!EstateRules.Runs(h) && EstateRules.Handles(h), "agency book: a letting agent markets and lets a building but does not run it");
        h.Manager = (int)EstateManager.Self;
        Check(!EstateRules.Runs(h) && !EstateRules.Handles(h) && EstateRules.ApplicantLift(h) == 1000, "agency book: the player runs a building himself when nobody else does");
        Check(EstateCatalogue.Managers == 4 && EstateText_ManagerKeysMatch(), "agency book: the player's own agency is the fourth who can run a building");
        var s = new EstateState { Seed = 3 };
        EstateHolding kept = Building();
        kept.Manager = (int)EstateManager.Agency;
        kept.Place = "Elm~4";
        s.Holdings.Add(kept);
        int version;
        EstateState again = EstateStore.Read(EstateStore.Write(s), out version);
        Check(again != null && again.Holdings.Count == 1 && again.Holdings[0].Manager == (int)EstateManager.Agency && !again.Holdings[0].Covered, "agency book: a building under the agency stays so in the save, and its cover is worked out afresh each morning");
        string director = ModSource(@"Economy\Estate\EstateDirector.Duties.cs");
        int cut = director.IndexOf("private void AgentFee(", StringComparison.Ordinal), end = director.IndexOf("// A contractor paid for and sent", StringComparison.Ordinal);
        string fees = cut >= 0 && end > cut ? director.Substring(cut, end - cut) : string.Empty;
        Check(fees.Contains("h.Manager != (int)EstateManager.Agent") && fees.Contains("h.Manager != (int)EstateManager.Manager") && !fees.Contains("Agency") && BookOccurs(fees, "EstateGame.ManagerKey") == 2,
            "agency book: only a letting agent and a property manager charge the player; his own agency moves no money"
            );
    }

    private static bool EstateText_ManagerKeysMatch()
    {
        string text = ModSource(@"Economy\Estate\EstateText.cs");
        return text.Contains("\"reallife_estate_manager_agency\"") && text.Split(new[] { "reallife_estate_manager_" }, StringSplitOptions.None).Length == 5;
    }

    private static void AgencyBookSources()
    {
        string book = ModSource(@"Economy\Estate\EstateDirector.Book.cs"), rules = ModSource(@"Economy\Estate\AgencyBookRules.cs"), game = ModSource(@"Economy\Estate\EstateGame.cs");
        Check(!rules.Contains("UnityEngine") && !rules.Contains("SaveGameManager") && !ModSource(@"Economy\Estate\AgencyBook.cs").Contains("UnityEngine"), "agency book: the rules and the model are free of the game");
        Check(BookOccurs(book, "EstateGame.ManagementFeeKey") == 1 && BookOccurs(book, "EstateGame.LettingFeeKey") == 1 && BookOccurs(book, "Pay(e.Amount") == 2 && BookOccurs(book, "AgencyBookRules.MonthEnd(") == 1,
            "agency book: a fee is paid in one place only, from an event of the rules (the month's call is one: " + BookOccurs(book, "AgencyBookRules.MonthEnd(") + ")");
        Check(ModSource(@"Economy\Estate\EstateDirector.BookTests.cs").Contains("AgencyBookRules.MonthEnd(agency, System.Math.Max(_state.Months, agency.Book.Month) + 1, _blockEvents)") && BookOccurs(book, "_blockEvents.Clear();") >= 2,
            "agency book: the test's month is a month beyond the last, so no fee is paid twice for it");
        Check(game.Contains("ManagementFeeKey = \"reallife_transaction_estate_agency_fee\"") && game.Contains("LettingFeeKey = \"reallife_transaction_estate_agency_letting\"")
              && BizLines.Of("reallife_transaction_estate_agency_fee") == BizLine.Takings && BizLines.Of("reallife_transaction_estate_agency_letting") == BizLine.Takings,
            "agency book: both fee keys are takings of the business among BizLines' lists, so the Company app's books and BizMan count them once");
        Check(BizLines.Audit(new[] { "reallife_transaction_estate_agency_fee", "reallife_transaction_estate_agency_letting" }) == null && BizLines.Audit(new[] { "reallife_transaction_estate_agency_new" }) != null,
            "agency book: a new key of the agency that is in no list is caught");
        Check(ModSource(@"Economy\Estate\EstateDirector.Agency.cs").Contains("BookDay(day)") && ModSource(@"Economy\Estate\EstateDirector.Agency.cs").Contains("AgencyGone();")
              && ModSource(@"Economy\Estate\EstateDirector.Property.cs").Contains("AgencyCover();"), "agency book: the agency's day runs the book before its instructions' hours, a shut agency lets its blocks go, and the buildings are told their cover each morning");
        string app = ModSource(@"Economy\Estate\EstateApp.Agency.cs"), kit = ModSource(@"Ui\Estate\EstateLive.Market.cs"), building = ModSource(@"Ui\Estate\EstateLive.Building.cs");
        Check(app.Contains("\"blk:\"") && kit.Contains("\"blk:\"") && app.Contains("_director.PitchBlock(") && kit.Contains("_d.PitchBlock(") && app.Contains("_director.ResignBlock(") && kit.Contains("_d.ResignBlock(")
              && app.Contains("_director.WithdrawPitch(") && kit.Contains("_d.WithdrawPitch("), "agency book: the old Agency page and the kit's both list the blocks, pitch for them, take a pitch back and hand a block back");
        Check(ModSource(@"Economy\Estate\EstateApp.Duties.cs").Contains("EstateManager.Agency") && building.Contains("EstateManager.Agency"), "agency book: both interfaces let the player choose his own agency for a building");
        string keys = ModSource(@"Ui\UiScreens.Estate.cs");
        bool listed = true;
        foreach (string key in new[] { "reallife_estate_block_owner", "reallife_estate_btn_pitch", "reallife_estate_book_blocks", "reallife_ui_estate_fig_blocks", "reallife_estate_filter_managing", "reallife_estate_manager_agency" })
        {
            listed &= keys.Contains("\"" + key + "\"");
        }
        Check(listed, "agency book: the kit lists the new words of its Property screen");
        string tests = ModSource(@"Core\RealLifeOverhaulMod.cs");
        Check(tests.Contains("TestBlockOffer()") && tests.Contains("TestBlockMonth()") && ModSource(@"Meta\FeatureList.Economy.cs").Contains("reallife_estate_test_block_month"), "agency book: the two tests are on the options page and in the feature list");
        Check(game.Contains("agency.Skill = hours > 0 ? (int)(weighted / hours) : 0;"), "agency book: the agents' skill is read by their hours");
        string guide = ModSource(@"Economy\Estate\EstateGuide.cs");
        Check(guide.Contains("\"agb_blocks\"") && guide.Contains("\"agb_fee_low\"") && guide.Contains("\"agb_own_cover\""), "agency book: the Guide's page has the book's figures as facts");
    }

    private static int BookOccurs(string text, string find)
    {
        int n = 0, at = 0;
        while ((at = text.IndexOf(find, at, StringComparison.Ordinal)) >= 0)
        {
            n++;
            at += find.Length;
        }
        return n;
    }

    private static void AgencyBookTexts()
    {
        Dictionary<string, string> en = LocaleFile("en");
        string[] keys =
        {
            "reallife_estate_manager_agency", "reallife_estate_duty_agency_what", "reallife_estate_filter_managing", "reallife_estate_stage_managed", "reallife_estate_stage_lost", "reallife_estate_row_block",
            "reallife_estate_owner_0", "reallife_estate_owner_1", "reallife_estate_owner_2", "reallife_estate_owner_3", "reallife_estate_owner_4", "reallife_estate_owner_5", "reallife_estate_rival_agency",
            "reallife_estate_block_why_rival", "reallife_estate_block_why_nobody", "reallife_estate_block_why_standard", "reallife_estate_block_why_inspection", "reallife_estate_block_why_resigned",
            "reallife_estate_btn_pitch", "reallife_estate_btn_undercut", "reallife_estate_btn_withdraw", "reallife_estate_btn_resign", "reallife_estate_note_block_offer", "reallife_estate_note_block_won",
            "reallife_estate_note_block_lost_rival", "reallife_estate_note_block_dropped", "reallife_estate_note_block_fees", "reallife_estate_note_block_fined", "reallife_estate_note_block_penalty",
            "reallife_estate_why_no_agents", "reallife_estate_why_no_hours", "reallife_estate_why_blocks_full", "reallife_estate_why_pitched", "reallife_estate_why_offer_gone", "reallife_estate_why_no_agency",
            "reallife_estate_why_no_agency_own", "reallife_transaction_estate_agency_fee", "reallife_transaction_estate_agency_fee_label", "reallife_transaction_estate_agency_letting",
            "reallife_transaction_estate_agency_letting_label", "reallife_estate_test_block", "reallife_estate_test_block_month", "reallife_estate_test_block_full", "reallife_estate_test_block_none"
        };
        var lacking = keys.Where(k => !en.ContainsKey(k)).ToList();
        Check(lacking.Count == 0, "agency book: the words are in the English texts" + (lacking.Count > 0 ? " (lacking " + lacking[0] + ")" : string.Empty));
        string notes = string.Empty;
        foreach (string k in keys.Where(k => k.Contains("_note_") || k.Contains("_why_no") || k.Contains("_why_blocks") || k.Contains("_why_pitched") || k.Contains("_why_offer") || k.Contains("_test_block_")))
        {
            string v;
            if (en.TryGetValue(k, out v) && v.Length > 240)
            {
                notes += k + " ";
            }
        }
        Check(notes.Length == 0, "agency book: every notice is at most 240 characters " + notes);
        string all = string.Join("\n", keys.Where(en.ContainsKey).Select(k => en[k]));
        Check(!all.Contains('—') && !all.Contains('–') && !all.Contains(" - ") && !all.Contains('!'), "agency book: the words have no dash and no exclamation mark");
        Check(en.TryGetValue("reallife_estate_note_block_offer", out string offer) && offer.Contains("{owner}") && offer.Contains("{units}") && offer.Contains("{district}")
              && en["reallife_estate_note_block_won"].Contains("{owner}") && en["reallife_estate_note_block_won"].Contains("{block}")
              && en["reallife_estate_note_block_lost_rival"].Contains("{agency}") && en["reallife_estate_note_block_dropped"].Contains("{why}")
              && en["reallife_estate_note_block_fees"].Contains("{amount}") && en["reallife_estate_note_block_fined"].Contains("{block}") && en["reallife_estate_note_block_penalty"].Contains("{amount}"),
            "agency book: each notice names what its code fills in");
        string guide;
        Check(en.TryGetValue("reallife_guide_economy_estate", out guide) && guide.Contains("# Managing other owners' blocks") && guide.Contains("# Your own buildings") && guide.Contains("{agb_fee_low}")
              && guide.Contains("<b>Your own agency</b>") && en["reallife_estate_manager_agency"] == "Your own agency" && guide.Contains("<b>Hand it back</b>") && en["reallife_estate_btn_resign"] == "Hand it back"
              && guide.Contains("<b>Managing</b>") && en["reallife_estate_filter_managing"] == "Managing", "agency book: the Guide describes the blocks and the player's own buildings under the agency, naming the buttons as they read");
        string next;
        Check(en.TryGetValue("reallife_changes_next", out next) && next.Contains("estate agency"), "agency book: What's new tells of it");
        string docs = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\docs\features\estate-agency.md"));
        Check(System.IO.File.Exists(docs) && System.IO.File.ReadAllText(docs).Contains(AgencyBookStore.Key), "agency book: docs/features/estate-agency.md describes it and its save line");
    }

    private static void EstateAgencyBookCheckRun()
    {
        EstateAgencyBook();
        var watch = Stopwatch.StartNew();
        AgencyFile a = BookAgency(capacity: 400, skill: 85);
        var events = new List<BlockEvent>();
        for (int i = 0; i < 6; i++)
        {
            BookHeld(a);
        }
        for (int day = 1; day <= 365; day++)
        {
            for (int i = 0; i < a.Book.Blocks.Count; i++)
            {
                AgencyBookRules.BlockDay(a, a.Book.Blocks[i], 1, day, Climate(), events);
            }
            events.Clear();
        }
        Check(watch.Elapsed.TotalMilliseconds < 60, "agency book: a year of six blocks' days takes well under a frame (" + watch.Elapsed.TotalMilliseconds.ToString("0.00") + " ms)");
    }
}
