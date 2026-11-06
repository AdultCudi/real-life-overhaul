using System;
using Extensions;
using RealLifeOverhaul.Economy;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The net worth card's live words: today's net worth as the Overview counts it (one read of the game's wealth, as the card is made)
    /// and the days on record in the save (WorthWatch), made into the card's model by UiNetWorthTip. Nothing is invented: with fewer than two
    /// days on record the card says its line starts later. Cost: a read and a model each time the pointer settles on the money.
    /// </summary>
    internal static class TooltipWorth
    {
        private static float[] _line = new float[NetWorthRing.Most + 1];

        /// <summary>The card for today, or false when the net worth cannot be read.</summary>
        internal static bool Make(UiTooltipModel into)
        {
            GameInstance save = SaveGameManager.Current;
            if (save == null || !WorthWatch.Now(out long now))
            {
                return false;
            }
            WorthBook book = WorthWatch.Book;
            int today = save.Day;
            WorthSummary s;
            if (book != null)
            {
                int need = WorthBook.LineLength(book.Player, today);
                if (_line.Length < need)
                {
                    _line = new float[need];
                }
                s = WorthBook.Summary(book.Player, today, now, _line);
            }
            else
            {
                s = new WorthSummary { Now = now, High = now, Low = now, HighDay = today, LowDay = today, FromDay = today, Points = 1 };
            }
            float[] line = s.Points >= 2 ? Fit(_line, s.Points) : null;
            UiTooltipModel made = UiNetWorthTip.Model(UiText.Words, s, today, line, Money, Short);
            into.Clear();
            into.Blocks.AddRange(made.Blocks);
            return true;
        }

        // The line's values as an array of exactly their count: the spark reads its whole array.
        private static float[] Fit(float[] values, int count)
        {
            var line = new float[count];
            Array.Copy(values, line, count);
            return line;
        }

        private static string Money(long amount) => ((double)amount).ToShortCurrencyFormat();

        private static string Short(long amount) => ((double)amount).ToShortCurrencyFormat(true);
    }
}
