using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.DistrictSave
{
    /// <summary>
    /// One record of the mod's section: what kind it is, where it stood (its index in its list, and the game's record it
    /// stood in, for an element), the mod's places it names (for the load's question whether they are made this session),
    /// where the record itself is in the payload, and a stand-in's real values.
    /// </summary>
    public sealed class StoredRecord
    {
        public string Code;          // the kind's code (DistrictSaveRules.Code); kept as read when this version does not know it
        public bool Known;
        public SaveKind Kind;
        public int Index;
        public string Parent;        // the game's record it stood in, for an element; null for a whole record or a field
        public readonly List<PlaceKey> Addresses = new List<PlaceKey>(1);
        public readonly List<string> Districts = new List<string>(0);
        public int Slot = -1;        // its place in the payload; -1 for a stand-in that is all values
        public readonly List<string> Values = new List<string>(0);

        public StoredRecord()
        {
        }

        public StoredRecord(SaveKind kind, int index, string parent)
        {
            Kind = kind;
            Known = true;
            Code = DistrictSaveRules.Code(kind);
            Index = index;
            Parent = parent;
        }
    }

    /// <summary>A line of the store as read: its version, its records, and the payload (the records' text, as the codec made it).</summary>
    public sealed class StoredLine
    {
        public int Version;
        public readonly List<StoredRecord> Records = new List<StoredRecord>();
        public string Payload = string.Empty;
    }

    /// <summary>
    /// The district's records in the save's modData, game-free: one line under <see cref="Key"/>,
    /// "v=1^r:records^p:payload". Records are divided by ';' and their fields by '|': the kind's code, the index, the
    /// game's record it stood in, the mod's addresses it names (street#number, by ','), the mod's districts it names (by
    /// ','), its slot in the payload, then a stand-in's values; every text escaped (DistrictSaveRules.Escape). The payload
    /// is the records themselves, written by the game's own serializer (DistrictSaveCodec). A newer version adds fields at
    /// the end of a record; a line of a later version, or one that cannot be read, is never put back and is kept as it was
    /// read under <see cref="UnreadKey"/>, so that the version that wrote it finds it again.
    /// </summary>
    public static class DistrictSaveStore
    {
        /// <summary>
        /// Lets go every kept record (and its item, at the same index) that names an address on a street whose id starts with
        /// <paramref name="streetPrefix"/>: a feature's places of an earlier plan (DistrictSaveDirector.Retire). Returns how
        /// many. The others stay in their order.
        /// </summary>
        public static int Retire(List<StoredRecord> records, List<object> items, string streetPrefix)
        {
            if (records == null || string.IsNullOrEmpty(streetPrefix))
            {
                return 0;
            }
            int gone = 0;
            for (int i = records.Count - 1; i >= 0; i--)
            {
                StoredRecord r = records[i];
                bool named = false;
                for (int k = 0; r != null && k < r.Addresses.Count && !named; k++)
                {
                    named = r.Addresses[k].Street.StartsWith(streetPrefix, StringComparison.Ordinal);
                }
                if (!named)
                {
                    continue;
                }
                records.RemoveAt(i);
                if (items != null && i < items.Count)
                {
                    items.RemoveAt(i);
                }
                gone++;
            }
            return gone;
        }

        public const string Key = "reallife:district_save";
        public const string UnreadKey = "reallife:district_save_unread";
        public const int Version = 1;

        private const int Fields = 6;   // the fields every record has before a stand-in's values

        public static string Write(List<StoredRecord> records, string payload)
        {
            var t = new StringBuilder(64 + (records != null ? records.Count * 48 : 0) + (payload != null ? payload.Length : 0));
            t.Append("v=").Append(Version.ToString(CultureInfo.InvariantCulture)).Append("^r:");
            for (int i = 0; records != null && i < records.Count; i++)
            {
                StoredRecord r = records[i];
                if (i > 0)
                {
                    t.Append(';');
                }
                t.Append(DistrictSaveRules.Escape(r.Code)).Append('|')
                 .Append(r.Index.ToString(CultureInfo.InvariantCulture)).Append('|')
                 .Append(DistrictSaveRules.Escape(r.Parent)).Append('|');
                for (int k = 0; k < r.Addresses.Count; k++)
                {
                    t.Append(k > 0 ? "," : string.Empty).Append(DistrictSaveRules.Place(r.Addresses[k].Street, r.Addresses[k].Number));
                }
                t.Append('|');
                for (int k = 0; k < r.Districts.Count; k++)
                {
                    t.Append(k > 0 ? "," : string.Empty).Append(DistrictSaveRules.Escape(r.Districts[k]));
                }
                t.Append('|').Append(r.Slot.ToString(CultureInfo.InvariantCulture));
                for (int k = 0; k < r.Values.Count; k++)
                {
                    t.Append('|').Append(DistrictSaveRules.Escape(r.Values[k]));
                }
            }
            t.Append("^p:").Append(payload ?? string.Empty);
            return t.ToString();
        }

        /// <summary>Reads a line; null for one that is not this store's, cannot be read, or is of a later version.</summary>
        public static StoredLine Read(string text, out int version)
        {
            version = 0;
            if (string.IsNullOrEmpty(text) || !text.StartsWith("v=", StringComparison.Ordinal))
            {
                return null;
            }
            try
            {
                int r = text.IndexOf("^r:", StringComparison.Ordinal);
                int p = text.IndexOf("^p:", StringComparison.Ordinal);
                if (r < 0 || p < r || !int.TryParse(text.Substring(2, r - 2), NumberStyles.Integer, CultureInfo.InvariantCulture, out version) || version < 1)
                {
                    return null;
                }
                if (version > Version)
                {
                    return null;   // a later version's: its records may mean what this one does not know
                }
                var line = new StoredLine { Version = version, Payload = text.Substring(p + 3) };
                string records = text.Substring(r + 3, p - r - 3);
                if (records.Length == 0)
                {
                    return line;
                }
                foreach (string one in records.Split(';'))
                {
                    StoredRecord record = ReadRecord(one);
                    if (record == null)
                    {
                        return null;   // a damaged record: the whole line is kept as read
                    }
                    line.Records.Add(record);
                }
                return line;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static StoredRecord ReadRecord(string text)
        {
            string[] f = text.Split('|');
            if (f.Length < Fields)
            {
                return null;
            }
            var record = new StoredRecord { Code = DistrictSaveRules.Unescape(f[0]) };
            record.Known = DistrictSaveRules.KindOf(record.Code, out record.Kind);
            if (!int.TryParse(f[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out record.Index))
            {
                return null;
            }
            record.Parent = DistrictSaveRules.Unescape(f[2]);
            if (f[3].Length > 0)
            {
                foreach (string place in f[3].Split(','))
                {
                    if (!DistrictSaveRules.ReadPlace(place, out string street, out int number))
                    {
                        return null;
                    }
                    record.Addresses.Add(new PlaceKey(street, number));
                }
            }
            if (f[4].Length > 0)
            {
                foreach (string district in f[4].Split(','))
                {
                    record.Districts.Add(DistrictSaveRules.Unescape(district));
                }
            }
            if (!int.TryParse(f[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out record.Slot))
            {
                return null;
            }
            for (int i = Fields; i < f.Length; i++)
            {
                record.Values.Add(DistrictSaveRules.Unescape(f[i]));
            }
            return record;
        }

        /// <summary>
        /// True when every place a record names is made, as <paramref name="address"/> and <paramref name="district"/> say
        /// (the game's own lookups at a load: a building it knows by its address, a district it lists), so that it may go
        /// back into the game's lists; otherwise it stays in the mod's section until a load where they are.
        /// </summary>
        public static bool PlacesMade(StoredRecord record, Func<string, int, bool> address, Func<string, bool> district)
        {
            if (record == null || address == null || district == null)
            {
                return false;
            }
            for (int i = 0; i < record.Addresses.Count; i++)
            {
                if (!address(record.Addresses[i].Street, record.Addresses[i].Number))
                {
                    return false;
                }
            }
            for (int i = 0; i < record.Districts.Count; i++)
            {
                if (!district(record.Districts[i]))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// The text kept under <see cref="UnreadKey"/> with one more unreadable line added (each once, one a line), so that
        /// none is ever lost; the line under it as it stood when <paramref name="line"/> is already in it or empty.
        /// </summary>
        public static string KeepUnread(string unread, string line)
        {
            if (string.IsNullOrEmpty(line))
            {
                return unread;
            }
            if (string.IsNullOrEmpty(unread))
            {
                return line;
            }
            foreach (string kept in unread.Split('\n'))
            {
                if (kept == line)
                {
                    return unread;
                }
            }
            return unread + "\n" + line;
        }
    }
}
