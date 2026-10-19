using System;
using System.Collections.Generic;
using Buildings;
using Helpers;
using UnityEngine;

namespace RealLifeOverhaul.Family
{
    /// <summary>
    /// The one place the difficult moments read the game: the money in hand and the game's own record of its last seven
    /// midnights, the player's own places, the home's address, and whether the player is inside a business of their
    /// own. Only the local player's own, never another player's (GoingPublic.Mine). Each read is a small method of its
    /// own, so a game update that changes a member changes one of them. Cost: field reads and one pass over the city's
    /// building registrations, once a game hour (HardTimesWatch).
    /// </summary>
    internal static class HardTimesGame
    {
        private const string Residential = "ba:buildingtype_residential";

        /// <summary>
        /// The money in hand now, and the highest of the last seven midnights and how many of the last three ended in
        /// the red.
        /// </summary>
        public static void Cash(ref HardSigns g)
        {
            GameInstance save = SaveGameManager.Current;
            if (save == null)
            {
                return;
            }
            g.Cash = save.Money;
            // GameManager.RecordMidnightBankBalance: seven at most, the oldest first.
            List<float> nights = save.midnightBankBalances;
            float high = 0f;
            int red = 0;
            for (int i = 0; nights != null && i < nights.Count; i++)
            {
                high = Mathf.Max(high, nights[i]);
                if (i >= nights.Count - 3 && nights[i] < 0f)
                {
                    red++;
                }
            }
            g.WeekHigh = high;
            g.RedMidnights = red;
        }

        /// <summary>Inside a building of the player's own that is not a home: a shop, an office, a warehouse.</summary>
        public static bool AtOwnBusiness()
        {
            if (!BuildingManager.IsInsideBuilding)
            {
                return false;
            }
            var manager = InstanceBehavior<BuildingManager>.Instance;
            return manager != null && manager.building != null && GoingPublic.Mine(manager.buildingRegistration)
                   && manager.building.BuildingType != Residential;
        }

        /// <summary>The player's own places, homes and businesses alike.</summary>
        public static int OwnPlaces()
        {
            GameInstance save = SaveGameManager.Current;
            List<BuildingRegistration> all = save != null ? save.BuildingRegistrations : null;
            int count = 0;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                BuildingRegistration r = all[i];
                if (r != null && r.RentedByPlayer && r.HasValidAddress && GoingPublic.Mine(r))
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>The family's home as a number of its address (HardTimesRules.Key); 0 with no home.</summary>
        public static int HomeKey()
        {
            try
            {
                BuildingRegistration home = FamilyPlaces.Home();
                Building b = home != null ? BuildingHelper.GetBuilding(home.Address) : null;
                return b != null ? HardTimesRules.Key(b.StreetName, b.StreetNumber) : 0;
            }
            catch (Exception)
            {
                return 0;   // a registration the game has no building for is nobody's home
            }
        }
    }
}
