using System;

namespace RealLifeOverhaul.Health
{
    /// <summary>How a premium's day ended.</summary>
    public enum PremiumResult
    {
        None = 0,
        Paid = 1,
        Missed = 2,
        Lapsed = 3
    }

    /// <summary>How the medical debt's day ended.</summary>
    public enum DebtResult
    {
        None = 0,
        Paid = 1,
        Missed = 2,
        PaidOff = 3,
        Court = 4
    }

    /// <summary>
    /// The steps the director takes on a day or a press, game-free: money moves through the one call it is handed (<c>pay</c> takes whole money from the player and says
    /// whether it could), so the checks drive the very steps the game runs. An appointment's hour and whether it is kept, a premium's day, the debt's day, a bill met, an
    /// admission and a discharge, and the option switched off and on again. COST: a few operations a call.
    /// </summary>
    public static class HealthDay
    {
        // ---- the doctor's appointment

        /// <summary>The hour an appointment asked for now is given: two hours on today while the surgery still has hours, else the first hour tomorrow.</summary>
        public static void Slot(int day, int hour, out int slotDay, out int slotHour)
        {
            int wanted = Math.Max(HealthCatalogue.AppointmentHour, hour + 2);
            bool today = wanted <= HealthCatalogue.AppointmentLastHour;
            slotDay = today ? day : day + 1;
            slotHour = today ? wanted : HealthCatalogue.AppointmentHour;
        }

        public static bool Book(HealthAppointment a, int day, int hour, string street, int number)
        {
            if (a.Booked || string.IsNullOrEmpty(street))
            {
                return false;
            }
            Slot(day, hour, out a.Day, out a.Hour);
            a.Kept = false;
            a.Street = street;
            a.Number = number;
            return true;
        }

        /// <summary>True if the player arriving at the doctor's now keeps the appointment: on its day, from a little before its hour to a little after.</summary>
        public static bool InWindow(HealthAppointment a, int day, int minuteOfDay)
        {
            int at = a.Hour * 60;
            return a.Booked && day == a.Day && minuteOfDay >= at - HealthCatalogue.AppointmentEarly && minuteOfDay <= at + HealthCatalogue.AppointmentLate;
        }

        /// <summary>True once an appointment not kept can no longer be: its window is past.</summary>
        public static bool Missed(HealthAppointment a, int day, int minuteOfDay) =>
            a.Booked && !a.Kept && (day > a.Day || (day == a.Day && minuteOfDay > a.Hour * 60 + HealthCatalogue.AppointmentLate));

        // ---- the premium

        /// <summary>The policy's day: a premium due is taken, or missed, and the second missed running ends the policy.</summary>
        public static PremiumResult Premium(HealthState s, int day, int age, int yearDays, int monthDays, int level, Func<long, bool> pay, out int amount)
        {
            amount = 0;
            HealthPolicy p = s.Policy;
            if (!HealthCover.Due(p, day))
            {
                return PremiumResult.None;
            }
            amount = HealthCover.Premium(p.Plan, age, HealthCover.ClaimsLastYear(p, day, yearDays), level);
            if (pay(amount))
            {
                HealthCover.Paid(p, day, monthDays);
                return PremiumResult.Paid;
            }
            return HealthCover.Missed(p, day, monthDays) ? PremiumResult.Lapsed : PremiumResult.Missed;
        }

        /// <summary>
        /// A plan is taken or changed: refused for the plan held already, and the first month is paid before anything is written, so a player who cannot pay has no cover.
        /// </summary>
        public static bool TakePlan(HealthState s, Plan plan, int day, int age, int yearDays, int monthDays, int level, Func<long, bool> pay, out int amount)
        {
            amount = 0;
            if (plan == Plan.None || plan >= Plan.Count || plan == s.Policy.Plan)
            {
                return false;
            }
            amount = HealthCover.Premium(plan, age, HealthCover.ClaimsLastYear(s.Policy, day, yearDays), level);
            if (!pay(amount))
            {
                return false;
            }
            HealthCover.TurnYear(s, day, yearDays);
            HealthCover.Take(s.Policy, plan, day, monthDays);
            return true;
        }

        // ---- the debt

        /// <summary>
        /// The debt's day, once a day whatever else is on or off: the instalment is taken from the cash that is there or the day is counted late, and after
        /// <see cref="HealthCatalogue.DebtClaimAfter"/> late days running the lender files its claim with the court (<paramref name="file"/> answers the court's number, or 0
        /// with no court, and then the debt stays owed here and is tried again each day).
        /// </summary>
        public static DebtResult Debt(HealthState s, int day, Func<long, bool> pay, Func<int, int> file, out long amount)
        {
            HealthDebt d = s.Debt;
            amount = HealthCover.Instalment(d, day);
            if (amount <= 0)
            {
                return DebtResult.None;
            }
            bool taken = pay(amount);
            if (HealthCover.Collected(d, day, taken ? amount : 0))
            {
                return DebtResult.PaidOff;
            }
            if (taken)
            {
                return DebtResult.Paid;
            }
            if (HealthCover.GoesToCourt(d) && file != null)
            {
                amount = d.Owed;
                int claim = file((int)Math.Min(int.MaxValue / 2, d.Owed));
                if (claim > 0)
                {
                    HealthCover.Filed(d, claim);
                    return DebtResult.Court;
                }
            }
            return DebtResult.Missed;
        }

