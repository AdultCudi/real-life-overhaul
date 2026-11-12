using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using RealLifeOverhaul;
using RealLifeOverhaul.Careers;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Health;
using RealLifeOverhaul.Population;
using HPlan = RealLifeOverhaul.Health.Plan;

// Health, the second round (run with --health, after HealthChecks.cs): the logic a review found wanting, each rule driven as the director drives it. A day partly worked is paid
// only for the part not worked; an accident at work is paid while the player is in care; cover starts on the illness's real first day; calling in sick is for a shift not over;
// a day gone by is judged by what was true on it, in either order of the working life's settling and health's day; the year only moves forward; the energy drain and the
// cap and the premium's cycle; the people's pass is always in slices; an appointment in hospital is not charged; Home's chances are drawn for each event; the tests take back
// only what they did; and the stay, the game's night, the option off and the time skip decide through rules the director calls.
internal static partial class Program
{
    private static void HealthFixes()
    {
        HealthDrawChecks();
        HealthBeganChecks();
        HealthPartDayChecks();
        HealthWorkAccidentChecks();
        HealthCallInChecks();
        HealthPastDayChecks();
        HealthYearChecks();
        HealthPolicyChecks();
        HealthPeopleDayChecks();
        HealthStayRunChecks();
        HealthAppointmentStayChecks();
        HealthSessionChecks();
        HealthCreditChecks();
        HealthMoneyButtonChecks();
        HealthDeathChecks();
        HealthDrainChecks();
    }

    // ---- the energy drain of a serious case does not outlive its save

    private static void HealthDrainChecks()
    {
        // The game's list of energy spenders is cleared only when the game starts, so the drain must be taken off on every way out, whatever health's own flag says. The game's
        // list cannot be run here; this reads that the removal does not wait on the flag, and that every way out calls it.
        string game = ModSource(@"Health\HealthGame.cs"), dir = ModSource(@"Health\HealthDirector.cs");
        int at = game.IndexOf("public static void DropDrain()", StringComparison.Ordinal);
        string body = at >= 0 ? game.Substring(at, game.IndexOf("public static void Reset()", at, StringComparison.Ordinal) - at) : string.Empty;
        Check(body.Contains("EnergyHelper.RemoveEnergySpender(SeriousDrain);") && !body.Contains("if (") && game.Contains("public static void Reset()") && game.Contains("DropDrain();"),
            "health wiring: the serious case's drain is taken off the game's list without asking health's own flag");
        int destroy = dir.IndexOf("private void OnDestroy()", StringComparison.Ordinal), forget = dir.IndexOf("public void Forget()", StringComparison.Ordinal), off = dir.IndexOf("private void OptionOff(int day)", StringComparison.Ordinal);
        int read = dir.IndexOf("private void Read()", StringComparison.Ordinal);
        Check(destroy > 0 && dir.IndexOf("HealthGame.DropDrain();", destroy, StringComparison.Ordinal) - destroy < 400 && forget > 0 && dir.IndexOf("HealthGame.DropDrain();", forget, StringComparison.Ordinal) - forget < 600
              && off > 0 && dir.IndexOf("HealthGame.DropDrain();", off, StringComparison.Ordinal) - off < 900 && read > 0 && dir.IndexOf("HealthGame.DropDrain();", read, StringComparison.Ordinal) - read < 1400,
            "health exploit: the drain of a serious case is taken off when the city goes, the line is forgotten, the option goes off and a save is read: it never follows the player into another save");
    }

    // A bill settler for the checks: the very step the director settles a bill with, over a wallet that is counted.
    private static Func<long, BillResult> HealthSettler(HealthState s, bool prepaid, long spare, Action<long> took, int day = 50)
    {
        return bill => HealthDay.Bill(s, Care.Hospital, bill, prepaid, HPlan.None, 0, day, 60, 5, 1000, spare, amount => { took?.Invoke(amount); return true; });
    }

    // ---- Home's chances, one draw to each event

    private static void HealthDrawChecks()
    {
        var d = new HealthState { Seed = 5 };
        float first = HealthRules.Draw(d, 40, 3), second = HealthRules.Draw(d, 40, 3), other = HealthRules.Draw(d, 40, 4);
        Check(first == HealthRules.Unit(5, 40, 3, 0) && second == HealthRules.Unit(5, 40, 3, 1) && first != second && other == HealthRules.Unit(5, 40, 4, 0) && d.Draws[3] == 2 && d.Draws[4] == 1 && d.DrawDay == 40,
            "health reload: every event of a day has a draw of its own (the chance's slot and which draw of it that day), and the day's first draw of a chance is what it always was");
        Check(HealthRules.Draw(d, 41, 3) == HealthRules.Unit(5, 41, 3, 0) && d.DrawDay == 41 && d.Draws[3] == 1 && d.Draws[4] == 0, "health reload: a new day counts its draws from the first again");
        var busy = new HealthState { Seed = 9 };
        int low = 0;
        var fractions = new HashSet<float>();
        for (int i = 0; i < 20000; i++)
        {
            low += HealthRules.Draw(busy, 77, 1) < 0.15f ? 1 : 0;
        }
        for (int i = 0; i < 60; i++)
        {
            fractions.Add(HealthRules.Draw(busy, 78, 7));
        }
        Check(low > 2700 && low < 3300 && fractions.Count > 50,
            "health reload: a chance of 15% asked of 20,000 events in a day comes true " + low + " times (about 3,000), as Home's own random number gave it, and the lengths of a day's illnesses are not one fraction (" + fractions.Count + " of 60)");
        var a = new HealthState { Seed = 12 };
        for (int i = 0; i < 6; i++)
        {
            HealthRules.Draw(a, 5, 2);
        }
        string line = HealthStore.Write(a);
        HealthState b = HealthStore.Read(line, out int _), c = HealthStore.Read(line, out int _);
        float nextA = HealthRules.Draw(a, 5, 2), nextB = HealthRules.Draw(b, 5, 2), nextC = HealthRules.Draw(c, 5, 2);
        Check(b.DrawDay == 5 && nextA == nextB && nextB == nextC && nextA == HealthRules.Unit(12, 5, 2, 6),
            "health reload: a save in the middle of a day goes on with the next number, and a load of that save twice gives the same draw twice: reloading changes nothing");
        HealthState wild = HealthStore.Read("v=1^w:-5|-1|2000000|3", out int _);
        HealthState none = HealthStore.Read("v=1", out int _);
        Check(wild.DrawDay == 0 && wild.Draws[0] == 0 && wild.Draws[1] == 1000000 && wild.Draws[2] == 3 && none.DrawDay == 0 && none.Draws.All(n => n == 0),
            "health save: the draws' count is put inside its bounds, and an older save has counted none");
    }

    // ---- an illness's real first day

