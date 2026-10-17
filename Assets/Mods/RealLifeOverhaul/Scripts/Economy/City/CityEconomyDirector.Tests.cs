using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UI.Notification;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The city's economy director: the test tools, which keep the state from before the first test to put back.</summary>
    public sealed partial class CityEconomyDirector
    {
        private bool TestReady(string test)
        {
            if (Save == null || !ModState.Loaded)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.NotLoaded);
                return false;
            }
            if (!Settings.Economy || !Settings.EconomyCity || !_running)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.FeatureOff);
                return false;
            }
            if (SharedCity.Follows('E'))
            {
                TestRefusals.TellKey(Logger, test, "reallife_gp_city_test_host");
                return false;
            }
            return true;
        }

        // A test tool is about to change the state: the state as it is now is kept, once.
        private void BeforeTest()
        {
            if (_beforeTests == null)
            {
                _beforeTests = CityStore.Write(_state);
            }
        }

        private void TestDone(string what)
        {
            SharedCity.Changed('E');
            ApplyAll(Save.Day, true, false, "a test");
            Logger?.Warn("City economy test: " + what + ". " + Summary());   // asked for by a button: written whether or not diagnostics are on
            ShowTestDone();
        }

        private void ShowTestDone()
        {
            Notifications.Show(NotificationType.Info, "reallife_macro_test_done", new Dictionary<string, string>
            {
                { "state", CityText.State(_state.Cycle) }, { "rate", CityText.Rate(_state.Base) }, { "index", CityText.Index(_state.Index) }
            }, 8f, null, Guard.Wrap(OpenCity), true, false);
        }

        public void TestShow()
        {
            if (!TestReady("City economy report"))
            {
                return;
            }
            try
            {
                var text = new StringBuilder(900);
                text.Append("City economy: day ").Append(Save.Day).Append(", model day ").Append(_state.Age).Append(", month ").Append(CityModel.MonthNumber(_state, _monthDays))
                    .Append(" (").Append(CityModel.DaysToTurn(_state, _monthDays)).Append(" day(s) to the next). ").Append(Summary());
                if (_state.Pending)
                {
                    text.Append(" Announced for the turn of the month: ").Append(_state.PendingMove == 0 ? "the rate is held" : "the rate moves by " + Quarters(_state.PendingMove))
                        .Append(" (").Append(_state.PendingReason).Append(").");
                }
                text.Append(" Rents and wages x").Append(Factor(CityModel.Costs(_state))).Append(", takings x").Append(Factor(CityModel.Takings(_state, _level)))
                    .Append("; carried: rent ").Append(_state.CarryRent).Append(", wages ").Append(_state.CarryWages).Append(", takings ").Append(_state.CarryTakings)
                    .Append(", shops and services ").Append(_state.CarryLiving).Append(". Test changes in place: ").Append(_beforeTests != null ? "yes" : "no").Append('.');
                Logger?.Warn(text.ToString());
                for (int i = 0; i < _state.Districts.Count; i++)
                {
                    CityDistrict d = _state.Districts[i];
                    Logger?.Warn("City economy:   district " + d.Name + ": income " + CityText.Income(d.Income) + " (started at " + CityText.Income(d.Start) + ", a month ago "
                                 + CityText.Income(d.Before) + "), footfall " + CityText.Percent(CityModel.DistrictFootfall(d)) + ", bears "
                                 + (CityModel.Tolerance(_state, d, _level) / 10f).ToString("0.#", CultureInfo.InvariantCulture) + "% of the game's own price.");
                }
                for (int k = 0; k < MacroCatalogue.SupplyKinds; k++)
                {
                    SupplyLine line = _state.Supply[k];
                    string shock = line.Shock == SupplyShock.None ? string.Empty
                        : ", " + line.Shock + (line.Announced(_state.Age) ? " from tomorrow" : " running") + " for " + line.Days + " day(s), size "
                          + (line.Size / 10f).ToString("0.#", CultureInfo.InvariantCulture) + "%";
                    Logger?.Warn("City economy:   supply " + k + " (" + CityGame.CountOf(k) + " good(s)): factor " + Factor(line.Factor) + ", with prices x"
                                 + Factor(CityModel.Wholesale(_state, k)) + shock + ((_state.Buys & (1 << k)) != 0 ? ", stocked by the player" : string.Empty) + ".");
                }
                float multiplier = Save.gameVariables != null ? Save.gameVariables.baseCustomerPromotionMultiplier : 0.5f;
                for (int i = 0; i < _shops.Count; i++)
                {
                    CityShop s = _shops[i];
                    Logger?.Warn("City economy:   business " + s.Reg.BusinessName + " in " + (s.District != null ? s.District.Name : "?") + ": footfall " + CityText.Percent(s.Wanted)
                                 + " asked (cycle " + s.Cycle + ", district " + s.Area + ", prices " + s.Price + "), "
                                 + (s.HasFigure ? "promotion " + s.Own + " to " + s.Written + ", which gives " + CityText.Percent(s.Achieved(multiplier)) : "no promotion figure")
                                 + "; prices " + (s.Against > 0 ? "at " + (s.Against / 10f).ToString("0.#", CultureInfo.InvariantCulture) + "% of what the district bears"
                                                                  + (s.Over ? " (over)" : string.Empty) : "none to judge by") + ".");
                }
                string detail;
                bool ok = CityGame.CheckWholesale(_state, out detail);
                Logger?.Warn("Economy check (city, the test button): " + detail + ": " + (ok ? "PASS" : "FAIL"));
                ShowTestDone();
            }
            catch (Exception e)
            {
                Fault("a test", e);
            }
        }

        /// <summary>"Test: step the city's economy a month": the model counts the days to the next turn of a month at once, each with today's season, weather and events.</summary>
        public void TestMonth()
        {
            if (!TestReady("City economy month"))
            {
                return;
            }
            try
            {
                BeforeTest();
                int today = Save.Day;
                CityDay env = DayOf(today);
                int steps = CityModel.DaysToTurn(_state, _monthDays);
                bool turned = false;
                for (int i = 0; i < steps; i++)
                {
                    turned |= CityModel.Day(_state, today, env, _level, _monthDays).MonthTurned;
                }
                TestDone("a month stepped (" + steps + " model day(s))");
                if (turned)
                {
                    CityEconomy.RaiseMonthTurned(e => Logger?.Error(e));
                }
            }
            catch (Exception e)
            {
                Fault("a test", e);
            }
        }

        /// <summary>"Test: put the economy in ...": the cycle is in that state from now, in its first month, for three months at least.</summary>
        public void TestCycle(CycleState state)
        {
            if (!TestReady("City economy cycle"))
            {
                return;
            }
            try
            {
                if (state == CycleState.Recession && !MacroCatalogue.Recessions(_level))
                {
                    TestRefusals.TellKey(Logger, "City economy cycle", "reallife_macro_test_no_recession");
                    return;
                }
                BeforeTest();
                _state.Cycle = state;
                _state.MonthsIn = 1;
                _state.MonthsLeft = MacroCatalogue.OpeningSteadyMonths;
                TestDone("the cycle set to " + state);
            }
            catch (Exception e)
            {
                Fault("a test", e);
            }
        }

        /// <summary>"Test: a shortage (or a glut) from tomorrow": the largest of its kind, for five days, in goods the player's businesses stock if there are any.</summary>
        public void TestShock(bool shortage)
        {
            if (!TestReady("City economy supply"))
            {
                return;
            }
            try
            {
                int kind = -1;
                for (int pass = 0; pass < 2 && kind < 0; pass++)
                {
                    for (int k = 0; k < MacroCatalogue.SupplyKinds && kind < 0; k++)
                    {
                        if (_state.Supply[k].Shock == SupplyShock.None && (pass == 1 || (_state.Buys & (1 << k)) != 0))
                        {
                            kind = k;
                        }
                    }
                }
                if (kind < 0)
                {
                    TestRefusals.TellKey(Logger, "City economy supply", "reallife_macro_test_supply_full");
                    return;
                }
                BeforeTest();
                CityModel.Announce(_state, Save.Day, kind, shortage ? SupplyShock.Shortage : SupplyShock.Glut,
                    shortage ? MacroCatalogue.ShortageMost : MacroCatalogue.GlutMost, 5, _level);
                TestDone((shortage ? "a shortage" : "a glut") + " of kind " + kind + " from tomorrow, " + CityModel.ShockPercent(_state.Supply[kind], _level) + "% for 5 days");
                Notify(new CityTick { ShockAnnounced = kind });
            }
            catch (Exception e)
            {
                Fault("a test", e);
            }
        }

        /// <summary>"Test: bank rate up (or down) one point": at once, inside its bounds.</summary>
        public void TestRate(int quarters)
        {
            if (!TestReady("City economy rate"))
            {
                return;
            }
            try
            {
                BeforeTest();
                int before = _state.Base;
                _state.Base = MacroCatalogue.Clamp(_state.Base + quarters, MacroCatalogue.BaseLeast, MacroCatalogue.BaseMost);
                TestDone("the bank rate " + Quarters(before) + " to " + Quarters(_state.Base));
                Notify(new CityTick { RateMoved = _state.Base - before, ShockAnnounced = -1 });
            }
            catch (Exception e)
            {
                Fault("a test", e);
            }
        }

        /// <summary>"Test: raise (or lower) this district's income by 10": the district the player is in, inside its bounds.</summary>
        public void TestDistrict(int tenths)
        {
            if (!TestReady("City economy district"))
            {
                return;
            }
            try
            {
                CityDistrict d = _state.District(NeighborhoodHelper.CurrentNeighborhood) ?? (_shops.Count > 0 ? _shops[0].District : null)
                                 ?? (_state.Districts.Count > 0 ? _state.Districts[0] : null);
                if (d == null)
                {
                    TestRefusals.Tell(Logger, "City economy district", TestRefusal.NothingFits);
                    return;
                }
                BeforeTest();
                int before = d.Income;
                d.Income = MacroCatalogue.Clamp(d.Income + tenths, Math.Max(MacroCatalogue.IncomeLeast, d.Start - MacroCatalogue.IncomeBand),
                    Math.Min(MacroCatalogue.IncomeMost, d.Start + MacroCatalogue.IncomeBand));
                TestDone("the income of " + d.Name + " " + CityText.Income(before) + " to " + CityText.Income(d.Income));
            }
            catch (Exception e)
            {
                Fault("a test", e);
            }
        }

        /// <summary>"Test: put the city's economy back": the state is what it was before the first test tool changed it.</summary>
        public void TestBack()
        {
            if (!TestReady("City economy back"))
            {
                return;
            }
            try
            {
                int version;
                CityState back = _beforeTests != null ? CityStore.Read(_beforeTests, out version) : null;
                if (back == null)
                {
                    _beforeTests = null;
                    TestRefusals.TellKey(Logger, "City economy back", "reallife_macro_test_nothing_back");
                    return;
                }
                back.LastDay = Save.Day;
                CityModel.KeepReal(back, _state);
                CityGame.Districts(back);
                _state = back;
                _beforeTests = null;
                CityEconomy.Live = back;
                TestDone("the state from before the tests is back");
            }
            catch (Exception e)
            {
                Fault("a test", e);
            }
        }
    }
}
