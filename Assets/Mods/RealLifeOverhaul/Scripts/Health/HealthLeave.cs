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
        public bool InCare;       // ...and the player was in care by then: in hospital, under a hospital's note, or treated
        public int WorkDays;      // ...and so many days had passed since the accident
    }

    /// <summary>
    /// Sick leave at the player's job, game-free. A day off sick is excused while the player is really ill, for <see cref="HealthCatalogue.SelfCertDays"/> working days
    /// running without a note and for as long as a doctor's or a hospital's note runs; a day called in sick while well is excused unless the employer sees through it, which
    /// is likelier each time. Sick pay is the pay of the part of a working day not worked, for the days the year allows, once a day, after the first days in a job. An accident
    /// at work is paid outside the year's days only while the player is in care, and for the first <see cref="HealthCatalogue.WorkFreeDays"/> days whatever they do, and never
    /// for longer than <see cref="HealthCatalogue.WorkPayDays"/> days from the accident. COST: a few comparisons a call.
    /// </summary>
    public static class HealthLeave
    {
        /// <summary>The paid sick days of a year of so many days.</summary>
        public static int PaidDays(int yearDays) => Math.Max(HealthCatalogue.PaidDaysLeast, yearDays * HealthCatalogue.PaidYearShare / 1000);

        /// <summary>
        /// The year turns: the paid days and the days called in start again, and the year that ended keeps its paid days so a day of it settled late still counts against it.
        /// The year only moves forward: a day of a year gone by, settled after the year turned, changes nothing here.
        /// </summary>
        public static void TurnYear(LeaveBook l, int day, int yearDays)
        {
            int year = HealthRules.YearOf(day, yearDays);
            if (year > l.Year)
            {
                l.PrevYear = l.Year >= 0 && year == l.Year + 1 ? l.Year : -1;
                l.PrevPaidUsed = l.PrevYear >= 0 ? l.PaidUsed : 0;
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
        /// True while an accident at work is paid as one: the first days whatever the player does, and after them only while they are in care, and never beyond the most days
        /// from the accident. An accident left without care stops being paid like that after a few days, and is a sick leave like any other from then on.
        /// </summary>
        public static bool WorkPaid(LeaveFacts f) =>
            f.AtWorkInjury && f.WorkDays < HealthCatalogue.WorkPayDays && (f.InCare || f.WorkDays < HealthCatalogue.WorkFreeDays);

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
                return WorkPaid(f) || SpellBefore(l, day) < HealthCatalogue.SelfCertDays;
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

        /// <summary>
        /// The player's case is seen open on a day: the span of days it ran, and the day care began on it, are kept, so a working day settled later (the working life settles
        /// yesterday in the morning, and a case cleared that morning is still the case that day was lived in) is judged by what was true on it.
        /// </summary>
        public static void Observe(LeaveBook l, bool open, int began, bool work, bool cared, int day)
        {
            if (!open)
            {
                return;
            }
            if (l.CaseFrom != began)
            {
                l.CaseFrom = began;
                l.CaseTo = began;
                l.CareFrom = 0;
            }
            l.CaseTo = Math.Max(l.CaseTo, day);
            l.CaseWork = work;
            if (cared && l.CareFrom == 0)
            {
                l.CareFrom = day;
            }
        }

        /// <summary>
        /// The player's case is seen open through a day (<paramref name="last"/>): an injury is healed on its day, so it ran through the day before that at most. The state's
        /// own case goes in, and <paramref name="cared"/> says the player is in hospital.
        /// </summary>
        public static void ObserveCase(LeaveBook l, HealthCase c, bool cared, int last)
        {
            if (c.Open && !c.Serious && c.Until > 0)
            {
                last = Math.Min(last, c.Until - 1);
            }
            Observe(l, c.Open, c.Began, c.Work, c.Treated || cared, last);
        }

        /// <summary>True if a day was a day of the player's case (the one open now from its first day, or the last one that ran).</summary>
        public static bool InSpan(LeaveBook l, int day) => l.CaseFrom > 0 && day >= l.CaseFrom && day <= l.CaseTo;

        /// <summary>
        /// What was true of a working day, for the leave rules: a day gone by is judged by the case as it was on that day (the one open now from its first day, or the span of the
        /// last one that ran, which a clearing that morning leaves in the leave book), so the order of the working life's settling of yesterday and health's own day does not
        /// matter. <paramref name="wasIll"/> and <paramref name="note"/> are what the mod's older rules and Home's doctor say of the day.
        /// </summary>
        public static LeaveFacts FactsFor(HealthState s, int day, int today, bool wasIll, bool note)
        {
            HealthCase c = s.Case;
            HealthStay st = s.Stay;
            LeaveBook l = s.Leave;
            bool open = c.Open && c.Began <= day;
            bool span = !open && InSpan(l, day);
            bool cased = open || span;
            int began = open ? c.Began : span ? l.CaseFrom : 0;
            bool work = open ? c.Work : span && l.CaseWork;
            bool cared = cased && (Noted(l, day) || (l.CaseFrom == began && l.CareFrom > 0 && day >= l.CareFrom) || (open && day >= today && (c.Treated || st.Active)));
            return new LeaveFacts
            {
                Ill = cased || wasIll,
                Note = note,
                InHospital = st.Active && day >= st.From / 1440 && day <= st.Until / 1440,
                AtWorkInjury = cased && work,
                InCare = cased && work && cared,
                WorkDays = cased ? Math.Max(0, day - began) : 0
            };
        }

        /// <summary>The minute of the day a profession's shift ends: its hour and its length (ProRules.DayOver reads the same).</summary>
        public static int ProShiftEnd(int hour, int hours) => (hour + hours) * 60;

        /// <summary>The minute of the day a street career's shift is over: the game's own no-show begins that many late hours after its hour.</summary>
        public static int StreetShiftEnd(int rotaHour, int lateHours) => (rotaHour + lateHours) * 60;

        /// <summary>True while a shift's hours are not over: there is one (its end is a minute of the day, -1 for none) and it is before that minute.</summary>
        public static bool ShiftLeft(int minuteOfDay, int shiftEndMinute) => shiftEndMinute > 0 && minuteOfDay < shiftEndMinute;

        /// <summary>True if the player may call in sick: there is a shift today, it is not over, and they have not called in already.</summary>
        public static bool CanCallIn(LeaveBook l, int day, int minuteOfDay, int shiftEndMinute) => ShiftLeft(minuteOfDay, shiftEndMinute) && l.ClaimDay != day;

        /// <summary>
        /// The player calls in sick for today. False if they did already, or the shift is over (a day already missed is not called in). A day called in while really ill is no
        /// claim against them.
        /// </summary>
        public static bool CallIn(LeaveBook l, int day, bool ill, int yearDays, int minuteOfDay, int shiftEndMinute)
        {
            TurnYear(l, day, yearDays);
            if (!CanCallIn(l, day, minuteOfDay, shiftEndMinute))
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

        /// <summary>The pay in cents of the part of a shift not worked: the day's pay less what was worked, never under nothing. The worked minutes were paid as they were worked.</summary>
        public static long PayLeft(long dayPayCents, int shiftMinutes, int workedMinutes)
        {
            if (dayPayCents <= 0 || shiftMinutes <= 0)
            {
                return 0;
            }
            long left = Math.Max(0, shiftMinutes - Math.Max(0, workedMinutes));
            return dayPayCents * left / shiftMinutes;
        }

        /// <summary>
        /// A working day not worked (under three quarters of it) is settled, once: the spell is counted, and what it was settled as is kept. Returns the sick pay due for it in
        /// cents: the pay of the part of the shift that was not worked and so not paid, while paid days are left (an accident at work is paid outside them while it is paid as
        /// one), nothing in a job's first days, nothing for a day seen through, nothing twice. A day called in sick and then worked in part is no sick day: the call is
        /// withdrawn, and the day is the working life's own missed day.
        /// </summary>
        public static long Settle(LeaveBook l, LeaveFacts f, int day, int seed, int dayPayCents, int hiredDay, int yearDays, int shiftMinutes, int workedMinutes)
        {
            if (day <= l.SpellLast)
            {
                return 0;
            }
            bool reason = f.Ill || f.InHospital || f.Note || Noted(l, day);
            if (!reason && workedMinutes > 0 && l.ClaimDay == day)
            {
                l.ClaimDay = 0;   // called in sick, then worked: not a sick day
                return 0;
            }
            if (!reason && l.ClaimDay != day)
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
            long cents = PayLeft(dayPayCents, shiftMinutes, workedMinutes);
            if (!covered || cents <= 0 || day <= l.PaidDay || day - hiredDay < HealthCatalogue.QualifyDays)
            {
                return 0;
            }
            if (!WorkPaid(f))
            {
                bool past = HealthRules.YearOf(day, yearDays) < l.Year;   // a day of the year that ended, settled after the year turned: counted against that year
                int used = past ? (HealthRules.YearOf(day, yearDays) == l.PrevYear ? l.PrevPaidUsed : PaidDays(yearDays)) : l.PaidUsed;
                if (used >= PaidDays(yearDays))
                {
                    return 0;
                }
                if (past)
                {
                    l.PrevPaidUsed++;
                }
                else
                {
                    l.PaidUsed++;
                }
            }
            l.PaidDay = day;
            l.PaidMoney += cents / 100;
            return cents;
        }
    }
}
