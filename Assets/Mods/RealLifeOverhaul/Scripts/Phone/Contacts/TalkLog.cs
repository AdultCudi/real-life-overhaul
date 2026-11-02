using System;
using System.Collections.Generic;

namespace RealLifeOverhaul
{
    /// <summary>One line of a conversation the player had with a contact: when (the game's minute since day 0), whose, and the words.</summary>
    public struct TalkLine
    {
        public int Minute;
        public bool Mine;    // the player's own line; false for the contact's
        public string Text;
    }

    /// <summary>
    /// What was said in the calls a contact took, kept for each contact so the Contacts thread shows the conversation after it
    /// ended: their lines as the dialog showed them and the player's replies and typed words, in the order they happened. The
    /// game keeps texts only (Contact.messagesQueue, its twenty newest), never a call, so this is the mod's own short history,
    /// capped by lines for a contact and by contacts. Game-free, saved by <see cref="TalkLogStore"/>, read and written through
    /// <see cref="TalkLogGame"/>. COST: a dictionary lookup and a short list; nothing a frame.
    /// </summary>
    public sealed class TalkLog
    {
        public const int LinesKept = 30;     // lines for one contact: the newest, as the game keeps twenty texts
        public const int MostPeople = 40;    // contacts with a history: the one talked to longest ago goes first
        public const int LineMost = 200;     // characters in a line

        private readonly Dictionary<string, List<TalkLine>> _by = new Dictionary<string, List<TalkLine>>(16, StringComparer.Ordinal);

        /// <summary>Counts every change, so a screen laid out from this can see that it must look again.</summary>
        public int Changes { get; private set; }

        /// <summary>The contacts with a history.</summary>
        public Dictionary<string, List<TalkLine>>.KeyCollection People => _by.Keys;

        public int PeopleCount => _by.Count;

        /// <summary>The line of a contact, kept as one word of the save's record.</summary>
        public static string IdOf(string id) => TalkLogRules.Clean(id, 80);

        /// <summary>
        /// Keeps a line for a contact. False for no contact, no words or the same line again in the same minute (one page shown
        /// twice). Past <see cref="LinesKept"/> the oldest of the contact's goes; past <see cref="MostPeople"/> the contact
        /// whose newest line is oldest goes.
        /// </summary>
        public bool Add(string contact, int minute, bool mine, string text)
        {
            string id = IdOf(contact), words = TalkLogRules.Clean(text, LineMost);
            if (id.Length == 0 || words.Length == 0)
            {
                return false;
            }
            if (!_by.TryGetValue(id, out List<TalkLine> lines))
            {
                lines = new List<TalkLine>(8);
                _by[id] = lines;
            }
            if (lines.Count > 0)
            {
                TalkLine last = lines[lines.Count - 1];
                if (last.Mine == mine && last.Minute == minute && string.Equals(last.Text, words, StringComparison.Ordinal))
                {
                    return false;
                }
            }
            lines.Add(new TalkLine { Minute = minute, Mine = mine, Text = words });
            if (lines.Count > LinesKept)
            {
                lines.RemoveRange(0, lines.Count - LinesKept);
            }
            if (_by.Count > MostPeople)
            {
                Evict(id);
            }
            Changes++;
            return true;
        }

        /// <summary>A contact's lines, oldest first; null for none. The list is the log's own: read it, never change it.</summary>
        public List<TalkLine> Of(string contact)
        {
            return _by.TryGetValue(IdOf(contact), out List<TalkLine> lines) && lines.Count > 0 ? lines : null;
        }

        /// <summary>True when the contact's history is as long as it is kept, so its start may be gone.</summary>
        public bool Full(string contact)
        {
            List<TalkLine> lines = Of(contact);
            return lines != null && lines.Count >= LinesKept;
        }

        /// <summary>Forgets a contact's history. True if there was one.</summary>
        public bool Clear(string contact)
        {
            bool had = _by.Remove(IdOf(contact));
            Changes += had ? 1 : 0;
            return had;
        }

        /// <summary>Forgets the history of every contact the caller does not keep (deleted contacts). How many went.</summary>
        public int Keep(HashSet<string> contacts)
        {
            var gone = new List<string>(4);
            foreach (string id in _by.Keys)
            {
                if (contacts == null || !contacts.Contains(id))
                {
                    gone.Add(id);
                }
            }
            for (int i = 0; i < gone.Count; i++)
            {
                _by.Remove(gone[i]);
            }
            Changes += gone.Count;
            return gone.Count;
        }

        // The contact, other than the one just added, whose newest line is the oldest.
        private void Evict(string except)
        {
            string oldest = null;
            int oldestMinute = int.MaxValue;
            foreach (KeyValuePair<string, List<TalkLine>> p in _by)
            {
                if (p.Key == except)
                {
                    continue;
                }
                int newest = p.Value.Count > 0 ? p.Value[p.Value.Count - 1].Minute : int.MinValue;
                if (newest < oldestMinute)
                {
                    oldestMinute = newest;
                    oldest = p.Key;
                }
            }
            if (oldest != null)
            {
                _by.Remove(oldest);
            }
        }
    }

    /// <summary>The rules the history is cleaned and ordered by. Game-free.</summary>
    public static class TalkLogRules
    {
        /// <summary>
        /// Words as one plain line of at most <paramref name="most"/> characters: control characters and the save's separators
        /// become spaces, runs of spaces one, the ends trimmed, a cut line never ends in half a pair of surrogates. Never null.
        /// </summary>
        public static string Clean(string text, int most)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }
            var sb = new System.Text.StringBuilder(Math.Min(text.Length, most));
            bool space = true;
            for (int i = 0; i < text.Length && sb.Length < most; i++)
            {
                char ch = text[i];
                bool blank = char.IsControl(ch) || char.IsWhiteSpace(ch) || ch == '^' || ch == '|';
                if (blank)
                {
                    if (!space)
                    {
                        sb.Append(' ');
                    }
                    space = true;
                    continue;
                }
                sb.Append(ch);
                space = false;
            }
            if (sb.Length > 0 && sb[sb.Length - 1] == ' ')
            {
                sb.Length--;
            }
            if (sb.Length > 0 && char.IsHighSurrogate(sb[sb.Length - 1]))
            {
                sb.Length--;
            }
            return sb.ToString();
        }

        /// <summary>
        /// True when a line of the history comes before a text of the game's thread (both in game minutes): the thread is read in
        /// order, and the history's line goes first only when it is earlier.
        /// </summary>
        public static bool Before(int talkMinute, int textMinute) => talkMinute < textMinute;

        /// <summary>The game day of a minute since day 0.</summary>
        public static int DayOf(int minute) => minute < 0 ? 0 : minute / (60 * 24);

        /// <summary>The hour of the day of a minute since day 0.</summary>
        public static int HourOf(int minute) => minute < 0 ? 0 : minute / 60 % 24;

        /// <summary>The minute of the hour of a minute since day 0.</summary>
        public static int MinuteOf(int minute) => minute < 0 ? 0 : minute % 60;
    }
}
