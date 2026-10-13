using System;
using System.Collections.Generic;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The mod's lines in a save that must come out of it exactly as they went in.
    ///
    /// A line a feature cannot read (damaged, or written by a newer version of the mod that this one does not know)
    /// is reported here by the feature's store as it is read. The feature starts afresh for the session, and every
    /// time the game saves, once all the features have written, the line is put back as it was read
    /// (<see cref="PutBack"/>): it is never written over and never removed, so a later version of the mod finds it
    /// again. The same holds for the lines of a feature switched off this session after its faults (Core/Guard): what
    /// it last wrote stays.
    ///
    /// Kept per save: the lines belong to the save they were read from (the game's own dictionary of the mod's data),
    /// and a different save being read starts the list afresh. No Unity or game types: ContractCheck tests it.
    /// </summary>
    public static class SaveLines
    {
        private sealed class Line
        {
            public string Key;
            public string Text;
            public string Why;
        }

        private static readonly List<Line> Kept = new List<Line>(4);
        private static object _save;   // the dictionary the kept lines were read from

        /// <summary>Said once for each line kept (the key, why), for Player.log and the notice; set by Core/Guard.</summary>
        public static Action<string, string> Told;

        /// <summary>
        /// A store could not read its line (<paramref name="why"/> says how, in English): the line is kept as it is in
        /// this save. Safe with a missing or empty line (nothing to keep).
        /// </summary>
        public static void Unreadable(Dictionary<string, string> data, string key, string why)
        {
            if (data == null || key == null || !data.TryGetValue(key, out string text) || string.IsNullOrEmpty(text))
            {
                return;
            }
            Keep(data, key, text, why);
        }

        /// <summary>As <see cref="Unreadable"/>, with the text the store was given (a store that read it from elsewhere).</summary>
        public static void Keep(Dictionary<string, string> data, string key, string text, string why)
        {
            if (data == null || key == null || string.IsNullOrEmpty(text))
            {
                return;
            }
            if (!ReferenceEquals(data, _save))
            {
                Kept.Clear();
                _save = data;
            }
            foreach (Line line in Kept)
            {
                if (line.Key == key)
                {
                    return;   // (the first text read is the one kept)
                }
            }
            Kept.Add(new Line { Key = key, Text = text, Why = why });
            Told?.Invoke(key, why);
        }

        /// <summary>
        /// What a store read from its line, checked: nothing read from a line that is there (it could not be read), or a
        /// line of a later version than the store knows (what it holds of that version would be lost), keeps the line as
        /// it is. Returns what was read, so that a store can say <c>return SaveLines.Checked(data, Key, Read(text, out
        /// version), version, Version);</c>.
        /// </summary>
        public static T Checked<T>(Dictionary<string, string> data, string key, T read, int version, int known) where T : class
        {
            if (read == null)
            {
                Unreadable(data, key, "it could not be read");
            }
            else if (version > known)
            {
                Unreadable(data, key, "it was written by a later version of the mod (line version " + version + ", this one knows " + known + ")");
            }
            return read;
        }

        /// <summary>True when this save's line under the key is being kept as it was read.</summary>
        public static bool IsKept(Dictionary<string, string> data, string key)
        {
            if (data == null || !ReferenceEquals(data, _save))
            {
                return false;
            }
            foreach (Line line in Kept)
            {
                if (line.Key == key)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>The keys kept in this save.</summary>
        public static List<string> Keys(Dictionary<string, string> data)
        {
            var keys = new List<string>(Kept.Count);
            if (data != null && ReferenceEquals(data, _save))
            {
                foreach (Line line in Kept)
                {
                    keys.Add(line.Key);
                }
            }
            return keys;
        }

        /// <summary>
        /// Every kept line of this save back as it was read, whatever a feature wrote there since. Called by the save
        /// hook after the features have written and before the game writes the save. Returns how many were put back.
        /// </summary>
        public static int PutBack(Dictionary<string, string> data)
        {
            if (data == null || !ReferenceEquals(data, _save))
            {
                return 0;
            }
            int put = 0;
            foreach (Line line in Kept)
            {
                if (!data.TryGetValue(line.Key, out string now) || now != line.Text)
                {
                    data[line.Key] = line.Text;
                    put++;
                }
            }
            return put;
        }

        /// <summary>Lets go of the kept lines (the player removed the mod's data from this save with the save tools).</summary>
        public static void Forget()
        {
            Kept.Clear();
            _save = null;
        }
    }
}
