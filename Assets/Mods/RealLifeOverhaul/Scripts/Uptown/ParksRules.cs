using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>What a piece of a park is: the game's own tree, bush, bench, bin, street lamp, fountain or statue it is copied from.</summary>
    public enum ParkKind
    {
        Tree = 0,       // a free tree of the game's (Variant picks the kind; the big maple is a feature tree)
        Bush = 1,       // a broadleaf bush (Variant picks the large or the middle one)
        Bench = 2,      // the game's sittable bench (OutsideBenchController), its front to its yaw
        Bin = 3,        // the game's litter bin
        Lamp = 4,       // the game's street lamp
        Fountain = 5,   // Madison Square Park's fountain
        Statue = 6      // Madison Square Park's statue
    }

    /// <summary>One piece of a park: what it is, its middle on the ground, its turn (degrees, its front to +Z at 0) and its variant.</summary>
    public struct ParkPiece
    {
        public ParkKind Kind;
        public float X, Z, Yaw;
        public int Variant;
    }

    /// <summary>
    /// Uptown's parks, on the two squares of the plan that are neither built nor the campus (blocks 309 and 117; the owner,
    /// 25 Oct 2026, found a bare square, and the plan had kept these two as parks with no lots and never laid them). Each is
    /// a block of 70 by 120 m, grass to its edges (the game's block ground), crossed by a spine of path from avenue to
    /// avenue, a cross path from street to street and two shorter ones, with a plaza at its middle: a fountain in one, a
    /// statue in the other. Round them stand the game's own trees, bushes, benches, lamps and bins, all on the grass beside
    /// the paths, none on them, so every path is walked end to end. Everything is worked out from the block and fixed
    /// figures (a number mixed from the block and the piece's place stands in for chance), so a park is the same on every
    /// machine and in every version. No Unity or game types: checked offline (NavCheck, UptownParksCheck.cs). COST: lists
    /// filled once a load.
    /// </summary>
    public static class ParksRules
    {
        public const float SpineHalf = 1.5f;       // m: half the spine's width (3 m, a pavement's)
        public const float CrossHalf = 1.5f;       // m: half the cross path's width
        public const float SideAt = 30f;           // m north and south of the middle the shorter cross paths run
        public const float SideHalf = 1.2f;        // m: half their width
        public const float FountainPlaza = 11f;    // m: half the fountain's plaza (22 by 22 m round a fountain 9.8 m across)
        public const float StatuePlaza = 8.5f;     // m: half the statue's plaza (17 by 17 m round a statue 3.8 by 5.6 m)
        public const float Margin = 2.5f;          // m kept clear between a path or plaza and a tree, bush, lamp, bin or bench
        public const float EdgeClear = 3.5f;       // m kept clear of the block's edge by a tree: the streets' pavements and their own trees are beyond it
        public const float Tile = 3f;              // m of a path's surface to a repeat of the game's pavement texture
        public const float Overlap = 0.15f;        // m the walking boxes of two path pieces overlap
        public const float PavementReach = 0.5f;   // m a path's walking box reaches onto the street's pavement it opens on
        public const float GridStepX = 10f;        // m between two trees of the lawns' grid across the park, before the jitter
        public const float GridStepZ = 11f;        // m between two along it
        public const float Jitter = 2f;            // m a tree may stand off its grid place, each way
        public const float TreeApart = 5f;         // m least between two trees' middles
        public const float ZoneAlong = 40f;        // m: a pedestrians' zone along the spine
        public const float ZoneFrom = 2f;          // m from a plaza's edge the zone begins
        public const int ZonesPerPark = 2;         // one north of the plaza and one south
        public const int TreeKinds = 4;            // the lawns' trees: broadleaf large, birch, broadleaf middle, pine (the maple is a feature tree)
        public const int Maple = 4;                // the feature tree's variant
        public const int BushKinds = 2;

        private static readonly int[] Fountains = { 309 };   // the squares with a fountain; the other has the statue

        /// <summary>The plan's parks, as Row * 100 + Column: every square of the plan but the campus's.</summary>
        public static void Blocks(List<int> into)
        {
            into.Clear();
            IReadOnlyList<int> squares = LayoutRules.Squares;
            for (int i = 0; i < squares.Count; i++)
            {
                if (squares[i] != LayoutRules.CampusBlock)
                {
                    into.Add(squares[i]);
                }
            }
        }

        /// <summary>True for a block the plan lays as a park.</summary>
        public static bool IsPark(int block)
        {
            return block != LayoutRules.CampusBlock && Contains(LayoutRules.Squares, block);
        }

        private static bool Contains(IReadOnlyList<int> list, int value)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == value)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>True for a park with a fountain at its middle (the other has a statue).</summary>
        public static bool HasFountain(int block) => Array.IndexOf(Fountains, block) >= 0;

        /// <summary>A park's ground: the block's west, south, east and north edges (m).</summary>
        public static void Ground(int block, out float x0, out float z0, out float x1, out float z1)
        {
            LayoutRules.Block(block / 100, block % 100, out x0, out z0, out x1, out z1);
        }

        /// <summary>A park's middle on the ground, where its plaza stands.</summary>
        public static void Centre(int block, out float x, out float z)
        {
            Ground(block, out float x0, out float z0, out float x1, out float z1);
            x = (x0 + x1) * 0.5f;
            z = (z0 + z1) * 0.5f;
        }

        /// <summary>Half the plaza's side in a park.</summary>
        public static float PlazaHalf(int block) => HasFountain(block) ? FountainPlaza : StatuePlaza;

        /// <summary>True when the spot lies within <paramref name="clear"/> metres of a path or a plaza of the park (in the park's own ground).</summary>
        public static bool NearPath(int block, float x, float z, float clear)
        {
            Centre(block, out float cx, out float cz);
            float u = Math.Abs(x - cx), v = Math.Abs(z - cz), plaza = PlazaHalf(block);
            return u <= SpineHalf + clear || v <= CrossHalf + clear || (u <= plaza + clear && v <= plaza + clear) || Math.Abs(v - SideAt) <= SideHalf + clear;
        }

        // ---- the paths

        /// <summary>
        /// A park's paving as disjoint rectangles (into the list, cleared; their tops at <see cref="LayoutPlans.GroundTop"/>):
        /// the spine from edge to edge, the cross path, the two shorter ones and the plaza, cut into bands across the park so
        /// no two overlap.
        /// </summary>
        public static void Paths(int block, List<PlanBox> into)
        {
            into.Clear();
            Ground(block, out float x0, out float z0, out float x1, out float z1);
            Centre(block, out float cx, out float cz);
            float hz = (z1 - z0) * 0.5f, hx = (x1 - x0) * 0.5f, plaza = PlazaHalf(block);
            float[] marks =
            {
                -hz, -(SideAt + SideHalf), -(SideAt - SideHalf), -plaza, -CrossHalf, CrossHalf, plaza, SideAt - SideHalf, SideAt + SideHalf, hz
            };
            for (int i = 0; i + 1 < marks.Length; i++)
            {
                float a = marks[i], b = marks[i + 1], mid = (a + b) * 0.5f, v = Math.Abs(mid);
                float half = SpineHalf;
                if (v <= CrossHalf || Math.Abs(v - SideAt) <= SideHalf)
                {
                    half = hx;
                }
                else if (v <= plaza)
                {
                    half = plaza;
                }
                into.Add(new PlanBox(cx - half, cz + a, cx + half, cz + b, LayoutPlans.GroundTop));
            }
        }

        /// <summary>
        /// The paths as ground for the pedestrians: each rectangle a little larger than its own where it meets another, and
        /// where it ends at the block's edge reaching onto the pavement beside it, so the walking mesh joins the street's.
        /// </summary>
        public static void Walks(int block, List<PlanBox> into)
        {
            Ground(block, out float x0, out float z0, out float x1, out float z1);
            var paths = new List<PlanBox>(10);
            Paths(block, paths);
            into.Clear();
            for (int i = 0; i < paths.Count; i++)
            {
                PlanBox p = paths[i];
                float a = p.X - p.HalfX - Overlap, b = p.Z - p.HalfZ - Overlap, c = p.X + p.HalfX + Overlap, d = p.Z + p.HalfZ + Overlap;
                a = p.X - p.HalfX <= x0 + 0.01f ? x0 - PavementReach : a;
                c = p.X + p.HalfX >= x1 - 0.01f ? x1 + PavementReach : c;
                b = p.Z - p.HalfZ <= z0 + 0.01f ? z0 - PavementReach : b;
                d = p.Z + p.HalfZ >= z1 - 0.01f ? z1 + PavementReach : d;
                into.Add(new PlanBox(a, b, c, d, LayoutPlans.GroundTop));
            }
        }

        /// <summary>The pedestrians' zones of a park: along the spine north and south of the plaza (the game's people come to them and stand there).</summary>
        public static void Zones(int block, List<PlanBox> into)
        {
            Centre(block, out float cx, out float cz);
            float from = PlazaHalf(block) + ZoneFrom;
            into.Add(new PlanBox(cx - SpineHalf, cz + from, cx + SpineHalf, cz + from + ZoneAlong, LayoutPlans.GroundTop));
            into.Add(new PlanBox(cx - SpineHalf, cz - from - ZoneAlong, cx + SpineHalf, cz - from, LayoutPlans.GroundTop));
        }

        // ---- the pieces

        /// <summary>
        /// Everything stood in a park (into the list, cleared): the fountain or the statue at the middle; benches round the
        /// plaza facing it and beside the paths facing them; lamps at the plaza's corners and along the spine; bins at the
        /// plaza's entrances; bushes at the plaza's corners and under some of the trees; and the trees of the lawns on a grid
        /// of <see cref="GridStepX"/> by <see cref="GridStepZ"/> m with a jitter, a third of the places left open, none within
        /// <see cref="Margin"/> of a path.
        /// </summary>
        public static void Pieces(int block, List<ParkPiece> into)
        {
            into.Clear();
            Ground(block, out float x0, out float z0, out float x1, out float z1);
            Centre(block, out float cx, out float cz);
            bool fountain = HasFountain(block);
            float plaza = PlazaHalf(block), hx = (x1 - x0) * 0.5f, hz = (z1 - z0) * 0.5f;
            Add(into, fountain ? ParkKind.Fountain : ParkKind.Statue, cx, cz, 0f, 0);
            // Benches round the plaza's four diagonals, facing the middle.
            float diagonal = fountain ? 7.6f : 5.6f;
            for (int s = 0; s < 4; s++)
            {
                float u = (s & 1) == 0 ? diagonal : -diagonal, v = (s & 2) == 0 ? diagonal : -diagonal;
                Add(into, ParkKind.Bench, cx + u, cz + v, Face(u, v, 0f, 0f), 0);
            }
            // Lamps at the plaza's corners and along the spine, beside it.
            for (int s = 0; s < 4; s++)
            {
                float u = (s & 1) == 0 ? plaza - 0.8f : -(plaza - 0.8f), v = (s & 2) == 0 ? plaza - 0.8f : -(plaza - 0.8f);
                Add(into, ParkKind.Lamp, cx + u, cz + v, 0f, 0);
            }
            float[] spineLamps = { 22f, -22f, 45f, -45f };
            for (int i = 0; i < spineLamps.Length; i++)
            {
                Add(into, ParkKind.Lamp, cx + (i % 2 == 0 ? SpineHalf + 0.9f : -(SpineHalf + 0.9f)), cz + spineLamps[i], 0f, 0);
            }
            // Bins at the plaza's entrances on the spine and a seat for each shorter path's crossing.
            Add(into, ParkKind.Bin, cx + SpineHalf + 0.9f, cz + plaza + 1.2f, 0f, 0);
            Add(into, ParkKind.Bin, cx - SpineHalf - 0.9f, cz - plaza - 1.2f, 0f, 0);
            Add(into, ParkKind.Bin, cx - SpineHalf - 0.9f, cz + SideAt + SideHalf + 1.0f, 0f, 0);
            Add(into, ParkKind.Bin, cx + SpineHalf + 0.9f, cz - SideAt - SideHalf - 1.0f, 0f, 0);
            if (fountain)
            {
                // A pair of benches on each shorter path's crossing, one each side of the spine, facing the path.
                for (int s = 0; s < 4; s++)
                {
                    float side = (s & 1) == 0 ? 1f : -1f, along = (s & 2) == 0 ? SideAt : -SideAt;
                    float v = along + side * (SideHalf + 0.9f);
                    Add(into, ParkKind.Bench, cx + side * 6f, cz + v, side > 0f ? 180f : 0f, 0);
                }
            }
            else
            {
                // Benches along the spine facing it, and the maples that stand where the fountain's park has its pairs.
                Add(into, ParkKind.Bench, cx + SpineHalf + 1.1f, cz + 38f, 270f, 0);
                Add(into, ParkKind.Bench, cx - SpineHalf - 1.1f, cz - 38f, 90f, 0);
                Add(into, ParkKind.Bench, cx + SpineHalf + 1.1f, cz - 8f - plaza, 270f, 0);
                Add(into, ParkKind.Bench, cx - SpineHalf - 1.1f, cz + 8f + plaza, 90f, 0);
                for (int s = 0; s < 4; s++)
                {
                    float u = (s & 1) == 0 ? 9f : -9f, v = (s & 2) == 0 ? 37f : -37f;
                    Add(into, ParkKind.Tree, cx + u, cz + v, 0f, Maple);
                }
            }
            // Bushes at the plaza's corners, two to each.
            for (int s = 0; s < 4; s++)
            {
                float su = (s & 1) == 0 ? 1f : -1f, sv = (s & 2) == 0 ? 1f : -1f;
                Add(into, ParkKind.Bush, cx + su * (plaza + 1.8f), cz + sv * (plaza - 3.2f), 0f, s % BushKinds);
                Add(into, ParkKind.Bush, cx + su * (plaza - 3.2f), cz + sv * (plaza + 1.8f), 0f, (s + 1) % BushKinds);
            }
            // The lawns' trees on a grid, off the paths, the plaza and the pieces already stood, a third of the places left open.
            int columns = (int)Math.Floor((hx - EdgeClear + Jitter) / GridStepX), rows = (int)Math.Floor((hz - EdgeClear + Jitter) / GridStepZ);
            for (int j = -rows; j <= rows; j++)
            {
                for (int i = -columns; i <= columns; i++)
                {
                    int h = Mix(block, i * 31 + j);
                    if (h % 3 == 0)
                    {
                        continue;
                    }
                    float u = i * GridStepX + (((h >> 4) & 255) / 255f * 2f - 1f) * Jitter;
                    float v = j * GridStepZ + (((h >> 12) & 255) / 255f * 2f - 1f) * Jitter;
                    if (Math.Abs(u) > hx - EdgeClear || Math.Abs(v) > hz - EdgeClear || NearPath(block, cx + u, cz + v, Margin) || Crowded(into, cx + u, cz + v, false))
                    {
                        continue;
                    }
                    Add(into, ParkKind.Tree, cx + u, cz + v, (h >> 20) % 360, (h >> 2) % TreeKinds);
                    if ((h >> 3) % 3 == 0)
                    {
                        float bu = cx + u + 1.9f, bv = cz + v + 1.4f;
                        if (!NearPath(block, bu, bv, Margin * 0.5f) && !Crowded(into, bu, bv, true))
                        {
                            Add(into, ParkKind.Bush, bu, bv, (h >> 16) % 360, (h >> 5) % BushKinds);
                        }
                    }
                }
            }
        }

        // True when a place is within the least distance of a piece already stood: a tree keeps its distance from trees, a bush
        // may stand close to the tree it grows under.
        private static bool Crowded(List<ParkPiece> pieces, float x, float z, bool bush)
        {
            for (int i = 0; i < pieces.Count; i++)
            {
                ParkPiece p = pieces[i];
                float dx = p.X - x, dz = p.Z - z;
                float least = p.Kind == ParkKind.Tree ? (bush ? 1.5f : TreeApart) : p.Kind == ParkKind.Bush ? 1.2f : p.Kind == ParkKind.Fountain || p.Kind == ParkKind.Statue ? 8f : bush ? 1.5f : 3f;
                if (dx * dx + dz * dz < least * least)
                {
                    return true;
                }
            }
            return false;
        }

        private static void Add(List<ParkPiece> into, ParkKind kind, float x, float z, float yaw, int variant)
        {
            into.Add(new ParkPiece { Kind = kind, X = (float)Math.Round(x, 2), Z = (float)Math.Round(z, 2), Yaw = (float)Math.Round(yaw, 1), Variant = variant });
        }

        // The turn that faces a piece at (u, v) towards (tu, tv), in degrees: its front is +Z at 0, +X at 90.
        private static float Face(float u, float v, float tu, float tv)
        {
            double yaw = Math.Atan2(tu - u, tv - v) * 180.0 / Math.PI;
            return (float)((yaw % 360.0 + 360.0) % 360.0);
        }

        // A number mixed from a park and a place of its grid: the same everywhere, never chance.
        private static int Mix(int block, int place)
        {
            unchecked
            {
                int h = block * 73856093 ^ place * 19349663 ^ 0x5bd1e995;
                h ^= h >> 13;
                h *= 0x5bd1e995;
                h ^= h >> 15;
                return h & 0x7fffffff;
            }
        }
    }
}
