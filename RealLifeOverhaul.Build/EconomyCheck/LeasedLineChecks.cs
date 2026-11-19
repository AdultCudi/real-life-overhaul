using System;
using System.Collections.Generic;
using RealLifeOverhaul;
using RealLifeOverhaul.Tech;
using RealLifeOverhaul.Utilities;

// The data centres' leased lines (Utilities/LeasedLineCatalogue.cs, LeasedLineRules.cs, UtilitiesDirector.Internet.cs, Wiring/TechLinks.cs): the plan table is monotone and a rack still
// pays at every step at the default prices (the sums are printed), who may have a line, the fees and waits by what the district has, the line's life as an add-on to the plan, the
// limits read from it, its save line, and that both interfaces and the refusals name it. Each source check is also run on a made-up wrong case, which must fail.
internal static partial class Program
{
    private static void LeasedLines()
    {
        LeasedTable();
        LeasedRackProfit();
        LeasedDistricts();
        LeasedLife();
        LeasedLimits();
        LeasedSaves();
        LeasedSources();
    }

    private static void LeasedTable()
    {
        int n = LeasedLineCatalogue.Steps;
        Check(LeasedLineCatalogue.Mbps.Length == n && LeasedLineCatalogue.MonthCents.Length == n && LeasedLineCatalogue.InstallDays.Length == n, "leased: every table has one row for each of the " + n + " steps");
        bool up = true, cheaper = true, longer = true;
        for (int i = 1; i < n; i++)
        {
            up &= LeasedLineCatalogue.Mbps[i] > LeasedLineCatalogue.Mbps[i - 1] && LeasedLineCatalogue.MonthCents[i] > LeasedLineCatalogue.MonthCents[i - 1];
            cheaper &= LeasedLineCatalogue.MonthCents[i] * (long)LeasedLineCatalogue.Mbps[i - 1] < LeasedLineCatalogue.MonthCents[i - 1] * (long)LeasedLineCatalogue.Mbps[i];   // a Gbps costs less in a bigger line
            longer &= LeasedLineCatalogue.InstallDays[i] > LeasedLineCatalogue.InstallDays[i - 1];
        }
        Check(up, "leased: a bigger step is faster and costs more a month");
        Check(cheaper, "leased: a Gbps costs less in each bigger step (the carriers' volume price)");
        Check(longer, "leased: a bigger step takes longer to install");
        Check(LeasedLineCatalogue.Mbps[0] == 5000 && LeasedLineCatalogue.Mbps[1] == 10000 && LeasedLineCatalogue.Mbps[2] == 40000 && LeasedLineCatalogue.Mbps[n - 1] == 100000, "leased: the steps are 5, 10, 40 and 100 Gbps");
        Check(LeasedLineCatalogue.RackMbps == InfraLimits.MbpsPerRack && LeasedLineCatalogue.GpuRackMbps == InfraLimits.MbpsPerGpuRack, "leased: a rack's and an AI rack's Mbps are the data centre's own");
        Check(LeasedLineCatalogue.Mbps[0] / LeasedLineCatalogue.RackMbps == 50 && LeasedLineCatalogue.Mbps[n - 1] / LeasedLineCatalogue.RackMbps == 1000, "leased: the steps carry 50 to 1,000 standard racks");
        int biggest = 0;
        for (int p = 0; p < InternetCatalogue.Providers.Length; p++)
        {
            foreach (int mbps in InternetCatalogue.Providers[p].Mbps)
            {
                biggest = Math.Max(biggest, mbps);
            }
        }
        Check(LeasedLineCatalogue.Mbps[0] > biggest, "leased: the smallest line (" + LeasedLineCatalogue.Mbps[0] + " Mbps) is above the largest ordinary plan (" + biggest + ")");
        Check(InternetCatalogue.Providers[InternetCatalogue.FibreIndex].Id == "brightvein" && InternetCatalogue.Providers[InternetCatalogue.CableIndex].Id == "kestrel",
            "leased: the fibre and the cable the districts are told apart by are Brightvein's and Kestrel's");
        Check(InternetRules.Count == InternetCatalogue.Providers.Length && InternetRules.Get(InternetCatalogue.LeasedIndex) == InternetCatalogue.Leased
            && InternetCatalogue.LeasedIndex >= InternetCatalogue.Providers.Length + 1, "leased: the carriers stand outside the providers' count, so no outage or promotion is drawn for them");
        for (int i = 0; i < n; i++)
        {
            Check(InternetRules.MonthCents(InternetCatalogue.LeasedIndex, i, true, true, PriceRules.Start, 800) == LeasedLineCatalogue.MonthCents[i]
                  && InternetRules.MonthCents(InternetCatalogue.LeasedIndex, i, false, false, PriceRules.Start, 1100) == LeasedLineCatalogue.MonthCents[i],
                "leased: step " + i + " is priced as sold, whatever the contract, the home price or the provider's month");
        }
        int level = PriceRules.Start * 3 / 2;
        Check(LeasedLineRules.MonthCents(0, level) == LeasedLineCatalogue.MonthCents[0] * 3 / 2, "leased: the price follows the game's price level, as every plan does");
    }

