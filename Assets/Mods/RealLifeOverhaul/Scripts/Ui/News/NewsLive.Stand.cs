using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Ledger;
using RealLifeOverhaul.Outlook;
using UI.Notification;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The Newsstand on the kit: a row for each paper with its mark, what it covers, today's lead and its price a week, and the one
    /// action that applies. The money and the state move through the game's own subscribe and cancel, the old page's path.
    /// </summary>
    public sealed partial class NewsLive
    {
        private readonly NewsReadingPage _reading = new NewsReadingPage();
        private INewsReadDesk _desk;

        // A press on the reading page: the paper or the day picked, the printed page, the way to the Titles tab.
        private bool PressRead(NewsModel model, int command, int arg)
        {
            LedgerDirector director = LedgerDirector.Instance;
            _desk = _desk ?? new ReadDesk(this);
            if (director == null || director.Online == null || !_reading.Press(model, command, arg, director.Online, _desk))
            {
                return false;
            }
            _local++;
            return true;
        }

        private void FillStand(NewsModel model)
        {
            model.Icon = UiIcon.Newspaper;
            model.Title = OutlookText.T("reallife_ledger_online_app");
            model.Across = true;
            model.Nav = new[]
            {
                Item(UiIcon.Newspaper, "reallife_ledger_online_tab_titles"),
                Item(UiIcon.Books, "reallife_ledger_online_tab_reading")
            };
            LedgerDirector director = LedgerDirector.Instance;
            if (director == null || !director.OnlineRunning)
            {
                model.Shape = NewsShape.Notice;
                model.EmptyTitle = OutlookText.T("reallife_news_stand_off");
                model.EmptyBody = OutlookText.T("reallife_news_stand_off_body");
                return;
            }
            NetState net = director.Net(out string home);
            if (!NewsstandRules.Works(net))
            {
                model.Shape = NewsShape.Notice;
                model.EmptyTitle = OutlookText.T(net == NetState.Outage ? "reallife_news_stand_out" : "reallife_news_stand_off");
                model.EmptyBody = OutlookText.T(net == NetState.Outage ? "reallife_news_stand_out_body" : "reallife_news_stand_off_body");
                model.Note = model.EmptyBody;
                return;
            }
            if (model.Tab == 1)
            {
                _desk = _desk ?? new ReadDesk(this);
                _reading.Fill(model, director.Online, _desk);   // only what you hold, the paper and the day you picked; a paper you do not hold shows how to take it out
                return;
            }
            model.Shape = NewsShape.Stand;
            NewsstandState state = director.Online;
            int level = RealLifeOverhaul.Prices.Level;
            int weekly = state != null ? NewsstandRules.Weekly(state, level) : 0;
            model.StandLine = LedgerText.T("reallife_ledger_online_box_net") + ": " + LedgerPage.Plain(LedgerDirector.NetWords(net, home));
            model.Note = OutlookText.T("reallife_news_stand_sum", "count", NewsstandRules.Running(state).ToString(Inv), "money", LedgerText.Money(weekly));
            model.Titles = StandTitles(director, state, level);
        }

        // A row for each of the four papers, then the Pass.
        private static NewsTitle[] StandTitles(LedgerDirector director, NewsstandState state, int level)
        {
            LedgerEdition latest = director.Latest;
            int today = director.Today;
            bool outNow = latest != null && latest.Day == today;
            OnlineSub pass = state != null ? state.Subs[NewsstandCatalogue.Pass] : null;
            bool passOn = pass != null && pass.Subscribed && !pass.Cancelled;
            var titles = new NewsTitle[NewsstandCatalogue.Subscriptions];
            int sum = 0;
            for (int i = 0; i < NewsstandCatalogue.Titles; i++)
            {
                sum += NewsstandRules.WeekPrice(i, level);
                OnlineSub x = state != null ? state.Subs[i] : null;
                bool taken = x != null && x.Subscribed && !x.Cancelled;
                bool ending = x != null && x.Subscribed && x.Cancelled;
                bool inPass = !taken && !ending && passOn;
                LedgerPiece lead = latest != null ? NewsstandRules.Lead(latest, i) : null;
                var row = new NewsTitle
                {
                    Mark = UiDraw.Initials(LedgerText.T(NewsstandCatalogue.NameKeys[i])),
                    Tint = UiTokens.Chart(i),
                    Name = LedgerText.T(NewsstandCatalogue.NameKeys[i]),
                    Tagline = LedgerText.T(NewsstandCatalogue.TaglineKeys[i]),
                    Covers = LedgerText.T(NewsstandCatalogue.AboutKeys[i]),
                    LeadLabel = OutlookText.T("reallife_news_stand_lead"),
                    Lead = lead != null ? PieceHead(lead) : null,
                    EditionLabel = LedgerText.T("reallife_ledger_online_box_today"),
                    Price = LedgerText.Money(director.OnlinePrice(i)),
                    PerWeek = OutlookText.T("reallife_news_stand_week")
                };
                bool covered = state != null && (NewsstandRules.Covered(state, i));
                if (state != null && NewsstandRules.Has(state, i, today))
                {
                    row.Edition = LedgerText.T("reallife_ledger_online_today_in");
                    row.EditionTone = UiRole.OkText;
                }
                else if (covered)
                {
                    row.Edition = outNow ? LedgerText.T("reallife_ledger_online_today_waiting")
                        : LedgerText.Fill("reallife_ledger_online_today_at", "hour", LedgerText.Hour(LedgerCatalogue.DeliveryHour));
                    row.EditionTone = outNow ? UiRole.DangerText : UiRole.Text2;
                }
                else
                {
                    row.Edition = OutlookText.T("reallife_news_stand_to_read");
                }
                if (taken)
                {
                    row.Chip = OutlookText.T("reallife_news_stand_chip");
                    row.Renews = LedgerText.Fill("reallife_ledger_online_state_renews", "day", (x.PaidUntil + 1).ToString(Inv));
                    row.Action = OutlookText.T("reallife_news_stand_stop");
                    row.ActionKind = UiButtonKind.Secondary;
                }
                else if (ending)
                {
                    row.Chip = OutlookText.T("reallife_news_stand_chip");
                    row.Renews = LedgerText.Fill("reallife_ledger_online_state_ends", "day", x.PaidUntil.ToString(Inv));
                    row.Action = LedgerText.T("reallife_ledger_online_resume");
                    row.ActionKind = UiButtonKind.Secondary;
                }
                else if (inPass)
                {
                    row.Chip = LedgerText.T("reallife_ledger_online_state_in_pass");
                }
                else
                {
                    row.Action = OutlookText.T("reallife_news_stand_sub");
                    row.ActionKind = UiButtonKind.Secondary;   // the Pass alone is the page's main action
                }
                titles[i] = row;
            }
            titles[NewsstandCatalogue.Pass] = PassRow(director, pass, level, sum);
            return titles;
        }

        // The Pass: all four titles for a week's price, the saving against taking each, and the one action.
        private static NewsTitle PassRow(LedgerDirector director, OnlineSub pass, int level, int sum)
        {
            int price = NewsstandRules.WeekPrice(NewsstandCatalogue.Pass, level);
            bool taken = pass != null && pass.Subscribed && !pass.Cancelled;
            bool ending = pass != null && pass.Subscribed && pass.Cancelled;
            var row = new NewsTitle
            {
                Pass = true,
                Icon = UiIcon.Newspaper,
                Name = LedgerText.T(NewsstandCatalogue.NameKeys[NewsstandCatalogue.Pass]),
                Covers = LedgerText.T(NewsstandCatalogue.AboutKeys[NewsstandCatalogue.Pass]),
                Tagline = OutlookText.T("reallife_news_stand_save", "money", LedgerText.Money(NewsRules.PassSaving(sum, price))),
                Price = LedgerText.Money(director.OnlinePrice(NewsstandCatalogue.Pass)),
                PerWeek = OutlookText.T("reallife_news_stand_week")
            };
            if (taken || ending)
            {
                row.Chip = OutlookText.T("reallife_news_stand_chip");
                row.Renews = ending ? LedgerText.Fill("reallife_ledger_online_state_ends", "day", pass.PaidUntil.ToString(Inv))
                    : LedgerText.Fill("reallife_ledger_online_state_renews", "day", (pass.PaidUntil + 1).ToString(Inv));
            }
            row.Action = taken ? OutlookText.T("reallife_news_stand_stop") : ending ? LedgerText.T("reallife_ledger_online_resume") : OutlookText.T("reallife_news_stand_sub");
            row.ActionKind = taken || ending ? UiButtonKind.Secondary : UiButtonKind.Primary;
            return row;
        }

        // A press on a paper's one action: stop it when it is taken, otherwise take it up. The notices and the money are the old page's.
        private bool TakeTitle(int sub)
        {
            LedgerDirector director = LedgerDirector.Instance;
            NewsstandState state = director != null ? director.Online : null;
            if (state == null || sub < 0 || sub >= NewsstandCatalogue.Subscriptions)
            {
                return false;
            }
            OnlineSub x = state.Subs[sub];
            NewsstandApp stand = director.Stand;
            if (x.Subscribed && !x.Cancelled)
            {
                if (stand != null)
                {
                    stand.Stop(sub);
                }
                else
                {
                    director.CancelOnline(sub);
                }
            }
            else if (stand != null)
            {
                stand.Take(sub);
            }
            else
            {
                director.SubscribeOnline(sub, out _);
            }
            _local++;
            return true;
        }

        // The game's side of the reading page: the editions the phone keeps, what the Ledger prints for a title, the printed page, the words.
        private sealed class ReadDesk : INewsReadDesk
        {
            private readonly NewsLive _owner;

            public ReadDesk(NewsLive owner) { _owner = owner; }

            private static LedgerDirector Director => LedgerDirector.Instance;

            public int Today => Director != null ? Director.Today : 0;

            public int Stamp => _owner.Generation;

            public LedgerEdition Edition(int day) => Director != null ? Director.OnlineEdition(day) : null;

            public UiReaderModel Reader(LedgerEdition edition, int title) => ReaderOf(edition, title);

            public bool ReadPrinted(LedgerEdition edition) => Director != null && Director.ReadPrinted(edition);

            public void Tell(string key) => Notifications.Show(NotificationType.Info, key, (Dictionary<string, string>)null, 6f);

            public string Fill(string key, params string[] pairs) => pairs.Length == 0 ? LedgerText.T(key) : LedgerText.Fill(key, pairs);

            public string TitleName(int title) => LedgerText.T(NewsstandCatalogue.NameKeys[title]);

            public string DayWords(int day)
            {
                return day == Today ? LedgerText.Fill("reallife_ledger_online_day_today", "day", day.ToString(Inv))
                    : LedgerText.Fill("reallife_ledger_online_day", "weekday", LedgerPage.Weekday(day), "day", day.ToString(Inv));
            }

            public string WeekPrice(int title) => LedgerText.Money(Director != null ? Director.OnlinePrice(title) : 0);

            public string DeliveryHour() => LedgerText.Hour(LedgerCatalogue.DeliveryHour);
        }
    }
}
