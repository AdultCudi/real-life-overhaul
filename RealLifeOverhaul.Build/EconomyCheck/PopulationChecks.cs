using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using RealLifeOverhaul;
using RealLifeOverhaul.Population;
using PopLedger = RealLifeOverhaul.Population.Ledger;

// The city's population (plans/tech-overhaul/POPULATION.md; the owner, 1 Nov 2026: "if we can make it more realistic that would be great"),
// game-free: Build 3682's city measured from its buildings (homes and jobs by district, in a realistic band), the people generated from it with
// the city's seed (the same seed the same city, to the byte), a day lived with every cent placed (what households spend is what businesses take
// plus what goes beyond the city; a household's spending at each business, summed over households, is that business's takings from residents),
// ten years lived without the player (the population in its band, births, deaths and moves plausible, the labour market clearing), the anchors
// that keep the game's own customers on the first day, and the costs against the slice and the save's budgets.
internal static partial class Program
{
    private static void PopulationChecks()
    {
        PopulationMeasure();
    }

    // ---------------------------------------------------------------- the city as the checks know it (EconomyCheck/PopulationCity.txt)

    private static string PopulationFile => System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\PopulationCity.txt"));

    // The customers a size has room for as a building type (BuildingSizeData.customerCapacities in defs_BuildingSizeData.json).
    private static int RoomOf(string type, string size, int version)
    {
        string s = size.Replace("ba:buildingsize_", string.Empty);
        if (type == "ba:buildingtype_cinema")
        {
            return version == 3 ? 100 : version == 2 ? 125 : 150;
        }
        if (type == "ba:buildingtype_theater")
        {
            return version == 3 ? 150 : version == 2 ? 175 : 200;
        }
        bool office = type == "ba:buildingtype_office";
        char c = s.Length > 0 ? s[0] : 'a';
        switch (c)
        {
            case 'a': return office ? 4 : 15;
            case 'c': return office ? 8 : 30;
            case 'd': return office ? 10 : 40;
            case 'm': return 75;
            case 'j': return 10;
            case 'k': return 50;
            case 'e': return 20;
            case 'g': return 50;
            case 'b': return 50;
            default: return office ? 8 : 30;
        }
    }

    /// <summary>Build 3682's city as a population's table.</summary>
    private static PopulationState PopulationCity()
    {
        var s = new PopulationState();
        if (!File.Exists(PopulationFile))
        {
            return s;
        }
        string[] lines = File.ReadAllLines(PopulationFile);
        foreach (string line in lines)
        {
            if (!line.StartsWith("D|", StringComparison.Ordinal))
            {
                continue;
            }
            string[] f = line.Split('|');
            var d = new PopDistrict
            {
                Id = f[1], Working = int.Parse(f[2]), Middle = int.Parse(f[3]), Upper = int.Parse(f[4]),
                RealEstate = (int)Math.Round(double.Parse(f[5], CultureInfo.InvariantCulture) * 1000), DemandWeight = (int)Math.Round(double.Parse(f[6], CultureInfo.InvariantCulture) * 1000),
                X = (int.Parse(f[7]) + int.Parse(f[9])) / 2f, Z = (int.Parse(f[8]) + int.Parse(f[10])) / 2f, Villas = f[1] == "ba:neighborhood_thehamptons"
            };
            s.Districts.Add(d);
        }
        foreach (string line in lines)
        {
            if (!line.StartsWith("B|", StringComparison.Ordinal))
            {
                continue;
            }
            string[] f = line.Split('|');
            int district = s.DistrictOf(f[3]);
            string special = f[10] == "-" ? null : f[10];
            string size = f[5];
            var b = new CityBuilding
            {
                Street = f[1], Number = int.Parse(f[2]), District = district, Use = MeasureRules.UseOf(f[4], special), UnitSqm = int.Parse(f[7]), TotalSqm = int.Parse(f[8]),
                Traffic = int.Parse(f[9]), Special = special, Villa = f[4] == "ba:buildingtype_residential" && size.StartsWith("ba:buildingsize_t", StringComparison.Ordinal),
                X = s.Districts[district].X, Z = s.Districts[district].Z
            };
            s.Buildings.Add(b);
            if (b.Use == Use.Shop || b.Use == Use.Cinema || b.Use == Use.Theatre)
            {
                s.Districts[district].Pull += RoomOf(f[4], size, int.Parse(f[6])) * s.Districts[district].DemandWeight / 1000;
            }
        }
        CityTable.Sort(s.Buildings);
        s.IndexBuildings();
        return s;
    }

