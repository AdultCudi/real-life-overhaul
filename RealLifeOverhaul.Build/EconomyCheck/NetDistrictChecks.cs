using System;
using System.Collections.Generic;
using RealLifeOverhaul;
using RealLifeOverhaul.Uptown;
using RealLifeOverhaul.Utilities;

// The player's own broadband network in every district of the city (Utilities/BroadbandCatalogue.cs, BroadbandRules.cs, BroadbandCompany.cs, UtilitiesStore.cs): the mod's own two districts,
// Uptown and Cedar Heights, in the catalogue after New York's seven; a district a free update adds given a place of its own, sized by the homes and businesses counted there and priced by
// its distance, kept in the save; the same dig, cost and time rules; the customers the rivals' shares leave it; and the player's own company's line for a data centre in a district it has dug,
// priced so that the carriers' lines still mean something. Each source check is also run on a made-up wrong case, which must fail.
internal static partial class Program
{
    private static void NetDistricts()
    {
        NetDistrictCatalogue();
        NetDistrictFigures();
        NetDistrictLater();
        NetDistrictMarket();
        NetDistrictSaves();
        NetOwnLine();
        NetDistrictSources();
    }

    private const string NetLater = "ba:neighborhood_uppereastside";

    private static void NetDistrictCatalogue()
    {
        int n = BroadbandCatalogue.Districts.Length;
        Check(n == Network.Districts && BroadbandCatalogue.Market.Length == n && BroadbandCatalogue.BuildCost.Length == n && BroadbandCatalogue.Far.Length == n
              && BroadbandCatalogue.MiddleX.Length == n && BroadbandCatalogue.MiddleZ.Length == n, "net districts: a figure for each of the " + n + " districts of the catalogue");
        Check(BroadbandCatalogue.Legacy == 7 && Network.Legacy == 7 && Network.Slots == n + BroadbandCatalogue.ExtraMax, "net districts: the seven a save before the mod's two kept by place, and room for " + BroadbandCatalogue.ExtraMax + " later ones");
        bool seven = true;
        for (int d = 0; d < 7; d++)
        {
            seven &= BroadbandCatalogue.Districts[d] == RealLifeOverhaul.CityDistricts.NewYork[d];
        }
        Check(seven && BroadbandCatalogue.Districts[7] == DistrictSplit.UptownId && BroadbandCatalogue.Districts[8] == DistrictSplit.CedarId,
            "net districts: New York's seven keep their places, Uptown and Cedar Heights come after them");
        bool ids = true;
        for (int d = 0; d < n; d++)
        {
            ids &= BroadbandRules.District(BroadbandCatalogue.Districts[d]) == d;
        }
        Check(ids && BroadbandRules.District(NetLater) == -1, "net districts: every catalogued district is found by its id, a later one is not in the catalogue");
        // the usual cost per connection, as the first seven bear it to their markets: $80, and the Hamptons' houses standing far apart
        bool usual = true;
        for (int d = 0; d < n; d++)
        {
            long perConnection = BroadbandCatalogue.BuildCost[d] * 100L / BroadbandCatalogue.Market[d];   // cents
            usual &= d == 6 ? perConnection > BroadbandCatalogue.BuildPerConnection * 4 : Math.Abs(perConnection - BroadbandCatalogue.BuildPerConnection) <= BroadbandCatalogue.BuildPerConnection / 24;
        }
        Check(usual, "net districts: each district costs $80 a connection to dig within a twenty fourth (Industry City's $83), the Hamptons five times as much, Uptown and Cedar Heights the usual");
        Check(BroadbandCatalogue.BuildCost[7] == BroadbandCatalogue.Market[7] * 80 && BroadbandCatalogue.BuildCost[8] == BroadbandCatalogue.Market[8] * 80,
            "net districts: Uptown's and Cedar Heights' fibre is their connections at the usual cost each");
        // the mod's two are sized by the population: ten connections to a household, from the band's flats
        Check(BroadbandCatalogue.Market[7] == 34000 && BroadbandCatalogue.Market[8] == 37000 && BroadbandCatalogue.PerHousehold == 10,
            "net districts: Uptown's 34,000 and Cedar Heights' 37,000 connections are ten for each household of their 106 and 116 home buildings' flats");
        // the distance rule: nothing over NearMetres from a catalogued district is dear, the usual cost for the districts the catalogue lists but the Hamptons
        bool near = true;
        for (int d = 0; d < n; d++)
        {
            double best = double.MaxValue;
            for (int e = 0; e < n; e++)
            {
                if (e != d)
                {
                    double dx = BroadbandCatalogue.MiddleX[d] - BroadbandCatalogue.MiddleX[e], dz = BroadbandCatalogue.MiddleZ[d] - BroadbandCatalogue.MiddleZ[e];
                    best = Math.Min(best, Math.Sqrt(dx * dx + dz * dz));
                }
            }
            near &= d == 6 || best <= BroadbandCatalogue.NearMetres;   // the Hamptons are dear by their houses, not by the road
        }
        Check(near, "net districts: every district of the catalogue but the Hamptons lies within " + BroadbandCatalogue.NearMetres + " m of another, so the usual cost per connection is the rule near a district");
        Check(BroadbandRules.FarShare(0) == 1000 && BroadbandRules.FarShare(BroadbandCatalogue.NearMetres) == 1000 && BroadbandRules.FarShare(BroadbandCatalogue.FarMetres) == BroadbandCatalogue.FarMost
              && BroadbandRules.FarShare((BroadbandCatalogue.NearMetres + BroadbandCatalogue.FarMetres) / 2) == (1000 + BroadbandCatalogue.FarMost) / 2 && BroadbandRules.FarShare(50000) == BroadbandCatalogue.FarMost,
            "net districts: the distance rule is the usual cost near, the Hamptons' three times far, in a line between");
        bool rises = true;
        for (int m = 1; m < 4000; m += 37)
        {
            rises &= BroadbandRules.FarShare(m) >= BroadbandRules.FarShare(m - 1);
        }
        Check(rises, "net districts: a farther district never costs less per connection");
    }

