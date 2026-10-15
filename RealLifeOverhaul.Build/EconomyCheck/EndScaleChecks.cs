using System;
using System.Collections.Generic;
using RealLifeOverhaul;
using BankCity = RealLifeOverhaul.Economy.BankCity;
using BankDay = RealLifeOverhaul.Economy.BankDay;
using BankCat = RealLifeOverhaul.Economy.OwnBankCatalogue;
using BankRules = RealLifeOverhaul.Economy.OwnBankRules;
using BankStore = RealLifeOverhaul.Economy.OwnBankStore;
using BbCat = RealLifeOverhaul.Utilities.BroadbandCatalogue;
using BbRules = RealLifeOverhaul.Utilities.BroadbandRules;
using Cloud = RealLifeOverhaul.Tech.Cloud;
using DataCentre = RealLifeOverhaul.Tech.DataCentre;
using InfraCat = RealLifeOverhaul.Tech.InfraCatalogue;
using InfraFile = RealLifeOverhaul.Tech.InfraFile;
using InfraRules = RealLifeOverhaul.Tech.InfraRules;
using InfraStore = RealLifeOverhaul.Tech.InfraStore;
using MacroCatalogue = RealLifeOverhaul.Economy.MacroCatalogue;
using Network = RealLifeOverhaul.Utilities.Network;
using OwnBank = RealLifeOverhaul.Economy.OwnBank;
using UtilService = RealLifeOverhaul.Utilities.Service;
using UtilRules = RealLifeOverhaul.Utilities.UtilitiesRules;
using UtilState = RealLifeOverhaul.Utilities.UtilitiesState;
using UtilStore = RealLifeOverhaul.Utilities.UtilitiesStore;

// The end game at scale (Core/EndScale and the late ventures' figures at scale: Tech/Infra, Utilities/Broadband*,
// Economy/OwnBank): the mixing and the phase-in rules, each venture's costs, upkeep, crews and income at each size, its
// payback against the aim (EndScaleRules.PaybackYearsLeast to Most game years of 60 days), an old save brought to scale
// step by step and never below nothing in one step, the option off giving the figures of before exactly, the save
// lines' new fields, and every new text in the three locales. Run with "endscale" on the command line, it also prints
// the before and after table the owner's report gives.
internal static partial class Program
{
    private const int ScaleYear = 60;                       // the game's own year, in days
    private const int Full = EndScaleRules.Full;
    private const long ProWageTech = 16000, ProWageEngineer = 40000;   // a professional's day the sims count (cents): a technician, an engineer (the game pays them; the rules do not)

    private static void EndScale()
    {
        int before = _run, failedBefore = _failed;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        ScaleMixing();
        ScaleOffIsBefore();
        ScaleInfraBySize();
        ScaleBroadbandBySize();
        ScaleBankBySize();
        ScalePayback();
        ScaleOldSaves();
        ScaleSaves();
        ScaleTexts();
        Console.WriteLine("  End game at scale: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed, " + watch.ElapsedMilliseconds + " ms");
        EndScaleTable(Array.IndexOf(Environment.GetCommandLineArgs(), "endscale") >= 0);
    }

    // ---------------------------------------------------------------- the mixing and the phase-in

    private static void ScaleMixing()
    {
        Check(EndScaleRules.Mix(100, 300, 0) == 100 && EndScaleRules.Mix(100, 300, Full) == 300 && EndScaleRules.Mix(100, 300, 500) == 200
              && EndScaleRules.Mix(100, 300, -5) == 100 && EndScaleRules.Mix(100, 300, 9999) == 300 && EndScaleRules.Mix(5000000000L, 100000000000L, 500) == 52500000000L,
            "end scale: a figure of before and its figure at scale mix by the share, within the whole, in longs too");
        int U = EndScaleRules.Unset;
        Check(EndScaleRules.From(false, 30, 50, true) == 30 && EndScaleRules.From(false, U, 50, true) == U && EndScaleRules.From(true, U, 50, true) == 50
              && EndScaleRules.From(true, U, 50, false) == 50 - EndScaleRules.PhaseDays && EndScaleRules.From(true, 30, 50, true) == 30 && EndScaleRules.From(true, U, 3, false) == 3 - EndScaleRules.PhaseDays,
            "end scale: a known day is kept, on or off; on, a venture held is phased in from today, one not held is at scale at once");
        // the option switched off on day 60 and on again on day 61: the weeks go on from where they were, and no second notice
        int from = EndScaleRules.From(true, U, 50, true), off = EndScaleRules.From(false, from, 60, true), again = EndScaleRules.From(true, off, 61, true);
        Check(again == 50 && EndScaleRules.Share(false, off, 60) == 0 && EndScaleRules.Share(true, again, 61) == EndScaleRules.Share(true, 50, 61) && EndScaleRules.Share(true, again, 61) > 0
              && !EndScaleRules.Begins(off, again, 61),
            "end scale: switching the option off and on neither restarts the weeks nor tells the notice again");
        // what is bought is at the whole scale while the option is on, whatever the weeks have reached
        int buy = EndScaleRules.Buying(true);
        Check(buy == Full && EndScaleRules.Buying(false) == 0 && InfraRules.RackPrice(false, buy) == InfraRules.RackPrice(false, Full) && InfraRules.FitCost(900, 2, buy) == InfraRules.FitCost(900, 2, Full)
              && InfraRules.Licence(buy) == InfraRules.Licence(Full) && BbRules.BuildCost(4, 1000, buy) == BbRules.BuildCost(4, 1000, Full) && BbRules.LicenceFee(1000, buy) == BbRules.LicenceFee(1000, Full)
              && BankRules.Licence(1000, buy) == BankRules.Licence(1000, Full) && BankRules.CapitalLeast(1000, 0, buy) == BankRules.CapitalLeast(1000, 0, Full),
            "end scale: racks, a fit-out, a network and the licences cost the whole scale's price while the option is on");
        ScaleRun early = CloudSim(36, 400, 24, 3, 2, true, 1), whole = CloudSim(Full, 400, 24, 3, 2, true, 1);
        var probe = new Network();
        BbRules.Found(probe, 1);
        BbRules.StartBuild(probe, 3, 1, 5, BbRules.BuildCost(3, 1000));
        BbRules.Finish(probe, 20);
        probe.Scale = 36;
        Check(early.Capex == whole.Capex && BbRules.ExtendCost(probe, 3, 1000, 20, EndScaleRules.Buying(true)) == BbRules.BuildCost(3, 1000, Full) - probe.Spent[3],
            "end scale: a hyperscaler built, or a district extended, on a day the share is 36 costs what it costs at the whole scale (" + Money(early.Capex) + ")");
        // the game's float cash past an int, worked out by hand
        Check(EndScaleRules.Whole(3000000000.0) == 3000000000L && EndScaleRules.Whole(2147483648.0f) == 2147483648L && EndScaleRules.Whole(9.5e12) == 9500000000000L
              && EndScaleRules.Whole(-5.0) == 0 && EndScaleRules.Whole(double.NaN) == 0 && EndScaleRules.Whole(1e30) == long.MaxValue / 4,
            "end scale: cash past two billion reads as itself, never as a negative int");
        // what could not be paid: owed, and the crews leave in that share
        Check(EndScaleRules.Unpaid(100, 40) == 600 && EndScaleRules.Unpaid(100, 100) == 0 && EndScaleRules.Unpaid(0, 0) == 0 && EndScaleRules.Leave(10, 500) == 5 && EndScaleRules.Leave(10, 0) == 10
              && EndScaleRules.Leave(3, 1) == 2 && EndScaleRules.Leave(7, Full) == 0,
            "end scale: the unpaid share of a day's costs, and the crews that leave for it");
        DataCentre unpaid = CentreAt(Full, 200, 0, 1);
        for (int day = 0; day < 30; day++)
        {
            InfraRules.Hire(unpaid, Full);
        }
        int paidCover = InfraRules.Cover(unpaid, Full);
        for (int day = 0; day < 30; day++)
        {
            InfraRules.Hire(unpaid, Full);
            InfraRules.Leave(unpaid, 600);   // the cash met two fifths of the day each day
        }
        Check(paidCover == Full && InfraRules.Cover(unpaid, Full) < 600, "end scale: a data centre whose costs go unpaid loses its crews and works less (" + InfraRules.Cover(unpaid, Full) + ")");
        bool rising = true;
        int last = -1;
        for (int day = 50; day <= 50 + EndScaleRules.PhaseDays + 5; day++)
        {
            int s = EndScaleRules.Share(true, 50, day);
            rising &= s >= last && s - Math.Max(0, last) <= Full / EndScaleRules.PhaseDays + 1;
            last = s;
        }
        Check(rising && EndScaleRules.Share(true, 50, 50) == 0 && EndScaleRules.Share(true, 50, 50 + EndScaleRules.PhaseDays / 2) == Full / 2 && EndScaleRules.Share(true, 50, 50 + EndScaleRules.PhaseDays) == Full
              && EndScaleRules.Share(true, 50, 999) == Full && EndScaleRules.Share(false, 50, 999) == 0 && EndScaleRules.Share(true, U, 999) == 0 && EndScaleRules.Share(true, 50, 10) == 0,
            "end scale: an old save comes to scale a step a day over four weeks, never down, never past the whole; off or unknown, none");
        Check(EndScaleRules.PhaseDays >= 21 && EndScaleRules.PhaseDays <= 35 && EndScaleRules.Share(true, 50, 64) == Full * 14 / EndScaleRules.PhaseDays && EndScaleRules.Share(true, 50, 70) < Full,
            "end scale: the phase-in takes a few game weeks, not days (" + EndScaleRules.PhaseDays + ")");
        Check(EndScaleRules.Begins(U, 50, 50) && !EndScaleRules.Begins(U, 50 - EndScaleRules.PhaseDays, 50) && !EndScaleRules.Begins(30, 30, 30) && !EndScaleRules.Begins(U, U, 50),
            "end scale: the notice is due only on the day a venture held begins to come to scale");
        Check(EndScaleRules.Need(10, 0) == 0 && EndScaleRules.Need(10, Full) == 10 && EndScaleRules.Need(10, 1) == 1 && EndScaleRules.Need(10, 500) == 5 && EndScaleRules.Need(0, Full) == 0
              && EndScaleRules.Per(0, 8) == 0 && EndScaleRules.Per(1, 8) == 1 && EndScaleRules.Per(8, 8) == 1 && EndScaleRules.Per(9, 8) == 2 && EndScaleRules.Per(5, 0) == 0,
            "end scale: a crew's need grows with the share (rounded up), one for every so many");
        Check(EndScaleRules.Hire(0, 100, 40) == 4 && EndScaleRules.Hire(0, 10, 40) == 1 && EndScaleRules.Hire(5, 3, 40) == 3 && EndScaleRules.Hire(99, 100, 40) == 100 && EndScaleRules.Hire(-3, 0, 40) == 0,
            "end scale: a crew short of its need takes on a share of it a day, one at the least; over it, it lets the rest go");
        int days = 0;
        for (int have = 0; have < 500 && days < 1000; days++)
        {
            have = EndScaleRules.Hire(have, 500, 40);
        }
        Check(days == 25, "end scale: a crew of five hundred is hired in 25 days (" + days + ")");
        Check(EndScaleRules.Cover(0, 0) == Full && EndScaleRules.Cover(5, 10) == 500 && EndScaleRules.Cover(20, 10) == Full && EndScaleRules.Working(0) == EndScaleRules.WorkingLeast && EndScaleRules.Working(Full) == Full
              && EndScaleRules.Working(500) == 750 && EndScaleRules.Outages(0) == EndScaleRules.OutageMost && EndScaleRules.Outages(Full) == Full,
            "end scale: short of its crews a venture does less and fails more; with them all, as usual");
        long room = 100;
        Check(EndScaleRules.Take(ref room, 150) == 100 && room == 0 && EndScaleRules.Take(ref room, 50) == 0 && room == 0, "end scale: what the scale adds is charged only as far as the cash goes");
        room = -40;
        Check(EndScaleRules.Take(ref room, 50) == 0 && room == -40 && EndScaleRules.Take(ref room, -5) == 0, "end scale: with the cash already below nothing, nothing more is charged by the scale");
    }

