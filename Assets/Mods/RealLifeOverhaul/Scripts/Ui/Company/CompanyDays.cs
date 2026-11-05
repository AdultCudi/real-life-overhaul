namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The arithmetic of the Company app's counts and calendar that more than one place needs: the sidebar's counts, a campaign's days and a tax quarter's days. Game-free, so the live
    /// source and the samples (and their checks) write the same figure from the same numbers, and no page can say a different day or count from the page beside it. Cost: a few integer
    /// operations when a page is read.
    /// </summary>
    public static class CompanyDays
    {
        /// <summary>The sidebar's counts: the staff, the managers, what waits on each of the others; null where there is nothing to count.</summary>
        public static string[] Counts(int staff, int managers, int shopsMissingLicence, bool returnWaiting, int openClaims, int campaigns)
        {
            var counts = new string[CompanyView.Tabs];
            counts[CompanyView.Staff] = Count(staff);
            counts[CompanyView.Managers] = Count(managers);
            counts[CompanyView.Tax] = Count(shopsMissingLicence + (returnWaiting ? 1 : 0));
            counts[CompanyView.Cover] = Count(openClaims);
            counts[CompanyView.Marketing] = Count(campaigns);
            return counts;
        }

        private static string Count(int n) => n > 0 ? n.ToString() : null;

        /// <summary>A campaign booked from <paramref name="from"/> up to <paramref name="end"/>, the first day without it, as it stands on <paramref name="today"/>.</summary>
        public struct Campaign
        {
            public int Length;      // days it runs
            public int Done;        // the day of it today, from 0 before it starts to its length once it is over
            public int Left;        // days still to run
            public int Last;        // its last day
            public int Report;      // the day its report comes

            public static Campaign Of(int from, int end, int today)
            {
                int length = end - from < 1 ? 1 : end - from;
                int done = today - from + 1;
                done = done < 0 ? 0 : done > length ? length : done;
                return new Campaign { Length = length, Done = done, Left = length - done, Last = end - 1, Report = end };
            }
        }

        /// <summary>A tax quarter from <paramref name="from"/> of <paramref name="days"/> days.</summary>
        public struct Quarter
        {
            public int Last;        // its last day
            public int Close;       // the day it closes and the next begins
            public int Day;         // the day of it today, within its length
            public int ReturnDue;   // the last day to file its return

            public static Quarter Of(int from, int days, int today, int returnDays)
            {
                int day = today - from + 1;
                return new Quarter { Last = from + days - 1, Close = from + days, Day = day < 0 ? 0 : day > days ? days : day, ReturnDue = from + days + returnDays };
            }
        }
    }
}
