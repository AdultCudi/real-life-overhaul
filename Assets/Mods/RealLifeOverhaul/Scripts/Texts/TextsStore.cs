using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Texts
{
    /// <summary>Words the model wrote for something the game shows again later (a Flare post, a reply, a message), by its key.</summary>
    public sealed class TextKept
    {
        public string Key;        // "fp:123" a Flare post of this save, "fn:45" a shared post by the host's number, "fr:123:2" a friend's reply, "fd:c4:9000" a message
        public int Day;           // the game day it was written
        public string Intent;     // the intent it was written for (a guest checks the host's words by it)
        public string Words;
        public bool Shared;       // the host's words for the session's shared feed or paper (go to guests)
    }

    /// <summary>
    /// What RLO Texts keeps in a save: the model's words for what is worded again each time it is shown (Flare's posts,
    /// replies and messages are worded when drawn, from their kind and seed), so a load shows the same words. A text on
    /// the phone needs nothing here: the game keeps the words in its own thread. One line under its own key, records
    /// separated by '^', fields by '|', the words escaped (% ^ | and line breaks):
    ///
    ///     v=1^w=key|day|intent|shared|words^...
    ///
    /// At most <see cref="Most"/> records, none older than <see cref="KeepDays"/> days (the oldest go first). A line of a
    /// later version, or one that cannot be read, gives none and is kept in the save as it was read (Core/SaveLines).
    /// Without the line nothing changes: the written lines are shown. Game-free (EconomyCheck, TextsChecks.cs).
    /// COST: nothing unless a save is loaded or written, or words come back (a dictionary write).
    /// </summary>
    public sealed class TextsStore
    {
        public const string SaveKey = "reallife:texts";
        public const int Version = 1;
        public const int Most = 800;            // records kept
        public const int KeepDays = 21;         // days a record is kept (Flare keeps its feed a week; a thread is read back for a while)

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private readonly Dictionary<string, TextKept> _byKey = new Dictionary<string, TextKept>(64, StringComparer.Ordinal);
        private readonly List<TextKept> _order = new List<TextKept>(64);

        public int Count => _order.Count;

        public IReadOnlyList<TextKept> All => _order;

        /// <summary>The words kept under a key; null if none.</summary>
        public string Words(string key)
        {
            return key != null && _byKey.TryGetValue(key, out TextKept k) ? k.Words : null;
        }

        public TextKept Get(string key)
        {
            return key != null && _byKey.TryGetValue(key, out TextKept k) ? k : null;
        }

        /// <summary>Keeps words under a key (replacing any), and lets the oldest go past the limit.</summary>
        public TextKept Keep(string key, int day, string intent, string words, bool shared)
        {
            if (key == null || words == null)
            {
                return null;
            }
            if (_byKey.TryGetValue(key, out TextKept old))
            {
                _order.Remove(old);
            }
            var k = new TextKept { Key = key, Day = day, Intent = intent, Words = words, Shared = shared };
            _byKey[key] = k;
            _order.Add(k);
            while (_order.Count > Most)
            {
                _byKey.Remove(_order[0].Key);
                _order.RemoveAt(0);
            }
            return k;
        }

        /// <summary>Records older than the days kept let go.</summary>
        public int Prune(int today)
        {
            int gone = 0;
            for (int i = _order.Count - 1; i >= 0; i--)
            {
                if (today - _order[i].Day > KeepDays)
                {
                    _byKey.Remove(_order[i].Key);
                    _order.RemoveAt(i);
                    gone++;
                }
            }
            return gone;
        }

        public void Clear()
        {
            _byKey.Clear();
            _order.Clear();
        }

        // ---------------------------------------------------------------- the save

        public string Write()
        {
            var t = new StringBuilder(16 + _order.Count * 120);
            t.Append("v=").Append(Version.ToString(Inv));
            for (int i = 0; i < _order.Count; i++)
            {
                TextKept k = _order[i];
                t.Append("^w=");
                Escape(k.Key, t);
                t.Append('|').Append(k.Day.ToString(Inv)).Append('|');
                Escape(k.Intent ?? string.Empty, t);
                t.Append('|').Append(k.Shared ? '1' : '0').Append('|');
                Escape(k.Words, t);
            }
            return t.ToString();
        }

        /// <summary>Reads a line into this store (emptied first). False if it is of another version or cannot be read (the store is then empty).</summary>
        public bool Read(string text)
        {
            Clear();
            if (string.IsNullOrEmpty(text))
            {
                return true;
            }
            string[] records = text.Split('^');
            if (records[0] != "v=" + Version.ToString(Inv))
            {
                return false;
            }
            for (int i = 1; i < records.Length; i++)
            {
                string r = records[i];
                if (r.Length < 2 || r[1] != '=')
                {
                    Clear();
                    return false;
                }
                if (r[0] != 'w')
                {
                    continue;   // (a record of a later version's kind: passed over)
                }
                string[] f = r.Substring(2).Split('|');
                if (f.Length < 5 || !int.TryParse(f[1], NumberStyles.Integer, Inv, out int day))
                {
                    Clear();
                    return false;
                }
                string intent = Unescape(f[2]);
                Keep(Unescape(f[0]), day, intent.Length > 0 ? intent : null, Unescape(f[4]), f[3] == "1");
            }
            return true;
        }

        /// <summary>Text with % ^ | and line breaks escaped, so it sits in one field of one record.</summary>
        public static void Escape(string text, StringBuilder t)
        {
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                switch (c)
                {
                    case '%': t.Append("%25"); break;
                    case '^': t.Append("%5E"); break;
                    case '|': t.Append("%7C"); break;
                    case '\n': t.Append("%0A"); break;
                    case '\r': t.Append("%0D"); break;
                    default: t.Append(c); break;
                }
            }
        }

        public static string Unescape(string text)
        {
            if (text.IndexOf('%') < 0)
            {
                return text;
            }
            var t = new StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '%' && i + 2 < text.Length && int.TryParse(text.Substring(i + 1, 2), NumberStyles.HexNumber, Inv, out int code))
                {
                    t.Append((char)code);
                    i += 2;
                }
                else
                {
                    t.Append(text[i]);
                }
            }
            return t.ToString();
        }
    }
}
