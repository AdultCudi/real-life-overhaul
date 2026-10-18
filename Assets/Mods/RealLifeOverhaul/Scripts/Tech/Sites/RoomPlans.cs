using System;

namespace RealLifeOverhaul.Tech
{
    /// <summary>A headquarters' lobby as planned (<see cref="LobbyPlan"/>).</summary>
    public sealed class Lobby : SitePlanBase
    {
        public bool HasDesk, HasSeating;
        public float StaffX, StaffZ;     // where the receptionist stands, behind the desk
        public float FrontX, FrontZ;     // where a visitor stands at it
        public float Depth;              // how far in the desk is

        public override void Clear()
        {
            base.Clear();
            HasDesk = HasSeating = false;
            StaffX = StaffZ = FrontX = FrontZ = Depth = 0f;
        }
    }

    /// <summary>
    /// A headquarters' lobby laid out on its unit's floor: a reception desk, a logo wall, seating, plants and screens, then the
    /// walkway check that keeps the desk and every seat reachable from the door.
    /// </summary>
    public static class LobbyPlan
    {
        public const float DeskWidth = 2.6f, DeskDepth = 0.9f, LogoWidth = 3.4f, LogoDepth = 0.3f;
        public const float SofaWidth = 2.2f, SofaDepth = 0.95f, ChairSize = 0.9f, TableWidth = 1.1f, TableDepth = 0.6f, PlantSize = 0.6f;
        public const float Corridor = 1.1f, Lane = 1.0f, Body = 0.22f, Step = 0.3f;

        public static void Plan(RoomGrid g, float doorX, float doorZ, float inX, float inZ, Lobby into)
        {
            into.Clear();
            ServerRoomPlan.Normalise(ref inX, ref inZ);
            var reached = new bool[g.Count];
            if (g.Reach(doorX, doorZ, null, reached) == 0)
            {
                into.Why = "no floor at the door";
                return;
            }
            var frame = new Frame { Dx = doorX, Dz = doorZ, Ix = inX, Iz = inZ, G = g, Reached = reached };
            float run = frame.Run(0f, 0.6f, 0f, 1f, 40f) + 0.6f;
            if (run < 4.4f)
            {
                into.Why = "the room is " + run.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + " m deep";
                float wall = run - 0.25f;
                if (wall > 1.8f && frame.Extent(1, wall) >= LogoWidth * 0.5f + 0.1f && frame.Extent(-1, wall) >= LogoWidth * 0.5f + 0.1f)
                {
                    frame.Try(into, SitePieceKind.LogoWall, 0f, wall, 0f, -1f, LogoWidth, LogoDepth, true);
                }
                Plants(frame, into, wall, wall);
                return;
            }
            float depth = ServerRoomPlan.Clamp(run * 0.42f, 3.0f, 6.0f);
            float left = frame.Extent(-1, depth), right = frame.Extent(1, depth);
            into.Depth = depth;
            if (left >= DeskWidth * 0.5f && right >= DeskWidth * 0.5f && Math.Max(left, right) >= DeskWidth * 0.5f + Lane
                && frame.Try(into, SitePieceKind.Reception, 0f, depth, 0f, -1f, DeskWidth, DeskDepth, true))
            {
                into.HasDesk = true;
                frame.Point(0f, depth + 0.85f, out into.StaffX, out into.StaffZ);
                frame.Point(0f, depth - DeskDepth * 0.5f - 0.45f, out into.FrontX, out into.FrontZ);
            }
            float logo = depth + 2.0f;
            if (logo + 0.35f > run)
            {
                logo = run - 0.25f;
            }
            if (logo >= depth + 1.35f && frame.Extent(1, logo) >= LogoWidth * 0.5f + 0.1f && frame.Extent(-1, logo) >= LogoWidth * 0.5f + 0.1f)
            {
                frame.Try(into, SitePieceKind.LogoWall, 0f, logo, 0f, -1f, LogoWidth, LogoDepth, true);
            }

            // the seating group against the wider side
            float seats = Math.Max(1.9f, depth - 0.6f);
            int side = frame.Extent(1, seats) >= frame.Extent(-1, seats) ? 1 : -1;
            float reach = Math.Min(frame.Extent(side, seats), Math.Min(frame.Extent(side, seats - 0.9f), frame.Extent(side, seats + 0.9f)));
            int before = into.Pieces.Count;
            if (reach >= 4.2f)
            {
                bool sofa = frame.Try(into, SitePieceKind.Sofa, side * (reach - SofaDepth * 0.5f - 0.05f), seats, -side, 0f, SofaWidth, SofaDepth, true);
                bool table = sofa && frame.Try(into, SitePieceKind.LowTable, side * (reach - 1.6f), seats, -side, 0f, TableWidth, TableDepth, true);
                bool one = table && frame.Try(into, SitePieceKind.Armchair, side * (reach - 2.6f), seats - 0.6f, side, 0f, ChairSize, ChairSize, true);
                bool two = one && frame.Try(into, SitePieceKind.Armchair, side * (reach - 2.6f), seats + 0.6f, side, 0f, ChairSize, ChairSize, true);
                into.HasSeating = two;
                if (!two)
                {
                    into.Pieces.RemoveRange(before, into.Pieces.Count - before);
                }
            }
            Plants(frame, into, depth, logo);

            // screens across from the seating at the desk's depth, and over the sofa
            float across = frame.Extent(-side, depth);
            if (across > 1.5f)
            {
                frame.Wall(into, SitePieceKind.Screen, -side * across, depth, side, 1.55f, 1.6f);
            }
            if (into.HasSeating)
            {
                frame.Wall(into, SitePieceKind.Screen, side * reach, seats, -side, 1.75f, 1.6f);
            }

            Check(frame, into, side, reach, seats);
        }