    private static void HealthBeganChecks()
    {
        var c = new HealthCase();
        HealthRules.Mirror(c, Condition.Flu, false, 50, 47);
        var unknown = new HealthCase();
        HealthRules.Mirror(unknown, Condition.Flu, false, 50, 0);
        var later = new HealthCase();
        HealthRules.Mirror(later, Condition.Flu, false, 50, 60);
        Check(c.Began == 47 && unknown.Began == 50 && later.Began == 50, "health cover: the case begins on the illness's real first day by Home's own record, today when that is not known, and never a day that is yet to come");
        // Switch the option off, fall ill, switch it on after the waiting days: the illness is not covered.
        var s = new HealthState();
        HealthDay.TakePlan(s, HPlan.Full, 10, 30, 60, 5, 1000, amount => true, out int _);
        HealthRules.Mirror(s.Case, Condition.Flu, false, 30, 12);
        BillResult off = HealthCover.Settle(s, Care.Hospital, 2600, s.Case.Began, false, HPlan.None, 0, 31, 60, 1000);
        var seen = new HealthState();
        HealthDay.TakePlan(seen, HPlan.Full, 10, 30, 60, 5, 1000, amount => true, out int _);
        HealthRules.Mirror(seen.Case, Condition.Flu, false, 30, 0);
        BillResult claimed = HealthCover.Settle(seen, Care.Hospital, 2600, seen.Case.Began, false, HPlan.None, 0, 31, 60, 1000);
        Check(s.Case.Began == 12 && off.Claim == 0 && claimed.Claim > 0,
            "health exploit: a plan taken, the option switched off, an illness caught and the option switched on after the waiting days claims nothing of it (day 12 against a plan from day 10), though the day health first saw it was past the wait");
        // An illness that starts just before midnight begins that day, not the next.
        var night = new HealthState();
        HealthDay.TakePlan(night, HPlan.Standard, 9, 30, 60, 5, 1000, amount => true, out int _);
        HealthRules.Mirror(night.Case, Condition.Cold, false, 12, 11);   // caught at ten to midnight on day 11, seen on day 12 at the first hour
        Check(night.Case.Began == 11 && !HealthCover.Covers(night.Policy.Plan, night.Policy.Since, night.Case.Began) && HealthCover.Covers(night.Policy.Plan, night.Policy.Since, 12),
            "health exploit: an illness caught just before midnight on day 11 is not covered by a plan from day 9, though the hour that saw it was day 12");
    }

    // ---- a day partly worked

    private static void HealthPartDayChecks()
    {
        const int Pay = 12000, Shift = 480;
        // Worked 74% of the shift, ill: the part not worked is paid, and with what was paid for the minutes it is never more than the day's pay.
        var part = new LeaveBook();
        long sick = HealthLeave.Settle(part, HealthIll, 100, 5, Pay, 50, 60, Shift, 355);
        long wages = 355L * Pay / Shift;
        Check(sick == Pay * (Shift - 355L) / Shift && sick > 0 && sick < Pay / 3 && wages + sick <= Pay && part.PaidUsed == 1,
            "health exploit: a day worked 74% while ill is paid sick pay for the 26% not worked only (" + sick + " of " + Pay + " cents), so wages and sick pay make the day's pay and no more");
        Check(HealthLeave.Settle(part, HealthIll, 100, 5, Pay, 50, 60, Shift, 355) == 0 && part.PaidUsed == 1, "health exploit: the same day is never paid twice");
        bool never = true;
        for (int worked = 0; worked < Shift * 3 / 4; worked++)
        {
            var one = new LeaveBook();
            long left = HealthLeave.Settle(one, HealthIll, 100, 5, Pay, 50, 60, Shift, worked);
            never &= left >= 0 && worked * (long)Pay / Shift + left <= Pay && left == HealthLeave.PayLeft(Pay, Shift, worked) && (worked > 0 || left == Pay);
        }
        Check(never, "health exploit: whatever part of a shift was worked short of three quarters, the wages paid for it and the sick pay for the rest are never more than the day's pay, and a day not worked at all is paid whole");
        Check(HealthLeave.PayLeft(Pay, Shift, 0) == Pay && HealthLeave.PayLeft(Pay, Shift, Shift) == 0 && HealthLeave.PayLeft(Pay, Shift, Shift + 90) == 0 && HealthLeave.PayLeft(Pay, 0, 0) == 0 && HealthLeave.PayLeft(-5, Shift, 0) == 0
              && HealthLeave.PayLeft(Pay, Shift, -30) == Pay, "health leave: the pay of the part not worked is none for a whole shift worked, and the whole day's for none of it");
        // Called in sick and then worked in part: no sick day.
        var call = new LeaveBook();
        HealthCallIn(call, 100, false, 60);
        long none = HealthLeave.Settle(call, HealthWell, 100, 5, Pay, 50, 60, Shift, 336);
        Check(none == 0 && call.ClaimDay == 0 && !HealthLeave.Covers(call, HealthWell, 100, 5) && call.SpellLast == 0 && call.PaidUsed == 0 && call.Claims == 1,
            "health exploit: calling in sick and then working 70% of the shift is not a sick day: no pay, not excused (the working life's own missed day), no spell, and the call still counts as made");
        var whole = new LeaveBook();
        HealthCallIn(whole, 100, false, 60);
        long paid = 0;
        for (int day = 100; day < 140 && paid == 0; day++)
        {
            whole = new LeaveBook();
            HealthCallIn(whole, day, false, 60);
            paid = HealthLeave.Settle(whole, HealthWell, day, 5, Pay, 50, 60, Shift, 0);
        }
        Check(paid == Pay, "health leave: a day called in sick and not worked at all, and not seen through, is paid the whole day's pay as before");
        var ill = new LeaveBook();
        HealthCallIn(ill, 100, true, 60);
        Check(HealthLeave.Settle(ill, HealthIll, 100, 5, Pay, 50, 60, Shift, 200) == Pay * (Shift - 200L) / Shift && ill.SpellDays == 1,
            "health leave: a player really ill who called in and worked part of the shift is paid for the rest, and the day is a sick day");
        // The working life's side: the bridge carries the minutes, which the careers hand over (the pay of the minutes worked was paid as they were).
        string work = ModSource(@"Careers\CareersDirector.Work.cs");
        Check(work.Contains("j.Def.Hours * 60, shiftMinutes);") && work.Contains("int shiftMinutes = j.TodayDay == day ? j.TodayMinutes : 0;   // read before the settling closes the day"),
            "health wiring: the working life hands the shift's minutes and the minutes worked to health when it settles a day not worked");
    }

    // ---- an accident at work

