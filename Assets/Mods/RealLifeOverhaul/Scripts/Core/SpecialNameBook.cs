using System;
using System.Collections.Generic;

namespace RealLifeOverhaul
{
    /// <summary>What a special building is to the mod. The last four are never given a name of their own.</summary>
    public enum SpecialKind
    {
        Civic,          // an office of the city's (the utilities office)
        Court,          // the courthouse
        Police,         // a police station
        Hotel,          // a hotel that is a building of its own
        Campus,         // a university or college
        Other,          // any other building a feature gives a role to
        Kiosk,          // a newspaper kiosk: a stand in front of somebody else's building
        StreetVendor,   // a stall or a cart in the street
        PlayerBusiness, // a business of the player's: it keeps the name the player gave it
        RivalBusiness   // a rival's business: it keeps the rival's name
    }

    /// <summary>One special building as the book holds it.</summary>
    public sealed class SpecialEntry
    {
        public string Id;          // the feature's own name for it ("utilities", "police_2"); one building per id
        public string Street;
        public int Number;
        public SpecialKind Kind;
        public string NameKey;     // the locale key of its name
        public string TypeKey;     // the locale key of what it is, for the line under the name on the game's card; may be null
        public Func<bool> Shown;   // the feature's own option; null for always
        public bool Literal;       // NameKey is the name itself (a company's proper name), never looked up
        public string[] Tenants;   // the proper names of who is in it, by floor; null for one feature alone
        public bool Board;         // its feature stood a board of its own at the door: that is its one sign

        internal string Name;      // NameKey in the current language, made when first asked for
    }

    /// <summary>
    /// The book of the mod's special buildings by address, and the name each carries: names and addresses only, nothing
    /// of the game's or the save's. A feature sets its building under its own id (<see cref="Set"/>; set again when it
    /// moves) and removes it when it lets go. Kinds that keep their own names, and an address another id holds, are
    /// refused. A fixed address can be reserved (<see cref="Reserve"/>) before any feature chooses; a reservation
    /// outlives <see cref="Clear"/> and is never saved. Game-free.
    /// </summary>
    public sealed class SpecialNameBook
    {
        private readonly Dictionary<string, SpecialEntry> _byId = new Dictionary<string, SpecialEntry>(StringComparer.Ordinal);
        private readonly Dictionary<string, SpecialEntry> _byAddress = new Dictionary<string, SpecialEntry>(StringComparer.Ordinal);
        private readonly Dictionary<string, Reservation> _reserved = new Dictionary<string, Reservation>(StringComparer.Ordinal);   // by address key

        private sealed class Reservation
        {
            public string Id;
            public string Street;
            public int Number;
            public Func<bool> On;   // the feature's own option; null for always
        }

        public int Count => _byId.Count;

        /// <summary>Changes each time the book does: a reader that keeps something made from it compares this.</summary>
        public int Stamp { get; private set; }

        public static bool Accepts(SpecialKind kind)
        {
            return kind != SpecialKind.Kiosk && kind != SpecialKind.StreetVendor && kind != SpecialKind.PlayerBusiness && kind != SpecialKind.RivalBusiness;
        }

