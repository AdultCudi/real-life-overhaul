using System;
using System.Collections.Generic;
using RealLifeOverhaul.Tech;

// Checks for data centres, the cloud and AI compute (Scripts/Tech/Infra): fitting out and racks, power, water and
// writing down, technicians and outages, racks let, the cloud's sales against the rivals and their price war, the steps
// to a cloud and a hyperscaler, an AI model's training, a year of a data centre's money, and the save line.
internal static partial class Program
{
    private static void Infra2()
    {
        InfraBuild();
        InfraOutages();
        InfraCloud();
        InfraSteps();
        InfraTraining();
        InfraYear();
        InfraSave();
    }

    private static DataCentre Centre(int racks, int gpu, int tier, string district = "a")
    {
        var d = new DataCentre { Place = "p" + racks + district, District = district, Tier = tier, Sqm = 6000 };
        InfraRules.Buy(d, racks, false);
        if (gpu > 0)
        {
            InfraRules.Buy(d, gpu, true);
        }
        return d;
    }

    private static void InfraBuild()
    {
        Check(InfraRules.FitCost(1000, 3) > InfraRules.FitCost(1000, 0) * 4, "infra: a tier IV fit-out costs far more than a tier I");
        Check(InfraRules.UpgradeCost(1000, 1, 2) == InfraRules.FitCost(1000, 2) - InfraRules.FitCost(1000, 1) && InfraRules.UpgradeCost(1000, 2, 1) == 0, "infra: raising a tier costs the difference, never less than nothing");
        var d = new DataCentre { Sqm = 120 };
        Check(d.RacksRoom == 20 && InfraRules.Buy(d, 19, false) && !InfraRules.Buy(d, 1, true) && InfraRules.Buy(d, 1, false) && InfraRules.Room(d) == 0, "infra: racks fit the floor, an AI rack takes two racks' room");
        Check(d.Book == 20L * InfraCatalogue.RackCost, "infra: racks bought go into the books at cost");
        long down = InfraRules.WriteDown(d);
        Check(Math.Abs(down * InfraCatalogue.WriteDownDays - d.Book) <= InfraCatalogue.WriteDownDays, "infra: racks are written down over five years");
        d.Book = 10;
        Check(InfraRules.WriteDown(d) == 10, "infra: never written down below nothing");
        DataCentre c = Centre(10, 1, 1);
        Check(InfraRules.Power(c) == (10L * InfraCatalogue.RackKw + InfraCatalogue.GpuRackKw) * 24 * 1000, "infra: power is the racks' kW the day round");
        long kwhDay = InfraRules.Power(Centre(1, 0, 1)) / 1000;
        long powerCents = kwhDay * 15;   // (a plain 15 cent tariff)
        Check(powerCents > 1500 && powerCents < InfraCatalogue.ColoMonth / FirmsCatalogue.MonthDays, "infra: a rack's power costs less than its rent (" + powerCents + " cents a day)");
        Check(InfraRules.Water(c) > 0 && InfraRules.Water(c) == InfraRules.Power(c) / 1000 * InfraCatalogue.WaterPerMwh / 1000, "infra: cooling water follows the power");
        Check(InfraRules.TechniciansNeeded(new DataCentre()) == 0 && InfraRules.TechniciansNeeded(Centre(1, 0, 0)) == 1 && InfraRules.TechniciansNeeded(Centre(41, 0, 0)) == 3, "infra: a technician for every twenty racks, one at the least");
        Check(InfraRules.Units(c) == 10 * InfraCatalogue.UnitsPerRack && InfraRules.GpuUnits(c) == InfraCatalogue.GpuUnitsPerRack, "infra: racks sell as units of compute");
        c.Colo = 4;
        Check(InfraRules.Units(c) == 6 * InfraCatalogue.UnitsPerRack, "infra: racks let are not sold as compute too");
        Check(InfraRules.ColoWanted(0) > InfraRules.ColoWanted(1) && InfraRules.ColoWanted(1) > InfraRules.ColoWanted(3), "infra: companies rent more racks in a boom");
    }

    private static int Outages(DataCentre d, int days)
    {
        int n = 0;
        for (int day = 0; day < days; day++)
        {
            n += InfraRules.Outage(11, d, day) ? 1 : 0;
        }
        return n;
    }

