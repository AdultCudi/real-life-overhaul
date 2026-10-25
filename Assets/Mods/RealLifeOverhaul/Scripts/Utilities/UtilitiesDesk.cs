using System;
using System.Collections.Generic;
using RealLifeOverhaul.GameUi;
using UI.Notification;

namespace RealLifeOverhaul.Utilities
{
    /// <summary>
    /// The utilities office's desk in the game's station list (DeskList): pay a bill, apply for an account, see one waiting.
    /// Built when the desk is spoken to; nothing per frame.
    /// </summary>
    public sealed class UtilitiesDesk
    {
        private const int Pay = 0, Apply = 1;   // an entry's id: its account's index in the list, twice, and what it does
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
                    continue;   // bought and paid online from its own provider: the Utilities app
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
                AccountOffer offer = UtilitiesRules.Offer(a);
                if (offer != AccountOffer.None)
                {
                    int cost = _director.ApplicationCost(a, out int deposit, out int fee);
                    string detail = offer == AccountOffer.OwedFirst
                        ? UtilitiesText.T("reallife_util_desk_apply_owed")
                        : UtilitiesText.T(cost > 0 ? "reallife_util_desk_apply_cost" : "reallife_util_desk_apply_free",
                            "deposit", UtilitiesText.Money(deposit), "fee", UtilitiesText.Money(fee), "amount", UtilitiesText.Money(cost));
                    list.Add(index * 2 + Apply, UtilitiesText.T(a.CutForDebt ? "reallife_util_desk_reconnect" : "reallife_util_desk_apply", "service", service, "place", place),
                        detail, ButtonApply, offer != AccountOffer.OwedFirst);
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
