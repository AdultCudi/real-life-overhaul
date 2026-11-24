using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Markets
{
    /// <summary>What a fall in the player's own company's price is put down to. Never saved: the news carries it as a number only while it is on the page.</summary>
    public enum OwnFallCause
    {
        None = 0,
        Figures,    // its latest profit fell on the quarter before, and the price follows the books
        Market,     // the city's index fell over the same days, and a share moves with the market by its beta
        Sector,     // the other companies of its sector fell over the same days
        Mood        // none of those: the market's mood about the shares, which the books pull back over the years
    }

    /// <summary>One measured fall: how far the price stands under the year's high, what the market did over the same days and which of them accounts for it.</summary>
    public struct OwnFall
    {
        public bool Fell;             // the price stands <see cref="OwnFallRules.FallBp"/> or more under the high
        public int PeakCents, NowCents, PeakDay;
        public int FallBp;            // positive: how far under the high
        public OwnFallCause Cause;
        public int IndexBp;           // the city index's move since the high's day, signed
        public int PeersBp;           // the other companies of its sector, weighed by value, since the high's day, signed; 0 with too few of them
        public int ProfitBp;          // the latest quarter's profit on the one before, signed; 0 when there are not two
        public bool HasProfit;
        public int WorthCents;        // what the books alone support a share, cents; 0 not worked out
    }

    /// <summary>
    /// Why the price of the player's own company fell far, worked out from what the market already follows: its daily closes, the city index, the companies of its sector, its two
    /// latest results and its worth from the books. Nothing is drawn and nothing is invented: a cause is named only if the figures show it by at least <see cref="ShareBp"/> of the
    /// fall, and the rest is called the market's mood. The same on every machine (pure functions of the market's state). Game-free. COST: one pass over the sector's companies and the
    /// closes kept, run once at a close for the player's company and once when its page is written; never per frame.
    /// </summary>
    public static class OwnFallRules
    {
        public const int FallBp = 3000;        // under the year's high by this much is a fall worth telling: 30 in a hundred
        public const int FiguresBp = 1500;     // a latest profit this far under the quarter before is what moved it
        public const int ShareBp = 4000;       // a market or sector move must stand for this share of the fall (a share of the fall, in basis points of it) to be named
        public const int PeersLeast = 3;       // companies of its sector needed for the sector's move to count

        /// <summary>The days the high is looked for in: a game year, as far back as the daily closes reach.</summary>
        public static int WindowDays(int year)
        {
            return Math.Max(8, year);
        }

        /// <summary>The days between two notices of a fall: a quarter.</summary>
        public static int GapDays(int year)
        {
            return Math.Max(2, Math.Max(8, year) / 4);
        }

        /// <summary>How far under the window's highest close a price stands, in basis points (never under nothing), and that close and its day.</summary>
        public static int Drawdown(List<Candle> daily, int day, int cents, int windowDays, out int peakCents, out int peakDay)
        {
            peakCents = 0;
            peakDay = 0;
            int from = day - windowDays;
            for (int i = daily.Count - 1; i >= 0; i--)
            {
                Candle c = daily[i];
                if (c.Day < from)
                {
                    break;
                }
                if (c.Day <= day && !c.Empty && c.C > peakCents)
                {
                    peakCents = c.C;
                    peakDay = c.Day;
                }
            }
            if (peakCents <= 0 || cents >= peakCents)
            {
                return 0;
            }
            return (int)((peakCents - (long)cents) * 10000L / peakCents);
        }

        /// <summary>The latest close on a day before <paramref name="day"/>; 0 when there is none.</summary>
        public static int CloseBefore(List<Candle> daily, int day)
        {
            for (int i = daily.Count - 1; i >= 0; i--)
            {
                if (daily[i].Day < day && !daily[i].Empty)
                {
                    return daily[i].C;
                }
            }
            return 0;
        }

        /// <summary>The latest close on or before a day; 0 when the closes kept begin after it.</summary>
        public static int CloseOn(List<Candle> daily, int day)
        {
            for (int i = daily.Count - 1; i >= 0; i--)
            {
                if (daily[i].Day <= day && !daily[i].Empty)
                {
                    return daily[i].C;
                }
            }
            return 0;
        }

        /// <summary>The price in cents the books alone support a share: its worth with no expectation beyond the figures, and the reserve it carries.</summary>
        public static int WorthCents(Stock s, in MarketEnv env)
        {
            double each = MarketValuation.Worth(s, env, 0.0) + (s.Own != null ? Math.Max(0.0, s.Own.Rps) : 0.0);
            return each <= 0.0 ? 0 : (int)Math.Min(int.MaxValue, Math.Round(each * 100.0));
        }

        /// <summary>
        /// Measures a company's price against the high of its year and, when it stands far under, what the market did over the same days. <paramref name="cents"/> is the price (the
        /// close at a close). The cause is the largest of the named ones: the figures first (a profit that fell is the books' own), then the market or the sector by their size.
        /// </summary>
        public static OwnFall Measure(MarketState state, Stock s, int day, int cents, in MarketEnv env)
        {
            var f = new OwnFall { NowCents = cents };
            if (state == null || s == null || s.Own == null || cents <= 0)
            {
                return f;
            }
            f.FallBp = Drawdown(s.Daily, day, cents, WindowDays(state.YearDays), out f.PeakCents, out f.PeakDay);
            f.Fell = f.FallBp >= FallBp;
            if (!f.Fell)
            {
                return f;
            }
            int index = CloseOn(state.IndexDaily, f.PeakDay);
            f.IndexBp = index > 0 && state.IndexLast > 0 ? (int)((state.IndexLast - (long)index) * 10000L / index) : 0;
            f.PeersBp = Peers(state, s, f.PeakDay, out int peers) >= PeersLeast ? peers : 0;
            int n = s.Results.Count;
            if (n >= 2 && s.Results[n - 2].ProfitK > 0)
            {
                f.HasProfit = true;
                f.ProfitBp = (int)((s.Results[n - 1].ProfitK - (long)s.Results[n - 2].ProfitK) * 10000L / s.Results[n - 2].ProfitK);
            }
            f.WorthCents = WorthCents(s, env);
            f.Cause = Cause(f.FallBp, f.IndexBp, s.Def != null ? s.Def.Beta : 100, f.PeersBp, f.HasProfit, f.ProfitBp);
            return f;
        }

        /// <summary>The cause by the figures; pure, so the offline checks hold its edges.</summary>
        public static OwnFallCause Cause(int fallBp, int indexBp, int beta, int peersBp, bool hasProfit, int profitBp)
        {
            if (hasProfit && profitBp <= -FiguresBp)
            {
                return OwnFallCause.Figures;
            }
            long market = indexBp < 0 ? -(long)indexBp * Math.Max(0, beta) / 100L : 0L;
            long sector = peersBp < 0 ? -(long)peersBp : 0L;
            long need = (long)fallBp * ShareBp / 10000L;
            bool byMarket = market > 0 && market >= need, bySector = sector > 0 && sector >= need;
            if (byMarket && (!bySector || market >= sector))
            {
                return OwnFallCause.Market;
            }
            if (bySector)
            {
                return OwnFallCause.Sector;
            }
            return OwnFallCause.Mood;
        }

        // The other companies of the sector, by their value, from the high's day to now, as a move in basis points; returns how many there were.
        private static int Peers(MarketState state, Stock own, int fromDay, out int bp)
        {
            bp = 0;
            double weight = 0.0, sum = 0.0;
            int count = 0;
            for (int i = 0; i < state.Stocks.Count; i++)
            {
                Stock k = state.Stocks[i];
                if (k == own || !k.Listed || k.Own != null || k.Sector != own.Sector || k.BidCents > 0 || k.Last <= 0)
                {
                    continue;
                }
                int then = CloseOn(k.Daily, fromDay);
                if (then <= 0)
                {
                    continue;
                }
                double w = MarketIndex.Value(k);
                sum += w * Math.Log(k.Last / (double)then);
                weight += w;
                count++;
            }
            if (count > 0 && weight > 0.0)
            {
                bp = (int)Math.Round((Math.Exp(sum / weight) - 1.0) * 10000.0);
            }
            return count;
        }
    }
}
