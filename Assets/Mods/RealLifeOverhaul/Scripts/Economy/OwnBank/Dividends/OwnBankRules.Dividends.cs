using System;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The bank's automatic dividends (BankDividends) in its rules: the time's kept profit added up a day at a time, and at
    /// its end a share of it paid out of the capital and the reserves together, as far as the bank may pay a dividend at all,
    /// then the next time begun. Game-free; a few sums a day, nothing allocated.
    /// </summary>
    public static partial class OwnBankRules
    {
        /// <summary>How often and what share; a change of how often begins the time again today, with nothing kept towards it yet.</summary>
        public static void SetDividends(OwnBank b, int every, int share, int today)
        {
            int e = Clamp(every, BankDividends.Off, BankDividends.Count - 1);
            if (e != b.DivEvery)
            {
                b.DivFrom = today;
                b.DivKept = 0;
            }
            b.DivEvery = e;
            b.DivShare = BankDividends.ShareOf(share);
        }

        /// <summary>The first day the next dividend is not yet paid on: it is paid at the end of the day before. 0 while they are off.</summary>
        public static int DividendDay(OwnBank b, int monthDays, int yearDays)
        {
            int days = BankDividends.Days(b.DivEvery, monthDays, yearDays);
            return days > 0 ? b.DivFrom + days - 1 : 0;
        }

        /// <summary>
        /// The end of a day for the dividends, after the owner's pay: the profit kept that day added to the time's, and at the
        /// time's end a share of it paid, short or not at all with why (BankDividends.Paid and the rest) in into.PaidOut and
        /// the bank's DivWanted, DivPaid and DivWhy. Nothing while they are off.
        /// </summary>
        public static void Dividends(OwnBank b, BankCity c, int day, BankDay into)
        {
            int days = BankDividends.Days(b.DivEvery, c.MonthDays, c.YearDays);
            if (!b.Running || days <= 0)
            {
                return;
            }
            b.DivKept += into.Profit - into.Pay;
            if (day + 1 - b.DivFrom < days)
            {
                return;
            }
            long wanted = b.DivKept > 0 ? b.DivKept * BankDividends.ShareOf(b.DivShare) / BankDividends.ShareMost : 0;
            int why = BankDividends.Paid;
            long paid = 0;
            if (b.Test)
            {
                why = BankDividends.Test;
            }
            else if (wanted <= 0)
            {
                why = BankDividends.NoProfit;
            }
            else if (b.Stage == Regulator.Breach || b.Run)
            {
                why = b.Run ? BankDividends.Run : BankDividends.Breach;
            }
            else
            {
                long cash = FreeCash(b), capital = CapitalNow(b) - Keep(b, c.Level) - PayAhead(b);
                paid = Math.Max(0, Math.Min(wanted, Math.Min(cash, capital)));
                if (paid < wanted)
                {
                    why = cash <= capital ? BankDividends.Reserves : BankDividends.Capital;
                }
            }
            b.Capital -= paid;
            b.Reserves -= paid;
            b.DivAll += paid;
            b.DivDay = day;
            b.DivWanted = wanted;
            b.DivPaid = paid;
            b.DivWhy = why;
            b.DivFrom = day + 1;
            b.DivKept = 0;
            into.PaidOut = paid;
            into.DividendDue = true;
        }
    }
}
