using System;
using RealLifeOverhaul.Economy;

// Build mode's rent a square metre by where the building stands (the owner, 5 Nov 2026: "rent per district not mapped for Uptown"):
// the figure and the Rents layer both read RentMapData.PerSqmIn for the cell's district and use, never the whole city's for the use.
internal static partial class Program
{
    private const string UptownRent = "reallife_neighborhood_uptown", CedarRent = "reallife_neighborhood_cedarheights", MidtownRent = "ba:neighborhood_midtown";

    private static RentMapData RentSample()
    {
        var m = new RentMapData();
        m.Clear(5, 30);
        m.Add(MidtownRent, 1, 400, 1000, 10, 0);    // shops: 12 000 a month over 1000 sqm = 12.00 a sqm
        m.Add(UptownRent, 1, 100, 1000, 10, 0);     // 3 000 over 1000 = 3.00
        m.Add(UptownRent, 2, 200, 1000, 10, 0);     // offices only in Uptown and Midtown
        m.Add(MidtownRent, 2, 500, 1000, 10, 0);
        m.Add(MidtownRent, 0, 300, 600, 10, 0);     // homes only in Midtown
        return m;
    }

    private static void BuildRentChecks()
    {
        RentMapData m = RentSample();
        Check(m.PerSqmIn(MidtownRent, 1) == 1200 && m.PerSqmIn(UptownRent, 1) == 300, "build rent: the same use in two districts gives two figures");
        Check(m.PerSqmIn(UptownRent, 1) != m.PerSqm(1) && m.PerSqm(1) == 750, "build rent: a district's figure is not the city's mean for the use");
        Check(m.PerSqmIn(UptownRent, 1) == m.PerSqm(m.Districts.IndexOf(UptownRent), 1), "build rent: the figure shown is the layer's cell");
        Check(m.PerSqmIn(UptownRent, 0) == m.PerSqm(0) && m.PerSqmIn(UptownRent, 0) > 0, "build rent: a district with none of a use takes the city's, a real figure");
        Check(m.PerSqmIn(CedarRent, 1) == m.PerSqm(1) && m.PerSqmIn(string.Empty, 2) == m.PerSqm(2) && m.PerSqmIn(null, 2) > 0, "build rent: an unknown or empty district never gives 0 or -1 while rents are read");
        Check(m.PerSqmIn(UptownRent, 3) == -1 && m.PerSqmIn(UptownRent, -1) == -1 && m.PerSqmIn(UptownRent, 9) == -1 && m.PerSqm(9) == -1, "build rent: a use nobody lets, or out of range, stays none (-1)");
        Check(new RentMapData().PerSqmIn(UptownRent, 1) == -1, "build rent: no rent data shows the same none state");
        RentMapData other = RentSample();
        Check(other.PerSqmIn(UptownRent, 1) == m.PerSqmIn(UptownRent, 1), "build rent: the same data gives the same figure for every reader (Going Public guests)");

        string director = ModSource(@"Construction\ConstructionDirector.BuildMode.cs");
        string window = ModSource(@"Construction\BuildMode.Window.cs");
        string hire = ModSource(@"Construction\Live\ConstructionLive.Hire.cs");
        string plots = ModSource(@"Construction\BuildMode.Plots.cs");
        Check(director.Contains("rents.PerSqmIn(district, use)") && !director.Contains("rents.PerSqm(u)") && !director.Contains("rents.PerSqm((int)use)"),
            "build rent: the director reads the district's rent, never the city's per use");
        Check(window.Contains("_d.RentPerSqm(_use, _site.FirstColumn)") && hire.Contains("ConstructionDirector.RentPerSqm(p.District, use)") && !hire.Contains("rents.PerSqm(use)"),
            "build rent: the catalogue card and a project's rent are read where the building stands");
        Check(plots.Contains("_d.RentStepOf(_plots.District[i], f.Use)") && director.Contains("RentStepOf(DistrictIdOf(view), use)"),
            "build rent: the Rents layer's tint of a square and of a plot reads the same local figure");
    }
}