    // The sums behind the prices: what a rack takes in a month in the cloud, less what it costs, against the line's share of it. Printed, and held.
    private static void LeasedRackProfit()
    {
        int month = FirmsCatalogue.MonthDays;
        bool kept = true;
        foreach (int share in new[] { 0, EndScaleRules.Full })
        {
            foreach (bool gpu in new[] { false, true })
            {
                var d = new DataCentre { Racks = gpu ? 0 : 1, GpuRacks = gpu ? 1 : 0, Dense = share > 0 && !gpu ? 1 : 0, DenseGpu = share > 0 && gpu ? 1 : 0, Tier = 0, Sqm = 0 };
                d.Book = InfraRules.CostBasis(d);
                long units = gpu ? InfraRules.GpuUnits(d, share) : InfraRules.Units(d, share);
                long takings = units * (gpu ? InfraCatalogue.GpuUnitDay : InfraCatalogue.UnitDay);                                   // a day at the rivals' list price
                long power = InfraRules.Power(d, share) * UtilitiesCatalogue.UnitCents[0] / 1000;                                     // a day's power at the city's tariff
                long write = InfraRules.WriteDown(d), upkeep = InfraRules.Upkeep(d, share);
                long profit = takings - power - write - upkeep;
                int slots = (gpu ? LeasedLineCatalogue.GpuRackMbps : LeasedLineCatalogue.RackMbps) / LeasedLineCatalogue.RackMbps;   // the bandwidth it uses, in standard racks
                for (int step = 0; step < LeasedLineCatalogue.Steps; step++)
                {
                    long perRack = (long)LeasedLineCatalogue.MonthCents[step] * slots / (LeasedLineCatalogue.Mbps[step] / LeasedLineCatalogue.RackMbps) / month;   // a day's line cost of one rack
                    long after = profit - perRack;
                    Console.WriteLine("leased: " + (gpu ? "AI" : "standard") + " rack at " + (share > 0 ? "scale" : "start") + " prices, " + LeasedLineCatalogue.Mbps[step] / 1000 + " Gbps: takes "
                                      + takings * month / 100 + " a month, costs " + (power + write + upkeep) * month / 100 + ", line " + perRack * month / 100 + ", keeps " + after * month / 100
                                      + " (" + (profit > 0 ? after * 100 / profit : 0) + "% of its profit)");
                    kept &= profit > 0 && after > 0 && perRack * 100 <= takings * 5 && after * 100 >= profit * 90;
                }
            }
        }
        Check(kept, "leased: at every step a rack, of either kind and at either price, still pays, the line costing at most 5% of its takings and a tenth of its profit");
    }

