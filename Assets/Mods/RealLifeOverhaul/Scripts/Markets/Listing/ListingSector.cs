using System;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// The sector a player's company is listed in, from what it earns now: its takings by trade followed over about a quarter
    /// (<see cref="OwnListing.Mix"/>), and a review at each quarter's turn that moves it to the sector that leads them. A sector
    /// it no longer earns anything in is left at the next review; one that only leads must lead by <see cref="LeadShare"/> at
    /// <see cref="LeadReviews"/> reviews in a row, so a company split near evenly between two sectors stays where it is. One
    /// sector, not a blend: the sector indices, the sector news, the options' spread and every page read one sector a company.
    /// Game-free and the same on every machine. Costs a pass over the trades once a market morning and at a review.
    /// </summary>
    public static class ListingSector
    {
        public const int LeadReviews = 2;      // reviews in a row a sector must lead the one the company is in, before it moves...
        public const double LeadShare = 0.1;   // ...by at least this share of the takings
        private const int Sectors = (int)MarketSector.Count;

        // Trades no catalogue company is in, and the sector the market files them under.
        private static readonly string[] OwnTrades = { "florist", "cardealership", "eventplanning" };
        private static readonly MarketSector[] OwnSectors = { MarketSector.Specialists, MarketSector.Specialists, MarketSector.Leisure };

        private static readonly MarketSector[] OfTrade = Table();

        // Each trade's sector is that of the catalogue's first company in it: fixed, so every machine and every year agree.
        private static MarketSector[] Table()
        {
            var table = new MarketSector[MarketInputs.Trades.Length];
            for (int t = 0; t < table.Length; t++)
            {
                table[t] = MarketSector.Services;
                int own = Array.IndexOf(OwnTrades, MarketInputs.Trades[t]);
                if (own >= 0)
                {
                    table[t] = OwnSectors[own];
                    continue;
                }
                for (int i = 0; i < MarketCatalogue.All.Length; i++)
                {
                    if (MarketCatalogue.All[i].Trade == MarketInputs.Trades[t])
                    {
                        table[t] = MarketCatalogue.All[i].Sector;
                        break;
                    }
                }
            }
            return table;
        }

        /// <summary>The sector a trade (MarketInputs.Trades) is filed under; business services for none.</summary>
        public static MarketSector SectorOf(int trade)
        {
            return trade >= 0 && trade < OfTrade.Length ? OfTrade[trade] : MarketSector.Services;
        }

        /// <summary>The trades' takings added up by sector into <paramref name="bySector"/>; returns them all.</summary>
        public static double BySector(double[] byTrade, double[] bySector)
        {
            Array.Clear(bySector, 0, bySector.Length);
            double all = 0.0;
            for (int t = 0; t < byTrade.Length && t < OfTrade.Length; t++)
            {
                double v = Math.Max(0.0, byTrade[t]);
                bySector[(int)OfTrade[t]] += v;
                all += v;
            }
            return all;
        }

        /// <summary>
        /// The sector with the most takings among those in <paramref name="among"/> (sector bits; -1 every one), a tie to the
        /// lower; its trade with the most in <paramref name="trade"/>. -1 for none.
        /// </summary>
        public static int Leading(double[] byTrade, out int trade, int among = -1)
        {
            trade = -1;
            int best = -1;
            double most = 0.0;
            for (int sec = 0; sec < Sectors; sec++)
            {
                if ((among & (1 << sec)) == 0)
                {
                    continue;
                }
                double sum = 0.0;
                for (int t = 0; t < byTrade.Length && t < OfTrade.Length; t++)
                {
                    sum += (int)OfTrade[t] == sec ? Math.Max(0.0, byTrade[t]) : 0.0;
                }
                if (sum > most)
                {
                    most = sum;
                    best = sec;
                }
            }
            trade = best >= 0 ? TopTrade(byTrade, best) : -1;
            return best;
        }

        /// <summary>The sector with the most of the takings by trade, and its trade with the most. -1 for none.</summary>
        public static int Leading(long[] byTrade, out int trade)
        {
            var copy = new double[byTrade.Length];
            for (int t = 0; t < copy.Length; t++)
            {
                copy[t] = byTrade[t];
            }
            return Leading(copy, out trade);
        }

        private static int TopTrade(double[] byTrade, int sector)
        {
            int top = -1;
            double most = 0.0;
            for (int t = 0; t < byTrade.Length && t < OfTrade.Length; t++)
            {
                if ((int)OfTrade[t] == sector && byTrade[t] > most)
                {
                    most = byTrade[t];
                    top = t;
                }
            }
            return top;
        }

        /// <summary>A market morning: yesterday's takings by trade into the mix, which forgets over about a quarter of market days.</summary>
        public static void Take(OwnListing o, in MarketEnv env, int year)
        {
            if (o == null || !o.Mixed)
            {
                return;
            }
            double keep = 1.0 - Weight(year);
            for (int t = 0; t < o.Mix.Length; t++)
            {
                o.Mix[t] *= keep;
            }
            if (env.TradeOf == null || env.TradeSales == null)
            {
                return;
            }
            double add = Weight(year);
            for (int i = 0; i < env.Trades && i < env.TradeOf.Length && i < env.TradeSales.Length; i++)
            {
                int t = env.TradeOf[i];
                if (t >= 0 && t < o.Mix.Length && env.TradeSales[i] > 0)
                {
                    o.Mix[t] += add * env.TradeSales[i];
                    o.Had |= 1 << (int)OfTrade[t];
                }
            }
        }

        /// <summary>A day's weight in the mix: two over a quarter's market days and one, as a moving average of about a quarter.</summary>
        public static double Weight(int year)
        {
            double days = Math.Max(5.0, MarketCatalogue.TradingDaysPerYear(year) / 4.0);
            return 2.0 / (days + 1.0);
        }

        /// <summary>
        /// The mix begun from the books (takings by trade over so many days), for a company listed before its mix was followed;
        /// with <paramref name="again"/> a followed mix is begun again too. False, nothing changed, when there is nothing to go on.
        /// </summary>
        public static bool Seed(OwnListing o, long[] takings, int days, bool again)
        {
            if (o == null || takings == null || days <= 0 || (o.Mixed && !again))
            {
                return false;
            }
            bool any = false;
            for (int t = 0; t < takings.Length; t++)
            {
                any |= takings[t] > 0;
            }
            if (!any)
            {
                return false;
            }
            o.Had = 0;
            for (int t = 0; t < o.Mix.Length; t++)
            {
                long v = t < takings.Length ? Math.Max(0L, takings[t]) : 0L;
                o.Mix[t] = v / (double)days;
                o.Had |= v > 0 ? 1 << (int)OfTrade[t] : 0;
            }
            o.Mixed = true;
            o.Lead = -1;
            o.LeadCount = 0;
            return true;
        }

        /// <summary>
        /// A review: the sector the company moves to, or -1 to stay, and the trade it is then most in. With
        /// <paramref name="books"/> a quarter's books already stand behind the mix, so a lead needs no second review.
        /// </summary>
        public static int Review(OwnListing o, int current, bool books, out int trade)
        {
            trade = -1;
            if (o == null || !o.Mixed)
            {
                return -1;
            }
            var bySector = new double[Sectors];
            double all = BySector(o.Mix, bySector);
            int had = o.Had;
            o.Had = 0;
            // Only a sector it earned in since the last review can lead: what it used to earn is fading from the mix.
            int leader = Leading(o.Mix, out int top, had);
            bool inSector = current >= 0 && current < Sectors;
            if (all <= 0.0 || leader < 0 || leader == current)
            {
                o.Lead = -1;
                o.LeadCount = 0;
                return -1;
            }
            bool gone = !inSector || (had & (1 << current)) == 0;
            bool ahead = bySector[leader] - (inSector ? bySector[current] : 0.0) >= LeadShare * all;
            if (!gone && !ahead)
            {
                o.Lead = -1;
                o.LeadCount = 0;
                return -1;
            }
            o.LeadCount = o.Lead == leader ? o.LeadCount + 1 : 1;
            o.Lead = leader;
            if (!gone && !books && o.LeadCount < LeadReviews)
            {
                return -1;
            }
            o.Lead = -1;
            o.LeadCount = 0;
            trade = top;
            return leader;
        }

        /// <summary>The sector a quarter's books (takings by trade) put a company in, from the one it is in: as a review with them does.</summary>
        public static int FromBooks(long[] takings, int current)
        {
            var byTrade = new double[OfTrade.Length];
            int had = 0;
            for (int t = 0; t < byTrade.Length && t < takings.Length; t++)
            {
                byTrade[t] = Math.Max(0L, takings[t]);
                had |= takings[t] > 0 ? 1 << (int)OfTrade[t] : 0;
            }
            var bySector = new double[Sectors];
            double all = BySector(byTrade, bySector);
            int leader = Leading(byTrade, out int _, had);
            if (all <= 0.0 || leader < 0 || leader == current)
            {
                return current;
            }
            bool inSector = current >= 0 && current < Sectors;
            bool gone = !inSector || bySector[current] <= 0.0;
            bool ahead = bySector[leader] - (inSector ? bySector[current] : 0.0) >= LeadShare * all;
            return gone || ahead ? leader : current;
        }

        /// <summary>The market day of the next review: the first of the next quarter.</summary>
        public static int NextReview(int day, int year)
        {
            return MarketTrading.NextTradingDay(MarketCatalogue.QuarterStart(MarketCatalogue.QuarterOf(day, year) + 1, year) - 1);
        }

        /// <summary>
        /// The sectors with the most of the mix, the most first, up to as many as <paramref name="sectors"/> holds, with their
        /// shares in bp; returns how many. Nothing for a mix not followed.
        /// </summary>
        public static int Top(OwnListing o, int[] sectors, int[] bps)
        {
            if (o == null || !o.Mixed)
            {
                return 0;
            }
            var bySector = new double[Sectors];
            double all = BySector(o.Mix, bySector);
            int n = 0;
            while (all > 0.0 && n < sectors.Length && n < bps.Length)
            {
                int best = -1;
                for (int sec = 0; sec < Sectors; sec++)
                {
                    if (bySector[sec] > 0.0 && (best < 0 || bySector[sec] > bySector[best]))
                    {
                        best = sec;
                    }
                }
                if (best < 0)
                {
                    break;
                }
                sectors[n] = best;
                bps[n] = (int)Math.Round(bySector[best] / all * 10000.0);
                bySector[best] = 0.0;
                n++;
            }
            return n;
        }

        /// <summary>The mix in the save: the lead, its reviews, the sectors earned in since the last review, then each trade's.</summary>
        public static void Write(System.Text.StringBuilder t, OwnListing o)
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            if (!o.Mixed)
            {
                return;   // an empty part: a company whose mix is not followed
            }
            t.Append(o.Lead.ToString(inv)).Append(',').Append(o.LeadCount.ToString(inv)).Append(',').Append(o.Had.ToString(inv));
            for (int i = 0; i < o.Mix.Length; i++)
            {
                double v = o.Mix[i];
                if (v > 0.0 && !double.IsNaN(v) && !double.IsInfinity(v))
                {
                    t.Append(',').Append(i.ToString(inv)).Append(':').Append(v.ToString("R", inv));
                }
            }
        }

        /// <summary>The mix read back; an empty or damaged part leaves it not followed.</summary>
        public static void Read(string text, OwnListing o)
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            o.Mixed = false;
            Array.Clear(o.Mix, 0, o.Mix.Length);
            if (string.IsNullOrEmpty(text))
            {
                return;
            }
            string[] f = text.Split(',');
            if (f.Length < 3 || !int.TryParse(f[0], System.Globalization.NumberStyles.Integer, inv, out int lead) || !int.TryParse(f[1], System.Globalization.NumberStyles.Integer, inv, out int count)
                || !int.TryParse(f[2], System.Globalization.NumberStyles.Integer, inv, out int had))
            {
                return;
            }
            o.Lead = lead >= 0 && lead < Sectors ? lead : -1;
            o.LeadCount = o.Lead >= 0 ? Math.Max(0, Math.Min(LeadReviews, count)) : 0;
            o.Had = had & ((1 << Sectors) - 1);
            for (int i = 3; i < f.Length; i++)
            {
                int colon = f[i].IndexOf(':');
                if (colon > 0 && int.TryParse(f[i].Substring(0, colon), System.Globalization.NumberStyles.Integer, inv, out int trade) && trade >= 0 && trade < o.Mix.Length
                    && double.TryParse(f[i].Substring(colon + 1), System.Globalization.NumberStyles.Float, inv, out double v) && v > 0.0 && !double.IsInfinity(v))
                {
                    o.Mix[trade] = v;
                }
            }
            o.Mixed = true;
        }
    }
}
