using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Texts
{
    /// <summary>
    /// What people remember of the player (data v6), in the save: v=1^m=person|kind|code|day|value|from^..., texts escaped
    /// as TextsStore's. A record of an unknown kind or with more fields is read as far as it can be; a line that cannot be
    /// read is kept as it was (Core/SaveLines). Capped in people and facts, kept in the order first remembered so a save
    /// writes the same line every time. Each player's own: nothing of it is ever sent. Game-free.
    /// COST: nothing unless a fact is remembered (a short list's pass), a card is built, or the game saves.
    /// </summary>
    public sealed class TextMemoryStore
    {
        public const string SaveKey = "reallife:texts_memory";
        public const int Version = 1;

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private readonly Dictionary<string, List<TextFact>> _byPerson = new Dictionary<string, List<TextFact>>(64, StringComparer.Ordinal);
        private readonly List<string> _people = new List<string>(64);

        public int People => _people.Count;

        public int Facts
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _people.Count; i++)
                {
                    n += _byPerson[_people[i]].Count;
                }
                return n;
            }
        }

        /// <summary>True once something was remembered or let go since the line was read: the save's line is written again.</summary>
        public bool Changed { get; private set; }

        /// <summary>A person's facts, in the order first remembered; null when nothing is remembered of them.</summary>
        public List<TextFact> Of(string person)
        {
            return person != null && _byPerson.TryGetValue(person, out List<TextFact> list) ? list : null;
        }

        /// <summary>Adds every fact of a person to a list, for a card.</summary>
        public void AddTo(string person, List<TextFact> into)
        {
            List<TextFact> list = Of(person);
            if (list != null)
            {
                into.AddRange(list);
            }
        }

        /// <summary>
        /// Remembers a fact of a person; false for a key a memory may not be kept under or an unknown code. The same kind,
        /// code and value again moves its day on.
        /// </summary>
        public bool Remember(string person, TextFact fact)
        {
            if (!TextMemoryRules.KeyOk(person) || TextMemoryRules.Template(fact.Kind, fact.Code) == null)
            {
                return false;
            }
            if (!_byPerson.TryGetValue(person, out List<TextFact> list))
            {
                if (_people.Count >= TextMemoryRules.PeopleMost)
                {
                    Forget(Oldest());
                }
                list = new List<TextFact>(4);
                _byPerson[person] = list;
                _people.Add(person);
            }
            Changed = true;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Kind == fact.Kind && list[i].Code == fact.Code && list[i].Value == fact.Value)
                {
                    if (fact.Day >= list[i].Day)
                    {
                        list[i] = fact;
                    }
                    return true;
                }
            }
            list.Add(fact);
            while (list.Count > TextMemoryRules.FactsMost)
            {
                list.RemoveAt(TextMemoryRules.Least(list));
            }
            return true;
        }

        /// <summary>Moves a stranger's facts to their new key once they are somebody the player knows.</summary>
        public int Move(string from, string to)
        {
            List<TextFact> list = Of(from);
            if (list == null || !TextMemoryRules.KeyOk(to) || from == to)
            {
                return 0;
            }
            int moved = 0;
            for (int i = 0; i < list.Count; i++)
            {
                moved += Remember(to, list[i]) ? 1 : 0;
            }
            Forget(from);
            return moved;
        }

        public bool Forget(string person)
        {
            if (person == null || !_byPerson.Remove(person))
            {
                return false;
            }
            _people.Remove(person);
            Changed = true;
            return true;
        }

        /// <summary>Lets go of facts past their lifetime and strangers past their day; how many facts went.</summary>
        public int Prune(int today)
        {
            int gone = 0;
            for (int p = _people.Count - 1; p >= 0; p--)
            {
                string person = _people[p];
                List<TextFact> list = _byPerson[person];
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    if (!TextMemoryRules.Kept(person, list[i], today))
                    {
                        list.RemoveAt(i);
                        gone++;
                    }
                }
                if (list.Count == 0)
                {
                    _byPerson.Remove(person);
                    _people.RemoveAt(p);
                }
            }
            Changed |= gone > 0;
            return gone;
        }

        public void Clear()
        {
            Changed = _people.Count > 0;
            _byPerson.Clear();
            _people.Clear();
        }

        // The person whose newest fact is oldest, the first such in order.
        private string Oldest()
        {
            string oldest = null;
            int day = int.MaxValue;
            for (int p = 0; p < _people.Count; p++)
            {
                List<TextFact> list = _byPerson[_people[p]];
                int newest = int.MinValue;
                for (int i = 0; i < list.Count; i++)
                {
                    newest = Math.Max(newest, list[i].Day);
                }
                if (newest < day)
                {
                    day = newest;
                    oldest = _people[p];
                }
            }
            return oldest;
        }

        public string Write()
        {
            var t = new StringBuilder(16 + _people.Count * 80);
            t.Append("v=").Append(Version.ToString(Inv));
            for (int p = 0; p < _people.Count; p++)
            {
                string person = _people[p];
                List<TextFact> list = _byPerson[person];
                for (int i = 0; i < list.Count; i++)
                {
                    TextFact f = list[i];
                    t.Append("^m=");
                    TextsStore.Escape(person, t);
                    t.Append('|').Append(TextMemoryRules.Name(f.Kind)).Append('|');
                    TextsStore.Escape(f.Code, t);
                    t.Append('|').Append(f.Day.ToString(Inv)).Append('|');
                    TextsStore.Escape(f.Value ?? string.Empty, t);
                    t.Append('|');
                    TextsStore.Escape(f.From ?? string.Empty, t);
                }
            }
            Changed = false;
            return t.ToString();
        }

        /// <summary>Reads a line into this store; false, and the store empty, if it is of a later version or cannot be read.</summary>
        public bool Read(string text)
        {
            _byPerson.Clear();
            _people.Clear();
            Changed = false;
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
                    Changed = false;
                    return false;
                }
                if (r[0] != 'm')
                {
                    continue;   // a later version's kind of record
                }
                string[] f = r.Substring(2).Split('|');
                if (f.Length < 5 || !int.TryParse(f[3], NumberStyles.Integer, Inv, out int day))
                {
                    Clear();
                    Changed = false;
                    return false;
                }
                TextMemoryKinds kind = TextMemoryRules.KindOf(f[1]);
                if (kind == TextMemoryKinds.None)
                {
                    continue;   // a later version's kind of memory
                }
                string value = TextsStore.Unescape(f[4]);
                string from = f.Length > 5 ? TextsStore.Unescape(f[5]) : string.Empty;
                Remember(TextsStore.Unescape(f[0]), new TextFact(kind, TextsStore.Unescape(f[2]), day, value.Length > 0 ? value : null, from.Length > 0 ? from : null));
            }
            Changed = false;
            return true;
        }
    }
}
