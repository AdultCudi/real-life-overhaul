using System;
using RealLifeOverhaul.Ui;

/// <summary>
/// The Stocks company page in each state the previews and the checks lay out: a made-up company of a hundred days with every
/// indicator, drawings, a hover, the tool rail's tip, the picker, the big screen, a limit buy with a position, the line and
/// area types and the five ranges. The numbers follow the market's own rules (the fee, the most the cash buys).
/// </summary>
internal static partial class UiSamples
{
    public static readonly string[] StockCompanyStates =
    {
        "company_days", "company_today", "company_weeks", "company_months", "company_years", "company_area", "company_line", "company_full", "company_draw",
        "company_hover", "company_tip", "company_picker", "company_big", "company_limit", "long"
    };

    /// <summary>A company's chart as a set of candles: a rising price with its noise, the last close $53.42.</summary>
    public static UiChartDeskModel StocksChartSample(UiWords t, int n, bool intraday, int kind, bool[] on)
    {
        var open = new float[n];
        var high = new float[n];
        var low = new float[n];
        var close = new float[n];
        var volume = new float[n];
        var day = new int[n];
        var stamp = new string[n];
        uint seed = 7919u;
        float price = 31f;
        for (int i = 0; i < n; i++)
        {
            seed = seed * 1664525u + 1013904223u;
            float noise = ((seed >> 8) % 1000) / 1000f - 0.47f;
            open[i] = price;
            price = Math.Max(5f, price + 0.22f + noise * 3.2f + (float)Math.Sin(i * 0.25) * 0.9f);
            close[i] = price;
            high[i] = Math.Max(open[i], close[i]) + 0.3f + ((seed >> 4) % 100) / 70f;
            low[i] = Math.Min(open[i], close[i]) - 0.3f - ((seed >> 12) % 100) / 70f;
            volume[i] = 120000f + ((seed >> 6) % 90000);
            day[i] = intraday ? 0 : i + 1 - n;
            stamp[i] = intraday ? (9 + (i * 15 + 30) / 60).ToString("00") + ":" + ((i * 15 + 30) % 60).ToString("00") : t("reallife_mkt_chart_day", "Day {day}").Replace("{day}", (i + 1 - n + 283).ToString());
        }
        float scale = 53.42f / close[n - 1];
        for (int i = 0; i < n; i++)
        {
            open[i] *= scale;
            high[i] *= scale;
            low[i] *= scale;
            close[i] *= scale;
        }
        var m = new UiChartDeskModel
        {
            Kind = kind, Head = "HSF, " + (intraday ? t("reallife_mkt_frame_today", "Today") : t("reallife_mkt_frame_days", "Days")), Open = open, High = high, Low = low, Close = close,
            Volume = volume, Day = day, Stamp = stamp, Shown = n, First = 0, Price = v => "$" + v.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
            LastTag = "$53.42", LastUp = true, DayLabel = t("reallife_mkt_col_day", "Day"), OpenLabel = t("reallife_stk_open", "Open"), HighLabel = t("reallife_stk_high", "High"),
            LowLabel = t("reallife_stk_low", "Low"), CloseLabel = t("reallife_stk_close", "Close"), VolumeLabel = t("reallife_mkt_col_volume", "Volume"),
            EmptyTitle = t("reallife_stk_chart_empty", "No candles yet"), EmptyBody = t("reallife_stk_chart_empty_body", "Candles appear as the market trades."),
            DeleteName = t("reallife_stk_delete_all", "Delete every drawing"), ToolNames = new string[UiChartTools.Count]
        };
        for (int i = 0; i < UiChartTools.Count; i++)
        {
            m.ToolNames[i] = t(UiChartTools.Keys[i], UiChartTools.English[i]);
        }
        var lines = new System.Collections.Generic.List<UiChartLine>();
        var panes = new System.Collections.Generic.List<UiChartPane>();
        string Name(StocksInd i) => t(StocksIndicators.Keys[(int)i], StocksIndicators.English[(int)i]);
        if (on != null && on[(int)StocksInd.Sma20])
        {
            lines.Add(new UiChartLine { Name = Name(StocksInd.Sma20), Values = SampleSma(close, 20), Tint = StocksIndicators.Tint(StocksInd.Sma20), Ind = StocksInd.Sma20 });
        }
        if (on != null && on[(int)StocksInd.Sma50])
        {
            lines.Add(new UiChartLine { Name = Name(StocksInd.Sma50), Values = SampleSma(close, 50), Tint = StocksIndicators.Tint(StocksInd.Sma50), Ind = StocksInd.Sma50 });
        }
        if (on != null && on[(int)StocksInd.Bollinger])
        {
            float[] mid = SampleSma(close, 20);
            var upper = new float[n];
            var lower = new float[n];
            for (int i = 0; i < n; i++)
            {
                float dev = 0f;
                for (int k = Math.Max(0, i - 19); k <= i; k++)
                {
                    dev += (close[k] - mid[i]) * (close[k] - mid[i]);
                }
                dev = (float)Math.Sqrt(dev / 20f);
                upper[i] = float.IsNaN(mid[i]) ? float.NaN : mid[i] + 2f * dev;
                lower[i] = float.IsNaN(mid[i]) ? float.NaN : mid[i] - 2f * dev;
            }
            lines.Add(new UiChartLine { Name = Name(StocksInd.Bollinger), Values = upper, Under = lower, Tint = StocksIndicators.Tint(StocksInd.Bollinger), Tinted = true, Ind = StocksInd.Bollinger });
        }
        if (on != null && on[(int)StocksInd.Rsi])
        {
            var rsi = new float[n];
            for (int i = 0; i < n; i++)
            {
                float up = 0f, down = 0f;
                for (int k = Math.Max(1, i - 13); k <= i; k++)
                {
                    float d = close[k] - close[k - 1];
                    up += Math.Max(0f, d);
                    down += Math.Max(0f, -d);
                }
                rsi[i] = i < 14 ? float.NaN : down < 1e-4f ? 100f : 100f - 100f / (1f + up / down);
            }
            panes.Add(new UiChartPane { Name = Name(StocksInd.Rsi), Values = rsi, Tint = StocksIndicators.Tint(StocksInd.Rsi), Fixed = true, Guides = new[] { 70f, 30f }, Format = "0.0", Ind = StocksInd.Rsi });
        }
        if (on != null && on[(int)StocksInd.Macd])
        {
            float[] fast = SampleEma(close, 12), slow = SampleEma(close, 26);
            var macd = new float[n];
            for (int i = 0; i < n; i++)
            {
                macd[i] = i < 25 ? float.NaN : fast[i] - slow[i];
            }
            float[] signal = SampleEma(macd, 9);
            var hist = new float[n];
            for (int i = 0; i < n; i++)
            {
                hist[i] = float.IsNaN(signal[i]) ? float.NaN : macd[i] - signal[i];
            }
            panes.Add(new UiChartPane
            {
                Name = Name(StocksInd.Macd), Values = macd, Second = signal, Histogram = hist, Tint = StocksIndicators.Tint(StocksInd.Macd), SecondTint = StocksIndicators.Second(StocksInd.Macd),
                Format = "0.00", Ind = StocksInd.Macd
            });
        }
        m.Lines = lines.ToArray();
        m.Panes = panes.ToArray();
        return m;
    }

