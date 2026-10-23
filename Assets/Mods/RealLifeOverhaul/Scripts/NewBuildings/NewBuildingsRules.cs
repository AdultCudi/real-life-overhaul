using System;
using System.Collections.Generic;
using System.Globalization;

namespace RealLifeOverhaul.NewBuildings
{
    /// <summary>
    /// One of the game's building designs as the copy engine knows it: a building of the city as it was placed in the
    /// scenes, measured round its own root in the root's own turn (metres), and the building of the game's it was copied
    /// from (by the caller's index). One entry per design and size, so that a made building of a size gets a design a
    /// building of that size wears.
    /// </summary>
    public sealed class DesignEntry
    {
        public string Name;         // the scene object's name with its copy number taken off ("SM_Hell's KitchenBuilding11")
        public string Size;         // the size id of the game's building it stands for ("ba:buildingsize_c")
        public int Version;         // that building's version
        public string Type;         // that building's type id
        public int Uses;            // buildings of the city of this size wearing it
        public bool Batched;        // drawn in a static batch with its neighbours: a copy would draw at the original's place
        public float MinX, MaxX;    // m: its footprint across, round its root
        public float MinZ, MaxZ;    // m: its footprint along its root's forward
        public float Height;        // m: from its root up to its highest point
        public float DoorX, DoorZ;  // m: its first door round its root
        public float DoorY;         // m: that door's height above its root: the root stands this far below the ground at the door
        public float DoorFacing;    // degrees: the way its first door faces out, from its root's forward
        public int Source;          // the caller's index of the game's building it is copied from
    }

    /// <summary>
    /// A place a made building may stand: a rectangle of open ground with one side on a street. A design is stood in it
    /// with its door on that side, in the middle of it and its front on the lot's front line.
    /// </summary>
    public struct MadeLot
    {
        public float X;        // m: the lot's middle, in the world
        public float Z;
        public float Facing;   // degrees round the vertical, as the game turns things (0 = +Z): the way the street lies from the lot
        public float Width;    // m: along the street
        public float Depth;    // m: back from the street

        public MadeLot(float x, float z, float facing, float width, float depth)
        {
            X = x;
            Z = z;
            Facing = facing;
            Width = width;
            Depth = depth;
        }
    }

    /// <summary>
    /// A building of the mod's own as a feature declares it (<c>MadeBuildings.Declare</c>): its address on a street
    /// of the mod's own, the game's size, version and type of its interior, its district, its look (a design by name, or one
    /// a building of its size wears) and where it may stand. Nothing of it is saved; it is made again at every load.
    /// </summary>
    public sealed class MadeSpec
    {
        public string Id;          // the feature's own name for it, also its hold in the special buildings' book
        public string Street;      // a street of the mod's own (MadeStreets), never one of the game's
        public int Number;         // its number in that street
        public string Size;        // the game's size id: the interior is the game's own structure of this size and version
        public int Version;
        public string Type;        // the game's building type id, never the special one
        public string District;    // the game's district id it is filed under
        public string Design;      // a design by name (DesignEntry.Name), or null for one a building of its size wears
        public MadeLot[] Lots;     // where it may stand, in order: the next free one after the preferred one is taken
        public int Preferred;      // the lot asked for first
        public int Traffic;        // the building's traffic index, 0 to 100, as the game's buildings have one
        public int Floors = 1;     // storeys of floor area its record has: the floors core gives a host that many floors
        public Func<bool> Wanted;  // the declaring feature's own option; null for always
    }

    /// <summary>A street of the mod's own: its id, the locale key of its name, its short name for the game's help, avenue or not.</summary>
    public sealed class MadeStreet
    {
        public readonly string Id;
        public readonly string NameKey;
        public readonly string Abbreviation;
        public readonly bool Avenue;

        public MadeStreet(string id, string nameKey, string abbreviation, bool avenue)
        {
            Id = id;
            NameKey = nameKey;
            Abbreviation = abbreviation;
            Avenue = avenue;
        }
    }

