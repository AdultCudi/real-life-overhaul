using System;
using System.Collections.Generic;
using System.Globalization;
using RealLifeOverhaul.DaySeries;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.GameUi;
using RealLifeOverhaul.Ledger;
using RealLifeOverhaul.Markets;
using RealLifeOverhaul.Outlook;
using RealLifeOverhaul.Permadeath;
using RealLifeOverhaul.Population;
using RealLifeOverhaul.Rivals;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Fills a news page from the city when the day or the tab changes, and carries a press to the feature that already
    /// owns it. Nothing a frame.
    /// </summary>
    public sealed class NewsLive : INewsSource
    {
        public static readonly NewsLive Economy = new NewsLive(NewsApp.Economy);
        public static readonly NewsLive People = new NewsLive(NewsApp.People);
        public static readonly NewsLive Stand = new NewsLive(NewsApp.Stand);
        public static readonly NewsLive Paper = new NewsLive(NewsApp.Paper);
        public static readonly NewsLive Ending = new NewsLive(NewsApp.Ending);
        public static readonly NewsLive Household = new NewsLive(NewsApp.Household);
        public static readonly NewsLive Renters = new NewsLive(NewsApp.Renters);

        public static int WantedTab = -1;

        /// <summary>Leaves the rent list or a household and returns to the page under it.</summary>
        public bool Back()
        {
            if (!_rent && !_homeOpen)
            {
                return false;
            }
            _rent = false;
            _homeOpen = false;
            _local++;
            return true;
        }
        public static bool EndingOpen;
        public static LifeStory EndingStory;

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly OutlookReadings Look = new OutlookReadings();
        private static readonly int[] Scratch = new int[8];
        private static readonly int[] Raw = new int[NewsRules.DaysBook];
        private static readonly float[] Inflation = new float[NewsRules.DaysBook];
        private static readonly float[] Rate = new float[NewsRules.DaysBook];
        private static readonly float[] Wages = new float[NewsRules.DaysBook];
        private static readonly List<OutlookInsight> Insights = new List<OutlookInsight>(8);
        private readonly NewsApp _app;
        private int _local;
        private bool _rent;
        private bool _homeOpen;
        private int _home = -1;
        private int[] _slots = new int[0];
        private int _range = 1;
        private int _view;

        private NewsLive(NewsApp app) { _app = app; }

        public int Generation
        {
            get
            {
                int day = OutlookDirector.Instance != null && OutlookDirector.Instance.History != null ? OutlookDirector.Instance.History.LastDay : 0;
                return day + _local + _range * 1000 + DaySeriesDirector.Generation;
            }
        }

        public void Fill(NewsModel model)
        {
            model.App = _app;
            if (WantedTab >= 0 && _app == NewsApp.Economy)
            {
                model.Tab = WantedTab;
                WantedTab = -1;
            }
            model.Prompts = new[]
            {
                new UiPrompt { Action = UiAction.Move, Label = OutlookText.T("reallife_ui_prompt_move") },
                new UiPrompt { Action = UiAction.Confirm, Label = OutlookText.T("reallife_ui_prompt_select") },
                new UiPrompt { Action = UiAction.Back, Label = OutlookText.T("reallife_ui_prompt_back") }
            };
            switch (_app)
            {
                case NewsApp.People: FillPeople(model); break;
                case NewsApp.Stand: FillStand(model); break;
                case NewsApp.Paper: FillPaper(model); break;
                case NewsApp.Ending: FillEnding(model); break;
                case NewsApp.Household: FillHome(model); break;
                case NewsApp.Renters: FillRenters(model); break;
                default: FillEconomy(model); break;
            }
        }

        public bool Press(NewsModel model, int command, int arg)
        {
            if (command == UiAppWindow.ActionCommand)
            {
                if (arg >= 0 && arg <= 2 && _app == NewsApp.Economy)
                {
                    _range = arg;
                    _local++;
                    return true;
                }
            }
            if (command == NewsCommands.Link)
            {
                return Open(arg);
            }
            if (command == NewsCommands.Rent)
            {
                _rent = true;
                _homeOpen = false;
                _local++;
                return true;
            }
            if (command == NewsCommands.Busy)
            {
                _view = 2;
                _local++;
                return true;
            }
            if (_rent && command == UiDataTable.First + UiDataTable.Row && arg >= 0 && arg < _slots.Length)
            {
                _home = _slots[arg];
                _homeOpen = true;
                _local++;
                return true;
            }
            if (command == NewsCommands.District + UiDataTable.Row)
            {
                model.Tab = 7;
                _view = 0;
                _local++;
                return true;
            }
            if (command == UiDecision.ChoiceCommand)
            {
                return EndChoice(arg);
            }
            if (_app == NewsApp.Stand && command == UiCatalogue.First + UiCatalogue.Action)
            {
                return Subscription(model, arg);
            }
            if (_app == NewsApp.Stand && command == UiCatalogue.First + UiCatalogue.Card)
            {
                model.Tab = 1;
                _local++;
                return true;
            }
            if (command == UiAppWindow.NavCommand && _app == NewsApp.Economy && arg == 7)
            {
                _view = 0;
                return true;
            }
            return command == UiAppWindow.NavCommand || command == UiInfoView.ViewCommand || command == UiDataTable.First + UiDataTable.Row
                || command == UiChart.First + UiChart.Point || command == UiEdition.StoryCommand || command == UiEdition.LeadCommand;
        }

        private void FillEconomy(NewsModel model)
        {
            model.Icon = UiIcon.Chart;
            model.Title = OutlookText.T("reallife_econapp_app");
            model.Nav = Nav();
            if (model.Tab == 7)
            {
                FillPeople(model);
                return;
            }
            if (_homeOpen)
            {
                FillHome(model);
                return;
            }
            if (_rent)
            {
                FillRenters(model);
                return;
            }
            bool read = OutlookGame.Read(Look) && Look.CityOn;
            model.Shape = (NewsShape)(model.Tab < 0 || model.Tab > 6 ? 0 : model.Tab);
            if (!read)
            {
                model.Shape = NewsShape.Notice;
                model.EmptyTitle = OutlookText.T("reallife_news_empty_city");
                model.EmptyBody = OutlookText.T("reallife_news_empty_city_body");
                model.EmptyIcon = UiIcon.Chart;
                return;
            }
            OutlookBook book = OutlookDirector.Instance != null ? OutlookDirector.Instance.History : null;
            OutlookVerdict verdict = book != null ? OutlookStudy.Run(book, Look, Scratch) : OutlookVerdict.Steady;
            model.Chip = OutlookText.Words.VerdictWord(verdict);
            model.Lead = model.Shape == NewsShape.Prices ? BasketLead(book) : OutlookText.Words.VerdictLine(verdict, Look);
            model.Body = model.Shape == NewsShape.Overview ? OutlookText.Words.VerdictLine(verdict, Look) : string.Empty;
            model.ChipTone = verdict == OutlookVerdict.Growing ? UiRole.OkText : verdict == OutlookVerdict.Recession ? UiRole.DangerText : UiRole.WarnText;
            model.Figures = model.Shape == NewsShape.Overview || model.Shape == NewsShape.Finances ? Figures() : new UiFigureModel[0];
            model.Chart = Chart(book, model.Shape);
            if (model.Shape == NewsShape.Overview && model.Figures.Length == 3)
            {
                int span = NewsRules.Span(_range);
                model.Figures[0].Trend = Tail(Inflation, span);
                model.Figures[1].Trend = Tail(Rate, span);
                model.Figures[2].Trend = Tail(Wages, span);
            }
            model.Bands = model.Shape == NewsShape.Overview ? Bands(book) : new NewsBand[0];
            model.Marks = model.Shape == NewsShape.Overview ? RiseMarks(book) : new int[0];
            model.RiseLine = model.Shape == NewsShape.Overview ? Rises(book) : string.Empty;
            model.AdviceTitle = OutlookText.T("reallife_news_advice");
            model.Advice = model.Shape == NewsShape.Overview || model.Shape == NewsShape.Insights ? Advice(book) : new NewsAdvice[0];
            model.Actions = model.Shape == NewsShape.Overview ? Ranges() : new UiActionModel[0];
            model.Note = model.Shape == NewsShape.Prices ? Movers() : model.Shape == NewsShape.Markets ? Holding() : string.Empty;
            model.Table = TableFor(model.Shape, book);
            model.Table2 = model.Shape == NewsShape.Overview ? DistrictTable() : null;
        }

        private static UiNavEntry[] Nav()
        {
            return new[]
            {
                Item(UiIcon.Chart, "reallife_news_nav_overview"),
                Item(UiIcon.Tag, "reallife_news_nav_prices"),
                Item(UiIcon.Briefcase, "reallife_news_nav_jobs"),
                Item(UiIcon.MapTrifold, "reallife_news_nav_districts"),
                Item(UiIcon.ChartLineUp, "reallife_news_nav_markets"),
                Item(UiIcon.Bank, "reallife_news_nav_finances"),
                Item(UiIcon.Lightbulb, "reallife_news_nav_insights"),
                Item(UiIcon.People, "reallife_news_nav_people")
            };
        }

        private static UiNavEntry Item(UiIcon icon, string key)
        {
            return new UiNavEntry { Icon = icon, Tint = UiRole.None, Label = OutlookText.T(key) };
        }

        private UiFigureModel[] Figures()
        {
            return new[]
            {
                Fig("reallife_econapp_tile_inflation", (Look.Inflation / 100f).ToString("0.0", Inv) + "%", Look.MonthChange),
                Fig("reallife_econapp_tile_rate", (Look.RateBp / 100f).ToString("0.00", Inv) + "%", Look.PendingMove),
                Fig("reallife_econapp_tile_unemployment", (Look.Unemployment / 10f).ToString("0.0", Inv) + "%", 0)
            };
        }

        private static UiFigureModel Fig(string key, string value, int change)
        {
            return new UiFigureModel
            {
                Label = OutlookText.T(key),
                Value = value,
                Change = change == 0 ? null : (change > 0 ? "+" : "") + change.ToString(Inv),
                Rising = change > 0,
                Good = change < 0
            };
        }

        private UiChartModel Chart(OutlookBook book, NewsShape shape)
        {
            int span = NewsRules.Span(_range);
            int end = book != null ? book.LastDay : 0;
            Copy(book, OutlookCatalogue.Inflation, end, span, Inflation);
            Copy(book, OutlookCatalogue.Rate, end, span, Rate);
            Copy(book, OutlookCatalogue.Income, end, span, Wages);
            var points = new string[span];
            for (int i = 0; i < span; i++)
            {
                points[i] = end > span ? (end - span + 1 + i).ToString(Inv) : (i + 1).ToString(Inv);
            }
            UiSeries[] series = SeriesFor(book, shape, end, span);
            return new UiChartModel
            {
                Kind = UiChartKind.Line,
                Title = OutlookText.T("reallife_news_chart"),
                Points = points,
                Series = series,
                Format = v => v.ToString("0.0", Inv),
                EmptyTitle = OutlookText.T("reallife_news_empty_city"),
                EmptyBody = OutlookText.T("reallife_news_empty_city_body")
            };
        }

        private UiSeries[] SeriesFor(OutlookBook book, NewsShape shape, int end, int span)
        {
            if (shape == NewsShape.Jobs)
            {
                Copy(book, OutlookCatalogue.Unemployment, end, span, Inflation);
                Copy(book, OutlookCatalogue.Vacancies, end, span, Rate);
                return new[]
                {
                    new UiSeries { Label = OutlookText.T("reallife_news_series_jobless"), Values = Take(Inflation, span), Tint = UiRole.Danger },
                    new UiSeries { Label = OutlookText.T("reallife_news_series_vacancies"), Values = Take(Rate, span), Tint = UiRole.Ok }
                };
            }
            if (shape == NewsShape.Finances)
            {
                Copy(book, OutlookCatalogue.Reserve, end, span, Inflation);
                Copy(book, OutlookCatalogue.Revenue, end, span, Rate);
                Copy(book, OutlookCatalogue.Spending, end, span, Wages);
                return new[]
                {
                    new UiSeries { Label = OutlookText.T("reallife_news_series_reserve"), Values = Take(Inflation, span), Tint = UiRole.Accent },
                    new UiSeries { Label = OutlookText.T("reallife_news_series_revenue"), Values = Take(Rate, span), Tint = UiRole.Ok },
                    new UiSeries { Label = OutlookText.T("reallife_news_series_spending"), Values = Take(Wages, span), Tint = UiRole.Danger }
                };
            }
            if (shape == NewsShape.Markets)
            {
                return IndexSeries(span);
            }
            if (shape == NewsShape.Prices)
            {
                Copy(book, OutlookCatalogue.Prices, end, span, Inflation);
                return new[] { new UiSeries { Label = OutlookText.T("reallife_news_series_index"), Values = Take(Inflation, span), Tint = UiRole.Accent } };
            }
            return new[]
            {
                new UiSeries { Label = OutlookText.T("reallife_news_series_inflation"), Values = Take(Inflation, span), Tint = UiRole.Danger },
                new UiSeries { Label = OutlookText.T("reallife_news_series_rate"), Values = Take(Rate, span), Tint = UiRole.Accent },
                new UiSeries { Label = OutlookText.T("reallife_news_series_wages"), Values = Take(Wages, span), Tint = UiRole.Ok }
            };
        }

        private string BasketLead(OutlookBook book)
        {
            int now = Look.Prices;
            int then = now;
            if (book != null && book.Get(OutlookCatalogue.Prices, book.LastDay - NewsRules.DaysMonth, out int earlier))
            {
                then = earlier;
            }
            int bp = NewsRules.BasketBp(now, then);
            string led = string.Empty;
            int best = 0;
            for (int i = 0; i < Look.Supply.Length; i++)
            {
                if (i >= OutlookWords.KindKeys.Length)
                {
                    continue;
                }
                int move = Look.Supply[i].Factor - 1000;
                if (move > best)
                {
                    best = move;
                    led = OutlookText.T(OutlookWords.KindKeys[i]);
                }
            }
            string change = (bp / 100f).ToString("0.0", Inv) + "%";
            return OutlookText.T("reallife_news_basket", "change", change) + (led.Length > 0 ? " " + OutlookText.T("reallife_news_led", "good", led) : string.Empty);
        }

        private string Movers()
        {
            string line = string.Empty;
            var file = RivalDirector.Instance != null ? RivalDirector.Instance.File : null;
            int day = Look.Day;
            if (file != null)
            {
                for (int i = 0; i < file.Moves.Count; i++)
                {
                    RivalMove move = file.Moves[i];
                    if (move.Kind != RivalMoveKind.PriceRise || day - move.Announced > 7)
                    {
                        continue;
                    }
                    string who = move.Tycoon >= 0 && move.Tycoon < RivalCatalogue.Tycoons.Length ? RivalCatalogue.Tycoons[move.Tycoon].Name : string.Empty;
                    line = Join(line, who);
                }
            }
            MarketState market = MarketDirector.Instance != null ? MarketDirector.Instance.State : null;
            int firms = 0;
            if (market != null)
            {
                for (int i = 0; i < market.Stocks.Count && firms < 3; i++)
                {
                    Stock stock = market.Stocks[i];
                    if (stock == null || stock.Name.Length == 0 || !string.IsNullOrEmpty(stock.RivalId) || stock.Last <= 0 || stock.Last == stock.PrevClose)
                    {
                        continue;
                    }
                    line = Join(line, stock.Name);
                    firms++;
                }
            }
            line = Join(line, PolicyName(Lever.TradeLevy));
            line = Join(line, PolicyName(Lever.BusinessTax));
            return line.Length == 0 ? string.Empty : OutlookText.T("reallife_news_moved") + " " + line;
        }

        private static string Join(string line, string who)
        {
            if (string.IsNullOrEmpty(who))
            {
                return line;
            }
            return line.Length == 0 ? who : line + ", " + who;
        }

        // City Hall only when a price lever actually stands off the city's own setting.
        private static string PolicyName(Lever lever)
        {
            int at = CivicPolicy.Position(lever);
            if (at == 0)
            {
                return string.Empty;
            }
            RealLifeOverhaul.PolicyDef[] all = CivicCatalogue.All;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].Lever == lever && all[i].Dir == at)
                {
                    return OutlookText.T(all[i].Name);
                }
            }
            return string.Empty;
        }

        private void PullFell(OutlookBook book, out string name, out int points)
        {
            name = string.Empty;
            points = 0;
            if (book == null || book.LastDay <= 0)
            {
                return;
            }
            var window = new int[8];
            for (int i = 0; i < Look.DistrictCount; i++)
            {
                string id = Look.Districts[i].Id;
                if (book.Window(OutlookCatalogue.OfDistrictPull(id), book.LastDay, 8, window) < 2)
                {
                    continue;
                }
                int now = window[7];
                int then = window[0];
                if (now == OutlookCatalogue.NoValue || then == OutlookCatalogue.NoValue || then <= now)
                {
                    continue;
                }
                int drop = then - now;
                if (drop <= points)
                {
                    continue;
                }
                points = drop;
                string named = OutlookText.Words.District != null ? OutlookText.Words.District(id) : id;
                name = string.IsNullOrEmpty(named) ? id : named;
            }
        }

        private UiSeries[] IndexSeries(int span)
        {
            MarketState state = MarketDirector.Instance != null ? MarketDirector.Instance.State : null;
            var values = new float[span];
            if (state != null && state.IndexDaily.Count > 0)
            {
                int n = state.IndexDaily.Count;
                int take = n < span ? n : span;
                int from = n - take;
                for (int i = 0; i < span; i++)
                {
                    int at = from + (i < take ? i : take - 1);
                    values[i] = state.IndexDaily[at].C / 100f;
                }
            }
            return new[] { new UiSeries { Label = OutlookText.T("reallife_news_series_index"), Values = values, Tint = UiRole.Accent } };
        }

        private string Holding()
        {
            MarketState state = MarketDirector.Instance != null ? MarketDirector.Instance.State : null;
            if (state == null)
            {
                return string.Empty;
            }
            Position best = null;
            Stock named = null;
            long gain = 0;
            long worth = 0;
            for (int i = 0; i < state.Positions.Count; i++)
            {
                Position p = state.Positions[i];
                if (p == null || p.Test || p.Shares == 0)
                {
                    continue;
                }
                Stock stock = state.Stock(p.StockId);
                if (stock == null || stock.Last <= 0)
                {
                    continue;
                }
                long now = p.Shares * stock.Last / 100L;
                long g = now - p.Cost;
                if (best == null || (g > 0 ? g : -g) > (gain > 0 ? gain : -gain))
                {
                    best = p;
                    named = stock;
                    worth = now;
                    gain = g;
                }
            }
            if (best == null || named == null)
            {
                return string.Empty;
            }
            string way = (gain >= 0 ? "+" : string.Empty) + LedgerText.Money((int)Math.Max(int.MinValue, Math.Min(int.MaxValue, gain)));
            return named.Name + " " + LedgerText.Money((int)Math.Max(0, Math.Min(int.MaxValue, worth))) + " " + way;
        }

        private UiDataTableModel TableFor(NewsShape shape, OutlookBook book)
        {
            if (shape == NewsShape.Overview || shape == NewsShape.Prices)
            {
                return PriceTable(shape == NewsShape.Prices);
            }
            if (shape == NewsShape.Districts)
            {
                return DistrictTable();
            }
            if (shape == NewsShape.Jobs)
            {
                return JobsTable();
            }
            if (shape == NewsShape.Markets)
            {
                return SectorTable();
            }
            return null;
        }

        private UiDataTableModel JobsTable()
        {
            var rows = new List<UiDataRow>(2)
            {
                new UiDataRow { Cells = new[] { OutlookText.T("reallife_news_series_jobless"), (Look.Unemployment / 10f).ToString("0.0", Inv) + "%" }, Values = new int[2], Tones = new UiRole[2] },
                new UiDataRow { Cells = new[] { OutlookText.T("reallife_news_series_vacancies"), Look.Vacancies.ToString(Inv) }, Values = new int[2], Tones = new UiRole[2] }
            };
            return Table(new[] { "reallife_news_col_good", "reallife_news_col_today" }, rows);
        }

        private UiDataTableModel SectorTable()
        {
            MarketState state = MarketDirector.Instance != null ? MarketDirector.Instance.State : null;
            var rows = new List<UiDataRow>(12);
            if (state != null)
            {
                int sectors = (int)MarketSector.Count;
                int up = -1;
                int down = -1;
                int upBp = int.MinValue;
                int downBp = int.MaxValue;
                for (int i = 0; i < sectors && i < state.SectorDaily.Length; i++)
                {
                    int bp = SessionBp(state.SectorDaily[i], 5);
                    if (bp > upBp)
                    {
                        upBp = bp;
                        up = i;
                    }
                    if (bp < downBp)
                    {
                        downBp = bp;
                        down = i;
                    }
                }
                AddSession(rows, OutlookText.T("reallife_news_riser"), up, upBp);
                AddSession(rows, OutlookText.T("reallife_news_faller"), down, downBp);
                AddTraded(rows, state);
            }
            return new UiDataTableModel
            {
                Columns = new[]
                {
                    new UiDataColumn { Title = OutlookText.T("reallife_news_col_good"), Kind = UiCellKind.Text, Width = 128f },
                    new UiDataColumn { Title = OutlookText.T("reallife_news_col_district"), Kind = UiCellKind.Text },
                    new UiDataColumn { Title = OutlookText.T("reallife_news_col_five"), Kind = UiCellKind.Trend, Width = 128f }
                },
                Rows = rows.ToArray(),
                RowCount = rows.Count,
                EmptyTitle = OutlookText.T("reallife_news_empty_city"),
                EmptyBody = OutlookText.T("reallife_news_empty_city_body"),
                EmptyIcon = UiIcon.ChartLineUp
            };
        }

        private static int SessionBp(List<int> daily, int sessions)
        {
            if (daily == null || daily.Count < 2)
            {
                return 0;
            }
            int last = daily[daily.Count - 1];
            int from = daily.Count - sessions;
            if (from < 0)
            {
                from = 0;
            }
            int then = daily[from];
            if (then == 0)
            {
                return 0;
            }
            return (int)((long)(last - then) * 10000L / then);
        }

        private static void AddSession(List<UiDataRow> rows, string kind, int sector, int bp)
        {
            if (sector < 0)
            {
                return;
            }
            int way = bp > 0 ? 1 : bp < 0 ? -1 : 0;
            rows.Add(new UiDataRow
            {
                Cells = new[] { kind, ((MarketSector)sector).ToString(), (bp / 100f).ToString("0.00", Inv) + "%" },
                Values = new[] { 0, 0, way },
                Tones = new[] { UiRole.None, UiRole.None, UiRole.None }
            });
        }

        private static void AddTraded(List<UiDataRow> rows, MarketState state)
        {
            int best = -1;
            long volume = -1;
            for (int i = 0; i < state.Stocks.Count; i++)
            {
                Stock s = state.Stocks[i];
                if (s != null && s.DayVolume > volume)
                {
                    volume = s.DayVolume;
                    best = i;
                }
            }
            if (best < 0 || volume <= 0)
            {
                return;
            }
            rows.Add(new UiDataRow
            {
                Cells = new[] { OutlookText.T("reallife_news_traded"), state.Stocks[best].Name, volume.ToString(Inv) },
                Values = new int[3],
                Tones = new UiRole[3]
            });
        }

        private static void Copy(OutlookBook book, string key, int end, int span, float[] into)
        {
            for (int i = 0; i < span; i++)
            {
                into[i] = 0f;
            }
            if (book == null || end <= 0)
            {
                return;
            }
            int found = book.Window(key, end, span, Raw);
            if (found <= 0)
            {
                return;
            }
            float last = 0f;
            for (int i = 0; i < span; i++)
            {
                if (Raw[i] != OutlookCatalogue.NoValue)
                {
                    last = Raw[i];
                }
                into[i] = last;
            }
        }

        private static float[] Tail(float[] from, int span)
        {
            int n = Math.Min(12, span);
            var tail = new float[n];
            int start = span - n;
            for (int i = 0; i < n; i++)
            {
                tail[i] = from[start + i];
            }
            return tail;
        }

        private static float[] Take(float[] from, int span)
        {
            var copy = new float[span];
            for (int i = 0; i < span; i++)
            {
                copy[i] = from[i];
            }
            return copy;
        }

        private NewsBand[] Bands(OutlookBook book)
        {
            if (book == null || book.LastDay <= 0)
            {
                return new NewsBand[0];
            }
            int span = NewsRules.Span(_range);
            int found = book.Window(OutlookCatalogue.Cycle, book.LastDay, span, Raw);
            if (found <= 0)
            {
                return new NewsBand[0];
            }
            var list = new List<NewsBand>(4);
            int run = 1;
            int phase = Raw[0] == OutlookCatalogue.NoValue ? Look.Cycle : Raw[0];
            for (int i = 1; i <= span; i++)
            {
                int next = i < span && Raw[i] != OutlookCatalogue.NoValue ? Raw[i] : phase;
                if (i == span || next != phase)
                {
                    list.Add(new NewsBand { Label = PhaseName(phase), Share = (float)run / span, Tint = PhaseRole(phase) });
                    phase = next;
                    run = 1;
                }
                else
                {
                    run++;
                }
            }
            return list.ToArray();
        }

        private static UiRole PhaseRole(int phase)
        {
            if (phase <= 0)
            {
                return UiRole.Ok;
            }
            if (phase == 1)
            {
                return UiRole.Accent;
            }
            if (phase == 2)
            {
                return UiRole.Warn;
            }
            return UiRole.Danger;
        }

        private static string PhaseName(int phase)
        {
            string[] keys =
            {
                "reallife_macro_state_boom", "reallife_macro_state_steady", "reallife_macro_state_slowdown", "reallife_macro_state_recession"
            };
            int i = phase < 0 ? 0 : phase > 3 ? 3 : phase;
            return OutlookText.T(keys[i]);
        }

        private int[] RiseMarks(OutlookBook book)
        {
            if (book == null)
            {
                return new int[0];
            }
            int span = NewsRules.Span(_range);
            book.Window(OutlookCatalogue.Rate, book.LastDay, span, Raw);
            var marks = new List<int>(4);
            int last = Raw[0];
            for (int i = 1; i < span && marks.Count < 4; i++)
            {
                if (Raw[i] != OutlookCatalogue.NoValue && last != OutlookCatalogue.NoValue && Raw[i] > last)
                {
                    marks.Add(i);
                }
                if (Raw[i] != OutlookCatalogue.NoValue)
                {
                    last = Raw[i];
                }
            }
            return marks.ToArray();
        }

        private string Rises(OutlookBook book)
        {
            if (book == null)
            {
                return OutlookText.T("reallife_news_no_rise");
            }
            int span = NewsRules.Span(_range);
            book.Window(OutlookCatalogue.Rate, book.LastDay, span, Raw);
            string days = string.Empty;
            int last = Raw[0];
            for (int i = 1; i < span; i++)
            {
                if (Raw[i] != OutlookCatalogue.NoValue && last != OutlookCatalogue.NoValue && Raw[i] > last)
                {
                    int day = book.LastDay - span + 1 + i;
                    days = days.Length == 0 ? day.ToString(Inv) : days + ", " + day.ToString(Inv);
                }
                if (Raw[i] != OutlookCatalogue.NoValue)
                {
                    last = Raw[i];
                }
            }
            return days.Length == 0 ? OutlookText.T("reallife_news_no_rise") : OutlookText.T("reallife_news_rise", "days", days);
        }

        private UiActionModel[] Ranges()
        {
            return new[]
            {
                new UiActionModel { Label = OutlookText.T("reallife_news_range_30"), Kind = _range == 0 ? UiButtonKind.Chip : UiButtonKind.Secondary, Command = UiAppWindow.ActionCommand, Arg = 0 },
                new UiActionModel { Label = OutlookText.T("reallife_news_range_120"), Kind = _range == 1 ? UiButtonKind.Chip : UiButtonKind.Secondary, Command = UiAppWindow.ActionCommand, Arg = 1 },
                new UiActionModel { Label = OutlookText.T("reallife_news_range_year"), Kind = _range == 2 ? UiButtonKind.Chip : UiButtonKind.Secondary, Command = UiAppWindow.ActionCommand, Arg = 2 }
            };
        }

        private NewsAdvice[] Advice(OutlookBook book)
        {
            var list = new List<NewsAdvice>(4);
            int under = 0;
            CompanyState company = CompanyDirector.Instance != null ? CompanyDirector.Instance.State : null;
            if (company != null)
            {
                for (int i = 0; i < company.Staff.Count; i++)
                {
                    under += NewsRules.Underpaid(company.Staff[i].Going, company.Staff[i].Paid);
                }
            }
            if (under > 0)
            {
                list.Add(new NewsAdvice
                {
                    Icon = UiIcon.People,
                    Title = OutlookText.T("reallife_news_pay_title"),
                    Body = OutlookText.T("reallife_news_pay_body", "count", under.ToString(Inv)),
                    Link = OutlookText.T("reallife_news_open_staff"),
                    Target = 1
                });
            }
            int saving = LoanSaving(out _);
            if (saving > 0)
            {
                list.Add(new NewsAdvice
                {
                    Icon = UiIcon.Bank,
                    Title = OutlookText.T("reallife_news_fix_title"),
                    Body = OutlookText.T("reallife_news_fix_body", "money", LedgerText.Money(saving)),
                    Link = OutlookText.T("reallife_news_fix_do"),
                    Target = 4
                });
            }
            if (Defensive())
            {
                list.Add(new NewsAdvice
                {
                    Icon = UiIcon.ChartLineUp,
                    Title = OutlookText.T("reallife_news_def_title"),
                    Body = OutlookText.T("reallife_news_def_body"),
                    Link = OutlookText.T("reallife_news_open_stocks"),
                    Target = 2
                });
            }
            PullFell(book, out string district, out int dropped);
            if (dropped > 0 && list.Count < 5)
            {
                list.Add(new NewsAdvice
                {
                    Icon = UiIcon.MapPin,
                    Title = OutlookText.T("reallife_news_pull_title"),
                    Body = district + " " + OutlookText.T("reallife_news_pull_body", "points", dropped.ToString(Inv)),
                    Link = OutlookText.T("reallife_news_open_map"),
                    Target = 3
                });
            }
            OutlookInsights.Build(Look, Insights);
            for (int i = 0; i < Insights.Count && list.Count < 5; i++)
            {
                list.Add(new NewsAdvice
                {
                    Icon = UiIcon.Lightbulb,
                    Title = OutlookText.Words.Head(Insights[i]),
                    Body = OutlookText.Words.Mean(Insights[i]),
                    Link = OutlookText.T("reallife_news_open_finance"),
                    Target = 0
                });
            }
            return list.ToArray();
        }

        private static int LoanSaving(out int index)
        {
            index = -1;
            var live = new List<LoanView>(4);
            GameLoans.Read(live);
            int best = 0;
            int at = -1;
            for (int i = 0; i < live.Count; i++)
            {
                if (live[i].Remaining > best && live[i].Interest > 0)
                {
                    best = (int)live[i].Remaining;
                    at = i;
                }
            }
            if (at < 0)
            {
                return 0;
            }
            int score = CreditLedger.Value;
            int own = Look.RateBp;
            int saving = NewsRules.Offer(best, live[at].Interest, score, own, AccountRules.DaysPerYear, out int fixedDaily);
            bool rising = Look.Pending && Look.PendingMove > 0;
            if (!NewsRules.WillFix(rising || saving > 0, live[at].Interest, fixedDaily))
            {
                return 0;
            }
            index = at;
            return saving;
        }

        private static bool Defensive()
        {
            MarketState state = MarketDirector.Instance != null ? MarketDirector.Instance.State : null;
            if (state == null || state.SectorVar == null || state.SectorVar.Length < 2)
            {
                return false;
            }
            return NewsRules.Defensive(state.SectorVar[0], state.SectorVar[state.SectorVar.Length - 1]);
        }

        private UiDataTableModel GoodsOrDistricts(NewsShape shape, OutlookBook book)
        {
            if (shape == NewsShape.Districts || shape == NewsShape.Overview)
            {
                return shape == NewsShape.Overview ? PriceTable(false) : DistrictTable();
            }
            if (shape == NewsShape.Prices)
            {
                return PriceTable(true);
            }
            return HoldingTable();
        }

        private UiDataTableModel PriceTable(bool full)
        {
            UiDataTableModel priced = RetailTable(full);
            if (priced != null)
            {
                return priced;
            }
            var rows = new List<UiDataRow>(6);
            for (int i = 0; i < OutlookWords.KindKeys.Length && i < Look.Supply.Length; i++)
            {
                int factor = Look.Supply[i].Factor;
                int way = factor > 1000 ? 1 : factor < 1000 ? -1 : 0;
                string move = way == 0 ? "0%" : (way > 0 ? "+" : "") + ((factor - 1000) / 10f).ToString("0.0", Inv) + "%";
                rows.Add(new UiDataRow
                {
                    Cells = new[] { OutlookText.T(OutlookWords.KindKeys[i]), (factor / 10f).ToString("0.0", Inv), move, string.Empty },
                    Values = new[] { 0, 0, way, 0 },
                    Tones = new[] { UiRole.None, UiRole.None, UiRole.Danger, UiRole.None }
                });
            }
            return GoodsColumns(rows, false);
        }

        // The player's retail series, one row a good. The prices page adds the week and your price. The series is that shop price.
        private UiDataTableModel RetailTable(bool full)
        {
            DaySeriesBook series = DaySeriesDirector.Book;
            if (series == null || series.LastDay <= 0)
            {
                return null;
            }
            List<string> names = series.Names();
            var rows = new List<UiDataRow>(8);
            int end = series.LastDay;
            for (int i = 0; i < names.Count && rows.Count < 8; i++)
            {
                string key = names[i];
                if (key == null || !key.StartsWith(DaySeriesCatalogue.PricePrefix, StringComparison.Ordinal) || key.Length <= DaySeriesCatalogue.PricePrefix.Length)
                {
                    continue;
                }
                int found = series.Window(key, end, NewsRules.DaysBook, Raw);
                if (found <= 0)
                {
                    continue;
                }
                int today = LastPriced(NewsRules.DaysBook);
                if (today == DaySeriesCatalogue.NoValue)
                {
                    continue;
                }
                int week = PricedAt(NewsRules.DaysBook - 8);
                int month = PricedAt(NewsRules.DaysBook - 1 - NewsRules.DaysMonth);
                int way;
                string move = Move(today, month == DaySeriesCatalogue.NoValue ? week : month, out way);
                string good = key.Substring(DaySeriesCatalogue.PricePrefix.Length);
                var line = new float[NewsRules.DaysBook];
                float carry = today / 100f;
                for (int d = 0; d < NewsRules.DaysBook; d++)
                {
                    if (Raw[d] != DaySeriesCatalogue.NoValue)
                    {
                        carry = Raw[d] / 100f;
                    }
                    line[d] = carry;
                }
                rows.Add(new UiDataRow
                {
                    Cells = new[] { good, Cents(today), Move(today, week, out _), move, string.Empty, Cents(today) },
                    Values = new[] { 0, 0, 0, way, 0, 0 },
                    Tones = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.Danger, way < 0 ? UiRole.Ok : UiRole.Danger, UiRole.None },
                    Line = line
                });
            }
            return rows.Count == 0 ? null : GoodsColumns(rows, full);
        }

        private static int LastPriced(int span)
        {
            for (int i = span - 1; i >= 0; i--)
            {
                if (Raw[i] != DaySeriesCatalogue.NoValue)
                {
                    return Raw[i];
                }
            }
            return DaySeriesCatalogue.NoValue;
        }

        private static int PricedAt(int index)
        {
            if (index < 0)
            {
                return DaySeriesCatalogue.NoValue;
            }
            return Raw[index] == DaySeriesCatalogue.NoValue ? DaySeriesCatalogue.NoValue : Raw[index];
        }

        private static string Cents(int cents) => (cents / 100.0).ToString("0.00", Inv);

        private static string Move(int now, int then, out int way)
        {
            way = 0;
            if (now == DaySeriesCatalogue.NoValue || then == DaySeriesCatalogue.NoValue || then <= 0)
            {
                return string.Empty;
            }
            double pct = (now - then) * 100.0 / then;
            way = pct > 0.05 ? 1 : pct < -0.05 ? -1 : 0;
            return (pct > 0 ? "+" : string.Empty) + pct.ToString("0.0", Inv) + "%";
        }

        private static UiDataTableModel GoodsColumns(List<UiDataRow> rows, bool full)
        {
            UiDataColumn[] columns = full
                ? new[]
                {
                    new UiDataColumn { Title = OutlookText.T("reallife_news_col_good"), Kind = UiCellKind.Text },
                    new UiDataColumn { Title = OutlookText.T("reallife_news_col_today"), Kind = UiCellKind.Text, Width = 96f },
                    new UiDataColumn { Title = OutlookText.T("reallife_news_col_week"), Kind = UiCellKind.Trend, Width = 112f },
                    new UiDataColumn { Title = OutlookText.T("reallife_news_col_month"), Kind = UiCellKind.Trend, Width = 112f },
                    new UiDataColumn { Title = OutlookText.T("reallife_news_col_spark"), Kind = UiCellKind.Spark, Width = 96f },
                    new UiDataColumn { Title = OutlookText.T("reallife_news_col_yours"), Kind = UiCellKind.Text, Width = 112f }
                }
                : new[]
                {
                    new UiDataColumn { Title = OutlookText.T("reallife_news_col_good"), Kind = UiCellKind.Text },
                    new UiDataColumn { Title = OutlookText.T("reallife_news_col_today"), Kind = UiCellKind.Text, Width = 96f },
                    new UiDataColumn { Title = OutlookText.T("reallife_news_col_month"), Kind = UiCellKind.Trend, Width = 128f },
                    new UiDataColumn { Title = OutlookText.T("reallife_news_col_spark"), Kind = UiCellKind.Spark, Width = 96f }
                };
            if (!full)
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    UiDataRow row = rows[i];
                    if (row.Cells == null || row.Cells.Length < 6)
                    {
                        continue;
                    }
                    row.Cells = new[] { row.Cells[0], row.Cells[1], row.Cells[3], string.Empty };
                    row.Values = new[] { 0, 0, row.Values[3], 0 };
                    row.Tones = new[] { UiRole.None, UiRole.None, UiRole.Danger, row.Tones != null && row.Tones.Length > 4 ? row.Tones[4] : UiRole.Accent };
                    rows[i] = row;
                }
            }
            return new UiDataTableModel
            {
                Columns = columns,
                Rows = rows.ToArray(),
                RowCount = rows.Count,
                EmptyTitle = OutlookText.T("reallife_news_empty_city"),
                EmptyBody = OutlookText.T("reallife_news_empty_city_body"),
                EmptyIcon = UiIcon.Tag
            };
        }

        private UiDataTableModel DistrictTable()
        {
            var rows = new List<UiDataRow>(Look.DistrictCount);
            for (int i = 0; i < Look.DistrictCount; i++)
            {
                OutlookDistrict d = Look.Districts[i];
                int pull = d.Pull < 0 ? 0 : d.Pull > 100 ? 100 : d.Pull;
                string name = OutlookText.Words.District != null ? OutlookText.Words.District(d.Id) : d.Id;
                rows.Add(new UiDataRow
                {
                    Cells = new[] { string.IsNullOrEmpty(name) ? d.Id : name, d.Pull.ToString(Inv), string.Empty, LedgerText.Money(d.Land) },
                    Values = new[] { (int)UiIcon.MapPin, pull, 0, 0 },
                    Tones = new[] { UiTokens.Chart(i), UiRole.None, UiRole.None, UiRole.None }
                });
            }
            return new UiDataTableModel
            {
                Columns = new[]
                {
                    new UiDataColumn { Title = OutlookText.T("reallife_news_col_district"), Kind = UiCellKind.Tile },
                    new UiDataColumn { Title = OutlookText.T("reallife_news_col_pull"), Kind = UiCellKind.Meter, Width = 160f },
                    new UiDataColumn { Title = OutlookText.T("reallife_news_col_week"), Kind = UiCellKind.Trend, Width = 112f },
                    new UiDataColumn { Title = OutlookText.T("reallife_news_col_rent"), Kind = UiCellKind.Text, Width = 112f }
                },
                Rows = rows.ToArray(),
                RowCount = rows.Count,
                EmptyTitle = OutlookText.T("reallife_news_empty_city"),
                EmptyBody = OutlookText.T("reallife_news_empty_city_body"),
                EmptyIcon = UiIcon.MapTrifold
            };
        }

        private static UiDataTableModel HoldingTable()
        {
            return Table(new[] { "reallife_news_col_good", "reallife_news_col_today" }, new List<UiDataRow>(0));
        }

        private static UiDataTableModel Table(string[] keys, List<UiDataRow> rows)
        {
            var columns = new UiDataColumn[keys.Length];
            for (int i = 0; i < keys.Length; i++)
            {
                columns[i] = new UiDataColumn { Title = OutlookText.T(keys[i]), Kind = i == 0 ? UiCellKind.Text : UiCellKind.Text, Width = i == 0 ? 0f : 120f };
            }
            return new UiDataTableModel
            {
                Columns = columns,
                Rows = rows.ToArray(),
                RowCount = rows.Count,
                EmptyTitle = OutlookText.T("reallife_news_empty_city"),
                EmptyBody = OutlookText.T("reallife_news_empty_city_body"),
                EmptyIcon = UiIcon.Chart
            };
        }

        private void FillPeople(NewsModel model)
        {
            model.Shape = NewsShape.People;
            model.Title = OutlookText.T("reallife_news_people_title");
            var ageCounts = new int[3];
            var incomeCounts = new int[3];
            int people = CountPeople(ageCounts, incomeCounts, out int homes, out string busy);
            var ageShares = new float[3];
            var incomeShares = new float[3];
            NewsRules.Shares(ageCounts, ageShares);
            NewsRules.Shares(incomeCounts, incomeShares);
            model.People = new UiInfoViewModel
            {
                Title = OutlookText.T("reallife_news_people_title"),
                Icon = UiIcon.People,
                Views = new[] { UiIcon.People, UiIcon.Home, UiIcon.Briefcase, UiIcon.Bank, UiIcon.Store },
                ViewTints = new[] { UiRole.Accent, UiRole.Ok, UiRole.Warn, UiRole.Chart3, UiRole.Chart2 },
                View = _view < 0 ? 0 : _view > 4 ? 4 : _view,
                HeadLabel = OutlookText.T("reallife_news_head_people"),
                HeadValue = people.ToString(Inv),
                Rows = new[]
                {
                    new UiInfoRow { Label = OutlookText.T("reallife_news_head_homes"), Value = homes.ToString(Inv) },
                    new UiInfoRow { Label = OutlookText.T("reallife_news_busy_link"), Value = busy }
                },
                ChartTitle = OutlookText.T("reallife_news_ages"),
                ChartLabels = new[] { OutlookText.T("reallife_news_age_young"), OutlookText.T("reallife_news_age_work"), OutlookText.T("reallife_news_age_older") },
                ChartValues = ShareWords(ageCounts),
                ChartShares = ageShares,
                Chart2Title = OutlookText.T("reallife_news_income_title"),
                Chart2Labels = new[] { OutlookText.T("reallife_news_income_work"), OutlookText.T("reallife_news_income_middle"), OutlookText.T("reallife_news_income_upper") },
                Chart2Values = ShareWords(incomeCounts),
                Chart2Shares = incomeShares,
                LegendTitle = OutlookText.T("reallife_news_legend"),
                ScaleLabel = OutlookText.T("reallife_news_scale"),
                ScaleLow = OutlookText.T("reallife_news_low"),
                ScaleHigh = OutlookText.T("reallife_news_high"),
                Prompts = model.Prompts,
                PromptCount = model.Prompts.Length
            };
            model.Actions = new[]
            {
                new UiActionModel { Label = OutlookText.T("reallife_news_rent_link"), Icon = UiIcon.Home, Command = NewsCommands.Rent, Arg = 0 },
                new UiActionModel { Label = OutlookText.T("reallife_news_busy_link"), Icon = UiIcon.MapPin, Command = NewsCommands.Busy, Arg = 0 }
            };
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
            NetState net = director.Net(out _);
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
                model.Shape = NewsShape.Reading;
                model.Reader = ReaderOf(director.Latest);
                return;
            }
            model.Shape = NewsShape.Stand;
            NewsstandState state = director.Online;
            int weekly = state != null ? NewsstandRules.Weekly(state, RealLifeOverhaul.Prices.Level) : 0;
            int level = RealLifeOverhaul.Prices.Level;
            int titles = 0;
            for (int i = 0; i < NewsstandCatalogue.Titles; i++)
            {
                titles += NewsstandRules.WeekPrice(i, level);
            }
            model.Note = OutlookText.T("reallife_news_stand_sum", "count", NewsstandRules.Running(state).ToString(Inv), "money", LedgerText.Money(weekly));
            model.Note2 = OutlookText.T("reallife_news_stand_save", "money", LedgerText.Money(NewsRules.PassSaving(titles, NewsstandRules.WeekPrice(NewsstandCatalogue.Pass, level))));
            var cards = new UiCatalogueCard[NewsstandCatalogue.Subscriptions];
            for (int i = 0; i < cards.Length; i++)
            {
                bool on = state != null && state.Subs[i].Subscribed && !state.Subs[i].Cancelled;
                cards[i] = new UiCatalogueCard
                {
                    Icon = UiIcon.Newspaper,
                    Name = LedgerText.T(NewsstandCatalogue.NameKeys[i]),
                    Figure = LedgerText.Money(director.OnlinePrice(i)),
                    State = on ? OutlookText.T("reallife_news_stand_on") : null,
                    StateTone = UiRole.Ok
                };
            }
            model.Catalogue = new UiCatalogueModel
            {
                Cards = cards,
                CardCount = cards.Length,
                Actions = new[]
                {
                    new UiActionModel { Label = OutlookText.T("reallife_news_stand_sub"), Kind = UiButtonKind.Primary, Key = UiAction.Alt },
                    new UiActionModel { Label = OutlookText.T("reallife_news_stand_stop"), Kind = UiButtonKind.Secondary, Key = UiAction.Option }
                }
            };
        }

        private bool Subscription(NewsModel model, int arg)
        {
            LedgerDirector director = LedgerDirector.Instance;
            if (director == null || model.Catalogue == null)
            {
                return false;
            }
            int sub = model.Catalogue.Chosen >= 0 ? model.Catalogue.Chosen : 0;
            if (arg == 0)
            {
                director.SubscribeOnline(sub, out _);
            }
            else
            {
                director.CancelOnline(sub);
            }
            _local++;
            return true;
        }

        private void FillPaper(NewsModel model)
        {
            model.Icon = UiIcon.Newspaper;
            LedgerDirector director = LedgerDirector.Instance;
            LedgerEdition edition = director != null ? director.Latest : null;
            if (edition == null)
            {
                model.Shape = NewsShape.Notice;
                model.Title = OutlookText.T("reallife_ledger_title");
                model.EmptyTitle = OutlookText.T("reallife_news_paper_quiet");
                model.EmptyBody = OutlookText.T("reallife_news_paper_quiet_body");
                return;
            }
            model.Shape = NewsShape.Paper;
            model.Title = OutlookText.T("reallife_ledger_title") + "  " + NewsRules.YearOf(edition.Day).ToString(Inv) + "  " + NewsRules.EditionOf(edition.Day).ToString(Inv);
            model.Edition = Edition(edition, OutlookDirector.Instance != null ? OutlookDirector.Instance.History : null);
        }

        private static UiEditionModel Edition(LedgerEdition edition, OutlookBook book)
        {
            LedgerPiece lead = null;
            for (int i = 0; i < edition.Pieces.Count; i++)
            {
                if (lead == null || edition.Pieces[i].Weight > lead.Weight)
                {
                    lead = edition.Pieces[i];
                }
            }
            var sections = new List<UiEditionSection>(4);
            for (int s = 0; s < (int)LedgerSection.Count; s++)
            {
                var stories = new List<UiEditionStory>(4);
                for (int i = 0; i < edition.Pieces.Count; i++)
                {
                    if ((int)edition.Pieces[i].Section != s || edition.Pieces[i] == lead)
                    {
                        continue;
                    }
                    stories.Add(Story(edition.Pieces[i]));
                }
                if (stories.Count > 0)
                {
                    sections.Add(new UiEditionSection { Icon = SectionIcon((LedgerSection)s), Label = SectionName((LedgerSection)s), Stories = stories.ToArray() });
                }
            }
            return new UiEditionModel
            {
                LeadIcon = lead != null ? SectionIcon(lead.Section) : UiIcon.Newspaper,
                LeadTint = UiRole.Accent,
                LeadKicker = lead != null ? SectionName(lead.Section) : string.Empty,
                LeadHeadline = lead != null ? PieceHead(lead) : OutlookText.T("reallife_news_paper_quiet"),
                LeadStandfirst = lead != null ? PieceBody(lead) : OutlookText.T("reallife_news_paper_quiet_body"),
                Markets = MarketFigures(edition, book),
                Sections = Openings(sections),
                QuietLine = edition.Pieces.Count < 3 ? OutlookText.T("reallife_news_paper_quiet_body") : null
            };
        }

        private static UiFigureModel[] MarketFigures(LedgerEdition edition, OutlookBook book)
        {
            float[] prices = SparkOf(book, OutlookCatalogue.Prices, 12);
            float[] rate = SparkOf(book, OutlookCatalogue.Rate, 12);
            return new[]
            {
                new UiFigureModel
                {
                    Label = OutlookText.T("reallife_news_series_index"),
                    Value = edition.IndexLevel <= 0 ? "-" : (edition.IndexLevel / 100f).ToString("0.00", Inv),
                    Trend = prices,
                    Good = true
                },
                new UiFigureModel
                {
                    Label = OutlookText.T("reallife_news_series_rate"),
                    Value = edition.RateBp < 0 ? "-" : (edition.RateBp / 100f).ToString("0.00", Inv),
                    Trend = rate,
                    Good = false
                }
            };
        }

        private static float[] SparkOf(OutlookBook book, string key, int span)
        {
            if (book == null || book.LastDay <= 0)
            {
                return null;
            }
            var raw = new int[span];
            if (book.Window(key, book.LastDay, span, raw) < 2)
            {
                return null;
            }
            var line = new float[span];
            float carry = 0f;
            bool any = false;
            for (int i = 0; i < span; i++)
            {
                if (raw[i] != OutlookCatalogue.NoValue)
                {
                    carry = raw[i];
                    any = true;
                }
                line[i] = carry;
            }
            return any ? line : null;
        }

        private static UiEditionStory Story(LedgerPiece piece)
        {
            return new UiEditionStory { Headline = PieceHead(piece), Body = PieceBody(piece), Byline = SectionName(piece.Section) };
        }

        private static string PieceHead(LedgerPiece piece)
        {
            LedgerText.Words(piece, out _, out string headline, out _);
            return string.IsNullOrEmpty(headline) ? OutlookText.T("reallife_news_paper_quiet") : headline;
        }

        private static string PieceBody(LedgerPiece piece)
        {
            LedgerText.Words(piece, out _, out _, out string body);
            return string.IsNullOrEmpty(body) ? OutlookText.T("reallife_news_paper_quiet_body") : body;
        }

        private static string SectionName(LedgerSection section)
        {
            int i = (int)section;
            if (i < 0 || i >= LedgerCatalogue.SectionKeys.Length)
            {
                return OutlookText.T("reallife_news_sec_city");
            }
            return LedgerText.T(LedgerCatalogue.SectionKeys[i]);
        }

        private static UiIcon SectionIcon(LedgerSection section)
        {
            switch (section)
            {
                case LedgerSection.Economy: return UiIcon.Chart;
                case LedgerSection.Business: return UiIcon.Store;
                case LedgerSection.Markets: return UiIcon.ChartLineUp;
                case LedgerSection.People: return UiIcon.People;
                case LedgerSection.AroundTown: return UiIcon.MapPin;
                case LedgerSection.Weather: return UiIcon.Cloud;
                default: return UiIcon.Newspaper;
            }
        }

        private static UiEditionSection[] Openings(List<UiEditionSection> sections)
        {
            int opened = Population.CityPopulation.FirmsOpened;
            string firm = OpenedFirm();
            if (opened > 0 && firm.Length > 0)
            {
                sections.Add(new UiEditionSection
                {
                    Icon = UiIcon.Store,
                    Label = OutlookText.T("reallife_news_sec_business"),
                    Stories = new[]
                    {
                        new UiEditionStory
                        {
                            Headline = firm,
                            Body = OutlookText.T("reallife_news_open_body", "count", opened.ToString(Inv)),
                            Byline = OutlookText.T("reallife_news_sec_city")
                        }
                    }
                });
            }
            return sections.ToArray();
        }

        // A city firm that is not the player's, named as the game names the business. Empty when none has opened.
        private static string OpenedFirm()
        {
            PopulationState pop = Population.PopulationDirector.Instance != null ? Population.PopulationDirector.Instance.State : null;
            if (pop == null || pop.Growth.Opened <= 0)
            {
                return string.Empty;
            }
            for (int e = pop.Employers.Count - 1; e >= 0; e--)
            {
                if (pop.Employers.Players[e])
                {
                    continue;
                }
                string key = pop.Employers.Key[e];
                string name = Population.PopulationGame.BusinessName(key);
                if (!string.IsNullOrEmpty(name) && name != key)
                {
                    return name;
                }
            }
            return string.Empty;
        }

        private static UiReaderModel ReaderOf(LedgerEdition edition)
        {
            if (edition == null || edition.Pieces.Count == 0)
            {
                return new UiReaderModel
                {
                    Title = OutlookText.T("reallife_ledger_title"),
                    Blocks = new[] { new UiReaderBlock { Kind = UiReaderKind.Paragraph, Text = OutlookText.T("reallife_news_paper_quiet_body") } }
                };
            }
            var blocks = new List<UiReaderBlock>(edition.Pieces.Count * 2);
            for (int i = 0; i < edition.Pieces.Count; i++)
            {
                blocks.Add(new UiReaderBlock { Kind = UiReaderKind.Heading, Text = PieceHead(edition.Pieces[i]) });
                blocks.Add(new UiReaderBlock { Kind = UiReaderKind.Paragraph, Text = PieceBody(edition.Pieces[i]) });
            }
            return new UiReaderModel
            {
                Title = OutlookText.T("reallife_ledger_title") + " " + NewsRules.YearOf(edition.Day).ToString(Inv) + " " + NewsRules.EditionOf(edition.Day).ToString(Inv),
                Blocks = blocks.ToArray()
            };
        }

        private static string Facts(LifeStory story)
        {
            int rank = 0;
            RivalFile file = RivalDirector.Instance != null ? RivalDirector.Instance.File : null;
            if (file != null)
            {
                rank = RivalRules.Rank(file, story.Worth, RealLifeOverhaul.Prices.Level);
            }
            string text = OutlookText.T("reallife_news_end_days") + " " + story.Day.ToString(Inv)
                + ". " + OutlookText.T("reallife_news_end_shops") + " " + story.Owned.ToString(Inv)
                + ". " + OutlookText.T("reallife_news_end_staff") + " " + story.Employees.ToString(Inv)
                + ". " + OutlookText.T("reallife_news_end_worth") + " " + LedgerText.Money((int)Math.Min(int.MaxValue, Math.Max(int.MinValue, story.Worth)));
            if (rank > 0)
            {
                text = text + ". " + OutlookText.T("reallife_news_end_rank") + " " + rank.ToString(Inv);
            }
            return text;
        }

        private void FillEnding(NewsModel model)
        {
            model.Shape = NewsShape.Ending;
            LifeStory story = EndingStory ?? new LifeStory();
            ObituaryPage page = PermadeathText.Page(story, false);
            string obituary = page.Body.Length > 0 ? page.Body : Facts(story);
            model.Decision = new UiDecisionModel
            {
                Kicker = page.Kicker.Length > 0 ? page.Kicker : OutlookText.T("reallife_news_end_kicker"),
                Title = story.Name.Length > 0 ? story.Name : page.Headline,
                Emblem = UiIcon.Person,
                Faces = story.Name.Length > 0 ? new[] { story.Name } : new string[0],
                Body = obituary,
                Prompts = model.Prompts,
                PromptCount = model.Prompts.Length,
                Choices = new[]
                {
                    new UiDecisionChoice
                    {
                        Key = UiAction.Pick1,
                        Name = OutlookText.T("reallife_news_end_load"),
                        Lines = new[] { new UiConsequence { Text = OutlookText.T("reallife_news_end_days") + " " + story.Day.ToString(Inv), Tone = 0 } }
                    },
                    new UiDecisionChoice
                    {
                        Key = UiAction.Pick2,
                        Name = OutlookText.T("reallife_news_end_quit"),
                        Lines = new[] { new UiConsequence { Text = OutlookText.T("reallife_news_end_worth"), Tone = 0 } }
                    }
                }
            };
        }

        private void FillHome(NewsModel model)
        {
            model.Shape = NewsShape.Household;
            string name = OutlookText.T("reallife_news_home_title");
            string rent = LedgerText.Money(0);
            string size = "0";
            PopulationState pop = Population.PopulationDirector.Instance != null ? Population.PopulationDirector.Instance.State : null;
            if (pop != null && _home >= 0 && _home < pop.Households.Count && pop.Households.Alive(_home))
            {
                int head = pop.Households.First[_home];
                if (head >= 0)
                {
                    name = Population.PopulationGame.NameOf(Population.PopulationViews.NameSeed(pop, head), pop.People.Female[head] == 1);
                }
                rent = LedgerText.Money(pop.Households.Rent[_home] / 100);
                size = pop.Households.Size[_home].ToString(Inv);
            }
            model.Household = new UiInfoModel
            {
                Title = name,
                Icon = UiIcon.Home,
                Prompts = model.Prompts,
                PromptCount = model.Prompts.Length,
                Sections = new[]
                {
                    new UiInfoSection
                    {
                        Label = OutlookText.T("reallife_news_home_title"),
                        Rows = new[]
                        {
                            new UiInfoRow { Label = OutlookText.T("reallife_news_head_people"), Value = size },
                            new UiInfoRow { Label = OutlookText.T("reallife_news_col_rent"), Value = rent }
                        }
                    }
                }
            };
        }

        private void FillRenters(NewsModel model)
        {
            model.Shape = NewsShape.Renters;
            model.Title = OutlookText.T("reallife_news_rent_link");
            model.Icon = UiIcon.Home;
            model.Table = RentTable();
            model.EmptyTitle = OutlookText.T("reallife_news_empty_rent");
            model.EmptyBody = OutlookText.T("reallife_news_empty_rent_body");
        }

        private int CountPeople(int[] ages, int[] incomes, out int homes, out string busy)
        {
            homes = 0;
            busy = string.Empty;
            int people = 0;
            PopulationState pop = Population.PopulationDirector.Instance != null ? Population.PopulationDirector.Instance.State : null;
            if (pop == null)
            {
                return 0;
            }
            var inDistrict = new int[256];
            for (int p = 0; p < pop.People.Count; p++)
            {
                if (pop.People.Id[p] == 0)
                {
                    continue;
                }
                people++;
                int age = pop.Age(p, pop.Day);
                if (age < 25)
                {
                    ages[0]++;
                }
                else if (age < 55)
                {
                    ages[1]++;
                }
                else
                {
                    ages[2]++;
                }
            }
            int busiest = 0;
            int busiestN = 0;
            for (int h = 0; h < pop.Households.Count; h++)
            {
                if (!pop.Households.Alive(h))
                {
                    continue;
                }
                homes++;
                int c = pop.Households.Class[h];
                if (c >= 0 && c < incomes.Length)
                {
                    incomes[c]++;
                }
                int d = pop.Households.District[h];
                inDistrict[d]++;
                if (inDistrict[d] > busiestN)
                {
                    busiestN = inDistrict[d];
                    busiest = d;
                }
            }
            if (busiestN > 0 && busiest < Look.DistrictCount)
            {
                string id = Look.Districts[busiest].Id;
                string named = OutlookText.Words.District != null ? OutlookText.Words.District(id) : id;
                busy = string.IsNullOrEmpty(named) ? id : named;
            }
            return people;
        }

        private static string[] ShareWords(int[] counts)
        {
            var words = new string[counts.Length];
            for (int i = 0; i < counts.Length; i++)
            {
                words[i] = counts[i].ToString(Inv);
            }
            return words;
        }

        private UiDataTableModel RentTable()
        {
            var rows = new List<UiDataRow>(6);
            var slots = new int[6];
            var rents = new int[6];
            int n = 0;
            PopulationState pop = Population.PopulationDirector.Instance != null ? Population.PopulationDirector.Instance.State : null;
            if (pop != null)
            {
                for (int h = 0; h < pop.Households.Count; h++)
                {
                    if (!pop.Households.Alive(h) || pop.Households.Rent[h] <= 0)
                    {
                        continue;
                    }
                    int rent = pop.Households.Rent[h];
                    int place = n < 6 ? n : 5;
                    if (n < 6 || rent > rents[5])
                    {
                        int at = place;
                        while (at > 0 && rent > rents[at - 1])
                        {
                            if (at < 6)
                            {
                                rents[at] = rents[at - 1];
                                slots[at] = slots[at - 1];
                            }
                            at--;
                        }
                        if (at < 6)
                        {
                            rents[at] = rent;
                            slots[at] = h;
                            if (n < 6)
                            {
                                n++;
                            }
                        }
                    }
                }
            }
            _slots = new int[n];
            for (int i = 0; i < n; i++)
            {
                _slots[i] = slots[i];
                int head = pop.Households.First[slots[i]];
                string name = head >= 0 ? Population.PopulationGame.NameOf(Population.PopulationViews.NameSeed(pop, head), pop.People.Female[head] == 1) : string.Empty;
                rows.Add(new UiDataRow
                {
                    Cells = new[] { name, LedgerText.Money(rents[i] / 100) },
                    Values = new[] { slots[i], rents[i] },
                    Tones = new UiRole[2]
                });
            }
            return Table(new[] { "reallife_news_col_name", "reallife_news_col_rent" }, rows);
        }

        private static bool Open(int target)
        {
            if (target == 4)
            {
                return FixLoan();
            }
            if (target == 1)
            {
                return PhoneApp.OpenById("MyEmployees");
            }
            if (target == 2)
            {
                return PhoneApp.OpenById(MarketDirector.AppId);
            }
            if (target == 3)
            {
                WantedTab = 7;
                return PhoneApp.OpenById(OutlookDirector.AppId);
            }
            return PhoneApp.OpenById(EconomyDirector.AppId);
        }

        private static bool FixLoan()
        {
            int saving = LoanSaving(out int index);
            if (saving <= 0 || index < 0 || CreditLedger.File == null)
            {
                return PhoneApp.OpenById(EconomyDirector.AppId);
            }
            var live = new List<LoanView>(4);
            GameLoans.Read(live);
            if (index >= live.Count)
            {
                return false;
            }
            LoanMark mark = null;
            for (int i = 0; i < CreditLedger.File.Loans.Count; i++)
            {
                LoanMark one = CreditLedger.File.Loans[i];
                if (one.Street == live[index].Street && one.Number == live[index].Number)
                {
                    mark = one;
                    break;
                }
            }
            if (mark == null)
            {
                return PhoneApp.OpenById(EconomyDirector.AppId);
            }
            int interest = live[index].Interest;
            int payment = live[index].Payment;
            NewsRules.Offer((int)live[index].Remaining, interest, CreditLedger.Value, Look.RateBp, AccountRules.DaysPerYear, out int fixedDaily);
            LoanHolds.Begin(mark, LoanHold.Fixed, ref interest, ref payment, GameLoans.Multiplier, AccountRules.DaysPerYear);
            mark.OwnInterest = fixedDaily;
            mark.OwnPayment = payment;
            LoanHolds.Apply(mark, ref interest, ref payment, GameLoans.Multiplier, AccountRules.DaysPerYear);
            GameLoans.WriteTerms(live[index], interest, payment);
            CreditLedger.Touch();
            return true;
        }

        private static bool EndChoice(int arg)
        {
            EndingOpen = false;
            if (arg == 0)
            {
                PermadeathGame.LoadSaves();
                return true;
            }
            PermadeathGame.QuitDesktop();
            return true;
        }
    }
}
