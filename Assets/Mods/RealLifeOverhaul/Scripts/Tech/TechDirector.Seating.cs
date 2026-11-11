using System;
using System.Collections.Generic;
using System.Globalization;
using Entities;
using UI.Notification;

namespace RealLifeOverhaul.Tech
{
    /// <summary>What a pass of seating hires found and did: the hires looked at, those seated with their shifts and hours, the weight of demands broken, and those not seated, by why.</summary>
    internal struct SeatReport
    {
        public int Seen, Seated, Shifts, Hours, Unmet, Closed, NoDesk, Full, Wrong;
    }

    /// <summary>
    /// The seating part: a professional hired at one of the player's tech firms, by the game's job board, recruitment office or headhunter or by the
    /// jobs board of the mod, is given a free desk and a week of shifts (SeatPlan, SeatShifts) so they work from their first day, as the player
    /// would otherwise do by hand in BizMan for each of them (the rota planner refuses a business of over 64 stations or 120 staff, so a large firm
    /// could not be planned at all). It runs on the game's hour, for the hires made in the last SeatPlan.Window days who have no shift at a desk, a
    /// batch of SeatPlan.Batch at most, and a hire for whom there is no desk is tried again once a day; the player is told once. Nothing of anyone
    /// else's schedule is moved, only the player's own firms are read, and it is switched off by its option. No frame cost: nothing runs per frame,
    /// and an hour with nobody to seat costs a pass over the people with a few comparisons each.
    /// </summary>
    public sealed partial class TechDirector
    {
        private readonly Dictionary<string, int> _seatTried = new Dictionary<string, int>(16);   // a hire's id: the day no desk could be found for them
        private readonly HashSet<string> _seatTold = new HashSet<string>();                      // the hires the player was told could not be seated
        private readonly HashSet<string> _seatQuiet = new HashSet<string>();                     // the hires the start note already said are being seated
        private readonly HashSet<string> _seatHad = new HashSet<string>();                       // the hires found with a shift at a desk, not looked at again within the window
        private const int SeatHadMost = 4096;                                                    // ids kept in the set above before it starts afresh
        private readonly List<Person> _seatWho = new List<Person>(32);
        private readonly List<BuildingRegistration> _seatAt = new List<BuildingRegistration>(32);   // the business each of them is assigned to now
        private bool _seatBroken;

        /// <summary>
        /// A hire the mod has just started at a business: true if the hour's pass will seat them (the option is on and the business is a tech firm with
        /// shifts), so the start note says so and the pass does not tell the player again. Their id is noted.
        /// </summary>
        private bool SeatsNew(EmployeeInstance e, BuildingRegistration at)
        {
            if (!Settings.TechSeatHires || _seatBroken || e == null || at == null || FirmsGame.KindOf(TalentGame.TypeOf(at)) < 0 || !TalentGame.Shifts(at))
            {
                return false;
            }
            _seatQuiet.Add(e.id);
            return true;
        }

        // What the part remembers is let go when the people part stops.
        private void ForgetSeating()
        {
            _seatTried.Clear();
            _seatTold.Clear();
            _seatQuiet.Clear();
            _seatHad.Clear();
            _seatWho.Clear();
            _seatAt.Clear();
        }

        // Each game hour, with the people read: those lately hired are seated. A fault switches this one off for the session, not the sector.
        private void SeatHour(int day)
        {
            if (!Settings.TechSeatHires || _seatBroken)
            {
                return;
            }
            try
            {
                if (SeatPass(day, false).Seated > 0)
                {
                    Changed();
                }
            }
            catch (Exception e)
            {
                _seatBroken = true;
                Logger?.Error(e);
                Logger?.Warn("Tech: seating new hires is switched off for this session after an error. Schedules are as they were; give shifts in BizMan, Schedule.");
            }
        }

