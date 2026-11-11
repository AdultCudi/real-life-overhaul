using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.RivalsSeen
{
    /// <summary>
    /// The rivals seen around the city in the save: one line of modData under <see cref="Key"/>, game-free.
    ///
    ///     v=1^h:seed|day|hour|player day^r:record;record^n:note;note
    ///
    /// Parts are divided by '^' and begin with a letter and a colon; records inside a part by ';', fields by '|'. h is the header, r what is kept of each rival (key,
    /// name, figure, before, shops, shops before, mark day, form, tier, rung, car, car day, known, greet day, greets), n the sightings (day, hour, kind, spot, form,
    /// posted, key, name, firm, place, district, car, old, test). A record's sixteenth field (an older line's day the player last stood near them) is no longer
    /// kept: it is read past, and a later field must come after it. Words are escaped, so a name may hold any character. A newer version adds fields at the end of a
    /// record or a part with a new letter: this reader takes what it knows, drops a sighting of a kind it does not know and puts every figure inside its bounds, a
    /// day after the line's own day included. A save from before the feature has no line and loads with nothing in it. Every number is in the invariant culture.
    /// </summary>
    public static class SeenStore
    {
        public const string Key = "reallife:rivals_seen";
        public const string TestKey = "reallife:rivals_seen_test";
        public const int Version = 1;

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static SeenFile Load(Dictionary<string, string> data, out int version)
        {
            version = 0;
            if (data == null || !data.TryGetValue(Key, out string text) || string.IsNullOrEmpty(text))
            {
                return null;
            }
            return SaveLines.Checked(data, Key, Read(text, out version), version, Version);   // (a line that cannot be read is kept as it is: Core/SaveLines)
        }

        public static string Write(SeenFile f)
        {
            var t = new StringBuilder(128 + f.Records.Count * 96 + f.Notes.Count * 128);
            t.Append("v=").Append(Version.ToString(Inv)).Append("^h:");
            Join(t, f.Seed, f.Day, f.Hour, f.PlayerDay);
            if (f.Records.Count > 0)
            {
                t.Append("^r:");
                for (int i = 0; i < f.Records.Count; i++)
                {
                    SeenRecord r = f.Records[i];
                    t.Append(i > 0 ? ";" : string.Empty).Append(Esc(r.Key)).Append('|').Append(Esc(r.Name)).Append('|');
                    t.Append(r.Figure.ToString(Inv)).Append('|').Append(r.Before.ToString(Inv)).Append('|');
                    Join(t, r.Shops, r.ShopsBefore, r.MarkDay, (int)r.Form, r.Tier, r.Rung);
                    t.Append('|').Append(Esc(r.Car)).Append('|');
                    Join(t, r.CarDay, r.Known, r.GreetDay, r.Greets);
                }
            }
            if (f.Notes.Count > 0)
            {
                t.Append("^n:");
                for (int i = 0; i < f.Notes.Count; i++)
                {
                    SeenNote n = f.Notes[i];
                    t.Append(i > 0 ? ";" : string.Empty);
                    Join(t, n.Day, n.Hour, (int)n.Kind, (int)n.Spot, (int)n.Form, n.Posted ? 1 : 0);
                    t.Append('|').Append(Esc(n.Key)).Append('|').Append(Esc(n.Name)).Append('|').Append(Esc(n.Firm)).Append('|').Append(Esc(n.Place)).Append('|')
                        .Append(Esc(n.District)).Append('|').Append(Esc(n.Car)).Append('|').Append(Esc(n.Old)).Append('|').Append(n.Test ? '1' : '0');
                }
            }
            return t.ToString();
        }

        /// <summary>Reads a line. Null for one that is not this feature's at all.</summary>
        public static SeenFile Read(string text, out int version)
        {
            version = 0;
            if (string.IsNullOrEmpty(text) || !text.StartsWith("v=", StringComparison.Ordinal))
            {
                return null;
            }
            string[] parts = text.Split('^');
            if (!int.TryParse(parts[0].Substring(2), NumberStyles.Integer, Inv, out version))
            {
                return null;
            }
            var f = new SeenFile();
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
                    case 'h': Header(f, body); break;
                    case 'r': Records(f, body); break;
                    case 'n': Notes(f, body); break;
                }
            }
            Bound(f);
            return f;
        }

        // No day of the line is past the line's own day: a damaged or carried-over figure would hold the player's gap or a car for days that never came.
        private static void Bound(SeenFile f)
        {
            f.PlayerDay = Math.Min(f.PlayerDay, Math.Max(f.Day, -1000));
            if (f.Day < 0)
            {
                return;
            }
            SeenRules.Settle(f, f.Day);
        }

        private static void Header(SeenFile f, string body)
        {
            string[] x = body.Split('|');
            f.Seed = I(x, 0, 0);
            f.Day = Math.Max(-1, I(x, 1, -1));
            f.Hour = SeenCatalogue.Clamp(I(x, 2, -1), -1, 23);
            f.PlayerDay = Math.Max(-1000, I(x, 3, -1000));
        }

        private static void Records(SeenFile f, string body)
        {
            foreach (string record in body.Split(';'))
            {
                string[] x = record.Split('|');
                string key = Un(x, 0);
                if (key.Length == 0 || f.Of(key) != null)
                {
                    continue;
                }
                f.Records.Add(new SeenRecord
                {
                    Key = key, Name = Un(x, 1), Figure = Math.Max(0L, L(x, 2)), Before = Math.Max(0L, L(x, 3)), Shops = Math.Max(0, I(x, 4, 0)), ShopsBefore = Math.Max(-1, I(x, 5, -1)),
                    MarkDay = Math.Max(-1, I(x, 6, -1)), Form = (SeenForm)SeenCatalogue.Clamp(I(x, 7, (int)SeenForm.Steady), 0, (int)SeenForm.Count - 1),
                    Tier = SeenCatalogue.Clamp(I(x, 8, 0), 0, SeenCatalogue.Tiers - 1), Rung = SeenCatalogue.Clamp(I(x, 9, -1), -1, SeenCatalogue.Rungs - 1), Car = Un(x, 10),
                    CarDay = Math.Max(-1, I(x, 11, -1)), Known = SeenCatalogue.Clamp(I(x, 12, 0), 0, SeenCatalogue.GreetWarmMost * 10), GreetDay = Math.Max(-1, I(x, 13, -1)),
                    Greets = Math.Max(0, I(x, 14, 0))
                });
            }
        }

        private static void Notes(SeenFile f, string body)
        {
            int tests = 0;
            foreach (string record in body.Split(';'))
            {
                string[] x = record.Split('|');
                int kind = I(x, 2, -1), spot = I(x, 3, -1), form = I(x, 4, -1);
                bool test = I(x, 13, 0) == 1;
                if (x.Length < 6 || kind < 0 || kind >= (int)SeenNoteKind.Count || spot < 0 || spot >= (int)SeenSpot.Count || form < 0 || form >= (int)SeenForm.Count)
                {
                    continue;   // a kind, a place or a form a newer version added: this one has no line for it
                }
                if (test ? tests >= SeenCatalogue.TestNotesKept : f.Notes.Count - tests >= SeenCatalogue.NotesKept)
                {
                    continue;   // past the bound a damaged line can hold
                }
                tests += test ? 1 : 0;
                f.Notes.Add(new SeenNote
                {
                    Day = I(x, 0, 0), Hour = SeenCatalogue.Clamp(I(x, 1, 0), 0, 23), Kind = (SeenNoteKind)kind, Spot = (SeenSpot)spot, Form = (SeenForm)form, Posted = I(x, 5, 0) != 0,
                    Key = Un(x, 6), Name = Un(x, 7), Firm = Un(x, 8), Place = Un(x, 9), District = Un(x, 10), Car = Un(x, 11), Old = Un(x, 12), Test = test
                });
            }
        }

        private static void Join(StringBuilder t, params int[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                t.Append(i > 0 ? "|" : string.Empty).Append(values[i].ToString(Inv));
            }
        }

        private static string Esc(string s) => Uri.EscapeDataString(s ?? string.Empty);

        private static string Un(string[] x, int i)
        {
            if (i >= x.Length || x[i].Length == 0)
            {
                return string.Empty;
            }
            try
            {
                return Uri.UnescapeDataString(x[i]);
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        private static int I(string[] x, int i, int otherwise)
        {
            return i < x.Length && int.TryParse(x[i], NumberStyles.Integer, Inv, out int v) ? v : otherwise;
        }

        private static long L(string[] x, int i)
        {
            return i < x.Length && long.TryParse(x[i], NumberStyles.Integer, Inv, out long v) ? v : 0L;
        }
    }
}
