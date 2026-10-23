using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Markets
{
    /// <summary>What a piece of builder news did to one company, so a test's can be taken back.</summary>
    public struct BuilderMove
    {
        public int Stock;
        public double Mood;      // ln: added to its mood
        public double Margin;    // added to its usual margin for good
    }

    /// <summary>
    /// Part of the engine: the city's builders (BuilderMarket). A piece of the construction game's news is an event on the
    /// tape (EventBuilder), so every machine plays it at the same moment; it moves the builder it names, and for a tender
    /// or the district's growth every other listed builder too, through its mood and for a scandal or an award its usual
    /// margin as a crisis does. At each close a listed builder may have news of its own, drawn from the day's secret. A
    /// look at five companies an event and a draw each a close; nothing per frame.
    /// </summary>
    public sealed partial class MarketEngine
    {
        // The builders' own news is drawn only in a market that lists them (their own rows joined it while the option was
        // on): the market's state decides, so every machine of a session that plays the same tape draws the same.
        private bool BuildersListed()
        {
            for (int i = 0; i < BuilderMarket.OwnRows.Length; i++)
            {
                if (State.Stock(BuilderMarket.OwnRows[i]) != null)
                {
                    return true;
                }
            }
            return false;
        }

        // The construction game's news as the tape plays it.
        private void BuilderEvent(in MarketEvent e)
        {
            Builder(e.A, BuilderMarket.KindOf(e.B), BuilderMarket.WeightOf(e.B), null);
        }

        // A listed builder's own news at the close: a bribe that came out or a strike, from the day's secret.
        private void BuildersClose(int day)
        {
            if (!BuildersListed())
            {
                return;
            }
            double year = TradingYear;
            for (int r = 0; r < BuilderMarket.Rivals; r++)
            {
                int id = BuilderMarket.RivalStock[r];
                Stock s = id > 0 ? State.Stock(id) : null;
                if (s == null || !s.Listed || s.BidCents > 0 || s.Own != null)
                {
                    continue;
                }
                BuilderNewsKind kind = BuilderMarket.Drawn(MarketRandom.Unit(_secret, MarketRandom.BuilderNews, id, 0), year);
                if (kind != BuilderNewsKind.None)
                {
                    Builder(r, kind, 1, null);
                }
            }
        }

        /// <summary>
        /// The test tools: a piece of builder news played now as the tape would play it; what it did to each company goes
        /// into <paramref name="moves"/>. How many companies it moved.
        /// </summary>
        public int BuilderNow(int rival, BuilderNewsKind kind, int weight, List<BuilderMove> moves)
        {
            Finish();
            return Builder(rival, kind, weight, moves);
        }

        /// <summary>The test tools: what <see cref="BuilderNow"/> did taken back, each company repriced; how many.</summary>
        public int BuilderUndo(IList<BuilderMove> moves)
        {
            Finish();
            int undone = 0;
            for (int i = moves.Count - 1; i >= 0; i--)
            {
                BuilderMove m = moves[i];
                Stock s = State.Stock(m.Stock);
                if (s == null || !s.Listed)
                {
                    continue;
                }
                Shift(s, -m.Mood, -m.Margin);
                undone++;
            }
            return undone;
        }

        // One piece of news: the named builder and, for a tender or the district, the other listed builders.
        private int Builder(int rival, BuilderNewsKind kind, int weight, List<BuilderMove> moves)
        {
            if (kind <= BuilderNewsKind.None || kind >= BuilderNewsKind.Count)
            {
                return 0;
            }
            bool others = BuilderMarket.MovesOthers(kind);
            int moved = 0;
            for (int r = 0; r < BuilderMarket.Rivals; r++)
            {
                int id = BuilderMarket.RivalStock[r];
                bool named = r == rival;
                if (id <= 0 || (!named && !others))
                {
                    continue;
                }
                Stock s = State.Stock(id);
                if (s == null || !s.Listed || s.Own != null)
                {
                    continue;
                }
                int before = s.MidCents;
                double mood = BuilderMarket.MoodBp(kind, weight, named, s.Size) / 10000.0;
                int permille = named ? BuilderMarket.MarginPermille(kind) : 0;
                double margin = 0.0;
                if (permille != 0)
                {
                    double usual = Math.Max(0.02, s.Def.MarginBp / 10000.0);
                    double shift = MarketValuation.Clamp(s.MarginShift + permille / 1000.0 * usual, -2.0 * usual, usual + 0.05);
                    margin = shift - s.MarginShift;
                }
                Shift(s, mood, margin);
                moves?.Add(new BuilderMove { Stock = id, Mood = mood, Margin = margin });
                moved++;
                if (named)
                {
                    int change = before > 0 ? (int)((long)(s.MidCents - before) * 10000L / before) : 0;
                    Say(new MarketNews { Day = State.Day, Kind = MarketNewsKind.Builder, Stock = id, A = (int)kind, B = change, C = Math.Max(1, Math.Min(BuilderMarket.WeightMost, weight)) });
                }
            }
            return moved;
        }

        // A company's mood and usual margin moved, priced at once (a margin as a crisis is: its worth is reckoned again).
        private void Shift(Stock s, double mood, double margin)
        {
            if (margin != 0.0)
            {
                s.MarginShift += margin;
                s.Margin = MarketValuation.Clamp(s.Margin + margin, -0.5, 0.8);
                s.MarginTarget += margin;
                s.Distress = MarketValuation.DistressOf(s, State.Env);
                s.LogFair = MarketLife.LogFairOf(s, State.Env, State.Day, Year);
            }
            s.Mis += mood;
            s.Sent += mood;
            if (Trading)
            {
                // Dealt at once, inside the day's range; after the close the price follows at the next open, the day's candle as it closed.
                s.Last = s.MidCents;
                s.High = Math.Max(s.High, s.Last);
                s.Low = s.Low > 0 ? Math.Min(s.Low, s.Last) : s.Last;
            }
        }
    }
}
