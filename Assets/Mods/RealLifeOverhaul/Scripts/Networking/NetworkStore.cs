using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Networking
{
    /// <summary>
    /// The networking life in a save: one entry of the save's data, short lines.
    ///
    ///   reallife:network
    ///     NW1|last favour day
    ///     C|person|role|via|met day|given|told|street|number|org     one person rolled when met
    ///
    /// Numbers are in the invariant culture. A line of a kind this version does not know (from a later one) is skipped,
    /// and fields past the ones it knows are left; a first line of another version leaves the whole entry unread, and
    /// it is then kept as it was read (Core/SaveLines). Game-free.
    /// </summary>
    public static class NetworkStore
    {
        public const string Key = "reallife:network";
        private const string Version = "NW1";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const int Roles = (int)NetRole.Landlord + 1;   // the positions this version knows
        private const int Vias = (int)NetVia.Family + 1;

        public static string Write(NetworkFile f)
        {
            var b = new StringBuilder(16 + f.Contacts.Count * 48);
            b.Append(Version).Append('|').Append(f.LastFavourDay.ToString(Inv));
            for (int i = 0; i < f.Contacts.Count; i++)
            {
                NetContact c = f.Contacts[i];
                b.Append("\nC|").Append(c.Person.ToString(Inv)).Append('|').Append(((int)c.Role).ToString(Inv)).Append('|').Append(((int)c.Via).ToString(Inv))
                    .Append('|').Append(c.MetDay.ToString(Inv)).Append('|').Append(c.Given.ToString(Inv)).Append('|').Append(c.Told.ToString(Inv))
                    .Append('|').Append(Clean(c.Street)).Append('|').Append(c.Number.ToString(Inv)).Append('|').Append(Clean(c.Org));
            }
            return b.ToString();
        }

        /// <summary>The file from the save's line; null if it is not this version's or cannot be read at all.</summary>
        public static NetworkFile Read(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }
            try
            {
                string[] lines = text.Split('\n');
                string[] h = lines[0].TrimEnd('\r').Split('|');
                if (h.Length < 2 || h[0] != Version)
                {
                    return null;
                }
                var f = new NetworkFile { LastFavourDay = I(h, 1, -1000) };
                for (int i = 1; i < lines.Length; i++)
                {
                    string[] c = lines[i].TrimEnd('\r').Split('|');
                    if (c[0] != "C" || c.Length < 10 || f.Contacts.Count >= NetworkFile.Most)
                    {
                        continue;
                    }
                    int person = I(c, 1, -1), role = I(c, 2, -1), via = I(c, 3, -1);
                    if (person < 0 || person >= NetworkFile.Most || role < 0 || role >= Roles || via < 0 || via >= Vias || f.Of(person) != null)
                    {
                        continue;
                    }
                    f.Contacts.Add(new NetContact
                    {
                        Person = person, Role = (NetRole)role, Via = (NetVia)via, MetDay = I(c, 4, 0), Given = Math.Max(0, I(c, 5, 0)), Told = Math.Max(0, I(c, 6, 0)),
                        Street = c[7], Number = I(c, 8, 0), Org = c[9]
                    });
                }
                return f;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The save's file, a new one if it has none, or null when its line is one this version cannot read (kept as read).</summary>
        public static NetworkFile Load(Dictionary<string, string> data)
        {
            if (data == null || !data.TryGetValue(Key, out string text) || string.IsNullOrEmpty(text))
            {
                return new NetworkFile();
            }
            NetworkFile f = Read(text);
            if (f == null)
            {
                SaveLines.Unreadable(data, Key, "it could not be read");
            }
            return f;
        }

        public static void Save(Dictionary<string, string> data, NetworkFile f)
        {
            if (data != null && f != null)
            {
                data[Key] = Write(f);
            }
        }

        private static string Clean(string s)
        {
            return string.IsNullOrEmpty(s) ? string.Empty : s.Replace("|", " ").Replace("\n", " ").Replace("\r", string.Empty);
        }

        private static int I(string[] a, int at, int fallback)
        {
            return at < a.Length && int.TryParse(a[at], NumberStyles.Integer, Inv, out int v) ? v : fallback;
        }
    }
}