    // ---------------------------------------------------------------- the option off: the figures of before, exactly

    private static void ScaleOffIsBefore()
    {
        var d = new DataCentre { Place = "x", District = "a", Tier = 2, Sqm = 1200 };
        InfraRules.Buy(d, 60, false, 0);
        InfraRules.Buy(d, 4, true, 0);
        d.Colo = 7;
        Check(d.Dense == 0 && d.DenseGpu == 0 && d.Book == 60L * InfraCat.RackCost + 4L * InfraCat.GpuRackCost, "end scale off: racks bought are of the old kind, at the old prices");
        Check(InfraRules.FitCost(1200, 2, 0) == 1200L * InfraCat.FitPerSqm[2] && InfraRules.UpgradeCost(1200, 1, 3, 0) == 1200L * (InfraCat.FitPerSqm[3] - InfraCat.FitPerSqm[1])
              && InfraRules.RackPrice(false, 0) == InfraCat.RackCost && InfraRules.RackPrice(true, 0) == InfraCat.GpuRackCost && InfraRules.RacksStep(0) == 5,
            "end scale off: fitting out and racks cost what they did");
        Check(InfraRules.Units(d, 0) == (60 - 7) * InfraCat.UnitsPerRack && InfraRules.GpuUnits(d, 0) == 4 * InfraCat.GpuUnitsPerRack
              && InfraRules.Power(d, 0) == (60L * InfraCat.RackKw + 4L * InfraCat.GpuRackKw) * 24 * 1000 && InfraRules.Upkeep(d, 0) == 0 && InfraRules.WriteDown(d) == d.Book / InfraCat.WriteDownDays,
            "end scale off: a data centre sells, draws and writes down as before, with no upkeep");
        bool noCrew = true;
        for (int k = 0; k < InfraCat.CrewKinds; k++)
        {
            noCrew &= InfraRules.CrewNeed(d, k, 0) == 0;
        }
        InfraRules.Hire(d, 0);
        Check(noCrew && InfraRules.Cover(d, 0) == Full && InfraRules.Crews(d) == 0 && InfraRules.OpsNeed(100000, 0) == 0, "end scale off: no crews are needed or paid");
        var c = new Cloud { Hyper = true };
        Check(InfraRules.Market(c, false, 0, 7) == 3L * InfraCat.CloudDemand && InfraRules.Market(c, true, 0, 7) == 3L * InfraCat.GpuDemand && InfraRules.WarMarket(c, 0, 7) == InfraCat.CloudDemand
              && InfraRules.Licence(0) == InfraCat.LicenceFee && InfraRules.CloudRacksLeast(0) == InfraCat.CloudRacksLeast && InfraRules.CloudCashLeast(0) == InfraCat.CloudCashLeast
              && InfraRules.HyperRacks(0) == InfraCat.HyperRacks && InfraRules.HyperCash(0) == InfraCat.HyperCash && InfraRules.ColoWanted(1, 0) == InfraCat.ColoDemand,
            "end scale off: the cloud's market, its licence and its steps are as before, whatever its regions");
        var dense = new DataCentre { Sqm = 600 };
        InfraRules.Buy(dense, 10, false, Full);
        Check(dense.Dense == 10 && InfraRules.Units(dense, 0) == 10 * InfraCat.UnitsPerRack && InfraRules.Upkeep(dense, 0) == 0,
            "end scale off: racks of the new kind sell as the old with the option off, and cost nothing to keep");

        // the broadband company
        var n = new Network();
        BbRules.Found(n, 1);
        n.Scale = 0;
        bool same = BbRules.LicenceFee(1250, 0) == BbRules.At(BbCat.LicenceFee, 1250) && BbRules.Installs(10, 30, 1000, 0) == 20 * BbCat.InstallCents / 100;
        for (int x = 0; x < Network.Districts; x++)
        {
            same &= BbRules.BuildCost(x, 1250, 0) == BbRules.At(BbCat.BuildCost[x], 1250) && BbRules.MarketOf(n, x) == BbCat.Market[x];
            BbRules.StartBuild(n, x, 1, 5, BbRules.BuildCost(x, 1000, 0));
            same &= !n.Scaled[x];
        }
        BbRules.Finish(n, 99);
        for (int x = 0; x < Network.Districts; x++)
        {
            n.Share[x, Network.Own] = 200;
        }
        BbRules.Hire(n, 99);
        BbRules.DayCosts(n, 1000, 99, 5, out int staff, out int network, out _);
        long conns = BbRules.Connections(n), upkeep = 0;
        for (int x = 0; x < Network.Districts; x++)
        {
            upkeep += (long)BbRules.BuildCost(x, 1000) * BbCat.UpkeepPerMille / 1000;
        }
        Check(same && conns == 25000 && n.Crew[0] + n.Crew[1] + n.Crew[2] == 0 && n.Cover == Full && staff == (int)((conns * BbCat.StaffCents / 5 + 50) / 100)
              && network == (int)((conns * BbCat.NetworkCents / 5 + upkeep * 100 / 5 + 50) / 100) && BbRules.ExtendCost(n, 2, 1000, 99, 0) == 0,
            "end scale off: the broadband company's licence, networks, market, staff, network costs and installations are as before, with no crews and nothing to extend");

        // the bank
        BankCity city = BankCity.Quiet(ScaleYear);
        var b = new OwnBank();
        BankRules.Found(b, 1, 7, BankRules.CapitalLeast(city.Level), "Before");
        var day = new BankDay();
        bool exact = true;
        for (int dd = 1; dd <= 200; dd++)
        {
            BankRules.Day(b, city, dd, null, day);
            long old = BankCat.StaffDayCents * city.Level / PriceRules.Start + (b.Savings * BankCat.StaffOnSavings + (b.Lent + b.InArrears) * BankCat.StaffOnLoans) / (1000L * ScaleYear);
            exact &= day.Staff == old && day.Systems == 0;
        }
        Check(exact && b.Crew[0] + b.Crew[1] + b.Crew[2] + b.Crew[3] == 0 && BankRules.Licence(1000, 0) == BankCat.LicenceCents && BankRules.CapitalLeast(1000, 0, 0) == BankCat.CapitalLeastCents
              && BankRules.CapitalLeast(1000, 2, 0) == BankRules.CapitalLeast(1000, 2) && BankRules.SavingsPool(city, 0) == BankCat.SavingsCents[0] && BankRules.DemandPool(city, 4) == BankCat.DemandCents[4],
            "end scale off: the bank's licence, capital, the city's savings and borrowing, and its staff are as before, with no crews and no systems");
    }

