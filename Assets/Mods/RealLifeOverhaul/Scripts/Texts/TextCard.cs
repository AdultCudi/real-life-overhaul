using System;
using System.Collections.Generic;
using System.Text;

namespace RealLifeOverhaul.Texts
{
    /// <summary>One line of a thread on a card: who said it and what they said.</summary>
    public struct TextSaid
    {
        public string Who;
        public string Said;

        public TextSaid(string who, string said)
        {
            Who = who;
            Said = said;
        }
    }

    /// <summary>The character sheet the text model reads, filled from the game's state, never a model's words. Game-free; kept and cleared.</summary>
    public sealed class TextSheet
    {
        public string Who, Role, Mood, Style, Player, When, News, Write;
        public string Lang = "en";
        public readonly List<string> Traits = new List<string>(3);
        public readonly List<string> Facts = new List<string>(4);
        public readonly List<string> Memory = new List<string>(3);       // data v6: what the speaker remembers of the player, one line each
        public readonly List<TextSaid> History = new List<TextSaid>(3);
        public readonly List<string> Must = new List<string>(3);
        public readonly List<TextSlotKind> MustKinds = new List<TextSlotKind>(3);   // ...what each must fact is: a name is kept in every language, a word in English only

        public void Clear()
        {
            Who = Role = Mood = Style = Player = When = News = Write = null;
            Lang = "en";
            Traits.Clear();
            Facts.Clear();
            Memory.Clear();
            History.Clear();
            Must.Clear();
            MustKinds.Clear();
        }
    }

    /// <summary>
    /// The card and the prompt, byte for byte as the models were trained on them (common.py's SYSTEM, card() and chatml()).
    /// Game-free. COST: one string of a few hundred characters a request.
    /// </summary>
    public static class TextCard
    {
        /// <summary>The system turn every model was trained with (common.SYSTEM).</summary>
        public const string System = "You write one message for a character in the game Big Ambitions. Stay in character. Use only the facts "
                                     + "given and add no new events, money, times or promises. Plain British English, no dashes. One short message, "
                                     + "nothing else.";

        /// <summary>The system turn of data v6, where the card's lang line names the language.</summary>
        public const string SystemSix = "You write one message for a character in the game Big Ambitions. Stay in character. Use only the facts "
                                        + "given and add no new events, money, times or promises. Write in the language the lang line names; English "
                                        + "in plain British English. No dashes and no exclamation marks. One short message, nothing else.";

        public const string Stop = "<|im_end|>";   // where the model's reply ends (ChatML)

        public static string SystemOf(int data) => data >= TextIntents.DataV6 ? SystemSix : System;

        // The service's system turns of data v6, one a language in TextLanguages.All's order, made the first time each is asked.
        private static readonly string[] ServiceSix = new string[TextLanguages.All.Length];

        /// <summary>
        /// The system turn Your own AI is sent: the pack's, and in another language a sentence naming it as its natives write
        /// it, since a large model reads "lang: pt" as any Portuguese. COST: a string made once a language.
        /// </summary>
        public static string ServiceSystem(int data, string lang)
        {
            int at = Array.IndexOf(TextLanguages.All, lang);
            if (data < TextIntents.DataV6 || at <= 0)
            {
                return SystemOf(data);   // English, or a code that is none of the seven
            }
            return ServiceSix[at] ?? (ServiceSix[at] = SystemSix + " The lang line here is " + lang + ": write " + TextLanguages.Native(lang)
                                                      + " as a native speaker talks in a big city today, in everyday words, never like a translation. "
                                                      + TextLanguages.AddressOf(lang));
        }

        /// <summary>The user turn: one fact per line, in the trained order.</summary>
        public static string Card(TextSheet s)
        {
            var t = new StringBuilder(512);
            Append(s, t);
            return t.ToString();
        }

        /// <summary>The exact text the model reads: system, the card as the user's turn, and the assistant's turn opened.</summary>
        public static string ChatMl(TextSheet s) => ChatMl(s, System);

        public static string ChatMl(TextSheet s, string system)
        {
            var t = new StringBuilder(1024);
            t.Append("<|im_start|>system\n").Append(system).Append("<|im_end|>\n<|im_start|>user\n");
            Append(s, t);
            t.Append("<|im_end|>\n<|im_start|>assistant\n");
            return t.ToString();
        }

        private static void Append(TextSheet s, StringBuilder t)
        {
            int start = t.Length;
            Line(t, start, "who", s.Who);
            Line(t, start, "role", s.Role);
            List(t, start, "traits", s.Traits);
            Line(t, start, "mood", s.Mood);
            Line(t, start, "style", s.Style);
            Line(t, start, "player", s.Player);
            Line(t, start, "when", s.When);
            Line(t, start, "news", s.News);
            List(t, start, "facts", s.Facts);
            List(t, start, "memory", s.Memory);
            if (s.History.Count > 0)
            {
                Open(t, start);
                t.Append("history:");
                for (int i = 0; i < s.History.Count; i++)
                {
                    t.Append("\n  ").Append(s.History[i].Who).Append(": ").Append(s.History[i].Said);
                }
            }
            Line(t, start, "write", s.Write);
            List(t, start, "must", s.Must);
            Line(t, start, "lang", s.Lang);
        }

        private static void Open(StringBuilder t, int start)
        {
            if (t.Length > start)
            {
                t.Append('\n');
            }
        }

        private static void Line(StringBuilder t, int start, string key, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }
            Open(t, start);
            t.Append(key).Append(": ").Append(value);
        }

        private static void List(StringBuilder t, int start, string key, List<string> values)
        {
            if (values.Count == 0)
            {
                return;
            }
            Open(t, start);
            t.Append(key).Append(": ");
            for (int i = 0; i < values.Count; i++)
            {
                t.Append(i > 0 ? "; " : string.Empty).Append(values[i]);
            }
        }
    }
}
