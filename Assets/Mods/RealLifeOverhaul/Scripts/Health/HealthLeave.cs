using System;

namespace RealLifeOverhaul.Health
{
    /// <summary>What is true of a working day, as the leave rules read it.</summary>
    public struct LeaveFacts
    {
        public bool Ill;          // really ill or hurt that day (Home's illness, Life's sickness, an injury of Health's)
        public bool Note;         // a doctor's sick note covers it (Home's)
        public bool InHospital;   // in hospital that day
        public bool AtWorkInjury; // the case is an accident at work
    }

    /// <summary>
    /// Sick leave at the player's job, game-free. A day off sick is excused while the player is really ill, for <see cref="HealthCatalogue.SelfCertDays"/> working days
    /// running without a note and for as long as a doctor's or a hospital's note runs; a day called in sick while well is excused unless the employer sees through it, which
    /// is likelier each time. Sick pay is a working day's pay for the days the year allows, once a day, after the first days in a job, and never for a day worked. COST: a
    /// few comparisons a call.
    /// </summary>
    public static class HealthLeave
    {
        /// <summary>The paid sick days of a year of so many days.</summary>
        public static int PaidDays(int yearDays) => Math.Max(HealthCatalogue.PaidDaysLeast, yearDays * HealthCatalogue.PaidYearShare / 1000);

        /// <summary>The year turns: the paid days and the days called in start again.</summary>
        public static void TurnYear(LeaveBook l, int day, int yearDays)
        {
            int year = HealthRules.YearOf(day, yearDays);
            if (l.Year != year)
            {
                l.Year = year;
                l.PaidUsed = l.Claims = l.Noticed = 0;
                l.PaidMoney = 0;
            }
        }

        /// <summary>The sick working days running before a day: the spell's, if its last was close enough, else none.</summary>
        public static int SpellBefore(LeaveBook l, int day) => l.SpellLast > 0 && day > l.SpellLast && day - l.SpellLast <= HealthCatalogue.SpellGap ? l.SpellDays : 0;

        /// <summary>The chance the employer sees through a day called in sick while well, thousandths: more for each such day of the year before it.</summary>
        public static int NoticeChance(int claimsBefore) =>
            Math.Min(HealthCatalogue.NoticeMost, HealthCatalogue.NoticeFirst + Math.Max(0, claimsBefore) * HealthCatalogue.NoticeStep);

        /// <summary>True if the day the player called in sick while well is seen through. The same answer for the day however often it is asked.</summary>
        public static bool SeenThrough(int seed, int day, int claimsBefore) => HealthRules.Roll(seed, day, HealthRules.SlotNotice, 0, 1000) < NoticeChance(claimsBefore);

        /// <summary>
        /// True if a working day not worked is excused as sick. Asking changes nothing, so it may be asked any number of times; a day already settled answers as it was settled.
        /// </summary>
        public static bool Covers(LeaveBook l, LeaveFacts f, int day, int seed)
        {
            if (day == l.SpellLast)
            {
                return l.LastCovered;
            }
            if (f.InHospital || f.Note || Noted(l, day))
            {
                return true;
            }
            if (f.Ill)
            {
                return f.AtWorkInjury || SpellBefore(l, day) < HealthCatalogue.SelfCertDays;
            }
            if (l.ClaimDay == day && day > 0)
            {
                return !SeenThrough(seed, day, Math.Max(0, l.Claims - 1));
            }
            return false;
        }

        /// <summary>True if a hospital's note covers a day.</summary>
        public static bool Noted(LeaveBook l, int day) => l.NoteUntil > 0 && day >= l.NoteFrom && day <= l.NoteUntil;

        /// <summary>A hospital's note from a day to another: joined to one that still runs, so a second stay never shortens the first's.</summary>
        public static void Note(LeaveBook l, int from, int until)
        {
            l.NoteFrom = l.NoteUntil >= from && l.NoteUntil > 0 ? Math.Min(l.NoteFrom, from) : from;
            l.NoteUntil = Math.Max(l.NoteUntil, until);
        }

        /// <summary>The player calls in sick for today. False if they did already. A day called in while really ill is no claim against them.</summary>
        public static bool CallIn(LeaveBook l, int day, bool ill, int yearDays)
        {
            TurnYear(l, day, yearDays);
            if (l.ClaimDay == day)
            {
                return false;
            }
            l.ClaimDay = day;
            if (!ill)
            {
                l.Claims++;
            }
            return true;
        }

        /// <summary>
        /// A working day not worked is settled, once: the spell is counted, and what it was settled as is kept. Returns the sick pay due for it in cents: the day's pay while
        /// paid days are left (an accident at work is paid outside them), nothing in a job's first days, nothing for a day seen through, nothing twice.
        /// </summary>
        public static long Settle(LeaveBook l, LeaveFacts f, int day, int seed, int dayPayCents, int hiredDay, int yearDays)
        {
            if (day <= l.SpellLast)
            {
                return 0;
            }
            if (!f.Ill && !f.InHospital && !f.Note && !Noted(l, day) && l.ClaimDay != day)
            {
                return 0;   // not a sick day at all (the place was shut, the internet was down, the player stayed away): nothing of the leave's
            }
            TurnYear(l, day, yearDays);
            bool covered = Covers(l, f, day, seed);
            if (!f.Ill && !f.InHospital && !f.Note && l.ClaimDay == day && !covered)
            {
                l.Noticed++;
            }
            l.SpellDays = SpellBefore(l, day) + 1;
            l.SpellLast = day;
            l.LastCovered = covered;
            if (!covered || dayPayCents <= 0 || day <= l.PaidDay || day - hiredDay < HealthCatalogue.QualifyDays)
            {
                return 0;
            }
            if (!f.AtWorkInjury)
            {
                if (l.PaidUsed >= PaidDays(yearDays))
                {
                    return 0;
                }
                l.PaidUsed++;
            }
            l.PaidDay = day;
            l.PaidMoney += dayPayCents / 100;
            return dayPayCents;
        }
    }
}