    // ---------------------------------------------------------------- each venture at each size

    private static void ScaleInfraBySize()
    {
        Check(InfraRules.RackPrice(false, Full) == InfraCat.RackCostScale && InfraRules.RackPrice(true, Full) == InfraCat.GpuRackCostScale && InfraRules.RacksStep(Full) == InfraCat.RacksStep
              && InfraRules.FitCost(1000, 2, Full) == 1000L * InfraCat.FitPerSqmScale[2] && InfraCat.FitPerSqmScale[3] > InfraCat.FitPerSqmScale[0] * 2,
            "end scale: a rack of dense servers, an AI rack and a fit-out cost what the real ones do, more for a higher tier");
        bool dearer = true;
        for (int t = 0; t < InfraCat.Tiers; t++)
        {
            dearer &= InfraCat.FitPerSqmScale[t] > InfraCat.FitPerSqm[t] * 5 && (t == 0 || InfraCat.PueScale[t] < InfraCat.PueScale[t - 1]) && InfraCat.PueScale[t] > 1000;
        }
        Check(dearer, "end scale: every tier costs far more to fit out, and a better tier cools with less power");
        DataCentre small = CentreAt(Full, 40, 0, 2), mid = CentreAt(Full, 200, 10, 2), big = CentreAt(Full, 800, 40, 2);
        long sBuild = InfraRules.FitCost(small.Sqm, 2, Full) + small.Book, bBuild = InfraRules.FitCost(big.Sqm, 2, Full) + big.Book;
        Check(sBuild > 10000000L * 100 && bBuild > 300000000L * 100, "end scale: a small data centre costs tens of millions, a large one hundreds of millions (" + Money(sBuild) + ", " + Money(bBuild) + ")");
        Check(InfraRules.Units(big, Full) == 800 * InfraCat.UnitsPerRackScale && InfraRules.GpuUnits(big, Full) == 40 * InfraCat.GpuUnitsPerRackScale && InfraRules.Units(big, 500) < InfraRules.Units(big, Full),
            "end scale: racks of the new kind sell far more compute, as the share comes in");
        Check(InfraRules.Upkeep(small, Full) < InfraRules.Upkeep(mid, Full) && InfraRules.Upkeep(mid, Full) < InfraRules.Upkeep(big, Full)
              && InfraRules.Upkeep(big, Full) >= 800L * InfraCat.UpkeepRack + 40L * InfraCat.UpkeepGpu && InfraRules.Power(big, Full) > InfraRules.Power(big, 0),
            "end scale: upkeep grows with the racks, and the cooling's power comes on top");
        int[] sNeed = Needs(small), mNeed = Needs(mid), bNeed = Needs(big);
        Check(sNeed[0] == Math.Max(InfraCat.OperatorsLeast, EndScaleRules.Per(40, InfraCat.OperatorRacks)) && bNeed[0] == EndScaleRules.Per(800, InfraCat.OperatorRacks) + EndScaleRules.Per(40, InfraCat.OperatorGpu) && bNeed[1] == EndScaleRules.Per(800L * 8 + 40 * 32, InfraCat.EngineerKw) && bNeed[2] == InfraCat.GuardsLeast + big.Sqm / InfraCat.GuardSqm
              && mNeed[0] > sNeed[0] && bNeed[0] > mNeed[0] && bNeed[1] > sNeed[1] && bNeed[2] > sNeed[2],
            "end scale: operators for the racks, engineers for the draw and security for the site, more of each for a larger data centre (" + bNeed[0] + ", " + bNeed[1] + ", " + bNeed[2] + ")");
        Check(InfraRules.CrewNeed(new DataCentre { Sqm = 900 }, InfraCat.Security, Full) == 0, "end scale: a data centre with no racks needs no crew yet");
        int down = 0, staffed = 0;
        DataCentre bare = CentreAt(Full, 200, 0, 0);
        bare.Technicians = InfraRules.TechniciansNeeded(bare);
        for (int day = 0; day < 4000; day++)
        {
            down += InfraRules.Outage(5, bare, day, 0) ? 1 : 0;
            staffed += InfraRules.Outage(5, bare, day, Full) ? 1 : 0;
        }
        Check(down > staffed * 2 && staffed > 0, "end scale: with none of its crews a data centre goes down far more often (" + down + " to " + staffed + " in 4000 days)");
        for (int day = 0; day < 30; day++)
        {
            InfraRules.Hire(mid, Full);
        }
        Check(InfraRules.Cover(mid, Full) == Full && InfraRules.Crews(mid) == (long)mNeed[0] * InfraCat.CrewWage[0] + (long)mNeed[1] * InfraCat.CrewWage[1] + (long)mNeed[2] * InfraCat.CrewWage[2],
            "end scale: in a month the crews are all there, and their wages are the heads by the kind's wage");
        var c = new Cloud { Hyper = true };
        Check(InfraRules.Market(c, false, Full, 5) > InfraRules.Market(c, false, Full, 3) && InfraRules.Market(c, false, Full, 3) == (long)InfraCat.CloudDemandScale * InfraCat.HyperDemandScale / 1000
              && InfraRules.Market(new Cloud(), false, Full, 5) == InfraCat.CloudDemandScale && InfraRules.Market(c, false, Full, 3) > InfraRules.Market(c, false, 0, 3) * 10,
            "end scale: the cloud's market is far larger, and a hyperscaler's grows with each region past three");
        Check(InfraRules.CityDemand(c, 1, 1000000, false, Full, 5) > InfraRules.CityDemand(c, 1, 1000000, false, Full, 3), "end scale: more regions sell more");
        Check(InfraRules.Licence(Full) == InfraCat.LicenceFeeScale && InfraRules.CloudCashLeast(Full) == InfraCat.CloudCashLeastScale && InfraRules.HyperCash(Full) == InfraCat.HyperCashScale
              && InfraCat.HyperCashScale >= 100000000000L && InfraRules.HyperRacks(Full) > InfraRules.CloudRacksLeast(Full),
            "end scale: a hyperscaler wants a billion in cash and hundreds of racks");
        // the old kind replaced by the new
        var r = new DataCentre { Sqm = 600 };
        InfraRules.Buy(r, 30, false, 0);
        InfraRules.Buy(r, 2, true, 0);
        long book = r.Book;
        Check(!InfraRules.Replace(r, 5, false, 0) && !InfraRules.Replace(r, 31, false, Full) && InfraRules.Replace(r, 10, false, Full) && r.Dense == 10 && r.Legacy == 20
              && r.Book == book - book * 10 * InfraCat.RackCost / InfraRules.CostBasis(new DataCentre { Racks = 30, GpuRacks = 2 }) + 10L * InfraCat.RackCostScale,
            "end scale: old racks are replaced by new ones only at the scale, their part of the books written off and the new price put in");
        Check(InfraRules.Replace(r, 2, true, Full) && r.DenseGpu == 2 && r.LegacyGpu == 0 && !InfraRules.Replace(r, 1, true, Full), "end scale: old AI racks are replaced too, and no more than there are");
        r.Colo = 25;
        Check(InfraRules.Units(r, Full) == (10 - 5) * InfraCat.UnitsPerRackScale, "end scale: racks let to companies are the old kind first");
    }

