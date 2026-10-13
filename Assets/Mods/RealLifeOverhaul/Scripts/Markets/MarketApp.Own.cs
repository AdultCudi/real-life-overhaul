using System;
using System.Collections.Generic;
using System.Globalization;
using Localizor;
using RealLifeOverhaul.GameUi;
using UI;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// The player's own company in the Stocks app (Markets/Listing): its page, in place of a tab's table, from the Market
    /// and Portfolio tabs' box ("Your company"), and the ticket on its company page.
    ///
    ///   on the way    the box says where the company stands and has the next step's button (advisers, the audit, the
    ///                 prospectus with its name, the share offered and the least price taken, withdrawing); the table lists
    ///                 every requirement against the company's own figures, then its books quarter by quarter
    ///   listed        the box has its price, the founder's stake, the reserve, pay and the board, what is coming (results,
    ///                 the closed time, the lock-up, the yearly meeting, an investor's demand, a bid) and the board's own
    ///                 buttons (a dividend proposed, a buy-back, new shares, taking it private); the table its register
    ///                 and its quarters
    ///   its page      as every company's (chart and figures), with the founder's own ticket: buy or sell at the market
    ///                 under the rules a founder deals by
    ///
    /// Written when the app opens and when the player does something in it, never per frame.
    /// </summary>
    public sealed partial class MarketApp
    {
        public const int OwnPage = -2;   // the player's own company's page (a company's page is its id, the index's IndexPage)
        private const string OwnRow = "own:";

        private int _floatBp = ListingCatalogue.FloatDefault;
        private int _walk;              // the least price taken: 0 the range's bottom, 1 its middle, 2 any price
        private string _name;           // the company's name as typed for the prospectus
        private OwnSide _ownSide;
        private long _ownQty = 1000;
        private int _ownView;           // the table: 0 what it takes or the register, 1 the books or the results

        private enum OwnSide { Buy = 0, Sell = 1 }

        private static readonly string[] OwnSideKeys = { "reallife_lst_side_buy", "reallife_lst_side_sell" };
        private static readonly string[] WalkKeys = { "reallife_lst_walk_low", "reallife_lst_walk_mid", "reallife_lst_walk_any" };
        private static readonly string[] WayViews = { "reallife_lst_view_needs", "reallife_lst_view_books" };
        private static readonly string[] ListedViews = { "reallife_lst_view_register", "reallife_lst_view_results" };

        // The table the page shows, and the choice of it at the head of the box.
        private TableSpec View(InfoBox box, bool listed)
        {
            string[] views = listed ? ListedViews : WayViews;
            box.Selector(views.Length, _ownView, i => MarketText.T(views[i]), i => UiClone.Safe(() =>
            {
                _ownView = i;
                Refresh();
            }));
            return listed ? (_ownView == 0 ? MarketTables.ListingRegister : MarketTables.ListingResults) : (_ownView == 0 ? MarketTables.ListingNeeds : MarketTables.ListingBooks);
        }

        // ---------------------------------------------------------------- the way to the page

        // A button to the player's company's page, on a tab's box.
        private void OwnButton(InfoBox box)
        {
            Stock own = _director.MyCompany;
            ListingFile f = _director.Listing;
            if (f == null || (own == null && !Settings.MarketsListing && f.Stage == ListingStage.None))
            {
                return;
            }
            Transform row = box.ButtonRow();
            InfoBox.Button(row, InfoBox.Additional, own != null ? "reallife_lst_open_listed" : "reallife_lst_open_way", () => UiClone.Safe(() =>
            {
                OpenPage(OwnPage);
                Refresh();
            }));
        }

        // ---------------------------------------------------------------- the page

        private TableSpec WriteListing(InfoBox box, MarketState s)
        {
            Stock own = _director.MyCompany;
            ListingFile f = s.Listing;
            if (own != null)
            {
                box.Title(own.Name + "  <size=70%>" + own.Ticker + "</size>");
                TableSpec listed = View(box, true);
                WriteListed(box, s, own, f);
                return listed;
            }
            box.Title(MarketText.T("reallife_lst_title"));
            TableSpec spec = View(box, false);
            if (_director.CompanyAside)
            {
                box.Line(MarketText.T("reallife_lst_aside"));
            }
            bool all = _director.Requirements(out Requirement[] r);
            if (_ownView == 0)
            {
                WriteRequirements(r, s.YearDays);
            }
            else
            {
                WriteBooks(f, s.Day, s.YearDays);
            }
            box.Line(MarketText.Fill("reallife_lst_box_stage", "stage", ListingText.Stage(f.Stage)));
            if (!Settings.MarketsListing && f.Stage == ListingStage.None)
            {
                box.Line(MarketText.T("reallife_lst_box_off"));
                BackRow(box);
                return spec;
            }
            switch (f.Stage)
            {
                case ListingStage.None:
                {
                    int met = 0;
                    for (int i = 0; i < r.Length; i++)
                    {
                        met += r[i].Met ? 1 : 0;
                    }
                    box.Line(MarketText.Fill("reallife_lst_box_met", "met", met.ToString(), "count", r.Length.ToString()));
                    box.Line(MarketText.T("reallife_lst_box_steps"));
                    bool ready = ListingBook.ReadyForAdvisers(r);
                    long fee = _director.Retainer();
                    box.Line(MarketText.Fill(ready ? "reallife_lst_box_advisers" : "reallife_lst_box_not_ready", "amount", MarketText.Money(fee)));
                    Transform row = box.ButtonRow();
                    if (ready)
                    {
                        InfoBox.Button(row, InfoBox.Positive, "reallife_lst_engage", () => UiClone.Safe(() => Step(_director.Engage())));
                    }
                    InfoBox.Button(row, InfoBox.Negative, "reallife_mkt_back", () => UiClone.Safe(ClosePage));
                    break;
                }
                case ListingStage.Advised:
                {
                    box.Line(MarketText.Fill("reallife_lst_box_advised", "day", f.AdvisedDay.ToString()));
                    if (f.AuditDay > 0 && (f.Findings & ListingBook.Qualifying) != 0)
                    {
                        box.Line(MarketText.Fill("reallife_lst_box_qualified", "found", ListingText.Findings(f.Findings)));
                    }
                    box.Line(MarketText.Fill("reallife_lst_box_audit_fee", "amount", MarketText.Money(_director.AuditFee()), "days", ListingCatalogue.AuditDays.ToString()));
                    Transform row = box.ButtonRow();
                    InfoBox.Button(row, InfoBox.Positive, "reallife_lst_audit", () => UiClone.Safe(() => Step(_director.OrderAudit())));
                    InfoBox.Button(row, InfoBox.Additional, "reallife_lst_withdraw", () => UiClone.Safe(Withdraw));
                    InfoBox.Button(row, InfoBox.Negative, "reallife_mkt_back", () => UiClone.Safe(ClosePage));
                    break;
                }
                case ListingStage.Auditing:
                {
                    box.Line(MarketText.Fill("reallife_lst_box_auditing", "day", f.AuditDone.ToString()));
                    Transform row = box.ButtonRow();
                    InfoBox.Button(row, InfoBox.Additional, "reallife_lst_withdraw", () => UiClone.Safe(Withdraw));
                    InfoBox.Button(row, InfoBox.Negative, "reallife_mkt_back", () => UiClone.Safe(ClosePage));
                    break;
                }
                case ListingStage.Audited:
                    WriteOffer(box, s, f, all);
                    break;
                case ListingStage.Building:
                {
                    box.Line(MarketText.Fill("reallife_lst_box_book", "name", f.Name, "share", MarketText.Percent(f.FloatBp), "low", MarketText.Price(f.LowCents), "high", MarketText.Price(f.HighCents),
                        "day", f.BookEnd.ToString()));
                    box.Line(MarketText.Fill("reallife_lst_box_least", "price", MarketText.Price(f.WalkCents)));
                    Transform row = box.ButtonRow();
                    InfoBox.Button(row, InfoBox.Additional, "reallife_lst_withdraw", () => UiClone.Safe(Withdraw));
                    InfoBox.Button(row, InfoBox.Negative, "reallife_mkt_back", () => UiClone.Safe(ClosePage));
                    break;
                }
                case ListingStage.Priced:
                {
                    long raised = MarketTrading.Value(f.NewShares, f.PriceCents, false);
                    box.Line(MarketText.Fill("reallife_lst_box_priced", "price", MarketText.Price(f.PriceCents), "day", f.ListDay.ToString(), "amount", MarketText.Money(raised)));
                    BackRow(box);
                    break;
                }
                default:
                    BackRow(box);
                    break;
            }
            if (f.Fees > 0)
            {
                box.Line(MarketText.Fill("reallife_lst_box_fees", "amount", MarketText.Money(f.Fees)));
            }
            return spec;
        }

        private void BackRow(InfoBox box)
        {
            InfoBox.Button(box.ButtonRow(), InfoBox.Negative, "reallife_mkt_back", () => UiClone.Safe(ClosePage));
        }

        // A clean audit in hand: the offer to be made (its name, the share offered, the least price taken) and what it comes to.
        private void WriteOffer(InfoBox box, MarketState s, ListingFile f, bool all)
        {
            string name = string.IsNullOrEmpty(_name) ? f.Name : _name;
            box.Line(MarketText.Fill("reallife_lst_box_audited", "day", f.AuditDay.ToString(), "until", (f.AuditDay + Math.Max(1, s.YearDays * ListingCatalogue.AuditValidQuarters / 4)).ToString()));
            int at = Array.IndexOf(ListingCatalogue.FloatSteps, _floatBp);
            box.Selector(ListingCatalogue.FloatSteps.Length, at < 0 ? 0 : at, i => MarketText.Fill("reallife_lst_float", "share", MarketText.Percent(ListingCatalogue.FloatSteps[i])), i => UiClone.Safe(() =>
            {
                _floatBp = ListingCatalogue.FloatSteps[i];
                Refresh();
            }));
            box.Selector(WalkKeys.Length, _walk, i => MarketText.T(WalkKeys[i]), i => UiClone.Safe(() =>
            {
                _walk = i;
                Refresh();
            }));
            // What the prospectus would say, worked out on a copy of the file (nothing is changed until it is published).
            double worth = _director.Worth();
            long founder = ListingBook.FounderShares(worth, Prices.Level / 1000.0);
            long offered = ListingBook.NewShares(founder, _floatBp);
            double each = founder + offered > 0 ? worth / (founder + offered) : 0.0;
            int mid = (int)Math.Round(each * 100.0 * (1.0 - ListingCatalogue.IpoDiscount));
            int low = ListingBook.Step(mid * (1.0 - ListingCatalogue.RangeSpread), false), high = ListingBook.Step(mid * (1.0 + ListingCatalogue.RangeSpread), true);
            box.Line(MarketText.Fill("reallife_lst_box_offer", "name", name, "count", Count(offered), "share", MarketText.Percent(_floatBp), "low", MarketText.Price(low), "high", MarketText.Price(high)));
            long raised = MarketTrading.Value(offered, (low + high) / 2, false);
            box.Line(MarketText.Fill("reallife_lst_box_raise", "amount", MarketText.Money(raised), "fees", MarketText.Money(ListingBook.Underwriting(raised, Prices.Level / 1000.0)
                + ListingBook.ListingFee((founder + offered) * ((low + high) / 200.0), Prices.Level / 1000.0)), "stake", MarketText.Percent(10000 - _floatBp)));
            if (_floatBp > 5000)
            {
                box.Line(MarketText.Fill("reallife_lst_box_control", "threshold", MarketText.Percent(ListingCatalogue.TakeoverThresholdBp)));
            }
            Transform row = box.ButtonRow();
            if (all)
            {
                int walk = _walk == 0 ? ListingBook.WalkLow : _walk == 1 ? ListingBook.WalkMiddle : ListingBook.WalkAny;
                string least = _walk == 0 ? MarketText.Price(low) : _walk == 1 ? MarketText.Price(ListingBook.Step((low + high) * 0.5, false)) : MarketText.T("reallife_lst_walk_any");
                InfoBox.Button(row, InfoBox.Positive, "reallife_lst_publish", () => UiClone.Safe(() => Publish(name, walk, least)));
            }
            AskName(row, name);
            InfoBox.Button(row, InfoBox.Negative, "reallife_lst_withdraw", () => UiClone.Safe(Withdraw));
            BackRow(box);
        }

        // The game's own text box for the company's name.
        private void AskName(Transform row, string name)
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (uis == null || uis.textInputUi == null)
            {
                return;
            }
            InfoBox.Button(row, InfoBox.Additional, "reallife_lst_name", () => uis.textInputUi.Show("reallife_lst_name_placeholder", "common_confirm", text => UiClone.Safe(() =>
            {
                string typed = MarketStore.Clean((text ?? string.Empty).Trim());
                if (typed.Length < 2)
                {
                    Notifications.ShowError("reallife_lst_note_name_short", null, false);
                    return;
                }
                _name = typed;
                Refresh();
            }), null, name ?? string.Empty, false, 32));
        }

        private void Publish(string name, int walk, string least)
        {
            HudConfirm.Show("reallife_lst_confirm_publish".Localize(new { name }),
                "reallife_lst_confirm_publish_body".Localize(new { name, share = MarketText.Percent(_floatBp), days = ListingCatalogue.BookDays.ToString(), least }),
                () => UiClone.Safe(() =>
                {
                    Step(_director.Publish(name, _floatBp, walk));
                    _name = null;
                }), null, "reallife_lst_publish", "common_cancel", false, false);
        }

        private void Withdraw()
        {
            HudConfirm.Show("reallife_lst_confirm_withdraw", "reallife_lst_confirm_withdraw_body", () => UiClone.Safe(() =>
            {
                _director.Withdraw();
                Refresh();
            }), null, "reallife_lst_withdraw", "common_cancel", false, true);
        }

        // The answer to a step: a refusal is the game's notification with the reason.
        private void Step(OwnWhy why)
        {
            if (why != OwnWhy.None)
            {
                Notifications.Show(NotificationType.Error, "reallife_lst_note_not_done", new Dictionary<string, string> { { "why", ListingText.Why(why) } }, 8f, null, null, true, false);
            }
            Refresh();
        }

        // Every requirement against the company's figures.
        private void WriteRequirements(Requirement[] r, int year)
        {
            for (int i = 0; i < r.Length; i++)
            {
                TableRow row = Row(OwnRow, i);
                row.Pick = false;
                row.Set(0, ListingText.Need(i));
                row.Set(1, Figure(r[i], r[i].Needed, year));
                row.Set(2, Figure(r[i], r[i].Has, year));
                row.Set(3, MarketText.T(r[i].Met ? "reallife_lst_met" : "reallife_lst_not_met"), r[i].Met ? TableRow.Green : TableRow.Red);
            }
        }

        private static string Figure(in Requirement r, long value, int year)
        {
            switch (r.Which)
            {
                case ListingBook.NeedYears:
                    return MarketText.Fill("reallife_lst_years", "years", (value / (double)Math.Max(8, year)).ToString("0.0", CultureInfo.CurrentCulture));
                case ListingBook.NeedTurnover:
                case ListingBook.NeedProfit:
                    return MarketText.Large(value);
                case ListingBook.NeedAudit:
                    return MarketText.T(value > 0 ? "reallife_lst_yes" : "reallife_lst_no");
                default:
                    return value.ToString(CultureInfo.CurrentCulture);
            }
        }

        // The company's books: each quarter's takings, profit and days, newest first.
        private void WriteBooks(ListingFile f, int day, int year)
        {
            for (int i = f.Books.Count - 1; i >= 0; i--)
            {
                BookQuarter q = f.Books[i];
                TableRow row = Row(OwnRow, 100 + i);
                row.Pick = false;
                int y = MarketCatalogue.FloorDiv(q.Quarter, 4);
                row.Set(0, MarketText.Fill("reallife_lst_books_quarter", "quarter", (q.Quarter - y * 4 + 1).ToString(), "year", (y + 1).ToString())
                           + (q.Quarter == MarketCatalogue.QuarterOf(day, year) ? "  <size=80%>" + MarketText.T("reallife_lst_books_running") + "</size>" : string.Empty));
                row.Set(1, MarketText.Large(q.Revenue));
                row.Set(2, MarketText.Large(q.Profit), q.Profit < 0 ? TableRow.Red : TableRow.Plain);
                row.Set(3, MarketText.Fill("reallife_lst_books_days", "days", q.Days.ToString()));
            }
        }

        // ---------------------------------------------------------------- the company on the market

        private void WriteListed(InfoBox box, MarketState s, Stock k, ListingFile f)
        {
            OwnListing o = k.Own;
            int bp = Bp(k.Last, k.PrevClose);
            box.Line(MarketText.Fill("reallife_lst_box_price", "price", MarketText.Price(k.Last), "change", MarketText.Change(bp), "worth", MarketText.Large(k.MarketValue)));
            box.Line(MarketText.Fill("reallife_lst_box_stake", "count", Count(o.Founder), "share", MarketText.Percent(o.StakeBp(k)), "worth", MarketText.Large(o.Founder * (k.Last / 100.0))));
            box.Line(MarketText.Fill("reallife_lst_box_reserve", "each", MarketText.Price((int)Math.Round(o.Rps * 100.0)), "amount", MarketText.Large(Math.Max(0.0, o.Rps) * o.Others(k))));
            if (o.Board)
            {
                int at = Array.IndexOf(ListingCatalogue.PaySteps, o.PayBp);
                box.Selector(ListingCatalogue.PaySteps.Length, at < 0 ? 0 : at, i => MarketText.Fill("reallife_lst_pay", "share", MarketText.Percent(ListingCatalogue.PaySteps[i]),
                    "fair", MarketText.Percent(ListingCatalogue.PayRefBp)), i => UiClone.Safe(() => Step(_director.Ask(OwnAction.Pay, ListingCatalogue.PaySteps[i]))));
            }
            else
            {
                box.Line(MarketText.Fill("reallife_lst_box_board_lost", "share", MarketText.Percent(o.PayBp), "payout", MarketText.Percent(ListingCatalogue.BoardPayoutBp)));
            }
            box.Line(Coming(s, k));
            if (o.ActBp > 0)
            {
                box.Line(MarketText.Fill("reallife_lst_box_activist", "stake", MarketText.Percent(o.ActBp), "demand", ListingText.Demand(o.ActDemand), "day", o.ActDay.ToString()));
            }
            if (k.BidCents > 0)
            {
                box.Line(MarketText.Fill(ListingVotes.CanWin(k) ? "reallife_lst_box_bid_can" : "reallife_lst_box_bid_cannot", "price", MarketText.Price(k.BidCents), "day", k.BidEndDay.ToString(),
                    "threshold", MarketText.Percent(ListingCatalogue.TakeoverThresholdBp)));
            }
            else if (o.Votes != 0)
            {
                box.Line(MarketText.Fill("reallife_lst_box_votes", "result", ListingText.Meeting(o.Votes), "against", MarketText.Percent(o.VotesAgainstBp)));
            }
            Transform row = box.ButtonRow();
            InfoBox.Button(row, InfoBox.Positive, "reallife_lst_trade", () => UiClone.Safe(() =>
            {
                OpenPage(k.Id);
                Refresh();
            }));
            if (o.Board)
            {
                AskCount(row, InfoBox.Additional, "reallife_lst_dividend", "reallife_lst_dividend_placeholder", true, cents => Step(_director.Ask(OwnAction.Dividend, cents)));
            }
            InfoBox.Button(row, InfoBox.Negative, "reallife_mkt_back", () => UiClone.Safe(ClosePage));
            if (o.Board)
            {
                Transform board = box.ButtonRow();
                AskCount(board, InfoBox.Positive, "reallife_lst_buyback", "reallife_mkt_shares_placeholder", false, n => Confirm(k, OwnAction.BuyBack, n));
                AskCount(board, InfoBox.Additional, "reallife_lst_issue", "reallife_mkt_shares_placeholder", false, n => Confirm(k, OwnAction.Issue, n));
                InfoBox.Button(board, InfoBox.Negative, "reallife_lst_private", () => UiClone.Safe(() => Confirm(k, OwnAction.Private, 0)));
            }
            if (_ownView == 0)
            {
                WriteRegister(k);
            }
            else
            {
                WriteResults(k);
            }
        }

        // What is coming: the dividend proposed, the results, the time the founder may not deal, the lock-up, the yearly meeting.
        private static string Coming(MarketState s, Stock k)
        {
            OwnListing o = k.Own;
            string closed = ListingRules.Closed(k, s.Day, s.YearDays)
                ? MarketText.Fill("reallife_lst_closed_until", "day", ListingRules.OpensAgain(k, s.Day, s.YearDays).ToString())
                : MarketText.T("reallife_lst_open_now");
            string text = MarketText.Fill("reallife_lst_box_coming", "proposed", o.ProposedCents > 0 ? MarketText.Price(Math.Min(o.ProposedCents, ListingRules.Most(o))) : MarketText.T("reallife_lst_none"),
                "results", k.ResultsDay > 0 ? k.ResultsDay.ToString() : "-", "closed", closed);
            if (s.Day < o.LockDay)
            {
                text += " " + MarketText.Fill("reallife_lst_box_lockup", "day", o.LockDay.ToString());
            }
            if (o.AgmDay > 0)
            {
                text += " " + MarketText.Fill("reallife_lst_box_meeting", "day", o.AgmDay.ToString());
            }
            return text;
        }

        // The register: the founder, an investor pressing the board, the other holders.
        private void WriteRegister(Stock k)
        {
            OwnListing o = k.Own;
            long investor = k.Shares * o.ActBp / 10000L;
            long others = Math.Max(0L, o.Others(k) - investor);
            Holder(0, MarketText.T("reallife_lst_holder_you"), o.Founder, k);
            if (investor > 0)
            {
                Holder(1, MarketText.Fill("reallife_lst_holder_investor", "demand", ListingText.Demand(o.ActDemand)), investor, k);
            }
            Holder(2, MarketText.T("reallife_lst_holder_others"), others, k);
        }

        // The company's results, newest first.
        private void WriteResults(Stock k)
        {
            for (int i = k.Results.Count - 1; i >= 0; i--)
            {
                QuarterResult r = k.Results[i];
                TableRow row = Row(OwnRow, 10 + i);
                row.Pick = false;
                int y = MarketCatalogue.FloorDiv(r.Quarter, 4);
                row.Set(0, MarketText.Fill("reallife_lst_books_quarter", "quarter", (r.Quarter - y * 4 + 1).ToString(), "year", (y + 1).ToString()));
                row.Set(1, MarketText.Large(r.RevenueK * 1000.0));
                row.Set(2, MarketText.Large(r.ProfitK * 1000.0), r.ProfitK < 0 ? TableRow.Red : TableRow.Plain);
                row.Set(3, r.DpsCents > 0 ? MarketText.Fill("reallife_lst_dividend_paid", "amount", MarketText.Price(r.DpsCents)) : "-");
            }
        }

        private void Holder(int index, string who, long shares, Stock k)
        {
            TableRow row = Row(OwnRow, index);
            row.Pick = false;
            row.Set(0, who);
            row.Set(1, Count(shares));
            row.Set(2, MarketText.Percent(k.Shares > 0 ? (int)(shares * 10000L / k.Shares) : 0));
            row.Set(3, MarketText.Large(shares * (k.Last / 100.0)));
        }

        // The game's own text box for a count of shares, or a price (cents) with 'price'.
        private void AskCount(Transform row, int kind, string key, string placeholderKey, bool price, Action<long> then)
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (uis == null || uis.textInputUi == null)
            {
                return;
            }
            InfoBox.Button(row, kind, key, () => uis.textInputUi.Show(placeholderKey, "common_confirm", text => UiClone.Safe(() =>
            {
                long value;
                if (!(price ? TryCents(text, out value) : long.TryParse((text ?? string.Empty).Trim().Replace(",", string.Empty), NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                    || value <= 0)
                {
                    Notifications.ShowError("common_notification_invalid_amount", null, false);
                    return;
                }
                then(value);
            }), null, string.Empty, false, TypedLength));
        }

        // A buy-back, new shares or taking the company private, put to the player with what it comes to before it is asked for.
        private void Confirm(Stock k, OwnAction action, long shares)
        {
            OwnQuote q = _director.Quote(action, shares);
            if (q.Why != OwnWhy.None)
            {
                Step(q.Why);
                return;
            }
            string title = action == OwnAction.BuyBack ? "reallife_lst_confirm_buyback" : action == OwnAction.Issue ? "reallife_lst_confirm_issue" : "reallife_lst_confirm_private";
            HudConfirm.Show(title.Localize(new { name = k.Name }),
                (title + "_body").Localize(new
                {
                    count = Count(q.Shares), price = MarketText.Price(q.Cents), value = MarketText.Money(q.Value), reserve = MarketText.Money(q.Reserve), fee = MarketText.Money(q.Fee),
                    total = MarketText.Money(Math.Abs(q.Total))
                }),
                () => UiClone.Safe(() => Step(_director.Ask(action, shares))), null, "common_confirm", "common_cancel", false, action == OwnAction.Private);
        }

        // ---------------------------------------------------------------- the founder's ticket on the company's page

        private void WriteOwnTicket(InfoBox box, MarketState s, Stock k)
        {
            OwnListing o = k.Own;
            box.Title(k.Name + "  <size=70%>" + k.Ticker + "</size>");
            Quote q = MarketTrading.QuoteOf(s, k, s.Phase);
            box.Line(MarketText.Fill("reallife_mkt_ticket_quote", "bid", MarketText.Price(q.Bid), "ask", MarketText.Price(q.Ask), "last", MarketText.Price(k.Last), "change", MarketText.Change(Bp(k.Last, k.PrevClose))));
            box.Line(MarketText.Fill("reallife_lst_ticket_held", "count", Count(o.Founder), "share", MarketText.Percent(o.StakeBp(k))));
            box.Line(ListingRules.Closed(k, s.Day, s.YearDays)
                ? MarketText.Fill("reallife_lst_ticket_closed", "day", ListingRules.OpensAgain(k, s.Day, s.YearDays).ToString())
                : s.Day < o.LockDay ? MarketText.Fill("reallife_lst_ticket_lockup", "day", o.LockDay.ToString()) : MarketText.Fill("reallife_lst_ticket_rules", "days", Math.Max(1, s.YearDays / ListingCatalogue.ShortSwingYearShare).ToString()));
            box.Selector(OwnSideKeys.Length, (int)_ownSide, i => MarketText.T(OwnSideKeys[i]), i => UiClone.Safe(() =>
            {
                _ownSide = (OwnSide)i;
                Refresh();
            }));
            Transform typed = box.ButtonRow();
            AskNumber(typed, InfoBox.Additional, MarketText.Fill("reallife_mkt_ticket_shares", "count", Count(_ownQty)), "reallife_mkt_shares_placeholder", false, value =>
            {
                _ownQty = Math.Max(1, value);
            });
            long shares = _ownSide == OwnSide.Buy ? _ownQty : -_ownQty;
            OwnQuote e = _director.Quote(OwnAction.Deal, shares);
            if (e.Why == OwnWhy.None)
            {
                box.Line(MarketText.Fill(shares > 0 ? "reallife_lst_ticket_cost" : "reallife_lst_ticket_brings", "price", MarketText.Price(e.Cents), "value", MarketText.Money(e.Value),
                    "reserve", MarketText.Money(e.Reserve), "fee", MarketText.Money(e.Fee), "total", MarketText.Money(Math.Abs(e.Total))));
            }
            else
            {
                box.Line(ListingText.Why(e.Why));
            }
            Transform buttons = box.ButtonRow();
            Stock company = k;
            InfoBox.Button(buttons, InfoBox.Positive, shares > 0 ? "reallife_mkt_buy" : "reallife_mkt_sell", () => UiClone.Safe(() => ConfirmDeal(company, shares)));
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_lst_open_listed", () => UiClone.Safe(() =>
            {
                OpenPage(OwnPage);
                Refresh();
            }));
            InfoBox.Button(buttons, InfoBox.Negative, "reallife_mkt_back", () => UiClone.Safe(ClosePage));
        }

        private void ConfirmDeal(Stock k, long shares)
        {
            OwnQuote q = _director.Quote(OwnAction.Deal, shares);
            if (q.Why != OwnWhy.None)
            {
                Step(q.Why);
                return;
            }
            HudConfirm.Show((shares > 0 ? "reallife_lst_confirm_buy" : "reallife_lst_confirm_sell").Localize(new { name = k.Name }),
                "reallife_lst_confirm_deal_body".Localize(new
                {
                    count = Count(q.Shares), price = MarketText.Price(q.Cents), value = MarketText.Money(q.Value), reserve = MarketText.Money(q.Reserve), fee = MarketText.Money(q.Fee),
                    total = MarketText.Money(Math.Abs(q.Total)), days = Math.Max(1, _director.State.YearDays / ListingCatalogue.ShortSwingYearShare).ToString()
                }),
                () => UiClone.Safe(() => Step(_director.Ask(OwnAction.Deal, shares))), null, shares > 0 ? "reallife_mkt_buy" : "reallife_mkt_sell", "common_cancel", false, shares < 0);
        }

        // ---------------------------------------------------------------- the company among the others

        // The founder's shares as the "held" figure of their own company on the Companies tab.
        private long HeldOrFounder(MarketState s, Stock k)
        {
            return k.Own != null && _director.Engine != null && _director.Engine.Mine(k) ? k.Own.Founder : s.Held(k.Id);
        }

        // The founder's own stake at the head of the Portfolio tab (not part of the account's figures).
        private void FounderRow(MarketState s)
        {
            Stock k = _director.MyCompany;
            if (k == null)
            {
                return;
            }
            Quote q = MarketTrading.QuoteOf(s, k, s.Phase);
            long value = MarketTrading.Value(k.Own.Founder, q.Bid, false);
            TableRow row = Row(CompanyRow, k.Id);
            row.Pinned = true;
            row.NoTotal = true;
            row.Kind = (int)k.Sector;
            row.Set(0, k.Name + "  <size=80%>" + MarketText.T("reallife_lst_yours") + "</size>");
            row.Set(1, Count(k.Own.Founder), k.Own.Founder);
            row.Set(2, "-", double.NaN);
            row.Set(3, MarketText.Price(k.Last), k.Last / 100.0);
            row.Set(4, MarketText.Money(value), value);
            row.Set(5, "-", double.NaN);
            row.Set(6, "-", double.NaN);
            row.Set(7, "-", double.NaN);
        }
    }
}
