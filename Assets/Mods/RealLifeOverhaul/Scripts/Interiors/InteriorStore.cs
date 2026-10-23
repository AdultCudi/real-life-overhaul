using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Interiors
{
    /// <summary>
    /// What the feature keeps in the save: a file for each business seen, at most <see cref="Most"/>, in one line
    /// ("2|place,wear,interior,day,story,refresh,first|..."; a version 1 line, without the first day, reads as businesses
    /// trading already), and a line of its own for the day the rules first read the save ("1,day"), so an older version
    /// of the mod keeps the files. A refit's new fittings have a third line ("1|place,points,day|..."), apart for the same reason:
    /// the files' line keeps its count of fields. A line this version cannot read is kept as read (Core/SaveLines). Game-free.
    /// </summary>
    public sealed class InteriorStore
    {
        public const string Key = "reallife:interiors";
        public const string RulesKey = "reallife:interiors_rules";
        public const string RefitKey = "reallife:interiors_refits";
        public const int Most = 200;
        private const string Version = "2";
        private const string VersionOne = "1";   // no first day: read as businesses that were trading already
        private const string RulesVersion = "1";
        private const string RefitVersion = "1";

        public readonly List<InteriorFile> Files = new List<InteriorFile>(16);

        /// <summary>The day these rules first read the save (<see cref="InteriorFile.Unset"/> until they have).</summary>
        public int RulesFrom = InteriorFile.Unset;

        /// <summary>The day a business's effects began to come in: its own first day, or the rules' first day if it traded before.</summary>
        public int PhaseFrom(InteriorFile f)
        {
            if (RulesFrom == InteriorFile.Unset)
            {
                return InteriorFile.Unset;
            }
            return f == null || f.FirstDay == InteriorFile.Unset || f.FirstDay == InteriorFile.LongAgo ? RulesFrom : Math.Max(RulesFrom, f.FirstDay);
        }

        /// <summary>The rules' line ("1,day"), or null while they have not read the save.</summary>
        public string WriteRules()
        {
            return RulesFrom == InteriorFile.Unset ? null : RulesVersion + "," + RulesFrom.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>The rules' line read back; one that cannot be read leaves the day unset (the rules meet the save again that day).</summary>
        public void ReadRules(string line)
        {
            RulesFrom = InteriorFile.Unset;
            if (string.IsNullOrEmpty(line))
            {
                return;
            }
            string[] f = line.Split(',');
            int day;
            if (f.Length == 2 && f[0] == RulesVersion && Int(f[1], out day) && day != InteriorFile.Unset && day != InteriorFile.LongAgo)
            {
                RulesFrom = day;
            }
        }

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
                 .Append(',').Append(f.RefreshDay.ToString(CultureInfo.InvariantCulture))
                 .Append(',').Append(f.FirstDay.ToString(CultureInfo.InvariantCulture));
            }
            return w.ToString();
        }

        /// <summary>The refits' line: the files with a refit's fittings, or null while none has.</summary>
        public string WriteRefits()
        {
            StringBuilder w = null;
            for (int i = 0; i < Files.Count; i++)
            {
                InteriorFile f = Files[i];
                if (f.Refit <= 0)
                {
                    continue;
                }
                w = w ?? new StringBuilder(8 + Files.Count * 24).Append(RefitVersion);
                w.Append('|').Append(f.Place).Append(',').Append(f.Refit.ToString(CultureInfo.InvariantCulture)).Append(',').Append(f.RefitDay.ToString(CultureInfo.InvariantCulture));
            }
            return w?.ToString();
        }

        /// <summary>The refits' line read into the files (made where missing); false for a line this version cannot read.</summary>
        public bool ReadRefits(string line)
        {
            if (string.IsNullOrEmpty(line))
            {
                return true;
            }
            string[] parts = line.Split('|');
            if (parts[0] != RefitVersion)
            {
                return false;
            }
            for (int i = 1; i < parts.Length; i++)
            {
                string[] f = parts[i].Split(',');
                int points, day;
                if (f.Length < 3 || f[0].Length == 0 || !Int(f[1], out points) || !Int(f[2], out day))
                {
                    continue;
                }
                InteriorFile file = File(f[0], true);
                if (file != null)
                {
                    file.Refit = InteriorRules.Clamp(points, 0, InteriorRules.RefitMost);
                    file.RefitDay = day;
                }
            }
            return true;
        }

        /// <summary>The lines of a save read, any may be missing; one this version cannot read gives an empty store, unset rules or no refits.</summary>
        public static InteriorStore Load(Dictionary<string, string> data)
        {
            string line = null, rules = null, refits = null;
            if (data != null)
            {
                data.TryGetValue(Key, out line);
                data.TryGetValue(RulesKey, out rules);
                data.TryGetValue(RefitKey, out refits);
            }
            InteriorStore store = Read(line);
            if (!Readable(line))
            {
                SaveLines.Unreadable(data, Key, "its version is not one this version of the mod reads");
            }
            store.ReadRules(rules);
            if (!string.IsNullOrEmpty(rules) && store.RulesFrom == InteriorFile.Unset)
            {
                SaveLines.Unreadable(data, RulesKey, "it could not be read");
            }
            if (!store.ReadRefits(refits))
            {
                SaveLines.Unreadable(data, RefitKey, "its version is not one this version of the mod reads");
            }
            return store;
        }

        /// <summary>True for a missing or empty line, and for one of the two versions this version of the mod reads.</summary>
        public static bool Readable(string line)
        {
            if (string.IsNullOrEmpty(line))
            {
                return true;
            }
            int bar = line.IndexOf('|');
            string version = bar >= 0 ? line.Substring(0, bar) : line;
            return version == Version || version == VersionOne;
        }

        public static InteriorStore Read(string line)
        {
            var store = new InteriorStore();
            if (string.IsNullOrEmpty(line))
            {
                return store;
            }
            string[] parts = line.Split('|');
            bool one = parts[0] == VersionOne;
            if (parts[0] != Version && !one)
            {
                return store;
            }
            int fields = one ? 6 : 7;
            for (int i = 1; i < parts.Length; i++)
            {
                string[] f = parts[i].Split(',');
                int wear, interior, day, story, refresh, first = InteriorFile.LongAgo;
                if (f.Length != fields || f[0].Length == 0 || !Int(f[1], out wear) || !Int(f[2], out interior) || !Int(f[3], out day) || !Int(f[4], out story)
                    || !Int(f[5], out refresh) || (!one && !Int(f[6], out first)))
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
                file.FirstDay = first;
            }
            return store;
        }

        private static bool Int(string s, out int value)
        {
            return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }
    }
}
