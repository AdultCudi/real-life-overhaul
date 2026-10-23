using System;
using UI.Notification;

namespace RealLifeOverhaul.Utilities
{
    /// <summary>The utilities' bills part: accounts connecting and cut, bills falling due, paying, and applying in person.</summary>
    public sealed partial class UtilitiesDirector
    {
        private void Turns(int today)
        {
            for (int i = 0; i < _state.Accounts.Count; i++)
            {
                Account a = _state.Accounts[i];
                SupplyState before = a.State;
                if (!UtilitiesRules.Turn(a, today))
                {
                    continue;
                }
                string where = NameOf(a);
                if (before == SupplyState.Pending && a.State == SupplyState.Active)
                {
                    UtilitiesText.Toast(NotificationType.Success, UtilitiesText.T("reallife_util_toast_connected", "service", UtilitiesText.Service(a.Service), "place", where), 8f);
                }
                else if (a.State == SupplyState.Cut)
                {
                    UtilitiesText.Toast(NotificationType.Warning, UtilitiesText.T("reallife_util_toast_temporary_over", "service", UtilitiesText.Service(a.Service), "place", where,
                        "office", OfficeName()), 12f);
                }
            }
        }

        // bills falling due: auto debit pays them; a day overdue adds the late fee, three days cut the supply
        private void Dues(int today)
        {
            for (int i = 0; i < _state.Accounts.Count; i++)
            {
                Account a = _state.Accounts[i];
                if (a.Owed > 0 && a.AutoDebit && a.DueDay > 0 && today >= a.DueDay)
                {
                    if (!Pay(a, today, true))
                    {
                        UtilitiesText.Toast(NotificationType.Warning, UtilitiesText.T("reallife_util_toast_debit_failed", "service", UtilitiesText.Service(a.Service), "place", NameOf(a),
                            "amount", UtilitiesText.Money(a.Owed)), 10f);
                    }
                }
                switch (UtilitiesRules.Due(a, today))
                {
                    case DueStep.ChargeLate:
                        int fee = UtilitiesCatalogue.LateFee;
                        a.Owed = (int)Math.Min((long)a.Owed + fee, int.MaxValue / 4);
                        a.LateCharged = true;
                        UtilitiesText.Toast(NotificationType.Warning, UtilitiesText.T("reallife_util_toast_late", "service", UtilitiesText.Service(a.Service), "place", NameOf(a),
                            "fee", UtilitiesText.Money(fee), "day", UtilitiesText.Day(a.DueDay + UtilitiesCatalogue.CutAfterDays)), 12f);
                        break;
                    case DueStep.Cut:
                        UtilitiesRules.Cut(a, today, true);
                        UtilitiesText.Toast(NotificationType.Error, UtilitiesText.T("reallife_util_toast_cut", "service", UtilitiesText.Service(a.Service), "place", NameOf(a),
                            "office", OfficeName()), 14f);
                        break;
                }
            }
        }

        /// <summary>What is owed on an account paid now; false without the cash (the game says so unless quiet).</summary>
        public bool Pay(Account a, int today, bool quiet = false)
        {
            if (a == null || a.Owed <= 0)
            {
                return true;
            }
            PlaceInfo? place = PlaceOf(a);
            string key = a.Service == Service.Power ? "reallife_transaction_util_power" : a.Service == Service.Water ? "reallife_transaction_util_water" : "reallife_transaction_util_net_bill";
            if (!UtilitiesGame.Pay(a.Owed, key, place?.Reg, deductible: true))
            {
                return false;
            }
            int paid = a.Owed;
            int back = UtilitiesRules.Paid(a, today);
            if (back > 0)
            {
                UtilitiesGame.Give(back, "reallife_transaction_util_deposit_back", place?.Reg);
            }
            if (!quiet)
            {
                UtilitiesText.Toast(NotificationType.Success, UtilitiesText.T("reallife_util_toast_paid", "service", UtilitiesText.Service(a.Service), "place", NameOf(a),
                    "amount", UtilitiesText.Money(paid)), 8f);
            }
            _view?.Changed();
            return true;
        }

        /// <summary>An application's cost today: the deposit by credit grade, and the reconnection fee after a cut for debt.</summary>
        public int ApplicationCost(Account a, out int deposit, out int fee)
        {
            deposit = fee = 0;
            if (a == null)
            {
                return 0;
            }
            PlaceInfo? place = PlaceOf(a);
            if (a.Owned && place.HasValue)
            {
                int season = CalendarRules.Season(UtilitiesGame.Today, GameCalendar.DaysPerYear);
                long use = UtilitiesRules.Use(a.Service, place.Value.Kind, place.Value.Sqm, UtilitiesGame.People(place.Value), true, season);
                int month = UtilitiesRules.Month(UtilitiesRules.DayCents(a.Service, use, Prices.Level, false), GameCalendar.MonthDays);
                deposit = UtilitiesRules.Deposit(UtilitiesBridges.GradeNow, month);
            }
            fee = a.CutForDebt ? UtilitiesCatalogue.ReconnectFee : 0;
            return deposit + fee;
        }

        /// <summary>An account applied for in person: paid now, it connects the next morning. False with a reason key if it cannot be.</summary>
        public bool Apply(Account a, out string why)
        {
            why = null;
            int today = UtilitiesGame.Today;
            if (a == null || a.Owed > 0)
            {
                why = "reallife_util_desk_owed_first";
                return false;
            }
            int cost = ApplicationCost(a, out int deposit, out int fee);
            PlaceInfo? place = PlaceOf(a);
            if (cost > 0 && !UtilitiesGame.Pay(cost, fee > 0 && deposit == 0 ? "reallife_transaction_util_reconnect" : "reallife_transaction_util_deposit", place?.Reg, deductible: deposit == 0))
            {
                why = "reallife_util_desk_no_cash";
                return false;
            }
            bool reconnect = a.CutForDebt;
            if (!UtilitiesRules.Apply(a, today))
            {
                why = "reallife_util_desk_not_now";
                return false;
            }
            a.Deposit += deposit;
            Logger?.Info("Utilities: " + a.Service + " applied for at " + a.Place + (reconnect ? " (reconnection)" : string.Empty) + ": deposit " + deposit + ", fee " + fee
                         + "; connects on day " + a.ConnectDay + ".");
            _view?.Changed();
            return true;
        }
    }
}
