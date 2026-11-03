using System;
using System.Globalization;

namespace RealLifeOverhaul.Ui
{
    /// <summary>How a scene's page stands on the kit: a moment's decision card, or a conversation docked at the right.</summary>
    public enum SceneLook : byte
    {
        Decision = 0,   // a hearing's verdict, a fame or office scene, an interview's last word: faces, the narrative, numbered choices
        Talk            // questions and answers (an interview's, a pitch's rounds): the docked conversation, its thread and its choices
    }

    /// <summary>
    /// The rules the conversations on the kit read the game by, game-free and checked offline (UiChecks.Dialog): which of
    /// the game's dialogs and forms stay the game's own (the casino's), what a form's pieces are by the names the game
    /// gives them (a splitter, a spacer, a total, a warning), the game's moods as glyphs, a scene list's lines split into
    /// what is said and the progress under it, and which look a scene's page takes. Cost: a few string comparisons.
    /// </summary>
    public static class DialogRules
    {
        /// <summary>The casino's games and their bets: the owner keeps them on the game's own panel.</summary>
        public static readonly string[] CasinoDialogs = { "BlackjackDialog", "RouletteDialog", "SlotMachineDialog" };

        public static readonly string[] CasinoForms = { "SlotMachine", "Roulette", "RouletteBetSettings", "Blackjack", "BlackjackBetSettings" };

        public const string SwatchKey = "dialog:swatch:";   // a colour's picture: the key and the colour in hex
        public const string PictureKey = "dialog:picture:"; // the spoken line's picture, as the game holds it
        public const int FigureMost = 28;                   // characters a read-only figure takes at the right before it reads as a note
        public const int BodyMost = 5;                      // lines a decision card's narrative holds; a longer one is a conversation
        public const int MostRows = 48;                     // rows a form is read into: more than any of the game's holds
        public const int MostLines = 30;                    // lines a thread keeps: the game's own panel keeps its last thirty

        /// <summary>True for a casino game's dialog, by its class's name.</summary>
        public static bool Casino(string dialogType) => dialogType != null && Array.IndexOf(CasinoDialogs, dialogType) >= 0;

        /// <summary>True for a casino game's form, by the game's name for it.</summary>
        public static bool CasinoForm(string template) => template != null && Array.IndexOf(CasinoForms, template) >= 0;

        /// <summary>The game's hairline between a form's parts: Splitter, Splitter (1), SplitterGray.</summary>
        public static bool Splitter(string name) => name != null && name.StartsWith("Splitter", StringComparison.Ordinal);

        /// <summary>An empty spacer the game lays between a form's lines: space, space (1).</summary>
        public static bool Spacer(string name) => name != null && (name == "space" || name.StartsWith("space (", StringComparison.Ordinal));

        /// <summary>A form's total, whose words stand out: TotalPrice, TotalCost, TotalDailyPayment, a campaign's PriceRow.</summary>
        public static bool Total(string name) =>
            name != null && (name.IndexOf("Total", StringComparison.Ordinal) >= 0 || name.StartsWith("PriceRow", StringComparison.Ordinal) || name.StartsWith("PricePerDay", StringComparison.Ordinal));

        /// <summary>A line of a form that warns (a design that needs mods the player lacks).</summary>
        public static bool Warning(string name) => name != null && name.IndexOf("Warning", StringComparison.Ordinal) >= 0;

        /// <summary>The game's buttons of a step that are a figure's steps, not answers.</summary>
        public static bool Less(string name) => name == "DecreaseButton";

        public static bool More(string name) => name == "IncreaseButton";

        /// <summary>A figure too long to stand at the right of its label (a sentence): it reads as a heading and a note.</summary>
        public static bool LongFigure(string figure) => figure != null && figure.Length > FigureMost;

        /// <summary>What the bank's investment form's confirm says under it, by its funds, the cash, the sum typed and the bank's least.</summary>
        public enum InvestNote : byte
        {
            NoFund,   // the bank offers no fund here: nothing to choose
            Short,    // the cash is under the bank's least: the form keeps each sum to the cash, so no sum the bank takes can be typed
            Range,    // no sum typed yet: the least and the most, the cash
            Least,    // a sum under the bank's least
            Takes     // a sum the bank takes: it leaves the account today
        }

        /// <summary>
        /// The investment form's note (BankInvestmentSettings, BankDialog.InvestmentRequest): the game keeps each sum typed to
        /// the player's cash and refuses one under its least only after Confirm, so the confirm says which it is before.
        /// </summary>
        public static InvestNote Invest(int funds, float cash, float sum, float least)
        {
            if (funds <= 0)
            {
                return InvestNote.NoFund;
            }
            if (cash < least)
            {
                return InvestNote.Short;
            }
            if (!(sum > 0f))
            {
                return InvestNote.Range;
            }
            return sum < least ? InvestNote.Least : InvestNote.Takes;
        }

        /// <summary>A label's words on one line: the game's line breaks (a campaign's kind over its price) as a middle dot.</summary>
        public static string OneLine(string words)
        {
            string plain = ContactsRules.Plain(words);
            if (string.IsNullOrEmpty(plain) || plain.IndexOf('\n') < 0)
            {
                return plain;
            }
            string[] parts = plain.Split('\n');
            string line = string.Empty;
            for (int i = 0; i < parts.Length; i++)
            {
                string p = parts[i].Trim();
                if (p.Length > 0)
                {
                    line = line.Length == 0 ? p : line + " · " + p;
                }
            }
            return line;
        }

        /// <summary>The glyph by a line for the mood the game shows there: content, unsure, angry; none for none.</summary>
        public static UiIcon MoodIcon(int mood) => mood > 0 ? UiIcon.SmileyHappy : mood == 0 ? UiIcon.SmileyMeh : mood == -1 ? UiIcon.SmileySad : UiIcon.None;

