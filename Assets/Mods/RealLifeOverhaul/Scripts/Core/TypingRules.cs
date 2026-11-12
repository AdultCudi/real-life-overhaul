using System.Text.RegularExpressions;

namespace RealLifeOverhaul
{
    /// <summary>
    /// What a source file must do to keep the rule of <see cref="TypingNow"/>: a file that reads the keys a player types
    /// with (a letter, a digit, an arrow, or a key action of the game's that sits on one: Move, Interact, RotateLeft...)
    /// asks <c>TypingNow.Now</c>, and a text field the mod makes is put on the game's UI layer so the game holds its own
    /// shortcuts. Esc and Enter (the game's Cancel and Confirm) and a held Shift, Ctrl or Alt are not shortcuts and are left
    /// alone. Game-free; EconomyCheck's TypingChecks reads every source of the mod with it. COST: offline only.
    /// </summary>
    public static class TypingRules
    {
        // A key the engine is asked about by name, other than a modifier; the keyboard device itself; a key action of the game's
        // other than Cancel and Confirm.
        private static readonly Regex Readers = new Regex(
            @"\bInput\.GetKey(Down|Up)?\((?!KeyCode\.(Left|Right)(Shift|Control|Alt)\b)|\bKeyboard\.current\b|\bPlayerAction\.(?!Cancel\b|Confirm\b)\w+\.(Pressed|Released|Vector|IsPressed|Held)\(",
            RegexOptions.CultureInvariant);

        /// <summary>Files that name a key action of the game's and still act on no typed key, with why.</summary>
        public static readonly string[,] Exempt =
        {
            { "Photos/PhotosGame.cs", "only takes the turn keys away from the game while the phone shows a photo; it acts on none" },
        };

        /// <summary>The source reads keys a player could be typing (comments left out).</summary>
        public static bool ReadsKeys(string source)
        {
            foreach (string line in source.Split('\n'))
            {
                string trimmed = line.TrimStart();
                if (!trimmed.StartsWith("//") && Readers.IsMatch(trimmed))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>The source asks the one rule.</summary>
        public static bool Asks(string source)
        {
            return source.Contains("TypingNow.Now") || source.Contains("TypingNow.Selected(");
        }

        /// <summary>A file reads typed keys and never asks, and is not one that only takes keys away (<paramref name="relative"/>, with forward slashes).</summary>
        public static bool Unguarded(string relative, string source)
        {
            for (int i = 0; i < Exempt.GetLength(0); i++)
            {
                if (relative == Exempt[i, 0])
                {
                    return false;
                }
            }
            return ReadsKeys(source) && !Asks(source);
        }

        /// <summary>The source makes a text field and leaves it off the layer the game's shortcuts look at.</summary>
        public static bool FieldOffLayer(string source)
        {
            bool makes = source.Contains("AddComponent<TMP_InputField>") || source.Contains("AddComponent<InputField>");
            return makes && !source.Contains("TypingNow.OnGameLayer(") && !source.Contains("NameToLayer(\"UI\")");
        }
    }
}
