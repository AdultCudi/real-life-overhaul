using System;
using System.Collections.Generic;

// The Property app's one sample world: the buildings you hold with their units, tenants, calls, services and applicants, the hotels you run, the car hire fleet, the agency, and the
// city's market for sale and its rents. Every page reads this world; every head, count and share is worked out from it by EstateFigures or by the plain sums here, so a figure on one tab
// can only differ from another's if the world says two things. Day 412 is "today" for every tab. Rents are kept a day, as the game keeps them, and a month is thirty days of them.
// Names and prices are placeholders and never ship.
internal static partial class UiSamples
{
    public const int MonthDays = 30, DaysPerYear = 360;

    internal sealed class EPerson
    {
        public string Name, Job;
        public int Age;
        public EPerson(string name, string job, int age)
        {
            Name = name;
            Job = job;
            Age = age;
        }
    }

    internal sealed class EPay
    {
        public int Due, Paid, State;   // State: 0 on time, 1 late, 2 missed
    }

    internal sealed class EApplicant
    {
        public int Kind, Offer, Record, Lapse, Term;
    }

    internal sealed class EService
    {
        public int Kind, Day, Fee;
        public string Who;
    }

    internal sealed class EUnit
    {
        public EPerson Tenant;
        public int RentDay, Until, ArrearsDays, Happy, Record, Since, VoidDays, MarketDay, Repairs, ValueForRent;
        public int Ask = 1050;   // thousandths of the market's rent
        public bool Notice, ToLet = true, CallBooked;
        public int Call, CallDay, CallCost, CallDue, Improve, ImproveCost, Refused;
        public readonly List<EApplicant> Applicants = new List<EApplicant>();
        public readonly List<EPay> Pays = new List<EPay>();
        public bool Let => Tenant != null;
        public int Arrears => Let ? RentDay * ArrearsDays : 0;   // the rent of the days it is behind: "owes {amount}, {days} days of rent" holds by this
        public int AskDay => (MarketDay * Ask + 500) / 1000;
    }

    internal sealed class EBuilding
    {
        public string Place, District;
        public char Use;   // h homes, s shops, o offices
        public int Level, Cond, Value, Paid, Spent, RentYear, CostYear, InspectIn, GameNight;
        public bool InspectPassed = true, Managed = true, Self, Cover = true;
        public readonly List<EUnit> Units = new List<EUnit>();
        public readonly List<EService> Services = new List<EService>();
    }

    internal sealed class EHotel
    {
        public string Name, District;
        public int Stars, Clean, Staff, TakingsYear, CostsYear, Costs, PushDays;
        public int[] Rooms = new int[2], Sold = new int[2], Going = new int[2], Yours = new int[2], Reviews = new int[0];
        public int RoomsAll => Rooms[0] + Rooms[1];
        public int SoldAll => Sold[0] + Sold[1];
        public int Takings => Sold[0] * Yours[0] + Sold[1] * Yours[1];
    }

    internal sealed class ECar
    {
        public string Name, Class;
        public int Rate, Day, Hires, Takings, Cover;
        public bool Out;
    }

    internal sealed class EAdd
    {
        public string Name, Class;
        public int Rate;
    }

    internal sealed class EInstruction
    {
        public bool Sale;
        public string District;
        public int Value, Stage, Day, Hours, Sqm, Ask, Fee, Chance;   // Stage: 0 offered, 1 being marketed, 2 completing
        public int Commission => Sale ? Value / 50 : Value / 2;
    }

    internal sealed class EListing
    {
        public string Place, District;
        public char Use;
        public long Price;
        public int Cond, RentDay, Value;
        public int Units = 1;
    }

    internal sealed class EDistrict
    {
        public string Name;
        public int PerSqm, Rep, Chance;   // PerSqm in cents
    }

    internal sealed class ELandlord
    {
        public string Name;
        public int Units, MeanMonth, ChangeTenths;
    }