        /// <summary>The whole debt paid now, if the cash is there.</summary>
        public static bool PayOff(HealthState s, int day, Func<long, bool> pay, out long amount)
        {
            amount = s.Debt.Owed;
            if (amount <= 0 || !pay(amount))
            {
                return false;
            }
            int was = s.Debt.LastDay;
            HealthCover.Collected(s.Debt, day, amount);
            s.Debt.LastDay = Math.Max(was, s.Debt.LastDay);
            return true;
        }

        // ---- a bill

        /// <summary>
        /// A bill is met. The plan that covers it pays its part (HealthCover.Settle). A bill already paid at the desk (<paramref name="prepaid"/>: Home's doctor takes the fee
        /// itself) leaves only the plan's part to come back to the player; any other is paid from the cash that is there, in full or in part, and what is left is owed as
        /// the medical debt. Nobody is taken under nothing and nobody is refused care.
        /// </summary>
        public static BillResult Bill(HealthState s, Care care, long bill, bool prepaid, Plan employer, int employerSince, int day, int yearDays, int monthDays, int level,
            long spare, Func<long, bool> pay)
        {
            HealthCase c = s.Case;
            int began = c.Open ? c.Began : day;
            BillResult r = HealthCover.Settle(s, care, bill, began, c.Open && c.Work, employer, employerSince, day, yearDays, level);
            long due = r.Bill - r.Claim;
            if (prepaid)
            {
                r.Paid = due;
                return r;
            }
            long can = Math.Min(due, Math.Max(0L, spare));
            if (can > 0 && pay(can))
            {
                r.Paid = can;
            }
            r.Owed = due - r.Paid;
            HealthCover.Owe(s.Debt, r.Owed, monthDays, day);
            return r;
        }

        // ---- the hospital

        /// <summary>
        /// The player is admitted for the serious case: a stay of the condition's days, the first of them the game's own night where the game put them there. False without a
        /// serious case, or in hospital already.
        /// </summary>
        public static bool Admit(HealthState s, int nowMinutes, bool gameNight)
        {
            HealthCase c = s.Case;
            HealthStay st = s.Stay;
            if (!c.Open || !c.Serious || st.Active)
            {
                return false;
            }
            st.Active = true;
            st.Kind = c.Kind;
            st.Days = HealthRules.StayDays(c.Kind);
            st.GameNight = gameNight;
            st.From = nowMinutes;
            st.Until = gameNight ? nowMinutes + (st.Days - 1) * 1440 : HealthRules.StayUntil(nowMinutes, st.Days);
            return true;
        }

        /// <summary>The bill of the stay as it stands: every day of it at the discharge, or the days begun so far if it is cut short.</summary>
        public static long StayBill(HealthStay st, int nowMinutes, int level)
        {
            if (!st.Active)
            {
                return 0;
            }
            int days = nowMinutes >= st.Until ? st.Days : HealthCatalogue.Clamp((nowMinutes - st.From) / 1440 + 1, 1, st.Days);
            return HealthCover.StayBill(days, st.GameNight, level);
        }

        /// <summary>The stay ends: the case is treated, a note covers its days and the two after, and the stay is closed. The bill is the caller's to settle first.</summary>
        public static void Discharge(HealthState s, int day)
        {
            HealthStay st = s.Stay;
            if (!st.Active)
            {
                return;
            }
            HealthLeave.Note(s.Leave, st.From / 1440, day + HealthCatalogue.NoteAfterStay);
            HealthRules.Discharged(s.Case, day);
            st.Clear();
        }

        // ---- the option

        /// <summary>
        /// The option is switched off: the case under way ends, the appointment is dropped with no fee, and a stay is cut short (its bill for the days begun is settled by
        /// the caller first, <see cref="StayBill"/>). What is owed stays owed, and the policy's record stays in the line.
        /// </summary>
        public static void Clear(HealthState s)
        {
            s.Stay.Clear();
            s.Case.Clear();
            s.Appointment.Clear();
            s.Staff.Clear();
            s.Pop.Cursor = -1;
        }

        /// <summary>
        /// The option is on again (or a save loads): the game's nights in hospital while it was away are not claimed afterwards, and a policy whose premiums were not taken
        /// for a month or more has lapsed, so nobody keeps cover by switching the option off between bills. True if it lapsed.
        /// </summary>
        public static bool BackOn(HealthState s, int day, int gameStays, int monthDays)
        {
            s.GameStays = gameStays;
            HealthPolicy p = s.Policy;
            if (p.Plan != Plan.None && p.NextDue > 0 && day >= p.NextDue + Math.Max(1, monthDays))
            {
                HealthCover.Cancel(p);
                return true;
            }
            return false;
        }
    }
}