        /// <summary>The key a building is filed under: its street and number, as the game's address has them.</summary>
        public static string Key(string street, int number)
        {
            return (street ?? string.Empty) + "\u0001" + number.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>Puts a building in under the id, in place of what it held; false, with nothing under the id, if refused.</summary>
        public bool Set(string id, string street, int number, SpecialKind kind, string nameKey, string typeKey = null, Func<bool> shown = null, bool literal = false, string[] tenants = null)
        {
            if (string.IsNullOrEmpty(id))
            {
                return false;
            }
            Remove(id);   // what the id held goes, even if the new building is refused
            string key = Key(street, number);
            string reserved = ReservedFor(street, number);
            if (string.IsNullOrEmpty(street) || string.IsNullOrEmpty(nameKey) || !Accepts(kind) || _byAddress.ContainsKey(key)
                || (reserved != null && !string.Equals(reserved, id, StringComparison.Ordinal)))
            {
                return false;
            }
            var entry = new SpecialEntry
            {
                Id = id, Street = street, Number = number, Kind = kind, NameKey = nameKey,
                TypeKey = string.IsNullOrEmpty(typeKey) ? null : typeKey, Shown = shown, Literal = literal, Name = literal ? nameKey : null,
                Tenants = tenants != null && tenants.Length > 0 ? (string[])tenants.Clone() : null
            };
            _byId[id] = entry;
            _byAddress[key] = entry;
            Stamp++;
            return true;
        }

        public bool Remove(string id)
        {
            if (string.IsNullOrEmpty(id) || !_byId.TryGetValue(id, out SpecialEntry entry))
            {
                return false;
            }
            _byId.Remove(id);
            _byAddress.Remove(Key(entry.Street, entry.Number));
            Stamp++;
            return true;
        }

        public void Clear()
        {
            if (_byId.Count > 0)
            {
                _byId.Clear();
                _byAddress.Clear();
                Stamp++;
            }
        }

        /// <summary>The entry at an address while its feature shows it, or null.</summary>
        public SpecialEntry At(string street, int number)
        {
            SpecialEntry entry = Filed(street, number);
            return entry != null && IsShown(entry) ? entry : null;
        }

        // The entry at an address, shown or not.
        private SpecialEntry Filed(string street, int number)
        {
            return _byAddress.Count > 0 && !string.IsNullOrEmpty(street) && _byAddress.TryGetValue(Key(street, number), out SpecialEntry entry) ? entry : null;
        }

        /// <summary>True while an entry's feature shows it (an entry with no option always is).</summary>
        public static bool IsShown(SpecialEntry entry) => entry != null && (entry.Shown == null || entry.Shown());

        /// <summary>True if any feature holds the building at an address, shown or not, or reserves it while on.</summary>
        public bool Holds(string street, int number)
        {
            return (_byAddress.Count > 0 && !string.IsNullOrEmpty(street) && _byAddress.ContainsKey(Key(street, number))) || ReservedFor(street, number) != null;
        }

        /// <summary>True if a saved building gives way on load: the player's now, or held by an id not of <paramref name="ownPrefix"/>.</summary>
        public static bool GivesWay(bool players, string holder, string ownPrefix)
        {
            return players || (holder != null && (string.IsNullOrEmpty(ownPrefix) || !holder.StartsWith(ownPrefix, StringComparison.Ordinal)));
        }

        /// <summary>Reserves an address for an id while <paramref name="on"/> holds (null: always); false if another id has it.</summary>
        public bool Reserve(string id, string street, int number, Func<bool> on = null)
        {
            if (string.IsNullOrEmpty(id))
            {
                return false;
            }
            Unreserve(id);
            string key = Key(street, number);
            if (string.IsNullOrEmpty(street) || _reserved.ContainsKey(key))
            {
                return false;
            }
            _reserved[key] = new Reservation { Id = id, Street = street, Number = number, On = on };
            Stamp++;
            return true;
        }

        public bool Unreserve(string id)
        {
            if (string.IsNullOrEmpty(id) || _reserved.Count == 0)
            {
                return false;
            }
            string found = null;
            foreach (KeyValuePair<string, Reservation> r in _reserved)
            {
                if (string.Equals(r.Value.Id, id, StringComparison.Ordinal))
                {
                    found = r.Key;
                    break;
                }
            }
            if (found == null)
            {
                return false;
            }
            _reserved.Remove(found);
            Stamp++;
            return true;
        }

        /// <summary>The id an address is reserved for while that feature is on; null for none.</summary>
        public string ReservedFor(string street, int number)
        {
            if (_reserved.Count == 0 || string.IsNullOrEmpty(street) || !_reserved.TryGetValue(Key(street, number), out Reservation r))
            {
                return null;
            }
            return r.On == null || r.On() ? r.Id : null;
        }


        /// <summary>
        /// The largest building of the candidates that <paramref name="fits"/> and not <paramref name="taken"/>, ties
        /// broken by "street#number" in ordinal order; its index, or -1. The same city gives the same pick in every game.
        /// </summary>
        public static int PickLargest(int count, Func<int, bool> fits, Func<int, int> sqm, Func<int, string> street, Func<int, int> number, Func<string, int, bool> taken)
        {
            int best = -1, bestSqm = int.MinValue;
            string bestName = null;
            for (int i = 0; i < count; i++)
            {
                if (fits != null && !fits(i))
                {
                    continue;
                }
                string s = street(i);
                int n = number(i);
                if (taken != null && taken(s, n))
                {
                    continue;
                }
                int size = Math.Max(0, Math.Min(999999, sqm(i)));
                string name = s + "#" + n.ToString(System.Globalization.CultureInfo.InvariantCulture);
                if (size > bestSqm || (size == bestSqm && string.CompareOrdinal(name, bestName) < 0))
                {
                    best = i;
                    bestSqm = size;
                    bestName = name;
                }
            }
            return best;
        }

        /// <summary>The id holding an address, shown or not, a reservation that is on first; null for none.</summary>
        public string HolderOf(string street, int number)
        {
            string reserved = ReservedFor(street, number);
            if (reserved != null)
            {
                return reserved;
            }
            return _byAddress.Count > 0 && !string.IsNullOrEmpty(street) && _byAddress.TryGetValue(Key(street, number), out SpecialEntry entry) ? entry.Id : null;
        }

        /// <summary>The id of the entry put in the book at an address, shown or not, leaving reservations aside; null for none.</summary>
        public string FiledId(string street, int number)
        {
            SpecialEntry entry = Filed(street, number);
            return entry != null ? entry.Id : null;
        }

        public SpecialEntry Of(string id)
        {
            return !string.IsNullOrEmpty(id) && _byId.TryGetValue(id, out SpecialEntry entry) ? entry : null;
        }

        /// <summary>The name at an address in the current language; null if not shown or the player's (<paramref name="players"/>).</summary>
        public string NameAt(string street, int number, bool players, Func<string, string> localise)
        {
            if (players)
            {
                return null;
            }
            SpecialEntry entry = At(street, number);
            if (entry == null)
            {
                return null;
            }
            if (entry.Name == null)
            {
                string name = localise != null ? localise(entry.NameKey) : entry.NameKey;
                entry.Name = string.IsNullOrEmpty(name) ? entry.NameKey : name;
            }
            return entry.Name;
        }

        /// <summary>True if a feature may make a building its own: no player rents or owns it, the same on every machine.</summary>
        public static bool MaySit(bool rented, bool owned)
        {
            return !rented && !owned;
        }

        /// <summary>True if the building is off the market here: held by a feature that is on and not the player's; never saved.</summary>
        public bool Refuses(string street, int number, bool rented, bool owned)
        {
            return RefusesEntry(Filed(street, number), rented, owned);
        }

        /// <summary>The one rule of the refusal, for an entry in hand: its option is on and no player rents or owns it.</summary>
        public static bool RefusesEntry(SpecialEntry entry, bool rented, bool owned)
        {
            return !rented && !owned && IsShown(entry);
        }

        /// <summary>Marks whether the id's feature stood a board of its own at the door; false if the id holds none.</summary>
        public bool Boarded(string id, bool board)
        {
            SpecialEntry entry = Of(id);
            if (entry == null || entry.Board == board)
            {
                return entry != null;
            }
            entry.Board = board;
            Stamp++;
            return true;
        }

        /// <summary>Every entry, shown or not, into the caller's list (cleared first).</summary>
        public void All(List<SpecialEntry> into)
        {
            into.Clear();
            foreach (SpecialEntry entry in _byId.Values)
            {
                into.Add(entry);
            }
        }

        /// <summary>Every entry whose feature shows it now, into the caller's list (cleared first).</summary>
        public void Shown(List<SpecialEntry> into)
        {
            into.Clear();
            foreach (SpecialEntry entry in _byId.Values)
            {
                if (IsShown(entry))
                {
                    into.Add(entry);
                }
            }
        }

        private readonly HashSet<string> _shown = new HashSet<string>(StringComparer.Ordinal);   // the ids shown at the last look
        private bool _looked;

        /// <summary>
        /// Moves <see cref="Stamp"/> when the set shown has changed (an option switched changes no entry): the ids shown now
        /// against the set kept at the last look, so two different sets are never taken for one. Nothing allocated; the set
        /// is written again only when it changed.
        /// </summary>
        public bool LookShown()
        {
            bool same = _looked;
            int count = 0;
            foreach (SpecialEntry entry in _byId.Values)
            {
                if (IsShown(entry))
                {
                    count++;
                    same &= _shown.Contains(entry.Id);
                }
            }
            if (same && count == _shown.Count)
            {
                return false;   // ids are unique: as many, and every one kept, is the same set
            }
            bool first = !_looked;
            _looked = true;
            _shown.Clear();
            foreach (SpecialEntry entry in _byId.Values)
            {
                if (IsShown(entry))
                {
                    _shown.Add(entry.Id);
                }
            }
            if (!first)
            {
                Stamp++;
            }
            return !first;
        }

        /// <summary>The language has changed: every name is looked up again when next asked for.</summary>
        public void Forget()
        {
            foreach (SpecialEntry entry in _byId.Values)
            {
                entry.Name = entry.Literal ? entry.NameKey : null;
            }
            Stamp++;
        }
    }
}