    private static float[] SampleSma(float[] v, int period)
    {
        var o = new float[v.Length];
        for (int i = 0; i < v.Length; i++)
        {
            if (i < period - 1)
            {
                o[i] = float.NaN;
                continue;
            }
            float sum = 0f;
            for (int k = i - period + 1; k <= i; k++)
            {
                sum += v[k];
            }
            o[i] = sum / period;
        }
        return o;
    }

    private static float[] SampleEma(float[] v, int period)
    {
        var o = new float[v.Length];
        float k = 2f / (period + 1f), prev = float.NaN;
        for (int i = 0; i < v.Length; i++)
        {
            if (float.IsNaN(v[i]))
            {
                o[i] = float.NaN;
                continue;
            }
            prev = float.IsNaN(prev) ? v[i] : v[i] * k + prev * (1f - k);
            o[i] = prev;
        }
        return o;
    }

    private static void Company(StocksModel m, UiWords t, string state)
    {
        bool today = state == "company_today", years = state == "company_years", weeks = state == "company_weeks", months = state == "company_months";
        bool full = state == "company_full" || state == "company_draw" || state == "company_hover" || state == "company_tip" || state == "company_picker" || state == "company_big";
        int n = today ? 26 : years ? 8 : weeks ? 40 : months ? 30 : 104;
        int kind = state == "company_area" ? 2 : state == "company_line" ? 1 : 0;
        var on = new bool[(int)StocksInd.Count];
        on[(int)StocksInd.Sma20] = true;
        if (full)
        {
            on[(int)StocksInd.Sma50] = true;
            on[(int)StocksInd.Bollinger] = true;
            on[(int)StocksInd.Rsi] = true;
            on[(int)StocksInd.Macd] = true;
        }
        UiChartDeskModel chart = StocksChartSample(t, n, today, kind, on);
        string frame = today ? "Today" : years ? "Years" : weeks ? "Weeks" : months ? "Months" : "Days";
        chart.Head = "HSF, " + t("reallife_mkt_frame_" + frame.ToLowerInvariant(), frame);
        if (today)
        {
            chart.Marks = new UiChartMark[0];
        }
        if (state == "company_draw" || state == "company_hover" || state == "company_big")
        {
            chart.Marks = new[]
            {
                new UiChartMark { Kind = UiChartMarkKind.Trend, FromX = n - 70, ToX = n - 12, FromY = chart.Close[n - 70], ToY = chart.Close[n - 12] + 1.5f },
                new UiChartMark { Kind = UiChartMarkKind.Level, FromX = 0, ToX = 0, FromY = 38.65f, ToY = 38.65f, Label = "$38.65", Tint = UiRole.Danger },
                new UiChartMark { Kind = UiChartMarkKind.Fibonacci, FromX = n - 50, ToX = n - 8, FromY = 48.61f, ToY = 38.25f, Selected = state == "company_draw" },
                new UiChartMark { Kind = UiChartMarkKind.Ray, FromX = n - 30, ToX = n - 20, FromY = chart.Close[n - 30] - 4f, ToY = chart.Close[n - 20] - 4f },
                new UiChartMark { Kind = UiChartMarkKind.Measure, FromX = n - 90, ToX = n - 75, FromY = chart.Close[n - 90], ToY = chart.Close[n - 75], Label = "+9.4% in 15 days" },
                new UiChartMark { Kind = UiChartMarkKind.Rectangle, FromX = n - 100, ToX = n - 88, FromY = chart.Close[n - 100] + 3f, ToY = chart.Close[n - 100] - 3f },
                new UiChartMark { Kind = UiChartMarkKind.Trend, FromX = n + 20, ToX = n + 40, FromY = 70f, ToY = 90f }   // wholly off the plot: nothing of it is drawn
            };
        }
        else if (state == "company_limit")
        {
            chart.Marks = new[] { new UiChartMark { Kind = UiChartMarkKind.Level, FromX = 0, ToX = 0, FromY = 38.65f, ToY = 38.65f, Label = "$38.65" } };
        }
        if (state == "company_hover")
        {
            chart.Focus = Math.Max(0, n - 34);
        }
        if (state == "company_tip")
        {
            chart.Tip = 4;
            chart.Tool = 1;
        }
        if (state == "company_draw")
        {
            chart.Tool = 1;
            chart.Anchor = n - 6;
            chart.AnchorY = chart.Close[n - 6];
            chart.DeleteName = t("reallife_stk_delete_one", "Delete this drawing");
        }
        chart.Shown = n;
        var c = new StocksCompany
        {
            Name = state == "long" ? "Harbourside Freight Holdings International Corporation" : "Harbourside Freight", Ticker = "HSF", Sector = t("reallife_mkt_sector_logistics", "Logistics"),
            Meta = t("reallife_stk_cap_mid", "Mid cap"), LogoTint = UiRole.Chart1,
            Price = "$53.42", ChangeMoney = "+$0.69", ChangePercent = "+1.31%", Way = 1, Watching = state == "company_limit",
            FactLabels = new[] { t("reallife_stk_open", "Open"), t("reallife_stk_high", "High"), t("reallife_stk_low", "Low"), t("reallife_mkt_col_volume", "Volume"), t("reallife_stk_cap", "Market cap"), t("reallife_mkt_col_pe", "P/E"), t("reallife_mkt_col_yield", "Yield") },
            FactValues = new[] { "$52.73", "$53.78", "$52.49", "148.2K", "$2.6B", "14.8", "2.9%" },
            Ranges = new[] { t("reallife_mkt_frame_today", "Today"), t("reallife_mkt_frame_days", "Days"), t("reallife_mkt_frame_weeks", "Weeks"), t("reallife_mkt_frame_months", "Months"), t("reallife_mkt_frame_years", "Years") },
            Range = today ? 0 : years ? 4 : weeks ? 2 : months ? 3 : 1,
            Types = new[] { t("reallife_stk_kind_candles", "Candles"), t("reallife_stk_kind_line", "Line"), t("reallife_stk_kind_area", "Area") }, Type = kind,
            Views = new[] { t("reallife_mkt_view_chart", "Chart"), t("reallife_mkt_view_figures", "Figures") },
            AddLabel = t("reallife_stk_add_indicator", "+ Indicator"), Big = state == "company_big", PickOpen = state == "company_picker",
            Chart = chart
        };
        int count = (int)StocksInd.Count;
        c.PickNames = new string[count];
        c.PickHints = new string[count];
        c.PickOn = on;
        var chips = new System.Collections.Generic.List<StocksChip>();
        for (int i = 0; i < count; i++)
        {
            c.PickNames[i] = t(StocksIndicators.Keys[i], StocksIndicators.English[i]);
            c.PickHints[i] = t(StocksIndicators.HintKeys[i], StocksIndicators.HintEnglish[i]);
            if (on[i])
            {
                chips.Add(new StocksChip { Label = c.PickNames[i], Tint = StocksIndicators.Tint((StocksInd)i), Ind = (StocksInd)i });
            }
        }
        c.Chips = chips.ToArray();
        bool limit = state == "company_limit";
        c.Order = new StocksTicket
        {
            Title = t("reallife_stk_order_title", "Order"), CashWords = t("reallife_stk_cash_line", "Cash {cash}").Replace("{cash}", "$1,204,300"),
            Sides = new[] { t("reallife_mkt_buy", "Buy"), t("reallife_mkt_sell", "Sell") },
            Kinds = new[] { t("reallife_stk_market_order", "Market"), t("reallife_stk_limit", "Limit"), t("reallife_mkt_type_stop_loss", "Stop loss"), t("reallife_mkt_type_take_profit", "Take profit") },
            Lives = new[] { t("reallife_stk_day", "Day"), t("reallife_stk_gtc", "Until cancelled") },
            SideAt = 0, KindAt = limit ? 1 : 0, LifeAt = 0, LifeLabel = t("reallife_stk_good_for", "Good for"), SharesLabel = t("reallife_mkt_col_shares", "Shares"),
            PriceLabel = limit ? t("reallife_stk_limit_price", "Limit price") : null, Shares = "500", Price = limit ? "$53.22" : null, PutBackLabel = t("reallife_stk_put_back", "Dividends put back"),
            PutBack = limit ? "50%" : null,
            Rows = limit
                ? new[]
                {
                    new StocksRow { Label = t("reallife_stk_row_cost", "Cost"), Value = "$26,610" }, new StocksRow { Label = t("reallife_stk_row_fee", "Fee"), Value = "$27" },
                    new StocksRow { Label = t("reallife_stk_row_total", "Total"), Value = "$26,637" }, new StocksRow { Label = t("reallife_stk_cash_after", "Cash after"), Value = "$1,177,663" },
                    new StocksRow { Label = t("reallife_stk_position_after", "Position after"), Value = t("reallife_stk_position_value", "{shares} at {price}").Replace("{shares}", "1,700").Replace("{price}", "$45.76") },
                    new StocksRow { Label = t("reallife_stk_row_most_buy", "Most you can buy"), Value = "22,590" }
                }
                : new[]
                {
                    new StocksRow { Label = t("reallife_stk_row_price", "Price"), Value = "$53.42" }, new StocksRow { Label = t("reallife_stk_row_cost", "Cost"), Value = "$26,710" },
                    new StocksRow { Label = t("reallife_stk_row_fee", "Fee"), Value = "$27" }, new StocksRow { Label = t("reallife_stk_row_total", "Total"), Value = "$26,737" },
                    new StocksRow { Label = t("reallife_stk_cash_after", "Cash after"), Value = "$1,177,563" },
                    new StocksRow { Label = t("reallife_stk_position_after", "Position after"), Value = t("reallife_stk_position_value", "{shares} at {price}").Replace("{shares}", "500").Replace("{price}", "$53.42") },
                    new StocksRow { Label = t("reallife_stk_row_most_buy", "Most you can buy"), Value = "22,590" }
                },
            Place = t("reallife_stk_place_buy", "Place buy order")
        };
        c.PositionTitle = t("reallife_stk_position_title", "Your position");
        c.PositionNone = t("reallife_stk_position_none", "You hold no shares of this company");
        if (limit || full)
        {
            c.Position = new[]
            {
                new StocksRow { Label = t("reallife_mkt_col_shares", "Shares"), Value = "1,200" }, new StocksRow { Label = t("reallife_stk_avg_cost", "Average cost"), Value = "$44.10" },
                new StocksRow { Label = t("reallife_stk_gain", "Gain"), Value = "+$11,185 (+21.1%)", Tone = UiRole.OkText }
            };
        }
        c.SignalsTitle = t("reallife_stk_signals_title", "Signals");
        c.SignalsNone = t("reallife_stk_signals_none", "Add an indicator or draw a line to see what it says");
        c.Signals = StocksSignals.Read(chart, t);
        c.AlertsTitle = t("reallife_stk_alerts_title", "Price alerts");
        c.AlertsNone = t("reallife_stk_alerts_none", "No alert for this company");
        c.AlertSet = t("reallife_stk_alert_set", "Set an alert");
        c.AlertLine = t("reallife_stk_alert_line_off", "Pick a line first");
        c.AlertLineOff = true;
        if (limit || full)
        {
            c.Alerts = new[]
            {
                new StocksAlertRow { Words = t("reallife_stk_alert_above", "Above {price}").Replace("{price}", "$56.00"), State = t("reallife_stk_alert_state_on", "on") },
                new StocksAlertRow { Words = t("reallife_stk_alert_below", "Below {price}").Replace("{price}", "$38.65"), State = t("reallife_stk_alert_state_on", "on") }
            };
        }
        m.Co = c;
        m.PromptPlace = c.Order.Place;
        m.PromptOther = c.Big ? t("reallife_stk_small_chart", "Close the big chart") : t("reallife_stk_big_chart", "Open the big chart");
    }
}