    private static void NetDistrictFigures()
    {
        var n = new Network();
        int level = PriceRules.Start, full = EndScaleRules.Full;
        Console.WriteLine("  net districts, the figures at the start's prices, and at the end game's scale:");
        for (int d = 0; d < n.Count; d++)
        {
            Console.WriteLine("    " + n.Id[d] + ": " + n.Size[d] + " connections, dig " + BroadbandRules.BuildCost(n, d, level) + " in " + BroadbandCatalogue.BuildMonths + " months; at scale " + BroadbandRules.MarketAtScale(n, d)
                              + " connections, dig " + BroadbandRules.BuildCost(n, d, level, full));
        }
        Check(BroadbandRules.BuildCost(n, 7, level) == 2720000 && BroadbandRules.BuildCost(n, 8, level) == 2960000,
            "net districts: Uptown's network costs $2,720,000 and Cedar Heights' $2,960,000 to dig at the start's prices (" + BroadbandRules.BuildCost(n, 7, level) + ", " + BroadbandRules.BuildCost(n, 8, level) + ")");
        Check(BroadbandRules.BuildCost(n, 7, level, full) == 59500000 && BroadbandRules.BuildCost(n, 8, level, full) == 64750000,
            "net districts: at the end game's scale they cost $59,500,000 and $64,750,000, every home and business of the district at $350 (" + BroadbandRules.BuildCost(n, 7, level, full) + ", " + BroadbandRules.BuildCost(n, 8, level, full) + ")");
        bool same = true;
        for (int d = 0; d < 7; d++)
        {
            same &= BroadbandRules.BuildCost(n, d, level) == BroadbandCatalogue.BuildCost[d] && BroadbandRules.BuildCost(d, level) == BroadbandCatalogue.BuildCost[d];
        }
        Check(same && BroadbandRules.BuildCost(n, 6, level, full) == 15750000 && BroadbandRules.BuildCost(n, 4, level, full) == 52500000,
            "net districts: the first seven's cost is what it was, at the start's prices and at scale (the Hamptons $15,750,000, Midtown $52,500,000)");
        Check(BroadbandRules.BuildCost(n, 7, PriceRules.Most) == 4 * BroadbandRules.BuildCost(n, 7, PriceRules.Start) && BroadbandRules.BuildCost(n, -1, level) == 0 && BroadbandRules.BuildCost(n, 9, level) == 0
              && BroadbandRules.BuildCost(null, 0, level) == 0, "net districts: the cost follows the price level; a place the network has not got costs nothing to ask about");
        // the same time: dug for the same months, opened by the same morning, upkeep the same share
        n.Register(NetLater, 20000, 1000);
        BroadbandRules.Found(n, 1);
        BroadbandRules.StartBuild(n, 7, 10, 5, BroadbandRules.BuildCost(n, 7, level));
        BroadbandRules.StartBuild(n, 9, 10, 5, BroadbandRules.BuildCost(n, 9, level));
        Check(n.Digging(7) && n.Digging(9) && n.ReadyDay(7) == 10 + BroadbandCatalogue.BuildMonths * 5 && n.ReadyDay(9) == n.ReadyDay(7) && !n.Open(7, 19) && BroadbandRules.Finish(n, 20) == 2
              && n.Open(7, 20) && n.Open(9, 20), "net districts: Uptown and a later district are dug for the same months as any, and open on the same morning");
        BroadbandRules.DayCosts(n, level, 25, 5, out _, out int network, out _);
        Check(network > 0, "net districts: an open district's upkeep and connections are costs of the day");
    }

