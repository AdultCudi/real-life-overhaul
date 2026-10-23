using UI.Notification;

namespace RealLifeOverhaul.Utilities
{
    /// <summary>The utilities' test buttons (Options, Tests).</summary>
    public sealed partial class UtilitiesDirector
    {
        /// <summary>Test: a month of the market and the company's books now.</summary>
        public void TestNetworkMonth()
        {
            if (!Ready())
            {
                return;
            }
            if (!_state.Network.Exists)
            {
                UtilitiesText.Toast(NotificationType.Warning, UtilitiesText.T("reallife_bb_not_running"), 8f);
                return;
            }
            _company.Month(UtilitiesGame.Today, Prices.Level, GameCalendar.MonthDays, 0);
            _view?.Changed();
        }

        /// <summary>Test: every network being dug opens this morning.</summary>
        public void TestNetworkBuilt()
        {
            if (!Ready())
            {
                return;
            }
            int opened = _company.TestBuilt(UtilitiesGame.Today);
            UtilitiesText.Toast(NotificationType.Info, UtilitiesText.T("reallife_util_test_network_built_done", "count", opened.ToString()), 8f);
            _view?.Changed();
        }

        /// <summary>Test: every connection in an outage from now for two hours.</summary>
        public void TestOutage()
        {
            if (!Ready())
            {
                return;
            }
            int now = UtilitiesGame.Today * 24 + (SaveGameManager.Current != null ? SaveGameManager.Current.Hour : 0), count = 0;
            for (int i = 0; i < _state.Accounts.Count; i++)
            {
                Account a = _state.Accounts[i];
                if (a.Service == Service.Internet && a.State == SupplyState.Active)
                {
                    a.OutageFrom = now;
                    a.OutageUntil = now + 2;
                    count++;
                }
            }
            Effects();
            UtilitiesText.Toast(NotificationType.Warning, UtilitiesText.T("reallife_util_test_outage_done", "count", count.ToString()), 8f);
            _view?.Changed();
        }

        /// <summary>Test: the month billed now, as at its turn.</summary>
        public void TestBill()
        {
            if (!Ready())
            {
                return;
            }
            int today = UtilitiesGame.Today, count = 0, total = 0;
            for (int i = 0; i < _state.Accounts.Count; i++)
            {
                int bill = UtilitiesRules.Bill(_state.Accounts[i], today, GameCalendar.MonthDays);
                if (bill > 0)
                {
                    count++;
                    total += bill;
                }
            }
            _state.MonthStart = today;
            UtilitiesText.Toast(NotificationType.Info, UtilitiesText.T("reallife_util_toast_bills", "count", count.ToString(), "amount", UtilitiesText.Money(total),
                "day", UtilitiesText.Day(today + UtilitiesRules.DueIn(GameCalendar.MonthDays))), 10f);
            _view?.Changed();
        }

        /// <summary>Test: the bills owed fall due today, so that tomorrow is a day late (and the cut follows).</summary>
        public void TestDue()
        {
            if (!Ready())
            {
                return;
            }
            int today = UtilitiesGame.Today, count = 0;
            for (int i = 0; i < _state.Accounts.Count; i++)
            {
                Account a = _state.Accounts[i];
                if (a.Owed > 0)
                {
                    a.DueDay = today;
                    count++;
                }
            }
            UtilitiesText.Toast(NotificationType.Info, UtilitiesText.T("reallife_util_test_due_done", "count", count.ToString()), 8f);
            _view?.Changed();
        }

        /// <summary>Test: the supply at the player's home cut now, as for an unpaid bill.</summary>
        public void TestCutHome()
        {
            if (!Ready())
            {
                return;
            }
            int today = UtilitiesGame.Today, count = 0;
            for (int i = 0; i < _state.Accounts.Count; i++)
            {
                Account a = _state.Accounts[i];
                if (a.Kind == PlaceKind.Home && a.State != SupplyState.Closed)
                {
                    UtilitiesRules.Cut(a, today, true);
                    count++;
                }
            }
            Effects();
            UtilitiesText.Toast(NotificationType.Warning, UtilitiesText.T("reallife_util_test_cut_done", "count", count.ToString()), 8f);
            _view?.Changed();
        }

        /// <summary>Test: every account supplied again with nothing owed, as if all had been paid and reconnected.</summary>
        public void TestRestore()
        {
            if (!Ready())
            {
                return;
            }
            for (int i = 0; i < _state.Accounts.Count; i++)
            {
                Account a = _state.Accounts[i];
                if (a.State == SupplyState.Closed)
                {
                    continue;
                }
                a.Owed = 0;
                a.DueDay = 0;
                a.LateCharged = false;
                a.CutForDebt = false;
                a.State = SupplyState.Active;
                a.TemporaryUntil = 0;
            }
            UtilitiesText.Toast(NotificationType.Success, UtilitiesText.T("reallife_util_test_restore_done"), 8f);
            _view?.Changed();
        }

        private bool Ready()
        {
            if (_loaded)
            {
                return true;
            }
            UtilitiesText.Toast(NotificationType.Warning, UtilitiesText.T("reallife_util_test_not_running"), 6f);
            return false;
        }
    }
}
