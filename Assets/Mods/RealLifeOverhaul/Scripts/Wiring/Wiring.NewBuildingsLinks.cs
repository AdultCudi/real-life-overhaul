using System.Collections.Generic;
using RealLifeOverhaul.Nav;
using RealLifeOverhaul.NewBuildings;
using UnityEngine;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// The minimap and the mod's own buildings (Nav/NavBridges, NewBuildings/MadeBuildings and BuildingCopies):
        ///
        ///   MoreBuildings        the box of every made building shown and of every copy standing, drawn on the minimap's
        ///                        base beside the game's own buildings
        ///   MoreBuildingsStamp   changes when one is made, hidden, shown or taken away: the base is drawn again
        ///   NotMeasured          a made building is left out when the city is measured, so the kept file is the game's alone
        ///
        /// Asked when the base is drawn and once a second by the minimap's slow look; a fault is said once
        /// (<see cref="Fault"/>) and the base shows the game's buildings alone.
        /// </summary>
        public static class NewBuildingsLinks
        {
            private static readonly List<MadeBuilding> Made = new List<MadeBuilding>(8);

            internal static void Connect()
            {
                NavBridges.MoreBuildings = MoreBuildings;
                NavBridges.MoreBuildingsStamp = Stamp;
                NavBridges.NotMeasured = NotMeasured;
            }

            internal static void Disconnect()
            {
                NavBridges.MoreBuildings = null;
                NavBridges.MoreBuildingsStamp = null;
                NavBridges.NotMeasured = null;
            }

            private static void MoreBuildings(List<MeshRenderer> into)
            {
                try
                {
                    Made.Clear();
                    MadeBuildings.All(Made);
                    for (int i = 0; i < Made.Count; i++)
                    {
                        CityBuildingController c = Made[i].Controller;
                        if (Made[i].Shown && c != null && c.lodVersion != null)
                        {
                            into.Add(c.lodVersion);
                        }
                    }
                    Made.Clear();
                    BuildingCopies.Boxes(into);
                }
                catch (System.Exception e)
                {
                    Fault("NewBuildingsLinks.MoreBuildings", e);
                }
            }

            private static int Stamp()
            {
                return MadeBuildings.Stamp;
            }

            private static bool NotMeasured(CityBuildingController building)
            {
                return MadeBuildings.Count > 0 && MadeBuildings.Of(building) != null;
            }
        }
    }
}
