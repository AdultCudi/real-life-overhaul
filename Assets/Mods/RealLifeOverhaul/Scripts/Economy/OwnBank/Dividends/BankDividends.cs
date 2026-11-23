namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The bank's automatic dividends (Economy/OwnBank/Dividends), game-free so the offline checks read the same table: how
    /// often one is paid (every seven days, every game month, every quarter of three months or every game year), the share of the profit the bank kept over
    /// that time, and why one came to less than asked or nothing. A dividend is discretionary, so it stops short of the
    /// reserve, the capital's least and the lending line, and of the fixed pay held back for the owner (OwnBankRules.OutRoom).
    /// </summary>
    public static class BankDividends
    {
        // How often, saved as a number: never renumber.
        public const int Off = 0, Weekly = 1, Monthly = 2, Yearly = 3, Quarterly = 4, Count = 5;
        public static readonly string[] EveryKeys = { "reallife_ob_div_off", "reallife_ob_div_week", "reallife_ob_div_month", "reallife_ob_div_year", "reallife_ob_div_quarter" };
        public const int WeekDays = 7;                 // days: "every 7 days", whatever the game's month
        public const int QuarterMonths = 3;            // game months in a quarter

        /// <summary>The order the choices are shown in, shortest time first: the saved numbers are not renumbered, so Quarterly is shown before Yearly.</summary>
        public static readonly int[] Order = { Off, Weekly, Monthly, Quarterly, Yearly };

        /// <summary>Where a choice stands in <see cref="Order"/>.</summary>
        public static int PositionOf(int every)
        {
            for (int i = 0; i < Order.Length; i++)
            {
                if (Order[i] == every)
                {
                    return i;
                }
            }
            return 0;
        }

        public const int ShareStep = 50;               // thousandths: the slider moves in steps of 5%
        public const int ShareMost = 1000;             // thousandths: all of the profit kept over the time

        // Why a dividend came to less than asked, or nothing. Saved as a number: never renumber.
        public const int Paid = 0;
        public const int NoProfit = 1;                 // the bank kept no profit over the time
        public const int Breach = 2;                   // the regulator's breach: nothing is paid out
        public const int Run = 3;                      // a run: nothing is paid out
        public const int Reserves = 4;                 // its cash over the reserve kept against its savings was short
        public const int Capital = 5;                  // its capital over its least, its lending line and the owner's fixed pay was short
        public const int Test = 6;                     // a test's bank pays nothing out
        public const int Watch = 7;                    // watched by the regulator with the bank risk on: no dividend while it is
        public const int WhyMost = Watch;
        public static readonly string[] WhyKeys =
        {
            null, "reallife_ob_div_why_profit", "reallife_ob_div_why_breach", "reallife_ob_div_why_run", "reallife_ob_div_why_reserves", "reallife_ob_div_why_capital",
            "reallife_ob_div_why_test", "reallife_ob_div_why_watch"
        };

        /// <summary>Days between two dividends; 0 while they are off.</summary>
        public static int Days(int every, int monthDays, int yearDays)
        {
            return every == Weekly ? WeekDays : every == Monthly ? System.Math.Max(1, monthDays) : every == Yearly ? System.Math.Max(1, yearDays)
                : every == Quarterly ? System.Math.Max(1, System.Math.Min(yearDays, QuarterMonths * monthDays)) : 0;
        }

        /// <summary>A share for the dividends, on its step and within its bounds.</summary>
        public static int ShareOf(int share)
        {
            int s = share < 0 ? 0 : share > ShareMost ? ShareMost : share;
            return s / ShareStep * ShareStep;
        }
    }
}