    private static void InfraOutages()
    {
        DataCentre low = Centre(20, 0, 0, "x"), high = Centre(20, 0, 3, "x");
        low.Technicians = high.Technicians = 1;
        int lo = Outages(low, 4000), hi = Outages(high, 4000);
        Check(lo > hi * 5 && lo > 40 && lo < 140, "infra: a tier I data centre goes down far more than a tier IV (" + lo + " to " + hi + " in 4000 days)");
        low.Technicians = 0;
        Check(Outages(low, 4000) > lo * 2, "infra: short of technicians it goes down more often");
        high.Power = false;
        Check(InfraRules.Outage(11, high, 5), "infra: with its power cut it is down");
        high.Power = true;
        high.Net = false;
        Check(InfraRules.Outage(11, high, 5), "infra: with its connection cut it is down");
        Check(!InfraRules.Outage(11, new DataCentre { Power = false }, 5), "infra: an empty data centre has nothing to go down");
    }

    private static void InfraCloud()
    {
        var c = new Cloud();
        int fair = InfraRules.CityDemand(c, 1, int.MaxValue, false);
        c.Price = 1300;
        int dear = InfraRules.CityDemand(c, 1, int.MaxValue, false);
        c.Price = 700;
        int cheap = InfraRules.CityDemand(c, 1, int.MaxValue, false);
        Check(cheap > fair && fair > dear && dear > 0, "infra: the cheaper the cloud, the more the city buys (" + cheap + ", " + fair + ", " + dear + ")");
        c.Price = 1000;
        Check(fair < InfraCatalogue.CloudDemand, "infra: the rival clouds keep part of the city");
        Check(InfraRules.CityDemand(c, 1, 100, false) == 100, "infra: the cloud sells no more than it has");
        Check(InfraRules.CityDemand(c, 0, int.MaxValue, false) > InfraRules.CityDemand(c, 3, int.MaxValue, false), "infra: the city buys more compute in a boom");
        int rep = InfraRules.CityDemand(c, 1, int.MaxValue, false);
        c.Reputation = 100;
        Check(InfraRules.CityDemand(c, 1, int.MaxValue, false) < rep, "infra: a cloud that keeps going down sells less");
        c.Reputation = 600;
        c.Hyper = true;
        Check(InfraRules.CityDemand(c, 1, int.MaxValue, false) > rep * 2, "infra: a hyperscaler sells to the city's large companies too");
        c.Hyper = false;
        Check(InfraRules.Takings(c, 100, 0) == 100L * InfraCatalogue.UnitDay && InfraRules.Takings(c, 0, 10) == 10L * InfraCatalogue.GpuUnitDay, "infra: compute sells at the cloud's price");
        // a rack as cloud brings more than let
        long asCloud = (long)InfraCatalogue.UnitsPerRack * InfraCatalogue.UnitDay, asColo = InfraCatalogue.ColoMonth / FirmsCatalogue.MonthDays;
        Check(asCloud > asColo, "infra: a rack sold as cloud brings more than one let (" + asCloud + " to " + asColo + " cents a day)");
        Check((long)InfraCatalogue.UnitDay * InfraCatalogue.OwnHostingShare / 1000 < InfraCatalogue.UnitDay, "infra: the player's own firms host on their own cloud for less");
        // the price war
        var w = new Cloud();
        int cuts = 0;
        for (int day = 30; day < 30 * 30; day++)
        {
            cuts += InfraRules.PriceWar(w, InfraCatalogue.CloudDemand / 2, day) ? 1 : 0;
        }
        Check(cuts == (1000 - InfraCatalogue.PriceFloor) / InfraCatalogue.PriceCut && w.RivalPrice == InfraCatalogue.PriceFloor, "infra: holding half the city, the rivals cut a twentieth a month down to their floor (" + cuts + " cuts)");
        int war = w.RivalPrice;
        for (int day = 900; day < 900 + 30 * 20; day++)
        {
            InfraRules.PriceWar(w, 0, day);
        }
        Check(w.RivalPrice > war && w.RivalPrice <= 1000, "infra: the rivals let prices back up once the cloud is small again");
        var q = new Cloud();
        Check(!InfraRules.PriceWar(q, InfraCatalogue.CloudDemand / 10, 60) && q.RivalPrice == 1000, "infra: a small cloud starts no price war");
        var lower = new Cloud { RivalPrice = 600 };
        Check(InfraRules.CityDemand(lower, 1, int.MaxValue, false) < InfraRules.CityDemand(new Cloud(), 1, int.MaxValue, false), "infra: a price war takes the cloud's customers");
        var r = new Cloud();
        InfraRules.Reputation(r, 0);
        int up = r.Reputation;
        InfraRules.Reputation(r, 2);
        Check(up > 600 && r.Reputation < 600 - 100, "infra: reputation creeps up and falls with each outage");
    }

