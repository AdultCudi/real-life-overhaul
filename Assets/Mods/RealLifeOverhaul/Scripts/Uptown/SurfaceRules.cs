using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>A flat piece of road or ground seen from above: its rectangle on the world's axes and the height of its top (m).</summary>
    public struct SurfacePlate
    {
        public float X0;
        public float Z0;
        public float X1;
        public float Z1;
        public float Y;

        public SurfacePlate(float x0, float z0, float x1, float z1, float y)
        {
            X0 = x0;
            Z0 = z0;
            X1 = x1;
            Z1 = z1;
            Y = y;
        }
    }

    /// <summary>One piece of the game's or the district's ground: its name for the log and its flat plates (an overlap is read plate against plate).</summary>
    public sealed class SurfacePiece
    {
        public readonly string Name;
        public readonly List<SurfacePlate> Plates;
        public bool Game;   // one of the game's own, laid as it is and never moved: two of these are the game's layout, not ours to change
        public float X0 = float.MaxValue, Z0 = float.MaxValue, X1 = float.MinValue, Z1 = float.MinValue;   // every plate's rectangle together

        public SurfacePiece(string name, int plates)
        {
            Name = name;
            Plates = new List<SurfacePlate>(plates);
        }

        public void Add(float x0, float z0, float x1, float z1, float y)
        {
            Plates.Add(new SurfacePlate(x0, z0, x1, z1, y));
            X0 = Math.Min(X0, x0);
            Z0 = Math.Min(Z0, z0);
            X1 = Math.Max(X1, x1);
            Z1 = Math.Max(Z1, z1);
        }
    }

    /// <summary>Two pieces whose surfaces lie at the same height over the same ground: where (the middle of the largest overlap), how much (m2) and how near in height (m).</summary>
    public struct SurfaceSpot
    {
        public string A;
        public string B;
        public float X;
        public float Z;
        public float Area;
        public float Gap;
    }

    /// <summary>
    /// Where two pieces of Uptown's road and ground lie at the same height over the same place (game-free). Two surfaces
    /// within a millimetre or two of each other in height fight for the pixels where they overlap, and the picture flickers
    /// between them (a player's #general post, 26 Oct 2026: "the road keep flicking" on 7th Street): the backdrop's own
    /// block ground lies at 0.05518 m, the street and avenue modules' pavements at 0.0551 m and 0.055 m, and the grid the
    /// backdrop's grounds stand on is not the modules' (the grounds' edges reach 0.1 to 0.5 m over a pavement, or metres
    /// where they lie off the grid), so a ground's ring of slabs, laid as asphalt since 24 Oct, fought the pavement it lay
    /// over. The load now lays one ground to each block of the plan's grid and sets each kind of piece at a height of its
    /// own (<see cref="Lift"/>), so no two surfaces that overlap are within <see cref="Coplanar"/> of each other.
    /// Each kind's plates are the flat faces of the game's own module (Build 3682, read from its mesh). COST: a pass over
    /// the pieces' pairs at the load with Developer diagnostics on and when the test is pressed; nothing per frame.
    /// </summary>
    public static class SurfaceRules
    {
        public const float Coplanar = 0.0015f;      // m: two surfaces this near in height can fight for a pixel (the game's own street and crossing are 0.002 m apart)
        public const float MinArea = 0.0025f;       // m2: an overlap this small is a seam, not a patch
        public const float AvenueLift = 0.003f;     // m an avenue length is raised: its asphalt (-0.00226 m) and pavements (0.055 m)
                                                    // clear the crossing's (-0.002 m, 0.053 m) by 2.7 and 5 mm
        public const float GroundDrop = 0.006f;     // m a block's ground is lowered: its slabs (0.05518 m) lie 6 mm under a street's pavement,
                                                    // 5.8 mm under the game's avenue, 3.8 mm under a crossing's
        public const float ReachNear = 60f;         // m round a place the test lists the overlaps of

        // Each kind's flat faces from above, in its own frame (x across, z along; m): x0, z0, x1, z1, y. A street module's pivot is its middle, as an avenue's, a crossing's and a ground's.
        private static readonly float[] StreetPlates =
        {
            -5.132f, -50f, 5.15f, 50f, 0.00004f,                // the asphalt
            -8.65f, -50f, -4.984f, 50.01f, 0.05516f,            // the west pavement
            4.984f, -50f, 8.65f, 50.01f, 0.05516f,              // the east pavement
            -5.13f, -50f, -4.97f, 50.01f, 0.1f,                 // the kerbs' tops
            4.97f, -50f, 5.13f, 50.01f, 0.1f
        };

        private static readonly float[] AvenuePlates =
        {
            -25f, -10.072f, 25f, 10.092f, -0.00226f,            // the asphalt
            -25.033f, 9.934f, 25.027f, 13.63f, 0.055f,          // the north pavement
            -25.033f, -13.63f, 25.027f, -9.934f, 0.055f,        // the south pavement
            -25f, 9.916f, 25f, 10.076f, 0.09774f,               // the kerbs' tops
            -25f, -10.084f, 25f, -9.924f, 0.09774f
        };

        // A crossing is a cross: the street's arm and the avenue's band of asphalt, and the pavements along each arm; its corners are empty.
        private static readonly float[] CrossingPlates =
        {
            -5.16f, -23.61f, 5.16f, 23.6f, -0.002f,             // the street's asphalt through it
            -18.91f, -10.09f, 18.91f, 10.1f, -0.002f,           // the avenue's
            4.98f, 13.9f, 8.65f, 23.6f, 0.053f,                 // the street's pavements, north of the avenue
            -8.65f, 13.9f, -4.98f, 23.6f, 0.053f,
            4.98f, -23.6f, 8.65f, -13.9f, 0.053f,               // ... and south of it
            -8.65f, -23.6f, -4.98f, -13.9f, 0.053f,
            8.65f, 10.1f, 18.65f, 13.65f, 0.053f,               // the avenue's, east of the street
            8.65f, -13.65f, 18.65f, -10.1f, 0.053f,
            -18.65f, 10.1f, -8.65f, 13.65f, 0.053f,             // ... and west of it
            -18.65f, -13.65f, -8.65f, -10.1f, 0.053f
        };

        // A block's ground is one flat face, its ring of slabs and its middle at one height.
        private static readonly float[] GroundPlates = { -35.001f, -60.001f, 35.001f, 60.001f, 0.05518f };

        /// <summary>The height a piece of this kind is set at, from the game's own module's place (m): avenues up, grounds down, streets and crossings as the game has them.</summary>
        public static float Lift(ModuleKind kind)
        {
            return kind == ModuleKind.Avenue ? AvenueLift : kind == ModuleKind.Ground ? -GroundDrop : 0f;
        }

        /// <summary>A piece of the game's of a kind standing with its middle at (x, z) and its own height <paramref name="y"/> (m) above the plates' own.</summary>
        public static SurfacePiece Place(ModuleKind kind, string name, float x, float y, float z)
        {
            float[] data = kind == ModuleKind.Street ? StreetPlates : kind == ModuleKind.Avenue ? AvenuePlates : kind == ModuleKind.Crossing ? CrossingPlates : GroundPlates;
            var piece = new SurfacePiece(name, data.Length / 5);
            for (int i = 0; i + 4 < data.Length; i += 5)
            {
                piece.Add(x + data[i], z + data[i + 1], x + data[i + 2], z + data[i + 3], y + data[i + 4]);
            }
            return piece;
        }

        /// <summary>A flat piece measured as it stands (a ground of the backdrop's: its rectangle and the height of its top).</summary>
        public static SurfacePiece Flat(string name, float x0, float z0, float x1, float z1, float y)
        {
            var piece = new SurfacePiece(name, 1);
            piece.Add(x0, z0, x1, z1, y);
            return piece;
        }

        /// <summary>
        /// The game's own pieces at 1st Avenue's north side that the district meets (Manhattan's, fixed, at the game's own
        /// height): each continued street's crossing and 1st Avenue's lengths between them.
        /// </summary>
        public static void GamePieces(List<SurfacePiece> into)
        {
            float z = LayoutRules.FirstAvenueZ;
            for (int k = 0; k <= LayoutRules.FirstColumns; k++)
            {
                SurfacePiece crossing = Place(ModuleKind.Crossing, "the game's crossing at " + k, LayoutRules.StreetX(k), 0f, z);
                crossing.Game = true;
                into.Add(crossing);
                if (k < LayoutRules.FirstColumns)
                {
                    SurfacePiece avenue = Place(ModuleKind.Avenue, "the game's 1st Avenue at " + k, LayoutRules.StreetX(k) + LayoutRules.StreetPitch * 0.5f, 0f, z);
                    avenue.Game = true;
                    into.Add(avenue);
                }
            }
        }

        /// <summary>
        /// The plan's pieces as the load lays them: every module of <see cref="LayoutPlans.Modules"/> and the game's own at 1st
        /// Avenue, each at its kind's height when <paramref name="lifted"/> (as the load does), else all at the game's own.
        /// </summary>
        public static void PlanPieces(List<SurfacePiece> into, bool lifted)
        {
            var places = new List<ModulePlace>(512);
            LayoutPlans.Modules(places);
            for (int i = 0; i < places.Count; i++)
            {
                ModulePlace p = places[i];
                into.Add(Place(p.Kind, Name(p), p.X, lifted ? Lift(p.Kind) : 0f, p.Z));
            }
            GamePieces(into);
        }

        /// <summary>A module's name for the log: its kind and where it stands in the plan.</summary>
        public static string Name(ModulePlace p)
        {
            return p.Kind.ToString().ToLowerInvariant() + " " + p.Street + "/" + p.Row + (p.Scenery ? " (scenery)" : string.Empty);
        }

        /// <summary>
        /// Every pair of pieces with a plate each over the same ground at heights within <paramref name="tolerance"/>: how many
        /// pairs, the area they share in all (m2), and the <paramref name="keep"/> worst by area in <paramref name="worst"/>
        /// (largest first, cleared first). Two pieces of the game's own are never a pair: its layout is what the district meets.
        /// </summary>
        public static int Overlaps(List<SurfacePiece> pieces, float tolerance, List<SurfaceSpot> worst, int keep, out float area)
        {
            worst.Clear();
            area = 0f;
            int pairs = 0;
            for (int i = 0; i < pieces.Count; i++)
            {
                SurfacePiece a = pieces[i];
                for (int j = i + 1; j < pieces.Count; j++)
                {
                    SurfacePiece b = pieces[j];
                    if ((a.Game && b.Game) || a.X1 <= b.X0 || b.X1 <= a.X0 || a.Z1 <= b.Z0 || b.Z1 <= a.Z0)
                    {
                        continue;   // two of the game's own are its layout, not ours to change
                    }
                    if (Meet(a, b, tolerance, out SurfaceSpot spot))
                    {
                        pairs++;
                        area += spot.Area;
                        Keep(worst, spot, keep);
                    }
                }
            }
            return pairs;
        }

        // One pair: the area of every plate of the one over a plate of the other at a height within the tolerance; the largest overlap's middle and gap.
        private static bool Meet(SurfacePiece a, SurfacePiece b, float tolerance, out SurfaceSpot spot)
        {
            spot = new SurfaceSpot { A = a.Name, B = b.Name, Gap = float.MaxValue };
            float total = 0f, largest = 0f;
            for (int i = 0; i < a.Plates.Count; i++)
            {
                SurfacePlate p = a.Plates[i];
                for (int j = 0; j < b.Plates.Count; j++)
                {
                    SurfacePlate q = b.Plates[j];
                    float gap = Math.Abs(p.Y - q.Y);
                    if (gap >= tolerance)
                    {
                        continue;
                    }
                    float x0 = Math.Max(p.X0, q.X0), x1 = Math.Min(p.X1, q.X1), z0 = Math.Max(p.Z0, q.Z0), z1 = Math.Min(p.Z1, q.Z1);
                    if (x1 <= x0 || z1 <= z0)
                    {
                        continue;
                    }
                    float shared = (x1 - x0) * (z1 - z0);
                    total += shared;
                    spot.Gap = Math.Min(spot.Gap, gap);
                    if (shared > largest)
                    {
                        largest = shared;
                        spot.X = (x0 + x1) * 0.5f;
                        spot.Z = (z0 + z1) * 0.5f;
                    }
                }
            }
            spot.Area = total;
            return total >= MinArea;
        }

        private static void Keep(List<SurfaceSpot> worst, SurfaceSpot spot, int keep)
        {
            int at = worst.Count;
            while (at > 0 && worst[at - 1].Area < spot.Area)
            {
                at--;
            }
            if (at >= keep)
            {
                return;
            }
            worst.Insert(at, spot);
            if (worst.Count > keep)
            {
                worst.RemoveAt(worst.Count - 1);
            }
        }

        /// <summary>True when a point lies within <see cref="ReachNear"/> of a place, on the ground's plan.</summary>
        public static bool Near(float x, float z, float atX, float atZ)
        {
            float dx = x - atX, dz = z - atZ;
            return dx * dx + dz * dz <= ReachNear * ReachNear;
        }
    }
}
