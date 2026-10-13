using System;
using System.Collections.Generic;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul.FitOut
{
    /// <summary>
    /// The fit-out of a building's floor (Scripts/FitOut): what of the game's own design for the unit stands round what
    /// the building already has, and what the floor that is still bare is given, rooms of the building's kind laid out by
    /// the rules of FitPlanner.Zones.cs. Plain numbers on a <see cref="RoomGrid"/> measured on the way in; no engine type,
    /// so all of it is checked in a console test (RealLifeOverhaul.Build/NavCheck/FitCheck.cs).
    ///
    /// THE DESIGN. The game ships finished designs for every size of unit (the interior installation firm's, and the
    /// layouts of its own businesses): its pieces at their places in that very room. A piece is left out if it would stand
    /// on what is already there or on floor kept free, at a door or straight in from it; what stands on it or is drawn up
    /// to it goes with it; and when a design has more pieces than a visit is given, the desk clutter goes first, then the
    /// decoration.
    ///
    /// THE BARE FLOOR. The largest rectangle of floor still bare, a walkway's width from everything, is given the next room
    /// of the building's kind it has room for, then the next largest, until none is left of two and a half metres a side.
    /// What goes against a wall goes against one; nothing tall stands in the windows of the street wall, nothing within
    /// reach of a door or a way out.
    ///
    /// THE WALKWAY CHECK. Everything that stands is cut out of the floor, grown by a walker's width, and from the door the
    /// plan must still reach every seat and every piece somebody walks up to, every place the building already had to be
    /// reached, and every part of the floor it could reach before: nothing may close off a corner of the room or a room of
    /// the unit. A piece of the design that closes a way is left out, the one nearest the closed place first (and the whole
    /// design if that is not enough); a room of the plan's own that would close one is not laid out.
    ///
    /// The same room, the same building and the same pieces always give the same plan. Its cost: a breadth-first walk of
    /// the grid for each piece left out and each room tried, once per visit, off the main thread (FitView).
    /// </summary>
    public static partial class FitPlanner
    {
        public const int MostPieces = 360;            // pieces a visit is given at most, the design's and the plan's own
        public const int MostZones = 24;
        public const float Aisle = 1.0f;              // metres of walkway kept round every room of the plan's own and between rooms
        public const float DoorClear = 1.9f;          // round the door the player comes in by
        public const float WayIn = 2.6f, WayInHalf = 0.7f;   // and straight in from it
        public const float ExitClear = 1.6f;          // round any other way out
        public const float FacadeBand = 1.5f;         // nothing tall within this much of the street wall
        public const float ZoneLeast = 2.5f;          // the smallest side of a bare rectangle that is given a room
        public const float Reach = 0.6f;              // a piece is reached if the walkway comes this near it
        public const float BareReach = 2.0f;          // floor further than this from anything counts as bare
        public const float FromFurniture = 0.9f;      // metres of floor kept free round anything of the player's
        public const int PocketCells = 6;             // a closed-off part of the floor smaller than this (or a cell wide) is a gap behind something, not a room
        private const int RoundsMost = 90;
        private const int LooksMost = 80;             // bare rectangles looked at in one plan at most

        // ---------------------------------------------------------------- the plan

        /// <summary>
        /// The fit-out of a room: the design's pieces (none: null) chosen round what the site already has, then the bare
        /// floor given rooms of its kind. <paramref name="design"/> is read, not changed.
        /// </summary>
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

        // ---------------------------------------------------------------- the game's design

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
                        p.Role = FitRole.Hung;   // (nothing under it: it hangs on a wall)
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
                // Not enough: the design is given up and the floor is laid out by the plan's own rules.
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

        // ---------------------------------------------------------------- the bare floor

        private static void Fill(Work w)
        {
            FitPlan into = w.Into;
            ZoneType[] order = Order(w.Site.Kind, out int[] most);
            var left = (int[])most.Clone();
            int next = w.Site.Kind == FitKind.TechOffice ? (w.Site.Seed & 0x7fffffff) % 2 : 0;   // (two headquarters of a size do not begin alike)
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
                        // The room takes the corner of the box at the start of its wall, as much of it as it needs.
                        Box b = (tight == 0 ? box : box.Inset(0.5f)).Clip(mostU, mostV);
                        bool laid = b.LU >= 1.2f && b.LV >= 1.2f && Lay(w, type, b, into.Zones) && into.Pieces.Count > first;
                        if (!laid || !w.Check(true, out _, out _, out _, out _))
                        {
                            into.Pieces.RemoveRange(first, into.Pieces.Count - first);
                            continue;
                        }
                        if (w.Site.ZoneLamps)
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
                        // The floor its pieces stand on and a walkway round it are taken; the rest of the box is bare still.
                        w.Footprint(first, out float bx0, out float bz0, out float bx1, out float bz1);
                        w.Unmark(bx0 - Aisle, bz0 - Aisle, bx1 + Aisle, bz1 + Aisle);
                    }
                }
                if (!placed)
                {
                    w.Unmark(x0, z0, x1, z1);   // (nothing has room here: the next largest is looked at)
                }
            }
        }

        // Lamps on the ceiling over a room of the plan's own: one for every four and a half metres or so each way.
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

        // ---------------------------------------------------------------- the box a room is laid out in

        /// <summary>
        /// A bare rectangle in its own frame: u along its back (a wall, if it has one), v out from it. What is laid out in it
        /// is placed by u and v and turned by its facing in the frame.
        /// </summary>
        private struct Box
        {
            public float Ox, Oz, Ux, Uz, Vx, Vz;   // the corner at u = v = 0, and the frame's axes
            public float LU, LV;
            public bool WallV0;                    // a wall runs along v = 0
            public bool FacadeV0;                  // ...and it is the street wall, with the windows
            public float WallBack;                 // where a piece backing onto that wall has its back (v): the wall's real surface

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
                    // The four sides: 0 low x, 1 high x, 2 low z, 3 high z. A side is a wall if most of the cells beyond it are outside the floor.
                    int beyond = 0, solid = 0;
                    float half = g.Cell * 0.5f;
                    bool alongZ = s < 2;
                    float from = alongZ ? z0 : x0, to = alongZ ? z1 : x1;
                    for (float t = from + half; t < to; t += g.Cell)
                    {
                        float x = s == 0 ? x0 - half : s == 1 ? x1 + half : t, z = s == 2 ? z0 - half : s == 3 ? z1 + half : t;
                        beyond++;
                        solid += !g.CellOf(x, z, out int c, out int r) || g[c, r] == RoomGrid.Outside ? 1 : 0;   // (never the player's furniture: nothing backs onto it)
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
                // The walkable floor stops a walker's width short of a wall: a piece backing onto it goes back that much further.
                b.WallBack = b.WallV0 ? -(w.Site.Erosion - 0.05f) : 0f;
                return b;
            }
        }

        // ---------------------------------------------------------------- the working state of one plan

        private sealed class Work
        {
            public readonly RoomGrid G;
            public readonly FitSite Site;
            public readonly FitPlan Into;
            public readonly bool[] Usable;
            private readonly bool[] _fixedClosed, _base, _closed, _after, _seen, _narrow, _trial;
            private readonly bool[] _keepValid;
            private readonly bool[] _sides;              // the floor beside the player's furniture, from which it is used
            private bool _hasSides;
            private readonly int[] _stack;
            private readonly float _margin;

            public Work(RoomGrid g, FitSite site, FitPlan into)
            {
                G = g;
                Site = site;
                Into = into;
                int n = g.Count;
                Usable = new bool[n];
                _fixedClosed = new bool[n];
                _base = new bool[n];
                _closed = new bool[n];
                _after = new bool[n];
                _seen = new bool[n];
                _stack = new int[n];
                _narrow = new bool[n];
                _trial = new bool[n];
                _sides = new bool[n];
                _keepValid = new bool[site.Keep.Count];
                _margin = Math.Max(0.1f, site.Erosion);   // (what stands is cut out of the walkable floor by a walker's width, as the game cuts its own)
            }

            /// <summary>The floor reached from the door round what is already there, and which of the site's places it reaches.</summary>
            public bool Begin()
            {
                for (int i = 0; i < Site.Fixed.Count; i++)
                {
                    FitRect r = Site.Fixed[i];
                    if (r.Solid)
                    {
                        CloseRect(r.X0 - _margin, r.Z0 - _margin, r.X1 + _margin, r.Z1 + _margin, _fixedClosed);
                    }
                }
                if (G.Reach(Site.DoorX, Site.DoorZ, _fixedClosed, _base) == 0)
                {
                    return false;
                }
                Narrow();
                _hasSides = G.FurnitureSides(_base, _sides) > 0;
                // (a place the building could not reach before is not the plan's to keep)
                Array.Copy(_base, _after, _after.Length);
                for (int i = 0; i < _keepValid.Length; i++)
                {
                    FitSpot s = Site.Keep[i];
                    _keepValid[i] = ReachedNear(s.X, s.Z, Math.Max(Reach, s.R));
                }
                return true;
            }

            // The unit's doorways and narrow ways: open floor less than 1.8 m across between walls, running on for 2.4 m or
            // more the other way, and the floor within 0.9 m of it. Nothing of the plan's stands there.
            private void Narrow()
            {
                int cols = G.Cols, rows = G.Rows;
                var across = new int[cols * rows];
                var along = new int[cols * rows];
                for (int r = 0; r < rows; r++)
                {
                    for (int c = 0; c < cols;)
                    {
                        if (!G.IsOpen(c, r))
                        {
                            c++;
                            continue;
                        }
                        int start = c;
                        while (c < cols && G.IsOpen(c, r))
                        {
                            c++;
                        }
                        for (int k = start; k < c; k++)
                        {
                            across[r * cols + k] = c - start;
                        }
                    }
                }
                for (int c = 0; c < cols; c++)
                {
                    for (int r = 0; r < rows;)
                    {
                        if (!G.IsOpen(c, r))
                        {
                            r++;
                            continue;
                        }
                        int start = r;
                        while (r < rows && G.IsOpen(c, r))
                        {
                            r++;
                        }
                        for (int k = start; k < r; k++)
                        {
                            along[k * cols + c] = r - start;
                        }
                    }
                }
                float cell = G.Cell;
                int reach = (int)Math.Ceiling(0.9f / cell);
                for (int i = 0; i < _narrow.Length; i++)
                {
                    float x = across[i] * cell, z = along[i] * cell;
                    if (across[i] == 0 || !((x <= 1.8f && z >= 2.4f) || (z <= 1.8f && x >= 2.4f)))
                    {
                        continue;
                    }
                    int c0 = i % cols, r0 = i / cols;
                    for (int dr = -reach; dr <= reach; dr++)
                    {
                        for (int dc = -reach; dc <= reach; dc++)
                        {
                            int c = c0 + dc, r = r0 + dr;
                            if (c >= 0 && r >= 0 && c < cols && r < rows)
                            {
                                _narrow[r * cols + c] = true;
                            }
                        }
                    }
                }
            }

            /// <summary>True if a rectangle covers a doorway or a narrow way of the unit.</summary>
            public bool InNarrow(float x0, float z0, float x1, float z1)
            {
                G.CellOf(x0, z0, out int c0, out int r0);
                G.CellOf(x1, z1, out int c1, out int r1);
                for (int r = Math.Max(0, r0); r <= Math.Min(G.Rows - 1, r1); r++)
                {
                    for (int c = Math.Max(0, c0); c <= Math.Min(G.Cols - 1, c1); c++)
                    {
                        G.Centre(c, r, out float x, out float z);
                        if (x >= x0 && x <= x1 && z >= z0 && z <= z1 && _narrow[r * G.Cols + c])
                        {
                            return true;
                        }
                    }
                }
                return false;
            }

            /// <summary>
            /// The largest rectangle of bare floor with no side shorter than <paramref name="least"/> (the histogram method,
            /// one pass a row, every rectangle bounded by the floor around it looked at): a long strip a room cannot use never
            /// wins over a smaller square one it can. False if there is none.
            /// </summary>
            public bool Largest(float least, out float x0, out float z0, out float x1, out float z1)
            {
                x0 = z0 = x1 = z1 = 0f;
                int cols = G.Cols, rows = G.Rows;
                int minCols = (int)Math.Ceiling(least / G.Cell - 1e-4f), minRows = minCols;
                var height = new int[cols];
                var stack = new int[cols + 1];
                int bestArea = 0, bestC0 = 0, bestC1 = 0, bestR0 = 0, bestR1 = 0;
                for (int r = 0; r < rows; r++)
                {
                    for (int c = 0; c < cols; c++)
                    {
                        height[c] = Usable[r * cols + c] ? height[c] + 1 : 0;
                    }
                    int top = 0;
                    for (int c = 0; c <= cols; c++)
                    {
                        int h = c < cols ? height[c] : 0;
                        while (top > 0 && height[stack[top - 1]] >= h)
                        {
                            int tall = height[stack[--top]];
                            int left = top > 0 ? stack[top - 1] + 1 : 0;
                            int wide = c - left;
                            if (tall >= minRows && wide >= minCols && tall * wide > bestArea)
                            {
                                bestArea = tall * wide;
                                bestC0 = left;
                                bestC1 = c - 1;
                                bestR1 = r;
                                bestR0 = r - tall + 1;
                            }
                        }
                        stack[top++] = c;
                    }
                }
                if (bestArea == 0)
                {
                    return false;
                }
                x0 = G.MinX + bestC0 * G.Cell;
                x1 = G.MinX + (bestC1 + 1) * G.Cell;
                z0 = G.MinZ + bestR0 * G.Cell;
                z1 = G.MinZ + (bestR1 + 1) * G.Cell;
                return true;
            }

            /// <summary>The floor the standing pieces from an index on take, in the room's axes.</summary>
            public void Footprint(int first, out float x0, out float z0, out float x1, out float z1)
            {
                x0 = z0 = float.MaxValue;
                x1 = z1 = float.MinValue;
                for (int i = first; i < Into.Pieces.Count; i++)
                {
                    FitPiece p = Into.Pieces[i];
                    if (!p.Kept || (p.Role != FitRole.Floor && p.Role != FitRole.Seat && p.Role != FitRole.Hung))
                    {
                        continue;
                    }
                    x0 = Math.Min(x0, p.CX - p.HalfX);
                    z0 = Math.Min(z0, p.CZ - p.HalfZ);
                    x1 = Math.Max(x1, p.CX + p.HalfX);
                    z1 = Math.Max(z1, p.CZ + p.HalfZ);
                }
                if (x0 > x1)
                {
                    x0 = x1 = z0 = z1 = 0f;
                }
            }

            /// <summary>
            /// The kept piece of the design whose going opens a closed place again: of those that touch it (the part of the
            /// floor closed off, or the place not reached), the first that does, partitions last and the smallest first;
            /// failing that, the nearest. Left out already when it is returned. -1 for none.
            /// </summary>
            public int Opener(float px, float pz, int first)
            {
                List<FitPiece> pieces = Into.Pieces;
                G.CellOf(px, pz, out int pc, out int pr);
                int start = pc >= 0 && pr >= 0 && pc < G.Cols && pr < G.Rows ? pr * G.Cols + pc : -1;
                bool pocket = start >= 0 && _base[start] && !_closed[start] && !_after[start];
                if (pocket)
                {
                    Array.Clear(_seen, 0, _seen.Length);
                    Pocket(start, out _, out _, out _, out _);   // (its cells are marked in _seen)
                }
                var candidates = new List<int>(16);
                float grow = _margin + G.Cell;
                for (int i = first; i < pieces.Count; i++)
                {
                    FitPiece p = pieces[i];
                    if (!p.Kept || !p.Design || p.Role != FitRole.Floor)
                    {
                        continue;
                    }
                    bool touches = pocket ? Touches(p, grow, _seen) : new FitRect(p.CX - p.HalfX, p.CZ - p.HalfZ, p.CX + p.HalfX, p.CZ + p.HalfZ, true).Distance(px, pz) < 1.5f;
                    if (touches)
                    {
                        candidates.Add(i);
                    }
                }
                candidates.Sort((a, b) =>
                {
                    int byKind = (FitRoles.IsPartition(pieces[a].Prefab) ? 1 : 0).CompareTo(FitRoles.IsPartition(pieces[b].Prefab) ? 1 : 0);
                    return byKind != 0 ? byKind : (pieces[a].HalfX * pieces[a].HalfZ).CompareTo(pieces[b].HalfX * pieces[b].HalfZ);
                });
                for (int k = 0; k < candidates.Count && k < 10; k++)
                {
                    int i = candidates[k];
                    FitPiece p = pieces[i];
                    p.Kept = false;
                    pieces[i] = p;
                    if (Opens(px, pz))
                    {
                        return i;
                    }
                    p.Kept = true;
                    pieces[i] = p;
                }
                int nearest = NearestDroppable(px, pz, first);
                if (nearest >= 0)
                {
                    FitPiece p = pieces[nearest];
                    p.Kept = false;
                    pieces[nearest] = p;
                }
                return nearest;
            }

            // A piece's footprint, grown, over any marked cell.
            private bool Touches(FitPiece p, float grow, bool[] marked)
            {
                G.CellOf(p.CX - p.HalfX - grow, p.CZ - p.HalfZ - grow, out int c0, out int r0);
                G.CellOf(p.CX + p.HalfX + grow, p.CZ + p.HalfZ + grow, out int c1, out int r1);
                for (int r = Math.Max(0, r0); r <= Math.Min(G.Rows - 1, r1); r++)
                {
                    for (int c = Math.Max(0, c0); c <= Math.Min(G.Cols - 1, c1); c++)
                    {
                        if (marked[r * G.Cols + c])
                        {
                            return true;
                        }
                    }
                }
                return false;
            }

            // With the pieces kept as they are now, is a place reached from the door?
            private bool Opens(float x, float z)
            {
                Array.Copy(_fixedClosed, _closed, _closed.Length);
                List<FitPiece> pieces = Into.Pieces;
                for (int i = 0; i < pieces.Count; i++)
                {
                    FitPiece p = pieces[i];
                    if (p.Kept && p.Role == FitRole.Floor)
                    {
                        CloseRect(p.CX - p.HalfX - _margin, p.CZ - p.HalfZ - _margin, p.CX + p.HalfX + _margin, p.CZ + p.HalfZ + _margin, _closed);
                    }
                }
                G.Reach(Site.DoorX, Site.DoorZ, _closed, _trial);
                G.CellOf(x - Reach, z - Reach, out int c0, out int r0);
                G.CellOf(x + Reach, z + Reach, out int c1, out int r1);
                for (int r = Math.Max(0, r0); r <= Math.Min(G.Rows - 1, r1); r++)
                {
                    for (int c = Math.Max(0, c0); c <= Math.Min(G.Cols - 1, c1); c++)
                    {
                        G.Centre(c, r, out float cx, out float cz);
                        if (_trial[r * G.Cols + c] && (cx - x) * (cx - x) + (cz - z) * (cz - z) <= Reach * Reach)
                        {
                            return true;
                        }
                    }
                }
                return false;
            }

            private void CloseRect(float x0, float z0, float x1, float z1, bool[] into)
            {
                G.CellOf(x0, z0, out int c0, out int r0);
                G.CellOf(x1, z1, out int c1, out int r1);
                for (int r = Math.Max(0, r0); r <= Math.Min(G.Rows - 1, r1); r++)
                {
                    for (int c = Math.Max(0, c0); c <= Math.Min(G.Cols - 1, c1); c++)
                    {
                        G.Centre(c, r, out float x, out float z);
                        if (x >= x0 && x <= x1 && z >= z0 && z <= z1)
                        {
                            into[r * G.Cols + c] = true;
                        }
                    }
                }
            }

            public int CountBase()
            {
                int n = 0;
                for (int i = 0; i < _base.Length; i++)
                {
                    n += _base[i] ? 1 : 0;
                }
                return n;
            }

            /// <summary>The reached cells further than <see cref="BareReach"/> from everything that stands, already there or planned, but for the ways in and the doorways.</summary>
            public int Bare()
            {
                Array.Clear(_seen, 0, _seen.Length);
                for (int i = 0; i < Site.Fixed.Count; i++)
                {
                    FitRect r = Site.Fixed[i];
                    CloseRect(r.X0 - BareReach, r.Z0 - BareReach, r.X1 + BareReach, r.Z1 + BareReach, _seen);
                }
                for (int i = 0; i < Into.Pieces.Count; i++)
                {
                    FitPiece p = Into.Pieces[i];
                    if (p.Kept && (p.Role == FitRole.Floor || p.Role == FitRole.Seat || p.Role == FitRole.Flat))
                    {
                        CloseRect(p.CX - p.HalfX - BareReach, p.CZ - p.HalfZ - BareReach, p.CX + p.HalfX + BareReach, p.CZ + p.HalfZ + BareReach, _seen);
                    }
                }
                int bare = 0;
                float h = G.Cell * 0.5f;
                for (int i = 0; i < _base.Length; i++)
                {
                    if (!_base[i] || _seen[i] || _narrow[i])
                    {
                        continue;
                    }
                    G.Centre(i, out float x, out float z);
                    bare += AtADoor(x - h, z - h, x + h, z + h) ? 0 : 1;   // (the way in and the doorways are kept open on purpose)
                }
                return bare;
            }

            /// <summary>A piece of the design that would stand where it may not: on what is there, on floor kept free, at a door.</summary>
            public bool Clashes(FitPiece p)
            {
                if (!FitRoles.Fitted(p.Prefab))
                {
                    return true;
                }
                if (p.Role == FitRole.Lamp || p.Role == FitRole.OnTop)
                {
                    return false;   // (a lamp is on the ceiling; what stands on a piece goes with it)
                }
                if (p.Role == FitRole.Hung)
                {
                    for (int i = 0; i < Site.Fixed.Count; i++)
                    {
                        FitRect r = Site.Fixed[i];
                        if ((r.Wall && r.Distance(p.X, p.Z) < 1.0f) || (r.Solid && r.Distance(p.X, p.Z) < 0.05f))
                        {
                            return true;
                        }
                    }
                    return false;
                }
                float grow = p.Role == FitRole.Flat ? 0f : 0.2f;
                for (int i = 0; i < Site.Fixed.Count; i++)
                {
                    if (Overlaps(p, Site.Fixed[i], grow))
                    {
                        return true;
                    }
                }
                if (p.Role == FitRole.Flat)
                {
                    return false;   // (a carpet may lie at the door and on the way in)
                }
                for (int i = 0; i < Site.KeepFree.Count; i++)
                {
                    if (Overlaps(p, Site.KeepFree[i], 0f))
                    {
                        return true;
                    }
                }
                return AtADoor(p.CX - p.HalfX, p.CZ - p.HalfZ, p.CX + p.HalfX, p.CZ + p.HalfZ);
            }

            private static bool Overlaps(FitPiece p, FitRect r, float grow)
            {
                return p.CX + p.HalfX > r.X0 - grow && p.CX - p.HalfX < r.X1 + grow && p.CZ + p.HalfZ > r.Z0 - grow && p.CZ - p.HalfZ < r.Z1 + grow;
            }

            /// <summary>A rectangle within reach of the door, on the way straight in from it, or round another way out.</summary>
            public bool AtADoor(float x0, float z0, float x1, float z1)
            {
                var foot = new FitRect(x0, z0, x1, z1, true);
                if (foot.Distance(Site.DoorX, Site.DoorZ) < DoorClear)
                {
                    return true;
                }
                // The way in, a corridor straight in from the door (the door's frame is square to the room's axes).
                float ax = Site.InZ * WayInHalf, az = -Site.InX * WayInHalf;
                float ex = Site.DoorX + Site.InX * WayIn, ez = Site.DoorZ + Site.InZ * WayIn;
                float wx0 = Math.Min(Math.Min(Site.DoorX - ax, Site.DoorX + ax), Math.Min(ex - ax, ex + ax));
                float wx1 = Math.Max(Math.Max(Site.DoorX - ax, Site.DoorX + ax), Math.Max(ex - ax, ex + ax));
                float wz0 = Math.Min(Math.Min(Site.DoorZ - az, Site.DoorZ + az), Math.Min(ez - az, ez + az));
                float wz1 = Math.Max(Math.Max(Site.DoorZ - az, Site.DoorZ + az), Math.Max(ez - az, ez + az));
                if (x1 > wx0 && x0 < wx1 && z1 > wz0 && z0 < wz1)
                {
                    return true;
                }
                for (int i = 0; i < Site.Exits.Count; i++)
                {
                    FitSpot e = Site.Exits[i];
                    if (foot.Distance(e.X, e.Z) < e.R)
                    {
                        return true;
                    }
                }
                return false;
            }

            /// <summary>In the band of floor along the street wall, where its windows are.</summary>
            public bool Facade(float x0, float z0, float x1, float z1)
            {
                float least = float.MaxValue;
                for (int corner = 0; corner < 4; corner++)
                {
                    float x = (corner & 1) == 0 ? x0 : x1, z = (corner & 2) == 0 ? z0 : z1;
                    least = Math.Min(least, (x - Site.DoorX) * Site.InX + (z - Site.DoorZ) * Site.InZ);
                }
                return least < FacadeBand;
            }

            /// <summary>Whatever stands on a piece left out, or is drawn up to it, is left out with it.</summary>
            public void DropDependents(int first)
            {
                List<FitPiece> pieces = Into.Pieces;
                for (int pass = 0; pass < 3; pass++)
                {
                    bool any = false;
                    for (int i = first; i < pieces.Count; i++)
                    {
                        FitPiece p = pieces[i];
                        if (p.Kept && p.Anchor >= 0 && !pieces[p.Anchor].Kept)
                        {
                            p.Kept = false;
                            pieces[i] = p;
                            any = true;
                        }
                    }
                    if (!any)
                    {
                        return;
                    }
                }
            }

            /// <summary>
            /// The walkway check over everything kept: true if from the door every place to be reached is, and no part of the
            /// floor reached before is closed off. Otherwise the counts, and the first place found wanting.
            /// </summary>
            public bool Check(bool stopAtFirst, out int unreached, out int pockets, out float px, out float pz)
            {
                unreached = pockets = 0;
                px = pz = 0f;
                bool found = false;
                Array.Copy(_fixedClosed, _closed, _closed.Length);
                List<FitPiece> pieces = Into.Pieces;
                for (int i = 0; i < pieces.Count; i++)
                {
                    FitPiece p = pieces[i];
                    if (p.Kept && p.Role == FitRole.Floor)
                    {
                        CloseRect(p.CX - p.HalfX - _margin, p.CZ - p.HalfZ - _margin, p.CX + p.HalfX + _margin, p.CZ + p.HalfZ + _margin, _closed);
                    }
                }
                G.Reach(Site.DoorX, Site.DoorZ, _closed, _after);
                // The places the site already had to be reached.
                for (int i = 0; i < _keepValid.Length; i++)
                {
                    FitSpot s = Site.Keep[i];
                    if (!_keepValid[i] || ReachedNear(s.X, s.Z, Math.Max(Reach, s.R)))
                    {
                        continue;
                    }
                    unreached++;
                    if (!found)
                    {
                        found = true;
                        px = s.X;
                        pz = s.Z;
                    }
                    if (stopAtFirst)
                    {
                        return false;
                    }
                }
                // Every place the player's own furniture is used from.
                for (int i = 0; _hasSides && i < _sides.Length; i++)
                {
                    if (!_sides[i] || _after[i])
                    {
                        continue;
                    }
                    unreached++;
                    if (!found)
                    {
                        found = true;
                        G.Centre(i, out px, out pz);
                    }
                    if (stopAtFirst)
                    {
                        return false;
                    }
                }
                // Every seat and every piece somebody walks up to.
                for (int i = 0; i < pieces.Count; i++)
                {
                    FitPiece p = pieces[i];
                    if (!p.Kept || p.Use == FitUse.None || p.Role == FitRole.Lamp || p.Role == FitRole.Hung
                        || ReachedNearRect(p.CX - p.HalfX, p.CZ - p.HalfZ, p.CX + p.HalfX, p.CZ + p.HalfZ, Reach))
                    {
                        continue;
                    }
                    unreached++;
                    if (!found)
                    {
                        found = true;
                        px = p.CX;
                        pz = p.CZ;
                    }
                    if (stopAtFirst)
                    {
                        return false;
                    }
                }
                // Every part of the floor that could be walked to before.
                Array.Clear(_seen, 0, _seen.Length);
                for (int i = 0; i < _base.Length; i++)
                {
                    if (!_base[i] || _closed[i] || _after[i] || _seen[i])
                    {
                        continue;
                    }
                    int cells = Pocket(i, out int c0, out int r0, out int c1, out int r1);
                    if (cells < PocketCells || c1 - c0 < 1 || r1 - r0 < 1)
                    {
                        continue;   // (a gap behind something, a cell wide: nobody's way)
                    }
                    pockets++;
                    if (!found)
                    {
                        found = true;
                        G.Centre(i, out px, out pz);
                    }
                    if (stopAtFirst)
                    {
                        return false;
                    }
                }
                return !found;
            }

            // The part of the floor closed off with a cell: how many cells, and its extent in cells.
            private int Pocket(int start, out int c0, out int r0, out int c1, out int r1)
            {
                int top = 0, count = 0;
                c0 = c1 = start % G.Cols;
                r0 = r1 = start / G.Cols;
                _stack[top++] = start;
                _seen[start] = true;
                while (top > 0)
                {
                    int i = _stack[--top];
                    count++;
                    int c = i % G.Cols, r = i / G.Cols;
                    c0 = Math.Min(c0, c);
                    c1 = Math.Max(c1, c);
                    r0 = Math.Min(r0, r);
                    r1 = Math.Max(r1, r);
                    for (int k = 0; k < 4; k++)
                    {
                        int nc = c + (k == 0 ? 1 : k == 1 ? -1 : 0), nr = r + (k == 2 ? 1 : k == 3 ? -1 : 0);
                        if (nc < 0 || nr < 0 || nc >= G.Cols || nr >= G.Rows)
                        {
                            continue;
                        }
                        int n = nr * G.Cols + nc;
                        if (_base[n] && !_closed[n] && !_after[n] && !_seen[n])
                        {
                            _seen[n] = true;
                            _stack[top++] = n;
                        }
                    }
                }
                return count;
            }

            private bool ReachedNear(float x, float z, float within)
            {
                G.CellOf(x - within, z - within, out int c0, out int r0);
                G.CellOf(x + within, z + within, out int c1, out int r1);
                float sqr = within * within;
                for (int r = Math.Max(0, r0); r <= Math.Min(G.Rows - 1, r1); r++)
                {
                    for (int c = Math.Max(0, c0); c <= Math.Min(G.Cols - 1, c1); c++)
                    {
                        if (!_after[r * G.Cols + c])
                        {
                            continue;
                        }
                        G.Centre(c, r, out float cx, out float cz);
                        if ((cx - x) * (cx - x) + (cz - z) * (cz - z) <= sqr)
                        {
                            return true;
                        }
                    }
                }
                return false;
            }

            // A reached cell within a distance of a rectangle.
            private bool ReachedNearRect(float x0, float z0, float x1, float z1, float within)
            {
                G.CellOf(x0 - within, z0 - within, out int c0, out int r0);
                G.CellOf(x1 + within, z1 + within, out int c1, out int r1);
                var rect = new FitRect(x0, z0, x1, z1, false);
                for (int r = Math.Max(0, r0); r <= Math.Min(G.Rows - 1, r1); r++)
                {
                    for (int c = Math.Max(0, c0); c <= Math.Min(G.Cols - 1, c1); c++)
                    {
                        if (!_after[r * G.Cols + c])
                        {
                            continue;
                        }
                        G.Centre(c, r, out float cx, out float cz);
                        if (rect.Distance(cx, cz) <= within)
                        {
                            return true;
                        }
                    }
                }
                return false;
            }

            /// <summary>The kept floor piece of the design that stands nearest a place, to be left out; -1 for none.</summary>
            public int NearestDroppable(float x, float z, int first)
            {
                int best = -1;
                float least = float.MaxValue;
                for (int i = first; i < Into.Pieces.Count; i++)
                {
                    FitPiece p = Into.Pieces[i];
                    if (!p.Kept || !p.Design || p.Role != FitRole.Floor)
                    {
                        continue;
                    }
                    float dx = Math.Max(0f, Math.Abs(x - p.CX) - p.HalfX), dz = Math.Max(0f, Math.Abs(z - p.CZ) - p.HalfZ);
                    float d = dx * dx + dz * dz;
                    if (d < least)
                    {
                        least = d;
                        best = i;
                    }
                }
                return best;
            }

            /// <summary>The bare floor rooms may be laid out on: reached, a walkway from everything, clear of the doors and of the floor kept free.</summary>
            public void MarkUsable()
            {
                Array.Copy(_base, Usable, Usable.Length);
                bool[] near = _seen;
                Array.Clear(near, 0, near.Length);
                for (int i = 0; i < Site.Fixed.Count; i++)
                {
                    FitRect r = Site.Fixed[i];
                    CloseRect(r.X0 - Aisle, r.Z0 - Aisle, r.X1 + Aisle, r.Z1 + Aisle, near);
                }
                for (int i = 0; i < Site.KeepFree.Count; i++)
                {
                    FitRect r = Site.KeepFree[i];
                    CloseRect(r.X0 - 0.3f, r.Z0 - 0.3f, r.X1 + 0.3f, r.Z1 + 0.3f, near);
                }
                if (_hasSides)
                {
                    var furniture = new bool[near.Length];
                    G.NearFurniture(FromFurniture, furniture);
                    for (int i = 0; i < near.Length; i++)
                    {
                        near[i] |= furniture[i];
                    }
                }
                for (int i = 0; i < Into.Pieces.Count; i++)
                {
                    FitPiece p = Into.Pieces[i];
                    if (p.Kept && (p.Role == FitRole.Floor || p.Role == FitRole.Seat))
                    {
                        CloseRect(p.CX - p.HalfX - Aisle, p.CZ - p.HalfZ - Aisle, p.CX + p.HalfX + Aisle, p.CZ + p.HalfZ + Aisle, near);
                    }
                }
                float h = G.Cell * 0.5f;
                for (int i = 0; i < Usable.Length; i++)
                {
                    if (!Usable[i])
                    {
                        continue;
                    }
                    G.Centre(i, out float x, out float z);
                    Usable[i] = !near[i] && !_narrow[i] && !AtADoor(x - h, z - h, x + h, z + h);
                }
            }

            /// <summary>A rectangle of the bare floor taken: no other room is laid out on it.</summary>
            public void Unmark(float x0, float z0, float x1, float z1)
            {
                bool[] off = _seen;
                Array.Clear(off, 0, off.Length);
                CloseRect(x0, z0, x1, z1, off);
                for (int i = 0; i < Usable.Length; i++)
                {
                    Usable[i] &= !off[i];
                }
            }

            /// <summary>The places somebody walking about goes to: in front of whatever is walked up to, and beside a chair of each group of desks.</summary>
            public void Stops()
            {
                Into.Stops.Clear();
                var groups = new HashSet<long>();
                for (int i = 0; i < Into.Pieces.Count && Into.Stops.Count < 24; i++)
                {
                    FitPiece p = Into.Pieces[i];
                    if (!p.Kept || p.Role == FitRole.Lamp || p.Role == FitRole.Hung)
                    {
                        continue;
                    }
                    if (p.Use == FitUse.Stop)
                    {
                        float ahead = p.HalfAlongFront + 0.55f;
                        Into.Stops.Add(new FitSpot(p.CX + p.FrontX * ahead, p.CZ + p.FrontZ * ahead, 0f));
                        continue;
                    }
                    if (p.Role != FitRole.Seat || (p.Use != FitUse.Desk && p.Use != FitUse.Meeting))
                    {
                        continue;
                    }
                    // One for each room of the plan's, and each six metres square of the design's: a word with a colleague.
                    long group = p.Design ? 1000000L + (long)Math.Floor(p.CX / 6f) * 1000L + (long)Math.Floor(p.CZ / 6f) : p.Zone * 8L + (long)p.Use;
                    if (groups.Add(group))
                    {
                        float sideX = p.FrontZ, sideZ = -p.FrontX;
                        Into.Stops.Add(new FitSpot(p.CX + sideX * 0.75f - p.FrontX * 0.2f, p.CZ + sideZ * 0.75f - p.FrontZ * 0.2f, 0f));
                    }
                }
            }

            // ------------------------------------------------------------ laying pieces out

            /// <summary>
            /// A piece laid out in a box: at (u, v), its front turned to (fu, fv), there by the middle of its floor (centred)
            /// or by its pivot. Its index, or -1 if it would stand at a door or, being tall, in the windows of the street wall.
            /// </summary>
            public int Put(Box b, string prefab, FitRole role, FitUse use, float u, float v, float fu, float fv, int anchor, int zone, bool centred)
            {
                FitSize s = Site.Size(prefab);
                b.Point(u, v, out float px, out float pz);
                b.Dir(fu, fv, out float fx, out float fz);
                float yaw = SitePlanBase.YawOf(fx, fz);
                float sin = (float)Math.Sin(yaw * Math.PI / 180.0), cos = (float)Math.Cos(yaw * Math.PI / 180.0);
                // The drawn middle from the pivot, turned: (x, z) goes to (x cos + z sin, -x sin + z cos).
                float mx = s.CX * cos + s.CZ * sin, mz = -s.CX * sin + s.CZ * cos;
                float cx = px, cz = pz;
                if (centred)
                {
                    px -= mx;
                    pz -= mz;
                }
                else
                {
                    cx += mx;
                    cz += mz;
                }
                float halfX = Math.Abs(cos) * s.W * 0.5f + Math.Abs(sin) * s.D * 0.5f, halfZ = Math.Abs(sin) * s.W * 0.5f + Math.Abs(cos) * s.D * 0.5f;
                bool tall = s.H > 1.3f;
                if (AtADoor(cx - halfX, cz - halfZ, cx + halfX, cz + halfZ) || InNarrow(cx - halfX, cz - halfZ, cx + halfX, cz + halfZ)
                    || (tall && Site.Windows && Facade(cx - halfX, cz - halfZ, cx + halfX, cz + halfZ)))
                {
                    return -1;
                }
                Into.Pieces.Add(new FitPiece
                {
                    Prefab = prefab, Role = role, Use = use, X = px, Z = pz, Yaw = yaw, CX = cx, CZ = cz, HalfX = halfX, HalfZ = halfZ,
                    Height = s.H, Anchor = anchor, Zone = zone, Kept = true, Tall = tall
                });
                return Into.Pieces.Count - 1;
            }

            /// <summary>A piece backing onto the box's wall at u, facing into the box.</summary>
            public int AgainstWall(Box b, string prefab, float u, FitUse use, int zone)
            {
                FitSize s = Site.Size(prefab);
                return Put(b, prefab, FitRole.Floor, use, u, b.WallBack + s.D * 0.5f, 0f, 1f, -1, zone, centred: true);
            }

            /// <summary>Something small on a piece: at a place in that piece's own frame, turned a little more if asked.</summary>
            public int OnTop(int under, string prefab, float localX, float localZ, float turn, float top, FitUse use = FitUse.None)
            {
                if (under < 0)
                {
                    return -1;
                }
                FitPiece d = Into.Pieces[under];
                float sin = (float)Math.Sin(d.Yaw * Math.PI / 180.0), cos = (float)Math.Cos(d.Yaw * Math.PI / 180.0);
                float x = d.X + localX * cos + localZ * sin, z = d.Z - localX * sin + localZ * cos;
                FitSize s = Site.Size(prefab);
                Into.Pieces.Add(new FitPiece
                {
                    Prefab = prefab, Role = FitRole.OnTop, Use = use, X = x, Z = z, Y = top, Yaw = d.Yaw + turn, CX = x, CZ = z, HalfX = s.W * 0.5f, HalfZ = s.D * 0.5f,
                    Height = s.H, Anchor = under, Zone = d.Zone, Kept = true
                });
                return Into.Pieces.Count - 1;
            }

            /// <summary>Something hung on the box's wall at u, at a height; the game side finds the wall's real surface behind it.</summary>
            public void Hang(Box b, string prefab, float u, float height, int zone)
            {
                if (!b.WallV0)
                {
                    return;
                }
                FitSize s = Site.Size(prefab);
                b.Point(u, 0.05f, out float x, out float z);
                b.Dir(0f, 1f, out float fx, out float fz);
                Into.Pieces.Add(new FitPiece
                {
                    Prefab = prefab, Role = FitRole.Hung, X = x, Z = z, Y = height, Yaw = SitePlanBase.YawOf(fx, fz), CX = x, CZ = z,
                    HalfX = Math.Abs(fz) * s.W * 0.5f + 0.05f, HalfZ = Math.Abs(fx) * s.W * 0.5f + 0.05f, Height = s.H, Anchor = -1, Zone = zone, Kept = true
                });
            }

            /// <summary>Floor in a box that no piece of the room being laid out stands on, and not at a door.</summary>
            public bool Free(Box b, float u, float v, float halfU, float halfV, int zone)
            {
                b.Point(u, v, out float x, out float z);
                b.Dir(1f, 0f, out float ax, out float az);
                float hx = Math.Abs(ax) * halfU + Math.Abs(az) * halfV, hz = Math.Abs(az) * halfU + Math.Abs(ax) * halfV;
                for (int i = 0; i < Into.Pieces.Count; i++)
                {
                    FitPiece p = Into.Pieces[i];
                    if (p.Kept && p.Zone == zone && (p.Role == FitRole.Floor || p.Role == FitRole.Seat)
                        && Math.Abs(p.CX - x) < p.HalfX + hx && Math.Abs(p.CZ - z) < p.HalfZ + hz)
                    {
                        return false;
                    }
                }
                return !AtADoor(x - hx, z - hz, x + hx, z + hz) && !InNarrow(x - hx, z - hz, x + hx, z + hz);
            }
        }
    }
}
