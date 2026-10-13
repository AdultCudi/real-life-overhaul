using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Permadeath
{
    /// <summary>
    /// A life as the mod's records tell it, taken down at the moment it ended (or for a test, at the moment it was asked
    /// for): who they were, how they died, what they built and owned, the people in their life, their record and what
    /// the city knew of them. The obituary is written from this and nothing else, so a save opened again prints the same
    /// page whatever has changed since. Game-free; one line of text (<see cref="Write"/>, <see cref="Read"/>).
    /// </summary>
    public sealed class LifeStory
    {
        public const int NamesMost = 6;      // names kept in each list: a page has room for a few
        public const int BusinessesMost = 3;
        public const int MournersMost = 8;
        private const int Version = 1;
        private const char Field = '|', Item = ';';

        public string Name = string.Empty;
        public int Age;                      // years
        public int Day;                      // the game day it ended on (day 1 is the first)
        public int DaysPerYear = 60;         // the save's own
        public DeathCause Cause;
        public string Place = string.Empty;  // the street it happened in; empty when not known (an illness)

        public long Worth;                   // the estate: what the game counts the player worth, less what the mod knows they owe; may be below zero
        public int Founded;                  // businesses run in the life, ever
        public int Owned;                    // businesses running at the end
        public int Employees;
        public int Properties;
        public readonly List<string> Businesses = new List<string>(BusinessesMost);   // the names of those running at the end

        public int Friends;                  // friends and close friends (not family, not the partner)
        public readonly List<string> Close = new List<string>(NamesMost);            // the close friends, by name
        public string Partner = string.Empty;
        public int Bond;                     // 0 none, 1 together, 2 engaged, 3 married
        public readonly List<string> Children = new List<string>(NamesMost);
        public readonly List<string> Family = new List<string>(NamesMost);           // the people of their own family they knew (Mom)

        public int Arrests;
        public int Convictions;
        public int Followers;                // on Flare
        public int Elections;                // voted in

        /// <summary>The days lived in the city (the first day counts as one), whole years of them and the days over.</summary>
        public int Days => Day < 1 ? 1 : Day;
        public int Years => Days / (DaysPerYear < 1 ? 1 : DaysPerYear);
        public int DaysOver => Days % (DaysPerYear < 1 ? 1 : DaysPerYear);

        /// <summary>
        /// Who mourns them, in the order a notice names them: the partner, the children, the family, then the close
        /// friends; nobody twice, at most <see cref="MournersMost"/>.
        /// </summary>
        public void Mourners(List<string> into)
        {
            into.Clear();
            Add(into, Partner);
            for (int i = 0; i < Children.Count; i++) Add(into, Children[i]);
            for (int i = 0; i < Family.Count; i++) Add(into, Family[i]);
            for (int i = 0; i < Close.Count; i++) Add(into, Close[i]);
        }

        private static void Add(List<string> into, string name)
        {
            if (into.Count < MournersMost && !string.IsNullOrEmpty(name) && !into.Contains(name))
            {
                into.Add(name);
            }
        }

        /// <summary>A name kept in one of the lists, if there is room and it is not there already.</summary>
        public static void Keep(List<string> list, string name, int most = NamesMost)
        {
            name = Clean(name);
            if (list.Count < most && name.Length > 0 && !list.Contains(name))
            {
                list.Add(name);
            }
        }

        public void Clear()
        {
            Name = string.Empty;
            Age = 0;
            Day = 0;
            DaysPerYear = 60;
            Cause = DeathCause.None;
            Place = string.Empty;
            Worth = 0;
            Founded = Owned = Employees = Properties = 0;
            Businesses.Clear();
            Friends = 0;
            Close.Clear();
            Partner = string.Empty;
            Bond = 0;
            Children.Clear();
            Family.Clear();
            Arrests = Convictions = Followers = Elections = 0;
        }

        public void CopyFrom(LifeStory o)
        {
            Read(o.Write());
        }

        // ---------------------------------------------------------------- the line

        public string Write()
        {
            var t = new StringBuilder(256);
            t.Append(Version.ToString(CultureInfo.InvariantCulture)).Append(Field).Append(Clean(Name)).Append(Field);
            Num(t, Age).Num(t, Day).Num(t, DaysPerYear).Num(t, (int)Cause);
            t.Append(Clean(Place)).Append(Field);
            t.Append(Worth.ToString(CultureInfo.InvariantCulture)).Append(Field);
            Num(t, Founded).Num(t, Owned).Num(t, Employees).Num(t, Properties);
            List(t, Businesses);
            Num(t, Friends);
            List(t, Close);
            t.Append(Clean(Partner)).Append(Field);
            Num(t, Bond);
            List(t, Children);
            List(t, Family);
            Num(t, Arrests).Num(t, Convictions).Num(t, Followers).Num(t, Elections);
            return t.ToString();
        }

        /// <summary>False (and the story cleared) if the line is not one of this version's.</summary>
        public bool Read(string line)
        {
            Clear();
            if (string.IsNullOrEmpty(line))
            {
                return false;
            }
            string[] f = line.Split(Field);
            if (f.Length < 23 || Int(f[0]) != Version)
            {
                return false;
            }
            int i = 1;
            Name = f[i++];
            Age = Clamp(Int(f[i++]), 0, 200);
            Day = Clamp(Int(f[i++]), 0, int.MaxValue);
            DaysPerYear = Clamp(Int(f[i++]), 1, 10000);
            int cause = Int(f[i++]);
            Cause = cause >= 0 && cause <= (int)DeathCause.Test ? (DeathCause)cause : DeathCause.None;
            Place = f[i++];
            long.TryParse(f[i++], NumberStyles.Integer, CultureInfo.InvariantCulture, out Worth);
            Founded = Clamp(Int(f[i++]), 0, 100000);
            Owned = Clamp(Int(f[i++]), 0, 100000);
            Employees = Clamp(Int(f[i++]), 0, 1000000);
            Properties = Clamp(Int(f[i++]), 0, 100000);
            Items(f[i++], Businesses, BusinessesMost);
            Friends = Clamp(Int(f[i++]), 0, 100000);
            Items(f[i++], Close, NamesMost);
            Partner = f[i++];
            Bond = Clamp(Int(f[i++]), 0, 3);
            Items(f[i++], Children, NamesMost);
            Items(f[i++], Family, NamesMost);
            Arrests = Clamp(Int(f[i++]), 0, 100000);
            Convictions = Clamp(Int(f[i++]), 0, 100000);
            Followers = Clamp(Int(f[i++]), 0, int.MaxValue);
            Elections = Clamp(Int(f[i++]), 0, 100000);
            return true;
        }

        /// <summary>A name as the line can hold it: no dividers, no line breaks, trimmed.</summary>
        public static string Clean(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return string.Empty;
            }
            return name.Replace(Field, ' ').Replace(Item, ' ').Replace('\n', ' ').Replace('\r', ' ').Trim();
        }

        private LifeStory Num(StringBuilder t, int value)
        {
            t.Append(value.ToString(CultureInfo.InvariantCulture)).Append(Field);
            return this;
        }

        private static void List(StringBuilder t, List<string> items)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (i > 0)
                {
                    t.Append(Item);
                }
                t.Append(Clean(items[i]));
            }
            t.Append(Field);
        }

        private static void Items(string text, List<string> into, int most)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }
            string[] parts = text.Split(Item);
            for (int i = 0; i < parts.Length; i++)
            {
                Keep(into, parts[i], most);
            }
        }

        private static int Int(string text)
        {
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : 0;
        }

        private static int Clamp(int value, int min, int max)
        {
            return value < min ? min : value > max ? max : value;
        }
    }
}