    private static void NetDistrictLater()
    {
        // a district a free update adds: a place of its own, sized by the homes and businesses counted there, priced by its distance
        var n = new Network();
        Check(n.Count == 9 && n.IndexOf(NetLater) == -1 && n.IndexOf(null) == -1 && n.IndexOf(string.Empty) == -1, "net districts: a network starts with the catalogue's nine places");
        int at = n.Register(NetLater, BroadbandRules.SizeFromHomes(4200), BroadbandRules.FarShare(BroadbandRules.NearestMetres(900f, 1300f)));
        Check(at == 9 && n.Count == 10 && n.Id[9] == NetLater && n.Size[9] == 42000 && n.Register(NetLater, 1, 1) == 9 && n.Count == 10,
            "net districts: a later district counted with 4,200 homes and businesses is sized 42,000 connections, given the next place once and kept there");
        Check(BroadbandRules.SizeFromHomes(0) == BroadbandCatalogue.DefaultMarket && BroadbandRules.SizeFromHomes(-1) == BroadbandCatalogue.DefaultMarket && BroadbandRules.SizeFromHomes(5) == BroadbandCatalogue.PerHousehold * 300,
            "net districts: a district the population cannot count has the default size, and a village's size is the least");
        int dig = BroadbandRules.BuildCost(n, 9, PriceRules.Start);
        Check(dig == 42000 * 80 && BroadbandRules.BuildCost(n, 9, PriceRules.Start, EndScaleRules.Full) == 42000 * 5 * 350, "net districts: it costs its connections at $80 each, and at $350 each at the end game's scale ($3,360,000 and $73,500,000)");
        // distance
        int far = BroadbandRules.FarShare(BroadbandRules.NearestMetres(-6000f, -3500f)), nearby = BroadbandRules.FarShare(BroadbandRules.NearestMetres(300f, 500f));
        Check(nearby == 1000 && far == BroadbandCatalogue.FarMost, "net districts: a district beside Manhattan's costs the usual, one far out costs the Hamptons' three times per connection (" + nearby + ", " + far + ")");
        var farther = new Network();
        farther.Register("ba:neighborhood_far", 10000, far);
        Check(BroadbandRules.BuildCost(farther, 9, PriceRules.Start) == 10000 * 80 * 3, "net districts: the farther district's fibre is dearer in proportion ($2,400,000 for 10,000 connections)");
        // the places are full: the next district gets none and costs nothing to ask about
        var full = new Network();
        for (int i = 0; i < BroadbandCatalogue.ExtraMax; i++)
        {
            full.Register("ba:neighborhood_x" + i, 1000 * (i + 1), 1000);
        }
        Check(full.Count == Network.Slots && full.Register("ba:neighborhood_one_more", 1000, 1000) == -1 && full.Count == Network.Slots && BroadbandRules.BuildCost(full, Network.Slots, 1000) == 0,
            "net districts: with every place given, one more district is left out, never a fault");
        // a rival reaches a later district by the rules it always had: wireless and copper everywhere, cable and fibre only where they run
        Check(!InternetRules.Reaches(InternetCatalogue.CableIndex, NetLater) && !InternetRules.Reaches(InternetCatalogue.FibreIndex, NetLater) && InternetRules.Reaches(2, NetLater) && InternetRules.Reaches(3, NetLater)
              && !InternetRules.Reaches(InternetCatalogue.CableIndex, DistrictSplit.UptownId) && !InternetRules.Reaches(InternetCatalogue.FibreIndex, DistrictSplit.CedarId)
              && InternetRules.Reaches(2, DistrictSplit.UptownId) && InternetRules.Reaches(3, DistrictSplit.CedarId),
            "net districts: Kestrel and Brightvein stay where they were, Uptown, Cedar Heights and a later district have only wireless and copper until the player's network is dug");
    }

