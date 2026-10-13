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
    /// </summary>
    public sealed class TechWriter
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private readonly StringBuilder _t = new StringBuilder(4096);
        private bool _first;

        public TechWriter(int version)
        {
            _t.Append("v=").Append(version.ToString(Inv));
        }

        /// <summary>Starts a part.</summary>
        public TechWriter Part(char letter)
        {
            _t.Append('~').Append(letter).Append(':');
            _first = true;
            return this;
        }

        /// <summary>Starts a record in the part.</summary>
        public TechWriter Record()
        {
            if (!_first)
            {
                _t.Append(';');
            }
            _first = false;
            _fields = 0;
            return this;
        }

        private int _fields;

        public TechWriter Add(long value)
        {
            Sep();
            _t.Append(value.ToString(Inv));
            return this;
        }

        public TechWriter Add(bool value)
        {
            return Add(value ? 1 : 0);
        }

        public TechWriter Add(string text)
        {
            Sep();
            _t.Append(Clean(text));
            return this;
        }

        /// <summary>A place key (an escaped street name, '~', a number): its '~' kept as "%7E", which an escaped name never holds.</summary>
        public TechWriter Place(string key)
        {
            Sep();
            _t.Append((key ?? string.Empty).Replace("~", "%7E").Replace(";", " ").Replace("|", " "));
            return this;
        }

        private void Sep()
        {
            if (_fields++ > 0)
            {
                _t.Append('|');
            }
        }

        public override string ToString()
        {
            return _t.ToString();
        }

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