    private static void HealthWorkAccidentChecks()
    {
        const int Pay = 12000;
        LeaveFacts Untreated(int days) => new LeaveFacts { Ill = true, AtWorkInjury = true, InCare = false, WorkDays = days };
        LeaveFacts Treated(int days) => new LeaveFacts { Ill = true, AtWorkInjury = true, InCare = true, WorkDays = days };
        // A broken arm at work, the player stays home and never sees a doctor: paid for the first days only, said so, and then as any sick leave.
        var home = new LeaveBook();
        long total = 0;
        int paidDays = 0, covered = 0;
        for (int d = 0; d < 90; d++)
        {
            long cents = HealthLeave.Settle(home, Untreated(d), 100 + d, 5, Pay, 10, 365, 480, 0);
            total += cents;
            paidDays += cents > 0 ? 1 : 0;
            covered += home.LastCovered ? 1 : 0;
        }
        Check(paidDays == HealthCatalogue.WorkFreeDays && total == (long)Pay * HealthCatalogue.WorkFreeDays && covered == HealthCatalogue.WorkFreeDays && home.PaidUsed == 0,
            "health exploit: an accident at work that the player never sees a doctor about is paid for " + HealthCatalogue.WorkFreeDays + " days and no more in 90 days at home, and is excused for as long (" + paidDays + " paid)");
        // In care, it is paid for as long as the care lasts and no longer than the most days from the accident, outside the year's paid days.
        var care = new LeaveBook();
        int carePaid = 0;
        for (int d = 0; d < 60; d++)
        {
            carePaid += HealthLeave.Settle(care, Treated(d), 100 + d, 5, Pay, 10, 365, 480, 0) > 0 ? 1 : 0;
        }
        Check(carePaid == HealthCatalogue.WorkPayDays && care.PaidUsed == 0, "health work: in care an accident at work is paid for at most " + HealthCatalogue.WorkPayDays + " days from the accident, outside the year's paid days (" + carePaid + ")");
        // Left alone for five days and then seen to: the days between are not paid, the days in care are.
        var late = new LeaveBook();
        var days = new List<int>();
        for (int d = 0; d < 12; d++)
        {
            LeaveFacts f = d < 5 ? Untreated(d) : Treated(d);
            if (HealthLeave.Settle(late, f, 100 + d, 5, Pay, 10, 365, 480, 0) > 0)
            {
                days.Add(d);
            }
        }
        Check(days.SequenceEqual(new[] { 0, 1, 2, 5, 6, 7, 8, 9, 10, 11 }), "health work: left alone for five days and then seen to, the accident is paid again from the day of the care (" + string.Join(",", days) + ")");
        // The rule itself, at its edges.
        Check(HealthLeave.WorkPaid(Untreated(HealthCatalogue.WorkFreeDays - 1)) && !HealthLeave.WorkPaid(Untreated(HealthCatalogue.WorkFreeDays)) && HealthLeave.WorkPaid(Treated(HealthCatalogue.WorkPayDays - 1))
              && !HealthLeave.WorkPaid(Treated(HealthCatalogue.WorkPayDays)) && !HealthLeave.WorkPaid(new LeaveFacts { Ill = true, InCare = true, WorkDays = 1 }),
            "health work: an accident is paid as one for the first days, then only in care, never beyond the most days, and an illness is never one");
        // Not excused any more once it is not paid as an accident: the ordinary two days of self certification run out.
        var spell = new LeaveBook();
        bool excusedAfter = true;
        for (int d = 0; d < 8; d++)
        {
            HealthLeave.Settle(spell, Untreated(d), 100 + d, 5, Pay, 10, 365, 480, 0);
            excusedAfter &= d < HealthCatalogue.WorkFreeDays || !spell.LastCovered;
        }
        Check(excusedAfter, "health work: an accident left without care is a day off needing a doctor's note once its free days are over, as any sick leave");
        // The director's facts for it: the accident's days since, and care, come out of the case and its span.
        var s = new HealthState();
        HealthRules.Injure(s.Case, Condition.BrokenArm, 3, 100, 0, true);
        LeaveFacts open = HealthLeave.FactsFor(s, 104, 104, false, false);
        s.Case.Treated = true;
        HealthLeave.ObserveCase(s.Leave, s.Case, false, 105);
        LeaveFacts cared = HealthLeave.FactsFor(s, 106, 106, false, false);
        LeaveFacts before = HealthLeave.FactsFor(s, 104, 106, false, false);
        Check(open.AtWorkInjury && open.WorkDays == 4 && !open.InCare && cared.InCare && cared.WorkDays == 6 && !before.InCare,
            "health work: the facts of a working day say how long ago the accident was and whether care had begun by then, not only whether it has begun by now");
    }

    // ---- calling in sick

    private static void HealthCallInChecks()
    {
        var l = new LeaveBook();
        int end = HealthLeave.ProShiftEnd(9, 8);
        Check(!HealthLeave.CallIn(l, 100, false, 60, end, end) && l.ClaimDay == 0 && !HealthLeave.CallIn(l, 100, false, 60, 22 * 60, end) && !HealthLeave.CallIn(l, 100, false, 60, 600, -1)
              && HealthLeave.CallIn(l, 100, false, 60, end - 1, end) && l.ClaimDay == 100 && l.Claims == 1 && !HealthLeave.CallIn(l, 100, false, 60, 600, end),
            "health exploit: nobody calls in sick once the shift's hours are over (a day missed already is not excused and paid), or with no shift; before or during them they can, once");
        // For a profession: the shift ends where the working life counts the day over, to the minute, for every profession and every minute of its working day.
        bool pro = true;
        foreach (Profession p in ProCatalogue.All)
        {
            int day = Enumerable.Range(1, 14).First(d => ProRules.WorkDay(p, d));
            int shiftEnd = HealthLeave.ProShiftEnd(p.Hour, p.Hours);
            for (int minute = 0; minute < 1440; minute++)
            {
                pro &= HealthLeave.ShiftLeft(minute, shiftEnd) == !ProRules.DayOver(p, day, day, minute);
            }
        }
        Check(pro, "health exploit: for every profession a day can be called in sick exactly until the working life counts that day over (ProRules.DayOver), minute by minute");
        // For a street career: until the game's own no-show hour.
        bool street = true;
        for (int rota = 6; rota <= 20; rota++)
        {
            for (int hour = 0; hour < 24; hour++)
            {
                street &= HealthLeave.ShiftLeft(hour * 60 + 59, HealthLeave.StreetShiftEnd(rota, CareerCatalogue.LateHours)) == (hour < rota + CareerCatalogue.LateHours);
            }
        }
        Check(street, "health exploit: for a street career a day can be called in sick until the hour the game counts a no-show (" + CareerCatalogue.LateHours + " hours after its start)");
        Check(HealthLeave.CanCallIn(new LeaveBook(), 5, 600, 1020) && !HealthLeave.CanCallIn(new LeaveBook { ClaimDay = 5 }, 5, 600, 1020) && !HealthLeave.CanCallIn(new LeaveBook(), 5, 1020, 1020),
            "health leave: the page offers calling in sick while there is a shift not over and the player has not called in");
    }

    // ---- a day gone by is judged by what was true on it

