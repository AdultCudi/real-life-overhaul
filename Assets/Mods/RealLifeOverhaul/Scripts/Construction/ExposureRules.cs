using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Construction
{
    /// <summary>What a project will still buy of one material at one stage: tenths of a load, the day, the money at today's prices.</summary>
    public struct MaterialNeed
    {
        public Material Material;
        public WorkStage Stage;
        public int Tenths;
        public int Day;
        public long Money;
    }

    /// <summary>One future of a project's hedge as the app shows it: the material, its contract day, units and the money it stands for.</summary>
    public struct HedgeLine
    {
        public Material Material;
        public int SettleDay;
        public long Units;
        public long Money;
    }

    /// <summary>What a hedge of a project's materials would be now, from the raw materials exchange.</summary>
    public struct HedgeQuote
    {
        public int Legs;
        public long Money, Margin, Fees, TooNear, Unpriced, Cut;
        public string WhyKey;   // why it cannot be bought now (a text key); null when it can
    }

    /// <summary>
    /// A project's materials exposure, game-free: what each stage still to come will buy of each material, on the day its
    /// stage is due to begin by the project's own schedule, at today's merchant prices (the usual merchant, the speciality's
    /// saving, the exchange's moves). The stage under way buys what it lacks and has not ordered at once; an order is paid
    /// when it is made, so it is no exposure. The loads are the very ones the site will ask for (<see cref="StageNeed"/>,
    /// which the director's stages use too). A few dozen operations a project; asked when its page is written or hedged.
    /// </summary>
    public static class ExposureRules
    {
        public const int BooksDays = 400;     // days of the company's books kept: a listed company reads a year and its quarter
        public const int HedgesMost = 24;     // futures a project keeps track of (the exchange holds 40 at once)

        /// <summary>Tenths of a load of a material a stage of a project needs, as its site asks when the stage begins.</summary>
        public static int StageNeed(ProjectFile p, WorkStage stage, Material material)
        {
            return p.Model == ProjectKind.Contract ? PlansRules.WorksLoads(p.CivicKind, p.Hours, stage, material)
                : PlansRules.Loads(p.Use, p.Area, p.Storeys, p.Kind, stage, material) * p.CostPermille / 1000;   // a dearer design of the player's needs more
        }

        /// <summary>
        /// A project's purchases to come, into <paramref name="into"/> (cleared); none for one not ready or building.
        /// <paramref name="exchange"/> is each material's move in thousandths, <paramref name="materialsPermille"/> the
        /// company's saving on its speciality. How many.
        /// </summary>
        public static int Of(ProjectFile p, int today, int prices, int[] exchange, int materialsPermille, List<MaterialNeed> into)
        {
            into.Clear();
            if (p == null || (p.Stage != ProjectStage.Ready && p.Stage != ProjectStage.Building))
            {
                return 0;
            }
            bool building = p.Stage == ProjectStage.Building;
            int start = building && p.StartDay > 0 ? p.StartDay : today;
            int due = building && p.DueDay > start ? p.DueDay : PlanningRules.AfterWorkingDays(start, TenderRules.StandardDays(p.Hours) + 2);
            int working = WorkingDaysBetween(start, due);
            int current = building ? Math.Max(0, p.StageSeen) : 0;
            ProjectKind model = p.Model;
            for (int s = current; s < (int)WorkStage.Count; s++)
            {
                var stage = (WorkStage)s;
                PlansRules.Span(model, stage, out double from, out _);
                bool now = building && s == p.StageSeen;
                int day = now ? today : Math.Max(today + 1, PlanningRules.AfterWorkingDays(start, (int)Math.Round(from * working)));
                for (int m = 0; m < (int)Material.Count; m++)
                {
                    int tenths = now ? Math.Max(0, p.Need[m] - p.Have[m] - p.OnOrder[m]) : StageNeed(p, stage, (Material)m);
                    if (tenths <= 0)
                    {
                        continue;
                    }
                    int factor = exchange != null && m < exchange.Length && exchange[m] > 0 ? exchange[m] : 1000;
                    long money = PlansRules.Cost(tenths, PlansRules.LoadPrice((Material)m, prices, factor, BuildCatalogue.MerchantPricePermille[0]));
                    money = money * Math.Max(1, materialsPermille) / 1000L;
                    into.Add(new MaterialNeed { Material = (Material)m, Stage = stage, Tenths = tenths, Day = day, Money = money });
                }
            }
            return into.Count;
        }

        /// <summary>The money of a project's purchases to come of one material, and the tenths of a load, over every stage.</summary>
        public static long Total(List<MaterialNeed> needs, Material material, out int tenths)
        {
            long money = 0;
            tenths = 0;
            for (int i = 0; needs != null && i < needs.Count; i++)
            {
                if (needs[i].Material == material)
                {
                    money += needs[i].Money;
                    tenths += needs[i].Tenths;
                }
            }
            return money;
        }

        /// <summary>The working days after one day up to and with another.</summary>
        public static int WorkingDaysBetween(int from, int to)
        {
            int n = 0;
            for (int d = from + 1; d <= to && n < 4000; d++)
            {
                n += SiteRules.WorkingDay(d) ? 1 : 0;
            }
            return n;
        }

        /// <summary>A hedge's futures that have ended folded into what it made: each look's result kept, an ended one's added to <c>HedgeEnded</c>.</summary>
        public static int Fold(ProjectFile p, Func<int, HedgeLook> look)
        {
            int ended = 0;
            for (int i = p.Hedges.Count - 1; i >= 0; i--)
            {
                HedgeHold h = p.Hedges[i];
                HedgeLook l = look != null ? look(h.Contract) : default(HedgeLook);
                if (l.Known)
                {
                    h.Result = l.Result;
                    p.Hedges[i] = h;
                }
                if (!l.Known || !l.Open)
                {
                    p.HedgeEnded += h.Result;   // gone from the book or ended: its last result is what it made
                    p.Hedges.RemoveAt(i);
                    ended++;
                }
            }
            return ended;
        }
    }

    /// <summary>A future on the exchange's book as a project's hedge looks at it: known, open, what it has made in cents.</summary>
    public struct HedgeLook
    {
        public bool Known, Open;
        public long Result;
    }
}
