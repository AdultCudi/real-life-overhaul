using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The small rules the sweep's screens share, apart from the game so EconomyCheck runs them: a desk session's choice says what
    /// it does in the panel's own sentence, which the decision card shows as plain lines, one a sentence, none coloured because the
    /// panel does not say which way a sentence goes for the player. Cost: a few string cuts when a page changes.
    /// </summary>
    public static class SweepRules
    {
        public const int MostLines = 3;

        /// <summary>
        /// A choice's trade as the card's lines: the sentences of the panel's words, without the choice's own name when the panel
        /// opens with it ("Lift morale: Burnout eases..."), each without its full stop, at most <see cref="MostLines"/>.
        /// </summary>
        public static UiConsequence[] Consequences(string trade, string name)
        {
            if (string.IsNullOrWhiteSpace(trade))
            {
                return new UiConsequence[0];
            }
            string text = trade.Trim();
            if (!string.IsNullOrEmpty(name) && text.StartsWith(name + ":", StringComparison.Ordinal))
            {
                text = text.Substring(name.Length + 1).TrimStart();
            }
            var lines = new List<UiConsequence>(MostLines);
            int from = 0;
            for (int i = 0; i < text.Length && lines.Count < MostLines; i++)
            {
                bool end = text[i] == '.' && (i + 1 == text.Length || text[i + 1] == ' ');
                if (end || i + 1 == text.Length)
                {
                    string sentence = text.Substring(from, i + 1 - from).Trim().TrimEnd('.').Trim();
                    from = i + 1;
                    if (sentence.Length > 0)
                    {
                        lines.Add(new UiConsequence { Text = sentence });
                    }
                }
            }
            return lines.ToArray();
        }
    }
}
