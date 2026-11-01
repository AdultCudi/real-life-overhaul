using System;

namespace RealLifeOverhaul.Tech
{
    public static partial class SectorRules
    {
        // ---------------------------------------------------------------- racks let to the city's firms

        /// <summary>
        /// The racks the city's firms rent from data centres, the player's and the rivals': a rack and a half a large firm, an
        /// eighth a mid-sized one, hardly any a small one, times the economy; at the end game's scale the region a large data
        /// centre lets to as well. The place of the old 40 racks.
        /// </summary>
        public static int RacksWanted(SectorCityFigures city, int cycle, int share)
        {
            long racks = ((long)city.Large * SectorCatalogue.RacksLarge + (long)city.Mid * SectorCatalogue.RacksMid + (long)city.Small * SectorCatalogue.RacksSmall) / 1000;
            racks = racks * FirmsRules.CycleSpend[Clamp(cycle, 0, 3)] / 1000;
            return (int)Math.Min(int.MaxValue / 4, EndScaleRules.Mix(racks, racks * SectorCatalogue.ColoScale / 1000, share));
        }

        /// <summary>A data centre's share of the racks let, thousandths: its tier and the cloud's name against the rival data centres' (ScaleRules.Share).</summary>
        public static int RackShare(int tier, int reputation)
        {
            long mine = (long)SectorCatalogue.TierAppeal[Clamp(tier, 0, InfraCatalogue.Tiers - 1)] * (500 + Clamp(reputation, 0, 1000)) / 1000;
            return ScaleRules.Share(mine, mine + SectorCatalogue.RivalRackAppeal);
        }

        /// <summary>The racks the player's data centres can let today: the city's racks at their best tier's share, or the number capacity planning set (never past that share).</summary>
        public static int RacksToLet(SectorFile f, InfraFile infra, int cycle, int share)
        {
            int tier = 0;
            for (int i = 0; i < infra.Centres.Count; i++)
            {
                tier = Math.Max(tier, infra.Centres[i].Tier);
            }
            int market = (int)((long)RacksWanted(f.City, cycle, share) * RackShare(tier, infra.Cloud.Reputation) / 1000);
            return f.ColoTarget >= 0 ? Math.Min(market, f.ColoTarget) : market;
        }

        // ---------------------------------------------------------------- the city's compute

        /// <summary>
        /// The rivals' compute the cloud's market takes in beside the firms' own: the rivals' software customers' hosting and
        /// the rival labs' training, set on the market's day from the rival products.
        /// </summary>
        public static void RivalCompute(SectorFile f)
        {
            long hosting = 0, training = 0;
            for (int i = 0; i < f.Rivals.Count; i++)
            {
                RivalProduct r = f.Rivals[i];
                int seg = BuyersOf(r.Market);
                hosting += r.Customers * SectorCatalogue.HostingUnits[Clamp(seg, 0, 2)] / 1000;
                if (r.Market >= (int)SectorMarket.ConsumerAi)
                {
                    training += (long)(r.Startups ? SectorCatalogue.StartupLabCompute : SectorCatalogue.RivalLabCompute) * SectorCatalogue.RivalCloudRent / 1000;
                }
            }
            f.Hosting = hosting;
            f.Training = training;
        }

        /// <summary>
        /// The compute (or AI compute) the city buys a day at the list price, the player's cloud's and the rivals': each firm's
        /// own by its size, the rivals' software customers' hosting (or their labs' training), times the economy; at the end
        /// game's scale the market beyond the city, more for a hyperscaler and its regions past three (the old rule's factors).
        /// The place of the old fixed demand.
        /// </summary>
        public static long CloudMarket(SectorFile f, bool gpu, int cycle, int share, bool hyper, int regions)
        {
            SectorCityFigures c = f.City;
            long firms = gpu
                ? ((long)c.Large * SectorCatalogue.GpuLarge + (long)c.Mid * SectorCatalogue.GpuMid + (long)c.Small * SectorCatalogue.GpuSmall) / 1000 + f.Training
                : ((long)c.Large * SectorCatalogue.CloudLarge + (long)c.Mid * SectorCatalogue.CloudMid + (long)c.Small * SectorCatalogue.CloudSmall) / 1000 + f.Hosting;
            long before = firms * FirmsRules.CycleSpend[Clamp(cycle, 0, 3)] / 1000;
            long scale = before * (gpu ? SectorCatalogue.GpuScale : SectorCatalogue.CloudScale) / 1000;
            if (hyper)
            {
                before = before * InfraCatalogue.HyperDemand / 1000;
                int past = Math.Max(0, regions - InfraCatalogue.HyperRegions);
                scale = scale * (InfraCatalogue.HyperDemandScale + InfraCatalogue.HyperRegionScale * past) / 1000;
            }
            return Math.Max(0L, EndScaleRules.Mix(before, scale, share));
        }

