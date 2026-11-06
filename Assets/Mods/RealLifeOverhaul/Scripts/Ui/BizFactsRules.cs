using System.Text;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// What a business's or a building's card says besides what the game's box wrote, game-free and checked offline: when the game's
    /// BizMan shows a rival's business its valuation, which of the game's figures count as a figure, and a card's lines put together
    /// without a gap. Cost: a few comparisons, at a layout only.
    /// </summary>
    public static class BizFactsRules
    {
        /// <summary>
        /// True where the game's BizMan sets a business its estimated valuation (BizManPresentation.SetAiOwned): a building that can be
        /// taken over, with no special service in it, that is not a warehouse, is not for rent and is not the player's own lease.
        /// </summary>
        public static bool ShowsValuation(bool special, bool canBeTakenOver, bool warehouse, bool forRent, bool rentedByPlayer) =>
            !special && canBeTakenOver && !warehouse && !forRent && !rentedByPlayer;

        /// <summary>The game writes a dash for a market value under nothing (below zero) and a figure for any other.</summary>
        public static bool IsFigure(float value) => value >= 0f && !float.IsNaN(value) && !float.IsInfinity(value);

        /// <summary>A valuation is a figure to show above nothing: the game's own answer for a building it does not value is zero.</summary>
        public static bool IsValuation(float value) => value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);

        /// <summary>The lines that have words, one under another.</summary>
        public static string Lines(params string[] lines)
        {
            StringBuilder b = null;
            for (int i = 0; lines != null && i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                {
                    continue;
                }
                b = b == null ? new StringBuilder(lines[i].Trim()) : b.Append('\n').Append(lines[i].Trim());
            }
            return b != null ? b.ToString() : null;
        }

        /// <summary>The pieces that have words, with a label and its figure joined as the game's rows are ("Market value: $1.2M").</summary>
        public static string Pair(string label, string value) => string.IsNullOrWhiteSpace(label) || string.IsNullOrWhiteSpace(value) ? null : label.Trim().TrimEnd(':') + ": " + value.Trim();

        /// <summary>The pieces that have words, joined by a comma.</summary>
        public static string Commas(params string[] pieces)
        {
            StringBuilder b = null;
            for (int i = 0; pieces != null && i < pieces.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(pieces[i]))
                {
                    continue;
                }
                b = b == null ? new StringBuilder(pieces[i].Trim()) : b.Append(", ").Append(pieces[i].Trim());
            }
            return b != null ? b.ToString() : null;
        }

        /// <summary>The number of lines in a text (a text of none has none).</summary>
        public static int LineCount(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }
            int n = 1;
            for (int i = 0; i < text.Length; i++)
            {
                n += text[i] == '\n' ? 1 : 0;
            }
            return n;
        }
    }
}
