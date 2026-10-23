using System.Collections.Generic;
using RealLifeOverhaul.NewBuildings;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// The player and Uptown's buildings, for its stand-down in play: whether the player is inside one, and the game's own
    /// way out of it (BuildingManager.ExitFromBuilding, behind its fade, to the building's door). Nothing here runs a frame
    /// but <see cref="Indoors"/>, read a frame at a time while the game takes the player out.
    /// </summary>
    internal static partial class LifeGame
    {
        /// <summary>True while the player is inside a building, or the game is taking them in or out of one.</summary>
        public static bool Indoors
        {
            get
            {
                BuildingManager manager = InstanceBehavior<BuildingManager>.IsInitialized ? InstanceBehavior<BuildingManager>.Instance : null;
                return BuildingManager.IsInsideBuilding || (manager != null && (manager.enteringBuilding || manager.exitingBuilding));
            }
        }

        /// <summary>True when the player is inside one of the district's buildings.</summary>
        public static bool PlayerInside(List<MadeBuilding> buildings)
        {
            BuildingManager manager = InstanceBehavior<BuildingManager>.IsInitialized ? InstanceBehavior<BuildingManager>.Instance : null;
            if (!BuildingManager.IsInsideBuilding || manager == null || manager.building == null)
            {
                return false;
            }
            for (int i = 0; i < buildings.Count; i++)
            {
                if (buildings[i] != null && buildings[i].Building == manager.building)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>The game takes the player out by the building's first door to the street, as its own door does.</summary>
        public static void WalkOut()
        {
            BuildingManager manager = InstanceBehavior<BuildingManager>.IsInitialized ? InstanceBehavior<BuildingManager>.Instance : null;
            manager?.ExitFromBuilding(0);
        }
    }
}
