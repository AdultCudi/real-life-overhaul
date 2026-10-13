using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Interiors
{
    /// <summary>
    /// What the feature keeps in the save: one file per business of the player's it has seen, at most
    /// <see cref="Most"/>. Its only line ("reallife:interiors"): "1|place,wear,interior,day,story,refresh|...". A place is
    /// business pressure's own key (the street escaped, so it holds no '|' and no ','). A line that cannot be read gives
    /// an empty store; a file that cannot be read is left out. Game-free, checked by EconomyCheck.
    /// </summary>
    public sealed class InteriorStore
    {
        public const string Key = "reallife:interiors";
        public const int Most = 200;
        private const string Version = "1";

        public readonly List<InteriorFile> Files = new List<InteriorFile>(16);

        /// <summary>The file of a place; made (if there is room) when asked to.</summary>
        public InteriorFile File(string place, bool make)
        {
            if (string.IsNullOrEmpty(place))
            {
                return null;
            }
            for (int i = 0; i < Files.Count; i++)
            {
                if (string.Equals(Files[i].Place, place, StringComparison.Ordinal))
                {
                    return Files[i];
                }
            }
            if (!make || Files.Count >= Most || place.IndexOf('|') >= 0 || place.IndexOf(',') >= 0)
            {
                return null;
            }
            var f = new InteriorFile { Place = place };
            Files.Add(f);
            return f;
        }

        /// <summary>The files of places that are no longer the player's are dropped.</summary>
        public int Prune(Predicate<string> stillOwned)
        {
            return Files.RemoveAll(f => !stillOwned(f.Place));
        }

        public void Clear()
        {
            Files.Clear();
        }

        public string Write()
        {
            var w = new StringBuilder(8 + Files.Count * 48);
            w.Append(Version);
            for (int i = 0; i < Files.Count; i++)
            {
                InteriorFile f = Files[i];
                w.Append('|').Append(f.Place)
                 .Append(',').Append(f.Wear.ToString(CultureInfo.InvariantCulture))
                 .Append(',').Append(f.LastInterior.ToString(CultureInfo.InvariantCulture))
                 .Append(',').Append(f.LastDay.ToString(CultureInfo.InvariantCulture))
                 .Append(',').Append(f.StoryDay.ToString(CultureInfo.InvariantCulture))
                 .Append(',').Append(f.RefreshDay.ToString(CultureInfo.InvariantCulture));
            }
            return w.ToString();
        }

        public static InteriorStore Read(string line)
        {
            var store = new InteriorStore();
            if (string.IsNullOrEmpty(line))
            {
                return store;
            }
            string[] parts = line.Split('|');
            if (parts[0] != Version)
            {
                return store;
            }
            for (int i = 1; i < parts.Length; i++)
            {
                string[] f = parts[i].Split(',');
                int wear, interior, day, story, refresh;
                if (f.Length != 6 || f[0].Length == 0 || !Int(f[1], out wear) || !Int(f[2], out interior) || !Int(f[3], out day) || !Int(f[4], out story)
                    || !Int(f[5], out refresh))
                {
                    continue;
                }
                InteriorFile file = store.File(f[0], true);
                if (file == null)
                {
                    continue;
                }
                file.Wear = InteriorRules.Clamp(wear, 0, InteriorRules.WearMost);
                file.LastInterior = InteriorRules.Clamp(interior, -1, 100);
                file.LastDay = day;
                file.StoryDay = story;
                file.RefreshDay = refresh;
            }
            return store;
        }

        private static bool Int(string s, out int value)
        {
            return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }
    }
}
