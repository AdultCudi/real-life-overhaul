using System;

namespace RealLifeOverhaul.Economy
{
    /// <summary>How one month of a unit's rent ended. Saved as a number.</summary>
    public enum PaymentState : byte
    {
        OnTime = 0,
        Late = 1,       // met, but not on the day
        Missed = 2      // still owed when the month ended
    }

    /// <summary>One month of a unit's rent record: the day the month's rent was complete, the day it was met, what it was and how it ended.</summary>
    public struct RentPayment
    {
        public int Due, Paid, Amount;
        public byte State;
    }

    /// <summary>A unit's rent record: the last months of rent, each with the day it was complete, the day it was met and how it ended. Game-free.</summary>
    public static class RentRecordRules
    {
        /// <summary>
        /// One morning of a let unit, after its tenancy's day: a missed day makes the running month late, arrears met (a lump) say on which
        /// day, and when the month is up it is written to the record, newest first, the oldest dropped.
        /// </summary>
        public static void Day(EstateUnit u, bool missed, int lump, int day, int monthDays)
        {
            if (u == null || !u.Let)
            {
                return;
            }
            monthDays = Math.Max(1, monthDays);
            if (u.PeriodFrom <= 0)
            {
                u.PeriodFrom = day;
            }
            if (missed)
            {
                u.PeriodLate++;
            }
            if (lump > 0 && u.PeriodLate > 0 && u.Arrears == 0)
            {
                u.PeriodPaid = day;
            }
            if (day - u.PeriodFrom + 1 < monthDays)
            {
                return;
            }
            PaymentState state = u.PeriodLate == 0 ? PaymentState.OnTime : u.Arrears > 0 ? PaymentState.Missed : PaymentState.Late;
            Push(u, new RentPayment
            {
                Due = day,
                Paid = state == PaymentState.Missed ? 0 : state == PaymentState.OnTime ? day : u.PeriodPaid > 0 ? u.PeriodPaid : day,
                Amount = Math.Max(0, u.Rent) * monthDays,
                State = (byte)state
            });
            u.PeriodFrom = day + 1;
            u.PeriodLate = 0;
            u.PeriodPaid = 0;
        }

        public static void Push(EstateUnit u, RentPayment p)
        {
            int keep = Math.Min(u.Payments.Length, u.PaymentCount + 1);
            for (int i = keep - 1; i > 0; i--)
            {
                u.Payments[i] = u.Payments[i - 1];
            }
            u.Payments[0] = p;
            u.PaymentCount = keep;
        }
    }
}
