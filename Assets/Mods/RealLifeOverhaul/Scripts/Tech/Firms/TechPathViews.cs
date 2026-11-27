using System.Collections.Generic;

namespace RealLifeOverhaul.Tech
{
    /// <summary>What the way up reads of the player's tech business today, counted by the director (every amount of money at the start's prices, as the rules hold it).</summary>
    public sealed class PathFacts
    {
        public int Firms, LiveProducts, People;     // tech firms of the studio kinds, products on sale, professionals across all the tech firms
        public long Monthly;                        // recurring revenue a month across the firms
        public int Centres, Racks, RacksAll;        // data centres, racks (and with the AI racks)
        public int TierTwo, Net;                    // data centres at tier II or better, and those on a business connection
        public int Engineers;                       // site reliability and DevOps people
        public int Districts;                       // districts with a data centre that has racks
        public bool Cto;                            // a chief technology officer is employed
        public int StaffInfra;                      // infrastructure people of staff level or above
        public long Cash;                           // the player's cash
        public bool CloudOpen, Hyper;               // the cloud is open; it has grown into a hyperscaler
        public int Models;                          // AI models on sale
        public int Share;                           // the end game's buying scale (the figures the cloud's needs are mixed at)
    }

    /// <summary>One thing a step asks for: its text, the figures in it, and whether it is met.</summary>
    public struct PathReq
    {
        public string Key;
        public string[] Pairs;
        public bool Met;
    }

    /// <summary>One step of the way up: what it means and where to do it, what it gave once done, and what it asks for with what is met.</summary>
    public sealed class PathStep
    {
        public string TitleKey, WhatKey, GaveKey;
        public bool Done;
        public readonly List<PathReq> Reqs = new List<PathReq>(5);
    }

    /// <summary>
    /// The Tech app's "The way up" explained (a player, 25 Nov 2026: "I'm not quite sure what this refers to, nor can I tell if I've met the
    /// requirements for it yet"): each of the seven steps says what it is and where to do it, a step done says what it gave, and a step
    /// not done lists what it asks for with the figure and whether it is met. The rules are the ones that judge the steps
    /// (<see cref="InfraRules.CloudNeeds"/>, <see cref="InfraRules.HyperNeeds"/>, <see cref="TechPath"/>), so a line can never say a step is met when the day's judging says it is not.
    /// Game-free; EconomyCheck's TechPlanChecks hold every step against its rule. Both the old Tech app and the kit's draw this one list.
    /// </summary>
    public static class TechPathViews
    {
        private const string Mod = "reallife";
        private const string P = Mod + "_tech_path_";

        public const int Steps = 7;

        private static string[] Pairs(IStudioFormat fmt, long have, long need) => new[] { "have", fmt.Whole(have), "need", fmt.Whole(need) };

        private static string[] Money(IStudioFormat fmt, long have, long need) => new[] { "have", fmt.Money(have), "need", fmt.Money(need) };

        private static PathReq Req(string key, bool met, string[] pairs) => new PathReq { Key = key, Met = met, Pairs = pairs };