    private static DataCentre CentreAt(int share, int racks, int gpu, int tier)
    {
        var d = new DataCentre { Place = "c" + racks, District = "a", Tier = tier, Sqm = (racks + 2 * gpu) * InfraCat.SqmPerRack };
        InfraRules.Buy(d, racks, false, share);
        if (gpu > 0)
        {
            InfraRules.Buy(d, gpu, true, share);
        }
        return d;
    }

    private static int[] Needs(DataCentre d)
    {
        var need = new int[InfraCat.CrewKinds];
        for (int k = 0; k < need.Length; k++)
        {
            need[k] = InfraRules.CrewNeed(d, k, Full);
        }
        return need;
    }

    private static void ScaleBroadbandBySize()
    {
        Check(BbRules.LicenceFee(1000, Full) == BbCat.LicenceFeeScale && BbRules.Installs(0, 10, 1000, Full) == 10 * BbCat.InstallCentsScale / 100,
            "end scale: the operator's licence and an installation cost what the real ones do");
        bool fortune = true;
        long all = 0;
        for (int d = 0; d < Network.Districts; d++)
        {
            int cost = BbRules.BuildCost(d, 1000, Full);
            all += cost;
            fortune &= cost >= BbRules.BuildCost(d, 1000, 0) * 5 && BbRules.MarketAtScale(d) == BbCat.Market[d] * BbCat.MarketScale / 1000
                       && cost == (int)((long)BbRules.MarketAtScale(d) * (d == BbCat.FarDistrict ? BbCat.HomeCentsFarScale : BbCat.HomeCentsScale) / 100);
        }
        Check(fortune && all > 150000000L && BbRules.BuildCost(4, 4000, Full) == 4 * BbRules.BuildCost(4, 1000, Full),
            "end scale: a district's network is dug to every home of the city at its real size, a fortune each, the whole city over a hundred and fifty million, and dearer with prices (" + all + ")");
        var n = new Network();
        BbRules.Found(n, 1);
        n.Scale = Full;
        BbRules.StartBuild(n, 4, 1, 5, BbRules.BuildCost(4, 1000, Full), true);
        BbRules.StartBuild(n, 0, 1, 5, BbRules.BuildCost(0, 1000, Full), true);
        BbRules.Finish(n, 20);
        n.Share[4, Network.Own] = 200;
        n.Share[0, Network.Own] = 100;
        Check(n.Scaled[4] && BbRules.MarketOf(n, 4) == BbRules.MarketAtScale(4) && BbRules.Connections(n, 4) == BbRules.MarketAtScale(4) / 5, "end scale: a network dug at scale reaches the whole district at its real size");
        int field = BbRules.CrewNeed(n, BbCat.Field, 20), calls = BbRules.CrewNeed(n, BbCat.Calls, 20), ops = BbRules.CrewNeed(n, BbCat.Operations, 20);
        int conns = BbRules.Connections(n);
        Check(field == EndScaleRules.Per(conns, BbCat.FieldConnections) && calls == EndScaleRules.Per(conns, BbCat.CallConnections) && ops == 2 * BbCat.OperationsPerDistrict && calls > field,
            "end scale: field engineers and call centre agents by the connections, network operations by the districts open (" + field + ", " + calls + ", " + ops + ")");
        double pull = BbRules.Pull(n);
        BbRules.Hire(n, 20);
        Check(n.Cover < Full && BbRules.Pull(n) < pull, "end scale: a company short of its crews wins customers more slowly");
        for (int day = 21; day < 90; day++)
        {
            BbRules.Hire(n, day);
        }
        Check(n.Cover == Full && n.Crew[BbCat.Field] == field && BbRules.CrewCents(n) == (long)field * BbCat.CrewWage[0] + (long)calls * BbCat.CrewWage[1] + (long)ops * BbCat.CrewWage[2],
            "end scale: in weeks the crews are all there, and paid by the head (" + n.Cover + " " + n.Crew[0] + "/" + field + " " + n.Crew[1] + "/" + calls + " " + n.Crew[2] + "/" + ops + ")");
        BbRules.DayCosts(n, 1000, 90, 5, out int staff, out int network, out _);
        BbRules.DayCosts(n, 1000, 90, 5, out int staff0, out int network0, out _, 0);
        Check(staff == (int)((BbRules.CrewCents(n) + 50) / 100) && network > network0, "end scale: the staff at scale are the crews' wages; the network's upkeep is on the real network");
        var old = new Network();
        BbRules.Found(old, 1);
        BbRules.StartBuild(old, 3, 1, 5, BbRules.BuildCost(3, 1000));
        BbRules.Finish(old, 20);
        old.Scale = Full;
        int extend = BbRules.ExtendCost(old, 3, 1000, 20, Full);
        Check(!old.Scaled[3] && BbRules.MarketOf(old, 3) == BbCat.Market[3] && extend == BbRules.BuildCost(3, 1000, Full) - old.Spent[3] && BbRules.ExtendCost(old, 2, 1000, 20, Full) == 0,
            "end scale: a district dug before reaches the old number of homes, and can be extended for the difference");
        BbRules.Extend(old, 3, extend);
        Check(old.Scaled[3] && BbRules.MarketOf(old, 3) == BbRules.MarketAtScale(3) && old.Spent[3] == BbRules.BuildCost(3, 1000, Full) && BbRules.ExtendCost(old, 3, 1000, 20, Full) == 0,
            "end scale: extended, it reaches the whole district and has cost what a network at scale does");
        Check(BbRules.BuildCostRange(1000, 0, false) == BbRules.BuildCost(2, 1000) && BbRules.BuildCostRange(1000, 0, true) == BbRules.BuildCost(4, 1000)
              && BbRules.BuildCostRange(1000, Full, true) == BbRules.BuildCost(4, 1000, Full),
            "end scale: the Guide's least and most a district costs are the catalogue's");
    }

