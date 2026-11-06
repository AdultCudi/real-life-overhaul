using System;
using System.Collections.Generic;
using RealLifeOverhaul.Construction;
using RealLifeOverhaul.Corners;
using UnityEngine;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// The construction game and build mode (Construction's ConstructionBridges, BuildModeBridges) with the corner buildings
        /// (Scripts/Corners): the corner buildings that fit a corner site, turned to its two streets, with the ground the game's building
        /// beneath each takes; a corner building's name; build mode's ghost of one. (The district's own buildings ask the corner buildings
        /// for their look while the city loads: Corners' CornerLook.Register at game start.) The adapters alone: they are wired in and
        /// out with the construction game's bridges (Wiring.ConstructionLinks) and build mode's (Wiring.BuildModeLinks). Asked on a pick
        /// or a day, never per frame; a fault is said once and the bridge's default answered.
        /// </summary>
        public static class CornerLinks
        {
            internal static int Check()
            {
                try
                {
                    return CornerLook.CheckAll();
                }
                catch (Exception e)
                {
                    Fault("CornerLinks.Check", e);
                    return -2;
                }
            }

            // Every corner building that stands on a corner's ground, turned to its two streets, and the ground left for the game's building beneath it.
            internal static void Offers(CellCorner corner, List<CornerOffer> into)
            {
                into.Clear();
                try
                {
                    var g = new CornerGround { X = corner.X, Z = corner.Z, Facing = corner.Facing, Width = corner.Width, Depth = corner.Depth, Low = corner.Low, Edge = corner.Edge };
                    CornerPose p = CornerRules.Pose(g);
                    for (int i = 0; i < CornerRules.Models.Length; i++)
                    {
                        CornerModel m = CornerRules.Models[i];
                        if (!CornerRules.Fits(m, p))
                        {
                            continue;
                        }
                        CornerRules.CopyGround(m, g, p, out float x, out float z, out float width, out float depth);
                        into.Add(new CornerOffer
                        {
                            Look = m.Id, NameKey = m.NameKey, Uses = m.Uses, X = p.X, Z = p.Z, Yaw = p.Yaw, CopyX = x, CopyZ = z, CopyWidth = width, CopyDepth = depth, Height = m.Height
                        });
                    }
                }
                catch (Exception e)
                {
                    into.Clear();
                    Fault("CornerLinks.Offers", e);
                }
            }

            internal static string Name(string look)
            {
                try
                {
                    return CornerRules.Find(look, out CornerModel m) ? CornersText.T(m.NameKey) : null;
                }
                catch (Exception e)
                {
                    Fault("CornerLinks.Name", e);
                    return null;
                }
            }

            internal static Transform Ghost(string look, float x, float z, int yaw)
            {
                try
                {
                    return CornerLook.Ghost(look, x, z, yaw);
                }
                catch (Exception e)
                {
                    Fault("CornerLinks.Ghost", e);
                    return null;
                }
            }

            internal static void GhostGone(Transform root)
            {
                try
                {
                    CornerLook.GhostGone(root);
                }
                catch (Exception e)
                {
                    Fault("CornerLinks.GhostGone", e);
                }
            }
        }
    }
}
