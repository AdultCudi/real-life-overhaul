using System;
using System.Collections.Generic;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The mod's lines in a save that must come out of it exactly as they went in. A line a store cannot read (damaged,
    /// or a newer version's) is reported here; the feature starts afresh, and at every save the line is put back as it
    /// was read (<see cref="PutBack"/>), so a later version of the mod finds it again. Kept per save: another save's
    /// dictionary starts the list afresh. Game-free.
    /// </summary>
    public static class SaveLines
    {
        private sealed class Line
        {
            public string Key;
            public string Text;
        }

        private static readonly List<Line> Kept = new List<Line>(4);
        private static object _save;   // the dictionary the kept lines were read from

        /// <summary>Said once for each line kept (the key, why), for the log and the notice; set by Core/Guard.</summary>
        public static Action<string, string> Told;

        /// <summary>
        /// A store could not read its line (<paramref name="why"/>, in English): it is kept as it is in this save. Safe
        /// with a missing or empty line.
        /// </summary>
        public static void Unreadable(Dictionary<string, string> data, string key, string why)
        {
            if (data == null || key == null || !data.TryGetValue(key, out string text) || string.IsNullOrEmpty(text))
            {
                return;
            }
            Keep(data, key, text, why);
        }

        /// <summary>As <see cref="Unreadable"/>, with the text the store was given (read from elsewhere).</summary>
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
                    return;   // the first text read is the one kept
                }
            }
            Kept.Add(new Line { Key = key, Text = text });
            Told?.Invoke(key, why);
        }

        /// <summary>Said once for a line of a later version than its store knows, which is read as far as it can be (key, versions).</summary>
        public static Action<string, string> Later;

        /// <summary>
        /// Checks what a store read: null keeps the line as it is; a later version's line is read as far as it can be and
        /// only said. Returns what was read: <c>return SaveLines.Checked(data, Key, Read(text, out version), version, Version);</c>
        /// </summary>
        public static T Checked<T>(Dictionary<string, string> data, string key, T read, int version, int known) where T : class
        {
            if (read == null)
            {
                Unreadable(data, key, "it could not be read");
            }
            else if (version > known)
            {
                Later?.Invoke(key, "line version " + version + ", this one knows " + known);
            }
            return read;
        }

        /// <summary>True when this save's line under the key is kept as it was read.</summary>
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

        /// <summary>
        /// The feature replaced what a kept line held (the player chose afresh): the line is let go and the feature's own
        /// is saved from now on. Safe when nothing is kept.
        /// </summary>
        public static void Release(Dictionary<string, string> data, string key)
        {
            if (data == null || !ReferenceEquals(data, _save))
            {
                return;
            }
            for (int i = Kept.Count - 1; i >= 0; i--)
            {
                if (Kept[i].Key == key)
                {
                    Kept.RemoveAt(i);
                }
            }
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
        /// Puts every kept line back as it was read, after the features have written and before the game writes the
        /// save; returns how many.
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
