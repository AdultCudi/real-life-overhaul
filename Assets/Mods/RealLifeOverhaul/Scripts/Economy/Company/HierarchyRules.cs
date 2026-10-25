using System;
using System.Globalization;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The hierarchy's own rules beyond the policy and the escalations, game-free (EconomyCheck compiles them): the regions
    /// a player makes from their stores, and the reports' week.
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
