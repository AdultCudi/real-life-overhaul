using System;
using System.Collections.Generic;
using RealLifeOverhaul.Markets;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The Portfolio's Pays and Account views, the History table and the Market's open-orders box: the old app's views that the first kit
    /// build left out, read from the same market state the old views read (InvestRules, the account, the trades). Figures come from
    /// <see cref="StocksFigures"/>, which the checks read; this part only puts them in words. Built when the page changes, not every frame.
    /// </summary>
    public static partial class StocksBuild
    {
        public const int ViewHeld = 0, ViewPays = 1, ViewAccount = 2, Views = 3;
        public const int PeriodMonth = 0, PeriodQuarter = 1, PeriodYear = 2, PeriodAll = 3, Periods = 4;

        private static readonly string[] ViewKeys = { "reallife_stk_view_held", "reallife_stk_view_pays", "reallife_stk_view_account" };
        private static readonly string[] ViewEnglish = { "Held", "Pays", "Account" };
        private static readonly string[] PeriodKeys = { "reallife_stk_period_month", "reallife_stk_period_quarter", "reallife_stk_period_year", "reallife_stk_period_all" };
        private static readonly string[] PeriodEnglish = { "1 month", "3 months", "1 year", "All" };

        /// <summary>The days of the portfolio chart for a period: 30, 90, 360, or every day the candles keep.</summary>
        public static int PeriodDays(int period) => period == PeriodMonth ? 30 : period == PeriodQuarter ? 90 : period == PeriodYear ? 360 : CandlesMost * 2;

        public static string PeriodWord(int period, UiWords w)
        {
            int i = period >= 0 && period < Periods ? period : 0;
            return w(PeriodKeys[i], PeriodEnglish[i]);
        }

        private static void ViewLabels(StocksModel m, int view, UiWords w)
        {
            m.ViewLabels = new string[Views];
            for (int i = 0; i < Views; i++)
            {
                m.ViewLabels[i] = w(ViewKeys[i], ViewEnglish[i]);
            }
            m.View = view >= 0 && view < Views ? view : ViewHeld;
        }

        // ------------------------------------------------------------------------------------------------ pays

        /// <summary>
        /// What it pays: the next year's income by kind of holding before tax, what shares sold short owe, the yield on the worth, and the
        /// payments to come with the day of each. The old Pays view's figures, the same rows and sums (InvestRules).
        /// </summary>
        public static void PaysView(StocksModel m, MarketDirector d, UiWords w)
        {
            MarketState s = d.State;
            var f = new PaysFigures();
            StocksFigures.Pays(s, s.Phase, Settings.MarketsBonds, f);
            m.Figures = new[]
            {
                new UiFigureModel { Label = w("reallife_stk_pays_year", "Income over the next year"), Value = BondText.Money(f.YearCents) },
                new UiFigureModel { Label = w("reallife_mkt_inv_yield", "Yield on the worth"), Value = MarketText.Percent(f.Sums.YieldBp) },
                new UiFigureModel { Label = w("reallife_stk_pays_next", "Payments to come"), Value = f.NextAll.ToString() }
            };
            var rows = new List<StocksRow>(HoldingKindCount + 3);
            for (int k = 0; k < (int)HoldingKind.Count; k++)
            {
                if (f.Sums.YearCents[k] > 0)
                {
                    rows.Add(new StocksRow { Label = InvestText.Income((HoldingKind)k), Value = BondText.Money(f.Sums.YearCents[k]) });
                }
            }
            if (f.Sums.OwedCents > 0)
            {
                rows.Add(new StocksRow { Label = w("reallife_mkt_inv_income_owed", "Owed by shares sold short"), Value = "-" + BondText.Money(f.Sums.OwedCents), Tone = UiRole.DangerText });
            }
            if (f.Any)
            {
                rows.Add(new StocksRow { Label = w("reallife_mkt_inv_income_all", "In all"), Value = BondText.Money(f.YearCents) });
            }
            m.RowsTitle = w("reallife_mkt_inv_sec_year", "Over the next year");
            m.Rows = rows.ToArray();
            m.Status = Margin(d, w);
            var payments = new UiDataRow[f.Next.Count];
            for (int i = 0; i < payments.Length; i++)
            {
                ComingPayment p = f.Next[i];
                Stock paying = p.Stock > 0 ? s.Stock(p.Stock) : null;
                string sub = paying != null ? paying.Ticker : p.Ends ? w("reallife_stk_pays_ends", "Repays at its end") : p.Rolls ? w("reallife_stk_pays_rolls", "Then it rolls over") : null;
                payments[i] = Row(new[] { PaymentName(s, p), p.Day.ToString(), (p.Cents < 0 ? "-" : string.Empty) + BondText.Money(Math.Abs(p.Cents)) },
                    (int)(p.Bond > 0 ? UiIcon.Bank : UiIcon.Building), sub, new[] { UiRole.None, UiRole.None, p.Cents < 0 ? UiRole.DangerText : UiRole.None });
            }
            m.TableTitle = w("reallife_ibox_sec_payments", "Payments");
            m.Table = new UiDataTableModel
            {
                Columns = new[]
                {
                    Col(w("reallife_stk_pays_from", "Paid by"), UiCellKind.Tile), Col(w("reallife_mkt_col_day", "Day"), UiCellKind.Figure, 100f),
                    Col(w("reallife_stk_sh_amount", "Amount"), UiCellKind.Figure, 140f)
                },
                Rows = payments, RowCount = payments.Length, EmptyIcon = UiIcon.Receipt,
                EmptyTitle = w("reallife_stk_pays_empty", "No payment is due yet"), EmptyBody = Plain(MarketText.T(f.Any ? "reallife_mkt_inv_income_note" : "reallife_mkt_inv_income_none"))
            };
            m.Note = f.Any ? Plain(MarketText.T("reallife_mkt_inv_income_note")) : null;
        }

        private const int HoldingKindCount = (int)HoldingKind.Count;

        // What pays a payment to come: the company (with a bot's firm), or the bond.
        private static string PaymentName(MarketState s, in ComingPayment p)
        {
            if (p.Bond > 0)
            {
                return BondText.Name(s.Bonds.Holding(p.Bond));
            }
            Stock k = s.Stock(p.Stock);
            BotFirm f = p.Firm > 0 ? MarketBots.Firm(p.Firm) : null;
            return (k != null ? k.Name : string.Empty) + (f != null ? InvestText.Mark(f.Name) : string.Empty);
        }

        /// <summary>The company or bond a payment row stands for, as the screen opens it: a company's id, or minus a bond's number.</summary>
        public static int PaymentTarget(MarketDirector d, int row)
        {
            MarketState s = d.State;
            var f = new PaysFigures();
            StocksFigures.Pays(s, s.Phase, Settings.MarketsBonds, f);
            if (row < 0 || row >= f.Next.Count)
            {
                return 0;
            }
            ComingPayment p = f.Next[row];
            return p.Bond > 0 ? -p.Bond : p.Stock;
        }

        // ------------------------------------------------------------------------------------------------ account

        /// <summary>The margin call in words, or null: the old Account view's red line.</summary>
        public static string Margin(MarketDirector d, UiWords w)
        {
            AccountFigures a = StocksFigures.Account(d.State, d.State.Phase, 0);
            return a.Called ? Plain(MarketText.Fill("reallife_mkt_account_call", "amount", Money(a.CallAmount), "day", a.CallDay.ToString())) : null;
        }

        /// <summary>
        /// The account: the margin call over the card of what the old view said (cash, equity and what a margin account must keep, its buying
        /// power and what can be taken out), the dividends paid to the player, the commission paid, and what a margin account costs.
        /// </summary>
        public static void AccountView(StocksModel m, MarketDirector d, UiWords w)
        {
            MarketState s = d.State;
            AccountFigures a = StocksFigures.Account(s, s.Phase, d.MostWithdrawable());
            m.Banner = a.Called ? Margin(d, w) : null;
            m.BannerTone = UiRole.DangerText;
            var figs = new List<UiFigureModel>(4)
            {
                new UiFigureModel { Label = w("reallife_stk_cash", "Cash"), Value = Money(a.Cash) }
            };
            if (a.Margin)
            {
                figs.Add(new UiFigureModel { Label = w("reallife_stk_sh_equity", "Equity"), Value = Money(a.Equity) });
            }
            figs.Add(new UiFigureModel { Label = w("reallife_stk_acc_dividends", "Dividends paid to you"), Value = Money(a.Dividends) });
            figs.Add(new UiFigureModel { Label = w("reallife_stk_acc_commission", "Commission paid"), Value = Money(a.Commission) });
            m.Figures = figs.ToArray();
            var rows = new List<StocksRow>(10)
            {
                new StocksRow { Label = w("reallife_stk_cash", "Cash"), Value = Money(a.Cash), Tone = a.Cash < 0 ? UiRole.DangerText : UiRole.None }
            };
            if (a.Margin)
            {
                rows.Add(new StocksRow { Label = w("reallife_stk_sh_equity", "Equity"), Value = Money(a.Equity) });
                rows.Add(new StocksRow { Label = w("reallife_stk_sh_maintenance", "Must keep"), Value = Money(a.Need) });
                rows.Add(new StocksRow { Label = w("reallife_stk_acc_power", "Buying power"), Value = Money(a.Power) });
                rows.Add(new StocksRow { Label = w("reallife_stk_sh_withdrawable", "Can be withdrawn"), Value = Money(a.Withdrawable) });
            }
            rows.Add(new StocksRow { Label = w("reallife_stk_realised_all", "Realised, all time"), Value = Money(a.Realised),
                Tone = a.Realised > 0 ? UiRole.OkText : a.Realised < 0 ? UiRole.DangerText : UiRole.None });
            rows.Add(new StocksRow { Label = w("reallife_stk_acc_dividends", "Dividends paid to you"), Value = Money(a.Dividends) });
            if (a.DividendsPaid > 0)
            {
                rows.Add(new StocksRow { Label = w("reallife_stk_acc_div_paid", "Dividends you paid on shorts"), Value = Money(a.DividendsPaid) });
            }
            rows.Add(new StocksRow { Label = w("reallife_stk_acc_commission", "Commission paid"), Value = Money(a.Commission) });
            if (a.Interest > 0)
            {
                rows.Add(new StocksRow { Label = w("reallife_stk_acc_interest", "Interest paid"), Value = Money(a.Interest) });
            }
            if (a.BorrowFees > 0)
            {
                rows.Add(new StocksRow { Label = w("reallife_stk_acc_borrow", "Borrowing fees paid"), Value = Money(a.BorrowFees) });
            }
            m.RowsTitle = w(a.Margin ? "reallife_mkt_account_margin" : "reallife_mkt_account_cash", a.Margin ? "Your margin account" : "Your account");
            m.Rows = rows.ToArray();
            m.Table = null;
        }

        // ------------------------------------------------------------------------------------------------ history

        private static readonly string[] TradeKindKeys =
        {
            "reallife_mkt_kind_bought", "reallife_mkt_kind_sold", "reallife_mkt_kind_dividend", "reallife_mkt_kind_dividend_paid", "reallife_mkt_kind_payout", "reallife_mkt_kind_interest",
            "reallife_mkt_kind_borrow_fee", "reallife_mkt_kind_deposit", "reallife_mkt_kind_withdrawal", "reallife_mkt_kind_shortfall", "reallife_mkt_kind_bot_added",
            "reallife_mkt_kind_bot_taken"
        };

        /// <summary>The trade a History row stands for: the rows run newest first, so row 0 is the last trade of the list.</summary>
        public static int TradeAt(MarketState s, int row) => row >= 0 && row < s.Trades.Count ? s.Trades.Count - 1 - row : -1;

        /// <summary>Every line the account keeps (the last <see cref="MarketCatalogue.TradesKept"/>): day, company, what, shares, price, money and commission.</summary>
        public static void HistoryPage(StocksModel m, MarketState s, UiWords w)
        {
            var rows = new UiDataRow[s.Trades.Count];
            for (int r = 0; r < rows.Length; r++)
            {
                Trade t = s.Trades[s.Trades.Count - 1 - r];
                Stock k = t.StockId > 0 ? s.Stock(t.StockId) : null;
                int kind = Math.Max(0, Math.Min(TradeKindKeys.Length - 1, (int)t.Kind));
                BotFirm firm = t.Bot > 0 ? MarketBots.Firm(t.Bot) : null;
                string what = MarketText.T(TradeKindKeys[kind]) + (t.Test ? InvestText.Mark(MarketText.T("reallife_mkt_test_mark")) : string.Empty)
                    + (firm != null ? InvestText.Mark(firm.Name) : string.Empty)
                    + (t.Order > 0 ? InvestText.Mark(MarketText.T("reallife_mkt_inv_mark_worked")) : t.Back ? InvestText.Mark(MarketText.T("reallife_mkt_inv_mark_put_back")) : string.Empty);
                rows[r] = Row(new[]
                {
                    k != null ? k.Name : w("reallife_stk_none", "None"), MarketText.Fill("reallife_mkt_day", "day", t.Day.ToString()), Plain(what), t.Qty != 0 ? t.Qty.ToString("N0") : string.Empty,
                    t.PriceCents > 0 ? MarketText.Price(t.PriceCents) : string.Empty, MarketText.Money(t.Value), t.Fee > 0 ? MarketText.Money(t.Fee) : string.Empty
                }, (int)UiIcon.Building, k != null ? k.Ticker : null,
                    new[] { k != null ? UiTokens.Chart((int)k.Sector) : UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None,
                        t.Value > 0 ? UiRole.OkText : t.Value < 0 ? UiRole.DangerText : UiRole.None, UiRole.None });
            }
            Account a = s.Account;
            string note = Plain(MarketText.Fill("reallife_mkt_history_totals", "commission", MarketText.Money(a.Commission), "dividends", MarketText.Money(a.Dividends),
                "realised", MarketText.Money(a.Realised)));
            if (a.DividendsPaid + a.Interest + a.BorrowFees > 0)
            {
                note += "  " + Plain(MarketText.Fill("reallife_mkt_history_costs", "paid", MarketText.Money(a.DividendsPaid), "interest", MarketText.Money(a.Interest),
                    "fees", MarketText.Money(a.BorrowFees)));
            }
            m.Note = note;
            m.Table = new UiDataTableModel
            {
                Columns = new[]
                {
                    Col(w("reallife_mkt_col_company", "Company"), UiCellKind.Tile), Col(w("reallife_mkt_col_day", "Day"), UiCellKind.Figure, 88f),
                    Col(w("reallife_mkt_col_what", "What"), UiCellKind.Text, 224f), Col(w("reallife_mkt_col_shares", "Shares"), UiCellKind.Figure, 104f),
                    Col(w("reallife_mkt_col_price", "Price"), UiCellKind.Figure, 100f), Col(w("reallife_mkt_col_money", "Money"), UiCellKind.Figure, 120f),
                    Col(w("reallife_mkt_col_commission", "Commission"), UiCellKind.Figure, 112f)
                },
                Rows = rows, RowCount = rows.Length, EmptyIcon = UiIcon.Clock,
                EmptyTitle = w("reallife_stk_hist_empty", "No trades yet"), EmptyBody = w("reallife_stk_hist_empty_body", "Fills, dividends and cash you move show here.")
            };
        }

        // ------------------------------------------------------------------------------------------------ price alerts

        /// <summary>Why an alert could not be set, in the player's words.</summary>
        public static string AlertWhyWords(AlertWhy why, UiWords w)
        {
            switch (why)
            {
                case AlertWhy.Full: return w("reallife_stk_alert_why_full", "You keep as many alerts as you may. Take one off first.");
                case AlertWhy.NoLine: return w("reallife_stk_alert_why_line", "Select a level line on the chart first: the horizontal line.");
                case AlertWhy.NoStock: return w("reallife_stk_none", "None");
                default: return w("reallife_stk_alert_why_level", "An alert over the price needs a level over it, and one under the price a level under it.");
            }
        }

        /// <summary>The level an alert starts at: a twentieth over the price now, or under it.</summary>
        public static int AlertStart(int lastCents, bool above) => above ? Math.Max(lastCents + 1, lastCents * 105 / 100) : Math.Max(1, Math.Min(lastCents - 1, lastCents * 95 / 100));

        /// <summary>The alert sheet: which side of the price it is on and the level, a stepper that may also be typed in dollars.</summary>
        public static StocksSheetModel AlertSheet(MarketDirector d, StocksSheetState st, UiWords w)
        {
            MarketState s = d.State;
            Stock k = s.Stock(st.Key);
            StocksSheetModel m = Frame(w, w("reallife_stk_alerts_title", "Price alerts"), w("reallife_stk_alert_sheet", "Set a price alert"), UiIcon.Bell);
            if (k == null)
            {
                m.Name = w("reallife_stk_none", "None");
                return m;
            }
            bool above = st.V[StocksSheets.AlertKindAt] == 0;
            long level = Math.Max(1L, Math.Min(AlertMost(k), st.V[StocksSheets.AlertLevel]));
            m.Name = k.Name;
            m.Sub = k.Ticker;
            m.Big = MarketText.Price(k.Last);
            int mine = 0;
            for (int i = 0; i < s.Charts.Alerts.Count; i++)
            {
                mine += s.Charts.Alerts[i].StockId == k.Id ? 1 : 0;
            }
            m.SideLabels = new[] { w("reallife_stk_row_price", "Price"), w("reallife_stk_alerts_title", "Price alerts") };
            m.SideValues = new[] { MarketText.Price(k.Last), mine.ToString() };
            m.About = w("reallife_stk_alert_about", "You are told when the price crosses the level. It is checked as the market moves.");
            m.Fields = new[]
            {
                PickField(StocksSheets.AlertKindAt, w("reallife_stk_alert_goes", "Tell me when the price goes"),
                    w(above ? "reallife_stk_alert_over" : "reallife_stk_alert_under", above ? "Over the level" : "Under the level"), 2),
                StepField(StocksSheets.AlertLevel, w("reallife_stk_alert_level", "Level"), MarketText.Price((int)level), level, 1L, AlertMost(k), Math.Max(1L, k.Last / 100), true)
            };
            AlertWhy why = s.Charts.CheckAlert(k.Id, above ? AlertKind.Above : AlertKind.Below, (int)level, k.Last);
            m.Reason = why == AlertWhy.None ? null : AlertWhyWords(why, w);
            m.Confirm = w("reallife_stk_alert_set_go", "Set the alert");
            m.ConfirmOff = why != AlertWhy.None;
            return m;
        }

        /// <summary>The highest level an alert may be set at: ten times the price now.</summary>
        public static long AlertMost(Stock k) => Math.Max(100L, k.Last * 10L);

        // ------------------------------------------------------------------------------------------------ the Market's open orders

        /// <summary>The open orders the Market's box lists, newest first (the Orders page's open rows): their ids, so a press opens the same order.</summary>
        public static void OpenOrders(MarketState s, StocksModel m, UiWords w)
        {
            var ids = new List<int>(16);
            OrderRows(s, false, ids);
            int n = Math.Min(3, ids.Count);
            m.OrderLines = new string[n];
            m.OrderValues = new string[n];
            for (int i = 0; i < n; i++)
            {
                RealLifeOverhaul.Markets.Order o = s.Order(ids[i]);
                Stock k = s.Stock(o.StockId);
                int cents = o.Type == OrderType.Limit ? o.LimitCents : o.Type == OrderType.Stop ? o.StopCents : k != null ? k.Last : 0;
                bool buying = o.Side == OrderSide.Buy;
                string line = w(buying ? "reallife_mkt_buy" : "reallife_mkt_sell", buying ? "Buy" : "Sell") + " " + o.Qty.ToString("N0") + " " + (k != null ? k.Ticker : string.Empty);
                if (o.Type != OrderType.Market)
                {
                    line += ", " + OrderKindWords(o).ToLowerInvariant() + " " + MarketText.Price(cents);
                }
                m.OrderLines[i] = line;
                m.OrderValues[i] = MarketText.Money(MarketTrading.Value(o.Qty, cents, o.Side == OrderSide.Buy));
            }
            m.OrdersTitle = w("reallife_stk_your_orders", "Your orders");
            m.OrdersCount = ids.Count == 0 ? null : MarketText.Fill("reallife_stk_orders_open_count", "count", ids.Count.ToString());
            if (ids.Count == 0)
            {
                m.OrderLines = new string[0];
                m.OrderValues = new string[0];
            }
        }
    }
}
