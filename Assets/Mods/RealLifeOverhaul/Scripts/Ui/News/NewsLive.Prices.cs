using System;
using System.Collections.Generic;
using Extensions;
using Localizor;
using RealLifeOverhaul.DaySeries;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Ledger;
using RealLifeOverhaul.Outlook;
using RealLifeOverhaul.Rivals;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The Economy app's prices page: the basket in the lead and the goods that led it, the shops' goods with your own price where you sell
    /// them, the index of the goods that moved most, and who moved prices this week. Every figure is the game's or the city's own.
    /// </summary>
    public sealed partial class NewsLive
    {
        private static readonly int[] Mine = new int[8];
        private static readonly int[] GoodRaw = new int[NewsRules.DaysBook];
        private const int MovesMost = 6;

        private void FillPrices(NewsModel model, OutlookBook book)
        {
            int bp = BasketChange(book, out string led);
            model.Chip = led.Length > 0 ? OutlookText.T("reallife_news_chip_led", "good", led) : model.Chip;
            model.Lead = BasketSentence(bp);
            model.Body = led.Length > 0 ? OutlookText.T("reallife_news_led", "good", led) : string.Empty;
            model.Figures = new[]
            {
                new UiFigureModel
                {
                    Label = OutlookText.T("reallife_news_basket_label"),
                    Value = (bp > 0 ? "+" : string.Empty) + (bp / 100f).ToString("0.0", Inv) + "%",
                    Change = OutlookText.T("reallife_news_in_month"),
                    Rising = bp > 0,
                    Good = bp <= 0
                }
            };
            model.TableTitle = OutlookText.T("reallife_news_prices_shops");
            model.Table = RetailTable(true);
            model.SideChart = GoodsChart();
            model.MovesTitle = OutlookText.T("reallife_news_moved");
            model.Moves = MovedThisWeek(Look.Day);
        }

        // The basket's change over a month in basis points, and the kind of goods that led it ("" for none).
        private int BasketChange(OutlookBook book, out string led)
        {
            int now = Look.Prices;
            int then = now;
            if (book != null && book.Get(OutlookCatalogue.Prices, book.LastDay - NewsRules.DaysMonth, out int earlier))
            {
                then = earlier;
            }
            led = string.Empty;
            int best = 0;
            for (int i = 0; i < Look.Supply.Length && i < OutlookWords.KindKeys.Length; i++)
            {
                int move = Look.Supply[i].Factor - 1000;
                if (move > best)
                {
                    best = move;
                    led = OutlookText.T(OutlookWords.KindKeys[i]);
                }
            }
            return NewsRules.BasketBp(now, then);
        }

        // "Shopping costs 4.8% more than a month ago", or less, or the same.
        private static string BasketSentence(int bp)
        {
            string change = (Math.Abs(bp) / 100f).ToString("0.0", Inv) + "%";
            return OutlookText.T(NewsText.BasketKey(bp), "change", change);
        }

        // A good as the game names it: a key written out, any other name as it is.
        private static string GoodName(string good)
        {
            return string.IsNullOrEmpty(good) ? "?" : LocalizorManager.IsLocalizedKey(good) ? good.GetLocalization() : good;
        }

        // The goods the city's shops sell, one row each: its price today, the week, the month, its 120 days, and the price of your own shop where you sell it.
        private UiDataTableModel RetailTable(bool full)
        {
            DaySeriesBook series = DaySeriesDirector.Book;
            if (series == null || series.LastDay <= 0)
            {
                return null;
            }
            var goods = new List<string>(DaySeriesCatalogue.CityGoodsMost);
            List<string> names = series.Names();
            for (int i = 0; i < names.Count; i++)
            {
                string key = names[i];
                if (key != null && key.Length > DaySeriesCatalogue.CityPrefix.Length && key.StartsWith(DaySeriesCatalogue.CityPrefix, StringComparison.Ordinal))
                {
                    goods.Add(key.Substring(DaySeriesCatalogue.CityPrefix.Length));
                }
            }
            goods.Sort((a, b) => string.CompareOrdinal(GoodName(a), GoodName(b)));
            var rows = new List<UiDataRow>(goods.Count);
            int end = series.LastDay;
            for (int g = 0; g < goods.Count && rows.Count < (full ? DaySeriesCatalogue.CityGoodsMost : 6); g++)
            {
                if (series.Window(DaySeriesCatalogue.CityPrefix + goods[g], end, NewsRules.DaysBook, GoodRaw) <= 0)
                {
                    continue;
                }
                int today = LastOf(GoodRaw);
                if (today == DaySeriesCatalogue.NoValue)
                {
                    continue;
                }
                int week = At(GoodRaw, NewsRules.DaysBook - 8);
                int month = At(GoodRaw, NewsRules.DaysBook - 1 - NewsRules.DaysMonth);
                string weekMove = Move(today, week, out int weekWay);
                string monthMove = Move(today, month == DaySeriesCatalogue.NoValue ? week : month, out int monthWay);
                var line = new float[NewsRules.DaysBook];
                float carry = today / 100f;
                for (int d = 0; d < NewsRules.DaysBook; d++)
                {
                    if (GoodRaw[d] != DaySeriesCatalogue.NoValue)
                    {
                        carry = GoodRaw[d] / 100f;
                    }
                    line[d] = carry;
                }
                rows.Add(new UiDataRow
                {
                    Cells = new[] { GoodName(goods[g]), "$" + Cents(today), weekMove, monthMove, string.Empty, YourPrice(series, goods[g], end) },
                    Values = new[] { 0, 0, weekWay, monthWay, 0, 0 },
                    Tones = new[] { UiRole.None, UiRole.None, UiRole.Danger, UiRole.Danger, monthWay < 0 ? UiRole.Ok : UiRole.Danger, UiRole.None },
                    Line = line
                });
            }
            return rows.Count == 0 ? null : GoodsColumns(rows, full);
        }

        // What your own shop charges for the good today, from your own plans; empty for a good you do not sell.
        private static string YourPrice(DaySeriesBook series, string good, int end)
        {
            if (series.Window(DaySeriesCatalogue.PricePrefix + good, end, Mine.Length, Mine) <= 0)
            {
                return string.Empty;
            }
            int last = Mine[Mine.Length - 1];
            return last == DaySeriesCatalogue.NoValue ? string.Empty : "$" + Cents(last);
        }

        private static int LastOf(int[] values)
        {
            for (int i = values.Length - 1; i >= 0; i--)
            {
                if (values[i] != DaySeriesCatalogue.NoValue)
                {
                    return values[i];
                }
            }
            return DaySeriesCatalogue.NoValue;
        }

        private static int At(int[] values, int index)
        {
            return index < 0 || index >= values.Length ? DaySeriesCatalogue.NoValue : values[index];
        }

        // The goods that moved most, each rebased to 100 on the first day of the window, as lines.
        private UiChartModel GoodsChart()
        {
            DaySeriesBook series = DaySeriesDirector.Book;
            if (series == null || series.LastDay <= 0)
            {
                return null;
            }
            int span = NewsRules.DaysBook;
            int end = series.LastDay;
            var all = new List<UiSeries>(DaySeriesCatalogue.CityGoodsMost);
            var swing = new List<float>(DaySeriesCatalogue.CityGoodsMost);
            List<string> names = series.Names();
            for (int i = 0; i < names.Count; i++)
            {
                string key = names[i];
                if (key == null || key.Length <= DaySeriesCatalogue.CityPrefix.Length || !key.StartsWith(DaySeriesCatalogue.CityPrefix, StringComparison.Ordinal)
                    || series.Window(key, end, span, GoodRaw) <= 0)
                {
                    continue;
                }
                float first = 0f;
                for (int d = 0; d < span && first <= 0f; d++)
                {
                    first = GoodRaw[d] == DaySeriesCatalogue.NoValue ? 0f : GoodRaw[d];
                }
                if (first <= 0f)
                {
                    continue;
                }
                var line = new float[span];
                float carry = 100f;
                for (int d = 0; d < span; d++)
                {
                    carry = GoodRaw[d] == DaySeriesCatalogue.NoValue ? carry : GoodRaw[d] * 100f / first;
                    line[d] = carry;
                }
                all.Add(new UiSeries { Label = GoodName(key.Substring(DaySeriesCatalogue.CityPrefix.Length)), Values = line });
                swing.Add(Math.Abs(line[span - 1] - 100f));
            }
            if (all.Count == 0)
            {
                return null;
            }
            var chosen = new List<UiSeries>(4);
            for (int pick = 0; pick < 4 && all.Count > 0; pick++)
            {
                int top = 0;
                for (int i = 1; i < all.Count; i++)
                {
                    top = swing[i] > swing[top] ? i : top;
                }
                chosen.Add(all[top]);
                all.RemoveAt(top);
                swing.RemoveAt(top);
            }
            int shown = Math.Min(span, NewsRules.DaysBook);
            var whole = new string[span];
            for (int i = 0; i < span; i++)
            {
                whole[i] = OutlookText.T("reallife_news_axis_day", "day", (end - span + 1 + i).ToString(Inv));
            }
            return new UiChartModel
            {
                Kind = UiChartKind.Line,
                Title = OutlookText.T("reallife_news_goods_index", "day", (end - span + 1).ToString(Inv)),
                Points = NewsText.AxisPoints(whole, NewsText.AxisStep(shown)),
                Reads = whole,
                Series = chosen.ToArray(),
                Format = v => v.ToString("0", Inv),
                EmptyTitle = OutlookText.T("reallife_news_empty_city"),
                EmptyBody = OutlookText.T("reallife_news_empty_city_body")
            };
        }

        // Rivals who raised prices this week, from their moves, and City Hall only while a price policy that exists stands off the city's own.
        private NewsMove[] MovedThisWeek(int day)
        {
            var moves = new List<NewsMove>(MovesMost);
            RivalFile file = RivalDirector.Instance != null ? RivalDirector.Instance.File : null;
            for (int i = 0; file != null && i < file.Moves.Count && moves.Count < MovesMost; i++)
            {
                RivalMove move = file.Moves[i];
                bool open = move.State != RivalMoveState.Stopped && move.State != RivalMoveState.Failed;
                if (move.Kind != RivalMoveKind.PriceRise || move.Test || !open || day - move.Announced > 7 || move.Tycoon < 0 || move.Tycoon >= RivalCatalogue.Tycoons.Length)
                {
                    continue;
                }
                TycoonDef who = RivalCatalogue.Tycoons[move.Tycoon];
                moves.Add(new NewsMove
                {
                    Mark = UiDraw.Initials(who.Name),
                    Head = OutlookText.T("reallife_news_moved_rival", "who", who.Name),
                    Detail = OutlookText.T("reallife_news_moved_detail", "firm", who.Holding, "day", move.Announced.ToString(Inv))
                });
            }
            AddHall(moves, Lever.TradeLevy);
            AddHall(moves, Lever.BusinessTax);
            return moves.ToArray();
        }

        // City Hall's own policy, named, while it stands in force.
        private static void AddHall(List<NewsMove> moves, Lever lever)
        {
            string name = PolicyName(lever);
            if (name.Length == 0 || moves.Count >= MovesMost)
            {
                return;
            }
            moves.Add(new NewsMove
            {
                Mark = OutlookText.T("reallife_news_hall_mark"),
                Head = name,
                Detail = OutlookText.T("reallife_news_hall_detail")
            });
        }
    }
}
