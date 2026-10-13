using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Tech
{
    /// <summary>What a piece of the tech sector's buildings is (Tech/Sites).</summary>
    public enum SitePieceKind
    {
        Rack, AiRack, NetworkRack, Cooling, Ups, Tray, Floor, Vent, OpsDesk,     // a server room
        Reception, LogoWall, Sofa, Armchair, LowTable, Plant, Screen,            // a headquarters' lobby
        ClosetRack, Whiteboard, TechBench                                        // a tech firm's office
    }

    /// <summary>One piece placed in a room, in the room's own axes (the game's world axes inside a building). Plain numbers.</summary>
    public struct SitePiece
    {
        public SitePieceKind Kind;
        public float X, Z;           // the middle of its footprint
        public float Y;              // above the floor: 0 for what stands on it
        public int Yaw;              // where its front looks: 0 is +Z, 90 is +X, 180 is -Z, 270 is -X
        public float HalfX, HalfZ;   // its footprint along the room's axes
        public float Length, Width;  // a tray's or a floor panel's own length (its Z) and width (its X); 0 for the model as it is
        public int Row;              // the row of racks it belongs to; -1 for none
        public bool Stands;          // it stands on the floor in anybody's way

        public bool Contains(float x, float z, float margin) => Math.Abs(x - X) <= HalfX + margin && Math.Abs(z - Z) <= HalfZ + margin;
    }

    /// <summary>
    /// The floor of a room as a grid of square cells: outside it (a wall, or no floor), open, or under something of the
    /// player's (the game's furniture they have put there). The game side fills it in once on the way in, from the game's
    /// own navigation mesh and the player's items; everything placed is planned on it. No engine type in it: the plans are
    /// checked in a console test (RealLifeOverhaul.Build/NavCheck/TechSiteCheck.cs).
    /// </summary>
    public sealed class RoomGrid
    {
        public const byte Outside = 0, Open = 1, Furniture = 2;

        private readonly byte[] _cells;
        private int[] _queue;

        public RoomGrid(float minX, float minZ, float maxX, float maxZ, float cell)
        {
            if (cell <= 0.01f || maxX <= minX || maxZ <= minZ)
            {
                throw new ArgumentException("a grid needs a size and a cell");
            }
            MinX = minX;
            MinZ = minZ;
            Cell = cell;
            Cols = Math.Max(1, (int)Math.Ceiling((maxX - minX) / cell));
            Rows = Math.Max(1, (int)Math.Ceiling((maxZ - minZ) / cell));
            _cells = new byte[Cols * Rows];
        }

        public float MinX { get; }
        public float MinZ { get; }
        public float Cell { get; }
        public int Cols { get; }
        public int Rows { get; }
        public int Count => _cells.Length;

        public byte this[int col, int row]
        {
            get => col >= 0 && row >= 0 && col < Cols && row < Rows ? _cells[row * Cols + col] : Outside;
            set
            {
                if (col >= 0 && row >= 0 && col < Cols && row < Rows)
                {
                    _cells[row * Cols + col] = value;
                }
            }
        }

        public byte At(int index) => _cells[index];

        public bool IsOpen(int col, int row) => this[col, row] == Open;

        public void Centre(int col, int row, out float x, out float z)
        {
            x = MinX + (col + 0.5f) * Cell;
            z = MinZ + (row + 0.5f) * Cell;
        }

        public void Centre(int index, out float x, out float z) => Centre(index % Cols, index / Cols, out x, out z);

        public bool CellOf(float x, float z, out int col, out int row)
        {
            col = (int)Math.Floor((x - MinX) / Cell);
            row = (int)Math.Floor((z - MinZ) / Cell);
            return col >= 0 && row >= 0 && col < Cols && row < Rows;
        }

        /// <summary>The open cells that can be walked to from a point, round anything closed: into[cell]. The count; 0 if the point has no open cell within a metre.</summary>
        public int Reach(float x, float z, bool[] closed, bool[] into)
        {
            Array.Clear(into, 0, into.Length);
            int start = NearestOpen(x, z, closed);
            if (start < 0)
            {
                return 0;
            }
            if (_queue == null || _queue.Length < _cells.Length)
            {
                _queue = new int[_cells.Length];
            }
            int head = 0, tail = 0;
            _queue[tail++] = start;
            into[start] = true;
            while (head < tail)
            {
                int i = _queue[head++];
                int c = i % Cols, r = i / Cols;
                for (int k = 0; k < 4; k++)
                {
                    int nc = c + (k == 0 ? 1 : k == 1 ? -1 : 0), nr = r + (k == 2 ? 1 : k == 3 ? -1 : 0);
                    if (nc < 0 || nr < 0 || nc >= Cols || nr >= Rows)
                    {
                        continue;
                    }
                    int n = nr * Cols + nc;
                    if (!into[n] && _cells[n] == Open && (closed == null || !closed[n]))
                    {
                        into[n] = true;
                        _queue[tail++] = n;
                    }
                }
            }
            return tail;
        }

        /// <summary>The open cell nearest a point, within a metre, that is not closed; -1 for none.</summary>
        public int NearestOpen(float x, float z, bool[] closed)
        {
            CellOf(x, z, out int col, out int row);
            int reach = Math.Max(1, (int)Math.Ceiling(1f / Cell));
            int best = -1;
            float bestSqr = float.MaxValue;
            for (int r = row - reach; r <= row + reach; r++)
            {
                for (int c = col - reach; c <= col + reach; c++)
                {
                    if (c < 0 || r < 0 || c >= Cols || r >= Rows)
                    {
                        continue;
                    }
                    int i = r * Cols + c;
                    if (_cells[i] != Open || (closed != null && closed[i]))
                    {
                        continue;
                    }
                    Centre(c, r, out float cx, out float cz);
                    float sqr = (cx - x) * (cx - x) + (cz - z) * (cz - z);
                    if (sqr < bestSqr && sqr <= 1.0001f + Cell * Cell)
                    {
                        bestSqr = sqr;
                        best = i;
                    }
                }
            }
            return best;
        }

        /// <summary>The cells within a distance of something of the player's: into[cell].</summary>
        public void NearFurniture(float within, bool[] into)
        {
            Array.Clear(into, 0, into.Length);
            int reach = (int)Math.Ceiling(within / Cell);
            float sqr = within * within + 1e-4f;
            for (int i = 0; i < _cells.Length; i++)
            {
                if (_cells[i] != Furniture)
                {
                    continue;
                }
                int c = i % Cols, r = i / Cols;
                for (int dr = -reach; dr <= reach; dr++)
                {
                    for (int dc = -reach; dc <= reach; dc++)
                    {
                        int nc = c + dc, nr = r + dr;
                        if (nc >= 0 && nr >= 0 && nc < Cols && nr < Rows && (dc * dc + dr * dr) * Cell * Cell <= sqr)
                        {
                            into[nr * Cols + nc] = true;
                        }
                    }
                }
            }
        }

        /// <summary>The cells under the pieces that stand on the floor, each grown by a margin, closed: into[cell] (what was there is kept).</summary>
        public void Close(IList<SitePiece> pieces, float margin, bool[] into)
        {
            for (int p = 0; pieces != null && p < pieces.Count; p++)
            {
                SitePiece piece = pieces[p];
                if (!piece.Stands)
                {
                    continue;
                }
                CellOf(piece.X - piece.HalfX - margin, piece.Z - piece.HalfZ - margin, out int c0, out int r0);
                CellOf(piece.X + piece.HalfX + margin, piece.Z + piece.HalfZ + margin, out int c1, out int r1);
                for (int r = Math.Max(0, r0); r <= Math.Min(Rows - 1, r1); r++)
                {
                    for (int c = Math.Max(0, c0); c <= Math.Min(Cols - 1, c1); c++)
                    {
                        Centre(c, r, out float x, out float z);
                        if (piece.Contains(x, z, margin))
                        {
                            into[r * Cols + c] = true;
                        }
                    }
                }
            }
        }

        /// <summary>True if every cell whose middle is inside the rectangle is open, allowed and (with <paramref name="reached"/>) reached; and there is at least one.</summary>
        public bool AllOpen(float x0, float z0, float x1, float z1, bool[] barred, bool[] reached = null)
        {
            CellOf(x0, z0, out int c0, out int r0);
            CellOf(x1, z1, out int c1, out int r1);
            int seen = 0;
            for (int r = r0; r <= r1; r++)
            {
                for (int c = c0; c <= c1; c++)
                {
                    Centre(c, r, out float x, out float z);
                    if (x < x0 || x > x1 || z < z0 || z > z1)
                    {
                        continue;
                    }
                    if (c < 0 || r < 0 || c >= Cols || r >= Rows)
                    {
                        return false;
                    }
                    int i = r * Cols + c;
                    if (_cells[i] != Open || (barred != null && barred[i]) || (reached != null && !reached[i]))
                    {
                        return false;
                    }
                    seen++;
                }
            }
            return seen > 0;
        }

        /// <summary>
        /// The largest rectangle of cells that may all be used, by area (the histogram method: one pass a row). False if
        /// there is none. The bounds are the outer edges of its cells.
        /// </summary>
        public bool Largest(bool[] usable, out float x0, out float z0, out float x1, out float z1)
        {
            x0 = z0 = x1 = z1 = 0f;
            var height = new int[Cols];
            var stack = new int[Cols + 1];
            int bestArea = 0, bestC0 = 0, bestC1 = 0, bestR0 = 0, bestR1 = 0;
            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Cols; c++)
                {
                    height[c] = usable[r * Cols + c] ? height[c] + 1 : 0;
                }
                int top = 0;
                for (int c = 0; c <= Cols; c++)
                {
                    int h = c < Cols ? height[c] : 0;
                    while (top > 0 && height[stack[top - 1]] >= h)
                    {
                        int tall = height[stack[--top]];
                        int left = top > 0 ? stack[top - 1] + 1 : 0;
                        int area = tall * (c - left);
                        if (area > bestArea)
                        {
                            bestArea = area;
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
            x0 = MinX + bestC0 * Cell;
            x1 = MinX + (bestC1 + 1) * Cell;
            z0 = MinZ + bestR0 * Cell;
            z1 = MinZ + (bestR1 + 1) * Cell;
            return true;
        }

        /// <summary>Every open cell next to something of the player's that is reached: into[cell] (the places from which it is used).</summary>
        public int FurnitureSides(bool[] reached, bool[] into)
        {
            Array.Clear(into, 0, into.Length);
            int count = 0;
            for (int i = 0; i < _cells.Length; i++)
            {
                if (_cells[i] != Furniture)
                {
                    continue;
                }
                int c = i % Cols, r = i / Cols;
                for (int k = 0; k < 4; k++)
                {
                    int nc = c + (k == 0 ? 1 : k == 1 ? -1 : 0), nr = r + (k == 2 ? 1 : k == 3 ? -1 : 0);
                    if (nc < 0 || nr < 0 || nc >= Cols || nr >= Rows)
                    {
                        continue;
                    }
                    int n = nr * Cols + nc;
                    if (_cells[n] == Open && reached[n] && !into[n])
                    {
                        into[n] = true;
                        count++;
                    }
                }
            }
            return count;
        }

        /// <summary>A point's cell is reached.</summary>
        public bool Reached(float x, float z, bool[] reached)
        {
            return CellOf(x, z, out int c, out int r) && reached[r * Cols + c];
        }
    }

    /// <summary>A planned room: its pieces, and what the walkway check found.</summary>
    public class SitePlanBase
    {
        public readonly List<SitePiece> Pieces = new List<SitePiece>(64);
        public int Unreachable;       // places that have to be reached and could not be, from the door
        public string Why;            // why little or nothing went in; null when it all did

        public int CountOf(SitePieceKind kind)
        {
            int n = 0;
            for (int i = 0; i < Pieces.Count; i++)
            {
                n += Pieces[i].Kind == kind ? 1 : 0;
            }
            return n;
        }

        public virtual void Clear()
        {
            Pieces.Clear();
            Unreachable = 0;
            Why = null;
        }

        /// <summary>The yaw of a facing along the room's axes, to the nearest quarter turn.</summary>
        public static int YawOf(float x, float z)
        {
            if (Math.Abs(x) > Math.Abs(z))
            {
                return x > 0f ? 90 : 270;
            }
            return z >= 0f ? 0 : 180;
        }

        /// <summary>A piece of a width (its own X) and a depth (its own Z) at a point, facing along the room's axes.</summary>
        public static SitePiece Make(SitePieceKind kind, float x, float z, float faceX, float faceZ, float width, float depth, bool stands, int row = -1, float y = 0f)
        {
            int yaw = YawOf(faceX, faceZ);
            bool turned = yaw == 90 || yaw == 270;
            return new SitePiece
            {
                Kind = kind, X = x, Z = z, Y = y, Yaw = yaw, Row = row, Stands = stands,
                HalfX = (turned ? depth : width) * 0.5f, HalfZ = (turned ? width : depth) * 0.5f
            };
        }
    }

    /// <summary>A data centre's server room as planned (<see cref="ServerRoomPlan"/>).</summary>
    public sealed class ServerRoom : SitePlanBase
    {
        public readonly List<float> Patrol = new List<float>(32);   // x, z, x, z: the way a technician walks the aisles
        public float X0, Z0, X1, Z1;      // the hall the rows stand in
        public int Slots, Servers, Accelerators, Network, Rows, Pairs, Cooling, Ups, Trays, Panels;
        public bool HasDesk;
        public float ChairX, ChairZ;      // where the operations desk is worked from
        public bool Carve;                // the rows may be cut out of the floor people walk on: nothing of the player's is cut off by them

        public override void Clear()
        {
            base.Clear();
            Patrol.Clear();
            X0 = Z0 = X1 = Z1 = 0f;
            Slots = Servers = Accelerators = Network = Rows = Pairs = Cooling = Ups = Trays = Panels = 0;
            HasDesk = false;
            Carve = false;
        }
    }

    /// <summary>
    /// The server room of a data centre, laid out from the floor of the warehouse it is fitted into (a <see cref="RoomGrid"/>
    /// measured on the way in) and from what the data centre has: its tier, its racks of servers and of AI accelerators.
    ///
    ///   the hall      the largest rectangle of open floor that can be walked to from the door, a body's width or more from
    ///                 anything of the player's, leaving the way in clear (a circle round the door and a corridor straight in)
    ///   the rows      along the hall's length, in pairs back to back: each pair's racks face out onto a cold aisle and back
    ///                 onto the hot aisle between them. Tier I is sparse (wide aisles, a gap every four racks), tier IV
    ///                 dense (narrow aisles, rows without a break). A walkway runs all round the rows
    ///   the racks     filled row by row from the door's side as they are bought: the network core rack first (two at tier
    ///                 IV), the racks of servers, then the AI accelerator racks from the start of the next row. More racks
    ///                 than the hall holds show as a full hall in their proportions; at most <see cref="MostShown"/>
    ///   cooling       a cooling unit at the far end of each pair's hot aisle (one for every two pairs at tier I, a spare at
    ///                 tier III, and a second set at the near end at tier IV: the redundancy of the tier)
    ///   power         battery cabinets against a side wall (one at tier I, two at II), against both at III and IV (A and B
    ///                 feeds), four on each side at IV
    ///   overhead      a cable tray over each row of racks; the raised floor under the whole hall, perforated over the cold aisles
    ///   the desk      a small operations desk beside the door, worked from the door's side
    ///
    /// Then the walkway check: from the door every aisle's ends and the desk's chair can be walked to round the racks. And
    /// whether the rows may be cut out of the floor people walk on: only if every place from which the player's own
    /// furniture is used can still be reached round them; otherwise they are only to be seen.
    /// </summary>
    public static class ServerRoomPlan
    {
        public const int MostShown = 240;
        public const int PairsMost = 8;
        public const float RackWidth = 0.6f, RackDepth = 1.07f, AiDepth = 1.2f, RowDepth = 1.2f;
        public const float CoolingWidth = 1.8f, CoolingDepth = 0.9f, UpsWidth = 0.8f, UpsDepth = 0.85f, UpsBand = 0.9f, UpsStep = 0.9f;
        public const float TrayHeight = 2.45f, TrayWidth = 0.4f, TrayMost = 2.0f, Panel = 2.4f, VentWidth = 0.6f;
        public const float DeskWidth = 1.6f, DeskDepth = 0.8f;
        public const int PanelsMost = 150;
        public const float DoorClear = 2.2f, CorridorHalf = 0.8f, CorridorLength = 3.2f, FromFurniture = 0.6f, Body = 0.22f;
        public const float HallLeast = 3.2f;

        public static readonly float[] Walk = { 1.3f, 1.2f, 1.1f, 1.0f };        // the walkway round the rows, by tier
        public static readonly float[] ColdAisle = { 1.8f, 1.5f, 1.3f, 1.2f };
        public static readonly float[] HotAisle = { 1.2f, 1.1f, 1.0f, 0.9f };
        public static readonly int[] Group = { 4, 6, 10, 0 };                   // racks between the gaps in a row (0: no gaps)
        public static readonly int[] UpsFirst = { 1, 2, 2, 4 }, UpsSecond = { 0, 0, 2, 4 };

        private struct Line
        {
            public float V;          // the middle of the row's footprint across the hall
            public int Face;         // its fronts look this way across the hall: -1 or +1
            public int Pair;
            public int First, Count; // its slots in the list of slots
        }

        private struct Slot
        {
            public int Line;
            public float U;
            public bool FirstOfLine;
        }

        public static void Plan(RoomGrid g, float doorX, float doorZ, float inX, float inZ, int tier, int racks, int accelerators, ServerRoom into)
        {
            into.Clear();
            int t = Math.Max(0, Math.Min(3, tier));
            racks = Math.Max(0, racks);
            accelerators = Math.Max(0, accelerators);
            Normalise(ref inX, ref inZ);
            int n = g.Count;
            var near = new bool[n];
            var before = new bool[n];
            var usable = new bool[n];
            g.NearFurniture(FromFurniture, near);
            if (g.Reach(doorX, doorZ, null, before) == 0)
            {
                into.Why = "no floor at the door";
                return;
            }
            for (int i = 0; i < n; i++)
            {
                g.Centre(i, out float x, out float z);
                usable[i] = before[i] && !near[i] && !Reserved(x, z, doorX, doorZ, inX, inZ);
            }
            if (!g.Largest(usable, out float x0, out float z0, out float x1, out float z1) || x1 - x0 < HallLeast || z1 - z0 < HallLeast)
            {
                into.Why = "no open floor of " + HallLeast.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + " m square away from the door and the furniture";
                Desk(g, doorX, doorZ, inX, inZ, near, before, into);
                return;
            }
            into.X0 = x0;
            into.Z0 = z0;
            into.X1 = x1;
            into.Z1 = z1;

            // The hall's own axes: u along its length from the end nearer the door, v across it.
            bool alongX = x1 - x0 >= z1 - z0;
            float ux, uz, vx = 0f, vz = 0f, ox, oz, length, width;
            if (alongX)
            {
                bool fromLow = Math.Abs(doorX - x0) <= Math.Abs(doorX - x1);
                ux = fromLow ? 1f : -1f;
                uz = 0f;
                vz = 1f;
                ox = fromLow ? x0 : x1;
                oz = z0;
                length = x1 - x0;
                width = z1 - z0;
            }
            else
            {
                bool fromLow = Math.Abs(doorZ - z0) <= Math.Abs(doorZ - z1);
                ux = 0f;
                uz = fromLow ? 1f : -1f;
                vx = 1f;
                ox = x0;
                oz = fromLow ? z0 : z1;
                length = z1 - z0;
                width = x1 - x0;
            }
            var hall = new Hall { Ux = ux, Uz = uz, Vx = vx, Vz = vz, Ox = ox, Oz = oz };
            float walk = Walk[t], cold = ColdAisle[t], hot = HotAisle[t];
            int upsFirst = UpsFirst[t], upsSecond = UpsSecond[t];
            float side0 = walk + (upsFirst > 0 ? UpsBand : 0f), side1 = walk + (upsSecond > 0 ? UpsBand : 0f);
            float nearBand = walk + (t == 3 ? CoolingDepth : 0f), farBand = CoolingDepth + walk;
            float uA = nearBand, uB = length - farBand, vA = side0, vB = width - side1;
            if (uB - uA < RackWidth * 2f || vB - vA < RowDepth)
            {
                // Too narrow for the side cabinets: the rows have the whole width, and the power goes.
                side0 = side1 = walk;
                upsFirst = upsSecond = 0;
                vA = side0;
                vB = width - side1;
            }
            if (uB - uA < RackWidth * 2f || vB - vA < RowDepth)
            {
                into.Why = "the hall is too small for a row of racks";
                Desk(g, doorX, doorZ, inX, inZ, near, before, into);
                return;
            }

            // The rows across the hall.
            var lines = new List<Line>(PairsMost * 2);
            float pairWidth = 2f * RowDepth + hot, zone = vB - vA;
            int pairs = zone >= pairWidth ? Math.Min(PairsMost, 1 + (int)((zone - pairWidth) / (pairWidth + cold))) : 0;
            var hotMiddle = new List<float>(PairsMost);
            if (pairs > 0)
            {
                float total = pairs * pairWidth + (pairs - 1) * cold, start = vA + (zone - total) * 0.5f;
                for (int p = 0; p < pairs; p++)
                {
                    float vp = start + p * (pairWidth + cold);
                    lines.Add(new Line { V = vp + RowDepth * 0.5f, Face = -1, Pair = p });
                    lines.Add(new Line { V = vp + RowDepth + hot + RowDepth * 0.5f, Face = 1, Pair = p });
                    hotMiddle.Add(vp + RowDepth + hot * 0.5f);
                }
            }
            else
            {
                lines.Add(new Line { V = vA + zone * 0.5f, Face = -1, Pair = 0 });
                hotMiddle.Add(vA + zone * 0.5f);
            }
            into.Pairs = pairs;
            into.Rows = lines.Count;

            // The slots along each row, and the order rows are filled in: the one nearest the door's side first.
            var along = new List<float>(64);
            for (float pos = uA; pos + RackWidth <= uB + 1e-4f;)
            {
                along.Add(pos + RackWidth * 0.5f);
                pos += RackWidth;
                if (Group[t] > 0 && along.Count % Group[t] == 0)
                {
                    pos += RackWidth;
                }
            }
            float doorV = (doorX - ox) * vx + (doorZ - oz) * vz;
            var order = new List<int>(lines.Count);
            for (int i = 0; i < lines.Count; i++)
            {
                order.Add(i);
            }
            order.Sort((a, b) =>
            {
                int byDistance = Math.Abs(lines[a].V - doorV).CompareTo(Math.Abs(lines[b].V - doorV));
                return byDistance != 0 ? byDistance : a.CompareTo(b);
            });
            int network = t == 3 && order.Count > 1 ? 2 : 1;
            var slots = new List<Slot>(Math.Min(MostShown, along.Count * lines.Count));
            for (int k = 0; k < order.Count; k++)
            {
                int line = order[k];
                Line l = lines[line];
                l.First = slots.Count;
                for (int s = k < network ? 1 : 0; s < along.Count && slots.Count < MostShown; s++)
                {
                    slots.Add(new Slot { Line = line, U = along[s], FirstOfLine = s == (k < network ? 1 : 0) });
                }
                l.Count = slots.Count - l.First;
                lines[line] = l;
            }
            into.Slots = slots.Count;

            // How many of each are shown: all of them while they fit, else a full hall in their proportions.
            int servers = racks, ai = accelerators;
            if (racks + accelerators > slots.Count)
            {
                servers = (int)Math.Round((double)slots.Count * racks / (racks + accelerators));
                ai = slots.Count - servers;
            }
            int aiStart = servers;
            for (int s = servers; s < slots.Count && ai > 0; s++)
            {
                if (slots[s].FirstOfLine && s + ai <= slots.Count)
                {
                    aiStart = s;
                    break;
                }
            }
            if (aiStart + ai > slots.Count)
            {
                aiStart = slots.Count - ai;
            }
            var shownIn = new int[lines.Count];
            var firstU = new float[lines.Count];
            var lastU = new float[lines.Count];
            for (int i = 0; i < lines.Count; i++)
            {
                firstU[i] = float.MaxValue;
                lastU[i] = float.MinValue;
            }
            for (int k = 0; k < network && k < order.Count; k++)
            {
                int line = order[k];
                Rack(into, hall, SitePieceKind.NetworkRack, along[0], lines[line], line);
                Shown(line, along[0], shownIn, firstU, lastU);
                into.Network++;
            }
            for (int s = 0; s < slots.Count; s++)
            {
                bool server = s < servers, accelerator = s >= aiStart && s < aiStart + ai;
                if (!server && !accelerator)
                {
                    continue;
                }
                Slot slot = slots[s];
                Rack(into, hall, accelerator ? SitePieceKind.AiRack : SitePieceKind.Rack, slot.U, lines[slot.Line], slot.Line);
                Shown(slot.Line, slot.U, shownIn, firstU, lastU);
                if (accelerator)
                {
                    into.Accelerators++;
                }
                else
                {
                    into.Servers++;
                }
            }

            // The trays over the rows that have racks.
            for (int i = 0; i < lines.Count; i++)
            {
                if (shownIn[i] == 0)
                {
                    continue;
                }
                float from = firstU[i] - RackWidth * 0.5f, to = lastU[i] + RackWidth * 0.5f, run = to - from;
                int pieces = Math.Max(1, (int)Math.Ceiling(run / TrayMost - 1e-4f));
                for (int k = 0; k < pieces; k++)
                {
                    float u = from + run * (k + 0.5f) / pieces;
                    SitePiece tray = hall.Piece(SitePieceKind.Tray, u, lines[i].V, 1f, 0f, TrayWidth, run / pieces, false, i, TrayHeight);
                    tray.Length = run / pieces;
                    tray.Width = TrayWidth;
                    into.Pieces.Add(tray);
                    into.Trays++;
                }
            }

            // Cooling, by the tier's redundancy, for the pairs that have racks (the first pair if none has).
            var active = new List<int>(PairsMost);
            int groups = Math.Max(1, pairs);
            for (int p = 0; p < groups; p++)
            {
                bool any = false;
                for (int i = 0; i < lines.Count; i++)
                {
                    any |= lines[i].Pair == p && shownIn[i] > 0;
                }
                if (any)
                {
                    active.Add(p);
                }
            }
            if (active.Count == 0)
            {
                active.Add(0);
            }
            var coolingV = new List<float>(PairsMost + 1);
            for (int k = 0; k < active.Count; k++)
            {
                if (t == 0 && k % 2 == 1)
                {
                    continue;
                }
                float v = Clamp(hotMiddle[active[k]], CoolingWidth * 0.5f, width - CoolingWidth * 0.5f);
                coolingV.Add(v);
                into.Pieces.Add(hall.Piece(SitePieceKind.Cooling, length - CoolingDepth * 0.5f, v, -1f, 0f, CoolingWidth, CoolingDepth, true));
                if (t == 3)
                {
                    into.Pieces.Add(hall.Piece(SitePieceKind.Cooling, CoolingDepth * 0.5f, v, 1f, 0f, CoolingWidth, CoolingDepth, true));
                    into.Cooling++;
                }
                into.Cooling++;
            }
            if (t >= 2)
            {
                float spare = pairs >= 2 ? (hotMiddle[0] + hotMiddle[1]) * 0.5f : hotMiddle[0] + 2.4f <= width - CoolingWidth * 0.5f ? hotMiddle[0] + 2.4f : hotMiddle[0] - 2.4f;
                bool free = spare >= CoolingWidth * 0.5f && spare <= width - CoolingWidth * 0.5f;
                for (int k = 0; free && k < coolingV.Count; k++)
                {
                    free = Math.Abs(coolingV[k] - spare) >= CoolingWidth + 0.1f;
                }
                if (free)
                {
                    into.Pieces.Add(hall.Piece(SitePieceKind.Cooling, length - CoolingDepth * 0.5f, spare, -1f, 0f, CoolingWidth, CoolingDepth, true));
                    into.Cooling++;
                }
            }

            // The battery cabinets along the side walls, from the near end.
            for (int side = 0; side < 2; side++)
            {
                int count = side == 0 ? upsFirst : upsSecond;
                float v = side == 0 ? UpsDepth * 0.5f : width - UpsDepth * 0.5f;
                for (int k = 0; k < count; k++)
                {
                    float u = uA + UpsWidth * 0.5f + k * UpsStep;
                    if (u + UpsWidth * 0.5f > uB)
                    {
                        break;
                    }
                    into.Pieces.Add(hall.Piece(SitePieceKind.Ups, u, v, 0f, side == 0 ? 1f : -1f, UpsWidth, UpsDepth, true));
                    into.Ups++;
                }
            }

            // The raised floor under the whole hall, a panel of four tiles by four, larger panels in a very large hall.
            int scale = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(length * width / (PanelsMost * Panel * Panel))));
            int nu = Math.Max(1, (int)Math.Ceiling(length / (Panel * scale) - 1e-4f)), nv = Math.Max(1, (int)Math.Ceiling(width / (Panel * scale) - 1e-4f));
            for (int a = 0; a < nu; a++)
            {
                for (int b = 0; b < nv; b++)
                {
                    SitePiece floor = hall.Piece(SitePieceKind.Floor, length * (a + 0.5f) / nu, width * (b + 0.5f) / nv, 1f, 0f, width / nv, length / nu, false);
                    floor.Length = length / nu;
                    floor.Width = width / nv;
                    into.Pieces.Add(floor);
                    into.Panels++;
                }
            }

            // The cold aisles: perforated tiles down their middle, and the technicians' way along them.
            // (the outer ones just in front of the outermost rows, within the walkway or the floor left over beside it)
            var aisles = new List<float>(PairsMost + 1);
            float outer = Math.Min(cold, walk) * 0.5f;
            aisles.Add(lines[0].V - RowDepth * 0.5f - outer);
            for (int p = 0; p + 1 < pairs; p++)
            {
                aisles.Add((lines[2 * p + 1].V + RowDepth * 0.5f + lines[2 * p + 2].V - RowDepth * 0.5f) * 0.5f);
            }
            if (pairs > 0)
            {
                aisles.Add(lines[lines.Count - 1].V + RowDepth * 0.5f + outer);
            }
            float ventRun = uB - uA;
            int vents = Math.Max(1, (int)Math.Ceiling(ventRun / (Panel * scale) - 1e-4f));
            for (int k = 0; k < aisles.Count; k++)
            {
                for (int s = 0; s < vents; s++)
                {
                    SitePiece vent = hall.Piece(SitePieceKind.Vent, uA + ventRun * (s + 0.5f) / vents, aisles[k], 1f, 0f, VentWidth, ventRun / vents, false);
                    vent.Length = ventRun / vents;
                    vent.Width = VentWidth;
                    into.Pieces.Add(vent);
                }
            }
            float u0 = uA - walk * 0.5f, u1 = uB + walk * 0.5f;
            int walked = 0;
            for (int k = 0; k < aisles.Count; k++)
            {
                if (!AisleUsed(aisles[k], lines, shownIn) && !(k == 0 && into.Servers + into.Accelerators == 0))
                {
                    continue;
                }
                bool outward = walked % 2 == 0;
                hall.Add(into.Patrol, outward ? u0 : u1, aisles[k]);
                hall.Add(into.Patrol, outward ? u1 : u0, aisles[k]);
                walked++;
            }

            Desk(g, doorX, doorZ, inX, inZ, near, before, into);
            Check(g, doorX, doorZ, before, into);
        }

        // The way in, kept clear: round the door and straight in from it.
        private static bool Reserved(float x, float z, float doorX, float doorZ, float inX, float inZ)
        {
            float dx = x - doorX, dz = z - doorZ;
            if (dx * dx + dz * dz < DoorClear * DoorClear)
            {
                return true;
            }
            float ahead = dx * inX + dz * inZ, aside = dx * inZ - dz * inX;
            return ahead >= -0.3f && ahead <= CorridorLength && Math.Abs(aside) <= CorridorHalf;
        }

        private static bool AisleUsed(float v, List<Line> lines, int[] shownIn)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                float front = lines[i].V + lines[i].Face * RowDepth * 0.5f;
                if (shownIn[i] > 0 && Math.Abs(front - v) < 1.5f)
                {
                    return true;
                }
            }
            return false;
        }

        private static void Shown(int line, float u, int[] count, float[] first, float[] last)
        {
            count[line]++;
            first[line] = Math.Min(first[line], u);
            last[line] = Math.Max(last[line], u);
        }

        // A rack in a row, its front flush with the row's cold-aisle edge.
        private static void Rack(ServerRoom into, Hall hall, SitePieceKind kind, float u, Line line, int index)
        {
            float depth = kind == SitePieceKind.AiRack ? AiDepth : RackDepth;
            float v = line.V + line.Face * (RowDepth - depth) * 0.5f;
            into.Pieces.Add(hall.Piece(kind, u, v, 0f, line.Face, RackWidth, depth, true, index));
        }

        // The operations desk beside the door: worked from the door's side, facing into the room, clear of the way in.
        private static void Desk(RoomGrid g, float doorX, float doorZ, float inX, float inZ, bool[] near, bool[] reached, ServerRoom into)
        {
            float rx = inZ, rz = -inX;   // to the right of somebody walking in
            var closed = new bool[g.Count];
            g.Close(into.Pieces, Body, closed);
            for (int i = 0; i < closed.Length; i++)
            {
                closed[i] |= near[i] || !reached[i];
            }
            float[] asides = { 2.6f, -2.6f, 3.3f, -3.3f, 4.0f, -4.0f };
            float[] depths = { 1.7f, 2.3f, 1.3f };
            foreach (float d in depths)
            {
                foreach (float a in asides)
                {
                    float x = doorX + inX * d + rx * a, z = doorZ + inZ * d + rz * a;
                    float cx = doorX + inX * (d - DeskDepth) + rx * a, cz = doorZ + inZ * (d - DeskDepth) + rz * a;
                    SitePiece desk = SitePlanBase.Make(SitePieceKind.OpsDesk, x, z, -inX, -inZ, DeskWidth, DeskDepth, true);
                    SitePiece chair = SitePlanBase.Make(SitePieceKind.OpsDesk, cx, cz, -inX, -inZ, DeskWidth, DeskDepth, false);
                    if (g.AllOpen(desk.X - desk.HalfX, desk.Z - desk.HalfZ, desk.X + desk.HalfX, desk.Z + desk.HalfZ, closed)
                        && g.AllOpen(chair.X - chair.HalfX, chair.Z - chair.HalfZ, chair.X + chair.HalfX, chair.Z + chair.HalfZ, closed))
                    {
                        into.Pieces.Add(desk);
                        into.HasDesk = true;
                        into.ChairX = cx;
                        into.ChairZ = cz;
                        return;
                    }
                }
            }
        }

        // From the door: every aisle's ends and the desk's chair, round everything that stands. The desk goes if its chair
        // cannot be reached. The rows may be cut out of the floor only if the player's own furniture can all still be used.
        private static void Check(RoomGrid g, float doorX, float doorZ, bool[] before, ServerRoom into)
        {
            var closed = new bool[g.Count];
            var after = new bool[g.Count];
            g.Close(into.Pieces, Body, closed);
            g.Reach(doorX, doorZ, closed, after);
            for (int i = 0; i + 1 < into.Patrol.Count; i += 2)
            {
                into.Unreachable += g.Reached(into.Patrol[i], into.Patrol[i + 1], after) ? 0 : 1;
            }
            if (into.HasDesk && !g.Reached(into.ChairX, into.ChairZ, after))
            {
                into.Pieces.RemoveAll(p => p.Kind == SitePieceKind.OpsDesk);
                into.HasDesk = false;
            }
            var sides = new bool[g.Count];
            g.FurnitureSides(before, sides);
            bool cut = false;
            for (int i = 0; i < sides.Length && !cut; i++)
            {
                cut = sides[i] && !after[i];
            }
            into.Carve = !cut;
        }

        internal static float Clamp(float value, float least, float most) => value < least ? least : value > most ? most : value;

        internal static void Normalise(ref float x, ref float z)
        {
            // To the room's nearest axis: the rooms of the game's buildings are square to the world.
            if (Math.Abs(x) >= Math.Abs(z))
            {
                x = x >= 0f ? 1f : -1f;
                z = 0f;
            }
            else
            {
                z = z >= 0f ? 1f : -1f;
                x = 0f;
            }
        }

        // A hall's axes: u along it from the near end, v across it.
        private struct Hall
        {
            public float Ux, Uz, Vx, Vz, Ox, Oz;

            public SitePiece Piece(SitePieceKind kind, float u, float v, float faceU, float faceV, float width, float depth, bool stands, int row = -1, float y = 0f)
            {
                float x = Ox + Ux * u + Vx * v, z = Oz + Uz * u + Vz * v;
                return SitePlanBase.Make(kind, x, z, Ux * faceU + Vx * faceV, Uz * faceU + Vz * faceV, width, depth, stands, row, y);
            }

            public void Add(List<float> into, float u, float v)
            {
                into.Add(Ox + Ux * u + Vx * v);
                into.Add(Oz + Uz * u + Vz * v);
            }
        }
    }
}