    private static void HealthPastDayChecks()
    {
        // A sprain's last sick day is settled on the morning it is cleared: the working life settles yesterday on the game's new day event and health's day runs a second
        // later, or the other way about. Either way the last day is the sick day it was.
        for (int order = 0; order < 2; order++)
        {
            bool all = true;
            int lastDay = 0, warnings = 0;
            bool ill = false;
            for (int seed = 1; seed <= 60; seed++)
            {
                var s = new HealthState { Seed = seed };
                HealthRules.Injure(s.Case, Condition.Sprain, seed, 50, seed, false);
                lastDay = s.Case.Until - 1;
                for (int hour = 0; hour < 3; hour++)
                {
                    HealthLeave.ObserveCase(s.Leave, s.Case, false, 50);
                }
                bool coveredLast = false;
                for (int day = 51; day <= lastDay + 1; day++)
                {
                    int yesterday = day - 1;
                    bool covered = false;
                    Action settle = () =>
                    {
                        LeaveFacts f = HealthLeave.FactsFor(s, yesterday, day, false, false);
                        HealthLeave.Settle(s.Leave, f, yesterday, seed, 0, 10, 365, 480, 0);
                        covered = f.Ill;   // a day of the case is a day really ill (how many such days run without a note is the leave rules' own business)
                    };
                    Action pass = () =>
                    {
                        HealthLeave.ObserveCase(s.Leave, s.Case, false, day - 1);
                        if (HealthRules.CaseDay(s.Case, HealthCareful, seed, day) == CaseEvent.Healed)
                        {
                            s.Case.Clear();   // (as the director's day does)
                        }
                        if (s.Case.Open)
                        {
                            HealthLeave.ObserveCase(s.Leave, s.Case, false, day);
                        }
                    };
                    if (order == 0)
                    {
                        settle();
                        pass();
                    }
                    else
                    {
                        pass();
                        settle();
                    }
                    coveredLast = yesterday == lastDay ? covered : coveredLast;
                    ill |= yesterday == lastDay && covered;
                }
                all &= coveredLast && !s.Case.Open;
                warnings += coveredLast ? 0 : 1;
            }
            Check(all && ill, "health exploit: the last sick day of a sprain is a day really ill, not a day for no reason (a warning), when the working life settles it " + (order == 0 ? "before health's day clears the case that morning" : "after health's day has cleared it")
                       + " (" + warnings + " of 60 sprains settled it as a day with no reason)");
        }
        // The same for a flu Home's own hour clears early in the day it ends, and a day the case never covered.
        var flu = new HealthState();
        HealthRules.Mirror(flu.Case, Condition.Flu, false, 70, 70);
        HealthLeave.ObserveCase(flu.Leave, flu.Case, false, 70);
        HealthLeave.ObserveCase(flu.Leave, flu.Case, false, 71);
        flu.Case.Clear();   // Home's illness is over at one in the morning of day 72
        LeaveFacts monday = HealthLeave.FactsFor(flu, 71, 72, false, false), tuesday = HealthLeave.FactsFor(flu, 72, 72, false, false), before = HealthLeave.FactsFor(flu, 69, 72, false, false);
        Check(monday.Ill && !tuesday.Ill && !before.Ill, "health leave: a day gone by is ill if the case ran through it, whether or not it is open now, and the days outside the case's span are not");
        // A time skip that jumps over the day an injury heals: the days after it are not sick days.
        var skip = new HealthState { Seed = 3 };
        HealthRules.Injure(skip.Case, Condition.Sprain, 3, 50, 0, false);
        int healed = skip.Case.Until;
        HealthLeave.ObserveCase(skip.Leave, skip.Case, false, 50);
        HealthLeave.ObserveCase(skip.Leave, skip.Case, false, healed + 2);   // health's day sees the new day only after the skip
        if (HealthRules.CaseDay(skip.Case, HealthCareful, 3, healed + 3) == CaseEvent.Healed)
        {
            skip.Case.Clear();
        }
        Check(HealthLeave.FactsFor(skip, healed - 1, healed + 3, false, false).Ill && !HealthLeave.FactsFor(skip, healed, healed + 3, false, false).Ill && !HealthLeave.FactsFor(skip, healed + 2, healed + 3, false, false).Ill,
            "health leave: a time skip over the day an injury heals does not make the days after it sick days: the span ends the day before it heals");
        var hosp = new HealthState();
        hosp.Stay.Active = true;
        hosp.Stay.From = 80 * 1440 + 600;
        hosp.Stay.Until = 82 * 1440 + 480;
        Check(HealthLeave.FactsFor(hosp, 81, 81, false, false).InHospital && !HealthLeave.FactsFor(hosp, 79, 81, false, false).InHospital && !HealthLeave.FactsFor(hosp, 83, 83, false, false).InHospital,
            "health leave: a day is a day in hospital only within the stay under way");
    }

    // ---- the year only moves forward

    private static void HealthYearChecks()
    {
        var y = new LeaveBook();
        var note = new LeaveFacts { Ill = true, Note = true };
        long a = HealthLeave.Settle(y, note, 58, 5, 12000, 10, 60, 480, 0), b = HealthLeave.Settle(y, note, 59, 5, 12000, 10, 60, 480, 0);
        HealthLeave.TurnYear(y, 61, 60);   // health's day: the year turns
        long late = HealthLeave.Settle(y, note, 60, 5, 12000, 10, 60, 480, 0);   // the working life settles the last day of the year that has ended, after the turn
        Check(a == 12000 && b == 12000 && late == 0 && y.Year == 1 && y.PaidUsed == 0 && y.PrevYear == 0 && y.PrevPaidUsed == HealthLeave.PaidDays(60),
            "health exploit: the last day of a year settled after the year has turned counts against its own year's paid days (2 of 2 used), not a fresh year's: no extra paid day at the turn");
        long fresh = HealthLeave.Settle(y, note, 61, 5, 12000, 10, 60, 480, 0);
        Check(fresh == 12000 && y.PaidUsed == 1 && y.Year == 1, "health leave: the new year has its paid days from its first day");
        HealthLeave.TurnYear(y, 59, 60);
        HealthLeave.TurnYear(y, 3, 60);
        Check(y.Year == 1 && y.PaidUsed == 1 && y.PrevYear == 0, "health leave: a day of a year gone by never turns the year back: its paid days and its calls are not reset");
        var ahead = new LeaveBook();
        HealthLeave.TurnYear(ahead, 130, 60);
        HealthLeave.TurnYear(ahead, 61, 60);
        Check(ahead.Year == 2 && ahead.PrevYear == -1, "health leave: after years away the year before is not one whose days can be counted against");
        var older = new LeaveBook();
        HealthLeave.Settle(older, note, 58, 5, 12000, 10, 60, 480, 0);
        HealthLeave.TurnYear(older, 200, 60);
        Check(HealthLeave.Settle(older, note, 59, 5, 12000, 10, 60, 480, 0) == 0, "health leave: a day of a year too long gone to count against is not paid");
        // The year's bills and the policy's cap turn the same way: forward only.
        var s = new HealthState();
        HealthCover.TurnYear(s, 130, 60);
        s.BillsYear = 500;
        s.Policy.PaidYear = 200;
        HealthCover.TurnYear(s, 61, 60);
        Check(s.Year == 2 && s.BillsYear == 500 && s.Policy.PaidYear == 200, "health insurance: a bill settled for a day of a year gone by neither turns the year back nor resets the year's cap");
    }

    // ---- the policy: the cap, the premium's cycle