    /// <summary>
    /// The streets of the mod's own. Their ids carry the mod's prefix, which no street of the game's can have, so a free
    /// update that puts buildings in a street the game left empty can never take a made building's address, and every
    /// address a made building ever had is told by its street alone (the save guard needs nothing else). The game shows
    /// them by name once they are among its own streets (<c>MadeGame.AddStreets</c>, at every load).
    /// </summary>
    public static class MadeStreets
    {
        public const string Prefix = "reallife:street_";   // the game's own begin "ba:street_"

        public static readonly MadeStreet[] All = WithDistricts(new[]
        {
            new MadeStreet(Prefix + "provinglane", NewBuildingsText.StreetProvingLane, "PRL", false),
        });

        // The mod's own streets, then those of its districts (Uptown's, Scripts/Uptown/LayoutStreets.cs), so a building on a
        // district's lot has an address the game writes and finds.
        private static MadeStreet[] WithDistricts(MadeStreet[] own)
        {
            Uptown.LayoutStreet[] uptown = Uptown.LayoutStreets.All;
            var all = new MadeStreet[own.Length + uptown.Length];
            own.CopyTo(all, 0);
            for (int i = 0; i < uptown.Length; i++)
            {
                all[own.Length + i] = new MadeStreet(uptown[i].Id, uptown[i].NameKey, uptown[i].Abbreviation, uptown[i].Avenue);
            }
            return all;
        }

        /// <summary>True for a street of the mod's own, whether or not a building of it stands in this city. A string test.</summary>
        public static bool IsMine(string street)
        {
            return street != null && street.StartsWith(Prefix, StringComparison.Ordinal);
        }

        /// <summary>The street of that id, or null.</summary>
        public static MadeStreet Find(string id)
        {
            for (int i = 0; i < All.Length; i++)
            {
                if (string.Equals(All[i].Id, id, StringComparison.Ordinal))
                {
                    return All[i];
                }
            }
            return null;
        }
    }

    /// <summary>
    /// The rules of the mod's own buildings, game-free: the name of a design, which design a made building wears, where it
    /// stands on its lot and whether that lot is clear, which lot it takes when the one it asked for is not, whether a
    /// declaration can be made at all, and what ten copies may cost. Every answer comes from the arguments alone, so every
    /// machine of a Going Public session with the same game and the same mod gets the same buildings in the same places.
    /// </summary>
    public static class NewBuildingsRules
    {
        public const float Gap = 1.5f;              // m kept clear round a footprint: the game's buildings, obstacles and anything standing
        public const float RoadClear = 3.5f;        // m from a footprint to the middle of the nearest traffic lane: a lane and its kerb
        public const float StreetReach = 60f;       // m: a lot with no traffic lane this near fronts no street
        public const float GroundLift = 0.4f;       // m above the highest walkable ground under a footprint where the check for anything standing begins
        public const float GroundStep = 1.5f;       // m the walkable ground may rise or fall under a footprint: more is no plot to build on
        public const float DoorReach = 2.5f;        // m: the player's walking mesh must come this near a made building's door
        public const float EdgeMargin = 40f;        // m kept inside the city map's bounds and the traffic grid
        public const float UprightMost = 1f;        // degrees a design's root may lean and still be stood upright
        public const float CopyAhead = 40f;         // m in front of the player: where the copy test stands its building
        public const int CostCopies = 10;           // copies the frame cost is measured with
        public const float CostWindow = 4f;         // s of real time each side of the frame cost test is measured over
        public const float MostAddedMs = 0.5f;      // ms: what ten copies in view may add to the frame
        public const float DoorNear = 3f;           // m: the player stands at a made building's door after leaving it when this near
        public const int MostFloors = 12;           // storeys a made building's record may have: the most floors the floors core gives a building
        public const float FillShare = 0.7f;        // of the ground the best fitting design covers: the designs a plot or a building's place is given among

        public const string SpecialType = "ba:buildingtype_special";   // the game's type that needs a special service
        public const string HomeType = "ba:buildingtype_residential";  // the game's type its food deliveries are brought to

        private const float Deg = (float)(Math.PI / 180.0);

