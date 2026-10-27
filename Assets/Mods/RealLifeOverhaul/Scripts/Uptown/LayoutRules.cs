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

        // ---- the lots: the game's sizes on whole spaces (SpacesRules), in Manhattan's mix

        public const int PlanGeneration = 4;           // the plan's generation, kept in every save with Uptown: a save of an earlier one has its district's records retired
        public const float LotGap = 0f;                // m between two lots: none, Manhattan's buildings stand side by side
        public const float BackGap = 2f;               // m from the backs of 1st Avenue's buildings to the first lots behind them
        public const float FirstNorthDepth = 32f;      // m: the first block's strip along Uptown Avenue (no warehouse) ...
        public const float DeepNorthDepth = 39f;       // m: ... or one deep enough for a warehouse, in its two deepest blocks
        public const float StreetDepth = 35f;          // m: a strip along a street, half a block across: the two streets' lots meet back to back
        public const float AvenueDepth = 39f;          // m: a strip along an avenue beyond the first block, any class, its ends the block's corners
        private static readonly int[] MixShare = { 64, 14, 13, 7 };   // per cent of Manhattan's buildings in each class (the other 2%: theatres, cinemas, towers)

        // ---- the open runs: the construction game's land (the owner, 25 Oct 2026: "keep 30" open lots), a run of single
        // spaces on a block's west street, so a player joins one, two, three or more of them into a site

        public const int OpenRuns = 13;                // blocks with a run: 65 spaces, the frontage of the 30 open lots the plan kept before
        public const int OpenRunSpaces = 5;            // spaces in a run: a site of up to five, a K1 office's or a theatre's
        private static readonly int[] LandmarkBlocks = { 110, 111, 113 };   // the landmarks' own blocks (LandmarkRules.Blocks' firsts), open whole: never a run's

        // The backs of 1st Avenue's north side, block by block east of 9th Street (m, the game's buildings' boxes, Build 3682).
        private static readonly float[] FirstBacks = { 508.8f, 508.5f, 504.7f, 520.0f, 510.8f, 506.9f, 510.7f, 510.8f };

        // Ground the plan keeps on whole spaces of one strip: from a space (counted along the strip from its west or south end)
        // for so many (0: the whole strip), as deep as the strip or as Back says. Uptown's big places (Scripts/Uptown/
        // BigPlacesRules): the wholesale store over block 108's whole Uptown Avenue front and back to z 557 (its car park, its
        // yard east of it), the streets' lots ending PlaceClear m short of its back; the filling station on block 206's corner
        // of 9th Street and Uptown Avenue, four spaces, 32 m back. The district's venues (the owner, 25 Oct 2026: cinemas "to
        // be rented"), lots of their own numbered as their block's: five spaces each on an avenue, two theatres on Summit
        // Avenue, three cinemas on Uptown, Uptown and Crest Avenue, spread west to east.
        private static readonly PlaceGround[] PlaceTable =
        {
            new PlaceGround("uptown_place_wholesale", 1, FirstColumn + 2, 0, 0, 0, 557f, LotClass.Warehouse, null, 0),
            new PlaceGround("uptown_place_fuel", 2, FirstColumn, 180, 0, 4, 32f, LotClass.Office, null, 0),
            new PlaceGround(null, 2, 7, 0, 2, 5, 0f, LotClass.Theatre, "ba:buildingsize_r", 1),
            new PlaceGround(null, 3, 14, 180, 2, 5, 0f, LotClass.Theatre, "ba:buildingsize_r", 2),
            new PlaceGround(null, 1, 4, 0, 2, 5, 0f, LotClass.Cinema, "ba:buildingsize_s", 1),
            new PlaceGround(null, 2, 12, 180, 2, 5, 0f, LotClass.Cinema, "ba:buildingsize_s", 2),
            new PlaceGround(null, 3, 16, 0, 2, 5, 0f, LotClass.Cinema, "ba:buildingsize_s", 3),
        };

        public const float PlaceClear = 1.5f;          // m the streets' lots end short of a place deeper than its avenue's strip

        // One place's ground as the table gives it: its block, the strip's facing, its spaces, and how far back it reaches (a
        // depth in m, or for an avenue's strip a z the place's back stands on when over 100; 0 the strip's own).
        private struct PlaceGround
        {
            public readonly string Id;
            public readonly int Row, Column, Facing, First, Spaces;
            public readonly float Back;
            public readonly LotClass Class;
            public readonly string Size;
            public readonly int Version;

            public PlaceGround(string id, int row, int column, int facing, int first, int spaces, float back, LotClass kind, string size, int version)
            {
                Id = id;
                Row = row;
                Column = column;
                Facing = facing;
                First = first;
                Spaces = spaces;
                Back = back;
                Class = kind;
                Size = size;
                Version = version;
            }

            // m from its street's line back to its back.
            public float Depth(float front, float strip)
            {
                return Back <= 0f ? strip : Back > 100f ? Math.Abs(front - Back) : Back;
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
        private static HashSet<string> _pinned;     // lots a save keeps as the first plan had them (LayoutRules.Pin): set before the plan is read
        private static List<string> _swaps;         // the lots the rebalance builds as J1 offices in place of M1 stores, before any pin
        private static HashSet<string> _swapped;    // of those, the ones built as J1 offices now (the pinned stay stores)

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

        // The squares kept as parks (row * 100 + column): one in the west, one in the middle and one in the east of the band,
        // each in another row, so every part of the district keeps green ground; every other square is built (the owner, 25 Oct
        // 2026: "make the empty spaces denser").
        private static readonly int[] Parks = { 202, 309, 117 };

        // The west square was never laid as a park, and the owner found it bare (25 Oct 2026: "Try making it a big school"):
        // Birchwood School's campus stands on it (Scripts/Schools). It stays a square of the plan, with no lots, so no lot's
        // id, street number or day one building moves.
        public const int CampusBlock = 202;

        /// <summary>The plan's squares, as Row * 100 + Column: the campus's among them.</summary>
        public static IReadOnlyList<int> Squares => Parks;

        /// <summary>
        /// True for a block left as a square (a park or a plaza), no lots on it: three of the plan's squares (every third block
        /// of rows 2 and 3 on a stagger and every other block of row 1 beyond Manhattan's), one in each part of the band.
        /// </summary>
        public static bool IsSquare(int row, int column)
        {
            return PlannedSquare(row, column) && Array.IndexOf(Parks, row * 100 + column) >= 0;
        }

        /// <summary>
        /// True for a block of the ring round the band, past its outer streets (the owner, 25 Oct 2026: "the edge lots of the
        /// district aren't filled. Please fill it buildings"): row 4 north of Crest Avenue, column 0 west of the westmost
        /// street and column 19 east of the eastmost, each built along the side that faces the band (its inner strip), the
        /// rest of it the scenery behind the district's walls.
        /// </summary>
        public static bool IsRing(int row, int column)
        {
            return (row == Rows + 1 && column >= 1 && column <= Columns) || ((column == 0 || column == Columns + 1) && row >= 1 && row <= Rows);
        }

        /// <summary>
        /// A ring block's inner strip: its ground from the outer street's or Crest Avenue's pavement back
        /// <see cref="RingDepth"/> (west, south, east, north, m); the way its street lies from it (MadeLot's facing).
        /// </summary>
        public static void RingStrip(int row, int column, out float x0, out float z0, out float x1, out float z1, out int facing)
        {
            Block(row, column, out x0, out z0, out x1, out z1);
            if (row == Rows + 1)
            {
                z1 = z0 + AvenueDepth;
                facing = 180;
            }
            else if (column == 0)
            {
                x0 = x1 - StreetDepth;
                facing = 90;
            }
            else
            {
                x1 = x0 + StreetDepth;
                facing = 270;
            }
        }

        /// <summary>m a ring strip reaches past the band's edge: an avenue's strip north of Crest Avenue, a street's west and east.</summary>
        public static float RingDepth(bool north) => north ? AvenueDepth : StreetDepth;

        /// <summary>True for a block the plan first left as a square: built now where <see cref="IsSquare"/> is false.</summary>
        public static bool PlannedSquare(int row, int column)
        {
            if (IsFirst(row, column))
            {
                return false;
            }
            return row == 1 ? column % 2 == 1 : (column * 2 + row) % 3 == 0;
        }

        /// <summary>
        /// How deep a block's avenue strip is on the side its lots face (180: the strip along the avenue to the south, 0: along the avenue
        /// to the north), in m: the ground a big place stands on is the strip's to its back, whatever its own building takes.
        /// </summary>
        public static float AvenueStripDepth(int row, int column, int facing)
        {
            if (facing == 180 || !IsFirst(row, column))
            {
                return AvenueDepth;
            }
            return Array.IndexOf(DeepFirst, column) >= 0 ? DeepNorthDepth : FirstNorthDepth;
        }

        /// <summary>
        /// A big place's whole ground on the world's axes: its own lot, and for one on an avenue strip the rest of the strip's depth behind it
        /// (the filling station's building takes 32 m of the strip's 39, and its yard behind it is no lot's). x1 &lt; x2, z1 &lt; z2.
        /// </summary>
        public static void PlaceYard(Lot place, out float x1, out float z1, out float x2, out float z2)
        {
            bool avenue = place.Facing < 1f || place.Facing > 359f || Math.Abs(place.Facing - 180f) < 1f;
            float halfX = (avenue ? place.Width : place.Depth) * 0.5f, halfZ = (avenue ? place.Depth : place.Width) * 0.5f;
            x1 = place.X - halfX;
            x2 = place.X + halfX;
            z1 = place.Z - halfZ;
            z2 = place.Z + halfZ;
            if (avenue)
            {
                float deep = Math.Max(place.Depth, AvenueStripDepth(place.Row, place.Column, (int)Math.Round(place.Facing)));
                if (place.Facing > 90f)
                {
                    z2 = z1 + deep;   // facing south: its front on the avenue's north pavement, the strip goes north
                }
                else
                {
                    z1 = z2 - deep;
                }
            }
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

        /// <summary>
        /// A strip's two corners: true for a strip along an avenue of a block of the band (the plan gives the corners to the avenues'
        /// strips, which run the block's whole width), with the block's west and east edges, where its two cross streets' building
        /// lines run; false for a street's strip (its ends meet the avenues' strips, no street), a ring block's or a square's.
        /// <paramref name="facing"/>: the way the strip's street lies (0 north, 180 south). Build mode's corner sites (Scripts/Corners).
        /// </summary>
        public static bool StreetEnds(int row, int column, int facing, out float low, out float high)
        {
            low = high = 0f;
            int f = ((facing % 360) + 360) % 360;
            if ((f != 0 && f != 180) || row < 1 || row > Rows || column < 1 || column > Columns || IsRing(row, column) || IsSquare(row, column))
            {
                return false;
            }
            Block(row, column, out low, out _, out high, out _);
            return true;
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

        /// <summary>The class's least footprint along its street and back from it (m): its narrowest size's spaces and its shallowest depth class.</summary>
        public static void Footprint(LotClass kind, out int width, out int depth)
        {
            width = int.MaxValue;
            depth = int.MaxValue;
            for (int i = 0; i < SpacesRules.Grid.Length; i++)
            {
                SpaceSize s = SpacesRules.Grid[i];
                if (s.Class == kind)
                {
                    width = Math.Min(width, s.Spaces * SpacesRules.SpaceWidth);
                    depth = Math.Min(depth, s.DepthClass);
                }
            }
            width = width == int.MaxValue ? 0 : width;
            depth = depth == int.MaxValue ? 0 : depth;
        }

        /// <summary>Manhattan's share of a class among its buildings of the four classes, per cent of 98.</summary>
        public static int Share(LotClass kind)
        {
            return (int)kind < MixShare.Length ? MixShare[(int)kind] : 0;   // the venues are the table's, outside the mix
        }

        /// <summary>
        /// True for a block with an open run: <see cref="OpenRuns"/> of the band's built blocks but the landmarks', spread
        /// evenly in block order (row by row, west to east).
        /// </summary>
        public static bool OpenRunBlock(int row, int column)
        {
            int index = -1, count = 0;
            for (int r = 1; r <= Rows; r++)
            {
                for (int c = 1; c <= Columns; c++)
                {
                    if (IsSquare(r, c) || Array.IndexOf(LandmarkBlocks, r * 100 + c) >= 0)
                    {
                        continue;
                    }
                    index = r == row && c == column ? count : index;
                    count++;
                }
            }
            return index >= 0 && (long)(index + 1) * OpenRuns / count > (long)index * OpenRuns / count;
        }

        /// <summary>The landmarks' own blocks the open runs keep off (for the checks, against LandmarkRules).</summary>
        public static IReadOnlyList<int> KeptForLandmarks => LandmarkBlocks;

        // ---- the rebalance (the owner, 27 Oct 2026, after a player's "the amount of 75 capacity stores"): the game's largest store, M1 with
        // room for 75 customers, was 28 of the band's 566 buildings against 12 in Midtown, the game's most; the plan keeps only as many as
        // each district's quota (DistrictSplit.LargeStoresOf) and builds the rest as J1 offices on the same five spaces, so no lot's id,
        // address or neighbour moves. Which stay is fixed data: the special places' lots first, then the avenues' (a big store belongs on a
        // main road), then by the hash of the lot's id.

        public const string LargeSize = "ba:buildingsize_m";   // M1: the game's largest store, 75 customers
        public const string SwapSize = "ba:buildingsize_j";    // J1: an office of 285 m2 on the same five spaces (29.9 m frontage against M1's 32.4 m)
        public const int RebalanceGeneration = 1;               // the rebalance's generation, kept in a save's line (LifeStore ^r): 0 before it was decided there

        // M1 lots a special place stands on (SpecialsRules: the furniture store and the car dealership): never swapped.
        private static readonly string[] LargeKept = { "uptown_2_09_0001", "uptown_2_13_0005" };

        /// <summary>The lots the rebalance builds as J1 offices in place of M1 stores, in id order, whatever any save pins.</summary>
        public static IReadOnlyList<string> Swaps
        {
            get
            {
                Make();
                return _swaps;
            }
        }

        /// <summary>True when the lot is built as a J1 office now, in place of an M1 store.</summary>
        public static bool IsSwapped(string lotId)
        {
            Make();
            return lotId != null && _swapped.Contains(lotId);
        }

        /// <summary>
        /// Lots a save keeps as the first plan had them, M1 stores someone holds (the player or another player, a building the
        /// construction game made): set before the plan is read for a load, and the plan is made again from them. Null or none for a
        /// save with nothing held, which has them all rebalanced.
        /// </summary>
        public static void Pin(ICollection<string> lotIds)
        {
            HashSet<string> next = lotIds != null && lotIds.Count > 0 ? new HashSet<string>(lotIds, StringComparer.Ordinal) : null;
            bool same = (next == null && _pinned == null) || (next != null && _pinned != null && next.SetEquals(_pinned));
            if (same)
            {
                return;
            }
            _pinned = next;
            _lots = null;
            _places = null;
            _first = null;
            _byId = null;
            _swaps = null;
            _swapped = null;
        }

        // The rebalance over the laid lots, in id order after Number: each district's M1 lots in the order they are kept in, the quota
        // kept and the rest built as J1 (their size and version; their class stays Big, so no lease or class count moves).
        private static void Rebalance(List<Laid> laid)
        {
            var swaps = new List<string>(24);
            var swapped = new HashSet<string>(StringComparer.Ordinal);
            for (int d = 0; d < DistrictSplit.Count; d++)
            {
                var large = new List<Laid>(24);
                for (int i = 0; i < laid.Count; i++)
                {
                    Laid l = laid[i];
                    if (l.Place == null && !l.Open && l.Size == LargeSize && DistrictSplit.OfColumn(l.Column) == d)
                    {
                        large.Add(l);
                    }
                }
                large.Sort((a, b) =>
                {
                    int byRank = KeepRank(a).CompareTo(KeepRank(b));
                    if (byRank != 0)
                    {
                        return byRank;
                    }
                    int byHash = Fnv(IdOf(a) + "|large").CompareTo(Fnv(IdOf(b) + "|large"));
                    return byHash != 0 ? byHash : string.CompareOrdinal(IdOf(a), IdOf(b));
                });
                for (int i = DistrictSplit.LargeStoresOf(d); i < large.Count; i++)
                {
                    string id = IdOf(large[i]);
                    swaps.Add(id);
                    if (_pinned != null && _pinned.Contains(id))
                    {
                        continue;
                    }
                    large[i].Size = SwapSize;
                    large[i].Version = 1;
                    swapped.Add(id);
                }
            }
            swaps.Sort(StringComparer.Ordinal);
            _swaps = swaps;
            _swapped = swapped;
        }

        private static string IdOf(Laid l)
        {
            return "uptown_" + l.Row.ToString(Inv) + "_" + l.Column.ToString("00", Inv) + "_" + l.InBlock.ToString("0000", Inv);
        }

        // 0 a special place's lot, 1 a lot on an avenue, 2 any other.
        private static int KeepRank(Laid l)
        {
            return Array.IndexOf(LargeKept, IdOf(l)) >= 0 ? 0 : l.Yaw == 0 || l.Yaw == 180 ? 1 : 2;
        }

        private static uint Fnv(string text)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < text.Length; i++)
            {
                hash ^= text[i];
                hash = unchecked(hash * 16777619u);
            }
            return hash;
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
            public int Width, Depth; // m: along its street (its spaces'), and its strip's depth
            public string Place;     // a place's id (PlaceTable), or null for a lot
            public bool OddSide;     // its street's odd side: a street's west side, an avenue's south side
            public int Number, Order;
            public int Strip, Index, Spaces;   // its strip (Lot.Strip), its first space along it and how many it takes
            public string Size;      // the game's size and version planned for it, or null for an open run's space
            public int Version;
            public bool Open;        // a space of an open run
        }

        private static void Make()
        {
            if (_lots != null)
            {
                return;
            }
            var laid = new List<Laid>(768);
            var counts = new int[SpacesRules.Grid.Length];
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
            for (int row = 1; row <= Rows + 1; row++)
            {
                for (int column = 0; column <= Columns + 1; column++)
                {
                    if (IsRing(row, column))
                    {
                        LayRing(row, column, laid, counts);
                    }
                }
            }
            // In id order, row by row and west to east, each block's own order kept: the ring's blocks were laid after the band's.
            var order = new Dictionary<Laid, int>(laid.Count);
            for (int i = 0; i < laid.Count; i++)
            {
                order[laid[i]] = i;
            }
            laid.Sort((a, b) => a.Row != b.Row ? a.Row.CompareTo(b.Row) : a.Column != b.Column ? a.Column.CompareTo(b.Column) : order[a].CompareTo(order[b]));
            Number(laid);
            Rebalance(laid);
            var lots = new List<Lot>(laid.Count);
            var places = new List<Lot>(PlaceTable.Length);
            var first = new List<Lot>(128);
            var byId = new Dictionary<string, Lot>(laid.Count, StringComparer.Ordinal);
            for (int i = 0; i < laid.Count; i++)
            {
                Laid l = laid[i];
                // Four digits in the block: the earlier plans' ids had two or three, so none of their lots is ever taken for one of these.
                string id = l.Place ?? "uptown_" + l.Row.ToString(Inv) + "_" + l.Column.ToString("00", Inv) + "_" + l.InBlock.ToString("0000", Inv);
                bool isFirst = IsFirst(l.Row, l.Column);
                var lot = new Lot(id, l.Row, l.Column, l.Street, l.Number, l.Order, l.X, l.Z, l.Yaw, l.Width, l.Depth, l.Class, isFirst, l.Strip, l.Index, l.Spaces, l.Size,
                    l.Version, l.Open);
                if (l.Place != null)
                {
                    places.Add(lot);
                    continue;
                }
                lot.Rebalanced = _swapped.Contains(id);
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
            Strip(row, column, laid, counts, x0, x1, z1, north, 0, LayoutStreets.Avenue(row).Id, 0);
            float sideFrom = z0;
            if (row > 1)
            {
                Strip(row, column, laid, counts, x0, x1, z0, AvenueDepth, 180, LayoutStreets.Avenue(row - 1).Id, 0);
                sideFrom = z0 + AvenueDepth + LotGap;
            }
            float sideTo = Math.Min(z1 - north - LotGap, PlaceBackZ(row, column, z1, north) - PlaceClear);
            Strip(row, column, laid, counts, sideFrom, sideTo, x0, StreetDepth, 270, LayoutStreets.Street(west).Id, OpenRunBlock(row, column) ? OpenRunSpaces : 0);
            Strip(row, column, laid, counts, sideFrom, sideTo, x1, StreetDepth, 90, LayoutStreets.Street(west + 1).Id, 0);
            int inBlock = 0;
            for (int i = start; i < laid.Count; i++)
            {
                laid[i].InBlock = laid[i].Place != null ? 0 : ++inBlock;
            }
        }

        // A ring block's one strip, facing the band across its outer street or Crest Avenue, laid as any strip is.
        private static void LayRing(int row, int column, List<Laid> laid, int[] counts)
        {
            RingStrip(row, column, out float x0, out float z0, out float x1, out float z1, out int facing);
            int start = laid.Count;
            if (facing == 180)
            {
                Strip(row, column, laid, counts, x0, x1, z0, AvenueDepth, 180, LayoutStreets.Avenue(Rows).Id, 0);
            }
            else
            {
                Strip(row, column, laid, counts, z0, z1, facing == 90 ? x1 : x0, StreetDepth, facing, LayoutStreets.Street(facing == 90 ? LayoutStreets.WestMost : LayoutStreets.EastMost).Id, 0);
            }
            for (int i = start; i < laid.Count; i++)
            {
                laid[i].InBlock = i - start + 1;
            }
        }

        // One strip along a frontage, from its start to its end, cut into spaces (SpacesRules.Cut), its places on their
        // spaces, its first <open> spaces an open run of single spaces, and the rest laid as buildings: each the size and
        // version furthest below its share of the band's buildings that fits the strip's depth and the spaces left before
        // the next place or the strip's end, on exactly its spaces. facing: the way the street lies from the lots (0 north,
        // 90 east, 180 south, 270 west); front: the street's line the lots' fronts stand on (z for an avenue, x for a street).
        private static void Strip(int row, int column, List<Laid> laid, int[] counts, float from, float to, float front, float depth, int facing, string street, int open)
        {
            var widths = new List<int>(16);
            float lead = SpacesRules.Cut(to - from, widths) * 0.5f;
            int n = widths.Count;
            var starts = new float[n + 1];
            starts[0] = from + lead;
            for (int i = 0; i < n; i++)
            {
                starts[i + 1] = starts[i] + widths[i];
            }
            var s = new StripCut { Row = row, Column = column, Front = front, Depth = depth, Facing = facing, Street = street, Starts = starts, Key = (row * 100 + column) * 1000 + facing };
            int next = 0;
            for (int i = 0; i < Math.Min(open, n); i++)
            {
                Add(s, laid, i, 1, depth, LotClass.Small, null, 0, null).Open = true;
                next = i + 1;
            }
            for (int p = 0; p < PlaceTable.Length; p++)
            {
                PlaceGround g = PlaceTable[p];
                if (g.Row != row || g.Column != column || g.Facing != facing)
                {
                    continue;
                }
                int at = Math.Max(next, g.First), count = g.Spaces <= 0 ? n - at : Math.Min(g.Spaces, n - at);
                Run(s, laid, counts, next, at);
                Add(s, laid, at, count, (float)Math.Round(g.Depth(front, depth)), g.Class, g.Size, g.Version, g.Id);
                next = at + count;
            }
            Run(s, laid, counts, next, n);
        }

        // A strip as cut: where it lies and its spaces' starts along it (the last its end).
        private sealed class StripCut
        {
            public int Row, Column, Facing, Key;
            public float Front, Depth;
            public string Street;
            public float[] Starts;
        }

        // One run of a strip's spaces between its ends (or a place's ground), as the strip comment says.
        private static void Run(StripCut s, List<Laid> laid, int[] counts, int from, int to)
        {
            int at = from;
            while (at < to)
            {
                int pick = Pick(counts, s.Depth, to - at);
                if (pick < 0)
                {
                    break;   // never: A1 takes one space of any strip
                }
                SpaceSize size = SpacesRules.Grid[pick];
                counts[pick]++;
                Add(s, laid, at, size.Spaces, s.Depth, size.Class, size.Size, size.Version, null);
                at += size.Spaces;
            }
        }

        // A lot (or a place's ground) on spaces [at, at + count) of a strip, as deep as given.
        private static Laid Add(StripCut s, List<Laid> laid, int at, int count, float depth, LotClass kind, string size, int version, string place)
        {
            float a = s.Starts[at], b = s.Starts[at + count];
            var l = new Laid
            {
                Row = s.Row, Column = s.Column, Class = kind, Street = s.Street, Place = place, Width = (int)Math.Round(b - a), Depth = (int)Math.Round(depth), Strip = s.Key,
                Index = at, Spaces = count, Size = size, Version = version
            };
            float along = (a + b) * 0.5f, half = depth * 0.5f;
            switch (s.Facing)
            {
                case 0:
                    l.X = along;
                    l.Z = s.Front - half;
                    break;
                case 180:
                    l.X = along;
                    l.Z = s.Front + half;
                    break;
                case 270:
                    l.X = s.Front + half;
                    l.Z = along;
                    break;
                default:
                    l.X = s.Front - half;
                    l.Z = along;
                    break;
            }
            l.X = Round(l.X);
            l.Z = Round(l.Z);
            l.Yaw = (s.Facing + 180) % 360;
            // A street's west side is its odd one (lots facing east toward it), an avenue's south side (lots facing north).
            l.OddSide = s.Facing == 90 || s.Facing == 0;
            l.Along = s.Facing == 0 || s.Facing == 180 ? -l.X : l.Z;   // avenues number from their east end, as 1st Avenue; streets from the south
            laid.Add(l);
            return l;
        }

        // The z of the back of a place in a block's north avenue strip (its streets' lots end short of it when it reaches past
        // the strip), else none.
        private static float PlaceBackZ(int row, int column, float front, float strip)
        {
            float back = float.MaxValue;
            for (int p = 0; p < PlaceTable.Length; p++)
            {
                PlaceGround g = PlaceTable[p];
                if (g.Row == row && g.Column == column && g.Facing == 0)
                {
                    back = Math.Min(back, front - g.Depth(front, strip));
                }
            }
            return back;
        }

        // The size and version of the mix's four classes furthest below its share that fits the strip's depth and the
        // spaces left; -1 when none fits. A size's share: its class's of Manhattan's buildings, split by the game's own
        // buildings of each of the class's sizes.
        private static int Pick(int[] counts, float depth, int room)
        {
            int laid = 0;
            var classWeight = new int[MixShare.Length];
            for (int i = 0; i < SpacesRules.Grid.Length; i++)
            {
                laid += counts[i];
                int c = (int)SpacesRules.Grid[i].Class;
                if (c < MixShare.Length)
                {
                    classWeight[c] += SpacesRules.Grid[i].Weight;
                }
            }
            int best = -1;
            double most = double.MinValue;
            for (int i = 0; i < SpacesRules.Grid.Length; i++)
            {
                SpaceSize s = SpacesRules.Grid[i];
                int c = (int)s.Class;
                if (c >= MixShare.Length || s.DepthClass > depth + 0.01f || s.Spaces > room)
                {
                    continue;
                }
                double share = MixShare[c] / 98.0 * s.Weight / classWeight[c];
                double below = share * (laid + 1) - counts[i];
                if (below > most + 1e-9)
                {
                    most = below;
                    best = i;
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
                    for (int r = 1; r <= Rows + 1; r++)
                    {
                        for (int c = 0; c <= Columns + 1; c++)
                        {
                            if (IsRing(r, c))
                            {
                                RingStrip(r, c, out float rx0, out float rz0, out float rx1, out float rz1, out _);
                                rects.Add(new GroundRect(rx0, rz0, rx1, rz1));   // the ring's built strips past the band's outer streets
                            }
                        }
                    }
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
