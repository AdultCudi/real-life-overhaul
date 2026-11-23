using System;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// The sector a player's company is listed in, from what it earns now: its takings by trade followed over about a quarter
    /// (<see cref="OwnListing.Mix"/>), and a review at each quarter's turn that moves it to the sector that leads them. A sector
    /// it no longer earns anything in is left at the next review; one that only leads must lead by <see cref="LeadShare"/> of the
    /// takings, so a company split near evenly between two sectors stays where it is. One
    /// sector, not a blend: the sector indices, the sector news, the options' spread and every page read one sector a company.
    /// Game-free and the same on every machine. Costs a pass over the trades once a market morning and at a review.
    /// </summary>
    public static class ListingSector
    {
        // The mix already averages over about a quarter, so the margin alone keeps a near even company still; asking the lead to hold at a second review as well put a company
        // whose largest takings had changed half a year behind the quarter's report it was meant to follow.
        public const int LeadReviews = 1;      // reviews in a row a sector must lead the one the company is in, before it moves...
        public const double LeadShare = 0.1;   // ...by at least this share of the takings
        public const int RuleVersion = 2;      // 2: a review reads the quarter's own takings, not the average that still holds the old mix; a company under an older rule is reviewed once at the load
        public const int QuarterDaysMin = 5;   // market mornings of its own takings a review needs to read them instead of the average
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
            o.QuarterDays++;
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
                    o.Quarter[t] += env.TradeSales[i];
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
            Array.Clear(o.Quarter, 0, o.Quarter.Length);
            o.QuarterDays = 0;
            return true;
        }

        /// <summary>
        /// A review: the sector the company moves to, or -1 to stay, and the trade it is then most in. With
        /// <paramref name="books"/> a quarter's books already stand behind the mix, so a lead needs no second review.
        /// </summary>
        public static int Review(OwnListing o, int current, bool books, out int trade, int day = -1)
        {
            trade = -1;
            if (o == null || !o.Mixed)
            {
                return -1;
            }
            var bySector = new double[Sectors];
            // The quarter's own takings when a few days stand behind them: the average still holds a tail of what the company earned before, so a company
            // that had been mostly in one sector for a whole quarter could still show a lead of a few points, short of a tenth, in its old sector.
            double[] read = o.QuarterDays >= QuarterDaysMin ? (double[])o.Quarter.Clone() : o.Mix;   // a copy: the quarter starts afresh below
            double all = BySector(read, bySector);
            int had = o.Had;
            o.Had = 0;
            Array.Clear(o.Quarter, 0, o.Quarter.Length);
            o.QuarterDays = 0;
            o.Rule = RuleVersion;
            Saw(o, bySector, all, day);
            // Only a sector it earned in since the last review can lead: what it used to earn is fading from the mix.
            int leader = Leading(read, out int top, had);
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

        // What the review saw, for the company's page: each sector's share of the takings, in bp.
        private static void Saw(OwnListing o, double[] bySector, double all, int day)
        {
            if (all <= 0.0)
            {
                return;
            }
            o.SeenDay = Math.Max(0, day);
            for (int sec = 0; sec < Sectors; sec++)
            {
                o.SeenBp[sec] = (int)Math.Round(bySector[sec] / all * 10000.0);
            }
        }

        /// <summary>The sectors the last review saw takings in, the most first, up to as many as <paramref name="sectors"/> holds; how many. Nothing before a review has seen any.</summary>
        public static int SeenTop(OwnListing o, int[] sectors, int[] bps)
        {
            if (o == null || o.SeenDay < 0)
            {
                return 0;
            }
            var left = (int[])o.SeenBp.Clone();
            int n = 0;
            while (n < sectors.Length && n < bps.Length)
            {
                int best = -1;
                for (int sec = 0; sec < Sectors; sec++)
                {
                    if (left[sec] > 0 && (best < 0 || left[sec] > left[best]))
                    {
                        best = sec;
                    }
                }
                if (best < 0)
                {
                    break;
                }
                sectors[n] = best;
                bps[n++] = left[best];
                left[best] = 0;
            }
            return n;
        }

        /// <summary>
        /// The sector that led the company's at the last review by less than a move needs, and its lead in bp. False when none did (the company was
        /// in the lead, or it moved).
        /// </summary>
        public static bool SeenShort(OwnListing o, int current, out int leader, out int gapBp)
        {
            leader = -1;
            gapBp = 0;
            if (o == null || o.SeenDay < 0 || current < 0 || current >= Sectors)
            {
                return false;
            }
            for (int sec = 0; sec < Sectors; sec++)
            {
                if (sec != current && o.SeenBp[sec] - o.SeenBp[current] > gapBp)
                {
                    leader = sec;
                    gapBp = o.SeenBp[sec] - o.SeenBp[current];
                }
            }
            return leader >= 0 && gapBp < (int)Math.Round(LeadShare * 10000.0);
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
            // After what an older version reads: its reader skips a token whose name is not a trade's number.
            t.Append(",R:").Append(o.Rule.ToString(inv)).Append(",n:").Append(o.QuarterDays.ToString(inv));
            if (o.SeenDay >= 0)
            {
                t.Append(",D:").Append(o.SeenDay.ToString(inv));
                for (int sec = 0; sec < o.SeenBp.Length; sec++)
                {
                    if (o.SeenBp[sec] > 0)
                    {
                        t.Append(",S").Append(sec.ToString(inv)).Append(':').Append(o.SeenBp[sec].ToString(inv));
                    }
                }
            }
            for (int i = 0; i < o.Quarter.Length; i++)
            {
                double v = o.Quarter[i];
                if (v > 0.0 && !double.IsNaN(v) && !double.IsInfinity(v))
                {
                    t.Append(",q").Append(i.ToString(inv)).Append(':').Append(v.ToString("R", inv));
                }
            }
        }

        // One of the tokens after the mix: the rule's version, the quarter's days and takings, the last review's day and shares.
        private static void ReadKept(string name, string value, OwnListing o)
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            var integer = System.Globalization.NumberStyles.Integer;
            if (name == "R" && int.TryParse(value, integer, inv, out int rule))
            {
                o.Rule = Math.Max(0, Math.Min(RuleVersion, rule));
            }
            else if (name == "n" && int.TryParse(value, integer, inv, out int days))
            {
                o.QuarterDays = Math.Max(0, Math.Min(1000, days));
            }
            else if (name == "D" && int.TryParse(value, integer, inv, out int day))
            {
                o.SeenDay = day >= 0 ? day : -1;
            }
            else if (name.Length > 1 && name[0] == 'S' && int.TryParse(name.Substring(1), integer, inv, out int sec) && sec >= 0 && sec < Sectors
                     && int.TryParse(value, integer, inv, out int bp))
            {
                o.SeenBp[sec] = Math.Max(0, Math.Min(10000, bp));
            }
            else if (name.Length > 1 && name[0] == 'q' && int.TryParse(name.Substring(1), integer, inv, out int trade) && trade >= 0 && trade < o.Quarter.Length
                     && double.TryParse(value, System.Globalization.NumberStyles.Float, inv, out double v) && v > 0.0 && !double.IsInfinity(v))
            {
                o.Quarter[trade] = v;
            }
        }

        /// <summary>The mix read back; an empty or damaged part leaves it not followed.</summary>
        public static void Read(string text, OwnListing o)
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            o.Mixed = false;
            Array.Clear(o.Mix, 0, o.Mix.Length);
            Array.Clear(o.Quarter, 0, o.Quarter.Length);
            Array.Clear(o.SeenBp, 0, o.SeenBp.Length);
            o.QuarterDays = o.Rule = 0;
            o.SeenDay = -1;
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
                else if (colon > 0)
                {
                    ReadKept(f[i].Substring(0, colon), f[i].Substring(colon + 1), o);
                }
            }
            o.Mixed = true;
        }
    }
}