    private static void NetDistrictMarket()
    {
        var n = new Network();
        n.Register(NetLater, 20000, 1000);
        BroadbandRules.Found(n, 1);
        int rivalsUptown = n.Share[7, 2] + n.Share[7, 3];
        Check(n.Share[7, 0] == 0 && n.Share[7, 1] == 0 && n.Share[7, 2] == 400 && n.Share[7, 3] == 600 && n.Share[7, Network.Own] == 0 && rivalsUptown == 1000
              && n.Share[9, 2] == 400 && n.Share[9, 3] == 600, "net districts: before the network is dug the wireless and the copper hold Uptown and a later district 40% and 60%, as the rivals' holds put them");
        for (int d = 0; d < n.Count; d++)
        {
            int sum = 0;
            for (int p = 0; p < Network.Providers; p++)
            {
                sum += n.Share[d, p];
            }
            Check(sum == 1000, "net districts: " + n.Id[d] + " is a whole thousandths before the network is dug (" + sum + ")");
        }
        // dug: customers come as in any district, over months, by the district's own people
        BroadbandRules.StartBuild(n, 7, 1, 5, BroadbandRules.BuildCost(n, 7, 1000));
        BroadbandRules.Finish(n, 11);
        int day = 11;
        var calm = new[] { 1000, 1000, 1000, 1000 };
        BroadbandRules.Month(n, calm, day, 5);
        int first = n.Share[7, Network.Own];
        for (int m = 0; m < 30; m++)
        {
            day += 5;
            BroadbandRules.Month(n, calm, day, 5);
        }
        int settled = n.Share[7, Network.Own], total = 0;
        for (int p = 0; p < Network.Providers; p++)
        {
            total += n.Share[7, p];
        }
        Check(first > 0 && first < 100 && settled > 400 && settled < 700 && total == 1000 && n.Share[7, 0] == 0 && n.Share[7, 1] == 0 && n.Share[8, Network.Own] == 0,
            "net districts: nobody is won in a day, and after thirty months the player's network holds about half of Uptown's connections (" + first + " the first month, " + settled + " after) and none of Cedar Heights'");
        BroadbandRules.Apply(n, day);
        Check(InternetRules.Reaches(InternetCatalogue.OwnIndex, DistrictSplit.UptownId) && !InternetRules.Reaches(InternetCatalogue.OwnIndex, DistrictSplit.CedarId) && !InternetRules.Reaches(InternetCatalogue.OwnIndex, NetLater),
            "net districts: where the network is open it is a provider, and only there");
        // the customers there are the mod's population of the district
        n.People[7] = 1250;
        Check(BroadbandRules.MarketOf(n, 7) == 34000 * 1250 / 1000 && BroadbandRules.Connections(n, 7) == (int)((long)n.Share[7, Network.Own] * 42500 / 1000) && BroadbandRules.PeopleOf(n, 9) == 1000,
            "net districts: the connections follow the population's homes and businesses there, 25% up is 25% more customers");
        // the rivals answer by the same rule, now over the districts they reach
        int cap = BroadbandRules.AnswerCap(n, 2);
        Check(cap < InternetCatalogue.PromoMost && cap >= InternetCatalogue.PromoLeast && BroadbandRules.AnswerCap(n, InternetCatalogue.CableIndex) == InternetCatalogue.PromoMost,
            "net districts: wireless answers where the network has taken its customers, cable (which never reaches Uptown) is untouched (" + cap + ")");
        Check(BroadbandRules.Connections(n) >= BroadbandRules.Connections(n, 7) && BroadbandRules.SaleValue(n) >= (long)n.Spent[7] * BroadbandCatalogue.SaleFloorShare / 1000,
            "net districts: Uptown's network is in the company's connections and what a buyer pays for");
        long ms = NetTime(() => { for (int i = 0; i < 2000; i++) BroadbandRules.Month(n, calm, day, 5); });
        Check(ms < 400, "net districts: two thousand months of the market over every district are quick (" + ms + " ms)");
    }