    private static void LeasedDistricts()
    {
        string[] fibre = { "ba:neighborhood_lowermanhattan", "ba:neighborhood_midtown", "ba:neighborhood_murrayhill" };
        string[] cable = { "ba:neighborhood_garmentdistrict", "ba:neighborhood_hellskitchen", "ba:neighborhood_industrycity" };
        string[] dig = { "ba:neighborhood_thehamptons", "reallife:neighborhood_uptown", "reallife:neighborhood_cedarheights", "reallife:neighborhood_uppereastside", "ba:neighborhood_madeupbyafreeupdate", string.Empty };
        bool classes = true, reaches = true;
        foreach (string d in fibre)
        {
            classes &= LeasedLineRules.Class(d) == LeasedLineCatalogue.Fibre;
        }
        foreach (string d in cable)
        {
            classes &= LeasedLineRules.Class(d) == LeasedLineCatalogue.Cable;
        }
        foreach (string d in dig)
        {
            classes &= LeasedLineRules.Class(d) == LeasedLineCatalogue.Dig;
            reaches &= string.IsNullOrEmpty(d) || InternetRules.Reaches(InternetCatalogue.LeasedIndex, d);
        }
        foreach (string d in fibre)
        {
            reaches &= InternetRules.Reaches(InternetCatalogue.LeasedIndex, d);
        }
        foreach (string d in cable)
        {
            reaches &= InternetRules.Reaches(InternetCatalogue.LeasedIndex, d);
        }
        Check(classes, "leased: Brightvein's three districts have fibre, Kestrel's three only cable, and the Hamptons, Uptown, Cedar Heights, Upper East Side and any new district nothing");
        Check(reaches, "leased: the carriers sell in every district, the new ones too");
        Check(!InternetRules.Reaches(InternetCatalogue.LeasedIndex, null), "leased: no district, no line");
        bool fees = true, waits = true;
        for (int step = 0; step < LeasedLineCatalogue.Steps; step++)
        {
            int f = LeasedLineRules.InstallCents(step, fibre[0], PriceRules.Start), c = LeasedLineRules.InstallCents(step, cable[0], PriceRules.Start), g = LeasedLineRules.InstallCents(step, dig[0], PriceRules.Start);
            fees &= f == LeasedLineCatalogue.MonthCents[step] * 3 / 2 && c > f && g > c;
            int wf = LeasedLineRules.InstallDays(step, fibre[0]), wc = LeasedLineRules.InstallDays(step, cable[0]), wd = LeasedLineRules.InstallDays(step, dig[0]);
            waits &= wf == LeasedLineCatalogue.InstallDays[step] && wc > wf && wd > wc;
            Console.WriteLine("leased: step " + LeasedLineCatalogue.Mbps[step] / 1000 + " Gbps installs for " + f / 100 + " in " + wf + " days where fibre runs, " + c / 100 + " in " + wc + " with cable only, " + g / 100 + " in " + wd + " with nothing");
        }
        Check(fees, "leased: the installation is a month and a half of the price where fibre runs, and dearer with cable only and dearer still with nothing");
        Check(waits, "leased: the wait is the step's days where fibre runs, and longer with cable only and longer still with nothing");
        Check(LeasedLineRules.InstallDays(99, "ba:neighborhood_midtown") == LeasedLineCatalogue.InstallDays[LeasedLineCatalogue.Steps - 1] && LeasedLineRules.MonthCents(-4, PriceRules.Start) == LeasedLineCatalogue.MonthCents[0],
            "leased: a step out of range is brought to the nearest");
    }

