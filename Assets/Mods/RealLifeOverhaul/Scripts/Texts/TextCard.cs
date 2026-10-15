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

    /// <summary>
    /// The character sheet the text model reads: one fact per line, the same fields the models were trained on. Filled by
    /// the game's state (TextFaces for who is speaking, TextIntents for what happened), never by free text from a model.
    /// Game-free. Kept and cleared, not made anew, so a request costs its strings only.
    /// </summary>
    public sealed class TextSheet
    {
        public string Who, Role, Mood, Style, Player, When, News, Write;
        public string Lang = "en";
        public readonly List<string> Traits = new List<string>(3);
        public readonly List<string> Facts = new List<string>(4);
        public readonly List<TextSaid> History = new List<TextSaid>(3);
        public readonly List<string> Must = new List<string>(3);

        public void Clear()
        {
            Who = Role = Mood = Style = Player = When = News = Write = null;
            Lang = "en";
            Traits.Clear();
            Facts.Clear();
            History.Clear();
            Must.Clear();
        }
    }

    /// <summary>
    /// The card and the prompt, byte for byte as the models were trained on them (the training data's common.py: SYSTEM,
    /// card() and chatml()): the fields in a fixed order (who, role, traits, mood, style, player, when, news, facts,
    /// history, write, must, lang), empty ones left out, a list joined with "; ", the thread under "history:" one line each
    /// as "  Name: line", and the whole in ChatML. Proved identical to the Python for every voice and channel used
    /// (EconomyCheck, TextsChecks.cs, against cards made by the training generator itself). Game-free.
    /// COST: one string of a few hundred characters a request; nothing per frame.
    /// </summary>
    public static class TextCard
    {
        /// <summary>The system turn every model was trained with (common.SYSTEM).</summary>
        public const string System = "You write one message for a character in the game Big Ambitions. Stay in character. Use only the facts "
                                     + "given and add no new events, money, times or promises. Plain British English, no dashes. One short message, "
                                     + "nothing else.";

        public const string Stop = "<|im_end|>";   // where the model's reply ends (ChatML)

        /// <summary>The user turn: one fact per line, in the trained order.</summary>
        public static string Card(TextSheet s)
        {
            var t = new StringBuilder(512);
            Append(s, t);
            return t.ToString();
        }

        /// <summary>The exact text the model reads: system, the card as the user's turn, and the assistant's turn opened.</summary>
        public static string ChatMl(TextSheet s)
        {
            var t = new StringBuilder(1024);
            t.Append("<|im_start|>system\n").Append(System).Append("<|im_end|>\n<|im_start|>user\n");
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
