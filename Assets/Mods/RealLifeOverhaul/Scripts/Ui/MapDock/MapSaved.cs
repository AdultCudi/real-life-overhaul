using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Ui
{
    /// <summary>One place the player saved: its street and number as the game keeps an address, and the name it had when it was saved.</summary>
    public struct MapSavedPlace
    {
        public string Street;
        public int Number;
        public string Name;
    }

    /// <summary>
    /// The player's saved places: a list kept in the save (the place card's Save and the "Your places" line), each a street, a number and the name it had. The player's
    /// bookmark and nothing else: rivals and the city's people have none, and a place that is saved changes nothing in the world. The line in the save,
    /// <c>reallife:map_places</c>: <c>1;street|number|name;street|number|name</c>; a place is in the list once, the newest first, and at most <see cref="Most"/>. A line this
    /// version cannot read is left as it was read (Core/SaveLines). Game-free.
    /// </summary>
    public sealed class MapSaved
    {
        public const string Key = "reallife:map_places";
        public const int Most = 40;
        private const string Version = "1";

        private readonly List<MapSavedPlace> _places = new List<MapSavedPlace>(8);

        public int Count => _places.Count;

        public MapSavedPlace this[int index] => _places[index];

        /// <summary>The place's index in the list, or -1.</summary>
        public int IndexOf(string street, int number)
        {
            street = Clean(street);
            for (int i = 0; i < _places.Count; i++)
            {
                if (_places[i].Number == number && string.Equals(_places[i].Street, street, StringComparison.Ordinal))
                {
                    return i;
                }
            }
            return -1;
        }

        public bool Contains(string street, int number) => IndexOf(street, number) >= 0;

        /// <summary>Saves a place at the front; false when it has no street or is already there. At the most, the oldest goes.</summary>
        public bool Add(string street, int number, string name)
        {
            street = Clean(street);
            if (string.IsNullOrEmpty(street) || Contains(street, number))
            {
                return false;
            }
            _places.Insert(0, new MapSavedPlace { Street = street, Number = number, Name = Clean(name) ?? string.Empty });
            while (_places.Count > Most)
            {
                _places.RemoveAt(_places.Count - 1);
            }
            return true;
        }

        public bool Remove(string street, int number)
        {
            int at = IndexOf(street, number);
            if (at < 0)
            {
                return false;
            }
            _places.RemoveAt(at);
            return true;
        }

        /// <summary>The place saved if it was not, taken out if it was; returns whether it is saved now.</summary>
        public bool Toggle(string street, int number, string name)
        {
            if (Contains(street, number))
            {
                Remove(street, number);
                return false;
            }
            return Add(street, number, name);
        }

        // The separators of the line never ride in a name.
        private static string Clean(string text) => text == null ? null : text.Replace(';', ' ').Replace('|', ' ').Trim();

        /// <summary>The line for the save, or null when none is saved (no key then).</summary>
        public string Write()
        {
            if (_places.Count == 0)
            {
                return null;
            }
            var w = new StringBuilder(16 + _places.Count * 40);
            w.Append(Version);
            for (int i = 0; i < _places.Count; i++)
            {
                w.Append(';').Append(_places[i].Street).Append('|').Append(_places[i].Number.ToString(CultureInfo.InvariantCulture)).Append('|').Append(_places[i].Name);
            }
            return w.ToString();
        }

        /// <summary>True when a line is one this version reads: none, or one of its own version.</summary>
        public static bool Readable(string line)
        {
            if (string.IsNullOrEmpty(line))
            {
                return true;
            }
            int cut = line.IndexOf(';');
            return (cut < 0 ? line : line.Substring(0, cut)) == Version;
        }

        /// <summary>The places a line holds; one that cannot be read is left out, a line of another version gives none.</summary>
        public static MapSaved Read(string line)
        {
            var saved = new MapSaved();
            if (!Readable(line) || string.IsNullOrEmpty(line))
            {
                return saved;
            }
            string[] parts = line.Split(';');
            for (int i = parts.Length - 1; i >= 1; i--)   // the list is newest first: added from the oldest, each to the front
            {
                string[] one = parts[i].Split('|');
                if (one.Length == 3 && int.TryParse(one[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int number))
                {
                    saved.Add(one[0], number, one[2]);
                }
            }
            return saved;
        }
    }
}
