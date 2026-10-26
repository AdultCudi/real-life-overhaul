using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.DistrictSave
{
    /// <summary>
    /// Where the mod's own places come from: every feature that makes buildings, streets or a district of the mod's
    /// registers once with <see cref="Add"/>, and its fill names what it makes, from its own data alone (the same on every
    /// machine, no DLC asked). The set is built when a save is read, before the city loads (DistrictSaveEntry), and again
    /// at every city load; <see cref="Changed"/> builds it afresh in play. A feature whose buildings are to have their
    /// records back before the city's scenes are built also gives an early step, which puts its buildings' assets into the
    /// game's address lookup then (<see cref="Early"/>): a record goes back only once the game knows its places (each
    /// address by BuildingHelper.GetBuilding, each district in NeighborhoodHelper.Neighborhoods). The API stays as it is
    /// (docs/features/district-save.md, "How a feature names its places"). With no feature registered, or none naming
    /// anything, the district save costs nothing.
    /// Game-free. COST: nothing per frame; one call of each fill at a load and on <see cref="Changed"/>.
    /// </summary>
    public static class DistrictPlaces
    {
        private static readonly List<string> Owners = new List<string>(4);
        private static readonly List<Action<PlaceSet>> Fills = new List<Action<PlaceSet>>(4);
        private static readonly List<Action> Befores = new List<Action>(4);   // each feature's early step, or null
        private static readonly List<string> Faults = new List<string>(2);
        private static PlaceSet _current = new PlaceSet();

        /// <summary>
        /// A feature's places, under its own name: <paramref name="fill"/> adds them to the set it is given (streets,
        /// addresses, buildings with their doors, districts, ground and the ways out to the game's streets);
        /// <paramref name="early"/>, if given, runs
        /// when a save has been read and before its city is built, and puts the feature's buildings' assets into the game's
        /// address lookup, so that their records go back before the game builds its lookups from the save's lists.
        /// Registering again under the same name replaces both.
        /// </summary>
        public static void Add(string owner, Action<PlaceSet> fill, Action early = null)
        {
            if (string.IsNullOrEmpty(owner) || fill == null)
            {
                return;
            }
            int at = Owners.IndexOf(owner);
            if (at >= 0)
            {
                Fills[at] = fill;
                Befores[at] = early;
                return;
            }
            Owners.Add(owner);
            Fills.Add(fill);
            Befores.Add(early);
        }

        /// <summary>The features' early steps, in the order they registered; one that throws is named in <see cref="LastFaults"/>.</summary>
        public static void Early()
        {
            Faults.Clear();
            for (int i = 0; i < Befores.Count; i++)
            {
                try
                {
                    Befores[i]?.Invoke();
                }
                catch (Exception e)
                {
                    Faults.Add(Owners[i] + " (early): " + e.GetType().Name + ": " + e.Message);
                }
            }
        }

        /// <summary>Takes a feature's places out; the next build leaves them out.</summary>
        public static void Remove(string owner)
        {
            int at = Owners.IndexOf(owner);
            if (at >= 0)
            {
                Owners.RemoveAt(at);
                Fills.RemoveAt(at);
                Befores.RemoveAt(at);
            }
        }

        /// <summary>The set as last built.</summary>
        public static PlaceSet Current => _current;

        /// <summary>How many features have registered places.</summary>
        public static int Providers => Owners.Count;

        /// <summary>A feature's places changed in play (switched on or off): set by the director at its next look.</summary>
        public static bool Stale { get; private set; }

        /// <summary>Asks for the set to be built again before the next save.</summary>
        public static void Changed()
        {
            Stale = true;
        }

        /// <summary>
        /// Builds the set from every registered fill, in the order they registered; a fill that throws adds nothing and
        /// is named in <see cref="LastFaults"/>. Returns the new set, which is <see cref="Current"/> from then on.
        /// </summary>
        public static PlaceSet Build()
        {
            var set = new PlaceSet();
            for (int i = Faults.Count - 1; i >= 0; i--)
            {
                if (!Faults[i].Contains(" (early): "))
                {
                    Faults.RemoveAt(i);   // the early steps' faults stay to be said with the fills'
                }
            }
            for (int i = 0; i < Fills.Count; i++)
            {
                var own = new PlaceSet();
                try
                {
                    Fills[i](own);
                }
                catch (Exception e)
                {
                    Faults.Add(Owners[i] + ": " + e.GetType().Name + ": " + e.Message);
                    continue;
                }
                set.Take(own);
            }
            _current = set;
            Stale = false;
            return set;
        }

        /// <summary>The fills that threw at the last build, each as "owner: what", for the log.</summary>
        public static IList<string> LastFaults => Faults;

        /// <summary>
        /// One feature's places alone, for a take-out of that feature's records in play (DistrictSaveDirector.KeepOut): its
        /// fill run into a scoped set, which names only what the fill names, not every id with the mod's prefix, so another
        /// feature's buildings are never swept in with it. Null when no feature registered under that name or its fill
        /// throws (said in <see cref="LastFaults"/>). A scoped set is never <see cref="Current"/> and never taken into another.
        /// </summary>
        internal static PlaceSet Of(string owner)
        {
            int at = string.IsNullOrEmpty(owner) ? -1 : Owners.IndexOf(owner);
            if (at < 0)
            {
                Faults.Add((owner ?? "(none)") + " (scoped): no feature registered its places under that name");
                return null;
            }
            PlaceSet own = PlaceSet.Scoped();
            try
            {
                Fills[at](own);
            }
            catch (Exception e)
            {
                Faults.Add(owner + " (scoped): " + e.GetType().Name + ": " + e.Message);
                return null;
            }
            return own;
        }

        /// <summary>Forgets every registration and the set (the checks' fresh start).</summary>
        public static void Reset()
        {
            Owners.Clear();
            Fills.Clear();
            Befores.Clear();
            Faults.Clear();
            _current = new PlaceSet();
            Stale = false;
        }
    }

    /// <summary>A street's id and a number, as the save writes an address. Compared by ordinal text. Game-free.</summary>
    public readonly struct PlaceKey : IEquatable<PlaceKey>
    {
        public readonly string Street;
        public readonly int Number;

        public PlaceKey(string street, int number)
        {
            Street = street ?? string.Empty;
            Number = number;
        }

        public bool Equals(PlaceKey other)
        {
            return Number == other.Number && string.Equals(Street, other.Street, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is PlaceKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return unchecked((StringComparer.Ordinal.GetHashCode(Street ?? string.Empty) * 397) ^ Number);
        }

        public override string ToString()
        {
            return Street + " " + Number.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    /// <summary>A rectangle of the mod's own ground, in world metres on the ground plane (x and z).</summary>
    public struct PlaceGround
    {
        public float MinX;
        public float MinZ;
        public float MaxX;
        public float MaxZ;

        public bool Contains(float x, float z)
        {
            return x >= MinX && x <= MaxX && z >= MinZ && z <= MaxZ;
        }
    }

    /// <summary>
    /// A way out to the game's own streets: a spot on a street of the game's, outside every place of the mod's, where the
    /// player and the player's cars are written into the save while they are on the mod's ground or in its buildings; and
    /// the game's district that spot is in, written for a car's parking district.
    /// </summary>
    public struct PlaceExit
    {
        public float X;
        public float Y;
        public float Z;
        public float Heading;     // degrees about the up axis, as the game's rotations read
        public string District;   // a district of the game's own
    }

    /// <summary>
    /// The places of the mod's own: whole streets, addresses (a building's with its door), districts, ground and the ways
    /// out. A street or district whose id is the mod's (DistrictSaveRules.IsModId) is the mod's whether or not a feature
    /// named it; what a feature names is made this session (<see cref="Has"/>). Game-free; built by
    /// <see cref="DistrictPlaces.Build"/>, read at a save and a load, never per frame.
    /// </summary>
    public sealed class PlaceSet
    {
        private readonly HashSet<string> _streets = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<PlaceKey> _addresses = new HashSet<PlaceKey>();
        private readonly HashSet<string> _districts = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<PlaceGround> _ground = new List<PlaceGround>(2);
        private readonly List<PlaceExit> _exits = new List<PlaceExit>(2);
        private readonly Dictionary<PlaceKey, PlaceExit> _doors = new Dictionary<PlaceKey, PlaceExit>();
        private readonly bool _scoped;   // one feature's places alone (DistrictPlaces.Of): the mod's prefix alone names nothing

        public PlaceSet()
        {
        }

        private PlaceSet(bool scoped)
        {
            _scoped = scoped;
        }

        /// <summary>A set that names only what is added to it: one feature's places, for a take-out of its records in play.</summary>
        internal static PlaceSet Scoped()
        {
            return new PlaceSet(true);
        }

        /// <summary>True for one feature's places alone (<see cref="DistrictPlaces.Of"/>).</summary>
        public bool IsScoped => _scoped;

        /// <summary>Every number on a street of the mod's own (its id is the mod's).</summary>
        public PlaceSet Street(string id)
        {
            if (!string.IsNullOrEmpty(id))
            {
                _streets.Add(id);
            }
            return this;
        }

        /// <summary>One address the feature makes: on a street of the mod's, or a number no game building uses on a game street.</summary>
        public PlaceSet Address(string street, int number)
        {
            if (!string.IsNullOrEmpty(street))
            {
                _addresses.Add(new PlaceKey(street, number));
            }
            return this;
        }

        /// <summary>A district of the mod's own (its id is the mod's).</summary>
        public PlaceSet District(string id)
        {
            if (!string.IsNullOrEmpty(id))
            {
                _districts.Add(id);
            }
            return this;
        }

        /// <summary>A rectangle of the mod's own ground, corners in any order.</summary>
        public PlaceSet Ground(float x1, float z1, float x2, float z2)
        {
            _ground.Add(new PlaceGround { MinX = Math.Min(x1, x2), MinZ = Math.Min(z1, z2), MaxX = Math.Max(x1, x2), MaxZ = Math.Max(z1, z2) });
            return this;
        }

        /// <summary>A way out: a spot on a game street outside the mod's places, its heading, and the game's district it is in.</summary>
        public PlaceSet Exit(float x, float y, float z, float heading, string gameDistrict)
        {
            _exits.Add(new PlaceExit { X = x, Y = y, Z = z, Heading = heading, District = gameDistrict });
            return this;
        }

        /// <summary>
        /// A building of the mod's at an address, and the spot outside its door (on the street it fronts, facing out) where
        /// a save made inside it puts the player; the game's district that spot is in. The address is the mod's from then on.
        /// </summary>
        public PlaceSet Door(string street, int number, float x, float y, float z, float heading, string gameDistrict)
        {
            if (!string.IsNullOrEmpty(street))
            {
                var key = new PlaceKey(street, number);
                _addresses.Add(key);
                _doors[key] = new PlaceExit { X = x, Y = y, Z = z, Heading = heading, District = gameDistrict };
            }
            return this;
        }

        /// <summary>Adds another set's places (one feature's) to this one.</summary>
        internal void Take(PlaceSet other)
        {
            _streets.UnionWith(other._streets);
            _addresses.UnionWith(other._addresses);
            _districts.UnionWith(other._districts);
            _ground.AddRange(other._ground);
            _exits.AddRange(other._exits);
            foreach (KeyValuePair<PlaceKey, PlaceExit> door in other._doors)
            {
                _doors[door.Key] = door.Value;
            }
        }

        /// <summary>
        /// True for an address the save must never name: on a street whose id is the mod's, or one a feature named. A scoped
        /// set (one feature's) names only what that feature named.
        /// </summary>
        public bool Names(string street, int number)
        {
            if (string.IsNullOrEmpty(street))
            {
                return false;
            }
            return (!_scoped && DistrictSaveRules.IsModId(street)) || _streets.Contains(street) || _addresses.Contains(new PlaceKey(street, number));
        }

        /// <summary>True for a district the save must never name: its id is the mod's, or a feature named it (a scoped set: only that).</summary>
        public bool NamesDistrict(string id)
        {
            return !string.IsNullOrEmpty(id) && ((!_scoped && DistrictSaveRules.IsModId(id)) || _districts.Contains(id));
        }

        /// <summary>True for an address a feature makes this session: a record kept for it may go back.</summary>
        public bool Has(string street, int number)
        {
            return !string.IsNullOrEmpty(street) && (_streets.Contains(street) || _addresses.Contains(new PlaceKey(street, number)));
        }

        /// <summary>True for a district a feature makes this session.</summary>
        public bool HasDistrict(string id)
        {
            return !string.IsNullOrEmpty(id) && _districts.Contains(id);
        }

        /// <summary>True for a point on the mod's own ground.</summary>
        public bool OnGround(float x, float z)
        {
            for (int i = 0; i < _ground.Count; i++)
            {
                if (_ground[i].Contains(x, z))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>The way out nearest to a point on the ground plane (the first of equals); false when there is none.</summary>
        public bool NearestExit(float x, float z, out PlaceExit exit)
        {
            exit = default;
            float best = float.MaxValue;
            bool found = false;
            for (int i = 0; i < _exits.Count; i++)
            {
                float dx = _exits[i].X - x;
                float dz = _exits[i].Z - z;
                float d = dx * dx + dz * dz;
                if (d < best)
                {
                    best = d;
                    exit = _exits[i];
                    found = true;
                }
            }
            return found;
        }

        /// <summary>The spot outside the door of the mod's building at an address; false when no feature named one.</summary>
        public bool DoorOf(string street, int number, out PlaceExit door)
        {
            door = default;
            return !string.IsNullOrEmpty(street) && _doors.TryGetValue(new PlaceKey(street, number), out door);
        }

        /// <summary>The first way out a feature named; false when there is none.</summary>
        public bool FirstExit(out PlaceExit exit)
        {
            exit = _exits.Count > 0 ? _exits[0] : default;
            return _exits.Count > 0;
        }

        public int Streets => _streets.Count;
        public int Addresses => _addresses.Count;
        public int Districts => _districts.Count;
        public int GroundCount => _ground.Count;
        public int Exits => _exits.Count;
        public int Doors => _doors.Count;

        /// <summary>True when no feature named anything: the save is then left as the game has it.</summary>
        public bool Empty => _streets.Count == 0 && _addresses.Count == 0 && _districts.Count == 0 && _ground.Count == 0;

        /// <summary>The streets and districts named, for the self-check's line (ordinal order, the same on every machine).</summary>
        public List<string> Describe()
        {
            var all = new List<string>(_streets.Count + _addresses.Count + _districts.Count);
            foreach (string s in _streets)
            {
                all.Add("street " + s);
            }
            foreach (PlaceKey k in _addresses)
            {
                all.Add("address " + k);
            }
            foreach (string d in _districts)
            {
                all.Add("district " + d);
            }
            all.Sort(StringComparer.Ordinal);
            return all;
        }
    }
}
