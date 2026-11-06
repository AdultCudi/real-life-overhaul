using System;
using System.Collections.Generic;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The session record's tally of the mod's objects by holder or kind against the session's first count, so the line a
    /// minute names what grows ("Civic Props > cone +120"). A kind is a name without the mod's prefix, numbers or a copy's
    /// marks. Game-free, checked offline. Cost: a few hundred look-ups once a minute, with diagnostics on.
    /// </summary>
    public sealed class ObjectKinds
    {
        public const int MostKinds = 512;      // kinds kept; any more are counted together under Others
        public const int Shown = 3;            // growers named in the line
        public const int LongestName = 40;     // characters of a kind's name kept
        public const string Others = "(others)";
        public const string Join = " > ";
        private const string Prefix = "RealLifeOverhaul";
        private const string ShortPrefix = "RealLife";
        private const string Copy = "(Clone)";

        private readonly Dictionary<string, int> _now = new Dictionary<string, int>(256, StringComparer.Ordinal);
        private readonly Dictionary<string, int> _first = new Dictionary<string, int>(256, StringComparer.Ordinal);
        private readonly string[] _topKind = new string[Shown];
        private readonly int[] _topGrown = new int[Shown];
        private readonly int[] _topNow = new int[Shown];

        /// <summary>The minute of the first count, which growth is measured from; -1 before it.</summary>
        public int FirstMinute { get; private set; } = -1;

        /// <summary>How many kinds the last count found.</summary>
        public int Count => _now.Count;

        /// <summary>A new count begins.</summary>
        public void Begin()
        {
            _now.Clear();
        }

        /// <summary>So many objects of a kind (a key from <see cref="Key"/>).</summary>
        public void Add(string key, int objects)
        {
            if (objects <= 0 || key == null)
            {
                return;
            }
            if (!_now.TryGetValue(key, out int had) && _now.Count >= MostKinds)
            {
                key = Others;
                _now.TryGetValue(key, out had);
            }
            _now[key] = had + objects;
        }

        /// <summary>The count is done: the session's first is what the later ones are measured against.</summary>
        public void End(int minute)
        {
            if (FirstMinute >= 0)
            {
                return;
            }
            FirstMinute = minute;
            foreach (KeyValuePair<string, int> pair in _now)
            {
                _first[pair.Key] = pair.Value;
            }
        }

        /// <summary>The kinds that grew the most since the first count, at most <see cref="Shown"/>; read them with <see cref="Top"/>.</summary>
        public int Rank()
        {
            int found = 0;
            foreach (KeyValuePair<string, int> pair in _now)
            {
                _first.TryGetValue(pair.Key, out int then);
                int grown = pair.Value - then;
                if (grown <= 0)
                {
                    continue;
                }
                int at = found;
                while (at > 0 && (grown > _topGrown[at - 1] || grown == _topGrown[at - 1] && string.CompareOrdinal(pair.Key, _topKind[at - 1]) < 0))
                {
                    at--;
                }
                if (at >= Shown)
                {
                    continue;
                }
                for (int i = Math.Min(found, Shown - 1); i > at; i--)
                {
                    _topKind[i] = _topKind[i - 1];
                    _topGrown[i] = _topGrown[i - 1];
                    _topNow[i] = _topNow[i - 1];
                }
                _topKind[at] = pair.Key;
                _topGrown[at] = grown;
                _topNow[at] = pair.Value;
                found = Math.Min(Shown, found + 1);
            }
            return found;
        }

        /// <summary>The k-th of the last <see cref="Rank"/>: its kind, how many it grew by and how many there are now.</summary>
        public void Top(int k, out string kind, out int grown, out int now)
        {
            kind = _topKind[k];
            grown = _topGrown[k];
            now = _topNow[k];
        }

        /// <summary>The key of a holder (below null) or of a kind under it: the root's kind (left out for the mod's own root), the holder's, the kind's.</summary>
        public static string Key(string root, string holder, string below)
        {
            string r = Kind(root);
            string key = r == Prefix ? null : r;
            if (holder != null)
            {
                key = key == null ? Kind(holder) : key + Join + Kind(holder);
            }
            if (below != null)
            {
                key = key == null ? Kind(below) : key + Join + Kind(below);
            }
            return key ?? r;
        }

        /// <summary>An object's name as a kind: "RealLifeOverhaul Bus Stop 12" is "Bus Stop", "Cone (3)" is "Cone".</summary>
        public static string Kind(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return "(unnamed)";
            }
            int start = 0;
            if (name.StartsWith(Prefix + " ", StringComparison.Ordinal))
            {
                start = Prefix.Length + 1;
            }
            else if (name.StartsWith(ShortPrefix + " ", StringComparison.Ordinal))
            {
                start = ShortPrefix.Length + 1;
            }
            int end = name.Length;
            bool trimmed = true;
            while (trimmed && end > start)
            {
                trimmed = false;
                if (end - start >= Copy.Length && string.CompareOrdinal(name, end - Copy.Length, Copy, 0, Copy.Length) == 0)
                {
                    end -= Copy.Length;
                    trimmed = true;
                }
                while (end > start && Trailing(name[end - 1]))
                {
                    end--;
                    trimmed = true;
                }
            }
            if (end <= start)
            {
                return name.Length > LongestName ? name.Substring(0, LongestName) : name;
            }
            return name.Substring(start, Math.Min(end - start, LongestName));
        }

        private static bool Trailing(char c)
        {
            return c >= '0' && c <= '9' || c == ' ' || c == '(' || c == ')' || c == '#' || c == '_' || c == '-' || c == '.';
        }
    }
}