    private static void InfraSteps()
    {
        var none = new List<DataCentre>();
        Check(InfraRules.CloudNeeds(none, 0, 0, false) == 31, "infra: a cloud with nothing needs everything");
        var one = new List<DataCentre> { Centre(20, 0, 1) };
        Check(InfraRules.CloudNeeds(one, 2, InfraCatalogue.CloudCashLeast, true) == 0, "infra: twenty racks at tier II, two engineers, the cash and a connection open a cloud");
        Check(InfraRules.CloudNeeds(new List<DataCentre> { Centre(20, 0, 0) }, 2, InfraCatalogue.CloudCashLeast, true) == 2, "infra: a tier I data centre is not enough for a cloud");
        Check(InfraRules.CloudNeeds(one, 1, InfraCatalogue.CloudCashLeast, true) == 4 && InfraRules.CloudNeeds(one, 2, 0, true) == 8 && InfraRules.CloudNeeds(one, 2, InfraCatalogue.CloudCashLeast, false) == 16, "infra: each missing thing is named");
        var three = new List<DataCentre> { Centre(100, 0, 2, "a"), Centre(100, 0, 2, "b"), Centre(100, 0, 2, "c") };
        Check(InfraRules.Regions(three) == 3 && InfraRules.HyperNeeds(three, true, InfraCatalogue.HyperCash) == 0, "infra: three districts, three hundred racks, the chiefs and the cash make a hyperscaler");
        var two = new List<DataCentre> { Centre(150, 0, 2, "a"), Centre(150, 0, 2, "a"), Centre(10, 0, 2, "b") };
        Check(InfraRules.Regions(two) == 2 && (InfraRules.HyperNeeds(two, true, InfraCatalogue.HyperCash) & 1) == 1, "infra: two data centres in one district are one region");
        Check((InfraRules.HyperNeeds(three, false, InfraCatalogue.HyperCash) & 4) == 4 && (InfraRules.HyperNeeds(three, true, 0) & 8) == 8, "infra: a hyperscaler needs its chiefs and its cash");
        Check(InfraCatalogue.HyperCash > InfraCatalogue.CloudCashLeast && InfraCatalogue.HyperRacks > InfraCatalogue.CloudRacksLeast, "infra: each step up asks more");
    }

    private static void InfraTraining()
    {
        Check(InfraRules.TrainNeed(0) == InfraCatalogue.TrainBase && InfraRules.TrainNeed(2) == InfraCatalogue.TrainBase * 2 && InfraRules.TrainNeed(4) == InfraCatalogue.TrainBase * 4, "infra: each generation of a model takes twice the training");
        Check(InfraRules.TrainNeed(1000) > 0, "infra: training need never overflows");
        Check(InfraRules.TrainLevels[0] == 0 && InfraRules.TrainLevels[4] > InfraRules.TrainLevels[1], "infra: training levels rise from none");
        int days = (int)(InfraRules.TrainNeed(0) / InfraRules.TrainLevels[2]);
        Check(days >= 4 && days <= 30, "infra: the first generation trains in days at a middle level (" + days + ")");
        Check(InfraRules.Inference(new Product { Users = 1000, Free = 20000 }) > InfraRules.Inference(new Product { Users = 1000 }), "infra: free users use compute too");
        Check(InfraRules.Inference(new Product { Users = 0 }) == 0, "infra: a model with no customers runs on nothing");
        // a model's engineering waits for its training
        var lab = new Firm();
        var crew = new Crew { Engineering = 100, SeniorShare = 500, Faults = 100 };
        Product p = FirmsRules.NewProduct(new FirmsFile { Seed = 2 }, "x", ProductKind.Model, Segment.Business, 1, "m");
        p.Progress = (int)FirmsRules.FeatureCost(0) * 3;
        Check(p.Train == 2 && FirmsRules.Build(lab, p, crew, 0, 0) == 0 && p.Features == 0, "infra: a model's engineering waits for its training");
        p.Compute = InfraRules.TrainNeed(0);
        Check(FirmsRules.Build(lab, p, crew, 0, 0) == 1 && p.Compute == 0, "infra: a generation's training spent finishes its feature");
        Product app = FirmsRules.NewProduct(new FirmsFile { Seed = 2 }, "x", ProductKind.Saas, Segment.Business, 1, "s");
        app.Progress = (int)FirmsRules.FeatureCost(0);
        Check(FirmsRules.Build(lab, app, crew, 0, 0) == 1, "infra: other products need no training");
        long rentedDay = (long)InfraRules.TrainLevels[2] * InfraCatalogue.GpuUnitDay * InfraCatalogue.RentedGpuShare / 1000;
        long ownDay = (long)InfraRules.TrainLevels[2] / InfraCatalogue.GpuUnitsPerRack * InfraCatalogue.GpuRackKw * 24 * 15;
        Check(rentedDay > ownDay * 5, "infra: rented AI compute costs far more than the power of one's own (" + rentedDay + " to " + ownDay + ")");
    }

