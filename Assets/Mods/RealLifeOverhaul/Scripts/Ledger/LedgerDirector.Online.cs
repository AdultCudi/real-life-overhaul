using System;
using System.Collections.Generic;
using System.Text;
using UI.Notification;

namespace RealLifeOverhaul.Ledger
{
    /// <summary>The director's Newsstand part: the online subscriptions and their editions. Nothing here runs per frame.</summary>
    public sealed partial class LedgerDirector
    {
        private readonly OnlineMorning[] _results = new OnlineMorning[NewsstandCatalogue.Subscriptions];
        private readonly StringBuilder _names = new StringBuilder(128);
        private NewsstandState _online;
        private NewsstandApp _stand;
        private Func<int, bool> _payOnline;
        private int _paidNow;

        /// <summary>True while Newsstand runs: the paper runs and its online titles are switched on.</summary>
        public bool OnlineRunning => Running && Settings.LedgerOnline && _online != null;

        public NewsstandState Online => OnlineRunning ? _online : null;

        public NewsstandApp Stand => OnlineRunning ? _stand : null;

        public int OnlinePrice(int sub)
        {
            return NewsstandRules.WeekPrice(sub, Prices.Level);
        }

        /// <summary>Today, in the save's days; 0 without a save.</summary>
        public int Today => Save != null ? Save.Day : 0;

        public int Hour => Save != null ? Save.Hour : 0;

        /// <summary>The latest edition (today's once it is out), or null.</summary>
        public LedgerEdition Latest => Running ? _state.Edition : null;

        private void OnlineStart(GameInstance save)
        {
            _online = NewsstandStore.Load(save.modData);
            _stand = new NewsstandApp(this);
            if (_payOnline == null)
            {
                _payOnline = PayOnline;
            }
            int running = NewsstandRules.Running(_online);
            Logger?.Info("Newsstand: ready; " + (running == 0 ? "no online subscription" : running + " online subscription(s), " + OnlineWeekly() + " a week") + "; "
                         + _online.Earlier.Count + " earlier edition(s) kept.");
        }

        private void OnlineSave(Dictionary<string, string> data)
        {
            if (_online != null)
            {
                NewsstandStore.Save(data, _online);
            }
        }

        private void OnlineStop()
        {
            _stand?.Remove();
            _stand = null;
            _online = null;
        }

        private string OnlineWeekly()
        {
            return _online != null ? NewsstandRules.Weekly(_online, Prices.Level).ToString() : "0";
        }

        private void OnlineSlow(int day, int hour, bool skipping)
        {
            if (_online == null || _stand == null)
            {
                return;
            }
            if (!Settings.LedgerOnline)
            {
                _stand.Remove();
                return;
            }
            _stand.Install();
            if (!skipping)
            {
                OnlineRound(day, hour);
            }
        }

        private void KeepEarlier(LedgerEdition old, int day)
        {
            if (_online != null && old != null && old.Day != day)
            {
                NewsstandRules.Keep(_online, old, day);
            }
        }

        private void OnlineRound(int day, int hour)
        {
            if (_state.Edition == null || _state.Edition.Day != day || hour < LedgerCatalogue.DeliveryHour || !NewsstandRules.Due(_online, day))
            {
                return;
            }
            NetState net = Net(out string home);
            _paidNow = 0;
            int came = NewsstandRules.Round(_online, day, NewsstandRules.Works(net), _payOnline, _results);
            bool waiting = false;
            for (int i = 0; i < _results.Length; i++)
            {
                switch (_results[i])
                {
                    case OnlineMorning.Waiting:
                        waiting = true;
                        break;
                    case OnlineMorning.Billed:
                        Logger?.Info("Newsstand: " + NewsstandCatalogue.NameKeys[i] + " renewed on day " + day + " (" + OnlinePrice(i) + "), paid until day " + _online.Subs[i].PaidUntil + ".");
                        break;
                    case OnlineMorning.Lapsed:
                        Notifications.Show(NotificationType.Warning, "reallife_ledger_online_note_lapsed",
                            LedgerText.Data("title", LedgerText.T(NewsstandCatalogue.NameKeys[i]), "price", LedgerText.Money(OnlinePrice(i))), 10f);
                        Logger?.Info("Newsstand: " + NewsstandCatalogue.NameKeys[i] + " could not be paid on day " + day + " (" + OnlinePrice(i) + "); the subscription has stopped.");
                        break;
                    case OnlineMorning.Ended:
                        Notifications.Show(NotificationType.Info, "reallife_ledger_online_note_ended", LedgerText.Data("title", LedgerText.T(NewsstandCatalogue.NameKeys[i])), 8f);
                        Logger?.Info("Newsstand: the cancelled subscription to " + NewsstandCatalogue.NameKeys[i] + " has ended on day " + day + ".");
                        break;
                }
            }
            if (waiting && _online.WaitingDay < day)
            {
                _online.WaitingDay = day;
                Notifications.Show(NotificationType.Warning, "reallife_ledger_online_note_waiting", LedgerText.Data("why", NetWords(net, home)), 10f);
                Logger?.Info("Newsstand: day " + day + ": no working connection at home (" + net + "); today's editions wait for one, and nothing is charged meanwhile.");
            }
            if (came > 0)
            {
                Told(day);
                _stand?.Changed();
                Logger?.Info("Newsstand: day " + day + ": " + came + " edition(s) came to the phone" + (_paidNow > 0 ? ", " + _paidNow + " paid for renewals" : string.Empty) + ".");
            }
        }

