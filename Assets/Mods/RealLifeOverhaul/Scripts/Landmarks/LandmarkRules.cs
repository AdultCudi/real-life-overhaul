using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Landmarks
{
    /// <summary>Uptown's three landmarks. Saved as a number: never renumbered.</summary>
    public enum LandmarkKind
    {
        Tower = 0,     // Summit Tower: the tallest office design of the city's that fits, on a plaza
        Mall = 1,      // Uptown Galleria: the largest shop design that fits, its shops to let round it
        Stadium = 2,   // Crest Field: a stadium of the mod's own, home of Uptown FC
        Count = 3
    }

    /// <summary>Where a landmark stands in its life. Saved as a number: new ones go at the end.</summary>
    public enum LandmarkStage
    {
        Planned = 0,     // not announced yet
        Announced = 1,   // in the City Ledger, its site settled
        Tendered = 2,    // out to tender in the construction game
        Building = 3,    // works going on: the player's company, a rival or the city's own works
        Finished = 4,    // handed over, its opening ahead
        Opened = 5       // open to the city
    }

    /// <summary>One of Uptown's lots as the landmarks' rules read it (made from UptownLots by the game side). Game-free.</summary>
    public struct SiteLot
    {
        public string Id;
        public int Block;          // Row * 100 + Column, the plan's
        public bool First;         // in the first block, laid at every load
        public bool FirstDay;      // built from the district's first day (DistrictBuildings.FirstDay): never a landmark's
        public bool Avenue;        // on the avenue north of its block (its facing 0), not a side street
        public float X, Z;         // m: its middle
        public int Yaw;            // whole degrees, as a plot takes its land: its front at -Z turned by it
        public int Width, Depth;   // m along its street and back from it
        public int Class;          // UptownLots' LotClass: small, office, big, warehouse
    }

    /// <summary>A rectangle of ground along the world's axes, in metres.</summary>
    public struct SiteRect
    {
        public float X0, Z0, X1, Z1;

        public SiteRect(float x0, float z0, float x1, float z1)
        {
            X0 = Math.Min(x0, x1);
            Z0 = Math.Min(z0, z1);
            X1 = Math.Max(x0, x1);
            Z1 = Math.Max(z0, z1);
        }

        public float Width => X1 - X0;
        public float Depth => Z1 - Z0;
        public float MidX => (X0 + X1) * 0.5f;
        public float MidZ => (Z0 + Z1) * 0.5f;
        public bool Empty => X1 <= X0 || Z1 <= Z0;

        /// <summary>Metres from a point to the rectangle, 0 inside it.</summary>
        public float Distance(float x, float z)
        {
            float dx = x < X0 ? X0 - x : x > X1 ? x - X1 : 0f;
            float dz = z < Z0 ? Z0 - z : z > Z1 ? z - Z1 : 0f;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }

        public bool Holds(float x, float z, float margin)
        {
            return x >= X0 - margin && x <= X1 + margin && z >= Z0 - margin && z <= Z1 + margin;
        }
    }

    /// <summary>The ground the works stand on, as the city's construction programme takes a plot: its middle, the land's turn, its width and depth.</summary>
    public struct PlotLand
    {
        public float X, Z;
        public int Yaw;
        public int Width, Depth;
    }

    /// <summary>A landmark's site on Uptown's first block: its lots, what stands where, its ground and the works' plot.</summary>
    public sealed class LandmarkSite
    {
        public LandmarkKind Kind;
        public int Block;
        public string Address;                                  // the avenue lot whose street and number the landmark takes
        public readonly List<string> Lots = new List<string>(12);     // every lot of the site, the address's first, then by id
        public readonly List<string> Anchor = new List<string>(12);   // the lots the main building stands over, the address's first
        public readonly List<string> Units = new List<string>(8);     // the mall's shops, one on each lot
        public SiteRect Ground;                                 // round every lot of the site
        public SiteRect AnchorGround;                           // round the anchor's lots
        public PlotLand Plot;                                   // the works' ground
    }

    /// <summary>A piece of what a landmark stands in the street: a prop's id, its middle on the ground, its turn and its stretch.</summary>
    public struct LandmarkPiece
    {
        public string Id;
        public float X, Z;
        public float Yaw;          // degrees, as the game turns things (0 faces +Z)
        public float StretchX;     // along its own X, 1 for the prop's own size
        public float StretchZ;
        public bool Solid;         // stands in the way: the player walks round it, and the walking meshes are cut by it
    }

    /// <summary>A square of pavement a crowd of the game's pedestrians stands on: its middle, its size along X and along Z.</summary>
    public struct CrowdSquare
    {
        public float X, Z;
        public float Width, Depth;
    }

    /// <summary>
    /// The rules of Uptown's landmarks (docs/features/landmarks.md): which block of the first block each stands on and which
    /// of its lots, the works' plot, the timetable a city's megaproject keeps, the traffic its crowds bring to the lots
    /// near it, its jobs, the stadium's fixtures and results and the pieces each stands in the street. Every answer comes
    /// from the arguments alone, picks by FNV-1a hashes: the same on every machine with the same mod, in any order the
    /// lots come. Game-free (EconomyCheck's LandmarksChecks).
    /// COST: nothing per frame; a site is a pass over the first block's lots, asked on a day's pass or a page.
    /// </summary>
    public static class LandmarkRules
    {
        public const string SitePrefix = "landmark:";   // a landmark's site among the construction game's lots: never a lot of the plan's

        // ---- where they stand: each landmark's blocks in the order it takes them (Row * 100 + Column; the first block is 106 to 113)

        private static readonly int[][] Blocks =
        {
            new[] { 111, 112, 113, 110, 107, 106, 108, 109 },   // the tower: between 4th and 3rd Street, beside the stadium
            new[] { 113, 112, 111, 110, 107, 106, 108, 109 },   // the mall: between 2nd and 1st Street, nearest the east side
            new[] { 110, 112, 113, 111, 107, 106, 108, 109 }    // the stadium: between 5th and 4th Street, where the buses end
        };

        public const float StadiumLeastDepth = 58f;   // m of open ground back from the avenue a stadium's bowl needs: its stands, its pitch and its forecourt
        public const int PlotMost = 60;               // m each way at most of the works' plot (the city's programme, PlotRules.MostLand)

        // ---- the timetable

        private static readonly int[] AnnounceDay = { 12, 26, 40 };      // the game day each is announced on at the earliest: the same on every machine of a session
        private static readonly int[] AnnounceAfter = { 3, 17, 31 };     // days after the programme first ran in a save, alone
        public const int CityLead = 3;                                   // days after the announcement the city's own works begin, when nobody bids
        public const int LapseDays = 3;                                  // days after a tender should have closed that the city takes it on itself
        private static readonly int[] CityWorkingDays = { 48, 40, 48 };  // the city's own works, in working days (Monday to Friday)
        public const int OpeningFrom = 10, OpeningTo = 18;               // game hours of the opening's ceremony, as the construction game's ribbon
        private static readonly int[] PhaseEnds = { 400, 600, 800 };     // thousandths of the works: groundworks, frame, envelope; then the fit-out (the plot's stages)

        // ---- what they do for the district

        private static readonly float[] LiftReach = { 130f, 150f, 170f };   // m from a landmark's ground within which its crowds lift a lot's traffic
        private static readonly int[] LiftMost = { 8, 10, 6 };              // traffic points next to it, falling to none at the reach
        public const int LiftCap = 14;                                      // points at most, every landmark together
        public const float MatchReach = 200f;                               // m round the stadium a match day's crowds reach
        public const int MatchLift = 12;                                    // traffic points on a match day near the stadium, over its own lift
        private static readonly int[] Jobs = { 1400, 520, 260 };            // people working there once it is open
        public const int MatchJobs = 650;                                   // stewards, catering and the rest on a match day

        // ---- the stadium's season

        public const int MatchWeekday = 5;     // Saturday (0 is a Monday; the game's day 1 is a Monday)
        public const int KickOff = 19;         // game hour
        public const int CrowdFrom = 17;       // game hours the crowds stand outside
        public const int CrowdTo = 22;
        public const int Capacity = 22000;     // Crest Field's seats
        public const int AttendanceLeast = 12000;
        public const int OpponentCount = 8;    // the league's other clubs (texts reallife_lmk_club_0 to 7)

        // ---- the money and the works, as the construction game prices them (its BuildUse numbers: 1 shops, 2 offices, 3 warehouses)

        private static readonly int[] Use = { 2, 1, 3 };          // the tower's offices, the mall's shops, the stadium's steel and concrete
        private static readonly int[] Area = { 14000, 9000, 16000 };   // m2 of floor the works are priced on
        private static readonly int[] Storeys = { 10, 3, 3 };      // storeys the works are priced on (the frame shows ten at most)

        public static int UseOf(LandmarkKind kind) => Use[Index(kind)];
        public static int AreaOf(LandmarkKind kind) => Area[Index(kind)];
        public static int StoreysOf(LandmarkKind kind) => Storeys[Index(kind)];
        public static int JobsOf(LandmarkKind kind) => Jobs[Index(kind)];
        public static string SiteId(LandmarkKind kind) => SitePrefix + Name(kind);
        public static bool IsSite(string lot) => lot != null && lot.StartsWith(SitePrefix, StringComparison.Ordinal);

        /// <summary>The landmark a site id names, or -1.</summary>
        public static int KindOf(string site)
        {
            for (int k = 0; k < (int)LandmarkKind.Count; k++)
            {
                if (string.Equals(site, SiteId((LandmarkKind)k), StringComparison.Ordinal))
                {
                    return k;
                }
            }
            return -1;
        }

        /// <summary>The landmark's own word, in ids and in the log.</summary>
        public static string Name(LandmarkKind kind)
        {
            switch (kind)
            {
                case LandmarkKind.Tower: return "tower";
                case LandmarkKind.Mall: return "mall";
                default: return "stadium";
            }
        }

        private static int Index(LandmarkKind kind) => Math.Max(0, Math.Min((int)LandmarkKind.Count - 1, (int)kind));

        // ---------------------------------------------------------------- the site

        /// <summary>The blocks a landmark takes, in its order.</summary>
        public static IReadOnlyList<int> BlocksOf(LandmarkKind kind) => Blocks[Index(kind)];

        /// <summary>
        /// True for a block some landmark takes first (each one's own block): Uptown keeps these open from the first day, so the
        /// sites stand where they were planned and the rest of the district is built up. Fixed data, whatever the option says.
        /// </summary>
        public static bool OwnBlock(int block)
        {
            for (int k = 0; k < Blocks.Length; k++)
            {
                if (Blocks[k].Length > 0 && Blocks[k][0] == block)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// The site a landmark takes: of its blocks in order, the first where every lot is in the first block, every lot not
        /// built from the first day is free (<paramref name="claimed"/> false: no building on it or finished there, nobody's
        /// land, no development, no project) and none of it is another landmark's (<paramref name="heldBlocks"/>), with lots on
        /// the avenue north of it (and for the stadium ground deep enough for its bowl). Its lots are every lot of that block
        /// not built from the first day. Null when no block will do yet. Into <paramref name="into"/> (filled afresh).
        /// </summary>
        public static bool Site(LandmarkKind kind, IList<SiteLot> lots, Func<string, bool> claimed, IList<int> heldBlocks, LandmarkSite into)
        {
            int[] order = Blocks[Index(kind)];
            for (int b = 0; b < order.Length; b++)
            {
                if (heldBlocks != null && heldBlocks.Contains(order[b]))
                {
                    continue;
                }
                if (SiteOn(kind, order[b], lots, claimed, into))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>The site a landmark has on a block, false when the block will not do (see <see cref="Site"/>).</summary>
        public static bool SiteOn(LandmarkKind kind, int block, IList<SiteLot> lots, Func<string, bool> claimed, LandmarkSite into)
        {
            into.Kind = kind;
            into.Block = block;
            into.Address = null;
            into.Lots.Clear();
            into.Anchor.Clear();
            into.Units.Clear();
            int addressAt = -1;
            float middle = float.NaN, best = float.MaxValue;
            float x0 = float.MaxValue, z0 = float.MaxValue, x1 = float.MinValue, z1 = float.MinValue;
            for (int i = 0; lots != null && i < lots.Count; i++)
            {
                if (lots[i].Block == block && !lots[i].FirstDay)
                {
                    Bounds(lots[i], ref x0, ref z0, ref x1, ref z1);
                }
            }
            if (x0 == float.MaxValue)
            {
                return false;
            }
            middle = (x0 + x1) * 0.5f;
            for (int i = 0; i < lots.Count; i++)
            {
                SiteLot l = lots[i];
                if (l.Block != block || l.FirstDay)
                {
                    continue;
                }
                if (!l.First || string.IsNullOrEmpty(l.Id) || (claimed != null && claimed(l.Id)))
                {
                    return false;
                }
                into.Lots.Add(l.Id);
                float off = Math.Abs(l.X - middle);
                if (l.Avenue && (off < best - 0.001f || (Math.Abs(off - best) <= 0.001f && string.CompareOrdinal(l.Id, lots[addressAt].Id) < 0)))
                {
                    best = off;
                    addressAt = i;
                }
            }
            if (addressAt < 0)
            {
                return false;
            }
            into.Address = lots[addressAt].Id;
            SortIds(into.Lots);
            into.Lots.Remove(into.Address);
            into.Lots.Insert(0, into.Address);
            into.Ground = new SiteRect(x0, z0, x1, z1);
            if (kind == LandmarkKind.Stadium && into.Ground.Depth < StadiumLeastDepth)
            {
                return false;
            }
            Anchored(kind, lots, into);
            into.Plot = PlotOf(into.Ground, lots[addressAt].Yaw);
            return true;
        }

        // The anchor's lots and the mall's shops: the tower and the stadium stand over every lot of the site; the mall's main
        // building over the avenue's lots and the side pair nearest the avenue, a shop on each side lot behind them.
        private static void Anchored(LandmarkKind kind, IList<SiteLot> lots, LandmarkSite into)
        {
            if (kind != LandmarkKind.Mall)
            {
                into.Anchor.AddRange(into.Lots);
                into.AnchorGround = into.Ground;
                return;
            }
            float nearest = float.MinValue;   // the side lots' row nearest the avenue: their middles furthest north
            for (int i = 0; i < lots.Count; i++)
            {
                if (lots[i].Block == into.Block && !lots[i].FirstDay && !lots[i].Avenue)
                {
                    nearest = Math.Max(nearest, lots[i].Z);
                }
            }
            float x0 = float.MaxValue, z0 = float.MaxValue, x1 = float.MinValue, z1 = float.MinValue;
            for (int k = 0; k < into.Lots.Count; k++)
            {
                int i = IndexOf(lots, into.Lots[k]);
                SiteLot l = lots[i];
                if (l.Avenue || Math.Abs(l.Z - nearest) < 0.01f)
                {
                    into.Anchor.Add(l.Id);
                    Bounds(l, ref x0, ref z0, ref x1, ref z1);
                }
                else
                {
                    into.Units.Add(l.Id);
                }
            }
            into.AnchorGround = new SiteRect(x0, z0, x1, z1);
        }

        /// <summary>The works' plot on a site's ground: as wide and deep as the ground, at most <see cref="PlotMost"/> each way, its front on the avenue, in the middle of its width.</summary>
        public static PlotLand PlotOf(SiteRect ground, int yaw)
        {
            int width = Math.Min(PlotMost, (int)Math.Floor(ground.Width)), depth = Math.Min(PlotMost, (int)Math.Floor(ground.Depth));
            return new PlotLand { X = ground.MidX, Z = ground.Z1 - depth * 0.5f, Yaw = yaw, Width = width, Depth = depth };
        }

        private static void Bounds(SiteLot l, ref float x0, ref float z0, ref float x1, ref float z1)
        {
            bool alongX = l.Avenue;   // a lot on an avenue runs along X; one on a street along Z
            float hx = (alongX ? l.Width : l.Depth) * 0.5f, hz = (alongX ? l.Depth : l.Width) * 0.5f;
            x0 = Math.Min(x0, l.X - hx);
            x1 = Math.Max(x1, l.X + hx);
            z0 = Math.Min(z0, l.Z - hz);
            z1 = Math.Max(z1, l.Z + hz);
        }

        private static int IndexOf(IList<SiteLot> lots, string id)
        {
            for (int i = 0; i < lots.Count; i++)
            {
                if (string.Equals(lots[i].Id, id, StringComparison.Ordinal))
                {
                    return i;
                }
            }
            return -1;
        }

        // An insertion sort, ordinal: a dozen ids, once a day at most.
        private static void SortIds(List<string> ids)
        {
            for (int i = 1; i < ids.Count; i++)
            {
                string k = ids[i];
                int j = i - 1;
                while (j >= 0 && string.CompareOrdinal(ids[j], k) > 0)
                {
                    ids[j + 1] = ids[j];
                    j--;
                }
                ids[j + 1] = k;
            }
        }

        // ---------------------------------------------------------------- the timetable

        /// <summary>
        /// The day a landmark is announced on. In a Going Public session, the same fixed day on every machine; alone, also no
        /// sooner than a few days after the programme first ran in the save (<paramref name="anchor"/>), so a save that met the
        /// district late still sees each announced in turn.
        /// </summary>
        public static int AnnounceOn(LandmarkKind kind, int anchor, bool session)
        {
            int k = Index(kind);
            return session ? AnnounceDay[k] : Math.Max(AnnounceDay[k], anchor + AnnounceAfter[k]);
        }

        /// <summary>0 for a Monday to 6 for a Sunday (the game's day 1 is a Monday).</summary>
        public static int Weekday(int day)
        {
            return ((day - 1) % 7 + 7) % 7;
        }

        /// <summary>The day so many working days (Monday to Friday) after a day ends.</summary>
        public static int AfterWorkingDays(int day, int working)
        {
            int d = day, left = Math.Max(0, working);
            while (left > 0)
            {
                d++;
                if (Weekday(d) < 5)
                {
                    left--;
                }
            }
            return d;
        }

        /// <summary>The day the city's own works, begun on a day, hand a landmark over.</summary>
        public static int CityDue(LandmarkKind kind, int start)
        {
            return AfterWorkingDays(start, CityWorkingDays[Index(kind)]);
        }

        /// <summary>The working days the city's own works on a landmark take.</summary>
        public static int CityDays(LandmarkKind kind) => CityWorkingDays[Index(kind)];

        /// <summary>The phase works are in at a share of their hours (0 to 1): 0 groundworks, 1 the frame, 2 the envelope, 3 the fit-out.</summary>
        public static int Phase(double progress)
        {
            int p = (int)Math.Floor(Math.Max(0.0, Math.Min(1.0, progress)) * 1000.0);
            for (int i = 0; i < PhaseEnds.Length; i++)
            {
                if (p < PhaseEnds[i])
                {
                    return i;
                }
            }
            return PhaseEnds.Length;
        }

        /// <summary>The share of the works at which a phase ends, in thousandths.</summary>
        public static int PhaseEnd(int phase) => phase >= 0 && phase < PhaseEnds.Length ? PhaseEnds[phase] : 1000;

        /// <summary>
        /// The works' pace as the city's programme asks it for a landmark's site the timetable hands over (a rival's, the
        /// city's): the hours a working hour does, so that the site stands just short of its end on its due day, which hands it
        /// over. Hours given back no further than 99.5% of the whole.
        /// </summary>
        public static float Pace(double done, double needed, float hours, int day, int start, int due)
        {
            if (needed <= 0.0)
            {
                return hours;
            }
            int span = Math.Max(1, due - start);
            double wanted = needed * Math.Max(0.0, Math.Min(0.995, (day - start + 1) / (double)span));
            double left = Math.Max(0.0, wanted - done);
            return (float)Math.Max(0.0, Math.Min(Math.Max(hours, 0f) * 4.0, left));
        }

        // ---------------------------------------------------------------- what they do for the district

        /// <summary>
        /// The traffic points a lot at (x, z) gets from the landmarks that are open (their kinds and grounds, as many as
        /// <paramref name="count"/>): from each within its reach, the most next to it falling to none at the reach, all of them
        /// together at most <see cref="LiftCap"/>.
        /// </summary>
        public static int Lift(float x, float z, IList<LandmarkKind> kinds, IList<SiteRect> grounds, int count)
        {
            int sum = 0;
            for (int i = 0; i < count && kinds != null && grounds != null && i < kinds.Count && i < grounds.Count; i++)
            {
                int k = Index(kinds[i]);
                float d = grounds[i].Distance(x, z);
                if (d < LiftReach[k])
                {
                    sum += (int)Math.Round(LiftMost[k] * (1.0 - d / LiftReach[k]), MidpointRounding.AwayFromZero);
                }
            }
            return Math.Max(0, Math.Min(LiftCap, sum));
        }

        /// <summary>The traffic points a match day adds to a lot at (x, z) near the stadium's ground: <see cref="MatchLift"/> within <see cref="MatchReach"/>.</summary>
        public static int MatchDayLift(float x, float z, SiteRect stadium)
        {
            return stadium.Empty || stadium.Distance(x, z) >= MatchReach ? 0 : MatchLift;
        }

        // ---------------------------------------------------------------- the stadium's season

        /// <summary>True for a home match day of Uptown FC: a Saturday after the stadium opened.</summary>
        public static bool MatchDay(int day, int opened)
        {
            return opened > 0 && day > opened && Weekday(day) == MatchWeekday;
        }

        /// <summary>The first home match day from a day on (that day too).</summary>
        public static int NextMatch(int from, int opened)
        {
            int d = Math.Max(from, opened + 1);
            for (int i = 0; i < 8; i++, d++)
            {
                if (MatchDay(d, opened))
                {
                    return d;
                }
            }
            return d;
        }

        /// <summary>The club Uptown FC meet on a day: each of the league's others in turn, the order shuffled by the day's week.</summary>
        public static int Opponent(int day)
        {
            int week = (day - 1) / 7;
            return (int)((uint)Hash("opponent|" + (week / OpponentCount)) % OpponentCount + week) % OpponentCount;
        }

        /// <summary>The score of a day's match: Uptown FC's goals, then the visitors'.</summary>
        public static void Score(int day, out int home, out int away)
        {
            uint h = (uint)Hash("score|" + day);
            int[] homeGoals = { 0, 1, 1, 2, 2, 2, 3, 3, 4 };   // a home side scores a little more
            int[] awayGoals = { 0, 0, 1, 1, 1, 2, 2, 3 };
            home = homeGoals[h % (uint)homeGoals.Length];
            away = awayGoals[(h / 16u) % (uint)awayGoals.Length];
        }

        /// <summary>The crowd at a day's match: from <see cref="AttendanceLeast"/> to <see cref="Capacity"/>, fuller the longer the ground has been open.</summary>
        public static int Attendance(int day, int opened)
        {
            int weeks = Math.Max(0, (day - opened) / 7);
            int spread = Capacity - AttendanceLeast;
            int settled = Math.Min(spread, spread * Math.Min(weeks, 10) / 10);
            int luck = (int)((uint)Hash("crowd|" + day) % 2400u);
            return Math.Max(AttendanceLeast, Math.Min(Capacity, AttendanceLeast + settled / 2 + luck * 2));
        }

        // ---------------------------------------------------------------- the stadium's pieces

        public const string Stand = "stadium_stand";            // a 10 m section of a main stand: 12 m deep, roofed; its seats face +Z
        public const string End = "stadium_end";                // a 10 m section of an end stand: 9 m deep, open; its seats face +Z
        public const string Corner = "stadium_corner";          // a stair tower in a corner: 9 m along X, 12 m along Z, alike from every side
        public const string Floodlight = "stadium_floodlight";  // a mast 34 m high, its lamp frame facing +Z
        public const string Lamps = "stadium_lamps";            // that frame's lamps, lit at night (the glow material), stood where the mast stands
        public const string Pitch = "stadium_pitch";            // the field: 52 x 48 m of grass, lines and a running track
        public const string Turnstile = "turnstile";            // the first bundle's: walked past along Z on its +X side
        public const float StandDepth = 12f, EndDepth = 9f, Section = 10f, Forecourt = 5f;
        public const float StandHeight = 15f, EndHeight = 8f, CornerHeight = 10f, FloodlightHeight = 34f;
        public const float PitchWidth = 52f, PitchDepth = 48f;  // the pitch prop's own size: stretched to the field the stands leave
        public const int Gates = 6;                             // turnstiles across the forecourt
        public const float GateSpacing = 2.4f;                  // m between two turnstiles

        /// <summary>
        /// The stadium on a site's ground, its front on the avenue at the ground's north edge: a forecourt with the turnstiles
        /// along the front, a roofed main stand north and south, an open end stand west and east, the four corners, a
        /// floodlight mast at each outer corner and the pitch in the middle. Into the caller's list (cleared).
        /// </summary>
        public static void Stadium(SiteRect ground, List<LandmarkPiece> into)
        {
            into.Clear();
            float x0 = ground.X0, x1 = ground.X1, z0 = ground.Z0, z1 = ground.Z1 - Forecourt;
            if (x1 - x0 < 2f * EndDepth + Section || z1 - z0 < 2f * StandDepth + Section)
            {
                return;
            }
            // The main stands, north and south, between the corners: whole sections stretched to the length.
            float mainLength = x1 - x0 - 2f * EndDepth;
            int mains = Math.Max(1, (int)Math.Round(mainLength / Section, MidpointRounding.AwayFromZero));
            float mainStretch = mainLength / (mains * Section);
            for (int i = 0; i < mains; i++)
            {
                float x = x0 + EndDepth + (i + 0.5f) * Section * mainStretch;
                into.Add(new LandmarkPiece { Id = Stand, X = x, Z = z1 - StandDepth * 0.5f, Yaw = 180f, StretchX = mainStretch, StretchZ = 1f, Solid = true });
                into.Add(new LandmarkPiece { Id = Stand, X = x, Z = z0 + StandDepth * 0.5f, Yaw = 0f, StretchX = mainStretch, StretchZ = 1f, Solid = true });
            }
            // The ends, west and east, between the corners.
            float endLength = z1 - z0 - 2f * StandDepth;
            int ends = Math.Max(1, (int)Math.Round(endLength / Section, MidpointRounding.AwayFromZero));
            float endStretch = endLength / (ends * Section);
            for (int i = 0; i < ends; i++)
            {
                float z = z0 + StandDepth + (i + 0.5f) * Section * endStretch;
                into.Add(new LandmarkPiece { Id = End, X = x0 + EndDepth * 0.5f, Z = z, Yaw = 90f, StretchX = endStretch, StretchZ = 1f, Solid = true });
                into.Add(new LandmarkPiece { Id = End, X = x1 - EndDepth * 0.5f, Z = z, Yaw = 270f, StretchX = endStretch, StretchZ = 1f, Solid = true });
            }
            // The corners: stair towers alike from every side, unturned, so each keeps its 9 by 12 m between an end and a main stand.
            into.Add(new LandmarkPiece { Id = Corner, X = x0 + EndDepth * 0.5f, Z = z0 + StandDepth * 0.5f, Yaw = 0f, StretchX = 1f, StretchZ = 1f, Solid = true });
            into.Add(new LandmarkPiece { Id = Corner, X = x1 - EndDepth * 0.5f, Z = z0 + StandDepth * 0.5f, Yaw = 0f, StretchX = 1f, StretchZ = 1f, Solid = true });
            into.Add(new LandmarkPiece { Id = Corner, X = x1 - EndDepth * 0.5f, Z = z1 - StandDepth * 0.5f, Yaw = 0f, StretchX = 1f, StretchZ = 1f, Solid = true });
            into.Add(new LandmarkPiece { Id = Corner, X = x0 + EndDepth * 0.5f, Z = z1 - StandDepth * 0.5f, Yaw = 0f, StretchX = 1f, StretchZ = 1f, Solid = true });
            // The floodlights at the bowl's outer corners, each looking at the middle of the pitch.
            float midX = (x0 + x1) * 0.5f, midZ = (z0 + z1) * 0.5f;
            Light(into, x0 + 1.5f, z0 + 1.5f, midX, midZ);
            Light(into, x1 - 1.5f, z0 + 1.5f, midX, midZ);
            Light(into, x1 - 1.5f, z1 - 1.5f, midX, midZ);
            Light(into, x0 + 1.5f, z1 - 1.5f, midX, midZ);
            // The field the stands leave, the pitch stretched to it.
            float fieldX = x1 - x0 - 2f * EndDepth, fieldZ = z1 - z0 - 2f * StandDepth;
            into.Add(new LandmarkPiece { Id = Pitch, X = midX, Z = midZ, Yaw = 0f, StretchX = fieldX / PitchWidth, StretchZ = fieldZ / PitchDepth, Solid = false });
            // The turnstiles across the forecourt, in the middle of the front, walked through north to south.
            float first = midX - (Gates - 1) * GateSpacing * 0.5f;
            for (int g = 0; g < Gates; g++)
            {
                into.Add(new LandmarkPiece { Id = Turnstile, X = first + g * GateSpacing, Z = ground.Z1 - Forecourt * 0.5f, Yaw = 0f, StretchX = 1f, StretchZ = 1f, Solid = false });
            }
        }

        private static void Light(List<LandmarkPiece> into, float x, float z, float toX, float toZ)
        {
            float yaw = (float)(Math.Atan2(toX - x, toZ - z) * 180.0 / Math.PI);
            into.Add(new LandmarkPiece { Id = Floodlight, X = x, Z = z, Yaw = (yaw + 360f) % 360f, StretchX = 1f, StretchZ = 1f, Solid = true });
            into.Add(new LandmarkPiece { Id = Lamps, X = x, Z = z, Yaw = (yaw + 360f) % 360f, StretchX = 1f, StretchZ = 1f, Solid = false });
        }

        /// <summary>The spot just out in front of a landmark's ground on the avenue's pavement: where its panel is offered and a test puts the player.</summary>
        public static void Front(SiteRect ground, out float x, out float z)
        {
            x = ground.MidX;
            z = ground.Z1 + 1.6f;
        }

        // ---------------------------------------------------------------- the crowds

        public const float PavementIn = 1.85f;   // m from a block's edge to the middle of the pavement round it (half a 3.7 m pavement)
        public const float CrowdAcross = 2.6f;   // m of the pavement's width a crowd stands on, leaving the kerb's edge to the walkers
        public const float CrowdFront = 18f;     // m along the front an opening's crowd stands on
        public const float CrowdSide = 26f;      // m along a side street a match's crowd reaches back from the corner

        /// <summary>
        /// Where the crowds stand outside a landmark: at an opening, on the avenue's pavement in front of it; on a match day,
        /// along the whole front in two halves and up both side streets' pavements from the corners. Into the caller's list (cleared).
        /// </summary>
        public static void Crowds(SiteRect ground, bool match, List<CrowdSquare> into)
        {
            into.Clear();
            if (ground.Empty)
            {
                return;
            }
            float front = ground.Z1 + PavementIn;
            if (!match)
            {
                into.Add(new CrowdSquare { X = ground.MidX, Z = front, Width = Math.Min(CrowdFront, ground.Width), Depth = CrowdAcross });
                return;
            }
            float half = ground.Width * 0.5f - 2f;
            into.Add(new CrowdSquare { X = ground.X0 + 1f + half * 0.5f, Z = front, Width = half, Depth = CrowdAcross });
            into.Add(new CrowdSquare { X = ground.X1 - 1f - half * 0.5f, Z = front, Width = half, Depth = CrowdAcross });
            float side = Math.Min(CrowdSide, ground.Depth - 4f), sideZ = ground.Z1 - 2f - side * 0.5f;
            into.Add(new CrowdSquare { X = ground.X0 - PavementIn, Z = sideZ, Width = CrowdAcross, Depth = side });
            into.Add(new CrowdSquare { X = ground.X1 + PavementIn, Z = sideZ, Width = CrowdAcross, Depth = side });
        }

        // ---------------------------------------------------------------- the plaza and the forecourt

        public const string FlagPole = "flag_pole";     // the first bundle's: the flag flies to +X
        public const string Bench = "precinct_bench";   // the sitter faces +Z
        public const string Bin = "street_bin";
        public const string Bollard = "bollard";
        public const float BollardEvery = 3f;           // m between two bollards along a plaza's front

        /// <summary>
        /// The tower's plaza or the mall's forecourt, round the main building (its footprint <paramref name="width"/> by
        /// <paramref name="depth"/> at the front of <paramref name="anchor"/>, in the middle of its width): flag poles either side
        /// of its front, bollards along the open front, benches with a bin beside each in the open ground at the sides. Into the
        /// caller's list (cleared). Nothing stands within a metre and a half of the building.
        /// </summary>
        public static void Plaza(SiteRect anchor, float width, float depth, bool flags, List<LandmarkPiece> into)
        {
            into.Clear();
            if (anchor.Empty || width <= 0f || depth <= 0f)
            {
                return;
            }
            float midX = anchor.MidX, front = anchor.Z1;
            float left = midX - width * 0.5f - 1.5f, right = midX + width * 0.5f + 1.5f;
            if (flags)
            {
                for (int i = 0; i < 3; i++)
                {
                    if (left - 1.5f - i * 2.5f > anchor.X0 + 1f)
                    {
                        into.Add(new LandmarkPiece { Id = FlagPole, X = left - 1.5f - i * 2.5f, Z = front - 1.2f, Yaw = 0f, StretchX = 1f, StretchZ = 1f });
                    }
                    if (right + 1.5f + i * 2.5f < anchor.X1 - 1f)
                    {
                        into.Add(new LandmarkPiece { Id = FlagPole, X = right + 1.5f + i * 2.5f, Z = front - 1.2f, Yaw = 0f, StretchX = 1f, StretchZ = 1f });
                    }
                }
            }
            for (float x = anchor.X0 + 1f; x < left - 9f; x += BollardEvery)
            {
                into.Add(new LandmarkPiece { Id = Bollard, X = x, Z = front - 0.4f, Yaw = 0f, StretchX = 1f, StretchZ = 1f });
            }
            for (float x = anchor.X1 - 1f; x > right + 9f; x -= BollardEvery)
            {
                into.Add(new LandmarkPiece { Id = Bollard, X = x, Z = front - 0.4f, Yaw = 0f, StretchX = 1f, StretchZ = 1f });
            }
            float back = front - depth;
            float sideZ = Math.Max(anchor.Z0 + 4f, front - depth * 0.5f);
            if (left - anchor.X0 > 7f)
            {
                Seat(into, (anchor.X0 + left) * 0.5f, sideZ, 90f);
            }
            if (anchor.X1 - right > 7f)
            {
                Seat(into, (right + anchor.X1) * 0.5f, sideZ, 270f);
            }
            if (back - anchor.Z0 > 8f)
            {
                Seat(into, midX - 4f, (anchor.Z0 + back) * 0.5f, 0f);
                Seat(into, midX + 4f, (anchor.Z0 + back) * 0.5f, 0f);
            }
        }

        private static void Seat(List<LandmarkPiece> into, float x, float z, float yaw)
        {
            into.Add(new LandmarkPiece { Id = Bench, X = x, Z = z, Yaw = yaw, StretchX = 1f, StretchZ = 1f });
            double a = yaw * Math.PI / 180.0;
            into.Add(new LandmarkPiece { Id = Bin, X = x + (float)Math.Cos(a) * 1.5f, Z = z - (float)Math.Sin(a) * 1.5f, Yaw = 0f, StretchX = 1f, StretchZ = 1f });
        }

        // ---------------------------------------------------------------- picks

        /// <summary>The contractor of the city's programme (one of its eight) the city's own works on a landmark engage: the same on every machine.</summary>
        public static int CityContractor(LandmarkKind kind)
        {
            return (int)((uint)Hash("contractor|" + Name(kind)) % 8u);
        }

        /// <summary>FNV-1a of a text, the same on every machine and every run.</summary>
        public static int Hash(string text)
        {
            unchecked
            {
                uint h = 2166136261u;
                for (int i = 0; text != null && i < text.Length; i++)
                {
                    h = (h ^ text[i]) * 16777619u;
                }
                return (int)h;
            }
        }

        /// <summary>The tallest of the designs that fit (their heights, best covering first): its index, the first of equals; -1 for none.</summary>
        public static int Tallest(IList<float> heights)
        {
            int best = -1;
            for (int i = 0; heights != null && i < heights.Count; i++)
            {
                if (best < 0 || heights[i] > heights[best] + 0.01f)
                {
                    best = i;
                }
            }
            return best;
        }
    }
}
