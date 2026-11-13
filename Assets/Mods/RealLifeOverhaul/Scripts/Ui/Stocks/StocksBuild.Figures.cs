using System;
using System.Globalization;
using RealLifeOverhaul.Markets;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The company page's Figures view: the worth, the year's turnover and profit, the ratios, the dividend, the ranges and the quarterly
    /// results, with the sums the old page makes (MarketPage.Figures).
    /// </summary>
    public static partial class StocksBuild
    {
        public const int ResultsShown = 6;   // the newest quarters the results table carries

        private static StocksFacts Facts(MarketState state, Stock k, UiWords words)
        {
            double worth = k.Shares * (k.Last / 100.0);
            long revenue = 0, profit = 0;
            int quarters = 0;
            for (int i = Math.Max(0, k.Results.Count - 4); i < k.Results.Count; i++)
            {
                revenue += k.Results[i].RevenueK * 1000L;
                profit += k.Results[i].ProfitK * 1000L;
                quarters++;
            }
            double eps = MarketApp.TrailingEps(k), pe = MarketApp.Pe(k);
            double book = k.Shares > 0 ? k.Equity / k.Shares : 0.0;
            var left = new System.Collections.Generic.List<StocksRow>(10)
            {
                Row(words("reallife_mkt_fig_worth", "Worth"), MarketText.Large(worth)),
                Row(words("reallife_mkt_fig_shares", "Shares in issue"), k.Shares.ToString("N0")),
                Row(words("reallife_mkt_fig_turnover", "Turnover, last four quarters"), quarters == 4 ? MarketText.Large(revenue) : "-"),
                Row(words("reallife_mkt_fig_profit", "Profit, last four quarters"), quarters == 4 ? MarketText.Large(profit) : "-"),
                Row(words("reallife_mkt_fig_margin", "Profit on turnover"), quarters == 4 && revenue > 0 ? MarketText.Percent((int)(profit * 10000L / revenue)) : "-"),
                Row(words("reallife_mkt_fig_eps", "Earnings a share"), double.IsNaN(eps) ? "-" : MarketText.PerShare((int)Math.Round(eps * 10000.0))),
                Row(words("reallife_mkt_fig_pe", "Price on earnings"), double.IsNaN(pe) ? "-" : pe.ToString("0.0", CultureInfo.CurrentCulture)),
                Row(words("reallife_mkt_fig_book", "Book value a share"), book > 0
                    ? MarketText.Price((int)Math.Round(book * 100.0)) + "  ("
                        + MarketText.Fill("reallife_mkt_fig_pb", "times", (k.Last / 100.0 / book).ToString("0.0", CultureInfo.CurrentCulture)) + ")"
                    : "-"),
                Row(words("reallife_mkt_fig_debt", "Debt"), MarketText.Large(k.Debt)
                    + (k.Rev > 0 ? "  (" + MarketText.Fill("reallife_mkt_fig_of_turnover", "share", MarketText.Percent((int)(k.Debt * 10000.0 / k.Rev))) + ")" : string.Empty))
            };
            var right = new System.Collections.Generic.List<StocksRow>(10);
            int yearly = k.DpsCents * 4;
            right.Add(Row(words("reallife_mkt_fig_dividend", "Dividend a year"),
                yearly > 0 ? MarketText.Price(yearly) + "  (" + MarketText.Percent(MarketApp.YieldBp(k)) + ")" : words("reallife_mkt_fig_none", "None")));
            right.Add(Row(words("reallife_mkt_fig_payout", "Of earnings paid out"),
                yearly > 0 && !double.IsNaN(eps) && eps > 0 ? MarketText.Percent((int)(yearly / 100.0 / eps * 10000.0)) : "-"));
            if (k.DeclaredCents > 0 && k.PayDay >= state.Day)
            {
                right.Add(Row(words("reallife_mkt_fig_declared", "Dividend declared"), MarketText.Fill("reallife_mkt_fig_declared_value", "amount", MarketText.Price(k.DeclaredCents),
                    "record", k.RecordDay.ToString(), "pay", k.PayDay.ToString())));
            }
            right.Add(Row(words("reallife_mkt_fig_results", "Next results"), k.ResultsDay > 0 ? MarketText.Fill("reallife_mkt_day", "day", k.ResultsDay.ToString()) : "-"));
            right.Add(Row(words("reallife_mkt_fig_beta", "Moves with the market (beta)"), (k.Def.Beta / 100.0).ToString("0.00", CultureInfo.CurrentCulture)));
            right.Add(Row(words("reallife_mkt_fig_range_day", "Today's range"), MarketText.Fill("reallife_mkt_range", "low", MarketText.Price(k.Low), "high", MarketText.Price(k.High))));
            int lo = int.MaxValue, hi = 0;
            for (int i = 0; i < k.Daily.Count; i++)
            {
                lo = Math.Min(lo, k.Daily[i].L);
                hi = Math.Max(hi, k.Daily[i].H);
            }
            right.Add(Row(words("reallife_mkt_fig_range_days", "Range over the last 100 days"),
                hi > 0 ? MarketText.Fill("reallife_mkt_range", "low", MarketText.Price(lo), "high", MarketText.Price(hi)) : "-"));
            right.Add(Row(words("reallife_mkt_fig_volume", "Usual volume"), ((long)k.Adv).ToString("N0")));
            bool high = k.Distress >= 0.3, some = k.Distress >= MarketCatalogue.DistressFrom;
            right.Add(new StocksRow
            {
                Label = words("reallife_mkt_fig_distress", "Its finances"),
                Value = words(high ? "reallife_mkt_fig_distress_high" : some ? "reallife_mkt_fig_distress_some" : "reallife_mkt_fig_distress_none",
                    high ? "in trouble: it may fail" : some ? "stretched: its debt weighs on it" : "sound"),
                Tone = high ? UiRole.DangerText : some ? UiRole.WarnText : UiRole.None
            });

            int n = Math.Min(ResultsShown, k.Results.Count);
            var rows = new string[n][];
            var beat = new bool[n];
            for (int i = 0; i < n; i++)
            {
                QuarterResult r = k.Results[k.Results.Count - 1 - i];
                int y = MarketCatalogue.FloorDiv(r.Quarter, 4);
                beat[i] = r.Eps >= r.Consensus;
                rows[i] = new[]
                {
                    MarketText.Fill("reallife_mkt_res_quarter_value", "quarter", (r.Quarter - y * 4 + 1).ToString(), "year", (y + 1).ToString()), r.Day.ToString(),
                    MarketText.Large(r.RevenueK * 1000.0), MarketText.Large(r.ProfitK * 1000.0), MarketText.PerShare(r.Eps), MarketText.PerShare(r.Consensus),
                    r.DpsCents > 0 ? MarketText.Price(r.DpsCents) : "-"
                };
            }
            return new StocksFacts
            {
                Left = left.ToArray(), Right = right.ToArray(),
                ResultHead = new[]
                {
                    words("reallife_mkt_res_quarter", "Quarter"), words("reallife_mkt_res_day", "Day"), words("reallife_mkt_res_turnover", "Turnover"), words("reallife_mkt_res_profit", "Profit"),
                    words("reallife_mkt_res_eps", "Earnings"), words("reallife_mkt_res_expected", "Expected"), words("reallife_mkt_res_dividend", "Dividend")
                },
                Results = rows, Beat = beat
            };
        }

        private static StocksRow Row(string label, string value) => new StocksRow { Label = label, Value = value };
    }
}
