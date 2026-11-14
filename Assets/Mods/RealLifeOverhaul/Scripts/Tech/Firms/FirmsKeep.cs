using System;
using System.Collections.Generic;
using System.Text;

namespace RealLifeOverhaul.Tech
{
    /// <summary>What the game says about the building a firm's record stands at, as the firms part reads it.</summary>
    public struct PlaceReading
    {
        public bool Registered;      // the game's list holds a registration at the place
        public bool BuildingFound;   // ...and the city has the building it names
        public bool Rented;          // the registration is rented by a player
        public bool Foreign;         // ...and in a Going Public session by another player
        public bool Established;     // a business is set up there
        public int Kind;             // the firm kind of its business type, -1 for a type that is no tech firm's
    }

    /// <summary>Whether a firm's record may go: only when the game shows its business is gone, never because it was not read.</summary>
    public enum PlaceFate { Stands, Gone, Unsure }

    /// <summary>
    /// A firm's record, its products and its work were closed the moment its business was not among the registrations read, which
    /// happens whenever the game's list is short for a session (a registration not yet back, a building the city has not made, a
    /// part of the mod standing down). Here a record goes only on evidence, and a copy of the lines it lives in is kept in the
    /// save beside it. Game-free.
    /// </summary>
    public static class FirmsKeep
    {
        /// <summary>The backup's line in the save: the firms', the markets' and the studio's lines as they were when the save was read.</summary>
        public const string BackupKey = "reallife:tech_backup";

        /// <summary>Days a richer copy outlives a poorer save, so that a loss noticed within them can be put right.</summary>
        public const int BackupDays = 45;

        public static PlaceFate Fate(in PlaceReading r)
        {
            if (!r.Registered || !r.BuildingFound)
            {
                return PlaceFate.Unsure;
            }
            if (!r.Rented)
            {
                return PlaceFate.Gone;
            }
            if (r.Foreign)
            {
                return PlaceFate.Unsure;
            }
            if (r.Kind < 0)
            {
                return PlaceFate.Gone;
            }
            return r.Established ? PlaceFate.Stands : PlaceFate.Unsure;
        }

        /// <summary>
        /// The firms no business was read for: each is closed with its products and work only when the game shows its business is gone, else it is
        /// put in <paramref name="held"/> and left as it is. <paramref name="said"/> gets each decision (place, fate, products closed with it).
        /// </summary>
        public static int Sweep(FirmsFile f, int day, Func<string, PlaceReading> read, ISet<string> held, Action<string, PlaceFate, int> said)
        {
            int closed = 0;
            held.Clear();
            for (int i = f.Firms.Count - 1; i >= 0; i--)
            {
                Firm firm = f.Firms[i];
                if (firm.Seen)
                {
                    continue;
                }
                PlaceFate fate = Fate(read(firm.Place));
                if (fate != PlaceFate.Gone)
                {
                    held.Add(firm.Place);
                    said?.Invoke(firm.Place, fate, 0);
                    continue;
                }
                string place = firm.Place;
                int products = FirmsRules.Close(f, firm, day);
                closed++;
                said?.Invoke(place, fate, products);
            }
            return closed;
        }

        /// <summary>The weight of a firms' line: firms first, then what stands on sale, then every product. -1 for a line that cannot be read.</summary>
        public static long Score(string firmsLine)
        {
            if (string.IsNullOrEmpty(firmsLine))
            {
                return 0;
            }
            try
            {
                FirmsFile f = FirmsStore.Read(firmsLine, out _);
                long live = 0;
                for (int i = 0; i < f.Products.Count; i++)
                {
                    live += f.Products[i].Stage == 1 ? 1 : 0;
                }
                return f.Firms.Count * 100000L + live * 100 + f.Products.Count;
            }
            catch (Exception)
            {
                return -1;
            }
        }

        public static string[] BackedKeys => new[] { FirmsStore.Key, SectorStore.Key, StudioStore.Key };

        /// <summary>The lines the backup would hold, as the save holds them now.</summary>
        public static Dictionary<string, string> Lines(Dictionary<string, string> data)
        {
            var lines = new Dictionary<string, string>(3, StringComparer.Ordinal);
            string[] keys = BackedKeys;
            for (int i = 0; data != null && i < keys.Length; i++)
            {
                if (data.TryGetValue(keys[i], out string text) && !string.IsNullOrEmpty(text))
                {
                    lines[keys[i]] = text;
                }
            }
            return lines;
        }

        /// <summary>The backup's line for lines as loaded: day, then each key and its text escaped, one to a row.</summary>
        public static string WriteBackup(int day, IDictionary<string, string> lines)
        {
            var b = new StringBuilder(4096);
            b.Append("b1\n").Append(day.ToString(System.Globalization.CultureInfo.InvariantCulture));
            string[] keys = BackedKeys;
            for (int i = 0; i < keys.Length; i++)
            {
                if (lines != null && lines.TryGetValue(keys[i], out string text) && !string.IsNullOrEmpty(text))
                {
                    b.Append('\n').Append(keys[i]).Append('\n').Append(text.Replace("\\", "\\\\").Replace("\n", "\\n").Replace("\r", "\\r"));
                }
            }
            return b.ToString();
        }

        /// <summary>A backup read back: its day and its lines; null when the line is not one.</summary>
        public static Dictionary<string, string> ReadBackup(string line, out int day)
        {
            day = 0;
            if (string.IsNullOrEmpty(line))
            {
                return null;
            }
            string[] rows = line.Split('\n');
            if (rows.Length < 2 || rows[0] != "b1" || !int.TryParse(rows[1], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out day))
            {
                return null;
            }
            var lines = new Dictionary<string, string>(3, StringComparer.Ordinal);
            for (int i = 2; i + 1 < rows.Length; i += 2)
            {
                lines[rows[i]] = Unescape(rows[i + 1]);
            }
            return lines;
        }

        private static string Unescape(string s)
        {
            if (s.IndexOf('\\') < 0)
            {
                return s;
            }
            var b = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '\\' && i + 1 < s.Length)
                {
                    char n = s[++i];
                    b.Append(n == 'n' ? '\n' : n == 'r' ? '\r' : n);
                }
                else
                {
                    b.Append(c);
                }
            }
            return b.ToString();
        }

        /// <summary>
        /// The backup the save should now hold: the lines as loaded replace it when they are as rich as it, or when it has aged past
        /// <see cref="BackupDays"/>; a poorer save never writes over a richer copy before then. Null: leave the save's line as it is.
        /// </summary>
        public static string Refresh(string held, IDictionary<string, string> loaded, int today)
        {
            if (loaded == null || !loaded.TryGetValue(FirmsStore.Key, out string firms) || string.IsNullOrEmpty(firms))
            {
                return null;
            }
            long now = Score(firms);
            if (now < 0)
            {
                return null;
            }
            Dictionary<string, string> kept = ReadBackup(held, out int keptDay);
            long was = kept != null && kept.TryGetValue(FirmsStore.Key, out string old) ? Score(old) : -1;
            if (kept == null || was < 0 || now >= was || today - keptDay > BackupDays)
            {
                return WriteBackup(today, loaded);
            }
            return null;
        }
    }
}
