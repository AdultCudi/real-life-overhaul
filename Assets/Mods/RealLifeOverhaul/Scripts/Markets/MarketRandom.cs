using System;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// The market's numbers by chance: every draw is a hash of a secret and what it is for, so the same secret gives the same
    /// market whatever order things are asked in. The secrets come from the tape, a fresh one each market day.
    /// </summary>
    public static class MarketRandom
    {
        // Purposes: what a draw is for. Never renumber; 16 and 27 are unused.
        public const int MarketShock = 1, SectorShock = 2, OwnShock = 3, OwnTail = 4, MarketTail = 5, SectorTail = 6, BarHigh = 7, BarLow = 8, Volume = 9,
            Revenue = 10, Margin = 11, Growth = 12, Crash = 13, CrashSize = 14, Mania = 15, Takeover = 17, TakeoverTerms = 18, Failure = 19,
            Listing = 20, ListingPick = 21, ResultsShock = 22, OpenShock = 23, OpenTail = 24, Start = 25, Recovery = 26, Bidder = 28,
            Mood = 29, OpenMood = 30, Crisis = 31, CrisisTerms = 32,
            Meeting = 33, Activist = 34, OwnBid = 35, Book = 36;   // a player's company: its meetings, investors, a bid, its book

        // The mood's draws are keyed by step and 0 for the market, MoodSector + sector, or MoodOwn + company id.
        public const int MoodSector = 1000, MoodOwn = 2000;

        /// <summary>A 32-bit integer mixer (lowbias32).</summary>
        public static uint Mix32(uint x)
        {
            unchecked
            {
                x ^= x >> 16;
                x *= 0x7FEB352Du;
                x ^= x >> 15;
                x *= 0x846CA68Bu;
                x ^= x >> 16;
                return x;
            }
        }

        /// <summary>The SplitMix64 finaliser: every bit of the input moves every bit of the output.</summary>
        public static ulong Mix64(ulong z)
        {
            unchecked
            {
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }

        public static ulong Key(ulong secret, int purpose, int a, int b = 0)
        {
            unchecked
            {
                ulong h = Mix64(secret ^ ((ulong)(uint)purpose * 0x9E3779B97F4A7C15UL));
                h = Mix64(h ^ ((ulong)(uint)a * 0xD1B54A32D192ED03UL));
                return Mix64(h ^ ((ulong)(uint)b * 0x8CB92BA72F3D8DD7UL + 0x632BE59BD9B4E019UL));
            }
        }

        /// <summary>A number in (0, 1): never exactly nothing or one, so a logarithm of it is always finite.</summary>
        public static double Unit(ulong key)
        {
            return ((key >> 11) + 0.5) / 9007199254740992.0;
        }

        public static double Unit(ulong secret, int purpose, int a, int b = 0)
        {
            return Unit(Key(secret, purpose, a, b));
        }

        /// <summary>From 0 up to, not including, the count.</summary>
        public static int Below(ulong secret, int purpose, int a, int b, int count)
        {
            return count <= 1 ? 0 : (int)(Key(secret, purpose, a, b) % (ulong)count);
        }

        /// <summary>A bell-shaped number of mean 0 and spread 1 (the inverse of the normal distribution, to nine places).</summary>
        public static double Normal(ulong secret, int purpose, int a, int b = 0)
        {
            return InverseNormal(Unit(Key(secret, purpose, a, b)));
        }

        /// <summary>A bell-shaped number with fat tails, spread 1: mostly a narrow bell, now and then a much wider one, as daily moves are.</summary>
        public static double Fat(ulong secret, int purpose, int tailPurpose, int a, int b = 0)
        {
            double z = Normal(secret, purpose, a, b);
            bool tail = Unit(secret, tailPurpose, a, b) < MarketCatalogue.TailShare;
            return z * (tail ? MarketCatalogue.TailScale : MarketCatalogue.BodyScale) / FatSpread;
        }

        public static readonly double FatSpread = Math.Sqrt((1.0 - MarketCatalogue.TailShare) * MarketCatalogue.BodyScale * MarketCatalogue.BodyScale
                                                            + MarketCatalogue.TailShare * MarketCatalogue.TailScale * MarketCatalogue.TailScale);

        private static readonly double[] A = { -3.969683028665376e+01, 2.209460984245205e+02, -2.759285104469687e+02, 1.383577518672690e+02, -3.066479806614716e+01, 2.506628277459239e+00 };
        private static readonly double[] B = { -5.447609879822406e+01, 1.615858368580409e+02, -1.556989798598866e+02, 6.680131188771972e+01, -1.328068155288572e+01 };
        private static readonly double[] C = { -7.784894002430293e-03, -3.223964580411365e-01, -2.400758277161838e+00, -2.549732539343734e+00, 4.374664141464968e+00, 2.938163982698783e+00 };
        private static readonly double[] D = { 7.784695709041462e-03, 3.224671290700398e-01, 2.445134137142996e+00, 3.754408661907416e+00 };

        /// <summary>The point of the normal distribution below which a share p lies (Acklam's approximation).</summary>
        public static double InverseNormal(double p)
        {
            if (p <= 0.0)
            {
                return -8.0;
            }
            if (p >= 1.0)
            {
                return 8.0;
            }
            const double low = 0.02425, high = 1.0 - low;
            if (p < low)
            {
                double q = Math.Sqrt(-2.0 * Math.Log(p));
                return (((((C[0] * q + C[1]) * q + C[2]) * q + C[3]) * q + C[4]) * q + C[5]) / ((((D[0] * q + D[1]) * q + D[2]) * q + D[3]) * q + 1.0);
            }
            if (p <= high)
            {
                double q = p - 0.5, r = q * q;
                return (((((A[0] * r + A[1]) * r + A[2]) * r + A[3]) * r + A[4]) * r + A[5]) * q / (((((B[0] * r + B[1]) * r + B[2]) * r + B[3]) * r + B[4]) * r + 1.0);
            }
            double t = Math.Sqrt(-2.0 * Math.Log(1.0 - p));
            return -(((((C[0] * t + C[1]) * t + C[2]) * t + C[3]) * t + C[4]) * t + C[5]) / ((((D[0] * t + D[1]) * t + D[2]) * t + D[3]) * t + 1.0);
        }

        /// <summary>The 64-bit hash of a text that is the same on every machine (string.GetHashCode is not).</summary>
        public static ulong Of(string text)
        {
            unchecked
            {
                ulong h = 14695981039346656037UL;
                for (int i = 0; text != null && i < text.Length; i++)
                {
                    h = (h ^ text[i]) * 1099511628211UL;
                }
                return Mix64(h);
            }
        }
    }
}
