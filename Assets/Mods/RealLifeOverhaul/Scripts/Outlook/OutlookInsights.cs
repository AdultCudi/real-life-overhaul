using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;

namespace RealLifeOverhaul.Outlook
{
    /// <summary>What a piece of advice is: a chance, a risk, or just news.</summary>
    public enum OutlookInsightKind
    {
        Opportunity = 0,
        Warning = 1,
        Info = 2
    }

    /// <summary>One piece of advice: the rule that made it, how much it matters, and the figures its sentence is written from.</summary>
    public struct OutlookInsight
    {
        public int Rule;
        public OutlookInsightKind Kind;
        public int Score;          // higher first
        public int Variant;        // which of the rule's two meanings: 0 or 1
        public int A, B, C, D;     // the figures, as each rule says
        public string Name;        // a district's id, where the rule is about one
        public int Tab;            // the tab that shows the figures
    }

    /// <summary>
    /// The advice the Economy app gives, by rule from the readings (game-free, EconomyCheck's OutlookChecks). Every rule reads
    /// figures the mod keeps and says nothing when they are not there: no rule invents a number. Each is about one thing the
    /// player can act on, and the sentences say what it means for them. The advice is ranked: what touches the player's own
    /// debts, shops, staff, buildings and stock first, then by how large the move is. The same readings give the same advice on
    /// every machine.
    /// </summary>
    public static class OutlookInsights
    {
        public const int RateUp = 0, RateDown = 1, RateAheadUp = 2, RateAheadDown = 3, PricesHigh = 4, PricesLow = 5, JobsTight = 6, JobsSlack = 7,
            Boom = 8, Slump = 9, WorthUp = 10, WorthDown = 11, IncomeUp = 12, IncomeDown = 13, Shortage = 14, Glut = 15, StocksUp = 16, StocksDown = 17,
            DemandHigh = 18, DemandLow = 19, Building = 20, Deficit = 21, CostsUp = 22;
        public const int Count = 23;

        /// <summary>Each rule's name, which its texts are named by: reallife_econapp_ins_{name}_head, _mean and, for five, _mean2.</summary>
        public static readonly string[] Names =
        {
            "rate_up", "rate_down", "rate_ahead_up", "rate_ahead_down", "prices_high", "prices_low", "jobs_tight", "jobs_slack",
            "boom", "slump", "worth_up", "worth_down", "income_up", "income_down", "shortage", "glut", "stocks_up", "stocks_down",
            "demand_high", "demand_low", "building", "deficit", "costs_up"
        };

        /// <summary>What has happened, with its figures: a rule's first sentence.</summary>
        public static readonly string[] HeadKeys =
        {
            "reallife_econapp_ins_rate_up_head", "reallife_econapp_ins_rate_down_head", "reallife_econapp_ins_rate_ahead_up_head",
            "reallife_econapp_ins_rate_ahead_down_head", "reallife_econapp_ins_prices_high_head", "reallife_econapp_ins_prices_low_head",
            "reallife_econapp_ins_jobs_tight_head", "reallife_econapp_ins_jobs_slack_head", "reallife_econapp_ins_boom_head",
            "reallife_econapp_ins_slump_head", "reallife_econapp_ins_worth_up_head", "reallife_econapp_ins_worth_down_head",
            "reallife_econapp_ins_income_up_head", "reallife_econapp_ins_income_down_head", "reallife_econapp_ins_shortage_head",
            "reallife_econapp_ins_glut_head", "reallife_econapp_ins_stocks_up_head", "reallife_econapp_ins_stocks_down_head",
            "reallife_econapp_ins_demand_high_head", "reallife_econapp_ins_demand_low_head", "reallife_econapp_ins_building_head",
            "reallife_econapp_ins_deficit_head", "reallife_econapp_ins_costs_up_head"
        };

