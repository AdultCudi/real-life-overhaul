using System;
using RealLifeOverhaul;
using RealLifeOverhaul.Flare;
using RealLifeOverhaul.Population;

// The residents' day's lift when the business table is not the one the day's pools and Flare's figures were sized for: a business founded after
// them has no row in either, the pass runs in slices while the table changes between them, and a table with no businesses at all.
internal static partial class Program
{
    private static void FlareLiftSliceChecks()
    {
        FlareLiftNewcomer();
        FlareLiftBetweenSlices();
        FlareLiftNoBusinesses();
        FlareLiftSweep();
    }

    // A business of the same kind and district as another, added to the table (as the game's registrations add one).
    private static int FlareLiftAddTwin(BusinessTable bt, int like, string key)
    {
        int n = bt.Add(key);
        bt.Type[n] = bt.Type[like];
        bt.District[n] = bt.District[like];
        bt.Kind[n] = bt.Kind[like];
        bt.Room[n] = bt.Room[like];
        bt.Hours[n] = bt.Hours[like];
        bt.Seen[n] = true;
        return n;
    }

    private static void FlareLiftNewcomer()
    {
        FlareResidentRig rig = FlareResidentCity(42, 25000, (int)FlareTier.Influencer);
        PopulationState s = rig.S;
        FlareCityState c = rig.C;
        int b = FlareResidentShop(s, Spend.Coffee);
        s.Businesses.Attention[b] = 300;
        c.Given[b] = 300;
        FlareCityRules.TakeHabit(c, s.Businesses, s.Day);
        int rowsBefore = c.BusinessCount;
        // a business founded after Flare sized its figures, in time to be in the population's pools of the next day
        int fresh = FlareLiftAddTwin(s.Businesses, b, "selftest|1");
        string fault = null;
        try
        {
            rig.Sim.BeginDay(s.Day + 1, Ordinary, rig.Seeds);
            rig.Sim.RunAll();
            FlareCityRules.MeasureLift(c, s, rig.Sim.Demand);
        }
        catch (Exception e)
        {
            fault = e.GetType().Name;
        }
        Check(fault == null, "flare city lift: a business in the day's pools that Flare has no row for does not fault the measure (" + (fault ?? "none") + ")");
        Check(c.BusinessCount == rowsBefore && c.Lift.Length == rowsBefore, "flare city lift: the figures keep their size until the pass binds the table again");
        Check(fresh >= c.BusinessCount, "flare city lift: the newcomer is past the figures' rows, as the fault needed");
    }

    private static void FlareLiftBetweenSlices()
    {
        FlareResidentRig rig = FlareResidentCity(42, 25000, (int)FlareTier.Influencer);
        PopulationState s = rig.S;
        int b = FlareResidentShop(s, Spend.Coffee);
        string fault = null;
        int slices = 0, added = -1;
        try
        {
            rig.Pass.Begin(rig.C, s, rig.Sim.Demand, s.Day + 1);
            rig.Pass.Inputs.Day = s.Day + 1;
            while (true)
            {
                WorkSlice slice = WorkSlice.Of(0, 1);
                bool done = rig.Pass.Step(ref slice);
                slices++;
                if (slices == 2)
                {
                    added = FlareLiftAddTwin(s.Businesses, b, "selftest|2");
                }
                if (slices == 5 && added >= 0)
                {
                    s.Businesses.Seen[added] = false;
                    s.Businesses.Key[added] = null;
                    s.Businesses.Count = added;   // and the table shrinks again
                }
                if (done || slices > 100000)
                {
                    break;
                }
            }
        }
        catch (Exception e)
        {
            fault = e.GetType().Name + " " + e.StackTrace;
        }
        Check(fault == null && slices > 5, "flare city lift: a pass driven slice by slice while a business is added and removed between the slices finishes (" + slices + " slices" + (fault != null ? ", " + fault : string.Empty) + ")");
    }

    private static void FlareLiftNoBusinesses()
    {
        var c = new FlareCityState();
        var s = new PopulationState();
        var dd = new PoolRules.DemandDay();
        string fault = null;
        try
        {
            FlareCityRules.MeasureLift(c, s, dd);
            FlareCityRules.MeasureLift(c, s, null);
        }
        catch (Exception e)
        {
            fault = e.GetType().Name;
        }
        Check(fault == null, "flare city lift: no businesses, no demand day: nothing measured and nothing faults (" + (fault ?? "none") + ")");
    }

    // The sliced pass and the whole pass end in the same figures, for cities of several seeds, with the table still.
    private static void FlareLiftSweep()
    {
        bool same = true;
        string why = string.Empty;
        for (int seed = 1; seed <= 4 && same; seed++)
        {
            FlareResidentRig whole = FlareResidentCity(seed * 7, 3000, (int)FlareTier.Known);
            FlareResidentRig sliced = FlareResidentCity(seed * 7, 3000, (int)FlareTier.Known);
            for (int round = 0; round < 2 && same; round++)
            {
                int day = whole.S.Day + 1 + round;
                whole.Pass.Inputs.Day = sliced.Pass.Inputs.Day = day;
                whole.Pass.Begin(whole.C, whole.S, whole.Sim.Demand, day);
                whole.Pass.RunAll();
                sliced.Pass.Begin(sliced.C, sliced.S, sliced.Sim.Demand, day);
                int guard = 0;
                WorkSlice slice = WorkSlice.Of(0, 1);
                while (!sliced.Pass.Step(ref slice) && guard++ < 1000000)
                {
                    slice = WorkSlice.Of(0, 1);
                }
                for (int b = 0; b < whole.C.BusinessCount && same; b++)
                {
                    if (whole.C.Lift[b] != sliced.C.Lift[b] || whole.C.Attention[b] != sliced.C.Attention[b])
                    {
                        same = false;
                        why = "seed " + seed + " business " + b;
                    }
                }
            }
        }
        Check(same, "flare city lift: the sliced pass and the whole pass end in the same lift and attention for every business (" + (same ? "4 cities, 2 days each" : why) + ")");
    }
}
