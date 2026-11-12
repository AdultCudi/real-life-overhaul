using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Population
{
    /// <summary>
    /// A buyer that keeps a business's staff holds them to it: for <see cref="Days"/> game days the people who worked there stay in their jobs. The
    /// population is where those people live on once the game has dropped them (a sale removes the game's EmployeeInstance, EmployeeInstance.RemoveEmployee),
    /// so the hold is theirs: nobody held quits (LabourRules.Day), is shed with the employer's slots (LabourRules.Shed) or is let go when the staff
    /// link ends (PopulationLinks.Staff). A person who dies, retires or leaves the city is still gone. Game-free.
    /// COST: nothing per frame; a day's pass over the few holds, a lookup per worker in the day's slices.
    /// </summary>
    public static class HoldRules
    {
        public const int Days = 30;
        public const string Key = "reallife:stafftold";
        private const string Head = "v=1";

        /// <summary>True while the person is held to their job.</summary>
        public static bool Held(PopulationState s, int p) => s.Holds.Count > 0 && s.Holds.ContainsKey(s.People.Id[p]);

        /// <summary>Holds one person from the day for <see cref="Days"/> days: a worker only, once. False if they are not one.</summary>
        public static bool Start(PopulationState s, int p, int day)
        {
            if (p < 0 || p >= s.People.Count || s.People.Id[p] == 0 || s.People.Status[p] != (byte)Status.Worker || s.People.Job[p] < 0)
            {
                return false;
            }
            s.Holds[s.People.Id[p]] = day + Days;
            return true;
        }

        /// <summary>Holds the residents of the city who work for an employer (not the player's linked staff, whose system holds them); returns how many.</summary>
        public static int StartAt(PopulationState s, int employer, int day)
        {
            int n = 0;
            for (int p = 0; employer >= 0 && p < s.People.Count; p++)
            {
                if (s.People.Id[p] != 0 && s.People.Job[p] == employer && (s.People.Flags[p] & (PersonFlags.Linked | PersonFlags.Frozen)) == 0 && Start(s, p, day))
                {
                    n++;
                }
            }
            return n;
        }

        /// <summary>Ends the holds whose days are past: on the day it falls due the person is as free as anyone. Returns how many ended.</summary>
        public static int Expire(PopulationState s, int day)
        {
            if (s.Holds.Count == 0)
            {
                return 0;
            }
            var done = new List<int>(4);
            foreach (KeyValuePair<int, int> h in s.Holds)
            {
                if (day >= h.Value)
                {
                    done.Add(h.Key);
                }
            }
            for (int i = 0; i < done.Count; i++)
            {
                s.Holds.Remove(done[i]);
            }
            return done.Count;
        }

        /// <summary>The holds as a save line: the city they belong to (its seed and day generated), then each person and end day. Null with none.</summary>
        public static string Write(PopulationState s)
        {
            if (s.Holds.Count == 0)
            {
                return null;
            }
            var b = new StringBuilder(Head).Append('~').Append(s.Seed.ToString(CultureInfo.InvariantCulture)).Append('~').Append(s.Generated.ToString(CultureInfo.InvariantCulture)).Append('~');
            bool first = true;
            foreach (KeyValuePair<int, int> h in s.Holds)
            {
                b.Append(first ? "" : ",").Append(h.Key.ToString(CultureInfo.InvariantCulture)).Append(':').Append(h.Value.ToString(CultureInfo.InvariantCulture));
                first = false;
            }
            return b.ToString();
        }

        /// <summary>Puts a saved line's holds back, only if the line is of this very city (people's ids mean nothing in another). Returns how many.</summary>
        public static int Read(PopulationState s, string line)
        {
            s.Holds.Clear();
            string[] part = string.IsNullOrEmpty(line) ? null : line.Split('~');
            if (part == null || part.Length != 4 || part[0] != Head || part[1] != s.Seed.ToString(CultureInfo.InvariantCulture)
                || part[2] != s.Generated.ToString(CultureInfo.InvariantCulture))
            {
                return 0;
            }
            string[] each = part[3].Split(',');
            for (int i = 0; i < each.Length; i++)
            {
                string[] pair = each[i].Split(':');
                if (pair.Length == 2 && int.TryParse(pair[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int id)
                    && int.TryParse(pair[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int until) && id > 0)
                {
                    s.Holds[id] = until;
                }
            }
            return s.Holds.Count;
        }
    }
}