        /// <summary>The seven steps in order, each with its needs while not done.</summary>
        public static PathStep[] Build(PathFacts f, IStudioFormat fmt)
        {
            var steps = new PathStep[Steps];
            long scale = TechPath.ScaleUpMonthly;
            steps[0] = new PathStep { TitleKey = P + "studio", WhatKey = P + "studio_what", GaveKey = P + "studio_gave", Done = f.Firms > 0 };
            steps[0].Reqs.Add(Req(P + "req_studio", f.Firms > 0, Pairs(fmt, f.Firms, 1)));
            steps[1] = new PathStep { TitleKey = P + "product", WhatKey = P + "product_what", GaveKey = P + "product_gave", Done = f.LiveProducts > 0 };
            steps[1].Reqs.Add(Req(P + "req_product", f.LiveProducts > 0, Pairs(fmt, f.LiveProducts, 1)));
            bool scaled = f.Monthly >= scale && f.People >= TechPath.ScaleUpPeople;
            steps[2] = new PathStep { TitleKey = P + "scale", WhatKey = P + "scale_what", GaveKey = P + "scale_gave", Done = scaled };
            steps[2].Reqs.Add(Req(P + "req_scale_monthly", f.Monthly >= scale, Money(fmt, f.Monthly, scale)));
            steps[2].Reqs.Add(Req(P + "req_scale_people", f.People >= TechPath.ScaleUpPeople, Pairs(fmt, f.People, TechPath.ScaleUpPeople)));
            steps[3] = new PathStep { TitleKey = P + "centre", WhatKey = P + "centre_what", GaveKey = P + "centre_gave", Done = f.Centres > 0 };
            steps[3].Reqs.Add(Req(P + "req_centre", f.Centres > 0, Pairs(fmt, f.Centres, 1)));
            int racksNeed = InfraRules.CloudRacksLeast(f.Share);
            long cloudCash = InfraRules.CloudCashLeast(f.Share);
            steps[4] = new PathStep { TitleKey = P + "cloud", WhatKey = P + "cloud_what", GaveKey = P + "cloud_gave", Done = f.CloudOpen };
            steps[4].Reqs.Add(Req(P + "req_cloud_racks", f.Racks >= racksNeed, Pairs(fmt, f.Racks, racksNeed)));
            steps[4].Reqs.Add(Req(P + "req_cloud_tier", f.TierTwo > 0, Pairs(fmt, f.TierTwo, 1)));
            steps[4].Reqs.Add(Req(P + "req_cloud_engineers", f.Engineers >= InfraCatalogue.CloudEngineersLeast, Pairs(fmt, f.Engineers, InfraCatalogue.CloudEngineersLeast)));
            steps[4].Reqs.Add(Req(P + "req_cloud_cash", f.Cash >= cloudCash, Money(fmt, f.Cash, cloudCash)));
            steps[4].Reqs.Add(Req(P + "req_cloud_net", f.Centres > 0 && f.Net >= f.Centres,
                new[] { "have", fmt.Whole(f.Net), "need", fmt.Whole(f.Centres), "mbps", fmt.Whole(InfraCatalogue.NetNeeded) }));
            int hyperRacks = InfraRules.HyperRacks(f.Share);
            long hyperCash = InfraRules.HyperCash(f.Share);
            steps[5] = new PathStep { TitleKey = P + "hyper", WhatKey = P + "hyper_what", GaveKey = P + "hyper_gave", Done = f.Hyper };
            steps[5].Reqs.Add(Req(P + "req_hyper_regions", f.Districts >= InfraCatalogue.HyperRegions, Pairs(fmt, f.Districts, InfraCatalogue.HyperRegions)));
            steps[5].Reqs.Add(Req(P + "req_hyper_racks", f.RacksAll >= hyperRacks, Pairs(fmt, f.RacksAll, hyperRacks)));
            steps[5].Reqs.Add(Req(P + "req_hyper_cto", f.Cto, Pairs(fmt, f.Cto ? 1 : 0, 1)));
            steps[5].Reqs.Add(Req(P + "req_hyper_staff", f.StaffInfra >= InfraCatalogue.HyperStaffInfra, Pairs(fmt, f.StaffInfra, InfraCatalogue.HyperStaffInfra)));
            steps[5].Reqs.Add(Req(P + "req_hyper_cash", f.Cash >= hyperCash, Money(fmt, f.Cash, hyperCash)));
            steps[6] = new PathStep { TitleKey = P + "ai", WhatKey = P + "ai_what", GaveKey = P + "ai_gave", Done = f.Models > 0 };
            steps[6].Reqs.Add(Req(P + "req_ai", f.Models > 0, Pairs(fmt, f.Models, 1)));
            return steps;
        }

        /// <summary>
        /// The bits of a cloud's needs not met, as <see cref="InfraRules.CloudNeeds"/> reads them, from the same facts: the list and the day's judging cannot part.
        /// </summary>
        public static int CloudBits(PathFacts f)
        {
            return (f.Racks >= InfraRules.CloudRacksLeast(f.Share) ? 0 : 1) | (f.TierTwo > 0 ? 0 : 2) | (f.Engineers >= InfraCatalogue.CloudEngineersLeast ? 0 : 4)
                   | (f.Cash >= InfraRules.CloudCashLeast(f.Share) ? 0 : 8) | (f.Centres > 0 && f.Net >= f.Centres ? 0 : 16);
        }

        /// <summary>The bits of a hyperscaler's needs not met (<see cref="InfraRules.HyperNeeds"/>): regions, racks, the chief and the staff people together, cash.</summary>
        public static int HyperBits(PathFacts f)
        {
            return (f.Districts >= InfraCatalogue.HyperRegions ? 0 : 1) | (f.RacksAll >= InfraRules.HyperRacks(f.Share) ? 0 : 2)
                   | (InfraRules.HasChiefs(f.Cto, f.StaffInfra) ? 0 : 4) | (f.Cash >= InfraRules.HyperCash(f.Share) ? 0 : 8);
        }
    }
}
