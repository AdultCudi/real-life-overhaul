using System;

namespace RealLifeOverhaul.Construction
{
    /// <summary>What a building costs, part by part, in whole money at today's prices.</summary>
    public struct CostSheet
    {
        public long Materials, Labour, Plant, Fees, Parking, Exits, Contingency;

        public long Total => Materials + Labour + Plant + Fees + Parking + Exits + Contingency;
    }

    /// <summary>An architect's terms for drawing a set of plans.</summary>
    public struct PlansQuote
    {
        public int Fee;       // money
        public int Days;      // days to draw them
        public int Quality;   // 0 to 100
    }

    /// <summary>
    /// The plans: a design's storeys, the working hours and materials of each stage, the requirements (fire exits, parking,
    /// the district's height limit, an engineer's sign off), an architect's terms, and the estimate of what it all costs.
    /// Money is given at today's prices: <c>prices</c> is the city's price level in thousandths of the start's (1000 at
    /// the start), and each material's price on the exchange is a factor in thousandths. Game-free.
    /// </summary>
    public static class PlansRules
    {
        /// <summary>The storeys a design of this height stands for, one at least.</summary>
        public static int Storeys(double height)
        {
            return Math.Max(1, (int)Math.Round(height / BuildCatalogue.StoreyHeight));
        }

        /// <summary>Working hours a site needs: by the floor area and use, more for each storey; a rebuild is lighter work.</summary>
        public static int Hours(BuildUse use, int area, int storeys, ProjectKind kind)
        {
            int u = BuildCatalogue.Clamp((int)use, 0, (int)BuildUse.Count - 1);
            long tenths = (long)Math.Max(0, area) * BuildCatalogue.HoursPerHundred[u] / 100L;
            long hours = tenths * (1000L + Math.Max(0, storeys - 1) * BuildCatalogue.StoreyHoursPermille) / 10000L;
            if (kind == ProjectKind.Rebuild)
            {
                hours = hours * 600L / 1000L;
            }
            return (int)Math.Max(BuildCatalogue.HoursLeast, Math.Min(BuildCatalogue.HoursMost, hours));
        }

        /// <summary>The share of a site's progress, in thousandths, at which each stage ends, by the kind of project.</summary>
        public static int[] Ends(ProjectKind kind)
        {
            return kind == ProjectKind.Rebuild ? BuildCatalogue.RebuildStageEnds : kind == ProjectKind.Contract ? BuildCatalogue.ContractStageEnds : BuildCatalogue.PlotStageEnds;
        }

        /// <summary>The stage a site is in at a progress (0 to 1).</summary>
        public static WorkStage StageAt(ProjectKind kind, double progress)
        {
            int[] ends = Ends(kind);
            int p = (int)Math.Floor(progress * 1000.0);
            for (int s = 0; s < ends.Length; s++)
            {
                if (p < ends[s])
                {
                    return (WorkStage)s;
                }
            }
            return WorkStage.Envelope;
        }

        /// <summary>Where a stage begins and ends, as shares of the site's progress.</summary>
        public static void Span(ProjectKind kind, WorkStage stage, out double from, out double to)
        {
            int[] ends = Ends(kind);
            int s = BuildCatalogue.Clamp((int)stage, 0, ends.Length - 1);
            from = s == 0 ? 0.0 : ends[s - 1] / 1000.0;
            to = ends[s] / 1000.0;
        }

        /// <summary>The working hours of one stage.</summary>
        public static int StageHours(int hours, ProjectKind kind, WorkStage stage)
        {
            Span(kind, stage, out double from, out double to);
            return Math.Max(1, (int)Math.Round(hours * (to - from)));
        }

        /// <summary>The inspection that ends a stage, or -1 for none. A contract has the final one only.</summary>
        public static int InspectionAfter(ProjectKind kind, WorkStage stage)
        {
            if (kind == ProjectKind.Contract)
            {
                return stage == WorkStage.Envelope ? (int)Inspection.Final : -1;
            }
            switch (stage)
            {
                case WorkStage.Foundation: return (int)Inspection.Foundation;
                case WorkStage.Frame: return (int)Inspection.Frame;
                case WorkStage.Envelope: return (int)Inspection.Final;
                default: return -1;
            }
        }

