using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;

// The Services app's new logic, run day by day from the rules (ContractRules.Rivals.cs, the round's crew order, ServicesStore): rival cleaning firms bidding for the
// same contract and winning some, the named crews and the contracts they serve, the fees contracted a month over twelve months, a business's worth by day, and the
// save's line round trip with an old line that has none of it. The director calls these same rules on its day turn; the page reads the state they write.
internal static partial class Program
{
    private static ServicesState ServicesSeeded(int seed)
    {
        var s = new ServicesState { Seed = seed, FirstDay = 1, LastDay = 1 };
        s.Crew.Add("e1");
        s.Crew.Add("e2");
        s.Crew.Add("e3");
        return s;
    }

    private static List<ContractCandidate> ServicesCandidates()
    {
        var list = new List<ContractCandidate>();
        for (int i = 0; i < 6; i++)
        {
            list.Add(new ContractCandidate { Place = "Street" + i + "~" + (10 + i), Name = "Client " + i, District = "District " + (i % 3), Sqm = 200 + i * 40, Capacity = 20 + i * 5, X = i * 100, Z = 50, Income = 100 });
        }
        return list;
    }

    // One game day of the contract rules, in the order the director runs them: close what ran out, tidy the crews, note the month, draw an offer and let rivals bid for it.
    // The player takes an offer when its fee is under 1400 a week and keeps what a crew of that skill can win.
    private static void ServicesDay(ServicesState s, int day, int crewSkill, List<ContractCandidate> candidates, ref int bids, ref int taken)
    {
        s.LastDay = day;
        ContractRules.Close(s, day, crewSkill, null);
        ContractRules.Tidy(s);
        ContractRules.NoteMonth(s, day);
        if (ContractRules.OfferDue(s, day))
        {
            CleaningOffer offer = ContractRules.Draw(s, candidates, day, 1000);
            if (offer != null)
            {
                bids += ContractRules.Bid(s, offer, day);
            }
        }
        for (int i = s.Offers.Count - 1; i >= 0; i--)
        {
            if (s.Offers[i].Fee < 1400 && ContractRules.Winner(s.Offers[i], crewSkill) < 0)
            {
                CleaningClient c = ContractRules.Accept(s, s.Offers[i], day);
                taken += c != null ? 1 : 0;
                if (c != null)
                {
                    c.Fee = Math.Max(c.Fee, 400);
                }
            }
        }
    }

