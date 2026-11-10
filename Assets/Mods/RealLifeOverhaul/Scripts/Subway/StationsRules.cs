using System;
using System.Collections.Generic;
using RealLifeOverhaul.Uptown;

namespace RealLifeOverhaul.Subway
{
    /// <summary>What a station's entrance stands on.</summary>
    public enum StationGround
    {
        Pavement = 0,   // an avenue's pavement, at its kerb's side: the way past is kept behind it
        Park = 1        // a park's lawn behind the avenue's pavement, beside the head of the park's spine
    }

    /// <summary>One station of the districts' own: where its entrance stands, its turn, its name's key and the district it is in. Fixed data.</summary>
    public sealed class StationSite
    {
        public readonly string Id;            // letters only, never changes: "birchwood", "summit", "east"
        public readonly string NameKey;       // its name in the texts
        public readonly int Block;            // the square it stands by: Row * 100 + Column
        public readonly StationGround Ground;
        public readonly float X, Z;           // m: the entrance's origin on the ground
        public readonly int Yaw;              // whole degrees, a multiple of 90: its mouth looks along +Z turned by it
        public readonly int District;         // DistrictSplit's: 0 Uptown, 1 Cedar Heights

        public StationSite(string id, string nameKey, int block, StationGround ground, float x, float z, int yaw)
        {
            Id = id;
            NameKey = nameKey;
            Block = block;
            Ground = ground;
            X = x;
            Z = z;
            Yaw = ((yaw % 360) + 360) % 360;
            District = DistrictSplit.OfColumn(block % 100);
        }
    }

    /// <summary>When a station of the districts' own may be taken away.</summary>
    public enum StationRemoval
    {
        Now = 0,          // nothing of the game's holds it
        MapFirst = 1,     // the game's map is open on its list of stations from one of them: the map is shut first, then it goes
        AfterRide = 2     // a ride of the game's begins or ends at one of them: it goes when the rider has come up
    }

    /// <summary>
    /// The subway stations of Uptown and Cedar Heights (docs/features/subway-uptown.md), game-free: where the three entrances
    /// stand and why there, the entrance's own measures (the game's, Build 3682), what each must keep clear of (every lot,
    /// the campus's gates, the parks' and pavements' pieces, the bus stops the routes want, the carriageways), where a rider
    /// comes up and that it is ground both walking meshes hold, where a station goes in the game's sorted list, when one may
    /// be taken away, and the people who come up and go down by the trains' own clock. The same on every machine and in
    /// every version: fixed figures, a mixed number in place of chance. No Unity or game types: NavCheck and EconomyCheck
    /// run the very rules the director calls. COST: lists filled once a load; a handful of sums on a quarter-second look.
    /// </summary>
    public static class StationsRules
    {
        // ---------------------------------------------------------------- the entrance, as the game's own stands (its mesh SM_Subway and its travel point)

        public const float HalfWidth = 1.135f;     // m either side of its middle line (the mesh's bounds, Build 3682)
        public const float Back = 3.2f;            // m behind its origin its stairwell's far end reaches
        public const float Front = 2.03f;          // m ahead of its origin its mouth reaches
        public const float ArriveAhead = 2.809f;   // m ahead of its origin its travel point stands: where a rider comes up
        public const float Height = 3.05f;         // m above the ground its canopy reaches
        public const float CarveShrink = 0.2f;     // m the walking meshes' cut is narrower than the entrance each side: a cut is wider than its box by the walker's radius
        public const float Sunk = 0.115f;          // m the game's entrances stand into the ground they are on (Midtown's origins at -0.06 on slabs at 0.055)
        public const float LawnTop = 0.0492f;      // m: a block's ground in the districts (the game's slabs' 0.05518, laid 6 mm lower: SurfaceRules.GroundDrop)
        public const float PavementTop = 0.058f;   // m: an avenue's pavement in the districts (the game's 0.055, laid 3 mm higher: SurfaceRules.AvenueLift)