    private static void HealthPolicyChecks()
    {
        // Cancelling and taking a plan again, or changing plan, is no new cap.
        var s = new HealthState();
        HealthDay.TakePlan(s, HPlan.Basic, 10, 30, 60, 5, 1000, amount => true, out int _);
        BillResult one = HealthCover.Settle(s, Care.Hospital, 30000, 20, false, HPlan.None, 0, 25, 60, 1000);
        long paidBefore = s.Policy.PaidYear;
        HealthCover.Cancel(s.Policy);
        HealthDay.TakePlan(s, HPlan.Basic, 40, 30, 60, 5, 1000, amount => true, out int _);
        BillResult two = HealthCover.Settle(s, Care.Hospital, 30000, 50, false, HPlan.None, 0, 52, 60, 1000);
        Check(one.Claim == 20000 && paidBefore == 20000 && s.Policy.PaidYear == 20000 && two.Claim == 0,
            "health exploit: the basic plan's year cap of 20,000 is reached, and cancelling and taking the plan again in the same year pays nothing more (" + two.Claim + ")");
        HealthCover.Cancel(s.Policy);
        HealthDay.TakePlan(s, HPlan.Standard, 53, 30, 60, 5, 1000, amount => true, out int _);
        BillResult three = HealthCover.Settle(s, Care.Hospital, 90000, 56, false, HPlan.None, 0, 56, 60, 1000);
        Check(three.Claim == 40000 && s.Policy.PaidYear == 60000, "health exploit: changing to the standard plan's cap of 60,000 leaves only what the year has not paid already (40,000), not a fresh 60,000");
        BillResult next = HealthCover.Settle(s, Care.Hospital, 90000, 62, false, HPlan.None, 0, 65, 60, 1000);
        Check(next.Claim > 0 && s.Policy.Year == 1 && s.Policy.PaidYear == next.Claim, "health insurance: only the year turning gives the cap again");
        // The premium's cycle: due days roll in whole months from the old due day.
        bool whole = true, never = true;
        foreach (int lateBy in new[] { 0, 1, 7, 28, 29, 30, 31, 59, 60, 95 })
        {
            foreach (int month in new[] { 5, 30 })
            {
                var p = new HealthPolicy { Plan = HPlan.Standard, NextDue = 40 };
                HealthCover.Paid(p, 40 + lateBy, month);
                var q = new HealthPolicy { Plan = HPlan.Standard, NextDue = 40 };
                HealthCover.Missed(q, 40 + lateBy, month);
                whole &= (p.NextDue - 40) % month == 0 && (q.NextDue - 40) % month == 0;
                never &= p.NextDue > 40 + lateBy && p.NextDue <= 40 + lateBy + month && q.NextDue > 40 + lateBy && q.NextDue <= 40 + lateBy + month;
            }
        }
        Check(whole && never, "health premium: a premium paid or missed late is followed by the next on the cycle of the old due day (whole months on from it), after today and within a month of it");
        var cycle = new HealthState();
        HealthDay.TakePlan(cycle, HPlan.Standard, 10, 30, 365, 30, 1000, amount => true, out int _);   // due 40, 70, 100 ...
        HealthDay.Premium(cycle, 69, 30, 365, 30, 1000, amount => true, out int _);   // paid 29 days late
        int afterLate = cycle.Policy.NextDue;
        Check(afterLate == 70 && HealthCover.NextDueAfter(40, 70, 30) == 100 && HealthCover.NextDueAfter(40, 71, 30) == 100 && HealthCover.NextDueAfter(0, 8, 5) == 13 && HealthCover.NextDueAfter(40, 30, 30) == 70,
            "health premium: paid on day 69, 29 days late, the next falls due on day 70 of the cycle, and each premium keeps to the cycle however late it was");
        var months = new HealthState();
        HealthDay.TakePlan(months, HPlan.Basic, 10, 30, 365, 30, 1000, amount => true, out int _);
        int taken = 0;
        for (int day = 11; day <= 400; day++)
        {
            if (day % 97 < 3)
            {
                continue;   // away for three days in ninety seven
            }
            taken += HealthDay.Premium(months, day, 30, 365, 30, 1000, amount => true, out int _) == PremiumResult.Paid ? 1 : 0;
        }
        Check(taken >= 12 && taken <= 13, "health premium: over 390 days with some days away a plan pays 12 or 13 premiums (" + taken + "), a month's each, never one for every day late");
    }

    // ---- the people's day is always in slices

    private static void HealthPeopleDayChecks()
    {
        PopulationState city = HealthCity(6000, 51);
        var o = new PopHealth();
        HealthPeople.Turn(o, 100, 3);
        Check(o.Day == 100 && o.Cursor == 0 && o.Pending == 0, "health people: the day turning begins its pass");
        HealthPeople.Slice(city, o, 51, 1000, 1700);
        int cursor = o.Cursor, cases = o.Cases;
        HealthPeople.Turn(o, 101, 3);
        HealthPeople.Turn(o, 102, 3);
        Check(o.Cursor == cursor && o.Cases == cases && o.Day == 100 && o.Pending == 102,
            "health people: a day that turns while the pass is under way does not finish it in one go (the cursor and the sums are where they were), and the days that turn meanwhile are one waiting day, the latest");
        int slices = 0;
        while (o.Cursor >= 0 && slices < 100)
        {
            HealthPeople.Slice(city, o, 51, 1000, HealthCatalogue.PassSlice / 4);
            slices++;
        }
        bool resumed = HealthPeople.Resume(o, 5);
        Check(slices > 3 && resumed && o.Day == 102 && o.Cursor == 0 && o.Pending == 0 && !HealthPeople.Resume(o, 5), "health people: when the pass is done the waiting day begins, once, and from its first person");
        HealthPeople.Turn(o, 102, 3);
        Check(o.Cursor == 0 && o.Day == 102, "health people: a day whose pass has begun is not begun again");
        // The city not being ready is a wait, away is a stand down.
        Check(HealthPeople.ModeOf(true, true, false, false) == HealthPeople.Mode.Ready && HealthPeople.ModeOf(true, false, false, false) == HealthPeople.Mode.NotReady
              && HealthPeople.ModeOf(false, true, false, false) == HealthPeople.Mode.Away && HealthPeople.ModeOf(true, true, true, false) == HealthPeople.Mode.Away
              && HealthPeople.ModeOf(true, true, false, true) == HealthPeople.Mode.Away && HealthPeople.ModeOf(true, false, false, true) == HealthPeople.Mode.Away,
            "health session: the city's people are ready when running here and the player's own, only not yet while loading, and away in a Going Public session, as a guest, or with the living population off");
        var away = new PopHealth { Day = 100, Cursor = 4000, Pending = 103 };
        HealthPeople.StandDown(away);
        Check(away.Cursor == -1 && away.Pending == 0, "health session: away, no pass is under way and none waits");
        // A pass cut in two by a save and a load goes on from its cursor, and the waiting day with it.
        var carrier = new HealthState();
        CopyPop(o, carrier.Pop);
        carrier.Pop.Pending = 140;
        HealthState back = HealthStore.Read(HealthStore.Write(carrier), out int _);
        Check(back.Pop.Pending == 140 && back.Pop.Cursor == o.Cursor, "health save: the day waiting for the people's pass is in the save");
    }

    // ---- the stay, the game's night, the option off, the time skip

