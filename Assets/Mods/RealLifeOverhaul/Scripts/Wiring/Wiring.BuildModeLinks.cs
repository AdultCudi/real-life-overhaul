using System;
using System.Collections.Generic;
using RealLifeOverhaul.Construction;
using RealLifeOverhaul.Landmarks;
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
                BuildModeBridges.MarketLevel = district => DistrictBuildings.MarketLevel(district);
                BuildModeBridges.DistrictAt = (x, z) => DistrictSplit.OfX(x);   // the demand bars show the district the map's middle is over
                BuildModeBridges.GhostStand = GhostStand;
                BuildModeBridges.GhostGone = GhostGone;
                BuildModeBridges.CompleteNow = DistrictBuildings.CompleteNow;
                BuildModeBridges.CompleteAll = DistrictBuildings.CompleteAll;
                BuildModeBridges.Held = HeldInto;           // the ground no square covers, each piece named (Uptown's HeldGroundRules)
                BuildModeBridges.Blocks = BlocksInto;
                BuildModeBridges.LandmarkOf = LandmarksDirector.SiteIdOfLot;
                BuildModeBridges.CornerGhost = CornerLinks.Ghost;   // a corner building's model as the ghost, and the corner buildings' check (Wiring.CornerLinks)
                BuildModeBridges.CornerGhostGone = CornerLinks.GhostGone;
                BuildModeBridges.CornerCheck = CornerLinks.Check;
            }

            internal static void Disconnect()
            {
                BuildModeBridges.Demand = null;
                BuildModeBridges.MarketLevel = null;
                BuildModeBridges.DistrictAt = null;
                BuildModeBridges.GhostStand = null;
                BuildModeBridges.GhostGone = null;
                BuildModeBridges.CompleteNow = null;
                BuildModeBridges.CompleteAll = null;
                BuildModeBridges.Held = null;
                BuildModeBridges.Blocks = null;
                BuildModeBridges.LandmarkOf = null;
                BuildModeBridges.CornerGhost = null;
                BuildModeBridges.CornerGhostGone = null;
                BuildModeBridges.CornerCheck = null;
            }

            // Every piece of the band's ground that no lot covers, as build mode's held grounds: a place's carries whether its building stands.
            private static void HeldInto(List<HeldView> into)
            {
                into.Clear();
                try
                {
                    IReadOnlyList<HeldGround> all = HeldGroundRules.All;
                    for (int i = 0; i < all.Count; i++)
                    {
                        HeldGround h = all[i];
                        into.Add(new HeldView
                        {
                            Kind = (HeldWhat)(int)h.Kind, Block = h.Block, District = DistrictSplit.OfBlock(h.Block), Lot = h.Lot, Street = h.Street, Number = h.Number, Name = h.Name,
                            NameKey = h.NameKey, Stands = h.Kind != HeldGroundKind.Place || DistrictBuildings.Finished(h.Lot), X1 = h.X1, Z1 = h.Z1, X2 = h.X2, Z2 = h.Z2
                        });
                    }
                }
                catch (Exception e)
                {
                    Fault("BuildModeLinks.Held", e);
                    into.Clear();
                }
            }

            // The band's blocks (the ring's too) as rectangles.
            private static void BlocksInto(List<BlockView> into)
            {
                into.Clear();
                try
                {
                    for (int row = 1; row <= LayoutRules.Rows + 1; row++)
                    {
                        for (int column = 0; column <= LayoutRules.Columns + 1; column++)
                        {
                            bool band = row <= LayoutRules.Rows && column >= 1 && column <= LayoutRules.Columns;
                            if (!band && !LayoutRules.IsRing(row, column))
                            {
                                continue;
                            }
                            LayoutRules.Block(row, column, out float x0, out float z0, out float x1, out float z1);
                            into.Add(new BlockView { Block = row * 100 + column, X1 = x0, Z1 = z0, X2 = x1, Z2 = z1 });
                        }
                    }
                }
                catch (Exception e)
                {
                    Fault("BuildModeLinks.Blocks", e);
                    into.Clear();
                }
            }

            private static int Demand(int use, int district)
            {
                try
                {
                    return DistrictBuildings.Lives ? DistrictBuildings.Demand(use, district) : -1;
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
