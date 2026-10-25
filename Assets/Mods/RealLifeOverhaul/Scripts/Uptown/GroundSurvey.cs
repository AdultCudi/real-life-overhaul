using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>What a spot of ground is, as the district's plan has it (<see cref="GroundSurvey.At"/>).</summary>
    public enum GroundKind
    {
        GameGround = 0,    // the game's own city: none of the district's ground
        PastWalls = 1,     // the scenery past the district's walls: seen, never walked
        Street = 2,        // one of the district's streets, its pavements in
        Avenue = 3,        // one of its avenues, or 1st Avenue's continuations
        Crossing = 4,      // where a street meets an avenue
        Lot = 5,           // a lot of the plan, built from the first day by Uptown's own rule
        OpenRun = 6,       // a space of an open run: the construction game's land
        LandmarkSite = 7,  // a lot of a landmark's own block, held whole for it
        BigPlace = 8,      // the ground of a big special place (the wholesale store, the filling station)
        Campus = 9,        // Birchwood School's campus, on the plan's west square
        ParkSquare = 10,   // a square the plan kept as a park, with no lots
        Between = 11,      // a block's ground between its lots: behind and between the buildings, the fillers' ground
        Ring = 12          // the ring past the district's outer streets, behind its lots
    }

    /// <summary>
    /// One spot as the plan reads it: what the ground is, the block and lot it is in, its nearest street on each side and the
    /// street or avenue it is on. Street names are locale keys (Uptown's own, or the game's "ba:street_" ones); null for none.
    /// </summary>
    public struct GroundAnswer
    {
        public GroundKind Kind;
        public int Row, Column, Block;     // its block (Row * 100 + Column), 0 when on no block
        public bool First;                 // in the first block, behind 1st Avenue's north side
        public string Lot;                 // the lot's id or the big place's, null for none
        public string LotStreet;           // that lot's address: a street of the mod's own and its number
        public int LotNumber;
        public string On;                  // the street or avenue the spot is on
        public string West, East, South, North;
    }

    /// <summary>
    /// The district's ground read at a spot, from the plan alone: the answer to the owner's "I can't find what area that is"
    /// (25 Oct 2026). Every answer comes from fixed data and the spot, the same on every machine; what stands there now (a
    /// building made, a landmark's works, the construction game's claims) is the game side's to add. No Unity or game types:
    /// checked offline (NavCheck, GroundSurveyCheck.cs). COST: a pass over one block's lots, asked by a test button.
    /// </summary>
    public static class GroundSurvey
    {
        public const string FirstAvenue = "ba:street_firstavenue";   // the game's own avenue the first block backs onto
        public const float PastWalls = 60f;                            // m past the district's outer edge that still counts as its scenery

        /// <summary>What the ground at (x, z) is, by the plan.</summary>
        public static GroundAnswer At(float x, float z)
        {
            var a = new GroundAnswer();
            int west = (int)Math.Floor((x - LayoutRules.StreetFirstX) / LayoutRules.StreetPitch);
            int south = (int)Math.Floor((z - LayoutRules.FirstAvenueZ) / LayoutRules.AvenuePitch);
            a.West = StreetKey(west);
            a.East = StreetKey(west + 1);
            a.South = AvenueKey(south);
            a.North = AvenueKey(south + 1);
            if (!LayoutRules.OnGround(x, z))
            {
                a.Kind = Scenery(x, z) ? GroundKind.PastWalls : GroundKind.GameGround;
                if (a.Kind == GroundKind.GameGround)
                {
                    a.West = a.East = a.South = a.North = null;   // the game's own city: its streets are the game side's to name
                }
                return a;
            }
            int onStreet = OnStreet(x);
            int onAvenue = OnAvenue(z);
            if (onStreet != int.MinValue && onAvenue != int.MinValue)
            {
                a.Kind = GroundKind.Crossing;
                a.On = StreetKey(onStreet);
                a.West = StreetKey(onStreet - 1);
                a.East = StreetKey(onStreet + 1);
                a.South = AvenueKey(onAvenue);
                a.North = AvenueKey(onAvenue);
                return a;
            }
            if (onStreet != int.MinValue)
            {
                a.Kind = GroundKind.Street;
                a.On = StreetKey(onStreet);
                a.West = StreetKey(onStreet - 1);
                a.East = StreetKey(onStreet + 1);
                return a;
            }
            if (onAvenue != int.MinValue)
            {
                a.Kind = GroundKind.Avenue;
                a.On = AvenueKey(onAvenue);
                a.South = AvenueKey(onAvenue - 1);
                a.North = AvenueKey(onAvenue + 1);
                return a;
            }
            a.Row = south + 1;
            a.Column = west + LayoutRules.FirstColumn;
            a.Block = a.Row * 100 + a.Column;
            a.First = LayoutRules.IsFirst(a.Row, a.Column);
            Lot lot = LotAt(LayoutRules.Places, a.Block, x, z);
            if (lot != null)
            {
                a.Kind = GroundKind.BigPlace;
                Name(ref a, lot);
                return a;
            }
            lot = LotAt(LayoutRules.Lots, a.Block, x, z);
            if (lot != null)
            {
                a.Kind = lot.Open ? GroundKind.OpenRun : IsLandmarkBlock(a.Block) ? GroundKind.LandmarkSite : GroundKind.Lot;
                Name(ref a, lot);
                return a;
            }
            if (LayoutRules.IsRing(a.Row, a.Column))
            {
                a.Kind = GroundKind.Ring;
            }
            else if (a.Block == LayoutRules.CampusBlock)
            {
                a.Kind = GroundKind.Campus;
            }
            else if (LayoutRules.IsSquare(a.Row, a.Column))
            {
                a.Kind = GroundKind.ParkSquare;
            }
            else if (IsLandmarkBlock(a.Block))
            {
                a.Kind = GroundKind.LandmarkSite;
            }
            else
            {
                a.Kind = GroundKind.Between;
            }
            return a;
        }

        /// <summary>
        /// The band's blocks with nothing the plan builds from the first day, as Row * 100 + Column (into the list, cleared):
        /// its squares (the campus's among them) and the landmarks' own blocks, whole until their works stand.
        /// </summary>
        public static List<int> BareBlocks(List<int> into)
        {
            into.Clear();
            for (int row = 1; row <= LayoutRules.Rows; row++)
            {
                for (int column = 1; column <= LayoutRules.Columns; column++)
                {
                    int block = row * 100 + column;
                    if (LayoutRules.IsSquare(row, column) || IsLandmarkBlock(block))
                    {
                        into.Add(block);
                    }
                }
            }
            return into;
        }

        /// <summary>A block's four streets as locale keys: west, east, south, north (1st Avenue's for the first row's south).</summary>
        public static void StreetsOf(int block, out string west, out string east, out string south, out string north)
        {
            int row = block / 100, column = block % 100;
            int w = LayoutRules.WestStreet(column);
            west = StreetKey(w);
            east = StreetKey(w + 1);
            south = AvenueKey(row - 1);
            north = AvenueKey(row);
        }

        /// <summary>True for a landmark's own block, held whole from the first load (LandmarkRules.Blocks' firsts).</summary>
        public static bool IsLandmarkBlock(int block)
        {
            IReadOnlyList<int> kept = LayoutRules.KeptForLandmarks;
            for (int i = 0; i < kept.Count; i++)
            {
                if (kept[i] == block)
                {
                    return true;
                }
            }
            return false;
        }

        // The street a spot stands on (its module's width, pavements in), or int.MinValue for none.
        private static int OnStreet(float x)
        {
            int nearest = (int)Math.Round((x - LayoutRules.StreetFirstX) / LayoutRules.StreetPitch);
            if (nearest < LayoutStreets.WestMost || nearest > LayoutStreets.EastMost)
            {
                return int.MinValue;
            }
            return Math.Abs(x - LayoutRules.StreetX(nearest)) <= LayoutRules.StreetHalf ? nearest : int.MinValue;
        }

        // The avenue a spot stands on (1st Avenue's continuations as row 0), or int.MinValue for none.
        private static int OnAvenue(float z)
        {
            int nearest = (int)Math.Round((z - LayoutRules.FirstAvenueZ) / LayoutRules.AvenuePitch);
            if (nearest < 0 || nearest > LayoutRules.Rows)
            {
                return int.MinValue;
            }
            return Math.Abs(z - LayoutRules.AvenueZ(nearest)) <= LayoutRules.AvenueHalf ? nearest : int.MinValue;
        }

        private static string StreetKey(int index)
        {
            LayoutStreet s = LayoutStreets.Street(index);
            return s != null ? s.NameKey : null;
        }

        private static string AvenueKey(int row)
        {
            if (row == 0)
            {
                return FirstAvenue;
            }
            LayoutStreet s = LayoutStreets.Avenue(row);
            return s != null ? s.NameKey : null;
        }

        // Past the district's outer edge, within the scenery's reach north, west and east of the band (never south of 1st Avenue).
        private static bool Scenery(float x, float z)
        {
            float west = LayoutRules.StreetX(LayoutStreets.WestMost) - LayoutRules.StreetHalf - LayoutRules.RingDepth(false);
            float east = LayoutRules.StreetX(LayoutStreets.EastMost) + LayoutRules.StreetHalf + LayoutRules.RingDepth(false);
            float north = LayoutRules.AvenueZ(LayoutRules.Rows) + LayoutRules.AvenueHalf + LayoutRules.RingDepth(true);
            bool inBand = x >= west - PastWalls && x <= east + PastWalls && z <= north + PastWalls && z >= LayoutRules.FirstAvenueZ + LayoutRules.AvenueHalf;
            bool behindManhattan = x > LayoutRules.StreetX(0) - LayoutRules.CrossingHalfX && x < LayoutRules.StreetX(LayoutRules.FirstColumns) + LayoutRules.CrossingHalfX
                                   && z < LayoutRules.AvenueZ(1) - LayoutRules.AvenueHalf;
            return inBand && !behindManhattan && (x < west || x > east || z > north);
        }

        // The lot of a block whose ground holds the spot: an avenue's lot is as wide as its street front along x, a street's along z.
        private static Lot LotAt(IReadOnlyList<Lot> lots, int block, float x, float z)
        {
            for (int i = 0; i < lots.Count; i++)
            {
                Lot l = lots[i];
                if (l.Block != block)
                {
                    continue;
                }
                bool avenue = l.Facing < 1f || l.Facing > 359f || Math.Abs(l.Facing - 180f) < 1f;
                float halfX = (avenue ? l.Width : l.Depth) * 0.5f, halfZ = (avenue ? l.Depth : l.Width) * 0.5f;
                if (Math.Abs(x - l.X) <= halfX && Math.Abs(z - l.Z) <= halfZ)
                {
                    return l;
                }
            }
            return null;
        }

        private static void Name(ref GroundAnswer a, Lot lot)
        {
            a.Lot = lot.Id;
            a.LotStreet = lot.Street;
            a.LotNumber = lot.Number;
        }
    }
}
