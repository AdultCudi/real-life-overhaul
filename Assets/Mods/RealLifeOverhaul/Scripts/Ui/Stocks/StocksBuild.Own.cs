using System;
using System.Collections.Generic;
using RealLifeOverhaul.Markets;

namespace RealLifeOverhaul.Ui
{
    /// <summary>Scratch the sheets share: lists a sheet's builder and its press both read, kept so nothing is made while the sheet is up.</summary>
    public sealed class StocksSheetParts
    {
        public readonly int[] Days = new int[DerivCatalogue.WeeksAhead];
        public readonly List<string> PlaceKeys = new List<string>(8), PlaceNames = new List<string>(8);
        public readonly List<int> Picks = new List<int>(MarketBots.PicksMost);   // a firm being written: its picks for the hire
        public readonly List<int> Candidates = new List<int>(32);                 // the picker's companies
        public readonly List<int> Companies = new List<int>(64);                  // a rule's companies
        public string Name = string.Empty;                                        // the prospectus' name as typed
        public int RuleKindSeen = -1;                                             // the kind the level was last set for
    }

    /// <summary>
    /// The rest of the sheets: an order, the margin account, the company's own listing and the founder's actions, and the one
    /// builder the screen asks for whichever sheet is up.
    /// </summary>
    public static partial class StocksBuild
    {
        public const int OwnEngage = 1, OwnAudit = 2, OwnWithdraw = 3, OwnPublish = 4;

        public static StocksSheetModel SheetOf(MarketDirector d, StocksSheetState st, StocksSheetParts p, UiWords w)
        {
            MarketState s = d.State;
            switch (st.Kind)
            {
                case StocksSheets.BondBuy:
                case StocksSheets.Ladder: return BondBuy(d, st, w);
                case StocksSheets.BondHeld: return BondHeld(d, st, w);
                case StocksSheets.Material: return Material(d, st, w, p.Days, p.PlaceKeys, p.PlaceNames);
                case StocksSheets.ShareOption: return ShareOption(d, st, w, p.Days);
                case StocksSheets.Contract:
                {
                    DerivContract c = s.Derivs.Contract(st.Key);
                    long worth = c != null && c.IsOpen ? ContractWorth(d, c) : 0;
                    return Contract(d, st, w, worth, c != null && c.IsOpen ? ContractGain(c, worth) : 0);
                }
                case StocksSheets.Firm:
                {
                    Bot b = MarketBots.Hired(s, st.Key);
                    PickCandidates(s, d.Engine, b != null ? b.Picks : p.Picks, p.Candidates);
                    return Firm(d, st, w, b != null ? b.Picks : p.Picks, p.Candidates);
                }
                case StocksSheets.Rule:
                    RuleCompanies(s, p.Companies);
                    return RuleSheet(d, st, w, p.Companies, st.Key < 0);
                case StocksSheets.Order: return OrderSheet(d, st, w);
                case StocksSheets.Margin: return MarginSheet(d, st, w);
                case StocksSheets.Own: return OwnSheet(d, st, p, w);
                case StocksSheets.Founder: return FounderSheet(d, st, w);
                case StocksSheets.Alert: return AlertSheet(d, st, w);
                case StocksSheets.Ticket: return TicketSheet(d, st, w);
                case StocksSheets.Fund: return FundSheet(d, st, w);
                case StocksSheets.Block: return BlockSheet(d, st, w, p.Companies);
                case StocksSheets.Split: return SplitSheet(d, st, w);
                case StocksSheets.BankList: return BankSheet(d, st, w);
                default: return null;
            }
        }

        // ------------------------------------------------------------------------------------------------ an order

