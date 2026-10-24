using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>The kinds of Uptown's big special places, each a copy of one of the game's own whole.</summary>
    public enum BigKind
    {
        Wholesale = 0,    // a drive-in wholesale store: its goods ordered from the phone and delivered, or picked up by car
        GasStation = 1    // a filling station: pumps, a car wash, repair bays
    }

    /// <summary>The sides of a big place whose ground a car crosses from the road: kept clear of parking and of the street's litter.</summary>
    [Flags]
    public enum BigMouth
    {
        Front = 1,   // onto the street its lot fronts
        Right = 2    // onto the cross street on its right, looking out (a corner)
    }

    /// <summary>
    /// One of Uptown's big special places: a building of the game's own special services copied whole (its look with its
    /// workings, NewBuildingsRules.Whole) and its service copied with a name of its own, standing over its own lot and the
    /// lots beside it its ground needs. Fixed data, the same on every machine and in every version: its lots are kept in
    /// saves by id, so they never change once shipped.
    /// </summary>
    public sealed class BigPlace
    {
        public readonly BigKind Kind;
        public readonly string Lot;          // its own lot: its address, and its id among the district's buildings
        public readonly string[] Over;       // the other lots its ground spans (its yard)
        public readonly string[] KeptClear;  // lots kept empty behind or beside it, which its walls would stand too near
        public readonly string Service;      // the game's special service it copies, by its asset's name (Build 3682)
        public readonly string Name;         // its business name: the same in every language, as the game's own are
        public readonly string Design;       // the game's building it stands as, copied whole (NewBuildingsRules.Whole)
        public readonly string Size;         // that building's structure: its interior and the service's layout are the game's for it
        public readonly int Version;
        public readonly float Inset;         // m its front stands back from its ground's front line, as the game's own stands back from its street
        public readonly int Side;            // -1 against its ground's left side, +1 its right, looking out to the street
        public readonly float SideGap;       // m between that side of its ground and its own
        public readonly BigMouth Mouths;

        public BigPlace(BigKind kind, string lot, string[] over, string[] keptClear, string service, string name, string design, string size, int version,
            float inset, int side, float sideGap, BigMouth mouths)
        {
            Kind = kind;
            Lot = lot;
            Over = over;
            KeptClear = keptClear;
            Service = service;
            Name = name;
            Design = design;
            Size = size;
            Version = version;
            Inset = inset;
            Side = side;
            SideGap = sideGap;
            Mouths = mouths;
        }
    }

    /// <summary>
    /// Uptown's big special places, game-free (EconomyCheck's UptownBigChecks): a wholesale store and a filling station,
    /// each a copy of one of the game's own whole on Uptown ground big enough for it, and which lots they take from the
    /// district's plan, where on them each stands (as the game's own stands to its streets, measured from Build 3682's
    /// scenes, not-in-repo filllab/facts/specials_scan.py and specials_parts.py), and the mouths a car crosses into them by,
    /// kept clear of parked cars and litter. Every answer comes from fixed data and the arguments alone.
    /// COST: nothing per frame; a few loops over two places at a load.
    /// </summary>
    public static class BigPlacesRules
    {
        public const string SpecialType = BuildingsRules.SpecialType;
        public const float MouthIntoRoad = 3f;   // m a mouth reaches past the kerb: over the kerb's parking lane, which the game lays 1.3 m in and 2.5 m wide

        // The one place Uptown's big places are chosen: a re-plan of the lots changes these rows alone. The store, Total
        // Produce Trading's drive-in, over block 108's whole Uptown Avenue front (7th to 6th Street): 10.5 m back so its car
        // park lines (10.47 m past its walls) end at the pavement, against the west end so its garage doors open east onto a
        // yard about 22 m wide; 7th Street 1008 behind it kept empty (its back wall 1.2 m off that lot). The station, United
        // Gasoline's, on the north-east corner of 9th Street and Uptown Avenue as United Gasoline stands on 2nd Street and 1st
        // Avenue: the pumps' open end 1.87 m behind the avenue's pavement, its forecourt and repair bays 5.11 m from the street's.
        /// <summary>The two places, in a fixed order.</summary>
        public static readonly BigPlace[] All =
        {
            new BigPlace(BigKind.Wholesale, "uptown_1_08_01", new[] { "uptown_1_08_02" }, new[] { "uptown_1_08_06" }, "MurrayHillWholesaleStore",
                "Northgate Wholesale", "DriveInMurrayHill", "ba:buildingsize_e", 1, 10.5f, -1, 0.5f, BigMouth.Front),
            new BigPlace(BigKind.GasStation, "uptown_2_06_04", new string[0], new string[0], "UnitedGasoline",
                "Summit Fuel", "GasStationBuilding", "ba:buildingsize_a", 2, 1.87f, 1, 3.61f, BigMouth.Front | BigMouth.Right),
        };

        /// <summary>True for a lot one of the places stands on, spans or keeps clear: never one of the plan's ordinary buildings.</summary>
        public static bool Holds(string lotId)
        {
            return HolderOf(lotId) != null;
        }

        /// <summary>The place that stands on, spans or keeps clear that lot, or null.</summary>
        public static BigPlace HolderOf(string lotId)
        {
            if (string.IsNullOrEmpty(lotId))
            {
                return null;
            }
            for (int i = 0; i < All.Length; i++)
            {
                BigPlace p = All[i];
                if (p.Lot == lotId || Array.IndexOf(p.Over, lotId) >= 0 || Array.IndexOf(p.KeptClear, lotId) >= 0)
                {
                    return p;
                }
            }
            return null;
        }

        /// <summary>The place whose own lot that is (its address), or null.</summary>
        public static BigPlace On(string lotId)
        {
            for (int i = 0; lotId != null && i < All.Length; i++)
            {
                if (All[i].Lot == lotId)
                {
                    return All[i];
                }
            }
            return null;
        }

        /// <summary>The place of that kind.</summary>
        public static BigPlace Of(BigKind kind)
        {
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i].Kind == kind)
                {
                    return All[i];
                }
            }
            return null;
        }

        /// <summary>
        /// The ground a place stands on: its own lot (its address, its street, its facing) grown to the rectangle round it and
        /// the lots it spans (BuildingsRules.SpanGround). False when one of its lots is not in <paramref name="plan"/>.
        /// </summary>
        public static bool Ground(BigPlace place, IList<BuildingsLot> plan, out BuildingsLot ground)
        {
            ground = default;
            if (place == null || plan == null)
            {
                return false;
            }
            var span = new List<BuildingsLot>(1 + place.Over.Length);
            if (!Find(plan, place.Lot, out BuildingsLot own))
            {
                return false;
            }
            span.Add(own);
            for (int i = 0; i < place.Over.Length; i++)
            {
                if (!Find(plan, place.Over[i], out BuildingsLot other) || other.Block != own.Block)
                {
                    return false;
                }
                span.Add(other);
            }
            if (!BuildingsRules.SpanGround(span, out float x, out float z, out float width, out float depth))
            {
                return false;
            }
            ground = own;
            ground.X = x;
            ground.Z = z;
            ground.Width = (int)Math.Floor(width + 0.001f);   // whole metres, never past the lots' own edges
            ground.Depth = (int)Math.Floor(depth + 0.001f);
            return true;
        }

        private static bool Find(IList<BuildingsLot> plan, string id, out BuildingsLot lot)
        {
            for (int i = 0; i < plan.Count; i++)
            {
                if (plan[i].Id == id)
                {
                    lot = plan[i];
                    return true;
                }
            }
            lot = default;
            return false;
        }

        /// <summary>
        /// The mouths a car crosses into a place by, as rectangles of the world (into the caller's list, not cleared): from its
        /// ground's front line over the pavement to <see cref="MouthIntoRoad"/> past the avenue's kerb, its ground's width
        /// across; and with <see cref="BigMouth.Right"/> from its ground's right side over the cross street's pavement to past
        /// that street's kerb, its ground's depth along. The district's grid lies along the world's axes, so a lot faces 0,
        /// 90, 180 or 270 and the rectangles do too.
        /// </summary>
        public static void Mouths(BigPlace place, BuildingsLot ground, List<GroundRect> into)
        {
            if (place == null)
            {
                return;
            }
            int facing = ((int)Math.Round(ground.Facing / 90.0) % 4 + 4) % 4;   // 0 north, 1 east, 2 south, 3 west
            float halfW = ground.Width * 0.5f, halfD = ground.Depth * 0.5f;
            if ((place.Mouths & BigMouth.Front) != 0)
            {
                into.Add(Out(ground.X, ground.Z, facing, halfW, halfD));
            }
            if ((place.Mouths & BigMouth.Right) != 0)
            {
                // The right, looking out: facing north it is east, and so round.
                int right = (facing + 1) % 4;
                into.Add(Out(ground.X, ground.Z, right, right % 2 == facing % 2 ? halfW : halfD, right % 2 == facing % 2 ? halfD : halfW));
            }
        }

        // From the side of the ground's rectangle facing that way out to the road's kerb and past it: an avenue's when facing
        // north or south, a street's when east or west. halfAlong is the rectangle's half length along that side.
        private static GroundRect Out(float cx, float cz, int way, float halfAlong, float halfOut)
        {
            if (way == 0 || way == 2)
            {
                float edge = way == 0 ? cz + halfOut : cz - halfOut;
                float road = LayoutRules.AvenueZ((int)Math.Round((edge - LayoutRules.FirstAvenueZ) / LayoutRules.AvenuePitch));
                float far = way == 0 ? road - LayoutRules.AvenueKerb + MouthIntoRoad : road + LayoutRules.AvenueKerb - MouthIntoRoad;
                return new GroundRect(cx - halfAlong, edge, cx + halfAlong, far);
            }
            float side = way == 1 ? cx + halfOut : cx - halfOut;
            float street = LayoutRules.StreetX((int)Math.Round((side - LayoutRules.StreetFirstX) / LayoutRules.StreetPitch));
            float reach = way == 1 ? street - LayoutRules.StreetKerbWest + MouthIntoRoad : street + LayoutRules.StreetKerbEast - MouthIntoRoad;
            return new GroundRect(side, cz - halfAlong, reach, cz + halfAlong);
        }

        /// <summary>True when the upright box (world metres) meets any of the mouths: a parking lane there is not laid, a bag of litter is put away.</summary>
        public static bool Meets(IList<GroundRect> mouths, float minX, float minZ, float maxX, float maxZ)
        {
            for (int i = 0; mouths != null && i < mouths.Count; i++)
            {
                GroundRect m = mouths[i];
                if (minX <= m.X2 && maxX >= m.X1 && minZ <= m.Z2 && maxZ >= m.Z1)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>True when the point lies in any of the mouths.</summary>
        public static bool In(IList<GroundRect> mouths, float x, float z)
        {
            return Meets(mouths, x, z, x, z);
        }
    }
}