        /// <summary>Tenths of a load of a material one stage of a building needs.</summary>
        public static int Loads(BuildUse use, int area, int storeys, ProjectKind kind, WorkStage stage, Material material)
        {
            int s = BuildCatalogue.Clamp((int)stage, 0, (int)WorkStage.Count - 1), m = BuildCatalogue.Clamp((int)material, 0, (int)Material.Count - 1);
            int u = BuildCatalogue.Clamp((int)use, 0, (int)BuildUse.Count - 1);
            long tenths = (long)BuildCatalogue.StageLoads[s][m] * Math.Max(0, area) / 100L;
            tenths = tenths * BuildCatalogue.UseMaterialsPermille[u] / 1000L;
            tenths = tenths * (1000L + Math.Max(0, storeys - BuildCatalogue.TallFrom) * BuildCatalogue.StoreyMaterialsPermille) / 1000L;
            if (kind == ProjectKind.Rebuild)
            {
                tenths = tenths * 550L / 1000L;   // the frame stands: the new front, the roof, the floors and services
            }
            return (int)Math.Min(int.MaxValue, tenths);
        }

        /// <summary>Tenths of a load a contract of the city's programme needs at a stage, by its kind and hours.</summary>
        public static int WorksLoads(int civicKind, int hours, WorkStage stage, Material material)
        {
            int k = BuildCatalogue.Clamp(civicKind, 0, BuildCatalogue.WorksLoads.Length - 1), m = BuildCatalogue.Clamp((int)material, 0, (int)Material.Count - 1);
            Span(ProjectKind.Contract, stage, out double from, out double to);
            double tenths = BuildCatalogue.WorksLoads[k][m] * Math.Max(0, hours) * (to - from) / 100.0;
            return (int)Math.Round(tenths);
        }

        /// <summary>A lorry load's price today: the list price at the city's prices, the exchange's factor and the merchant's own.</summary>
        public static long LoadPrice(Material material, int prices, int exchange, int merchant)
        {
            int m = BuildCatalogue.Clamp((int)material, 0, (int)Material.Count - 1);
            long price = (long)BuildCatalogue.LoadPrice[m] * Math.Max(1, prices) / 1000L;
            price = price * Math.Max(1, exchange) / 1000L;
            return price * Math.Max(1, merchant) / 1000L;
        }

        /// <summary>What a number of tenths of a load costs at a load's price.</summary>
        public static long Cost(int tenths, long loadPrice)
        {
            return Math.Max(0, tenths) * loadPrice / 10L;
        }

        /// <summary>A crew's day: the trades' rates for its people and its foreman's, at today's prices, more for a skilled crew.</summary>
        public static int CrewDay(int gang, int skill, int prices)
        {
            int[] people = BuildCatalogue.Gangs[BuildCatalogue.Clamp(gang, 0, BuildCatalogue.GangCount - 1)];
            long day = BuildCatalogue.ForemanRate;
            for (int t = 0; t < people.Length; t++)
            {
                day += (long)people[t] * BuildCatalogue.TradeRate[t];
            }
            day = day * (900L + BuildCatalogue.Clamp(skill, 0, 100) * 5L / 2L) / 1000L;   // 90 % at nought skill, 115 % at 100
            return (int)(day * Math.Max(1, prices) / 1000L);
        }

        /// <summary>The usual crew's day for a stage, at today's prices: what the estimate counts on.</summary>
        public static long StageCrewDay(WorkStage stage, int prices)
        {
            int[] people = BuildCatalogue.StageCrew[BuildCatalogue.Clamp((int)stage, 0, (int)WorkStage.Count - 1)];
            long day = BuildCatalogue.ForemanRate;
            for (int t = 0; t < people.Length; t++)
            {
                day += (long)people[t] * BuildCatalogue.TradeRate[t];
            }
            return day * Math.Max(1, prices) / 1000L;
        }

        /// <summary>The usual crew's day for the city's works of a kind, at today's prices.</summary>
        public static long WorksCrewDay(int civicKind, int prices)
        {
            int[] people = BuildCatalogue.WorksCrew[BuildCatalogue.Clamp(civicKind, 0, BuildCatalogue.WorksCrew.Length - 1)];
            long day = BuildCatalogue.ForemanRate;
            for (int t = 0; t < people.Length; t++)
            {
                day += (long)people[t] * BuildCatalogue.TradeRate[t];
            }
            return day * Math.Max(1, prices) / 1000L;
        }

