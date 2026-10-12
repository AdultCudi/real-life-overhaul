using System;
using System.Collections.Generic;
using RealLifeOverhaul.GameUi;
using UI.Notification;

namespace RealLifeOverhaul.Utilities
{
    /// <summary>
    /// The utilities office's desk, in the window the game lists its subway stations in (TravelList, through the
    /// feature's DeskList): the game's own form for "pick one of these". An entry for each thing the desk can do today:
    ///
    ///   pay        a bill owed at a place: what it comes to and when it falls (or fell) due
    ///   apply      a bought place in its temporary supply, or a place whose supply is cut: an account applied for, with
    ///              the deposit (by the credit rating) and, after a cut for an unpaid bill, the reconnection fee; it
    ///              connects the next morning
    ///   waiting    an account applied for, and the morning it connects
    ///
    /// and a note that bills and auto debit are in the Utilities app. Choosing an entry does it at once and writes the
    /// list again; the answer is a notification. Built when the desk is spoken to; nothing runs per frame.
    /// </summary>
    public sealed class UtilitiesDesk
    {
        private const int Pay = 0, Apply = 1;   // an entry's id: its account's place in the list, twice, and what it does
        private const string ButtonPay = "reallife_util_desk_button_pay";
        private const string ButtonApply = "reallife_util_desk_button_apply";

        private readonly UtilitiesDirector _director;
        private readonly Action<int> _onPick;
        private readonly Action _onClosed;
        private readonly List<Account> _accounts = new List<Account>(16);

        public UtilitiesDesk(UtilitiesDirector director)
        {
            _director = director;
            _onPick = Picked;
            _onClosed = Closed;
        }

        /// <summary>Opens the desk. False if the game's window could not be made.</summary>
        public bool Open()
        {
            TravelList list = _director.Window.Take(this);
            if (list == null)
            {
                return false;
            }
            Fill(list);
            list.Open(_onPick, _onClosed);
            return true;
        }

        private void Fill(TravelList list)
        {
            UtilitiesState state = _director.State;
            _accounts.Clear();
            list.Begin(UtilitiesText.T("reallife_util_desk_title"), UtilitiesText.T("reallife_util_desk_greeting", "office", _director.OfficeName()));
            if (state == null)
            {
                return;
            }
            int today = UtilitiesGame.Today, entries = 0;
            for (int i = 0; i < state.Accounts.Count; i++)
            {
                Account a = state.Accounts[i];
                if (a.Service == Service.Internet)
                {
                    continue;   // (bought and paid online, from its own provider: the Utilities app)
                }
                string service = UtilitiesText.Service(a.Service), place = _director.NameOf(a);
                int index = _accounts.Count;
                _accounts.Add(a);
                if (a.Owed > 0)
                {
                    string when = a.DueDay > 0 && today > a.DueDay ? "reallife_util_desk_pay_overdue" : "reallife_util_desk_pay_due";
                    list.Add(index * 2 + Pay, UtilitiesText.T("reallife_util_desk_pay", "service", service, "place", place),
                        UtilitiesText.T(when, "amount", UtilitiesText.Money(a.Owed), "day", UtilitiesText.Day(a.DueDay)), ButtonPay, true);
                    entries++;
                }
                if ((a.State == SupplyState.Temporary || a.State == SupplyState.Cut) && a.State != SupplyState.Closed)
                {
                    int cost = _director.ApplicationCost(a, out int deposit, out int fee);
                    string detail = a.Owed > 0
                        ? UtilitiesText.T("reallife_util_desk_apply_owed")
                        : UtilitiesText.T(cost > 0 ? "reallife_util_desk_apply_cost" : "reallife_util_desk_apply_free",
                            "deposit", UtilitiesText.Money(deposit), "fee", UtilitiesText.Money(fee), "amount", UtilitiesText.Money(cost));
                    list.Add(index * 2 + Apply, UtilitiesText.T(a.CutForDebt ? "reallife_util_desk_reconnect" : "reallife_util_desk_apply", "service", service, "place", place),
                        detail, ButtonApply, a.Owed <= 0);
                    entries++;
                }
                else if (a.State == SupplyState.Pending)
                {
                    list.AddNote(UtilitiesText.T("reallife_util_desk_waiting", "service", service, "place", place),
                        UtilitiesText.T("reallife_util_desk_waiting_detail", "day", UtilitiesText.Day(a.ConnectDay)), 120f);
                    entries++;
                }
            }
            if (entries == 0)
            {
                list.AddNote(UtilitiesText.T("reallife_util_desk_nothing"), UtilitiesText.T("reallife_util_desk_nothing_detail"), 120f);
            }
            list.AddNote(UtilitiesText.T("reallife_util_desk_app_note"), UtilitiesText.T("reallife_util_desk_app_detail"), 120f);
        }

        private void Picked(int id)
        {
            try
            {
                TravelList list = _director.Window.Take(this);
                int index = id / 2, act = id % 2;
                if (list == null || !list.IsOpen || index < 0 || index >= _accounts.Count)
                {
                    return;
                }
                Account a = _accounts[index];
                if (act == Pay)
                {
                    if (!_director.Pay(a, UtilitiesGame.Today))
                    {
                        UtilitiesText.Toast(NotificationType.Warning, UtilitiesText.T("reallife_util_desk_no_cash"), 8f);
                    }
                }
                else if (_director.Apply(a, out string why))
                {
                    UtilitiesText.Toast(NotificationType.Success, UtilitiesText.T("reallife_util_toast_applied", "service", UtilitiesText.Service(a.Service), "place", _director.NameOf(a),
                        "day", UtilitiesText.Day(a.ConnectDay)), 10f);
                }
                else
                {
                    UtilitiesText.Toast(NotificationType.Warning, UtilitiesText.T(why ?? "reallife_util_desk_not_now"), 8f);
                }
                Fill(list);
                list.Open(_onPick, _onClosed);
            }
            catch (Exception e)
            {
                UtilitiesDirector.Logger?.Error(e);
                _director.Window.Close();
            }
        }

        private void Closed()
        {
            _accounts.Clear();
        }
    }
}
