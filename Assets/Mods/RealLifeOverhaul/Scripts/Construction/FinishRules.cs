using System;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// The fit-out finish of a building: basic, standard or premium, the tiers Let it grow builds to (<see cref="GrowTier"/>). A project's company picks one before
    /// the site is set out (the city's builders pick by what they can afford), and it changes the fit-out's cost, its days and the rent the finished building asks.
    /// The fit-out is a share of the build, so a premium finish is dearer by a third of that share and not of the whole. Game-free and deterministic.
    /// </summary>
    public static class FinishRules
    {
        public const int Tiers = (int)GrowTier.Count;
        public const int Basic = (int)GrowTier.Basic, Standard = (int)GrowTier.Standard, Premium = (int)GrowTier.Premium;

        /// <summary>Thousandths of a build's cost and working hours that its fit-out is.</summary>
        public const int FitShare = 350;

        /// <summary>The fit-out's cost against the standard finish's, by tier.</summary>
        public static readonly int[] CostPermille = GrowRules.TierCostPermille;

        /// <summary>The fit-out's working hours against the standard finish's, by tier.</summary>
        public static readonly int[] DaysPermille = { 880, 1000, 1250 };

        /// <summary>The rent a square metre against the market's, by tier (the game holds a landlord's rent within 0.8 to 1.2 of it).</summary>
        public static readonly int[] RentPermille = GrowRules.TierRentPermille;

        /// <summary>What a developer clears on the land it built on, in thousandths of the land's price, by tier.</summary>
        public static readonly int[] GainPermille = { 80, 120, 190 };

        /// <summary>Days after permission is granted that a company has to choose before the standard finish is taken for it.</summary>
        public const int WindowDays = 10;

        public static int Clamp(int tier) => BuildCatalogue.Clamp(tier, 0, Tiers - 1);

        /// <summary>The tier a project's saved number means: 0 is not chosen yet and reads as the standard finish.</summary>
        public static int TierOf(int saved) => saved <= 0 ? Standard : Clamp(saved - 1);

        /// <summary>A project's cost against the standard finish's, in thousandths, for the finish it has saved: the fit-out's share of the build, dearer or cheaper by the tier.</summary>
        public static int CostOf(int saved) => 1000 + FitShare * (CostPermille[TierOf(saved)] - 1000) / 1000;

        /// <summary>A project's estimate under another finish than the one it has: scaled by the two finishes' costs.</summary>
        public static long Estimate(long estimate, int fromSaved, int toTier) => Math.Max(0L, estimate) * CostOf(toTier + 1) / CostOf(fromSaved);

        /// <summary>The working hours a site is set out for at a tier, from its hours at the standard finish.</summary>
        public static int Hours(int hours, int tier)
        {
            long fitOut = Math.Max(1, hours) * (long)FitShare / 1000L;
            return (int)Math.Max(1L, Math.Max(1, hours) + fitOut * (DaysPermille[Clamp(tier)] - 1000) / 1000L);
        }

        /// <summary>The working days a tier adds to the standard finish's (negative: it saves them).</summary>
        public static int DaysMore(int hours, int tier) => TenderRules.StandardDays(Hours(hours, tier)) - TenderRules.StandardDays(Math.Max(1, hours));

        /// <summary>The rent of a month at a tier, from the standard finish's, kept to the share the game allows a landlord.</summary>
        public static long Rent(long month, int tier)
        {
            int share = BuildCatalogue.Clamp(RentPermille[Clamp(tier)], 800, 1200);
            return Math.Max(0L, month) * share / 1000L;
        }

        /// <summary>Whole months of a tier's rent that pay back its cost; 0 where there is no rent.</summary>
        public static int PaybackMonths(long cost, long month) => month > 0L ? (int)Math.Min(9999L, (Math.Max(0L, cost) + month - 1L) / month) : 0;

        /// <summary>
        /// The tier a builder picks for its own project by what it can afford: basic when the fit-out would take a fifth of its cash or more, premium when it would take
        /// under a twentieth and the use is wanted (demand 50 or more, or not known), else standard. The same rule picks for the company's builders and for a company
        /// that did not choose in its window.
        /// </summary>
        public static int ByMeans(long cash, long estimate, int demand)
        {
            long fitOut = Math.Max(0L, estimate) * FitShare / 1000L;
            if (cash <= 0L || fitOut * 5L >= cash)
            {
                return Basic;
            }
            return fitOut * 20L < cash && (demand < 0 || demand >= 50) ? Premium : Standard;
        }

        /// <summary>What a developer clears on a building it has put up at a tier: a share of the land's price.</summary>
        public static long Gain(long landPrice, int tier) => Math.Max(0L, landPrice) * GainPermille[Clamp(tier)] / 1000L;

        /// <summary>The last day to choose, from the day permission was first seen granted.</summary>
        public static int Deadline(int day) => day + WindowDays;
    }
}