        /// <summary>True when the city's works of a kind work with a piece of plant.</summary>
        public static bool NeedsWorksPlant(int civicKind, Plant plant)
        {
            int k = BuildCatalogue.Clamp(civicKind, 0, BuildCatalogue.WorksPlant.Length - 1), p = BuildCatalogue.Clamp((int)plant, 0, (int)Plant.Count - 1);
            return BuildCatalogue.WorksPlant[k][p] != 0;
        }

        /// <summary>People of a trade the city's works of a kind need at their usual pace.</summary>
        public static int WorksNeed(int civicKind, Trade trade)
        {
            int k = BuildCatalogue.Clamp(civicKind, 0, BuildCatalogue.WorksCrew.Length - 1), t = BuildCatalogue.Clamp((int)trade, 0, (int)Trade.Count - 1);
            return BuildCatalogue.WorksCrew[k][t];
        }

        /// <summary>A piece of plant hired for a day, at today's prices.</summary>
        public static long HireDay(Plant plant, int prices)
        {
            return (long)BuildCatalogue.HireDay[BuildCatalogue.Clamp((int)plant, 0, (int)Plant.Count - 1)] * Math.Max(1, prices) / 1000L;
        }

        /// <summary>True when a stage works with a piece of plant: a crane only for a frame of three storeys or more.</summary>
        public static bool NeedsPlant(WorkStage stage, Plant plant, int storeys)
        {
            int s = BuildCatalogue.Clamp((int)stage, 0, (int)WorkStage.Count - 1), p = BuildCatalogue.Clamp((int)plant, 0, (int)Plant.Count - 1);
            if (BuildCatalogue.StagePlant[s][p] == 0)
            {
                return false;
            }
            return plant != Plant.Crane || storeys >= BuildCatalogue.CraneFrom;
        }

        /// <summary>People of a trade a stage needs at its usual pace: a crane operator only where there is a crane.</summary>
        public static int CrewNeed(WorkStage stage, Trade trade, int storeys)
        {
            int s = BuildCatalogue.Clamp((int)stage, 0, (int)WorkStage.Count - 1), t = BuildCatalogue.Clamp((int)trade, 0, (int)Trade.Count - 1);
            if (trade == Trade.CraneOperator && storeys < BuildCatalogue.CraneFrom)
            {
                return 0;
            }
            return BuildCatalogue.StageCrew[s][t];
        }

        // ------------------------------------------------------------------ requirements

        /// <summary>Fire exits a building needs: one, two from three storeys and for every big floor, three from nine storeys.</summary>
        public static int ExitsNeeded(int storeys, int area)
        {
            int perStorey = storeys > 0 ? area / storeys : area;
            int exits = 1 + perStorey / BuildCatalogue.ExitArea;
            if (storeys >= BuildCatalogue.ExitTallFrom)
            {
                exits = Math.Max(exits, 2);
            }
            if (storeys >= BuildCatalogue.ExitVeryTallFrom)
            {
                exits = Math.Max(exits, 3);
            }
            return Math.Min(exits, 6);
        }

        /// <summary>Parking spaces the council asks of a building of a use and floor area.</summary>
        public static int ParkingNeeded(BuildUse use, int area)
        {
            int u = BuildCatalogue.Clamp((int)use, 0, (int)BuildUse.Count - 1);
            return Math.Max(0, area) / BuildCatalogue.ParkingPerSpace[u];
        }

        /// <summary>The storeys a district allows; a district the table does not know has the default (a free update's).</summary>
        public static int HeightLimit(string district, bool uptown)
        {
            if (uptown)
            {
                return BuildCatalogue.UptownHeightLimit;
            }
            for (int i = 0; i < BuildCatalogue.HeightDistricts.Length; i++)
            {
                if (string.Equals(BuildCatalogue.HeightDistricts[i], district, StringComparison.Ordinal))
                {
                    return BuildCatalogue.HeightLimits[i];
                }
            }
            return BuildCatalogue.DefaultHeightLimit;
        }

        /// <summary>True when the plans need an engineer's sign off: a tall frame, or a rebuild's work on a standing one.</summary>
        public static bool NeedsEngineer(int storeys, ProjectKind kind)
        {
            return kind == ProjectKind.Rebuild || (kind == ProjectKind.Plot && storeys >= BuildCatalogue.EngineerFromStoreys);
        }