    private static void NetDistrictSaves()
    {
        var state = new UtilitiesState();
        Network n = state.Network;
        n.Register(NetLater, 42000, 2000);
        BroadbandRules.Found(n, 3);
        BroadbandRules.StartBuild(n, 7, 4, 5, 2720000, true);
        BroadbandRules.StartBuild(n, 9, 5, 5, 3360000);
        BroadbandRules.Finish(n, 14);
        n.Share[7, Network.Own] = 120;
        n.Share[7, 2] = 400 - 120 / 2;
        n.Share[7, 3] = 1000 - n.Share[7, Network.Own] - n.Share[7, 2];
        n.Share[9, Network.Own] = 55;
        n.Share[9, 3] = 1000 - 55 - n.Share[9, 2];
        UtilitiesState back = UtilitiesStore.Read(UtilitiesStore.Write(state), out _);
        Network b = back != null ? back.Network : null;
        bool same = b != null && b.Count == 10 && b.Id[7] == DistrictSplit.UptownId && b.Id[9] == NetLater && b.Size[9] == 42000 && b.Far[9] == 2000 && b.Built[7] == n.Built[7] && b.Spent[7] == 2720000 && b.Scaled[7] && !b.Scaled[9]
                    && b.Built[9] == n.Built[9] && b.Spent[9] == 3360000;
        for (int p = 0; same && p < Network.Providers; p++)
        {
            same &= b.Share[7, p] == n.Share[7, p] && b.Share[9, p] == n.Share[9, p];
        }
        Check(same, "net districts: the mod's districts and a later one, with their size, distance, build, cost, scale and shares, survive the save");
        string text = UtilitiesStore.Write(state);
        int x = text.IndexOf("~x:", StringComparison.Ordinal);
        Check(x > 0, "net districts: the districts after the first seven are in a part of their own");
        // a build that does not know the part skips it and keeps the seven: cut it off
        int endX = text.IndexOf('~', x + 3);
        string without = text.Substring(0, x) + (endX < 0 ? string.Empty : text.Substring(endX));
        UtilitiesState older = UtilitiesStore.Read(without, out _);
        Network o = older != null ? older.Network : null;
        Check(o != null && o.Exists && o.Count == 9 && o.Built[0] == n.Built[0] && o.State == CompanyState.Running && o.Share[7, 2] == 400 && o.Share[7, 3] == 600 && o.Share[7, Network.Own] == 0 && o.Built[7] == 0,
            "net districts: a save made before the two districts (or read without the part) loads with the company as it was and the mod's two untouched, held by the rivals as the market stands");
        // the first seven's own record is the one it always was: its length does not change with the new districts
        string[] n7 = text.Substring(text.IndexOf("~n:", StringComparison.Ordinal) + 3, (x > 0 ? x : text.Length) - text.IndexOf("~n:", StringComparison.Ordinal) - 3).Split('|');
        Check(n7.Length == 14 + Network.Legacy * (2 + Network.Providers) + BroadbandCatalogue.HistoryMonths + 1 + Network.Legacy + BroadbandCatalogue.CrewKinds + 1,
            "net districts: the company's own record has the first seven's figures and no more, as an older build reads it (" + n7.Length + ")");
        // out of range is brought within it, an id that cannot be read is left out
        string key = UtilitiesStore.Escape(NetLater);
        string broken = text.Replace(key + "|42000|2000", key + "|42000|99999");
        UtilitiesState clamped = UtilitiesStore.Read(broken, out _);
        Check(clamped != null && clamped.Network.Count == 10 && clamped.Network.Far[9] == BroadbandCatalogue.FarMost, "net districts: a distance out of range in a save is brought to the largest");
        var empty = new UtilitiesState();
        UtilitiesState none = UtilitiesStore.Read(UtilitiesStore.Write(empty), out _);
        Check(none != null && !none.Network.Exists && none.Network.Count == 9 && !UtilitiesStore.Write(empty).Contains("~x:"), "net districts: a save with no company has no district part and loads as before");
        // a full set of later districts survives
        var big = new UtilitiesState();
        for (int i = 0; i < BroadbandCatalogue.ExtraMax + 2; i++)
        {
            big.Network.Register("ba:neighborhood_extra" + i, 5000 + i, 1000);
        }
        BroadbandRules.Found(big.Network, 1);
        UtilitiesState bigBack = UtilitiesStore.Read(UtilitiesStore.Write(big), out _);
        Check(bigBack != null && bigBack.Network.Count == Network.Slots && bigBack.Network.Id[Network.Slots - 1] == "ba:neighborhood_extra" + (BroadbandCatalogue.ExtraMax - 1), "net districts: every place given survives the save, and no more");
    }