        // The morning's one notification: which titles are in, and what was paid for renewals this morning.
        private void Told(int day)
        {
            if (_online.NotedDay >= day)
            {
                return;
            }
            _online.NotedDay = day;
            if (!Settings.LedgerOnlineNotes)
            {
                return;
            }
            _names.Length = 0;
            int count = 0;
            for (int t = 0; t < NewsstandCatalogue.Titles; t++)
            {
                if (NewsstandRules.Has(_online, t, day))
                {
                    _names.Append(count++ > 0 ? ", " : string.Empty).Append(LedgerText.T(NewsstandCatalogue.NameKeys[t]));
                }
            }
            if (count == 0)
            {
                return;
            }
            string key = _paidNow > 0 ? (count == 1 ? "reallife_ledger_online_note_in_one_paid" : "reallife_ledger_online_note_in_paid")
                : count == 1 ? "reallife_ledger_online_note_in_one" : "reallife_ledger_online_note_in";
            Notifications.Show(NotificationType.Info, key, LedgerText.Data("count", count.ToString(), "titles", _names.ToString(), "paid", LedgerText.Money(_paidNow)), 8f);
        }

        private bool PayOnline(int sub)
        {
            int price = OnlinePrice(sub);
            if (!Charge(price, NewsstandCatalogue.TransactionKeys[sub]))
            {
                return false;
            }
            _paidNow += price;
            return true;
        }

        /// <summary>How the internet stands at the best of the player's homes, and that home's name (null for none).</summary>
        public NetState Net(out string home)
        {
            home = null;
            NetState net = NetState.NotKept;
            try
            {
                net = LedgerBridges.Connection != null ? LedgerBridges.Connection(out home) : NetState.NotKept;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            if (net == NetState.NotKept)
            {
                home = HomeName;
                return Home() != null ? NetState.Online : NetState.NoHome;
            }
            return net;
        }

        public static string NetWords(NetState net, string home)
        {
            switch (net)
            {
                case NetState.Online: return LedgerText.Fill("reallife_ledger_online_net_online", "home", home ?? string.Empty);
                case NetState.Outage: return LedgerText.Fill("reallife_ledger_online_net_outage", "home", home ?? string.Empty);
                case NetState.Installing: return LedgerText.Fill("reallife_ledger_online_net_installing", "home", home ?? string.Empty);
                case NetState.Cut: return LedgerText.Fill("reallife_ledger_online_net_cut", "home", home ?? string.Empty);
                case NetState.NoHome: return LedgerText.T("reallife_ledger_online_net_no_home");
                default: return LedgerText.T("reallife_ledger_online_net_none");
            }
        }

        /// <summary>Opens the Utilities app, where a connection is bought; false if it is not on the phone.</summary>
        public bool OpenUtilities()
        {
            return LedgerBridges.OpenUtilities != null && LedgerBridges.OpenUtilities();
        }

        /// <summary>A subscription from the app, its first week paid now; a cancelled one is taken up again free (<paramref name="resumed"/>).</summary>
        public OnlineRefusal SubscribeOnline(int sub, out bool resumed)
        {
            resumed = false;
            GameInstance save = Save;
            if (!OnlineRunning || save == null || sub < 0 || sub >= NewsstandCatalogue.Subscriptions)
            {
                return OnlineRefusal.Off;
            }
            OnlineRefusal refusal = NewsstandRules.Subscribe(_online, sub, Net(out _), out bool payNow);
            if (refusal != OnlineRefusal.None)
            {
                return refusal;
            }
            if (!payNow)
            {
                resumed = true;
                Logger?.Info("Newsstand: the cancelled subscription to " + NewsstandCatalogue.NameKeys[sub] + " is taken up again on day " + save.Day
                             + "; paid until day " + _online.Subs[sub].PaidUntil + ".");
                return OnlineRefusal.None;
            }
            int price = OnlinePrice(sub);
            if (!Charge(price, NewsstandCatalogue.TransactionKeys[sub]))
            {
                return OnlineRefusal.NoMoney;
            }
            int cancelled = NewsstandRules.Start(_online, sub, save.Day);
            if (_state.Edition != null && _state.Edition.Day == save.Day)
            {
                _online.NotedDay = Math.Max(_online.NotedDay, save.Day);   // taken out in the app: the app says the edition is in
            }
            Logger?.Info("Newsstand: subscribed to " + NewsstandCatalogue.NameKeys[sub] + " on day " + save.Day + " (" + price + " a week)"
                         + (cancelled > 0 ? "; " + cancelled + " title(s) of its own cancelled, running to their last day paid for" : string.Empty) + ".");
            OnlineRound(save.Day, save.Hour);
            return OnlineRefusal.None;
        }

        /// <summary>A subscription cancelled: its editions come until the last day paid for. False if there is none to cancel.</summary>
        public bool CancelOnline(int sub)
        {
            if (!OnlineRunning || sub < 0 || sub >= NewsstandCatalogue.Subscriptions || !NewsstandRules.Cancel(_online, sub))
            {
                return false;
            }
            Logger?.Info("Newsstand: the subscription to " + NewsstandCatalogue.NameKeys[sub] + " is cancelled; the last edition is on day " + _online.Subs[sub].PaidUntil + ".");
            return true;
        }

        /// <summary>The edition of a day: today's (the paper's own) or one kept; null if there is none.</summary>
        public LedgerEdition OnlineEdition(int day)
        {
            return OnlineRunning ? NewsstandRules.Find(_online, _state.Edition, day) : null;
        }

        /// <summary>The City Ledger online opened as the printed page, from the phone. False if the page is not in the menu.</summary>
        public bool ReadPrinted(LedgerEdition edition)
        {
            return Running && _page != null && edition != null && _page.Show(edition, LedgerPage.FromPhone);
        }
    }
}