        /// <summary>The requirements a set of plans does not meet (0 to 4): exits, parking, the height limit, the engineer.</summary>
        public static int Unmet(int exits, int exitsNeeded, bool parkingUnder, int parkingNeeded, int storeys, int limit, bool engineerNeeded, bool engineered, out int reasons)
        {
            reasons = 0;
            if (exits < exitsNeeded)
            {
                reasons |= 1;
            }
            if (!parkingUnder && parkingNeeded > 0)
            {
                reasons |= 2;
            }
            if (storeys > limit)
            {
                reasons |= 4;
            }
            if (engineerNeeded && !engineered)
            {
                reasons |= 8;
            }
            int n = 0;
            for (int r = reasons; r != 0; r >>= 1)
            {
                n += r & 1;
            }
            return n;
        }

        // ------------------------------------------------------------------ the architect and the estimate

        /// <summary>
        /// An architect's terms: a practice's fee is a share of the estimate; an architect of the company draws them for their
        /// wage, as well as their skill allows; the player's own plans are as good as their design's score.
        /// </summary>
        public static PlansQuote Quote(ArchitectKind kind, int firm, int skill, long estimate, int area)
        {
            int extra = Math.Max(0, area) / BuildCatalogue.DaysPerArea;
            switch (kind)
            {
                case ArchitectKind.Staff:
                    return new PlansQuote
                    {
                        Fee = 0, Days = BuildCatalogue.StaffDays + extra,
                        Quality = BuildCatalogue.Clamp(BuildCatalogue.StaffQualityBase + BuildCatalogue.Clamp(skill, 0, 100) * BuildCatalogue.StaffQualityPermille / 1000, 0, 100)
                    };
                case ArchitectKind.Player:
                    return new PlansQuote
                    {
                        Fee = 0, Days = 0,
                        Quality = BuildCatalogue.Clamp(BuildCatalogue.PlayerQualityBase + BuildCatalogue.Clamp(skill, 0, 100) * BuildCatalogue.PlayerQualityPermille / 1000, 0, 100)
                    };
                default:
                {
                    int f = BuildCatalogue.Clamp(firm, 0, BuildCatalogue.FirmCount - 1);
                    return new PlansQuote
                    {
                        Fee = (int)Math.Min(int.MaxValue, Math.Max(0L, estimate) * BuildCatalogue.FirmFeePermille[f] / 1000L), Days = BuildCatalogue.FirmDays[f] + extra,
                        Quality = BuildCatalogue.FirmQuality[f]
                    };
                }
            }
        }

        /// <summary>
        /// A sheet with its design's cost against the plans' budget applied (thousandths; 1000 as drawn): a dearer design needs
        /// more materials and more hands, and its contingency follows; fees, parking and exits stay as they are.
        /// </summary>
        public static CostSheet Costed(CostSheet sheet, int costPermille)
        {
            int c = BuildCatalogue.Clamp(costPermille, BuildCatalogue.DesignCostLeast, BuildCatalogue.DesignCostMost);
            if (c == 1000)
            {
                return sheet;
            }
            sheet.Materials = sheet.Materials * c / 1000L;
            sheet.Labour = sheet.Labour * c / 1000L;
            sheet.Contingency = sheet.Contingency * c / 1000L;
            return sheet;
        }

        /// <summary>
        /// What a building costs: its materials stage by stage (at the exchange's prices, by the usual merchant), the usual crews'
        /// days, the plant hired, the inspections and the planning fee, the parking under it, the fire exits after the first,
        /// and the contingency. <paramref name="exchange"/> is each material's factor in thousandths (1000 where it has none).
        /// </summary>
        public static CostSheet Estimate(ProjectKind kind, BuildUse use, int area, int storeys, bool parkingUnder, int exits, int prices, int[] exchange)
        {
            int hours = Hours(use, area, storeys, kind);
            CostSheet sheet = Works(kind, hours, storeys, prices, exchange, -1, use, area);
            long build = sheet.Materials + sheet.Labour + sheet.Plant;
            sheet.Fees += PlanningFee(build, prices);
            if (parkingUnder)
            {
                sheet.Parking = (long)ParkingNeeded(use, area) * BuildCatalogue.ParkingSpaceCost * prices / 1000L;
            }
            if (exits > 1)
            {
                sheet.Exits = build * (exits - 1) * BuildCatalogue.ExitCostPermille / 1000L;
            }
            sheet.Contingency = (build + sheet.Parking + sheet.Exits) * BuildCatalogue.ContingencyPermille / 1000L;
            return sheet;
        }