    private static HealthState HealthSeriousFlu(HPlan plan)
    {
        var s = new HealthState { Seed = 4 };
        s.Case.Kind = Condition.Flu;
        s.Case.Began = 8;
        s.Case.Serious = true;
        if (plan != HPlan.None)
        {
            HealthDay.TakePlan(s, plan, 1, 30, 60, 5, 1000, amount => true, out int _);
        }
        return s;
    }

    private static void HealthStayRunChecks()
    {
        // A whole stay as the director runs it: admitted, the clock skipped a day at a time to the morning of the discharge, billed, a note, the illness over.
        var s = HealthSeriousFlu(HPlan.Standard);
        int now = 10 * 1440 + 14 * 60, clock = now, rounds = 0;
        HealthDay.Admit(s, now, false);
        for (int round = 0; HealthDay.NextSkip(s.Stay, clock, round, out int target); round++)
        {
            int before = clock;
            clock = target;
            rounds++;
            if (clock <= before)
            {
                break;
            }
        }
        long took = 0;
        BillResult? released = HealthDay.Release(s, clock, clock / 1440, false, 1000, HealthSettler(s, false, 100000, n => took += n, 12), out bool illness);
        Check(rounds == 2 && clock == 12 * 1440 + 480 && released.HasValue && released.Value.Bill == 2600 && released.Value.Claim == 1997 && took == 603 && illness && !s.Stay.Active && !s.Case.Open
              && HealthLeave.Noted(s.Leave, 10) && HealthLeave.Noted(s.Leave, 14) && !HealthLeave.Noted(s.Leave, 15),
            "health hospital: a serious flu is two days in, the clock skipped a day at a time to the morning of the discharge (" + rounds + " skips), billed 2,600 with the standard plan's 1,997, 603 paid, a note to day 14 (got "
            + (released.HasValue ? released.Value.Bill + ", " + released.Value.Claim : "no bill") + ", " + took + ", " + illness + ", " + s.Leave.NoteFrom + " to " + s.Leave.NoteUntil + ")");
        Check(HealthDay.Release(s, clock, 12, false, 1000, HealthSettler(s, false, 100000, null), out bool _) == null, "health hospital: a discharge is once: with no stay nothing is billed");
        // A clock that does not move: no more skips than the stay has days, and a discharge for the days gone.
        var stuck = HealthSeriousFlu(HPlan.None);
        HealthDay.Admit(stuck, now, false);
        int skips = 0;
        int clock2 = now;
        for (int round = 0; HealthDay.NextSkip(stuck.Stay, clock2, round, out int t2); round++)
        {
            skips++;
            int before = clock2;
            if (clock2 <= before)
            {
                break;
            }
        }
        int forever = 0;
        for (int round = 0; HealthDay.NextSkip(stuck.Stay, now, round, out int t3); round++)
        {
            forever++;
        }
        long stuckBill = 0;
        HealthDay.Release(stuck, clock2, clock2 / 1440, false, 1000, HealthSettler(stuck, false, 100000, n => stuckBill += n), out bool _);
        Check(skips == 1 && forever == HealthCatalogue.StayMost + 2 && stuckBill == 1700,
            "health hospital: a clock that will not move is asked once and the stay ends billed for the day begun (1,700); asked for ever, the skips stop at " + (HealthCatalogue.StayMost + 2));
        // A save and a load between every two skips goes on to the same morning and the same bill.
        var mid = HealthSeriousFlu(HPlan.Full);
        int clock3 = now;
        HealthDay.Admit(mid, now, false);
        bool goesOn = true;
        for (int round = 0; HealthDay.NextSkip(mid.Stay, clock3, round, out int target4); round++)
        {
            mid = HealthStore.Read(HealthStore.Write(mid), out int _);
            goesOn &= mid.Stay.Active && !HealthDay.StayOver(mid.Stay, clock3) && mid.Case.Serious;
            clock3 = target4;
        }
        BillResult done = HealthDay.Release(mid, clock3, clock3 / 1440, false, 1000, HealthSettler(mid, false, 100000, null, 12), out bool _).Value;
        Check(goesOn && clock3 == 12 * 1440 + 480 && done.Bill == 2600 && !mid.Stay.Active,
            "health save: a save and a load before each skip of a stay goes on to the same morning, and the bill is the same (the game saves during a time skip, at its timer and at eleven at night)");
        Check(HealthDay.StayOver(mid.Stay, 0) && HealthDay.StayOver(s.Stay, 5) && HealthDay.StayOver(new HealthStay(), 0), "health hospital: no stay under way is a stay over");
        var late = HealthSeriousFlu(HPlan.None);
        HealthDay.Admit(late, now, false);
        Check(!HealthDay.StayOver(late.Stay, late.Stay.Until - 2) && HealthDay.StayOver(late.Stay, late.Stay.Until - 1) && HealthDay.StayOver(late.Stay, late.Stay.Until + 600),
            "health save: a load whose clock is a minute from the discharge or past it ends the stay at once");
        // A shared clock (Going Public) is never moved: the whole stay is billed at once.
        HealthStay shared = late.Stay;
        Check(HealthDay.DischargeBill(shared, shared.From + 10, true, 1000) == 2600 && HealthDay.DischargeBill(shared, shared.From + 10, false, 1000) == 1700 && HealthDay.DischargeBill(shared, shared.Until + 5000, false, 1000) == 2600,
            "health session: with a shared clock a stay is billed in full without the days passing; otherwise for the days that have");
        // The game's own night: the plan's part of the game's bill, a serious case stays on, the appointment slept through is dropped.
        var night = HealthSeriousFlu(HPlan.Standard);
        HealthDay.Book(night.Appointment, 29, 8, "Park Avenue", 12);
        int morning = 30 * 1440 + 8 * 60;
        BillResult nr = HealthDay.GameNight(night, morning, 30, HealthSettler(night, true, 100000, null, 30), out bool admitted);
        Check(nr.Bill == HealthCatalogue.GameStayBill && nr.Claim == 1487 && nr.Paid == 513 && admitted && night.Stay.Active && night.Stay.GameNight && night.Stay.Until == morning + 1440 && !night.Appointment.Booked,
            "health hospital: after the game's own night the standard plan pays 1,487 of its 2,000, the serious flu stays in for its second day, and the appointment the night slept through is dropped");
        var mild = new HealthState { Seed = 4 };
        mild.Case.Kind = Condition.Cold;
        mild.Case.Began = 8;
        HealthDay.Book(mild.Appointment, 30, 8, "Park Avenue", 12);
        HealthDay.GameNight(mild, morning, 30, HealthSettler(mild, true, 100000, null, 30), out bool mildAdmitted);
        Check(!mildAdmitted && !mild.Stay.Active && mild.Case.Treated && mild.Appointment.Booked, "health hospital: a night in the game's hospital sees to a mild illness and keeps tomorrow's appointment");
        // The option off in the middle of a stay: billed for the days begun, everything of the case ends, what is owed is still collected in the same look.
        var off = HealthSeriousFlu(HPlan.None);
        HealthDay.Admit(off, now, false);
        HealthCover.Owe(off.Debt, 1500, 5, 40);
        off.Debt.LastDay = 40;
        long billTaken = 0, debtTaken = 0;
        DebtResult r = HealthDay.WhileOff(off, true, now + 30, 41, 1000, HealthSettler(off, false, 100000, n => billTaken += n, 41), amount => { debtTaken += amount; return true; }, null, out long instalment);
        Check(billTaken == 1700 && !off.Stay.Active && !off.Case.Open && r == DebtResult.Paid && instalment == 150 && debtTaken == 150 && off.Debt.Owed == 1350 + 0,
            "health option: switched off in the middle of a stay it is billed for the day begun (1,700), the case ends, and the debt's instalment of that day is taken in the same look");
        DebtResult again = HealthDay.WhileOff(off, false, now + 60, 42, 1000, HealthSettler(off, false, 100000, n => billTaken += n, 42), amount => { debtTaken += amount; return true; }, null, out long second);
        Check(again == DebtResult.Paid && second == 150 && billTaken == 1700 && off.Debt.Owed == 1200, "health option: with the option off the debt goes on being collected, a day at a time, and nothing more is billed");
        var noDebt = new HealthState();
        Check(HealthDay.WhileOff(noDebt, false, 0, 50, 1000, HealthSettler(noDebt, false, 100, null), amount => true, null, out long zero) == DebtResult.None && zero == 0, "health option: with nothing owed the option off takes nothing");
        // Nobody is admitted from here, nothing is billed for a stay never begun.
        Check(HealthDay.SwitchOff(new HealthState(), 0, 1000, HealthSettler(new HealthState(), false, 100, null)) == 0, "health option: switching off with no stay bills nothing");
    }

