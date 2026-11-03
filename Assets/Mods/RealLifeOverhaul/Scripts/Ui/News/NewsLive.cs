using System.Collections.Generic;
using System.Globalization;
using RealLifeOverhaul.DaySeries;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.GameUi;
using RealLifeOverhaul.Ledger;
using RealLifeOverhaul.Markets;
using RealLifeOverhaul.Outlook;
using RealLifeOverhaul.Permadeath;
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
                return PhoneApp.OpenById("renters") || true;
            }
            if (command == NewsCommands.Busy)
            {
                _view = 2;
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
            model.Lead = model.Chip;
            model.Body = OutlookText.Words.VerdictLine(verdict, Look);
            model.ChipTone = verdict == OutlookVerdict.Growing ? UiRole.OkText : verdict == OutlookVerdict.Recession ? UiRole.DangerText : UiRole.WarnText;
            model.Figures = Figures();
            model.Chart = Chart(book);
            model.Bands = Bands(book);
            model.RiseLine = Rises(book);
            model.AdviceTitle = OutlookText.T("reallife_news_advice");
            model.Advice = Advice(book);
            model.Actions = Ranges();
            model.Table = model.Shape == NewsShape.Overview || model.Shape == NewsShape.Prices || model.Shape == NewsShape.Districts
                || model.Shape == NewsShape.Markets ? GoodsOrDistricts(model.Shape, book) : null;
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

        private UiChartModel Chart(OutlookBook book)
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
            return new UiChartModel
            {
                Kind = UiChartKind.Line,
                Title = OutlookText.T("reallife_news_chart"),
                Points = points,
                Series = new[]
                {
                    new UiSeries { Label = OutlookText.T("reallife_news_series_inflation"), Values = Take(Inflation, span), Tint = UiRole.Danger },
                    new UiSeries { Label = OutlookText.T("reallife_news_series_rate"), Values = Take(Rate, span), Tint = UiRole.Accent },
                    new UiSeries { Label = OutlookText.T("reallife_news_series_wages"), Values = Take(Wages, span), Tint = UiRole.Ok }
                },
                Format = v => v.ToString("0.0", Inv),
                EmptyTitle = OutlookText.T("reallife_news_empty_city"),
                EmptyBody = OutlookText.T("reallife_news_empty_city_body")
            };
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
                    list.Add(new NewsBand { Label = phase.ToString(Inv), Share = (float)run / span, Tint = PhaseRole(phase) });
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
                return shape == NewsShape.Overview ? PriceTable(book) : DistrictTable();
            }
            if (shape == NewsShape.Prices)
            {
                return PriceTable(book);
            }
            return HoldingTable();
        }

        private UiDataTableModel PriceTable(OutlookBook book)
        {
            var rows = new List<UiDataRow>(6);
            for (int i = 0; i < OutlookWords.KindKeys.Length && i < Look.Supply.Length; i++)
            {
                int factor = Look.Supply[i].Factor;
                string today = (factor / 10f).ToString("0.0", Inv) + "%";
                rows.Add(new UiDataRow
                {
                    Cells = new[] { OutlookText.T(OutlookWords.KindKeys[i]), today, string.Empty, string.Empty, today },
                    Values = new int[5],
                    Tones = new UiRole[5]
                });
            }
            return Table(new[] { "reallife_news_col_good", "reallife_news_col_today", "reallife_news_col_week", "reallife_news_col_month", "reallife_news_col_yours" }, rows);
        }

        private UiDataTableModel DistrictTable()
        {
            var rows = new List<UiDataRow>(Look.DistrictCount);
            for (int i = 0; i < Look.DistrictCount; i++)
            {
                OutlookDistrict d = Look.Districts[i];
                rows.Add(new UiDataRow
                {
                    Cells = new[] { d.Id, d.Pull.ToString(Inv), LedgerText.Money(d.Land) },
                    Values = new[] { 0, d.Pull, 0 },
                    Tones = new[] { UiRole.Text, UiRole.Text, UiRole.Text }
                });
            }
            return Table(new[] { "reallife_news_col_district", "reallife_news_col_pull", "reallife_news_col_rent" }, rows);
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
            model.People = new UiInfoViewModel
            {
                Title = OutlookText.T("reallife_news_people_title"),
                Icon = UiIcon.People,
                Views = new[] { UiIcon.People, UiIcon.Home, UiIcon.Briefcase, UiIcon.Bank, UiIcon.Store },
                ViewTints = new[] { UiRole.Accent, UiRole.Ok, UiRole.Warn, UiRole.Chart3, UiRole.Chart2 },
                View = _view < 0 ? 0 : _view > 4 ? 4 : _view,
                HeadLabel = OutlookText.T("reallife_news_people_title"),
                HeadValue = Look.DistrictCount.ToString(Inv),
                Rows = new[]
                {
                    new UiInfoRow { Label = OutlookText.T("reallife_news_col_district"), Value = Look.DistrictCount.ToString(Inv) }
                },
                ChartTitle = OutlookText.T("reallife_news_ages"),
                ChartLabels = new[] { OutlookText.T("reallife_news_age_young"), OutlookText.T("reallife_news_age_work"), OutlookText.T("reallife_news_age_older") },
                ChartValues = new[] { "1", "1", "1" },
                ChartShares = new[] { 0.22f, 0.58f, 0.2f },
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
                model.Reader = new UiReaderModel
                {
                    Title = OutlookText.T("reallife_ledger_online_app"),
                    Blocks = new[] { new UiReaderBlock { Kind = UiReaderKind.Paragraph, Text = OutlookText.T("reallife_news_paper_quiet_body") } }
                };
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
            model.Edition = Edition(edition);
        }

        private static UiEditionModel Edition(LedgerEdition edition)
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
                    sections.Add(new UiEditionSection { Icon = UiIcon.Newspaper, Label = ((LedgerSection)s).ToString(), Stories = stories.ToArray() });
                }
            }
            return new UiEditionModel
            {
                LeadIcon = UiIcon.Newspaper,
                LeadKicker = lead != null ? lead.Section.ToString() : string.Empty,
                LeadHeadline = lead != null ? LedgerText.Fill(string.IsNullOrEmpty(lead.Head) ? lead.Key : lead.Head, lead.Data) : OutlookText.T("reallife_news_paper_quiet"),
                LeadStandfirst = lead != null ? LedgerText.Fill(lead.Key, lead.Data) : OutlookText.T("reallife_news_paper_quiet_body"),
                Markets = new[]
                {
                    new UiFigureModel { Label = OutlookText.T("reallife_news_series_rate"), Value = edition.RateBp < 0 ? "-" : (edition.RateBp / 100f).ToString("0.00", Inv), Good = true }
                },
                Sections = sections.ToArray(),
                QuietLine = edition.Pieces.Count < 3 ? OutlookText.T("reallife_news_paper_quiet_body") : null
            };
        }

        private static UiEditionStory Story(LedgerPiece piece)
        {
            return new UiEditionStory
            {
                Headline = LedgerText.Fill(string.IsNullOrEmpty(piece.Head) ? piece.Key : piece.Head, piece.Data),
                Body = LedgerText.Fill(piece.Key, piece.Data),
                Byline = piece.Section.ToString()
            };
        }

        private void FillEnding(NewsModel model)
        {
            model.Shape = NewsShape.Ending;
            LifeStory story = EndingStory ?? new LifeStory();
            model.Decision = new UiDecisionModel
            {
                Kicker = OutlookText.T("reallife_news_end_kicker"),
                Title = story.Name.Length > 0 ? story.Name : OutlookText.T("reallife_news_end_kicker"),
                Emblem = UiIcon.Person,
                Body = PermadeathText.Fill("reallife_pd_obit", story),
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
            model.Household = new UiInfoModel
            {
                Title = OutlookText.T("reallife_news_home_title"),
                Icon = UiIcon.Home,
                Prompts = model.Prompts,
                PromptCount = model.Prompts.Length,
                Sections = new[]
                {
                    new UiInfoSection
                    {
                        Label = OutlookText.T("reallife_news_home_title"),
                        Rows = new[] { new UiInfoRow { Label = OutlookText.T("reallife_news_col_rent"), Value = LedgerText.Money(0) } }
                    }
                }
            };
        }

        private void FillRenters(NewsModel model)
        {
            model.Shape = NewsShape.Renters;
            model.Title = OutlookText.T("reallife_news_rent_link");
            model.Icon = UiIcon.Home;
            model.Table = Table(new[] { "reallife_news_col_name", "reallife_news_col_rent" }, new List<UiDataRow>(0));
            model.EmptyTitle = OutlookText.T("reallife_news_empty_rent");
            model.EmptyBody = OutlookText.T("reallife_news_empty_rent_body");
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