        public static StocksSheetModel OrderSheet(MarketDirector d, StocksSheetState st, UiWords w)
        {
            MarketState s = d.State;
            RealLifeOverhaul.Markets.Order o = s.Order(st.Key);
            StocksSheetModel m = Frame(w, w("reallife_mkt_tab_orders", "Orders"), w("reallife_stk_sh_order", "Order"), UiIcon.Receipt);
            if (o == null)
            {
                m.Name = w("reallife_stk_none", "None");
                return m;
            }
            Stock k = s.Stock(o.StockId);
            m.Name = k != null ? k.Name : string.Empty;
            m.Big = OrderStateWords(o);
            m.Sub = (k != null ? k.Ticker + "  " : string.Empty) + MarketText.T(OrderSideKeys[(int)o.Side]) + "  " + OrderKindWords(o);
            var side = new List<string>(8);
            var vals = new List<string>(8);
            side.Add(w("reallife_mkt_col_shares", "Shares")); vals.Add(o.Qty.ToString("N0"));
            if (o.Type == OrderType.Limit)
            {
                side.Add(w("reallife_stk_limit", "Limit")); vals.Add(MarketText.Price(o.LimitCents));
            }
            else if (o.Type == OrderType.Stop)
            {
                side.Add(w("reallife_stk_stop", "Stop")); vals.Add(MarketText.Price(o.StopCents));
            }
            side.Add(w("reallife_mkt_col_lasts",
                "Lasts")); vals.Add(o.Life == OrderLife.Day ? MarketText.T("reallife_mkt_life_day") : MarketText.Fill("reallife_mkt_until_day", "day", o.ExpireDay.ToString()));
            side.Add(w("reallife_stk_sh_placed", "Placed on day")); vals.Add(o.PlacedDay.ToString());
            if (o.Filled > 0)
            {
                side.Add(w("reallife_stk_sh_filled", "Filled")); vals.Add(o.Filled.ToString("N0") + " @ " + MarketText.Price((int)Math.Min(int.MaxValue, o.FilledValue * 100 / o.Filled)));
            }
            m.SideLabels = side.ToArray();
            m.SideValues = vals.ToArray();
            if (o.Why != OrderWhy.None && !o.IsOpen)
            {
                m.Reason = MarketText.Why(o.Why, k);
            }
            var extras = new List<UiActionModel>(2);
            if (k != null)
            {
                extras.Add(Extra(w("reallife_mkt_open_company", "Open the company"), false));
            }
            if (o.IsOpen && !o.Forced)
            {
                extras.Add(Extra(w("reallife_mkt_cancel_order", "Cancel the order"), false));
            }
            m.Extras = extras.ToArray();
            return m;
        }

        // ------------------------------------------------------------------------------------------------ the margin account

        public static StocksSheetModel MarginSheet(MarketDirector d, StocksSheetState st, UiWords w)
        {
            MarketState s = d.State;
            Account a = s.Account;
            bool deposit = st.Key != 1;
            long least = Prices.At(MarketCatalogue.MarginOpenLeastAtStart);
            long most = deposit ? Math.Max(1, d.Cash) : Math.Max(1, d.MostWithdrawable());
            long min = deposit && !a.Margin ? least : 1;
            StocksSheetModel m = Frame(w, w("reallife_mkt_tab_portfolio", "Portfolio"),
                w(deposit ? (a.Margin ? "reallife_mkt_deposit" : "reallife_mkt_open_margin") : "reallife_mkt_withdraw", deposit ? "Deposit" : "Withdraw"), UiIcon.Bank);
            m.Name = w(a.Margin ? "reallife_mkt_account_margin" : "reallife_mkt_account_cash", a.Margin ? "Margin account" : "Cash account");
            m.Big = Money(a.Cash);
            long equity = MarketTrading.Equity(s, s.Phase), need = MarketTrading.Maintenance(s, s.Phase);
            m.SideLabels = new[] { w("reallife_stk_sh_equity", "Equity"), w("reallife_stk_sh_maintenance", "Must keep"),
                w("reallife_stk_sh_withdrawable", "Can be withdrawn"), w("reallife_stk_cash", "Cash") };
            m.SideValues = new[] { Money(equity), Money(need), Money(d.MostWithdrawable()), Money(d.Cash) };
            long v = st.V[StocksSheets.Sum];
            m.Fields = new[] { StepField(StocksSheets.Sum, w("reallife_stk_sh_amount", "Amount"), Money(v), v, min, most, Nice(most / 20, 1), true) };
            bool ok = v >= min && v <= most && (deposit ? Settings.MarketsMargin : true) && !d.Behind;
            m.Reason = ok ? null : (deposit && !a.Margin ? Plain(MarketText.Fill("reallife_mkt_account_margin_offer", "least",
                Money(least))) : MarketText.T(deposit ? "reallife_mkt_note_deposit_refused" : "reallife_mkt_note_withdraw_refused"));
            m.Confirm = w(deposit ? "reallife_mkt_deposit" : "reallife_mkt_withdraw", deposit ? "Deposit" : "Withdraw");
            m.ConfirmOff = !ok;
            return m;
        }