        /// <summary>A design's name: the scene object's name with Unity's copy numbers (" (1)") taken off the end.</summary>
        public static string DesignName(string objectName)
        {
            if (string.IsNullOrEmpty(objectName))
            {
                return string.Empty;
            }
            string name = objectName.Trim();
            while (name.EndsWith(")", StringComparison.Ordinal))
            {
                int open = name.LastIndexOf(" (", StringComparison.Ordinal);
                if (open <= 0 || open + 2 >= name.Length - 1)
                {
                    break;
                }
                bool digits = true;
                for (int i = open + 2; i < name.Length - 1; i++)
                {
                    digits &= name[i] >= '0' && name[i] <= '9';
                }
                if (!digits)
                {
                    break;
                }
                name = name.Substring(0, open).TrimEnd();
            }
            return name;
        }

        /// <summary>The order the catalogue keeps: by name, then size, ordinal, so that the same city lists the same designs everywhere.</summary>
        public static int CompareDesigns(DesignEntry a, DesignEntry b)
        {
            int byName = string.CompareOrdinal(a.Name, b.Name);
            return byName != 0 ? byName : string.CompareOrdinal(a.Size, b.Size);
        }

        /// <summary>
        /// True when a building of a design already measured on another should be the one it is measured and copied from:
        /// one not drawn in a static batch before one that is, then the lower address (street, ordinal, then number), so
        /// that every machine measures each design on the same building, whatever order the scene lists them in.
        /// </summary>
        public static bool MeasuredFrom(bool batched, string street, int number, bool wasBatched, string wasStreet, int wasNumber)
        {
            if (batched != wasBatched)
            {
                return !batched;
            }
            int byStreet = string.CompareOrdinal(street, wasStreet);
            return byStreet != 0 ? byStreet < 0 : number < wasNumber;
        }

        /// <summary>The order the declarations are made in: by id, ordinal, whatever order the features declared them in.</summary>
        public static int CompareSpecs(MadeSpec a, MadeSpec b)
        {
            return string.CompareOrdinal(a.Id, b.Id);
        }

        /// <summary>FNV-1a over the text's characters: the same number on every machine and every run, unlike string.GetHashCode.</summary>
        public static uint Hash(string text)
        {
            uint hash = 2166136261u;
            for (int i = 0; text != null && i < text.Length; i++)
            {
                hash ^= text[i];
                hash = unchecked(hash * 16777619u);
            }
            return hash;
        }

        /// <summary>True when a root that leans no more than <see cref="UprightMost"/> (the cosine of its up from the vertical) may be stood upright.</summary>
        public static bool Upright(float upY)
        {
            return upY >= Math.Cos(UprightMost * Deg);
        }

        /// <summary>A design's footprint in the frame of its door: u across it (to the door's right), v the way it faces out.</summary>
        public static void DoorFrame(DesignEntry d, out float uMin, out float uMax, out float vMin, out float vMax)
        {
            float s = (float)Math.Sin(d.DoorFacing * Deg), c = (float)Math.Cos(d.DoorFacing * Deg);
            uMin = vMin = float.MaxValue;
            uMax = vMax = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                float x = (i & 1) == 0 ? d.MinX : d.MaxX;
                float z = (i & 2) == 0 ? d.MinZ : d.MaxZ;
                float u = x * c - z * s;
                float v = x * s + z * c;
                uMin = Math.Min(uMin, u);
                uMax = Math.Max(uMax, u);
                vMin = Math.Min(vMin, v);
                vMax = Math.Max(vMax, v);
            }
        }

        /// <summary>True when the design stood with its door to the street takes no more than the lot's width and depth.</summary>
        public static bool Fits(DesignEntry d, float width, float depth)
        {
            DoorFrame(d, out float uMin, out float uMax, out float vMin, out float vMax);
            return uMax - uMin <= width && vMax - vMin <= depth && uMax > uMin && vMax > vMin;
        }