    // A year of a twenty-rack, tier II data centre with a technician, let and sold as cloud, at a plain tariff.
    private static void InfraYear()
    {
        DataCentre d = Centre(20, 0, 1);
        d.Technicians = 1;
        var c = new Cloud { Open = true };
        long takings = 0, costs = 0, down = 0;
        int outs = 0;
        for (int day = 1; day <= 365; day++)
        {
            bool o = InfraRules.Outage(17, d, day);
            outs += o ? 1 : 0;
            InfraRules.Reputation(c, o ? 1 : 0);
            int sold = o ? 0 : InfraRules.CityDemand(c, 1, InfraRules.Units(d), false);
            InfraRules.PriceWar(c, sold, day);
            long t = InfraRules.Takings(c, sold, 0);
            takings += t;
            InfraRules.Roll(d, day, t);
            costs += InfraRules.Power(d) / 1000 * 15 + InfraRules.Water(d) * 200 / 1000 + InfraCatalogue.LicenceFee / 365;
            long w = InfraRules.WriteDown(d);
            down += w;
            d.Book -= w;
        }
        long fit = InfraRules.FitCost(d.Sqm, 1);
        Check(takings > costs + down, "infra: a staffed cloud data centre pays its power, water, licence and writing down (" + takings / 100 + " to " + (costs + down) / 100 + " a year)");
        Check(takings - costs - down < (fit + 20L * InfraCatalogue.RackCost) / 2, "infra: it takes years to earn back the building (" + (takings - costs - down) / 100 + " a year)");
        Check(outs < 30, "infra: a tier II data centre goes down a few days a year (" + outs + ")");
        long sum = 0;
        for (int i = 0; i < d.Days.Length; i++)
        {
            sum += d.Days[i];
        }
        Check(sum == d.Takings30 && d.Takings30 > 0, "infra: the last thirty days are rolled");
    }

    private static void InfraSave()
    {
        var f = new InfraFile { Seed = 9, FirstDay = 3, LastDay = 40 };
        DataCentre d = Centre(30, 2, 2, "Midtown East");
        d.Place = "Main%20St~12";
        d.Colo = 3;
        d.Outages = 2;
        d.DownUntil = 41;
        InfraRules.Roll(d, 5, 12345);
        f.Centres.Add(d);
        f.Cloud.Open = true;
        f.Cloud.Name = "North Sky Cloud";
        f.Cloud.Price = 850;
        f.Cloud.RivalPrice = 900;
        f.Cloud.LicenceUntil = 400;
        f.Cloud.Hyper = true;
        f.Cloud.Sold = 900;
        int version;
        InfraFile back = InfraStore.Read(InfraStore.Write(f), out version);
        DataCentre b = back.Centres[0];
        Check(version == InfraStore.Version && back.Seed == 9 && back.FirstDay == 3 && back.LastDay == 40, "infra save: the header comes back");
        Check(b.Place == d.Place && b.District == d.District && b.Tier == 2 && b.Racks == 30 && b.GpuRacks == 2 && b.Book == d.Book && b.Colo == 3 && b.Outages == 2 && b.DownUntil == 41, "infra save: a data centre comes back");
        Check(b.Takings30 == 12345 && b.Days[5] == 12345, "infra save: its thirty days come back");
        Check(back.Cloud.Open && back.Cloud.Name == f.Cloud.Name && back.Cloud.Price == 850 && back.Cloud.RivalPrice == 900 && back.Cloud.LicenceUntil == 400 && back.Cloud.Hyper && back.Cloud.Sold == 900, "infra save: the cloud comes back");
        var data = new Dictionary<string, string> { { InfraStore.Key, "nonsense" } };
        Check(InfraStore.Load(data, out version) == null || InfraStore.Load(data, out version).Centres.Count == 0, "infra save: a damaged line is not loaded as data centres");
        Check(InfraStore.Load(new Dictionary<string, string>(), out version) == null, "infra save: no line, no file");
    }
}
