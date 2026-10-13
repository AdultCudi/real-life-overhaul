using System;
using UI.Notification;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The test tools of the player's bank (the options page). Each does at once what otherwise takes months, and opens
    /// the bank's app on it. The bank's line as it stood before the first test of the session is kept (in the save
    /// too, under <see cref="TestKey"/>), so "put my bank back" undoes every test since.
    ///
    /// "Found a bank for a test" founds one with the least capital, the licence and the capital not taken from the
    /// player: such a bank pays nothing out (no dividend, no sale, nothing from a winding up). "A month now" and "a hard
    /// month" run a month of the bank's days at once, booked today (the money moves as on any day: the day's profit is
    /// kept in the bank, so the player's cash is where it was), the second in a recession with every borrower under
    /// business pressure at its worst. "A run" fills the city with the scare a run begins from; "the regulator's
    /// deadline" brings a breach's deadline to today; "the fund trades" trades it to its parts now.
    /// </summary>
    public sealed partial class OwnBankDirector
    {
        public const string TestKey = "reallife:economy_ownbank_test";

        private bool TestReady(string test, bool needsBank)
        {
            if (Save == null || !ModState.Loaded)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.NotLoaded);
                return false;
            }
            if (!Settings.Economy || !Settings.EconomyBank || !Running || _bank == null)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.FeatureOff);
                return false;
            }
            if (needsBank && !_bank.Running)
            {
                TestRefusals.TellKey(Logger, test, "reallife_ob_test_no_bank");
                return false;
            }
            return true;
        }

        // The bank's line before the first test of the session, kept to be put back.
        private void Keep()
        {
            if (_beforeTests != null)
            {
                return;
            }
            _beforeTests = _bank.Exists ? OwnBankStore.Write(_bank) : string.Empty;
            if (Save.modData != null)
            {
                Save.modData[TestKey] = _beforeTests;
            }
        }

        private void TestDone(string what, int tab)
        {
            Logger?.Warn("Own bank test: " + what + ". " + Describe() + ".");   // asked for by a button: written whether or not diagnostics are on
            Changed();
            Notifications.Show(NotificationType.Info, "reallife_ob_test_done", null, 7f, null, () => Open(tab), true, false);
            Open(tab);
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

        /// <summary>"Test: found a bank" with the least capital, paid by nobody; it pays nothing out.</summary>
        public void TestFound()
        {
            if (!TestReady("Found a bank", false))
            {
                return;
            }
            TestGuard("found", () =>
            {
                if (_bank.Running)
                {
                    TestRefusals.TellKey(Logger, "Found a bank", "reallife_ob_test_already");
                    return;
                }
                Keep();
                BuildingRegistration hq = OwnBankGame.Headquarters();
                OwnBankRules.Found(_bank, Save.Day, new System.Random().Next(1, int.MaxValue), OwnBankRules.CapitalLeast(Prices.Level), Named(null, hq));
                _bank.Test = true;
                TestDone("a bank founded with the least capital, paid by nobody", TabBank);
            });
        }

        /// <summary>"Test: a month now": a month of the bank's days run at once in the city as it is.</summary>
        public void TestMonth()
        {
            if (TestReady("A month of my bank", true))
            {
                TestGuard("month", () => TestDays(GameCalendar.MonthDays, false, "a month of days run now"));
            }
        }

        /// <summary>"Test: a hard month": the same in a recession, with inflation high and business pressure at its worst.</summary>
        public void TestHardMonth()
        {
            if (TestReady("A hard month", true))
            {
                TestGuard("hard month", () => TestDays(GameCalendar.MonthDays, true, "a month of days run now in a recession with inflation at 8% and business pressure at its worst"));
            }
        }

        /// <summary>"Test: a run": the city as scared as a run begins from, and a day run.</summary>
        public void TestRun()
        {
            if (TestReady("A run on my bank", true))
            {
                TestGuard("run", () =>
                {
                    Keep();
                    _bank.Scare = OwnBankCatalogue.ScareMost;
                    TestDays(1, false, "the city's scare at its most, and a day run");
                });
            }
        }

        /// <summary>"Test: the regulator's deadline": a breach's deadline brought to today, and a day run.</summary>
        public void TestDeadline()
        {
            if (!TestReady("The regulator's deadline", true))
            {
                return;
            }
            TestGuard("deadline", () =>
            {
                if (_bank.Stage != Regulator.Breach)
                {
                    TestRefusals.TellKey(Logger, "The regulator's deadline", "reallife_ob_test_no_breach");
                    return;
                }
                Keep();
                _bank.Deadline = _bank.LastDay + 1;
                TestDays(1, false, "the breach's deadline brought to today, and a day run");
            });
        }

        /// <summary>"Test: the fund trades": traded to its parts now.</summary>
        public void TestFund()
        {
            if (!TestReady("The fund trades", true))
            {
                return;
            }
            TestGuard("fund", () =>
            {
                if (_bank.Strategy == 0 || _bank.FundLimit == 0)
                {
                    TestRefusals.TellKey(Logger, "The fund trades", "reallife_ob_test_no_fund");
                    return;
                }
                Keep();
                _bank.Rebalance = true;
                TestDays(1, false, "the fund traded to its parts, and a day run");
            });
        }

        /// <summary>"Test: put my bank back": the bank as it was before the first test of the session.</summary>
        public void TestBack()
        {
            if (!TestReady("Put my bank back", false))
            {
                return;
            }
            TestGuard("back", () =>
            {
                string before = _beforeTests;
                if (before == null && Save.modData != null)
                {
                    Save.modData.TryGetValue(TestKey, out before);
                }
                if (before == null)
                {
                    TestRefusals.TellKey(Logger, "Put my bank back", "reallife_ob_test_nothing_back");
                    return;
                }
                OwnBank back = before.Length > 0 ? OwnBankStore.Read(before, out _) : null;
                _bank = back ?? new OwnBank();
                if (_bank.Running)
                {
                    _bank.LastDay = Save.Day - 1;
                }
                _beforeTests = null;
                Save.modData?.Remove(TestKey);
                TestDone("the bank put back as it was before the tests", TabBank);
            });
        }

        // Some of the bank's days run now, booked today: the bank's own days are counted on and its clock then set back, so
        // that the game's next day is its next day and everything that waits on a day (a deadline, its months) has moved on.
        private void TestDays(int days, bool hard, string what)
        {
            Keep();
            BankCity city = OwnBankGame.City();
            if (hard)
            {
                city.Cycle = CycleState.Recession;
                city.InflationBp = 800;
                city.Trouble = OwnBankCatalogue.FactorMost;
            }
            int from = _bank.LastDay + 1;
            for (int i = 0; i < days && _bank.Running; i++)
            {
                RunDay(from + i, city, false);
            }
            if (_bank.Running)
            {
                OwnBankRules.Rewind(_bank, _bank.LastDay - (from - 1));
            }
            TestDone(what, TabBank);
        }
    }
}