        /// <summary>
        /// The design a made building wears: the one named if it can be copied and fits; else, of the designs buildings of
        /// its size wear that can be copied and fit, those of its type if any, one picked by its id. -1 when none.
        /// <paramref name="designs"/> is in <see cref="CompareDesigns"/> order.
        /// </summary>
        public static int Pick(IList<DesignEntry> designs, string size, string type, string named, float width, float depth, string id)
        {
            if (designs == null)
            {
                return -1;
            }
            if (!string.IsNullOrEmpty(named))
            {
                for (int i = 0; i < designs.Count; i++)
                {
                    if (string.Equals(designs[i].Name, named, StringComparison.Ordinal) && !designs[i].Batched && Fits(designs[i], width, depth))
                    {
                        return i;
                    }
                }
                return -1;
            }
            int sized = 0, typed = 0;
            for (int i = 0; i < designs.Count; i++)
            {
                if (Candidate(designs[i], size, width, depth))
                {
                    sized++;
                    typed += string.Equals(designs[i].Type, type, StringComparison.Ordinal) ? 1 : 0;
                }
            }
            if (sized == 0)
            {
                return -1;
            }
            bool byType = typed > 0;
            int wanted = (int)(Hash(id) % (uint)(byType ? typed : sized));
            for (int i = 0; i < designs.Count; i++)
            {
                if (!Candidate(designs[i], size, width, depth) || (byType && !string.Equals(designs[i].Type, type, StringComparison.Ordinal)))
                {
                    continue;
                }
                if (wanted-- == 0)
                {
                    return i;
                }
            }
            return -1;
        }

        private static bool Candidate(DesignEntry d, string size, float width, float depth)
        {
            return string.Equals(d.Size, size, StringComparison.Ordinal) && !d.Batched && Fits(d, width, depth);
        }

        /// <summary>
        /// A design for ground of that width and depth when none is named (a plot's, or a building's own place for another
        /// look): of the designs that can be copied, fit and are not named <paramref name="other"/> (the look replaced), those
        /// covering at least <see cref="FillShare"/> of what the best fitting one covers, one picked by <paramref name="key"/>.
        /// -1 when none fits. <paramref name="designs"/> is in <see cref="CompareDesigns"/> order.
        /// </summary>
        public static int PickFor(IList<DesignEntry> designs, float width, float depth, string other, string key)
        {
            if (designs == null)
            {
                return -1;
            }
            float best = 0f;
            for (int i = 0; i < designs.Count; i++)
            {
                best = Fitting(designs[i], width, depth, other) ? Math.Max(best, Covers(designs[i])) : best;
            }
            if (best <= 0f)
            {
                return -1;
            }
            int pool = 0;
            for (int i = 0; i < designs.Count; i++)
            {
                pool += Fitting(designs[i], width, depth, other) && Covers(designs[i]) >= best * FillShare ? 1 : 0;
            }
            int wanted = (int)(Hash(key) % (uint)pool);
            for (int i = 0; i < designs.Count; i++)
            {
                if (Fitting(designs[i], width, depth, other) && Covers(designs[i]) >= best * FillShare && wanted-- == 0)
                {
                    return i;
                }
            }
            return -1;
        }

        private static bool Fitting(DesignEntry d, float width, float depth, string other)
        {
            return d != null && !d.Batched && Fits(d, width, depth) && !string.Equals(d.Name, other, StringComparison.Ordinal);
        }

        // m2: the ground a design takes, measured from its door.
        private static float Covers(DesignEntry d)
        {
            DoorFrame(d, out float uMin, out float uMax, out float vMin, out float vMax);
            return (uMax - uMin) * (vMax - vMin);
        }

        /// <summary>
        /// Where a design's root stands on a lot and its turn (degrees): its door to the street, its front on the lot's front
        /// line, in the middle of the lot's width.
        /// </summary>
        public static void Place(DesignEntry d, MadeLot lot, out float x, out float z, out float yaw)
        {
            DoorFrame(d, out float uMin, out float uMax, out _, out float vMax);
            yaw = Turn(lot.Facing - d.DoorFacing);
            float f = lot.Facing * Deg;
            float fx = (float)Math.Sin(f), fz = (float)Math.Cos(f);    // the way the street lies
            float rx = (float)Math.Cos(f), rz = -(float)Math.Sin(f);   // along the street, to the right of it
            float across = (uMin + uMax) * 0.5f, toFront = lot.Depth * 0.5f - vMax;
            x = lot.X + toFront * fx - across * rx;
            z = lot.Z + toFront * fz - across * rz;
        }

