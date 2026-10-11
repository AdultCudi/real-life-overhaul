using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Economy
{
    /// <summary>How one bank's terms differ from the others': points on its rates, a share of the usual limits, a share of the usual fee.</summary>
    public struct BankProfile
    {
        public int RatePoints;      // whole points a year on every rate of the mod's own credit at this bank
        public int LimitPercent;    // the usual limit of a card, an overdraft or a purchase on finance, in per cent
        public int FeePercent;      // the usual arrangement fee of a loan by phone, in per cent (its least is the same everywhere)
    }

    /// <summary>
    /// The rule that sets the city's banks apart, so that choosing a bank matters. Game-free: compiled as it is by
    /// RealLifeOverhaul.Build/EconomyCheck.
    ///
    /// WHICH BANK IS WHICH. The banks are the game's own, under the game's own names (BankTerms.Lenders). Branches
    /// with the same name are one bank. They are ranked by the most the bank lends on its own terms (its
    /// BankSettings.maxTotalLoanAmount before the rating touches it), the largest first, and by name among equals.
    /// The first is the bank the mod's own accounts have always been held with (BankTerms.House): its profile is
    /// the neutral one, so nothing that was offered before these apps changes.
    ///
    ///   rank 0   the largest     rates as they were, limits as they were, the fee as it was
    ///   rank 1   the second      a point dearer, limits a quarter higher, half the arrangement fee
    ///   rank 2   the third       a point cheaper, limits a fifth lower, three quarters of the fee
    ///   rank 3+  any other       as rank 0 (it has no app; the Finance app still offers its loans)
    ///
    /// WHAT IT MOVES. Only the mod's own terms, and only through the one desk that decides them today
    /// (CreditDesk.Offer, Business, BusinessQuote, PhoneLending.Quote): the overdraft, the card, car finance and the
    /// mortgage (rate and limit), the business loan (rate and most), and the fee of a loan by phone. The game's own
    /// loan rate and cap at its bank counter stay the game's (BankTerms writes them by the rating alone): the banks
    /// already differ there by their own settings. The bounds hold on every profile: under the lending threshold
    /// every limit is nothing, whatever the share, and no rate goes under the catalogue's floor.
    /// </summary>
    public static class BankRules
    {
        /// <summary>The most bank apps there are on the phone: one for each of the three largest banks.</summary>
        public const int AppsMost = 3;

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

        /// <summary>The arrangement fee of a loan by phone at a bank: its share, rounded up to a whole unit, never under the least.</summary>
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
            // (the small step back keeps a product that is whole but for float noise from being rounded up one more)
            return Math.Max(CreditCatalogue.PhoneFeeLeast, (int)Math.Ceiling(amount * (double)CreditCatalogue.PhoneFeeShare * percent / 100.0 - 1e-4));
        }

        /// <summary>The bank's points on a product's points.</summary>
        public static int Points(int productPoints, int rank)
        {
            return productPoints + Of(rank).RatePoints;
        }

        /// <summary>Whether any bank lends at this rating. The answer is the same in every app: under the threshold, none does.</summary>
        public static bool Lends(int score)
        {
            return score >= CreditCatalogue.LendingThreshold;
        }

        /// <summary>
        /// The banks' ranks, from each branch's name and its own cap: branches with one name are one bank, ranked by
        /// the largest cap among its branches, the largest first, and by name (ordinal) among equals. Writes each
        /// branch's rank into <paramref name="ranks"/> and returns how many banks there are. The lists are the
        /// caller's; <paramref name="order"/> is a list to work in, used again.
        /// </summary>
        public static int Rank(IList<string> names, IList<float> caps, int[] ranks, List<int> order)
        {
            order.Clear();
            for (int i = 0; i < names.Count; i++)
            {
                int first = FirstOf(names, i);
                if (first == i)
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
