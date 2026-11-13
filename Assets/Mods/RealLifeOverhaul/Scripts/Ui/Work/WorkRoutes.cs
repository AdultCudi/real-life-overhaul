using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Where a notice, a button or another app that asks for a tab of Career, Campus or Open Roles lands on the kit. The old apps number their tabs
    /// one way and the kit's pages another, so a request is mapped by what the tab is, never by its number. Game-free: the checks run it.
    /// </summary>
    public static class WorkRoutes
    {
        // The kit's Career tabs, the same numbers as CareerPages (a check reads both): kept here so the rules stay free of the game's types.
        public const int Professions = 0, Jobs = 1, Applications = 2, MyJob = 3, Management = 4, Own = 5, Cv = 6;

        /// <summary>The Career tab of the old app (Start, Jobs, Applications, My job, CV, Own work, Ladder) as the kit numbers it.</summary>
        public static int CareerTab(int oldTab)
        {
            switch (oldTab)
            {
                case 0: return Professions;
                case 1: return Jobs;
                case 2: return Applications;
                case 3: return MyJob;
                case 4: return Cv;
                case 5: return Own;
                case 6: return Management;   // the old Ladder tab is the kit's Management page
                default: return -1;
            }
        }

        /// <summary>The Open Roles tab of the old app (Team, Jobs, Applicants, Market, Search) as the kit numbers it (Jobs, Applicants, Market, Search, Team).</summary>
        public static int RolesTab(int oldTab)
        {
            switch (oldTab)
            {
                case 0: return 4;
                case 1: return 0;
                case 2: return 1;
                case 3: return 2;
                case 4: return 3;
                default: return -1;
            }
        }

        /// <summary>The id after a row kind ("post:12" with "post:" gives 12), or -1 when the pick is not of that kind.</summary>
        public static int PickId(string pick, string kind)
        {
            if (string.IsNullOrEmpty(pick) || !pick.StartsWith(kind, StringComparison.Ordinal))
            {
                return -1;
            }
            return int.TryParse(pick.Substring(kind.Length), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int id) ? id : -1;
        }

        /// <summary>
        /// The kit's Career tab for a pick the old app took, when the pick names a row of one list: a profession, a post or an application. The old
        /// tab is the answer when the pick names none.
        /// </summary>
        public static int CareerTabOfPick(int oldTab, string pick)
        {
            if (PickId(pick, "prof:") >= 0)
            {
                return Professions;
            }
            if (PickId(pick, "post:") >= 0)
            {
                return Jobs;
            }
            if (PickId(pick, "app:") >= 0)
            {
                return Applications;
            }
            return CareerTab(oldTab);
        }
    }
}
