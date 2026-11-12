using System;
using System.Collections.Generic;
using RealLifeOverhaul.Population;

// A buyer that keeps the staff holds them to their jobs for 30 days (Scripts/Population/HoldRules.cs): the people a sale takes off the player's payroll
// stay in their jobs, are not shed or quit, and are free on the thirtieth day; the hold is in its own save line; no hold without a sale or in an old save.
internal static partial class Program
{
    private static void StaffHoldChecks()
    {
        PopulationState s = PopulationCity(out List<BusinessSeed> seeds);
        int shop = seeds.FindIndex(x => x.Type == "ba:businesstype_clothingstore");
        BusinessSeed mine = seeds[shop];
        mine.Owner = RealLifeOverhaul.Population.Owner.Player;
        mine.Staff = 2;
        seeds[shop] = mine;
        var sim = new PopulationSim();
        sim.BeginGenerate(s, 321, 1, 60, seeds);
        sim.RunAll();
        string key = PopulationState.KeyOf(mine.Street, mine.Number);
        int e = s.EmployerOfKey(key);
        var staff = new List<StaffRef>
        {
            new StaffRef { Id = "hold-a", Key = key, Pay = 2400, Hours = 40, Age = 30, Female = true },
            new StaffRef { Id = "hold-b", Key = key, Pay = 1800, Hours = 40, Age = 40, Female = false },
            new StaffRef { Id = "hold-c", Key = key, Pay = 1800, Hours = 40, Age = 35, Female = false }
        };
        PopulationLinks.Staff(s, staff, 2);
        int a = PopulationLinks.Linked(s, LinkKind.Employee, "hold-a"), b = PopulationLinks.Linked(s, LinkKind.Employee, "hold-b"), c = PopulationLinks.Linked(s, LinkKind.Employee, "hold-c");
        Check(s.Holds.Count == 0 && !HoldRules.Held(s, a), "staff hold: nothing is held before any sale");

        // the sale: a and b are kept, then the game drops all three; c is let go as always (a sale that kept nobody starts no hold)
        Check(HoldRules.Start(s, a, 10) && HoldRules.Start(s, b, 10) && HoldRules.Held(s, a) && HoldRules.Held(s, b) && !HoldRules.Held(s, c), "staff hold: the people a buyer kept are held, others are not");
        Check(!HoldRules.Start(s, -1, 10) && !HoldRules.Start(s, s.People.Count + 3, 10), "staff hold: nobody who is not there is held");
        staff.Clear();
        PopulationLinks.Staff(s, staff, 10);
        Check(s.People.Status[a] == (byte)Status.Worker && s.People.Job[a] == e && s.People.Status[b] == (byte)Status.Worker && s.People.Job[b] == e
              && (s.People.Flags[a] & PersonFlags.Linked) == 0, "staff hold: the game drops them but the held stay in their jobs, no longer the player's staff");
        Check(s.People.Status[c] == (byte)Status.Unemployed && s.People.Job[c] < 0, "staff hold: a member of staff not kept is out of work, as before");

        // the employer has no slots left (the business was shut): shedding and the daily turnover leave the held alone
        s.Employers.Slots[e] = 0;
        LabourRules.Shed(s, e);
        bool stayed = true;
        for (int day = 11; day < 10 + HoldRules.Days; day++)
        {
            LabourRules.Day(s, a, day, new List<int>[PopulationCatalogue.Fields], 1000);
            LabourRules.Day(s, b, day, new List<int>[PopulationCatalogue.Fields], 1000);
            stayed &= s.People.Job[a] == e && s.People.Job[b] == e;
        }
        Check(stayed && s.Employers.Filled[e] == 2, "staff hold: shed and the daily quits do not touch them inside the thirty days");

        Check(HoldRules.Expire(s, 10 + HoldRules.Days - 1) == 0 && HoldRules.Held(s, a), "staff hold: the day before the thirtieth they are still held");
        string line = HoldRules.Write(s);
        PopulationState back = PopulationStore.Decode(PopulationStore.Encode(s), out _);
        Check(line != null && back != null && HoldRules.Read(back, line) == 2 && HoldRules.Held(back, a) && HoldRules.Held(back, b) && back.Holds[s.People.Id[a]] == 10 + HoldRules.Days,
            "staff hold: a save and reload keeps the hold and its end day");
        PopulationState other = PopulationStore.Decode(PopulationStore.Encode(s), out _);
        other.Seed += 1;
        Check(HoldRules.Read(other, line) == 0 && other.Holds.Count == 0, "staff hold: a line from another city starts no hold");
        PopulationState old = PopulationStore.Decode(PopulationStore.Encode(s), out _);
        Check(HoldRules.Read(old, null) == 0 && HoldRules.Read(old, "garbage") == 0 && old.Holds.Count == 0, "staff hold: an old save with no line, or a damaged one, starts none");

        Check(HoldRules.Expire(s, 10 + HoldRules.Days) == 2 && !HoldRules.Held(s, a) && !HoldRules.Held(s, b) && HoldRules.Write(s) == null,
            "staff hold: it ends by itself on the thirtieth day and leaves nothing in the save");
        LabourRules.Shed(s, e);
        Check(s.Employers.Filled[e] == 0, "staff hold: after it, the employer's missing slots shed them like anyone");

        // a rival's business sold: its residents are held, not the player's linked staff
        PopulationState r = PopulationCity(out List<BusinessSeed> rseeds);
        var rsim = new PopulationSim();
        rsim.BeginGenerate(r, 321, 1, 60, rseeds);
        rsim.RunAll();
        int re = -1;
        for (int i = 0; i < r.Employers.Count && re < 0; i++)
        {
            re = r.Employers.Filled[i] > 0 && r.Employers.Kind[i] == (byte)EmployerKind.Business ? i : -1;
        }
        int held = HoldRules.StartAt(r, re, 5);
        Check(re >= 0 && held == r.Employers.Filled[re] && held > 0, "staff hold: when a rival sells a business the residents working there are held for the same thirty days");
        Check(HoldRules.StartAt(r, -1, 5) == 0, "staff hold: no business, no hold");
    }
}
