using System;
using System.Collections.Generic;
using System.Text;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// How a piece of the game's scene is taken to be copied, with no Unity or game types (checked offline, NavCheck's
    /// UptownParksCheck.cs): which names are the piece asked for, which of two is nearer its fixed place, and how the
    /// templates found and missed at a load are put into one line for Player.log. The scene's side is GamePieces. The
    /// search is deliberately blind to whether a piece is switched on: the game puts whole groups of its scene out (the far
    /// districts' tweak, the city map's hiders), and a copy of a piece that is off is switched on again when it is laid.
    /// COST: nothing held; a few string compares per piece of a scene root, once a load.
    /// </summary>
    public static class PieceRules
    {
        /// <summary>
        /// True when <paramref name="name"/> is the piece called <paramref name="want"/>, with the numbers the game's editor
        /// gives copies ("SleepingBench (11)") but not another kind whose name merely begins the same ("SM_Trash Can_LOD1",
        /// "SM_FountainWater", "Playground_Fence_ModularFinal" for the module).
        /// </summary>
        public static bool Matches(string name, string want)
        {
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(want) || !name.StartsWith(want, StringComparison.Ordinal))
            {
                return false;
            }
            return name.Length == want.Length || name[want.Length] == ' ';
        }

        /// <summary>
        /// True when a mesh called <paramref name="meshName"/> is the piece's own: Unity names the mesh of a static batch "Combined Mesh (root: ...)", and a copy
        /// of a piece in one draws the whole batch.
        /// </summary>
        public static bool OwnMesh(string meshName)
        {
            return !string.IsNullOrEmpty(meshName) && !meshName.StartsWith("Combined Mesh", StringComparison.Ordinal);
        }

        /// <summary>The squared flat distance from a piece to the place its template is looked for.</summary>
        public static float Flat(float x, float z, float wantX, float wantZ)
        {
            float dx = x - wantX, dz = z - wantZ;
            return dx * dx + dz * dz;
        }

        /// <summary>
        /// True when a piece at <paramref name="distance"/> (squared) is taken over the best so far: nearer, and within
        /// <paramref name="within"/> metres of the fixed place. Ties keep the first, so the same piece is taken on every
        /// machine that walks the scene in the same order.
        /// </summary>
        public static bool Takes(float distance, float best, float within)
        {
            return distance < best && distance <= within * within;
        }

        /// <summary>The squared distance a search starts from: nothing found, only a piece within the reach is taken.</summary>
        public static float Start(float within) => within * within + 1f;

        /// <summary>
        /// Picks from named pieces the one a template asks for (the offline form of the scene's search): the index of the
        /// nearest that <see cref="Matches"/> and lies within the reach of the fixed place, or -1. Whether a piece is on or
        /// off is not asked.
        /// </summary>
        public static int Pick(IReadOnlyList<string> names, IReadOnlyList<float> xs, IReadOnlyList<float> zs, string want, float wantX, float wantZ, float within)
        {
            int taken = -1;
            float best = Start(within);
            for (int i = 0; i < names.Count; i++)
            {
                if (!Matches(names[i], want))
                {
                    continue;
                }
                float d = Flat(xs[i], zs[i], wantX, wantZ);
                if (Takes(d, best, within))
                {
                    best = d;
                    taken = i;
                }
            }
            return taken;
        }

        /// <summary>
        /// The one line a load writes about its templates: "found: a, b; not found: c" (a side with none left out), so a
        /// piece the game moved or renamed shows in Player.log once.
        /// </summary>
        public static string Words(IReadOnlyList<string> names, IReadOnlyList<bool> found)
        {
            var yes = new StringBuilder(128);
            var no = new StringBuilder(128);
            for (int i = 0; i < names.Count; i++)
            {
                StringBuilder to = found[i] ? yes : no;
                if (to.Length > 0)
                {
                    to.Append(", ");
                }
                to.Append(names[i]);
            }
            return (yes.Length > 0 ? "found: " + yes : string.Empty) + (yes.Length > 0 && no.Length > 0 ? "; " : string.Empty) + (no.Length > 0 ? "not found: " + no : string.Empty);
        }
    }
}
