using System.Collections.Generic;
using RealLifeOverhaul.Population;
using UI.Notification;

namespace RealLifeOverhaul.Health
{
    /// <summary>
    /// Options, Tests: each situation the player would otherwise wait for. Each says what happened in a notice and writes one "Health check" line. Bills are real: a test
    /// stay is billed as any other. What a test changed in the player's own case and in the staff is noted as it is done, and the last test button takes back exactly that and
    /// nothing else: never a bill, a debt, a plan, the leave or any money.
    /// </summary>
    public sealed partial class HealthDirector
    {
        // What the tests did, noted as they are pressed: the case a test opened, the case a test turned serious (and how it stood), the member of staff made ill tomorrow.
        private bool _testIll, _testSerious, _testSeriousTreated;
        private Condition _testIllKind, _testSeriousKind;
        private int _testIllBegan, _testSeriousBegan, _testStaffDay, _testStaffPrevious;
        private string _testStaffId;

        private void ClearTests()
        {
            _testIll = _testSerious = _testSeriousTreated = false;
            _testIllKind = _testSeriousKind = Condition.None;
            _testIllBegan = _testSeriousBegan = _testStaffDay = _testStaffPrevious = 0;
            _testStaffId = null;
        }

        private bool TestReady()
        {
            if (!Running)
            {
                HealthGame.Tell(NotificationType.Warning, "reallife_hc_test_off", null);
                return false;
            }
            return true;
        }

        private static void TestSays(string key, Dictionary<string, string> data)
        {
            HealthGame.Tell(NotificationType.Info, key, data);
        }

        /// <summary>Options, Tests: the player falls ill now: the flu by Home's own rules where they allow it, else a sprain.</summary>
        public void TestIll()
        {
            if (!TestReady())
            {
                return;
            }
            int day = HealthGame.Day;
            if (_state.Case.Open)
            {
                TestSays("reallife_hc_test_ill_already", HealthGame.Data("illness", HealthText.Name(_state.Case.Kind)));
                return;
            }
            bool flu = HealthBridges.Catch != null && HealthBridges.Catch((int)Condition.Flu, "reallife_hc_cause_test");
            if (flu)
            {
                MirrorHome(day);
            }
            else
            {
                TakeInjury(Condition.Sprain, false);
            }
            if (_state.Case.Open)
            {
                _testIll = true;
                _testIllKind = _state.Case.Kind;
                _testIllBegan = _state.Case.Began;
            }
            TestSays("reallife_hc_test_ill", HealthGame.Data("illness", HealthText.Name(_state.Case.Kind)));
            Logger?.Info("Health check (test, falling ill): " + _state.Case.Kind + ": " + (_state.Case.Open ? "PASS" : "FAIL, no case was opened"));
        }

        /// <summary>Options, Tests: what the player has turns serious now, as after days of neglect.</summary>
        public void TestSerious()
        {
            if (!TestReady())
            {
                return;
            }
            HealthCase c = _state.Case;
            if (!c.Open || _state.Stay.Active)
            {
                TestSays("reallife_hc_test_nothing", null);
                return;
            }
            if (!c.Serious)
            {
                _testSerious = true;
                _testSeriousKind = c.Kind;
                _testSeriousBegan = c.Began;
                _testSeriousTreated = c.Treated;
            }
            c.Serious = true;
            c.Treated = false;
            HealthGame.Drain(true);
            _revision++;
            TestSays("reallife_hc_test_serious", HealthGame.Data("illness", HealthText.Name(c.Kind)));
            Logger?.Info("Health check (test, turned serious): " + c.Kind + ", held on until the hospital: " + (NeedsAdmission ? "PASS" : "FAIL"));
        }