        /// <summary>What it means for the player: the second sentence.</summary>
        public static readonly string[] MeanKeys =
        {
            "reallife_econapp_ins_rate_up_mean", "reallife_econapp_ins_rate_down_mean", "reallife_econapp_ins_rate_ahead_up_mean",
            "reallife_econapp_ins_rate_ahead_down_mean", "reallife_econapp_ins_prices_high_mean", "reallife_econapp_ins_prices_low_mean",
            "reallife_econapp_ins_jobs_tight_mean", "reallife_econapp_ins_jobs_slack_mean", "reallife_econapp_ins_boom_mean",
            "reallife_econapp_ins_slump_mean", "reallife_econapp_ins_worth_up_mean", "reallife_econapp_ins_worth_down_mean",
            "reallife_econapp_ins_income_up_mean", "reallife_econapp_ins_income_down_mean", "reallife_econapp_ins_shortage_mean",
            "reallife_econapp_ins_glut_mean", "reallife_econapp_ins_stocks_up_mean", "reallife_econapp_ins_stocks_down_mean",
            "reallife_econapp_ins_demand_high_mean", "reallife_econapp_ins_demand_low_mean", "reallife_econapp_ins_building_mean",
            "reallife_econapp_ins_deficit_mean", "reallife_econapp_ins_costs_up_mean"
        };

        /// <summary>The second meaning of a rule that has two (see <see cref="HasTwo"/>); null for the others.</summary>
        public static readonly string[] MeanTwoKeys =
        {
            "reallife_econapp_ins_rate_up_mean2", null, null,
            null, null, null,
            "reallife_econapp_ins_jobs_tight_mean2", "reallife_econapp_ins_jobs_slack_mean2", null,
            null, null, null,
            null, null, "reallife_econapp_ins_shortage_mean2",
            "reallife_econapp_ins_glut_mean2", null, null,
            null, null, null,
            null, null
        };

        /// <summary>The rules that have a second meaning, for a player it does not touch in the same way.</summary>
        public static bool HasTwo(int rule)
        {
            return rule == RateUp || rule == JobsTight || rule == JobsSlack || rule == Shortage || rule == Glut;
        }

        // How much each rule matters on its own; what touches the player and how large the move is come on top.
        private static readonly int[] Base =
        {
            58, 52, 66, 50, 56, 32, 48, 46,
            54, 64, 52, 54, 36, 42, 50, 44, 30, 36,
            42, 40, 28, 40, 34
        };

        private static readonly int[] TabOf =
        {
            OutlookTab.Prices, OutlookTab.Prices, OutlookTab.Prices, OutlookTab.Prices, OutlookTab.Prices, OutlookTab.Prices, OutlookTab.Jobs, OutlookTab.Jobs,
            OutlookTab.Overview, OutlookTab.Overview, OutlookTab.Districts, OutlookTab.Districts, OutlookTab.Districts, OutlookTab.Districts, OutlookTab.Prices, OutlookTab.Prices,
            OutlookTab.Markets, OutlookTab.Markets, OutlookTab.Districts, OutlookTab.Districts, OutlookTab.Districts, OutlookTab.Finances, OutlookTab.Prices
        };

        private const int Mine = 24;     // points a piece of advice gains for touching something the player has
        private const int Biggest = 20;  // points a large move gains at most