    // A line beside a plan: ordered, installed, billed from its first day, changed without a gap, and gone with a cut or a cancelled connection.
    private static void LeasedLife()
    {
        const string midtown = "ba:neighborhood_midtown", hamptons = "ba:neighborhood_thehamptons";
        var a = new RealLifeOverhaul.Utilities.Account { Place = "Dock~1", Service = Service.Internet, State = SupplyState.Active, Provider = 3, Plan = 1 };
        Check(!LeasedLineRules.Has(a) && LeasedLineRules.LineMbps(a, 100) == 0, "leased: an account starts with no line");
        LeasedLineRules.Order(a, 0, hamptons, 100);
        int wait = LeasedLineRules.InstallDays(0, hamptons);
        Check(LeasedLineRules.Has(a) && a.LineStep == 1 && a.LineDay == 100 + wait && wait == 7 + 14, "leased: a line ordered in the Hamptons waits " + wait + " days");
        Check(LeasedLineRules.LineMbps(a, 100 + wait - 1) == 0 && LeasedLineRules.LineMbps(a, 100 + wait) == 5000, "leased: the line gives nothing until its day, then its speed");
        LeasedLineRules.AddDay(a, 100 + wait - 1, 30, PriceRules.Start);
        Check(a.Accrued == 0, "leased: nothing is billed for the days it is being installed");
        LeasedLineRules.AddDay(a, 100 + wait, 30, PriceRules.Start);
        Func<int, int> perDay = month => (month + 15) / 30;   // a day's share of the month, rounded
        Check(a.Accrued == perDay(LeasedLineCatalogue.MonthCents[0]), "leased: a live day adds a thirtieth of the month's price (" + a.Accrued + ")");
        for (int d = 1; d < 30; d++)
        {
            LeasedLineRules.AddDay(a, 100 + wait + d, 30, PriceRules.Start);
        }
        Check(Math.Abs(a.Accrued - LeasedLineCatalogue.MonthCents[0]) <= 30, "leased: thirty live days add the month's price (" + a.Accrued + " of " + LeasedLineCatalogue.MonthCents[0] + ")");
        // the plan's own speed stays, and the place has the faster of the two
        Check(LeasedLineRules.Speed(300, LeasedLineRules.LineMbps(a, 200)) == 5000 && LeasedLineRules.Speed(2000, 0) == 2000 && LeasedLineRules.Speed(0, 5000) == 5000, "leased: the place has the faster of its plan and its line");
        // a change keeps the running line until the new one is live
        int day = 200;
        LeasedLineRules.Order(a, 2, midtown, day);
        int wait2 = LeasedLineRules.InstallDays(2, midtown);
        Check(a.LineStep == 1 && a.LineNext == 3 && a.LineNextDay == day + wait2 && LeasedLineRules.Ordered(a) == 3, "leased: a bigger line is ordered beside the running one");
        Check(LeasedLineRules.LineMbps(a, day) == 5000 && LeasedLineRules.LineMbps(a, day + wait2 - 1) == 5000, "leased: the old line runs on until the new one is live");
        Check(LeasedLineRules.LineMbps(a, day + wait2) == 40000, "leased: then the new line gives its speed");
        int before = a.Accrued;
        LeasedLineRules.AddDay(a, day + wait2 - 1, 30, PriceRules.Start);
        LeasedLineRules.AddDay(a, day + wait2, 30, PriceRules.Start);
        Check(a.Accrued - before == perDay(LeasedLineCatalogue.MonthCents[0]) + perDay(LeasedLineCatalogue.MonthCents[2]), "leased: the old line is billed to its last day and the new from its first");
        Check(!LeasedLineRules.Turn(a, day + wait2 - 1) && LeasedLineRules.Turn(a, day + wait2) && a.LineStep == 3 && a.LineNext == 0 && a.LineDay == day + wait2, "leased: the day's turn makes the changed line the line");
        // not live in a cut or closed account, kept through a plan changed to another provider
        a.State = SupplyState.Cut;
        Check(LeasedLineRules.LineMbps(a, 400) == 0, "leased: a cut connection carries no line");
        a.State = SupplyState.Pending;
        Check(LeasedLineRules.LineMbps(a, 400) == 40000, "leased: a plan changed to another provider leaves the line running through the visit");
        a.State = SupplyState.Closed;
        Check(LeasedLineRules.LineMbps(a, 400) == 0, "leased: a closed account carries none");
        a.State = SupplyState.Active;
        LeasedLineRules.Drop(a);
        Check(!LeasedLineRules.Has(a) && LeasedLineRules.LineMbps(a, 400) == 0 && a.LineStep == 0 && a.LineNext == 0 && a.LineDay == 0 && a.LineNextDay == 0, "leased: a line given up is gone, with any change not yet live");
        // a second order while the first is still being installed replaces it
        LeasedLineRules.Order(a, 0, midtown, 500);
        LeasedLineRules.Order(a, 1, midtown, 502);
        Check(a.LineStep == 2 && a.LineNext == 0 && a.LineDay == 502 + LeasedLineRules.InstallDays(1, midtown), "leased: a line still being installed is replaced by a new order, not queued behind it");
    }