        /// <summary>
        /// Seats the player's professionals at tech firms who have no shift at a desk: those hired within SeatPlan.Window days, at most SeatPlan.Batch
        /// (all of them, at most SeatPlan.TestMost, when <paramref name="all"/> is set for the test). The player is told once for a business, and not
        /// again for a hire who stays unseated.
        /// </summary>
        private SeatReport SeatPass(int day, bool all)
        {
            var report = new SeatReport();
            _seatWho.Clear();
            _seatAt.Clear();
            int most = all ? SeatPlan.TestMost : SeatPlan.Batch;
            for (int i = 0; i < _people.Count && _seatWho.Count < most; i++)
            {
                Person person = _people[i];
                EmployeeInstance e = person.Game;
                if (e == null || e.IsTraining || (!all && !SeatPlan.Newcomer(e.dayHired, day)))
                {
                    continue;
                }
                if (!all && (_seatHad.Contains(e.id) || (_seatTried.TryGetValue(e.id, out int tried) && tried >= day)))
                {
                    continue;
                }
                BuildingRegistration at = TalentGame.Business(e);   // where they are assigned now: the read of the people may be an hour old
                int kind = at != null ? FirmsGame.KindOf(TalentGame.TypeOf(at)) : -1;
                if (kind < 0 || !GoingPublic.Mine(at) || !TalentGame.Shifts(at) || Array.IndexOf(SeatRules.PrimaryOf(kind), TalentGame.Skill(e)) < 0)
                {
                    continue;   // not at a tech firm of the player's with shifts, or hired under a skill the firm does not hire under (a lawyer, an HR manager)
                }
                if (TalentGame.StationShift(e, at))
                {
                    _seatHad.Add(e.id);   // seated by the player or by the game's rota: not looked for again
                    continue;
                }
                _seatWho.Add(person);
                _seatAt.Add(at);
            }
            report.Seen = _seatWho.Count;
            if (_seatHad.Count > SeatHadMost)
            {
                _seatHad.Clear();
            }
            for (int i = 0; i < _seatWho.Count; i++)
            {
                if (_seatWho[i] == null)
                {
                    continue;
                }
                BuildingRegistration reg = _seatAt[i];
                string business = TalentGame.NameOf(reg), first = null;
                int group = 0, done = 0, shifts = 0, hours = 0, unmet = 0, wrong = 0, closed = 0, noDesk = 0, full = 0, toldClosed = 0, toldNoDesk = 0, toldFull = 0;
                SeatShifts.Begin(reg);
                try
                {
                    for (int k = i; k < _seatWho.Count; k++)
                    {
                        Person person = _seatWho[k];
                        if (person == null || _seatAt[k] != reg)
                        {
                            continue;
                        }
                        _seatWho[k] = null;
                        group++;
                        EmployeeInstance e = person.Game;
                        int result = SeatShifts.Seat(e, out int n, out int h, out int broken);
                        if (result == SeatPlan.Seated)
                        {
                            _seatTried.Remove(e.id);
                            wrong += TalentGame.StationShift(e, reg) ? 0 : 1;   // the shifts are in the business's days, at a desk that is placed
                            shifts += n;
                            hours += h;
                            unmet += broken;
                            if (!_seatQuiet.Remove(e.id))
                            {
                                done++;
                                first = first ?? TalentGame.NameOf(e);
                            }
                            continue;
                        }
                        _seatTried[e.id] = day;
                        _seatQuiet.Remove(e.id);   // the start note said a desk would follow and none was free: a later seat is told
                        bool tell = _seatTold.Add(e.id);
                        closed += result == SeatPlan.Closed ? 1 : 0;
                        noDesk += result == SeatPlan.NoDesk ? 1 : 0;
                        full += result == SeatPlan.Full ? 1 : 0;
                        toldClosed += tell && result == SeatPlan.Closed ? 1 : 0;
                        toldNoDesk += tell && result == SeatPlan.NoDesk ? 1 : 0;
                        toldFull += tell && result == SeatPlan.Full ? 1 : 0;
                    }
                }
                finally
                {
                    SeatShifts.End();
                }
                int seated = group - closed - noDesk - full;
                if (Verbose || wrong > 0)
                {
                    string line = "Tech check (seat, " + business + ", day " + day + "): " + seated + " of " + group + " hire(s) given a desk and shifts (" + shifts + " shift(s), "
                                  + hours + " hour(s) a week, demands broken: " + unmet + "), " + closed + " with the business closed, " + noDesk + " with no desk they may use, " + full
                                  + " with every desk taken: " + (wrong == 0 ? "PASS" : "FAIL (" + wrong + " without a shift at a desk after it)");
                    if (wrong == 0)
                    {
                        Logger?.Info(line);
                    }
                    else
                    {
                        Logger?.Warn(line);
                    }
                }
                report.Seated += seated;
                report.Shifts += shifts;
                report.Hours += hours;
                report.Unmet += unmet;
                report.Wrong += wrong;
                report.Closed += closed;
                report.NoDesk += noDesk;
                report.Full += full;
                if (all)
                {
                    continue;   // the test tells the player once, for all of them
                }
                if (done > 0)
                {
                    Tell(NotificationType.Success, done == 1 ? "reallife_hire_note_seated_one" : "reallife_hire_note_seated",
                        Data("name", first, "count", done.ToString(CultureInfo.InvariantCulture), "business", business), TabTeam, null);
                }
                if (toldClosed > 0)
                {
                    Tell(NotificationType.Warning, "reallife_hire_note_seat_closed", Data("count", toldClosed.ToString(CultureInfo.InvariantCulture), "business", business), TabTeam, null);
                }
                if (toldNoDesk > 0)
                {
                    Tell(NotificationType.Warning, "reallife_hire_note_seat_no_desk", Data("count", toldNoDesk.ToString(CultureInfo.InvariantCulture), "business", business), TabTeam, null);
                }
                if (toldFull > 0)
                {
                    Tell(NotificationType.Warning, "reallife_hire_note_seat_full", Data("count", toldFull.ToString(CultureInfo.InvariantCulture), "business", business), TabTeam, null);
                }
            }
            _seatWho.Clear();
            _seatAt.Clear();
            return report;
        }