        /// <summary>The key of the word for that mood beside the speaker's face (Friendly); null where the game shows none.</summary>
        public static string MoodWord(int mood) =>
            mood > 0 ? "reallife_ui_dialog_mood_friendly" : mood == 0 ? "reallife_ui_dialog_mood_unsure" : mood == -1 ? "reallife_ui_dialog_mood_upset" : null;

        /// <summary>A colour's picture key, made once a colour by the screen that shows it.</summary>
        public static string Swatch(byte r, byte g, byte b) =>
            SwatchKey + r.ToString("X2", CultureInfo.InvariantCulture) + g.ToString("X2", CultureInfo.InvariantCulture) + b.ToString("X2", CultureInfo.InvariantCulture);

        /// <summary>The colour in a swatch's key; false for any other key.</summary>
        public static bool SwatchColour(string key, out byte r, out byte g, out byte b)
        {
            r = g = b = 0;
            if (key == null || key.Length != SwatchKey.Length + 6 || !key.StartsWith(SwatchKey, StringComparison.Ordinal))
            {
                return false;
            }
            return byte.TryParse(key.Substring(SwatchKey.Length, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out r)
                   && byte.TryParse(key.Substring(SwatchKey.Length + 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out g)
                   && byte.TryParse(key.Substring(SwatchKey.Length + 4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out b);
        }

        /// <summary>
        /// A scene list's lines under its headline as the kit shows them: what is said (the answers heard, the question),
        /// and the progress the list writes small at its foot (the question it is on, in its smaller size), each as plain words.
        /// </summary>
        public static void SplitInfo(string info, out string said, out string progress)
        {
            said = null;
            progress = null;
            if (string.IsNullOrEmpty(info))
            {
                return;
            }
            int small = info.IndexOf("<size=", StringComparison.OrdinalIgnoreCase);
            if (small >= 0)
            {
                int close = info.IndexOf("</size>", small, StringComparison.OrdinalIgnoreCase);
                string inner = close > small ? info.Substring(small, close - small) : info.Substring(small);
                progress = Blank(ContactsRules.Plain(inner));
                info = info.Substring(0, small) + (close > small ? info.Substring(close + 7) : string.Empty);
            }
            said = Blank(ContactsRules.Plain(info));
        }

        /// <summary>
        /// The look a scene's page takes: a conversation where the scene asks for one (questions and answers) or where what
        /// is said is longer than a decision card's narrative holds, else the decision card.
        /// </summary>
        public static SceneLook Look(bool talk, int bodyLines) => talk || bodyLines > BodyMost ? SceneLook.Talk : SceneLook.Decision;

        /// <summary>
        /// The name a dialog gives its speaker (the game's npcNameKey: the bank's Banker, the mod's Business desk, a person
        /// the player knows by name), as the head shows it: its words where it is a key the game or the mod translates, the
        /// name itself where it is one, and null for a record's id that is neither, so no id reaches the screen.
        /// </summary>
        public static string SpeakerName(string key, string translated)
        {
            if (!string.IsNullOrEmpty(translated) && translated.Trim().Length > 0)
            {
                return translated.Trim();
            }
            if (string.IsNullOrEmpty(key) || key.Trim().Length == 0 || key.StartsWith("ba:", StringComparison.Ordinal) || key.StartsWith("reallife", StringComparison.Ordinal)
                || key.IndexOf('_') >= 0 || key.IndexOf(':') >= 0)
            {
                return null;
            }
            return key.Trim();
        }

        /// <summary>
        /// The answers' keys, as the conversation and the decision card show them: F on the first answer that can be taken,
        /// P on the next one, Escape on the way out (<paramref name="leave"/>, -1 for none). Returns the answer F takes, the
        /// one the screen leads with and shows chosen; -1 where none can be taken. The second's index in <paramref name="second"/>.
        /// </summary>
        public static int Keys(UiDecisionChoice[] choices, int leave, out int second)
        {
            int main = -1;
            second = -1;
            for (int i = 0; choices != null && i < choices.Length; i++)
            {
                choices[i].Cap = i == leave ? UiChoiceCap.Leave : UiChoiceCap.None;
                if (i == leave || choices[i].Disabled)
                {
                    continue;
                }
                if (main < 0)
                {
                    main = i;
                    choices[i].Cap = UiChoiceCap.Main;
                }
                else if (second < 0)
                {
                    second = i;
                    choices[i].Cap = UiChoiceCap.Second;
                }
            }
            return main;
        }

        /// <summary>
        /// A line of a file ("Owed: $1,850 in all.") as a statement's label and figure, split at its first colon (a full-width
        /// one in Chinese), the full stop at its end taken off; false for a line with no colon, or one whose figure is a
        /// sentence (it then stays words).
        /// </summary>
        public static bool Pair(string line, out string label, out string value)
        {
            label = value = null;
            string t = Blank(line);
            if (t == null)
            {
                return false;
            }
            int at = t.IndexOf(": ", StringComparison.Ordinal);
            int wide = t.IndexOf('：');
            int cut = at >= 0 && (wide < 0 || at < wide) ? at : wide;
            if (cut <= 0)
            {
                return false;
            }
            label = Blank(t.Substring(0, cut));
            value = Blank(t.Substring(cut + (cut == at ? 2 : 1)).TrimEnd('.', '。'));
            return label != null && value != null && value.Length <= FigureMost * 2;
        }

        private static string Blank(string text)
        {
            string t = text != null ? text.Trim() : null;
            return string.IsNullOrEmpty(t) ? null : t;
        }
    }
}