    private static void LeasedLimits()
    {
        // a hall of 1,000 racks' floor at the top tier: the connection is what stops it, until a line lifts it
        var hall = new DataCentre { Tier = 3, Sqm = 6000 };
        Check(hall.RacksRoom == 1000, "leased: the test hall has room for a thousand racks");
        int wireless = InfraLimits.Fit(hall, false, 150, out int binding0), plan1000 = InfraLimits.Fit(hall, false, 1000, out int binding1);
        Check(wireless == 1 && binding0 == InfraLimits.Bandwidth && plan1000 == 10 && binding1 == InfraLimits.Bandwidth, "leased: on a 150 Mbps plan a hall takes one rack, on a 1000 Mbps plan ten, the connection the limit");
        int last = -1;
        bool grows = true;
        for (int step = 0; step < LeasedLineCatalogue.Steps; step++)
        {
            int mbps = LeasedLineRules.Speed(150, LeasedLineCatalogue.Mbps[step]);
            int fit = InfraLimits.Fit(hall, false, mbps, out int binding);
            Console.WriteLine("leased: a hall on 150 Mbps with a " + LeasedLineCatalogue.Mbps[step] / 1000 + " Gbps line takes " + fit + " standard racks, held by " + binding);
            grows &= fit > last && (step < LeasedLineCatalogue.Steps - 1 ? binding == InfraLimits.Bandwidth : binding != InfraLimits.Bandwidth);
            last = fit;
        }
        Check(grows, "leased: every step lifts the limit, and the largest leaves the power feed (not the connection) as the limit of a full hall");
        Check(InfraLimits.Fit(hall, false, 5000, out _) == LeasedLineCatalogue.Mbps[0] / LeasedLineCatalogue.RackMbps && InfraLimits.Fit(hall, true, 5000, out _) == LeasedLineCatalogue.Mbps[0] / LeasedLineCatalogue.GpuRackMbps,
            "leased: a 5 Gbps line carries 50 standard racks or 12 AI racks, as the limits count them");
        // a hall bought before the limit existed keeps running: over its plan it can add nothing, and nothing takes its racks away
        var old = new DataCentre { Tier = 3, Sqm = 2000, Racks = 325 };
        CentreLimits l = InfraLimits.Of(old, 1000);
        Check(l.NetUse == 32500 && l.NetCap == 1000 && InfraLimits.Fit(old, false, 1000, out int bound) == 0 && bound == InfraLimits.Bandwidth && old.Racks == 325, "leased: a hall of 325 racks over a 1000 Mbps plan adds nothing and loses nothing");
        Check(InfraLimits.Fit(old, false, 40000, out _) > 0 && InfraLimits.Of(old, 40000).NetCap == 40000, "leased: a 40 Gbps line lets that hall grow again");
    }

    private static void LeasedSaves()
    {
        var state = new UtilitiesState();
        var line = new RealLifeOverhaul.Utilities.Account { Place = "Dock~1", Service = Service.Internet, Kind = PlaceKind.Storage, State = SupplyState.Active, AutoDebit = true, Provider = -1, LineStep = 3, LineDay = 120, LineNext = 4, LineNextDay = 150 };
        var plain = new RealLifeOverhaul.Utilities.Account { Place = "Main~2", Service = Service.Internet, State = SupplyState.Active, Provider = 1, Plan = 2 };
        state.Seed = 17;
        state.Accounts.Add(line);
        state.Accounts.Add(plain);
        UtilitiesState back = UtilitiesStore.Read(UtilitiesStore.Write(state), out _);
        RealLifeOverhaul.Utilities.Account got = back != null ? back.Find("Dock~1", Service.Internet) : null;
        Check(got != null && got.LineStep == 3 && got.LineDay == 120 && got.LineNext == 4 && got.LineNextDay == 150 && got.Provider == -1, "leased: a line, a change under way and a place with no plan but the line survive the save");
        RealLifeOverhaul.Utilities.Account plainBack = back != null ? back.Find("Main~2", Service.Internet) : null;
        Check(plainBack != null && plainBack.LineStep == 0 && plainBack.Provider == 1 && plainBack.Plan == 2, "leased: a place with only a plan keeps it and has no line");
        // a save made before the line: its records end at the outage, and read as having none
        string text = UtilitiesStore.Write(state);
        int at = text.IndexOf("~a:", StringComparison.Ordinal);
        int end = text.IndexOf('~', at + 3);
        string[] records = (end < 0 ? text.Substring(at + 3) : text.Substring(at + 3, end - at - 3)).Split(';');
        string tail = end < 0 ? string.Empty : text.Substring(end);
        var old = new List<string>(records.Length);
        foreach (string r in records)
        {
            string[] c = r.Split('|');
            old.Add(string.Join("|", c, 0, 26));   // the place and the 25 figures before the line's four
        }
        UtilitiesState older = UtilitiesStore.Read(text.Substring(0, at + 3) + string.Join(";", old) + tail, out _);
        RealLifeOverhaul.Utilities.Account oldPlan = older != null ? older.Find("Main~2", Service.Internet) : null;
        RealLifeOverhaul.Utilities.Account oldLine = older != null ? older.Find("Dock~1", Service.Internet) : null;
        Check(oldPlan != null && oldPlan.Provider == 1 && oldPlan.Plan == 2 && oldLine != null && !LeasedLineRules.Has(oldLine), "leased: a save made before the line loads with no line and every plan as it was");
        // figures out of range are brought within it
        line.LineStep = 9;
        UtilitiesState clamped = UtilitiesStore.Read(UtilitiesStore.Write(state), out _);
        RealLifeOverhaul.Utilities.Account c2 = clamped != null ? clamped.Find("Dock~1", Service.Internet) : null;
        Check(c2 != null && c2.LineStep == LeasedLineCatalogue.Steps, "leased: a step out of range in a save is brought to the largest");
    }