        /// <summary>
        /// The player's cloud's share of the city's compute, thousandths: its price against the rivals' (the money
        /// scale's curve) and its name, against the rival clouds' and the firms running their own.
        /// </summary>
        public static int CloudShare(Cloud c)
        {
            long mine = (long)ScaleRules.PriceAppeal(Math.Max(100, c.Price), Math.Max(100, c.RivalPrice)) * (500 + Clamp(c.Reputation, 0, 1000)) / 1000;
            long rivals = (long)SectorCatalogue.RivalCloudWeight * (500 + SectorCatalogue.RivalCloudReputation) / 1000;
            return ScaleRules.Share(mine, mine + rivals);
        }

        /// <summary>The compute the city buys from the player's cloud a day: its share of the market, no more than it has to sell.</summary>
        public static int CloudDemand(Cloud c, long market, int capacity) => (int)Math.Max(0L, Math.Min(capacity, market * CloudShare(c) / 1000));

        // ---------------------------------------------------------------- the power price's spikes

        /// <summary>
        /// The power price's spikes for data centres (seeded from the sector's seed; a guest applies the host's): one every
        /// 45 to 90 days, for 4 to 8 days, at one and a half to two and a half times the tariff. True on the day one begins.
        /// </summary>
        public static bool SpikeDay(SectorFile f, int day)
        {
            if (f.NextSpike <= 0)
            {
                f.NextSpike = day + TechRandom.Range(f.Seed, day, 93, 0, SectorCatalogue.SpikeGapLeast, SectorCatalogue.SpikeGapMost);
                return false;
            }
            if (day < f.NextSpike)
            {
                return false;
            }
            f.SpikeFrom = day;
            f.SpikeUntil = day + TechRandom.Range(f.Seed, day, 94, 0, SectorCatalogue.SpikeDaysLeast, SectorCatalogue.SpikeDaysMost);
            f.SpikeFactor = TechRandom.Range(f.Seed, day, 95, 0, SectorCatalogue.SpikeLeast, SectorCatalogue.SpikeMost);
            f.Hedged = false;
            f.NextSpike = f.SpikeUntil + TechRandom.Range(f.Seed, day, 96, 0, SectorCatalogue.SpikeGapLeast, SectorCatalogue.SpikeGapMost);
            return true;
        }

        /// <summary>True while a spike runs.</summary>
        public static bool Spiking(SectorFile f, int day) => f != null && day >= f.SpikeFrom && day < f.SpikeUntil;

        /// <summary>The data centres' power tariff today, thousandths of the usual: a running spike's unless its price was fixed at the desk.</summary>
        public static int PowerFactor(SectorFile f, int day) => Spiking(f, day) && !f.Hedged ? Math.Max(1000, f.SpikeFactor) : 1000;

        /// <summary>The AI racks' share of their compute and power today: half while throttled for a spike.</summary>
        public static int GpuWorking(SectorFile f, int day) => f != null && day < f.ThrottleUntil ? 1000 - SectorCatalogue.ThrottleCut : 1000;

        /// <summary>A data centre's outages today, thousandths of the usual: fewer while a post-mortem's fixes hold.</summary>
        public static int OutageFactor(SectorFile f, int day) => f != null && day < f.FailoverUntil ? 1000 - SectorCatalogue.FailoverCut : 1000;
    }
}