    private static void NetOwnLine()
    {
        int n = LeasedLineCatalogue.Steps;
        int share = LeasedLineCatalogue.OwnPriceShare;
        Check(share >= 750 && share < 1000 && LeasedLineCatalogue.OwnInstallShare > 0 && LeasedLineCatalogue.OwnInstallShare < 1000,
            "own line: the company's own line costs between three quarters and the whole of the carriers' price, and installs for less than theirs");
        bool priced = true, never = true, cheaperThanDig = true;
        for (int step = 0; step < n; step++)
        {
            foreach (int level in new[] { PriceRules.Least, PriceRules.Start, PriceRules.Most })
            {
                int carrier = LeasedLineRules.MonthCents(step, level), ownCents = LeasedLineRules.MonthCents(step, level, true);
                priced &= ownCents == (int)((long)carrier * share / 1000) && ownCents < carrier;
                never &= ownCents * 4L >= carrier * 3L;
                cheaperThanDig &= LeasedLineRules.InstallCents(step, "ba:neighborhood_midtown", level, true) < LeasedLineRules.InstallCents(step, "ba:neighborhood_midtown", level)
                                  && LeasedLineRules.InstallCents(step, "ba:neighborhood_thehamptons", level, true) < LeasedLineRules.InstallCents(step, "ba:neighborhood_thehamptons", level);
            }
        }
        Check(priced && never && cheaperThanDig, "own line: at every step and price level it is a fifth under the carriers' price and never under three quarters of it, and its installation is under theirs even where fibre runs");
        // a rack on an own line costs the same per rack as ever, never a free one: the sums the carriers' table already holds
        long rack = LeasedLineRules.MonthCents(1, PriceRules.Start, true) / (LeasedLineCatalogue.Mbps[1] / LeasedLineCatalogue.RackMbps);
        Check(rack == 12800, "own line: a standard rack on a 10 Gbps own line costs $128 a month, a carrier's $160, against the $4,200 a rack of the old kind takes (" + rack + " cents)");
        Check(LeasedLineRules.InstallDays(1, "ba:neighborhood_thehamptons", true) == LeasedLineCatalogue.InstallDays[1] && LeasedLineRules.InstallDays(1, "ba:neighborhood_thehamptons") == LeasedLineCatalogue.InstallDays[1] + LeasedLineCatalogue.ExtraDays[LeasedLineCatalogue.Dig],
            "own line: no carrier has to dig, so the wait is the step's own, the district's dig left out");
        // ordering, changing, giving up, with the owner of each line kept
        var a = new RealLifeOverhaul.Utilities.Account { Place = "Dock~1", Service = Service.Internet, State = SupplyState.Active, Provider = -1 };
        LeasedLineRules.Order(a, 1, "ba:neighborhood_midtown", 10, true);
        Check(a.LineOwn == LeasedLineRules.RunningOwn && LeasedLineRules.OrderedOwn(a) && !LeasedLineRules.Has(null) && LeasedLineRules.LiveAt(a, 11) == 0 && LeasedLineRules.LiveAt(a, 10 + LeasedLineCatalogue.InstallDays[1]) == 2,
            "own line: an own line is ordered, waits its days and goes live");
        int live = 10 + LeasedLineCatalogue.InstallDays[1];
        Check(LeasedLineRules.LiveOwn(a, live) && LeasedLineRules.LineCents(a, live, PriceRules.Start) == LeasedLineRules.MonthCents(1, PriceRules.Start, true), "own line: it is billed at its own price from the day it is live");
        LeasedLineRules.Order(a, 2, "ba:neighborhood_midtown", live + 5, false);   // a change to a carrier's line: the own one runs on until it is live
        Check(a.LineNext == 3 && (a.LineOwn & LeasedLineRules.RunningOwn) != 0 && (a.LineOwn & LeasedLineRules.NextOwn) == 0 && !LeasedLineRules.OrderedOwn(a) && LeasedLineRules.LiveOwn(a, live + 5)
              && LeasedLineRules.LineCents(a, live + 5, PriceRules.Start) == LeasedLineRules.MonthCents(1, PriceRules.Start, true), "own line: a change to a carrier's line leaves the own one running at its own price until the new one is live");
        int turnDay = a.LineNextDay;
        Check(LeasedLineRules.Turn(a, turnDay) && !LeasedLineRules.LiveOwn(a, turnDay) && a.LineStep == 3 && a.LineOwn == 0 && LeasedLineRules.LineCents(a, turnDay, PriceRules.Start) == LeasedLineRules.MonthCents(2, PriceRules.Start),
            "own line: when the change takes over the line is the carrier's and billed at the carrier's price");
        LeasedLineRules.Order(a, 0, "ba:neighborhood_midtown", turnDay + 1, true);
        Check(a.LineNext == 1 && (a.LineOwn & LeasedLineRules.NextOwn) != 0 && (a.LineOwn & LeasedLineRules.RunningOwn) == 0 && LeasedLineRules.Turn(a, a.LineNextDay) && a.LineOwn == LeasedLineRules.RunningOwn, "own line: a change to an own line takes the bit over with it");
        LeasedLineRules.Drop(a);
        Check(a.LineOwn == 0 && !LeasedLineRules.Has(a), "own line: giving the line up clears it, the owner too");
        // the days of the month add up at the line's own price
        var b = new RealLifeOverhaul.Utilities.Account { Place = "Dock~2", Service = Service.Internet, State = SupplyState.Active, Provider = -1 };
        LeasedLineRules.Order(b, 0, "ba:neighborhood_midtown", 1, true);
        int d0 = 1 + LeasedLineCatalogue.InstallDays[0];
        for (int day = d0; day < d0 + 5; day++)
        {
            LeasedLineRules.AddDay(b, day, 5, PriceRules.Start);
        }
        Check(b.Accrued == LeasedLineRules.MonthCents(0, PriceRules.Start, true), "own line: a month of the line adds up to its own price (" + b.Accrued + ")");
        // the company as a provider of the line: only where its network is open, and gone when it is sold
        BroadbandRules.Apply(null, 1);
        Check(InternetRules.Get(InternetCatalogue.OwnLineIndex) == null && !InternetRules.Reaches(InternetCatalogue.OwnLineIndex, DistrictSplit.UptownId) && InternetRules.Count == InternetCatalogue.Providers.Length
              && InternetCatalogue.IsLine(InternetCatalogue.OwnLineIndex) && InternetCatalogue.IsLine(InternetCatalogue.LeasedIndex) && !InternetCatalogue.IsLine(2),
            "own line: with no company there is no own line to buy, and the lines stand outside the providers' count");
        var net = new Network();
        BroadbandRules.Found(net, 1);
        BroadbandRules.StartBuild(net, 7, 1, 5, 1);
        BroadbandRules.Finish(net, 20);
        BroadbandRules.Apply(net, 20);
        Check(InternetRules.Get(InternetCatalogue.OwnLineIndex) == InternetCatalogue.OwnLine && InternetRules.Reaches(InternetCatalogue.OwnLineIndex, DistrictSplit.UptownId) && !InternetRules.Reaches(InternetCatalogue.OwnLineIndex, DistrictSplit.CedarId)
              && !InternetRules.Reaches(InternetCatalogue.OwnLineIndex, "ba:neighborhood_midtown") && InternetRules.MonthCents(InternetCatalogue.OwnLineIndex, 1, true, true, PriceRules.Start, 900) == LeasedLineRules.MonthCents(1, PriceRules.Start, true),
            "own line: it is offered in the districts the network is open in and priced as it is, whatever the provider's month");
        net.State = CompanyState.Sold;
        BroadbandRules.Apply(net, 21);
        Check(InternetRules.Get(InternetCatalogue.OwnLineIndex) == null, "own line: sold, the company's own line goes with it");
        BroadbandRules.Apply(null, 22);
        // the save
        var state = new UtilitiesState();
        var own = new RealLifeOverhaul.Utilities.Account { Place = "Dock~1", Service = Service.Internet, State = SupplyState.Active, Provider = -1, LineStep = 2, LineDay = 30, LineNext = 3, LineNextDay = 50, LineOwn = LeasedLineRules.RunningOwn | LeasedLineRules.NextOwn };
        state.Accounts.Add(own);
        UtilitiesState back = UtilitiesStore.Read(UtilitiesStore.Write(state), out _);
        RealLifeOverhaul.Utilities.Account got = back != null ? back.Find("Dock~1", Service.Internet) : null;
        Check(got != null && got.LineOwn == 3 && got.LineStep == 2 && got.LineNext == 3, "own line: whose each line is survives the save");
        string text = UtilitiesStore.Write(state);
        int at = text.IndexOf("~a:", StringComparison.Ordinal);
        string[] c = text.Substring(at + 3).Split('|');
        Check(c.Length >= 31, "own line: the account's record has the owner of its lines at the end");
        string older = text.Substring(0, at + 3) + string.Join("|", c, 0, 30);
        UtilitiesState oldBack = UtilitiesStore.Read(older, out _);
        RealLifeOverhaul.Utilities.Account oldGot = oldBack != null ? oldBack.Find("Dock~1", Service.Internet) : null;
        Check(oldGot != null && oldGot.LineStep == 2 && oldGot.LineOwn == 0 && LeasedLineRules.Has(oldGot), "own line: a line saved before it was the carriers' reads as the carriers'");
    }