    private static void LeasedSources()
    {
        string internet = ModSource(@"Utilities\UtilitiesDirector.Internet.cs"), links = ModSource(@"Wiring\TechLinks.cs"), utilityLinks = ModSource(@"Wiring\UtilityLinks.cs");
        string oldApp = ModSource(@"Utilities\UtilitiesApp.Internet.cs"), kitLive = ModSource(@"Ui\Money\UtilitiesLive.cs"), kitBuild = ModSource(@"Ui\Money\UtilitiesBuild.Internet.cs");
        string infra = ModSource(@"Tech\TechDirector.Infra.cs"), kitInfra = ModSource(@"Ui\Tech\TechLive.Infra.cs"), oldInfra = ModSource(@"Tech\Infra\TechApp.Infra.cs");
        string bridges = ModSource(@"Tech\TechBridges.cs"), bills = ModSource(@"Utilities\UtilitiesDirector.Bills.cs"), day = ModSource(@"Utilities\UtilitiesDirector.cs");
        // who may have one: a warehouse fitted out as a data centre, asked of the tech sector, and nothing else
        string buy = MemberText(internet, "public bool BuyLine(");
        Func<string, bool> asksFirst = t => t.Contains("if (!LineSite(place))") && t.IndexOf("LineSite(place)", StringComparison.Ordinal) < t.IndexOf("UtilitiesGame.Pay(", StringComparison.Ordinal)
                                            && t.Contains("reallife_net_line_site");
        Check(asksFirst(buy) && !asksFirst(buy.Replace("if (!LineSite(place))", "if (false)")),
            "leased: an order for a place that is not a data centre is refused before any money is taken, with the reason (and the check fails on a purchase that does not ask)");
        string site = MemberText(internet, "public bool LineSite(");
        Check(site.Contains("UtilitiesBridges.LineSite") && site.Contains("PlaceKind.Home") && site.Contains("place.Reg != null"), "leased: the site is asked of the tech sector's bridge, and a home or a place with no building never has one");
        Check(utilityLinks.Contains("UtilitiesBridges.LineSite = LineSite;") && Between(utilityLinks, "private static bool LineSite(", "private static bool Shops(").Contains("tech.IsCentre(reg)"),
            "leased: the bridge says a place is a site if it is one of the tech sector's data centres");
        Check(internet.Contains("if (provider == InternetCatalogue.LeasedIndex)") && MemberText(internet, "public bool BuyNet(").IndexOf("BuyLine(", StringComparison.Ordinal)
            < MemberText(internet, "public bool BuyNet(").IndexOf("InternetRules.Reaches(", StringComparison.Ordinal), "leased: both interfaces buy a line through the plan purchase, which hands it to BuyLine first");
        // the speed the work and the limits read
        Check(MemberText(internet, "public int Speed(").Contains("LeasedLineRules.LineMbps(") && Between(links, "private static int Mbps(", "private static int LineMbps(").Contains("LeasedLineRules.LineMbps(")
              && Between(links, "private static int LineMbps(", "private static void Books(").Contains("LeasedLineRules.LineMbps("), "leased: the work's speed, the connection check and the limits all read the line");
        Check(Occurs(infra, "TechBridges.CentreMbps(") == 1 && Occurs(kitInfra, "TechBridges.CentreMbps(") == 2 && !infra.Contains("TechBridges.PlanMbpsAt(") && !kitInfra.Contains("TechBridges.PlanMbpsAt("),
            "leased: the limits, the refusal and both of the kit's pages read the connection through the plan or the line, whichever is faster");
        Check(MemberText(bridges, "public static int CentreMbps(").Contains("line > plan ? line : plan"), "leased: a hall with no line is read as its plan, as before");
        // the money: billed with the month's internet, a fee when ordered, and the line is dropped with a cancelled connection
        Check(day.Contains("LeasedLineRules.AddDay(a, d, billing, level);") && bills.Contains("LeasedLineRules.Turn(a, today);"), "leased: a day of the line is added to the month, and its change takes over on its day");
        Check(MemberText(internet, "public bool CancelNet(").Contains("LeasedLineRules.Drop(a);") && MemberText(internet, "public bool CancelLine(").Contains("LeasedLineRules.Drop(a);"), "leased: cancelling the connection or the line ends the line");
        Check(buy.Contains("reallife_transaction_util_net_line_install") && buy.Contains("deductible: true"), "leased: the installation is a deductible expense on the data centre's books");
        // both interfaces, and the way to grow named where a hall is full
        Check(oldApp.Contains("_reach.Add(InternetCatalogue.LeasedIndex)") && oldApp.Contains("reallife_net_box_line_cancel") && oldApp.Contains("LineInstall(box, place, _plan, today)"),
            "leased: the old Utilities app lists the line among a data centre's providers with its fee and wait, and can give it up");
        Check(kitLive.Contains("Leased = true") && kitLive.Contains("InstallFee = d.LineFee(place, i)") && kitBuild.Contains("o.Leased && !same") && kitBuild.Contains("reallife_net_box_line_cancel"),
            "leased: the kit's Internet page lists the lines for a data centre with the fee, the wait and what the district has, and can give one up");
        Check(infra.Contains("\"reallife_infra_note_limit_line\"") && infra.Contains("LinePairs()") && kitInfra.Contains("reallife_infra_centre_line") && oldInfra.Contains("reallife_infra_centre_line")
              && kitInfra.Contains("reallife_infra_button_line") && oldInfra.Contains("reallife_infra_button_line"), "leased: the refusal, the old hall page and the kit's hall page name the leased line as the way to grow");
        Dictionary<string, string> en = LocaleFile("en");
        string[] keys =
        {
            "reallife_net_provider_leased", "reallife_net_kind_leased", "reallife_net_gbps", "reallife_net_plan_option_line", "reallife_net_box_line_about", "reallife_net_box_line",
            "reallife_net_box_line_change", "reallife_net_line_live", "reallife_net_line_ordered", "reallife_net_box_line_install", "reallife_net_box_line_fibre", "reallife_net_box_line_cable",
            "reallife_net_box_line_dig", "reallife_net_box_line_cancel", "reallife_net_line_carries", "reallife_ui_util_your_line", "reallife_net_line_site", "reallife_net_line_cut",
            "reallife_net_line_same", "reallife_net_toast_line_bought", "reallife_net_toast_line_live", "reallife_net_toast_line_cancelled", "reallife_transaction_util_net_line_install",
            "reallife_transaction_util_net_line_install_label", "reallife_infra_centre_connection", "reallife_infra_centre_line", "reallife_infra_button_line", "reallife_infra_note_limit_line"
        };
        bool all = true;
        foreach (string k in keys)
        {
            all &= en.TryGetValue(k, out string said) && !string.IsNullOrWhiteSpace(said);
        }
        Check(all, "leased: every text of the leased lines is in the English file");
        bool short240 = true;
        foreach (string k in new[] { "reallife_net_toast_line_bought", "reallife_net_toast_line_live", "reallife_net_toast_line_cancelled", "reallife_infra_note_limit_line" })
        {
            short240 &= en.TryGetValue(k, out string said) && said.Length <= 240;
        }
        Check(short240, "leased: every notice is at most 240 characters");
        Check(en.TryGetValue("reallife_infra_note_limit_line", out string limit) && limit.Contains("leased line") && limit.Contains("{low}") && limit.Contains("{high}") && limit.Contains("{from}"),
            "leased: the refusal says a leased line lifts the limit, with its sizes and its price");
        Check(en.TryGetValue("reallife_changes_next", out string next) && next.Contains("leased line"), "leased: What's new tells of the leased lines");
    }
}
