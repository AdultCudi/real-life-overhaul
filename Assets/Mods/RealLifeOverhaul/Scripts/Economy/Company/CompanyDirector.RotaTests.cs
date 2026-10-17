using System;
using System.Collections.Generic;
using UI.Notification;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The rota planner's test tools (Options > Tests).</summary>
    public sealed partial class CompanyDirector
    {
        private bool RotaTestReady(string test)
        {
            if (Save == null || !ModState.Loaded)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.NotLoaded);
                return false;
            }
            if (!Settings.Economy || !Settings.EconomyCompany || !RotaOn)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.FeatureOff);
                return false;
            }
            RotaRead();
            if (_rotaPlaces.Count == 0)
            {
                TestRefusals.TellKey(Logger, test, "reallife_co_test_no_business");
                return false;
            }
            return RotaOn;   // reading the places may have switched the planner off
        }

        /// <summary>"Test: open the rota of my busiest business": the most customers a week by the game's calculator, else the most staff.</summary>
        public void TestRotaOpen() => RotaTestBusiest(false);

        /// <summary>"Test: show my busiest business's plans in the log": its ladder rung by rung.</summary>
        public void TestRotaPlans() => RotaTestBusiest(true);

        private void RotaTestBusiest(bool plans)
        {
            if (!RotaTestReady(plans ? "Rota plans" : "Rota open"))
            {
                return;
            }
            try
            {
                RotaPlace best = null;
                int most = -1;
                for (int i = 0; i < _rotaPlaces.Count; i++)
                {
                    RotaPlace place = _rotaPlaces[i];
                    int customers = place.Type == RotaType.Customers ? RotaGame.WeekCustomers(place.Reg) : 0;
                    int key = customers * 1000 + Math.Min(999, place.Staff);
                    if (key > most)
                    {
                        most = key;
                        best = place;
                    }
                }
                if (plans)
                {
                    Logger?.Warn("Company test: the plans of " + best.Name + " (" + most / 1000 + " customers a week by the game's calculator, " + best.Staff
                                 + " staff) follow in the log" + (RotaFresh(best) ? "." : " once they are worked out, over the coming frames."));
                    if (RotaFresh(best))
                    {
                        RotaPlansLog(best, _rotaSheet, true);
                    }
                    else
                    {
                        RotaAsk(best.Place, JobShow, true);
                    }
                    return;
                }
                Logger?.Warn("Company test: the rota of " + best.Name + " opened (" + most / 1000 + " customers a week by the game's calculator, " + best.Staff
                             + " staff).");
                Open(TabRota, "rota:" + best.Place);
            }
            catch (Exception e)
            {
                RotaFault("a test (open)", e);
            }
        }

        /// <summary>"Test: fill every business's rota": no dialogue, a notice at the end; the weeks as they were are kept.</summary>
        public void TestRotaFillAll()
        {
            if (!RotaTestReady("Rota fill"))
            {
                return;
            }
            try
            {
                _rotaTestFilled = _rotaTestShifts = 0;
                for (int i = 0; i < _rotaPlaces.Count; i++)
                {
                    if (_rotaPlaces[i].Type != RotaType.Warehouse && _rotaPlaces[i].Staff > 0)
                    {
                        RotaAsk(_rotaPlaces[i].Place, JobFill, true);
                    }
                }
                RotaAsk(string.Empty, JobTell, true);
            }
            catch (Exception e)
            {
                RotaFault("a test (fill)", e);
            }
        }

        /// <summary>"Test: put every rota back": every week kept, the tests' and the player's own.</summary>
        public void TestRotaBackAll()
        {
            if (!RotaTestReady("Rota back"))
            {
                return;
            }
            int back = 0;
            try
            {
                while (_rotaUndo.Count > 0 && RotaOn)
                {
                    back += RotaPutBack(_rotaUndo[_rotaUndo.Count - 1].Place) >= 0 ? 1 : 0;
                }
            }
            catch (Exception e)
            {
                RotaFault("a test (back)", e);
            }
            if (back == 0)
            {
                TestRefusals.TellKey(Logger, "Rota back", "reallife_rota_test_nothing_back");
                return;
            }
            Logger?.Warn("Company test: " + back + " rota(s) put back as they were.");
            Notifications.Show(NotificationType.Info, "reallife_rota_test_put_back", new Dictionary<string, string> { { "count", back.ToString() } },
                7f, null, Guard.Wrap(() => Open(TabRota, null)), true, false);
            Changed();
        }
    }
}
