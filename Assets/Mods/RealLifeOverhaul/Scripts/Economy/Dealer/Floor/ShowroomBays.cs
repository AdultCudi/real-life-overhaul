using System;
using System.Collections.Generic;
using System.Text;

namespace RealLifeOverhaul.Economy.Dealership
{
    /// <summary>The bay the player chose for a car of a Car Showroom's, and whether it stands the other way round.</summary>
    public sealed class BayChoice
    {
        public string Place;                // the showroom, as the company writes a place
        public string Car;                  // the game's VehicleInstance.id
        public int Bay;
        public bool Turned;                 // facing the other way from the bay's own
    }

    /// <summary>
    /// The bays the player chose for the cars of their Car Showrooms, by the game's vehicle id. Only a choice is kept: a
    /// car nobody chose a bay for stands where the showroom put it (DealerState's place of the car), and a car that is no
    /// longer on the lot loses its choice when the save is written (<see cref="Prune"/>). Game-free.
    /// </summary>
    public sealed class ShowroomBays
    {
        private readonly Dictionary<string, BayChoice> _byCar = new Dictionary<string, BayChoice>(StringComparer.Ordinal);
        private readonly List<string> _drop = new List<string>(4);

        public int Count => _byCar.Count;

        public IEnumerable<BayChoice> All => _byCar.Values;

        /// <summary>The choice for a car of this showroom; null if there is none.</summary>
        public BayChoice Of(string place, string car)
        {
            return car != null && _byCar.TryGetValue(car, out BayChoice c) && c.Place == place ? c : null;
        }

        /// <summary>The bay chosen for a car of this showroom; -1 for none.</summary>
        public int BayOf(string place, string car)
        {
            BayChoice c = Of(place, car);
            return c != null ? c.Bay : -1;
        }

        public bool TurnedOf(string place, string car)
        {
            BayChoice c = Of(place, car);
            return c != null && c.Turned;
        }

        public void Choose(string place, string car, int bay, bool turned)
        {
            if (string.IsNullOrEmpty(car) || bay < 0)
            {
                return;
            }
            if (!_byCar.TryGetValue(car, out BayChoice c))
            {
                c = new BayChoice { Car = car };
                _byCar[car] = c;
            }
            c.Place = place ?? string.Empty;
            c.Bay = bay;
            c.Turned = turned;
        }

        public void Forget(string car)
        {
            if (car != null)
            {
                _byCar.Remove(car);
            }
        }

        public void Clear()
        {
            _byCar.Clear();
        }

        /// <summary>Every choice whose car is not among these (the showrooms' cars now) is dropped. Returns how many.</summary>
        public int Prune(HashSet<string> cars)
        {
            _drop.Clear();
            foreach (KeyValuePair<string, BayChoice> pair in _byCar)
            {
                if (cars == null || !cars.Contains(pair.Key))
                {
                    _drop.Add(pair.Key);
                }
            }
            for (int i = 0; i < _drop.Count; i++)
            {
                _byCar.Remove(_drop[i]);
            }
            return _drop.Count;
        }
    }

    /// <summary>
    /// What the Car Showroom's bays keep in a save: one line under its own key, apart from the showroom's own line, so
    /// that a save without it still loads and the save tools take it out with the mod's other lines:
    ///
    ///     v=1^Main%20Street~12|AbC0dE+fG/h|3|0^...
    ///
    /// the version, then for each choice the showroom's place, the vehicle's id, the bay and 1 if it stands turned round,
    /// sorted by the vehicle's id so that the same choices give the same line. In a place or an id, '%', '^' and '|' are
    /// written as %25, %5E and %7C. A line of a later version is read as far as its first four fields go (a later one
    /// adds fields at the end of a record); one that cannot be read gives no choices and is kept in the save as it was
    /// (Core/SaveLines). Game-free. Cost: nothing unless a save is loaded or written.
    /// </summary>
    public static class ShowroomBayStore
    {
        public const string Key = "reallife:showroom_bays";
        public const int Version = 1;

        public static string Write(ShowroomBays bays)
        {
            var sorted = new List<BayChoice>(bays.Count);
            foreach (BayChoice c in bays.All)
            {
                sorted.Add(c);
            }
            sorted.Sort((a, b) => string.CompareOrdinal(a.Car, b.Car));
            var t = new StringBuilder(8 + sorted.Count * 48);
            t.Append("v=").Append(Version);
            for (int i = 0; i < sorted.Count; i++)
            {
                BayChoice c = sorted[i];
                t.Append('^').Append(Escape(c.Place)).Append('|').Append(Escape(c.Car)).Append('|')
                 .Append(c.Bay.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append('|').Append(c.Turned ? '1' : '0');
            }
            return t.ToString();
        }

        /// <summary>Reads a line into the choices. False if it cannot be read: there are then none. The version read is given back.</summary>
        public static bool Read(string text, ShowroomBays into, out int version)
        {
            into.Clear();
            version = Version;
            if (string.IsNullOrEmpty(text))
            {
                return true;
            }
            string[] parts = text.Split('^');
            if (!parts[0].StartsWith("v=", StringComparison.Ordinal) || !int.TryParse(parts[0].Substring(2), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out version) || version < 1)
            {
                return false;
            }
            for (int i = 1; i < parts.Length; i++)
            {
                string[] f = parts[i].Split('|');
                if (f.Length < 4 || f[1].Length == 0 || !int.TryParse(f[2], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int bay) || bay < 0
                    || (f[3] != "0" && f[3] != "1"))
                {
                    into.Clear();
                    return false;
                }
                into.Choose(Unescape(f[0]), Unescape(f[1]), bay, f[3] == "1");
            }
            return true;
        }

        private static string Escape(string s)
        {
            return (s ?? string.Empty).Replace("%", "%25").Replace("^", "%5E").Replace("|", "%7C");
        }

        private static string Unescape(string s)
        {
            return s.Replace("%7C", "|").Replace("%5E", "^").Replace("%25", "%");
        }
    }
}