    // ---- an appointment and the hospital

    private static void HealthAppointmentStayChecks()
    {
        var s = HealthSeriousFlu(HPlan.None);
        HealthDay.Book(s.Appointment, 10, 8, "Park Avenue", 12);   // day 10 at ten
        HealthDay.Admit(s, 10 * 1440 + 9 * 60, false);
        Check(!s.Appointment.Booked && HealthDay.AppointmentDay(s.Appointment, 11, 600, true) == AppointmentResult.None,
            "health exploit: admission drops an appointment that falls within the stay, so the missed fee is never charged to a player in hospital");
        var after = HealthSeriousFlu(HPlan.None);
        after.Appointment.Day = 14;
        after.Appointment.Hour = 10;
        after.Appointment.Street = "Park Avenue";
        HealthDay.Admit(after, 10 * 1440 + 9 * 60, false);
        Check(after.Appointment.Booked, "health appointment: an appointment after the stay is kept");
        var a = new HealthAppointment();
        HealthDay.Book(a, 20, 8, "Park Avenue", 12);
        int past = 10 * 60 + HealthCatalogue.AppointmentLate + 1;
        Check(HealthDay.AppointmentDay(a, 20, past - 100, false) == AppointmentResult.None && a.Booked, "health appointment: before the end of its window nothing happens");
        Check(HealthDay.AppointmentDay(a, 20, past, true) == AppointmentResult.Excused && !a.Booked, "health exploit: an appointment that passes while the player is in hospital (a stay, or the game's night) costs nothing");
        HealthDay.Book(a, 20, 8, "Park Avenue", 12);
        Check(HealthDay.AppointmentDay(a, 20, past, false) == AppointmentResult.Fee && !a.Booked, "health appointment: an appointment that passes unkept costs the missed fee, once");
        HealthDay.Book(a, 20, 8, "Park Avenue", 12);
        a.Kept = true;
        Check(HealthDay.AppointmentDay(a, 20, past, false) == AppointmentResult.None && HealthDay.AppointmentDay(a, 21, 5, false) == AppointmentResult.Left && !a.Booked,
            "health appointment: one kept is never charged and is dropped the next day");
    }

    // ---- the rivals and the people in a session

    private static void HealthSessionChecks()
    {
        Check(HealthRivals.WorthIn(true, true, 5000) == -1 && HealthRivals.WorthIn(false, false, 5000) == -1 && HealthRivals.WorthIn(false, true, 5000) == 5000 && HealthRivals.WorthIn(true, false, 5000) == -1,
            "health session: a rival's worth is asked for only when they are forward and the rivals are not resting in a session");
        var rested = new HealthState { Seed = 13 };
        var active = new HealthState { Seed = 13 };
        int restedIll = 0, activeIll = 0, rests = 0, bills = 0, paidOut = 0;
        bool shaped = true;
        for (int day = 1; day <= 30000; day++)
        {
            restedIll += HealthRivals.Day(rested, day, 0, 1000, i => HealthRivals.WorthIn(true, true, 1000000), (i, bill) => 1L, (i, until) => rests++);
            activeIll += HealthRivals.Day(active, day, 0, 1000, i => HealthRivals.WorthIn(false, true, 1000000), (i, bill) => { bills++; paidOut += (int)bill; return bill; }, (i, until) => shaped &= until > day);
        }
        Check(restedIll == 0 && rests == 0 && activeIll > 60 && shaped && bills > 0 && bills < activeIll && active.Notes.Count > 0 && paidOut >= bills * 1700,
            "health session: in a Going Public session no rival falls ill in 30,000 days; out of it " + activeIll + " do, each told to rest until the day after theirs ends, " + bills + " in hospital paying a stay's bill and noted for the paper");
        Check(HealthRivals.Day(active, 1, 0, 1000, null, null, null) == 0, "health rivals: with no way to ask a rival's worth nobody falls ill");
        // Staff: only the player's own people (the game's list is walked by HealthGame, one of the three sources checked below).
        string game = ModSource(@"Health\HealthGame.cs");
        Check(game.Contains("reg != null && GoingPublic.Mine(reg)"), "health wiring: only the player's own staff are drawn (GoingPublic.Mine, a game call that cannot be run here)");
    }

    // ---- the medical debt on the credit file

