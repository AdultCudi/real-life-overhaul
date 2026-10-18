using System;

namespace RealLifeOverhaul.Ledger
{
    /// <summary>
    /// What a page of the City Ledger was last laid out for (LedgerPage.Turn): the paper, the language, the sheet's
    /// size and pixel, the face and the two prices it prints. A page turned to again for the same is left as it was
    /// laid out: laying out the front page measures its type up to forty times, some 20 to 150 ms. Game-free
    /// (EconomyCheck runs it).
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
