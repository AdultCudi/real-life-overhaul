using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// The rules of data centres, the cloud and AI compute: fitting out, racks and their writing down, the power and water
    /// they draw, the technicians they need and the outages they have; racks let to companies; the cloud's sales against
    /// the rival clouds and their price war; the steps to a cloud of one's own and to a hyperscaler; and what an AI model's
    /// training needs. Game-free and checked by EconomyCheck; every roll is <see cref="TechRandom"/>'s.
    /// </summary>
    public static class InfraRules
    {
        // ---------------------------------------------------------------- building it

        /// <summary>What fitting out a building of so many square metres to a tier costs, cents.</summary>
        public static long FitCost(int sqm, int tier) => (long)Math.Max(0, sqm) * InfraCatalogue.FitPerSqm[TalentCatalogue.Clamp(tier, 0, InfraCatalogue.Tiers - 1)];

        /// <summary>What raising a data centre from one tier to a higher costs, cents: the difference.</summary>
        public static long UpgradeCost(int sqm, int from, int to) => Math.Max(0, FitCost(sqm, to) - FitCost(sqm, from));

        /// <summary>Racks of either kind that still fit in the building.</summary>
        public static int Room(DataCentre d) => Math.Max(0, d.RacksRoom - d.Racks - d.GpuRacks * 2);

        /// <summary>Racks bought: their cost goes into the books. False if there is no room.</summary>
        public static bool Buy(DataCentre d, int racks, bool gpu)
        {
            int need = gpu ? racks * 2 : racks;
            if (racks <= 0 || Room(d) < need)
            {
                return false;
            }
            if (gpu)
            {
                d.GpuRacks += racks;
            }
            else
            {
                d.Racks += racks;
            }
            d.Book += (long)racks * (gpu ? InfraCatalogue.GpuRackCost : InfraCatalogue.RackCost);
            return true;
        }

        /// <summary>A day's writing down of the racks, cents: what they cost over five years, never more than their value in the books.</summary>
        public static long WriteDown(DataCentre d)
        {
            long cost = (long)d.Racks * InfraCatalogue.RackCost + (long)d.GpuRacks * InfraCatalogue.GpuRackCost;
            return Math.Min(d.Book, cost / InfraCatalogue.WriteDownDays);
        }

        /// <summary>The power a data centre draws in a day, thousandths of a kWh.</summary>
        public static long Power(DataCentre d) => ((long)d.Racks * InfraCatalogue.RackKw + (long)d.GpuRacks * InfraCatalogue.GpuRackKw) * 24 * 1000;

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
        public static bool Outage(int seed, DataCentre d, int day)
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
            return TechRandom.Hash(seed, TechRandom.Of(d.Place), day, 5) % 1000000u < chance;
        }

        /// <summary>The racks the city's companies want to rent: so many in a steady economy, more in a boom, fewer the dearer the cloud's price.</summary>
        public static int ColoWanted(int cycle) => InfraCatalogue.ColoDemand * FirmsRules.CycleSpend[TalentCatalogue.Clamp(cycle, 0, 3)] / 1000;

        /// <summary>The units of compute a data centre has to sell: its racks not let, and its accelerators.</summary>
        public static int Units(DataCentre d) => Math.Max(0, d.Racks - d.Colo) * InfraCatalogue.UnitsPerRack;

        public static int GpuUnits(DataCentre d) => d.GpuRacks * InfraCatalogue.GpuUnitsPerRack;

        // ---------------------------------------------------------------- the cloud

        /// <summary>
        /// The units of compute the city's companies buy from the cloud a day: the city's demand (three times as much for a
        /// hyperscaler) by the economy, the price against the rivals' (squared: buyers shop around) and its reputation for
        /// staying up, but never more than it has.
        /// </summary>
        public static int CityDemand(Cloud c, int cycle, int capacity, bool gpu)
        {
            long demand = gpu ? InfraCatalogue.GpuDemand : InfraCatalogue.CloudDemand;
            if (c.Hyper)
            {
                demand = demand * InfraCatalogue.HyperDemand / 1000;
            }
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
        public static bool PriceWar(Cloud c, int sold, int day)
        {
            int share = sold * 1000 / Math.Max(1, InfraCatalogue.CloudDemand);
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

        /// <summary>The cloud's reputation drifts up a little each day it stays up, and falls for each outage.</summary>
        public static void Reputation(Cloud c, int outages)
        {
            c.Reputation = TalentCatalogue.Clamp(c.Reputation + (outages > 0 ? -80 * outages : 5), 0, 1000);
        }

        /// <summary>What still stands between the player and a cloud of their own: racks, a tier, engineers, cash. 0 when nothing does; else a bit per thing missing (1 racks, 2 tier, 4 engineers, 8 cash, 16 the connection).</summary>
        public static int CloudNeeds(IList<DataCentre> centres, int engineers, long cash, bool net)
        {
            int racks = 0;
            bool tier = false;
            for (int i = 0; i < centres.Count; i++)
            {
                racks += centres[i].Racks;
                tier |= centres[i].Tier >= InfraCatalogue.CloudTierLeast;
            }
            return (racks >= InfraCatalogue.CloudRacksLeast ? 0 : 1) | (tier ? 0 : 2) | (engineers >= InfraCatalogue.CloudEngineersLeast ? 0 : 4) | (cash >= InfraCatalogue.CloudCashLeast ? 0 : 8) | (net ? 0 : 16);
        }

        /// <summary>What still stands between the cloud and a hyperscaler: 0 when nothing does; else 1 regions, 2 racks, 4 a chief technology officer and staff engineers, 8 cash.</summary>
        public static int HyperNeeds(IList<DataCentre> centres, bool chief, long cash)
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
            return (districts.Count >= InfraCatalogue.HyperRegions ? 0 : 1) | (racks >= InfraCatalogue.HyperRacks ? 0 : 2) | (chief ? 0 : 4) | (cash >= InfraCatalogue.HyperCash ? 0 : 8);
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