        /// <summary>
        /// Writes the advice for these readings into <paramref name="into"/> (cleared first), the most pressing first, at most
        /// <see cref="OutlookCatalogue.InsightsMost"/> pieces; returns how many. Nothing is said while the city's economy is off.
        /// </summary>
        public static int Build(OutlookReadings r, List<OutlookInsight> into)
        {
            into.Clear();
            if (r == null || !r.CityOn)
            {
                return 0;
            }
            bool debt = r.Debt > 0, staff = r.WagesTomorrow > 0, shops = r.Shops > 0;

            // The central bank.
            if (r.LastMove > 0)
            {
                Add(into, RateUp, debt ? OutlookInsightKind.Warning : OutlookInsightKind.Info, debt ? Mine : 0, Magnitude(r.LastMove, 1, 4), debt ? 0 : 1, r.LastMove, r.RateBp);
            }
            else if (r.LastMove < 0)
            {
                Add(into, RateDown, OutlookInsightKind.Opportunity, debt ? Mine : 0, Magnitude(-r.LastMove, 1, 4), 0, -r.LastMove, r.RateBp);
            }
            if (r.Pending && r.PendingMove != 0 && r.DaysToDecision <= OutlookCatalogue.RateAheadDays)
            {
                int after = r.RateBp + r.PendingMove * 25;
                if (r.PendingMove > 0)
                {
                    Add(into, RateAheadUp, OutlookInsightKind.Warning, debt ? Mine : 0, Magnitude(r.PendingMove, 1, 4), 0, r.PendingMove, r.DaysToDecision, after);
                }
                else
                {
                    Add(into, RateAheadDown, OutlookInsightKind.Info, debt ? Mine : 0, Magnitude(-r.PendingMove, 1, 4), 0, -r.PendingMove, r.DaysToDecision, after);
                }
            }

            // Prices.
            if (r.Inflation >= OutlookCatalogue.InflationHigh)
            {
                Add(into, PricesHigh, OutlookInsightKind.Warning, shops || staff ? Mine : 0,
                    Magnitude(r.Inflation - OutlookCatalogue.InflationHigh, 25, 6), 0, r.Inflation, MacroCatalogue.InflationTarget);
            }
            else if (r.Inflation <= OutlookCatalogue.InflationLow)
            {
                Add(into, PricesLow, OutlookInsightKind.Info, 0, 0, 0, r.Inflation, MacroCatalogue.InflationTarget);
            }
            if (r.Long[OutlookCatalogue.TilePrices].Known && r.Long[OutlookCatalogue.TilePrices].Value >= 300 && (r.WagesTomorrow > 0 || r.RentTomorrow > 0))
            {
                Add(into, CostsUp, OutlookInsightKind.Warning, Mine, Magnitude(r.Long[OutlookCatalogue.TilePrices].Value, 100, 4), 0,
                    r.Long[OutlookCatalogue.TilePrices].Value, OutlookCatalogue.LongDays);
            }

            // Jobs.
            if (r.JobsOn)
            {
                if (r.Unemployment <= OutlookCatalogue.JobsTight)
                {
                    Add(into, JobsTight, staff ? OutlookInsightKind.Warning : OutlookInsightKind.Info, staff ? Mine : 0, 0, staff ? 0 : 1, r.Unemployment, r.Vacancies);
                }
                else if (r.Unemployment >= OutlookCatalogue.JobsSlack)
                {
                    Add(into, JobsSlack, OutlookInsightKind.Opportunity, staff ? Mine : 0, Magnitude(r.Unemployment - OutlookCatalogue.JobsSlack, 10, 4), staff ? 0 : 1, r.Unemployment, r.Vacancies);
                }
            }

            // The good times and the bad.
            if (r.Cycle == (int)CycleState.Boom)
            {
                Add(into, Boom, OutlookInsightKind.Opportunity, shops ? Mine : 0, 0, 0, r.Footfall, (r.Spend - 1000) / 10);
            }
            else if (r.Cycle >= (int)CycleState.Slowdown)
            {
                Add(into, Slump, OutlookInsightKind.Warning, shops ? Mine : 0, r.Cycle == (int)CycleState.Recession ? Biggest : 6, 0, r.Footfall, (r.Spend - 1000) / 10);
            }

            Districts(r, into);

            // Wholesale goods.
            int best = -1, bestPercent = 0;
            bool bestStocked = false;
            for (int k = 0; k < OutlookReadings.Kinds; k++)
            {
                OutlookSupply s = r.Supply[k];
                int size = Math.Abs(s.Percent);
                if (s.Shock == 0 || size < OutlookCatalogue.ShockPercent)
                {
                    continue;
                }
                if (best < 0 || (s.Stocked && !bestStocked) || (s.Stocked == bestStocked && size > bestPercent))
                {
                    best = k;
                    bestPercent = size;
                    bestStocked = s.Stocked;
                }
            }
            if (best >= 0)
            {
                OutlookSupply s = r.Supply[best];
                bool shortage = s.Shock == 1;
                OutlookInsightKind kind = shortage ? (s.Stocked ? OutlookInsightKind.Warning : OutlookInsightKind.Info)
                    : (s.Stocked ? OutlookInsightKind.Opportunity : OutlookInsightKind.Info);
                Add(into, shortage ? Shortage : Glut, kind,
                    s.Stocked ? Mine : 0, Magnitude(bestPercent, 5, 6), s.Stocked ? 0 : 1, best, bestPercent, s.Days, s.Announced ? 1 : 0);
            }

            // The market.
            if (r.MarketOn)
            {
                OutlookDelta stocks = r.Long[OutlookCatalogue.TileStocks];
                if (stocks.Known && stocks.Value >= OutlookCatalogue.StocksMove)
                {
                    Add(into, StocksUp, OutlookInsightKind.Info, 0, Magnitude(stocks.Value, 100, 6), 0, stocks.Value, OutlookCatalogue.LongDays);
                }
                else if (stocks.Known && stocks.Value <= -OutlookCatalogue.StocksMove)
                {
                    Add(into, StocksDown, OutlookInsightKind.Info, 0, Magnitude(-stocks.Value, 100, 6), 0, -stocks.Value, OutlookCatalogue.LongDays);
                }
            }

            // The council's books.
            if (r.CouncilOn && r.Revenue > 0 && (r.Spending > r.Revenue || r.Reserve < 0))
            {
                long reserve = Math.Max(int.MinValue + 1L, Math.Min(int.MaxValue, r.Reserve));
                Add(into, Deficit, OutlookInsightKind.Warning, 0, Magnitude(r.Spending - r.Revenue, Math.Max(1, r.Revenue / 20), 6), 0,
                    r.Revenue - r.Spending, (int)reserve);
            }

            Sort(into);
            if (into.Count > OutlookCatalogue.InsightsMost)
            {
                into.RemoveRange(OutlookCatalogue.InsightsMost, into.Count - OutlookCatalogue.InsightsMost);
            }
            return into.Count;
        }