        // ------------------------------------------------------------------------------------------------ the company's own listing

        public static StocksSheetModel OwnSheet(MarketDirector d, StocksSheetState st, StocksSheetParts p, UiWords w)
        {
            MarketState s = d.State;
            ListingFile f = s.Listing;
            StocksSheetModel m = Frame(w, w("reallife_stk_nav_listing", "Listing"), w("reallife_lst_title", "Your company's listing"), UiIcon.Presentation);
            m.Name = f != null && !string.IsNullOrEmpty(f.Name) ? f.Name : w("reallife_stk_list_empty", "Your company is not listing");
            m.Sub = f != null ? ListingText.Stage(f.Stage) : string.Empty;
            m.Big = f != null && f.Fees > 0 ? Money(f.Fees) : null;
            if (f == null)
            {
                return m;
            }
            if (f.Fees > 0)
            {
                m.About = Plain(MarketText.Fill("reallife_lst_box_fees", "amount", Money(f.Fees)));
            }
            switch (st.Key)
            {
                case OwnEngage:
                {
                    bool ready = d.Requirements(out Requirement[] r) && ListingBook.ReadyForAdvisers(r);
                    int met = 0;
                    for (int i = 0; i < r.Length; i++)
                    {
                        met += r[i].Met ? 1 : 0;
                    }
                    m.Title = w("reallife_lst_engage", "Engage advisers");
                    m.Fields = new[]
                    {
                        Fact(w("reallife_stk_sh_requirements", "Requirements met"), met + " / " + r.Length, ready ? 1 : -1),
                        Fact(w("reallife_stk_sh_fee", "Commission"), Money(d.Retainer())),
                        Note(Plain(MarketText.T("reallife_lst_box_steps")))
                    };
                    m.Confirm = w("reallife_lst_engage", "Engage advisers");
                    m.ConfirmOff = !ready;
                    m.Reason = ready ? null : Plain(MarketText.Fill("reallife_lst_box_not_ready", "amount", Money(d.Retainer())));
                    return m;
                }
                case OwnAudit:
                    m.Title = w("reallife_lst_audit", "Order the audit");
                    m.Fields = new[]
                    {
                        Fact(w("reallife_stk_sh_fee", "Commission"), Money(d.AuditFee())),
                        Fact(w("reallife_stk_sh_days", "Days it takes"), ListingCatalogue.AuditDays.ToString()),
                        Note(f.AuditDay > 0 && (f.Findings & ListingBook.Qualifying) != 0 ? Plain(MarketText.Fill("reallife_lst_box_qualified",
                            "found", ListingText.Findings(f.Findings))) : string.Empty)
                    };
                    m.Confirm = w("reallife_lst_audit", "Order the audit");
                    return m;
                case OwnWithdraw:
                    m.Title = w("reallife_lst_withdraw", "Withdraw");
                    m.Fields = new[] { Note(Asked(MarketText.T("reallife_lst_confirm_withdraw"), MarketText.T("reallife_lst_confirm_withdraw_body"))) };
                    m.Confirm = w("reallife_lst_withdraw", "Withdraw");
                    return m;
                default:
                    return PublishSheet(d, st, p, w, m, f);
            }
        }