        /// <summary>The height an entrance's origin is put at: as far into its ground as the game's own stand into theirs.</summary>
        public static float GroundOf(StationSite site)
        {
            return (site.Ground == StationGround.Park ? LawnTop : PavementTop) - Sunk;
        }

        // ---------------------------------------------------------------- where they stand

        public const float ParkIn = 1.75f;         // m inside a park's edge an entrance's middle line runs: 0.6 m of lawn to the pavement, short of the lawn's trees (3.5 m in)
        public const float SpineGap = 0.3f;        // m between an entrance's mouth and the edge of the park's spine it opens on
        public const float KerbIn = 1.35f;         // m from an avenue's kerb to the middle line of an entrance on its pavement: 0.2 m to the kerb, 1.2 m of pavement behind it
        public const float SchoolAlong = 19.7f;    // m east of the campus block's west corner Birchwood's entrance stands: between the avenue's first lamp and the main gate's crowd
        public const float PavementWidth = LayoutRules.AvenueHalf - LayoutRules.AvenueKerb;   // m: an avenue's pavement, kerb to the block's edge (3.7)

        public const float LotClear = 0.5f;        // m kept between an entrance (and where a rider comes up) and any lot
        public const float PieceClear = 1.2f;      // m kept between an entrance and the middle of a tree, a bench, a bin or a lamp
        public const float LampClear = 0.6f;       // m kept from a lamp post's middle on the pavement the entrance stands on
        public const float StopClear = 28f;        // m kept from a bus stop the routes want: the same stretch the pavements' furniture keeps (StreetsRules.StopClear)
        public const float WayPast = 1.1f;         // m of pavement left behind an entrance that stands on one
        public const float RoadNear = 30f;         // m within which a lane a car drives runs: a taxi and a private driver reach every station
        public const float ArriveRoom = 0.45f;     // m of walking ground wanted round where a rider comes up

        // The avenue module's own lamp posts (SM_RoadModule 120-160-70, the one the district copies along every avenue, Build 3682): metres along
        // the module from its west end, which lies a crossing's half past its west street's middle; 0.95 m in from the kerb.
        public static readonly float[] LampsNorth = { 4.93f, 22.13f, 41.84f };   // the north pavement's
        public static readonly float[] LampsSouth = { 5.02f, 22.92f, 41.84f };   // the south pavement's
        public const float LampIn = 0.95f;         // m from the kerb a lamp post stands

        public const string BirchwoodKey = "reallife_subway_station_birchwood";
        public const string SummitKey = "reallife_subway_station_summit";
        public const string EastKey = "reallife_subway_station_east";

        public const int BirchwoodBlock = LayoutRules.CampusBlock;   // 202: Birchwood School's campus, in Cedar Heights
        public const int SummitBlock = 309;                          // the west park, the band's middle, on Summit Avenue
        public const int EastBlock = 117;                            // the east park, on Uptown Avenue

        private static StationSite[] _sites;

        /// <summary>
        /// The three stations, west to east. Birchwood: on Uptown Avenue's north pavement beside the school's campus, west of its main gate, its mouth
        /// to the gate (the campus is fenced a step inside its block's edge and every other block there is built to its frontage, so the pavement is the
        /// only ground). Summit Park: on the lawn of the middle park's south edge on Summit Avenue, beside the head of the park's spine, its mouth on the
        /// spine. Uptown East: on the lawn of the east park's north edge on Uptown Avenue, the same way. The two parks and the campus are the plan's
        /// three squares, one in the west, one in the middle and one in the east, each in another row: the only blocks that are not lots.
        /// </summary>
        public static IReadOnlyList<StationSite> Sites => _sites ??= Make();

