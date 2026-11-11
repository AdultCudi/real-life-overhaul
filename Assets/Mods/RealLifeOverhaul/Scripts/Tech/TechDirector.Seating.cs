using System;
using System.Collections.Generic;
using System.Globalization;
using Entities;
using UI.Notification;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// What a pass of seating hires found and did: the hires found without a seat, those seated with their shifts and hours, the weight of demands
    /// broken, and those not seated, by why.
    /// </summary>
    internal struct SeatReport
    {
        public int Seen, Seated, Shifts, Hours, Unmet, Closed, NoDesk, Full, Wrong;
    }

    /// <summary>
    /// The seating part: a professional of the player's tech firms who has no shift at a desk (a hire made by the game's job board, recruitment office or
    /// headhunter or by the jobs board of the mod, or one hired long before) is given a free desk and a week of shifts (SeatPlan, SeatShifts) so they work,
    /// as the player would otherwise do by hand in BizMan for each of them (the rota planner refuses a business of over 64 stations or 120 staff, so a
    /// large firm could not be planned at all). It runs on the game's hour, the newest hires first, at most SeatPlan.Batch hires seated (those seated:
    /// a hire for whom there is no room does not use the batch up) and SeatPlan.Look looked at in an hour (SeatRound). A business that has no room for a
    /// skill is asked once a day, not for each hire waiting there, and the player is told once for it (SeatLedger). One who is found with a shift is not
    /// looked at again in the session, so the shifts the player takes away afterwards stay taken away. Nothing of anyone else's schedule is moved, only the
    /// player's own firms are read, and it is switched off by its option. No frame cost: nothing runs per frame, and an hour with nobody to seat costs one
    /// look at each professional's flags; the first hours after a load read each business's shifts once (a pass over its week) and look at a thousand at most.
    /// </summary>
    public sealed partial class TechDirector
    {
        private readonly SeatLedger _seatLedger = new SeatLedger();
        private readonly HashSet<string> _seatQuiet = new HashSet<string>();                                                  // the hires the start note already said are being seated
        private readonly Dictionary<string, BuildingRegistration> _seatReg = new Dictionary<string, BuildingRegistration>(32);   // the businesses met in a pass, by place key
        private readonly Dictionary<string, HashSet<string>> _seatHolders = new Dictionary<string, HashSet<string>>(32);          // each business's people with a shift at a desk, by place key
        private readonly Stack<HashSet<string>> _seatSpare = new Stack<HashSet<string>>(8);
        private readonly Dictionary<string, int> _seatToldDay = new Dictionary<string, int>(32);                              // the day the player was last told of hires seated at a business
        private int _seatHoldersDay = -1;
        private SeatSource _seatBoard;
        private bool _seatBroken;

        /// <summary>
        /// True if the hour's pass gives a hire of a skill at a business a desk and shifts (the option is on, nothing has broken it, and
        /// <see cref="SeatsAt"/>). What the start note and the hire form promise is asked here, so it is what the pass does.
        /// </summary>
        internal bool SeatsHire(BuildingRegistration at, string skill)
        {
            return Settings.TechSeatHires && !_seatBroken && SeatsAt(at, skill);
        }

        // The business is the player's own tech firm with shifts, and the skill one it hires under: the pass seats them. A lawyer or an HR manager at a
        // software studio, an account manager at the game's web development agency, and anyone at a law firm, a headquarters or a bank are not.
        private static bool SeatsAt(BuildingRegistration at, string skill)
        {
            return at != null && GoingPublic.Mine(at) && TalentGame.Shifts(at) && SeatRules.Seats(FirmsGame.KindOf(TalentGame.TypeOf(at)), skill);
        }

        /// <summary>
        /// A hire the mod has just started at a business: true if the hour's pass will seat them, so the start note says so and the pass does not tell the
        /// player again. Their id is noted.
        /// </summary>
        private bool SeatsNew(EmployeeInstance e, BuildingRegistration at)
        {
            if (e == null || e.characterData == null || e.characterData.skills == null || e.characterData.skills.Count == 0 || !SeatsHire(at, TalentGame.Skill(e)))
            {
                return false;
            }
            _seatQuiet.Add(e.id);
            return true;
        }

        // What the part remembers is let go when the people part stops.
        private void ForgetSeating()
        {
            _seatLedger.Clear();
            _seatQuiet.Clear();
            _seatReg.Clear();
            _seatToldDay.Clear();
            ReleaseHolders();
        }

        // The shifts read for the day are let go: each business is read again the next time a professional of it is looked at.
        private void ReleaseHolders()
        {
            foreach (KeyValuePair<string, HashSet<string>> held in _seatHolders)
            {
                held.Value.Clear();
                _seatSpare.Push(held.Value);
            }
            _seatHolders.Clear();
            _seatHoldersDay = -1;
        }

        // Each game hour, with the people read: those without a seat are seated. A fault switches this one off for the session, not the sector.
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
        /// Seats the player's professionals at tech firms who have no shift at a desk, from the newest hire back: at most SeatPlan.Batch seated and
        /// SeatPlan.Look looked at (every one found, at most SeatPlan.TestMost seated, when <paramref name="all"/> is set for the test). A business that
        /// has no room for a skill is asked once for the day (SeatRound), and the player is told once for it, not again for the hires who stay unseated.
        /// </summary>
        private SeatReport SeatPass(int day, bool all)
        {
            if (_seatBoard == null)
            {
                _seatBoard = new SeatSource(this);
            }
            if (all || _seatHoldersDay != day)
            {
                ReleaseHolders();   // the shifts are read again each day, and for the test as they stand
                _seatHoldersDay = day;
            }
            SeatSource board = _seatBoard;
            board.Start(day, all);
            SeatTally tally;
            try
            {
                tally = SeatRound.Run(board, _seatLedger, day, all ? SeatPlan.TestMost : SeatPlan.Batch, all ? int.MaxValue : SeatPlan.Look, all);
            }
            finally
            {
                _seatReg.Clear();
            }
            SeatReport report = board.Report;
            report.Seen = tally.Seen;
            report.Seated = tally.Seated;
            report.Closed = tally.Closed;
            report.NoDesk = tally.NoDesk;
            report.Full = tally.Full;
            return report;
        }

        // The people part's professionals as the seating rounds look at them, and the game's side of seating one: a business read, a hire given their
        // shifts, the game told, the player told. One instance, kept; the counts of the business being seated are kept in it between Begin and End.
        private sealed class SeatSource : ISeatBoard
        {
            private readonly TechDirector _d;
            private int _day;
            private bool _everyone;
            private BuildingRegistration _reg;
            private string _place, _business, _first;
            private int _group, _done, _shifts, _hours, _unmet, _wrong, _closed, _noDesk, _full;
            public SeatReport Report;

            public SeatSource(TechDirector director)
            {
                _d = director;
            }

            public void Start(int day, bool everyone)
            {
                _day = day;
                _everyone = everyone;
                Report = default(SeatReport);
            }

            public int Count => _d._people.Count;

            public bool Open(int i, bool everyone)
            {
                Person person = _d._people[i];
                return person.Game != null && person.At != null && !ReferenceEquals(person.SeatNoAt, person.At) && (everyone || !person.File.SeatKnown);
            }

            public string Place(int i) => _d._people[i].File.Place;

            public string Skill(int i) => _d._people[i].Skill;

            public bool Idle(int i, out string place)
            {
                place = null;
                Person person = _d._people[i];
                EmployeeInstance e = person.Game;
                if (e == null || e.IsTraining)
                {
                    return false;
                }
                BuildingRegistration at = TalentGame.Business(e);   // where they are assigned now: the read of the people may be an hour old
                if (!SeatsAt(at, person.Skill))
                {
                    person.SeatNoAt = at;   // not a hire the pass seats, until they are somewhere else
                    return false;
                }
                string key = TalentGame.Place(at);
                HashSet<string> holders = HoldersOf(key, at);
                if (holders.Contains(e.id))
                {
                    person.File.SeatKnown = true;
                    return false;
                }
                if (TalentGame.StationShift(e, at))
                {
                    person.File.SeatKnown = true;   // given one since the day's read (by the player, or the game's rota)
                    holders.Add(e.id);
                    return false;
                }
                place = key;
                return true;
            }

            public void Begin(string place)
            {
                _place = place;
                _reg = _d._seatReg[place];
                _business = TalentGame.NameOf(_reg);
                _first = null;
                _group = _done = _shifts = _hours = _unmet = _wrong = _closed = _noDesk = _full = 0;
                SeatShifts.Begin(_reg);
            }

            public int Seat(int i)
            {
                Person person = _d._people[i];
                EmployeeInstance e = person.Game;
                int result = SeatShifts.Seat(e, out int n, out int h, out int broken);
                _group++;
                if (result == SeatPlan.Seated)
                {
                    _wrong += TalentGame.StationShift(e, _reg) ? 0 : 1;   // the shifts are in the business's days, at a desk that is placed
                    _shifts += n;
                    _hours += h;
                    _unmet += broken;
                    person.File.SeatKnown = true;
                    if (_d._seatHolders.TryGetValue(_place, out HashSet<string> holders))
                    {
                        holders.Add(e.id);
                    }
                    if (!_d._seatQuiet.Remove(e.id))
                    {
                        _done++;
                        _first = _first ?? TalentGame.NameOf(e);
                    }
                    return result;
                }
                _d._seatQuiet.Remove(e.id);   // the start note said a desk would follow and none was free: a later seat is told
                _closed += result == SeatPlan.Closed ? 1 : 0;
                _noDesk += result == SeatPlan.NoDesk ? 1 : 0;
                _full += result == SeatPlan.Full ? 1 : 0;
                return result;
            }

            public void End(string place)
            {
                SeatShifts.End();
                int seated = _group - _closed - _noDesk - _full;
                if (Verbose || _wrong > 0)
                {
                    string line = "Tech check (seat, " + _business + ", day " + _day + "): " + seated + " of " + _group + " hire(s) given a desk and shifts (" + _shifts + " shift(s), "
                                  + _hours + " hour(s) a week, demands broken: " + _unmet + "), " + _closed + " with the business closed, " + _noDesk + " with no desk they may use, " + _full
                                  + " with every desk taken: " + (_wrong == 0 ? "PASS" : "FAIL (" + _wrong + " without a shift at a desk after it)");
                    if (_wrong == 0)
                    {
                        Logger?.Info(line);
                    }
                    else
                    {
                        Logger?.Warn(line);
                    }
                }
                Report.Shifts += _shifts;
                Report.Hours += _hours;
                Report.Unmet += _unmet;
                Report.Wrong += _wrong;
                if (_everyone)
                {
                    return;   // the test tells the player once, for all of them
                }
                bool toldToday = _d._seatToldDay.TryGetValue(place, out int toldOn) && toldOn == _day;
                if (_done > 0 && !toldToday)
                {
                    _d._seatToldDay[place] = _day;   // once a business a day: the backlog of a large firm is worked off over many hours
                    _d.Tell(NotificationType.Success, _done == 1 ? "reallife_hire_note_seated_one" : "reallife_hire_note_seated",
                        Data("name", _first, "count", _done.ToString(CultureInfo.InvariantCulture), "business", _business), TabTeam, null);
                }
                Warn(place, _closed, SeatPlan.Closed, "reallife_hire_note_seat_closed");
                Warn(place, _noDesk, SeatPlan.NoDesk, "reallife_hire_note_seat_no_desk");
                Warn(place, _full, SeatPlan.Full, "reallife_hire_note_seat_full");
            }

            // The player is told once that a business has no room for its hires, with how many wait there; again only after it has seated somebody.
            private void Warn(string place, int failed, int reason, string key)
            {
                if (failed > 0 && _d._seatLedger.Tell(place, reason))
                {
                    _d.Tell(NotificationType.Warning, key, Data("count", _d.SeatWaiting(place, _reg).ToString(CultureInfo.InvariantCulture), "business", _business), TabTeam, null);
                }
            }

            // The shifts of a business, read once a day: everyone with a shift at a placed desk, so each of thousands is asked of a set and not of the schedule.
            private HashSet<string> HoldersOf(string key, BuildingRegistration at)
            {
                if (!_d._seatHolders.TryGetValue(key, out HashSet<string> set))
                {
                    set = _d._seatSpare.Count > 0 ? _d._seatSpare.Pop() : new HashSet<string>();
                    TalentGame.StationHolders(at, set);
                    _d._seatHolders[key] = set;
                }
                _d._seatReg[key] = at;
                return set;
            }
        }

        // How many professionals of a business the pass would seat have no shift at a desk now: the number the player is told. One pass over the people, once for a business.
        private int SeatWaiting(string place, BuildingRegistration reg)
        {
            int kind = FirmsGame.KindOf(TalentGame.TypeOf(reg));
            _seatHolders.TryGetValue(place, out HashSet<string> holders);
            int n = 0;
            for (int i = 0; i < _people.Count; i++)
            {
                Person person = _people[i];
                if (person.Game != null && person.File.Place == place && SeatRules.Seats(kind, person.Skill) && (holders == null || !holders.Contains(person.Game.id)))
                {
                    n++;
                }
            }
            return Math.Max(1, n);
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
