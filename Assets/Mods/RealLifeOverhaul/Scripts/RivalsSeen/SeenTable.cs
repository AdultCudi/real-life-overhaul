using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.RivalsSeen
{
    /// <summary>
    /// The city as the rivals seen around it need it, made on the day's pass from what SeenGame reads of the city's buildings: the game's cars as a ladder by
    /// price, the eating places by district and the rent of their address, and the buildings of the day's rivals' own premises. Only that is kept: a business
    /// that is neither an eating place nor at the address of one of the day's offices is passed over, its address matched by number and then street with no
    /// string made. Game-free: the director's table and the checks' are the same code, and a building is the game's own object, held and handed back but never
    /// looked into. COST, measured by the checks on a city of 622 businesses: about 0.06 ms and 64 bytes a table (the sort's), where a key string for every
    /// business cost 39.5 KB; an hour's places about 0.015 ms and nothing allocated, a walk of a district's eating places for each rival due.
    /// </summary>
    public sealed class SeenTable : ISeenCity
    {
        private struct Eating
        {
            public string District, Street, Name;
            public int Number, Rent;
            public float X, Z;
            public object Building;
        }

        // By district, then the rent of the address, then the address as "street#number" compares by its characters: the order the table always had, so the
        // same pick names the same place it did.
        private sealed class ByDistrictAndRent : IComparer<Eating>
        {
            public int Compare(Eating a, Eating b)
            {
                int d = string.CompareOrdinal(a.District, b.District);
                if (d != 0)
                {
                    return d;
                }
                if (a.Rent != b.Rent)
                {
                    return a.Rent < b.Rent ? -1 : 1;
                }
                return AddressOrder(a.Street, a.Number, b.Street, b.Number);
            }
        }

        private static readonly ByDistrictAndRent Order = new ByDistrictAndRent();

        private readonly List<Eating> _eating = new List<Eating>(64);
        private readonly List<string> _districts = new List<string>(8);
        private readonly List<string> _cars = new List<string>(16);
        private readonly List<string> _offices = new List<string>(32);     // the day's offices, by their address key as the rivals' sources write it
        private readonly List<int> _officeCut = new List<int>(32), _officeNumber = new List<int>(32);   // ...taken apart: where the street ends, and the number
        private readonly List<object> _officeAt = new List<object>(32);    // ...the building found there; null while none is
        private readonly List<float> _officeX = new List<float>(32), _officeZ = new List<float>(32);
        private readonly ulong[] _numbers = new ulong[4];                  // ...their numbers' low eight bits, so a business at none of them is passed over at once
        private readonly List<int> _open = new List<int>(16);
        private string _away = string.Empty;

        /// <summary>The game's opening hours: whether a building is open at an hour of a day. Unset, every place is taken to be open.</summary>
        public Func<object, int, int, bool> Open;

        /// <summary>The district nobody eats in on a working day: the Hamptons, where a rival only goes for the weekend.</summary>
        public string Away => _away;

        /// <summary>The eating places on the table, for the log.</summary>
        public int Eateries => _eating.Count;

        /// <summary>The day's offices, and how many of them the city has a building for, for the log.</summary>
        public int Offices => _offices.Count;

        public int OfficesFound
        {
            get
            {
                int found = 0;
                for (int i = 0; i < _officeAt.Count; i++)
                {
                    found += _officeAt[i] != null ? 1 : 0;
                }
                return found;
            }
        }

        /// <summary>True for a business people eat at: the game's restaurants, coffee shops and cafes, by the type's own id.</summary>
        public static bool Eats(string type)
        {
            return !string.IsNullOrEmpty(type) && (type.IndexOf("restaurant", StringComparison.OrdinalIgnoreCase) >= 0 || type.IndexOf("coffeeshop", StringComparison.OrdinalIgnoreCase) >= 0
                                                    || type.IndexOf("cafe", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        /// <summary>A new table: everything of the last one goes, and the offices of the day's rivals are the addresses looked for.</summary>
        public void Begin(IList<SeenSubject> subjects, string away)
        {
            Forget();
            _away = away ?? string.Empty;
            for (int i = 0; subjects != null && i < subjects.Count; i++)
            {
                string office = subjects[i] != null ? subjects[i].Office : null;
                if (!string.IsNullOrEmpty(office) && !_offices.Contains(office))
                {
                    SeenRules.Address(office, out int cut, out int number);
                    _numbers[(number & 255) >> 6] |= 1UL << (number & 63);
                    _offices.Add(office);
                    _officeCut.Add(cut);
                    _officeNumber.Add(number);
                    _officeAt.Add(null);
                    _officeX.Add(0f);
                    _officeZ.Add(0f);
                }
            }
        }

        /// <summary>A car of the game's, in the order of the ladder: the cheapest first.</summary>
        public void AddCar(string type)
        {
            if (!string.IsNullOrEmpty(type))
            {
                _cars.Add(type);
            }
        }

        /// <summary>
        /// The place among the day's offices of a business's address, or -1. Asked of every business of the city, so no string is made, and a number none of the
        /// offices has is turned away by one test of a bit.
        /// </summary>
        public int OfficeAt(string street, int number)
        {
            if ((_numbers[(number & 255) >> 6] & (1UL << (number & 63))) == 0)
            {
                return -1;
            }
            int length = street != null ? street.Length : 0;
            for (int i = 0; i < _offices.Count; i++)
            {
                if (_officeNumber[i] == number && _officeCut[i] == length && (length == 0 || string.CompareOrdinal(_offices[i], 0, street, 0, length) == 0))
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>The building found at one of the day's offices (<see cref="OfficeAt"/>), and where it stands.</summary>
        public void SetOffice(int office, object building, float x, float z)
        {
            if (office < 0 || office >= _offices.Count || building == null)
            {
                return;
            }
            _officeAt[office] = building;
            _officeX[office] = x;
            _officeZ[office] = z;
        }

        /// <summary>An eating place of the city: its district, its address, its name, the rent of its address and where it stands.</summary>
        public void AddEating(string district, string street, int number, string name, int rent, float x, float z, object building)
        {
            if (building == null || string.IsNullOrEmpty(district))
            {
                return;
            }
            _eating.Add(new Eating { District = district, Street = street ?? string.Empty, Number = number, Name = name ?? string.Empty, Rent = Math.Max(0, rent), X = x, Z = z, Building = building });
        }

        /// <summary>The table is whole: the eating places in their order, and the districts a rival may eat in on a working day, each once.</summary>
        public void Done()
        {
            _eating.Sort(Order);
            _districts.Clear();
            for (int i = 0; i < _eating.Count; i++)
            {
                string d = _eating[i].District;
                if (d != _away && (_districts.Count == 0 || _districts[_districts.Count - 1] != d))
                {
                    _districts.Add(d);   // (sorted by district, so each comes once)
                }
            }
        }

        /// <summary>Everything is forgotten: a city unloading, the feature switched off, or a new table begun.</summary>
        public void Forget()
        {
            _eating.Clear();
            _districts.Clear();
            _cars.Clear();
            _offices.Clear();
            _officeCut.Clear();
            _officeNumber.Clear();
            _officeAt.Clear();
            _officeX.Clear();
            _officeZ.Clear();
            Array.Clear(_numbers, 0, _numbers.Length);
            _open.Clear();
        }

        public int Cars => _cars.Count;

        public string Car(int index) => index >= 0 && index < _cars.Count ? _cars[index] : string.Empty;

        /// <summary>The place of a car on the ladder; -1 for one that is not on it.</summary>
        public int CarPlace(string type) => string.IsNullOrEmpty(type) ? -1 : _cars.IndexOf(type);

        public int Districts => _districts.Count;

        public string District(int index) => index >= 0 && index < _districts.Count ? _districts[index] : string.Empty;

        public bool Eatery(string district, int grade, int pick, int day, int hour, out string name)
        {
            int at = Find(district, grade, pick, day, hour);
            name = at >= 0 ? _eating[at].Name : string.Empty;
            return at >= 0;
        }

        // The eating place of a district for a grade and a pick, among those open at that hour of that day; -1 with none.
        private int Find(string district, int grade, int pick, int day, int hour)
        {
            _open.Clear();
            for (int i = 0; i < _eating.Count; i++)
            {
                if (_eating[i].District == district && (Open == null || Open(_eating[i].Building, day, hour)))
                {
                    _open.Add(i);
                }
            }
            int index = SeenRules.VenueIndex(_open.Count, grade, pick);
            int found = index >= 0 ? _open[index] : -1;
            _open.Clear();
            return found;
        }

        /// <summary>The building of a rival's own premises by its address key, and where it stands; false where the city has none there.</summary>
        public bool Office(string key, out object building, out float x, out float z)
        {
            for (int i = 0; key != null && i < _offices.Count; i++)
            {
                if (_officeAt[i] != null && string.Equals(_offices[i], key, StringComparison.Ordinal))
                {
                    building = _officeAt[i];
                    x = _officeX[i];
                    z = _officeZ[i];
                    return true;
                }
            }
            building = null;
            x = z = 0f;
            return false;
        }

        /// <summary>
        /// The building a rival is at for a place of their week, its name and where it stands: their own premises for the office, an eating place of their district
        /// for a meal, one of the Hamptons' for the beach and the weekend there. Null where the city has no such building, or it is shut at that hour.
        /// </summary>
        public object Venue(SeenFile f, SeenSubject s, SeenDue due, int day, int hour, out string name, out float x, out float z)
        {
            name = string.Empty;
            x = z = 0f;
            switch (due.Where.Spot)
            {
                case SeenSpot.Office:
                case SeenSpot.Late:
                {
                    name = s.Firm ?? string.Empty;
                    return Office(s.Office, out object office, out x, out z) ? office : null;
                }
                case SeenSpot.Lunch:
                case SeenSpot.Dinner:
                case SeenSpot.Beach:
                case SeenSpot.Hamptons:
                {
                    bool away = due.Where.Spot == SeenSpot.Beach || due.Where.Spot == SeenSpot.Hamptons;
                    int at = Find(away ? _away : SeenDay.DistrictOf(f, s, this), away ? due.Pick % SeenCatalogue.Grades : due.Where.Grade, due.Pick, day, hour);
                    if (at < 0)
                    {
                        return null;
                    }
                    name = _eating[at].Name;
                    x = _eating[at].X;
                    z = _eating[at].Z;
                    return _eating[at].Building;
                }
                default:
                    return null;
            }
        }

        /// <summary>
        /// The hour's places: everybody who is somewhere this hour in their turn (<see cref="SeenRules.Due"/>), each with the building they are at, its name and
        /// where it stands, and no placing failed yet. Made when the hour turns; the list is the caller's, and nothing is allocated once it has grown.
        /// </summary>
        public void Places(SeenFile f, IList<SeenSubject> subjects, int day, int hour, List<SeenDue> into)
        {
            SeenRules.Due(f, subjects, day, hour, into);
            for (int i = 0; i < into.Count; i++)
            {
                SeenDue d = into[i];
                d.Building = Venue(f, subjects[d.Subject], d, day, hour, out string name, out float x, out float z);
                d.Place = name;
                d.X = x;
                d.Z = z;
                d.Failed = false;
                into[i] = d;
            }
        }

        /// <summary>
        /// Two addresses in the order their keys ("street#number", the number in invariant digits) compare by their characters, worked out without making either
        /// key: the street's characters, then '#', then the number's.
        /// </summary>
        public static int AddressOrder(string streetA, int numberA, string streetB, int numberB)
        {
            streetA = streetA ?? string.Empty;
            streetB = streetB ?? string.Empty;
            int lengthA = streetA.Length + 1 + Digits(numberA), lengthB = streetB.Length + 1 + Digits(numberB);
            for (int i = 0; i < lengthA && i < lengthB; i++)
            {
                char a = KeyChar(streetA, numberA, i), b = KeyChar(streetB, numberB, i);
                if (a != b)
                {
                    return a < b ? -1 : 1;
                }
            }
            return lengthA.CompareTo(lengthB);
        }

        // The characters of a number written in invariant digits, a minus sign counted.
        private static int Digits(int number)
        {
            long n = number;
            int count = n < 0 ? 2 : 1;
            for (n = Math.Abs(n); n >= 10; n /= 10)
            {
                count++;
            }
            return count;
        }

        // A character of an address's key by its place: the street's, the '#', then the number's.
        private static char KeyChar(string street, int number, int index)
        {
            if (index < street.Length)
            {
                return street[index];
            }
            if (index == street.Length)
            {
                return '#';
            }
            int at = index - street.Length - 1;
            long n = number;
            if (n < 0)
            {
                if (at == 0)
                {
                    return '-';
                }
                at--;
                n = -n;
            }
            long place = 1;
            for (int k = Digits(number) - (number < 0 ? 1 : 0) - 1 - at; k > 0; k--)   // (the digits alone, without the sign)
            {
                place *= 10;
            }
            return (char)('0' + (int)(n / place % 10));
        }
    }
}