    private static long NetTime(Action work)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        work();
        return watch.ElapsedMilliseconds;
    }

    private static void NetDistrictSources()
    {
        string company = ModSource(@"Utilities\BroadbandCompany.cs"), internet = ModSource(@"Utilities\UtilitiesDirector.Internet.cs"), oldApp = ModSource(@"Utilities\UtilitiesApp.Internet.cs");
        string oldNet = ModSource(@"Utilities\UtilitiesApp.Network.cs"), kitLive = ModSource(@"Ui\Money\UtilitiesLive.cs"), districtLinks = ModSource(@"Wiring\DistrictLinks.cs"), demandLinks = ModSource(@"Wiring\Wiring.DemandLinks.cs");
        string rules = ModSource(@"Utilities\BroadbandRules.cs"), game = ModSource(@"Utilities\UtilitiesGame.Districts.cs");
        // the later districts are found once a morning, from the loaded city's own list, and given a place
        string discover = MemberText(company, "public void Discover()");
        Check(MemberText(company, "public void Morning(int today)").Contains("Discover();") && discover.Contains("UtilitiesGame.CityDistricts(_ids)") && discover.Contains("n.Register(") && discover.Contains("BroadbandRules.Seed(n, place);")
              && discover.Contains("UtilitiesBridges.Homes") && discover.Contains("UtilitiesBridges.Middle") && game.Contains("CityCheck.NewYorkDistricts(NeighborhoodHelper.Neighborhoods, into)"),
            "net districts: the morning finds a district the catalogue lacks in the loaded city, sizes it by the population's homes and businesses and its middle, and gives it a place");
        Check(!discover.Replace("n.Register(", "n.Nothing(").Contains("n.Register(") && !company.Replace("Discover();", "Skip();").Contains("Discover();"), "net districts: the check on the morning's discovery fails when the discovery is left out");
        Check(demandLinks.Contains("UtilitiesBridges.Homes = HomesIn;") && demandLinks.Contains("UtilitiesBridges.Middle = MiddleOf;") && demandLinks.Contains("UtilitiesBridges.Homes = null;"),
            "net districts: the population's homes and businesses and a district's middle reach the network through the bridges, put away with them");
        // every list of districts in both interfaces is the network's own, so a district it has is a row
        Check(oldNet.Contains("for (int d = 0; d < n.Count; d++)") && !oldNet.Contains("Network.Districts") && kitLive.Contains("new UtilityDistrictFact[n.Count]") && !kitLive.Contains("Network.Districts"),
            "net districts: the old Network tab and the kit's page list every district the network has, Uptown, Cedar Heights and a later one among them");
        Check(!oldNet.Contains("BroadbandCatalogue.Market[") && !oldNet.Contains("BroadbandCatalogue.Districts[") && !kitLive.Contains("BroadbandCatalogue.Market[") && !kitLive.Contains("BroadbandCatalogue.Districts["),
            "net districts: neither interface reads a district's size or id from the catalogue's table, which a later district is not in");
        Check(districtLinks.Contains("network.IndexOf(district)") && !districtLinks.Contains("BroadbandRules.District(district)"), "net districts: the district view finds the network's place in a district by the network, so Uptown's and a later district's fibre show there");
        Check(company.Contains("district >= n.Count") && !company.Contains("Network.Districts") && !rules.Contains("d < Network.Districts"), "net districts: the company's dig and extension check the network's own places");
        Check(rules.Contains("InternetCatalogue.OwnLine.Districts = sold ? None : reach;"), "net districts: the company's own line is offered where the network is open and goes with the company");
        // the own line is bought through the same purchase, refused where the network is not open, before any money is taken
        string buy = MemberText(internet, "public bool BuyLine(");
        Check(buy.Contains("if (own && !OwnLineHere(place))") && buy.IndexOf("OwnLineHere(place)", StringComparison.Ordinal) < buy.IndexOf("UtilitiesGame.Pay(", StringComparison.Ordinal) && buy.Contains("reallife_net_line_own_none")
              && MemberText(internet, "public bool BuyNet(").Contains("provider == InternetCatalogue.OwnLineIndex")
              && !buy.Replace("if (own && !OwnLineHere(place))", "if (false)").Contains("if (own && !OwnLineHere(place))"), "own line: an own line is refused where the network is not open, with the reason and before any money is taken");
        Check(oldApp.Contains("_director.OwnLineHere(place)") && oldApp.Contains("InternetCatalogue.OwnLineIndex") && kitLive.Contains("d.OwnLineHere(place)") && kitLive.Contains("Line(options, d, place, district, a, live, InternetCatalogue.OwnLineIndex)"),
            "own line: the old Internet page and the kit's list the company's own line beside the carriers' at a data centre where the network is open");
        Check(company.Contains("a.LineOwn = 0;"), "own line: selling the company turns its lines into the buyer's, at the carriers' price");
        Dictionary<string, string> en = LocaleFile("en");
        string[] keys = { "reallife_net_provider_ownline", "reallife_net_box_line_own", "reallife_net_box_line_own_about", "reallife_net_line_own_none" };
        bool all = true, short240 = true;
        foreach (string k in keys)
        {
            all &= en.TryGetValue(k, out string said) && !string.IsNullOrWhiteSpace(said);
            short240 &= en.TryGetValue(k, out string t) && t.Length <= 240;
        }
        Check(all && short240, "net districts: every new text of the network and the own line is in the English file, a notice at most 240 characters");
        Check(en.TryGetValue("reallife_guide_utilities", out string guide) && !guide.Contains("cannot be dug in Uptown") && guide.Contains("every district of the city") && guide.Contains("own line"), "net districts: the Guide says the network can be dug everywhere and names the own line");
        Check(WhatsNewHas(en, "Uptown and Cedar Heights"), "net districts: What's new tells of the network in the mod's districts");
    }
}