        /// <summary>
        /// As <see cref="Place"/>, then moved along the lot's front until the design's door is <paramref name="doorAcross"/> m
        /// right of the lot's middle (looking out from it): a copy over one of the game's buildings has its door where the
        /// building's own is, as far as its footprint may move and stay within <paramref name="slack"/> of the lot. Returns the
        /// metres the door is still short by.
        /// </summary>
        public static float AlignDoor(DesignEntry d, MadeLot lot, float doorAcross, float slack, out float x, out float z, out float yaw)
        {
            Place(d, lot, out x, out z, out yaw);
            DoorAt(d, x, z, yaw, out float doorX, out float doorZ);
            float f = lot.Facing * Deg;
            float rx = (float)Math.Cos(f), rz = -(float)Math.Sin(f);
            float across = (doorX - lot.X) * rx + (doorZ - lot.Z) * rz;
            DoorFrame(d, out float uMin, out float uMax, out _, out _);
            float room = Math.Max(0f, lot.Width * 0.5f + slack - (uMax - uMin) * 0.5f);
            float shift = Math.Max(-room, Math.Min(room, doorAcross - across));
            x += shift * rx;
            z += shift * rz;
            return Math.Abs(doorAcross - across - shift);
        }

        /// <summary>The ground a design takes on a lot, as a turned rectangle: its middle and its half width and half depth (m), turned as the lot.</summary>
        public static void Footprint(DesignEntry d, MadeLot lot, out float cx, out float cz, out float halfWidth, out float halfDepth)
        {
            DoorFrame(d, out float uMin, out float uMax, out float vMin, out float vMax);
            halfWidth = (uMax - uMin) * 0.5f;
            halfDepth = (vMax - vMin) * 0.5f;
            float f = lot.Facing * Deg;
            float fx = (float)Math.Sin(f), fz = (float)Math.Cos(f);
            float toMiddle = lot.Depth * 0.5f - halfDepth;
            cx = lot.X + toMiddle * fx;
            cz = lot.Z + toMiddle * fz;
        }

        /// <summary>Where a design's door stands in the world when its root is at (x, z) turned by <paramref name="yaw"/> degrees.</summary>
        public static void DoorAt(DesignEntry d, float x, float z, float yaw, out float doorX, out float doorZ)
        {
            float a = yaw * Deg;
            float s = (float)Math.Sin(a), c = (float)Math.Cos(a);
            doorX = x + d.DoorX * c + d.DoorZ * s;
            doorZ = z - d.DoorX * s + d.DoorZ * c;
        }

        /// <summary>The game's structure for a size and version as the game names its sizes: "ba:buildingsize_c", 1 is "C1".</summary>
        public static string StructureName(string size, int version)
        {
            if (string.IsNullOrEmpty(size))
            {
                return "?" + version;
            }
            int cut = size.LastIndexOf('_');
            return size.Substring(cut + 1).ToUpperInvariant() + version;
        }

        /// <summary>An angle in degrees brought into [0, 360).</summary>
        public static float Turn(float degrees)
        {
            float t = degrees % 360f;
            return t < 0f ? t + 360f : t;
        }

        /// <summary>True when the point lies within <paramref name="margin"/> of the turned rectangle (or inside it).</summary>
        public static bool Inside(float px, float pz, float cx, float cz, float facing, float halfWidth, float halfDepth, float margin)
        {
            float f = facing * Deg;
            float fx = (float)Math.Sin(f), fz = (float)Math.Cos(f);
            float dx = px - cx, dz = pz - cz;
            float along = dx * fz - dz * fx;    // on the right axis (cos f, -sin f)
            float outward = dx * fx + dz * fz;  // on the facing axis (sin f, cos f)
            return Math.Abs(along) <= halfWidth + margin && Math.Abs(outward) <= halfDepth + margin;
        }