    internal sealed class EstateWorldData
    {
        public string Variant;
        public readonly List<EBuilding> Buildings = new List<EBuilding>();
        public readonly List<EHotel> Hotels = new List<EHotel>();
        public readonly List<EListing> Market = new List<EListing>();
        public readonly List<EListing> HotelsForSale = new List<EListing>();
        public readonly List<ECar> Fleet = new List<ECar>();
        public readonly List<EAdd> Adds = new List<EAdd>();
        public readonly List<EInstruction> Instructions = new List<EInstruction>();
        public readonly List<EDistrict> Districts = new List<EDistrict>();
        public readonly List<ELandlord> Landlords = new List<ELandlord>();
        public readonly int[] RentRing = { 2690, 2730, 2775, 2800, 2826, 2860 };   // the city's mean flat rent a month, oldest first: the last is this month's
        public string HireLot = "14 Warehouse Row", Office = "7 5th Avenue";
        public int HireSpaces = 16, HireRating = 7, HireCostsYear = 3480, AgencyDeals = 14, AgencyLost = 3, AgencyEarned = 212400, Negotiators = 2, HoursTenths = 115;
        public int MarketLevel = 1030, PricesSince = 1120;
        public bool Agency = true;
        public int CityFlatRent => RentRing[RentRing.Length - 1];
        public int RentMonthChange => (CityFlatRent - RentRing[RentRing.Length - 2]) * 1000 / RentRing[RentRing.Length - 2];   // thousandths
    }

    private static readonly Dictionary<string, EstateWorldData> EstateWorlds = new Dictionary<string, EstateWorldData>();

    private static EPerson P(string name, string job, int age) => new EPerson(name, job, age);

    private static EUnit LetUnit(EPerson who, int rentDay, int market, int until, int happy, int record, int since, int arrearsDays = 0)
    {
        var u = new EUnit { Tenant = who, RentDay = rentDay, MarketDay = market, Until = until, Happy = happy, Record = record, Since = since, ArrearsDays = arrearsDays, Repairs = 70 + happy / 40, ValueForRent = 40 + (market - rentDay) * 100 / Math.Max(1, market) * 4 + 20 };
        u.ValueForRent = Math.Max(10, Math.Min(95, u.ValueForRent));
        // Six months of the rent record (what the game keeps), the newest first, each paid on its day except where the unit has been late.
        int[] due = { 376, 346, 316, 286, 256, 226 };
        for (int i = 0; i < due.Length; i++)
        {
            u.Pays.Add(new EPay { Due = due[i], Paid = due[i] + (record < 60 && i == 1 ? 4 : 0), State = record < 60 && i == 1 ? 1 : 0 });
        }
        return u;
    }

    private static EBuilding Building(string place, string district, char use, int level, int cond, long value, long paid, int spent, int rentYear, int costYear, int inspectIn, params EUnit[] units)
    {
        var b = new EBuilding { Place = place, District = district, Use = use, Level = level, Cond = cond, Value = (int)value, Paid = (int)paid, Spent = spent, RentYear = rentYear, CostYear = costYear, InspectIn = inspectIn };
        b.Units.AddRange(units);
        return b;
    }

    private static EListing Listing(string place, string district, char use, long price, int cond, int rentDay, int value) =>
        new EListing { Place = place, District = district, Use = use, Price = price, Cond = cond, RentDay = rentDay, Value = value };

