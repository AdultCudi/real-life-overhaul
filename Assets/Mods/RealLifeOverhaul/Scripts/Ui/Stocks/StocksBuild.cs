using System;
using System.Collections.Generic;
using RealLifeOverhaul.Markets;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The Stocks desk filled from the market the save already has. Words through the mod's keys. Built when the desk
    /// changes, not every frame. The old page stays for the way back.
    /// </summary>
    public static partial class StocksBuild
    {
        public static readonly string[] NavKeys =
        {
            "reallife_mkt_tab_market", "reallife_mkt_tab_companies", "reallife_mkt_tab_watch", "reallife_mkt_tab_portfolio",
            "reallife_mkt_tab_orders", "reallife_mkt_tab_history", "reallife_mkt_tab_bots", "reallife_stk_nav_listing",
            "reallife_mkt_tab_bonds", "reallife_mkt_tab_futures"
        };

        public static readonly UiIcon[] NavIcons =
        {
            UiIcon.ChartLineUp, UiIcon.Building, UiIcon.Eye, UiIcon.ChartPie, UiIcon.Receipt, UiIcon.Clock, UiIcon.Cpu,
            UiIcon.Presentation, UiIcon.Bank, UiIcon.Chart
        };

        public static readonly StocksPage[] NavPages =
        {
            StocksPage.Market, StocksPage.Companies, StocksPage.Watch, StocksPage.Portfolio, StocksPage.Orders, StocksPage.History,
            StocksPage.Bots, StocksPage.Listing, StocksPage.Bonds, StocksPage.Futures
        };

        public static int NavOf(StocksPage page)
        {
            for (int i = 0; i < NavPages.Length; i++)
            {
                if (NavPages[i] == page)
                {
                    return i;
                }
            }
            return page == StocksPage.Company || page == StocksPage.Ticket ? 1 : 0;
        }

        public static void Nav(StocksModel m, UiWords words)
        {
            m.Nav = new UiNavEntry[NavKeys.Length];
            for (int i = 0; i < NavKeys.Length; i++)
            {
                m.Nav[i] = new UiNavEntry { Icon = NavIcons[i], Tint = UiRole.None, Label = words(NavKeys[i], NavKeys[i]) };
            }
            m.Groups = new[] { words("reallife_stk_group_markets", "Markets"), words("reallife_stk_group_yours", "Yours"), words("reallife_stk_group_other", "Other markets") };
            m.GroupAt = new[] { 0, 3, 8 };
            m.NavAt = NavOf(m.Page);
            m.PromptBack = words("reallife_ui_prompt_back", "Back");
            m.PromptMove = words("reallife_ui_prompt_move", "Move");
            m.PromptSelect = words("reallife_ui_prompt_select", "Select");
        }

        public static StocksModel Page(MarketState state, StocksPage page, int stockId, StocksView view, bool buy, OrderType type,
            OrderLife life, long qty, int sort, int sector, int size, int rule, string find, UiWords words,
            MarketDirector director = null, bool done = false, int kind = 0, int price = 0, int portfolioView = 0, int period = 0)
        {
            Built++;
            var m = new StocksModel { Title = words("reallife_mkt_app", "Stocks"), Page = page };
            Nav(m, words);
            if (state == null)
            {
                m.EmptyTitle = words("reallife_stk_empty_title", "The market is not open");
                m.EmptyBody = words("reallife_stk_empty_body", "Stocks opens with a city. Prices, orders and the tape show here.");
                return m;
            }
            switch (page)
            {
                case StocksPage.Companies: m.Table = Companies(state, words, false, sort, sector, size, find); break;
                case StocksPage.Watch: m.Table = Companies(state, words, true, sort, sector, size, find); break;
                case StocksPage.Market: Market(m, state, words, sort, sector, size, find); OpenOrders(state, m, words); break;
                case StocksPage.Company: Company(m, state, stockId, view ?? new StocksView(), buy, life, qty, words, kind, price, director); break;
                case StocksPage.Ticket: Ticket(m, state, stockId, buy, type, life, qty, words); break;
                case StocksPage.Portfolio: Portfolio(m, state, words, director, portfolioView, period); break;
                case StocksPage.Orders: OrdersPage(m, state, words, done); break;
                case StocksPage.History: HistoryPage(m, state, words); break;
                case StocksPage.Bots: if (director != null) { BotsPage(m, director, words); } break;
                case StocksPage.Listing: Listing(m, state, words); break;
                case StocksPage.Bonds: if (director != null) { BondsPage(m, director, words); } break;
                default: if (director != null) { FuturesPage(m, director, words); } break;
            }
            if (director != null)
            {
                PageButtons(m, director, words);
            }
            return m;
        }

        private static void Market(StocksModel m, MarketState state, UiWords words, int sort, int sector, int size, string find)
        {
            StocksMath.Today(state, out int up, out int down, out long turnover);
            m.Figures = new[]
            {
                Fig(words("reallife_mkt_col_index", "Index"), state.IndexLast.ToString(), StocksMath.ChangeBp(state.IndexPrevClose, state.IndexLast)),
                Fig(words("reallife_stk_small", "Small caps"), ((int)StocksMath.ValueOf(state.Stocks, (int)MarketSize.Small)).ToString(), 0),
                Fig(words("reallife_stk_all", "All shares"), ((int)StocksMath.ValueOf(state.Stocks, -1)).ToString(), 0),
                Fig(words("reallife_stk_today", "Market today"), up + " / " + down + " / " + MarketText.Large(turnover), 0)
            };
            if (state.Positions.Count > 0)
            {
                long mine = StocksFigures.YourDay(state);
                var more = new UiFigureModel[m.Figures.Length + 1];
                Array.Copy(m.Figures, more, m.Figures.Length);
                more[m.Figures.Length] = new UiFigureModel { Label = words("reallife_stk_your_day", "Your day"), Value = (mine > 0 ? "+" : mine < 0 ? "-" : string.Empty) + MarketText.Money(Math.Abs(mine)), Good = mine >= 0 };
                m.Figures = more;
            }
            m.TapeTitle = words("reallife_stk_tape", "Who is trading");
            m.Tape = Tape(state, 0, 3, words);
            m.Table = Companies(state, words, false, sort, sector, size, find);
            if (m.Table.RowCount == 0 && string.IsNullOrWhiteSpace(find))
            {
                m.Table.EmptyTitle = words("reallife_stk_market_empty", "No companies are listed");
                m.Table.EmptyBody = words("reallife_stk_market_empty_body", "Listed companies show here with the day's move.");
            }
        }


        private static UiFigureModel Fig(string label, string value, int bp)
        {
            return new UiFigureModel { Label = label, Value = value, Change = bp == 0 ? null : MarketText.Change(bp), Good = bp >= 0 };
        }

        private static UiDataTableModel Companies(MarketState state, UiWords words, bool watch, int sort, int sector, int size, string find)
        {
            var table = Head(words);
            table.SortColumn = 2;
            table.SortDown = sort != (int)MarketSort.Losers;
            string[] sizes = { words("reallife_mkt_size_large", "Large"), words("reallife_mkt_size_mid", "Mid"), words("reallife_mkt_size_small", "Small") };
            string sectorWord = sector >= 0 && sector < (int)MarketSector.Count ? MarketText.Sector((MarketSector)sector) : words("reallife_stk_filter_all", "All sectors");
            string sizeWord = size >= 0 && size < sizes.Length ? sizes[size] : words("reallife_stk_all", "All shares");
            table.Filters = new[] { SortWord(sort, words), sectorWord, sizeWord };
            int n = 0;
            var rows = new UiDataRow[state.Stocks.Count];
            var order = new int[state.Stocks.Count];
            int listed = StocksMath.Order(state.Stocks, (MarketSort)(sort >= 0 && sort <= 3 ? sort : 0), sector, size, order);
            for (int k = 0; k < listed; k++)
            {
                Stock s = state.Stocks[order[k]];
                if (watch && !state.Watching(s.Id))
                {
                    continue;
                }
                if (!StocksMath.Matches(s, find))
                {
                    continue;
                }
                int bp = StocksMath.ChangeBp(s.PrevClose, s.Last);
                var spark = new float[SparkDays];
                int days = StocksMath.Spark(s.Daily, SparkDays, spark);
                if (days > 1)
                {
                    Array.Resize(ref spark, days);
                }
                else
                {
                    spark = null;
                }
                bool rising = spark != null && spark[spark.Length - 1] >= spark[0];
                rows[n++] = new UiDataRow
                {
                    Cells = new[] { s.Name, MarketText.Price(s.Last), MarketText.Change(bp), string.Empty, s.DayVolume.ToString() },
                    Values = new[] { (int)UiIcon.Building, 0, bp > 0 ? 1 : bp < 0 ? -1 : 0, 0, 0 },
                    Tones = new[] { UiTokens.Chart((int)s.Sector), UiRole.None, UiRole.None, rising ? UiRole.Ok : UiRole.Danger, UiRole.None },
                    Sub = s.Ticker, Line = spark
                };
            }
            table.Rows = rows;
            table.RowCount = n;
            table.Query = find;
            bool searching = !string.IsNullOrWhiteSpace(find);
            if (searching)
            {
                table.Count = MarketText.Fill("reallife_stk_search_count", "shown", n.ToString(), "all", listed.ToString());
            }
            if (n == 0 && searching)
            {
                table.EmptyTitle = words("reallife_stk_search_none", "No company matches");
                table.EmptyBody = words("reallife_stk_search_none_body", "Nothing here has that in its name or ticker.");
                table.EmptyAction = words("reallife_stk_search_clear", "Clear search");
            }
            else if (n == 0)
            {
                table.EmptyTitle = words(watch ? "reallife_stk_watch_empty" : "reallife_stk_market_empty", watch ? "Nothing on the watch yet" : "No companies are listed");
                table.EmptyBody = words(watch ? "reallife_stk_watch_empty_body" : "reallife_stk_market_empty_body",
                    watch ? "Open a company and press Watch. It stays in this short list." : "Listed companies show here with the day's move.");
            }
            return table;
        }

        public const int SparkDays = 5;   // the days of the spark beside a company in the table

        private static readonly string[] SortKeys = { "reallife_stk_sort_movers", "reallife_stk_sort_gainers", "reallife_stk_sort_losers", "reallife_stk_sort_active" };
        private static readonly string[] SortEnglish = { "Movers", "Gainers", "Losers", "Most active" };

        /// <summary>The table's order in words: the first of its three filters says which of the four sorts is on.</summary>
        public static string SortWord(int sort, UiWords words)
        {
            int i = sort >= 0 && sort < SortKeys.Length ? sort : 0;
            return words(SortKeys[i], SortEnglish[i]);
        }

        private static UiDataTableModel Head(UiWords words)
        {
            return new UiDataTableModel
            {
                Columns = new[]
                {
                    new UiDataColumn { Title = words("reallife_mkt_col_company", "Company"), Kind = UiCellKind.Tile, Sortable = true },
                    new UiDataColumn { Title = words("reallife_mkt_col_price", "Price"), Kind = UiCellKind.Figure, Width = 120f, Sortable = true },
                    new UiDataColumn { Title = words("reallife_mkt_col_today", "Today"), Kind = UiCellKind.Trend, Width = 140f, Sortable = true },
                    new UiDataColumn { Title = words("reallife_stk_five_days", "5 days"), Kind = UiCellKind.Spark, Width = 120f },
                    new UiDataColumn { Title = words("reallife_mkt_col_volume", "Volume"), Kind = UiCellKind.Figure, Width = 120f, Sortable = true }
                },
                Filters = new[] { words("reallife_stk_sort_movers", "Movers"), words("reallife_stk_filter_all", "All sectors"), words("reallife_stk_all", "All shares") },
                SearchPlaceholder = words("reallife_stk_search", "Find a company"),
                EmptyIcon = UiIcon.ChartLineUp
            };
        }

        private static readonly string[] KindKeys = { "reallife_stk_market_order", "reallife_stk_limit", "reallife_mkt_type_stop_loss", "reallife_mkt_type_take_profit" };
        private static readonly string[] KindEnglish = { "Market", "Limit", "Stop loss", "Take profit" };

        /// <summary>The order type a ticket kind places: market, limit, a stop loss is a stop, a take profit a limit.</summary>
        public static OrderType TypeOf(int kind) => kind == 0 ? OrderType.Market : kind == 2 ? OrderType.Stop : OrderType.Limit;

        /// <summary>
        /// A limit's or a stop's price to begin with, as the old ticket takes it: a limit at the offer or bid, a stop loss a twentieth
        /// beyond, a take profit a tenth; the price typed once one was. Cents.
        /// </summary>
        public static int TicketPrice(MarketState state, Stock s, bool sell, int kind, int typed)
        {
            if (typed > 0)
            {
                return typed;
            }
            Quote q = MarketTrading.QuoteOf(state, s, state.Phase);
            if (kind == 2)
            {
                return sell ? Math.Max(1, q.Bid * 95 / 100) : q.Ask * 105 / 100;
            }
            if (kind == 3)
            {
                return sell ? q.Bid * 110 / 100 : Math.Max(1, q.Ask * 90 / 100);
            }
            return sell ? q.Bid : q.Ask;
        }

        /// <summary>A position as the ticket says it: its shares at their average cost ("50 at $10.00"), or none.</summary>
        public static string PositionWords(long shares, long averageCents)
        {
            return shares != 0 ? MarketText.Fill("reallife_stk_position_value", "shares", shares.ToString("N0"), "price", MarketText.Price((int)Math.Min(int.MaxValue, averageCents))) : "0";
        }

        private static StocksTicket TicketOf(MarketState state, Stock s, bool buy, OrderType type, OrderLife life, long qty, UiWords words, int kind = 0, int typedPrice = 0, MarketDirector d = null)
        {
            if (qty < 1)
            {
                qty = 1;
            }
            long held = state.Held(s.Id);
            bool closing = kind >= 2;
            if (closing)
            {
                buy = held < 0;
            }
            type = TypeOf(kind);
            int price = Math.Max(1, s.Last);
            int limit = TicketPrice(state, s, !buy, kind, typedPrice);
            long value = MarketTrading.Value(qty, price, buy);
            int least = d != null ? d.CommissionLeast : MarketCatalogue.CommissionLeastAtStart;   // the fee rule's floor, at today's prices
            long fee = MarketTrading.Commission(value, least);
            long total = buy ? value + fee : Math.Max(0, value - fee);
            string why = null;
            bool worked = false;
            if (d != null)
            {
                Estimate e = d.Estimate(s.Id, buy ? OrderSide.Buy : OrderSide.Sell, type, qty, type == OrderType.Limit ? limit : 0, type == OrderType.Stop ? limit : 0, closing);
                if (e.Why == OrderWhy.None)
                {
                    value = e.Value;
                    fee = e.Fee;
                    total = e.Total;
                }
                else if (!(e.Why == OrderWhy.TooLarge && type == OrderType.Market && !closing && Settings.MarketsLarge))
                {
                    why = MarketText.Why(e.Why, s);
                }
                else
                {
                    worked = true;
                    why = MarketText.Fill("reallife_mkt_inv_ticket_worked", "most", MarketTrading.MostPerOrder(s).ToString("N0"), "days", InvestRules.MarketDays(qty, s.Adv).ToString());
                }
            }
            // The cash an order spends is the player's and the broker's (what Estimate counts), not the margin account's own money.
            long cash = d != null ? d.Spendable : state.Account.Cash;
            long most = buy ? StocksMath.MostToBuy(cash, type == OrderType.Market ? price : limit, least) : Math.Max(0L, held);
            Position position = state.Position(s.Id);
            StocksFigures.PositionAfter(held, position != null ? position.Cost : 0, buy, qty, total, out long afterShares, out long average);
            int back = position != null && position.Shares > 0 && Settings.MarketsReinvest ? InvestRules.ReinvestOf(position.Reinvest) : -1;
            string[] priceKeys = { null, "reallife_stk_limit_price", "reallife_stk_stop_price", "reallife_stk_profit_price" };
            string[] priceEnglish = { null, "Limit price", "Stop price", "Take profit price" };
            var rows = new List<StocksRow>(5);
            if (type == OrderType.Market)
            {
                rows.Add(new StocksRow { Label = words("reallife_stk_row_price", "Price"), Value = MarketText.Price(price) });
            }
            rows.Add(new StocksRow { Label = words("reallife_stk_row_cost", "Cost"), Value = MarketText.Money(value) });
            rows.Add(new StocksRow { Label = words("reallife_stk_row_fee", "Fee"), Value = MarketText.Money(fee) });
            rows.Add(new StocksRow { Label = words("reallife_stk_row_total", "Total"), Value = MarketText.Money(total) });
            // What the order leaves: the cash, and the position with its average cost (what stays keeps its cost; the sale's money is not in it).
            rows.Add(new StocksRow { Label = words("reallife_stk_cash_after", "Cash after"), Value = MarketText.Money(Math.Max(0, cash - (buy ? total : -total))) });
            rows.Add(new StocksRow
            {
                Label = words("reallife_stk_position_after", "Position after"),
                Value = PositionWords(afterShares, average)
            });
            rows.Add(new StocksRow
            {
                Label = buy ? words("reallife_stk_row_most_buy", "Most you can buy") : words("reallife_stk_row_most_sell", "Most you can sell"),
                Value = most.ToString("N0")
            });
            return new StocksTicket
            {
                Side = buy ? words("reallife_mkt_buy", "Buy") : words("reallife_mkt_sell", "Sell"),
                Kind = words(KindKeys[kind], KindEnglish[kind]),
                Life = life == OrderLife.UntilCancelled ? words("reallife_stk_gtc", "Until cancelled") : words("reallife_stk_day", "Day"),
                Shares = qty.ToString(),
                Limit = type == OrderType.Market ? words("reallife_stk_max", "Most you can buy") : words("reallife_mkt_col_price", "Price"),
                Max = type == OrderType.Market ? most.ToString() : MarketText.Price(limit),
                Price = type == OrderType.Market ? null : MarketText.Price(limit),
                Cost = MarketText.Money(value),
                Fee = MarketText.Money(fee),
                Total = MarketText.Money(total),
                CashAfter = MarketText.Money(Math.Max(0, cash - (buy ? total : -total))),
                After = words("reallife_stk_position_after", "Position after") + " " + PositionWords(afterShares, average),
                Place = buy ? words("reallife_stk_place_buy", "Place buy order") : words("reallife_stk_place_sell", "Place sell order"),
                Instead = closing ? null : buy ? words("reallife_stk_sell_instead", "Sell instead") : words("reallife_stk_buy_instead", "Buy instead"),
                Why = why,
                PlaceDisabled = why != null && !worked,
                Watch = state.Watching(s.Id) ? words("reallife_mkt_unwatch", "Unwatch") : words("reallife_mkt_watch", "Watch"),
                PutBack = back >= 0 ? InvestText.Reinvest(back) : null,
                Title = words("reallife_stk_order_title", "Order"),
                CashWords = MarketText.Fill("reallife_stk_cash_line", "cash", MarketText.Money(cash)),
                Sides = new[] { words("reallife_mkt_buy", "Buy"), words("reallife_mkt_sell", "Sell") },
                Kinds = new[] { words(KindKeys[0], KindEnglish[0]), words(KindKeys[1], KindEnglish[1]), words(KindKeys[2], KindEnglish[2]), words(KindKeys[3], KindEnglish[3]) },
                Lives = new[] { words("reallife_stk_day", "Day"), words("reallife_stk_gtc", "Until cancelled") },
                SideAt = buy ? 0 : 1,
                KindAt = kind,
                LifeAt = life == OrderLife.UntilCancelled ? 1 : 0,
                LifeLabel = words("reallife_stk_good_for", "Good for"),
                SharesLabel = words("reallife_mkt_col_shares", "Shares"),
                PriceLabel = kind > 0 ? words(priceKeys[kind], priceEnglish[kind]) : null,
                PutBackLabel = words("reallife_stk_put_back", "Dividends put back"),
                Rows = rows.ToArray(),
                MostRow = rows.Count - 1,
                Most = most,
                Buying = buy
            };
        }

        private static void Ticket(StocksModel m, MarketState state, int stockId, bool buy, OrderType type, OrderLife life, long qty, UiWords words)
        {
            Stock s = state.Stock(stockId) ?? First(state);
            StocksTicket t = s != null ? TicketOf(state, s, buy, type, life, qty, words) : new StocksTicket { Place = words("reallife_stk_place", "Place order") };
            m.Decision = new UiDecisionModel
            {
                Kicker = s != null ? s.Ticker : words("reallife_mkt_app", "Stocks"),
                Title = words("reallife_stk_place", "Place order"),
                Emblem = UiIcon.ChartLineUp,
                Body = s == null ? words("reallife_stk_market_empty_body", "Listed companies show here with the day's move.")
                    : t.Side + " " + t.Shares + "  " + t.Cost + "  " + t.Fee,
                Buttons = new[] { new UiActionModel { Label = t.Place, Kind = UiButtonKind.Primary, Key = UiAction.Alt } },
                Prompts = new[] { new UiPrompt { Action = UiAction.Back, Label = words("reallife_ui_prompt_back", "Back") }, new UiPrompt { Action = UiAction.Alt, Label = t.Place } },
                PromptCount = 2
            };
        }

        private static void Portfolio(StocksModel m, MarketState state, UiWords words, MarketDirector d, int view, int period)
        {
            ViewLabels(m, view, words);
            if (d != null && m.View == ViewPays)
            {
                PaysView(m, d, words);
                return;
            }
            if (d != null && m.View == ViewAccount)
            {
                AccountView(m, d, words);
                return;
            }
            m.View = ViewHeld;
            if (d != null)
            {
                m.Status = Margin(d, words);   // a margin call is told on the first view too, as the old tab's chip was
            }
            if (state.Positions.Count == 0)
            {
                m.EmptyTitle = words("reallife_stk_port_empty", "You hold nothing yet");
                m.EmptyBody = words("reallife_stk_port_empty_body", "Buy shares from a company. The holding, its gain and the sector split show here.");
                m.Table = null;
                return;
            }
            long worth = 0;
            long div = 0;
            var by = new long[(int)MarketSector.Count];
            var values = new long[state.Positions.Count];
            var rows = new UiDataRow[state.Positions.Count];
            for (int i = 0; i < state.Positions.Count; i++)
            {
                Position p = state.Positions[i];
                Stock s = state.Stock(p.StockId);
                long value = s != null ? p.Shares * s.Last / 100L : 0L;
                values[i] = value;
                worth += value;
                if (s != null)
                {
                    int sector = (int)s.Sector;
                    if (sector >= 0 && sector < by.Length)
                    {
                        by[sector] += value;
                    }
                    int dps = s.DeclaredCents > 0 ? s.DeclaredCents : s.DpsCents;
                    div += p.Shares * dps / 100L;
                }
                int bp = p.Cost > 0 ? (int)((value - p.Cost) * 10000L / p.Cost) : 0;
                rows[i] = new UiDataRow
                {
                    Cells = new[] { s != null ? s.Name : words("reallife_stk_none", "None"), p.Shares.ToString(), MarketText.Money(value), "0%", MarketText.Change(bp) },
                    Values = new[] { (int)UiIcon.Building, 0, 0, 0, bp > 0 ? 1 : bp < 0 ? -1 : 0 },
                    Tones = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None },
                    Sub = s != null ? s.Ticker : string.Empty
                };
            }
            for (int i = 0; i < rows.Length; i++)
            {
                int weight = worth > 0 ? (int)(values[i] * 100L / worth) : 0;
                rows[i].Cells[3] = weight + "%";
            }
            m.Figures = new[]
            {
                Fig(words("reallife_stk_worth", "Worth"), MarketText.Money(worth), 0),
                Fig(words("reallife_stk_realised_all", "Realised, all time"), MarketText.Money(state.Account.Realised), state.Account.Realised > 0 ? 1 : state.Account.Realised < 0 ? -1 : 0),
                Fig(words("reallife_stk_next_div", "Next dividends"), MarketText.Money(div), 0)
            };
            m.Chart = WorthChart(state, words, period);
            m.Donut = Sectors(by, worth, out string[] labels, out string[] amounts);
            m.DonutLabels = labels;
            m.DonutValues = amounts;
            m.Table = new UiDataTableModel
            {
                Columns = new[]
                {
                    new UiDataColumn { Title = words("reallife_mkt_col_company", "Company"), Kind = UiCellKind.Tile },
                    new UiDataColumn { Title = words("reallife_mkt_col_shares", "Shares"), Kind = UiCellKind.Figure, Width = 120f },
                    new UiDataColumn { Title = words("reallife_stk_worth", "Worth"), Kind = UiCellKind.Figure, Width = 140f },
                    new UiDataColumn { Title = words("reallife_stk_weight", "Weight"), Kind = UiCellKind.Figure, Width = 100f },
                    new UiDataColumn { Title = words("reallife_mkt_col_today", "Today"), Kind = UiCellKind.Trend, Width = 120f }
                },
                Rows = rows, RowCount = rows.Length, EmptyIcon = UiIcon.ChartPie,
                Filters = new[] { PeriodWord(period, words) }
            };
        }

        // The worth of what is held over a period: the days the candles keep within it, each holding marked to its closes with the trades on their days.
        private static UiChartModel WorthChart(MarketState state, UiWords words, int period)
        {
            int n = 2;
            for (int p = 0; p < state.Positions.Count; p++)
            {
                Stock held = state.Stock(state.Positions[p].StockId);
                n = held != null ? Math.Max(n, Math.Min(PeriodDays(period), held.Daily.Count)) : n;
            }
            var values = new float[n];
            bool any = false;
            for (int p = 0; p < state.Positions.Count; p++)
            {
                Position held = state.Positions[p];
                Stock s = state.Stock(held.StockId);
                if (s == null || s.Daily.Count < 2)
                {
                    continue;
                }
                int len = Math.Min(n, s.Daily.Count);
                int origin = s.Daily.Count - len;
                var days = new int[len];
                var closes = new int[len];
                for (int i = 0; i < len; i++)
                {
                    days[i] = s.Daily[origin + i].Day;
                    closes[i] = s.Daily[origin + i].C;
                }
                var tradeDay = new int[TradesMarked];
                var tradeShares = new long[TradesMarked];
                int trades = 0;
                long start = held.Shares;
                for (int t = 0; t < state.Trades.Count && trades < tradeDay.Length; t++)
                {
                    Trade trade = state.Trades[t];
                    if (trade.StockId != held.StockId || (trade.Kind != TradeKind.Bought && trade.Kind != TradeKind.Sold) || trade.Day < days[0] || trade.Day > days[len - 1])
                    {
                        continue;
                    }
                    long signed = trade.Kind == TradeKind.Sold ? -trade.Qty : trade.Qty;
                    tradeDay[trades] = trade.Day;
                    tradeShares[trades] = signed;
                    trades++;
                    start -= signed;
                }
                var into = new long[len];
                StocksMath.Mark(days, closes, len, tradeDay, tradeShares, trades, start, into);
                int pad = n - len;
                for (int i = 0; i < len; i++)
                {
                    values[pad + i] += into[i];
                }
                any = true;
            }
            if (!any)
            {
                return null;
            }
            var points = new string[n];
            for (int i = 0; i < n; i++)
            {
                points[i] = string.Empty;
            }
            return new UiChartModel
            {
                Kind = UiChartKind.Line,
                Title = words("reallife_stk_worth", "Worth"),
                Points = points,
                Series = new[] { new UiSeries { Label = words("reallife_stk_worth", "Worth"), Values = values, Tint = UiRole.Accent } }
            };
        }

        private const int TradesMarked = 64;   // the trades of one holding the chart marks to its days

        private static float[] Sectors(long[] by, long worth, out string[] labels, out string[] amounts)
        {
            int n = 0;
            for (int i = 0; i < by.Length && n < 8; i++)
            {
                if (by[i] > 0)
                {
                    n++;
                }
            }
            if (n == 0 || worth <= 0)
            {
                labels = new string[0];
                amounts = new string[0];
                return new float[0];
            }
            var share = new float[n];
            labels = new string[n];
            amounts = new string[n];
            int at = 0;
            for (int i = 0; i < by.Length && at < n; i++)
            {
                if (by[i] <= 0)
                {
                    continue;
                }
                share[at] = by[i] / (float)worth;
                labels[at] = MarketText.Sector((MarketSector)i);
                amounts[at] = (by[i] * 100L / worth) + "%";
                at++;
            }
            return share;
        }

        private static void Listing(StocksModel m, MarketState state, UiWords words)
        {
            ListingFile f = state.Listing;
            if (f == null || f.Empty)
            {
                m.EmptyTitle = words("reallife_stk_list_empty", "Your company is not listing");
                m.EmptyBody = words("reallife_stk_list_empty_body", "When you take the company to market, the stages, the book and the holders show here.");
                return;
            }
            int price = f.SetCents > 0 ? f.SetCents : f.PriceCents;
            m.Hero = new UiHeroModel
            {
                Icon = UiIcon.Presentation,
                Title = string.IsNullOrEmpty(f.Name) ? words("reallife_stk_list_empty", "Your company is not listing") : f.Name,
                Kicker = f.Ticker, Line = MarketText.Price(price),
                Progress = (int)f.Stage * 100 / (int)ListingStage.Listed
            };
            m.Note = DividendPolicy(state, f, words);
            if (f.LowCents > 0 && f.HighCents >= f.LowCents && f.Stage < ListingStage.Listed)
            {
                m.Note += "  " + MarketText.Fill("reallife_stk_expected_range", "low", MarketText.Price(f.LowCents), "high", MarketText.Price(f.HighCents));
            }
            m.Track = new UiTrackModel
            {
                Title = words("reallife_stk_stages", "Stages"),
                Steps = new[]
                {
                    Step(Dated(words("reallife_lst_stage_advised", "advisers engaged"), f.AdvisedDay, 0), f.Stage > ListingStage.Advised, f.Stage == ListingStage.Advised),
                    Step(Dated(words("reallife_lst_stage_auditing", "being audited"), f.AuditFrom, f.AuditDone), f.Stage > ListingStage.Auditing, f.Stage == ListingStage.Auditing),
                    Step(Dated(words("reallife_lst_stage_building", "the book is open"), f.BookFrom, f.BookEnd), f.Stage > ListingStage.Building, f.Stage == ListingStage.Building),
                    Step(Dated(words("reallife_lst_stage_priced", "priced"), f.BookEnd, 0), f.Stage > ListingStage.Priced, f.Stage == ListingStage.Priced),
                    Step(Dated(words("reallife_lst_stage_listed", "on the market"), f.ListDay, 0), f.Stage > ListingStage.Listed, f.Stage == ListingStage.Listed)
                },
                StateWords = new[]
                {
                    words("reallife_stk_done", "Done"), words("reallife_stk_now", "Now"), words("reallife_stk_next", "Next"),
                    words("reallife_stk_later", "Later"), words("reallife_stk_later", "Later")
                }
            };
            var rows = new UiDataRow[f.BidCount];
            for (int i = 0; i < f.BidCount; i++)
            {
                BookBid b = f.Bids[i];
                rows[i] = new UiDataRow
                {
                    Cells = new[] { BidderWord(b.Who, words), MarketText.Price(b.Cents), b.Size.ToString(), b.Filled.ToString() },
                    Values = new[] { (int)UiIcon.Bank, 0, 0, 0 },
                    Tones = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None }
                };
            }
            m.Table = new UiDataTableModel
            {
                Columns = new[]
                {
                    new UiDataColumn { Title = words("reallife_stk_bidder", "Bidder"), Kind = UiCellKind.Tile },
                    new UiDataColumn { Title = words("reallife_mkt_col_price", "Price"), Kind = UiCellKind.Figure, Width = 120f },
                    new UiDataColumn { Title = words("reallife_mkt_col_shares", "Shares"), Kind = UiCellKind.Figure, Width = 120f },
                    new UiDataColumn { Title = words("reallife_stk_filled", "Filled"), Kind = UiCellKind.Figure, Width = 120f }
                },
                Rows = rows, RowCount = f.BidCount, EmptyIcon = UiIcon.Presentation,
                EmptyTitle = words("reallife_stk_book_empty", "The book is not open"),
                EmptyBody = words("reallife_stk_book_empty_body", "Bids show here when the book is priced, largest first.")
            };
        }

        // A stage's name with the day it comes on, or the days it runs from and to: nothing added until the day is known.
        private static string Dated(string label, int from, int to)
        {
            if (from <= 0)
            {
                return label;
            }
            return label + ", " + (to > from ? MarketText.Fill("reallife_stk_days_span", "from", from.ToString(), "to", to.ToString()) : MarketText.Fill("reallife_mkt_day", "day", from.ToString()));
        }

        /// <summary>
        /// The dividend policy as it stands: the founder's own, from the company's listing (the board proposes a dividend with each results
        /// while the founder chairs it, and pays out a share of the reserve when they do not), not a constant of the catalogue.
        /// </summary>
        public static string DividendPolicy(MarketState state, ListingFile f, UiWords words)
        {
            Stock k = f != null && f.StockId > 0 ? state.Stock(f.StockId) : null;
            OwnListing o = k != null ? k.Own : null;
            if (o == null)
            {
                return words("reallife_stk_policy_chair", "You chair the board: you propose the dividend with each results.");
            }
            if (!o.Board)
            {
                return MarketText.Fill("reallife_stk_policy_board", "payout", MarketText.Percent(ListingCatalogue.BoardPayoutBp));
            }
            return o.ProposedCents > 0 ? MarketText.Fill("reallife_stk_policy_proposed", "price", MarketText.Price(o.ProposedCents))
                : words("reallife_stk_policy_none", "No dividend is proposed yet.");
        }

        private static string BidderWord(BidWho who, UiWords words)
        {
            switch (who)
            {
                case BidWho.Fund: return words("reallife_stk_fund", "A fund");
                case BidWho.Investor: return words("reallife_stk_investor", "An investor");
                case BidWho.Retail: return words("reallife_stk_retail", "Retail investors");
                default: return words("reallife_stk_rival", "A rival");
            }
        }

        private static UiTrackStep Step(string label, bool done, bool now)
        {
            return new UiTrackStep { Icon = UiIcon.Check, Label = label, State = done ? UiStepState.Done : now ? UiStepState.Now : UiStepState.Later };
        }

        private static string[] Tape(MarketState state, int stockId, int most, UiWords words)
        {
            var lines = new System.Collections.Generic.List<string>(most);
            for (int i = 0; i < state.Names.Count && lines.Count < most; i++)
            {
                TradePrint p = state.Names.Newest(i);
                if (stockId != 0 && p.StockId != stockId)
                {
                    continue;
                }
                lines.Add(Who(p, state, words));
            }
            return lines.ToArray();
        }

        private static string Who(TradePrint p, MarketState state, UiWords words)
        {
            string who;
            switch (p.Who)
            {
                case TradeWho.You: who = words("reallife_stk_you", "You"); break;
                case TradeWho.Hired:
                    BotFirm firm = MarketBots.Firm(p.WhoId);
                    who = firm != null ? firm.Name : words("reallife_stk_bot", "Bot");
                    break;
                case TradeWho.Rival:
                    who = p.WhoId >= 0 && p.WhoId < MarketCatalogue.RivalTemplates.Length && !string.IsNullOrEmpty(MarketCatalogue.RivalTemplates[p.WhoId].Name)
                        ? MarketCatalogue.RivalTemplates[p.WhoId].Name : words("reallife_stk_rival", "A rival");
                    break;
                case TradeWho.Fund: who = words("reallife_stk_fund", "A fund"); break;
                case TradeWho.Investor: who = words("reallife_stk_investor", "An investor"); break;
                case TradeWho.Broker: who = words("reallife_stk_broker", "A broker"); break;
                default: who = words("reallife_stk_rule", "Rule"); break;
            }
            Stock s = state.Stock(p.StockId);
            // A whole sentence in the player's language ("A broker sold 124 ARE"), not the verb of the order button put after a name.
            return MarketText.Fill(p.Side == TradeSide.Buy ? "reallife_stk_tape_buy" : "reallife_stk_tape_sell", "who", who, "size", p.Size.ToString("N0"),
                "ticker", s != null ? s.Ticker : string.Empty);
        }

        private static Stock First(MarketState state)
        {
            for (int i = 0; i < state.Stocks.Count; i++)
            {
                if (state.Stocks[i].Listed)
                {
                    return state.Stocks[i];
                }
            }
            return null;
        }

    }
}