        /// <summary>True when an upright box (min and max corners) and the turned rectangle grown by <paramref name="margin"/> overlap: four axes are tried.</summary>
        public static bool Overlaps(float minX, float minZ, float maxX, float maxZ, float cx, float cz, float facing, float halfWidth, float halfDepth, float margin)
        {
            if (maxX < minX || maxZ < minZ)
            {
                return false;
            }
            float f = facing * Deg;
            float fx = (float)Math.Sin(f), fz = (float)Math.Cos(f);
            float rx = fz, rz = -fx;
            float hw = halfWidth + margin, hd = halfDepth + margin;
            // The rectangle's reach along the world's axes, then the box's along the rectangle's.
            float reachX = Math.Abs(rx) * hw + Math.Abs(fx) * hd;
            float reachZ = Math.Abs(rz) * hw + Math.Abs(fz) * hd;
            if (cx + reachX < minX || cx - reachX > maxX || cz + reachZ < minZ || cz - reachZ > maxZ)
            {
                return false;
            }
            float bx = (minX + maxX) * 0.5f - cx, bz = (minZ + maxZ) * 0.5f - cz;
            float ex = (maxX - minX) * 0.5f, ez = (maxZ - minZ) * 0.5f;
            float alongR = Math.Abs(bx * rx + bz * rz), boxR = ex * Math.Abs(rx) + ez * Math.Abs(rz);
            if (alongR > hw + boxR)
            {
                return false;
            }
            float alongF = Math.Abs(bx * fx + bz * fz), boxF = ex * Math.Abs(fx) + ez * Math.Abs(fz);
            return alongF <= hd + boxF;
        }