    private static void ScaleBankBySize()
    {
        BankCity c = BankCity.Quiet(ScaleYear);
        c.Scale = Full;
        Check(BankRules.Licence(1000, Full) == BankCat.LicenceCentsScale && BankRules.CapitalLeast(1000, 0, Full) == BankCat.CapitalLeastCentsScale
              && BankRules.CapitalLeast(1000, 1, Full) == BankCat.CapitalLeastCentsScale * (1000 + BankCat.AgainCapital) / 1000
              && BankRules.SavingsPool(c, 0) == BankCat.SavingsCents[0] * BankCat.PoolScale / 1000 && BankRules.DemandPool(c, 2) == BankCat.DemandCents[2] * BankCat.PoolScale / 1000,
            "end scale: a bank's licence and capital are a real bank's, and the city's savings and borrowing are at its real size");
        var b = new OwnBank();
        BankRules.Found(b, 1, 7, BankRules.CapitalLeast(1000, 0, Full), "Scale");
        b.Deposits[0] = 24000000000L;
        b.Deposits[1] = 7200000000L;
        b.Loans[2] = 16000000000L;
        b.Reserves += b.Deposits[0] + b.Deposits[1] - b.Loans[2];
        Check(BankRules.CrewNeed(b, BankCat.Tellers, Full) == 20 && BankRules.CrewNeed(b, BankCat.Managers, Full) == 10 && BankRules.CrewNeed(b, BankCat.Officers, Full) == 20
              && BankRules.CrewNeed(b, BankCat.Compliance, Full) == BankCat.ComplianceLeast + (int)((b.Savings + b.Capital) / BankCat.ComplianceCents) && BankRules.CrewNeed(b, BankCat.Tellers, 0) == 0,
            "end scale: tellers for the people's savings, managers for the businesses', officers for the loans, compliance for all of it");
        int cover = BankRules.Hire(b, Full);
        Check(cover < Full && b.Crew[BankCat.Tellers] >= 1, "end scale: a new bank is short of its crews at first");
        var short_ = new OwnBank();
        var whole = new OwnBank();
        BankRules.Found(short_, 1, 7, BankRules.CapitalLeast(1000, 0, Full), "Short");
        BankRules.Found(whole, 1, 7, BankRules.CapitalLeast(1000, 0, Full), "Whole");
        var day = new BankDay();
        for (int d = 1; d <= 40; d++)
        {
            BankRules.Day(whole, c, d, null, day);
            Array.Clear(short_.Crew, 0, short_.Crew.Length);
            BankRules.Day(short_, c, d, null, day);
            Array.Clear(short_.Crew, 0, short_.Crew.Length);
        }
        Check(short_.Lent < whole.Lent && short_.Savings < whole.Savings, "end scale: a bank always short of its crews lends and takes savings more slowly");
        long systems = 0, staff = 0;
        for (int d = 41; d <= 100; d++)
        {
            BankRules.Day(whole, c, d, null, day);
            systems = day.Systems;
            staff = day.Staff;
        }
        Check(systems >= BankCat.SystemsDayCents && staff == BankRules.Crews(whole) && BankRules.Balanced(whole), "end scale: at scale its staff are its crews' wages, and its systems and deposit insurance are paid; the books balance");
        BankCity yearLong = BankCity.Quiet(365);
        yearLong.Scale = Full;
        var longBank = new OwnBank();
        BankRules.Found(longBank, 1, 7, BankRules.CapitalLeast(1000, 0, Full), "Long");
        for (int d = 1; d <= 100; d++)
        {
            BankRules.Day(longBank, yearLong, d, null, day);
        }
        Check(day.Staff == BankRules.Crews(longBank) * BankCat.CostYearDays / 365 && day.Systems < BankCat.SystemsDayCents,
            "end scale: in a year of 365 days the bank's crews and systems are paid by the year, as its rates are");
    }

    // ---------------------------------------------------------------- the payback against the aim

    private static void ScalePayback()
    {
        int least = EndScaleRules.PaybackDaysLeast, most = EndScaleRules.PaybackDaysMost, largest = least + (most - least) * 3 / 8;   // (the largest builds take the longer end: 210 days or more)
        ScaleRun cloud = CloudSim(Full, 60, 4, 1, 2, false, 3000), cloud2 = CloudSim(Full, 120, 10, 1, 2, false, 3000);
        Check(cloud.Payback >= least && cloud.Payback <= most && cloud2.Payback >= least && cloud2.Payback <= most,
            "end scale: a cloud with its racks let as the city wants pays back within " + least + " to " + most + " game days (" + cloud.Payback + " and " + cloud2.Payback + " days)");
        ScaleRun h3 = CloudSim(Full, 400, 24, 3, 2, true, 3000), h5 = CloudSim(Full, 700, 60, 5, 2, true, 3000), h7 = CloudSim(Full, 1100, 100, 7, 2, true, 3000);
        Check(h3.Payback >= largest && h3.Payback <= most && h5.Payback >= largest && h5.Payback <= most && h7.Payback >= largest && h7.Payback <= most,
            "end scale: a hyperscaler pays back at the longer end of the aim, however large (" + h3.Payback + ", " + h5.Payback + " and " + h7.Payback + " days)");
        var letNew = new DataCentre { Sqm = 600 };
        InfraRules.Buy(letNew, 10, false, Full);
        letNew.Colo = 4;
        var letOld = new DataCentre { Sqm = 600 };
        InfraRules.Buy(letOld, 10, false, 0);
        letOld.Colo = 4;
        Check(InfraRules.ColoDay(letNew, Full) == 4L * InfraCat.ColoMonthScale / 30 && InfraRules.ColoDay(letOld, Full) == 4L * InfraCat.ColoMonth / 30 && InfraRules.ColoDay(letNew, 0) == 4L * InfraCat.ColoMonth / 30
              && InfraRules.Units(letNew, Full) == 6 * InfraCat.UnitsPerRackScale,
            "end scale: a rack of the new kind let to a company rents as managed servers; the old kind, and every rack with the option off, at the old rent");
        Check(h3.Capex > 150000000L * 100 && h7.Capex > 500000000L * 100, "end scale: a hyperscaler costs hundreds of millions to build (" + Money(h3.Capex) + " to " + Money(h7.Capex) + ")");
        Check(h7.Net > h5.Net && h5.Net > h3.Net && h3.Net > cloud2.Net && h7.Net * 400 / 1100 > h3.Net * 7 / 10 && h7.Net * 400 / 1100 < h3.Net * 13 / 10,
            "end scale: a larger hyperscaler earns proportionately more (" + Money(h3.Net) + ", " + Money(h5.Net) + ", " + Money(h7.Net) + " a day)");
        Check(h7.Crew > h3.Crew * 2 && h7.Crew > 500 && h3.Staff > 20, "end scale: a hyperscaler keeps hundreds of people besides its professionals (" + h3.Crew + " to " + h7.Crew + ")");
        int yLeast = EndScaleRules.PaybackYearsLeast, yMost = EndScaleRules.PaybackYearsMost;
        long[] cashYears = new long[12], saleYears = new long[12];
        ScaleRun bb = BroadbandSim(Full, ScaleYear, 720, cashYears, saleYears), bbOld = BroadbandSim(0, ScaleYear, 3000), bbLong = BroadbandSim(Full, 365, 6 * 365 + 10);
        Check(bb.Payback >= yLeast * ScaleYear && bb.Payback <= yMost * ScaleYear && bb.Capex > 200000000L * 100 && bb.Crew > 300,
            "end scale: a broadband network over the whole city costs hundreds of millions, keeps hundreds of people and pays back in " + yLeast + " to " + yMost + " game years (" + bb.Payback + " days)");
        Check(bbLong.Payback >= yLeast * 365 && bbLong.Payback <= yMost * 365, "end scale: in a year of 365 days it pays back in as many of its own years (" + bbLong.Payback + " days)");
        Check(bbOld.Payback > 0 && bbOld.Payback < 2 * ScaleYear && bb.Net > bbOld.Net * 4, "end scale: before, it paid back in under two years (" + bbOld.Payback + " days); at scale it earns far more a day");
        bool keep = true;
        for (int t = 1; t + 4 < cashYears.Length; t++)
        {
            keep &= cashYears[t] + saleYears[t] <= cashYears[t + 4];
        }
        Check(keep && cashYears[1] + saleYears[1] < 0, "end scale: digging a network to sell it never beats running it four more years; sold after a year it loses money (" + Money(cashYears[1] + saleYears[1]) + ")");
        long year, capital;
        ScaleRun bank = BankSim(Full, 0, 3000, out year, out capital), bankOld = BankSim(0, 0, 3000, out _, out _), bankLong = BankSim(Full, 0, 6 * 365 + 10, out _, out _, 365);
        Check(bank.Payback >= yLeast * ScaleYear && bank.Payback <= yMost * ScaleYear && bankOld.Payback > 0 && bankOld.Payback < 2 * ScaleYear,
            "end scale: a bank's licence pays back in " + yLeast + " to " + yMost + " game years (" + bank.Payback + " days; before, " + bankOld.Payback + ")");
        Check(bankLong.Payback >= yLeast * 365 && bankLong.Payback <= yMost * 365, "end scale: in a year of 365 days a bank pays back in as many of its own years (" + bankLong.Payback + " days)");
        // an old bank keeps the least capital it was founded under; a new one is held to the scale's
        var oldBank = new OwnBank();
        BankRules.Found(oldBank, 1, 7, 40000000000L, "Old");
        var newBank = new OwnBank();
        BankRules.Found(newBank, 1, 7, 40000000000L, "New");
        newBank.FoundedLeast = BankRules.LeastAt(Full);
        Check(BankRules.MostOut(oldBank, 1000) == BankRules.ToStep(40000000000L - BankCat.CapitalLeastCents) && BankRules.MostOut(newBank, 1000) == BankRules.ToStep(40000000000L - BankCat.CapitalLeastCentsScale),
            "end scale: a bank founded before the scale may take out all but the old least capital; one founded at it, all but the scale's");
        Check(InfraCat.IncomeAtScale == 1000 && BbCat.IncomeAtScale == 1000 && BankCat.IncomeAtScale == 1000 && InfraRules.Units(CentreAt(Full, 10, 0, 1), Full) == 10 * InfraCat.UnitsPerRackScale,
            "end scale: each venture's income at scale is set by one figure, at its first value");
        Check(year > 0 && year < BankRules.CapitalLeast(1000, 0, Full) && bank.Crew > 50, "end scale: a bank at scale makes a profit short of its capital a year and keeps dozens of people (" + Money(year) + ", " + bank.Crew + ")");
    }

