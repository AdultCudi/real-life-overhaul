using RealLifeOverhaul.GameUi;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Ledger
{
    /// <summary>Newsstand's app: the titles tab and its box.</summary>
    public sealed partial class NewsstandApp
    {
        private void WriteTitles(InfoBox box, NewsstandState s)
        {
            Rows();
            int today = _director.Today, level = Prices.Level;
            bool outNow = Out(today);
            for (int i = 0; i < NewsstandCatalogue.Subscriptions; i++)
            {
                OnlineSub x = s.Subs[i];
                TableRow row = Row("sub|" + i);
                row.Pick = true;
                row.Index = i;
                bool title = i < NewsstandCatalogue.Titles;
                bool inPass = title && !x.Subscribed && s.Subs[NewsstandCatalogue.Pass].Subscribed;
                row.Set(0, LedgerText.T(NewsstandCatalogue.NameKeys[i]));
                row.Set(1, LedgerText.T(NewsstandCatalogue.AboutKeys[i]));
                row.Set(2, LedgerText.Money(NewsstandRules.WeekPrice(i, level)));
                string state = x.Subscribed
                    ? (x.Cancelled ? LedgerText.Fill("reallife_ledger_online_state_ends", "day", x.PaidUntil.ToString())
                        : LedgerText.Fill("reallife_ledger_online_state_renews", "day", (x.PaidUntil + 1).ToString()))
                    : inPass ? LedgerText.T("reallife_ledger_online_state_in_pass") : "-";
                row.Set(3, state, x.Subscribed ? (x.Cancelled ? TableRow.Yellow : TableRow.Green) : inPass ? TableRow.Green : TableRow.Plain);
                if (!title)
                {
                    row.Set(4, "-");
                }
                else if (NewsstandRules.Has(s, i, today))
                {
                    row.Set(4, LedgerText.T("reallife_ledger_online_today_in"), TableRow.Green);
                }
                else if (NewsstandRules.Covered(s, i))
                {
                    row.Set(4, outNow ? LedgerText.T("reallife_ledger_online_today_waiting")
                            : LedgerText.Fill("reallife_ledger_online_today_at", "hour", LedgerText.Hour(LedgerCatalogue.DeliveryHour)),
                        outNow ? TableRow.Red : TableRow.Plain);
                }
                else
                {
                    row.Set(4, "-");
                }
            }
            UiClone.Words(_shell.Header, LedgerText.Fill("reallife_ledger_online_headline", "count", NewsstandRules.Running(s).ToString(), "price", LedgerText.Money(NewsstandRules.Weekly(s, level))));
            _table.Show(Titles, _rows, _pick >= 0 ? "sub|" + _pick : null);
            if (_pick >= 0 && _pick < NewsstandCatalogue.Subscriptions)
            {
                WriteSub(box, s, _pick, today, outNow);
            }
            else
            {
                WriteFront(box, s, today);
            }
        }

        // Nothing picked: what Newsstand is, the connection at home, and the way to read what came.
        private void WriteFront(InfoBox box, NewsstandState s, int today)
        {
            NetState net = _director.Net(out string home);
            box.Title(LedgerText.T("reallife_ledger_online_app"));
            if (!NewsstandRules.Works(net))
            {
                box.Line(LedgerText.T(net == NetState.NoHome ? "reallife_ledger_online_box_need_home" : "reallife_ledger_online_box_need_net"), CellTone.Yellow);
            }
            box.Row("reallife_ledger_online_box_net", LedgerPage.Plain(LedgerDirector.NetWords(net, home)));
            box.Note(LedgerText.T("reallife_ledger_online_box_pick"));
            Transform buttons = box.ButtonRow();
            int first = FirstReadable(s, today);
            if (first >= 0)
            {
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_ledger_online_read", () => UiClone.Safe(() => Read(first)));
            }
            if (!NewsstandRules.Works(net) && net != NetState.NoHome)
            {
                UtilitiesButton(buttons);
            }
            box.Note(LedgerText.Fill("reallife_ledger_online_box_intro", "hour", LedgerText.Hour(LedgerCatalogue.DeliveryHour)));
            Meta.Guide.Point(box, GuideTitle);
        }

        // A title or the pass picked: its price, its subscription, today's edition, and what can be done.
        private void WriteSub(InfoBox box, NewsstandState s, int sub, int today, bool outNow)
        {
            OnlineSub x = s.Subs[sub];
            OnlineSub pass = s.Subs[NewsstandCatalogue.Pass];
            bool title = sub < NewsstandCatalogue.Titles;
            int price = _director.OnlinePrice(sub);
            box.Title(LedgerText.T(NewsstandCatalogue.NameKeys[sub]));
            string state = x.Subscribed
                ? (x.Cancelled ? LedgerText.Fill("reallife_ledger_online_box_cancelled", "day", x.PaidUntil.ToString())
                    : LedgerText.Fill("reallife_ledger_online_box_running", "paid", x.PaidUntil.ToString(), "next", (x.PaidUntil + 1).ToString(), "price", LedgerText.Money(price)))
                : title && pass.Subscribed ? LedgerText.T("reallife_ledger_online_box_by_pass") : LedgerText.T("reallife_ledger_online_box_none");
            box.Row("reallife_ledger_online_box_sub", LedgerPage.Plain(state));
            if (title)
            {
                string edition = NewsstandRules.Has(s, sub, today) ? LedgerText.T("reallife_ledger_online_today_in")
                    : !NewsstandRules.Covered(s, sub) ? "-"
                    : outNow ? LedgerText.T("reallife_ledger_online_today_waiting") : LedgerText.Fill("reallife_ledger_online_today_at", "hour", LedgerText.Hour(LedgerCatalogue.DeliveryHour));
                box.Row("reallife_ledger_online_box_today", LedgerPage.Plain(edition));
                Readable(s, sub, today);
                if (_days.Count > 0)
                {
                    box.Row("reallife_ledger_online_box_editions", LedgerPage.Plain(_days.Count.ToString()));
                }
            }
            NetState net = NetState.Online;
            bool canTake = !x.Subscribed && !(title && pass.Subscribed && !pass.Cancelled);
            if (canTake)
            {
                net = _director.Net(out string home);
                if (!NewsstandRules.Works(net))
                {
                    box.Line(LedgerDirector.NetWords(net, home));
                    box.Line(LedgerText.T(net == NetState.NoHome ? "reallife_ledger_online_box_need_home" : "reallife_ledger_online_box_need_net"), CellTone.Yellow);
                }
            }
            box.Row("reallife_ledger_online_box_price", LedgerPage.Plain(LedgerText.Money(price)));
            box.Row("reallife_ledger_online_box_covers", LedgerPage.Plain(LedgerText.T(NewsstandCatalogue.AboutKeys[sub])));
            Transform buttons = box.ButtonRow();
            if (title && _days.Count > 0)
            {
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_ledger_online_read", () => UiClone.Safe(() => Read(sub)));
            }
            if (canTake && NewsstandRules.Works(net))
            {
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_ledger_online_subscribe", () => UiClone.Safe(() => Subscribe(sub)));
            }
            else if (canTake && net != NetState.NoHome)
            {
                UtilitiesButton(buttons);
            }
            if (x.Subscribed && x.Cancelled)
            {
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_ledger_online_resume", () => UiClone.Safe(() => Subscribe(sub)));
            }
            if (x.Subscribed && !x.Cancelled)
            {
                InfoBox.Button(buttons, InfoBox.Negative, "reallife_ledger_online_cancel", () => UiClone.Safe(() =>
                {
                    if (_director.CancelOnline(sub))
                    {
                        Say(NotificationType.Info, "reallife_ledger_online_note_cancelled", "title", LedgerText.T(NewsstandCatalogue.NameKeys[sub]), "day", x.PaidUntil.ToString());
                    }
                    Refresh();
                }));
            }
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_ledger_online_back", Back);
            if (!title && !x.Subscribed)
            {
                int sum = 0;
                for (int t = 0; t < NewsstandCatalogue.Titles; t++)
                {
                    sum += _director.OnlinePrice(t);
                }
                box.Note(LedgerText.Fill("reallife_ledger_online_box_pass_note", "price", LedgerText.Money(price), "sum", LedgerText.Money(sum)));
            }
            box.Line("<i>" + LedgerPage.Plain(LedgerText.T(title ? NewsstandCatalogue.TaglineKeys[sub] : NewsstandCatalogue.AboutKeys[sub])) + "</i>");
            Meta.Guide.Point(box, GuideTitle);
        }

        private void Subscribe(int sub)
        {
            string name = LedgerText.T(NewsstandCatalogue.NameKeys[sub]);
            int price = _director.OnlinePrice(sub);
            OnlineRefusal refusal = _director.SubscribeOnline(sub, out bool resumed);
            switch (refusal)
            {
                case OnlineRefusal.None:
                    NewsstandState s = _director.Online;
                    bool came = s != null && sub < NewsstandCatalogue.Titles && NewsstandRules.Has(s, sub, _director.Today);
                    Say(NotificationType.Success, resumed ? "reallife_ledger_online_note_resumed" : sub == NewsstandCatalogue.Pass ? "reallife_ledger_online_note_pass"
                        : came ? "reallife_ledger_online_note_subscribed_in" : "reallife_ledger_online_note_subscribed", "title", name, "price", LedgerText.Money(price),
                        "hour", LedgerText.Hour(LedgerCatalogue.DeliveryHour));
                    break;
                case OnlineRefusal.NoMoney:
                    Say(NotificationType.Warning, "reallife_ledger_online_note_no_money", "title", name, "price", LedgerText.Money(price));
                    break;
                case OnlineRefusal.NoHome:
                    Say(NotificationType.Warning, "reallife_ledger_online_box_need_home");
                    break;
                case OnlineRefusal.NoNet:
                    Say(NotificationType.Warning, "reallife_ledger_online_box_need_net");
                    break;
                case OnlineRefusal.InPass:
                    Say(NotificationType.Info, "reallife_ledger_online_box_by_pass");
                    break;
            }
            Refresh();
        }

        private void UtilitiesButton(Transform buttons)
        {
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_ledger_online_utilities", () => UiClone.Safe(() =>
            {
                if (!_director.OpenUtilities())
                {
                    Say(NotificationType.Info, "reallife_ledger_online_no_utilities");
                }
            }));
        }

        private bool Out(int today)
        {
            LedgerEdition latest = _director.Latest;
            return latest != null && latest.Day == today;
        }

        // The table's rows back to the pool before they are written again (not while the reading page is over it).
        private void Rows()
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                _pool.Add(_rows[i]);
            }
            _rows.Clear();
        }

        private TableRow Row(string id)
        {
            TableRow row;
            if (_pool.Count > 0)
            {
                row = _pool[_pool.Count - 1];
                _pool.RemoveAt(_pool.Count - 1);
            }
            else
            {
                row = new TableRow();
            }
            _rows.Add(row.Reset(id));
            return row;
        }
    }
}