        private static StocksSheetModel PublishSheet(MarketDirector d, StocksSheetState st, StocksSheetParts p, UiWords w, StocksSheetModel m, ListingFile f)
        {
            MarketState s = d.State;
            m.Title = w("reallife_lst_publish", "Open the book");
            bool all = d.Requirements(out Requirement[] r);
            int floatBp = ListingCatalogue.FloatSteps[Idx(st.V[StocksSheets.Float], ListingCatalogue.FloatSteps.Length)];
            int walkAt = Idx(st.V[StocksSheets.Walk], 3);
            string name = string.IsNullOrEmpty(p.Name) ? f.Name : p.Name;
            double worth = d.Worth();
            long founder = ListingBook.FounderShares(worth, Prices.Level / 1000.0);
            long offered = ListingBook.NewShares(founder, floatBp);
            double each = founder + offered > 0 ? worth / (founder + offered) : 0.0;
            int mid = (int)Math.Round(each * 100.0 * (1.0 - ListingCatalogue.IpoDiscount));
            int low = ListingBook.Step(mid * (1.0 - ListingCatalogue.RangeSpread), false), high = ListingBook.Step(mid * (1.0 + ListingCatalogue.RangeSpread), true);
            long raised = MarketTrading.Value(offered, (low + high) / 2, false);
            m.SideLabels = new[]
            {
                w("reallife_stk_sh_offered", "Shares offered"), w("reallife_stk_sh_range", "Price range"), w("reallife_stk_sh_raise", "Raised"),
                    w("reallife_stk_sh_fee", "Commission"), w("reallife_stk_sh_keep", "You keep")
            };
            m.SideValues = new[]
            {
                offered.ToString("N0"), MarketText.Price(low) + " - " + MarketText.Price(high), Money(raised),
                Money(ListingBook.Underwriting(raised, Prices.Level / 1000.0) + ListingBook.ListingFee((founder + offered) * ((low + high) / 200.0), Prices.Level / 1000.0)),
                MarketText.Percent(10000 - floatBp)
            };
            var fields = new List<StocksFieldModel>(4);
            fields.Add(new StocksFieldModel { Kind = StocksFieldKind.Text, Id = StocksSheets.Name, Label = w("reallife_stk_sh_name", "Name"), Value = name });
            fields.Add(PickField(StocksSheets.Float, w("reallife_stk_sh_float", "Offered"), MarketText.Fill("reallife_lst_float", "share",
                MarketText.Percent(floatBp)), ListingCatalogue.FloatSteps.Length));
            fields.Add(PickField(StocksSheets.Walk, w("reallife_stk_sh_walk", "Least price"), MarketText.T(WalkKeys[walkAt]), 3));
            if (floatBp > 5000)
            {
                fields.Add(Note(Plain(MarketText.Fill("reallife_lst_box_control", "threshold", MarketText.Percent(ListingCatalogue.TakeoverThresholdBp))), -1));
            }
            string least = walkAt == 0 ? MarketText.Price(low) : walkAt == 1 ? MarketText.Price(ListingBook.Step((low + high) * 0.5, false)) : MarketText.T("reallife_lst_walk_any");
            fields.Add(Note(Asked(MarketText.Fill("reallife_lst_confirm_publish", "name", name), MarketText.Fill("reallife_lst_confirm_publish_body", "name", name,
                "share", MarketText.Percent(floatBp), "days", ListingCatalogue.BookDays.ToString(), "least", least))));
            m.Fields = fields.ToArray();
            m.Confirm = w("reallife_lst_publish", "Open the book");
            m.ConfirmOff = !all || name.Length < 2;
            m.Reason = all ? (name.Length < 2 ? MarketText.T("reallife_lst_note_name_short") : null) : Plain(MarketText.Fill("reallife_lst_box_met",
                "met", "-", "count", r.Length.ToString()));
            return m;
        }

        public static readonly string[] WalkKeys = { "reallife_lst_walk_low", "reallife_lst_walk_mid", "reallife_lst_walk_any" };

        /// <summary>The least price a book takes by its choice: the range's bottom, its middle or any price, as the old page words them.</summary>
        public static int WalkOf(int at) => at == 0 ? ListingBook.WalkLow : at == 1 ? ListingBook.WalkMiddle : ListingBook.WalkAny;

        // ------------------------------------------------------------------------------------------------ the founder