        private static StationSite[] Make()
        {
            float spineOff = ParksSpineHalf + SpineGap + Front;
            LayoutRules.Block(BirchwoodBlock / 100, BirchwoodBlock % 100, out float bx0, out float bz0, out _, out _);
            LayoutRules.Block(SummitBlock / 100, SummitBlock % 100, out float sx0, out float sz0, out float sx1, out _);
            LayoutRules.Block(EastBlock / 100, EastBlock % 100, out float ex0, out _, out float ex1, out float ez1);
            return new[]
            {
                new StationSite("birchwood", BirchwoodKey, BirchwoodBlock, StationGround.Pavement, bx0 + SchoolAlong, bz0 - PavementWidth + KerbIn, 90),
                new StationSite("summit", SummitKey, SummitBlock, StationGround.Park, (sx0 + sx1) * 0.5f + spineOff, sz0 + ParkIn, 270),
                new StationSite("east", EastKey, EastBlock, StationGround.Park, (ex0 + ex1) * 0.5f - spineOff, ez1 - ParkIn, 90)
            };
        }

        public const float ParksSpineHalf = 1.5f;   // m: half a park's spine (ParksRules.SpineHalf; NavCheck holds the two equal)

        /// <summary>The site of an id, or null.</summary>
        public static StationSite Get(string id)
        {
            IReadOnlyList<StationSite> sites = Sites;
            for (int i = 0; i < sites.Count; i++)
            {
                if (sites[i].Id == id)
                {
                    return sites[i];
                }
            }
            return null;
        }

        // ---------------------------------------------------------------- its ground

        /// <summary>The way an entrance's mouth looks, on the ground: (0, 1) at 0 degrees, (1, 0) at 90.</summary>
        public static void Forward(int yaw, out float fx, out float fz)
        {
            switch (((yaw % 360) + 360) % 360)
            {
                case 90: fx = 1f; fz = 0f; break;
                case 180: fx = 0f; fz = -1f; break;
                case 270: fx = -1f; fz = 0f; break;
                default: fx = 0f; fz = 1f; break;
            }
        }

        /// <summary>The ground an entrance covers, west, south, east and north (m): its stairwell behind its origin and its mouth ahead of it.</summary>
        public static void Footprint(StationSite site, out float x0, out float z0, out float x1, out float z1)
        {
            Forward(site.Yaw, out float fx, out float fz);
            float ax = site.X - fx * Back, az = site.Z - fz * Back, bx = site.X + fx * Front, bz = site.Z + fz * Front;
            float halfX = Math.Abs(fz) * HalfWidth, halfZ = Math.Abs(fx) * HalfWidth;
            x0 = Math.Min(ax, bx) - halfX;
            x1 = Math.Max(ax, bx) + halfX;
            z0 = Math.Min(az, bz) - halfZ;
            z1 = Math.Max(az, bz) + halfZ;
        }

        /// <summary>Where a rider comes up: the entrance's travel point, ahead of its mouth.</summary>
        public static void Arrival(StationSite site, out float x, out float z)
        {
            Forward(site.Yaw, out float fx, out float fz);
            x = site.X + fx * ArriveAhead;
            z = site.Z + fz * ArriveAhead;
        }

        /// <summary>The walking meshes' cut for an entrance, in its own frame: its middle ahead of the origin and its size across and along.</summary>
        public static void Carve(out float centreAhead, out float across, out float along)
        {
            centreAhead = (Front - Back) * 0.5f;
            across = HalfWidth * 2f - CarveShrink * 2f;
            along = Front + Back - CarveShrink * 2f;
        }

        // True when a rectangle and a rectangle grown by a margin share ground.
        private static bool Meet(float ax0, float az0, float ax1, float az1, float bx0, float bz0, float bx1, float bz1, float margin)
        {
            return ax0 < bx1 + margin && ax1 > bx0 - margin && az0 < bz1 + margin && az1 > bz0 - margin;
        }

        /// <summary>A lot's ground: its width along its street, its depth back from it.</summary>
        public static void LotGround(Lot lot, out float x0, out float z0, out float x1, out float z1)
        {
            bool avenue = (int)lot.Facing % 180 == 0;   // its street lies north or south of it: an avenue's lot
            float halfX = (avenue ? lot.Width : lot.Depth) * 0.5f, halfZ = (avenue ? lot.Depth : lot.Width) * 0.5f;
            x0 = lot.X - halfX;
            x1 = lot.X + halfX;
            z0 = lot.Z - halfZ;
            z1 = lot.Z + halfZ;
        }