    // ---------------------------------------------------------------- old saves

    private static void ScaleOldSaves()
    {
        // an old hyperscaler (its racks of the old kind) brought to scale: still more in than out each day
        ScaleRun oldHyper = CloudSim(Full, 300, 18, 3, 2, true, 200, 0);
        Check(oldHyper.Net > 0, "end scale: an old save's hyperscaler at scale, its racks of the old kind, still earns more than it costs (" + Money(oldHyper.Net) + " a day)");
        // the costs the scale adds come in a step a day, and never past the cash
        var d = CentreAt(0, 300, 18, 2);
        int from = 100;
        long lastExtra = 0, fullExtra = 0, worstStep = 0;
        for (int day = from; day <= from + EndScaleRules.PhaseDays + 30; day++)
        {
            int share = EndScaleRules.Share(true, from, day);
            InfraRules.Hire(d, share);
            long extra = InfraRules.Upkeep(d, share) + InfraRules.Crews(d) + (InfraRules.Power(d, share) - InfraRules.Power(d, 0)) / 1000 * 15;
            worstStep = Math.Max(worstStep, extra - lastExtra);
            lastExtra = extra;
            fullExtra = extra;
        }
        Check(fullExtra > 0 && worstStep * EndScaleRules.PhaseDays <= fullExtra * 3, "end scale: an old data centre's new costs come in step by step over the weeks, never in one jump (the largest day " + Money(worstStep) + " of " + Money(fullExtra) + ")");
        long cash = 5000000, charged = 0;
        long room = cash;
        charged += EndScaleRules.Take(ref room, fullExtra);
        charged += EndScaleRules.Take(ref room, fullExtra);
        Check(charged <= cash && room >= 0, "end scale: a day's new costs never take the cash below nothing");
        // an old broadband company: its old districts at the old figures, and its crews no dearer than its old staff
        var n = new Network();
        BbRules.Found(n, 1);
        for (int x = 0; x < Network.Districts; x++)
        {
            BbRules.StartBuild(n, x, 1, 5, BbRules.BuildCost(x, 1000));
            n.Share[x, Network.Own] = 250;
        }
        BbRules.Finish(n, 30);
        BbRules.DayCosts(n, 1000, 30, 5, out int staff0, out int net0, out _);
        long takings0 = BbRules.DayTakings(n, 1000, 5, false, true);
        n.Scale = Full;
        for (int day = 31; day < 90; day++)
        {
            BbRules.Hire(n, day);
        }
        BbRules.DayCosts(n, 1000, 90, 5, out int staff1, out int net1, out _);
        Check(net1 == net0 && takings0 == BbRules.DayTakings(n, 1000, 5, false, true) && takings0 - staff1 - net1 > 0,
            "end scale: an old broadband company keeps its old districts' figures, and still earns more than it costs (staff " + staff0 + " then " + staff1 + ")");
        // an old bank at scale: it grows with the city and its capital is never taken below nothing by the scale
        BankCity c = BankCity.Quiet(ScaleYear);
        var b = new OwnBank();
        BankRules.Found(b, 1, 7, BankRules.CapitalLeast(1000), "Old");
        var day2 = new BankDay();
        for (int dd = 1; dd <= 300; dd++)
        {
            BankRules.Day(b, c, dd, null, day2);
        }
        long lentBefore = b.Lent;
        bool positive = true;
        for (int dd = 301; dd <= 600 && b.Running; dd++)
        {
            c.Scale = EndScaleRules.Share(true, 301, dd);
            BankRules.Day(b, c, dd, null, day2);
            positive &= b.Capital > 0 && BankRules.Balanced(b);
        }
        Check(b.Running && positive && b.Lent > lentBefore, "end scale: an old bank brought to scale stays open, its capital above nothing, and grows with the city");
        var poor = new OwnBank();
        BankRules.Found(poor, 1, 7, 1000, "Poor");
        c.Scale = Full;
        BankRules.Day(poor, c, 1, null, day2);
        Check(day2.Systems <= 1000 + day2.Income && poor.Capital >= 0, "end scale: a bank with almost no capital is not charged past it by the scale in a day");
    }

    // ---------------------------------------------------------------- the save lines

    private static void ScaleSaves()
    {
        var f = new InfraFile { Seed = 4, FirstDay = 2, LastDay = 50, ScaleFrom = 33 };
        DataCentre d = CentreAt(Full, 40, 4, 1);
        d.Place = "Dock%20St~4";
        d.Crew[0] = 7;
        d.Crew[1] = 3;
        d.Crew[2] = 5;
        d.Sqm += 10 * InfraCat.SqmPerRack;
        InfraRules.Buy(d, 10, false, 0);
        f.Centres.Add(d);
        f.Cloud.Open = true;
        f.Cloud.Ops = 41;
        int version;
        InfraFile back = InfraStore.Read(InfraStore.Write(f), out version);
        DataCentre b = back.Centres[0];
        Check(back.ScaleFrom == 33 && back.Cloud.Ops == 41 && b.Racks == 50 && b.Dense == 40 && b.DenseGpu == 4 && b.Crew[0] == 7 && b.Crew[1] == 3 && b.Crew[2] == 5,
            "end scale save: the data centres' scale day, racks of the new kind, crews and the cloud's operations come back (" + back.ScaleFrom + " " + back.Cloud.Ops + " " + b.Racks + " " + b.Dense + " " + b.DenseGpu + " " + b.Crew[0] + b.Crew[1] + b.Crew[2] + ")");
        string line = InfraStore.Write(new InfraFile { Seed = 4, ScaleFrom = 9, Owed = 123456789012L });
        string old = line.Replace("|9|123456789012~", "~");
        Check(InfraStore.Read(line, out _).Owed == 123456789012L && old != line && InfraStore.Read(old, out _).ScaleFrom == EndScaleRules.Unset && InfraStore.Read(old, out _).Owed == 0,
            "end scale save: what the data centres owe comes back; an older line has no scale day (so they are phased in) and owes nothing");
        var u = new UtilState();
        BbRules.Found(u.Network, 3);
        u.Network.ScaleFrom = 77;
        u.Network.Scaled[4] = true;
        u.Network.Crew[0] = 12;
        u.Network.Crew[2] = 18;
        u.Network.Owed = 3000000000L;
        u.Network.SalePrice = 4000000000L;
        string ul = UtilStore.Write(u);
        UtilState ub = UtilStore.Read(ul, out _);
        Check(ub.Network.ScaleFrom == 77 && ub.Network.Scaled[4] && !ub.Network.Scaled[3] && ub.Network.Crew[0] == 12 && ub.Network.Crew[2] == 18 && ub.Network.Owed == 3000000000L && ub.Network.SalePrice == 4000000000L,
            "end scale save: the broadband company's scale day, its districts at scale and its crews come back");
        int cut = ul.IndexOf("|77|", StringComparison.Ordinal);
        UtilState uo = UtilStore.Read(ul.Substring(0, cut), out _);
        Check(cut > 0 && uo != null && uo.Network.Running && uo.Network.ScaleFrom == EndScaleRules.Unset && !uo.Network.Scaled[4] && uo.Network.Crew[0] == 0,
            "end scale save: an older line has no scale day, no district at scale and no crews");
        var o = new OwnBank();
        BankRules.Found(o, 1, 7, 500000000L, "Saved");
        o.ScaleFrom = 61;
        o.Crew[BankCat.Officers] = 9;
        o.Crew[BankCat.Compliance] = 13;
        o.FoundedLeast = BankCat.CapitalLeastCentsScale;
        string bl = BankStore.Write(o);
        OwnBank ob = BankStore.Read(bl, out _);
        OwnBank oo = BankStore.Read(bl.Substring(0, bl.IndexOf("^e:", StringComparison.Ordinal)), out _);
        Check(ob.ScaleFrom == 61 && ob.Crew[BankCat.Officers] == 9 && ob.Crew[BankCat.Compliance] == 13 && ob.FoundedLeast == BankCat.CapitalLeastCentsScale && oo != null && oo.ScaleFrom == EndScaleRules.Unset
              && oo.Crew[BankCat.Officers] == 0 && oo.FoundedLeast == 0,
            "end scale save: the bank's scale day and crews come back; an older line has neither");
    }

