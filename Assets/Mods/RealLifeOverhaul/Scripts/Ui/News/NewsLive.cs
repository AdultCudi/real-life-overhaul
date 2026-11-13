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
    public sealed partial class NewsLive : INewsSource
    {
        public static readonly NewsLive Economy = new NewsLive(NewsApp.Economy);
        public static readonly NewsLive People = new NewsLive(NewsApp.People);
        public static readonly NewsLive Stand = new NewsLive(NewsApp.Stand);
        public static readonly NewsLive Paper = new NewsLive(NewsApp.Paper);
        public static readonly NewsLive Ending = new NewsLive(NewsApp.Ending);
        public static readonly NewsLive Household = new NewsLive(NewsApp.Household);
        public static readonly NewsLive Renters = new NewsLive(NewsApp.Renters);

        public static int WantedTab = -1;

        /// <summary>Steps back from a household to the page it was opened from, from the rent list or a district to the page under it. False when none was open.</summary>
        public bool Back()
        {
            if (!_path.Back())
            {
                return false;
            }
            _local++;
            return true;
        }

        /// <summary>The app is closed: a list, a household or a district opened from a tab is put away, so it opens on its tab, never on that.</summary>
        public void Leave()
        {
            if (_path.Clear())
            {
                _local++;
            }
        }

        /// <summary>The page is asked to show a tab (a notice's link): whatever was open on it is put away, and the tab is taken at the next layout.</summary>
        public void Want(int tab)
        {
            WantedTab = tab;
            _path.Clear();
            _local++;
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
        private static readonly string[] VerdictHeads =
        {
            "reallife_news_head_growing", "reallife_news_head_steady", "reallife_news_head_slowing", "reallife_news_head_overheating", "reallife_news_head_recession"
        };
        private static readonly List<OutlookInsight> Insights = new List<OutlookInsight>(8);
        private readonly NewsApp _app;
        private int _local;
        private readonly NewsPath _path = new NewsPath();
        private int _home = -1;                       // the household of the page open (a place in the population's table)
        private int[] _slots = new int[0];            // the household of each row of the list showing
        private int[] _districtSlots = new int[0];    // the district of each row of the people's table
        private int _range = 1;

        private NewsLive(NewsApp app) { _app = app; }

        public int Generation
        {
            get
            {
                int day = OutlookDirector.Instance != null && OutlookDirector.Instance.History != null ? OutlookDirector.Instance.History.LastDay : 0;
                return day + _local + _range * 1000 + DaySeriesDirector.Generation + (_app == NewsApp.Paper ? PaperStamp() : 0);
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
                case NewsApp.People: model.Tab = 7; FillEconomy(model); break;
                case NewsApp.Stand: FillStand(model); break;
                case NewsApp.Paper: FillPaper(model); break;
                case NewsApp.Ending: FillEnding(model); break;
                case NewsApp.Household: FillHome(model); break;
                case NewsApp.Renters:
                    if (_path.HomeOpen)
                    {
                        FillHome(model);
                    }
                    else
                    {
                        FillRenters(model);
                    }
                    break;
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
            if (command == NewsCommands.Back || command == NewsCommands.Rent || command == NewsCommands.Busy)
            {
                if (!_path.Press(command))
                {
                    return false;
                }
                _local++;
                return true;
            }
            bool row = command == UiDataTable.First + UiDataTable.Row || command == NewsCommands.District + UiDataTable.Row;
            if (row && (_path.RentersOpen || _path.DistrictOpen || _app == NewsApp.Renters) && !_path.HomeOpen && arg >= 0 && arg < _slots.Length)
            {
                _home = _slots[arg];
                if (_app == NewsApp.Renters)
                {
                    _path.OpenRenters();   // the list stands alone: its households open from it
                }
                _path.OpenHome();
                _local++;
                return true;
            }
            if (command == NewsCommands.District + UiDataTable.Row)
            {
                return OpenDistrict(model, arg);
            }
            if ((_app == NewsApp.Economy || _app == NewsApp.People) && command == UiAppWindow.NavCommand)
            {
                _path.Press(command);
                _local++;
                return true;
            }
            if (_app == NewsApp.Ending && command == UiDecision.ChoiceCommand)
            {
                return EndChoice(arg);   // the death card's own choice only: its number is the tabs' too (UiAppWindow.NavCommand), and a tab press must never load the saves or quit the game
            }
            if (_app == NewsApp.Stand && command == NewsCommands.Title)
            {
                return TakeTitle(arg);
            }
            if (_app == NewsApp.Stand && (command == NewsCommands.ReadTitle || command == NewsCommands.ReadDay || command == NewsCommands.ReadPrinted || command == NewsCommands.ToTitles))
            {
                return PressRead(model, command, arg);
            }
            return command == UiAppWindow.NavCommand || command == UiDataTable.First + UiDataTable.Row
                || command == UiChart.First + UiChart.Point || command == UiEdition.StoryCommand || command == UiEdition.LeadCommand;
        }

        private void FillEconomy(NewsModel model)
        {
            model.Icon = UiIcon.Chart;
            model.Title = OutlookText.T("reallife_econapp_app");
            model.Nav = Nav();
            model.StripNote = OutlookText.Day(Look.Day);
            switch (_path.Page(model.Tab))
            {
                case NewsPage.Household: FillHome(model); return;
                case NewsPage.Renters: FillRenters(model); return;
                case NewsPage.District: FillDistrict(model); return;
                case NewsPage.People: FillPeople(model); return;
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
            model.Lead = OutlookText.T(VerdictHeads[(int)verdict < VerdictHeads.Length ? (int)verdict : 1]);
            model.Body = OutlookText.Words.VerdictLine(verdict, Look);
            model.ChipTone = verdict == OutlookVerdict.Growing ? UiRole.OkText : verdict == OutlookVerdict.Recession ? UiRole.DangerText : UiRole.WarnText;
            model.Figures = model.Shape == NewsShape.Overview ? Figures() : model.Shape == NewsShape.Finances ? FinanceFigures(book) : new UiFigureModel[0];
            model.Chart = Chart(book, model.Shape);
            if (model.Shape == NewsShape.Overview && model.Figures.Length == 3)
            {
                int span = NewsRules.Span(_range);
                model.Figures[0].Trend = Tail(Inflation, span);
                model.Figures[1].Trend = Tail(Rate, span);
                int back = Math.Max(0, span - 1 - NewsRules.DaysMonth);
                float rateMove = Rate[span - 1] - Rate[back];
                if (Math.Abs(rateMove) >= 0.005f)
                {
                    model.Figures[1] = Fig("reallife_econapp_tile_rate", (Look.RateBp / 100f).ToString("0.00", Inv) + "%", rateMove, "0.00");
                    model.Figures[1].Trend = Tail(Rate, span);
                }
            }
            model.Bands = model.Shape == NewsShape.Overview ? Bands(book) : new NewsBand[0];
            model.Marks = model.Shape == NewsShape.Overview ? RiseMarks(book) : new int[0];
            model.RiseLine = model.Shape == NewsShape.Overview ? Rises(book) : string.Empty;
            model.AdviceTitle = OutlookText.T("reallife_news_advice");
            model.Advice = model.Shape == NewsShape.Overview || model.Shape == NewsShape.Insights ? Advice(book) : new NewsAdvice[0];
            model.Actions = model.Shape == NewsShape.Overview ? Ranges() : new UiActionModel[0];
            model.Table = TableFor(model.Shape, book);
            model.Table2 = model.Shape == NewsShape.Overview ? DistrictTable(book) : null;
            model.TableTitle = OutlookText.T(model.Shape == NewsShape.Overview ? "reallife_news_prices_today" : model.Shape == NewsShape.Districts ? "reallife_news_districts_pull"
                : "reallife_news_nav_jobs");
            if (model.Shape == NewsShape.Jobs)
            {
                model.TableTitle = null;
            }
            model.Table2Title = OutlookText.T("reallife_news_districts_pull");
            if (model.Shape == NewsShape.Prices)
            {
                FillPrices(model, book);
            }
            else if (model.Shape == NewsShape.Markets)
            {
                FillMarkets(model);
            }
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

        // The three key figures of the overview: inflation and the bank rate over a month, and the city's unemployment over the range chosen.
        private UiFigureModel[] Figures()
        {
            return new[]
            {
                Fig("reallife_econapp_tile_inflation", (Look.Inflation / 100f).ToString("0.0", Inv) + "%", Look.MonthChange / 100f, "0.0"),
                Fig("reallife_econapp_tile_rate", (Look.RateBp / 100f).ToString("0.00", Inv) + "%", 0f, "0.00"),
                Jobless()
            };
        }

        // The city's unemployment as the population counts it (the book's own reading while the population is not running), its change over the range from its own
        // day series, and that series as the spark. With under two days of it there is no change to give: the figure says so by showing none, never a nought.
        private UiFigureModel Jobless()
        {
            int now = Population.CityPopulation.Unemployment;
            now = now >= 0 ? now : Look.Unemployment;
            DaySeriesBook series = DaySeriesDirector.Book;
            var fig = new UiFigureModel { Label = OutlookText.T("reallife_econapp_tile_unemployment"), Value = (now / 10f).ToString("0.0", Inv) + "%" };
            if (series == null || series.LastDay <= 0)
            {
                return fig;
            }
            int span = NewsRules.Span(_range);
            series.Window(DaySeriesCatalogue.Unemployment, series.LastDay, span, Raw);
            fig.Trend = NewsRules.Spark(Raw, span, DaySeriesCatalogue.NoValue, 12);
            if (NewsRules.SeriesChange(Raw, span, DaySeriesCatalogue.NoValue, out _, out int change, out int days) && change != 0)
            {
                fig.Change = (change > 0 ? "+" : "-") + (Math.Abs(change) / 10f).ToString("0.0", Inv) + " " + OutlookText.T("reallife_news_in_days", "days", days.ToString(Inv));
                fig.Rising = change > 0;
                fig.Good = change < 0;
            }
            return fig;
        }

        // The council's books on the Finances page: the reserve, a week's takings and a week's spending, each with its change over the range and its line.
        private UiFigureModel[] FinanceFigures(OutlookBook book)
        {
            return new[]
            {
                BookFigure(book, OutlookCatalogue.Reserve, "reallife_news_series_reserve", Look.Reserve, false),
                BookFigure(book, OutlookCatalogue.Revenue, "reallife_news_series_revenue", Look.Revenue, false),
                BookFigure(book, OutlookCatalogue.Spending, "reallife_news_series_spending", Look.Spending, true)
            };
        }

        // A money figure of the council's: its value now, and its change over the range from the book (kept in thousands) when the book holds two readings.
        private UiFigureModel BookFigure(OutlookBook book, string key, string label, long now, bool lowerIsGood)
        {
            var fig = new UiFigureModel { Label = OutlookText.T(label), Value = MoneyOf(now) };
            if (book == null || book.LastDay <= 0)
            {
                return fig;
            }
            int span = NewsRules.Span(_range);
            book.Window(key, book.LastDay, span, Raw);
            fig.Trend = NewsRules.Spark(Raw, span, OutlookCatalogue.NoValue, 12);
            if (NewsRules.SeriesChange(Raw, span, OutlookCatalogue.NoValue, out _, out int change, out int days) && change != 0)
            {
                fig.Change = (change > 0 ? "+" : "-") + MoneyOf(Math.Abs((long)change) * 1000L) + " " + OutlookText.T("reallife_news_in_days", "days", days.ToString(Inv));
                fig.Rising = change > 0;
                fig.Good = lowerIsGood ? change < 0 : change > 0;
            }
            return fig;
        }

        private static string MoneyOf(long money) => LedgerText.Money((int)Math.Max(int.MinValue, Math.Min(int.MaxValue, money)));

        // A figure with its change over the month in words ("+0.6 in a month"), or none when it did not move.
        private static UiFigureModel Fig(string key, string value, float change, string format)
        {
            bool moved = Math.Abs(change) >= 0.005f;
            return new UiFigureModel
            {
                Label = OutlookText.T(key),
                Value = value,
                Change = moved ? (change > 0 ? "+" : "-") + Math.Abs(change).ToString(format, Inv) + " " + OutlookText.T("reallife_news_in_month") : null,
                Rising = change > 0,
                Good = change < 0
            };
        }

        private UiChartModel Chart(OutlookBook book, NewsShape shape)
        {
            int end = book != null ? book.LastDay : 0;
            int span = NewsRules.DaysShown(NewsRules.Span(_range), end);
            Copy(book, OutlookCatalogue.Inflation, end, span, Inflation);
            Copy(book, OutlookCatalogue.Rate, end, span, Rate);
            Copy(book, OutlookCatalogue.Income, end, span, Wages);
            if (shape == NewsShape.Overview)
            {
                Units(span);
            }
            var whole = new string[span];
            for (int i = 0; i < span; i++)
            {
                whole[i] = OutlookText.T("reallife_news_axis_day", "day", (Math.Max(1, end - span + 1) + i).ToString(Inv));
            }
            string[] points = NewsText.AxisPoints(whole, NewsText.AxisStep(span));
            UiSeries[] series = SeriesFor(book, shape, end, span);
            return new UiChartModel
            {
                Kind = UiChartKind.Line,
                Title = OutlookText.T("reallife_news_chart"),
                Points = points,
                Reads = whole,
                Series = series,
                Format = shape == NewsShape.Finances ? (Func<float, string>)(v => LedgerText.Money((int)Math.Max(int.MinValue, Math.Min(int.MaxValue, v * 1000f))))
                    : v => v.ToString("0.0", Inv),
                EmptyTitle = OutlookText.T("reallife_news_empty_city"),
                EmptyBody = OutlookText.T("reallife_news_empty_city_body")
            };
        }

        // The book's units into the chart's: inflation in per cent a year, the bank rate in per cent, and wages as the growth of the
        // income level over a month taken to a year, so the three lines share one axis.
        private static void Units(int span)
        {
            var level = new float[span];
            Array.Copy(Wages, level, span);
            for (int i = 0; i < span; i++)
            {
                Inflation[i] /= 100f;
                Rate[i] = (OwnBankCatalogue.CityRateStart + Rate[i] * (100 / MacroCatalogue.QuartersPerPoint)) / 100f;
                int w = Math.Min(NewsRules.DaysMonth, i);
                Wages[i] = w == 0 || level[i - w] <= 0f ? 0f : (level[i] / level[i - w] - 1f) * 100f * NewsRules.YearDays / w;
            }
        }

        private UiSeries[] SeriesFor(OutlookBook book, NewsShape shape, int end, int span)
        {
            if (shape == NewsShape.Jobs)
            {
                Copy(book, OutlookCatalogue.Unemployment, end, span, Inflation);
                Copy(book, OutlookCatalogue.Vacancies, end, span, Rate);
                for (int i = 0; i < span; i++)
                {
                    Inflation[i] /= 10f;   // per mille into per cent
                }
                return new[] { new UiSeries { Label = OutlookText.T("reallife_news_series_jobless"), Values = Take(Inflation, span), Tint = UiRole.Danger } };
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
            return new[]
            {
                new UiSeries { Label = OutlookText.T("reallife_news_series_inflation"), Values = Take(Inflation, span), Tint = UiRole.Danger },
                new UiSeries { Label = OutlookText.T("reallife_news_series_rate"), Values = Take(Rate, span), Tint = UiRole.Accent },
                new UiSeries { Label = OutlookText.T("reallife_news_series_wages"), Values = Take(Wages, span), Tint = UiRole.Ok }
            };
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

        private UiDataTableModel TableFor(NewsShape shape, OutlookBook book)
        {
            if (shape == NewsShape.Overview)
            {
                return PriceTable(false);
            }
            if (shape == NewsShape.Districts)
            {
                return DistrictTable(book);
            }
            if (shape == NewsShape.Jobs)
            {
                return JobsTable();
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
            return Table(new[] { "reallife_news_col_name", "reallife_news_col_today" }, rows);
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
            int saving = LoanSaving();
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

        private static bool Defensive()
        {
            MarketState state = MarketDirector.Instance != null ? MarketDirector.Instance.State : null;
            if (state == null || state.SectorVar == null || state.SectorVar.Length < 2)
            {
                return false;
            }
            return NewsRules.Defensive(state.SectorVar[0], state.SectorVar[state.SectorVar.Length - 1]);
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
                    new UiDataColumn { Title = OutlookText.T("reallife_news_col_today"), Kind = UiCellKind.Text, Width = 80f },
                    new UiDataColumn { Title = OutlookText.T("reallife_news_col_month"), Kind = UiCellKind.Trend, Width = 104f },
                    new UiDataColumn { Title = OutlookText.T("reallife_news_col_spark"), Kind = UiCellKind.Spark, Width = 88f }
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

        // The districts by their pull on customers: the pull, its change over the week where the book has a week of it (blank where it has not, never a nought), and
        // what a household there pays a day, counted from the population's records (blank where nobody rents there).
        private UiDataTableModel DistrictTable(OutlookBook book)
        {
            var rows = new List<UiDataRow>(Look.DistrictCount);
            PopulationState pop = Pop;
            PeopleTally people = pop != null ? PeopleTally.Read(pop) : null;
            var window = new int[8];
            for (int i = 0; i < Look.DistrictCount; i++)
            {
                OutlookDistrict d = Look.Districts[i];
                int pull = d.Pull < 0 ? 0 : d.Pull > 100 ? 100 : d.Pull;
                string name = OutlookText.Words.District != null ? OutlookText.Words.District(d.Id) : d.Id;
                string week = string.Empty;
                int moved = 0;
                if (book != null && book.LastDay > 0 && book.Window(OutlookCatalogue.OfDistrictPull(d.Id), book.LastDay, 8, window) >= 2
                    && window[0] != OutlookCatalogue.NoValue && window[7] != OutlookCatalogue.NoValue)
                {
                    int change = window[7] - window[0];
                    moved = change > 0 ? 1 : change < 0 ? -1 : 0;
                    week = change > 0 ? "+" + change.ToString(Inv) : change < 0 ? "-" + (-change).ToString(Inv) : "0";   // a held pull is a true nought
                }
                int rent = people != null ? people.DistrictMeanRent(pop.DistrictOf(d.Id)) : 0;
                rows.Add(new UiDataRow
                {
                    Cells = new[] { string.IsNullOrEmpty(name) ? d.Id : name, d.Pull.ToString(Inv), week, rent > 0 ? LedgerText.Money(rent) : string.Empty },
                    Values = new[] { (int)UiIcon.MapPin, pull, moved, 0 },
                    Tones = new[] { UiTokens.Chart(i), UiRole.None, UiRole.None, UiRole.None }
                });
            }
            return new UiDataTableModel
            {
                Columns = new[]
                {
                    new UiDataColumn { Title = OutlookText.T("reallife_news_col_district"), Kind = UiCellKind.Tile },
                    new UiDataColumn { Title = OutlookText.T("reallife_news_col_pull"), Kind = UiCellKind.Meter, Width = 104f },
                    new UiDataColumn { Title = OutlookText.T("reallife_news_col_week"), Kind = UiCellKind.Trend, Width = 80f },
                    new UiDataColumn { Title = OutlookText.T("reallife_pop_fig_rent"), Kind = UiCellKind.Text, Width = 110f }
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

        private static PopulationState Pop => Population.PopulationDirector.Instance != null ? Population.PopulationDirector.Instance.State : null;

        // The game's words and money for the people's page; the district names are those of the population's table.
        private sealed class Words : INewsWords
        {
            public PopulationState Pop;

            public string T(string key) => OutlookText.T(key);

            public string T(string key, string name, string value) => OutlookText.T(key, name, value);

            public string Money(int whole) => LedgerText.Money(whole);

            public string District(int place) => DistrictName(Pop, place);
        }

        private static readonly Words PageWords = new Words();

        private static string DistrictName(PopulationState pop, int d)
        {
            if (pop == null || d < 0 || d >= pop.Districts.Count)
            {
                return string.Empty;
            }
            string id = pop.Districts[d].Id;
            string named = OutlookText.Words.District != null ? OutlookText.Words.District(id) : id;
            return string.IsNullOrEmpty(named) ? id : named;
        }

        // The city's people: the headline figures counted from the records (the district figures the day keeps are not in a save until its next day),
        // the ages and incomes as donuts, and the districts ranked by where most households live, each a way into its page.
        private void FillPeople(NewsModel model)
        {
            model.Shape = NewsShape.People;
            model.BackLabel = null;
            model.PageTitle = null;
            PopulationState pop = Pop;
            PeopleTally t = PeopleTally.Read(pop);
            _path.Busy = t.Busiest();
            if (t.People == 0)
            {
                model.Shape = NewsShape.Notice;
                model.EmptyTitle = OutlookText.T("reallife_news_empty_city");
                model.EmptyBody = OutlookText.T("reallife_news_empty_city_body");
                model.EmptyIcon = UiIcon.People;
                return;
            }
            PageWords.Pop = pop;
            model.People = NewsPeopleRules.City(t, PageWords, out _districtSlots);
            model.Actions = NewsPeopleRules.Links(t, PageWords);
        }

        // A district's own page, the people's page for that district alone: its figures, its ages and incomes, and who pays most rent in it.
        private void FillDistrict(NewsModel model)
        {
            model.Shape = NewsShape.People;
            PopulationState pop = Pop;
            PeopleTally t = PeopleTally.Read(pop);
            int d = _path.District;
            if (t.People == 0 || d < 0 || d >= t.DistrictPeople.Length)
            {
                _path.Back();
                FillPeople(model);
                return;
            }
            PageWords.Pop = pop;
            model.BackLabel = OutlookText.T("reallife_news_people_title");
            model.PageTitle = null;   // the card carries the district's name
            model.People = NewsPeopleRules.District(t, d, PageWords);
            model.People.Table = RentTable(d);
            model.Actions = new UiActionModel[0];
        }

        private void FillHome(NewsModel model)
        {
            model.Shape = NewsShape.Household;
            PopulationState pop = Pop;
            if (pop == null || _home < 0 || _home >= pop.Households.Count || !pop.Households.Alive(_home))
            {
                _path.HomeGone();   // that household is gone from the city since the list was made: the list it came from shows again
                if (_path.RentersOpen || _app == NewsApp.Renters)
                {
                    FillRenters(model);
                }
                else if (_path.DistrictOpen)
                {
                    FillDistrict(model);
                }
                else
                {
                    FillPeople(model);
                }
                return;
            }
            int head = pop.Households.First[_home];
            string name = head >= 0 ? Population.PopulationGame.NameOf(Population.PopulationViews.NameSeed(pop, head), pop.People.Female[head] == 1) : OutlookText.T("reallife_news_home_title");
            model.BackLabel = _path.RentersOpen || _app == NewsApp.Renters ? OutlookText.T("reallife_news_rent_link") : DistrictName(pop, _path.District);
            model.PageTitle = null;   // the card carries the household's name
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
                            new UiInfoRow { Label = OutlookText.T("reallife_news_head_people"), Value = pop.Households.Size[_home].ToString(Inv) },
                            new UiInfoRow { Label = OutlookText.T("reallife_news_col_district"), Value = DistrictName(pop, pop.Households.District[_home]) },
                            new UiInfoRow { Label = OutlookText.T("reallife_pop_fig_rent"), Value = LedgerText.Money(pop.Households.Rent[_home] / 100) }
                        }
                    }
                }
            };
        }

        private void FillRenters(NewsModel model)
        {
            model.Shape = NewsShape.Renters;
            if (string.IsNullOrEmpty(model.Title))
            {
                model.Title = OutlookText.T("reallife_news_rent_link");
            }
            model.Icon = _app == NewsApp.Renters ? UiIcon.Home : model.Icon;
            model.BackLabel = _app == NewsApp.Economy || _app == NewsApp.People ? OutlookText.T("reallife_news_people_title") : null;
            model.PageTitle = OutlookText.T("reallife_news_rent_link");
            model.Table = RentTable(-1);
            model.EmptyTitle = OutlookText.T("reallife_news_empty_rent");
            model.EmptyBody = OutlookText.T("reallife_news_empty_rent_body");
        }

        private const int RentersMost = 12;

        // The households that pay the most rent, the highest first (an owner's upkeep is not rent): its head's name and what it pays a day. In one district, or in all.
        private UiDataTableModel RentTable(int district)
        {
            var rows = new List<UiDataRow>(RentersMost);
            var slots = new int[RentersMost];
            var rents = new int[RentersMost];
            int n = 0;
            PopulationState pop = Pop;
            if (pop != null)
            {
                for (int h = 0; h < pop.Households.Count; h++)
                {
                    if (!pop.Households.Alive(h) || pop.Households.Landlord[h] == Landlords.Own || pop.Households.Rent[h] <= 0
                        || (district >= 0 && pop.Households.District[h] != district))
                    {
                        continue;
                    }
                    int rent = pop.Households.Rent[h];
                    if (n == RentersMost && rent <= rents[RentersMost - 1])
                    {
                        continue;
                    }
                    int at = n < RentersMost ? n++ : RentersMost - 1;
                    while (at > 0 && rent > rents[at - 1])
                    {
                        rents[at] = rents[at - 1];
                        slots[at] = slots[at - 1];
                        at--;
                    }
                    rents[at] = rent;
                    slots[at] = h;
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
            return Table(new[] { "reallife_news_col_name", "reallife_pop_fig_rent" }, rows);
        }

        // A district row of the districts' table or the overview's: that district's own page. The table's rows are the city's readings in their order; the page is the
        // population's, found by the district's name. A row of the people's own table is a place in its own order.
        private bool OpenDistrict(NewsModel model, int row)
        {
            PopulationState pop = Pop;
            int d = -1;
            if (model.Tab == NewsPath.PeopleTab)
            {
                d = row >= 0 && row < _districtSlots.Length ? _districtSlots[row] : -1;
            }
            else if (pop != null && row >= 0 && row < Look.DistrictCount)
            {
                d = pop.DistrictOf(Look.Districts[row].Id);
            }
            if (!_path.OpenDistrict(d))
            {
                return false;
            }
            _local++;
            return true;
        }

        private static bool Open(int target)
        {
            if (target == 4)
            {
                return NewsLoanFix.Press(FixDesk);   // the question first; the loan is changed only on yes
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
                Economy.Want(3);   // the districts' tab, where a district's pull is read
                return PhoneApp.OpenById(OutlookDirector.AppId);
            }
            return PhoneApp.OpenById(EconomyDirector.AppId);
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
