using System;
using System.Globalization;
using RealLifeOverhaul.Careers;

namespace RealLifeOverhaul.Economy
{
    /// <summary>What the escalations raised in a week were about, counted for the executives' proposals. Never saved.</summary>
    public struct WeekFigures
    {
        public int Gaps, Budgets, People, Kpis;     // people: requests, standing, leavers and exceptions
    }

    /// <summary>The figures the regional managers' replacing goes by, game-free so the rules and the director share them.</summary>
    public static class RegionCatalogue
    {
        public const int ReplaceScore = 50;         // a place's score under which its manager may be replaced, 0 to 100
        public const int ReplaceRuns = 2;           // ...after this many times the policy's days off target in a row
    }

    /// <summary>
    /// The hierarchy's own rules beyond the policy and the escalations, game-free (EconomyCheck compiles them): the regions
    /// a player makes from their stores, a regional manager replacing a store's, an executive's proposal, and the reports' week.
    /// </summary>
    public static class HierarchyRules
    {
        public const int CustomMost = 8;            // regions a player can make of their own, named A to H
        private const string CustomPrefix = "~region:";   // no district of the game's is called so

        /// <summary>The key of a region the player makes, 1 to CustomMost.</summary>
        public static string CustomArea(int number)
        {
            return CustomPrefix + CompanyCatalogue.Clamp(number, 1, CustomMost).ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>The number of a region the player made, 1 to CustomMost; 0 for a district.</summary>
        public static int CustomNumber(string area)
        {
            int n;
            if (string.IsNullOrEmpty(area) || !area.StartsWith(CustomPrefix, StringComparison.Ordinal)
                || !int.TryParse(area.Substring(CustomPrefix.Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out n))
            {
                return 0;
            }
            return n >= 1 && n <= CustomMost ? n : 0;
        }

        /// <summary>The letter a region the player made is named by.</summary>
        public static string Letter(int number)
        {
            return ((char)('A' + CompanyCatalogue.Clamp(number, 1, CustomMost) - 1)).ToString();
        }

        public const int ProposalDays = 7;          // days at least between two proposals of one executive
        public const int ExecutivePremium = 250;    // thousandths of a day's pay at their going rate an executive is paid a day on top of it

        /// <summary>
        /// An executive's allowance a day, whole money, on top of the pay they have as a professional: a share of a working
        /// day at the going rate of their role and skill (the talent catalogue's, cents an hour).
        /// </summary>
        public static int ExecutiveAllowance(int goingCents)
        {
            long cents = (long)Math.Max(0, goingCents) * RealLifeOverhaul.Tech.TalentCatalogue.DayHours * ExecutivePremium / 1000;
            return (int)Math.Min(int.MaxValue / 4, (cents + 50) / 100);
        }

        /// <summary>
        /// Whether a store's manager is due to be replaced by the regional manager: their place off its targets for twice
        /// the policy's days in a row, and scoring under the line.
        /// </summary>
        public static bool ReplaceDue(int kpiRun, int kpiDays, int score)
        {
            return kpiRun >= Math.Max(1, kpiDays) * RegionCatalogue.ReplaceRuns && score < RegionCatalogue.ReplaceScore;
        }

        /// <summary>
        /// An executive's proposal for the week, if it asks for one: the operations chief lets gaps be filled from further
        /// when two or more were escalated, else has the regional managers replace managers off target; the people chief
        /// widens the pay band when two or more people's matters were, else the room past it; the finance chief raises the
        /// managers' budget when two or more budgets ran out, else the top-up. Each only a step looser than the company's now,
        /// and none past a rule's last step.
        /// </summary>
        public static bool Propose(ExecSeat seat, WeekFigures week, PolicySet company, out PolicyField field, out int step)
        {
            field = PolicyField.Count;
            step = -1;
            int count = seat == ExecSeat.Coo ? Math.Max(week.Gaps, week.Kpis) : seat == ExecSeat.Chro ? week.People : week.Budgets;
            if (count < 2)
            {
                return false;
            }
            PolicyField first, second;
            switch (seat)
            {
                case ExecSeat.Coo:
                    first = week.Gaps >= 2 ? PolicyField.Sources : PolicyField.RegionReplaces;
                    second = PolicyField.RegionReplaces;
                    break;
                case ExecSeat.Chro:
                    first = PolicyField.PayBand;
                    second = PolicyField.ExceptionMargin;
                    break;
                case ExecSeat.Cfo:
                    first = PolicyField.Budget;
                    second = PolicyField.FinanceMost;
                    break;
                default:
                    return false;
            }
            return Looser(company, first, out field, out step) || Looser(company, second, out field, out step);
        }

        // A rule one step looser than the company's now, if it has one.
        private static bool Looser(PolicySet company, PolicyField rule, out PolicyField field, out int step)
        {
            PolicyDef d = PolicyCatalogue.Def(rule);
            int now = company != null && company.Sets(rule) ? company.Of(rule) : d.Default;
            field = rule;
            step = d.Way == PolicyWay.LooseDown ? now - 1 : now + 1;
            return step >= 0 && step < d.Steps.Length;
        }

        /// <summary>Whether a week of the reports has ended since it began.</summary>
        public static bool WeekOver(int from, int day)
        {
            return day - from >= 7;
        }

        /// <summary>Whether the managers' notice of the day's decisions goes out today under the policy: every day, on a Sunday, or never.</summary>
        public static bool DigestDue(int reportsStep, int day)
        {
            int reports = PolicyCatalogue.Value(PolicyField.Reports, reportsStep);
            return reports == PolicyCatalogue.ReportDaily || (reports == PolicyCatalogue.ReportWeekly && ManagerRules.Weekday(day) == ManagerCatalogue.Sunday);
        }
    }
}
