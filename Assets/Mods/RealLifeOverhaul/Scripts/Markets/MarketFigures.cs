using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Markets
{
    /// <summary>One more row of a company's page: its label's key and English words (the kit's fallback) and its value.</summary>
    public struct FigureRow
    {
        public string Key, English, Value;
    }

    /// <summary>
    /// What a company's page says beyond its figures: its size segment, why it and its sector last moved, and for the player's own company its revenue by business line and its
    /// statement for the quarter so far and the next, from the figures the engine already follows (the books' takings and profit, the founder's pay, the tax rate, the trade mix).
    /// The page's two interfaces both write these rows. Game-free; built when a page is written, never per frame.
    /// </summary>
    public static class MarketFigures
    {
        public const int LinesShown = 5;     // the business lines named, the most first
        public const int NewsShown = 2;      // the pieces of news the page carries

        /// <summary>The page's extra rows for a company; <paramref name="mine"/> the player's own company, whose books are theirs to see.</summary>
        public static void Rows(MarketState s, Stock k, in MarketEnv env, int day, bool mine, List<FigureRow> into)
        {
            int year = Math.Max(8, s.YearDays);
            int quarter = MarketCatalogue.QuarterOf(day, year);
            string worth = MarketText.Large(k.MarketValue);
            into.Add(new FigureRow
            {
                Key = "reallife_mkt_fig_segment", English = "Segment",
                Value = MarketText.Fill("reallife_mkt_fig_segment_value", "segment", MarketText.Size(k.Size), "worth", worth,
                    "large", MarketText.Large(MarketSegments.Band(MarketSegments.LargeFromM, env.PriceLevel)), "mid", MarketText.Large(MarketSegments.Band(MarketSegments.MidFromM, env.PriceLevel)),
                    "day", MarketCatalogue.QuarterStart(quarter + 1, year).ToString())
            });
            var news = new List<string>(NewsShown);
            PageNews(s, k, day, NewsShown, news);
            for (int i = 0; i < news.Count; i++)
            {
                into.Add(new FigureRow { Key = "reallife_mkt_fig_news", English = "Latest news", Value = news[i] });
            }
            if (!mine || k.Own == null || k.Own.Firm)
            {
                return;
            }
            OwnListing o = k.Own;
            var sectors = new int[LinesShown];
            var bps = new int[LinesShown];
            int lines = ListingSector.Top(o, sectors, bps);
            int quarterDays = MarketCatalogue.QuarterStart(quarter + 1, year) - MarketCatalogue.QuarterStart(quarter, year);
            for (int i = 0; i < lines; i++)
            {
                into.Add(new FigureRow
                {
                    Key = "reallife_mkt_fig_lines", English = "Takings by business line",
                    Value = MarketText.Fill("reallife_mkt_fig_line_value", "sector", MarketText.Sector((MarketSector)sectors[i]), "share", MarketText.Percent(bps[i]),
                        "money", MarketText.Large(o.RevQ * quarterDays * bps[i] / 10000.0))
                });
            }
            QuarterStatement now = MarketStatement.Now(k, env);
            if (now.Known && (now.Revenue > 0.0 || now.Net != 0.0))
            {
                into.Add(new FigureRow { Key = "reallife_mkt_fig_q_now", English = "This quarter so far", Value = Statement(now) });
            }
            QuarterStatement next = MarketStatement.Next(k, env, day, year, out int days);
            if (next.Known)
            {
                into.Add(new FigureRow { Key = "reallife_mkt_fig_q_next", English = "Next quarter, projected", Value = Statement(next) });
            }
            if (o.Expect != 0.0)
            {
                into.Add(new FigureRow { Key = "reallife_mkt_fig_q_expect", English = "Expected this quarter after tax", Value = MarketText.Large(o.Expect) });
            }
        }

        /// <summary>
        /// The pieces of news a company's page carries: its own news of the last days (results, a crisis or windfall, a bid, why it moved, its segment) and why its sector moved,
        /// newest first, as sentences, at most <paramref name="most"/>.
        /// </summary>
        public static int PageNews(MarketState s, Stock k, int day, int most, List<string> into)
        {
            int said = 0;
            for (int i = s.News.Count - 1; i >= 0 && said < most; i--)
            {
                MarketNews n = s.News[i];
                if (n.Day < day - MarketCatalogue.PageNewsDays)
                {
                    break;
                }
                bool mine = n.Stock == k.Id && n.Kind != MarketNewsKind.Mover && n.Kind != MarketNewsKind.Paid;
                bool sector = n.Kind == MarketNewsKind.Why && n.Stock == 0 && MarketWhy.SectorOf(n) == (int)k.Sector;
                if (!mine && !sector)
                {
                    continue;
                }
                string text = MarketText.News(n, s);
                if (text.Length > 0)
                {
                    into.Add(text);
                    said++;
                }
            }
            return said;
        }

        private static string Statement(in QuarterStatement q)
        {
            return MarketText.Fill("reallife_mkt_fig_statement", "revenue", MarketText.Large(q.Revenue), "costs", MarketText.Large(q.Costs), "pay", MarketText.Large(q.Pay),
                "tax", MarketText.Large(q.Tax), "net", MarketText.Large(q.Net));
        }
    }
}
