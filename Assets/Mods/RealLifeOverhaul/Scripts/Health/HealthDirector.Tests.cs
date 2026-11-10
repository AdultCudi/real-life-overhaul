using System.Collections.Generic;
using RealLifeOverhaul.Population;
using UI.Notification;

namespace RealLifeOverhaul.Health
{
    /// <summary>
    /// Options, Tests: each situation the player would otherwise wait for. Each says what happened in a notice and writes one "Health check" line. Bills are real: a test
    /// stay is billed as any other.
    /// </summary>
    public sealed partial class HealthDirector
    {
        private bool TestReady()
        {
            if (!Running)
            {
                HealthGame.Tell(NotificationType.Warning, "reallife_hc_test_off", null);
                return false;
            }
            if (_beforeTests == null)
            {
                _beforeTests = HealthStore.Write(_state);   // what "put health back as before" restores
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
            string name = HealthGame.StaffTest(_state, HealthGame.Day);
            TestSays(name != null ? "reallife_hc_test_staff" : "reallife_hc_test_no_staff", HealthGame.Data("name", name ?? string.Empty));
            Logger?.Info("Health check (test, a member of staff off tomorrow): " + (name != null ? name + ": PASS" : "nobody to make ill"));
        }

        /// <summary>Options, Tests: health as it was before the first test. Money that moved stays moved.</summary>
        public void TestBack()
        {
            if (!Running || _beforeTests == null)
            {
                HealthGame.Tell(NotificationType.Info, "reallife_hc_test_back_none", null);
                return;
            }
            HealthState before = HealthStore.Read(_beforeTests, out int _);
            if (before != null)
            {
                EndStay();
                before.GameStays = HealthGame.GameStays;
                before.LastDay = _state.LastDay;
                before.LastHour = _state.LastHour;
                _state = before;
                HealthGame.Drain(false);
                if (HomeIllness() != Condition.None && !_state.Case.Open)
                {
                    HealthBridges.Cure?.Invoke();   // the test's flu goes with the test
                }
            }
            _beforeTests = null;
            _revision++;
            HealthGame.Tell(NotificationType.Info, "reallife_hc_test_back", null);
            Logger?.Info("Health check (test, put back): the state as before the tests: " + (before != null ? "PASS" : "FAIL, the kept line could not be read"));
        }
    }
}
