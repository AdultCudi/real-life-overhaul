using System;
using System.Collections.Generic;
using System.Globalization;
using RealLifeOverhaul.DaySeries;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Outlook;
using RealLifeOverhaul.Population;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The Economy app's cards the old pages drew under their charts, read from the same city readings: the overview's tiles and facts, the prices' charts with the
    /// costs, the bank and the wholesale prices, the jobs' vacancies and what it means to hire, the districts' figures with your own places, and the finances'
    /// books, departments and tax. A fill when the day or the tab changes, never a frame.
    /// </summary>
    public sealed partial class NewsLive
    {
        private static readonly int[] CardRaw = new int[OutlookCatalogue.HistoryDays];
        private int[] _districtRows = new int[0];   // the district of each row of the districts' table, in the order the table lists them

        // The model's fields that only some tabs fill, put back so a tab never shows what the tab before it left.
        private static void ClearCards(NewsModel model)
        {
            model.Tiles = new NewsTile[0];
            model.Panels = new NewsPanel[0];
            model.Charts = new UiChartModel[0];
            model.Facts = new string[0];
            model.FactTones = null;
            model.FactsNote = null;
            model.AdviceMore = null;
            model.Note = null;
            model.SideChart = null;
            model.Moves = new NewsMove[0];
            model.MovesLink = null;
            model.Boards = new UiDataTableModel[0];
            model.BoardTitles = new string[0];
            model.EmptyTitle = null;
            model.EmptyBody = null;
        }

        // ---------------------------------------------------------------- the overview

        // The tiles the band does not hold (the price index, the districts' income, property, the stock index, the council's reserve), each with its week and month and its line.
        private void FillOverviewCards(NewsModel model, OutlookBook book)
        {
            int[] tiles = { OutlookCatalogue.TilePrices, OutlookCatalogue.TileIncome, OutlookCatalogue.TileProperty, OutlookCatalogue.TileStocks, OutlookCatalogue.TileReserve };
            var list = new NewsTile[tiles.Length];
            for (int i = 0; i < tiles.Length; i++)
            {
                list[i] = Tile(book, tiles[i]);
            }
            model.Tiles = list;
            var facts = new List<string>(3)
            {
                OutlookText.T("reallife_econapp_fact_month", "n", Look.MonthsIn.ToString(Inv)),
                OutlookText.T(Look.DaysToDecision == 1 ? "reallife_econapp_fact_decision_one" : "reallife_econapp_fact_decision", "days", Look.DaysToDecision.ToString(Inv))
            };
            for (int i = 0; i < Look.DistrictCount; i++)
            {
                if (Look.Districts[i].Rank == 1)
                {
                    facts.Add(OutlookText.T("reallife_econapp_fact_best", "district", OutlookText.Words.District(Look.Districts[i].Id)));
                    break;
                }
            }
            model.Facts = facts.ToArray();
            model.FactsNote = OutlookText.T("reallife_econapp_hero_effects", "footfall", OutlookWords.Whole(Look.Footfall), "spend", OutlookWords.Factor(Look.Spend));
        }

        private NewsTile Tile(OutlookBook book, int tile)
        {
            OutlookTile def = OutlookCatalogue.Tiles[tile];
            bool kept = true;
            string value;
            switch (tile)
            {
                case OutlookCatalogue.TilePrices: value = OutlookWords.Index(Look.Prices); break;
                case OutlookCatalogue.TileIncome: value = OutlookWords.Tenths(Look.Income); break;
                case OutlookCatalogue.TileProperty: value = OutlookWords.Factor(Look.Property); break;
                case OutlookCatalogue.TileStocks:
                    kept = Look.MarketOn;
                    value = (Look.Stocks / 100.0).ToString("N2", CultureInfo.CurrentCulture);
                    break;
                default:
                    kept = Look.CouncilOn;
                    value = OutlookText.Money(Look.Reserve);
                    break;
            }
            var t = new NewsTile { Label = OutlookText.T(def.LabelKey), Value = kept ? value : "-", Tab = def.Tab };
            if (!kept)
            {
                t.Short = OutlookText.T("reallife_econapp_not_kept");
                t.Long = string.Empty;
                return t;
            }
            bool percent = def.Kind == OutlookChange.Percent;
            t.Short = Changed(Look.Short[tile], def.Good, percent, tile, OutlookCatalogue.ShortDays, out t.ShortWay, out t.ShortTone);
            t.Long = Changed(Look.Long[tile], def.Good, percent, tile, OutlookCatalogue.LongDays, out t.LongWay, out t.LongTone);
            for (int i = 0; i < CardRaw.Length; i++)
            {
                CardRaw[i] = OutlookCatalogue.NoValue;
            }
            if (tile == OutlookCatalogue.TileStocks)
            {
                OutlookStudy.StocksWindow(Look, CardRaw);
            }
            else if (def.Series != null && book != null)
            {
                book.Window(def.Series, Look.Day, CardRaw.Length, CardRaw);
            }
            t.Line = NewsCardRules.Kept(CardRaw, CardRaw.Length, OutlookCatalogue.NoValue, v => v, out int _);
            return t;
        }

        // A change over some days as the pill writes it, and the colour of what it means to the player.
        private static string Changed(OutlookDelta d, int good, bool percent, int tile, int days, out int way, out UiRole tone)
        {
            string text = NewsCardRules.Pill(d.Known, d.Value, percent, n => percent ? OutlookWords.Percent(n) : Points(tile, n), out way);
            OutlookDelta shown = d.Known && percent && Math.Abs(d.Value) < 5 ? OutlookDelta.Of(0) : d;
            OutlookTone outcome = OutlookRules.OutcomeOf(shown, good);
            tone = outcome == OutlookTone.Good ? UiRole.OkText : outcome == OutlookTone.Bad ? UiRole.DangerText : UiRole.Text2;
            return text + "  " + OutlookText.Days(days);
        }

        // The size of a change in the figure's own points, as the old tile wrote it.
        private static string Points(int tile, int absolute)
        {
            double n;
            string format = "0.0";
            switch (tile)
            {
                case OutlookCatalogue.TileInflation: n = absolute / 100.0; format = "0.0#"; break;
                case OutlookCatalogue.TileRate: n = absolute / (double)MacroCatalogue.QuartersPerPoint; format = "0.##"; break;
                case OutlookCatalogue.TileUnemployment: n = absolute / 10.0; break;
                case OutlookCatalogue.TileIncome: n = absolute / 10.0; break;
                default: n = absolute; format = "0"; break;
            }
            return OutlookText.Pts(n.ToString(format, Inv));
        }

        // ---------------------------------------------------------------- prices

        // The price index, inflation and the bank rate as charts, and the costs, the bank and the wholesale prices as cards.
        private void FillPricesCards(NewsModel model, OutlookBook book)
        {
            model.Charts = new[]
            {
                TileChart(book, OutlookCatalogue.TilePrices, OutlookCatalogue.Prices, "reallife_econapp_chart_prices", OutlookWords.Index(Look.Prices), v => v / 1000f,
                    v => v.ToString("0.0", Inv)),
                TileChart(book, OutlookCatalogue.TileInflation, OutlookCatalogue.Inflation, "reallife_econapp_chart_inflation", OutlookWords.Hundredths(Look.Inflation), v => v / 100f,
                    v => v.ToString("0.0#", Inv) + "%"),
                TileChart(book, OutlookCatalogue.TileRate, OutlookCatalogue.Rate, "reallife_econapp_chart_rate", OutlookWords.Rate(Look.RateBp),
                    v => (OwnBankCatalogue.CityRateStart + v * (100 / MacroCatalogue.QuartersPerPoint)) / 100f, v => v.ToString("0.00", Inv) + "%")
            };
            model.Panels = new[] { CostsPanel(), BankPanel(), WholesalePanel() };
        }

        // One tile's figure over the days the book holds, with its name, its figure and its two changes in the chart's head.
        private UiChartModel TileChart(OutlookBook book, int tile, string key, string title, string value, Func<int, float> conv, Func<float, string> format)
        {
            OutlookTile def = OutlookCatalogue.Tiles[tile];
            int end = book != null ? book.LastDay : 0;
            int span = NewsRules.DaysShown(NewsRules.DaysBook, end);
            float[] line = null;
            int first = 0;
            if (book != null && end > 0)
            {
                book.Window(key, end, span, CardRaw);
                line = NewsCardRules.Kept(CardRaw, span, OutlookCatalogue.NoValue, conv, out first);
            }
            string name = OutlookText.T(title);
            bool percent = def.Kind == OutlookChange.Percent;
            string head = name + "  " + value + "  " + Signed(Changed(Look.Short[tile], def.Good, percent, tile, OutlookCatalogue.ShortDays, out int sw, out _), sw)
                + "  " + Signed(Changed(Look.Long[tile], def.Good, percent, tile, OutlookCatalogue.LongDays, out int lw, out _), lw);
            return DaysChart(head, name, line, end - span + 1 + first, format, false);
        }

        // A chart of one line over days that have a reading: its axis begins on the first of them, and no day before it is drawn.
        private static UiChartModel DaysChart(string title, string label, float[] line, int firstDay, Func<float, string> format, bool whole)
        {
            int n = line == null ? 0 : line.Length;
            var days = new string[n];
            for (int i = 0; i < n; i++)
            {
                days[i] = OutlookText.T("reallife_news_axis_day", "day", (firstDay + i).ToString(Inv));
            }
            return new UiChartModel
            {
                Kind = UiChartKind.Line,
                Title = title,
                Points = NewsText.AxisPoints(days, NewsText.AxisStep(n)),
                Reads = days,
                Series = new[] { new UiSeries { Label = label, Values = line ?? new float[0], Tint = UiRole.Accent } },
                Format = format,
                Whole = whole,
                EmptyTitle = OutlookText.T("reallife_news_empty_city"),
                EmptyBody = OutlookText.T("reallife_news_empty_city_body")
            };
        }

        // The pill's words in a chart's head, where there is no arrow: the sign goes before the size.
        private static string Signed(string pill, int way) => way > 0 ? "+" + pill : way < 0 ? "-" + pill : pill;

        // Rents, wages, takings and what customers spend, each on the game's own figure, with a meter from 80 to 120 per cent of the usual.
        private NewsPanel CostsPanel()
        {
            string[] keys = { "reallife_econapp_costs_rents", "reallife_econapp_costs_takings", "reallife_econapp_costs_spend" };
            int[] values = { Look.Costs, Look.Takings, Look.Spend };
            var rows = new NewsRow[3];
            for (int i = 0; i < 3; i++)
            {
                int v = values[i];
                bool up = v > 1000;
                bool down = v < 1000;
                UiRole ink = up ? (i == 0 ? UiRole.WarnText : UiRole.OkText) : down ? (i == 0 ? UiRole.OkText : UiRole.WarnText) : UiRole.Text;
                rows[i] = new NewsRow
                {
                    Label = OutlookText.T(keys[i]),
                    Value = Math.Abs(v - 1000) < MacroCatalogue.AdjustQuiet ? OutlookText.T("reallife_econapp_costs_usual") : OutlookWords.Factor(v),
                    Ink = ink,
                    Bar = true,
                    Share = NewsCardRules.Share(v, 800f, 1200f),
                    MidAt = 0.5f,
                    Fill = ink == UiRole.WarnText ? UiRole.Warn : ink == UiRole.OkText ? UiRole.Ok : UiRole.Notice
                };
            }
            string you = OutlookText.T("reallife_econapp_costs_you", "wages", OutlookText.Money(Look.WagesTomorrow), "rent", OutlookText.Money(Look.RentTomorrow));
            if (Look.Carried > 0)
            {
                you += " " + OutlookText.T("reallife_macro_page_carried") + " " + OutlookText.Money(Look.Carried);
            }
            return new NewsPanel
            {
                Title = OutlookText.T("reallife_econapp_costs_title"),
                Note = OutlookText.T("reallife_econapp_costs_note"),
                Rows = rows,
                Foot = you
            };
        }

        // The central bank: its rate, its last decision and the next.
        private NewsPanel BankPanel()
        {
            string last = Look.LastMove > 0 ? OutlookText.T("reallife_econapp_bank_raised", "points", OutlookWords.Quarters(Look.LastMove))
                : Look.LastMove < 0 ? OutlookText.T("reallife_econapp_bank_cut", "points", OutlookWords.Quarters(Look.LastMove)) : OutlookText.T("reallife_econapp_bank_held");
            string next = Look.Pending && Look.PendingMove != 0
                ? OutlookText.T(Look.PendingMove > 0 ? "reallife_econapp_bank_next_up" : "reallife_econapp_bank_next_down",
                    "points", OutlookWords.Quarters(Look.PendingMove), "days", Look.DaysToDecision.ToString(Inv))
                : OutlookText.T("reallife_econapp_bank_next", "days", Look.DaysToDecision.ToString(Inv));
            return new NewsPanel
            {
                Title = OutlookText.T("reallife_econapp_bank_title"),
                Rows = new[]
                {
                    new NewsRow { Label = OutlookText.T("reallife_econapp_bank_rate"), Value = OutlookWords.Rate(Look.RateBp) + "  " + CityText.Rate(Look.Rate), MidAt = -1f },
                    new NewsRow { Label = OutlookText.T("reallife_econapp_bank_last"), Value = last, MidAt = -1f },
                    new NewsRow { Label = OutlookText.T("reallife_econapp_bank_upcoming"), Value = next, MidAt = -1f }
                }
            };
        }

        // The six kinds of goods: today's wholesale price against the usual on a scale from 60 to 160 per cent, with a shortage or a glut and whether you stock it.
        private NewsPanel WholesalePanel()
        {
            int n = Math.Min(OutlookReadings.Kinds, OutlookWords.KindKeys.Length);
            var rows = new NewsRow[n];
            for (int k = 0; k < n; k++)
            {
                OutlookSupply sup = Look.Supply[k];
                string state = sup.Shock == 0 ? string.Empty : OutlookText.T(sup.Shock == 1
                    ? (sup.Announced ? "reallife_macro_supply_shortage_tomorrow" : "reallife_macro_supply_shortage_now")
                    : (sup.Announced ? "reallife_macro_supply_glut_tomorrow" : "reallife_macro_supply_glut_now"),
                    "percent", Math.Abs(sup.Percent).ToString(Inv), "days", sup.Days.ToString(Inv));
                if (state.Length == 0 && sup.Stocked)
                {
                    state = OutlookText.T("reallife_macro_supply_stocked");
                }
                rows[k] = new NewsRow
                {
                    Label = NewsCardRules.Capital(OutlookText.T(OutlookWords.KindKeys[k])),
                    Value = OutlookWords.Factor(sup.Factor),
                    Detail = state.Length > 0 ? state : null,
                    Ink = sup.Shock == 1 ? UiRole.DangerText : UiRole.Text,
                    Bar = true,
                    Share = NewsCardRules.Share(sup.Factor, 600f, 1600f),
                    MidAt = 0.4f,
                    Fill = sup.Shock == 1 ? UiRole.Danger : sup.Shock == 2 ? UiRole.Notice : UiRole.Accent
                };
            }
            return new NewsPanel { Title = OutlookText.T("reallife_econapp_wholesale_title"), Note = OutlookText.T("reallife_econapp_wholesale_note"), Rows = rows };
        }

        // ---------------------------------------------------------------- jobs

        // Unemployment and vacancies as charts with their figure and changes, what the jobs market means to someone who hires, and how well off each district is.
        private void FillJobsCards(NewsModel model, OutlookBook book)
        {
            if (!Look.JobsOn)
            {
                model.Chart = null;
                model.EmptyTitle = OutlookText.T("reallife_econapp_jobs_off_title");
                model.EmptyBody = OutlookText.T("reallife_econapp_jobs_off_body");
            }
            else
            {
                int end = book != null ? book.LastDay : 0;
                int span = NewsRules.DaysShown(NewsRules.DaysBook, end);
                OutlookTile def = OutlookCatalogue.Tiles[OutlookCatalogue.TileUnemployment];
                string head = OutlookText.T("reallife_econapp_chart_unemployment") + "  " + OutlookWords.Permille(Look.Unemployment) + "  "
                    + Signed(Changed(Look.Short[OutlookCatalogue.TileUnemployment], def.Good, false, OutlookCatalogue.TileUnemployment, OutlookCatalogue.ShortDays, out int sw, out _), sw)
                    + "  " + Signed(Changed(Look.Long[OutlookCatalogue.TileUnemployment], def.Good, false, OutlookCatalogue.TileUnemployment, OutlookCatalogue.LongDays, out int lw, out _), lw);
                model.Chart.Title = head;
                // The chart of the jobless is the page's own (Chart); the vacancies stand beside it.
                float[] line = null;
                int first = 0;
                OutlookDelta shortChange = OutlookDelta.None, longChange = OutlookDelta.None;
                if (book != null && end > 0)
                {
                    book.Window(OutlookCatalogue.Vacancies, end, span, CardRaw);
                    line = NewsCardRules.Kept(CardRaw, span, OutlookCatalogue.NoValue, v => v, out first);
                    var full = new int[OutlookCatalogue.HistoryDays];
                    book.Window(OutlookCatalogue.Vacancies, Look.Day, full.Length, full);
                    shortChange = OutlookRules.Over(full, full.Length, OutlookCatalogue.ShortDays, Look.Vacancies, OutlookChange.Percent);
                    longChange = OutlookRules.Over(full, full.Length, OutlookCatalogue.LongDays, Look.Vacancies, OutlookChange.Percent);
                }
                string vacancies = OutlookText.T("reallife_econapp_chart_vacancies") + "  " + Look.Vacancies.ToString(Inv) + "  "
                    + Signed(Changed(shortChange, 1, true, 0, OutlookCatalogue.ShortDays, out int vs, out _), vs)
                    + "  " + Signed(Changed(longChange, 1, true, 0, OutlookCatalogue.LongDays, out int vl, out _), vl);
                model.Charts = new[] { DaysChart(vacancies, OutlookText.T("reallife_news_series_vacancies"), line, end - span + 1 + first, v => v.ToString("0", Inv), true) };
                model.Panels = new[] { HirePanel() };
            }
            model.Table = IncomeTable();
            model.TableTitle = OutlookText.T("reallife_econapp_income_title");
            model.Note2 = OutlookText.T("reallife_econapp_income_note");
        }

        // What the jobs market means for someone who hires: how many look for work, the chance a job is lost, and how that reads.
        private NewsPanel HirePanel()
        {
            string layoffs = Look.Layoffs <= 0 ? OutlookText.T("reallife_econapp_people_layoffs_none")
                : OutlookText.T("reallife_econapp_people_layoffs_value", "rate", (Look.Layoffs / 10.0).ToString("0.#", Inv));
            string hiring = OutlookText.T(Look.Unemployment <= OutlookCatalogue.JobsTight ? "reallife_econapp_people_hiring_hard"
                : Look.Unemployment >= OutlookCatalogue.JobsSlack ? "reallife_econapp_people_hiring_easy" : "reallife_econapp_people_hiring_usual");
            return new NewsPanel
            {
                Title = OutlookText.T("reallife_econapp_people_title"),
                Note = OutlookText.T("reallife_econapp_people_note"),
                Rows = new[]
                {
                    new NewsRow { Label = OutlookText.T("reallife_econapp_people_seekers"), Value = Look.Seekers.ToString(Inv), MidAt = -1f },
                    new NewsRow { Label = OutlookText.T("reallife_econapp_people_layoffs"), Value = layoffs, MidAt = -1f },
                    new NewsRow { Label = OutlookText.T("reallife_econapp_people_hiring"), Value = hiring, MidAt = -1f }
                }
            };
        }

        // How well off the districts' people are, the richest first: the income level on its scale, and its change over a month.
        private UiDataTableModel IncomeTable()
        {
            int n = Look.DistrictCount;
            var order = new int[n];
            for (int i = 0; i < n; i++)
            {
                order[i] = i;
            }
            Array.Sort(order, (a, b) => Look.Districts[b].Income != Look.Districts[a].Income ? Look.Districts[b].Income.CompareTo(Look.Districts[a].Income) : a.CompareTo(b));
            var rows = new List<UiDataRow>(n);
            for (int r = 0; r < n; r++)
            {
                OutlookDistrict d = Look.Districts[order[r]];
                int share = (int)(NewsCardRules.Share(d.Income, MacroCatalogue.IncomeLeast, MacroCatalogue.IncomeMost) * 100f);
                string name = OutlookText.Words.District != null ? OutlookText.Words.District(d.Id) : d.Id;
                string move = Changed(d.IncomeLong, 1, false, OutlookCatalogue.TileIncome, OutlookCatalogue.LongDays, out int way, out _);
                rows.Add(new UiDataRow
                {
                    Cells = new[] { string.IsNullOrEmpty(name) ? d.Id : name, OutlookWords.Tenths(d.Income), d.IncomeLong.Known ? move : string.Empty },
                    Values = new[] { (int)UiIcon.MapPin, share, way },
                    Tones = new[] { UiTokens.Chart(order[r]), UiRole.None, UiRole.None }
                });
            }
            return new UiDataTableModel
            {
                Columns = new[]
                {
                    new UiDataColumn { Title = OutlookText.T("reallife_news_col_district"), Kind = UiCellKind.Tile },
                    new UiDataColumn { Title = OutlookText.T("reallife_econapp_district_income"), Kind = UiCellKind.Meter, Width = 160f },
                    new UiDataColumn { Title = OutlookText.T("reallife_news_col_month"), Kind = UiCellKind.Trend, Width = 160f }
                },
                Rows = rows.ToArray(),
                RowCount = rows.Count,
                EmptyTitle = OutlookText.T("reallife_news_empty_city"),
                EmptyBody = OutlookText.T("reallife_news_empty_city_body"),
                EmptyIcon = UiIcon.MapTrifold
            };
        }

        // ---------------------------------------------------------------- districts

        // Every figure the old district card showed, one row a district in the order of the pull; your businesses and buildings in tables under them.
        private UiDataTableModel DistrictsFull(OutlookBook book)
        {
            int n = Look.DistrictCount;
            var order = new int[n];
            OutlookRules.Order(Look, order);
            _districtRows = order;
            bool demand = false, level = false, going = false;
            for (int i = 0; i < n; i++)
            {
                demand |= Look.Districts[i].HasDemand;
                level |= Look.Districts[i].Level > 0;
                going |= Look.Districts[i].Growing >= 0;
            }
            PopulationState pop = Pop;
            PeopleTally people = pop != null ? PeopleTally.Read(pop) : null;
            var window = new int[8];
            var rows = new List<UiDataRow>(n);
            for (int r = 0; r < n; r++)
            {
                OutlookDistrict d = Look.Districts[order[r]];
                string name = OutlookText.Words.District != null ? OutlookText.Words.District(d.Id) : d.Id;
                string week = string.Empty;
                int moved = 0;
                if (book != null && book.LastDay > 0 && book.Window(OutlookCatalogue.OfDistrictPull(d.Id), book.LastDay, 8, window) >= 2
                    && window[0] != OutlookCatalogue.NoValue && window[7] != OutlookCatalogue.NoValue)
                {
                    int change = window[7] - window[0];
                    moved = change > 0 ? 1 : change < 0 ? -1 : 0;
                    week = change > 0 ? "+" + change.ToString(Inv) : change < 0 ? "-" + (-change).ToString(Inv) : "0";
                }
                string month = Changed(d.IncomeLong, 1, false, OutlookCatalogue.TileIncome, OutlookCatalogue.LongDays, out int incomeWay, out _);
                float[] line = null;
                if (book != null && book.LastDay > 0)
                {
                    book.Window(OutlookCatalogue.OfDistrictIncome(d.Id), book.LastDay, CardRaw.Length, CardRaw);
                    line = NewsCardRules.Kept(CardRaw, CardRaw.Length, OutlookCatalogue.NoValue, v => v, out int _);
                }
                int rent = people != null ? people.DistrictMeanRent(pop.DistrictOf(d.Id)) : 0;
                var cells = new List<string>(14);
                var values = new List<int>(14);
                var tones = new List<UiRole>(14);
                Cell(cells, values, tones, string.IsNullOrEmpty(name) ? d.Id : name, (int)UiIcon.MapPin, UiTokens.Chart(order[r]));
                Cell(cells, values, tones, d.Pull.ToString(Inv), d.Pull < 0 ? 0 : d.Pull > 100 ? 100 : d.Pull, UiRole.None);
                Cell(cells, values, tones, week, moved, UiRole.None);
                Cell(cells, values, tones, OutlookWords.Tenths(d.Income), 0, UiRole.None);
                Cell(cells, values, tones, d.IncomeLong.Known ? month : string.Empty, incomeWay, UiRole.None);
                Cell(cells, values, tones, string.Empty, 0, UiRole.Accent);
                Cell(cells, values, tones, OutlookWords.Factor(d.Property), 0, UiRole.None);
                Cell(cells, values, tones, d.Land > 0 ? OutlookText.Money(d.Land) : "-", 0, UiRole.None);
                Cell(cells, values, tones, OutlookWords.Whole(d.Footfall + Look.Footfall), 0, UiRole.None);
                Cell(cells, values, tones, OutlookWords.Factor(d.Bears), 0, UiRole.None);
                Cell(cells, values, tones, d.Working.ToString(Inv) + " / " + d.Middle.ToString(Inv) + " / " + d.Upper.ToString(Inv) + "%", 0, UiRole.None);
                if (demand)
                {
                    Cell(cells, values, tones, DemandText(d), 0, UiRole.None);
                }
                if (level)
                {
                    Cell(cells, values, tones, d.Level > 0 ? OutlookWords.Factor(d.Level) : "-", 0, UiRole.None);
                }
                if (going)
                {
                    Cell(cells, values, tones, d.Growing >= 0 ? d.Growing.ToString(Inv) : "-", 0, UiRole.None);
                }
                Cell(cells, values, tones, rent > 0 ? LedgerTextMoney(rent) : string.Empty, 0, UiRole.None);
                rows.Add(new UiDataRow
                {
                    Cells = cells.ToArray(),
                    Values = values.ToArray(),
                    Tones = tones.ToArray(),
                    Sub = d.Rank > 0 ? "#" + d.Rank.ToString(Inv) : null,
                    Line = line
                });
            }
            var columns = new List<UiDataColumn>(14)
            {
                new UiDataColumn { Title = OutlookText.T("reallife_news_col_district"), Kind = UiCellKind.Tile },
                new UiDataColumn { Title = OutlookText.T("reallife_econapp_district_pull"), Kind = UiCellKind.Meter, Width = 112f },
                new UiDataColumn { Title = OutlookText.T("reallife_news_col_week"), Kind = UiCellKind.Trend, Width = 72f },
                new UiDataColumn { Title = OutlookText.T("reallife_econapp_district_income"), Kind = UiCellKind.Figure, Width = 80f },
                new UiDataColumn { Title = OutlookText.T("reallife_news_col_month"), Kind = UiCellKind.Trend, Width = 104f },
                new UiDataColumn { Title = OutlookText.T("reallife_news_col_spark"), Kind = UiCellKind.Spark, Width = 88f },
                new UiDataColumn { Title = OutlookText.T("reallife_econapp_district_worth"), Kind = UiCellKind.Figure, Width = 88f },
                new UiDataColumn { Title = OutlookText.T("reallife_econapp_district_land"), Kind = UiCellKind.Figure, Width = 96f },
                new UiDataColumn { Title = OutlookText.T("reallife_econapp_district_foot"), Kind = UiCellKind.Figure, Width = 80f },
                new UiDataColumn { Title = OutlookText.T("reallife_econapp_district_bears"), Kind = UiCellKind.Figure, Width = 88f },
                new UiDataColumn { Title = OutlookText.T("reallife_econapp_district_people"), Kind = UiCellKind.Text, Width = 128f }
            };
            if (demand)
            {
                columns.Add(new UiDataColumn { Title = OutlookText.T("reallife_econapp_district_demand"), Kind = UiCellKind.Text, Width = 152f });
            }
            if (level)
            {
                columns.Add(new UiDataColumn { Title = OutlookText.T("reallife_news_col_level"), Kind = UiCellKind.Figure, Width = 80f });
            }
            if (going)
            {
                columns.Add(new UiDataColumn { Title = OutlookText.T("reallife_news_col_going"), Kind = UiCellKind.Figure, Width = 88f });
            }
            columns.Add(new UiDataColumn { Title = OutlookText.T("reallife_pop_fig_rent"), Kind = UiCellKind.Figure, Width = 96f });
            return new UiDataTableModel
            {
                Columns = columns.ToArray(),
                Rows = rows.ToArray(),
                RowCount = rows.Count,
                EmptyTitle = OutlookText.T("reallife_econapp_districts_none_title"),
                EmptyBody = OutlookText.T("reallife_econapp_districts_none_body"),
                EmptyIcon = UiIcon.MapTrifold
            };
        }

        private static void Cell(List<string> cells, List<int> values, List<UiRole> tones, string text, int value, UiRole tone)
        {
            cells.Add(text);
            values.Add(value);
            tones.Add(tone);
        }

        private static string LedgerTextMoney(int money) => RealLifeOverhaul.Ledger.LedgerText.Money(money);

        // A use's demand out of a hundred for homes, shops, offices and warehouses; a dash where the mod does not keep it for the district.
        private static string DemandText(OutlookDistrict d)
        {
            var text = new System.Text.StringBuilder(24);
            for (int u = 0; u < d.Demand.Length; u++)
            {
                text.Append(u > 0 ? " / " : string.Empty).Append(d.Demand[u] < 0 ? "-" : d.Demand[u].ToString(Inv));
            }
            return text.ToString();
        }

        // Your businesses against what their districts bear, and your buildings at today's value: a table each, only for those you have.
        private void FillPlaces(NewsModel model)
        {
            var tables = new List<UiDataTableModel>(2);
            var titles = new List<string>(2);
            if (Look.Businesses.Count > 0)
            {
                tables.Add(ShopsTable());
                titles.Add(OutlookText.T("reallife_econapp_places_shops"));
            }
            if (Look.Estates.Count > 0)
            {
                tables.Add(EstatesTable());
                titles.Add(OutlookText.T("reallife_econapp_places_estates"));
            }
            model.Boards = tables.ToArray();
            model.BoardTitles = titles.ToArray();
        }

        private string DistrictOf(OutlookPlace p)
        {
            return string.IsNullOrEmpty(p.District) ? string.Empty : OutlookText.Words.District(p.District);
        }

        private UiDataTableModel ShopsTable()
        {
            var rows = new List<UiDataRow>(Look.Businesses.Count + 1);
            for (int i = 0; i < Look.Businesses.Count; i++)
            {
                OutlookPlace p = Look.Businesses[i];
                string customers = !p.Known ? OutlookText.T("reallife_macro_shop_no_figure")
                    : p.Customers == 0 ? OutlookText.T("reallife_macro_shop_none") : OutlookText.T("reallife_macro_shop_customers", "percent", OutlookWords.Whole(p.Customers));
                UiRole tone = !p.Known || p.Customers == 0 ? UiRole.Muted : p.Customers > 0 ? UiRole.Ok : UiRole.Danger;
                string verdict = p.Prices == 0 ? string.Empty : OutlookText.T(p.Prices == 2 ? "reallife_macro_shop_over" : "reallife_macro_shop_suit");
                rows.Add(new UiDataRow
                {
                    Cells = new[] { p.Name, DistrictOf(p), customers, verdict },
                    Values = new int[4],
                    Tones = new[] { UiRole.None, UiRole.None, tone, p.Prices == 2 ? UiRole.Danger : UiRole.Ok }
                });
            }
            if (Look.MoreBusinesses > 0)
            {
                rows.Add(MoreRow(4, Look.MoreBusinesses));
            }
            return new UiDataTableModel
            {
                Columns = new[]
                {
                    new UiDataColumn { Title = OutlookText.T("reallife_news_col_name"), Kind = UiCellKind.Text },
                    new UiDataColumn { Title = OutlookText.T("reallife_news_col_district"), Kind = UiCellKind.Text, Width = 140f },
                    new UiDataColumn { Title = OutlookText.T("reallife_news_col_customers"), Kind = UiCellKind.Chip, Width = 220f },
                    new UiDataColumn { Title = OutlookText.T("reallife_econapp_tile_prices"), Kind = UiCellKind.Chip, Width = 240f }
                },
                Rows = rows.ToArray(),
                RowCount = rows.Count,
                EmptyTitle = OutlookText.T("reallife_news_empty_city"),
                EmptyBody = OutlookText.T("reallife_news_empty_city_body"),
                EmptyIcon = UiIcon.Chart
            };
        }

        private UiDataTableModel EstatesTable()
        {
            var rows = new List<UiDataRow>(Look.Estates.Count + 1);
            for (int i = 0; i < Look.Estates.Count; i++)
            {
                OutlookPlace p = Look.Estates[i];
                int move = p.Factor - 1000;
                rows.Add(new UiDataRow
                {
                    Cells = new[] { p.Name, DistrictOf(p), OutlookText.Money(p.Value), OutlookText.Money(p.Today), OutlookWords.Factor(p.Factor) },
                    Values = new[] { 0, 0, 0, 0, move > 0 ? 1 : move < 0 ? -1 : 0 },
                    Tones = new UiRole[5]
                });
            }
            if (Look.MoreEstates > 0)
            {
                rows.Add(MoreRow(5, Look.MoreEstates));
            }
            return new UiDataTableModel
            {
                Columns = new[]
                {
                    new UiDataColumn { Title = OutlookText.T("reallife_news_col_name"), Kind = UiCellKind.Text },
                    new UiDataColumn { Title = OutlookText.T("reallife_news_col_district"), Kind = UiCellKind.Text, Width = 140f },
                    new UiDataColumn { Title = OutlookText.T("reallife_news_col_game_value"), Kind = UiCellKind.Figure, Width = 112f },
                    new UiDataColumn { Title = OutlookText.T("reallife_news_col_worth_today"), Kind = UiCellKind.Figure, Width = 112f },
                    new UiDataColumn { Title = OutlookText.T("reallife_news_col_move"), Kind = UiCellKind.Trend, Width = 104f }
                },
                Rows = rows.ToArray(),
                RowCount = rows.Count,
                EmptyTitle = OutlookText.T("reallife_news_empty_city"),
                EmptyBody = OutlookText.T("reallife_news_empty_city_body"),
                EmptyIcon = UiIcon.Chart
            };
        }

        // The last row of a list the reading cut short: how many more there are.
        private static UiDataRow MoreRow(int columns, int more)
        {
            var cells = new string[columns];
            cells[0] = OutlookText.T("reallife_econapp_places_more", "n", more.ToString(Inv));
            for (int i = 1; i < columns; i++)
            {
                cells[i] = string.Empty;
            }
            return new UiDataRow { Cells = cells, Values = new int[columns], Tones = new UiRole[columns] };
        }

        // ---------------------------------------------------------------- finances

        // The council's week, its four departments and the tax; with the council not started, the tax alone and what to do.
        private void FillFinanceCards(NewsModel model)
        {
            NewsPanel tax = TaxPanel();
            if (!Look.CouncilOn)
            {
                model.Chart = null;
                model.Figures = new UiFigureModel[0];
                model.EmptyTitle = OutlookText.T("reallife_econapp_council_off_title");
                model.EmptyBody = OutlookText.T("reallife_econapp_council_off_body");
                model.Panels = new[] { tax };
                return;
            }
            model.Panels = new[] { BooksPanel(), DepartmentsPanel(), tax };
        }

        private NewsPanel BooksPanel()
        {
            long most = Math.Max(1L, Math.Max(Look.Revenue, Look.Spending));
            long balance = (long)Look.Revenue - Look.Spending;
            return new NewsPanel
            {
                Title = OutlookText.T("reallife_econapp_books_title"),
                Note = OutlookText.T("reallife_econapp_books_note"),
                Rows = new[]
                {
                    new NewsRow
                    {
                        Label = OutlookText.T("reallife_econapp_books_revenue"), Value = OutlookText.Money(Look.Revenue), Bar = true, Share = Look.Revenue / (float)most,
                        MidAt = -1f, Fill = UiRole.Ok
                    },
                    new NewsRow
                    {
                        Label = OutlookText.T("reallife_econapp_books_spending"), Value = OutlookText.Money(Look.Spending), Bar = true, Share = Look.Spending / (float)most,
                        MidAt = -1f, Fill = UiRole.Warn
                    },
                    new NewsRow
                    {
                        Label = OutlookText.T("reallife_econapp_books_balance"), Value = OutlookText.Money(balance), Ink = balance >= 0 ? UiRole.OkText : UiRole.DangerText,
                        MidAt = -1f
                    }
                }
            };
        }

        // The four departments, each from two steps under its usual share of the spending to two over it.
        private NewsPanel DepartmentsPanel()
        {
            string[] keys = { "reallife_econapp_dept_safety", "reallife_econapp_dept_roads", "reallife_econapp_dept_services", "reallife_econapp_dept_living" };
            var rows = new NewsRow[4];
            for (int d = 0; d < 4; d++)
            {
                int level = Look.Departments[d] < -2 ? -2 : Look.Departments[d] > 2 ? 2 : Look.Departments[d];
                rows[d] = new NewsRow
                {
                    Label = OutlookText.T(keys[d]),
                    Value = level > 0 ? "+" + level.ToString(Inv) : level.ToString(Inv),
                    Bar = true,
                    Share = NewsCardRules.Step(level),
                    MidAt = 0.5f,
                    Fill = level == 0 ? UiRole.Notice : level > 0 ? UiRole.Ok : UiRole.Warn
                };
            }
            return new NewsPanel { Title = OutlookText.T("reallife_econapp_departments_title"), Note = OutlookText.T("reallife_econapp_departments_note"), Rows = rows };
        }

        // The tax: the rate the game charges and the bill that is owing, with the day it falls due.
        private NewsPanel TaxPanel()
        {
            bool owing = Look.TaxDue > 0;
            return new NewsPanel
            {
                Title = OutlookText.T("reallife_econapp_tax_title"),
                Note = OutlookText.T("reallife_econapp_tax_note"),
                Rows = new[]
                {
                    new NewsRow { Label = OutlookText.T("reallife_econapp_tax_rate"), Value = Look.TaxPercent.ToString(Inv) + "%", MidAt = -1f },
                    new NewsRow
                    {
                        Label = OutlookText.T("reallife_econapp_tax_bill"),
                        Value = owing ? OutlookText.Money(Look.TaxDue) : OutlookText.T("reallife_econapp_tax_nothing"),
                        Ink = owing ? UiRole.WarnText : UiRole.Text,
                        Detail = owing ? OutlookText.T("reallife_econapp_tax_due", "day", Look.TaxDay.ToString(Inv)) : null,
                        MidAt = -1f
                    }
                }
            };
        }
    }
}