        // The old question's key for the founder's buy-back, new shares and going private (its body is the same key and "_body").
        private static string FounderConfirmKey(OwnAction a) =>
            a == OwnAction.BuyBack ? "reallife_lst_confirm_buyback" : a == OwnAction.Issue ? "reallife_lst_confirm_issue" : "reallife_lst_confirm_private";

        public static StocksSheetModel FounderSheet(MarketDirector d, StocksSheetState st, UiWords w)
        {
            MarketState s = d.State;
            int target = (int)st.V[StocksSheets.Target];   // a firm of theirs that listed on its own, else their whole company
            Stock k = d.FounderStock(target);
            var action = (OwnAction)Math.Max(0, Math.Min((int)OwnAction.Private, st.Key));
            StocksSheetModel m = Frame(w, w("reallife_stk_nav_listing", "Listing"), FounderTitle(action, w), UiIcon.Presentation);
            if (k == null || k.Own == null)
            {
                m.Name = w("reallife_stk_list_empty", "Your company is not listing");
                return m;
            }
            OwnListing o = k.Own;
            m.Name = k.Name;
            m.Big = MarketText.Price(k.Last);
            m.Sub = k.Ticker;
            m.SideLabels = new[] { w("reallife_stk_sh_you_hold", "You hold"), w("reallife_stk_sh_stake", "Your stake"), w("reallife_stk_cash", "Cash") };
            m.SideValues = new[] { o.Founder.ToString("N0"), MarketText.Percent(o.StakeBp(k)), Money(d.Cash) };
            var f = new List<StocksFieldModel>(6);
            long v = st.V[StocksSheets.Sum];
            OwnQuote q = default(OwnQuote);
            bool quoted = false;
            switch (action)
            {
                case OwnAction.Pay:
                {
                    int at = Idx(st.V[StocksSheets.Sum], ListingCatalogue.PaySteps.Length);
                    f.Add(PickField(StocksSheets.Sum, w("reallife_stk_sh_pay", "Your pay"), MarketText.Fill("reallife_lst_pay", "share",
                        MarketText.Percent(ListingCatalogue.PaySteps[at]), "fair", MarketText.Percent(ListingCatalogue.PayRefBp)), ListingCatalogue.PaySteps.Length));
                    m.Confirm = w("reallife_stk_sh_save", "Save changes");
                    m.ConfirmOff = !o.Board;
                    m.Reason = o.Board ? null : Plain(MarketText.Fill("reallife_lst_box_board_lost", "share", MarketText.Percent(o.PayBp), "payout",
                        MarketText.Percent(ListingCatalogue.BoardPayoutBp)));
                    break;
                }
                case OwnAction.Dividend:
                    f.Add(StepField(StocksSheets.Sum, w("reallife_stk_sh_dividend", "Dividend a share"), MarketText.Price((int)Math.Min(int.MaxValue,
                        v)), v, 1, Math.Max(1, ListingRules.Most(o)), Math.Max(1, k.Last / 100), true));
                    m.Confirm = w("reallife_lst_dividend", "Propose a dividend");
                    m.ConfirmOff = !o.Board || v < 1;
                    break;
                default:
                {
                    bool deal = action == OwnAction.Deal;
                    bool sell = deal && st.V[StocksSheets.Side] != 0;
                    if (deal)
                    {
                        f.Add(PickField(StocksSheets.Side, w("reallife_mkt_col_side", "Side"), MarketText.T(sell ? "reallife_lst_side_sell" : "reallife_lst_side_buy"), 2));
                    }
                    if (action != OwnAction.Private)
                    {
                        f.Add(StepField(StocksSheets.Sum, w("reallife_mkt_col_shares", "Shares"), v.ToString("N0"), v, 1, Math.Max(1, k.Shares), Nice(Math.Max(1, k.Shares) / 50, 1), true));
                    }
                    long shares = deal ? (sell ? -v : v) : action == OwnAction.Private ? 0 : v;
                    q = d.Quote(action, shares, target);
                    quoted = true;
                    if (q.Why == OwnWhy.None)
                    {
                        f.Add(Fact(w("reallife_mkt_col_price", "Price"), MarketText.Price(q.Cents)));
                        f.Add(Fact(w("reallife_stk_sh_value", "Worth"), Money(q.Value)));
                        f.Add(Fact(w("reallife_stk_sh_reserve", "Reserve"), Money(q.Reserve)));
                        f.Add(Fact(w("reallife_stk_sh_fee", "Commission"), Money(q.Fee)));
                        f.Add(Fact(w("reallife_stk_sh_total", "Total"), Money(Math.Abs(q.Total)), q.Total > 0 ? -1 : 1));
                        f.Add(Note(Asked(MarketText.Fill(deal ? (sell ? "reallife_lst_confirm_sell" : "reallife_lst_confirm_buy") : FounderConfirmKey(action), "name", k.Name),
                            MarketText.Fill(deal ? "reallife_lst_confirm_deal_body" : FounderConfirmKey(action) + "_body", "count", q.Shares.ToString("N0"), "price", MarketText.Price(q.Cents),
                                "value", Money(q.Value), "reserve", Money(q.Reserve), "fee", Money(q.Fee), "total", Money(Math.Abs(q.Total)),
                                "days", Math.Max(1, s.YearDays / ListingCatalogue.ShortSwingYearShare).ToString()))));
                    }
                    m.Confirm = FounderTitle(action, w);
                    m.ConfirmOff = q.Why != OwnWhy.None;
                    break;
                }
            }
            m.Fields = f.ToArray();
            if (quoted && q.Why != OwnWhy.None)
            {
                m.Reason = ListingText.Why(q.Why);
            }
            return m;
        }