        /// <summary>
        /// Null when an entrance and where its rider comes up keep <see cref="LotClear"/> from every lot of the list (the plan's buildings, its open
        /// runs the player builds on, the big special places); else the lot's id.
        /// </summary>
        public static string MeetsLot(StationSite site, IReadOnlyList<Lot> lots)
        {
            Footprint(site, out float x0, out float z0, out float x1, out float z1);
            Arrival(site, out float ax, out float az);
            for (int i = 0; lots != null && i < lots.Count; i++)
            {
                LotGround(lots[i], out float lx0, out float lz0, out float lx1, out float lz1);
                if (Meet(x0, z0, x1, z1, lx0, lz0, lx1, lz1, LotClear) || Meet(ax, az, ax, az, lx0, lz0, lx1, lz1, LotClear))
                {
                    return lots[i].Id;
                }
            }
            return null;
        }

        /// <summary>
        /// Null when no point of the list (a tree's, a bench's, a bin's, a lamp's or a stop's pole's middle) lies within <paramref name="margin"/> of an
        /// entrance's ground or of where its rider comes up; else <paramref name="what"/> with the point.
        /// </summary>
        public static string MeetsPoint(StationSite site, IReadOnlyList<float> x, IReadOnlyList<float> z, float margin, string what)
        {
            Footprint(site, out float x0, out float z0, out float x1, out float z1);
            Arrival(site, out float ax, out float az);
            for (int i = 0; x != null && i < x.Count; i++)
            {
                float dx = Math.Max(Math.Max(x0 - x[i], 0f), x[i] - x1), dz = Math.Max(Math.Max(z0 - z[i], 0f), z[i] - z1);
                float rx = x[i] - ax, rz = z[i] - az;
                if (dx * dx + dz * dz < margin * margin || rx * rx + rz * rz < margin * margin)
                {
                    return what + " at (" + x[i].ToString("F1", Inv) + ", " + z[i].ToString("F1", Inv) + ")";
                }
            }
            return null;
        }

        /// <summary>Null when a rectangle (a gate's crowd, a campus, a path) keeps <paramref name="margin"/> from an entrance's ground; else <paramref name="what"/>.</summary>
        public static string MeetsBox(StationSite site, float bx0, float bz0, float bx1, float bz1, float margin, string what)
        {
            Footprint(site, out float x0, out float z0, out float x1, out float z1);
            return Meet(x0, z0, x1, z1, Math.Min(bx0, bx1), Math.Min(bz0, bz1), Math.Max(bx0, bx1), Math.Max(bz0, bz1), margin) ? what : null;
        }

        private static readonly System.Globalization.CultureInfo Inv = System.Globalization.CultureInfo.InvariantCulture;

        /// <summary>
        /// The block whose ground or pavements hold an entrance whole, with where its rider comes up (Row * 100 + Column), and whether all of it is on
        /// the block's own ground (a park's lawn) or on its pavement; 0 when any of it reaches a carriageway or leaves the band.
        /// </summary>
        public static int Holds(StationSite site, out bool pavement)
        {
            pavement = false;
            Footprint(site, out float x0, out float z0, out float x1, out float z1);
            Arrival(site, out float ax, out float az);
            x0 = Math.Min(x0, ax);
            x1 = Math.Max(x1, ax);
            z0 = Math.Min(z0, az);
            z1 = Math.Max(z1, az);
            for (int row = 1; row <= LayoutRules.Rows; row++)
            {
                for (int column = 1; column <= LayoutRules.Columns; column++)
                {
                    LayoutRules.Block(row, column, out float bx0, out float bz0, out float bx1, out float bz1);
                    // Its pavements: the east one of its west street, the west one of its east street, its two avenues'.
                    float west = bx0 - (LayoutRules.StreetHalf - LayoutRules.StreetKerbEast), east = bx1 + (LayoutRules.StreetHalf - LayoutRules.StreetKerbWest);
                    float south = bz0 - PavementWidth, north = bz1 + PavementWidth;
                    if (x0 >= west && x1 <= east && z0 >= south && z1 <= north)
                    {
                        pavement = x0 < bx0 || x1 > bx1 || z0 < bz0 || z1 > bz1;
                        return row * 100 + column;
                    }
                }
            }
            return 0;
        }

