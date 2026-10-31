using System;
using System.Collections.Generic;
using RealLifeOverhaul.Population;

// The people of other systems as residents: the player's staff each linked to a fitting resident in the job the game gives them at the game's pay, the
// same resident on every machine for the same city, out of work again once let go; the mod's own people linked and let go by kind; and what other
// features read of the population (CityPopulation) answering as a city without one while it does not run.
internal static partial class Program
{
    private static void PopulationLinkChecks()
    {
        PopulationState s = PopulationCity(out List<BusinessSeed> seeds);
        int shop = seeds.FindIndex(x => x.Type == "ba:businesstype_clothingstore");
        BusinessSeed mine = seeds[shop];
        mine.Owner = Owner.Player;
        mine.Staff = 2;
        seeds[shop] = mine;
        var sim = new PopulationSim();
        sim.BeginGenerate(s, 321, 1, 60, seeds);
        sim.RunAll();
        string key = PopulationState.KeyOf(mine.Street, mine.Number);
        int e = s.EmployerOfKey(key);
        Check(e >= 0 && s.Employers.Players[e] && s.Employers.Slots[e] == 2 && s.Employers.Commuters[e] == 0, "population links: the player's business employs the game's own staff, no commuters");
        var staff = new List<StaffRef>
        {
            new StaffRef { Id = "staff-a", Key = key, Pay = 2400, Hours = 40, Age = 30, Female = true },
            new StaffRef { Id = "staff-b", Key = key, Pay = 1800, Hours = 30, Age = 52, Female = false }
        };
        int made = PopulationLinks.Staff(s, staff, 2);
        int a = PopulationLinks.Linked(s, LinkKind.Employee, "staff-a"), b = PopulationLinks.Linked(s, LinkKind.Employee, "staff-b");
        Check(made == 2 && a >= 0 && b >= 0 && a != b && s.People.Job[a] == e && s.People.Pay[a] == 2400 && s.People.Hours[b] == 30 && s.People.Female[a] == 1
              && Math.Abs(s.Age(b, 2) - 52) <= PopulationLinks.AgeGap && (s.People.Flags[a] & PersonFlags.Linked) != 0 && s.Employers.Filled[e] == 2,
            "population links: each member of the player's staff is a resident of their age and sex, in the job the game gives them at the game's pay");
        Check(PopulationLinks.Staff(s, staff, 3) == 0 && PopulationLinks.Linked(s, LinkKind.Employee, "staff-a") == a, "population links: a link holds from day to day, the same resident");
        PopulationState again = PopulationCity(out List<BusinessSeed> seeds2);
        seeds2[shop] = mine;
        var sim2 = new PopulationSim();
        sim2.BeginGenerate(again, 321, 1, 60, seeds2);
        sim2.RunAll();
        PopulationLinks.Staff(again, staff, 2);
        Check(PopulationLinks.Linked(again, LinkKind.Employee, "staff-a") == a, "population links: the same city links the same resident on every machine");
        staff.RemoveAt(1);
        PopulationLinks.Staff(s, staff, 4);
        Check(PopulationLinks.Linked(s, LinkKind.Employee, "staff-b") < 0 && s.People.Status[b] == (byte)Status.Unemployed && (s.People.Flags[b] & PersonFlags.Linked) == 0 && s.Employers.Filled[e] == 1,
            "population links: one the player lets go is out of work again, and no longer linked");
        var asks = new List<LinkAsk> { new LinkAsk { Kind = LinkKind.Partner, Key = "social:3", Age = 29, Female = false }, new LinkAsk { Kind = LinkKind.Roommate, Key = "mate:1", Age = 25, Female = true } };
        Check(PopulationLinks.People(s, asks, 4) == 2 && PopulationLinks.Linked(s, LinkKind.Partner, "social:3") >= 0, "population links: the mod's own people are residents too, each by its system's id");
        asks.RemoveAt(1);
        PopulationLinks.People(s, asks, 5);
        Check(PopulationLinks.Linked(s, LinkKind.Roommate, "mate:1") < 0 && PopulationLinks.Linked(s, LinkKind.Partner, "social:3") >= 0, "population links: one no longer asked for (the roommate moved out) is let go, the others kept");
        string line = PopulationStore.Encode(s);
        PopulationState back = PopulationStore.Decode(line, out _);
        Check(back != null && PopulationLinks.Linked(back, LinkKind.Employee, "staff-a") == a, "population links: the links come back with the save");

        CityPopulation.Live = null;
        CityPopulation.Pools = null;
        Check(!CityPopulation.Running && CityPopulation.People == 0 && CityPopulation.Unemployment == -1 && CityPopulation.IncomeLevel("ba:neighborhood_midtown", 1300) == 0
              && CityPopulation.Pull("ba:neighborhood_midtown", 0) == -1000 && !CityPopulation.Business("x|1", out _, out _, out int factor) && factor == 1000,
            "population links: while the population does not run every reader gets a city without one (no figure, the game's own counts)");
        CityPopulation.Live = s;
        CityPopulation.Pools = sim.Demand;
        int midtown = s.DistrictOf("ba:neighborhood_midtown");
        int start = 1300;
        Check(CityPopulation.Running && CityPopulation.People == s.People.Live && CityPopulation.IncomeLevel("ba:neighborhood_midtown", start) == start
              && CityPopulation.District("ba:neighborhood_midtown") == s.Figures[midtown] && CityPopulation.Spending(Spend.Groceries) > 0,
            "population links: running, the city economy's income starts where it stood (its district's mean income against its first day) and the readers see the figures");
        CityPopulation.Live = null;
        CityPopulation.Pools = null;
    }
}
