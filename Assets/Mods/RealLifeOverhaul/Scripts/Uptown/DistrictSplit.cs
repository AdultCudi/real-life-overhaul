using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// The band north of Manhattan as two districts of the game (the owner, 27 Oct 2026: "Can we split the Uptown district into
    /// 2? ... Every single thing must be separated properly"; a player: "Uptown feels game breaking"). The band is cut along
    /// 5th Street, a street of the grid, so every block, with all the lots round it, is wholly in one: the east half, columns
    /// 10 to 18 and the ring past Lake Street, is <see cref="Uptown"/> (the landmarks, Uptown Avenue's stadium, tower and mall, the
    /// supermarket and the dealership, the east park), and the west half, columns 1 to 9 and the ring past Willow Street, is
    /// <see cref="CedarHeights"/> (Birchwood School, the bank's branch, the clinic, the wholesale store, the west park). The
    /// avenues run through both. Which district a thing is in is answered from its block's column, or from a point's x, alone:
    /// fixed data, the same on every machine and in every version, so a save's records are put in the right district by their
    /// position. Game-free (EconomyCheck's UptownSplitChecks). COST: nothing per frame; an integer comparison per answer.
    /// </summary>
    public static class DistrictSplit
    {
        public const int Count = 2;                    // the districts the band is made of
        public const int Uptown = 0;                   // the east half: the first district, whose id the whole band had before the split
        public const int CedarHeights = 1;             // the west half
        public const string UptownId = DistrictValues.Id;
        public const string CedarId = "reallife_neighborhood_cedarheights";   // the second district's id, never one of the game's, and its name's locale key
        public const int LastWestColumn = 9;           // the westmost district takes the blocks of columns up to this one (the ring's column 0 too)
        public const int BoundaryStreet = 4;           // 5th Street (index 4, the grid's x from 9th Street's 0): the cut runs along it
        public const float BoundaryX = LayoutRulesX0 + LayoutRulesPitch * BoundaryStreet;   // m: its middle, a lot is never on it

        // The grid's own figures (LayoutRules.StreetFirstX and StreetPitch; the checks hold the two together).
        private const float LayoutRulesX0 = -301.67f;
        private const float LayoutRulesPitch = 87.3f;

        public static readonly string[] Ids = { UptownId, CedarId };

        private const int ToLet = 33;   // % of a district's shops and offices kept free to rent: the owner's third (MixRules.ToLetShare, which the checks hold it to)

        /// <summary>The district of a block's column (the ring's 0 and 19 included): 0 or 1.</summary>
        public static int OfColumn(int column)
        {
            return column <= LastWestColumn ? CedarHeights : Uptown;
        }

        /// <summary>The district of a block, as Row * 100 + Column.</summary>
        public static int OfBlock(int block)
        {
            return OfColumn(block % 100);
        }

        /// <summary>The district of a lot of the plan.</summary>
        public static int Of(Lot lot)
        {
            return lot != null ? OfColumn(lot.Column) : Uptown;
        }

        /// <summary>The district of a point on the ground (x in m): west of 5th Street's middle is Cedar Heights, east of it Uptown.</summary>
        public static int OfX(float x)
        {
            return x < BoundaryX ? CedarHeights : Uptown;
        }

        /// <summary>The district's id.</summary>
        public static string IdOf(int district)
        {
            return district >= 0 && district < Count ? Ids[district] : UptownId;
        }

        /// <summary>The district's id for a lot or a block's column.</summary>
        public static string IdOfColumn(int column)
        {
            return Ids[OfColumn(column)];
        }

        /// <summary>The index of a district id of the band, or -1 for any other (the game's own districts, none).</summary>
        public static int Index(string id)
        {
            for (int i = 0; i < Count; i++)
            {
                if (string.Equals(Ids[i], id, StringComparison.Ordinal))
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>True for the id of either of the band's districts.</summary>
        public static bool Is(string id)
        {
            return Index(id) >= 0;
        }

        /// <summary>The city map's rectangle a district's ground covers (m), clipped at the cut: the bounds of the ground's rectangles on its side.</summary>
        public static bool Bounds(int district, IReadOnlyList<GroundRect> ground, out float x1, out float z1, out float x2, out float z2)
        {
            x1 = z1 = float.MaxValue;
            x2 = z2 = float.MinValue;
            for (int i = 0; ground != null && i < ground.Count; i++)
            {
                float left = Math.Min(ground[i].X1, ground[i].X2), right = Math.Max(ground[i].X1, ground[i].X2);
                float low = Math.Min(ground[i].Z1, ground[i].Z2), high = Math.Max(ground[i].Z1, ground[i].Z2);
                if (district == CedarHeights)
                {
                    right = Math.Min(right, BoundaryX);
                }
                else
                {
                    left = Math.Max(left, BoundaryX);
                }
                if (right <= left)
                {
                    continue;
                }
                x1 = Math.Min(x1, left);
                x2 = Math.Max(x2, right);
                z1 = Math.Min(z1, low);
                z2 = Math.Max(z2, high);
            }
            return x1 != float.MaxValue;
        }

        // ---- the character of each district: what the game keeps in a district's data, and what the plan does with its lots

        /// <summary>The district's figures, as the game's own data has a district's: its class mix, its price level, its customers.</summary>
        public static DistrictFigures Figures(int district)
        {
            return district == CedarHeights ? CedarFigures : UptownFigures;
        }

        // Uptown, the upscale core: mostly middle class and some upper, the land the city's second dearest after Midtown's, customers about half Midtown's.
        private static readonly DistrictFigures UptownFigures = new DistrictFigures
        {
            Working = 5, Middle = 76, Upper = 19, RealEstate = 1.5f, Parking = 9f, Demand = 0.45f, CarTraffic = 90, InteriorScore = 15,
            BasePrice = 20554f, RentPerPrice = 0.00056f, Marketing = 0.6f, ShutDownBelow = 750f, Offices = true, EmptyOffices = ToLet, EmptyShops = ToLet
        };

        // Cedar Heights, the working and middle class district: land about as dear as Murray Hill's, customers a little under its.
        private static readonly DistrictFigures CedarFigures = new DistrictFigures
        {
            Working = 40, Middle = 50, Upper = 10, RealEstate = 0.95f, Parking = 4f, Demand = 0.3f, CarTraffic = 85, InteriorScore = 0,
            BasePrice = 20554f, RentPerPrice = 0.00056f, Marketing = 0.8f, ShutDownBelow = 650f, Offices = true, EmptyOffices = ToLet, EmptyShops = ToLet
        };

        public const int LargeStoresUptown = 6;        // stores of the game's largest size (M1, 75 customers) the plan keeps in Uptown: Midtown, the game's most, has 12
        public const int LargeStoresCedar = 3;         // ... and in Cedar Heights

        /// <summary>The large stores (75 customers) the plan keeps in a district; the plan's other M lots are built as J1 offices.</summary>
        public static int LargeStoresOf(int district)
        {
            return district == CedarHeights ? LargeStoresCedar : LargeStoresUptown;
        }

        /// <summary>
        /// Traffic points a district's lots get on top of the rules' own (BuildingsRules.Traffic): Uptown a little busier than the plan's
        /// middle, Cedar Heights, further from the landmarks and the avenues' crowds, a little quieter.
        /// </summary>
        public static int TrafficShift(int district)
        {
            return district == CedarHeights ? -4 : 2;
        }
    }
}
