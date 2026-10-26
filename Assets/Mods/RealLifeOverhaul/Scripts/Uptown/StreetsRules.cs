using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>The kinds of the game's street furniture Uptown's pavements are furnished with, each a copy of the game's own.</summary>
    public enum StreetPiece
    {
        Bench,      // the game's pavement bench (SleepingBench, OutsideBenchController): sat on through the game's own rest
        Bin,        // its litter bin (SM_Trash Can)
        Tree        // a tree in its pavement hole (SM_PavementHolefor*)
    }

    /// <summary>One piece of furniture: its kind, where it stands, its turn in whole degrees, the street whose pavement it is on.</summary>
    public struct StreetPlace
    {
        public StreetPiece Kind;
        public float X;
        public float Z;
        public int Yaw;          // degrees, as the game's own pieces of the kind stand on Manhattan's pavements
        public int Street;       // its street's index in LayoutStreets.All, or StreetsRules.FirstAvenue on 1st Avenue's continuations
        public int Variant;      // which of the kind's templates (the trees' three kinds); 0 for the rest
    }

    /// <summary>
    /// Uptown's streets as the district's buses and pavements use them, game-free and keyed to the grid alone (the streets,
    /// avenues, pavements and crossings of LayoutRules and LayoutStreets, never a lot): the two bus routes that put every
    /// block within <see cref="ReachWanted"/> of a stop, its far corners included, from beside the two Manhattan subway
    /// entrances nearest the district; the reach of a set of stops over the band's blocks; and the furniture of every
    /// pavement at Manhattan's own spacing, measured from the game's scene (Build 3682: the lamps, bins, trees, benches and
    /// benches on Midtown's, Hell's Kitchen's, the Garment District's and Murray Hill's 72 street and 72 avenue
    /// faces, per 100 m of pavement; its bike-share docks are left out: the one Industry City's scene gives is drawn as a bare black
    /// slab), clear of the crossings, of the places the routes stop and of the copied modules'
    /// own lamps. The same on every machine and in every version. Checked by NavCheck (UptownStreetsCheck.cs).
    /// </summary>
    public static class StreetsRules
    {
        public const float ReachWanted = 250f;        // m from a stop every block's every corner should be (the owner, 25 Oct 2026: transit to the corners)
        public const float StationZ = 412f;           // m: where a route's stop stands on 9th and 2nd Street south of 1st Avenue, beside the subway entrances there
        public const float RouteFromZ = 350f;         // m: where its way begins and ends on those streets, a stretch before that stop
        public const float StopKerbStreet = 5.5f;     // m from a street's middle to where a stop's pole stands, either side
        public const float StopKerbAvenue = 10.5f;    // m from an avenue's middle
        public const float StopClear = 28f;           // m along a pavement no furniture stands either side of a stop wanted there: its pole, shelter and doors, and its window of 12 m
        public const float CrossingClear = 3f;        // m past a crossing's module the furniture keeps off: the corner and its crosswalk
        public const float NorthWestStationX = -312.54f;   // m: the game's Midtown North West Station's entrance, 9th Street at 1st Avenue (Build 3682)
        public const float NorthWestStationZ = 442.31f;
        public const float EastStationX = 295.98f;         // m: its station by 2nd Street at 1st Avenue
        public const float EastStationZ = 423.28f;

        // Across the pavement, metres in from the kerb, the medians of Manhattan's own pieces of each kind.
        public const float BenchIn = 0.95f;
        public const float BinIn = 0.8f;
        public const float TreeIn = 1.0f;

        // Along a street's face (94 m between its crossings' clearances), metres from its south end: between the copied
        // module's own lamps (at 13.3, 37.2, 58.0 and 81.9 m on both pavements), as Manhattan's stand round theirs.
        private static readonly float[] StreetTrees = { 19.2f, 31.2f, 64.0f, 76.0f };  // its trees 12 to 13 m apart, on a face in three
        private static readonly float[] StreetBenches = { 25.2f, 50.5f };              // a pair on a face in seven: 0.30 per 100 m (Manhattan's 0.28)
        private static readonly float[] StreetBins = { 43.0f, 88.0f };                 // 45 m apart on five faces in six: 1.77 per 100 m (Manhattan's 1.75)

        // Along an avenue's face (44 m), metres from its west end: between its module's lamps (at 2.0, 19.5 and 38.8 m).
        private static readonly float[] AvenueTrees = { 10.75f, 29.15f };   // on a face in three: 1.5 per 100 m (Manhattan's 1.56)
        private const float AvenueBench = 15.0f;                            // on a face in four: 0.57 per 100 m (Manhattan's 0.53)
        private const float AvenueBin = 24.5f;                              // on every face, and a second
        private const float AvenueSecondBin = 6.5f;                         // on a face in three: 3.0 per 100 m (Manhattan's 2.75)

        public const int Kinds = 3;              // the kinds of StreetPiece
        public static readonly int FirstAvenue = LayoutStreets.All.Length;   // where 1st Avenue's continuations are counted: no street of the district's
        public const int TreeKinds = 3;          // the game's street trees in holes Manhattan's pavements have most: Broadleaf M1, Birch, Broadleaf L1

        // ---------------------------------------------------------------- the buses

        /// <summary>A point on a street's middle, midway along a row of blocks (1 to 3; 0 the stretch south of 1st Avenue).</summary>
        public static void OnStreet(int street, int row, out float x, out float z)
        {
            x = LayoutRules.StreetX(street);
            z = (LayoutRules.AvenueZ(row - 1) + LayoutRules.AvenueZ(row)) * 0.5f;
        }

        /// <summary>A point on an avenue's middle (row 0 is 1st Avenue), midway along a column's block (1 to 18).</summary>
        public static void OnAvenue(int row, int column, out float x, out float z)
        {
            int west = LayoutRules.WestStreet(column);
            x = (LayoutRules.StreetX(west) + LayoutRules.StreetX(west + 1)) * 0.5f;
            z = LayoutRules.AvenueZ(row);
        }

        /// <summary>
        /// The district's two bus routes, each from beside Midtown North West Station (9th Street) to beside the station by
        /// 2nd Street, both at 1st Avenue: the round one up 9th Street, west along Uptown Avenue, up Willow Street, east the
        /// whole of Crest Avenue, down Lake Street and west along Uptown Avenue to 2nd Street; the middle one up 9th Street,
        /// east along Uptown Avenue and down 2nd Street. Their stops: beside each station, on 9th and 2nd Street midway along
        /// the first block, on Willow and Lake Street midway along the second row, and on the avenues every three blocks
        /// from the band's ends, each midway between two crossings. Into <paramref name="into"/>, each route there and back.
        /// </summary>
        public static void Routes(List<TransitPlan.Route> into)
        {
            into.Clear();
            var vx = new List<float>(16);
            var vz = new List<float>(16);
            var sx = new List<float>(24);
            var sz = new List<float>(24);

            // The round route: the band's west, north and east edges and Uptown Avenue's two ends.
            Begin(vx, vz, sx, sz);
            Street(vx, vz, 0, 1);
            Avenue(vx, vz, 1, 5);
            Avenue(vx, vz, 1, 1);
            Street(vx, vz, LayoutStreets.WestMost, 2);
            Street(vx, vz, LayoutStreets.WestMost, 3);
            Avenue(vx, vz, 3, 1);
            Avenue(vx, vz, 3, LayoutRules.Columns);
            Street(vx, vz, LayoutStreets.EastMost, 3);
            Street(vx, vz, LayoutStreets.EastMost, 2);
            Avenue(vx, vz, 1, LayoutRules.Columns);
            Avenue(vx, vz, 1, 13);
            Street(vx, vz, 7, 1);
            End(vx, vz);
            StopOnStreet(sx, sz, 0, StationZ);
            StopMidStreet(sx, sz, 0, 1);
            StopOnAvenue(sx, sz, 1, 4);
            StopOnAvenue(sx, sz, 1, 1);
            StopMidStreet(sx, sz, LayoutStreets.WestMost, 2);
            for (int column = 1; column < LayoutRules.Columns; column += 3)
            {
                StopOnAvenue(sx, sz, 3, column);
            }
            StopOnAvenue(sx, sz, 3, LayoutRules.Columns);
            StopMidStreet(sx, sz, LayoutStreets.EastMost, 2);
            StopOnAvenue(sx, sz, 1, LayoutRules.Columns);
            StopOnAvenue(sx, sz, 1, LayoutRules.Columns - 3);
            StopMidStreet(sx, sz, 7, 1);
            StopOnStreet(sx, sz, 7, StationZ);
            Add(into, "Uptown round", vx, vz, sx, sz);

            // The middle route: Uptown Avenue between 9th and 2nd Street.
            vx.Clear(); vz.Clear(); sx.Clear(); sz.Clear();
            Begin(vx, vz, sx, sz);
            Street(vx, vz, 0, 1);
            Avenue(vx, vz, 1, LayoutRules.FirstColumn);
            Avenue(vx, vz, 1, 12);
            Street(vx, vz, 7, 1);
            End(vx, vz);
            StopOnStreet(sx, sz, 0, StationZ);
            StopMidStreet(sx, sz, 0, 1);
            StopOnAvenue(sx, sz, 1, 8);
            StopOnAvenue(sx, sz, 1, 11);
            StopMidStreet(sx, sz, 7, 1);
            StopOnStreet(sx, sz, 7, StationZ);
            Add(into, "Uptown Avenue", vx, vz, sx, sz);
        }

        private static void Begin(List<float> vx, List<float> vz, List<float> sx, List<float> sz)
        {
            vx.Add(LayoutRules.StreetX(0));
            vz.Add(RouteFromZ);
        }

        private static void End(List<float> vx, List<float> vz)
        {
            vx.Add(LayoutRules.StreetX(7));
            vz.Add(RouteFromZ);
        }

        private static void Street(List<float> vx, List<float> vz, int street, int row)
        {
            OnStreet(street, row, out float x, out float z);
            vx.Add(x);
            vz.Add(z);
        }

        private static void Avenue(List<float> vx, List<float> vz, int row, int column)
        {
            OnAvenue(row, column, out float x, out float z);
            vx.Add(x);
            vz.Add(z);
        }

        private static void StopOnStreet(List<float> sx, List<float> sz, int street, float z)
        {
            sx.Add(LayoutRules.StreetX(street));
            sz.Add(z);
        }

        private static void StopMidStreet(List<float> sx, List<float> sz, int street, int row)
        {
            OnStreet(street, row, out float x, out float z);
            sx.Add(x);
            sz.Add(z);
        }

        private static void StopOnAvenue(List<float> sx, List<float> sz, int row, int column)
        {
            OnAvenue(row, column, out float x, out float z);
            sx.Add(x);
            sz.Add(z);
        }

        private static void Add(List<TransitPlan.Route> into, string name, List<float> vx, List<float> vz, List<float> sx, List<float> sz)
        {
            var there = new TransitPlan.Route(name, vx.ToArray(), vz.ToArray(), sx.ToArray(), sz.ToArray());
            into.Add(there);
            into.Add(there.Back(name + " back"));
        }

        /// <summary>Every place a route wants a stop, once each (a place both routes stop at counted once): the centre line's points.</summary>
        public static void WantedPlaces(List<TransitPlan.Route> routes, List<float> x, List<float> z)
        {
            x.Clear();
            z.Clear();
            for (int r = 0; r < routes.Count; r++)
            {
                TransitPlan.Route route = routes[r];
                for (int s = 0; s < route.Stops; s++)
                {
                    bool seen = false;
                    for (int k = 0; k < x.Count && !seen; k++)
                    {
                        seen = Math.Abs(x[k] - route.StopX[s]) < 1f && Math.Abs(z[k] - route.StopZ[s]) < 1f;
                    }
                    if (!seen)
                    {
                        x.Add(route.StopX[s]);
                        z.Add(route.StopZ[s]);
                    }
                }
            }
        }

        /// <summary>
        /// Where a stop wanted on a street's or an avenue's middle has its poles: at its kerb on both sides, as the route's
        /// two directions stop there on their own sides of the road.
        /// </summary>
        public static void Poles(List<float> placeX, List<float> placeZ, List<float> x, List<float> z)
        {
            x.Clear();
            z.Clear();
            for (int i = 0; i < placeX.Count; i++)
            {
                bool avenue = OnAnAvenue(placeZ[i]);
                float aside = avenue ? StopKerbAvenue : StopKerbStreet;
                x.Add(placeX[i] - (avenue ? 0f : aside));
                z.Add(placeZ[i] - (avenue ? aside : 0f));
                x.Add(placeX[i] + (avenue ? 0f : aside));
                z.Add(placeZ[i] + (avenue ? aside : 0f));
            }
        }

        private static bool OnAnAvenue(float z)
        {
            for (int row = 0; row <= LayoutRules.Rows; row++)
            {
                if (Math.Abs(z - LayoutRules.AvenueZ(row)) < 1f)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>The reach of one block: how far its farthest corner is from the nearest stop, and how many stops are within reach of every corner.</summary>
        public struct BlockReach
        {
            public int Row;
            public int Column;
            public float Farthest;     // m from its farthest corner to the stop nearest that corner
            public int Stops;          // stops within ReachWanted of its middle
        }

        /// <summary>
        /// Every block of the band (three rows of eighteen, its squares among them) against a set of stops: each block's
        /// farthest corner from its nearest stop and the stops within reach of its middle. Returns the farthest of all, in
        /// metres; float.MaxValue with no stops. The block's ground is taken from its avenue's pavement on the south, so a
        /// block of the first block, which begins behind 1st Avenue's buildings, counts from further south than it is.
        /// </summary>
        public static float Reach(List<float> stopX, List<float> stopZ, List<BlockReach> into)
        {
            into?.Clear();
            float worst = 0f;
            for (int row = 1; row <= LayoutRules.Rows; row++)
            {
                for (int column = 1; column <= LayoutRules.Columns; column++)
                {
                    int west = LayoutRules.WestStreet(column);
                    float x0 = LayoutRules.StreetX(west) + LayoutRules.StreetHalf, x1 = LayoutRules.StreetX(west + 1) - LayoutRules.StreetHalf;
                    float z0 = LayoutRules.AvenueZ(row - 1) + LayoutRules.AvenueHalf, z1 = LayoutRules.AvenueZ(row) - LayoutRules.AvenueHalf;
                    float far = Math.Max(Math.Max(Nearest(stopX, stopZ, x0, z0), Nearest(stopX, stopZ, x0, z1)), Math.Max(Nearest(stopX, stopZ, x1, z0), Nearest(stopX, stopZ, x1, z1)));
                    float mx = (x0 + x1) * 0.5f, mz = (z0 + z1) * 0.5f;
                    int near = 0;
                    for (int s = 0; s < stopX.Count; s++)
                    {
                        float dx = stopX[s] - mx, dz = stopZ[s] - mz;
                        near += dx * dx + dz * dz <= ReachWanted * ReachWanted ? 1 : 0;
                    }
                    into?.Add(new BlockReach { Row = row, Column = column, Farthest = far, Stops = near });
                    worst = Math.Max(worst, far);
                }
            }
            return worst;
        }

        private static float Nearest(List<float> stopX, List<float> stopZ, float x, float z)
        {
            float best = float.MaxValue;
            for (int s = 0; s < stopX.Count; s++)
            {
                float dx = stopX[s] - x, dz = stopZ[s] - z, d = dx * dx + dz * dz;
                if (d < best)
                {
                    best = d;
                }
            }
            return best == float.MaxValue ? float.MaxValue : (float)Math.Sqrt(best);
        }

        // ---------------------------------------------------------------- the furniture

        /// <summary>
        /// Every piece of furniture of the band's pavements, in a fixed order: each street's two pavements along its three
        /// rows (west to east), then each avenue's (south to north, 1st Avenue's continuations' north pavement among them),
        /// each face given its kinds by a rule of its place, so Manhattan's share of faces has each. Nothing stands within a
        /// crossing's module and <see cref="CrossingClear"/> past it, nor within <see cref="StopClear"/> of a stop the routes
        /// want on either pavement of its road. Returns the pieces kept off a stop.
        /// </summary>
        public static int Furniture(List<TransitPlan.Route> routes, List<StreetPlace> into)
        {
            into.Clear();
            var wantedX = new List<float>(32);
            var wantedZ = new List<float>(32);
            WantedPlaces(routes, wantedX, wantedZ);
            int kept = 0;
            for (int street = LayoutStreets.WestMost; street <= LayoutStreets.EastMost; street++)
            {
                int index = street - LayoutStreets.WestMost;
                for (int row = 1; row <= LayoutRules.Rows; row++)
                {
                    for (int side = 0; side < 2; side++)
                    {
                        kept += StreetFace(street, index, row, side, wantedX, wantedZ, into);
                    }
                }
            }
            for (int row = 0; row <= LayoutRules.Rows; row++)
            {
                int index = row == 0 ? FirstAvenue : LayoutStreets.EastMost - LayoutStreets.WestMost + row;
                for (int column = 1; column <= LayoutRules.Columns; column++)
                {
                    if (row == 0 && LayoutRules.IsFirst(1, column))
                    {
                        continue;   // 1st Avenue itself, the game's, between 9th and 1st Street
                    }
                    for (int side = row == 0 ? 1 : 0; side < 2; side++)
                    {
                        kept += AvenueFace(row, column, side, index, wantedX, wantedZ, into);
                    }
                }
            }
            return kept;
        }

        /// <summary>A street face's length between its crossings' clearances, metres.</summary>
        public static float StreetFaceLength => LayoutRules.AvenuePitch - 2f * (LayoutRules.CrossingHalfZ + CrossingClear);

        /// <summary>An avenue face's length, metres.</summary>
        public static float AvenueFaceLength => LayoutRules.StreetPitch - 2f * (LayoutRules.CrossingHalfX + CrossingClear);

        /// <summary>The rule of a face's place that gives it its kinds: the same face, the same kinds, on every machine.</summary>
        public static int FaceRule(int a, int b, int side)
        {
            return (a + 8) * 11 + b * 5 + side * 3;
        }

        private static int StreetFace(int street, int index, int row, int side, List<float> wantedX, List<float> wantedZ, List<StreetPlace> into)
        {
            float middle = LayoutRules.StreetX(street);
            float kerb = side == 0 ? middle - LayoutRules.StreetKerbWest : middle + LayoutRules.StreetKerbEast;
            float inward = side == 0 ? -1f : 1f;
            float from = LayoutRules.AvenueZ(row - 1) + LayoutRules.CrossingHalfZ + CrossingClear;
            int away = side == 0 ? 270 : 90;   // a bench's back to the road, as Manhattan's stand on its streets
            int rule = FaceRule(street, row, side);
            int kept = 0;
            if (rule % 3 == 0)
            {
                kept += Along(StreetPiece.Tree, StreetTrees, 0, kerb + inward * TreeIn, from, true, 0, index, rule, middle, wantedX, wantedZ, into);
            }
            if (rule % 7 == 3)
            {
                kept += Along(StreetPiece.Bench, StreetBenches, 0, kerb + inward * BenchIn, from, true, away, index, 0, middle, wantedX, wantedZ, into);
            }
            if (rule % 6 != 0)
            {
                kept += Along(StreetPiece.Bin, StreetBins, 0, kerb + inward * BinIn, from, true, 0, index, 0, middle, wantedX, wantedZ, into);
            }
            return kept;
        }

        private static int AvenueFace(int row, int column, int side, int index, List<float> wantedX, List<float> wantedZ, List<StreetPlace> into)
        {
            float middle = LayoutRules.AvenueZ(row);
            float kerb = side == 0 ? middle - LayoutRules.AvenueKerb : middle + LayoutRules.AvenueKerb;
            float inward = side == 0 ? -1f : 1f;
            int west = LayoutRules.WestStreet(column);
            float from = LayoutRules.StreetX(west) + LayoutRules.CrossingHalfX + CrossingClear;
            int away = side == 0 ? 180 : 0;
            int street = index;
            int rule = FaceRule(column, row, side) + 1;
            int kept = 0;
            if (rule % 3 == 0)
            {
                kept += Along(StreetPiece.Tree, AvenueTrees, 0, kerb + inward * TreeIn, from, false, 0, street, rule, middle, wantedX, wantedZ, into);
            }
            if (rule % 4 == 2)
            {
                kept += Along(StreetPiece.Bench, new[] { AvenueBench }, 0, kerb + inward * BenchIn, from, false, away, street, 0, middle, wantedX, wantedZ, into);
            }
            kept += Along(StreetPiece.Bin, rule % 3 == 1 ? new[] { AvenueSecondBin, AvenueBin } : new[] { AvenueBin }, 0, kerb + inward * BinIn, from, false, 0, street, 0, middle,
                wantedX, wantedZ, into);
            return kept;
        }

        // The pieces of one kind along a face at its places (metres from its start), each but those in a wanted stop's reach.
        private static int Along(StreetPiece kind, float[] at, int first, float across, float from, bool street, int yaw, int index, int variant, float middle,
            List<float> wantedX, List<float> wantedZ, List<StreetPlace> into)
        {
            int kept = 0;
            for (int i = first; i < at.Length; i++)
            {
                float x = street ? across : from + at[i];
                float z = street ? from + at[i] : across;
                if (NearStop(x, z, street, middle, wantedX, wantedZ))
                {
                    kept++;
                    continue;
                }
                into.Add(new StreetPlace { Kind = kind, X = x, Z = z, Yaw = yaw, Street = index, Variant = kind == StreetPiece.Tree ? (variant + i) % TreeKinds : 0 });
            }
            return kept;
        }

        // A wanted stop on this road (its middle within a metre of the road's), within StopClear along it.
        private static bool NearStop(float x, float z, bool street, float middle, List<float> wantedX, List<float> wantedZ)
        {
            for (int k = 0; k < wantedX.Count; k++)
            {
                if (street ? Math.Abs(wantedX[k] - middle) < 1f && Math.Abs(wantedZ[k] - z) < StopClear
                           : Math.Abs(wantedZ[k] - middle) < 1f && Math.Abs(wantedX[k] - x) < StopClear)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>The pieces of each kind on each street's pavements: [street index in LayoutStreets.All, or FirstAvenue; kind].</summary>
        public static int[,] Counts(List<StreetPlace> places)
        {
            var counts = new int[FirstAvenue + 1, Kinds];
            for (int i = 0; i < places.Count; i++)
            {
                StreetPlace p = places[i];
                if (p.Street >= 0 && p.Street <= FirstAvenue)
                {
                    counts[p.Street, (int)p.Kind]++;
                }
            }
            return counts;
        }
    }
}
