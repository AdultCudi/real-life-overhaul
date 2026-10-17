using System;
using UI.Notification;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The bank's test tools; its line before the session's first test is kept (<see cref="TestKey"/>) to be put back.</summary>
    public sealed partial class OwnBankDirector
    {
        public const string TestKey = "reallife:economy_ownbank_test";

        /// <summary>Test (Core/EndScale): the bank starts coming to scale today, or is at it at once. False with no bank running.</summary>
        public bool TestScale(bool full)
        {
            if (Save == null || !Running || !_bank.Running)
            {
                return false;
            }
            _bank.ScaleFrom = full ? Save.Day - EndScaleRules.PhaseDays : EndScaleRules.Unset;
            ScaleNow();
            return true;
        }

        private bool TestReady(string test, bool needsBank)
        {
            if (Save == null || !ModState.Loaded)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.NotLoaded);
                return false;
            }
            if (!Settings.Economy || !Settings.EconomyBank || !Running)
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
            Notifications.Show(NotificationType.Info, "reallife_ob_test_done", null, 7f, null, Guard.Wrap(() => Open(tab)), true, false);
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

        /// <summary>A bank founded with the least capital, paid by nobody; it pays nothing out.</summary>
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
                OwnBankRules.Found(_bank, Save.Day, new System.Random().Next(1, int.MaxValue), OwnBankRules.CapitalLeast(Prices.Level, 0, EndScaleRules.Buying(Settings.EndScale)),
                    Named(null, hq));
                _bank.Test = true;
                TestDone("a bank founded with the least capital, paid by nobody", TabBank);
            });
        }

        public void TestMonth()
        {
            if (TestReady("A month of my bank", true))
            {
                TestGuard("month", () => TestDays(GameCalendar.MonthDays, false, "a month of days run now"));
            }
        }

        /// <summary>A month run now in a recession, with inflation high and business pressure at its worst.</summary>
        public void TestHardMonth()
        {
            if (TestReady("A hard month", true))
            {
                TestGuard("hard month", () => TestDays(GameCalendar.MonthDays, true, "a month of days run now in a recession with inflation at 8% and business pressure at its worst"));
            }
        }

        /// <summary>The city as scared as it gets, and a day run.</summary>
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

        /// <summary>A breach's deadline brought to today, and a day run.</summary>
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

        // Days run now and booked today, then the bank's clock set back by as many, so that what waits on a day has moved on.
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