        private static void Plants(Frame frame, Lobby into, float depth, float logo)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                float door = frame.Extent(side, 0.9f);
                if (door >= 1.2f + PlantSize)
                {
                    frame.Try(into, SitePieceKind.Plant, side * (door - PlantSize * 0.5f - 0.05f), 0.9f, 0f, 1f, PlantSize, PlantSize, true, true);
                }
                if (into.CountOf(SitePieceKind.LogoWall) > 0)
                {
                    frame.Try(into, SitePieceKind.Plant, side * (LogoWidth * 0.5f + 0.55f), logo, 0f, -1f, PlantSize, PlantSize, true, true);
                }
                float beside = frame.Extent(side, depth);
                if (beside >= DeskWidth * 0.5f + Lane + PlantSize + 0.2f)
                {
                    frame.Try(into, SitePieceKind.Plant, side * (beside - PlantSize * 0.5f - 0.05f), depth, 0f, -1f, PlantSize, PlantSize, true, true);
                }
            }
        }

        // From the door: the desk's front, the receptionist's place and every seat's front; what closes a way goes, seating
        // first, then the logo wall, then the desk.
        private static void Check(Frame frame, Lobby into, int side, float reach, float seats)
        {
            for (int round = 0; round < 4; round++)
            {
                var closed = new bool[frame.G.Count];
                var after = new bool[frame.G.Count];
                frame.G.Close(into.Pieces, Body, closed);
                frame.G.Reach(frame.Dx, frame.Dz, closed, after);
                int missing = 0;
                bool seating = true, staff = true;
                if (into.HasSeating)
                {
                    // the sofa is reached from beside the table's end, the armchairs from behind them
                    frame.Point(side * (reach - 1.3f), seats + 0.95f, out float sx, out float sz);
                    frame.Point(side * (reach - 3.45f), seats - 0.6f, out float ax, out float az);
                    frame.Point(side * (reach - 3.45f), seats + 0.6f, out float bx, out float bz);
                    seating = frame.G.Reached(sx, sz, after) && frame.G.Reached(ax, az, after) && frame.G.Reached(bx, bz, after);
                    missing += seating ? 0 : 1;
                }
                if (into.HasDesk)
                {
                    staff = frame.G.Reached(into.StaffX, into.StaffZ, after);
                    missing += staff ? 0 : 1;
                    missing += frame.G.Reached(into.FrontX, into.FrontZ, after) ? 0 : 1;
                }
                into.Unreachable = missing;
                if (missing == 0)
                {
                    return;
                }
                if (!seating)
                {
                    into.Pieces.RemoveAll(p => p.Kind == SitePieceKind.Sofa || p.Kind == SitePieceKind.Armchair || p.Kind == SitePieceKind.LowTable
                                               || (p.Kind == SitePieceKind.Screen && p.Y > 1.7f));
                    into.HasSeating = false;
                }
                else if (!staff && into.CountOf(SitePieceKind.LogoWall) > 0)
                {
                    into.Pieces.RemoveAll(p => p.Kind == SitePieceKind.LogoWall);
                }
                else if (into.HasDesk)
                {
                    into.Pieces.RemoveAll(p => p.Kind == SitePieceKind.Reception);
                    into.HasDesk = false;
                }
                else
                {
                    return;
                }
            }
        }

        // The way in as a frame: aside to the right of somebody walking in, depth straight in from the door.
        internal struct Frame
        {
            public float Dx, Dz, Ix, Iz;
            public RoomGrid G;
            public bool[] Reached;

            public void Point(float aside, float depth, out float x, out float z)
            {
                x = Dx + Ix * depth + Iz * aside;
                z = Dz + Iz * depth - Ix * aside;
            }

            // How far the open, reached floor runs from a point in a direction of the frame, metres.
            public float Run(float aside, float depth, float da, float dd, float most)
            {
                float run = 0f;
                while (run < most)
                {
                    Point(aside + da * (run + Step), depth + dd * (run + Step), out float x, out float z);
                    if (!G.Reached(x, z, Reached))
                    {
                        break;
                    }
                    run += Step;
                }
                return run;
            }

            /// <summary>How far the floor reaches to one side (+1 right, -1 left) of the way in at a depth.</summary>
            public float Extent(int side, float depth)
            {
                Point(0f, depth, out float x, out float z);
                return G.Reached(x, z, Reached) ? Run(0f, depth, side, 0f, 30f) + Step * 0.5f : 0f;
            }

            /// <summary>A piece at a place of the frame facing along it (faceA aside, faceD in), if its floor is open, reached and free.</summary>
            public bool Try(SitePlanBase into, SitePieceKind kind, float aside, float depth, float faceA, float faceD, float width, float deep, bool stands, bool keepCorridor = false)
            {
                Point(aside, depth, out float x, out float z);
                float fx = Ix * faceD + Iz * faceA, fz = Iz * faceD - Ix * faceA;
                SitePiece piece = SitePlanBase.Make(kind, x, z, fx, fz, width, deep, stands);
                if (keepCorridor && Math.Abs(aside) - Math.Max(piece.HalfX, piece.HalfZ) < Corridor)
                {
                    return false;
                }
                if (!G.AllOpen(piece.X - piece.HalfX, piece.Z - piece.HalfZ, piece.X + piece.HalfX, piece.Z + piece.HalfZ, null, Reached))
                {
                    return false;
                }
                for (int i = 0; i < into.Pieces.Count; i++)
                {
                    SitePiece other = into.Pieces[i];
                    if (other.Stands && Math.Abs(other.X - piece.X) < other.HalfX + piece.HalfX + 0.2f && Math.Abs(other.Z - piece.Z) < other.HalfZ + piece.HalfZ + 0.2f)
                    {
                        return false;
                    }
                }
                into.Pieces.Add(piece);
                return true;
            }

            /// <summary>A piece hung on the wall at the edge of the open floor, facing back into the room; the game finds the wall's surface itself.</summary>
            public void Wall(SitePlanBase into, SitePieceKind kind, float aside, float depth, int faceA, float height, float width)
            {
                Point(aside, depth, out float x, out float z);
                into.Pieces.Add(SitePlanBase.Make(kind, x, z, Iz * faceA, -Ix * faceA, width, 0.08f, false, -1, height));
            }
        }
    }

    /// <summary>A few tech fittings in the player's tech office, as planned (<see cref="OfficeFitPlan"/>).</summary>
    public sealed class OfficeFit : SitePlanBase
    {
    }

    /// <summary>A tech office's fittings, each backed to bare wall clear of the player's furniture and the door, kept only if every way stays open.</summary>
    public static class OfficeFitPlan
    {
        public const float FromFurniture = 0.9f, FromDoor = 2.5f, Apart = 1.5f, Body = 0.22f, FloorLeast = 20f;   // metres; FloorLeast square metres

        private struct Kind
        {
            public SitePieceKind Piece;
            public float Width, Depth, Front, Height;
            public bool Stands;
        }

        private static readonly Kind[] Kinds =
        {
            new Kind { Piece = SitePieceKind.ClosetRack, Width = 0.6f, Depth = 0.8f, Front = 0.8f, Stands = true },
            new Kind { Piece = SitePieceKind.TechBench, Width = 1.4f, Depth = 0.7f, Front = 0.9f, Stands = true },
            new Kind { Piece = SitePieceKind.Whiteboard, Width = 1.8f, Depth = 0.06f, Front = 1.0f, Height = 0.95f }
        };

        public static void Plan(RoomGrid g, float doorX, float doorZ, int most, OfficeFit into)
        {
            into.Clear();
            int n = g.Count;
            var reached = new bool[n];
            int floor = g.Reach(doorX, doorZ, null, reached);
            if (floor * g.Cell * g.Cell < FloorLeast)
            {
                into.Why = "the office has " + (floor * g.Cell * g.Cell).ToString("F0", System.Globalization.CultureInfo.InvariantCulture) + " square metres of floor";
                return;
            }
            var near = new bool[n];
            g.NearFurniture(FromFurniture, near);
            var sides = new bool[n];
            g.FurnitureSides(reached, sides);
            for (int k = 0; k < Kinds.Length && into.Pieces.Count < most; k++)
            {
                Kind kind = Kinds[k];
                SitePiece best = default;
                float bestScore = -1f;
                for (int i = 0; i < n; i++)
                {
                    if (g.At(i) != RoomGrid.Open || !reached[i] || near[i])
                    {
                        continue;
                    }
                    int c = i % g.Cols, r = i / g.Cols;
                    for (int d = 0; d < 4; d++)
                    {
                        int dc = d == 0 ? 1 : d == 1 ? -1 : 0, dr = d == 2 ? 1 : d == 3 ? -1 : 0;
                        if (g[c + dc, r + dr] != RoomGrid.Outside || !g.IsOpen(c - dc, r - dr))
                        {
                            continue;
                        }
                        g.Centre(c, r, out float x, out float z);
                        float back = g.Cell * 0.5f;
                        float px = x + dc * (back - kind.Depth * 0.5f), pz = z + dr * (back - kind.Depth * 0.5f);
                        SitePiece piece = SitePlanBase.Make(kind.Piece, px, pz, -dc, -dr, kind.Width, kind.Depth, kind.Stands, -1, kind.Height);
                        float frontOffset = kind.Depth * 0.5f + kind.Front * 0.5f;
                        SitePiece front = SitePlanBase.Make(kind.Piece, px - dc * frontOffset, pz - dr * frontOffset, -dc, -dr, kind.Width, kind.Front, false);
                        float score = (px - doorX) * (px - doorX) + (pz - doorZ) * (pz - doorZ);
                        if (score <= bestScore || score < FromDoor * FromDoor || !Apart2(into, piece)
                            || !g.AllOpen(front.X - front.HalfX, front.Z - front.HalfZ, front.X + front.HalfX, front.Z + front.HalfZ, near, reached)
                            || (kind.Stands && !g.AllOpen(piece.X - piece.HalfX, piece.Z - piece.HalfZ, piece.X + piece.HalfX, piece.Z + piece.HalfZ, near, reached)))
                        {
                            continue;
                        }
                        best = piece;
                        bestScore = score;
                    }
                }
                if (bestScore >= 0f)
                {
                    into.Pieces.Add(best);
                }
            }
            if (into.Pieces.Count == 0)
            {
                into.Why = "no stretch of wall with room in front of it";
                return;
            }
            // the walkway check, the last fitting first
            while (into.Pieces.Count > 0)
            {
                var closed = new bool[n];
                var after = new bool[n];
                g.Close(into.Pieces, Body, closed);
                g.Reach(doorX, doorZ, closed, after);
                bool cut = false;
                for (int i = 0; i < n && !cut; i++)
                {
                    cut = sides[i] && !after[i];
                }
                for (int p = 0; p < into.Pieces.Count && !cut; p++)
                {
                    SitePiece piece = into.Pieces[p];
                    float fx = (float)Math.Sin(piece.Yaw * Math.PI / 180.0), fz = (float)Math.Cos(piece.Yaw * Math.PI / 180.0);
                    float ahead = (piece.Yaw == 90 || piece.Yaw == 270 ? piece.HalfX : piece.HalfZ) + 0.45f;
                    cut = !g.Reached(piece.X + fx * ahead, piece.Z + fz * ahead, after);
                }
                if (!cut)
                {
                    return;
                }
                into.Pieces.RemoveAt(into.Pieces.Count - 1);
                into.Unreachable++;
            }
        }

        private static bool Apart2(SitePlanBase into, SitePiece piece)
        {
            for (int i = 0; i < into.Pieces.Count; i++)
            {
                SitePiece other = into.Pieces[i];
                if (Math.Abs(other.X - piece.X) < other.HalfX + piece.HalfX + Apart && Math.Abs(other.Z - piece.Z) < other.HalfZ + piece.HalfZ + Apart)
                {
                    return false;
                }
            }
            return true;
        }
    }

    /// <summary>The tech companies' invented colours, in the order of HiringCatalogue.RivalFirms: the logo wall, the desk's band and the sign.</summary>
    public static class HqColours
    {
        // r, g, b of the main colour, then of the lettering, for each company in turn
        private static readonly float[] Table =
        {
            0.05f, 0.45f, 0.55f, 0.95f, 0.97f, 0.96f,   // Codeharbour Software: teal
            0.16f, 0.47f, 0.85f, 0.97f, 0.98f, 1.00f,   // Skyvault Systems: sky blue
            0.13f, 0.42f, 0.25f, 0.96f, 0.93f, 0.82f,   // Abacus Lane Software: green
            0.93f, 0.45f, 0.10f, 0.10f, 0.10f, 0.12f,   // Shopfront Online: orange
            0.66f, 0.14f, 0.52f, 0.98f, 0.92f, 0.97f,   // Pixel Harbour Games: magenta
            0.36f, 0.24f, 0.64f, 0.96f, 0.95f, 0.99f,   // Tallyrow Analytics: purple
            0.78f, 0.16f, 0.14f, 0.98f, 0.97f, 0.95f,   // Signpost Search: red
            0.50f, 0.58f, 0.10f, 0.08f, 0.10f, 0.06f,   // Circuitry Works: olive
            0.34f, 0.40f, 0.50f, 0.92f, 0.95f, 0.98f,   // Quartzline Semiconductors: slate
            0.08f, 0.15f, 0.40f, 0.86f, 0.74f, 0.40f,   // Ledgerline Payments: navy and gold
            0.88f, 0.62f, 0.10f, 0.12f, 0.10f, 0.08f,   // Signalhill Telecom: amber
            0.05f, 0.62f, 0.72f, 0.04f, 0.10f, 0.14f    // Fibreway Networks: cyan
        };

        public static int Count => Table.Length / 6;

        /// <summary>The main colour (second: false) or the lettering's (true) of the company at an index, as r, g, b.</summary>
        public static void Of(int company, bool second, out float r, out float g, out float b)
        {
            int at = ((company % Count) + Count) % Count * 6 + (second ? 3 : 0);
            r = Table[at];
            g = Table[at + 1];
            b = Table[at + 2];
        }
    }
}
