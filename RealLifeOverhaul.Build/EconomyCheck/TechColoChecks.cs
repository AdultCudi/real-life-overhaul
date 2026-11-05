using System;
using System.Collections.Generic;
using RealLifeOverhaul;
using RealLifeOverhaul.Tech;

// The named colocation contracts, run as the day turn runs them (the letting moves DataCentre.Colo, then ColoRules.Sync and Turn): clients come from the city's companies, the contracts
// always add up to the racks let, rival halls bid only where they can host under the same supply limits, terms end in renewals and losses, and the save keeps them.
internal static partial class Program
{
    private static void TechColoChecks()
    {
        int offers = 0, renewed = 0, lost = 0, left = 0, days = 0;
        bool added = true, names = true, unique = true;
        var pool = new HashSet<int>(ColoRules.Pool());
        for (int seed = 1; seed <= 6; seed++)
        {
            var f = new InfraFile { Seed = seed * 977 };
            var d = new DataCentre { Place = "dc~" + seed, Tier = seed % InfraCatalogue.Tiers, Sqm = 6000, Racks = 400, Fitted = 0 };
            f.Centres.Add(d);
            var notes = new List<ColoNote>();
            for (int day = 1; day <= 900; day++)
            {
                if (day < 200)
                {
                    d.Colo++;   // the letting takes a rack a day until the hall is as full as the city wants
                }
                else if (day % 40 == 0 && d.Colo > 0)
                {
                    d.Colo = Math.Max(0, d.Colo - 12);   // and some churn
                }
                notes.Clear();
                ColoRules.Sync(f, d, day, notes);
                ColoRules.Turn(f, d, day, seed % 2 == 0 ? 1000 : 700, notes);
                days++;
                foreach (ColoNote n in notes)
                {
                    offers += n.Kind == ColoNote.Offer ? 1 : 0;
                    renewed += n.Kind == ColoNote.Renewed ? 1 : 0;
                    lost += n.Kind == ColoNote.Lost ? 1 : 0;
                    left += n.Kind == ColoNote.Left ? 1 : 0;
                }
                if (!(ColoRules.Held(f, d.Place) <= d.Colo && d.Colo - ColoRules.Held(f,
                    d.Place) < ColoRules.RacksLeast + ColoRules.RacksMost)) { added = false; Console.WriteLine("  colo gap seed " + seed + " day " + day + " held " + ColoRules.Held(f, d.Place) + " colo " + d.Colo); }
            }
            var clients = new HashSet<int>();
            foreach (ColoContract c in f.Colo)
            {
                names &= pool.Contains(c.Client) && ColoRules.NameOf(c.Client).Length > 0;
                unique &= clients.Add(c.Client);
            }
        }
        Check(added, "tech colo: the named contracts never hold more racks than the hall lets, and at most a few unplaced ones stay between");
        Check(names && unique, "tech colo: every client is one of the city's own companies and holds one contract in a hall");
        Check(offers > 0 && renewed > 0 && lost > 0,
            "tech colo: over " + days + " days of six halls rivals bid (" + offers + "), terms renew (" + renewed + ") and clients are lost to rivals (" + lost + ")");
        Check(left > 0, "tech colo: a client leaves when the racks let fall (" + left + ")");
        // The rival halls are held to the same supply limits: a rival cannot take more racks than its floor, feed, plant and line allow.
        bool every = true, tight = false;
        for (int r = 0; r < HiringCatalogue.RivalFirms.Length; r++)
        {
            DataCentre h = ColoRules.RivalHall(5, r);
            int fit = InfraLimits.Fit(h, false, ColoRules.RivalMbps, out _);
            every &= ColoRules.RivalCanHost(5, r, fit) && !ColoRules.RivalCanHost(5, r, fit + 1);
            tight |= fit < ColoRules.RacksMost;
        }
        Check(every && ColoRules.RivalCanHost(5, 0, 1) == (InfraLimits.Fit(ColoRules.RivalHall(5, 0), false, ColoRules.RivalMbps, out _) >= 1) && !ColoRules.RivalCanHost(5, 0, 100000),
            "tech colo: a rival hall can host exactly what its floor, feed, plant and connection let it, as a hall of the player's");
        Check(tight, "tech colo: some rival halls are too full to take a big client, so the limits matter to them");
        // A client with a rival's offer chooses by tier and uptime: a better hall keeps more of them.
        int stayGood = 0, stayBad = 0;
        for (int id = 1; id <= 400; id++)
        {
            foreach (int uptime in new[] { 1000, 400 })
            {
                var f = new InfraFile { Seed = 31 };
                var d = new DataCentre { Place = "dc~x", Tier = 2, Sqm = 6000, Colo = 10 };
                f.Colo.Add(new ColoContract { Id = id, Place = "dc~x", Client = 1, Racks = 10, End = 100, Rival = 3, Weighed = true });
                ColoRules.Turn(f, d, 100, uptime, null);
                if (f.Colo.Count > 0)
                {
                    stayGood += uptime == 1000 ? 1 : 0;
                    stayBad += uptime == 400 ? 1 : 0;
                }
            }
        }
        Check(stayGood > stayBad && stayBad > 0 && stayGood < 400, "tech colo: with a rival's bid a client stays more with a hall that stays up (" + stayGood + " of 400 against " + stayBad + ")");
        // the save keeps the contracts, and an old save has none and places its racks on its next day
        var file = new InfraFile { Seed = 7 };
        var hall = new DataCentre { Place = "dc~9", Sqm = 3000, Racks = 100, Colo = 30 };
        file.Centres.Add(hall);
        ColoRules.Sync(file, hall, 50, null);
        file.Colo[0].Rival = 2;
        file.Colo[0].Weighed = true;
        InfraFile back = InfraStore.Read(InfraStore.Write(file), out _);
        bool same = back.Colo.Count == file.Colo.Count && back.NextColo == file.NextColo && file.Colo.Count > 0;
        for (int i = 0; same && i < file.Colo.Count; i++)
        {
            ColoContract a = file.Colo[i], b = back.Colo[i];
            same = a.Id == b.Id && a.Place == b.Place && a.Client == b.Client && a.Racks == b.Racks && a.Fee == b.Fee && a.Start == b.Start && a.End == b.End && a.Rival == b.Rival && a.Weighed == b.Weighed;
        }
        Check(same, "tech colo: the save keeps each contract");
        string text = InfraStore.Write(file);
        int cut = text.IndexOf("~k:", StringComparison.Ordinal);
        InfraFile old = InfraStore.Read(cut > 0 ? text.Substring(0, cut) : text, out _);
        DataCentre od = old.CentreAt("dc~9");
        Check(cut > 0 && old.Colo.Count == 0 && od != null && od.Colo == 30, "tech colo: an old save loads with its racks let and no contracts");
        ColoRules.Sync(old, od, 60, null);
        Check(old.Colo.Count > 0 && ColoRules.Held(old, "dc~9") <= 30, "tech colo: and its racks are placed with named clients on the next day");
    }
}
