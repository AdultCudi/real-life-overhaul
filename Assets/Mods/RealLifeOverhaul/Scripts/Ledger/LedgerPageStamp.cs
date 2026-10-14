using System;

namespace RealLifeOverhaul.Ledger
{
    /// <summary>
    /// What a page of the City Ledger was last laid out for (LedgerPage.Turn): the paper (the edition, or a special front
    /// page), the language, the sheet's size and one screen pixel in its units, the face it is set in, and the two prices it
    /// prints (a copy's, a week's). The page's texts, rules and chart are objects that stay as they were written, so a page
    /// turned to again (a tab, the paper opened again) for the same of all of these is the page as it was laid out: it is
    /// not laid out again. Laying out the front page measures its type up to forty times, an estimated 20 to 150 ms a turn;
    /// the paper changes once a morning.
    ///
    /// Game-free: the offline checks run it as it is (EconomyCheck, AppsChecks.cs).
    /// </summary>
    public struct LedgerPageStamp
    {
        public object Paper;      // the edition or the special front page, by reference: a new one is a new object
        public string Language;   // the game's language (LocalizorManager.LoadedLocale)
        public float Width, Height, Pixel;
        public object Face;       // the font asset the paper is set in (null: the game's own)
        public int Copy, Week;    // the prices printed in the dateline and the house advert
        public bool Laid;         // a page has been laid out for it

        /// <summary>Whether a page laid out for 'then' is the page this would lay out: everything it is made from is the same.</summary>
        public bool Same(in LedgerPageStamp then)
        {
            return Laid && then.Laid && ReferenceEquals(Paper, then.Paper) && string.Equals(Language, then.Language, StringComparison.Ordinal)
                   && Width == then.Width && Height == then.Height && Pixel == then.Pixel && ReferenceEquals(Face, then.Face) && Copy == then.Copy && Week == then.Week;
        }
    }
}
