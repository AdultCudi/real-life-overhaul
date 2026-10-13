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

        internal string Name;      // NameKey in the language of the moment, made the first time it is asked for
    }

    /// <summary>
    /// The book of the mod's special buildings, by address: the buildings a feature gives a role to (the utilities office,
    /// the courthouse, the police stations, the hotels) and the name each carries wherever the game names a building. It
    /// holds names and addresses only, never anything of the game's: what is in a save stays as it is, and the game's own
    /// names come back as soon as a building is out of the book or the option is off.
    ///
    /// A feature puts its building in with one line when it has chosen it (<see cref="Set"/>, by its own id: a building
    /// that moves is simply set again under the same id), and takes it out when it lets it go (<see cref="Remove"/>).
    /// Kiosks, street vendors and the businesses of the player or a rival are refused: they keep the names they have.
    /// An address another id already holds is refused too, so two features never fight over one building.
    ///
    /// A name is looked up in the language of the moment the first time it is asked for and kept until the language
    /// changes (<see cref="Forget"/>). No Unity or game types: checked offline (EconomyCheck, SpecialNameChecks.cs).
    /// </summary>
    public sealed class SpecialNameBook
    {
        private readonly Dictionary<string, SpecialEntry> _byId = new Dictionary<string, SpecialEntry>(StringComparer.Ordinal);
        private readonly Dictionary<string, SpecialEntry> _byAddress = new Dictionary<string, SpecialEntry>(StringComparer.Ordinal);

        /// <summary>Buildings in the book.</summary>
        public int Count => _byId.Count;

        /// <summary>Changes by one each time the book does: a reader that keeps something made from it compares this.</summary>
        public int Stamp { get; private set; }

        /// <summary>False for the kinds that keep their own names.</summary>
        public static bool Accepts(SpecialKind kind)
        {
            return kind != SpecialKind.Kiosk && kind != SpecialKind.StreetVendor && kind != SpecialKind.PlayerBusiness && kind != SpecialKind.RivalBusiness;
        }

        /// <summary>The key a building is filed under: its street and number, as the game's address has them.</summary>
        public static string Key(string street, int number)
        {
            return (street ?? string.Empty) + "\u0001" + number.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Puts a building in the book under the feature's id, in place of whatever that id held before. False, with nothing
        /// under the id, for a kind that keeps its own name, a missing street or name, or an address another id holds (a
        /// missing id: false, and the book as it was).
        /// </summary>
        public bool Set(string id, string street, int number, SpecialKind kind, string nameKey, string typeKey = null, Func<bool> shown = null, bool literal = false)
        {
            if (string.IsNullOrEmpty(id))
            {
                return false;
            }
            Remove(id);   // (whatever the id held goes, even if the new building is refused: it is not that one any more)
            string key = Key(street, number);
            if (string.IsNullOrEmpty(street) || string.IsNullOrEmpty(nameKey) || !Accepts(kind) || _byAddress.ContainsKey(key))
            {
                return false;
            }
            var entry = new SpecialEntry
            {
                Id = id, Street = street, Number = number, Kind = kind, NameKey = nameKey,
                TypeKey = string.IsNullOrEmpty(typeKey) ? null : typeKey, Shown = shown, Literal = literal, Name = literal ? nameKey : null
            };
            _byId[id] = entry;
            _byAddress[key] = entry;
            Stamp++;
            return true;
        }

        /// <summary>Takes the id's building out of the book. False if it held none.</summary>
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

        /// <summary>Empties the book (the city is unloading).</summary>
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
            if (_byAddress.Count == 0 || string.IsNullOrEmpty(street) || !_byAddress.TryGetValue(Key(street, number), out SpecialEntry entry))
            {
                return null;
            }
            Func<bool> shown = entry.Shown;
            return shown == null || shown() ? entry : null;
        }

        /// <summary>True if any feature holds the building at an address, whether or not its name is shown just now.</summary>
        public bool Holds(string street, int number)
        {
            return _byAddress.Count > 0 && !string.IsNullOrEmpty(street) && _byAddress.ContainsKey(Key(street, number));
        }

        /// <summary>
        /// The pick of a feature that takes "the largest building of a kind": of the candidates that may serve
        /// (<paramref name="fits"/>), the one with the most floor, ties broken by address (street, then number, as the
        /// features have always broken them: "street#number" in ordinal order), leaving out every building another feature
        /// holds (<paramref name="taken"/>). Returns its index, or -1. The same city gives the same pick in every game.
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

        /// <summary>The id of the feature that holds the building at an address, whether or not its name is shown just now; null for none.</summary>
        public string HolderOf(string street, int number)
        {
            return _byAddress.Count > 0 && !string.IsNullOrEmpty(street) && _byAddress.TryGetValue(Key(street, number), out SpecialEntry entry) ? entry.Id : null;
        }

        /// <summary>The entry an id holds, shown or not, or null.</summary>
        public SpecialEntry Of(string id)
        {
            return !string.IsNullOrEmpty(id) && _byId.TryGetValue(id, out SpecialEntry entry) ? entry : null;
        }

        /// <summary>
        /// The name of the building at an address in the language of the moment, or null for one that is not in the book,
        /// whose feature is off, or that is the player's (<paramref name="players"/>: rented or owned by the player).
        /// </summary>
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

        // ---------------------------------------------------------------- off the market

        /// <summary>
        /// True if a feature may make a building its own: nobody of the players rents it or owns it (in a Going Public
        /// session another player's rental is on every machine as rented by the player, so the answer is the same
        /// everywhere). A building the player already has stays theirs: the feature chooses another.
        /// </summary>
        public static bool MaySit(bool rented, bool owned)
        {
            return !rented && !owned;
        }

        /// <summary>
        /// True if the building at an address is off the market here: a feature holds it, that feature is on, and the
        /// player neither rents nor owns it (what the player already has stays theirs to keep, run and give up). Its
        /// offers to rent, to buy and to take over are not shown and it is left out of the listings. Nothing of it is
        /// written into the save, so without the mod it is on the market again as the game has it.
        /// </summary>
        public bool Refuses(string street, int number, bool rented, bool owned)
        {
            return !rented && !owned && At(street, number) != null;
        }

        /// <summary>Every entry whose feature shows it just now, into a list the caller keeps (cleared first).</summary>
        public void Shown(List<SpecialEntry> into)
        {
            into.Clear();
            foreach (SpecialEntry entry in _byId.Values)
            {
                if (entry.Shown == null || entry.Shown())
                {
                    into.Add(entry);
                }
            }
        }

        /// <summary>Every name is looked up again the next time it is asked for (the language has changed).</summary>
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