    /// <summary>The sample world of the Property app for a variant: "std" (twelve units in nine buildings and the rest), "twenty" (twenty units), "first" (one unit), "empty" (nothing held).</summary>
    private static EstateWorldData EstateWorld(string variant)
    {
        if (EstateWorlds.TryGetValue(variant, out EstateWorldData have))
        {
            return have;
        }
        var w = new EstateWorldData { Variant = variant };
        // The city, the same in every variant: what the districts rent for, the other landlords, what is for sale.
        w.Districts.AddRange(new[]
        {
            new EDistrict { Name = "Midtown", PerSqm = 3850, Rep = 62, Chance = 140 }, new EDistrict { Name = "Garment District", PerSqm = 3480, Rep = 55, Chance = 120 },
            new EDistrict { Name = "Murray Hill", PerSqm = 3120, Rep = 31, Chance = 60 }, new EDistrict { Name = "Hell's Kitchen", PerSqm = 2790, Rep = 40, Chance = 80 },
            new EDistrict { Name = "Uptown", PerSqm = 1940, Rep = 48, Chance = 100 }
        });
        w.Landlords.AddRange(new[]
        {
            new ELandlord { Name = "Gustavo Ruiz", Units = 14, MeanMonth = 2930, ChangeTenths = 21 }, new ELandlord { Name = "Halden Estates", Units = 9, MeanMonth = 2640, ChangeTenths = 4 },
            new ELandlord { Name = "Okafor and Sons", Units = 6, MeanMonth = 2410, ChangeTenths = -8 }
        });
        w.Market.AddRange(new[]
        {
            Listing("301 W 54th Street", "Hell's Kitchen", 'h', 1840000, 80, 210, 1720000), Listing("14 Pier Street", "Garment District", 's', 3260000, 64, 425, 3100000),
            Listing("99 Broadway", "Midtown", 'o', 12400000, 91, 1720, 11800000), Listing("6 Harlem Row", "Uptown", 'h', 980000, 42, 85, 940000),
            Listing("88 Fulton Street", "Midtown", 's', 4700000, 58, 540, 4400000), Listing("250 Park Avenue", "Midtown", 'o', 9300000, 86, 1230, 8900000),
            Listing("17 Mercer Street", "Murray Hill", 'h', 1420000, 67, 160, 1380000), Listing("3 Canal Row", "Garment District", 's', 2880000, 49, 330, 2750000),
            Listing("60 W 57th Street", "Midtown", 'o', 15800000, 93, 2010, 15100000), Listing("9 W 43rd Street", "Hell's Kitchen", 'h', 1310000, 52, 125, 1260000)
        });
        w.HotelsForSale.Add(new EListing { Place = "Marlowe Grand Hotel", District = "Midtown", Use = 'g', Price = 8900000 });
        w.HotelsForSale.Add(new EListing { Place = "Riverside Hotel", District = "Uptown", Use = 'g', Price = 5200000 });
        if (variant == "empty")
        {
            EstateWorlds[variant] = w;
            return w;
        }
        EPerson priya = P("Priya Nair", "Nurse, Murray Hill Clinic", 34), daniel = P("Daniel Ortiz", "Cook, Pier Street Diner", 41), sofia = P("Sofia Marchetti", "Nurse, Murray Hill Clinic", 34),
            lena = P("Lena Hoffmann", "A tailor's shop", 47), ines = P("Ines Duarte", "A florist", 38), amara = P("Amara Kimani", "A bakery", 36), theo = P("Theo Brandt", "Courier, Eastside Parcels", 29),
            imani = P("Imani Cole", "Teacher, Lincoln School", 33), owen = P("Owen Park", "Designer, Park and Lane", 39), yuki = P("Yuki Tanaka", "An import firm", 44), chen = P("Chen Wei", "Chef, Harbour Kitchen", 42);
        if (variant == "first")
        {
            EBuilding only = Building("88 W 46th Street", "Hell's Kitchen", 'h', 1, 76, 1980000, 1700000, 40000, 25000, 4000, 22, LetUnit(theo, 73, 80, 533, 850, 90, 360));
            only.Services.Add(new EService { Kind = 1, Day = 431, Fee = 320, Who = "Okonkwo Heating" });
            w.Buildings.Add(only);
            EstateWorlds[variant] = w;
            return w;
        }
        EUnit sofiaUnit = LetUnit(sofia, 87, 90, 462, 510, 55, 58, 6);
        sofiaUnit.Call = 1;
        sofiaUnit.CallDay = 407;
        sofiaUnit.CallCost = 340;
        EUnit danielUnit = LetUnit(daniel, 110, 112, 551, 740, 80, 140);
        danielUnit.Improve = 1;
        danielUnit.ImproveCost = 2900;
        danielUnit.Refused = 1;
        EBuilding first = Building("41 E 37th Street", "Murray Hill", 'h', 1, 80, 4860000, 3900000, 120000, 98000, 14000, 9, LetUnit(priya, 87, 90, 490, 820, 85, 120), danielUnit, sofiaUnit);
        first.Services.Add(new EService { Kind = 5, Day = 421, Fee = 260, Who = "Garrick Damp Proofing" });
        EBuilding shops = Building("12 W 39th Street", "Garment District", 's', 1, 64, 3100000, 2500000, 60000, 112000, 9000, 31, LetUnit(lena, 180, 186, 620, 680, 80, 200), LetUnit(ines, 150, 156, 560, 810, 85, 90));
        shops.Services.Add(new EService { Kind = 4, Day = 424, Fee = 310, Who = "Hartley Locks" });
        EUnit amaraUnit = LetUnit(amara, 207, 210, 480, 390, 45, 80, 14);
        amaraUnit.Call = 4;
        amaraUnit.CallDay = 406;
        amaraUnit.CallCost = 410;
        amaraUnit.CallBooked = true;
        amaraUnit.CallDue = 418;
        EBuilding bakery = Building("233 W 39th Street", "Garment District", 's', 0, 58, 2600000, 2100000, 0, 60000, 7000, 18, amaraUnit);
        EUnit theoUnit = LetUnit(theo, 73, 80, 533, 850, 90, 360);
        theoUnit.Call = 4;
        theoUnit.CallDay = 408;
        theoUnit.CallCost = 280;
        theoUnit.CallBooked = true;
        theoUnit.CallDue = 415;
        EBuilding kitchen = Building("88 W 46th Street", "Hell's Kitchen", 'h', 1, 76, 1980000, 1700000, 40000, 25000, 4000, 22, theoUnit);
        EUnit imaniUnit = LetUnit(imani, 103, 108, 650, 900, 92, 300);
        imaniUnit.Call = 1;
        imaniUnit.CallDay = 409;
        imaniUnit.CallCost = 520;
        EBuilding harlem = Building("75 W 125th Street", "Uptown", 'h', 0, 58, 2210000, 1800000, 0, 30000, 5000, 6, imaniUnit);
        harlem.Services.Add(new EService { Kind = 6, Day = 431, Fee = 220, Who = "Fenwick Fittings" });
        var dock = new EUnit { MarketDay = 130, Ask = 1050, VoidDays = 12, Repairs = 70, ValueForRent = 58 };
        int[] dockDue = { 376, 346, 316, 286, 256, 226 };
        for (int i = 0; i < dockDue.Length; i++)
        {
            dock.Pays.Add(new EPay { Due = dockDue[i], Paid = dockDue[i] + (i == 3 ? 4 : 0), State = i == 3 ? 1 : 0 });
        }
        dock.Applicants.Add(new EApplicant { Kind = 2, Offer = 133, Record = 80, Lapse = 421, Term = 150 });
        dock.Applicants.Add(new EApplicant { Kind = 5, Offer = 124, Record = 52, Lapse = 424, Term = 120 });
        EBuilding dockSide = Building("1 Dock Street", "Uptown", 's', 1, 70, 1250000, 1000000, 22000, 36900, 1100, 9, dock);
        dockSide.Services.Add(new EService { Kind = 3, Day = 428, Fee = 290, Who = "Brightline Electrics" });
        EBuilding offices = Building("17 E 41st Street", "Midtown", 'o', 2, 71, 6100000, 5200000, 90000, 94000, 12000, 14, LetUnit(owen, 260, 266, 572, 710, 80, 220));
        EUnit yukiUnit = LetUnit(yuki, 187, 190, 430, 330, 60, 150);
        yukiUnit.Notice = true;
        EBuilding unfit = Building("520 8th Avenue", "Midtown", 'o', 0, 32, 3900000, 3300000, 0, 40000, 21000, 14, yukiUnit);
        unfit.InspectPassed = false;
        EBuilding chenFlat = Building("412 10th Avenue", "Hell's Kitchen", 'h', 1, 73, 2250000, 1900000, 30000, 48000, 6000, 27, LetUnit(chen, 160, 164, 590, 630, 75, 190));
        w.Buildings.AddRange(new[] { first, shops, bakery, kitchen, harlem, dockSide, offices, unfit, chenFlat });
        // Two offices the game lets for you: the agency works from the first.
        w.Buildings.Add(new EBuilding { Place = "7 5th Avenue", District = "Midtown", Use = 'o', Level = 1, Cond = 74, Value = 5200000, Paid = 4400000, Managed = false, Self = true, GameNight = 215, InspectIn = 0 });
        w.Buildings.Add(new EBuilding { Place = "22 Maiden Lane", District = "Midtown", Use = 'o', Level = 1, Cond = 66, Value = 3700000, Paid = 3100000, Managed = false, GameNight = 160, InspectIn = 0 });
        if (variant == "twenty")
        {
            EPerson ana = P("Ana Souza", "Dentist, Park Smiles", 37), ben = P("Ben Carter", "Teacher, Lincoln School", 40), tom = P("Tom Okafor", "Nurse, Murray Hill Clinic", 31), ravi = P("Ravi Shah", "A pharmacy", 45),
                mia = P("Mia Lang", "Architect, Lang Studio", 36), luis = P("Luis Pena", "Software, Pena Labs", 33), zoe = P("Zoe Adler", "Teacher, Lincoln School", 35), kofi = P("Kofi Mensah", "A grocer", 50);
            w.Buildings.Add(Building("60 W 58th Street", "Midtown", 'h', 1, 78, 3600000, 3000000, 20000, 90000, 7000, 12, LetUnit(ana, 127, 130, 300, 790, 85, 100), LetUnit(ben, 127, 130, 341, 720, 80, 130)));
            w.Buildings.Add(Building("3 Bridge Row", "Garment District", 's', 1, 66, 2300000, 1900000, 10000, 52000, 3000, 20, LetUnit(kofi, 147, 150, 410, 810, 85, 160)));
            w.Buildings.Add(Building("17 Mercer Place", "Murray Hill", 'h', 1, 70, 1500000, 1250000, 15000, 34000, 4000, 25, LetUnit(tom, 97, 100, 365, 660, 70, 200)));
            w.Buildings.Add(Building("88 Fulton Place", "Midtown", 's', 1, 72, 3900000, 3300000, 0, 62000, 5000, 16, LetUnit(ravi, 173, 175, 490, 840, 88, 210)));
            w.Buildings.Add(Building("250 Park Lane", "Midtown", 'o', 2, 80, 7400000, 6300000, 50000, 190000, 11000, 11, LetUnit(mia, 270, 275, 455, 750, 80, 250), LetUnit(luis, 270, 275, 430, 880, 90, 260)));
            w.Buildings.Add(Building("6 Harlem Court", "Uptown", 'h', 1, 64, 1100000, 900000, 0, 30000, 3000, 30, LetUnit(zoe, 90, 95, 380, 700, 75, 90)));
        }
        // The hotels you run, the cars you hire out, the agency's instructions: they stand beside the buildings in every variant that has any.
        w.Hotels.Add(new EHotel { Name = "Pier House Hotel", District = "Midtown", Stars = 8, Clean = 82, Staff = 70, Costs = 3300, TakingsYear = 44000, CostsYear = 36100, PushDays = 3, Rooms = new[] { 28, 12 }, Sold = new[] { 24, 10 },
            Going = new[] { 120, 100 }, Yours = new[] { 128, 96 }, Reviews = new[] { 1, 3 } });
        w.Hotels.Add(new EHotel { Name = "Canal Street Inn", District = "Garment District", Stars = 6, Clean = 64, Staff = 50, Costs = 1400, TakingsYear = 19600, CostsYear = 17500, Rooms = new[] { 20, 10 }, Sold = new[] { 12, 6 },
            Going = new[] { 110, 70 }, Yours = new[] { 104, 74 }, Reviews = new[] { 2, 4 } });
        w.Hotels.Add(new EHotel { Name = "Hartley Suites", District = "Uptown", Stars = 10, Clean = 91, Staff = 85, Costs = 5300, TakingsYear = 71400, CostsYear = 52900, Rooms = new[] { 30, 20 }, Sold = new[] { 24, 12 },
            Going = new[] { 200, 150 }, Yours = new[] { 210, 160 }, Reviews = new[] { 5, 1 } });
        w.Hotels.Add(new EHotel { Name = "Dockside Rooms", District = "Hell's Kitchen", Stars = 5, Clean = 58, Staff = 40, Costs = 1350, TakingsYear = 22000, CostsYear = 19500, Rooms = new[] { 26, 14 }, Sold = new[] { 14, 6 },
            Going = new[] { 92, 64 }, Yours = new[] { 88, 66 }, Reviews = new[] { 6, 3 } });
        string[] names = { "Corvin roadster", "Lark hatchback", "Hollis sedan", "Bram pickup", "Ivel sedan", "Rowan saloon", "Sable SUV", "Ivel estate" };
        string[] classes = { "premium", "economy", "standard", "standard", "standard", "standard", "standard", "standard" };
        int[] rates = { 210, 74, 88, 102, 84, 92, 118, 79 }, hires = { 14, 31, 22, 9, 18, 11, 12, 8 }, back = { 416, 0, 414, 0, 415, 0, 417, 0 };
        for (int i = 0; i < names.Length; i++)
        {
            w.Fleet.Add(new ECar { Name = names[i], Class = classes[i], Rate = rates[i], Out = back[i] > 0, Day = back[i], Hires = hires[i], Takings = hires[i] * rates[i] * 3 / 2 + i * 10, Cover = 4 + rates[i] / 20 });
        }
        string[] adds = { "Fenn van", "Tern camper van", "Cobb pickup", "Stout box truck", "Dale hatchback", "Moss sedan", "Pike pickup", "Reed SUV" };
        int[] addRates = { 96, 110, 102, 140, 68, 84, 98, 116 };
        for (int i = 0; i < adds.Length; i++)
        {
            w.Adds.Add(new EAdd { Name = adds[i], Rate = addRates[i], Class = i == 4 ? "economy" : "standard" });
        }
        w.Instructions.Add(new EInstruction { Sale = true, District = "Midtown", Value = 2400000, Stage = 0, Day = 419, Hours = 35, Sqm = 180, Ask = 1040, Fee = 4800, Chance = 420 });
        w.Instructions.Add(new EInstruction { Sale = false, District = "Uptown", Value = 3100, Stage = 1, Day = 422, Hours = 12 });
        w.Instructions.Add(new EInstruction { Sale = true, District = "Garment District", Value = 1150000, Stage = 1, Day = 426, Hours = 24 });
        w.Instructions.Add(new EInstruction { Sale = false, District = "Midtown", Value = 4800, Stage = 0, Day = 420, Hours = 16 });
        w.Instructions.Add(new EInstruction { Sale = true, District = "Uptown", Value = 1980000, Stage = 2, Day = 413, Hours = 21 });
        w.Instructions.Add(new EInstruction { Sale = false, District = "Hell's Kitchen", Value = 2900, Stage = 2, Day = 415, Hours = 10 });
        w.Instructions.Add(new EInstruction { Sale = true, District = "Murray Hill", Value = 1620000, Stage = 1, Day = 428, Hours = 26 });
        w.Instructions.Add(new EInstruction { Sale = false, District = "Garment District", Value = 5200, Stage = 1, Day = 424, Hours = 14 });
        EstateWorlds[variant] = w;
        return w;
    }

