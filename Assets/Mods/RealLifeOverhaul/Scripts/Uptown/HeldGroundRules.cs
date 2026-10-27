using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>What holds a piece of the band's ground that build mode's grid of squares does not cover.</summary>
    public enum HeldGroundKind
    {
        Place = 0,    // the ground of a big special place (the wholesale store, the filling station): its building stands on it
        Park = 1,     // a square the plan kept as a park
        Campus = 2,   // the plan's west square, Birchwood School's campus
        Edge = 3      // the ring blocks' ground past their buildings, behind the district's walls
    }

    /// <summary>One piece of the band's ground the plan keeps out of the lots, on the world's axes, with what holds it.</summary>
    public struct HeldGround
    {
        public HeldGroundKind Kind;
        public int Block;          // Row * 100 + Column
        public string Lot;         // a place's own lot id, else null
        public string Street;      // a place's address (a street of the mod's own and a number), else null
        public int Number;
        public string Name;        // a place's business name, the same in every language; else null
        public string NameKey;     // else the locale key of what holds it
        public float X1, Z1, X2, Z2;   // m, X1 < X2 and Z1 < Z2

        public float Area => (X2 - X1) * (Z2 - Z1);

        public bool Holds(float x, float z) => x >= X1 && x < X2 && z >= Z1 && z < Z2;
    }

    /// <summary>
    /// The band's ground that no lot of the plan covers, each piece with what holds it (the owner, 27 Oct 2026: "There are empty
    /// spots in the block with no grids"): the big special places' ground, the parks, the campus and the ring's scenery. Build mode
    /// draws each as a held square of its own and names it under the pointer; the plan's own check says every metre of every block
    /// is a lot's, one of these, or the plan's thin margin. Fixed data, the same on every machine. Game-free (EconomyCheck's
    /// BuildGroundChecks). COST: made once, a few dozen rectangles.
    /// </summary>
    public static class HeldGroundRules
    {
        public const string ParkKey = "reallife_bld_held_park_name";
        public const string CampusKey = "reallife_schools_name_birchwood";
        public const string EdgeKey = "reallife_bld_held_edge_name";

        private static List<HeldGround> _all;

        /// <summary>Every piece, in a fixed order: the places, the squares west to east by block, the ring's blocks.</summary>
        public static IReadOnlyList<HeldGround> All
        {
            get
            {
                if (_all == null)
                {
                    _all = Make();
                }
                return _all;
            }
        }

        /// <summary>The piece holding the spot, or -1.</summary>
        public static int At(float x, float z)
        {
            IReadOnlyList<HeldGround> all = All;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].Holds(x, z))
                {
                    return i;
                }
            }
            return -1;
        }

        private static List<HeldGround> Make()
        {
            var list = new List<HeldGround>(48);
            IReadOnlyList<Lot> places = LayoutRules.Places;
            for (int i = 0; i < places.Count; i++)
            {
                Lot p = places[i];
                BigPlace big = BigPlacesRules.On(p.Id);
                // A place whose building takes less than its avenue strip's depth holds the rest of the strip behind it too (its yard).
                LayoutRules.PlaceYard(p, out float x1, out float z1, out float x2, out float z2);
                list.Add(new HeldGround
                {
                    Kind = HeldGroundKind.Place, Block = p.Block, Lot = p.Id, Street = p.Street, Number = p.Number, Name = big != null ? big.Name : null,
                    NameKey = big != null ? null : "reallife_bld_held_place_name", X1 = x1, Z1 = z1, X2 = x2, Z2 = z2
                });
            }
            IReadOnlyList<int> squares = LayoutRules.Squares;
            var blocks = new List<int>(squares);
            blocks.Sort();
            for (int i = 0; i < blocks.Count; i++)
            {
                LayoutRules.Block(blocks[i] / 100, blocks[i] % 100, out float x0, out float z0, out float x1, out float z1);
                bool campus = blocks[i] == LayoutRules.CampusBlock;
                list.Add(new HeldGround
                {
                    Kind = campus ? HeldGroundKind.Campus : HeldGroundKind.Park, Block = blocks[i], NameKey = campus ? CampusKey : ParkKey, X1 = x0, Z1 = z0, X2 = x1, Z2 = z1
                });
            }
            for (int row = 1; row <= LayoutRules.Rows + 1; row++)
            {
                for (int column = 0; column <= LayoutRules.Columns + 1; column++)
                {
                    if (!LayoutRules.IsRing(row, column))
                    {
                        continue;
                    }
                    LayoutRules.Block(row, column, out float x0, out float z0, out float x1, out float z1);
                    LayoutRules.RingStrip(row, column, out float sx0, out float sz0, out float sx1, out float sz1, out int facing);
                    // The strip is the side of the block that faces the band; what is left of the block is the edge.
                    float rx0 = x0, rz0 = z0, rx1 = x1, rz1 = z1;
                    if (row == LayoutRules.Rows + 1)
                    {
                        rz0 = sz1;
                    }
                    else if (column == 0)
                    {
                        rx1 = sx0;
                    }
                    else
                    {
                        rx0 = sx1;
                    }
                    if (rx1 > rx0 && rz1 > rz0)
                    {
                        list.Add(new HeldGround { Kind = HeldGroundKind.Edge, Block = row * 100 + column, NameKey = EdgeKey, X1 = rx0, Z1 = rz0, X2 = rx1, Z2 = rz1 });
                    }
                }
            }
            return list;
        }
    }
}