        // ------------------------------------------------------------------------------------------------ page buttons

        /// <summary>The margin account's buttons by the sheet each opens: 0 deposit (or open the account), 1 withdraw.</summary>
        public static void PortfolioCodes(MarketState s, List<int> into)
        {
            into.Clear();
            if (Settings.MarketsExtras || s.Extras.Units > 0)
            {
                into.Add(CodeFund);   // the extras first: the account's own button stays the last, the page's main one; units held can always be sold
            }
            if (Settings.MarketsExtras)
            {
                into.Add(CodeBlock);
            }
            if (s.Account.Margin)
            {
                if (Settings.MarketsMargin)
                {
                    into.Add(0);
                }
                into.Add(1);
            }
            else if (Settings.MarketsMargin)
            {
                into.Add(0);
            }
        }

        /// <summary>The listing page's buttons for the stage the company is at: under 100 the listing's own steps, from 100 the founder's actions.</summary>
        public static void ListingCodes(MarketDirector d, List<int> into)
        {
            into.Clear();
            Stock own = d.MyCompany;
            ListingFile f = d.Listing;
            if (own != null && own.Own != null)
            {
                if (own.Own.Board)
                {
                    into.Add(100 + (int)OwnAction.Pay);
                    into.Add(100 + (int)OwnAction.Dividend);
                    into.Add(100 + (int)OwnAction.BuyBack);
                    into.Add(100 + (int)OwnAction.Issue);
                    into.Add(100 + (int)OwnAction.Private);
                }
                into.Add(100 + (int)OwnAction.Deal);
                if (own.Own.Board && !own.Own.Firm && Settings.MarketsExtras)
                {
                    into.Add(CodeSplit);
                }
                return;
            }
            if (Settings.MarketsExtras && d.BankFloat != null && (d.BankFloat.Stage != BankStage.None || d.BankRuns))
            {
                into.Add(CodeBank);   // a bank of the player's own may list by itself (first, so the company's own step stays where it was after it)
            }
            if (f == null || d.CompanyAside)
            {
                return;
            }
            switch (f.Stage)
            {
                case ListingStage.None:
                    if (Settings.MarketsListing)
                    {
                        into.Add(OwnEngage);
                    }
                    break;
                case ListingStage.Advised:
                    into.Add(OwnAudit);
                    into.Add(OwnWithdraw);
                    break;
                case ListingStage.Audited:
                    into.Add(OwnPublish);
                    into.Add(OwnWithdraw);
                    break;
                case ListingStage.Auditing:
                case ListingStage.Building:
                    into.Add(OwnWithdraw);
                    break;
            }
        }

