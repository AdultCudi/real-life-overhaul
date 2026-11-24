using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The game day each scooter rented from the game's stands was first seen, kept in the save's modData so that saving and loading
    /// cannot stretch a rental past its midnight. The game itself keeps nothing of the kind on a vehicle (its lastSeen is the
    /// away-clock, set and cleared by sight). A scooter with no line yet, one rented before this was kept or in a save from an earlier
    /// version, takes the day it is first seen, so nobody loses one on updating. Game-free, so that EconomyCheck runs this very store.
    /// </summary>
    public static class ScooterRentals
    {
        public const string Key = "reallife:garage_scooter_days";

        /// <summary>Vehicle id to the day the rental began.</summary>
        public static readonly Dictionary<string, int> Days = new Dictionary<string, int>();

        public static void Clear()
        {
            Days.Clear();
        }

        /// <summary>The day the rental began; a scooter not met before begins today and is remembered. One lookup, no allocation once known.</summary>
        public static int DayOf(string id, int today)
        {
            if (!Days.TryGetValue(id, out int day))
            {
                Days[id] = today;
                return today;
            }
            return day;
        }

        public static void Forget(string id)
        {
            Days.Remove(id);
        }

        /// <summary>Reads the saved lines ("id|day", one a line); a line that cannot be read is left out and its scooter begins again when seen.</summary>
        public static void Read(string text)
        {
            Days.Clear();
            if (string.IsNullOrEmpty(text))
            {
                return;
            }
            foreach (string line in text.Split('\n'))
            {
                int at = line.LastIndexOf('|');
                if (at > 0 && int.TryParse(line.Substring(at + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int day))
                {
                    Days[line.Substring(0, at)] = day;
                }
            }
        }

        /// <summary>The lines to save. Only the ids still in <paramref name="present"/> are kept (a scooter the game took away is forgotten); null keeps all.</summary>
        public static string Write(ICollection<string> present)
        {
            var text = new StringBuilder(64);
            var gone = new List<string>();
            foreach (KeyValuePair<string, int> pair in Days)
            {
                if (present != null && !present.Contains(pair.Key))
                {
                    gone.Add(pair.Key);
                    continue;
                }
                if (text.Length > 0)
                {
                    text.Append('\n');
                }
                text.Append(pair.Key).Append('|').Append(pair.Value.ToString(CultureInfo.InvariantCulture));
            }
            for (int i = 0; i < gone.Count; i++)
            {
                Days.Remove(gone[i]);
            }
            return text.ToString();
        }
    }
}
