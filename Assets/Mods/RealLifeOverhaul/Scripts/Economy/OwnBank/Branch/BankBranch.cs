using System;

namespace RealLifeOverhaul.Economy
{
    /// <summary>What a branch's figures count, by their place in <see cref="BankBranch.Month"/>. Saved by place: never renumber.</summary>
    public static class BranchFigure
    {
        public const int Visitors = 0;      // came in
        public const int Served = 1;        // seen to at the counter
        public const int Lost = 2;          // left without being seen to
        public const int Opened = 3;        // accounts opened
        public const int PaidIn = 4;        // cents paid in (accounts opened and deposits)
        public const int TakenOut = 5;      // cents taken out
        public const int Loans = 6;         // loans arranged
        public const int Lent = 7;          // cents lent
        public const int Refused = 8;       // loans asked for and turned down
        public const int Fees = 9;          // cents charged at the counter
        public const int Count = 10;
    }

    /// <summary>
    /// One of the player's bank branches as its bank keeps it: where it is, the visits its counter saw to on each of the last
    /// seven days (the reach is worked from them), this month's and last month's figures, and the part of a visitor carried
    /// to the next hour. Its money is the bank's own (OwnBankRules.Visit): nothing here is a second set of books.
    /// </summary>
    public sealed class BankBranch
    {
        public string Place = string.Empty;     // its address as the save names it (street|number)
        public string Name = string.Empty;      // the name the player gave the business, else its address
        public int Carry;                       // thousandths of a visitor carried to the next hour
        public int MonthFrom;                   // the first day of the month being added up
        public readonly int[] Days = new int[BranchCatalogue.WeekDays];      // the day each slot of the week is of
        public readonly int[] Week = new int[BranchCatalogue.WeekDays];      // visits seen to on that day
        public readonly long[] Month = new long[BranchFigure.Count];
        public readonly long[] Last = new long[BranchFigure.Count];          // last month's

        // What the hour last worked brought (not saved: the floor shows it while the player is there).
        public int HourIndex = int.MinValue;    // day x 24 + hour
        public int HourVisitors, HourServed, HourTellers;
        public bool HourOpen;

        public BankBranch()
        {
            for (int i = 0; i < Days.Length; i++)
            {
                Days[i] = int.MinValue;
            }
        }

        /// <summary>Visits seen to in the seven days up to and including <paramref name="today"/>.</summary>
        public int WeekServed(int today)
        {
            int sum = 0;
            for (int i = 0; i < Week.Length; i++)
            {
                if (Days[i] != int.MinValue && Days[i] <= today && today - Days[i] < BranchCatalogue.WeekDays)
                {
                    sum += Math.Max(0, Week[i]);
                }
            }
            return sum;
        }

        /// <summary>Visits seen to on a day, counted into its slot of the week (a slot of a week ago is begun again).</summary>
        public void CountServed(int day, int served)
        {
            int slot = ((day % BranchCatalogue.WeekDays) + BranchCatalogue.WeekDays) % BranchCatalogue.WeekDays;
            if (Days[slot] != day)
            {
                Days[slot] = day;
                Week[slot] = 0;
            }
            Week[slot] += Math.Max(0, served);
        }
    }
}
