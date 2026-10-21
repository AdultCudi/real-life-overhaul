using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// The raw materials exchange's prices. Game-free. Each market day's close moves a material's ln price against its own
    /// price at the price level as a Gaussian autoregression drawn from the day's secret (the tape's, so a replayed day sets
    /// the same prices and nobody knows a close before it is played): half its group's draw and half its own, pulled back
    /// to the mean with the group's half-life. Because every draw is Gaussian, the price expected at any later close and
    /// the spread around it are exact, so a future's fair price is the true expectation and an option's the true one.
    /// A close costs a few dozen operations a material; nothing here is per frame.
    /// </summary>
    public static class MaterialRules
    {
        private static readonly double GroupShare = Math.Sqrt(DerivCatalogue.GroupShareBp / 10000.0);
        private static readonly double OwnShare = Math.Sqrt(1.0 - DerivCatalogue.GroupShareBp / 10000.0);

        /// <summary>The long-run variance of a group's ln price: sigma^2 / (1 - phi^2).</summary>
        public static double LongVariance(MaterialGroup group)
        {
            double sigma = DerivCatalogue.Sigma(group), phi = DerivCatalogue.Phi(group);
            return sigma * sigma / (1.0 - phi * phi);
        }

        /// <summary>A close's pull, so the price's long-run mean is exactly its own price at the price level.</summary>
        public static double Drift(MaterialGroup group)
        {
            return -(1.0 - DerivCatalogue.Phi(group)) * LongVariance(group) * 0.5;
        }

        /// <summary>A material's draws are keyed by its name, the same on every machine (MarketRandom.Of).</summary>
        public static int KeyOf(string name)
        {
            return (int)(MarketRandom.Of(name ?? string.Empty) & 0x7FFFFFFFUL);
        }

        /// <summary>One close: the new ln price from its group's draw and its own (each of spread 1).</summary>
        public static double Step(double x, MaterialGroup group, double zGroup, double zOwn)
        {
            double next = DerivCatalogue.Phi(group) * x + Drift(group) + DerivCatalogue.Sigma(group) * (GroupShare * zGroup + OwnShare * zOwn);
            return Math.Max(-DerivCatalogue.XMost, Math.Min(DerivCatalogue.XMost, next));
        }

        /// <summary>What a close does to a material: its draws from the day's secret, its new price at the day's price level.</summary>
        public static void Close(RawMaterial m, ulong secret, int day, int level)
        {
            double zGroup = MarketRandom.Normal(secret, MarketRandom.MaterialGroupShock, (int)m.Group, 0);
            double zOwn = MarketRandom.Normal(secret, MarketRandom.MaterialShock, KeyOf(m.Name), 0);
            m.X = Step(m.X, m.Group, zGroup, zOwn);
            m.Level = Math.Max(1, level);
            m.Fix = Price(m.Base, m.Level, m.X);
            m.FixDay = day;
            m.Closes.Add(m.Fix);
            while (m.Closes.Count > DerivCatalogue.ClosesKept)
            {
                m.Closes.RemoveAt(0);
            }
        }

        /// <summary>A new material's first ln price: a draw from its long-run spread, the same on every machine for the market.</summary>
        public static double Start(ulong marketId, string name, MaterialGroup group)
        {
            double v = LongVariance(group);
            return -v * 0.5 + Math.Sqrt(v) * MarketRandom.Normal(marketId, MarketRandom.MaterialStart, KeyOf(name), 0);
        }

        /// <summary>Micros a unit: its own price at the price level (tenths of a point) and its ln move; one micro at least.</summary>
        public static long Price(long baseMicros, int level, double x)
        {
            double micros = baseMicros * (level / 1000.0) * Math.Exp(x);
            return micros >= long.MaxValue / 4 ? long.MaxValue / 4 : Math.Max(1L, (long)Math.Round(micros));
        }

        /// <summary>The mean and the variance of the ln price after so many closes from now (none: as it stands).</summary>
        public static void Ahead(double x, MaterialGroup group, int closes, out double mean, out double variance)
        {
            if (closes <= 0)
            {
                mean = x;
                variance = 0.0;
                return;
            }
            double phiN = Math.Pow(DerivCatalogue.Phi(group), closes), v = LongVariance(group);
            mean = phiN * x - v * 0.5 * (1.0 - phiN);
            variance = v * (1.0 - phiN * phiN);
        }

        /// <summary>
        /// Micros a unit: the price expected at a close so many closes ahead, exactly, with the price level grown by the
        /// year's inflation over the calendar days to it.
        /// </summary>
        public static long Forward(RawMaterial m, int closes, int level, int inflationBp, int days, int year)
        {
            Ahead(m.X, m.Group, closes, out double mean, out double variance);
            double grown = Level(level, inflationBp, days, year);
            double micros = m.Base * grown * Math.Exp(mean + variance * 0.5);
            return micros >= long.MaxValue / 4 ? long.MaxValue / 4 : Math.Max(1L, (long)Math.Round(micros));
        }

        /// <summary>The price level expected so many days on, as a factor of 1000 tenths: today's grown by the year's inflation.</summary>
        public static double Level(int level, int inflationBp, int days, int year)
        {
            double grow = 1.0 + inflationBp / 10000.0 * Math.Max(0, days) / Math.Max(8, year);
            return Math.Max(1, level) / 1000.0 * Math.Max(0.5, grow);
        }

        /// <summary>The spread of the ln price after so many closes: a share of the price.</summary>
        public static double Spread(MaterialGroup group, int closes)
        {
            Ahead(0.0, group, closes, out _, out double variance);
            return Math.Sqrt(variance);
        }

        /// <summary>A material's move over so many closes in bp: its price now against the one that many closes before; 0 with none.</summary>
        public static int MoveBp(RawMaterial m, int closes)
        {
            int n = m.Closes.Count;
            if (n < 2 || closes <= 0)
            {
                return 0;
            }
            long before = m.Closes[Math.Max(0, n - 1 - closes)], now = m.Closes[n - 1];
            return before > 0 ? (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, (now - before) * 10000L / before)) : 0;
        }

        /// <summary>
        /// The materials known to the market, in name order: new ones added at their first price, every Base set to what the
        /// game says now unless <paramref name="keepBases"/> (a Going Public guest: the host's line says them). How many were added.
        /// </summary>
        public static int Know(List<RawMaterial> materials, IList<string> names, IList<long> bases, ulong marketId, int level, int day, bool keepBases = false)
        {
            int added = 0;
            for (int i = 0; i < names.Count && i < bases.Count; i++)
            {
                string name = names[i];
                if (string.IsNullOrEmpty(name) || bases[i] <= 0)
                {
                    continue;
                }
                RawMaterial m = Find(materials, name);
                if (m != null)
                {
                    if (m.Base != bases[i] && !keepBases)
                    {
                        m.Base = bases[i];
                        m.Fix = Price(m.Base, m.Level, m.X);   // the game's own price moved: the same move on it
                    }
                    continue;
                }
                m = new RawMaterial { Name = name, Group = DerivCatalogue.GroupOf(name), Base = bases[i], Level = Math.Max(1, level) };
                m.X = Start(marketId, name, m.Group);
                m.Fix = Price(m.Base, m.Level, m.X);
                m.FixDay = 0;
                Insert(materials, m);
                added++;
            }
            return added;
        }

        public static RawMaterial Find(List<RawMaterial> materials, string name)
        {
            for (int i = 0; i < materials.Count; i++)
            {
                if (string.Equals(materials[i].Name, name, StringComparison.Ordinal))
                {
                    return materials[i];
                }
            }
            return null;
        }

        /// <summary>In ordinal name order, so every machine walks them alike.</summary>
        public static void Insert(List<RawMaterial> materials, RawMaterial m)
        {
            int at = materials.Count;
            while (at > 0 && string.CompareOrdinal(materials[at - 1].Name, m.Name) > 0)
            {
                at--;
            }
            materials.Insert(at, m);
        }
    }
}