    // ---------------------------------------------------------------- the texts

    private static readonly string[] ScaleKeys =
    {
        "reallife_endscale_enabled", "reallife_opt_tip_economy_endscale", "reallife_opt_sec_endscale", "reallife_endscale_test_phase", "reallife_endscale_test_full",
        "reallife_endscale_test_off", "reallife_endscale_test_none", "reallife_endscale_test_phase_done", "reallife_endscale_test_full_done",
        "reallife_meta_title_endscale", "reallife_guide_endscale_about", "reallife_guide_endscale", "reallife_endscale_note_phase",
        "reallife_endscale_what_infra", "reallife_endscale_what_broadband", "reallife_endscale_what_bank",
        "reallife_endscale_infra_crews", "reallife_endscale_infra_upkeep", "reallife_endscale_button_racks", "reallife_endscale_button_replace", "reallife_endscale_cloud_ops",
        "reallife_transaction_tech_upkeep", "reallife_transaction_tech_crew_wages",
        "reallife_endscale_bb_crews", "reallife_endscale_bb_crews_value", "reallife_endscale_bb_district_old", "reallife_endscale_bb_extend",
        "reallife_endscale_bb_confirm_extend_title", "reallife_endscale_bb_confirm_extend_body", "reallife_endscale_bb_extended",
        "reallife_endscale_ob_crews", "reallife_endscale_ob_crews_value", "reallife_transaction_ownbank_systems", "reallife_endscale_note_owed", "reallife_endscale_owed_line"
    };

    private static void ScaleTexts()
    {
        string folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Locales"));
        Dictionary<string, string> en = LoadWords(System.IO.Path.Combine(folder, "en.json")), pt = LoadWords(System.IO.Path.Combine(folder, "pt.json")),
            zh = LoadWords(System.IO.Path.Combine(folder, "zh-cn.json"));
        var missing = new List<string>();
        foreach (string key in ScaleKeys)
        {
            string e, p, z;
            if (!en.TryGetValue(key, out e) || !pt.TryGetValue(key, out p) || !zh.TryGetValue(key, out z) || e.Length == 0 || p.Length == 0 || z.Length == 0 || !HasHan(z))
            {
                missing.Add(key);
            }
        }
        Check(missing.Count == 0, "end scale: every new text is in English, Portuguese and Chinese" + (missing.Count > 0 ? " (missing: " + string.Join(", ", missing) + ")" : string.Empty));
        string note;
        Check(en.TryGetValue("reallife_endscale_note_phase", out note) && note.Length <= 240 && note.Contains("{what}") && note.Contains("{days}") && pt.TryGetValue("reallife_endscale_note_phase", out string pn) && pn.Contains("{what}")
              && zh.TryGetValue("reallife_endscale_note_phase", out string zn) && zn.Contains("{days}"),
            "end scale: the phase-in notice is short and names the venture and the days in every language");
        string page;
        bool facts = en.TryGetValue("reallife_guide_endscale", out page);
        Check(en.TryGetValue("reallife_endscale_button_racks", out string racks) && racks.Contains("{count}") && pt["reallife_endscale_button_racks"].Contains("{count}") && zh["reallife_endscale_button_racks"].Contains("{count}")
              && en["reallife_endscale_note_owed"].Length <= 240 && en["reallife_endscale_note_owed"].Contains("{owed}"),
            "end scale: the racks button counts from the code, and the owed notice is short and names the sum");
        foreach (string fact in new[] { "{es_days}", "{es_years_least}", "{es_years_most}", "{es_tech_days_least}", "{es_tech_days_most}", "{es_colo}", "{es_rack}", "{es_bb_build}", "{es_ob_licence}" })
        {
            facts &= page != null && page.Contains(fact);
        }
        Check(facts, "end scale: the Guide's page gives the scale's figures from the code");
        string next;
        Check(en.TryGetValue("reallife_changes_next", out next) && next.Contains("scale") && !next.StartsWith("Fixed: ", StringComparison.Ordinal), "end scale: What's new names the end game at scale");
    }

    private static bool HasHan(string text)
    {
        foreach (char ch in text)
        {
            if (ch >= '一' && ch <= '鿿')
            {
                return true;
            }
        }
        return false;
    }

    // ---------------------------------------------------------------- the sims

    private struct ScaleRun
    {
        public long Capex;        // what was put in, cents
        public int Payback;       // the day the cash came back to nothing; -1 never within the days
        public long Takings, Net; // a day's at the end, cents
        public int Crew, Staff;   // the crews and the professionals (or, for the bank, its crews) at the end
    }

    // Data centres sold through the cloud: so many racks and AI racks over so many districts, fitted at a tier, built on
    // day 0, the cloud matching the rivals' price, every professional in place (their wages counted), racks let to the
    // city's companies as the director lets them (a rack a day towards what the city wants, the old kind first, some
    // leaving each month). Bought at the whole scale while the option is on, as the director buys.
    private static ScaleRun CloudSim(int share, int racks, int gpu, int regions, int tier, bool hyper, int days, int bought = -1)
    {
        int at = bought < 0 ? EndScaleRules.Buying(share > 0) : bought;   // the share the data centres were fitted and their racks bought at (0: an old save's)
        int wanted = InfraRules.ColoWanted(1, share);
        var centres = new List<DataCentre>(regions);
        long capex = 0;
        for (int i = 0; i < regions; i++)
        {
            int r = racks / regions + (i < racks % regions ? 1 : 0), g = gpu / regions + (i < gpu % regions ? 1 : 0);
            var d = new DataCentre { Place = "p" + i, District = "d" + i, Tier = tier, Sqm = (r + 2 * g) * InfraCat.SqmPerRack, Technicians = 1000 };
            capex += InfraRules.FitCost(d.Sqm, tier, at);
            InfraRules.Buy(d, r, false, at);
            if (g > 0)
            {
                InfraRules.Buy(d, g, true, at);
            }
            capex += d.Book;
            centres.Add(d);
        }
        var c = new Cloud { Open = true, Hyper = hyper };
        var run = new ScaleRun { Capex = capex, Payback = -1 };
        long cash = -capex;
        for (int day = 1; day <= days; day++)
        {
            int capacity = 0, gpuUnits = 0, outages = 0, staff = 0, crew = 0, let = 0;
            long costs = 0, rent = 0;
            for (int i = 0; i < centres.Count; i++)
            {
                let += centres[i].Colo;
            }
            for (int i = 0; i < centres.Count; i++)
            {
                DataCentre d = centres[i];
                InfraRules.Hire(d, share);
                int cover = InfraRules.Cover(d, share), working = EndScaleRules.Working(cover);
                d.Technicians = InfraRules.TechniciansNeeded(d);
                bool down = InfraRules.Outage(23, d, day, cover);
                if (!down && let < wanted && d.Racks - d.Colo > d.Racks / 2)
                {
                    d.Colo++;
                    let++;
                }
                else if (d.Colo > 0 && (let > wanted || RealLifeOverhaul.Tech.TechRandom.Mille(23, i, day, 9) < InfraCat.ColoChurn / RealLifeOverhaul.Tech.FirmsCatalogue.MonthDays + (down ? 300 : 0)))
                {
                    d.Colo--;
                    let--;
                }
                if (down)
                {
                    outages++;
                }
                else
                {
                    rent += InfraRules.ColoDay(d, share);
                    capacity += (int)((long)InfraRules.Units(d, share) * working / 1000);
                    gpuUnits += (int)((long)InfraRules.GpuUnits(d, share) * working / 1000);
                }
                costs += UtilRules.DayCents(UtilService.Power, InfraRules.Power(d, share), PriceRules.Start, false) + UtilRules.DayCents(UtilService.Water, InfraRules.Water(d), PriceRules.Start, false)
                         + InfraRules.Upkeep(d, share) + InfraRules.Crews(d) + d.Technicians * ProWageTech;
                staff += d.Technicians;
                for (int k = 0; k < InfraCat.CrewKinds; k++)
                {
                    crew += d.Crew[k];
                }
            }
            int ops = InfraRules.OpsNeed((long)capacity + gpuUnits, share);
            c.Ops = EndScaleRules.Hire(c.Ops, ops, InfraCat.CrewPace);
            int cloudWorking = EndScaleRules.Working(EndScaleRules.Cover(c.Ops, ops));
            capacity = (int)((long)capacity * cloudWorking / 1000);
            gpuUnits = (int)((long)gpuUnits * cloudWorking / 1000);
            int engineers = InfraCat.CloudEngineersLeast + (hyper ? 3 : 0);   // site reliability people, and a CTO and two staff engineers for a hyperscaler
            costs += (long)c.Ops * InfraCat.OpsWage + engineers * ProWageEngineer + InfraRules.Licence(share) / ScaleYear;
            staff += engineers;
            crew += c.Ops;
            c.Price = c.RivalPrice;
            int city = InfraRules.CityDemand(c, 1, capacity, false, share, regions);
            int cityGpu = InfraRules.CityDemand(c, 1, gpuUnits, true, share, regions);
            long takings = InfraRules.Takings(c, city, cityGpu) + rent;
            InfraRules.Reputation(c, outages);
            InfraRules.PriceWar(c, city, day, InfraRules.WarMarket(c, share, regions));
            cash += takings - costs;
            if (run.Payback < 0 && cash >= 0)
            {
                run.Payback = day;
            }
            run.Takings = takings;
            run.Net = takings - costs;
            run.Crew = crew;
            run.Staff = staff;
        }
        return run;
    }

