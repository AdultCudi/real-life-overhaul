using System;
using System.Collections.Generic;
using UnityEngine;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// The corner buildings' tests (Options > Tests, construction; plans/build-mode.md section 12): one corner building stood on the
    /// nearest free corner of Uptown, the next of the seven at each press, handed to the district as the city's through the very call a
    /// development makes; and every corner building standing checked (its model shown and aligned, its own look hidden, its door on the
    /// walking mesh, its signs as its registration says, its whole-building line). Each writes a "Construction check (corner buildings:
    /// ...)" line and a notice; the building handed over stays the city's.
    /// </summary>
    public sealed partial class ConstructionDirector
    {
        private int _cornerTurn;   // the model the next press stands first: the seven in turn

        /// <summary>Test: the next corner building handed to the district on the nearest free corner site, standing at once out of view or at the next black screen.</summary>
        public static bool TestCornerStandNow()
        {
            ConstructionDirector d = ModeReady("corner buildings: stand one");
            return d != null && !GoingPublic.InSession && !GoingPublic.IsGuest && d.CornerStandTest();
        }

        private bool CornerStandTest()
        {
            const string test = "corner buildings: stand one";
            IReadOnlyList<LotView> lots = Lots;
            Vector3 near = ConstructionGame.Player;
            int best = -1;
            float far = float.MaxValue;
            for (int i = 0; i < lots.Count; i++)
            {
                LotView lot = lots[i];
                if (!lot.Open || !CellRules.Corner(lot, out _, out _) || !FreeSpace(lot) || _state.Development(lot.Id) != null)
                {
                    continue;
                }
                float d = (lot.X - near.x) * (lot.X - near.x) + (lot.Z - near.z) * (lot.Z - near.z);
                if (d < far)
                {
                    far = d;
                    best = i;
                }
            }
            if (best < 0)
            {
                return ModeLine(test, "no free lot of Uptown's plan stands at a block's corner", false, "reallife_corner_test_no_corner");
            }
            LotView site = lots[best];
            CellRules.Corner(site, out bool low, out float edge);
            var designs = new List<DesignChoice>(8);
            string tried = string.Empty;
            for (int k = 0; k < 7; k++)
            {
                for (int u = 0; u < (int)BuildUse.Count; u++)
                {
                    designs.Clear();
                    if (!CornerDesigns(site, low, edge, (BuildUse)u, designs))
                    {
                        return ModeLine(test, "the city's designs are still being looked through: press again in a moment", false, "reallife_bld_mode_test_fail");
                    }
                    for (int i = 0; i < designs.Count; i++)
                    {
                        DesignChoice d = designs[i];
                        if (CornerIndex(d.Look) != (_cornerTurn + k) % 7)
                        {
                            continue;
                        }
                        bool handed = FinishLot(site.Id, d.Name, d.Size, d.Version, BuildCatalogue.UseTypes[u], 0, false, 0.0, d.Look, out string why);
                        _cornerTurn = (_cornerTurn + k + 1) % 7;
                        return ModeLine(test, d.Look + " (" + (BuildUse)u + ", over the city's " + d.Name + " of size " + d.Size + ") on lot " + site.Id + " at " + LotWords(site.Id)
                                              + (handed ? ": handed to the district; it stands now out of view, else at the next black screen, its check line in Player.log"
                                                  : ": not handed over (" + why + ")"), handed, "reallife_bld_mode_test_fail");
                    }
                }
                tried += (k > 0 ? ", " : string.Empty) + CornerIdAt((_cornerTurn + k) % 7);
            }
            return ModeLine(test, "no corner building fits lot " + site.Id + " (" + site.Width + " by " + site.Depth + " m) with a design of the city's beneath it (tried " + tried + ")", false,
                "reallife_bld_mode_test_fail");
        }

        // The seven in build mode's order, as the corner buildings list them: their ids for the test's turn.
        private static readonly string[] CornerIds = { "corner_bodega", "corner_diner", "corner_bank", "corner_deco", "corner_glass", "corner_loft", "corner_prewar" };

        private static int CornerIndex(string look) => Array.IndexOf(CornerIds, look);

        private static string CornerIdAt(int i) => CornerIds[((i % CornerIds.Length) + CornerIds.Length) % CornerIds.Length];

        /// <summary>Test: every corner building standing checked now: its model, its own look hidden, its door, its signs and its whole-building line, one line each.</summary>
        public static bool TestCornerCheckNow()
        {
            ConstructionDirector d = ModeReady("corner buildings: check");
            if (d == null)
            {
                return false;
            }
            Func<int> check = BuildModeBridges.CornerCheck;
            int failed = check != null ? check() : -2;
            if (failed == -1)
            {
                return ModeLine("corner buildings: check", "no corner building stands at this load (stand one with the test above)", false, "reallife_corner_test_none");
            }
            string what = failed < 0 ? "the corner buildings cannot be asked at this load" : failed + " corner building(s) with an item failed (each named in its own line)";
            return ModeLine("corner buildings: check", what, failed == 0, "reallife_bld_mode_test_fail");
        }
    }
}
