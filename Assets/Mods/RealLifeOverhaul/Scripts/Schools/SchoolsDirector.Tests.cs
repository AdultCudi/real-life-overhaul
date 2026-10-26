using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Schools
{
    /// <summary>
    /// The schools' test entries (Options > Tests), one for each main part of the campus: each works from wherever the player
    /// is and brings forward what it needs (a results day, an inspection, a term's start, the school run, the school the
    /// player's), writes one "Schools check (...)" line with PASS or FAIL and says in a notice what it did or why not. The
    /// state before the first test is kept for "put the schools back".
    /// </summary>
    public sealed partial class SchoolsDirector
    {
        private const string TestName = "Schools";
        private const float RunTestSeconds = 120f;   // real seconds the families stand at the gates after the school run test
        private const float TakeWait = 20f;           // real seconds the game is given to take the player out of a building or a car

        private static bool _testRun;                 // the school run's test holds the families at the gates
        private static float _testRunEnd;
        private static float _takeUntil;              // the take me test waits to put the player at the gate until then
        private static bool _runAfterTake;            // the school run's test goes on once the player is at the gate
        private static readonly Dictionary<string, bool> TestPrivate = new Dictionary<string, bool>(4);   // a child the enrol test moved, and whether they were at a private school before

        // Why a test cannot run now, said in a notice; false when it can.
        private static bool Refused(string test, bool needsCampus)
        {
            if (SchoolsGame.Loading || Instance == null)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.NotLoaded);
                return true;
            }
            if (!Settings.Schools)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.FeatureOff);
                return true;
            }
            if (!_loaded || !ReferenceEquals(_save, SaveGameManager.Current))
            {
                Load();
            }
            if (_unread || _broken)
            {
                TestRefusals.TellKey(Logger, test, SchoolsText.TestUnread, _unread ? "its line in this save cannot be read" : "it was switched off after a fault");
                return true;
            }
            if (needsCampus && !CampusStands)
            {
                TestRefusals.TellKey(Logger, test, SchoolsText.TestNoCampus, "Birchwood School does not stand at this load");
                return true;
            }
            if (!Instance._running && Uptown.UptownLots.Ready)
            {
                Instance._running = true;
            }
            if (_state.Opened <= 0)
            {
                Instance.DayPass(SchoolsGame.Day);   // the campus opens on the day's pass: a test on the first day does not wait for it
            }
            return false;
        }

        // The state as it is before a test first changes it, kept once to be put back.
        private static void BeforeTest()
        {
            if (_beforeTests == null)
            {
                _beforeTests = SchoolStore.Write(_state);
            }
        }

        private static void Done(string line, bool pass, string what)
        {
            line += ": " + (pass ? "PASS" : "FAIL");
            if (pass)
            {
                Logger?.Info(line);
            }
            else
            {
                Logger?.Warn(line);
            }
            SchoolsGame.Tell(pass ? NotificationType.Info : NotificationType.Warning, pass ? SchoolsText.TestDone : SchoolsText.TestFailed, "what", what);
        }

        // Once a second: a test's waits, the families held at the gates and the take me test's way out of a building.
        private void TestTick()
        {
            if (_testRun && Time.unscaledTime > _testRunEnd)
            {
                _testRun = false;
            }
            CompareTick();
            if (_takeUntil > 0f)
            {
                if (Time.unscaledTime > _takeUntil)
                {
                    _takeUntil = 0f;
                    _runAfterTake = false;
                    Done("Schools check (take me): the game did not let the player out in " + TakeWait + " s", false, SchoolsText.T(SchoolsText.TestTake));
                }
                else if (TakeNow())
                {
                    _takeUntil = 0f;
                    if (_runAfterTake)
                    {
                        _runAfterTake = false;
                        RunNow();
                    }
                }
            }
        }

        // ---------------------------------------------------------------- the campus

        /// <summary>"Birchwood School: census": the campus standing (its main school, its pieces, its two other buildings, its classrooms) and the market's figures, against the rules.</summary>
        public static void TestCensus()
        {
            const string test = "Schools census";
            try
            {
                if (Refused(test, false))
                {
                    return;
                }
                var t = new StringBuilder(600);
                NewBuildings.MadeBuilding main = SchoolsGame.Main;
                SchoolsDirector d = Instance;
                int copies = 0;
                for (int i = 0; i < d._copies.Count; i++)
                {
                    copies += d.LookStands(i + 1) || (d._copies[i] != null && d._copies[i].Ready) ? 1 : 0;
                }
                bool campus = CampusStands && main != null;
                t.Append(campus ? "the main school at " + main.Address + " wearing " + main.Design + " (shown " + main.Shown + ")" : "the main school does not stand at this load");
                t.Append("; ").Append(PiecesStood).Append(" of ").Append(d._pieces.Count).Append(" pieces of the game's own (missing: ").Append(PiecesMissing ?? "none").Append(')');
                t.Append("; the hall and the junior block ").Append(copies).Append(" of ").Append(d._copies.Count).Append(" standing (")
                    .Append((d.LookStands(1) ? 1 : 0) + (d.LookStands(2) ? 1 : 0)).Append(" from the models), ").Append(d._cutCopies).Append(" cut");
                t.Append("; the main school's look ").Append(d.LookStands(0) ? "the model's" : "the copy engine's");
                t.Append("; classrooms held ").Append(d._held).Append(", doors quietened on the last floor laid ").Append(SchoolFloors.Quietened);
                int privatePupils = 0, overPlaces = 0;
                for (int i = 0; i < SchoolRules.Count; i++)
                {
                    SchoolFile f = _state.Files[i];
                    overPlaces += f.Pupils > f.Places ? 1 : 0;
                    privatePupils += SchoolRules.Private(i) ? f.Pupils + f.Waiting : 0;
                }
                t.Append("; the market: ").Append(Describe()).Append(", private families placed or waiting ").Append(privatePupils).Append(" of ").Append(SchoolRules.PrivatePool);
                bool pass = overPlaces == 0 && privatePupils <= SchoolRules.PrivatePool && (!campus || (PiecesStood == d._pieces.Count && copies == d._copies.Count));
                Done("Schools check (census): " + t, pass, SchoolsText.T(SchoolsText.TestCensus));
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>"Birchwood School: take me to its gate": the player out of any building or car and on Uptown Avenue's pavement before the main gate.</summary>
        public static void TestTake()
        {
            const string test = "Schools take me";
            try
            {
                if (Refused(test, true))
                {
                    return;
                }
                if (!TakeNow())
                {
                    _takeUntil = Time.unscaledTime + TakeWait;   // the game takes the player out of a building or a car first: the timer puts them there after
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        private static bool TakeNow()
        {
            SchoolGrounds.Gate(out float x, out float z, out float heading);
            if (!SchoolsGame.TakeTo(new Vector3(x, Uptown.UptownLots.GroundY, z), heading))
            {
                return false;
            }
            Done("Schools check (take me): the player at Birchwood School's main gate (" + x.ToString("F1", CultureInfo.InvariantCulture) + ", "
                 + z.ToString("F1", CultureInfo.InvariantCulture) + ")", true, SchoolsText.T(SchoolsText.Campus));
            return true;
        }

        /// <summary>"Birchwood School: the school run now": the families gather at the gates for two minutes, whatever the hour, while the player is near.</summary>
        public static void TestRun()
        {
            const string test = "Schools school run";
            try
            {
                if (Refused(test, true))
                {
                    return;
                }
                _testRun = true;
                _testRunEnd = Time.unscaledTime + RunTestSeconds;
                if (!Near(CrowdNear))
                {
                    _runAfterTake = true;   // the families gather only while the player is near: the player goes to the gate first
                    if (!TakeNow())
                    {
                        _takeUntil = Time.unscaledTime + TakeWait;
                        return;
                    }
                    _runAfterTake = false;
                }
                RunNow();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // The families at the gates now, whatever the hour, and the line that says how many squares stood.
        private static void RunNow()
        {
            SchoolsDirector d = Instance;
            if (d == null)
            {
                return;
            }
            d._crowding = false;
            d.NoCrowds();
            d.Crowds();
            int stood = 0;
            for (int i = 0; i < d._crowds.Length; i++)
            {
                stood += d._crowds[i] != null ? 1 : 0;
            }
            bool near = Near(CrowdNear);
            Done("Schools check (school run): " + stood + " of " + d._crowds.Length + " squares of families at the gates" + (near ? string.Empty : " (the player is further than "
                 + CrowdNear + " m: none gather)"), near && stood == d._crowds.Length, SchoolsText.T(SchoolsText.TestRun));
        }

        // ---------------------------------------------------------------- the market

        /// <summary>"Birchwood School: results day now": every school's results, the league table and its piece in tomorrow's City Ledger.</summary>
        public static void TestResults()
        {
            const string test = "Schools results day";
            try
            {
                if (Refused(test, false))
                {
                    return;
                }
                BeforeTest();
                int day = SchoolsGame.Day;
                SchoolRules.ResultsNow(_state, day, SchoolRules.YearOf(day, SchoolsGame.Year));
                int[] order = SchoolRules.League(_state);
                bool sorted = true, bounded = true;
                for (int i = 0; i < order.Length; i++)
                {
                    int r = _state.Files[order[i]].Results;
                    bounded &= r >= 0 && r <= 100;
                    sorted &= i == 0 || _state.Files[order[i - 1]].Results >= r;
                }
                int rank = Array.IndexOf(order, SchoolRules.CampusId) + 1;
                Written();
                SchoolsGame.Tell(NotificationType.Info, SchoolsText.NoticeResults, "rank", rank.ToString(CultureInfo.InvariantCulture), "count", order.Length.ToString(CultureInfo.InvariantCulture),
                    "score", _state.Campus.Results.ToString(CultureInfo.InvariantCulture));
                Done("Schools check (results day): the league's first " + SchoolsGame.Name(order[0]) + " at " + _state.Files[order[0]].Results + "%, Birchwood School " + rank + " of "
                     + order.Length + " at " + _state.Campus.Results + "%, in order " + sorted + ", every score from 0 to 100 " + bounded, sorted && bounded, SchoolsText.T(SchoolsText.TestResults));
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>"Birchwood School: an inspection now": the council's inspectors' verdict on it, by the rules.</summary>
        public static void TestInspect()
        {
            const string test = "Schools inspection";
            try
            {
                if (Refused(test, false))
                {
                    return;
                }
                BeforeTest();
                SchoolRules.InspectNow(_state, SchoolRules.CampusId, SchoolsGame.Day);
                int grade = _state.Campus.Grade;
                Written();
                SchoolsGame.Tell(NotificationType.Info, SchoolsText.NoticeInspected, "grade", SchoolsText.Grade(grade));
                Done("Schools check (inspection): Birchwood School rated " + (SchoolGrade)grade + " on teaching " + SchoolRules.Teaching(_state.Campus) + " and results "
                     + _state.Campus.Results, grade >= (int)SchoolGrade.Outstanding && grade <= (int)SchoolGrade.Inadequate, SchoolsText.T(SchoolsText.TestInspect));
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>"Birchwood School: a term starts now": the families placed by the rules and the pay war run, the player's school among the rest.</summary>
        public static void TestTerm()
        {
            const string test = "Schools term";
            try
            {
                if (Refused(test, false))
                {
                    return;
                }
                BeforeTest();
                int moved = SchoolRules.TermNow(_state, SchoolsGame.Day);
                int placed = 0, over = 0;
                for (int i = 0; i < SchoolRules.Count; i++)
                {
                    SchoolFile f = _state.Files[i];
                    over += f.Pupils > f.Places ? 1 : 0;
                    placed += SchoolRules.Private(i) ? f.Pupils + f.Waiting : 0;
                }
                Written();
                Done("Schools check (term): " + placed + " of the city's " + SchoolRules.PrivatePool + " paying families placed or waiting, " + over + " school(s) over their places, "
                     + moved + " teacher(s) moved in the pay war; Birchwood School " + _state.Campus.Pupils + " of " + _state.Campus.Places + ", " + _state.Campus.Waiting + " waiting",
                    placed == SchoolRules.PrivatePool && over == 0, SchoolsText.T(SchoolsText.TestTerm));
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>"Birchwood School: make it mine": the school the player's for nothing, to try its running (not in a Going Public session).</summary>
        public static void TestMine()
        {
            const string test = "Schools make it mine";
            try
            {
                if (Refused(test, false))
                {
                    return;
                }
                if (SchoolsGame.InSession)
                {
                    TestRefusals.TellKey(Logger, test, SchoolsText.NoticeSession, "a Going Public session");
                    return;
                }
                BeforeTest();
                if (_state.Campus.Owner != SchoolOwner.Player)
                {
                    SchoolRules.Sell(_state, SchoolRules.CampusId, SchoolOwner.Player, 0, SchoolsGame.Day);
                }
                Written();
                Instance?.Traffic(true);
                Done("Schools check (make it mine): Birchwood School run by " + OwnerWords(_state.Campus.Owner) + " since day " + _state.OwnedSince, _state.Campus.Owner == SchoolOwner.Player,
                    SchoolsText.T(SchoolsText.TestMine));
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>"Birchwood School: enrol my child": the player's first child of school age moved there; says so when the save has none.</summary>
        public static void TestEnrol()
        {
            const string test = "Schools enrol";
            try
            {
                if (Refused(test, false))
                {
                    return;
                }
                List<SchoolChild> kids = Children();
                string name = null;
                bool wasPrivate = false;
                for (int i = 0; i < kids.Count && name == null; i++)
                {
                    if (!kids[i].Grown && kids[i].Age >= SchoolRules.SchoolFrom && kids[i].Age < SchoolRules.SchoolTo)
                    {
                        name = kids[i].Name;
                        wasPrivate = kids[i].School == 2;
                    }
                }
                if (name == null)
                {
                    TestRefusals.TellKey(Logger, test, SchoolsText.TestNoChild, "the player has no child of school age in this save");
                    return;
                }
                BeforeTest();
                if (!SchoolRules.HasPlace(_state.Campus))
                {
                    SchoolRules.StepClass(_state.Campus, +1);   // a test makes room: a class opened if the campus is full
                }
                bool already = _state.Children.Contains(SchoolStore.Clean(name));
                string why = Enrol(name);
                bool there = _state.Children.Contains(SchoolStore.Clean(name));
                if (there && !already && !TestPrivate.ContainsKey(SchoolStore.Clean(name)))
                {
                    TestPrivate[SchoolStore.Clean(name)] = wasPrivate;   // put back to the school family life had them at
                }
                string reports = StandingOf(name).ToString("F1", CultureInfo.InvariantCulture);
                Done("Schools check (enrol): " + name + (there ? " at Birchwood School, its fee to family life " + FeeFor(name) + " a day, reports " + reports
                    : " not enrolled: " + why), there && FeeFor(name) >= 0, SchoolsText.T(SchoolsText.TestEnrol));
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>"Birchwood School: put everything back as before the tests": the market as it stood before the first test.</summary>
        public static void TestBack()
        {
            const string test = "Schools put back";
            try
            {
                if (Refused(test, false))
                {
                    return;
                }
                if (_beforeTests == null)
                {
                    Done("Schools check (back): nothing a test changed", true, SchoolsText.T(SchoolsText.TestBack));
                    return;
                }
                SchoolState before = SchoolStore.Read(_beforeTests, out _);
                if (before == null)
                {
                    Done("Schools check (back): the kept line could not be read", false, SchoolsText.T(SchoolsText.TestBack));
                    return;
                }
                for (int i = 0; i < _state.Children.Count; i++)
                {
                    string child = _state.Children[i];
                    if (!before.Children.Contains(child))
                    {
                        // A child a test enrolled goes back to the school family life had them at: private or public.
                        SchoolsBridges.SetSchool?.Invoke(child, TestPrivate.TryGetValue(child, out bool wasPrivate) && wasPrivate);
                    }
                }
                TestPrivate.Clear();
                _state = before;
                _beforeTests = null;
                _testRun = false;
                Written();
                Instance?.Traffic(true);
                Done("Schools check (back): the schools as before the first test: " + Describe(), true, SchoolsText.T(SchoolsText.TestBack));
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }
    }
}