        /// <summary>True when every corner of the turned rectangle lies inside the upright box shrunk by <paramref name="margin"/>.</summary>
        public static bool Within(float cx, float cz, float facing, float halfWidth, float halfDepth, float minX, float minZ, float maxX, float maxZ, float margin)
        {
            float f = facing * Deg;
            float fx = (float)Math.Sin(f), fz = (float)Math.Cos(f);
            float rx = fz, rz = -fx;
            for (int i = 0; i < 4; i++)
            {
                float a = (i & 1) == 0 ? -halfWidth : halfWidth;
                float b = (i & 2) == 0 ? -halfDepth : halfDepth;
                float x = cx + a * rx + b * fx;
                float z = cz + a * rz + b * fz;
                if (x < minX + margin || x > maxX - margin || z < minZ + margin || z > maxZ - margin)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// The lot a made building takes: the one it asked for if <paramref name="free"/> says so, else the next free one in
        /// the lots' order, round to the start. -1 when none is free (or there are none).
        /// </summary>
        public static int FirstFree(int lots, int preferred, Func<int, bool> free)
        {
            if (lots <= 0 || free == null)
            {
                return -1;
            }
            int start = preferred >= 0 && preferred < lots ? preferred : 0;
            for (int k = 0; k < lots; k++)
            {
                int lot = (start + k) % lots;
                if (free(lot))
                {
                    return lot;
                }
            }
            return -1;
        }

        /// <summary>
        /// The lot ahead of someone standing at (<paramref name="x"/>, <paramref name="z"/>) and looking along
        /// <paramref name="yaw"/> (degrees round the vertical, as the game turns things): its front line <paramref name="gap"/>
        /// metres ahead of them, its middle on their line of sight, its front turned back towards them. The test building's lot
        /// where the developer stood when they asked for it, so a plot found taken in the game can be swapped for ground they see.
        /// </summary>
        public static MadeLot LotAhead(float x, float z, float yaw, float width, float depth, float gap)
        {
            double turn = yaw * Math.PI / 180.0;
            float ahead = gap + depth * 0.5f;
            float facing = yaw + 180f;
            facing -= 360f * (float)Math.Floor(facing / 360f);
            return new MadeLot(x + (float)Math.Sin(turn) * ahead, z + (float)Math.Cos(turn) * ahead, facing, width, depth);
        }

        /// <summary>Where someone stood and the way they looked, as text kept until the next load: "x;z;yaw", the invariant culture's.</summary>
        public static string SpotText(float x, float z, float yaw)
        {
            return x.ToString("R", CultureInfo.InvariantCulture) + ";" + z.ToString("R", CultureInfo.InvariantCulture) + ";"
                   + yaw.ToString("R", CultureInfo.InvariantCulture);
        }

        /// <summary>The spot back from <see cref="SpotText"/>; false for anything else (nothing kept, or text of another form).</summary>
        public static bool ReadSpot(string text, out float x, out float z, out float yaw)
        {
            x = 0f;
            z = 0f;
            yaw = 0f;
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }
            string[] parts = text.Split(';');
            return parts.Length == 3 && float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out x)
                   && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out z)
                   && float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out yaw);
        }

        /// <summary>
        /// Why a declaration cannot be made at all (English, for the log), or null when it can. The game's own limits are
        /// asked, never assumed: <paramref name="structureTakes"/> whether the game has a structure of that size and version
        /// for that type, <paramref name="districtKnown"/> whether it has that district, <paramref name="homesLeftOut"/>
        /// whether the game's food deliveries can be told to leave a made home alone.
        /// </summary>
        public static string Refusal(MadeSpec spec, Func<string, int, string, bool> structureTakes, Func<string, bool> districtKnown, bool homesLeftOut)
        {
            if (spec == null || string.IsNullOrEmpty(spec.Id))
            {
                return "it has no id";
            }
            if (!MadeStreets.IsMine(spec.Street) || MadeStreets.Find(spec.Street) == null)
            {
                return "its street " + (spec.Street ?? "(none)") + " is not one of the mod's own streets (a free update may fill the game's own)";
            }
            if (spec.Number < 1)
            {
                return "its number " + spec.Number + " is not a house number";
            }
            if (string.IsNullOrEmpty(spec.Type) || spec.Type == SpecialType)
            {
                return "the game's special building type needs a special service of the game's (its towing reads one), so it is never made";
            }
            if (structureTakes == null || !structureTakes(spec.Size, spec.Version, spec.Type))
            {
                return "the game has no structure of size " + (spec.Size ?? "(none)") + " version " + spec.Version + " for the type " + spec.Type;
            }
            if (districtKnown == null || !districtKnown(spec.District))
            {
                return "the game has no district " + (spec.District ?? "(none)");
            }
            if (spec.Type == HomeType && !homesLeftOut)
            {
                return "a home would be sent the game's food deliveries, and the game's list of addresses those leave out could not be reached";
            }
            if (spec.Lots == null || spec.Lots.Length == 0)
            {
                return "it has nowhere to stand";
            }
            if (spec.Traffic < 0 || spec.Traffic > 100)
            {
                return "its traffic index " + spec.Traffic + " is not between 0 and 100";
            }
            if (spec.Floors < 1 || spec.Floors > MostFloors)
            {
                return "its floors " + spec.Floors + " are not between 1 and " + MostFloors;
            }
            return null;
        }

        /// <summary>True when what ten copies in view add to the frame is under <see cref="MostAddedMs"/>.</summary>
        public static bool CostPasses(float withMs, float withoutMs)
        {
            return withMs - withoutMs < MostAddedMs;
        }

        /// <summary>
        /// Where the copies of the frame cost test stand, ahead of a camera at (x, z) looking along <paramref name="facing"/>
        /// degrees: two rows of five across the view, the nearer at <see cref="CopyAhead"/> m, 30 m apart each way.
        /// </summary>
        public static void CostSpot(int index, float x, float z, float facing, out float spotX, out float spotZ)
        {
            int row = index / 5, column = index % 5;
            float ahead = CopyAhead + row * 30f;
            float across = (column - 2) * 30f;
            float f = facing * Deg;
            float fx = (float)Math.Sin(f), fz = (float)Math.Cos(f);
            spotX = x + ahead * fx + across * fz;
            spotZ = z + ahead * fz - across * fx;
        }
    }
}
