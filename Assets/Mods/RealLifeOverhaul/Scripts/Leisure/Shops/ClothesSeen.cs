using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Leisure
{
    /// <summary>
    /// The garments the clothes shop has shown the player, so that its New filter lists the ones the shop got since the player last looked
    /// (a game update, a DLC or a new version of the mod adds some). A category (a kind of garment) counts only once the player has seen
    /// it: its first look makes nothing new, so no one is told their whole rack is new. A garment is seen when the shop screen closes
    /// after it was shown. Saved as "reallife:clothes_seen": "v=1", then '^' and one id each, at most <see cref="Most"/>, the
    /// oldest kept; a save without the line sees nothing as new. Game-free; a set lookup when the grid is read, never per frame.
    /// </summary>
    public static class ClothesSeen
    {
        public const string Key = "reallife:clothes_seen";
        public const int Version = 1;
        public const int Most = 1500;   // ids kept: a few hundred garments, a category marker each

        private const string Category = "~";   // an id that starts with it marks a category as seen
        private static readonly HashSet<string> Seen = new HashSet<string>(StringComparer.Ordinal);
        private static readonly List<string> Order = new List<string>(64);   // seen, oldest first: what is dropped when the cap is reached
        private static readonly HashSet<string> Looked = new HashSet<string>(StringComparer.Ordinal);   // shown since the screen opened

        /// <summary>True when the garment is new: its category was seen before, and it was not shown in any look since.</summary>
        public static bool IsNew(string category, string id) => !string.IsNullOrEmpty(id) && Seen.Contains(Category + category) && !Seen.Contains(category + "/" + id);

        /// <summary>The garment was shown to the player in its category on this opening of the shop.</summary>
        public static void Show(string category, string id)
        {
            if (!string.IsNullOrEmpty(category) && !string.IsNullOrEmpty(id) && Looked.Count < Most)
            {
                Looked.Add(category + "/" + id);
                Looked.Add(Category + category);
            }
        }

        /// <summary>The shop closed: everything it showed is seen from now on. Called once a closing.</summary>
        public static void Commit()
        {
            foreach (string id in Looked)
            {
                if (Seen.Add(id))
                {
                    Order.Add(id);
                }
            }
            Looked.Clear();
            while (Order.Count > Most)
            {
                Seen.Remove(Order[0]);   // the oldest go: a garment dropped here is simply new again
                Order.RemoveAt(0);
            }
        }

        /// <summary>No save: nothing is seen.</summary>
        public static void Reset()
        {
            Seen.Clear();
            Order.Clear();
            Looked.Clear();
        }

        /// <summary>The ids seen, as a line that a state always writes the same way.</summary>
        public static string Write()
        {
            var t = new StringBuilder(256);
            t.Append("v=").Append(Version.ToString(CultureInfo.InvariantCulture));
            for (int i = 0; i < Order.Count; i++)
            {
                t.Append('^').Append(Order[i].Replace("^", string.Empty));
            }
            return t.ToString();
        }

        /// <summary>Reads a line into the state (replacing it); an empty one for no line. False for another version.</summary>
        public static bool Read(string text)
        {
            Reset();
            if (string.IsNullOrEmpty(text))
            {
                return true;
            }
            string[] records = text.Split('^');
            if (records[0] != "v=" + Version.ToString(CultureInfo.InvariantCulture))
            {
                return false;
            }
            for (int i = 1; i < records.Length && Order.Count < Most; i++)
            {
                if (records[i].Length > 0 && Seen.Add(records[i]))
                {
                    Order.Add(records[i]);
                }
            }
            return true;
        }

        /// <summary>From the mod's load of a save: the line read; a line this version cannot read is kept as it was and nothing is seen.</summary>
        public static void ReadSave(Dictionary<string, string> data)
        {
            data.TryGetValue(Key, out string text);
            if (!Read(text))
            {
                SaveLines.Unreadable(data, Key, "its version is not one this version of the mod reads");
            }
        }

        /// <summary>At a save: the ids seen written, unless the line was kept as it was read.</summary>
        public static void WriteSave(Dictionary<string, string> data)
        {
            if (!SaveLines.IsKept(data, Key))
            {
                data[Key] = Write();
            }
        }
    }
}
