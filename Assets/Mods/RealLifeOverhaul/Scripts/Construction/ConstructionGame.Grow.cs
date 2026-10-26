using Buildings;
using Entities;
using Helpers;
using UnityEngine;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// The construction game's reads and writes of the game for the city's growth (plans/growables.md): a Let it grow building's
    /// deed of the player's, its tenants' share of its floor (the game's own RealEstate.occupancy, which the game's nightly run
    /// pays the player for: "Rent revenue" in the game's books) and its rent a square metre at its tier. Never another player's.
    /// Nothing here keeps state. COST: nothing per frame; on a day's pass or a handover.
    /// </summary>
    internal static partial class ConstructionGame
    {
        /// <summary>The share of a building of the player's let to the city's tenants (0 to 100) and the most it may hold; false with no deed.</summary>
        internal static bool DeedLet(string street, int number, out int occupancy, out int most)
        {
            occupancy = most = 0;
            RealEstate deed = DeedOf(street, number);
            if (deed == null)
            {
                return false;
            }
            occupancy = deed.OccupancyPercentage;
            most = deed.MaxOccupancy;
            return true;
        }

        /// <summary>The developer's tenants for a building of the player's: so many points more of its floor let, never past the most it holds. The share now, or -1 with no deed.</summary>
        internal static int DeedFill(string street, int number, int points)
        {
            RealEstate deed = DeedOf(street, number);
            if (deed == null)
            {
                return -1;
            }
            deed.occupancy = Mathf.Min(deed.occupancy + Mathf.Max(0, points), deed.MaxOccupancy);
            return deed.OccupancyPercentage;
        }

        /// <summary>A building of the player's asking its tier's rent a square metre: a share of the market's, held where the game holds the player's own (0.8 to 1.2). False with no deed.</summary>
        internal static bool DeedRent(string street, int number, GrowTier tier, out float rent)
        {
            rent = 0f;
            RealEstate deed = DeedOf(street, number);
            Building b = deed != null ? deed.Building : null;
            if (b == null)
            {
                return false;
            }
            rent = GrowRules.TierRent(b.GetBuildingDailyMarketRentPerSqm(), tier);
            deed.pricePerSqm = rent;
            deed.pendingPricePerSqm = rent;
            deed.daysUntilUpdatingPricePerSqm = 0;
            return true;
        }

        /// <summary>The game's BizMan opened on a building's page (where the player starts a business of their own in it); false when it cannot open.</summary>
        internal static bool BizMan(string street, int number) => BuildModeGame.OpenBizMan(street, number);
    }
}