    private static void ServicesRunChecks()
    {
        // Rival firms bid for an offer as it opens, by the seed, the day and the place alone, and a worse crew loses more of them.
        var candidates = ServicesCandidates();
        int bids = 0, takenLow = 0, takenHigh = 0;
        ServicesState low = ServicesSeeded(11), high = ServicesSeeded(11);
        for (int day = 1; day <= 120; day++)
        {
            int b = 0, t = 0;
            ServicesDay(low, day, 40, candidates, ref bids, ref takenLow);
            ServicesDay(high, day, 95, candidates, ref b, ref takenHigh);
        }
        Check(bids > 0, "services run: rival cleaning firms bid for the offers over 120 days (" + bids + " bids)");
        Check(low.RivalWon > high.RivalWon && low.RivalWon > 0, "services run: a rival firm wins contracts from a weak crew and fewer from a strong one (" + low.RivalWon + " against " + high.RivalWon + ")");
        ServicesState again = ServicesSeeded(11);
        int b2 = 0, t2 = 0;
        for (int day = 1; day <= 120; day++)
        {
            ServicesDay(again, day, 40, candidates, ref b2, ref t2);
        }
        Check(ServicesStore.Write(again) == ServicesStore.Write(low), "services run: the same seed and days give the same bids, wins and months (deterministic, Going Public safe)");
        var probe = new CleaningOffer { Place = "X~1", Fee = 1000 };
        probe.Rivals.Add(new RivalBid { Firm = 0, Pct = 90, Skill = 60 });
        Check(ContractRules.Winner(probe, 60) == 0 && ContractRules.Winner(probe, 80) < 0 && ContractRules.Score(90, 60) > ContractRules.Score(100, 60),
            "services run: the client picks by fee and skill: a cheaper rival of the same skill wins, and a better crew beats it");
        bool bounds = true;
        foreach (CleaningOffer o in low.Offers)
        {
            bounds &= o.Rivals.Count <= ServicesCatalogue.RivalBidsMost;
            foreach (RivalBid r in o.Rivals)
            {
                bounds &= r.Pct >= 82 && r.Pct <= 110 && r.Skill >= 40 && r.Skill <= 90 && r.Firm >= 0 && r.Firm < ServicesCatalogue.RivalFirms.Length;
            }
        }
        Check(bounds, "services run: every bid is within its bounds (a fee of 82 to 110 per cent, a skill of 40 to 90, a firm of the catalogue, at most three rivals an offer)");

        // The fees contracted a month: one figure a month, twelve kept, no month rewritten.
        Check(low.FeeNoted >= 3 && low.FeeNoted <= ServicesCatalogue.FeeMonths && low.FeeMonth == 120 / ServicesCatalogue.MonthDays, "services run: a month is noted once on its first day (" + low.FeeNoted + " months)");
        int[] series = ContractRules.MonthSeries(low);
        Check(series.Length == low.FeeNoted && series[series.Length - 1] > 0, "services run: the ring of monthly fees holds the months noted, the last above nothing (" + series.Length + " of them)");
        ServicesState months = ServicesSeeded(3);
        for (int day = 1; day <= 400; day++)
        {
            ContractRules.NoteMonth(months, day);
        }
        Check(months.FeeNoted == ServicesCatalogue.FeeMonths && ContractRules.MonthSeries(months).Length == ServicesCatalogue.FeeMonths && !ContractRules.NoteMonth(months, 400),
            "services run: the monthly ring is capped at twelve and a day of a month already noted adds nothing");

        // The named crews: a place, a lead by skill, a contract kept by a crew, and what happens when its members go.
        ServicesState crews = ServicesSeeded(5);
        ContractRules.Place(crews, "e1", 0);
        ContractRules.Place(crews, "e2", 0);
        ContractRules.Place(crews, "e3", 1);
        ContractRules.Place(crews, "nobody", 1);
        Func<string, int> skill = id => id == "e1" ? 50 : id == "e2" ? 80 : 60;
        Check(crews.GroupOf("e2") == 0 && crews.GroupOf("e3") == 1 && crews.GroupOf("nobody") < 0 && ContractRules.Lead(crews, 0, skill) == "e2" && ContractRules.Lead(crews, 2, skill) == null,
            "services run: a cleaner stands in one named crew, the lead is the best of it, and somebody off the crew cannot be placed");
        crews.Clients.Add(new CleaningClient { Place = "a~1", Outside = true, Fee = 900, Crew = 1 });
        crews.Crew.Remove("e3");
        ContractRules.Tidy(crews);
        Check(crews.GroupOf("e3") < 0 && crews.Clients[0].Crew == -1, "services run: a cleaner who leaves the crew leaves the named crew, and a contract of an empty crew is kept by whoever is free");

        // The round gives a contract to its own crew first, unless that crew would be much later.
        var cache = new TravelCache();
        RoundCleaner own = new RoundCleaner { Id = "e1", Name = "A", From = 480, To = 1200, X = 0, Z = 0, Skill = 70, Mood = 80, Group = 1 };
        RoundCleaner other = new RoundCleaner { Id = "e2", Name = "B", From = 480, To = 1200, X = 0, Z = 0, Skill = 70, Mood = 80, Group = 0 };
        var visits = new List<RoundVisit>();
        RoundRules.Plan(new List<RoundCleaner> { other, own }, new List<RoundJob> { new RoundJob { Client = 0, X = 300, Z = 0, Force = 1, Earliest = 600, Need = 2000, Group = 1 } }, cache, false, 600, visits);
        bool ownFirst = visits.Count == 1 && visits[0].Cleaner == 1;
        visits.Clear();
        own.FreeFrom = 1000;   // the contract's own crew is busy for hours: the other crew takes it
        other.FreeFrom = 0;
        RoundRules.Plan(new List<RoundCleaner> { other, own }, new List<RoundJob> { new RoundJob { Client = 0, X = 300, Z = 0, Force = 1, Earliest = 600, Need = 2000, Group = 1 } }, cache, false, 600, visits);
        Check(ownFirst && visits.Count == 1 && visits[0].Cleaner == 0, "services run: a contract's own named crew goes first, and another crew takes it when the crew is busy much longer");

        // A business's worth by day: thirty days kept, the change over them, nothing for a business with one day.
        ServicesState worth = ServicesSeeded(9);
        for (int day = 100; day <= 150; day++)
        {
            ContractRules.NoteWorth(worth, "Shop~1", day, 100000 + (day - 100) * 1000);
        }
        int[] w = ContractRules.WorthSeries(worth, "Shop~1", 150);
        Check(w.Length == ServicesCatalogue.WorthDays && w[0] == 100000 + 21 * 1000 && w[w.Length - 1] == 150000 && ContractRules.Change(w) > 0 && ContractRules.WorthSeries(worth, "Other~2", 150).Length == 0
              && ContractRules.Change(new[] { 5 }) == 0, "services run: a business's worth is kept for thirty days, its change reads from the first to the last, and a business with none has no line");

        // The save: every new part comes back as written, and a line from before them loads with none.
        string line = ServicesStore.Write(low);
        ServicesState back = ServicesStore.Read(line, out _);
        Check(back != null && ServicesStore.Write(back) == line && back.RivalWon == low.RivalWon && back.FeeNoted == low.FeeNoted && back.Offers.Count == low.Offers.Count,
            "services run: the line read back and written again is the same line (bids, months, wins)");
        crews.Crew.Add("e3");
        ContractRules.Place(crews, "e3", 1);
        crews.Clients[0].Crew = 1;
        for (int day = 100; day <= 105; day++)
        {
            ContractRules.NoteWorth(crews, "Shop~1", day, 90000 + day);
        }
        string crewLine = ServicesStore.Write(crews);
        ServicesState crewBack = ServicesStore.Read(crewLine, out _);
        Check(crewBack != null && crewBack.GroupOf("e3") == 1 && crewBack.Clients.Count == 1 && crewBack.Clients[0].Crew == 1 && ContractRules.WorthSeries(crewBack, "Shop~1", 105).Length == 6 && ServicesStore.Write(crewBack) == crewLine,
            "services run: the named crews, a contract's crew and the worth by day survive the save");
        string old = line.Substring(0, line.IndexOf("^g:", StringComparison.Ordinal));
        ServicesState oldState = ServicesStore.Read(old, out _);
        Check(oldState != null && oldState.Places.Count == 0 && oldState.FeeNoted == 0 && oldState.FeeMonth == -1 && oldState.Worths.Count == 0 && oldState.Offers.Count == low.Offers.Count,
            "services run: a save from before the new parts loads with none of them and plays on");
        int bs = 0, ts = 0;
        for (int day = 121; day <= 150; day++)
        {
            ServicesDay(oldState, day, 60, candidates, ref bs, ref ts);
        }
        Check(oldState.FeeNoted >= 1, "services run: an old save plays 30 more days and notes its first month");

        // The next thirty days from a line saved on day 90 are the same as from the state that was saved.
        ServicesState live = ServicesSeeded(21);
        int lb = 0, lt = 0;
        for (int day = 1; day <= 90; day++)
        {
            ServicesDay(live, day, 55, candidates, ref lb, ref lt);
        }
        ServicesState loaded = ServicesStore.Read(ServicesStore.Write(live), out _);
        int cb = 0, ct = 0;
        for (int day = 91; day <= 120; day++)
        {
            ServicesDay(live, day, 55, candidates, ref lb, ref lt);
            ServicesDay(loaded, day, 55, candidates, ref cb, ref ct);
        }
        Check(ServicesStore.Write(live) == ServicesStore.Write(loaded), "services run: a save written on day 90 gives the same next 30 days");
    }
}
