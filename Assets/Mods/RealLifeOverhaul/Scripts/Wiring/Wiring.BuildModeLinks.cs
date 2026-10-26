using System;
using System.Collections.Generic;
using RealLifeOverhaul.Construction;
using RealLifeOverhaul.NewBuildings;
using RealLifeOverhaul.Uptown;
using UnityEngine;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// Build mode (Construction/BuildModeBridges) with the rest of the mod: the district's demand and market level (Uptown's
        /// LifeDirector.Market), the ghost of a design as the district would stand it (the copy engine's look, NewBuildingsRules.Place),
        /// and the whole-building check (Uptown's LifeDirector.Complete). Connected and disconnected with the construction game.
        /// Asked on a press or a pick, never per frame; a fault is said once and the bridge's default answered.
        /// </summary>
        public static class BuildModeLinks
        {
            private const string GhostName = "RealLife build mode ghost";

            internal static void Connect()
            {
                BuildModeBridges.Demand = Demand;
                BuildModeBridges.MarketLevel = () => DistrictBuildings.MarketLevel;
                BuildModeBridges.GhostStand = GhostStand;
                BuildModeBridges.GhostGone = GhostGone;
                BuildModeBridges.CompleteNow = DistrictBuildings.CompleteNow;
                BuildModeBridges.CompleteAll = DistrictBuildings.CompleteAll;
                BuildModeBridges.CornerGhost = CornerLinks.Ghost;   // a corner building's model as the ghost, and the corner buildings' check (Wiring.CornerLinks)
                BuildModeBridges.CornerGhostGone = CornerLinks.GhostGone;
                BuildModeBridges.CornerCheck = CornerLinks.Check;
            }

            internal static void Disconnect()
            {
                BuildModeBridges.Demand = null;
                BuildModeBridges.MarketLevel = null;
                BuildModeBridges.GhostStand = null;
                BuildModeBridges.GhostGone = null;
                BuildModeBridges.CompleteNow = null;
                BuildModeBridges.CompleteAll = null;
                BuildModeBridges.CornerGhost = null;
                BuildModeBridges.CornerGhostGone = null;
                BuildModeBridges.CornerCheck = null;
            }

            private static int Demand(int use)
            {
                try
                {
                    return DistrictBuildings.Lives ? DistrictBuildings.Demand(use) : -1;
                }
                catch (Exception e)
                {
                    Fault("BuildModeLinks.Demand", e);
                    return -1;
                }
            }

            // The design's look, its root on the district's ground at its door's height, placed as the district places a building on a lot.
            private static Transform GhostStand(string design, string size, float x, float z, float facing, float width, float depth)
            {
                try
                {
                    if (!CopyGame.Ready)
                    {
                        return null;
                    }
                    IList<DesignEntry> designs = CopyGame.Catalogue;
                    int found = -1;
                    for (int i = 0; i < designs.Count; i++)
                    {
                        if (designs[i].Name == design && (size == null || designs[i].Size == size))
                        {
                            found = i;
                            break;
                        }
                    }
                    if (found < 0)
                    {
                        return null;
                    }
                    DesignEntry d = designs[found];
                    NewBuildingsRules.Place(d, new MadeLot(x, z, facing, width, depth), out float rx, out float rz, out float yaw);
                    CopyLook look = BuildingCopies.StandLook(found, new Vector3(rx, UptownLots.GroundY - d.DoorY, rz), yaw, GhostName);
                    return BuildingCopies.RootOf(look);
                }
                catch (Exception e)
                {
                    Fault("BuildModeLinks.GhostStand", e);
                    return null;
                }
            }

            private static void GhostGone(Transform root)
            {
                try
                {
                    if (root != null)
                    {
                        BuildingCopies.RemoveLook(root.GetComponent<CopyLook>());
                    }
                }
                catch (Exception e)
                {
                    Fault("BuildModeLinks.GhostGone", e);
                }
            }
        }
    }
}
