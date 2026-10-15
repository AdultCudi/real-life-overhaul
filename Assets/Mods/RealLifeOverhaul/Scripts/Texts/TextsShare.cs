using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Texts
{
    /// <summary>
    /// RLO Texts in a Going Public session (the owner's rule, 16 Oct 2026):
    ///
    ///   personal   what is that player's own (texts from the partner, friends, family, the roommate, the in-laws, the boss;
    ///              Flare messages to them; replies addressed to them) is written on their own machine and never sent; a
    ///              player without a pack, or with it off, has the written lines
    ///   shared     what every player sees (the shared Flare feed: the city's people's posts and comments, the city's
    ///              accounts and their answers, the rivals; the shared City Ledger) is written once, by the host, checked
    ///              there, and sent as text in this part of the shared city (letter <see cref="Part"/>), small and in
    ///              the shared city's own pace; a guest that joins late has the whole part with the snapshot
    ///   guests     show the host's words for those items after checking them again on arrival
    ///              (<see cref="TextCheck.JudgeShared"/>); a guest never writes shared text. Without the host's words in
    ///              time (no pack, switched off, too slow) every player shows the written line, as before
    ///   older RLO  a part letter it does not know is passed over whole (SharedCity), and another version's messages are
    ///              never applied at all
    ///
    /// One line a text: "w|key|intent|words", the words escaped as the save's (TextsStore.Escape), at most
    /// <see cref="WordsMost"/> characters; the whole part at most <see cref="LinesMost"/> lines (the newest). The key is the
    /// session's number of the item ("fn:45" a post by the host's number, "lp:day:n" a piece of the paper). Game-free
    /// (EconomyCheck, TextsChecks.cs). COST: a line a text written on the host; nothing per frame.
    /// </summary>
    public static class TextsShare
    {
        public const char Part = 'T';
        public const int WordsMost = 200;        // characters of one text (the longest intent's most)
        public const int LinesMost = 300;        // lines of the whole part (a day of the feed and the paper)
        public const int ReadMost = 400;         // lines a guest reads of one message

        /// <summary>One shared text as a line.</summary>
        public static void Line(StringBuilder b, string key, string intent, string words)
        {
            if (b.Length > 0)
            {
                b.Append('\n');
            }
            b.Append("w|");
            TextsStore.Escape(key, b);
            b.Append('|');
            TextsStore.Escape(intent ?? string.Empty, b);
            b.Append('|');
            TextsStore.Escape(words.Length > WordsMost ? words.Substring(0, WordsMost) : words, b);
        }

        /// <summary>The host's whole part: every shared text kept (the newest <see cref="LinesMost"/>); null with none.</summary>
        public static string Whole(TextsStore store)
        {
            var b = new StringBuilder(4096);
            IReadOnlyList<TextKept> all = store.All;
            int from = 0, count = 0;
            for (int i = all.Count - 1; i >= 0 && count < LinesMost; i--)
            {
                if (all[i].Shared)
                {
                    from = i;
                    count++;
                }
            }
            for (int i = from; i < all.Count && count > 0; i++)
            {
                if (all[i].Shared)
                {
                    Line(b, NetKey(all[i].Key), all[i].Intent, all[i].Words);
                }
            }
            return b.Length > 0 ? b.ToString() : null;
        }

        /// <summary>A guest: reads the host's lines, checks each again, and keeps the ones that pass. Returns how many were kept.</summary>
        public static int Apply(string text, TextsStore store, int day, List<string> refused)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }
            string[] lines = text.Split('\n');
            int kept = 0;
            for (int i = 0; i < lines.Length && i < ReadMost; i++)
            {
                string[] f = lines[i].Split('|');
                if (f.Length != 4 || f[0] != "w")
                {
                    continue;   // (a line this version does not know)
                }
                string key = TextsStore.Unescape(f[1]), intentKey = TextsStore.Unescape(f[2]), words = TextsStore.Unescape(f[3]);
                if (!Shareable(key))
                {
                    continue;
                }
                TextVerdict verdict = TextCheck.JudgeShared(words, TextIntents.Find(intentKey) ?? TextIntents.Find(intentKey, "on"));
                if (verdict != TextVerdict.Ok)
                {
                    refused?.Add(key + " (" + verdict + ")");
                    continue;
                }
                store.Keep(key, day, intentKey, words, true);
                kept++;
            }
            return kept;
        }

        /// <summary>Only the session's shared items are taken from the host: a post by its number or a piece of the paper.</summary>
        public static bool Shareable(string key)
        {
            return key != null && (key.StartsWith("fn:", System.StringComparison.Ordinal) || key.StartsWith("lp:", System.StringComparison.Ordinal)) && key.Length <= 40;
        }

        /// <summary>The key a host's text goes under to the guests: a post of the host's own ("fp:12") by the session's number, the host's own ("fn:12").</summary>
        public static string NetKey(string key)
        {
            return key != null && key.StartsWith("fp:", System.StringComparison.Ordinal) ? "fn:" + key.Substring(3) : key;
        }

        /// <summary>The key of a shared post by the session's number.</summary>
        public static string PostKey(int netId) => "fn:" + netId.ToString(CultureInfo.InvariantCulture);
    }
}