    // ---------------------------------------------------------------- the measure

    private static void PopulationMeasure()
    {
        PopulationState s = PopulationCity();
        Check(s.Buildings.Count == 885 && s.Districts.Count == 7, "population measure: Build 3682's 885 buildings in seven districts read (" + s.Buildings.Count + ", " + s.Districts.Count + ")");
        var city = new DistrictMeasure();
        DistrictMeasure[] rows = MeasureRules.Measure(s.Buildings, s.Districts.Count, city);
        if (Environment.GetEnvironmentVariable("RLO_POPULATION_REPORT") == "1")
        {
            for (int d = 0; d < rows.Length; d++)
            {
                DistrictMeasure r = rows[d];
                Console.WriteLine("  " + s.Districts[d].Id.Replace("ba:neighborhood_", string.Empty) + ": " + r.HomeBuildings + " home buildings, " + r.Flats + " flats, " + r.Jobs + " jobs (offices "
                                  + r.OfficeJobs + ", shops " + r.ShopJobs + ", warehouses " + r.WarehouseJobs + ", venues " + r.VenueJobs + ", special " + r.SpecialJobs + ", building staff " + r.BuildingJobs + ")");
            }
            Console.WriteLine("  city: " + city.Flats + " flats (" + city.Villas + " villas), " + city.Jobs + " jobs");
        }
        Check(city.Villas == 28 && rows[s.DistrictOf("ba:neighborhood_thehamptons")].Flats == 28, "population measure: the Hamptons' 28 villas are a household each (" + city.Villas + ")");
        Check(city.Flats >= 8200 && city.Flats <= 9100, "population measure: the base city has 8,658 flats by the rule (residential floor x 0.8 over the game's unit), in its band (" + city.Flats + ")");
        Check(city.Jobs >= 55000 && city.Jobs <= 110000, "population measure: the base city's jobs are in a Manhattan band (" + city.Jobs + ")");
        Check(city.OfficeJobs > city.ShopJobs * 4 && city.OfficeJobs >= 50000, "population measure: offices hold most jobs, a desk for every 16 m2 of their net floor (" + city.OfficeJobs + " against " + city.ShopJobs + " in shops)");
        int midtown = s.DistrictOf("ba:neighborhood_midtown"), industry = s.DistrictOf("ba:neighborhood_industrycity");
        Check(rows[midtown].Jobs > rows[industry].Jobs * 2 && rows[industry].WarehouseJobs > rows[midtown].WarehouseJobs, "population measure: Midtown is the office district, Industry City the warehouses' (" + rows[midtown].Jobs + ", " + rows[industry].WarehouseJobs + ")");
        var villa = new CityBuilding { Use = Use.Homes, Villa = true, UnitSqm = 8496, TotalSqm = 11000 };
        var tower = new CityBuilding { Use = Use.Homes, UnitSqm = 54, TotalSqm = 760 };
        var none = new CityBuilding { Use = Use.Homes, UnitSqm = 0, TotalSqm = 800 };
        var shop = new CityBuilding { Use = Use.Shop, UnitSqm = 225, TotalSqm = 1900, Business = "ba:businesstype_fastfoodrestaurant" };
        Check(MeasureRules.Flats(villa) == 1 && MeasureRules.Flats(tower) == 11 && MeasureRules.Flats(none) == 8 && MeasureRules.Flats(shop) == 0,
            "population measure: a villa one flat, 760 m2 of 54 m2 units eleven, a size with no unit 80 m2 flats, a shop none");
        Check(MeasureRules.Jobs(shop) == 18 && MeasureRules.ShopJobs(225, null) == 8 && MeasureRules.ShopJobs(10, null) == PopulationCatalogue.ShopJobsLeast,
            "population measure: a shop's jobs by its sales floor and kind (fast food 12 m2 a job), the average shop's where the kind is not known, two at least");
        Check(MeasureRules.UseOf("ba:buildingtype_retail", "ba:businesstype_bank") == Use.Special && MeasureRules.UseOf("ba:buildingtype_theater", null) == Use.Theatre
              && MeasureRules.UseOf("reallife_unknown", null) == Use.None, "population measure: a special service is special whatever its building type; a type nobody knows has no use");
    }
}
