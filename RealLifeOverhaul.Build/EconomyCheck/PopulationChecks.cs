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
        PopulationGenerate();
        PopulationDay();
        PopulationYears();
        PopulationCosts();
        PopulationSaveChecks();
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

    private static readonly string[] CheckShopTypes =
    {
        "ba:businesstype_supermarket", "ba:businesstype_fruitandvegetablestore", "ba:businesstype_coffeeshop", "ba:businesstype_fastfoodrestaurant",
        "ba:businesstype_hairdresser", "ba:businesstype_gym", "ba:businesstype_giftshop", "ba:businesstype_bookstore", "ba:businesstype_liquorstore",
        "ba:businesstype_nightclub", "ba:businesstype_jewelrystore", "ba:businesstype_clothingstore", "ba:businesstype_electronicsstore", "ba:businesstype_florist",
        "reallife_icecreamparlour", "reallife_teabar", "reallife_tobacconist"
    };

    private static readonly string[] CheckOfficeTypes =
    {
        "ba:businesstype_headquarters", "ba:businesstype_lawfirm", "ba:businesstype_travelagency", "ba:businesstype_graphicdesigner",
        "ba:businesstype_eventplanningagency", "ba:businesstype_webdevelopmentagency"
    };

    /// <summary>Build 3682's city as a population's table, and the city's AI businesses as the game places them (a shop in nine of ten shop buildings, a kind by its address).</summary>
    private static PopulationState PopulationCity(out List<BusinessSeed> seeds)
    {
        var s = new PopulationState();
        seeds = new List<BusinessSeed>();
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
            int pull = RoomOf(f[4], size, int.Parse(f[6]));
            if (b.Use == Use.Shop || b.Use == Use.Cinema || b.Use == Use.Theatre)
            {
                s.Districts[district].Pull += pull * s.Districts[district].DemandWeight / 1000;
            }
            uint h = PopDraw.Hash(17, 1, b.Number, b.Street.Length * 131 + b.Street[b.Street.Length - 1]);
            string type = null;
            switch (b.Use)
            {
                case Use.Shop:
                    type = h % 10 == 0 ? null : CheckShopTypes[h / 10 % (uint)CheckShopTypes.Length];
                    break;
                case Use.Office:
                    type = h % 3 == 0 ? null : CheckOfficeTypes[h / 3 % (uint)CheckOfficeTypes.Length];
                    break;
                case Use.Cinema:
                    type = "ba:businesstype_cinema";
                    break;
                case Use.Theatre:
                    type = "ba:businesstype_theater";
                    break;
                case Use.Special:
                    type = special;
                    break;
            }
            if (type != null)
            {
                seeds.Add(new BusinessSeed
                {
                    Street = b.Street, Number = b.Number, Type = type, Owner = Owner.City, Room = pull, Hours = 12, Price = 1000, Quality = 50, Content = 50, Staff = -1,
                    Promotion = 500 + 750 * b.Traffic / 100
                });
            }
        }
        CityTable.Sort(s.Buildings);
        s.IndexBuildings();
        return s;
    }

    // A city generated whole from a seed, its first day lived.
    private static PopulationSim PopulationMade(int seed, out PopulationState s, out List<BusinessSeed> seeds)
    {
        s = PopulationCity(out seeds);
        var sim = new PopulationSim();
        sim.BeginGenerate(s, seed, 1, 60, seeds);
        sim.RunAll();
        return sim;
    }

    // ---------------------------------------------------------------- the measure

    private static void PopulationMeasure()
    {
        PopulationState s = PopulationCity(out List<BusinessSeed> seeds);
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

    // ---------------------------------------------------------------- the people

    private static void PopulationGenerate()
    {
        var clock = Stopwatch.StartNew();
        PopulationSim sim = PopulationMade(20261101, out PopulationState s, out List<BusinessSeed> seeds);
        double ms = clock.Elapsed.TotalMilliseconds;
        int people = s.People.Live, households = s.Households.Live;
        int[] bands = new int[5];   // under 18, 18-24, 25-44, 45-64, 65 and over
        int alone = 0, workers = 0, unemployed = 0, labour = 0, degree = 0, adults25 = 0;
        var byClass = new int[3];
        for (int h = 0; h < s.Households.Count; h++)
        {
            if (!s.Households.Alive(h))
            {
                continue;
            }
            alone += s.Households.Size[h] == 1 ? 1 : 0;
            byClass[s.Households.Class[h]]++;
        }
        for (int p = 0; p < s.People.Count; p++)
        {
            if (s.People.Id[p] == 0)
            {
                continue;
            }
            int age = s.Age(p, s.Day);
            bands[age < 18 ? 0 : age < 25 ? 1 : age < 45 ? 2 : age < 65 ? 3 : 4]++;
            workers += s.People.Status[p] == (byte)Status.Worker ? 1 : 0;
            unemployed += s.People.Status[p] == (byte)Status.Unemployed ? 1 : 0;
            if (age >= 25)
            {
                adults25++;
                degree += s.People.Education[p] >= (byte)Education.Degree ? 1 : 0;
            }
        }
        labour = workers + unemployed;
        int jobs = 0;
        for (int e = 0; e < s.Employers.Count; e++)
        {
            jobs += s.Employers.Slots[e];
        }
        if (Environment.GetEnvironmentVariable("RLO_POPULATION_REPORT") == "1")
        {
            Console.WriteLine("  generated in " + ms.ToString("F0", CultureInfo.InvariantCulture) + " ms: " + households + " households, " + people + " people (" + (people * 100 / Math.Max(1, households)) / 100.0
                              + " a household, " + alone * 100 / Math.Max(1, households) + "% alone); ages " + string.Join("/", Array.ConvertAll(bands, b => (b * 100 / Math.Max(1, people)).ToString())) + " %; classes "
                              + string.Join("/", Array.ConvertAll(byClass, b => (b * 100 / Math.Max(1, households)).ToString())) + " %; " + workers + " in work, " + unemployed + " out ("
                              + unemployed * 1000 / Math.Max(1, labour) / 10.0 + "%), " + jobs + " jobs, " + s.Region.Commuting + " commuters; " + degree * 100 / Math.Max(1, adults25) + "% of adults 25+ with a degree; median "
                              + s.Median / 100 + " a day after tax");
        }
        Check(households >= 7800 && households <= 8800, "population people: the base city's households fill its flats less the 5% kept free (" + households + ")");
        Check(people >= 15000 && people <= 21000, "population people: the base city's people at about two a household (" + people + ")");
        Check(alone * 1000 / Math.Max(1, households) >= 380 && alone * 1000 / Math.Max(1, households) <= 560, "population people: nearly half of households live alone, as Manhattan's do (" + alone * 100 / Math.Max(1, households) + "%)");
        Check(bands[0] * 1000 / people >= 90 && bands[0] * 1000 / people <= 220 && bands[4] * 1000 / people >= 90 && bands[4] * 1000 / people <= 250,
            "population people: children and the over 65s in Manhattan's band (" + bands[0] * 100 / people + "% and " + bands[4] * 100 / people + "%)");
        Check(degree * 1000 / Math.Max(1, adults25) >= 350 && degree * 1000 / Math.Max(1, adults25) <= 700, "population people: most adults have a degree, as in Manhattan (" + degree * 100 / Math.Max(1, adults25) + "%)");
        Check(jobs > workers && s.Region.Commuting > workers / 2, "population people: more jobs than residents in work, the rest filled by commuters (" + jobs + " jobs, " + workers + " residents, " + s.Region.Commuting + " commuters)");
        Check(unemployed * 1000 / Math.Max(1, labour) >= 30 && unemployed * 1000 / Math.Max(1, labour) <= 90, "population people: unemployment at the city's start rate (" + unemployed * 1000 / Math.Max(1, labour) / 10.0 + "%)");

        PopulationMade(20261101, out PopulationState again, out _);
        Check(Fingerprint(s) == Fingerprint(again) && again.People.Live == people, "population people: the same seed gives the same city, to the byte");
        PopulationMade(7, out PopulationState other, out _);
        Check(Fingerprint(s) != Fingerprint(other), "population people: another seed gives another city");
    }

    // A hash of every household's and person's record.
    private static ulong Fingerprint(PopulationState s)
    {
        ulong h = 1469598103934665603UL;
        void Mix(long v)
        {
            h = (h ^ (ulong)v) * 1099511628211UL;
        }
        for (int i = 0; i < s.Households.Count; i++)
        {
            Mix(s.Households.Id[i]);
            Mix(s.Households.Building[i]);
            Mix(s.Households.Unit[i]);
            Mix(s.Households.Class[i]);
            Mix(s.Households.Savings[i]);
            Mix(s.Households.Rent[i]);
            Mix(s.Households.Tastes[i]);
        }
        for (int i = 0; i < s.People.Count; i++)
        {
            Mix(s.People.Id[i]);
            Mix(s.People.Birth[i]);
            Mix(s.People.Status[i]);
            Mix(s.People.Job[i]);
            Mix(s.People.Pay[i]);
            Mix(s.People.Education[i]);
        }
        return h;
    }

    // ---------------------------------------------------------------- a day, every cent placed

    private static void PopulationDay()
    {
        PopulationSim sim = PopulationMade(42, out PopulationState s, out _);
        PoolRules.DemandDay dd = sim.Demand;
        BusinessTable bt = s.Businesses;
        long takings = 0, fromResidents = 0;
        for (int b = 0; b < bt.Count; b++)
        {
            takings += bt.Takings[b];
            fromResidents += bt.ByLine[b * BusinessTable.Lines + PoolRules.Home] + bt.ByLine[b * BusinessTable.Lines + PoolRules.Work];
        }
        Check(dd.Day == 1 && takings > 0 && takings == dd.Takings, "population day: the first day's pools were shared out (" + takings / 100 + " taken)");
        Check(dd.ResidentSpend + dd.OutsiderSpend == dd.Takings + dd.Outside,
            "population day: what is spent into the pools is what businesses take plus what goes beyond the city, to the cent (" + (dd.ResidentSpend + dd.OutsiderSpend) + " against " + (dd.Takings + dd.Outside) + ")");
        var perBusiness = new long[bt.Count];
        var perHousehold = new long[s.Households.Count];
        PoolRules.AttributeAll(s, dd, perBusiness, perHousehold);
        bool exact = true;
        long attributed = 0;
        for (int b = 0; b < bt.Count; b++)
        {
            long residents = bt.ByLine[b * BusinessTable.Lines + PoolRules.Home] + bt.ByLine[b * BusinessTable.Lines + PoolRules.Work];
            exact &= perBusiness[b] == residents;
            attributed += perBusiness[b];
        }
        Check(exact && attributed == fromResidents, "population day: each household's spending at each business, summed over households, is that business's takings from residents, to the cent (" + attributed + " against " + fromResidents + ")");
        long one = 0, many = 0;
        bool alike = true;
        for (int h = 0; h < s.Households.Count; h += 97)
        {
            if (!s.Households.Alive(h))
            {
                continue;
            }
            var mine = new long[bt.Count];
            PoolRules.Attribute(s, dd, h, mine);
            long sum = 0;
            for (int b = 0; b < mine.Length; b++)
            {
                sum += mine[b];
            }
            alike &= sum == perHousehold[h];
            one += sum;
            many++;
        }
        Check(alike && many > 50, "population day: one household's part worked out alone is its part in the whole (" + many + " households, " + one / 100 + " spent)");
        long ledger = 0, books = 0;
        bool balanced = true;
        for (int h = 0; h < s.Households.Count; h++)
        {
            if (!s.Households.Alive(h))
            {
                continue;
            }
            int row = h * PopLedger.Lines;
            int[] m = s.Households.Month;
            long income = (long)m[row + PopLedger.Wages] + m[row + PopLedger.Benefits] + m[row + PopLedger.Pension] + m[row + PopLedger.Returns];
            long outgo = (long)m[row + PopLedger.IncomeTax] + m[row + PopLedger.Rent] + m[row + PopLedger.Saving];
            for (int k = 0; k < PopLedger.SpendKinds; k++)
            {
                outgo += m[row + PopLedger.SpendFirst + k];
            }
            balanced &= income == outgo;
            ledger += m[row + PopLedger.IncomeTax];
        }
        books = s.Books.IncomeTax;
        Check(balanced, "population day: every household's month balances: what came in is its tax, rent, spending and saving, to the cent");
        Check(ledger == books && s.Books.SalesTax > 0 && s.Books.PropertyTax > 0, "population day: the council's income tax is its households' (" + books / 100 + "), and sales and property tax are booked");
        int anchored = 0, live = 0;
        for (int b = 0; b < bt.Count; b++)
        {
            live += bt.Seen[b] ? 1 : 0;
            anchored += bt.AnchorDay[b] == 1 && bt.Anchor[b] == bt.Takings[b] ? 1 : 0;
        }
        Check(anchored > live * 9 / 10, "population day: on the first day every business with takings is anchored at them, so the game's own count stands (" + anchored + " of " + live + ")");
    }
}