        /// <summary>
        /// For an entrance on an avenue's pavement: the metres of pavement left between it and the block's edge behind it (the way past), and the
        /// metres between it and the kerb. Both -1 for an entrance on a park's lawn, which leaves the whole pavement.
        /// </summary>
        public static void PavementRoom(StationSite site, out float behind, out float toKerb)
        {
            behind = toKerb = -1f;
            if (site.Ground != StationGround.Pavement)
            {
                return;
            }
            Footprint(site, out _, out float z0, out _, out float z1);
            LayoutRules.Block(site.Block / 100, site.Block % 100, out _, out float bz0, out _, out float bz1);
            bool south = site.Z < bz0;   // on the pavement south of its block, else north of it
            float edge = south ? bz0 : bz1, kerb = south ? bz0 - PavementWidth : bz1 + PavementWidth;
            behind = south ? edge - z1 : z0 - edge;
            toKerb = south ? z0 - kerb : kerb - z1;
        }

        /// <summary>
        /// The lamp posts of the avenue module an entrance on a pavement stands on (into the lists, cleared): the copied module's own, on the
        /// entrance's side of the avenue. None for an entrance on a park's lawn: its lamps stand beyond the pavement.
        /// </summary>
        public static void Lamps(StationSite site, List<float> x, List<float> z)
        {
            x.Clear();
            z.Clear();
            if (site.Ground != StationGround.Pavement)
            {
                return;
            }
            int row = site.Block / 100, column = site.Block % 100;
            LayoutRules.Block(row, column, out float bx0, out float bz0, out _, out float bz1);
            bool south = site.Z < bz0;                       // south of its block: the north pavement of the avenue below it
            float avenue = LayoutRules.AvenueZ(south ? row - 1 : row);
            float moduleWest = bx0 - LayoutRules.StreetHalf + LayoutRules.CrossingHalfX;
            float[] along = south ? LampsNorth : LampsSouth;
            for (int i = 0; i < along.Length; i++)
            {
                x.Add(moduleWest + along[i]);
                z.Add(avenue + (south ? 1f : -1f) * (LayoutRules.AvenueKerb + LampIn));
            }
        }

        // ---------------------------------------------------------------- the game's list of stations

        /// <summary>
        /// Where a station's entry goes among the game's, which are in the order of their names: before the first whose name comes after its own,
        /// so the list stays sorted as the game sorted it; after every name equal to it.
        /// </summary>
        public static int InsertAt(IReadOnlyList<string> names, string name, Comparison<string> order)
        {
            for (int i = 0; names != null && i < names.Count; i++)
            {
                if (order(names[i], name) > 0)
                {
                    return i;
                }
            }
            return names != null ? names.Count : 0;
        }

        /// <summary>True when a typed search keeps a station's entry shown: nothing typed, or the name holds it, as the game's own search reads its names.</summary>
        public static bool Shown(string name, string typed, StringComparison how)
        {
            string wanted = typed != null ? typed.Trim() : string.Empty;
            return wanted.Length == 0 || (name != null && name.IndexOf(wanted, how) >= 0);
        }

        // ---------------------------------------------------------------- standing and going

        /// <summary>Whether the stations stand: their option, the district laid and open at this load, and the game's network there to join.</summary>
        public static bool Wanted(bool option, bool districtOpen, int gameStations)
        {
            return option && districtOpen && gameStations > 0;
        }