    // The broadband company founded on day 0 with every district dug at once, its prices, maintenance and marketing as
    // usual, at the start's prices in a year of so many days (outages left out: a day's bills in a hundred).
    private static ScaleRun BroadbandSim(int share, int year, int days, long[] cashYears = null, long[] saleYears = null)
    {
        int m = MacroCatalogue.MonthDays(year), buy = EndScaleRules.Buying(share > 0);
        var calm = new[] { 1000, 1000, 1000, 1000 };
        var n = new Network();
        BbRules.Found(n, 0);
        n.Scale = share;
        long capex = BbRules.LicenceFee(PriceRules.Start, buy);
        for (int d = 0; d < Network.Districts; d++)
        {
            int cost = BbRules.BuildCost(d, PriceRules.Start, buy);
            BbRules.StartBuild(n, d, 0, m, cost, buy > 0);
            capex += cost;
        }
        var run = new ScaleRun { Capex = capex * 100, Payback = -1 };
        long cash = -capex * 100;
        for (int day = 1; day <= days; day++)
        {
            BbRules.Finish(n, day);
            BbRules.Hire(n, day);
            long takings = BbRules.DayTakings(n, PriceRules.Start, m, false, true);
            BbRules.DayCosts(n, PriceRules.Start, day, m, out int staff, out int network, out int marketing);
            long costs = staff + network + marketing;
            if (day % m == 0)
            {
                int before = BbRules.Connections(n);
                BbRules.Month(n, calm, day, m);
                costs += BbRules.Installs(before, BbRules.Connections(n), PriceRules.Start, share);
            }
            n.MonthTakings = (int)Math.Min(int.MaxValue / 4, n.MonthTakings + takings);
            n.MonthCosts = (int)Math.Min(int.MaxValue / 4, n.MonthCosts + costs);
            if (day % m == 0)
            {
                BbRules.CloseMonth(n, BbRules.Connections(n), 0);
            }
            cash += (takings - costs) * 100;
            if (cashYears != null && day % year == 0 && day / year < cashYears.Length)
            {
                cashYears[day / year] = cash;
                saleYears[day / year] = BbRules.SaleValue(n) * 100;
            }
            if (run.Payback < 0 && cash >= 0)
            {
                run.Payback = day;
            }
            run.Takings = takings * 100;
            run.Net = (takings - costs) * 100;
            run.Crew = n.Crew[0] + n.Crew[1] + n.Crew[2];
            run.Staff = BbRules.Connections(n);
        }
        return run;
    }

    // The player's bank founded with the least capital (and more, if asked) in a quiet city of a 60-day year; its payback
    // is the day what it kept in its capital came to its licence (the capital is the bank's and can be taken out again).
    private static ScaleRun BankSim(int share, long extraCapital, int days, out long yearProfit, out long capital, int year = ScaleYear)
    {
        BankCity c = BankCity.Quiet(year);
        c.Scale = share;
        int buy = EndScaleRules.Buying(share > 0);
        var b = new OwnBank();
        long put = BankRules.CapitalLeast(c.Level, 0, buy) + extraCapital;
        BankRules.Found(b, 1, 7, put, "Scale Savings");
        b.FoundedLeast = BankRules.LeastAt(buy);
        var day = new BankDay();
        long licence = BankRules.Licence(c.Level, buy);
        var run = new ScaleRun { Capex = licence + put, Payback = -1 };
        for (int d = 1; d <= days && b.Running; d++)
        {
            BankRules.Day(b, c, d, null, day);
            if (run.Payback < 0 && b.Capital - put >= licence)
            {
                run.Payback = d;
            }
            run.Takings = day.Income;
            run.Net = day.Profit;
        }
        for (int k = 0; k < BankCat.CrewKinds; k++)
        {
            run.Crew += b.Crew[k];
        }
        yearProfit = BankRules.YearProfit(b);
        capital = b.Capital;
        return run;
    }

    // ---------------------------------------------------------------- the table

    private static void EndScaleTable(bool print)
    {
        if (!print)
        {
            return;
        }
        Console.WriteLine("END GAME AT SCALE: venture | before: put in, payback (days, 60-day years), net a day | after: put in, payback, net a day, crews");
        Row("Cloud, 60 racks and 4 AI racks, one region", CloudSim(0, 60, 4, 1, 2, false, 4000), CloudSim(Full, 60, 4, 1, 2, false, 4000));
        Row("Cloud, 120 racks and 10 AI racks, one region", CloudSim(0, 120, 10, 1, 2, false, 4000), CloudSim(Full, 120, 10, 1, 2, false, 4000));
        Row("Hyperscaler, 400 racks and 24 AI racks, three regions", CloudSim(0, 400, 24, 3, 2, true, 4000), CloudSim(Full, 400, 24, 3, 2, true, 4000));
        Row("Hyperscaler, 450 racks and 40 AI racks, three regions", CloudSim(0, 450, 40, 3, 2, true, 4000), CloudSim(Full, 450, 40, 3, 2, true, 4000));
        Row("Hyperscaler, 700 racks and 60 AI racks, five regions", CloudSim(0, 700, 60, 5, 2, true, 4000), CloudSim(Full, 700, 60, 5, 2, true, 4000));
        Row("Hyperscaler, 1100 racks and 100 AI racks, seven regions", CloudSim(0, 1100, 100, 7, 2, true, 4000), CloudSim(Full, 1100, 100, 7, 2, true, 4000));
        Row("Broadband, every district (60-day year)", BroadbandSim(0, 60, 4000), BroadbandSim(Full, 60, 4000));
        Row("Broadband, every district (365-day year)", BroadbandSim(0, 365, 6000), BroadbandSim(Full, 365, 6000));
        Row("Own bank, the least capital (365-day year)", BankSim(0, 0, 6000, out _, out _, 365), BankSim(Full, 0, 6000, out _, out _, 365));
        Row("Own bank, the least capital", BankSim(0, 0, 3000, out _, out _), BankSim(Full, 0, 3000, out _, out _));
        Row("Own bank, the least capital and $100M more", BankSim(0, 10000000000L, 3000, out _, out _), BankSim(Full, 10000000000L, 3000, out _, out _));
    }

    private static void Row(string what, ScaleRun before, ScaleRun after)
    {
        Console.WriteLine("  " + what + " | " + Money(before.Capex) + ", " + Days(before.Payback) + ", " + Money(before.Net) + " | " + Money(after.Capex) + ", " + Days(after.Payback) + ", "
                          + Money(after.Net) + ", crews " + after.Crew + ", staff " + after.Staff);
    }

    private static string Money(long cents) => "$" + (cents / 100).ToString("N0", System.Globalization.CultureInfo.InvariantCulture);

    private static string Days(int days) => days < 0 ? "never" : days + " days (" + (days / (double)ScaleYear).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " y)";
}
