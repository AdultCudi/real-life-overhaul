using System;
using UI.Notification;

namespace RealLifeOverhaul.Rivals
{
    /// <summary>
    /// The rivals, part: the test buttons (Options > Tests), each doing at once what waits for the late game, through the
    /// code a day's move goes through. "Put the rivals back" restores the file from before the first test of the session
    /// (<see cref="RivalStore.TestKey"/>) and withdraws a test's claim; a test's stake, bid, offer, inspection and smear
    /// are other features' own and stay, as their buttons say.
    /// </summary>
    public sealed partial class RivalDirector
    {
        private bool TestReady(string test)
        {
            if (Save == null || !ModState.Loaded)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.NotLoaded);
                return false;
            }
            if (!Settings.Rivals || !Running)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.FeatureOff);
                return false;
            }
            if (_resting)
            {
                TestRefusals.TellKey(Logger, test, "reallife_rival_test_resting");
                return false;
            }
            return true;
        }

        // The line as it was before the first test of the session, kept to be put back.
        private void Keep()
        {
            if (_beforeTests != null)
            {
                return;
            }
            _beforeTests = RivalStore.Write(_file);
            if (Save.modData != null)
            {
                Save.modData[RivalStore.TestKey] = _beforeTests;
            }
        }

        private void TestGuard(string test, Action work)
        {
            try
            {
                work();
            }
            catch (Exception e)
            {
                Fault("a test (" + test + ")", e);
            }
        }

        private void TestDone(string what)
        {
            Logger?.Warn("Rivals test: " + what + ". " + Describe() + ".");   // asked for by a button: written whether or not diagnostics are on
            RivalBridges.Open?.Invoke();
        }

        // The first tycoon who has come forward; with none, the first comes forward now.
        private Tycoon TestTycoon(int day)
        {
            for (int i = 0; i < _file.Tycoons.Length; i++)
            {
                if (_file.Tycoons[i].Active)
                {
                    return _file.Tycoons[i];
                }
            }
            int came = RivalRules.Activate(_file, 1, day, Prices.Level, _world.Worth, Strength);
            for (int i = 0; i < _file.Tycoons.Length; i++)
            {
                if ((came & (1 << i)) != 0)
                {
                    CameForward(_file.Tycoons[i], day);
                    return _file.Tycoons[i];
                }
            }
            return null;
        }

        // What a kind of move needs of the game, for its test; null when it is there.
        private string TestNeeds(RivalMoveKind kind)
        {
            switch (kind)
            {
                case RivalMoveKind.Property:
                case RivalMoveKind.Outlet:
                    return _world.Shops.Count > 0 ? null : "reallife_rival_test_no_business";
                case RivalMoveKind.Poach:
                    return _world.Shops.Count == 0 ? "reallife_rival_test_no_business" : _world.Staff ? null : "reallife_rival_test_no_staff";
                case RivalMoveKind.Stake:
                case RivalMoveKind.Bid:
                    return _world.Listed ? null : "reallife_rival_test_no_company";
                case RivalMoveKind.Lobby:
                    return _world.Shops.Count == 0 ? "reallife_rival_test_no_business" : _world.Issues ? null : "reallife_rival_test_no_issues";
                case RivalMoveKind.Suit:
                    return _world.Court ? null : "reallife_rival_test_no_court";
                default:
                    return null;
            }
        }

        /// <summary>"Test: a rival's move" of one kind: announced by the first tycoon forward (one comes forward if needed) and come at once.</summary>
        public void TestMove(RivalMoveKind kind)
        {
            string test = "A rival's move (" + kind + ")";
            if (!TestReady(test))
            {
                return;
            }
            TestGuard(test, () =>
            {
                int day = Save.Day;
                RivalGame.Read(_world);
                string need = TestNeeds(kind);
                if (need != null)
                {
                    TestRefusals.TellKey(Logger, test, need);
                    return;
                }
                Keep();
                Tycoon t = TestTycoon(day);
                if (t == null)
                {
                    TestRefusals.TellKey(Logger, test, "reallife_rival_test_none");
                    return;
                }
                if ((kind == RivalMoveKind.Stake || kind == RivalMoveKind.Bid) && !t.Listed)
                {
                    t.Listed = List(t);
                    if (!t.Listed)
                    {
                        TestRefusals.TellKey(Logger, test, "reallife_rival_test_no_holding");
                        return;
                    }
                }
                int tier = Math.Max(RivalCatalogue.BidTier, RivalRules.Tier(_world.Worth, _world.Level, _world.FirstFrom));
                RivalMove m = RivalRules.Make(_file, t, kind, _world, day, tier, Strength);
                if (m == null)
                {
                    TestRefusals.TellKey(Logger, test, "reallife_rival_test_no_business");
                    return;
                }
                m.Test = true;
                Announced(m);
                m.Land = day;   // (come at once: the test shows what the day it comes does)
                Settle(day);
                TestDone(kind + " by " + t.Def.Name + " came: " + m.State + ", size " + m.Amount + (m.Place.Length > 0 ? " at " + m.Place : string.Empty));
            });
        }

        /// <summary>"Test: the next move": the rules pick a tycoon and a move now, announced as on a day the gap is over.</summary>
        public void TestAnnounce()
        {
            const string Test = "The rivals' next move";
            if (!TestReady(Test))
            {
                return;
            }
            TestGuard(Test, () =>
            {
                int day = Save.Day;
                RivalGame.Read(_world);
                Keep();
                TestTycoon(day);
                RivalMove m = RivalRules.Announce(_file, _world, day, Strength, true);
                if (m == null)
                {
                    TestRefusals.TellKey(Logger, Test, _file.Pending() != null ? "reallife_rival_test_pending" : "reallife_rival_test_no_business");
                    return;
                }
                m.Test = true;
                Announced(m);
                TestDone("announced " + m.Kind + " by " + RivalCatalogue.Tycoons[m.Tycoon].Name + " for day " + m.Land);
            });
        }

        /// <summary>"Test: the rivals come forward": all three, whatever the player is worth.</summary>
        public void TestForward()
        {
            const string Test = "The rivals come forward";
            if (!TestReady(Test))
            {
                return;
            }
            TestGuard(Test, () =>
            {
                int day = Save.Day;
                RivalGame.Read(_world);
                Keep();
                int came = RivalRules.Activate(_file, RivalCatalogue.Tiers - 1, day, Prices.Level, _world.Worth, Strength);
                if (came == 0)
                {
                    TestRefusals.TellKey(Logger, Test, "reallife_rival_test_all_forward");
                    return;
                }
                for (int i = 0; i < _file.Tycoons.Length; i++)
                {
                    if ((came & (1 << i)) != 0)
                    {
                        CameForward(_file.Tycoons[i], day);
                    }
                }
                TestDone("came forward: " + came);
            });
        }

        /// <summary>"Test: the rich list": the City Ledger's page of it, now.</summary>
        public void TestList()
        {
            const string Test = "The rich list";
            if (!TestReady(Test))
            {
                return;
            }
            TestGuard(Test, () =>
            {
                if (!ShowList())
                {
                    TestRefusals.TellKey(Logger, Test, "reallife_rival_note_list_later");
                    return;
                }
                Logger?.Warn("Rivals test: the rich list shown in the City Ledger. " + Describe() + ".");
            });
        }

        /// <summary>"Test: take a rival over": the first tycoon forward is out at once, nothing paid either way and their holding left on the market.</summary>
        public void TestTakeover()
        {
            const string Test = "Take a rival over";
            if (!TestReady(Test))
            {
                return;
            }
            TestGuard(Test, () =>
            {
                int day = Save.Day;
                Tycoon t = null;
                for (int i = 0; i < _file.Tycoons.Length && t == null; i++)
                {
                    t = _file.Tycoons[i].Active ? _file.Tycoons[i] : null;
                }
                if (t == null)
                {
                    TestRefusals.TellKey(Logger, Test, "reallife_rival_test_none");
                    return;
                }
                Keep();
                RivalRules.Defeat(_file, t.Index, day, 0, 0);
                Released(null);
                RivalGame.Tell(NotificationType.Success, RivalText.CounterDoneKey(RivalCounter.Takeover), RivalText.Data("rival", t.Def.Name, "holding", t.Def.Holding, "amount", RivalText.Money(0),
                    "back", RivalText.Money(0), "days", RivalCatalogue.BreakUpDays.ToString()), _openPage);
                TestDone(t.Def.Name + " is out (a test: nothing paid)");
            });
        }

        /// <summary>"Test: put the rivals back": the file as it was before the first test of the session.</summary>
        public void TestBack()
        {
            const string Test = "Put the rivals back";
            if (!TestReady(Test))
            {
                return;
            }
            TestGuard(Test, () =>
            {
                if (_beforeTests == null)
                {
                    TestRefusals.TellKey(Logger, Test, "reallife_rival_test_nothing_back");
                    return;
                }
                int withdrawn = 0;
                for (int i = 0; i < _file.Moves.Count; i++)
                {
                    RivalMove m = _file.Moves[i];
                    if (m.Test && m.Kind == RivalMoveKind.Suit && m.Second > 0 && RivalBridges.Withdraw != null && RivalBridges.Withdraw((int)m.Second))
                    {
                        withdrawn++;
                    }
                }
                RivalFile back = RivalStore.Read(_beforeTests, out int _);
                if (back != null)
                {
                    _file = back;
                }
                _beforeTests = null;
                Save.modData?.Remove(RivalStore.TestKey);
                Released(null);
                RivalGame.Tell(NotificationType.Info, "reallife_rival_test_back_done", null, _openPage);
                TestDone("put back as before the tests, " + withdrawn + " test claim(s) withdrawn");
            });
        }

        /// <summary>
        /// "Test: when the first rival comes" prints the threshold in effect: the option's step, its amount at the start's
        /// prices and at today's, the player's worth and tier, and whether the rules put the first tier exactly there and leave
        /// the second where it was. Changes nothing.
        /// </summary>
        public void TestFirst()
        {
            const string Test = "When the first rival comes";
            if (!TestReady(Test))
            {
                return;
            }
            TestGuard(Test, () =>
            {
                RivalGame.Read(_world);
                int step = Settings.RivalsFirst;
                long first = FirstFrom;
                int level = _world.Level;
                long at = RivalRules.At(first, level);
                bool known = step >= 0 && step < RivalCatalogue.FirstFromSteps.Length;
                bool read = _world.FirstFrom == first;
                bool under = RivalRules.Tier(at - 1L, level, first) == 0;
                bool on = RivalRules.Tier(at, level, first) == 1;
                bool second = RivalRules.Tier(RivalRules.At(RivalCatalogue.TierFrom[2], level), level, first) == 2;
                int tier = RivalRules.Tier(_world.Worth, level, first);
                string why = (known ? string.Empty : " step " + step + " is not one of the option's, read as the default;")
                             + (read ? string.Empty : " the day's world holds " + _world.FirstFrom + ";")
                             + (under ? string.Empty : " just under it is not tier 0;") + (on ? string.Empty : " at it is not tier 1;")
                             + (second ? string.Empty : " the second tier moved;");
                Logger?.Warn("Rivals check (first rival): option step " + step + ", " + first + " at the start's prices, " + at + " at today's level " + level
                             + "; the player is worth " + _world.Worth + ", tier " + tier + " (the file says " + _file.Tier + "); the second still at "
                             + RivalRules.At(RivalCatalogue.TierFrom[2], level) + ": " + (why.Length == 0 && known ? "PASS" : "FAIL (" + why.Trim() + ")") + ".");
                RivalGame.Tell(NotificationType.Info, "reallife_rival_test_first_done",
                    RivalText.Data("amount", RivalText.Money(at), "worth", RivalText.Money(_world.Worth)), _openPage);
            });
        }
    }
}
