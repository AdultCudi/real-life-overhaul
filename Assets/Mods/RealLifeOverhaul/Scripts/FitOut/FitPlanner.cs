using System;
using System.Collections.Generic;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul.FitOut
{
    /// <summary>
    /// The fit-out of a building's floor on a <see cref="RoomGrid"/>; no engine type, so it is checked offline (NavCheck).
    /// The design: the game's own finished design for the unit, less what would stand on what is there, on floor kept free
    /// or at a door, and the desk clutter then the decoration when it has too many pieces. The bare floor: its largest
    /// rectangle a walkway from everything gets the next room of the building's kind (FitPlanner.Zones.cs), until none is
    /// left of <see cref="ZoneLeast"/> a side. The walkway check: from the door every seat, every stop, every place the
    /// building had to reach and all the floor reached before must still be reached; a design piece closing a way goes,
    /// and a room of the plan's that would is not laid out. The same room gives the same plan. Cost: a breadth-first walk
    /// of the grid for each piece left out and each room tried, once a visit, off the main thread.
    /// </summary>
    public static partial class FitPlanner
    {
        public const int MostPieces = 360;            // pieces a visit is given at most, the design's and the plan's own
        public const int MostZones = 24;              // rooms of the plan's own at most
        public const float Aisle = 1.0f;              // metres of walkway kept round every room of the plan's own and between rooms
        public const float DoorClear = 1.9f;          // metres round the door the player comes in by
        public const float WayIn = 2.6f, WayInHalf = 0.7f;   // metres straight in from it, and half its width
        public const float ExitClear = 1.6f;          // metres round any other way out
        public const float FacadeBand = 1.5f;         // metres from the street wall where nothing tall stands
        public const float ZoneLeast = 2.5f;          // metres: the smallest side of a bare rectangle given a room
        public const float Reach = 0.6f;              // metres: a piece is reached if the walkway comes this near
        public const float BareReach = 2.0f;          // metres: floor further than this from anything counts as bare
        public const float FromFurniture = 0.9f;      // metres of floor kept free round anything of the player's
        public const int PocketCells = 6;             // a closed-off part smaller than this (or a cell wide) is a gap, not a room
        private const int RoundsMost = 90;            // design pieces cut for the walkway at most
        private const int LooksMost = 80;             // bare rectangles looked at in one plan at most

        /// <summary>The design's pieces (or null) chosen round what the site has, then the bare floor given rooms; the design is not changed.</summary>
        public static void Plan(RoomGrid g, FitSite site, IList<FitPiece> design, FitPlan into)
        {
            into.Clear();
            float inX = site.InX, inZ = site.InZ;
            ServerRoomPlan.Normalise(ref inX, ref inZ);
            site.InX = inX;
            site.InZ = inZ;
            var w = new Work(g, site, into);
            if (!w.Begin())
            {
                into.Why = "no floor at the door";
                return;
            }
            into.FloorArea = w.CountBase() * g.Cell * g.Cell;
            into.BareBefore = w.Bare() * g.Cell * g.Cell;
            if (design != null && design.Count > 0)
            {
                ChooseDesign(w, design);
            }
            if (site.Zones)
            {
                Fill(w);
            }
            w.Check(false, out int unreached, out int pockets, out _, out _);
            into.Unreached = unreached;
            into.Pockets = pockets;
            into.BareAfter = w.Bare() * g.Cell * g.Cell;
            w.Stops();
        }

        private static void ChooseDesign(Work w, IList<FitPiece> design)
        {
            FitPlan into = w.Into;
            List<FitPiece> pieces = into.Pieces;
            int first = pieces.Count;
            for (int i = 0; i < design.Count; i++)
            {
                FitPiece p = design[i];
                p.Design = true;
                p.Kept = true;
                p.Zone = -1;
                p.Anchor = -1;
                pieces.Add(p);
            }
            into.DesignOffered = design.Count;
            // What stands on what, what each chair is drawn up to, what is sat on or walked up to.
            for (int i = first; i < pieces.Count; i++)
            {
                FitPiece p = pieces[i];
                if (p.Role == FitRole.OnTop)
                {
                    p.Anchor = Under(pieces, first, p.X, p.Z, i);
                    if (p.Anchor < 0)
                    {
                        p.Role = FitRole.Hung;   // nothing under it: on a wall
                    }
                }
                else if (p.Role == FitRole.Seat)
                {
                    p.Anchor = DrawnUpTo(pieces, first, p, out bool desk);
                    p.Use = p.Anchor < 0 ? FitUse.Lounge : desk ? FitUse.Desk : MeetingTable(pieces[p.Anchor]) ? FitUse.Meeting : FitUse.Lounge;
                }
                else if (p.Role == FitRole.Floor && FitRoles.IsSofa(p.Prefab))
                {
                    p.Use = p.Prefab.IndexOf("waiting", StringComparison.Ordinal) >= 0 ? FitUse.Waiting : FitUse.Lounge;
                }
                if (p.Use == FitUse.None && (p.Role == FitRole.Floor || p.Role == FitRole.OnTop) && FitRoles.IsStop(p.Prefab))
                {
                    p.Use = FitUse.Stop;
                }
                pieces[i] = p;
            }
            // Left out: on what is there, on floor kept free, at a door; then whatever goes with what was left out.
            for (int i = first; i < pieces.Count; i++)
            {
                FitPiece p = pieces[i];
                if (w.Clashes(p))
                {
                    p.Kept = false;
                    pieces[i] = p;
                    into.DesignClashed++;
                }
            }
            w.DropDependents(first);
            // Too many: the clutter first, then the decoration, then what stands on or hangs; the last of the design first.
            int kept = 0;
            for (int i = first; i < pieces.Count; i++)
            {
                kept += pieces[i].Kept ? 1 : 0;
            }
            for (int spare = 3; spare >= 1 && kept > w.Site.MostPieces; spare--)
            {
                for (int i = pieces.Count - 1; i >= first && kept > w.Site.MostPieces; i--)
                {
                    FitPiece p = pieces[i];
                    int rank = FitRoles.Spare(p.Prefab) + (p.Role == FitRole.OnTop || p.Role == FitRole.Hung ? 1 : 0);
                    if (p.Kept && rank >= spare && p.Role != FitRole.Lamp)
                    {
                        p.Kept = false;
                        pieces[i] = p;
                        kept--;
                        into.DesignCapped++;
                    }
                }
            }
            w.DropDependents(first);
            // The walkway check: whatever closes a way goes, the piece nearest the closed place first.
            for (int round = 0; round < RoundsMost; round++)
            {
                if (w.Check(true, out _, out _, out float px, out float pz))
                {
                    return;
                }
                int worst = w.Opener(px, pz, first);
                if (worst < 0)
                {
                    break;
                }
                FitPiece p = pieces[worst];
                p.Kept = false;
                pieces[worst] = p;
                into.DesignCut++;
                w.DropDependents(first);
            }
            if (!w.Check(true, out _, out _, out _, out _))
            {
                // not enough: the design is given up for the plan's own rooms
                for (int i = first; i < pieces.Count; i++)
                {
                    FitPiece p = pieces[i];
                    p.Kept = false;
                    pieces[i] = p;
                }
                into.DesignGivenUp = true;
            }
        }

        // The smallest floor piece of the design whose footprint holds a point: what a raised piece stands on.
        private static int Under(List<FitPiece> pieces, int first, float x, float z, int self)
        {
            int best = -1;
            float least = float.MaxValue;
            for (int i = first; i < pieces.Count; i++)
            {
                FitPiece o = pieces[i];
                if (i == self || o.Role != FitRole.Floor || !o.Contains(x, z, 0.05f))
                {
                    continue;
                }
                float area = o.HalfX * o.HalfZ;
                if (area < least)
                {
                    least = area;
                    best = i;
                }
            }
            return best;
        }

        // The desk or table a chair is at: the nearest within a step and a half.
        private static int DrawnUpTo(List<FitPiece> pieces, int first, FitPiece chair, out bool desk)
        {
            desk = false;
            int best = -1;
            float bestSqr = 1.4f * 1.4f;
            for (int i = first; i < pieces.Count; i++)
            {
                FitPiece o = pieces[i];
                bool? surface = o.Role == FitRole.Floor ? FitRoles.Surface(o.Prefab) : null;
                if (surface == null)
                {
                    continue;
                }
                float dx = Math.Max(0f, Math.Abs(chair.X - o.CX) - o.HalfX), dz = Math.Max(0f, Math.Abs(chair.Z - o.CZ) - o.HalfZ);
                float sqr = dx * dx + dz * dz;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = i;
                    desk = surface.Value;
                }
            }
            return best;
        }

        private static bool MeetingTable(FitPiece table) => table.Prefab != null && (table.Prefab.IndexOf("meeting", StringComparison.Ordinal) >= 0 || table.HalfX * table.HalfZ > 0.6f);

        private static void Fill(Work w)
        {
            FitPlan into = w.Into;
            ZoneType[] order = Order(w.Site.Kind, out int[] most);
            var left = (int[])most.Clone();
            int next = w.Site.Kind == FitKind.TechOffice ? (w.Site.Seed & 0x7fffffff) % 2 : 0;   // two headquarters of a size begin unalike
            w.MarkUsable();
            for (int look = 0; look < LooksMost && into.Zones < MostZones && into.Kept < w.Site.MostPieces; look++)
            {
                if (!w.Largest(ZoneLeast, out float x0, out float z0, out float x1, out float z1))
                {
                    break;
                }
                Box box = Box.Of(w, x0, z0, x1, z1);
                bool placed = false;
                for (int k = 0; k <= order.Length && !placed; k++)
                {
                    ZoneType type = k < order.Length ? order[(next + k) % order.Length] : ZoneType.Plants;
                    if (left[(int)type] <= 0)
                    {
                        continue;
                    }
                    ZoneSize(w, type, out float mostU, out float mostV);
                    for (int tight = 0; tight < 2 && !placed; tight++)
                    {
                        int first = into.Pieces.Count;
                        into.ZoneTries++;
                        // the room takes the box's corner at the start of its wall, as much as it needs
                        Box b = (tight == 0 ? box : box.Inset(0.5f)).Clip(mostU, mostV);
                        bool laid = b.LU >= 1.2f && b.LV >= 1.2f && Lay(w, type, b, into.Zones) && into.Pieces.Count > first;
                        if (!laid || !w.Check(true, out _, out _, out _, out _))
                        {
                            into.Pieces.RemoveRange(first, into.Pieces.Count - first);
                            continue;
                        }
                        if (w.Site.ZoneLamps && !IsPreset(w.Site.Kind))   // a preset stands in a room the game has lit
                        {
                            Lamps(w, b, into.Zones);
                        }
                        placed = true;
                        left[(int)type]--;
                        into.ZoneCounts[(int)type]++;
                        into.Zones++;
                        if (k < order.Length)
                        {
                            next = (next + k + 1) % order.Length;
                        }
                        // its floor and a walkway round it are taken; the rest of the box stays bare
                        w.Footprint(first, out float bx0, out float bz0, out float bx1, out float bz1);
                        w.Unmark(bx0 - Aisle, bz0 - Aisle, bx1 + Aisle, bz1 + Aisle);
                    }
                }
                if (!placed)
                {
                    w.Unmark(x0, z0, x1, z1);   // nothing has room here: on to the next largest
                }
            }
        }

        // One ceiling lamp every four and a half metres or so each way.
        private static void Lamps(Work w, Box b, int zone)
        {
            string lamp = w.Site.Kind == FitKind.DataCentre ? "ceilinglampwarehouse" : "ceilinglampsquare";
            int nu = Math.Max(1, (int)Math.Round(b.LU / 4.5f)), nv = Math.Max(1, (int)Math.Round(b.LV / 4.5f));
            for (int i = 0; i < nu; i++)
            {
                for (int j = 0; j < nv; j++)
                {
                    b.Point((i + 0.5f) * b.LU / nu, (j + 0.5f) * b.LV / nv, out float x, out float z);
                    w.Into.Pieces.Add(new FitPiece { Prefab = lamp, Role = FitRole.Lamp, X = x, Z = z, CX = x, CZ = z, HalfX = 0.3f, HalfZ = 0.3f, Anchor = -1, Zone = zone, Kept = true });
                }
            }
        }

        /// <summary>A bare rectangle in its own frame: u along its back (a wall, if it has one), v out from it.</summary>
        private struct Box
        {
            public float Ox, Oz, Ux, Uz, Vx, Vz;   // the corner at u = v = 0, and the frame's axes
            public float LU, LV;
            public bool WallV0;                    // a wall runs along v = 0
            public bool FacadeV0;                  // and it is the street wall, with the windows
            public float WallBack;                 // v of the wall's real surface, where a piece backing onto it has its back

            public void Point(float u, float v, out float x, out float z)
            {
                x = Ox + Ux * u + Vx * v;
                z = Oz + Uz * u + Vz * v;
            }

            public void Dir(float fu, float fv, out float x, out float z)
            {
                x = Ux * fu + Vx * fv;
                z = Uz * fu + Vz * fv;
            }

            /// <summary>The same box from the same corner, no longer or deeper than asked.</summary>
            public Box Clip(float mostU, float mostV)
            {
                Box b = this;
                b.LU = Math.Min(LU, mostU);
                b.LV = Math.Min(LV, mostV);
                return b;
            }

            /// <summary>The box in the room's axes.</summary>
            public void Extent(out float x0, out float z0, out float x1, out float z1)
            {
                Point(0f, 0f, out float ax, out float az);
                Point(LU, LV, out float bx, out float bz);
                x0 = Math.Min(ax, bx);
                z0 = Math.Min(az, bz);
                x1 = Math.Max(ax, bx);
                z1 = Math.Max(az, bz);
            }

            /// <summary>The same box, a margin in from every side but its wall.</summary>
            public Box Inset(float by)
            {
                Box b = this;
                float back = WallV0 ? 0f : by;
                Point(by, back, out b.Ox, out b.Oz);
                b.LU = LU - 2f * by;
                b.LV = LV - back - by;
                return b;
            }

            // The rectangle, with its back on the best wall it has: one that is not the street wall, the longer first.
            public static Box Of(Work w, float x0, float z0, float x1, float z1)
            {
                RoomGrid g = w.G;
                int best = -1;
                float bestScore = float.MinValue;
                bool bestWall = false, bestFacade = false;
                for (int s = 0; s < 4; s++)
                {
                    // Sides 0 low x, 1 high x, 2 low z, 3 high z: a wall if most cells beyond it are outside the floor.
                    int beyond = 0, solid = 0;
                    float half = g.Cell * 0.5f;
                    bool alongZ = s < 2;
                    float from = alongZ ? z0 : x0, to = alongZ ? z1 : x1;
                    for (float t = from + half; t < to; t += g.Cell)
                    {
                        float x = s == 0 ? x0 - half : s == 1 ? x1 + half : t, z = s == 2 ? z0 - half : s == 3 ? z1 + half : t;
                        beyond++;
                        solid += !g.CellOf(x, z, out int c, out int r) || g[c, r] == RoomGrid.Outside ? 1 : 0;   // never the player's furniture
                    }
                    bool wall = beyond > 0 && solid >= beyond * 0.7f;
                    float mx = s == 0 ? x0 : s == 1 ? x1 : (x0 + x1) * 0.5f, mz = s == 2 ? z0 : s == 3 ? z1 : (z0 + z1) * 0.5f;
                    bool facade = w.Site.Windows && (mx - w.Site.DoorX) * w.Site.InX + (mz - w.Site.DoorZ) * w.Site.InZ < 1.0f;
                    float score = (wall ? 100f : 0f) + (wall && !facade ? 50f : 0f) + (alongZ ? z1 - z0 : x1 - x0);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = s;
                        bestWall = wall;
                        bestFacade = wall && facade;
                    }
                }
                var b = new Box { WallV0 = bestWall, FacadeV0 = bestFacade };
                switch (best)
                {
                    case 0: b.Ox = x0; b.Oz = z1; b.Ux = 0f; b.Uz = -1f; b.Vx = 1f; b.Vz = 0f; b.LU = z1 - z0; b.LV = x1 - x0; break;
                    case 1: b.Ox = x1; b.Oz = z0; b.Ux = 0f; b.Uz = 1f; b.Vx = -1f; b.Vz = 0f; b.LU = z1 - z0; b.LV = x1 - x0; break;
                    case 2: b.Ox = x0; b.Oz = z0; b.Ux = 1f; b.Uz = 0f; b.Vx = 0f; b.Vz = 1f; b.LU = x1 - x0; b.LV = z1 - z0; break;
                    default: b.Ox = x1; b.Oz = z1; b.Ux = -1f; b.Uz = 0f; b.Vx = 0f; b.Vz = -1f; b.LU = x1 - x0; b.LV = z1 - z0; break;
                }
                // the walkable floor stops a walker's width short of a wall: a piece backing onto it goes that much further
                b.WallBack = b.WallV0 ? -(w.Site.Erosion - 0.05f) : 0f;
                return b;
            }
        }
    }
}