        /// <summary>
        /// When a station may be taken away (the option switched off, the district closed, the city going): never under a rider. A ride of the game's
        /// that begins or ends at one of them is let finish first, since the game brings its rider up at the station's own travel point; a map open on
        /// the list of stations from one of them is shut first, since the game lights the station the rider stands at.
        /// </summary>
        public static StationRemoval Removal(bool riding, bool rideTouchesOwn, bool pickingFromOwn)
        {
            if (riding && rideTouchesOwn)
            {
                return StationRemoval.AfterRide;
            }
            return pickingFromOwn ? StationRemoval.MapFirst : StationRemoval.Now;
        }

        // ---------------------------------------------------------------- the city's people on the line

        public const int RidersMost = 2;           // people shown at an entrance at once: one coming up and one going down
        public const float RidersNear = 70f;       // m from an entrance within which its people are shown
        public const float RidersKeep = 95f;       // m to which they are kept, so nobody flickers at the edge
        public const float RiderWalk = 34f;        // m a rider who came up walks before going back to the game's people, and from where one who goes down walks in
        public const float RiderWalkLeast = 20f;   // m at the least
        public const int RushShare = 9;            // tenths of the trains in the weekday rush that bring somebody up at a station
        public const int DayShare = 6;             // by day and in the evening
        public const int NightShare = 2;           // from midnight to five
        public const int DownShare = 5;            // tenths of the trains somebody walks down to, at any hour but the night's (2)

        /// <summary>The train's number of the day at a minute of it, with a train every <paramref name="frequency"/> minutes.</summary>
        public static int Train(int minuteOfDay, int frequency)
        {
            return Math.Max(0, minuteOfDay) / Math.Max(1, frequency);
        }

        // A number mixed from a day, a train and a station: the same on every machine, never chance.
        private static int Mix(int day, int train, int station, int salt)
        {
            unchecked
            {
                uint x = (uint)(day * 73856093) ^ (uint)(train * 19349663) ^ (uint)(station * 83492791) ^ (uint)(salt * 2654435761u);
                x ^= x >> 15;
                x *= 2246822519u;
                x ^= x >> 13;
                x *= 3266489917u;
                x ^= x >> 16;
                return (int)(x & 0x7fffffff);
            }
        }

        private static int ShareAt(int hour, bool weekend)
        {
            if (hour < 5)
            {
                return NightShare;
            }
            bool rush = !weekend && ((hour >= 7 && hour < 9) || (hour >= 16 && hour < 19));
            return rush ? RushShare : DayShare;
        }

        /// <summary>True when a train of the day brings somebody up at a station: nine trains in ten in the weekday rush, six by day, two at night.</summary>
        public static bool ComesUp(int day, int train, int station, int hour, bool weekend)
        {
            return Mix(day, train, station, 1) % 10 < ShareAt(hour, weekend);
        }

        /// <summary>True when somebody walks down to a train of the day at a station: five trains in ten, two at night.</summary>
        public static bool GoesDown(int day, int train, int station, int hour, bool weekend)
        {
            return Mix(day, train, station, 2) % 10 < (hour < 5 ? NightShare : DownShare);
        }

        /// <summary>The seed of a rider's looks (never 0) and whether she is a woman: the same rider for the same train at the same station on every machine.</summary>
        public static int RiderLook(int day, int train, int station, bool down, out bool female)
        {
            int mixed = Mix(day, train, station, down ? 4 : 3);
            female = (mixed & 1) == 0;
            return (mixed >> 1) | 1;
        }

        /// <summary>Which of eight ways round an entrance a rider walks off along, or walks in from (0 east, then every 45 degrees towards north).</summary>
        public static int RiderBearing(int day, int train, int station, bool down, int attempt)
        {
            return (Mix(day, train, station, down ? 6 : 5) + attempt * 3) % 8;
        }

        /// <summary>The point <paramref name="metres"/> from a place along one of the eight ways.</summary>
        public static void Along(float x, float z, int bearing, float metres, out float ox, out float oz)
        {
            double angle = (((bearing % 8) + 8) % 8) * Math.PI / 4.0;
            ox = x + (float)Math.Cos(angle) * metres;
            oz = z + (float)Math.Sin(angle) * metres;
        }
    }
}
