using System;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Household
{
    /// <summary>
    /// Household staff's test tools (Options, Test tools), each working where the player stands and writing one check line:
    /// take on one of each, a day's work now, the staff seen at work at home, the mechanic's round now, the staff at a house
    /// in the Hamptons, the staff at two homes, and everything put back. They work at the home the player is in when it has
    /// staff, else the first home with staff, else the home the player is in or their first. The first one pressed keeps the
    /// state as it was (under <see cref="TestKey"/>, in the save too); money a test moves is real and is not taken back.
    /// </summary>
    public sealed partial class HouseholdDirector
    {
        private const float ShowSeconds = 180f;   // real seconds the test keeps somebody at work at home
        private const float ShowCheckAfter = 3f;  // seconds before the check line: the character is put down on the next looks
        private float _showCheck;
        private bool _showHamptons;               // the show check is the Hamptons test's: they must be inside the house's walls

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

        // The home a test works at: the one the player is in when it has staff, else the first home with staff, else theirs.
        private BuildingRegistration TestHome()
        {
            if (HouseholdGame.InsideHome(out string street, out int number) && HouseholdRules.Staffed(_state.Staff, street, number))
            {
                return HouseholdGame.HomeAt(street, number);
            }
            for (int i = 0; i < _state.Staff.Count; i++)
            {
                BuildingRegistration home = HomeOf(_state.Staff[i]);
                if (home != null)
                {
                    return home;
                }
            }
            return Home;
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

        private static string Named(BuildingRegistration home) => home != null ? home.StreetNumber + " " + home.StreetName : "no home";

        /// <summary>Takes on the agency's person of every kind the test's home has nobody of. Nothing is charged until they work.</summary>
        public void TestHire()
        {
            Test("hire", () =>
            {
                if (!TestReady("Household staff: hire", false))
                {
                    return;
                }
                BeforeTest();
                BuildingRegistration home = TestHome();
                string refused = null;
                for (int i = 0; i < HouseholdRules.Roles.Length; i++)
                {
                    StaffKind kind = HouseholdRules.Roles[i].Kind;
                    if (!_state.Has(kind, home.StreetName, home.StreetNumber))
                    {
                        string refusal = Hire(kind, home);
                        if (refusal != null)
                        {
                            refused = (refused == null ? string.Empty : refused + ", ") + kind + " (" + refusal + ")";
                        }
                    }
                }
                int here = CountAt(home);
                bool cookOff = !HouseholdGame.CanStore;
                int wanted = HouseholdRules.Roles.Length - (cookOff ? 1 : 0);
                Check("test hire", here >= wanted, here + " on the staff at " + Named(home) + " (" + Names() + "), wanted " + wanted
                      + (cookOff ? " (no cook: home cooking is off)" : string.Empty) + (refused != null ? "; refused: " + refused : string.Empty));
                HouseholdText.Toast(NotificationType.Info, "reallife_household_test_hired", new StaffFill().Count(here), 9f);
            });
        }

        private int CountAt(BuildingRegistration home)
        {
            int n = 0;
            for (int i = 0; home != null && i < _state.Staff.Count; i++)
            {
                n += _state.Staff[i].WorksAt(home.StreetName, home.StreetNumber) ? 1 : 0;
            }
            return n;
        }

        /// <summary>Everybody at the test's home does a visit's work now, paid as a visit is, and the day's note comes at once.</summary>
        public void TestDay()
        {
            Test("day", () =>
            {
                if (!TestReady("Household staff: a day's work", true))
                {
                    return;
                }
                BeforeTest();
                BuildingRegistration home = TestHome();
                int day = HouseholdGame.Day;
                float tidyBefore = HouseholdGame.Tidiness(home);
                int leftBefore = HouseholdGame.Leftovers;
                HouseholdGame.CarsAt(home, _cars);
                int cars = _cars.Count, dirtyBefore = 0;
                for (int i = 0; i < _cars.Count; i++)
                {
                    dirtyBefore += HouseholdGame.Dirty(_cars[i]) ? 1 : 0;
                }
                _cars.Clear();
                bool allPaid = true, cleans = false, cooks = false, washes = false;
                _round.Clear();
                _round.AddRange(_state.Staff);
                for (int i = 0; i < _round.Count; i++)
                {
                    StaffMember m = _round[i];
                    if (!m.WorksAt(home.StreetName, home.StreetNumber))
                    {
                        continue;
                    }
                    StaffRole role = HouseholdRules.Role(m.Kind);
                    cleans |= role.Cleans;
                    cooks |= role.Cooks && HouseholdGame.CanStore;
                    washes |= role.Washes;
                    int missed = m.Missed;
                    Begin(m, role, day, home);
                    allPaid &= m.Missed <= missed && _state.Staff.Contains(m);
                    if (role.Kind != StaffKind.Mechanic)
                    {
                        End(m, role, day, home);
                    }
                }
                _round.Clear();
                float tidyAfter = HouseholdGame.Tidiness(home);
                int leftAfter = HouseholdGame.Leftovers;
                HouseholdGame.CarsAt(home, _cars);
                int dirtyAfter = 0;
                for (int i = 0; i < _cars.Count; i++)
                {
                    dirtyAfter += HouseholdGame.Dirty(_cars[i]) ? 1 : 0;
                }
                _cars.Clear();
                bool pass = allPaid && (!cleans || tidyAfter >= tidyBefore) && (!cooks || leftAfter > leftBefore || leftBefore >= HouseholdRules.LeftoversMost)
                            && (!washes || dirtyAfter == 0);
                Check("test day", pass, "at " + Named(home) + ": tidiness " + Mathf.RoundToInt(tidyBefore) + "% to " + Mathf.RoundToInt(tidyAfter) + "%"
                      + (cleans ? " (wanted no lower)" : " (nobody cleans)") + ", portions in the fridge " + leftBefore + " to " + leftAfter
                      + (cooks ? " (wanted more, or the fridge full)" : " (nobody cooks)") + ", dirty cars at home " + dirtyBefore + " of " + cars + " to " + dirtyAfter
                      + (washes ? " (wanted none)" : " (nobody washes)") + (allPaid ? string.Empty : "; a visit could not be paid"));
                SendNote(day);
                HouseholdText.Toast(NotificationType.Info, "reallife_household_test_day_done", default, 9f);
            });
        }

        /// <summary>Somebody of the home's staff at work in it now, whatever the hour, for three minutes. The player must be in a staffed home.</summary>
        public void TestShow()
        {
            Test("show", () =>
            {
                if (!TestReady("Household staff: at work at home", true))
                {
                    return;
                }
                if (!HouseholdGame.InsideHome(out string street, out int number) || !HouseholdRules.Staffed(_state.Staff, street, number))
                {
                    TestRefusals.TellKey(Logger, "Household staff: at work at home", "reallife_household_test_not_home", Names());
                    return;
                }
                StaffMember who = Shown(street, number);
                if (who == null)
                {
                    TestRefusals.TellKey(Logger, "Household staff: at work at home", "reallife_household_test_nobody", "only the mechanic, who works at the cars");
                    return;
                }
                Force(who, false);
                HouseholdText.Toast(NotificationType.Info, "reallife_household_test_shown", StaffFill.For(who.Name).With(HouseholdText.KindWord(who.Kind)), 8f);
            });
        }

        // Somebody of that home who can be seen at work: anybody but the mechanic.
        private StaffMember Shown(string street, int number)
        {
            for (int i = 0; i < _state.Staff.Count; i++)
            {
                StaffMember m = _state.Staff[i];
                if (m.Kind != StaffKind.Mechanic && m.WorksAt(street, number))
                {
                    return m;
                }
            }
            return null;
        }

        private void Force(StaffMember who, bool hamptons)
        {
            float now = Time.unscaledTime;
            _forced = who;
            _forcedUntil = now + ShowSeconds;
            _showCheck = now + ShowCheckAfter;
            _showHamptons = hamptons;
            _next = 0f;
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
            if (_showHamptons)
            {
                bool indoors = pass && _atHome.Hamptons && _atHome.ShownIndoors;
                Check("test hamptons at work", indoors, !pass ? "nobody is shown (no clear floor inside the house on its navigation mesh, no character could be made, or the player left)"
                    : shown.Name + " is at work " + (_atHome.ShownIndoors ? "inside the house's walls" : "outside the house's walls") + " (wanted inside)");
                pass = indoors;
            }
            else
            {
                Check("test at home", pass, pass ? shown.Name + " is at work in the home" : "nobody is shown (no clear floor on a navigation mesh, no character could be made, or the player left)");
            }
            if (!pass)
            {
                HouseholdText.Toast(NotificationType.Info, "reallife_household_test_not_shown", default, 9f);
            }
        }

        /// <summary>The mechanic's round now, on every car of the player's at the test's home; with no mechanic there the agency's is taken on first.</summary>
        public void TestMechanic()
        {
            Test("mechanic", () =>
            {
                if (!TestReady("Household staff: the mechanic", false))
                {
                    return;
                }
                BeforeTest();
                BuildingRegistration home = TestHome();
                if (!_state.Has(StaffKind.Mechanic, home.StreetName, home.StreetNumber) && Hire(StaffKind.Mechanic, home) != null)
                {
                    TestRefusals.TellKey(Logger, "Household staff: the mechanic", "reallife_household_no_already");
                    return;
                }
                StaffMember m = HouseholdRules.FindAt(_state.Staff, StaffKind.Mechanic, home.StreetName, home.StreetNumber);
                HouseholdGame.CarsAt(home, _cars);
                if (_cars.Count == 0)
                {
                    _cars.Clear();
                    Check("test mechanic", false, "no car of the player's at " + Named(home) + ": " + HouseholdGame.CarRule(home));
                    TestRefusals.TellKey(Logger, "Household staff: the mechanic", "reallife_household_test_no_cars");
                    return;
                }
                string before = Cars(home);
                int missed = m.Missed;
                MechanicRound(m, HouseholdGame.Day, home);
                string after = Cars(home);
                HouseholdGame.CarsAt(home, _cars);
                bool done = m.Missed <= missed;
                for (int i = 0; i < _cars.Count; i++)
                {
                    done &= !HouseholdGame.Dirty(_cars[i]) && HouseholdGame.RepairCost(_cars[i]) == 0 && HouseholdGame.ServiceCost(_cars[i]) == 0;
                }
                _cars.Clear();
                Check("test mechanic", done, "at " + Named(home) + ", before: " + before + "; after: " + after + (m.Missed > missed ? "; the round could not be paid" : string.Empty)
                      + " (wanted every car clean, repaired and with nothing due)");
                SendNote(HouseholdGame.Day);
                HouseholdText.Toast(NotificationType.Info, "reallife_household_test_mechanic_done", default, 9f);
            });
        }

        // The cars at a home in a few words each, for the check line: dirt, repair bill, service due.
        private string Cars(BuildingRegistration home)
        {
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

        /// <summary>
        /// A house in the Hamptons the player stands in, as household staff see it: found as the player's home and the player
        /// at home in it, a cleaner (or the butler) there taken on if none is, a visit's cleaning done on the house's own floor
        /// (laid out through the game's Hamptons path), the cars on its plot counted, and then the cleaner shown at work for
        /// three minutes inside the house's walls (a second check line a few seconds later).
        /// </summary>
        public void TestHamptons()
        {
            Test("hamptons", () =>
            {
                const string name = "Household staff: a house in the Hamptons";
                if (!TestReady(name, false))
                {
                    return;
                }
                BuildingRegistration home = HouseholdGame.InsideHome(out string street, out int number) ? HouseholdGame.HomeAt(street, number) : null;
                if (home == null || !HouseholdGame.IsHamptons(home))
                {
                    TestRefusals.TellKey(Logger, name, "reallife_household_test_not_hamptons", home != null ? Named(home) + " is not a Hamptons house" : "not in a home of the player's");
                    return;
                }
                BeforeTest();
                bool found = HouseholdGame.Inside(street, number);
                StaffMember cleaner = HouseholdRules.FindAt(_state.Staff, StaffKind.Butler, street, number) ?? HouseholdRules.FindAt(_state.Staff, StaffKind.Cleaner, street, number);
                string refusal = null;
                if (cleaner == null)
                {
                    refusal = Hire(StaffKind.Cleaner, home);
                    cleaner = HouseholdRules.FindAt(_state.Staff, StaffKind.Cleaner, street, number);
                }
                if (cleaner == null)
                {
                    Check("test hamptons", false, "no cleaner could be taken on for " + Named(home) + " (" + refusal + ")");
                    TestRefusals.TellKey(Logger, name, refusal ?? "reallife_household_no_already");
                    return;
                }
                float before = HouseholdGame.Tidiness(home);
                int missed = cleaner.Missed;
                StaffRole role = HouseholdRules.Role(cleaner.Kind);
                Begin(cleaner, role, HouseholdGame.Day, home);
                bool paid = cleaner.Missed <= missed && _state.Staff.Contains(cleaner);
                if (paid)
                {
                    End(cleaner, role, HouseholdGame.Day, home);
                }
                float after = HouseholdGame.Tidiness(home);
                int cells = HouseholdGame.FloorCells(home);
                HouseholdGame.CarsAt(home, _cars);
                int cars = _cars.Count;
                _cars.Clear();
                bool pass = found && paid && cells > 0 && after >= before;
                Check("test hamptons", pass, Named(home) + " is the player's home and they are " + (found ? "in it" : "not in it, by the game's own test") + "; " + cleaner.Name
                      + " the " + cleaner.Kind + (paid ? " cleaned it" : " could not be paid") + "; its floor " + cells + " cells (wanted some, laid out through the game's Hamptons path), tidiness "
                      + Mathf.RoundToInt(before) + "% to " + Mathf.RoundToInt(after) + "% (wanted no lower); " + cars + " car(s) at home by " + HouseholdGame.CarRule(home));
                SendNote(HouseholdGame.Day);
                if (!_state.Staff.Contains(cleaner))
                {
                    return;
                }
                Force(cleaner, true);
                HouseholdText.Toast(NotificationType.Info, "reallife_household_test_hamptons_done", StaffFill.For(cleaner.Name).With(HouseholdText.KindWord(cleaner.Kind)), 9f);
            });
        }

        /// <summary>
        /// Staff at two homes: a cleaner taken on at the home the player is in (or their first) and at a second home if either
        /// has none, each found at their own home, the second cleaner refused a move to the first (it has one), each shown at
        /// their own home only, and the staff written to the save's line and read back with each member's home. Two homes needed.
        /// </summary>
        public void TestHomes()
        {
            Test("homes", () =>
            {
                const string name = "Household staff: two homes";
                if (!TestReady(name, false))
                {
                    return;
                }
                HouseholdGame.Homes(_homes);
                BuildingRegistration a = Home, b = null;
                for (int i = 0; i < _homes.Count && b == null; i++)
                {
                    b = ReferenceEquals(_homes[i], a) ? null : _homes[i];
                }
                int homes = _homes.Count;
                _homes.Clear();
                if (b == null)
                {
                    TestRefusals.TellKey(Logger, name, "reallife_household_test_two_homes", homes + " home(s)");
                    return;
                }
                BeforeTest();
                string refused = null;
                foreach (BuildingRegistration home in new[] { a, b })
                {
                    if (!_state.Has(StaffKind.Cleaner, home.StreetName, home.StreetNumber))
                    {
                        string refusal = Hire(StaffKind.Cleaner, home);
                        refused = refusal != null ? (refused == null ? string.Empty : refused + ", ") + Named(home) + " (" + refusal + ")" : refused;
                    }
                }
                StaffMember atA = HouseholdRules.FindAt(_state.Staff, StaffKind.Cleaner, a.StreetName, a.StreetNumber);
                StaffMember atB = HouseholdRules.FindAt(_state.Staff, StaffKind.Cleaner, b.StreetName, b.StreetNumber);
                bool both = atA != null && atB != null && !ReferenceEquals(atA, atB) && ReferenceEquals(HomeOf(atA), a) && ReferenceEquals(HomeOf(atB), b);
                bool refusedMove = atB != null && Move(atB, a) != null && atB.WorksAt(b.StreetName, b.StreetNumber);
                bool apart = true;
                if (both)
                {
                    // Both on a visit now, as the hour's round would have them: each is shown at their own home only.
                    int day = HouseholdGame.Day, keepA = atA.OnDay, keepB = atB.OnDay;
                    StaffRole role = HouseholdRules.Role(StaffKind.Cleaner);
                    atA.OnDay = day;
                    atB.OnDay = day;
                    apart = HouseholdRules.OnShow(_state.Staff, day, 0, role.From, a.StreetName, a.StreetNumber) != atB
                            && HouseholdRules.OnShow(_state.Staff, day, 0, role.From, b.StreetName, b.StreetNumber) != atA;
                    atA.OnDay = keepA;
                    atB.OnDay = keepB;
                }
                HouseholdState back = HouseholdStore.Read(HouseholdStore.Write(_state), out _);
                bool kept = back != null && back.Staff.Count == _state.Staff.Count;
                for (int i = 0; kept && i < back.Staff.Count; i++)
                {
                    kept = back.Staff[i].WorksAt(_state.Staff[i].HomeStreet, _state.Staff[i].HomeNumber) && back.Staff[i].Name == _state.Staff[i].Name;
                }
                bool pass = both && refusedMove && apart && kept;
                Check("test homes", pass, "a cleaner at " + Named(a) + (atA != null ? " (" + atA.Name + ")" : " (none)") + " and at " + Named(b) + (atB != null ? " (" + atB.Name + ")" : " (none)")
                      + (both ? ", each at their own home" : ", wanted one at each") + "; the second moved to the first: " + (refusedMove ? "refused, it has a cleaner" : "allowed (wanted refused)")
                      + "; each shown at their own home only: " + (apart ? "yes" : "no") + "; the save's line read back with every member's home: " + (kept ? "yes" : "no")
                      + (refused != null ? "; refused: " + refused : string.Empty));
                if (both)
                {
                    HouseholdText.Toast(NotificationType.Info, "reallife_household_test_homes_done", StaffFill.For(atA.Name).At(HouseholdGame.Where(a)).With(atB.Name), 9f);
                }
                else
                {
                    TestRefusals.TellKey(Logger, name, refused != null && refused.Contains("homes_most") ? "reallife_household_no_homes_most" : "reallife_household_no_already", refused);
                }
            });
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