    private static void HealthCreditChecks()
    {
        // The debt is a credit account whose balance and limit are what is owed. Against a real debt the score is never worse for it, and never better than owing nothing.
        int day = 40;
        var file = new CreditFile();
        var none = new CreditScore();
        var medical = new CreditScore();
        var real = new CreditScore();
        float income = 300f;
        foreach (float other in new[] { 0f, 20000f })
        {
            foreach (float owed in new[] { 500f, 5300f, 12000f })
            {
                CreditScoring.Compute(file, new CreditInputs { Owed = 0, Limit = other, DailyService = 0, DailyIncome = income }, day, none);
                CreditScoring.Compute(file, new CreditInputs { Owed = owed, Limit = other + owed, DailyService = Math.Min(owed, owed / 10f), DailyIncome = income }, day, medical);
                CreditScoring.Compute(file, new CreditInputs { Owed = owed, Limit = other, DailyService = Math.Min(owed, owed / 10f), DailyIncome = income }, day, real);
                Check(medical.Value <= none.Value && medical.Value >= real.Value && medical.Value >= 300 && medical.Value <= 850,
                    "health credit: a medical debt of " + owed + " on a limit of " + other + " scores " + medical.Value + ": no better than owing nothing (" + none.Value + "), no worse than the same debt with no limit of its own ("
                    + real.Value + ")");
            }
        }
        // Collected once: when the lender files its claim, the whole of what is owed leaves the debt, and nothing is collected here again.
        var s = new HealthState();
        HealthCover.Owe(s.Debt, 2000, 5, 10);
        int filed = 0, filings = 0;
        long collected = 0;
        for (int d = 11; d <= 40; d++)
        {
            HealthDay.Debt(s, d, amount => false, amount => { filed = amount; filings++; return 77; }, out long _);
        }
        Check(filings == 1 && filed == 2000 && s.Debt.Owed == 0 && s.Debt.Claim == 77 && collected == 0 && HealthCover.Instalment(s.Debt, 99) == 0,
            "health debt: the claim the lender files at court is for the whole debt, once, and what is left here is nothing, so it is collected by one party only (" + filings + " filing, " + collected + " taken here)");
        long again = 0;
        for (int d = 41; d <= 60; d++)
        {
            HealthDay.Debt(s, d, amount => { again += amount; return true; }, amount => 88, out long _);   // (an instalment would be asked for here only if the debt were still owed)
        }
        Check(again == 0, "health debt: after the claim is filed no instalment of it is taken here");
        Check(!HealthDay.PayOff(s, 50, amount => true, out long _), "health debt: a debt gone to court cannot be paid off here too");
        // The sources: the account, the Finance list and the court bridge, as wired (the game's side of them cannot be run here).
        string wiring = ModSource(@"Wiring\Wiring.HealthLinks.cs");
        Check(wiring.Contains("public float Balance => Owed(out int _);") && wiring.Contains("public float Limit => Owed(out int _);") && wiring.Contains("EconomyBridges.OtherDebts = Debts;")
              && wiring.Contains("EconomyBridges.OtherAccounts = Accounts;") && wiring.Contains("EconomyBridges.FileClaim(amount)"),
            "health wiring: the medical debt is a credit account whose balance and limit are what is owed, in the Finance app's debts, and its claim goes to the mod's court");
    }

    // ---- every money button reaches one booking

    private static void HealthMoneyButtonChecks()
    {
        int books = 0;
        Func<long, bool> pay = amount => { books++; return true; };
        var s = new HealthState();
        bool taken = HealthDay.TakePlan(s, HPlan.Standard, 20, 30, 60, 5, 1000, pay, out int amount);
        bool second = HealthDay.TakePlan(s, HPlan.Standard, 20, 30, 60, 5, 1000, pay, out int _);
        Check(taken && !second && books == 1 && amount == 140, "health pages: Take this plan books the first month once, and pressing it again books nothing");
        HealthCover.Owe(s.Debt, 900, 5, 20);
        bool paidOff = HealthDay.PayOff(s, 21, pay, out long owed);
        bool secondOff = HealthDay.PayOff(s, 21, pay, out long _);
        Check(paidOff && !secondOff && books == 2 && owed == 900 && s.Debt.Owed == 0, "health pages: Pay it off now books the debt once, and pressing it again books nothing");
        bool refused = !HealthDay.TakePlan(new HealthState(), HPlan.Full, 20, 30, 60, 5, 1000, a => false, out int _);
        Check(refused && books == 2, "health pages: a plan the wallet cannot pay is not taken and books nothing");
        var l = new LeaveBook();
        HealthLeave.CallIn(l, 20, false, 60, 600, 1020);
        HealthLeave.CallIn(l, 20, false, 60, 600, 1020);
        Check(books == 2 && l.Claims == 1, "health pages: Call in sick and Book an appointment book no money, and calling in twice is one call");
        string live = ModSource(@"Ui\Ventures\HealthLive.cs"), app = ModSource(@"Health\HealthApp.cs"), care = ModSource(@"Health\HealthDirector.Leave.cs");
        Check(CountOfText(live, "d.TakePlan(wanted, out amount)") == 1 && CountOfText(live, "d.PayOffDebt(out amount)") == 1 && CountOfText(app, "HealthLive.Instance.TakePlan(plan)") == 1
              && CountOfText(app, "HealthLive.Instance.PayDebt()") == 1 && CountOfText(care, "HealthDay.TakePlan(") == 1 && CountOfText(care, "HealthDay.PayOff(") == 1,
            "health wiring: each money button of both frames reaches one call of the director, which makes one call of the rules above");
    }

    // ---- nobody dies of health

    private static void HealthDeathChecks()
    {
        // Health holds a serious illness on until the hospital has seen it. Permadeath (when it is on) holds an untreated flu from its first warning and judges it by its own
        // stages. A flu turns serious through neglect only after days, and the check is that by then Permadeath's hold has begun: Health never keeps a flu on an hour sooner.
        bool held = true;
        int turnedFlu = 0;
        double earliest = double.MaxValue;
        for (int seed = 1; seed <= 400; seed++)
        {
            HealthCase c = HealthCaseOf(Condition.Flu, 100);
            for (int d = 1; d <= HealthCatalogue.NeglectCertain + 2; d++)
            {
                if (HealthRules.CaseDay(c, HealthRunDown, seed, 100 + d) == CaseEvent.TurnedSerious)
                {
                    int hours = d * 24;
                    turnedFlu++;
                    earliest = Math.Min(earliest, hours);
                    held &= RealLifeOverhaul.Permadeath.PermadeathRules.IllHeld(RealLifeOverhaul.Permadeath.PermadeathRules.IllStage(hours));
                    break;
                }
            }
        }
        Check(turnedFlu == 400 && held && earliest >= RealLifeOverhaul.Permadeath.PermadeathRules.IllWarnFirst,
            "health unwinnable: a flu Health holds on is held by Permadeath's own rule already (its first warning is at " + RealLifeOverhaul.Permadeath.PermadeathRules.IllWarnFirst + " hours, the earliest turn here at " + earliest
            + "), so Health adds no hour to what Permadeath can do, and what Permadeath does is its own rule with its own warnings");
        // Nothing of health's own can end a life, and nothing a serious case does is a death: it waits for the hospital for ever.
        bool never = true;
        var s = HealthSeriousFlu(HPlan.None);
        for (int d = 0; d < 400; d++)
        {
            never &= HealthRules.CaseDay(s.Case, HealthRunDown, 3, 200 + d) == CaseEvent.None && s.Case.Open && s.Case.Serious && s.Case.Kind == Condition.Flu;
        }
        Check(never, "health unwinnable: a serious case left alone for 400 days stays a serious case: health has no death of its own");
        string perma = ModSource(@"Permadeath\PermadeathRules.cs") + ModSource(@"Permadeath\Mortality.cs");
        bool clear = !perma.Contains("HealthDirector") && !perma.Contains("RealLifeOverhaul.Health");
        string folder = System.IO.Path.GetDirectoryName(ModPath(@"Health\HealthDirector.cs"));
        foreach (string file in System.IO.Directory.GetFiles(folder, "*.cs"))
        {
            string text = System.IO.File.ReadAllText(file);
            clear &= !text.Contains("Mortality") && !text.Contains("PlayerDead") && !text.Contains("Permadeath") && !text.Contains("FuneralHelper");
        }
        Check(clear, "health wiring: health never calls Permadeath, its deaths or the funeral, and Permadeath never calls health");
    }
}
