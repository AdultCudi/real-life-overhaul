using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.NewBuildings
{
    /// <summary>A piece of a showroom's floor as its layout places it: a seat, a desk, a plant, a showcase.</summary>
    public sealed class FloorPiece
    {
        public readonly string Name;   // the item's name without the game's "ba:itemname_" start
        public float X, Z;             // m, in the interior's own coordinates
        public readonly float Yaw;     // degrees about the up axis, as Unity turns a piece
        public bool Fixed;             // attached to another piece (a layout's parent): never moved

        public FloorPiece(string name, float x, float z, float yaw)
        {
            Name = name;
            X = x;
            Z = z;
            Yaw = yaw;
        }
    }

    /// <summary>
    /// The floor of a structure the rules know, in the interior's coordinates: its walls and the places just inside its doors. The
    /// game's M1 (a size M, version 1 unit) is the one a dealership's layout stands in (Manhattan Luxury Cars, Build 3682).
    /// </summary>
    public sealed class FloorRoom
    {
        public readonly float X0, Z0, X1, Z1;   // m: the inside of the walls
        public readonly float[] Doors;          // x, z of a place just inside each door, in pairs

        public FloorRoom(float x0, float z0, float x1, float z1, float[] doors)
        {
            X0 = x0;
            Z0 = z0;
            X1 = x1;
            Z1 = z1;
            Doors = doors;
        }

        /// <summary>The interior of the game's M1 structure: its two doors are on the north wall, 18 m apart (BuildingStructureM1's two Exit zones).</summary>
        public static readonly FloorRoom M1 = new FloorRoom(1030.4f, -178.6f, 1059.6f, -139.4f, new[] { 1036f, -140.6f, 1054f, -140.6f });

        /// <summary>The floor of a structure by its size and version, or null for one the rules do not know.</summary>
        public static FloorRoom For(string size, int version)
        {
            return string.Equals(size, "ba:buildingsize_m", StringComparison.Ordinal) && version == 1 ? M1 : null;
        }
    }

    /// <summary>What was found of a floor's seats, and what was done about it (<see cref="FloorMeasure.Tidy"/>).</summary>
    public sealed class FloorVerdict
    {
        public int Seats;          // seats a player may use that the rules know the footprints of
        public int Boxed;          // of them, seats whose place to stand up reaches no door through a way a person can walk
        public int Moved;          // seats moved to a place that frees them
        public bool DoorsOpen;     // the floor's doors themselves are open ground
        public string First;       // the first seat still boxed in, for the log: its name and place; null for none

        public bool Pass => DoorsOpen && Boxed == 0;
    }

    /// <summary>
    /// A showroom's floor and whether a person who stands up from any of its seats can walk out, game-free (EconomyCheck's
    /// UptownDealerChecks). The game's furniture each carries a box that cuts a hole out of the floor's walkable mesh, and a
    /// person standing up from a seat is put at the seat's own place to stand, a little in front of it. Two chairs set at an angle
    /// before a desk, an armchair before a footstool, leave that place a sliver of floor the mesh does not keep: the player is
    /// stood up with nothing to walk on and cannot move (Crest Motors, 26 and 28 Oct 2026). The mesh keeps a way open only where a
    /// person fits: each box's hole is the box and the agent's radius (0.25 m) more, and a way between two holes is kept only if it is
    /// the agent's width (0.5 m) across, so two boxes must stand 1 m apart: <see cref="Corridor"/> either side of the way's middle.
    /// So the rules cut every piece's box out of the floor by that margin, find the floor that reaches a
    /// door, and ask of each seat whether the nearest open floor to its place to stand (within <see cref="Snap"/>, where the game
    /// finds the nearest walkable point) is of it. A seat that fails is moved the smallest step that frees it and boxes in no other.
    /// COST: nothing per frame; a few milliseconds a measure, and a measure per step tried once a load, only when a seat is boxed in.
    /// </summary>
    public sealed class FloorMeasure
    {
        public const string Layout = "MurrayHillCarDealershipLuxury";   // the game's layout of Manhattan Luxury Cars (structure M1, Build 3682): the only one the footprints were read for
        public const float Corridor = 0.5f;   // m: half the narrowest gap between two boxes the floor must keep (a hole's 0.25 m of the agent's radius each side, and the agent's 0.5 m between)
        public const float Cell = 0.15f;      // m: the grid the floor is measured on (the game's own cells are a sixth of a metre)
        public const float Snap = 0.6f;       // m: how far from a seat's place to stand the nearest open floor may be (where the game puts a person who cannot stand there)
        public const float Reach = 2.4f;      // m: the furthest a seat is moved from where its layout put it (a chair shut in a lounge is led out along the open floor)
        public const float Step = 0.15f;      // m: the steps of that search
        public const float Apart = 0.03f;     // m: the clear space left between a moved seat and the pieces beside it

        private readonly FloorRoom _room;
        private readonly int _w, _h;
        private readonly byte[] _cover;       // how many pieces' boxes, cut out by the corridor, cover each cell (255 for a wall's edge)
        private readonly bool[] _main;        // the cells of the floor that reaches a door
        private readonly int[] _queue;
        private readonly List<float> _boxes = new List<float>(16);   // scratch: a piece's boxes in the room's coordinates (cx, cz, hx, hz, yaw radians)

        public FloorMeasure(FloorRoom room)
        {
            _room = room ?? throw new ArgumentNullException(nameof(room));
            _w = (int)Math.Ceiling((room.X1 - room.X0) / Cell) + 1;
            _h = (int)Math.Ceiling((room.Z1 - room.Z0) / Cell) + 1;
            _cover = new byte[_w * _h];
            _main = new bool[_w * _h];
            _queue = new int[_w * _h];
        }

        // ------------------------------------------------------------------ what a piece is to the floor

        /// <summary>True for a piece the rules know a footprint of (a seat, a desk, a table, a plant, a showcase, a counter).</summary>
        public static bool Known(string name)
        {
            return Shape(name) != null;
        }

        /// <summary>True for a seat a player may rest on: not the chair a member of staff sits at behind a desk.</summary>
        public static bool IsSeat(string name)
        {
            return string.Equals(name, "officechair", StringComparison.Ordinal) || string.Equals(name, "armchair2", StringComparison.Ordinal)
                   || string.Equals(name, "valenciachair", StringComparison.Ordinal) || string.Equals(name, "regularchair", StringComparison.Ordinal);
        }

        /// <summary>How far in front of a seat the game puts a person who stands up from it (the seat's own NavMeshTarget), m.</summary>
        public static float StandAhead(string name)
        {
            return string.Equals(name, "armchair2", StringComparison.Ordinal) ? 0.6f : 0.75f;
        }

        /// <summary>
        /// A piece's boxes cut out of the floor, in the piece's own axes: groups of four (centre x, centre z, half width, half depth),
        /// from the game's own prefabs (NavMeshObstacle, Build 3682), or null for a piece with none.
        /// </summary>
        public static float[] Shape(string name)
        {
            switch (name)
            {
                case "anselmoaf90showcase": return new[] { 0.000f, 0.198f, 1.115f, 2.328f };
                case "armchair2": return new[] { 0.000f, 0.000f, 0.402f, 0.431f };
                case "bima320showcase": return new[] { 0.012f, 0.000f, 1.057f, 2.371f };
                case "cheapcoffeemachine": return new[] { 0.000f, 0.000f, 0.200f, 0.200f };
                case "coffeetable2": return new[] { 0.000f, 0.000f, 0.500f, 0.250f };
                case "computer": return new[] { 0.000f, 0.039f, 0.250f, 0.232f };
                case "counter1": return new[] { 0.000f, 0.010f, 0.500f, 0.510f };
                case "counter2": return new[] { 0.000f, 0.018f, 0.500f, 0.518f };
                case "deliveryspot": return new[] { 0.002f, 0.005f, 0.717f, 0.970f };
                case "deskplant01": return new[] { 0.000f, 0.000f, 0.125f, 0.125f };
                case "deskplant02": return new[] { 0.000f, 0.000f, 0.125f, 0.125f };
                case "drinksfridge": return new[] { 0.000f, 0.000f, 0.250f, 0.250f };
                case "ferdinand112showcase": return new[] { 0.000f, -0.041f, 0.999f, 2.176f };
                case "floorlamp": return new[] { 0.000f, 0.000f, 0.170f, 0.170f };
                case "groundplant02": return new[] { 0.000f, 0.000f, 0.225f, 0.225f };
                case "groundplant03": return new[] { 0.000f, 0.000f, 0.350f, 0.250f };
                case "groundplant04": return new[] { 0.000f, 0.000f, 2.000f, 0.250f };
                case "handtruckspawner": return new[] { 0.000f, 0.026f, 0.370f, 0.317f };
                case "mersaidimgagtshowcase": return new[] { 0.000f, 0.000f, 1.031f, 2.258f };
                case "mersaidis500showcase": return new[] { 0.000f, 0.000f, 1.068f, 2.593f };
                case "missamvillianshowcase": return new[] { 0.000f, 0.000f, 1.000f, 2.500f };
                case "moderncounter": return new[] { 0.000f, 0.000f, 0.501f, 0.500f };
                case "officechair": return new[] { 0.000f, -0.122f, 0.374f, 0.413f };
                case "officechair2": return new[] { 0.000f, 0.013f, 0.330f, 0.310f };
                case "officedesk2left": return new[] { 1.000f, 0.000f, 0.500f, 1.000f, 0.000f, -0.500f, 1.500f, 0.500f };
                case "officedesk2right": return new[] { -1.000f, 0.000f, 0.500f, 1.000f, 0.000f, -0.500f, 1.500f, 0.500f };
                case "paperbin": return new[] { 0.000f, 0.000f, 0.260f, 0.260f };
                case "petrollsfantonshowcase": return new[] { 0.000f, 0.000f, 1.079f, 2.882f };
                case "regularchair": return new[] { 0.001f, -0.022f, 0.256f, 0.289f };
                case "umcnunavutshowcase": return new[] { 0.000f, 0.000f, 1.135f, 2.606f };
                case "valenciachair": return new[] { 0.000f, 0.000f, 0.475f, 0.475f };
                case "valenciaottoman": return new[] { 0.000f, 0.000f, 0.385f, 0.269f };
                case "vordponyshowcase": return new[] { 0.000f, 0.018f, 0.920f, 2.390f };
                case "windowedroomdivider": return new[] { 0.000f, 0.000f, 1.000f, 0.031f };
                default: return null;
            }
        }

        // A piece's boxes in the room's coordinates into _boxes: cx, cz, half width, half depth, yaw in radians.
        private void Boxes(FloorPiece p, float dx, float dz)
        {
            _boxes.Clear();
            float[] shape = Shape(p.Name);
            if (shape == null)
            {
                return;
            }
            float yaw = p.Yaw * (float)Math.PI / 180f, c = (float)Math.Cos(yaw), s = (float)Math.Sin(yaw);
            for (int i = 0; i + 3 < shape.Length; i += 4)
            {
                float lx = shape[i], lz = shape[i + 1];
                _boxes.Add(p.X + dx + lx * c + lz * s);
                _boxes.Add(p.Z + dz - lx * s + lz * c);
                _boxes.Add(shape[i + 2]);
                _boxes.Add(shape[i + 3]);
                _boxes.Add(yaw);
            }
        }

        // The distance from a point to an oriented box, 0 inside it.
        private static float Away(float x, float z, float cx, float cz, float hx, float hz, float yaw)
        {
            float c = (float)Math.Cos(yaw), s = (float)Math.Sin(yaw), wx = x - cx, wz = z - cz;
            float lx = Math.Abs(wx * c - wz * s) - hx, lz = Math.Abs(wx * s + wz * c) - hz;
            lx = lx > 0f ? lx : 0f;
            lz = lz > 0f ? lz : 0f;
            return (float)Math.Sqrt(lx * lx + lz * lz);
        }

        // ------------------------------------------------------------------ the floor

        private int CellOf(float x, float z)
        {
            int i = (int)Math.Round((x - _room.X0) / Cell), j = (int)Math.Round((z - _room.Z0) / Cell);
            return i < 0 || j < 0 || i >= _w || j >= _h ? -1 : j * _w + i;
        }

        private float CentreX(int cell) => _room.X0 + (cell % _w) * Cell;

        private float CentreZ(int cell) => _room.Z0 + (cell / _w) * Cell;

        // Every piece's boxes cut out of the floor by the corridor; the walls' edge; then the floor that reaches a door.
        private bool Lay(IList<FloorPiece> pieces)
        {
            Array.Clear(_cover, 0, _cover.Length);
            for (int j = 0; j < _h; j++)
            {
                for (int i = 0; i < _w; i++)
                {
                    float x = _room.X0 + i * Cell, z = _room.Z0 + j * Cell;
                    if (x < _room.X0 + Corridor || x > _room.X1 - Corridor || z < _room.Z0 + Corridor || z > _room.Z1 - Corridor)
                    {
                        _cover[j * _w + i] = 255;
                    }
                }
            }
            for (int k = 0; k < pieces.Count; k++)
            {
                Boxes(pieces[k], 0f, 0f);
                for (int b = 0; b + 4 < _boxes.Count; b += 5)
                {
                    Cut(_boxes[b], _boxes[b + 1], _boxes[b + 2], _boxes[b + 3], _boxes[b + 4], 1);
                }
            }
            Array.Clear(_main, 0, _main.Length);
            bool open = true;
            for (int d = 0; d + 1 < _room.Doors.Length; d += 2)
            {
                int cell = CellOf(_room.Doors[d], _room.Doors[d + 1]);
                if (cell < 0 || _cover[cell] != 0)
                {
                    open = false;
                    continue;
                }
                Flood(cell);
            }
            return open;
        }

        // A box's corridor-wide margin added to (or taken from) the cover.
        private void Cut(float cx, float cz, float hx, float hz, float yaw, int by)
        {
            float reach = (float)Math.Sqrt(hx * hx + hz * hz) + Corridor;
            int i0 = Math.Max(0, (int)Math.Floor((cx - reach - _room.X0) / Cell)), i1 = Math.Min(_w - 1, (int)Math.Ceiling((cx + reach - _room.X0) / Cell));
            int j0 = Math.Max(0, (int)Math.Floor((cz - reach - _room.Z0) / Cell)), j1 = Math.Min(_h - 1, (int)Math.Ceiling((cz + reach - _room.Z0) / Cell));
            for (int j = j0; j <= j1; j++)
            {
                for (int i = i0; i <= i1; i++)
                {
                    int cell = j * _w + i;
                    if (Away(_room.X0 + i * Cell, _room.Z0 + j * Cell, cx, cz, hx, hz, yaw) <= Corridor && _cover[cell] != 255)
                    {
                        _cover[cell] = (byte)(_cover[cell] + by);
                    }
                }
            }
        }

        // The open floor joined to a cell, marked in _main (4 ways, so a gap one cell wide at a corner is not a way).
        private void Flood(int start)
        {
            if (_main[start])
            {
                return;
            }
            int head = 0, tail = 0;
            _queue[tail++] = start;
            _main[start] = true;
            while (head < tail)
            {
                int cell = _queue[head++];
                int i = cell % _w, j = cell / _w;
                if (i > 0)
                {
                    Take(cell - 1, ref tail);
                }
                if (i < _w - 1)
                {
                    Take(cell + 1, ref tail);
                }
                if (j > 0)
                {
                    Take(cell - _w, ref tail);
                }
                if (j < _h - 1)
                {
                    Take(cell + _w, ref tail);
                }
            }
        }

        private void Take(int cell, ref int tail)
        {
            if (_main[cell] || _cover[cell] != 0)
            {
                return;
            }
            _main[cell] = true;
            _queue[tail++] = cell;
        }

        // Whether the person put at a seat's place to stand reaches a door: the nearest open floor within the snap is of the floor of a door.
        private bool Reaches(FloorPiece seat)
        {
            float yaw = seat.Yaw * (float)Math.PI / 180f, ahead = StandAhead(seat.Name);
            float x = seat.X + ahead * (float)Math.Sin(yaw), z = seat.Z + ahead * (float)Math.Cos(yaw);
            int at = CellOf(x, z);
            if (at >= 0 && _cover[at] == 0)
            {
                return _main[at];
            }
            int reach = (int)Math.Ceiling(Snap / Cell), ci = (int)Math.Round((x - _room.X0) / Cell), cj = (int)Math.Round((z - _room.Z0) / Cell);
            float best = float.MaxValue;
            bool found = false;
            for (int j = cj - reach; j <= cj + reach; j++)
            {
                for (int i = ci - reach; i <= ci + reach; i++)
                {
                    if (i < 0 || j < 0 || i >= _w || j >= _h || _cover[j * _w + i] != 0)
                    {
                        continue;
                    }
                    float dx = _room.X0 + i * Cell - x, dz = _room.Z0 + j * Cell - z, d = dx * dx + dz * dz;
                    if (d > Snap * Snap || d >= best)
                    {
                        continue;
                    }
                    best = d;
                    found = _main[j * _w + i];
                }
            }
            return found;
        }

        // ------------------------------------------------------------------ the verdict, and the tidy

        /// <summary>
        /// Which seats of the floor a person cannot walk away from. Their places in <paramref name="pieces"/> go into
        /// <paramref name="boxed"/> (not cleared); the verdict has the counts.
        /// </summary>
        public FloorVerdict Measure(IList<FloorPiece> pieces, List<int> boxed)
        {
            var verdict = new FloorVerdict();
            if (pieces == null)
            {
                return verdict;
            }
            verdict.DoorsOpen = Lay(pieces);
            for (int i = 0; i < pieces.Count; i++)
            {
                if (!IsSeat(pieces[i].Name))
                {
                    continue;
                }
                verdict.Seats++;
                if (!Reaches(pieces[i]))
                {
                    verdict.Boxed++;
                    boxed?.Add(i);
                    if (verdict.First == null)
                    {
                        verdict.First = pieces[i].Name + " at " + pieces[i].X.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + ", "
                                        + pieces[i].Z.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
                    }
                }
            }
            return verdict;
        }

        /// <summary>
        /// Each seat that is boxed in moved the smallest step that frees it without boxing in another or standing in anything: eight ways
        /// round, ring after ring of <see cref="Step"/> up to <see cref="Reach"/>, the same order every time. Pieces that are fixed
        /// (attached to another) stay. The pieces' places are changed in place; the verdict is of the floor after.
        /// </summary>
        public FloorVerdict Tidy(IList<FloorPiece> pieces)
        {
            var boxed = new List<int>(8);
            FloorVerdict verdict = Measure(pieces, boxed);
            int moved = 0;
            for (int n = 0; n < boxed.Count && verdict.DoorsOpen; n++)
            {
                FloorPiece seat = pieces[boxed[n]];
                if (seat.Fixed)
                {
                    continue;
                }
                float x0 = seat.X, z0 = seat.Z;
                bool done = false;
                int rings = (int)Math.Round(Reach / Step);
                for (int ring = 1; ring <= rings && !done; ring++)
                {
                    for (int way = 0; way < 8 && !done; way++)
                    {
                        float angle = way * (float)Math.PI / 4f, dx = ring * Step * (float)Math.Sin(angle), dz = ring * Step * (float)Math.Cos(angle);
                        seat.X = x0;   // each step is counted from where the layout put the seat, never from the step before
                        seat.Z = z0;
                        if (Stands(pieces, boxed[n], dx, dz) || Crosses(pieces, boxed[n], dx, dz))
                        {
                            continue;
                        }
                        seat.X = x0 + dx;
                        seat.Z = z0 + dz;
                        var again = new List<int>(8);
                        FloorVerdict now = Measure(pieces, again);
                        if (now.DoorsOpen && now.Boxed < verdict.Boxed && !again.Contains(boxed[n]))
                        {
                            verdict = now;
                            moved++;
                            done = true;
                        }
                    }
                }
                if (!done)
                {
                    seat.X = x0;
                    seat.Z = z0;
                }
            }
            if (moved > 0)
            {
                verdict = Measure(pieces, null);
            }
            verdict.Moved = moved;
            return verdict;
        }

        /// <summary>True when a piece stands within <see cref="Apart"/> of another piece or of a wall, as it is placed.</summary>
        public bool Crowded(IList<FloorPiece> pieces, int index)
        {
            return Stands(pieces, index, 0f, 0f);
        }

        // True when the way a seat would be carried to (dx, dz) passes through another piece or a wall (a seat is moved, never lifted over one).
        private bool Crosses(IList<FloorPiece> pieces, int index, float dx, float dz)
        {
            int parts = (int)Math.Ceiling(Math.Sqrt(dx * dx + dz * dz) / (Apart * 2f));
            for (int p = 1; p < parts; p++)
            {
                if (Stands(pieces, index, dx * p / parts, dz * p / parts))
                {
                    return true;
                }
            }
            return false;
        }

        // True when a seat moved by (dx, dz) would stand in another piece's box or outside the walls.
        private bool Stands(IList<FloorPiece> pieces, int index, float dx, float dz)
        {
            Boxes(pieces[index], dx, dz);
            var mine = new List<float>(_boxes);
            for (int b = 0; b + 4 < mine.Count; b += 5)
            {
                float reach = (float)Math.Sqrt(mine[b + 2] * mine[b + 2] + mine[b + 3] * mine[b + 3]);
                if (mine[b] - reach < _room.X0 + Apart || mine[b] + reach > _room.X1 - Apart || mine[b + 1] - reach < _room.Z0 + Apart || mine[b + 1] + reach > _room.Z1 - Apart)
                {
                    return true;
                }
            }
            for (int k = 0; k < pieces.Count; k++)
            {
                if (k == index)
                {
                    continue;
                }
                Boxes(pieces[k], 0f, 0f);
                for (int a = 0; a + 4 < mine.Count; a += 5)
                {
                    for (int b = 0; b + 4 < _boxes.Count; b += 5)
                    {
                        if (Overlap(mine, a, _boxes, b))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        // Two oriented boxes (cx, cz, hx, hz, yaw) closer than Apart, by the separating axis test.
        private static bool Overlap(List<float> a, int ia, List<float> b, int ib)
        {
            float ax = a[ia], az = a[ia + 1], ahx = a[ia + 2] + Apart, ahz = a[ia + 3] + Apart, ay = a[ia + 4];
            float bx = b[ib], bz = b[ib + 1], bhx = b[ib + 2], bhz = b[ib + 3], by = b[ib + 4];
            float ac = (float)Math.Cos(ay), asn = (float)Math.Sin(ay), bc = (float)Math.Cos(by), bsn = (float)Math.Sin(by);
            // each box's two axes in the room's coordinates: local x = (c, -s), local z = (s, c)
            float[] axes = { ac, -asn, asn, ac, bc, -bsn, bsn, bc };
            float dx = bx - ax, dz = bz - az;
            for (int k = 0; k < 8; k += 2)
            {
                float ux = axes[k], uz = axes[k + 1];
                float ra = ahx * Math.Abs(ux * ac - uz * asn) + ahz * Math.Abs(ux * asn + uz * ac);
                float rb = bhx * Math.Abs(ux * bc - uz * bsn) + bhz * Math.Abs(ux * bsn + uz * bc);
                if (Math.Abs(dx * ux + dz * uz) > ra + rb)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>The words of a verdict for Player.log and the census, English.</summary>
        public static string Words(FloorVerdict v)
        {
            if (v == null)
            {
                return "not measured";
            }
            if (!v.DoorsOpen)
            {
                return "a door of the floor is itself blocked";
            }
            string moved = v.Moved > 0 ? v.Moved + " moved to a place where they can be left" : "none needed moving";
            return v.Seats + " seats a player may use, " + v.Boxed + " that a person standing up from could not walk away from" + (v.Boxed > 0 ? " (first: " + v.First + ")" : string.Empty)
                   + ", " + moved;
        }
    }
}