        private static string CodeWords(int code, UiWords w)
        {
            if (code >= CodeSplit)
            {
                return ExtrasCodeWords(code, w);
            }
            if (code >= 100)
            {
                return FounderTitle((OwnAction)(code - 100), w);
            }
            return code == OwnEngage ? w("reallife_lst_engage", "Engage advisers") : code == OwnAudit ? w("reallife_lst_audit", "Order the audit")
                : code == OwnWithdraw ? w("reallife_lst_withdraw", "Withdraw") : w("reallife_lst_publish", "Open the book");
        }

        /// <summary>Fills the page buttons of the pages that have them from the market's state.</summary>
        public static void PageButtons(StocksModel m, MarketDirector d, UiWords w)
        {
            var codes = new List<int>(8);
            MarketState s = d.State;
            switch (m.Page)
            {
                case StocksPage.Portfolio:
                {
                    long bonds = 0;
                    for (int i = 0; i < s.Bonds.Holdings.Count; i++)
                    {
                        bonds += d.BondSale(s.Bonds.Holdings[i]) / 100;
                    }
                    if (m.Figures != null && m.Figures.Length > 0 && m.Figures.Length < 4 && (bonds > 0 || d.DerivWorth() != 0))
                    {
                        var more = new UiFigureModel[m.Figures.Length + 1];
                        Array.Copy(m.Figures, more, m.Figures.Length);
                        more[m.Figures.Length] = Fig(w("reallife_stk_sh_bonds_contracts", "Bonds and contracts"), Money(bonds + d.DerivWorth()), 0);
                        m.Figures = more;
                    }
                    PortfolioCodes(s, codes);
                    m.Actions = new UiActionModel[codes.Count];
                    for (int i = 0; i < codes.Count; i++)
                    {
                        m.Actions[i] = new UiActionModel
                        {
                            Label = codes[i] >= CodeFund ? ExtrasCodeWords(codes[i], w)
                                : w(codes[i] == 1 ? "reallife_mkt_withdraw" : s.Account.Margin ? "reallife_mkt_deposit" : "reallife_mkt_open_margin",
                                codes[i] == 1 ? "Withdraw" : s.Account.Margin ? "Deposit" : "Open a margin account"),
                            Kind = i == codes.Count - 1 ? UiButtonKind.Primary : UiButtonKind.Secondary, Key = UiAction.Count
                        };
                    }
                    break;
                }
                case StocksPage.Listing:
                {
                    ListingCodes(d, codes);
                    m.Actions = new UiActionModel[codes.Count];
                    for (int i = 0; i < codes.Count; i++)
                    {
                        m.Actions[i] = new UiActionModel { Label = CodeWords(codes[i], w), Kind = i == 0 ? UiButtonKind.Primary : UiButtonKind.Secondary, Key = UiAction.Count };
                    }
                    break;
                }
            }
            if (string.IsNullOrEmpty(m.Status) && (m.Page == StocksPage.Market || m.Page == StocksPage.Companies || m.Page == StocksPage.Watch || m.Page == StocksPage.Orders
                || m.Page == StocksPage.History || m.Page == StocksPage.Portfolio || m.Page == StocksPage.Index))
            {
                m.Status = Status(d);
            }
            if (m.Page == StocksPage.Market && !string.IsNullOrEmpty(m.Note))
            {
                m.Status += "   " + m.Note;   // the player's day beside the market's state
                m.Note = null;
            }
        }

        public static string FounderTitle(OwnAction a, UiWords w)
        {
            switch (a)
            {
                case OwnAction.Pay: return w("reallife_stk_sh_pay_policy", "Your pay");
                case OwnAction.Dividend: return w("reallife_lst_dividend", "Propose a dividend");
                case OwnAction.BuyBack: return w("reallife_lst_buyback", "Buy back");
                case OwnAction.Issue: return w("reallife_lst_issue", "Issue shares");
                case OwnAction.Private: return w("reallife_lst_private", "Go private");
                default: return w("reallife_stk_sh_deal", "Deal in your shares");
            }
        }
    }
}
