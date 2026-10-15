using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// The rules of data centres, the cloud and AI compute: fitting out, racks and their writing down, the power and water
    /// they draw, the technicians they need and the outages they have; racks let to companies; the cloud's sales against
    /// the rival clouds and their price war; the steps to a cloud of one's own and to a hyperscaler; and what an AI model's
    /// training needs. At the end game's scale (Core/EndScale, a share from 0 to 1000 handed in by the director) a data
    /// centre costs what the real thing does, its racks of the new kind sell far more compute and need upkeep, it keeps
    /// crews of operators, engineers and security, and the city's market grows with the cloud's regions; at a share of 0
    /// every figure is as before. Game-free and checked by EconomyCheck; every roll is <see cref="TechRandom"/>'s.
    /// Cost: nothing per frame; a few sums a data centre once a game day.
    /// </summary>
    public static class InfraRules
    {
        // ---------------------------------------------------------------- building it

        /// <summary>What fitting out a building of so many square metres to a tier costs, cents, at the scale's share.</summary>
        public static long FitCost(int sqm, int tier, int share = 0)
        {
            int t = TalentCatalogue.Clamp(tier, 0, InfraCatalogue.Tiers - 1);
            return (long)Math.Max(0, sqm) * EndScaleRules.Mix(InfraCatalogue.FitPerSqm[t], InfraCatalogue.FitPerSqmScale[t], share);
        }

        /// <summary>What raising a data centre from one tier to a higher costs, cents: the difference.</summary>
        public static long UpgradeCost(int sqm, int from, int to, int share = 0) => Math.Max(0, FitCost(sqm, to, share) - FitCost(sqm, from, share));

        /// <summary>What a rack costs at the scale's share, cents: of servers, or of AI accelerators.</summary>
        public static long RackPrice(bool gpu, int share) => gpu ? EndScaleRules.Mix((long)InfraCatalogue.GpuRackCost, InfraCatalogue.GpuRackCostScale, share)
                                                                 : EndScaleRules.Mix((long)InfraCatalogue.RackCost, InfraCatalogue.RackCostScale, share);

        /// <summary>Racks bought at a time: five before the scale, more at it.</summary>
        public static int RacksStep(int share) => share > 0 ? InfraCatalogue.RacksStep : 5;

        /// <summary>Racks of either kind that still fit in the building.</summary>
        public static int Room(DataCentre d) => Math.Max(0, d.RacksRoom - d.Racks - d.GpuRacks * 2);

        /// <summary>Racks bought: their cost goes into the books; at the scale they are of the new kind. False if there is no room.</summary>
        public static bool Buy(DataCentre d, int racks, bool gpu, int share = 0)
        {
            int need = gpu ? racks * 2 : racks;
            if (racks <= 0 || Room(d) < need)
            {
                return false;
            }
            if (gpu)
            {
                d.GpuRacks += racks;
                d.DenseGpu += share > 0 ? racks : 0;
            }
            else
            {
                d.Racks += racks;
                d.Dense += share > 0 ? racks : 0;
            }
            d.Book += racks * RackPrice(gpu, share);
            return true;
        }

        /// <summary>
        /// Racks of the old kind replaced by racks of the new (at the scale only): the old ones' part of the books is
        /// written off and the new ones' price goes in. False if there are not so many of the old kind, or no scale.
        /// </summary>
        public static bool Replace(DataCentre d, int racks, bool gpu, int share)
        {
            int old = gpu ? d.LegacyGpu : d.Legacy;
            if (racks <= 0 || share <= 0 || old < racks)
            {
                return false;
            }
            long basis = CostBasis(d);
            long gone = basis > 0 ? (long)((decimal)d.Book * racks * (gpu ? InfraCatalogue.GpuRackCost : InfraCatalogue.RackCost) / basis) : 0;
            d.Book = Math.Max(0, d.Book - gone) + racks * RackPrice(gpu, share);
            if (gpu)
            {
                d.DenseGpu += racks;
            }
            else
            {
                d.Dense += racks;
            }
            return true;
        }

        /// <summary>What the racks cost when bought, cents: the old kind at the old prices, the new at the scale's.</summary>
        public static long CostBasis(DataCentre d)
        {
            return (long)d.Legacy * InfraCatalogue.RackCost + (long)d.LegacyGpu * InfraCatalogue.GpuRackCost
                   + (long)Math.Min(d.Dense, d.Racks) * InfraCatalogue.RackCostScale + (long)Math.Min(d.DenseGpu, d.GpuRacks) * InfraCatalogue.GpuRackCostScale;
        }

        /// <summary>A day's writing down of the racks, cents: what they cost over five years, never more than their value in the books.</summary>
        public static long WriteDown(DataCentre d)
        {
            return Math.Min(d.Book, CostBasis(d) / InfraCatalogue.WriteDownDays);
        }

        /// <summary>The power a data centre draws in a day, thousandths of a kWh: its racks', and at the scale the cooling's on top by its tier.</summary>
        public static long Power(DataCentre d, int share = 0)
        {
            long racks = ((long)d.Racks * InfraCatalogue.RackKw + (long)d.GpuRacks * InfraCatalogue.GpuRackKw) * 24 * 1000;
            return racks * EndScaleRules.Mix(1000, InfraCatalogue.PueScale[TalentCatalogue.Clamp(d.Tier, 0, InfraCatalogue.Tiers - 1)], share) / 1000;
        }

        /// <summary>
        /// A day's upkeep at the scale, cents at the start's prices: support, spares, transit and licences for each rack of
        /// the new kind, and the building's upkeep and insurance on what fitting it out costs at scale. Nothing at a share of 0.
        /// </summary>
        public static long Upkeep(DataCentre d, int share)
        {
            if (share <= 0)
            {
                return 0;
            }
            long full = (long)Math.Min(d.Dense, d.Racks) * InfraCatalogue.UpkeepRack + (long)Math.Min(d.DenseGpu, d.GpuRacks) * InfraCatalogue.UpkeepGpu
                        + (d.Racks + d.GpuRacks > 0 ? FitCost(d.Sqm, d.Tier, EndScaleRules.Full) * InfraCatalogue.FacilityYear / 1000 / 365 : 0);
            return EndScaleRules.Mix(0L, full, share);
        }

        // ---------------------------------------------------------------- the crews at scale

        /// <summary>The people of a crew a data centre needs at the full scale: operators for its racks, engineers for its draw, security for its site; none while it has no racks.</summary>
        public static int CrewAtScale(DataCentre d, int kind)
        {
            if (d.Racks + d.GpuRacks <= 0)
            {
                return 0;
            }
            switch (kind)
            {
                case InfraCatalogue.Operators:
                    return Math.Max(InfraCatalogue.OperatorsLeast, EndScaleRules.Per(d.Racks, InfraCatalogue.OperatorRacks) + EndScaleRules.Per(d.GpuRacks, InfraCatalogue.OperatorGpu));
                case InfraCatalogue.Engineers:
                    return Math.Max(InfraCatalogue.EngineersLeast, EndScaleRules.Per((long)d.Racks * InfraCatalogue.RackKw + (long)d.GpuRacks * InfraCatalogue.GpuRackKw, InfraCatalogue.EngineerKw));
                default:
                    return InfraCatalogue.GuardsLeast + Math.Max(0, d.Sqm) / InfraCatalogue.GuardSqm;
            }
        }

        public static int CrewNeed(DataCentre d, int kind, int share) => EndScaleRules.Need(CrewAtScale(d, kind), share);

        /// <summary>A day's hiring: each crew a share of its need nearer it (or down to it).</summary>
        public static void Hire(DataCentre d, int share)
        {
            for (int k = 0; k < InfraCatalogue.CrewKinds; k++)
            {
                d.Crew[k] = EndScaleRules.Hire(d.Crew[k], CrewNeed(d, k, share), InfraCatalogue.CrewPace);
            }
        }

        /// <summary>How far a data centre's crews cover what it needs, thousandths: the shortest of them.</summary>
        public static int Cover(DataCentre d, int share)
        {
            int cover = EndScaleRules.Full;
            for (int k = 0; k < InfraCatalogue.CrewKinds; k++)
            {
                cover = Math.Min(cover, EndScaleRules.Cover(d.Crew[k], CrewNeed(d, k, share)));
            }
            return cover;
        }

        /// <summary>The crews' wages a day, cents at the start's prices.</summary>
        public static long Crews(DataCentre d)
        {
            long sum = 0;
            for (int k = 0; k < InfraCatalogue.CrewKinds; k++)
            {
                sum += (long)Math.Max(0, d.Crew[k]) * InfraCatalogue.CrewWage[k];
            }
            return sum;
        }

        /// <summary>The cloud's operations people needed for a capacity of compute, at a share.</summary>
        public static int OpsNeed(long units, int share) => EndScaleRules.Need(EndScaleRules.Per(units, InfraCatalogue.OpsUnits), share);

        /// <summary>The cooling water it uses in a day, thousandths of a cubic metre.</summary>
        public static long Water(DataCentre d) => Power(d) / 1000 * InfraCatalogue.WaterPerMwh / 1000;

        /// <summary>The technicians it needs: one for each twenty racks, one at the least once it has any.</summary>
        public static int TechniciansNeeded(DataCentre d)
        {
            int racks = d.Racks + d.GpuRacks;
            return racks <= 0 ? 0 : Math.Max(1, (racks + InfraCatalogue.RacksPerTechnician - 1) / InfraCatalogue.RacksPerTechnician);
        }

        /// <summary>
        /// Whether a data centre goes down today: by its tier, more often the shorter it is of technicians; certainly when
        /// its power or its connection is cut. Down, it is down for the rest of the day.
        /// </summary>
        public static bool Outage(int seed, DataCentre d, int day, int cover = EndScaleRules.Full)
        {
            if (d.Racks + d.GpuRacks == 0)
            {
                return false;
            }
            if (!d.Power || !d.Net)
            {
                return true;
            }
            int need = TechniciansNeeded(d);
            long chance = InfraCatalogue.OutageChance[TalentCatalogue.Clamp(d.Tier, 0, InfraCatalogue.Tiers - 1)] * 1000L;
            if (d.Technicians < need)
            {
                chance = chance * (1000 + (need - d.Technicians) * 3000L / need) / 1000;
            }
            chance = chance * EndScaleRules.Outages(cover) / 1000;
            return TechRandom.Hash(seed, TechRandom.Of(d.Place), day, 5) % 1000000u < chance;
        }

        /// <summary>The racks the city's companies want to rent: so many in a steady economy, more in a boom, fewer the dearer the cloud's price.</summary>
        public static int ColoWanted(int cycle, int share = 0) => EndScaleRules.Mix(InfraCatalogue.ColoDemand, InfraCatalogue.ColoDemandScale, share) * FirmsRules.CycleSpend[TalentCatalogue.Clamp(cycle, 0, 3)] / 1000;

        /// <summary>
        /// The units of compute a data centre has to sell: its racks not let (the old kind let first), and its
        /// accelerators; a rack of the new kind sells as much more as the scale's share has come in.
        /// </summary>
        public static int Units(DataCentre d, int share = 0)
        {
            int dense = Math.Min(d.Dense, d.Racks), legacy = d.Racks - dense;
            int letOld = Math.Min(Math.Max(0, d.Colo), legacy), letNew = Math.Min(dense, Math.Max(0, d.Colo - letOld));
            long units = (long)(legacy - letOld) * InfraCatalogue.UnitsPerRack + (long)(dense - letNew) * EndScaleRules.Mix(InfraCatalogue.UnitsPerRack, InfraCatalogue.UnitsPerRackScale, share);
            return (int)Math.Min(int.MaxValue / 4, units);
        }

        public static int GpuUnits(DataCentre d, int share = 0)
        {
            int dense = Math.Min(d.DenseGpu, d.GpuRacks);
            long units = (long)(d.GpuRacks - dense) * InfraCatalogue.GpuUnitsPerRack + (long)dense * EndScaleRules.Mix(InfraCatalogue.GpuUnitsPerRack, InfraCatalogue.GpuUnitsPerRackScale, share);
            return (int)Math.Min(int.MaxValue / 4, units);
        }

        // ---------------------------------------------------------------- the cloud

        /// <summary>
        /// The units of compute the city's companies buy from the cloud a day: the city's demand (three times as much for a
        /// hyperscaler) by the economy, the price against the rivals' (squared: buyers shop around) and its reputation for
        /// staying up, but never more than it has. At the scale the city's market is far larger, and a hyperscaler's grows
        /// with each region past the three it needs.
        /// </summary>
        public static int CityDemand(Cloud c, int cycle, int capacity, bool gpu, int share = 0, int regions = 0)
        {
            long demand = Market(c, gpu, share, regions);
            demand = demand * FirmsRules.CycleSpend[TalentCatalogue.Clamp(cycle, 0, 3)] / 1000;
            long price = 1000L * c.RivalPrice / Math.Max(100, c.Price);
            demand = demand * price / 1000 * price / 1000;
            demand = demand * (500 + c.Reputation) / 1000 / 2;   // (the rivals keep half the market even against a cloud as good)
            return (int)Math.Max(0, Math.Min(capacity, demand));
        }

        /// <summary>What a day of compute sold brings, cents: the city's units at the cloud's price, the player's own firms' at their share of the list price.</summary>
        public static long Takings(Cloud c, int cityUnits, int gpuUnits)
        {
            long price = (long)InfraCatalogue.UnitDay * c.Price / 1000, gpuPrice = (long)InfraCatalogue.GpuUnitDay * c.Price / 1000;
            return cityUnits * price + gpuUnits * gpuPrice;
        }

        /// <summary>
        /// The rival clouds' answer: once the cloud holds more than three tenths of what the city would buy at its price,
        /// they cut theirs by a twentieth a month, down to three fifths of their list price; while it holds less, they let
        /// their prices back up a little. True if they cut today.
        /// </summary>
        public static bool PriceWar(Cloud c, int sold, int day, long market = InfraCatalogue.CloudDemand)
        {
            int share = (int)Math.Min(1000000L, (long)sold * 1000 / Math.Max(1L, market));
            if (share > InfraCatalogue.PriceWarShare && day - c.LastCut >= FirmsCatalogue.MonthDays && c.RivalPrice > InfraCatalogue.PriceFloor)
            {
                c.RivalPrice = Math.Max(InfraCatalogue.PriceFloor, c.RivalPrice - InfraCatalogue.PriceCut);
                c.LastCut = day;
                return true;
            }
            if (share < InfraCatalogue.PriceWarShare / 2 && c.RivalPrice < 1000 && day - c.LastCut >= FirmsCatalogue.MonthDays)
            {
                c.RivalPrice = Math.Min(1000, c.RivalPrice + InfraCatalogue.PriceCut / 2);
                c.LastCut = day;
            }
            return false;
        }

        /// <summary>
        /// The units of compute (or AI compute) the city buys at the list price in a steady economy: three times as much for
        /// a hyperscaler before the scale; at it, the far larger market, four times for a hyperscaler and more for each
        /// region past three.
        /// </summary>
        public static long Market(Cloud c, bool gpu, int share, int regions)
        {
            long before = gpu ? InfraCatalogue.GpuDemand : InfraCatalogue.CloudDemand;
            long scale = gpu ? InfraCatalogue.GpuDemandScale : InfraCatalogue.CloudDemandScale;
            if (c.Hyper)
            {
                before = before * InfraCatalogue.HyperDemand / 1000;
                scale = scale * (InfraCatalogue.HyperDemandScale + InfraCatalogue.HyperRegionScale * Math.Max(0, regions - InfraCatalogue.HyperRegions)) / 1000;
            }
            return EndScaleRules.Mix(before, scale, share);
        }

        /// <summary>What the rivals measure the cloud's share against: the city's compute before the scale, the market it sells to at it.</summary>
        public static long WarMarket(Cloud c, int share, int regions) => EndScaleRules.Mix(InfraCatalogue.CloudDemand, Market(c, false, EndScaleRules.Full, regions), share);

        /// <summary>The cloud's licence a year at the scale's share, cents.</summary>
        public static long Licence(int share) => EndScaleRules.Mix(InfraCatalogue.LicenceFee, InfraCatalogue.LicenceFeeScale, share);

        public static int CloudRacksLeast(int share) => EndScaleRules.Mix(InfraCatalogue.CloudRacksLeast, InfraCatalogue.CloudRacksLeastScale, share);

        public static long CloudCashLeast(int share) => EndScaleRules.Mix(InfraCatalogue.CloudCashLeast, InfraCatalogue.CloudCashLeastScale, share);

        public static int HyperRacks(int share) => EndScaleRules.Mix(InfraCatalogue.HyperRacks, InfraCatalogue.HyperRacksScale, share);

        public static long HyperCash(int share) => EndScaleRules.Mix(InfraCatalogue.HyperCash, InfraCatalogue.HyperCashScale, share);

        /// <summary>The cloud's reputation drifts up a little each day it stays up, and falls for each outage.</summary>
        public static void Reputation(Cloud c, int outages)
        {
            c.Reputation = TalentCatalogue.Clamp(c.Reputation + (outages > 0 ? -80 * outages : 5), 0, 1000);
        }

        /// <summary>What still stands between the player and a cloud of their own: racks, a tier, engineers, cash. 0 when nothing does; else a bit per thing missing (1 racks, 2 tier, 4 engineers, 8 cash, 16 the connection).</summary>
        public static int CloudNeeds(IList<DataCentre> centres, int engineers, long cash, bool net, int share = 0)
        {
            int racks = 0;
            bool tier = false;
            for (int i = 0; i < centres.Count; i++)
            {
                racks += centres[i].Racks;
                tier |= centres[i].Tier >= InfraCatalogue.CloudTierLeast;
            }
            return (racks >= CloudRacksLeast(share) ? 0 : 1) | (tier ? 0 : 2) | (engineers >= InfraCatalogue.CloudEngineersLeast ? 0 : 4) | (cash >= CloudCashLeast(share) ? 0 : 8) | (net ? 0 : 16);
        }

        /// <summary>What still stands between the cloud and a hyperscaler: 0 when nothing does; else 1 regions, 2 racks, 4 a chief technology officer and staff engineers, 8 cash.</summary>
        public static int HyperNeeds(IList<DataCentre> centres, bool chief, long cash, int share = 0)
        {
            var districts = new HashSet<string>();
            int racks = 0;
            for (int i = 0; i < centres.Count; i++)
            {
                racks += centres[i].Racks + centres[i].GpuRacks;
                if (centres[i].Racks + centres[i].GpuRacks > 0 && centres[i].District.Length > 0)
                {
                    districts.Add(centres[i].District);
                }
            }
            return (districts.Count >= InfraCatalogue.HyperRegions ? 0 : 1) | (racks >= HyperRacks(share) ? 0 : 2) | (chief ? 0 : 4) | (cash >= HyperCash(share) ? 0 : 8);
        }

        /// <summary>The cloud's regions: the districts its data centres stand in.</summary>
        public static int Regions(IList<DataCentre> centres)
        {
            var districts = new HashSet<string>();
            for (int i = 0; i < centres.Count; i++)
            {
                if (centres[i].Racks + centres[i].GpuRacks > 0)
                {
                    districts.Add(centres[i].District);
                }
            }
            return districts.Count;
        }

        // ---------------------------------------------------------------- AI models

        /// <summary>The AI compute unit-days the next generation of a model takes: twice the last's.</summary>
        public static long TrainNeed(int features)
        {
            int generation = Math.Max(0, features) / 2;
            long need = InfraCatalogue.TrainBase;
            for (int g = 0; g < generation && need < long.MaxValue / 4; g++)
            {
                need = need * InfraCatalogue.TrainGrowth / 1000;
            }
            return need;
        }

        /// <summary>The training a model gets a day at a level (0 to 4), AI compute unit-days.</summary>
        public static readonly int[] TrainLevels = { 0, 20, 50, 100, 200 };

        /// <summary>A model's customers' use of AI compute a day, units.</summary>
        public static int Inference(Product p) => (int)Math.Min(int.MaxValue, (p.Users + p.Free / 20) * InfraCatalogue.InferencePerCustomer / 1000);

        /// <summary>A day's takings rolled into a data centre's last thirty days.</summary>
        public static void Roll(DataCentre d, int day, long takings)
        {
            int slot = ((day % FirmsCatalogue.MonthDays) + FirmsCatalogue.MonthDays) % FirmsCatalogue.MonthDays;
            d.Takings30 += takings - d.Days[slot];
            d.Days[slot] = takings;
        }
    }
}