        // The rules about one district each: the district that moved most, or wants most, says it.
        private static void Districts(OutlookReadings r, List<OutlookInsight> into)
        {
            OutlookDistrict worthUp = null, worthDown = null, incomeUp = null, incomeDown = null, building = null, demandHigh = null, demandLow = null;
            int useHigh = 0, useLow = 0;
            for (int i = 0; i < r.Districts.Count; i++)
            {
                OutlookDistrict d = r.Districts[i];
                if (d.WorthLong.Known)
                {
                    worthUp = d.WorthLong.Value >= OutlookCatalogue.WorthMoves && (worthUp == null || d.WorthLong.Value > worthUp.WorthLong.Value) ? d : worthUp;
                    worthDown = d.WorthLong.Value <= -OutlookCatalogue.WorthMoves && (worthDown == null || d.WorthLong.Value < worthDown.WorthLong.Value) ? d : worthDown;
                }
                if (d.IncomeLong.Known)
                {
                    incomeUp = d.IncomeLong.Value >= OutlookCatalogue.IncomeMoves && (incomeUp == null || Stronger(d, incomeUp, true)) ? d : incomeUp;
                    incomeDown = d.IncomeLong.Value <= -OutlookCatalogue.IncomeMoves && (incomeDown == null || Stronger(d, incomeDown, false)) ? d : incomeDown;
                }
                if (d.Growing >= OutlookCatalogue.BuildingsMany && (building == null || d.Growing > building.Growing))
                {
                    building = d;
                }
                for (int u = 0; u < d.Demand.Length; u++)
                {
                    int demand = d.Demand[u];
                    if (demand >= OutlookCatalogue.DemandHigh && (demandHigh == null || demand > demandHigh.Demand[useHigh]))
                    {
                        demandHigh = d;
                        useHigh = u;
                    }
                    if (demand >= 0 && demand <= OutlookCatalogue.DemandLow && (demandLow == null || demand < demandLow.Demand[useLow]))
                    {
                        demandLow = d;
                        useLow = u;
                    }
                }
            }
            if (worthUp != null)
            {
                Add(into, WorthUp, OutlookInsightKind.Opportunity, worthUp.Mine ? Mine : 0, Magnitude(worthUp.WorthLong.Value, 100, 6), 0,
                    worthUp.WorthLong.Value, OutlookCatalogue.LongDays, name: worthUp.Id);
            }
            if (worthDown != null)
            {
                Add(into, WorthDown, OutlookInsightKind.Warning, worthDown.Mine ? Mine : 0, Magnitude(-worthDown.WorthLong.Value, 100, 6), 0,
                    -worthDown.WorthLong.Value, OutlookCatalogue.LongDays, name: worthDown.Id);
            }
            if (incomeUp != null)
            {
                Add(into, IncomeUp, incomeUp.Mine ? OutlookInsightKind.Opportunity : OutlookInsightKind.Info, incomeUp.Mine ? Mine : 0, Magnitude(incomeUp.IncomeLong.Value, 10, 6), 0,
                    incomeUp.IncomeLong.Value, incomeUp.Income, name: incomeUp.Id);
            }
            if (incomeDown != null)
            {
                Add(into, IncomeDown, incomeDown.Mine ? OutlookInsightKind.Warning : OutlookInsightKind.Info, incomeDown.Mine ? Mine : 0, Magnitude(-incomeDown.IncomeLong.Value, 10, 6), 0,
                    -incomeDown.IncomeLong.Value, incomeDown.Income, name: incomeDown.Id);
            }
            if (demandHigh != null)
            {
                Add(into, DemandHigh, OutlookInsightKind.Opportunity, demandHigh.Mine ? Mine : 0,
                    Magnitude(demandHigh.Demand[useHigh] - OutlookCatalogue.DemandHigh, 3, 6), 0, useHigh, demandHigh.Demand[useHigh], name: demandHigh.Id);
            }
            if (demandLow != null)
            {
                Add(into, DemandLow, OutlookInsightKind.Warning, demandLow.Mine ? Mine : 0,
                    Magnitude(OutlookCatalogue.DemandLow - demandLow.Demand[useLow], 3, 6), 0, useLow, demandLow.Demand[useLow], name: demandLow.Id);
            }
            if (building != null)
            {
                Add(into, Building, OutlookInsightKind.Info, building.Mine ? Mine : 0, 0, 0, building.Growing, 0, name: building.Id);
            }
        }

