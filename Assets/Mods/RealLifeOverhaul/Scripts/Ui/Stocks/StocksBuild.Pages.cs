using System;
using System.Collections.Generic;
using RealLifeOverhaul.Markets;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The Bonds, Futures, Bots and Orders pages of the Stocks desk: what can be bought, opened or hired over what is held.
    /// Rows are built for the rows in view's sake once per change, and a row stands for one thing the player may press: an offer
    /// opens its sheet, a holding opens its own. The row order is fixed here so the screen maps a press back to the same thing.
    /// </summary>
    public static partial class StocksBuild
    {
        private static UiDataColumn Col(string title, UiCellKind kind, float width = 0f)
        {
            return new UiDataColumn { Title = title, Kind = kind, Width = width };
        }

        private static UiDataRow Row(string[] cells, int icon, string sub, UiRole[] tones = null)
        {
            var values = new int[cells.Length];
            values[0] = icon;
            return new UiDataRow { Cells = cells, Values = values, Tones = tones ?? new UiRole[cells.Length], Sub = sub };
        }

        /// <summary>The market's state in one line, as the old app's status line says it.</summary>
        public static string Status(MarketDirector d)
        {
            string key = d.Behind ? "reallife_mkt_status_behind"
                : d.Engine != null && d.Engine.Held ? "reallife_mkt_status_held" : d.Trading ? "reallife_mkt_status_open" : "reallife_mkt_status_shut";
            return Plain(MarketText.Fill(key, "open", Clock(MarketCatalogue.OpenMinute), "close", Clock(MarketCatalogue.CloseMinute)));
        }

        private static string Clock(int minute) => (minute / 60).ToString("00") + ":" + (minute % 60).ToString("00");

        // ------------------------------------------------------------------------------------------------ bonds

        public static void BondsPage(StocksModel m, MarketDirector d, UiWords w)
        {
            MarketState s = d.State;
            List<BondOffer> offers = d.BondOffers();
            var ladder = new List<BondOffer>(8);
            LadderOffers(offers, ladder);
            int year = Math.Max(8, s.YearDays);
            long worth = 0;
            int next = 0;
            for (int i = 0; i < s.Bonds.Holdings.Count; i++)
            {
                BondHolding h = s.Bonds.Holdings[i];
                worth += d.BondSale(h) / 100;
                int day = BondRules.NextPayDay(h);
                next = day > 0 && (next == 0 || day < next) ? day : next;
            }
            m.Status = Status(d);
            m.Figures = new[]
            {
                Fig(w("reallife_stk_worth", "Worth"), Money(worth), 0),
                Fig(w("reallife_stk_sh_bank_rate", "Bank rate"), BondText.Rate(s.Env.RateBp), 0),
                Fig(w("reallife_stk_sh_next", "Next payment"), next > 0 ? MarketText.Fill("reallife_mkt_day", "day", next.ToString()) : "-", 0)
            };
            m.TableTitle = w("reallife_mkt_bond_filter_offers", "On offer");
            m.Table2Title = w("reallife_mkt_bond_filter_held", "Yours");
            var rows = new UiDataRow[offers.Count + (ladder.Count > 0 ? 1 : 0)];
            string least = Money(d.BondLeast);
            for (int i = 0; i < offers.Count; i++)
            {
                BondOffer o = offers[i];
                rows[i] = Row(new[] { BondText.Name(o.Def, o.Issuer), BondText.Term(o.Def.TermQuarters), BondText.Rate(o.YieldBp), BondText.Pays(o.Def), least, BondText.Price(100.0) },
                    (int)UiIcon.Bank, BondText.KindWords(o.Def));
            }
            if (ladder.Count > 0)
            {
                rows[offers.Count] = Row(new[]
                {
                    MarketText.T("reallife_mkt_bond_ladder"),
                    MarketText.Fill("reallife_mkt_bond_ladder_terms", "first", BondText.Term(ladder[0].Def.TermQuarters), "last", BondText.Term(ladder[ladder.Count - 1].Def.TermQuarters)),
                    BondText.Rate(LadderYield(ladder)), MarketText.T("reallife_mkt_bond_ladder_pays"), least, BondText.Price(100.0)
                }, (int)UiIcon.Bank, BondText.KindWords(ladder[0].Def));
            }
            m.Table = new UiDataTableModel
            {
                Columns = new[]
                {
                    Col(w("reallife_mkt_bond_col_bond", "Bond"), UiCellKind.Tile), Col(w("reallife_stk_sh_term", "Term"), UiCellKind.Text, 152f),
                    Col(w("reallife_mkt_col_yield", "Yield"), UiCellKind.Figure, 88f), Col(w("reallife_stk_sh_pays", "Pays"), UiCellKind.Text, 152f),
                    Col(w("reallife_stk_sh_least", "Least"), UiCellKind.Figure, 112f), Col(w("reallife_mkt_col_price", "Price"), UiCellKind.Figure, 88f)
                },
                Rows = rows, RowCount = rows.Length, EmptyIcon = UiIcon.Bank,
                EmptyTitle = w("reallife_stk_bonds_offer_empty", "No bonds on offer"), EmptyBody = w("reallife_stk_bonds_offer_empty_body",
                    "Bonds are offered while the market runs and Bonds and deposits is on.")
            };
            int held = s.Bonds.Holdings.Count;
            var mine = new UiDataRow[held];
            for (int i = 0; i < held; i++)
            {
                BondHolding h = s.Bonds.Holdings[i];
                BondDef def = BondCatalogue.Find(h.Def);
                long sale = d.BondSale(h);
                int day = BondRules.NextPayDay(h);
                mine[i] = Row(new[]
                {
                    BondText.Name(h) + (h.Rollover ? "  " + MarketText.T("reallife_mkt_inv_mark_rolls") : string.Empty), BondText.Money(sale), BondText.Rate(h.RateBp), h.EndDay.ToString(),
                    day > 0 && def != null ? MarketText.Fill("reallife_mkt_next_pay", "amount", BondText.Money(BondRules.InterestCents(h, def)), "day", day.ToString()) : string.Empty
                }, (int)UiIcon.Bank, BondText.KindWords(def));
            }
            m.Table2 = new UiDataTableModel
            {
                Columns = new[]
                {
                    Col(w("reallife_mkt_bond_col_bond", "Bond"), UiCellKind.Tile), Col(w("reallife_stk_worth", "Worth"), UiCellKind.Figure, 120f),
                    Col(w("reallife_mkt_col_yield", "Yield"), UiCellKind.Figure, 88f), Col(w("reallife_stk_sh_ends", "Ends on day"), UiCellKind.Figure, 128f),
                    Col(w("reallife_stk_sh_next", "Next payment"), UiCellKind.Text, 192f)
                },
                Rows = mine, RowCount = held, EmptyIcon = UiIcon.Bank,
                EmptyTitle = w("reallife_stk_bonds_empty", "No bonds yet"), EmptyBody = w("reallife_stk_bonds_empty_body", "Treasury and city bonds you buy show here, with what they pay.")
            };
        }

        // ------------------------------------------------------------------------------------------------ futures

        /// <summary>The companies options are written on today, in the order the Futures page lists them.</summary>
        public static void OptionStocks(MarketState s, List<int> into)
        {
            into.Clear();
            for (int i = 0; i < s.Stocks.Count; i++)
            {
                if (DerivRules.ShareOffered(s.Stocks[i], s.Day) == DerivWhy.None)
                {
                    into.Add(s.Stocks[i].Id);
                }
            }
        }

        public static long ContractWorth(MarketDirector d, DerivContract c)
        {
            long price = d.ClosePrice(c);
            if (c.IsFuture)
            {
                return price > 0 ? DerivRules.Move(c.Kind, c.Qty, c.Mark, price) / 100 : 0;
            }
            return DerivRules.Money(c.Qty, price, false);
        }

        public static long ContractGain(DerivContract c, long worth) => c.Result / 100 + worth;

        public static void FuturesPage(StocksModel m, MarketDirector d, UiWords w)
        {
            MarketState s = d.State;
            DerivBook book = s.Derivs;
            var days = new int[DerivCatalogue.WeeksAhead];
            int open = 0;
            long worth = 0, gain = 0;
            var mine = new UiDataRow[book.Contracts.Count];
            for (int i = 0; i < book.Contracts.Count; i++)
            {
                DerivContract c = book.Contracts[i];
                long now = c.IsOpen ? ContractWorth(d, c) : 0;
                long made = c.IsOpen ? ContractGain(c, now) : c.Result / 100;
                if (c.IsOpen)
                {
                    open++;
                    worth += now;
                    gain += made;
                }
                mine[i] = Row(new[] { DerivText.Name(c), c.IsOpen ? DerivText.Kind(c.Kind) : DerivText.End(c.End), c.Qty.ToString("N0"),
                    c.IsOpen ? Money(now) : string.Empty, DerivText.Signed(made), c.SettleDay.ToString() },
                    (int)UiIcon.Chart, null, new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None,
                        made > 0 ? UiRole.OkText : made < 0 ? UiRole.DangerText : UiRole.None, UiRole.None });
            }
            m.Status = Status(d);
            m.Figures = new[]
            {
                Fig(w("reallife_stk_sh_open", "Open contracts"), open.ToString(), 0),
                Fig(w("reallife_stk_worth", "Worth"), Money(worth), 0),
                Fig(w("reallife_stk_sh_made", "Made so far"), DerivText.Signed(gain), gain > 0 ? 1 : gain < 0 ? -1 : 0),
                Fig(w("reallife_stk_sh_account", "Margin account"), Money(book.Balance / 100), 0)
            };
            if (book.CallDay > 0)
            {
                m.Actions = new[] { new UiActionModel { Label = w("reallife_mkt_drv_pay_call", "Pay the call"), Kind = UiButtonKind.Primary, Key = UiAction.Alt } };
                m.Status = Plain(MarketText.Fill("reallife_mkt_drv_box_call", "amount", Money(book.CallAmount), "day", book.CallDay.ToString()));
            }
            int dayCount = d.ContractDays(days);
            string next = dayCount > 0 ? w("reallife_mkt_drv_col_next", "Next week") : string.Empty;
            var stocks = new List<int>(16);
            OptionStocks(s, stocks);
            var rows = new UiDataRow[s.Materials.Count + stocks.Count];
            for (int i = 0; i < s.Materials.Count; i++)
            {
                RawMaterial mat = s.Materials[i];
                int day = MaterialRules.MoveBp(mat, 1);
                string week = dayCount > 0 ? DerivText.Unit(d.QuoteFuture(mat.Name, days[0], true, 1).Fair) : string.Empty;
                string used = d.Uses(mat.Name) ? "  " + MarketText.T("reallife_mkt_drv_row_uses") : string.Empty;
                rows[i] = Row(new[] { MaterialGame.Name(mat.Name) + used, DerivText.Group(mat.Group), DerivText.Unit(mat.Fix), MarketText.Change(day), week },
                    (int)UiIcon.Chart, MarketText.T("reallife_mkt_drv_filter_materials"), new[] { UiRole.None, UiRole.None, UiRole.None,
                        day > 0 ? UiRole.OkText : day < 0 ? UiRole.DangerText : UiRole.None, UiRole.None });
            }
            for (int i = 0; i < stocks.Count; i++)
            {
                Stock k = s.Stock(stocks[i]);
                int move = MarketHistory.DayMoveBp(k);
                rows[s.Materials.Count + i] = Row(new[] { k.Name, MarketText.Sector(k.Sector), MarketText.Price(k.Last), MarketText.Change(move), string.Empty },
                    (int)UiIcon.Building, k.Ticker, new[] { UiTokens.Chart((int)k.Sector), UiRole.None, UiRole.None,
                        move > 0 ? UiRole.OkText : move < 0 ? UiRole.DangerText : UiRole.None, UiRole.None });
            }
            m.TableTitle = w("reallife_mkt_bond_filter_offers", "On offer");
            m.Table2Title = w("reallife_stk_sh_your_contracts", "Your contracts");
            m.Table = new UiDataTableModel
            {
                Columns = new[]
                {
                    Col(w("reallife_mkt_drv_col_name", "Material or company"), UiCellKind.Tile), Col(w("reallife_mkt_drv_col_kind", "Group"), UiCellKind.Text, 192f),
                    Col(w("reallife_mkt_col_price", "Price"), UiCellKind.Figure, 112f), Col(w("reallife_mkt_col_today", "Today"), UiCellKind.Figure, 100f), Col(next, UiCellKind.Figure, 120f)
                },
                Rows = rows, RowCount = rows.Length, EmptyIcon = UiIcon.Chart,
                EmptyTitle = w("reallife_stk_fut_offer_empty", "Nothing on offer"), EmptyBody = MarketText.T(d.MaterialsLive ? "reallife_mkt_drv_box_help" : "reallife_mkt_drv_box_no_city")
            };
            m.Table2 = new UiDataTableModel
            {
                Columns = new[]
                {
                    Col(w("reallife_mkt_tab_futures", "Futures"), UiCellKind.Tile), Col(w("reallife_mkt_col_kind", "Kind"), UiCellKind.Text, 152f),
                        Col(w("reallife_stk_sh_qty", "Size"), UiCellKind.Figure, 100f),
                    Col(w("reallife_stk_worth", "Worth"), UiCellKind.Figure, 112f), Col(w("reallife_stk_sh_made", "Made so far"), UiCellKind.Figure,
                        120f), Col(w("reallife_stk_sh_settles", "Settles on day"), UiCellKind.Figure, 112f)
                },
                Rows = mine, RowCount = mine.Length, EmptyIcon = UiIcon.Chart,
                EmptyTitle = w("reallife_stk_fut_empty", "No futures yet"), EmptyBody = w("reallife_stk_fut_empty_body", "A future you open shows here until it settles.")
            };
        }

        // ------------------------------------------------------------------------------------------------ bots

        public static readonly string[] RuleKeys =
        {
            "reallife_stk_rule_dip", "reallife_stk_rule_rider", "reallife_stk_rule_div", "reallife_stk_rule_rebalance",
            "reallife_stk_rule_grid", "reallife_stk_rule_earn", "reallife_stk_rule_stop", "reallife_stk_rule_gap"
        };

        public static readonly string[] RuleEnglish = { "Dip buyer", "Trend rider", "Dividend", "Rebalance", "Grid", "Earnings", "Stop guard", "Open gap" };

        /// <summary>The player's rules first, then the rivals': the order of the Bots page's lower table.</summary>
        public static void RuleOrder(MarketState s, List<int> into)
        {
            into.Clear();
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < s.Rules.Count; i++)
                {
                    if ((s.Rules[i].Owner == 0) == (pass == 0))
                    {
                        into.Add(i);
                    }
                }
            }
        }

        public static string RuleName(int kind, UiWords w)
        {
            int i = kind >= 0 && kind < RuleKeys.Length ? kind : 0;
            return w(RuleKeys[i], RuleEnglish[i]);
        }

        public static string[] ChipWords(UiWords w) => new[]
        {
            w("reallife_stk_running", "Running"), w("reallife_stk_waiting", "Waiting"), w("reallife_stk_paused", "Paused"),
            w("reallife_stk_out_budget", "Out of budget"), w("reallife_stk_stopped", "Stopped"), w("reallife_mkt_bot_status_selling", "Selling up")
        };

        private static readonly UiRole[] ChipTones = { UiRole.Ok, UiRole.None, UiRole.Warn, UiRole.Danger, UiRole.Muted, UiRole.Warn };

        public static void BotsPage(StocksModel m, MarketDirector d, UiWords w)
        {
            MarketState s = d.State;
            string[] chips = ChipWords(w);
            long budget = 0, day = 0, week = 0;
            var firms = new UiDataRow[MarketBots.All.Length];
            for (int i = 0; i < MarketBots.All.Length; i++)
            {
                BotFirm f = MarketBots.All[i];
                Bot b = MarketBots.Hired(s, f.Id);
                string capital = string.Empty, worthText = string.Empty, gainText = string.Empty;
                int chip = -1;
                UiRole tone = UiRole.Muted;
                string state = MarketText.T("reallife_mkt_bot_status_free");
                if (b != null)
                {
                    long now = MarketBots.Worth(s, b, s.Phase);
                    long gain = now + b.PaidOut - b.Capital;
                    budget += b.Capital;
                    day += b.Realised;
                    capital = Money(b.Capital);
                    worthText = Money(now);
                    gainText = Money(gain);
                    chip = StocksMath.BotChip(b);
                    state = chips[chip < 0 || chip >= chips.Length ? 0 : chip];
                    tone = ChipTones[chip < 0 || chip >= ChipTones.Length ? 0 : chip];
                }
                firms[i] = Row(new[] { f.Name, MarketText.Percent(f.FeeBp), capital, worthText, gainText, state }, (int)UiIcon.Cpu, MarketText.T(StrategyKeys[(int)f.Strategy]),
                    new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, tone });
            }
            var order = new List<int>(12);
            RuleOrder(s, order);
            var rules = new UiDataRow[order.Count];
            for (int i = 0; i < order.Count; i++)
            {
                RuleBot r = s.Rules[order[i]];
                Stock k = s.Stock(r.StockId);
                if (r.Owner == 0)
                {
                    budget += r.Budget;
                    day += r.DayResult;
                    week += r.WeekResult;
                }
                int c = r.Chip < 0 || r.Chip >= chips.Length ? 0 : r.Chip;
                rules[i] = Row(new[] { RuleName((int)r.Kind, w), k != null ? k.Ticker : string.Empty, Money(r.Budget), Money(r.DayResult),
                    Money(r.WeekResult), r.Owner == 0 ? chips[c] : w("reallife_stk_rival", "A rival") },
                    (int)UiIcon.Cpu, r.Owner == 0 ? null : w("reallife_stk_rival", "A rival"),
                    new[] { UiRole.None, UiRole.None, UiRole.None,
                        r.DayResult > 0 ? UiRole.OkText : r.DayResult < 0 ? UiRole.DangerText : UiRole.None,
                        r.WeekResult > 0 ? UiRole.OkText : r.WeekResult < 0 ? UiRole.DangerText : UiRole.None, r.Owner == 0 ? ChipTones[c] : UiRole.Muted });
            }
            m.Status = Status(d);
            m.Figures = new[]
            {
                Fig(w("reallife_stk_budget", "Budget in use"), Money(budget), 0),
                Fig(w("reallife_stk_day_result", "Today"), DerivText.Signed(day), day > 0 ? 1 : day < 0 ? -1 : 0),
                Fig(w("reallife_stk_sh_this_week", "This week"), DerivText.Signed(week), week > 0 ? 1 : week < 0 ? -1 : 0)
            };
            m.Actions = new[]
            {
                new UiActionModel
                {
                    Label = w("reallife_stk_add_rule", "Add a rule"), Kind = UiButtonKind.Primary, Icon = UiIcon.Plus, Key = UiAction.Option,
                    Disabled = RuleBots.PlayerCount(s) >= RuleBots.PlayerMost
                }
            };
            m.TableTitle = w("reallife_stk_sh_firms", "Trading firms");
            m.Table2Title = w("reallife_stk_sh_rules", "Rules");
            m.Table = new UiDataTableModel
            {
                Columns = new[]
                {
                    Col(w("reallife_mkt_bot_col_firm", "Firm"), UiCellKind.Tile), Col(w("reallife_stk_sh_fee_month", "Fee a month"),
                        UiCellKind.Figure, 112f), Col(w("reallife_mkt_bot_col_capital", "Capital"), UiCellKind.Figure, 112f),
                    Col(w("reallife_mkt_col_worth", "Worth"), UiCellKind.Figure, 112f), Col(w("reallife_mkt_bot_col_profit", "Profit"),
                        UiCellKind.Figure, 112f), Col(w("reallife_mkt_col_state", "State"), UiCellKind.Chip, 152f)
                },
                Rows = firms, RowCount = firms.Length, EmptyIcon = UiIcon.Cpu
            };
            m.Table2 = new UiDataTableModel
            {
                Columns = new[]
                {
                    Col(w("reallife_stk_sh_rule", "Rule"), UiCellKind.Tile), Col(w("reallife_stk_sh_company", "Company"), UiCellKind.Text, 100f),
                        Col(w("reallife_stk_sh_budget", "Budget"), UiCellKind.Figure, 112f),
                    Col(w("reallife_stk_day_result", "Today"), UiCellKind.Figure, 100f), Col(w("reallife_stk_sh_this_week", "This week"),
                        UiCellKind.Figure, 100f), Col(w("reallife_mkt_col_state", "State"), UiCellKind.Chip, 152f)
                },
                Rows = rules, RowCount = rules.Length, EmptyIcon = UiIcon.Cpu,
                EmptyTitle = w("reallife_stk_bots_empty", "No bots yet"), EmptyBody = w("reallife_stk_bots_empty_body", "Hire a firm, or add a rule. Rivals run the same rules.")
            };
        }

        // ------------------------------------------------------------------------------------------------ rule sheet

        public static readonly int[] RuleGain = { 200, 400, 600, 800, 1000, 1500, 2000 };
        public static readonly int[] RuleStops = { 100, 200, 300, 500, 800, 1000, 1500 };
        public static readonly int[] RuleMaxOpen = { 1, 2, 3, 4, 5, 6, 8 };

        /// <summary>The companies a rule may be written on: listed, not another's own: the held and watched first, then the market's order.</summary>
        public static void RuleCompanies(MarketState s, List<int> into)
        {
            into.Clear();
            for (int i = 0; i < s.Positions.Count; i++)
            {
                AddRuleStock(s, s.Positions[i].StockId, into);
            }
            for (int i = 0; i < s.Watch.Count; i++)
            {
                AddRuleStock(s, s.Watch[i], into);
            }
            for (int i = 0; i < s.Stocks.Count; i++)
            {
                AddRuleStock(s, s.Stocks[i].Id, into);
            }
        }

        private static void AddRuleStock(MarketState s, int id, List<int> into)
        {
            Stock k = s.Stock(id);
            if (k != null && k.Listed && k.Own == null && !into.Contains(id))
            {
                into.Add(id);
            }
        }

        /// <summary>The level a kind of rule reads, in its own unit: RSI, a price in cents, a gap in basis points; none for the rest.</summary>
        public static bool HasLevel(RuleKind kind) => kind == RuleKind.Dip || kind == RuleKind.Grid || kind == RuleKind.StopGuard || kind == RuleKind.Gap;

        /// <summary>A rule's first level for a company: RSI 30, a gap of 2%, a price a tenth under today's.</summary>
        public static int DefaultLevel(RuleKind kind, Stock stock)
        {
            switch (kind)
            {
                case RuleKind.Dip: return 30;
                case RuleKind.Gap: return 200;
                case RuleKind.Grid: return Math.Max(1, stock.Last * 90 / 100);
                default: return Math.Max(1, stock.Last);
            }
        }

        public static StocksSheetModel RuleSheet(MarketDirector d, StocksSheetState st, UiWords w, List<int> companies, bool adding)
        {
            MarketState s = d.State;
            RuleBot r = adding ? null : st.Key >= 0 && st.Key < s.Rules.Count ? s.Rules[st.Key] : null;
            StocksSheetModel m = Frame(w, w("reallife_mkt_tab_bots", "Bots"), w(adding ? "reallife_stk_sh_new_rule" : "reallife_stk_sh_rule", adding ? "New rule" : "Rule"), UiIcon.Cpu);
            if (!adding && r == null)
            {
                m.Name = w("reallife_stk_none", "None");
                return m;
            }
            RuleKind kind = adding ? (RuleKind)Idx(st.V[StocksSheets.RuleKind], 8) : r.Kind;
            Stock stock = companies.Count > 0 ? s.Stock(companies[Idx(st.V[StocksSheets.RuleStock], companies.Count)]) : null;
            if (!adding)
            {
                stock = s.Stock(r.StockId);
            }
            m.Name = RuleName((int)kind, w);
            m.Big = r != null ? Money(r.DayResult) : null;
            m.Sub = stock != null ? stock.Name + "  " + stock.Ticker : string.Empty;
            m.About = RuleAbout(kind, w);
            if (r != null)
            {
                string[] chips = ChipWords(w);
                m.SideLabels = new[]
                {
                    w("reallife_mkt_col_state", "State"), w("reallife_stk_day_result", "Today"), w("reallife_stk_sh_this_week", "This week"), w("reallife_stk_sh_open_trades", "Open trades"),
                    w("reallife_stk_sh_cash_left", "Budget left")
                };
                m.SideValues = new[] { chips[r.Chip < 0 || r.Chip >= chips.Length ? 0 : r.Chip], Money(r.DayResult), Money(r.WeekResult), r.OpenTrades.ToString(), Money(r.Cash) };
            }
            if (r != null && r.Owner != 0)
            {
                m.Reason = w("reallife_stk_sh_rival_rule", "A rival's rule. It trades by the same rules as yours and never touches your cash.");
                m.ReasonGood = true;
                return m;
            }
            var f = new List<StocksFieldModel>(14);
            if (adding)
            {
                f.Add(PickField(StocksSheets.RuleKind, w("reallife_stk_sh_rule", "Rule"), RuleName((int)kind, w), 8));
                f.Add(PickField(StocksSheets.RuleStock, w("reallife_stk_sh_company", "Company"), stock != null ? stock.Name + "  " + stock.Ticker : "-", companies.Count));
            }
            if (HasLevel(kind))
            {
                long min = kind == RuleKind.Dip ? 5 : kind == RuleKind.Gap ? 50 : 1;
                long max = kind == RuleKind.Dip ? 50 : kind == RuleKind.Gap ? 2000 : Math.Max(100, (stock != null ? stock.Last : 100) * 5L);
                long step = kind == RuleKind.Dip ? 1 : kind == RuleKind.Gap ? 50 : Math.Max(1, (stock != null ? stock.Last : 100) / 100);
                long v = Math.Max(min, Math.Min(max, st.V[StocksSheets.Level]));
                string text = kind == RuleKind.Dip ? v.ToString() : kind == RuleKind.Gap ? MarketText.Percent((int)v) : MarketText.Price((int)v);
                f.Add(StepField(StocksSheets.Level,
                    w(kind == RuleKind.Dip ? "reallife_stk_sh_rsi_under" : kind == RuleKind.Gap ? "reallife_stk_sh_gap_of" : "reallife_stk_sh_level",
                    "Level"), text, v, min, max, step, false));
            }
            if (kind == RuleKind.Rebalance || kind == RuleKind.Grid || kind == RuleKind.Gap)
            {
                int gi = Idx(st.V[StocksSheets.Gain], RuleGain.Length);
                f.Add(PickField(StocksSheets.Gain, w("reallife_stk_sh_gain_target", "Sell at a gain of"), MarketText.Percent(RuleGain[gi]), RuleGain.Length));
            }
            f.Add(PickField(StocksSheets.OrderKind, w("reallife_stk_sh_order_type", "Order type"), w(Idx(st.V[StocksSheets.OrderKind],
                2) == 0 ? "reallife_stk_market_order" : "reallife_stk_limit", Idx(st.V[StocksSheets.OrderKind], 2) == 0 ? "Market" : "Limit"), 2));
            long cash = Math.Max(1000L, d.Cash);
            long budget = st.V[StocksSheets.Budget];
            f.Add(StepField(StocksSheets.Budget, w("reallife_stk_sh_budget", "Budget"), Money(budget), budget, 100L, cash, Nice(Math.Max(1000L, cash) / 20, 100L), true));
            long per = Math.Max(1L, Math.Min(100000L, st.V[StocksSheets.PerTrade]));
            f.Add(StepField(StocksSheets.PerTrade, w("reallife_stk_sh_per_trade", "Shares a trade"), per.ToString("N0"), per, 1L, 100000L, per >= 100 ? 10L : 1L, false));
            if (kind == RuleKind.StopGuard)
            {
                int si = Idx(st.V[StocksSheets.RuleStop], RuleStops.Length);
                f.Add(PickField(StocksSheets.RuleStop, w("reallife_stk_sh_stop_loss", "Stop loss"), "-" + MarketText.Percent(RuleStops[si]), RuleStops.Length));
            }
            long mo = Math.Max(1, Math.Min(8, st.V[StocksSheets.MaxOpen]));
            f.Add(StepField(StocksSheets.MaxOpen, w("reallife_stk_sh_max_open", "Most open"), mo.ToString(), mo, 1L, 8L, 1L, false));
            f.Add(SwitchField(StocksSheets.OpenOnly, w("reallife_stk_sh_only_open", "Only while open"), st.V[StocksSheets.OpenOnly] != 0, w));
            f.Add(SwitchField(StocksSheets.RuleReinvest, w("reallife_stk_sh_reinvest", "Reinvest gains"), st.V[StocksSheets.RuleReinvest] != 0, w));
            f.Add(SwitchField(StocksSheets.Tell, w("reallife_stk_sh_tell_me", "Tell me"), st.V[StocksSheets.Tell] != 0, w));
            m.Fields = f.ToArray();
            bool full = adding && RuleBots.PlayerCount(s) >= RuleBots.PlayerMost;
            m.Confirm = w(adding ? "reallife_stk_sh_add_rule" : "reallife_stk_sh_save", adding ? "Add the rule" : "Save changes");
            m.ConfirmOff = full || (adding && stock == null);
            m.Reason = full ? w("reallife_stk_sh_rules_full", "You run as many rules as you can.") : null;
            if (!adding)
            {
                var extras = new List<UiActionModel>(2);
                extras.Add(Extra(w(r.Status == RuleStatus.Running ? "reallife_stk_sh_pause" : "reallife_stk_sh_resume", r.Status == RuleStatus.Running ? "Pause" : "Resume"), false));
                extras.Add(Armed(Extra(w("reallife_stk_sh_remove", "Remove"), true), st.Armed == 1));
                m.Extras = extras.ToArray();
            }
            return m;
        }

        private static string RuleAbout(RuleKind kind, UiWords w)
        {
            switch (kind)
            {
                case RuleKind.Dip: return w("reallife_stk_about_dip", "Buys while the 14 day RSI is under the level, and sells when it climbs over 70.");
                case RuleKind.Rider: return w("reallife_stk_about_rider", "Buys while the 20 day average is over the 50 day average, and sells when it falls under.");
                case RuleKind.Dividend: return w("reallife_stk_about_div", "Buys a company that pays a dividend, and sells when it stops.");
                case RuleKind.Rebalance: return w("reallife_stk_about_rebalance", "Buys up to its budget and sells a gain.");
                case RuleKind.Grid: return w("reallife_stk_about_grid", "Buys under its level and sells the gain above it.");
                case RuleKind.Earnings: return w("reallife_stk_about_earn", "Buys after a profitable quarter, and sells after a loss.");
                case RuleKind.StopGuard: return w("reallife_stk_about_stop", "Sells a holding that falls through its stop.");
                default: return w("reallife_stk_about_gap", "Buys a gap down at the open, and sells a gain.");
            }
        }

        // ------------------------------------------------------------------------------------------------ orders

        /// <summary>The orders the table lists, newest first: the open ones, or the done.</summary>
        public static void OrderRows(MarketState s, bool done, List<int> into)
        {
            into.Clear();
            for (int i = s.Orders.Count - 1; i >= 0; i--)
            {
                if (s.Orders[i].IsOpen != done)
                {
                    into.Add(s.Orders[i].Id);
                }
            }
        }

        private static readonly string[] OrderSideKeys = { "reallife_mkt_side_buy", "reallife_mkt_side_sell" };
        private static readonly string[] OrderTypeKeys = { "reallife_mkt_type_market", "reallife_mkt_type_limit", "reallife_mkt_type_stop" };
        private static readonly string[] OrderStateKeys =
        {
            "reallife_mkt_state_open", "reallife_mkt_state_filled", "reallife_mkt_state_cancelled", "reallife_mkt_state_expired", "reallife_mkt_state_refused"
        };

        /// <summary>An order's kind in words: a stop loss and a take profit by their own names, a worked order and a dividend put back by theirs.</summary>
        public static string OrderKindWords(RealLifeOverhaul.Markets.Order o)
        {
            if (o.Worked || o.Back)
            {
                return MarketText.T(o.Worked ? "reallife_mkt_inv_type_worked" : "reallife_mkt_inv_type_back");
            }
            if (o.Closing && o.Type != OrderType.Market)
            {
                return MarketText.T(o.Type == OrderType.Stop ? "reallife_mkt_type_stop_loss" : "reallife_mkt_type_take_profit");
            }
            return MarketText.T(OrderTypeKeys[Math.Max(0, Math.Min(OrderTypeKeys.Length - 1, (int)o.Type))]);
        }

        /// <summary>An order's state: a worked order open is working, and one stopped before it was done, not by the player, is stopped.</summary>
        public static string OrderStateWords(RealLifeOverhaul.Markets.Order o)
        {
            if (o.Worked && o.IsOpen)
            {
                return MarketText.T("reallife_mkt_inv_state_working");
            }
            bool stopped = o.Worked && !o.IsOpen && o.State != OrderState.Filled && o.Why != OrderWhy.Cancelled;
            return MarketText.T(stopped ? "reallife_mkt_inv_state_stopped" : OrderStateKeys[Math.Max(0, Math.Min(OrderStateKeys.Length - 1, (int)o.State))]);
        }

        public static void OrdersPage(StocksModel m, MarketState s, UiWords w, bool done)
        {
            var ids = new List<int>(16);
            OrderRows(s, done, ids);
            var rows = new UiDataRow[ids.Count];
            for (int i = 0; i < ids.Count; i++)
            {
                RealLifeOverhaul.Markets.Order o = s.Order(ids[i]);
                Stock k = s.Stock(o.StockId);
                int cents = o.Type == OrderType.Limit ? o.LimitCents : o.Type == OrderType.Stop ? o.StopCents : o.Filled > 0 ? (int)(o.FilledValue * 100 / o.Filled) : 0;
                string shares = o.Worked || (o.Filled > 0 && o.Filled < o.Qty) ? o.Filled.ToString("N0") + " / " + o.Qty.ToString("N0") : o.Qty.ToString("N0");
                string lasts = o.Worked ? MarketText.T("reallife_mkt_inv_life_done")
                    : o.Life == OrderLife.Day ? MarketText.T("reallife_mkt_life_day") : MarketText.Fill("reallife_mkt_until_day", "day", o.ExpireDay.ToString());
                rows[i] = Row(new[]
                {
                    k != null ? k.Name : string.Empty, MarketText.T(OrderSideKeys[(int)o.Side]) + "  " + OrderKindWords(o), shares, cents > 0 ? MarketText.Price(cents) : "-", lasts,
                    OrderStateWords(o), o.PlacedDay.ToString()
                }, (int)UiIcon.Receipt, k != null ? k.Ticker : null,
                    new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None,
                        o.State == OrderState.Filled ? UiRole.Ok : o.State == OrderState.Refused ? UiRole.Danger : o.IsOpen ? UiRole.None : UiRole.Muted, UiRole.None });
            }
            m.Status = null;
            m.Table = new UiDataTableModel
            {
                Columns = new[]
                {
                    Col(w("reallife_mkt_col_company", "Company"), UiCellKind.Tile), Col(w("reallife_mkt_col_kind", "Kind"), UiCellKind.Text, 192f),
                        Col(w("reallife_mkt_col_shares", "Shares"), UiCellKind.Figure, 112f),
                    Col(w("reallife_mkt_col_price", "Price"), UiCellKind.Figure, 100f), Col(w("reallife_mkt_col_lasts", "Lasts"), UiCellKind.Text,
                        120f), Col(w("reallife_mkt_col_state", "State"), UiCellKind.Chip, 120f),
                    Col(w("reallife_stk_sh_placed", "Placed on day"), UiCellKind.Figure, 88f)
                },
                Rows = rows, RowCount = rows.Length, EmptyIcon = UiIcon.Receipt,
                Filters = new[] { w(done ? "reallife_mkt_filter_done" : "reallife_mkt_filter_open", done ? "Done" : "Open") },
                EmptyTitle = w(done ? "reallife_stk_hist_empty" : "reallife_stk_orders_empty", done ? "No trades yet" : "No open orders"),
                EmptyBody = w(done ? "reallife_stk_hist_empty_body" : "reallife_stk_orders_empty_body",
                    done ? "Fills, dividends and cash you move show here." : "An order you place and that has not filled shows here.")
            };
        }
    }
}