        /// <summary>Options, Tests: the player is admitted now for the serious case, wherever they stand: the days pass and the bill comes.</summary>
        public void TestAdmit()
        {
            if (!TestReady())
            {
                return;
            }
            if (!NeedsAdmission)
            {
                TestSays("reallife_hc_test_not_serious", null);
                return;
            }
            Condition kind = _state.Case.Kind;
            bool ok = Admit(false);
            TestSays(ok ? "reallife_hc_test_admitted" : "reallife_hc_test_not_serious", HealthGame.Data("illness", HealthText.Name(kind), "days", HealthRules.StayDays(kind).ToString()));
            Logger?.Info("Health check (test, admitted for " + kind + "): " + (ok ? "PASS" : "FAIL"));
        }

        /// <summary>Options, Tests: the day's pass over the city's people now, a slice a second.</summary>
        public void TestPeople()
        {
            if (!TestReady())
            {
                return;
            }
            PopulationState people = HealthGame.People();
            if (people == null)
            {
                TestSays("reallife_hc_test_no_people", null);
                return;
            }
            if (_state.Pop.Cursor < 0)
            {
                _state.Pop.Cursor = 0;   // today's draws again: the same people come out, as every draw is the day's own
                _state.Pop.Day = HealthGame.Day;
            }
            TestSays("reallife_hc_test_people", HealthGame.Data("people", people.People.Live.ToString()));
            Logger?.Info("Health check (test, the people's day): begun over " + people.People.Live + " people, a slice a second: PASS");
        }

        /// <summary>Options, Tests: one of the player's staff is off sick tomorrow, through the game's own sick day.</summary>
        public void TestStaff()
        {
            if (!TestReady())
            {
                return;
            }
            string name = HealthGame.StaffTest(_state, HealthGame.Day, out string id, out int previous);
            if (name != null)
            {
                _testStaffId = id;
                _testStaffDay = HealthGame.Day;
                _testStaffPrevious = previous;
            }
            TestSays(name != null ? "reallife_hc_test_staff" : "reallife_hc_test_no_staff", HealthGame.Data("name", name ?? string.Empty));
            Logger?.Info("Health check (test, a member of staff off tomorrow): " + (name != null ? name + ": PASS" : "nobody to make ill"));
        }

        /// <summary>
        /// Options, Tests: takes back what the test buttons did to the player's own case and to the staff, and nothing else. An illness or injury a test opened goes if it is still
        /// that one; a case a test turned serious is as it was if it is still that one and not in hospital; the member of staff a test made ill is well again tomorrow. A real
        /// case that came since is left alone, and no bill, debt, plan, leave or money is touched.
        /// </summary>
        public void TestBack()
        {
            if (!Running)
            {
                HealthGame.Tell(NotificationType.Info, "reallife_hc_test_back_none", null);
                return;
            }
            HealthCase c = _state.Case;
            bool undone = false;
            if (_testStaffId != null)
            {
                HealthGame.StaffUntest(_state, _testStaffId, _testStaffDay, _testStaffPrevious);
                undone = true;
            }
            if (_testSerious && c.Open && c.Kind == _testSeriousKind && c.Began == _testSeriousBegan && c.Serious && !_state.Stay.Active)
            {
                c.Serious = false;
                c.Treated = _testSeriousTreated;
                HealthGame.Drain(false);
                undone = true;
            }
            if (_testIll && c.Open && c.Kind == _testIllKind && c.Began == _testIllBegan && !_state.Stay.Active)
            {
                bool illness = !HealthCatalogue.Is(HealthCatalogue.Injury, c.Kind);
                LeaveBook l = _state.Leave;
                if (l.CaseFrom == c.Began)
                {
                    l.CaseFrom = l.CaseTo = l.CareFrom = 0;   // the days of a case that never was are not sick days
                    l.CaseWork = false;
                }
                c.Clear();
                HealthGame.Drain(false);
                if (illness)
                {
                    HealthBridges.Cure?.Invoke();   // the test's flu goes with the test
                }
                undone = true;
            }
            ClearTests();
            _revision++;
            HealthGame.Tell(NotificationType.Info, undone ? "reallife_hc_test_back" : "reallife_hc_test_back_none", null);
            Logger?.Info("Health check (test, taken back): "
                         + (undone ? "what the tests did to the case and the staff is undone, no money touched" : "nothing of the tests is left to undo") + ": PASS");
        }
    }
}
