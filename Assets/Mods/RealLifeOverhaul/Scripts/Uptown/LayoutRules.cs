using System;
using System.Collections.Generic;
using System.Globalization;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// Uptown's layout, the band north of Manhattan behind its old north wall, on the game's own street grid continued: its
    /// streets every 87.3 m in line with Manhattan's (whose road modules stand at those places), its avenues every 147.2 m
    /// north of 1st Avenue (a street module and a crossing, as the game's backdrop already draws them there), three rows of
    /// eighteen blocks between them, and the lots round each block in Manhattan's mix, built up as Manhattan's are (the owner,
    /// 24 Oct 2026: "Like Manhattan"): side by side along every street, the corners taken by the avenues' lots and the
    /// streets' lots back to back across the block, half the band's squares built. The whole band is laid at every load;
    /// its first block is the eight blocks behind 1st Avenue's north side between 9th and 1st Street, where it meets
    /// Manhattan. Every figure is fixed data or worked out from it, so the plan is the same on every machine and in every
    /// version (saves keep lots by id and address). No Unity or game types: checked offline (NavCheck, UptownCheck.cs).
    /// Built once, when first read.
    /// </summary>
    public static class LayoutRules
    {
        // ---- the game's grid at Manhattan's north edge (its road modules' middles, Build 3682), continued north

        public const float StreetFirstX = -301.67f;   // m: 9th Street's module middle, the westmost of Manhattan's streets at 1st Avenue
        public const float StreetPitch = 87.3f;        // m between two streets' middles, the game's own
        public const float FirstAvenueZ = 464.58f;     // m: 1st Avenue's module middle
        public const float AvenuePitch = 147.2f;       // m between avenues: a street module and a crossing module, as the backdrop lays them
        public const float StreetHalf = 8.65f;         // m: half a street module, its pavements in
        public const float StreetKerbWest = 5.05f;     // m west of a street's middle its west kerb runs (its parking lane is on the west)
        public const float StreetKerbEast = 4.95f;     // m east of its middle, the east kerb
        public const float StreetLanes = 1.2f;         // m east of a street's middle the line between its two lanes runs, as the game's
        public const float StreetLaneWidth = 3.8f;     // m between a street's two lanes, the game's
        public const float AvenueHalf = 13.65f;        // m: half an avenue module, its pavements in
        public const float AvenueKerb = 9.95f;         // m from an avenue's middle to its kerbs
        public const float AvenueLaneWidth = 4f;       // m between an avenue's lanes, the game's
        public const float CrossingHalfX = 18.65f;     // m: half a crossing module across its street
        public const float CrossingHalfZ = 23.6f;      // m: half a crossing module along its street
        public const float StreetModule = 100f;        // m: a street module's length
        public const float AvenueModule = 50f;         // m: an avenue module's length
        public const int Rows = 3;                     // rows of blocks north of 1st Avenue
        public const int Columns = 18;                 // blocks west to east, between the band's nineteen streets
        public const int FirstColumn = 6;              // the first block's westmost column: the block east of 9th Street
        public const int FirstColumns = 8;             // ... and its eight blocks, to 1st Street

        // ---- the lots: places.md's footprints of Manhattan's size classes, in its mix

        public const int PlanGeneration = 2;           // the plan's generation, kept in every save with Uptown: a save of an earlier one has its district's records retired
        public const float LotGap = 0f;                // m between two lots: none, Manhattan's buildings stand side by side
        public const float BackGap = 2f;               // m from the backs of 1st Avenue's buildings to the first lots behind them
        public const float FirstNorthDepth = 32f;      // m: the first block's strip along Uptown Avenue (no warehouse) ...
        public const float DeepNorthDepth = 39f;       // m: ... or one deep enough for a warehouse, in its two deepest blocks
        public const float StreetDepth = 35f;          // m: a strip along a street, half a block across: the two streets' lots meet back to back
        public const float AvenueDepth = 39f;          // m: a strip along an avenue beyond the first block, any class, its ends the block's corners
        private static readonly int[] MixShare = { 64, 14, 13, 7 };   // per cent of Manhattan's buildings in each class (the other 2%: theatres, cinemas, towers)
        private static readonly int[] ClassWidth = { 11, 23, 23, 29 };   // m along the street: places.md's typical footprint of each class
        private static readonly int[] ClassDepth = { 24, 32, 31, 39 };   // m back from it

        // The backs of 1st Avenue's north side, block by block east of 9th Street (m, the game's buildings' boxes, Build 3682).
        private static readonly float[] FirstBacks = { 508.8f, 508.5f, 504.7f, 520.0f, 510.8f, 506.9f, 510.7f, 510.8f };

        // Ground the plan keeps for Uptown's big places (Scripts/Uptown/BigPlacesRules): each in one strip of one block, from
        // and to along it (m, x for an avenue's strip, z for a street's) and back from its street's line; the strip's lots stand
        // PlaceClear m off it, and a street's strips of its block end that far short of a place deeper than its avenue's.
        // The wholesale store over block 108's Uptown Avenue front, x -113.4 to -53.4 and back to z 557 (its car park, its
        // yard east of it); the filling station on block 206's corner of 9th Street and Uptown Avenue, x -291.5 to -268.5,
        // 32 m back, as the game's own stand to their streets (BigPlacesRules' measures, Build 3682).
        private static readonly PlaceGround[] PlaceTable =
        {
            new PlaceGround("uptown_place_wholesale", 1, FirstColumn + 2, 0, -113.4f, -53.4f, 557f, LotClass.Warehouse),
            new PlaceGround("uptown_place_fuel", 2, FirstColumn, 180, -291.5f, -268.5f, 32f, LotClass.Office),
        };

        public const float PlaceClear = 1.5f;          // m between a place's ground and the lots round it: a made building's clearance

        // One place's ground as the table gives it: its block, the strip's facing, its run along the strip, and how far back it
        // reaches (a depth in m, or for an avenue's strip a z the place's back stands on when over 100).
        private struct PlaceGround
        {
            public readonly string Id;
            public readonly int Row, Column, Facing;
            public readonly float From, To, Back;
            public readonly LotClass Class;

            public PlaceGround(string id, int row, int column, int facing, float from, float to, float back, LotClass kind)
            {
                Id = id;
                Row = row;
                Column = column;
                Facing = facing;
                From = from;
                To = to;
                Back = back;
                Class = kind;
            }

            // m from its street's line back to its back.
            public float Depth(float front)
            {
                return Back > 100f ? Math.Abs(front - Back) : Back;
            }
        }

        // The first block's two deepest blocks get a strip along the avenue deep enough for a warehouse.
        private static readonly int[] DeepFirst = { FirstColumn + 2, FirstColumn + 5 };

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private static List<Lot> _lots;
        private static List<Lot> _places;
        private static List<Lot> _first;
        private static Dictionary<string, Lot> _byId;
        private static List<GroundRect> _firstGround;
        private static List<GroundRect> _bandGround;

        // ---- the grid

        /// <summary>A street's middle, x: index 0 is 9th Street, -5 to 13 the band's.</summary>
        public static float StreetX(int index)
        {
            return StreetFirstX + StreetPitch * index;
        }

        /// <summary>An avenue's middle, z: row 0 is 1st Avenue, 1 to 3 Uptown's.</summary>
        public static float AvenueZ(int row)
        {
            return FirstAvenueZ + AvenuePitch * row;
        }

        /// <summary>The street west of a column's block (column 1 is the band's westmost block).</summary>
        public static int WestStreet(int column)
        {
            return column - FirstColumn;
        }

        /// <summary>True for a block of the first block: row 1, between 9th and 1st Street.</summary>
        public static bool IsFirst(int row, int column)
        {
            return row == 1 && column >= FirstColumn && column < FirstColumn + FirstColumns;
        }

        /// <summary>
        /// True for a block left as a square (a park or a plaza), no lots on it. The plan's squares are every third block of
        /// rows 2 and 3 on a stagger and every other block of row 1 beyond Manhattan's; of each row's, west to east, every
        /// second is built (the owner, 24 Oct 2026: "build half the squares"), so the parks left are spread along every row.
        /// </summary>
        public static bool IsSquare(int row, int column)
        {
            if (!PlannedSquare(row, column))
            {
                return false;
            }
            int before = 0;
            for (int c = 1; c < column; c++)
            {
                before += PlannedSquare(row, c) ? 1 : 0;
            }
            return before % 2 == 0;
        }

        /// <summary>True for a block the plan first left as a square: built now where <see cref="IsSquare"/> is false.</summary>
        public static bool PlannedSquare(int row, int column)
        {
            if (IsFirst(row, column))
            {
                return false;
            }
            return row == 1 ? column % 2 == 1 : (column * 2 + row) % 3 == 0;
        }

        /// <summary>A block's ground between its streets and avenues: west, south, east, north (m).</summary>
        public static void Block(int row, int column, out float x0, out float z0, out float x1, out float z1)
        {
            int west = WestStreet(column);
            x0 = StreetX(west) + StreetHalf;
            x1 = StreetX(west + 1) - StreetHalf;
            z1 = AvenueZ(row) - AvenueHalf;
            z0 = IsFirst(row, column) ? FirstBacks[column - FirstColumn] + BackGap : AvenueZ(row - 1) + AvenueHalf;
        }

        // ---- the lots

        /// <summary>Every lot of the band, in id order.</summary>
        public static List<Lot> Lots
        {
            get
            {
                Make();
                return _lots;
            }
        }

        /// <summary>
        /// The ground the plan keeps for Uptown's big places (the wholesale store, the filling station), each with its id and
        /// its address in its street's numbers as a lot has; never among <see cref="Lots"/>.
        /// </summary>
        public static List<Lot> Places
        {
            get
            {
                Make();
                return _places;
            }
        }

        /// <summary>The first block's lots, in id order.</summary>
        public static List<Lot> FirstLots
        {
            get
            {
                Make();
                return _first;
            }
        }

        /// <summary>Every lot by its id.</summary>
        public static Dictionary<string, Lot> ById
        {
            get
            {
                Make();
                return _byId;
            }
        }

        /// <summary>The class's footprint along its street and back from it (m): the least a lot of it takes. A lot is as deep as its strip and takes its share of what the strip has over.</summary>
        public static void Footprint(LotClass kind, out int width, out int depth)
        {
            width = ClassWidth[(int)kind];
            depth = ClassDepth[(int)kind];
        }

        /// <summary>Manhattan's share of a class among its buildings of the four classes, per cent of 98.</summary>
        public static int Share(LotClass kind)
        {
            return MixShare[(int)kind];
        }

        // One lot as laid, before its street and number are known.
        private sealed class Laid
        {
            public int Row, Column, InBlock;
            public LotClass Class;
            public float X, Z;
            public int Yaw;
            public string Street;
            public float Along;      // m along its street, for the numbers
            public int Width, Depth; // m: along its street (its class's and its share of what the strip has over), and its strip's depth
            public string Place;     // a place's id (PlaceTable), or null for a lot
            public bool OddSide;     // its street's odd side: a street's west side, an avenue's south side
            public int Number, Order;
        }

        private static void Make()
        {
            if (_lots != null)
            {
                return;
            }
            var laid = new List<Laid>(512);
            var counts = new int[4];
            for (int row = 1; row <= Rows; row++)
            {
                for (int column = 1; column <= Columns; column++)
                {
                    if (IsSquare(row, column))
                    {
                        continue;
                    }
                    LayBlock(row, column, laid, counts);
                }
            }
            Number(laid);
            var lots = new List<Lot>(laid.Count);
            var places = new List<Lot>(PlaceTable.Length);
            var first = new List<Lot>(128);
            var byId = new Dictionary<string, Lot>(laid.Count, StringComparer.Ordinal);
            for (int i = 0; i < laid.Count; i++)
            {
                Laid l = laid[i];
                // Three digits in the block: the first plan's ids had two, so none of its lots is ever taken for one of these.
                string id = l.Place ?? "uptown_" + l.Row.ToString(Inv) + "_" + l.Column.ToString("00", Inv) + "_" + l.InBlock.ToString("000", Inv);
                bool isFirst = IsFirst(l.Row, l.Column);
                var lot = new Lot(id, l.Row, l.Column, l.Street, l.Number, l.Order, l.X, l.Z, l.Yaw, l.Width, l.Depth, l.Class, isFirst);
                if (l.Place != null)
                {
                    places.Add(lot);
                    continue;
                }
                lots.Add(lot);
                byId[id] = lot;
                if (isFirst)
                {
                    first.Add(lot);
                }
            }
            _byId = byId;
            _first = first;
            _places = places;
            _lots = lots;
        }

        // A block's strips: along its avenue to the north, its avenue to the south (beyond row 1), each across the block's
        // whole width so its end lots take the corners, then its two streets' between them, back to back across the block.
        private static void LayBlock(int row, int column, List<Laid> laid, int[] counts)
        {
            Block(row, column, out float x0, out float z0, out float x1, out float z1);
            bool first = IsFirst(row, column);
            int west = WestStreet(column);
            int start = laid.Count;
            float north = first ? (Array.IndexOf(DeepFirst, column) >= 0 ? DeepNorthDepth : FirstNorthDepth) : AvenueDepth;
            Strip(row, column, laid, counts, x0, x1, z1, north, 0, LayoutStreets.Avenue(row).Id);
            float sideFrom = z0;
            if (row > 1)
            {
                Strip(row, column, laid, counts, x0, x1, z0, AvenueDepth, 180, LayoutStreets.Avenue(row - 1).Id);
                sideFrom = z0 + AvenueDepth + LotGap;
            }
            float sideTo = Math.Min(z1 - north - LotGap, PlaceBackZ(row, column, z1) - PlaceClear);
            Strip(row, column, laid, counts, sideFrom, sideTo, x0, StreetDepth, 270, LayoutStreets.Street(west).Id);
            Strip(row, column, laid, counts, sideFrom, sideTo, x1, StreetDepth, 90, LayoutStreets.Street(west + 1).Id);
            int inBlock = 0;
            for (int i = start; i < laid.Count; i++)
            {
                laid[i].InBlock = laid[i].Place != null ? 0 : ++inBlock;
            }
        }

        // One strip of lots along a frontage, from its start to its end: each lot the class furthest below its share of the
        // lots laid so far that fits the strip's depth and what is left of its length, side by side; what the strip has over
        // shared out among its lots in whole metres, the metres left to its end lots, the last part of a metre at its two
        // ends; every lot as deep as the
        // strip. facing: the way the street lies from the lots (0 north, 90 east, 180 south, 270 west); front: the
        // street's line the lots' fronts stand on (z for an avenue, x for a street).
        private static void Strip(int row, int column, List<Laid> laid, int[] counts, float from, float to, float front, float depth, int facing, string street)
        {
            float next = from;
            for (int p = 0; p < PlaceTable.Length; p++)
            {
                PlaceGround g = PlaceTable[p];
                if (g.Row != row || g.Column != column || g.Facing != facing)
                {
                    continue;
                }
                Run(row, column, laid, counts, next, g.From - PlaceClear, front, depth, facing, street);
                Place(g, row, column, laid, front, facing, street);
                next = g.To + PlaceClear;
            }
            Run(row, column, laid, counts, next, to, front, depth, facing, street);
        }

        // A place's ground laid as one entry of its strip, to be numbered with its lots.
        private static void Place(PlaceGround g, int row, int column, List<Laid> laid, float front, int facing, string street)
        {
            float depth = g.Depth(front), along = (g.From + g.To) * 0.5f, half = depth * 0.5f;
            var l = new Laid { Row = row, Column = column, Class = g.Class, Street = street, Place = g.Id, Width = (int)Math.Round(g.To - g.From), Depth = (int)Math.Round(depth) };
            l.X = facing == 0 || facing == 180 ? along : facing == 270 ? front + half : front - half;
            l.Z = facing == 0 ? front - half : facing == 180 ? front + half : along;
            l.X = Round(l.X);
            l.Z = Round(l.Z);
            l.Yaw = (facing + 180) % 360;
            l.OddSide = facing == 90 || facing == 0;
            l.Along = facing == 0 || facing == 180 ? -l.X : l.Z;
            laid.Add(l);
        }

        // The z of the back of a place in a block's north avenue strip (its streets' lots end short of it when it reaches past
        // the strip), else none.
        private static float PlaceBackZ(int row, int column, float front)
        {
            float back = float.MaxValue;
            for (int p = 0; p < PlaceTable.Length; p++)
            {
                PlaceGround g = PlaceTable[p];
                if (g.Row == row && g.Column == column && g.Facing == 0)
                {
                    back = Math.Min(back, front - g.Depth(front));
                }
            }
            return back;
        }

        // One run of a strip's lots between its ends (or a place's ground), as the strip comment says.
        private static void Run(int row, int column, List<Laid> laid, int[] counts, float from, float to, float front, float depth, int facing, string street)
        {
            float room = to - from;
            int begin = laid.Count;
            float used = 0f;
            while (true)
            {
                int pick = Pick(counts, laid.Count, depth, room - used);
                if (pick < 0)
                {
                    break;
                }
                counts[pick]++;
                laid.Add(new Laid { Row = row, Column = column, Class = (LotClass)pick, Along = used + ClassWidth[pick] * 0.5f, Street = street });
                used += ClassWidth[pick] + LotGap;
            }
            if (laid.Count == begin)
            {
                return;
            }
            int count = laid.Count - begin;
            float over = room - (used - LotGap);
            int extra = (int)Math.Floor((over + 0.01f) / count);   // a strip of 70 m is 69.99999 in floats
            int spare = (int)Math.Floor(over - extra * count + 0.01f);   // whole metres left: to the end lots, so the corners are built
            float next = from + (over - extra * count - spare) * 0.5f;
            for (int i = begin; i < laid.Count; i++)
            {
                Laid l = laid[i];
                l.Width = ClassWidth[(int)l.Class] + extra + (i == begin ? spare - spare / 2 : 0) + (i == laid.Count - 1 ? spare / 2 : 0);
                l.Depth = (int)depth;
                float along = next + l.Width * 0.5f;
                next += l.Width + LotGap;
                float half = depth * 0.5f;
                switch (facing)
                {
                    case 0:
                        l.X = along;
                        l.Z = front - half;
                        break;
                    case 180:
                        l.X = along;
                        l.Z = front + half;
                        break;
                    case 270:
                        l.X = front + half;
                        l.Z = along;
                        break;
                    default:
                        l.X = front - half;
                        l.Z = along;
                        break;
                }
                l.X = Round(l.X);
                l.Z = Round(l.Z);
                l.Yaw = (facing + 180) % 360;
                // A street's west side is its odd one (lots facing east toward it), an avenue's south side (lots facing north).
                l.OddSide = facing == 90 || facing == 0;
                l.Along = facing == 0 || facing == 180 ? -l.X : l.Z;   // avenues number from their east end, as 1st Avenue; streets from the south
            }
        }

        // The class furthest below its share that fits; -1 when none fits.
        private static int Pick(int[] counts, int laid, float depth, float room)
        {
            int best = -1;
            float most = float.MinValue;
            for (int c = 0; c < 4; c++)
            {
                if (ClassDepth[c] > depth || ClassWidth[c] > room)
                {
                    continue;
                }
                float below = MixShare[c] * (laid + 1) / 98f - counts[c];
                if (below > most + 0.000001f)
                {
                    most = below;
                    best = c;
                }
            }
            return best;
        }

        // The numbers along each side of each street, from its first number: odd on one side, even on the other.
        private static void Number(List<Laid> laid)
        {
            var order = new List<Laid>(64);
            for (int s = 0; s < LayoutStreets.All.Length; s++)
            {
                LayoutStreet street = LayoutStreets.All[s];
                for (int side = 0; side < 2; side++)
                {
                    order.Clear();
                    for (int i = 0; i < laid.Count; i++)
                    {
                        if (laid[i].Street == street.Id && laid[i].OddSide == (side == 0))
                        {
                            order.Add(laid[i]);
                        }
                    }
                    order.Sort((a, b) => a.Along.CompareTo(b.Along));
                    int firstOnSide = street.FirstNumber + (side == 0 ? 0 : 1);   // 1001 and 1002, or 1 and 2
                    for (int i = 0; i < order.Count; i++)
                    {
                        order[i].Order = i;
                        order[i].Number = firstOnSide + 2 * i;
                    }
                }
            }
        }

        private static float Round(float value)
        {
            return (float)Math.Round(value, 2);
        }

        // ---- the first block's own ground and the way out

        /// <summary>
        /// The ground the first block lays, as rectangles: each block from the backs of 1st Avenue's buildings to Uptown
        /// Avenue, each street from its old wall to the avenue, and the avenue with its crossings between 9th and 1st Street.
        /// The game's own ground of 1st Avenue south of the old wall is never among them.
        /// </summary>
        public static List<GroundRect> FirstGround
        {
            get
            {
                if (_firstGround == null)
                {
                    var rects = new List<GroundRect>(FirstColumns * 2 + 2);
                    float avenue = AvenueZ(1);
                    for (int c = 0; c < FirstColumns; c++)
                    {
                        int column = FirstColumn + c;
                        Block(1, column, out float x0, out _, out float x1, out float z1);
                        rects.Add(new GroundRect(x0, FirstBacks[c], x1, z1));
                    }
                    for (int k = 0; k <= FirstColumns; k++)
                    {
                        rects.Add(new GroundRect(StreetX(k) - StreetHalf, OldWallZ, StreetX(k) + StreetHalf, avenue - CrossingHalfZ));
                    }
                    rects.Add(new GroundRect(StreetX(0) - CrossingHalfX, avenue - CrossingHalfZ, StreetX(FirstColumns) + CrossingHalfX, avenue + CrossingHalfZ));
                    _firstGround = rects;
                }
                return _firstGround;
            }
        }

        /// <summary>
        /// The ground the whole band lays, as rectangles: the first block's (above), the band west of 9th and east of 1st
        /// Street from 1st Avenue's north pavement to Crest Avenue's, the rows north of Uptown Avenue between them, and 1st
        /// Avenue's continuations west and east of its own corners. 1st Avenue's own ground and its north side's buildings
        /// between 9th and 1st Street are never among them.
        /// </summary>
        public static List<GroundRect> BandGround
        {
            get
            {
                if (_bandGround == null)
                {
                    var rects = new List<GroundRect>(FirstGround);
                    float west = StreetX(LayoutStreets.WestMost) - StreetHalf, east = StreetX(LayoutStreets.EastMost) + StreetHalf;
                    float x0 = StreetX(0) - StreetHalf, x1 = StreetX(FirstColumns) + StreetHalf;
                    float row = AvenueZ(0) + AvenueHalf, north = AvenueZ(Rows) + AvenueHalf, south = AvenueZ(0) - AvenueHalf;
                    rects.Add(new GroundRect(west, row, x0, north));
                    rects.Add(new GroundRect(x1, row, east, north));
                    rects.Add(new GroundRect(x0, AvenueZ(1) - CrossingHalfZ, x1, north));
                    rects.Add(new GroundRect(west, south, StreetX(0) - CrossingHalfX, row));
                    rects.Add(new GroundRect(StreetX(FirstColumns) + CrossingHalfX, south, east, row));
                    _bandGround = rects;
                }
                return _bandGround;
            }
        }

        /// <summary>True for a point on the band's ground.</summary>
        public static bool OnGround(float x, float z)
        {
            List<GroundRect> rects = BandGround;
            for (int i = 0; i < rects.Count; i++)
            {
                if (rects[i].Holds(x, z))
                {
                    return true;
                }
            }
            return false;
        }

        public const float OldWallZ = 480.5f;   // m: the south face of the game's old walls across the street ends; north of it was backdrop

        /// <summary>The way out: a parking place on 1st Avenue's north side mid-block between 5th and 4th Street, heading west, in Midtown.</summary>
        public static UptownExit Exit => new UptownExit(StreetX(4) + StreetPitch * 0.5f, 0f, FirstAvenueZ + 8.75f, 270f, "ba:neighborhood_midtown");

        /// <summary>True for a point on the first block's ground.</summary>
        public static bool OnFirstGround(float x, float z)
        {
            List<GroundRect> rects = FirstGround;
            for (int i = 0; i < rects.Count; i++)
            {
                if (rects[i].Holds(x, z))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>The first block's back line in one of its columns (m): the backs of 1st Avenue's buildings there.</summary>
        public static float FirstBack(int column)
        {
            return FirstBacks[column - FirstColumn];
        }
    }
}
