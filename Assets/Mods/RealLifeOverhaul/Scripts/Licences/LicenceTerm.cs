using System;

namespace RealLifeOverhaul
{
    /// <summary>
    /// How long a driving licence lasts: two of the save's own years, and never fewer than the 120 days it always was (two of
    /// the game's default 60-day years). Game-free: EconomyCheck runs it. No cost a frame: asked when a licence is issued,
    /// renewed or read for a date, never per frame.
    /// </summary>
    public static class LicenceTerm
    {
        public const int Years = 2;            // years a licence runs
        public const int FloorDays = 120;      // days, the term every licence had before it followed the year, and the least it can be

        /// <summary>The term in days for a save whose year is this long (the saved figure, read as CalendarRules does).</summary>
        public static int Days(int savedYear)
        {
            return Math.Max(FloorDays, Years * CalendarRules.Year(savedYear));
        }

        /// <summary>
        /// The last day a card is valid. A card stores the day it ran to when it was issued or renewed and the term it was given
        /// (0 for a card saved before terms were kept: the 120 days). A year lengthened since then adds the difference, so a
        /// licence already held runs the rest of its term as the longer one, and a year shortened never takes a day off.
        /// </summary>
        public static int End(int stored, int grantedTerm, int termNow)
        {
            int was = grantedTerm > 0 ? grantedTerm : FloorDays;
            return stored + Math.Max(0, termNow - was);
        }
    }
}
