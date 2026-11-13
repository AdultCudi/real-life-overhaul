using System;
using System.Collections.Generic;
using System.Globalization;
using RealLifeOverhaul.Markets;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The Market page's sector table, its news and rate line, and the index page: what the old Market tab and its index page show,
    /// read from the same state with the same sums (MarketApp.Box.cs).
    /// </summary>
    public static partial class StocksBuild
    {
        public const int MarketCompanies = 0, MarketSectors = 1;   // the Market page's two views

        private static readonly int[] Backs = { 5, 21, 63, 100 };   // the days back the sector table measures

        /// <summary>An index level, kept in hundredths, as the old page writes it: "1,000.00".</summary>
        public static string IndexWords(int hundredths) => (hundredths / 100.0).ToString("N2", CultureInfo.CurrentCulture);

        /// <summary>The bank rate and inflation in one line, as the old Market box says it.</summary>
        public static string RateLine(MarketState s)
        {
            return Plain(MarketText.Fill("reallife_mkt_box_rate", "rate", (s.Env.RateBp / 100f).ToString("0.00", CultureInfo.CurrentCulture) + "%",
                "inflation", (s.Env.InflationBp / 100f).ToString("0.0", CultureInfo.CurrentCulture) + "%"));
        }

        /// <summary>
        /// The newest news, newest first, without the close and the movers (the table says those) and without a kind that has no sentence
        /// (a player's company's refusals). <paramref name="any"/> is false when there is none.
        /// </summary>
        public static string[] NewsLines(MarketState s, int most, out bool any)
        {
            var lines = new List<string>(most);
            for (int i = s.News.Count - 1; i >= 0 && lines.Count < most; i--)
            {
                MarketNews n = s.News[i];
                if (n.Kind == MarketNewsKind.Close || n.Kind == MarketNewsKind.Mover)
                {
                    continue;
                }
                string news = MarketText.News(n, s);
                if (news.Length == 0)
                {
                    continue;
                }
                lines.Add(Plain(news));
            }
            any = lines.Count > 0;
            return lines.ToArray();
        }

        /// <summary>The founder's own holding for a company that is theirs, the position otherwise.</summary>
        public static long HeldOrFounder(MarketState s, Stock k, MarketDirector d)
        {
            return k.Own != null && d != null && d.Engine != null && d.Engine.Mine(k) ? k.Own.Founder : s.Held(k.Id);
        }

        /// <summary>The sector a row of the sector table is: -1 for the index row, then each sector the market has a level for, in order.</summary>
        public static int SectorAt(MarketState s, int row)
        {
            if (row <= 0)
            {
                return -1;
            }
            int n = 0;
            for (int sec = 0; sec < (int)MarketSector.Count; sec++)
            {
                if (s.SectorLast[sec] <= 0)
                {
                    continue;
                }
                n++;
                if (n == row)
                {
                    return sec;
                }
            }
            return -2;
        }

        private static UiDataTableModel SectorTable(MarketState s, UiWords words)
        {
            var columns = new List<UiDataColumn>(7)
            {
                new UiDataColumn { Title = words("reallife_mkt_col_index", "Index"), Kind = UiCellKind.Tile },
                new UiDataColumn { Title = words("reallife_mkt_col_level", "Level"), Kind = UiCellKind.Figure, Width = 120f },
                new UiDataColumn { Title = words("reallife_mkt_col_today", "Today"), Kind = UiCellKind.Trend, Width = 120f },
                new UiDataColumn { Title = words("reallife_mkt_col_week", "5 days"), Kind = UiCellKind.Trend, Width = 110f },
                new UiDataColumn { Title = words("reallife_mkt_col_month", "21 days"), Kind = UiCellKind.Trend, Width = 110f },
                new UiDataColumn { Title = words("reallife_mkt_col_quarter", "63 days"), Kind = UiCellKind.Trend, Width = 110f },
                new UiDataColumn { Title = words("reallife_mkt_col_hundred", "100 days"), Kind = UiCellKind.Trend, Width = 110f }
            };
            var rows = new List<UiDataRow>((int)MarketSector.Count + 1);
            rows.Add(IndexRow(words("reallife_mkt_index", "The city index"), s.IndexLast, MarketHistory.IndexDayMoveBp(s), s.IndexDaily, null, (int)UiIcon.ChartLineUp, UiRole.Accent));
            for (int sec = 0; sec < (int)MarketSector.Count; sec++)
            {
                if (s.SectorLast[sec] <= 0)
                {
                    continue;
                }
                rows.Add(IndexRow(MarketText.Sector((MarketSector)sec), s.SectorLast[sec], MarketHistory.SectorDayMoveBp(s, sec), null, s.SectorDaily[sec], (int)UiIcon.Building,
                    UiTokens.Chart(sec)));
            }
            return new UiDataTableModel
            {
                Columns = columns.ToArray(), Rows = rows.ToArray(), RowCount = rows.Count, EmptyIcon = UiIcon.ChartLineUp,
                EmptyTitle = words("reallife_stk_market_empty", "No companies are listed"),
                EmptyBody = words("reallife_stk_market_empty_body", "Listed companies show here with the day's move.")
            };
        }

        // One row of the sector table: the level, today's move and the moves over 5, 21, 63 and 100 days; "-" where the history is not that long.
        private static UiDataRow IndexRow(string name, int last, int today, List<Candle> daily, List<int> closes, int icon, UiRole tint)
        {
            var cells = new string[3 + Backs.Length];
            var values = new int[cells.Length];
            var tones = new UiRole[cells.Length];
            cells[0] = name;
            values[0] = icon;
            tones[0] = tint;
            cells[1] = IndexWords(last);
            cells[2] = MarketText.Change(today);
            values[2] = Way(today);
            int count = daily != null ? daily.Count : closes.Count;
            for (int b = 0; b < Backs.Length; b++)
            {
                int at = count - Backs[b];
                if (at < 0)
                {
                    cells[3 + b] = "-";
                    continue;
                }
                int then = daily != null ? daily[at].C : closes[at];
                int bp = MarketHistory.MoveBp(last, then);
                cells[3 + b] = MarketText.Change(bp);
                values[3 + b] = Way(bp);
            }
            return new UiDataRow { Cells = cells, Values = values, Tones = tones };
        }

        private static int Way(int bp) => bp > 0 ? 1 : bp < 0 ? -1 : 0;

        /// <summary>
        /// The index page: its level and day's move, the day's range, the index over a period as a line, and the rate and inflation under it,
        /// as the old index page says them.
        /// </summary>
        private static void IndexPage(StocksModel m, MarketState s, UiWords words, int period)
        {
            int today = MarketHistory.IndexDayMoveBp(s);
            m.Figures = new[]
            {
                Fig(words("reallife_mkt_index", "The city index"), IndexWords(s.IndexLast), today),
                new UiFigureModel
                {
                    Label = words("reallife_mkt_fig_range_day", "Today's range"),
                    Value = MarketText.Fill("reallife_mkt_range", "low", IndexWords(s.IndexLow), "high", IndexWords(s.IndexHigh))
                }
            };
            m.ViewLabels = new string[Periods];
            for (int i = 0; i < Periods; i++)
            {
                m.ViewLabels[i] = PeriodWord(i, words);
            }
            m.View = period >= 0 && period < Periods ? period : PeriodMonth;
            m.Actions = new[] { new UiActionModel { Label = words("reallife_mkt_back", "Back"), Kind = UiButtonKind.Secondary, Key = UiAction.Count } };
            int n = Math.Min(PeriodDays(period), s.IndexDaily.Count);
            if (n >= 2)
            {
                var values = new float[n];
                var points = new string[n];
                int origin = s.IndexDaily.Count - n;
                for (int i = 0; i < n; i++)
                {
                    values[i] = s.IndexDaily[origin + i].C / 100f;
                    points[i] = s.IndexDaily[origin + i].Day.ToString();
                }
                m.Chart = new UiChartModel
                {
                    Kind = UiChartKind.Line,
                    Title = words("reallife_mkt_index", "The city index"),
                    Points = points,
                    EvenAxis = true,
                    Format = v => v.ToString("N2", CultureInfo.CurrentCulture),
                    Series = new[] { new UiSeries { Label = words("reallife_mkt_col_index", "Index"), Values = values, Tint = UiRole.Accent } }
                };
            }
            else
            {
                m.EmptyTitle = words("reallife_stk_chart_empty", "No candles yet");
                m.EmptyBody = words("reallife_stk_chart_empty_body", "Candles appear as the market trades.");
            }
            m.RateLine = RateLine(s);
            m.Note = words("reallife_mkt_box_index_about", "The worth of every listed company together, 1,000 at the first open.");
        }
    }
}