    // ---------------------------------------------------------------------------------------- what the pages ask of the world

    private static IEnumerable<EUnit> ManagedUnits(EstateWorldData w)
    {
        foreach (EBuilding b in w.Buildings)
        {
            if (!b.Managed)
            {
                continue;
            }
            foreach (EUnit u in b.Units)
            {
                yield return u;
            }
        }
    }

    private static List<RealLifeOverhaul.Ui.RollLine> RollLines(EstateWorldData w)
    {
        var lines = new List<RealLifeOverhaul.Ui.RollLine>();
        foreach (EUnit u in ManagedUnits(w))
        {
            lines.Add(new RealLifeOverhaul.Ui.RollLine { Let = u.Let, RentDay = u.RentDay, Arrears = u.Arrears, AskDay = u.Let ? 0 : u.AskDay });
        }
        return lines;
    }

    private static int WaitingOf(EstateWorldData w)
    {
        int waiting = 0;
        foreach (EBuilding b in w.Buildings)
        {
            foreach (EUnit u in b.Units)
            {
                waiting += u.Applicants.Count + (u.Improve > 0 ? 1 : 0) + (u.Let && u.Call > 0 && !u.CallBooked ? 1 : 0);
            }
        }
        foreach (EInstruction n in w.Instructions)
        {
            waiting += n.Stage == 0 ? 1 : 0;
        }
        return waiting;
    }
}
