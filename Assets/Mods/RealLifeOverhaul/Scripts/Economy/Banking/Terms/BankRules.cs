using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Economy
{
    /// <summary>How one bank's terms differ from the others': points on its rates, a share of the usual limits, a share of the usual fee.</summary>
    public struct BankProfile
    {
        public int RatePoints;      // whole points a year on every rate of the mod's own credit at this bank
        public int LimitPercent;    // of the usual limit of a card, an overdraft or a purchase on finance
        public int FeePercent;      // of the usual arrangement fee of a loan by phone (its least is the same everywhere)
    }

    /// <summary>
    /// What sets the city's banks apart: each bank's profile by its rank (<see cref="Rank"/>). Rank 0, the house bank that
    /// holds the mod's accounts, is neutral; a bank past the third lends as rank 0. A profile moves only the mod's own terms,
    /// never the game's loan at the counter. Game-free: compiled as it is by EconomyCheck.
    /// </summary>
    public static class BankRules
    {
        public const int AppsMost = 3;   // banks in the Banks app on the phone: the three largest

        private static readonly BankProfile[] Profiles =
        {
            new BankProfile { RatePoints = 0, LimitPercent = 100, FeePercent = 100 },
            new BankProfile { RatePoints = 1, LimitPercent = 125, FeePercent = 50 },
            new BankProfile { RatePoints = -1, LimitPercent = 80, FeePercent = 75 }
        };

        public static BankProfile Of(int rank)
        {
            return rank > 0 && rank < Profiles.Length ? Profiles[rank] : Profiles[0];
        }

        /// <summary>A revolving limit (overdraft, card) at a bank: the usual one times its share, to the step, never under the least. Nothing stays nothing.</summary>
        public static int Limit(int usual, int rank, int round, int least)
        {
            if (usual <= 0)
            {
                return 0;
            }
            int percent = Of(rank).LimitPercent;
            return percent == 100 ? usual : Math.Max(least, ProductCatalogue.Round(usual * (double)percent / 100.0, Math.Max(1, round)));
        }

        /// <summary>The most a bank finances or lends against a business: the usual most times its share, rounded down. Nothing stays nothing.</summary>
        public static int Most(int usual, int rank)
        {
            if (usual <= 0)
            {
                return 0;
            }
            int percent = Of(rank).LimitPercent;
            return percent == 100 ? usual : (int)Math.Min(int.MaxValue, Math.Floor(usual * (double)percent / 100.0));
        }

        /// <summary>The share of an amount a bank's arrangement fee takes, before its least.</summary>
        public static float FeeShare(int rank)
        {
            return CreditCatalogue.PhoneFeeShare * Of(rank).FeePercent / 100f;
        }

        /// <summary>The arrangement fee of a loan by phone at a bank: its share rounded up to a whole unit, never under the least.</summary>
        public static int Fee(float amount, int rank)
        {
            if (amount <= 0f)
            {
                return 0;
            }
            int percent = Of(rank).FeePercent;
            if (percent == 100)
            {
                return LendingTerms.PhoneFee(amount);
            }
            // the small step back keeps a product that is whole but for float noise from being rounded up one more
            return Math.Max(CreditCatalogue.PhoneFeeLeast, (int)Math.Ceiling(amount * (double)CreditCatalogue.PhoneFeeShare * percent / 100.0 - 1e-4));
        }

        public static int Points(int productPoints, int rank)
        {
            return productPoints + Of(rank).RatePoints;
        }

        /// <summary>Whether any bank lends at this rating: the same answer in every app.</summary>
        public static bool Lends(int score)
        {
            return score >= CreditCatalogue.LendingThreshold;
        }

        /// <summary>
        /// Each branch's rank into <paramref name="ranks"/>: one name is one bank, ranked by its branches' largest own cap, then
        /// by name. Returns how many banks there are; <paramref name="order"/> is the caller's list to work in.
        /// </summary>
        public static int Rank(IList<string> names, IList<float> caps, int[] ranks, List<int> order)
        {
            order.Clear();
            for (int i = 0; i < names.Count; i++)
            {
                if (FirstOf(names, i) == i)
                {
                    order.Add(i);
                }
            }
            order.Sort((a, b) =>
            {
                float ca = BankCap(names, caps, a), cb = BankCap(names, caps, b);
                return ca != cb ? cb.CompareTo(ca) : string.CompareOrdinal(names[a] ?? string.Empty, names[b] ?? string.Empty);
            });
            for (int i = 0; i < names.Count; i++)
            {
                ranks[i] = order.IndexOf(FirstOf(names, i));
            }
            return order.Count;
        }

        private static int FirstOf(IList<string> names, int i)
        {
            for (int j = 0; j < i; j++)
            {
                if (string.Equals(names[j], names[i], StringComparison.Ordinal))
                {
                    return j;
                }
            }
            return i;
        }

        private static float BankCap(IList<string> names, IList<float> caps, int first)
        {
            float most = 0f;
            for (int i = 0; i < names.Count; i++)
            {
                if (string.Equals(names[i], names[first], StringComparison.Ordinal))
                {
                    most = Math.Max(most, Math.Max(0f, caps[i]));
                }
            }
            return most;
        }
    }
}
