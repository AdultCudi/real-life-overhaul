using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using RealLifeOverhaul.Flare;
using RealLifeOverhaul.Utilities;

// The new day's and the hour's pace in the life features (Scripts/Utilities/UtilitiesModel's AccountIndex,
// Scripts/Social/Flare/FlareModel's shops seen): an account looked up by place and service from the index is the one
// UtilitiesState.Find gives, at the caps, after an account is put in place of a closed one, and with odd places; a shop
// seen looked up from Flare's index is the one the list's first gives, as the list grows between look-ups; and both
// cost a fraction of the passes of compares they replace.
internal static partial class Program
{
    private static void LifePace()
    {
        var watch = Stopwatch.StartNew();
        int before = _run, failedBefore = _failed;
        PaceAccounts();
        PaceAccountsCost();
        PaceShopsSeen();
        Console.WriteLine("  the new day's pace (life): " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed, " + watch.ElapsedMilliseconds + " ms");
    }

    // 300 places with power and water (and the internet at some), a few odd accounts: no place, a place twice.
    private static UtilitiesState PaceUtilities(int places, int seed)
    {
        var rng = new Random(seed);
        var s = new UtilitiesState();
        for (int p = 0; p < places; p++)
        {
            string place = "ba:street_" + (p % 37) + "~" + p;
            for (int service = 0; service < UtilitiesCatalogue.AllServices; service++)
            {
                if (service == (int)Service.Internet && rng.Next(3) != 0)
                {
                    continue;
                }
                s.Accounts.Add(new RealLifeOverhaul.Utilities.Account { Place = place, Service = (Service)service, OpenedDay = p * 10 + service });
            }
        }
        s.Accounts.Add(new RealLifeOverhaul.Utilities.Account { Place = null, Service = Service.Power, OpenedDay = -1 });
        s.Accounts.Insert(rng.Next(s.Accounts.Count), new RealLifeOverhaul.Utilities.Account { Place = "ba:street_3~3", Service = Service.Water, OpenedDay = -2 });   // (a place and service twice: the first is Find's)
        return s;
    }

    private static void PaceAccounts()
    {
        UtilitiesState s = PaceUtilities(300, 5);
        var index = new AccountIndex();
        var rng = new Random(6);
        int asked = 0, same = 0;
        for (int i = 0; i < 4000; i++)
        {
            string place = rng.Next(20) == 0 ? (rng.Next(2) == 0 ? null : "nowhere~1") : "ba:street_" + (rng.Next(320) % 37) + "~" + rng.Next(320);
            var service = (Service)rng.Next(UtilitiesCatalogue.AllServices);
            asked++;
            same += ReferenceEquals(index.Find(s, place, service), s.Find(place, service)) ? 1 : 0;
        }
        foreach (RealLifeOverhaul.Utilities.Account a in s.Accounts)
        {
            asked++;
            same += ReferenceEquals(index.Find(s, a.Place, a.Service), s.Find(a.Place, a.Service)) ? 1 : 0;   // (every account's own place and service)
        }
        Check(s.Accounts.Count > 600 && same == asked, "life pace: an account by place and service from the index is UtilitiesState.Find's, the first of a place twice and no place included (" + same + " of " + asked + ")");

        // Sync's way: a closed account taken out and a new one put in its place (in a list with no place and service twice,
        // as the store and Sync keep it); and the index built again for another state.
        s = PaceUtilities(300, 8);
        s.Accounts.RemoveAll(x => x.OpenedDay == -2);
        index.Build(s);
        bool put = true;
        for (int i = 0; i < 200; i++)
        {
            int at = rng.Next(s.Accounts.Count);
            RealLifeOverhaul.Utilities.Account old = s.Accounts[at];
            if (old.Place == null)
            {
                continue;
            }
            s.Accounts.Remove(old);
            var fresh = new RealLifeOverhaul.Utilities.Account { Place = old.Place, Service = old.Service, OpenedDay = 1000 + i };
            s.Accounts.Add(fresh);
            index.Put(s, fresh);
            put &= ReferenceEquals(index.Find(s, fresh.Place, fresh.Service), s.Find(fresh.Place, fresh.Service));
        }
        UtilitiesState other = PaceUtilities(40, 9);
        bool rebuilt = ReferenceEquals(index.Find(other, "ba:street_2~2", Service.Power), other.Find("ba:street_2~2", Service.Power));
        Check(put && rebuilt, "life pace: an account put in place of a closed one is found as Find finds it, and another state's accounts are indexed afresh");
        Check(index.Find(null, "x", Service.Power) == null && index.Find(s, "ba:street_3~3", (Service)7) == s.Find("ba:street_3~3", (Service)7),
            "life pace: no state, or a service out of the catalogue, is answered as Find would");
    }

    private static void PaceAccountsCost()
    {
        UtilitiesState s = PaceUtilities(300, 7);
        var places = new string[300];
        for (int p = 0; p < places.Length; p++)
        {
            places[p] = "ba:street_" + (p % 37) + "~" + p;
        }
        double linear = double.MaxValue, indexed = double.MaxValue;
        for (int attempt = 0; attempt < 3; attempt++)
        {
            linear = Math.Min(linear, PaceHourLinear(s, places));
            indexed = Math.Min(indexed, PaceHourIndexed(s, places));
        }
        Console.WriteLine("  life pace: an hour's look-ups for 300 places (power, water, the internet) " + linear.ToString("0.000") + " ms in passes of compares, " + indexed.ToString("0.000")
                          + " ms from the index built for it");
        Check(indexed < linear * 0.25, "life pace: an hour's look-ups from the index built for them cost a fraction of the passes of compares (" + indexed.ToString("0.000") + " against "
                                       + linear.ToString("0.000") + " ms)");
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static double PaceHourLinear(UtilitiesState s, string[] places)
    {
        int found = 0;
        long began = Stopwatch.GetTimestamp();
        for (int p = 0; p < places.Length; p++)
        {
            string place = places[p];
            found += s.Find(place, Service.Power) != null ? 1 : 0;
            found += s.Find(place, Service.Water) != null ? 1 : 0;
            found += s.Find(place, Service.Internet) != null ? 1 : 0;
        }
        double ms = (Stopwatch.GetTimestamp() - began) * 1000.0 / Stopwatch.Frequency;
        return found < 0 ? ms + 1 : ms;
    }

    private static readonly AccountIndex PaceIndex = new AccountIndex();

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static double PaceHourIndexed(UtilitiesState s, string[] places)
    {
        int found = 0;
        long began = Stopwatch.GetTimestamp();
        PaceIndex.Build(s);
        for (int p = 0; p < places.Length; p++)
        {
            string place = places[p];
            found += PaceIndex.Find(s, place, Service.Power) != null ? 1 : 0;
            found += PaceIndex.Find(s, place, Service.Water) != null ? 1 : 0;
            found += PaceIndex.Find(s, place, Service.Internet) != null ? 1 : 0;
        }
        double ms = (Stopwatch.GetTimestamp() - began) * 1000.0 / Stopwatch.Frequency;
        return found < 0 ? ms + 1 : ms;
    }

    // Flare's shops seen: the list grows (a new shop at its end) between look-ups, as the day's round has it.
    private static void PaceShopsSeen()
    {
        var f = new FlareFile();
        var rng = new Random(17);
        int asked = 0, same = 0;
        for (int i = 0; i < 3000; i++)
        {
            if (rng.Next(4) == 0 && f.Shops.Count < 400)
            {
                string place = rng.Next(30) == 0 ? null : "ba:street_" + rng.Next(25) + "~" + rng.Next(30);
                f.Shops.Add(new FlareShopSeen { Place = place, FirstDay = i });   // (a place twice now and then: the first is the one)
            }
            string ask = rng.Next(25) == 0 ? null : "ba:street_" + rng.Next(25) + "~" + rng.Next(30);
            FlareShopSeen want = null;
            for (int k = 0; k < f.Shops.Count; k++)
            {
                if (f.Shops[k].Place == ask)
                {
                    want = f.Shops[k];
                    break;
                }
            }
            asked++;
            same += ReferenceEquals(f.Shop(ask), want) ? 1 : 0;
        }
        f.Shops.Clear();
        f.Shops.Add(new FlareShopSeen { Place = "a~1" });
        bool shortened = f.Shop("a~1") == f.Shops[0] && f.Shop("ba:street_1~1") == null;
        Check(same == asked && shortened, "life pace: a shop seen, looked up from Flare's index as the list grows, is the list's first at the place (" + same + " of " + asked + "), a shortened list too");
    }
}
