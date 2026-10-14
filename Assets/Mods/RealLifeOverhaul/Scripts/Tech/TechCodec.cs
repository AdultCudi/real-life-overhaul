using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// The way every part of the tech sector writes its line of the save: parts divided by '~', each a letter and a
    /// colon; records inside a part by ';', fields by '|'. Text fields have the four dividers taken out. A reader takes
    /// the fields it knows and leaves out what it does not, so a later version's line with more fields at the end of a
    /// record still loads. Game-free.
    ///
    /// THE COST. A save writes every line (600 professionals, the firms, the hiring, the startups, the careers) on the
    /// game's autosave in play: the writer takes a builder kept from the last line (one per writer alive at once, its room
    /// grown once), and writes numbers, texts and place keys into it in place, so a line costs the one string it is and
    /// nothing besides (it made a string of every number and a builder and a string of every text). The text is
    /// character for character what it was (EconomyCheck, TechWriterChecks).
    /// </summary>
    public sealed class TechWriter
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        [ThreadStatic] private static Stack<StringBuilder> _kept;   // builders of finished lines, for the next writers
        private StringBuilder _t;
        private string _written;
        private bool _first;
        private readonly char[] _digits = new char[20];

        public TechWriter(int version)
        {
            Stack<StringBuilder> kept = _kept ?? (_kept = new Stack<StringBuilder>(4));
            _t = kept.Count > 0 ? kept.Pop() : new StringBuilder(4096);
            _t.Length = 0;
            _t.Append("v=");
            Number(version);
        }

        /// <summary>Starts a part.</summary>
        public TechWriter Part(char letter)
        {
            Builder.Append('~').Append(letter).Append(':');
            _first = true;
            return this;
        }

        /// <summary>Starts a record in the part.</summary>
        public TechWriter Record()
        {
            if (!_first)
            {
                Builder.Append(';');
            }
            _first = false;
            _fields = 0;
            return this;
        }

        private int _fields;

        public TechWriter Add(long value)
        {
            Sep();
            Number(value);
            return this;
        }

        public TechWriter Add(bool value)
        {
            return Add(value ? 1 : 0);
        }

        public TechWriter Add(string text)
        {
            Sep();
            StringBuilder t = Builder;
            for (int i = 0; text != null && i < text.Length; i++)
            {
                char c = text[i];
                t.Append(c == '~' || c == ';' || c == '|' ? ' ' : c);   // (as Clean)
            }
            return this;
        }

        /// <summary>A place key (an escaped street name, '~', a number): its '~' kept as "%7E", which an escaped name never holds.</summary>
        public TechWriter Place(string key)
        {
            Sep();
            StringBuilder t = Builder;
            for (int i = 0; key != null && i < key.Length; i++)
            {
                char c = key[i];
                if (c == '~')
                {
                    t.Append("%7E");
                }
                else
                {
                    t.Append(c == ';' || c == '|' ? ' ' : c);
                }
            }
            return this;
        }

        private void Sep()
        {
            if (_fields++ > 0)
            {
                Builder.Append('|');
            }
        }

        // A whole number as the invariant culture writes it ("-" and the digits), with no string made.
        private void Number(long value)
        {
            StringBuilder t = Builder;
            if (value == long.MinValue)
            {
                t.Append("-9223372036854775808");
                return;
            }
            if (value < 0)
            {
                t.Append('-');
                value = -value;
            }
            int n = 0;
            do
            {
                _digits[n++] = (char)('0' + (int)(value % 10));
                value /= 10;
            }
            while (value > 0);
            while (n > 0)
            {
                t.Append(_digits[--n]);
            }
        }

        private StringBuilder Builder => _t ?? throw new InvalidOperationException("the line has been written");

        /// <summary>The line; the writer's builder goes back for the next writer (the line is kept: asked again, the same string).</summary>
        public override string ToString()
        {
            if (_t != null)
            {
                _written = _t.ToString();
                if (_t.Capacity <= KeptMost)
                {
                    (_kept ?? (_kept = new Stack<StringBuilder>(4))).Push(_t);
                }
                _t = null;
            }
            return _written;
        }

        private const int KeptMost = 4 * 1024 * 1024;   // characters a kept builder may hold (a line of 600 professionals is about 100 thousand)

        /// <summary>A text with the three dividers taken out (an id of the game's may hold '=', '+' and '/', which are kept).</summary>
        public static string Clean(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }
            var b = new StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                b.Append(c == '~' || c == ';' || c == '|' ? ' ' : c);
            }
            return b.ToString();
        }
    }

    /// <summary>Reads a line <see cref="TechWriter"/> wrote: its version, and its parts by letter.</summary>
    public sealed class TechReader
    {
        private readonly Dictionary<char, string> _parts = new Dictionary<char, string>(8);

        public int Version { get; private set; }

        public TechReader(string text)
        {
            string[] parts = (text ?? string.Empty).Split('~');
            if (parts.Length == 0 || !parts[0].StartsWith("v=", StringComparison.Ordinal))
            {
                throw new FormatException("not a line of the tech sector");
            }
            Version = int.Parse(parts[0].Substring(2), CultureInfo.InvariantCulture);
            for (int i = 1; i < parts.Length; i++)
            {
                if (parts[i].Length >= 2 && parts[i][1] == ':')
                {
                    _parts[parts[i][0]] = parts[i].Substring(2);
                }
            }
        }

        /// <summary>The records of a part, each its fields; none if the part is not there.</summary>
        public IEnumerable<Fields> Records(char letter)
        {
            string part;
            if (!_parts.TryGetValue(letter, out part) || part.Length == 0)
            {
                yield break;
            }
            string[] records = part.Split(';');
            for (int i = 0; i < records.Length; i++)
            {
                if (records[i].Length > 0)
                {
                    yield return new Fields(records[i].Split('|'));
                }
            }
        }

        /// <summary>The first record of a part (a header); empty fields if the part is not there.</summary>
        public Fields One(char letter)
        {
            foreach (Fields f in Records(letter))
            {
                return f;
            }
            return new Fields(new string[0]);
        }
    }

    /// <summary>One record's fields, read in order; a field past the end reads as the default given.</summary>
    public sealed class Fields
    {
        private readonly string[] _f;
        private int _i;

        public Fields(string[] fields)
        {
            _f = fields;
        }

        public int Count => _f.Length;

        public int Int(int otherwise = 0)
        {
            long v = Long(otherwise);
            return v > int.MaxValue ? int.MaxValue : v < int.MinValue ? int.MinValue : (int)v;
        }

        public long Long(long otherwise = 0)
        {
            if (_i >= _f.Length)
            {
                _i++;
                return otherwise;
            }
            long v;
            return long.TryParse(_f[_i++], NumberStyles.Integer, CultureInfo.InvariantCulture, out v) ? v : otherwise;
        }

        /// <summary>A place key written by <see cref="TechWriter.Place"/>.</summary>
        public string Place()
        {
            return Text().Replace("%7E", "~");
        }

        public bool Bool(bool otherwise = false)
        {
            return Int(otherwise ? 1 : 0) != 0;
        }

        public string Text(string otherwise = "")
        {
            if (_i < _f.Length)
            {
                return _f[_i++];
            }
            _i++;
            return otherwise;
        }
    }
}