        // True if d moved further than the one held: by the size of the move, then by the id, so the same readings always choose the same.
        private static bool Stronger(OutlookDistrict d, OutlookDistrict held, bool up)
        {
            int a = up ? d.IncomeLong.Value : -d.IncomeLong.Value, b = up ? held.IncomeLong.Value : -held.IncomeLong.Value;
            return a != b ? a > b : string.CompareOrdinal(d.Id, held.Id) < 0;
        }

        // Points for the size of a move: one for each step of it past the first, up to a most.
        private static int Magnitude(int size, int step, int most)
        {
            return step <= 0 ? 0 : Math.Min(most, Math.Max(0, size / step)) * Biggest / Math.Max(1, most);
        }

        private static void Add(List<OutlookInsight> into, int rule, OutlookInsightKind kind, int relevance, int magnitude, int variant, int a, int b, int c = 0, int d = 0, string name = null)
        {
            into.Add(new OutlookInsight
            {
                Rule = rule, Kind = kind, Score = Base[rule] + relevance + magnitude, Variant = variant, A = a, B = b, C = c, D = d, Name = name, Tab = TabOf[rule]
            });
        }

        // Highest score first; equal scores by rule, then by district id: a stable order that depends on nothing but the readings.
        private static void Sort(List<OutlookInsight> list)
        {
            for (int i = 1; i < list.Count; i++)
            {
                OutlookInsight held = list[i];
                int j = i - 1;
                while (j >= 0 && Before(held, list[j]))
                {
                    list[j + 1] = list[j];
                    j--;
                }
                list[j + 1] = held;
            }
        }

        private static bool Before(OutlookInsight a, OutlookInsight b)
        {
            if (a.Score != b.Score)
            {
                return a.Score > b.Score;
            }
            if (a.Rule != b.Rule)
            {
                return a.Rule < b.Rule;
            }
            return string.CompareOrdinal(a.Name, b.Name) < 0;
        }
    }
}
