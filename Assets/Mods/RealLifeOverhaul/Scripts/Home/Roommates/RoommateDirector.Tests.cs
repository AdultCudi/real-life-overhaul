using System;
using UI.Notification;

namespace RealLifeOverhaul.Roommates
{
    /// <summary>
    /// The roommate's test tools (Options, Tests). The first one pressed keeps the state as it was (under <see cref="TestKey"/>,
    /// in the save too) and "put roommates back" restores it; money a test moves is real and is not taken back.
    /// </summary>
    public sealed partial class RoommateDirector
    {
        // True if the tools can run; otherwise the reason has been told.
        private bool TestReady(string test, bool needsMate)
        {
            if (SaveGameManager.Current == null || !ModState.Loaded)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.NotLoaded);
                return false;
            }
            if (!RoommateSettings.Enabled)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.FeatureOff);
                return false;
            }
            if (_broken)
            {
                TestRefusals.TellKey(Logger, test, "reallife_roommate_test_broken");
                return false;
            }
            if (!_loaded)
            {
                Load();
            }
            if (needsMate && !_state.Living)
            {
                TestRefusals.TellKey(Logger, test, "reallife_roommate_test_nobody");
                return false;
            }
            if (!needsMate && _state.Living)
            {
                TestRefusals.TellKey(Logger, test, "reallife_roommate_test_living");
                return false;
            }
            return true;
        }

        // The home and the room a test needs; false with the reason told.
        private bool TestRoom(string test, out RoomKind room)
        {
            room = RoomKind.None;
            BuildingRegistration home = RoommateGame.AnyHome();
            if (home == null)
            {
                TestRefusals.TellKey(Logger, test, "reallife_roommate_page_no_home");
                return false;
            }
            room = RoommateGame.Room(home, PartnerIn);
            if (room == RoomKind.None)
            {
                RoommateGame.Furniture(home, out int beds, out int sofas);
                TestRefusals.TellKey(Logger, test, "reallife_roommate_page_empty_none", beds + " bed(s), " + sofas + " sofa(s), partner in " + PartnerIn);
                return false;
            }
            return true;
        }

        private void BeforeTest()
        {
            if (_beforeTests == null)
            {
                _beforeTests = RoommateStore.Write(_state);
            }
        }

        private void TestDone(string what, string noticeKey, in Fill f)
        {
            BuildAlerts();
            _next = 0f;
            Logger?.Info("Roommates test: " + what + ".");
            RoommateText.Toast(NotificationType.Info, noticeKey, f, 9f);
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
                Logger?.Warn("Roommates test (" + name + "): stopped by the error above; nothing else was changed.");
            }
        }

        public void TestCandidates()
        {
            Test("candidates", () =>
            {
                if (!TestReady("Roommate candidates", false) || !TestRoom("Roommate candidates", out RoomKind room))
                {
                    return;
                }
                BeforeTest();
                // The notice number is moved on until it is one that three people answer: the same people every time for this save.
                for (int tries = 0; tries < 64; tries++)
                {
                    PutUp(room, true);
                    if (_state.Candidates.Count == 3)
                    {
                        break;
                    }
                }
                TestDone("a notice is up with " + _state.Candidates.Count + " answer(s) in, not charged", "reallife_roommate_test_candidates_done", new Fill().Count(_state.Candidates.Count));
            });
        }

        public void TestMoveIn()
        {
            Test("move in", () =>
            {
                if (!TestReady("Roommate moves in", false) || !TestRoom("Roommate moves in", out RoomKind room))
                {
                    return;
                }
                BeforeTest();
                Flatmate who;
                if (_state.AdUp && _state.Candidates.Count > 0)
                {
                    who = _state.Candidates[0];
                }
                else
                {
                    RoommateRules.Candidates(_state.Seed, _state.Ads + 1, RoommateGame.Stamp, room, _rolled);
                    who = _rolled[0];
                }
                string refusal = MoveIn(who);
                if (refusal != null)
                {
                    TestRefusals.TellKey(Logger, "Roommate moves in", refusal);
                    return;
                }
                TestDone(who.Person.Name + " moved in, " + _state.Weekly + " a week, next rent day " + _state.RentDay, "reallife_roommate_test_move_in_done", Fill.For(who.Person.Name));
            });
        }

        public void TestRentShort()
        {
            Test("rent short", () =>
            {
                if (!TestReady("Roommate rent short", true))
                {
                    return;
                }
                BeforeTest();
                int had = _state.Owed;
                RentDue(RoommateGame.Day, true);
                TestDone(_state.Mate.Person.Name + " paid " + (_state.Weekly - (_state.Owed - had)) + " of " + _state.Weekly + " and owes " + _state.Owed + " by day " + _state.OwedDue
                         + " (the payment is real)",
                    "reallife_roommate_test_rent_done", Fill.For(_state.Mate.Person.Name).Cost(_state.Owed));
            });
        }

        public void TestNoise()
        {
            Test("noise", () =>
            {
                if (!TestReady("Roommate noisy night", true))
                {
                    return;
                }
                BeforeTest();
                Events(DayEvents.Noise, RoommateGame.Day, true, RoommateGame.Day - _state.MovedIn);
                TestDone("a noisy night: the mood, the sleep owed and the topic are set", "reallife_roommate_test_noise_done", Fill.For(_state.Mate.Person.Name));
            });
        }

        public void TestCooked()
        {
            Test("cooked", () =>
            {
                if (!TestReady("Roommate cooked", true))
                {
                    return;
                }
                BeforeTest();
                int had = _state.Meals;
                Events(DayEvents.Cooked, RoommateGame.Day, true, RoommateGame.Day - _state.MovedIn);
                if (_state.Meals == had)
                {
                    TestRefusals.TellKey(Logger, "Roommate cooked", "reallife_roommate_test_cooked_no");
                    return;
                }
                TestDone("a portion was left in the fridge", "reallife_roommate_test_cooked_done", Fill.For(_state.Mate.Person.Name));
            });
        }

        public void TestNotice()
        {
            Test("notice", () =>
            {
                if (!TestReady("Roommate gives notice", true))
                {
                    return;
                }
                if (_state.OnNotice)
                {
                    TestRefusals.TellKey(Logger, "Roommate gives notice", "reallife_roommate_test_on_notice");
                    return;
                }
                BeforeTest();
                TheirNotice(EndWhy.NewJob, RoommateGame.Day);
                TestDone("notice given, leaving on day " + _state.LeaveDay, "reallife_roommate_test_notice_done", Fill.For(_state.Mate.Person.Name).On(RoommateText.When(_state.LeaveDay)));
            });
        }

        public void TestBack()
        {
            Test("back", () =>
            {
                if (SaveGameManager.Current == null || !ModState.Loaded)
                {
                    TestRefusals.Tell(Logger, "Roommates back", TestRefusal.NotLoaded);
                    return;
                }
                if (!RoommateSettings.Enabled)
                {
                    TestRefusals.Tell(Logger, "Roommates back", TestRefusal.FeatureOff);
                    return;
                }
                if (!_loaded)
                {
                    Load();
                }
                if (_beforeTests == null)
                {
                    TestRefusals.TellKey(Logger, "Roommates back", "reallife_roommate_test_back_nothing");
                    return;
                }
                _flat.Leave();
                int version;
                RoommateState before = RoommateStore.Read(_beforeTests, out version) ?? new RoommateState();
                if (before.Seed == 0)
                {
                    before.Seed = _state.Seed;
                }
                before.Shift(before.LastDay > 0 ? RoommateGame.Day - before.LastDay : 0);   // the days the tests were played with are not owed for
                before.LastDay = RoommateGame.Day;
                before.LastStamp = RoommateGame.Stamp;
                _state = before;
                _beforeTests = null;
                TestDone("the state from before the tests is back (" + (_state.Living ? _state.Mate.Person.Name + " lives in" : "nobody lives in") + "); money the tests moved is not taken back",
                    "reallife_roommate_test_back_done", default);
            });
        }
    }
}
