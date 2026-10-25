using System;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Household
{
    /// <summary>
    /// Household staff's test tools (Options, Test tools), each working where the player stands and writing one check line:
    /// take on one of each, a day's work now, the staff seen at work at home, the mechanic's round now, and everything put
    /// back. The first one pressed keeps the state as it was (under <see cref="TestKey"/>, in the save too); money a test
    /// moves is real and is not taken back.
    /// </summary>
    public sealed partial class HouseholdDirector
    {
        private const float ShowSeconds = 180f;   // real seconds the test keeps somebody at work at home
        private const float ShowCheckAfter = 3f;  // seconds before the check line: the character is put down on the next looks
        private float _showCheck;

        // True if the tools can run; otherwise the reason has been told.
        private bool TestReady(string test, bool needsStaff)
        {
            if (SaveGameManager.Current == null || !ModState.Loaded)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.NotLoaded);
                return false;
            }
            if (!Settings.HouseholdStaff)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.FeatureOff);
                return false;
            }
            if (_broken)
            {
                TestRefusals.TellKey(Logger, test, "reallife_household_phone_off", "switched off for this session after a fault");
                return false;
            }
            if (!_loaded)
            {
                Load();
            }
            if (Home == null)
            {
                TestRefusals.TellKey(Logger, test, "reallife_household_page_no_home");
                return false;
            }
            if (needsStaff && !_state.Any)
            {
                TestRefusals.TellKey(Logger, test, "reallife_household_test_nobody");
                return false;
            }
            return true;
        }

        private void BeforeTest()
        {
            if (_beforeTests == null)
            {
                _beforeTests = HouseholdStore.Write(_state);
            }
        }

        private static void Check(string what, bool pass, string detail)
        {
            Logger?.Info("Household staff check (" + what + "): " + detail + ": " + (pass ? "PASS" : "FAIL") + ".");
        }

        private void Test(string name, Action run)
        {
            try
            {
                run();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                Logger?.Warn("Household staff test (" + name + "): stopped by the error above; nothing else was changed.");
            }
        }

        /// <summary>Takes on the agency's person of every kind not on the staff yet, at the player's home. Nothing is charged until they work.</summary>
        public void TestHire()
        {
            Test("hire", () =>
            {
                if (!TestReady("Household staff: hire", false))
                {
                    return;
                }
                BeforeTest();
                string refused = null;
                for (int i = 0; i < HouseholdRules.Roles.Length; i++)
                {
                    StaffKind kind = HouseholdRules.Roles[i].Kind;
                    if (!_state.Has(kind))
                    {
                        string refusal = Hire(kind);
                        if (refusal != null)
                        {
                            refused = (refused == null ? string.Empty : refused + ", ") + kind + " (" + refusal + ")";
                        }
                    }
                }
                bool cookOff = !HouseholdGame.CanStore;
                int wanted = HouseholdRules.Roles.Length - (cookOff ? 1 : 0);
                Check("test hire", _state.Staff.Count >= wanted, _state.Staff.Count + " on the staff at " + _state.HomeNumber + " " + _state.HomeStreet + " (" + Names() + "), wanted "
                      + wanted + (cookOff ? " (no cook: home cooking is off)" : string.Empty) + (refused != null ? "; refused: " + refused : string.Empty));
                HouseholdText.Toast(NotificationType.Info, "reallife_household_test_hired", new StaffFill().Count(_state.Staff.Count), 9f);
            });
        }

        /// <summary>Everybody on the staff does a visit's work now, paid as a visit is, and the day's note comes at once.</summary>
        public void TestDay()
        {
            Test("day", () =>
            {
                if (!TestReady("Household staff: a day's work", true))
                {
                    return;
                }
                BeforeTest();
                BuildingRegistration home = WorkPlace(HouseholdGame.Day);
                if (home == null)
                {
                    return;
                }
                int day = HouseholdGame.Day;
                float tidyBefore = HouseholdGame.Tidiness();
                int leftBefore = HouseholdGame.Leftovers;
                HouseholdGame.CarsAt(home, _cars);
                int cars = _cars.Count, dirtyBefore = 0;
                for (int i = 0; i < _cars.Count; i++)
                {
                    dirtyBefore += HouseholdGame.Dirty(_cars[i]) ? 1 : 0;
                }
                _cars.Clear();
                bool allPaid = true;
                _round.Clear();
                _round.AddRange(_state.Staff);
                for (int i = 0; i < _round.Count; i++)
                {
                    StaffMember m = _round[i];
                    StaffRole role = HouseholdRules.Role(m.Kind);
                    int missed = m.Missed;
                    Begin(m, role, day, home);
                    allPaid &= m.Missed <= missed && _state.Staff.Contains(m);
                    if (role.Kind != StaffKind.Mechanic)
                    {
                        End(m, role, day, home);
                    }
                }
                _round.Clear();
                float tidyAfter = HouseholdGame.Tidiness();
                int leftAfter = HouseholdGame.Leftovers;
                HouseholdGame.CarsAt(home, _cars);
                int dirtyAfter = 0;
                for (int i = 0; i < _cars.Count; i++)
                {
                    dirtyAfter += HouseholdGame.Dirty(_cars[i]) ? 1 : 0;
                }
                _cars.Clear();
                bool cleans = _state.Has(StaffKind.Cleaner) || _state.Has(StaffKind.Butler);
                bool cooks = (_state.Has(StaffKind.Cook) || _state.Has(StaffKind.Butler)) && HouseholdGame.CanStore;
                bool washes = _state.Has(StaffKind.Butler) || _state.Has(StaffKind.Mechanic);
                bool pass = allPaid && (!cleans || tidyAfter >= tidyBefore) && (!cooks || leftAfter > leftBefore || leftBefore >= HouseholdRules.LeftoversMost)
                            && (!washes || dirtyAfter == 0);
                Check("test day", pass, "tidiness " + Mathf.RoundToInt(tidyBefore) + "% to " + Mathf.RoundToInt(tidyAfter) + "%" + (cleans ? " (wanted no lower)" : " (nobody cleans)")
                      + ", portions in the fridge " + leftBefore + " to " + leftAfter + (cooks ? " (wanted more, or the fridge full)" : " (nobody cooks)")
                      + ", dirty cars at home " + dirtyBefore + " of " + cars + " to " + dirtyAfter + (washes ? " (wanted none)" : " (nobody washes)")
                      + (allPaid ? string.Empty : "; a visit could not be paid"));
                SendNote(day);
                HouseholdText.Toast(NotificationType.Info, "reallife_household_test_day_done", default, 9f);
            });
        }

        /// <summary>Somebody on the staff at work in the home now, whatever the hour, for three minutes. The player must be in the staff's home.</summary>
        public void TestShow()
        {
            Test("show", () =>
            {
                if (!TestReady("Household staff: at work at home", true))
                {
                    return;
                }
                if (!HouseholdGame.Inside(_state.HomeStreet, _state.HomeNumber))
                {
                    TestRefusals.TellKey(Logger, "Household staff: at work at home", "reallife_household_test_not_home", _state.HomeNumber + " " + _state.HomeStreet);
                    return;
                }
                StaffMember who = null;
                for (int i = 0; i < _state.Staff.Count && who == null; i++)
                {
                    who = _state.Staff[i].Kind != StaffKind.Mechanic ? _state.Staff[i] : null;
                }
                if (who == null)
                {
                    TestRefusals.TellKey(Logger, "Household staff: at work at home", "reallife_household_test_nobody", "only the mechanic, who works at the cars");
                    return;
                }
                float now = Time.unscaledTime;
                _forced = who;
                _forcedUntil = now + ShowSeconds;
                _showCheck = now + ShowCheckAfter;
                _next = 0f;
                HouseholdText.Toast(NotificationType.Info, "reallife_household_test_shown", StaffFill.For(who.Name).With(HouseholdText.KindWord(who.Kind)), 8f);
            });
        }

        // The show test's check line, a few looks after it was pressed.
        private void ShowCheck(float now)
        {
            if (_showCheck <= 0f || now < _showCheck)
            {
                return;
            }
            _showCheck = 0f;
            StaffMember shown = _atHome.Shown;
            bool pass = shown != null && ReferenceEquals(shown, _forced);
            Check("test at home", pass, pass ? shown.Name + " is at work in the home" : "nobody is shown (no clear floor on a navigation mesh, no character could be made, or the player left)");
            if (!pass)
            {
                HouseholdText.Toast(NotificationType.Info, "reallife_household_test_not_shown", default, 9f);
            }
        }

        /// <summary>The mechanic's round now, on every car of the player's at home; with no mechanic on the staff the agency's is taken on first.</summary>
        public void TestMechanic()
        {
            Test("mechanic", () =>
            {
                if (!TestReady("Household staff: the mechanic", false))
                {
                    return;
                }
                BeforeTest();
                if (!_state.Has(StaffKind.Mechanic) && Hire(StaffKind.Mechanic) != null)
                {
                    TestRefusals.TellKey(Logger, "Household staff: the mechanic", "reallife_household_no_already");
                    return;
                }
                StaffMember m = HouseholdRules.Find(_state.Staff, StaffKind.Mechanic);
                BuildingRegistration home = WorkPlace(HouseholdGame.Day);
                if (home == null)
                {
                    return;
                }
                HouseholdGame.CarsAt(home, _cars);
                if (_cars.Count == 0)
                {
                    _cars.Clear();
                    Check("test mechanic", false, "no car of the player's in the home's garage or within " + HouseholdRules.HomeReach + " m of its door");
                    TestRefusals.TellKey(Logger, "Household staff: the mechanic", "reallife_household_test_no_cars");
                    return;
                }
                string before = Cars();
                int missed = m.Missed;
                MechanicRound(m, HouseholdGame.Day, home);
                string after = Cars();
                HouseholdGame.CarsAt(home, _cars);
                bool done = m.Missed <= missed;
                for (int i = 0; i < _cars.Count; i++)
                {
                    done &= !HouseholdGame.Dirty(_cars[i]) && HouseholdGame.RepairCost(_cars[i]) == 0 && HouseholdGame.ServiceCost(_cars[i]) == 0;
                }
                _cars.Clear();
                Check("test mechanic", done, "before: " + before + "; after: " + after + (m.Missed > missed ? "; the round could not be paid" : string.Empty)
                      + " (wanted every car clean, repaired and with nothing due)");
                SendNote(HouseholdGame.Day);
                HouseholdText.Toast(NotificationType.Info, "reallife_household_test_mechanic_done", default, 9f);
            });
        }

        // The cars at home in a few words each, for the check line: dirt, repair bill, service due.
        private string Cars()
        {
            BuildingRegistration home = Home;
            HouseholdGame.CarsAt(home, _cars);
            string text = null;
            for (int i = 0; i < _cars.Count; i++)
            {
                HomeCar c = _cars[i];
                string one = HouseholdGame.CarName(c) + " dirt " + HouseholdGame.DirtPercent(c) + "% repair " + HouseholdGame.RepairCost(c) + " service " + HouseholdGame.ServiceCost(c);
                text = text == null ? one : text + ", " + one;
            }
            _cars.Clear();
            return text ?? "no cars";
        }

        /// <summary>Puts the staff back as they were before the first test tool was pressed.</summary>
        public void TestBack()
        {
            Test("back", () =>
            {
                if (!TestReady("Household staff: put back", false))
                {
                    return;
                }
                if (_beforeTests == null)
                {
                    Check("test back", true, "nothing to put back: no test changed the staff");
                    HouseholdText.Toast(NotificationType.Info, "reallife_household_test_back_done", default, 9f);
                    return;
                }
                HouseholdState back = HouseholdStore.Read(_beforeTests, out _);
                StopAll();
                if (back != null)
                {
                    back.LastStamp = HouseholdGame.Stamp;
                    _state = back;
                }
                _beforeTests = null;
                Check("test back", back != null, back != null ? "the staff as before the tests: " + (_state.Any ? Names() : "nobody")
                      : "the kept line could not be read; the staff are as the tests left them");
                HouseholdText.Toast(NotificationType.Info, "reallife_household_test_back_done", default, 9f);
            });
        }
    }
}