        /// <summary>
        /// What one of the city's sites costs its contractor: its materials by the programme's kind and hours, the usual crews'
        /// days and the plant, and the final inspection; no plans, no planning fee.
        /// </summary>
        public static CostSheet EstimateWorks(int civicKind, int hours, int prices, int[] exchange)
        {
            CostSheet sheet = Works(ProjectKind.Contract, Math.Max(1, hours), 1, prices, exchange, civicKind, BuildUse.Shops, 0);
            sheet.Contingency = (sheet.Materials + sheet.Labour + sheet.Plant) * BuildCatalogue.ContingencyPermille / 1000L;
            return sheet;
        }

        // The stages' materials, crews and plant, and the inspections' fees: a building's by its use and floor area, or the
        // city's works by their kind and hours.
        private static CostSheet Works(ProjectKind kind, int hours, int storeys, int prices, int[] exchange, int civicKind, BuildUse use, int area)
        {
            var sheet = new CostSheet();
            for (int s = 0; s < (int)WorkStage.Count; s++)
            {
                var stage = (WorkStage)s;
                for (int m = 0; m < (int)Material.Count; m++)
                {
                    int tenths = civicKind >= 0 ? WorksLoads(civicKind, hours, stage, (Material)m) : Loads(use, area, storeys, kind, stage, (Material)m);
                    int factor = exchange != null && m < exchange.Length && exchange[m] > 0 ? exchange[m] : 1000;
                    sheet.Materials += Cost(tenths, LoadPrice((Material)m, prices, factor, BuildCatalogue.MerchantPricePermille[0]));
                }
                long days = (StageHours(hours, kind, stage) + 9) / 10;   // ten working hours a day
                sheet.Labour += days * (civicKind >= 0 ? WorksCrewDay(civicKind, prices) : StageCrewDay(stage, prices));
                for (int p = 0; p < (int)Plant.Count; p++)
                {
                    if (civicKind >= 0 ? NeedsWorksPlant(civicKind, (Plant)p) : NeedsPlant(stage, (Plant)p, storeys))
                    {
                        sheet.Plant += days * HireDay((Plant)p, prices) + (long)BuildCatalogue.HireDelivery * prices / 1000L;
                    }
                }
            }
            for (int i = 0; i < BuildCatalogue.InspectionCount; i++)
            {
                sheet.Fees += kind == ProjectKind.Contract && i < (int)Inspection.Final ? 0 : (long)BuildCatalogue.InspectionFee[i] * prices / 1000L;
            }
            return sheet;
        }

        /// <summary>The planning fee on a build of this cost.</summary>
        public static long PlanningFee(long build, int prices)
        {
            return (long)BuildCatalogue.PlanningFee * prices / 1000L + Math.Max(0L, build) * BuildCatalogue.PlanningFeePermille / 1000L;
        }

        /// <summary>How far an estimate may be out, in thousandths either way: an estimator on the staff, a practice's own, or none.</summary>
        public static int Spread(bool estimator, int skill, bool firm)
        {
            if (estimator)
            {
                return Math.Max(BuildCatalogue.EstimateSpreadLeast, BuildCatalogue.EstimateSpreadBase - BuildCatalogue.Clamp(skill, 0, 100) * BuildCatalogue.EstimateSpreadPerSkill);
            }
            return firm ? BuildCatalogue.EstimateSpreadFirm : BuildCatalogue.EstimateSpreadNone;
        }

        /// <summary>A project's own error in its estimate, in thousandths, inside the spread: the same for the same project every time.</summary>
        public static int Error(int seed, int project, int spread)
        {
            BuildRandom random = BuildRandom.Of(seed, project, 31);
            return spread <= 0 ? 0 : random.Range(-spread, spread + 1);
        }

        /// <summary>The estimate shown: the cost with the project's error in it, in thousands.</summary>
        public static long Shown(long cost, int error)
        {
            long shown = cost * (1000L + error) / 1000L;
            return Math.Max(1000L, (shown + 500L) / 1000L * 1000L);
        }
    }
}
