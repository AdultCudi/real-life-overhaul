using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// The district's looks at a load (the owner, 27 Oct 2026: "A lot of buildings have missing textures, there are gaps between
    /// buildings and buildings do not face the road properly"): once its buildings, fillers and edge stand, every end of a piece on
    /// the street's front that nothing stands against is looked at (LookCensus), the plain sides among them are given the game's own
    /// windows (SideWalls), and the census is written to Player.log. COST: nothing per frame; one pass over the pieces at a load.
    /// </summary>
    public sealed partial class LifeDirector
    {
        private static readonly List<LookPiece> LookPieces = new List<LookPiece>(1024);
        private static readonly List<Transform> LookRoots = new List<Transform>(1024);
        private static readonly List<LookEnd> LookEnds = new List<LookEnd>(1024);

        // The windows laid on the plain sides, a fault of it leaving the buildings as the game has them, said once.
        private static int Dress()
        {
            try
            {
                return DressSides();
            }
            catch (Exception e)
            {
                SideWalls.Clear();
                Logger?.Error(e);
                Logger?.Warn("Uptown: the windows on its plain side walls could not be laid at this load; its buildings stand as the game has them.");
                return 0;
            }
        }

        // The standing buildings and the fillers as pieces, the open ends among them, the windows laid on the plain ones: how many.
        private static int DressSides()
        {
            LookPieces.Clear();
            LookRoots.Clear();
            LifeGame.BuildingsInto(Standing, LookPieces, LookRoots);
            LifeGame.FillersInto(LookPieces, LookRoots);
            LookCensus.OpenEnds(LookPieces, LookEnds);
            int laid = SideWalls.Lay(LookPieces, LookRoots, LookEnds);
            Logger?.Info("Uptown: " + laid + " plain side walls given windows (" + SideWalls.Describe() + "; the game's materials: " + SideWalls.Found + ") on " + LookEnds.Count
                         + " open ends of " + LookPieces.Count + " buildings and fillers.");
            if (Meta.Dev.Verbose)
            {
                LookReport r = LookCensus.Count(LookPieces, Plan, id => StandingLots.Contains(id));
                string why = LookWhy(r, null, 0);
                Logger?.Info("Uptown check (look census, the load): " + r.Describe() + ": " + (why == null ? "PASS" : "FAIL (" + why + ")"));
            }
            return laid;
        }

        /// <summary>
        /// The look census of the pieces standing now against the plan: the report, and the faults a piece has in the game (a renderer with no
        /// material, one of an error shader, a building with nothing drawn, a plain open side with no windows on it). One pass, for a test.
        /// </summary>
        internal static LookReport LookNow(out string faults, out int checkedRenderers)
        {
            var pieces = new List<LookPiece>(Standing.Count + 400);
            var roots = new List<Transform>(Standing.Count + 400);
            LifeGame.BuildingsInto(Standing, pieces, roots);
            LifeGame.FillersInto(pieces, roots);
            LookReport report = LookCensus.Count(pieces, Plan, id => StandingLots.Contains(id));
            faults = LookFaults(pieces, roots, out checkedRenderers);
            return report;
        }

        // The game's side of the census: every piece's renderers each with a material of a shader that draws, something drawn at all, and the box
        // as the game has it; a plain side open to a street with a window overlay on it.
        private static string LookFaults(List<LookPiece> pieces, List<Transform> roots, out int checkedRenderers)
        {
            checkedRenderers = 0;
            var text = new System.Text.StringBuilder(256);
            int listed = 0, none = 0, error = 0, empty = 0;
            var renderers = new List<Renderer>(64);
            for (int i = 0; i < roots.Count; i++)
            {
                Transform root = roots[i];
                if (root == null || !root.gameObject.activeInHierarchy)
                {
                    continue;
                }
                renderers.Clear();
                root.GetComponentsInChildren(false, renderers);
                int drawn = 0;
                for (int r = 0; r < renderers.Count; r++)
                {
                    Renderer renderer = renderers[r];
                    if (renderer == null || !renderer.enabled || renderer.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly
                        || renderer is TrailRenderer || renderer is ParticleSystemRenderer)
                    {
                        continue;
                    }
                    checkedRenderers++;
                    drawn++;
                    Material[] materials = renderer.sharedMaterials;
                    for (int m = 0; materials != null && m < materials.Length; m++)
                    {
                        string why = materials[m] == null ? "no material" : materials[m].shader == null ? "no shader"
                            : materials[m].shader.name.IndexOf("InternalError", StringComparison.Ordinal) >= 0 || !materials[m].shader.isSupported
                            ? "a shader that does not draw (" + materials[m].shader.name + ")" : null;
                        if (why == null)
                        {
                            continue;
                        }
                        none += materials[m] == null ? 1 : 0;
                        error += materials[m] != null ? 1 : 0;
                        if (listed++ < 12)
                        {
                            text.Append(listed > 1 ? "; " : string.Empty).Append(pieces[i].Id).Append(' ').Append(renderer.name).Append(": ").Append(why);
                        }
                    }
                }
                if (drawn == 0)
                {
                    empty++;
                    if (listed++ < 12)
                    {
                        text.Append(listed > 1 ? "; " : string.Empty).Append(pieces[i].Id).Append(": nothing of it is drawn");
                    }
                }
            }
            return listed == 0 ? null : none + " renderers with no material, " + error + " with a shader that does not draw, " + empty + " pieces with nothing drawn (" + text + ")";
        }
    }
}