        /// <summary>
        /// For Options > Tests: every professional of a tech firm of the player's with no shift at a desk is seated now, whenever they were hired (at
        /// most SeatPlan.TestMost), and each is read again to see that they have a shift at a placed desk. The log line says how many, how many shifts
        /// and hours, and how many could not be seated and why.
        /// </summary>
        public void TestSeatHires()
        {
            if (!_peopleOn || Save == null)
            {
                Notifications.ShowError("reallife_talent_test_off");
                return;
            }
            ReadPeople(Save.Day);
            SeatReport r = SeatPass(Save.Day, true);
            if (r.Seen == 0)
            {
                Notifications.ShowError("reallife_hire_test_seat_none");
                return;
            }
            int left = r.Closed + r.NoDesk + r.Full;
            bool ok = r.Wrong == 0 && r.Seated + left == r.Seen;
            string line = "Tech check (seat test): " + r.Seen + " idle professional(s) at the player's tech firms, " + r.Seated + " seated with " + r.Shifts + " shift(s) and " + r.Hours
                          + " hour(s) a week (demands broken, by weight: " + r.Unmet + "), " + r.Closed + " in a business with no opening hours, " + r.NoDesk + " with no desk they may use, "
                          + r.Full + " with every desk taken, " + r.Wrong + " seated but without a shift at a desk after it: " + (ok ? "PASS" : "FAIL");
            if (ok)
            {
                Logger?.Info(line);
            }
            else
            {
                Logger?.Warn(line);
            }
            Notifications.Show(ok ? NotificationType.Success : NotificationType.Error, ok ? "reallife_hire_test_seat_pass" : "reallife_hire_test_seat_fail",
                Data("count", r.Seen.ToString(CultureInfo.InvariantCulture), "seated", r.Seated.ToString(CultureInfo.InvariantCulture), "left", left.ToString(CultureInfo.InvariantCulture),
                    "short", r.Wrong.ToString(CultureInfo.InvariantCulture)), 10f);
            Changed();
        }
    }
}
