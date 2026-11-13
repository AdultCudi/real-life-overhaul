using System;
using System.Collections.Generic;
using System.Globalization;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Ledger;
using RealLifeOverhaul.Markets;
using RealLifeOverhaul.Outlook;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The Economy app's markets page: the city index and its chart, the sectors over five sessions, the risers, the fallers and the most
    /// traded, and what you hold. Every figure is the market's own. A block with nothing to say is left out.
    /// </summary>
    public sealed partial class NewsLive
    {
        private const int Sessions = 5;            // the sessions a sector's return is read over
        private const int IndexDays = 60;          // the sessions of the index's chart
        private const int BoardRows = 4;

        private void FillMarkets(NewsModel model)
        {
            model.Lead = null;
            model.Body = null;
            model.Chip = null;
            model.Figures = new UiFigureModel[0];
            model.Advice = new NewsAdvice[0];
            model.Actions = new UiActionModel[0];
            MarketState state = MarketDirector.Instance != null ? MarketDirector.Instance.State : null;
            if (state == null || !Look.MarketOn)
            {
                model.Shape = NewsShape.Notice;
                model.EmptyTitle = OutlookText.T(Look.MarketOn || state == null ? "reallife_news_empty_city" : "reallife_econapp_markets_off_title");
                model.EmptyBody = OutlookText.T(Look.MarketOn || state == null ? "reallife_news_empty_city_body" : "reallife_econapp_markets_off_body");
                model.EmptyIcon = UiIcon.ChartLineUp;
                return;
            }
            // Whether the market is open now, as the old page wrote it in the corner of its chart.
            model.Facts = new[] { OutlookText.T(Look.MarketOpen ? "reallife_econapp_market_open" : "reallife_econapp_market_closed") };
            model.FactTones = new[] { Look.MarketOpen ? UiRole.Ok : UiRole.Muted };
            model.Chart = IndexChart(state);
            IndexFigures(state, model);
            Sectors(state, model);
            model.Boards = new[] { Board(state, 0), Board(state, 1), Board(state, 2) };
            model.BoardTitles = new[] { LedgerText.T("reallife_ledger_risers"), LedgerText.T("reallife_ledger_fallers"), OutlookText.T("reallife_news_traded") };
            model.MovesTitle = OutlookText.T("reallife_news_holdings");
            model.Moves = Holdings(state);
            model.MovesLink = model.Moves.Length > 0 ? OutlookText.T("reallife_news_open_stocks") : null;
            model.MovesTarget = 2;
        }

        // The index's close each session, a point a day, named in days at the foot a few at a time.
        private static UiChartModel IndexChart(MarketState state)
        {
            int n = state.IndexDaily.Count;
            int take = Math.Min(IndexDays, n);
            var values = new float[take];
            var whole = new string[take];
            for (int i = 0; i < take; i++)
            {
                Candle c = state.IndexDaily[n - take + i];
                values[i] = c.C / 100f;
                whole[i] = OutlookText.T("reallife_news_axis_day", "day", c.Day.ToString(Inv));
            }
            return new UiChartModel
            {
                Kind = UiChartKind.Line,
                Title = OutlookText.T("reallife_news_index_close"),
                Points = NewsText.AxisPoints(whole, Math.Max(1, take / 4)),
                Reads = whole,
                Series = new[] { new UiSeries { Label = OutlookText.T("reallife_news_series_index"), Values = values, Tint = UiRole.Accent } },
                Format = v => v.ToString("N1", CultureInfo.CurrentCulture),
                EmptyTitle = OutlookText.T("reallife_news_empty_city"),
                EmptyBody = OutlookText.T("reallife_news_empty_city_body")
            };
        }

        // The index's level, and how it moved on the day and over five sessions.
        private static void IndexFigures(MarketState state, NewsModel model)
        {
            if (state.IndexLast <= 0)
            {
                return;
            }
            model.BigValue = (state.IndexLast / 100f).ToString("N1", CultureInfo.CurrentCulture);
            // A change is given only where the market has the sessions for it: no close before today, no change on the day; fewer than five closes, none over five.
            var changes = new List<string>(2);
            var good = new List<bool>(2);
            if (state.IndexPrevClose > 0)
            {
                int day = (int)((long)(state.IndexLast - state.IndexPrevClose) * 10000L / state.IndexPrevClose);
                changes.Add(OutlookText.T("reallife_news_on_day", "change", Percent(day)));
                good.Add(day >= 0);
            }
            int n = state.IndexDaily.Count;
            int then = n > Sessions ? state.IndexDaily[n - 1 - Sessions].C : 0;
            if (then > 0)
            {
                int week = (int)((long)(state.IndexLast - then) * 10000L / then);
                changes.Add(LedgerText.Fill("reallife_ledger_week", "change", Percent(week)));
                good.Add(week >= 0);
            }
            model.BigChanges = changes.ToArray();
            model.BigGood = good.ToArray();
        }

        private static string Percent(int bp)
        {
            return (Math.Abs(bp) / 100f).ToString("0.0", CultureInfo.CurrentCulture) + "%";
        }

        // The sectors that rose most and fell most over five sessions, as bars from a centre line.
        private static void Sectors(MarketState state, NewsModel model)
        {
            int count = (int)MarketSector.Count;
            var order = new List<int>(count);
            var returns = new int[count];
            for (int i = 0; i < count && i < state.SectorDaily.Length; i++)
            {
                returns[i] = SessionBp(state.SectorDaily[i], Sessions);
                order.Add(i);
            }
            order.Sort((a, b) => returns[b] != returns[a] ? returns[b].CompareTo(returns[a]) : a.CompareTo(b));
            int most = 1;
            for (int i = 0; i < order.Count; i++)
            {
                most = Math.Max(most, Math.Abs(returns[order[i]]));
            }
            // Every sector the market has, the best first: the old page gave each a card, so none is left out for the room's sake.
            var bars = new List<NewsBar>(order.Count);
            for (int i = 0; i < order.Count; i++)
            {
                int s = order[i];
                bars.Add(new NewsBar
                {
                    Label = OutlookText.T(MarketCatalogue.SectorKeys[s]),
                    Value = (returns[s] > 0 ? "+" : returns[s] < 0 ? "-" : string.Empty) + Percent(returns[s]),
                    Share = returns[s] / (float)most
                });
            }
            model.Bars = bars.ToArray();
            model.BarsTitle = OutlookText.T("reallife_news_sectors");
            model.BarsNote = OutlookText.T("reallife_news_five_note");
            model.BarsMore = OutlookText.T("reallife_econapp_places_more");
        }

        private static int SessionBp(List<int> daily, int sessions)
        {
            if (daily == null || daily.Count < 2)
            {
                return 0;
            }
            int last = daily[daily.Count - 1];
            int from = Math.Max(0, daily.Count - 1 - sessions);
            int then = daily[from];
            return then == 0 ? 0 : (int)((long)(last - then) * 10000L / then);
        }

        // Risers (0), fallers (1) or most traded (2): code, company, price and the move or the shares.
        private static UiDataTableModel Board(MarketState state, int kind)
        {
            var picked = new List<Stock>(state.Stocks.Count);
            for (int i = 0; i < state.Stocks.Count; i++)
            {
                Stock s = state.Stocks[i];
                bool on = s != null && s.Listed && s.Last > 0 && s.PrevClose > 0 && s.Ticker.Length > 0;
                if (on && (kind == 2 ? s.DayVolume > 0 : kind == 0 ? s.Last > s.PrevClose : s.Last < s.PrevClose))
                {
                    picked.Add(s);
                }
            }
            picked.Sort((a, b) =>
            {
                int by = kind == 2 ? ((double)b.DayVolume * b.Last).CompareTo((double)a.DayVolume * a.Last)
                    : kind == 0 ? Change(b).CompareTo(Change(a)) : Change(a).CompareTo(Change(b));
                return by != 0 ? by : string.CompareOrdinal(a.Name, b.Name);
            });
            var rows = new List<UiDataRow>(BoardRows);
            for (int i = 0; i < picked.Count && i < BoardRows; i++)
            {
                Stock s = picked[i];
                int bp = Change(s);
                int way = bp > 0 ? 1 : bp < 0 ? -1 : 0;
                rows.Add(new UiDataRow
                {
                    Cells = new[]
                    {
                        s.Ticker, s.Name, "$" + LedgerText.Figure(s.Last),
                        kind == 2 ? s.DayVolume.ToString("N0", CultureInfo.CurrentCulture) : Percent(bp)
                    },
                    Values = new[] { 0, 0, 0, kind == 2 ? 0 : way },
                    Tones = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None }
                });
            }
            return new UiDataTableModel
            {
                Columns = new[]
                {
                    new UiDataColumn { Title = OutlookText.T("reallife_news_col_code"), Kind = UiCellKind.Text, Width = 64f },
                    new UiDataColumn { Title = OutlookText.T("reallife_news_col_company"), Kind = UiCellKind.Text },
                    new UiDataColumn { Title = OutlookText.T("reallife_news_col_price"), Kind = UiCellKind.Figure, Width = 80f },
                    new UiDataColumn { Title = OutlookText.T(kind == 2 ? "reallife_news_col_shares" : "reallife_news_col_move"), Kind = kind == 2 ? UiCellKind.Figure : UiCellKind.Trend, Width = 96f }
                },
                Rows = rows.ToArray(),
                RowCount = rows.Count,
                EmptyTitle = OutlookText.T("reallife_news_empty_board"),
                EmptyIcon = UiIcon.ChartLineUp
            };
        }

        private static int Change(Stock s)
        {
            return (int)((long)(s.Last - s.PrevClose) * 10000L / Math.Max(1, s.PrevClose));
        }

        // The player's largest holdings by what they are worth now: how many, what they are worth and the gain since they were bought.
        private static NewsMove[] Holdings(MarketState state)
        {
            var held = new List<Position>(state.Positions.Count);
            for (int i = 0; i < state.Positions.Count; i++)
            {
                Position p = state.Positions[i];
                Stock s = p != null ? state.Stock(p.StockId) : null;
                if (p != null && !p.Test && p.Shares > 0 && s != null && s.Last > 0)
                {
                    held.Add(p);
                }
            }
            held.Sort((a, b) => (b.Shares * state.Stock(b.StockId).Last).CompareTo(a.Shares * state.Stock(a.StockId).Last));
            var moves = new NewsMove[Math.Min(3, held.Count)];
            for (int i = 0; i < moves.Length; i++)
            {
                Position p = held[i];
                Stock s = state.Stock(p.StockId);
                long worth = p.Shares * s.Last / 100L;
                long gain = worth - p.Cost;
                string money = LedgerText.Money((int)Math.Max(0, Math.Min(int.MaxValue, worth)));
                string change = LedgerText.Money((int)Math.Min(int.MaxValue, Math.Abs(gain)));
                moves[i] = new NewsMove
                {
                    Mark = s.Ticker.Length > 1 ? s.Ticker.Substring(0, 2) : s.Ticker,
                    Head = OutlookText.T("reallife_news_hold_head", "shares", p.Shares.ToString("N0", CultureInfo.CurrentCulture), "ticker", s.Ticker),
                    Detail = OutlookText.T(gain >= 0 ? "reallife_news_hold_up" : "reallife_news_hold_down", "worth", money, "gain", change)
                };
            }
            return moves;
        }
    }
}
