using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Household
{
    /// <summary>
    /// Household staff in the save: one line of modData under <see cref="Key"/>, game-free.
    ///
    ///     v=2^h:...^s:...;...^n:...
    ///
    /// Parts are divided by '^' and begin with a letter and a colon; records inside a part by ';', fields by '|'.
    ///   h  seed|last hour stamp|home number|home street|candidates put forward by kind (four)|note day
    ///   s  a person: kind|name|female|age|look seed|skill|cents an hour|day taken on|visits|missed|paid in all|visit day|dish|
    ///      that visit's charge|home number|home street
    ///   n  the day's note so far
    ///
    /// Version 1 had one home for everybody, in the header. Since version 2 each person has their own at the end of their
    /// record; a person without one (a version 1 line) works at the header's home, so a save from before keeps everybody
    /// where they were. The header still names the first person's home, so an older reader keeps the staff at a home of
    /// the player's. Names, streets and the note are escaped. A newer version adds fields at the end or a part with a new
    /// letter: this reader takes what it knows, puts every figure inside its bounds, and gives null for a line it cannot
    /// read at all.
    /// </summary>
    public static class HouseholdStore
    {
        public const string Key = "reallife:home_household";
        public const int Version = 2;

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static HouseholdState Load(Dictionary<string, string> data, out int version)
        {
            version = 0;
            if (data == null || !data.TryGetValue(Key, out string text) || string.IsNullOrEmpty(text))
            {
                return null;
            }
            return SaveLines.Checked(data, Key, Read(text, out version), version, Version);   // (a line that cannot be read is kept as it is: Core/SaveLines)
        }

        public static string Write(HouseholdState s)
        {
            var t = new StringBuilder(128 + s.Staff.Count * 64);
            StaffMember first = s.Staff.Count > 0 ? s.Staff[0] : null;
            t.Append("v=").Append(Version).Append("^h:");
            Join(t, s.Seed, s.LastStamp, first != null ? first.HomeNumber : 0);
            t.Append('|').Append(Esc(first != null ? first.HomeStreet : null));
            for (int k = 1; k < s.Rolls.Length; k++)
            {
                t.Append('|').Append(s.Rolls[k].ToString(Inv));
            }
            t.Append('|').Append(s.NoteDay.ToString(Inv));
            if (s.Staff.Count > 0)
            {
                t.Append("^s:");
                for (int i = 0; i < s.Staff.Count && i < HouseholdRules.Most; i++)
                {
                    StaffMember m = s.Staff[i];
                    t.Append(i > 0 ? ";" : string.Empty);
                    Join(t, (int)m.Kind);
                    t.Append('|').Append(Esc(m.Name)).Append('|');
                    Join(t, m.Female ? 1 : 0, m.Age, m.Seed, m.Skill, m.Hourly, m.HiredDay, m.Visits, m.Missed, m.PaidTotal, m.OnDay, m.Dish, m.Charged, m.HomeNumber);
                    t.Append('|').Append(Esc(m.HomeStreet));
                }
            }
            if (!string.IsNullOrEmpty(s.Note))
            {
                t.Append("^n:").Append(Esc(s.Note));
            }
            return t.ToString();
        }

        /// <summary>Reads a line. Null for one that is not this feature's at all.</summary>
        public static HouseholdState Read(string text, out int version)
        {
            version = 0;
            if (string.IsNullOrEmpty(text) || !text.StartsWith("v=", StringComparison.Ordinal))
            {
                return null;
            }
            string[] parts = text.Split('^');
            if (!int.TryParse(parts[0].Substring(2), NumberStyles.Integer, Inv, out version) || version < 1)
            {
                return null;
            }
            var s = new HouseholdState();
            bool header = false;
            string homeStreet = null;   // version 1's one home, for everybody without one of their own
            int homeNumber = 0;
            string[] people = null;
            for (int i = 1; i < parts.Length; i++)
            {
                string part = parts[i];
                if (part.Length < 2 || part[1] != ':')
                {
                    continue;
                }
                string body = part.Substring(2);
                switch (part[0])
                {
                    case 'h':
                    {
                        string[] f = body.Split('|');
                        s.Seed = Int(f, 0);
                        s.LastStamp = Math.Max(0, Int(f, 1));
                        homeNumber = Int(f, 2);
                        homeStreet = Unesc(Str(f, 3));
                        for (int k = 1; k < s.Rolls.Length; k++)
                        {
                            s.Rolls[k] = Math.Max(0, Int(f, 3 + k));
                        }
                        s.NoteDay = Math.Max(0, Int(f, 3 + s.Rolls.Length));
                        header = true;
                        break;
                    }
                    case 's':
                        people = body.Split(';');   // read once the header is known: it may come after
                        break;
                    case 'n':
                        s.Note = Unesc(body);
                        break;
                }
            }
            if (!header)
            {
                return null;
            }
            for (int r = 0; people != null && r < people.Length && s.Staff.Count < HouseholdRules.Most; r++)
            {
                StaffMember m = Member(people[r].Split('|'), homeStreet, homeNumber);
                // Somebody without a home to work at is nobody on the staff; one of each kind at each home, the first kept.
                if (m != null && !string.IsNullOrEmpty(m.HomeStreet) && !s.Has(m.Kind, m.HomeStreet, m.HomeNumber)
                    && (HouseholdRules.Staffed(s.Staff, m.HomeStreet, m.HomeNumber) || HouseholdRules.HomesStaffed(s.Staff) < HouseholdRules.HomesMost))
                {
                    s.Staff.Add(m);
                }
            }
            return s;
        }

        private static StaffMember Member(string[] f, string homeStreet, int homeNumber)
        {
            int kind = Int(f, 0);
            string name = Unesc(Str(f, 1));
            if (HouseholdRules.Role((StaffKind)kind) == null || string.IsNullOrEmpty(name) || f.Length < 13)
            {
                return null;
            }
            var m = new StaffMember
            {
                Kind = (StaffKind)kind, Name = name, Female = Int(f, 2) != 0, Age = Bound(Int(f, 3), 16, 99), Seed = Int(f, 4), Skill = Bound(Int(f, 5), 1, 100),
                Hourly = Math.Max(0, Int(f, 6)), HiredDay = Math.Max(0, Int(f, 7)), Visits = Math.Max(0, Int(f, 8)), Missed = Bound(Int(f, 9), 0, HouseholdRules.QuitAfter),
                PaidTotal = Math.Max(0, Int(f, 10)), OnDay = Math.Max(-1, Int(f, 11)), Dish = Bound(Int(f, 12), -1, HouseholdRules.CookDishes.Length - 1),
                Charged = Math.Max(0, Int(f, 13))
            };
            if (m.Seed == 0)
            {
                m.Seed = 1;
            }
            string street = Unesc(Str(f, 15));
            if (string.IsNullOrEmpty(street))
            {
                m.HomeStreet = homeStreet;   // a version 1 record: the one home everybody worked at
                m.HomeNumber = homeNumber;
            }
            else
            {
                m.HomeStreet = street;
                m.HomeNumber = Int(f, 14);
            }
            return m;
        }

        private static int Bound(int value, int low, int high) => value < low ? low : value > high ? high : value;

        private static void Join(StringBuilder t, params int[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                if (i > 0)
                {
                    t.Append('|');
                }
                t.Append(values[i].ToString(Inv));
            }
        }

        private static string Str(string[] f, int index) => index < f.Length ? f[index] : string.Empty;

        private static int Int(string[] f, int index)
        {
            return index < f.Length && int.TryParse(f[index], NumberStyles.Integer, Inv, out int value) ? value : 0;
        }

        private static string Esc(string text) => string.IsNullOrEmpty(text) ? string.Empty : Uri.EscapeDataString(text);

        private static string Unesc(string text)
        {
            try
            {
                return string.IsNullOrEmpty(text) ? null : Uri.UnescapeDataString(text);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
